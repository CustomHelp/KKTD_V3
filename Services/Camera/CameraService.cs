using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using IDSImaging.Peak.API;
using IDSImaging.Peak.API.Core;
using IDSImaging.Peak.API.Core.Nodes;
using PeakBuffer = IDSImaging.Peak.API.Core.Buffer;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Camera;

/// <summary>
/// IDS Peak SDK Kamera-Service (Online).
/// Initialisiert das Peak-Library, öffnet die erste verfügbare GigE-Kamera,
/// stellt Mono8 + Framerate + Belichtung/Gain/Gamma/Bandbreite aus AppSettings ein
/// und liefert Mono8-Frames als byte[] über das FrameReceived-Event.
/// Tritt bei Connect oder im Acquisition-Loop ein Fehler auf, wird er geloggt;
/// die Anwendung bleibt lauffähig (z. B. wenn die Kamera nicht angeschlossen ist).
/// </summary>
public sealed class CameraService : ICameraService
{
    private readonly AppSettings _settings;
    private readonly ILogger<CameraService> _logger;
    private readonly object _gate = new();

    private bool _libraryInitialized;
    private Device? _device;
    private NodeMap? _remoteNodeMap;
    private DataStream? _dataStream;

    private CancellationTokenSource? _cts;
    private Task? _acquisitionTask;
    private long _frameCounter;

    public CameraService(AppSettings settings, ILogger<CameraService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsRunning { get; private set; }

    public event EventHandler<FrameEventArgs>? FrameReceived;

    public Task ConnectAsync()
    {
        lock (_gate)
        {
            if (_device is not null) return Task.CompletedTask;

            try
            {
                if (!_libraryInitialized)
                {
                    Library.Initialize();
                    _libraryInitialized = true;
                    _logger.LogInformation("IDS Peak Library {Version} initialisiert.", Library.Version());
                }

                var deviceManager = DeviceManager.Instance();
                deviceManager.Update(
                    DeviceManager.UpdatePolicy.ScanEnvironmentForProducerLibraries,
                    msg => _logger.LogWarning("DeviceManager.Update: {Message}", msg));

                var devices = deviceManager.Devices();
                if (devices.Count == 0)
                {
                    _logger.LogWarning("Keine IDS-Kamera gefunden — Anwendung läuft ohne Kamera weiter.");
                    return Task.CompletedTask;
                }

                DeviceDescriptor? descriptor = null;
                foreach (var d in devices)
                {
                    if (d.IsOpenable(DeviceAccessType.Control))
                    {
                        descriptor = d;
                        break;
                    }
                }
                if (descriptor is null)
                {
                    _logger.LogWarning(
                        "Keine öffenbare Kamera (Control-Zugriff) gefunden — evtl. von anderer Anwendung belegt.");
                    return Task.CompletedTask;
                }

                _device = descriptor.OpenDevice(DeviceAccessType.Control);
                _logger.LogInformation("Kamera geöffnet: {Display} (S/N {Serial}, TL {TL}).",
                    descriptor.DisplayName(), descriptor.SerialNumber(), descriptor.TLType());

                _remoteNodeMap = _device.RemoteDevice().NodeMaps()[0];
                ApplyCameraSettings(_remoteNodeMap);

                _dataStream = _device.DataStreams()[0].OpenDataStream();
                AllocateBuffers(_dataStream, _remoteNodeMap);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "IDS Peak Connect fehlgeschlagen — Anwendung läuft ohne Kamera weiter.");
                SafeCleanup();
            }
        }

        return Task.CompletedTask;
    }

    private void ApplyCameraSettings(NodeMap nm)
    {
        TryLoadDefaultUserSet(nm);

        TrySetEnumerationEntry(nm, "PixelFormat", _settings.Camera.PixelFormat);

        // Belichtung: AppSettings ist Millisekunden, IDS-Node erwartet Mikrosekunden.
        TrySetExposure(nm, _settings.Camera.ExposureTimeMs * 1000.0, _settings.Camera.AutoExposure);

        // Frame-Rate (kontinuierliche Erfassung).
        TrySetEnumerationEntry(nm, "AcquisitionMode", "Continuous");
        TrySetEnumerationEntry(nm, "TriggerMode", "Off");
        TrySetBoolean(nm, "AcquisitionFrameRateEnable", true);
        TrySetFloat(nm, "AcquisitionFrameRate", _settings.Camera.Framerate);

        // Gain (analog + digital, getrennt über GainSelector).
        TrySetGain(nm, "AnalogAll", _settings.Camera.AnalogGain, _settings.Camera.AutoGain);
        TrySetGain(nm, "DigitalAll", _settings.Camera.DigitalGain, autoGain: false);

        // Gamma (LUT).
        TrySetFloat(nm, "Gamma", _settings.Camera.Gamma);

        // Bandbreitenbegrenzung (GigE).
        TrySetEnumerationEntry(nm, "DeviceLinkThroughputLimitMode", "On");
        TrySetInteger(nm, "DeviceLinkThroughputLimit", _settings.Camera.BandwidthLimitBytesPerSec);
    }

    private void AllocateBuffers(DataStream stream, NodeMap nm)
    {
        long payloadSize = nm.FindNode<IntegerNode>("PayloadSize").Value();
        uint minBuffers = stream.NumBuffersAnnouncedMinRequired();
        uint allocate = Math.Max(minBuffers, 4u);

        for (uint i = 0; i < allocate; i++)
        {
            var buf = stream.AllocAndAnnounceBuffer((uint)payloadSize, IntPtr.Zero);
            stream.QueueBuffer(buf);
        }
        _logger.LogInformation("Buffer-Pool: {Count} × {Size} Byte angelegt.", allocate, payloadSize);
    }

    public Task StartAsync()
    {
        lock (_gate)
        {
            if (IsRunning) return Task.CompletedTask;
            if (_device is null || _dataStream is null || _remoteNodeMap is null)
            {
                _logger.LogWarning("StartAsync ohne aktive Kameraverbindung — Acquisition wird übersprungen.");
                return Task.CompletedTask;
            }

            try
            {
                _remoteNodeMap.FindNode<IntegerNode>("TLParamsLocked").SetValue(1);
                _dataStream.StartAcquisition();
                var startCmd = _remoteNodeMap.FindNode<CommandNode>("AcquisitionStart");
                startCmd.Execute();
                startCmd.WaitUntilDone();

                _cts = new CancellationTokenSource();
                _acquisitionTask = Task.Run(() => AcquisitionLoop(_cts.Token));
                IsRunning = true;
                _logger.LogInformation("Acquisition gestartet (Ziel-FPS {Fps}).",
                    _settings.Camera.Framerate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Acquisition-Start fehlgeschlagen.");
                IsRunning = false;
            }
        }

        return Task.CompletedTask;
    }

    private void AcquisitionLoop(CancellationToken token)
    {
        var stream = _dataStream!;
        const int waitTimeoutMs = 1000;

        while (!token.IsCancellationRequested)
        {
            PeakBuffer? buffer = null;
            try
            {
                buffer = stream.WaitForFinishedBuffer(waitTimeoutMs);
                if (buffer is null) continue;

                if (buffer.IsIncomplete())
                {
                    _logger.LogDebug("Frame {Id} unvollständig — verworfen.", buffer.FrameID());
                }
                else
                {
                    var pixels = CopyMono8Pixels(buffer);
                    if (pixels is not null)
                    {
                        var args = new FrameEventArgs(
                            pixels,
                            (int)buffer.Width(),
                            (int)buffer.Height(),
                            Interlocked.Increment(ref _frameCounter));
                        FrameReceived?.Invoke(this, args);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) break;
                _logger.LogWarning(ex, "Frame-Empfang gestört.");
            }
            finally
            {
                if (buffer is not null)
                {
                    try { stream.QueueBuffer(buffer); }
                    catch (Exception ex) { _logger.LogWarning(ex, "QueueBuffer fehlgeschlagen."); }
                }
            }
        }
    }

    private static byte[]? CopyMono8Pixels(PeakBuffer buffer)
    {
        IntPtr basePtr = buffer.BasePtr();
        if (basePtr == IntPtr.Zero) return null;

        int width = (int)buffer.Width();
        int height = (int)buffer.Height();
        if (width <= 0 || height <= 0) return null;

        IntPtr imagePtr = IntPtr.Add(basePtr, (int)buffer.ImageOffset());
        int xPadding = (int)buffer.XPadding();
        int stride = width + xPadding;
        var pixels = new byte[width * height];

        if (xPadding == 0)
        {
            Marshal.Copy(imagePtr, pixels, 0, width * height);
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(IntPtr.Add(imagePtr, y * stride), pixels, y * width, width);
            }
        }
        return pixels;
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (!IsRunning) return Task.CompletedTask;
            IsRunning = false;

            try { _cts?.Cancel(); } catch { /* ignore */ }

            try { _dataStream?.KillWait(); } catch { /* ignore */ }

            try { _acquisitionTask?.Wait(2000); } catch { /* ignore */ }

            try
            {
                if (_remoteNodeMap is not null)
                {
                    var stopCmd = _remoteNodeMap.FindNode<CommandNode>("AcquisitionStop");
                    stopCmd.Execute();
                    stopCmd.WaitUntilDone();
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "AcquisitionStop fehlgeschlagen."); }

            try { _dataStream?.StopAcquisition(AcquisitionStopMode.Default); }
            catch (Exception ex) { _logger.LogWarning(ex, "DataStream.StopAcquisition fehlgeschlagen."); }

            try
            {
                _remoteNodeMap?.FindNode<IntegerNode>("TLParamsLocked").SetValue(0);
            }
            catch { /* ignore */ }

            try { _dataStream?.Flush(DataStreamFlushMode.DiscardAll); } catch { /* ignore */ }

            try { _cts?.Dispose(); } catch { /* ignore */ }
            _cts = null;
            _acquisitionTask = null;
            _logger.LogInformation("Acquisition gestoppt.");
        }
        return Task.CompletedTask;
    }

    public async Task<byte[]?> CaptureSingleAsync()
    {
        if (_dataStream is null) return null;
        if (!IsRunning) await StartAsync();

        try
        {
            using var buffer = _dataStream.WaitForFinishedBuffer(2000);
            if (buffer is null || buffer.IsIncomplete()) return null;
            var pixels = CopyMono8Pixels(buffer);
            _dataStream.QueueBuffer(buffer);
            return pixels;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CaptureSingleAsync fehlgeschlagen.");
            return null;
        }
    }

    public async Task DisconnectAsync()
    {
        await StopAsync();

        lock (_gate)
        {
            SafeCleanup();
        }
    }

    private void SafeCleanup()
    {
        if (_dataStream is not null)
        {
            try
            {
                foreach (var b in _dataStream.AnnouncedBuffers())
                {
                    try { _dataStream.RevokeBuffer(b); } catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }

            try { _dataStream.Dispose(); } catch { /* ignore */ }
            _dataStream = null;
        }

        if (_device is not null)
        {
            try { _device.Dispose(); } catch { /* ignore */ }
            _device = null;
        }
        _remoteNodeMap = null;
    }

    public void Dispose()
    {
        try { DisconnectAsync().GetAwaiter().GetResult(); } catch { /* ignore */ }

        if (_libraryInitialized)
        {
            try { Library.Close(); } catch { /* ignore */ }
            _libraryInitialized = false;
        }
    }

    // ---- kleine Robust-Setter -----------------------------------------------

    private void TryLoadDefaultUserSet(NodeMap nm)
    {
        try
        {
            var sel = nm.FindNode<EnumerationNode>("UserSetSelector");
            sel.SetCurrentEntry("Default");
            var load = nm.FindNode<CommandNode>("UserSetLoad");
            load.Execute();
            load.WaitUntilDone();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UserSet 'Default' nicht ladbar — überspringe.");
        }
    }

    private void TrySetEnumerationEntry(NodeMap nm, string node, string entry)
    {
        try { nm.FindNode<EnumerationNode>(node).SetCurrentEntry(entry); }
        catch (Exception ex) { _logger.LogWarning(ex, "Setze {Node}={Entry} fehlgeschlagen.", node, entry); }
    }

    private void TrySetBoolean(NodeMap nm, string node, bool value)
    {
        try { nm.FindNode<BooleanNode>(node).SetValue(value); }
        catch (Exception ex) { _logger.LogDebug(ex, "Setze {Node}={Value} fehlgeschlagen.", node, value); }
    }

    private void TrySetFloat(NodeMap nm, string node, double value)
    {
        try
        {
            var n = nm.FindNode<FloatNode>(node);
            double clamped = Math.Min(Math.Max(value, n.Minimum()), n.Maximum());
            n.SetValue(clamped);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Setze {Node}={Value} fehlgeschlagen.", node, value); }
    }

    private void TrySetInteger(NodeMap nm, string node, long value)
    {
        try
        {
            var n = nm.FindNode<IntegerNode>(node);
            long clamped = Math.Min(Math.Max(value, n.Minimum()), n.Maximum());
            n.SetValue(clamped);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Setze {Node}={Value} fehlgeschlagen.", node, value); }
    }

    private void TrySetExposure(NodeMap nm, double exposureUs, bool autoExposure)
    {
        try
        {
            nm.FindNode<EnumerationNode>("ExposureAuto")
                .SetCurrentEntry(autoExposure ? "Continuous" : "Off");
        }
        catch (Exception ex) { _logger.LogDebug(ex, "ExposureAuto nicht setzbar."); }

        if (!autoExposure) TrySetFloat(nm, "ExposureTime", exposureUs);
    }

    private void TrySetGain(NodeMap nm, string selector, double value, bool autoGain)
    {
        try { nm.FindNode<EnumerationNode>("GainSelector").SetCurrentEntry(selector); }
        catch (Exception ex) { _logger.LogDebug(ex, "GainSelector={Sel} nicht setzbar.", selector); return; }

        try
        {
            nm.FindNode<EnumerationNode>("GainAuto")
                .SetCurrentEntry(autoGain ? "Continuous" : "Off");
        }
        catch { /* manche Modelle haben GainAuto nur am Selector AnalogAll */ }

        if (!autoGain) TrySetFloat(nm, "Gain", value);
    }
}
