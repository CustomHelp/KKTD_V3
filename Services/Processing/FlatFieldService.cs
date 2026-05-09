using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Processing;

/// <summary>
/// Flat-Field-Korrektur:
///   1. Referenzbild per Mehrfach-Mittelung beim Start aufnehmen (leeres Band).
///   2. Mittelwert-normalisiertes 32F-Referenzbild im VRAM behalten
///      (Werte ≈ 1.0; hellere Bereiche &gt;1, dunklere &lt;1).
///   3. Pro Frame:  corrected = input ÷ reference  →  Beleuchtungsschatten
///      werden weggeteilt, mittlere Bildhelligkeit bleibt erhalten.
/// </summary>
public sealed class FlatFieldService : IDisposable
{
    private readonly ILogger<FlatFieldService> _logger;

    private GpuMat? _referenceGpu;     // 32F, mittelwert-normalisiert
    private int _refWidth;
    private int _refHeight;

    // Capture-Zustand
    private TaskCompletionSource<bool>? _captureTcs;
    private float[]? _accumulator;
    private int _samplesNeeded;
    private int _samplesCollected;
    private int _captureWidth;
    private int _captureHeight;
    private readonly object _captureGate = new();

    public FlatFieldService(ILogger<FlatFieldService> logger)
    {
        _logger = logger;
    }

    public bool HasReference => _referenceGpu is not null;
    public bool IsCapturing => _captureTcs is not null;

    // ---- Capture --------------------------------------------------------

    /// <summary>
    /// Initialisiert eine Capture-Session über <paramref name="sampleCount"/> Frames.
    /// Der Aufrufer routet anschließend eingehende Frames per
    /// <see cref="FeedSampleFrame"/>; der zurückgegebene Task wird abgeschlossen,
    /// sobald genügend Frames gesammelt und das Referenzbild im VRAM ist.
    /// </summary>
    public Task<bool> BeginCaptureAsync(int sampleCount = 4, CancellationToken ct = default)
    {
        if (sampleCount < 1) sampleCount = 1;

        lock (_captureGate)
        {
            CancelCaptureLocked();
            _samplesNeeded = sampleCount;
            _samplesCollected = 0;
            _accumulator = null;
            _captureWidth = 0;
            _captureHeight = 0;
            _captureTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                lock (_captureGate)
                {
                    if (_captureTcs is null) return;
                    _captureTcs.TrySetResult(false);
                    _captureTcs = null;
                    _accumulator = null;
                }
            });
        }

        _logger.LogInformation("Flat-Field-Aufnahme gestartet ({Count} Frames Mittelwert).", sampleCount);
        return _captureTcs!.Task;
    }

    /// <summary>
    /// Übergibt einen Frame an die laufende Capture-Session.
    /// Wird nach genügend Samples die Referenz finalisiert (Mittelwert + Normalisierung + Upload),
    /// wird der von <see cref="BeginCaptureAsync"/> zurückgegebene Task abgeschlossen.
    /// Außerhalb einer Session ist der Aufruf ein No-Op.
    /// </summary>
    public void FeedSampleFrame(byte[] mono8, int width, int height)
    {
        TaskCompletionSource<bool>? finishedTcs = null;
        bool finishedOk = false;

        lock (_captureGate)
        {
            if (_captureTcs is null) return;

            if (_accumulator is null)
            {
                _accumulator = new float[width * height];
                _captureWidth = width;
                _captureHeight = height;
            }
            else if (width != _captureWidth || height != _captureHeight)
            {
                _logger.LogWarning("Frame-Größe wechselte während Flat-Field-Capture — Aufnahme verworfen.");
                CancelCaptureLocked();
                return;
            }

            for (int i = 0; i < _accumulator.Length; i++)
                _accumulator[i] += mono8[i];
            _samplesCollected++;

            if (_samplesCollected >= _samplesNeeded)
            {
                try
                {
                    FinalizeReferenceLocked();
                    finishedOk = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Flat-Field-Finalisierung fehlgeschlagen.");
                    finishedOk = false;
                }

                finishedTcs = _captureTcs;
                _captureTcs = null;
                _accumulator = null;
            }
        }

        finishedTcs?.TrySetResult(finishedOk);
    }

    private void FinalizeReferenceLocked()
    {
        var acc = _accumulator!;
        int w = _captureWidth;
        int h = _captureHeight;

        // Sample-Mittelung
        double inv = 1.0 / _samplesCollected;
        double sum = 0;
        for (int i = 0; i < acc.Length; i++)
        {
            float avg = (float)(acc[i] * inv);
            acc[i] = avg;
            sum += avg;
        }
        double mean = sum / acc.Length;
        if (mean < 1.0)
        {
            throw new InvalidOperationException(
                $"Referenzbild zu dunkel (Mittelwert {mean:F1}/255) — Beleuchtung prüfen.");
        }

        // Pixel ÷ Mittelwert  →  Werte um 1.0 herum
        float meanF = (float)mean;
        for (int i = 0; i < acc.Length; i++)
            acc[i] /= meanF;

        // CPU-Mat → GPU-Upload
        using var cpuFloat = new Mat(h, w, DepthType.Cv32F, 1);
        Marshal.Copy(acc, 0, cpuFloat.DataPointer, acc.Length);

        DisposeReference();
        _referenceGpu = new GpuMat();
        _referenceGpu.Upload(cpuFloat);
        _refWidth = w;
        _refHeight = h;

        _logger.LogInformation(
            "Flat-Field Referenz gespeichert ({W}×{H}, Bandhelligkeit {Mean:F1}/255).",
            w, h, mean);
    }

    private void CancelCaptureLocked()
    {
        if (_captureTcs is null) return;
        _captureTcs.TrySetResult(false);
        _captureTcs = null;
        _accumulator = null;
    }

    public void CancelCapture()
    {
        lock (_captureGate) CancelCaptureLocked();
    }

    public void ClearReference()
    {
        DisposeReference();
        _logger.LogInformation("Flat-Field Referenz gelöscht.");
    }

    // ---- Datei-IO -------------------------------------------------------

    public void SaveReferenceToFile(string path)
    {
        if (_referenceGpu is null) return;
        using var cpuFloat = new Mat();
        _referenceGpu.Download(cpuFloat);
        using var asByte = new Mat();
        // Speichern als 8U-Bild (× 128, da Werte um 1.0 herum liegen) — nur zur visuellen Inspektion.
        cpuFloat.ConvertTo(asByte, DepthType.Cv8U, 128.0);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        CvInvoke.Imwrite(path, asByte);
    }

    // ---- Apply (GPU) ----------------------------------------------------

    /// <summary>
    /// Wendet die Flat-Field-Korrektur auf der GPU an.
    /// Erwartet <paramref name="inputMono8"/> als 8U-1-Kanal-GpuMat in Sensor-Auflösung.
    /// </summary>
    public void Apply(GpuMat inputMono8, GpuMat output)
    {
        if (_referenceGpu is null)
        {
            inputMono8.CopyTo(output);
            return;
        }

        // 8U → 32F (kein Skalieren — Pixel bleiben 0..255)
        using var inputFloat = new GpuMat();
        inputMono8.ConvertTo(inputFloat, DepthType.Cv32F, 1.0);

        // corrected_float = input_float ÷ reference_normalized
        using var divided = new GpuMat();
        CudaInvoke.Divide(inputFloat, _referenceGpu, divided, 1.0, DepthType.Cv32F, null);

        // 32F → 8U mit Sättigung (Werte > 255 werden geclampt)
        divided.ConvertTo(output, DepthType.Cv8U, 1.0);
    }

    // ---- Apply (CPU-Convenience für die LiveView) ----------------------

    /// <summary>
    /// CPU-Convenience-API: lädt den Mono8-byte[]-Frame auf die GPU,
    /// wendet Apply an und gibt das korrigierte byte[] zurück.
    /// Bei fehlender Referenz wird der Eingang unverändert weitergereicht.
    /// </summary>
    public byte[] ApplyToBytes(byte[] mono8, int width, int height)
    {
        if (_referenceGpu is null) return mono8;
        if (width != _refWidth || height != _refHeight)
        {
            _logger.LogWarning(
                "Flat-Field-Referenz {RefW}×{RefH} passt nicht zu Frame {W}×{H} — Korrektur übersprungen.",
                _refWidth, _refHeight, width, height);
            return mono8;
        }

        using var cpuIn = new Mat(height, width, DepthType.Cv8U, 1);
        Marshal.Copy(mono8, 0, cpuIn.DataPointer, mono8.Length);
        using var gpuIn = new GpuMat();
        gpuIn.Upload(cpuIn);

        using var gpuOut = new GpuMat();
        Apply(gpuIn, gpuOut);

        using var cpuOut = new Mat();
        gpuOut.Download(cpuOut);
        var result = new byte[width * height];
        Marshal.Copy(cpuOut.DataPointer, result, 0, result.Length);
        return result;
    }

    // ---- Dispose --------------------------------------------------------

    public void Dispose()
    {
        CancelCapture();
        DisposeReference();
    }

    private void DisposeReference()
    {
        _referenceGpu?.Dispose();
        _referenceGpu = null;
        _refWidth = 0;
        _refHeight = 0;
    }
}
