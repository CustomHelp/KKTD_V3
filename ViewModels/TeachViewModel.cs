using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKTD_V3.Models;
using KKTD_V3.Services;

namespace KKTD_V3.ViewModels;

public partial class TeachViewModel : ObservableObject
{
    private readonly TeachService _teach;
    private readonly ArticleManager _articles;
    private readonly BarcodeService _barcode;

    [ObservableProperty] private string articleNumber = string.Empty;
    [ObservableProperty] private string articleName = string.Empty;
    [ObservableProperty] private double partLengthMm = 30;
    [ObservableProperty] private double partWidthMm = 10;
    [ObservableProperty] private bool canStand;
    [ObservableProperty] private int zoneCount;
    [ObservableProperty] private string statusText = string.Empty;

    public TeachViewModel(TeachService teach, ArticleManager articles, BarcodeService barcode)
    {
        _teach = teach;
        _articles = articles;
        _barcode = barcode;
    }

    [RelayCommand]
    private void Recalculate()
    {
        var article = BuildArticle();
        var plan = _teach.ComputeZonePlan(article);
        ZoneCount = plan.ZoneCount;
        StatusText = $"Zonengröße {plan.ZoneSizeMm:0} mm, Schritt {plan.StepMm:0} mm.";
    }

    [RelayCommand]
    private void Save()
    {
        var article = BuildArticle();
        _articles.Save(article);
        StatusText = $"Artikel {article.ArticleNumber} gespeichert.";
    }

    private ArticleData BuildArticle() => new()
    {
        ArticleNumber = ArticleNumber,
        Name = ArticleName,
        Dimensions = { LengthMm = PartLengthMm, WidthMm = PartWidthMm },
        CanStand = CanStand
    };
}
