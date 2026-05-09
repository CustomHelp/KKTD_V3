using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKTD_V3.Services;

namespace KKTD_V3.ViewModels;

public partial class CatalogViewModel : ObservableObject
{
    private readonly ArticleManager _articles;

    [ObservableProperty] private string? selectedArticle;

    public ObservableCollection<string> ArticleNumbers { get; } = new();

    public CatalogViewModel(ArticleManager articles)
    {
        _articles = articles;
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        ArticleNumbers.Clear();
        foreach (var n in _articles.ListArticleNumbers())
            ArticleNumbers.Add(n);
    }
}
