using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKTD_V3.Models;
using KKTD_V3.Services;
using KKTD_V3.Services.Camera;
using KKTD_V3.Services.Ejector;
using KKTD_V3.Services.Processing;

namespace KKTD_V3.ViewModels;

public partial class LiveViewModel : ObservableObject
{
    private readonly ICameraService _camera;
    private readonly ImageProcessingService _processing;
    private readonly EjectorService _ejector;
    private readonly FlatFieldService _flatField;
    private readonly ForegroundDetector _detector;
    private readonly SampleStorageService _sampleStorage;

    [ObservableProperty] private long frameCount;
    [ObservableProperty] private double fps;
    [ObservableProperty] private double lastProcessingTimeMs;
    [ObservableProperty] private string headlineText = "Kamera nicht verbunden.";
    [ObservableProperty] private WriteableBitmap? liveBitmap;
    [ObservableProperty] private int imageWidth;
    [ObservableProperty] private int imageHeight;
    [ObservableProperty] private bool flatFieldEnabled = true;
    [ObservableProperty] private bool flatFieldReady;
    [ObservableProperty] private string flatFieldStatus = "Flat-Field: nicht kalibriert.";
    [ObservableProperty] private bool detectionEnabled = true;
    [ObservableProperty] private string detectionStatus = "Detektion: aus";

    private readonly Stopwatch _fpsClock = Stopwatch.StartNew();
    private long _lastFpsFrame;
    private bool _autoPromptDone;
    private int _detectionInFlight;   // 0/1, via Interlocked

    // Letzter empfangener (unkorrigierter) Mono8-Frame — für Sample-Save.
    private byte[]? _lastFrameBytes;
    private int _lastFrameWidth;
    private int _lastFrameHeight;
    private readonly object _lastFrameGate = new();

    public ObservableCollection<DetectionResult> Detections { get; } = new();
    public ObservableCollection<DetectionResult> RecentResults { get; } = new();

    public LiveViewModel(
        ICameraService camera,
        ImageProcessingService processing,
        EjectorService ejector,
        FlatFieldService flatField,
        ForegroundDetector detector,
        SampleStorageService sampleStorage)
    {
        _camera = camera;
        _processing = processing;
        _ejector = ejector;
        _flatField = flatField;
        _detector = detector;
        _sampleStorage = sampleStorage;
        _camera.FrameReceived += OnFrameReceived;
    }

    private void OnFrameReceived(object? sender, FrameEventArgs e)
    {
        // Letzten Frame für Sample-Save merken (Original, vor Korrektur).
        lock (_lastFrameGate)
        {
            _lastFrameBytes = e.Mono8Pixels;
            _lastFrameWidth = e.Width;
            _lastFrameHeight = e.Height;
        }

        // Reference-Capture läuft auf der Service-Seite; Frames während der Aufnahme einspeisen.
        if (_flatField.IsCapturing)
        {
            _flatField.FeedSampleFrame(e.Mono8Pixels, e.Width, e.Height);
        }

        // Display-Pfad: optional Korrektur, dann Bitmap-Update auf UI-Thread.
        byte[] displayPixels = (FlatFieldEnabled && _flatField.HasReference)
            ? _flatField.ApplyToBytes(e.Mono8Pixels, e.Width, e.Height)
            : e.Mono8Pixels;

        // Detektion auf Worker-Thread (CPU-Otsu blockiert sonst den Frame-Callback).
        if (DetectionEnabled && Interlocked.CompareExchange(ref _detectionInFlight, 1, 0) == 0)
        {
            int w = e.Width;
            int h = e.Height;
            byte[] forDetection = displayPixels;
            _ = Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                IReadOnlyList<DetectionResult> blobs;
                try { blobs = _detector.Detect(forDetection, w, h); }
                finally { Interlocked.Exchange(ref _detectionInFlight, 0); }
                sw.Stop();
                PostDetections(blobs, sw.Elapsed.TotalMilliseconds);
            });
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        if (dispatcher.CheckAccess())
        {
            UpdateBitmap(displayPixels, e.Width, e.Height, e.FrameNumber);
        }
        else
        {
            dispatcher.BeginInvoke(new Action(() =>
                UpdateBitmap(displayPixels, e.Width, e.Height, e.FrameNumber)));
        }
    }

    private void PostDetections(IReadOnlyList<DetectionResult> blobs, double elapsedMs)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        dispatcher.BeginInvoke(new Action(() =>
        {
            Detections.Clear();
            foreach (var b in blobs) Detections.Add(b);
            LastProcessingTimeMs = elapsedMs;
            DetectionStatus = blobs.Count == 0
                ? $"Detektion: 0 Blobs ({elapsedMs:F0} ms)"
                : $"Detektion: {blobs.Count} Blob(s) ({elapsedMs:F0} ms)";
        }));
    }

    private void UpdateBitmap(byte[] pixels, int width, int height, long frameNumber)
    {
        if (LiveBitmap is null ||
            LiveBitmap.PixelWidth != width ||
            LiveBitmap.PixelHeight != height)
        {
            LiveBitmap = new WriteableBitmap(
                width, height, 96, 96,
                System.Windows.Media.PixelFormats.Gray8, null);
            ImageWidth = width;
            ImageHeight = height;
        }

        var rect = new System.Windows.Int32Rect(0, 0, width, height);
        LiveBitmap.WritePixels(rect, pixels, width, 0);

        FrameCount = frameNumber;

        if (_fpsClock.ElapsedMilliseconds >= 1000)
        {
            long delta = frameNumber - _lastFpsFrame;
            Fps = delta * 1000.0 / _fpsClock.ElapsedMilliseconds;
            _lastFpsFrame = frameNumber;
            _fpsClock.Restart();
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        await _camera.ConnectAsync();
        await _camera.StartAsync();
        _ejector.Start();
        HeadlineText = _camera.IsRunning ? "Live läuft." : "Kamera nicht verfügbar.";

        if (_camera.IsRunning && !_flatField.HasReference && !_autoPromptDone)
        {
            _autoPromptDone = true;
            _ = CaptureFlatFieldAsync();
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _camera.StopAsync();
        await _ejector.StopAsync();
        HeadlineText = "Gestoppt.";
    }

    [RelayCommand]
    private async Task CaptureFlatFieldAsync()
    {
        if (!_camera.IsRunning)
        {
            MessageBox.Show(
                "Bitte zuerst die Kamera über 'Start' aktivieren.",
                "Flat-Field", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            "Bitte das Förderband vollständig leeren und die Beleuchtung einschalten.\n\n" +
            "Mit OK werden 4 Frames als Referenzbild gemittelt.",
            "Flat-Field Referenz aufnehmen",
            MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (answer != MessageBoxResult.OK) return;

        FlatFieldStatus = "Flat-Field: nehme Referenz auf …";
        var ok = await _flatField.BeginCaptureAsync(sampleCount: 4);

        FlatFieldReady = _flatField.HasReference;
        FlatFieldStatus = ok && FlatFieldReady
            ? "Flat-Field: kalibriert."
            : "Flat-Field: Aufnahme fehlgeschlagen.";

        if (!ok)
        {
            MessageBox.Show(
                "Flat-Field-Aufnahme fehlgeschlagen — bitte Beleuchtung und Kamera prüfen und erneut versuchen.",
                "Flat-Field", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void ClearFlatField()
    {
        _flatField.ClearReference();
        FlatFieldReady = false;
        FlatFieldStatus = "Flat-Field: nicht kalibriert.";
    }

    [RelayCommand]
    private void SaveSample()
    {
        byte[]? bytes;
        int w, h;
        lock (_lastFrameGate)
        {
            bytes = _lastFrameBytes;
            w = _lastFrameWidth;
            h = _lastFrameHeight;
        }
        if (bytes is null || w == 0 || h == 0)
        {
            MessageBox.Show("Noch kein Frame empfangen.", "Sample speichern",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var path = _sampleStorage.Save(bytes, w, h);
        HeadlineText = path is null
            ? "Sample-Speichern fehlgeschlagen."
            : $"Gespeichert: {System.IO.Path.GetFileName(path)}";
    }

    partial void OnDetectionEnabledChanged(bool value)
    {
        if (!value)
        {
            DetectionStatus = "Detektion: aus";
            Detections.Clear();
        }
    }
}
