using System.Collections.ObjectModel;
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

    public ObservableCollection<DetectionResult> RecentResults { get; } = new();

    public LiveViewModel(
        ICameraService camera,
        ImageProcessingService processing,
        EjectorService ejector)
    {
        _camera = camera;
        _processing = processing;
        _ejector = ejector;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task StartAsync()
    {
        await _camera.ConnectAsync();
        await _camera.StartAsync();
        _ejector.Start();
        HeadlineText = "Live läuft.";
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task StopAsync()
    {
        await _camera.StopAsync();
        await _ejector.StopAsync();
        HeadlineText = "Gestoppt.";
    }
}
