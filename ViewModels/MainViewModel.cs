using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKTD_V3.Models;

namespace KKTD_V3.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LiveViewModel _live;
    private readonly TeachViewModel _teach;
    private readonly CatalogViewModel _catalog;
    private readonly AppSettings _settings;

    [ObservableProperty] private object? currentView;
    [ObservableProperty] private string statusText = "Bereit.";
    [ObservableProperty] private string modeText = string.Empty;
    [ObservableProperty] private bool workerPresent;

    public MainViewModel(
        LiveViewModel live,
        TeachViewModel teach,
        CatalogViewModel catalog,
        AppSettings settings)
    {
        _live = live;
        _teach = teach;
        _catalog = catalog;
        _settings = settings;
        ModeText = $"Modus: {settings.General.Mode}";
        CurrentView = _live;
    }

    [RelayCommand] private void ShowLive() => CurrentView = _live;
    [RelayCommand] private void ShowTeach() => CurrentView = _teach;
    [RelayCommand] private void ShowCatalog() => CurrentView = _catalog;
}
