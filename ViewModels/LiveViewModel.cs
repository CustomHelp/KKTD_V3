using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKTD_V3.Models;
using KKTD_V3.Services.Camera;
using KKTD_V3.Services.Ejector;
using KKTD_V3.Services.Processing;

namespace KKTD_V3.ViewModels;

public partial class LiveViewModel : ObservableObject
{
    private readonly ICameraService _camera;
    private readonly ImageProcessingService _processing;
    private readonly EjectorService _ejector;

    [ObservableProperty] private long frameCount;
    [ObservableProperty] private double fps;
    [ObservableProperty] private double lastProcessingTimeMs;
    [ObservableProperty] private string headlineText = "Kamera nicht verbunden.";
    [ObservableProperty] private WriteableBitmap? liveBitmap;

    private readonly Stopwatch _fpsClock = Stopwatch.StartNew();
    private long _lastFpsFrame;

    public ObservableCollection<DetectionResult> RecentResults { get; } = new();

    public LiveViewModel(
        ICameraService camera,
        ImageProcessingService processing,
        EjectorService ejector)
    {
        _camera = camera;
        _processing = processing;
        _ejector = ejector;
        _camera.FrameReceived += OnFrameReceived;
    }

    private void OnFrameReceived(object? sender, FrameEventArgs e)
    {
        // Bitmap-Update muss auf den UI-Thread.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        if (dispatcher.CheckAccess())
        {
            UpdateBitmap(e);
        }
        else
        {
            dispatcher.BeginInvoke(new Action(() => UpdateBitmap(e)));
        }
    }

    private void UpdateBitmap(FrameEventArgs e)
    {
        if (LiveBitmap is null ||
            LiveBitmap.PixelWidth != e.Width ||
            LiveBitmap.PixelHeight != e.Height)
        {
            LiveBitmap = new WriteableBitmap(
                e.Width, e.Height, 96, 96,
                System.Windows.Media.PixelFormats.Gray8, null);
        }

        var rect = new System.Windows.Int32Rect(0, 0, e.Width, e.Height);
        LiveBitmap.WritePixels(rect, e.Mono8Pixels, e.Width, 0);

        FrameCount = e.FrameNumber;

        // FPS-Glättung über 1-Sekunden-Fenster.
        if (_fpsClock.ElapsedMilliseconds >= 1000)
        {
            long delta = e.FrameNumber - _lastFpsFrame;
            Fps = delta * 1000.0 / _fpsClock.ElapsedMilliseconds;
            _lastFpsFrame = e.FrameNumber;
            _fpsClock.Restart();
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task StartAsync()
    {
        await _camera.ConnectAsync();
        await _camera.StartAsync();
        _ejector.Start();
        HeadlineText = _camera.IsRunning ? "Live läuft." : "Kamera nicht verfügbar.";
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task StopAsync()
    {
        await _camera.StopAsync();
        await _ejector.StopAsync();
        HeadlineText = "Gestoppt.";
    }
}
