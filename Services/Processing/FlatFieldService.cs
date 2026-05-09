using System;
using System.IO;
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Processing;

/// <summary>
/// Flat-Field-Korrektur: Referenzbild beim Start aufnehmen, im VRAM behalten,
/// pro Frame "Bild ÷ Referenz" → gleichmäßiger Hintergrund.
/// </summary>
public sealed class FlatFieldService : IDisposable
{
    private readonly ILogger<FlatFieldService> _logger;
    private GpuMat? _referenceFloatGpu;

    public FlatFieldService(ILogger<FlatFieldService> logger)
    {
        _logger = logger;
    }

    public bool HasReference => _referenceFloatGpu is not null;

    public void CaptureReference(byte[] mono8, int width, int height)
    {
        DisposeReference();

        using var cpu = new Mat(height, width, DepthType.Cv8U, 1);
        cpu.SetTo(mono8);
        using var asFloat = new Mat();
        cpu.ConvertTo(asFloat, DepthType.Cv32F, 1.0 / 255.0);

        _referenceFloatGpu = new GpuMat();
        _referenceFloatGpu.Upload(asFloat);
        _logger.LogInformation("Flat-Field Referenz aufgenommen ({W}×{H}).", width, height);
    }

    public void LoadReferenceFromFile(string path)
    {
        if (!File.Exists(path)) return;
        using var mat = CvInvoke.Imread(path, ImreadModes.Grayscale);
        var bytes = new byte[mat.Width * mat.Height];
        mat.CopyTo(bytes);
        CaptureReference(bytes, mat.Width, mat.Height);
    }

    public void SaveReferenceToFile(string path)
    {
        if (_referenceFloatGpu is null) return;
        using var cpuFloat = new Mat();
        _referenceFloatGpu.Download(cpuFloat);
        using var asByte = new Mat();
        cpuFloat.ConvertTo(asByte, DepthType.Cv8U, 255.0);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        CvInvoke.Imwrite(path, asByte);
    }

    /// <summary>Wendet die Flat-Field-Korrektur auf der GPU an.</summary>
    public void Apply(GpuMat inputMono8, GpuMat output)
    {
        if (_referenceFloatGpu is null)
        {
            inputMono8.CopyTo(output);
            return;
        }

        using var inputFloat = new GpuMat();
        inputMono8.ConvertTo(inputFloat, DepthType.Cv32F, 1.0 / 255.0);

        using var divided = new GpuMat();
        CudaInvoke.Divide(inputFloat, _referenceFloatGpu, divided);
        divided.ConvertTo(output, DepthType.Cv8U, 255.0);
    }

    public void Dispose() => DisposeReference();

    private void DisposeReference()
    {
        _referenceFloatGpu?.Dispose();
        _referenceFloatGpu = null;
    }
}
