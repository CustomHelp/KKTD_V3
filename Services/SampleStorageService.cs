using System;
using System.IO;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services;

/// <summary>
/// Speichert einen einzelnen Mono8-Frame als BMP unter
/// <see cref="GeneralSettings.OfflineImagePath"/> mit Zeitstempel-Dateinamen.
/// Dient als Quelle für den OfflineCameraService und für die Offline-Iteration
/// an der Pipeline.
/// </summary>
public sealed class SampleStorageService
{
    private readonly AppSettings _settings;
    private readonly ILogger<SampleStorageService> _logger;

    public SampleStorageService(AppSettings settings, ILogger<SampleStorageService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public string? Save(byte[] mono8, int width, int height, string? prefix = null)
    {
        try
        {
            var dir = _settings.General.OfflineImagePath;
            Directory.CreateDirectory(dir);

            var name = $"{prefix ?? "sample"}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";
            var path = Path.Combine(dir, name);

            using var mat = new Mat(height, width, DepthType.Cv8U, 1);
            Marshal.Copy(mono8, 0, mat.DataPointer, mono8.Length);
            CvInvoke.Imwrite(path, mat);

            _logger.LogInformation("Sample {Name} ({W}×{H}) gespeichert.", name, width, height);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sample-Speichern fehlgeschlagen.");
            return null;
        }
    }
}
