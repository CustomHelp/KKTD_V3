using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Camera;

/// <summary>
/// Liefert Bilder aus einem Verzeichnis (BMP/PNG, Mono8) statt aus der Kamera —
/// für Entwicklung, Tests und Offline-Reproduktion von Fehlerfällen.
/// </summary>
public sealed class OfflineCameraService : ICameraService
{
    private readonly AppSettings _settings;
    private readonly ILogger<OfflineCameraService> _logger;
    private CancellationTokenSource? _cts;
    private long _frameCounter;
    private string[] _files = Array.Empty<string>();
    private int _index;

    public OfflineCameraService(AppSettings settings, ILogger<OfflineCameraService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsRunning => _cts is { IsCancellationRequested: false };
    public event EventHandler<FrameEventArgs>? FrameReceived;

    public Task ConnectAsync()
    {
        var dir = _settings.General.OfflineImagePath;
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Offline-Verzeichnis {Dir} fehlt.", dir);
            _files = Array.Empty<string>();
            return Task.CompletedTask;
        }

        _files = Directory.EnumerateFiles(dir)
            .Where(f => f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToArray();
        _logger.LogInformation("Offline-Modus: {Count} Bilder in {Dir}.", _files.Length, dir);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync() => StopAsync();

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var period = TimeSpan.FromSeconds(1.0 / Math.Max(0.1, _settings.Camera.Framerate));

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && _files.Length > 0)
            {
                try
                {
                    var path = _files[_index % _files.Length];
                    _index++;
                    using var mat = CvInvoke.Imread(path, ImreadModes.Grayscale);
                    if (!mat.IsEmpty)
                    {
                        var bytes = new byte[mat.Width * mat.Height];
                        mat.CopyTo(bytes);
                        var args = new FrameEventArgs(bytes, mat.Width, mat.Height,
                            Interlocked.Increment(ref _frameCounter));
                        FrameReceived?.Invoke(this, args);
                    }
                    await Task.Delay(period, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Offline-Frame-Fehler.");
                }
            }
        }, token);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    public Task<byte[]?> CaptureSingleAsync()
    {
        if (_files.Length == 0) return Task.FromResult<byte[]?>(null);
        var path = _files[_index % _files.Length];
        _index++;
        using var mat = CvInvoke.Imread(path, ImreadModes.Grayscale);
        if (mat.IsEmpty) return Task.FromResult<byte[]?>(null);
        var bytes = new byte[mat.Width * mat.Height];
        mat.CopyTo(bytes);
        return Task.FromResult<byte[]?>(bytes);
    }

    public void Dispose() => _cts?.Cancel();
}
