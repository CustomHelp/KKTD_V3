using System;
using System.Threading.Tasks;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Camera;

/// <summary>
/// IDS Peak SDK Kamera-Service (Online).
/// Stub – Initialisierung des IDS Peak SDK erfolgt in der Implementierungs-Phase
/// (peak.Library.Initialize, DeviceManager, Buffer-Pool, Acquisition-Start).
/// </summary>
public sealed class CameraService : ICameraService
{
    private readonly AppSettings _settings;
    private readonly ILogger<CameraService> _logger;
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
        // TODO: peak.Library.Initialize();
        // TODO: DeviceManager.Update(); Device aus Liste öffnen
        // TODO: Pixelformat auf Mono8, ExposureTime, Gain, Gamma, BandwidthLimit setzen
        // TODO: BufferPool anlegen (zero-copy / pinned), Trigger-Quelle konfigurieren
        _logger.LogInformation("CameraService.ConnectAsync — IDS Peak SDK Initialisierung TODO.");
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        // TODO: peak.Library.Close();
        return Task.CompletedTask;
    }

    public Task StartAsync()
    {
        IsRunning = true;
        // TODO: DataStream starten, Acquisition starten, Frame-Callback verdrahten
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsRunning = false;
        return Task.CompletedTask;
    }

    public Task<byte[]?> CaptureSingleAsync()
    {
        // TODO: Software-Trigger auslösen, einen Buffer abholen
        return Task.FromResult<byte[]?>(null);
    }

    /// <summary>Wird vom IDS-Peak-Buffer-Callback aufgerufen.</summary>
    private void RaiseFrame(byte[] pixels, int width, int height)
    {
        var args = new FrameEventArgs(pixels, width, height, System.Threading.Interlocked.Increment(ref _frameCounter));
        FrameReceived?.Invoke(this, args);
    }

    public void Dispose()
    {
        try { DisconnectAsync().GetAwaiter().GetResult(); } catch { /* ignore */ }
    }
}
