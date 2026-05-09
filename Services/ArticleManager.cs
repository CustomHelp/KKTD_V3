using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services;

/// <summary>
/// Lädt / speichert Artikel als article.json in C:\CHP\Articles\{ArticleNumber}\.
/// </summary>
public sealed class ArticleManager
{
    private readonly ILogger<ArticleManager> _logger;
    private readonly AppSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

    public ArticleManager(AppSettings settings, ILogger<ArticleManager> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public string ArticleRoot => _settings.General.ArticlePath;

    public IReadOnlyList<string> ListArticleNumbers()
    {
        if (!Directory.Exists(ArticleRoot)) return Array.Empty<string>();
        return Directory.GetDirectories(ArticleRoot)
                        .Select(Path.GetFileName)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .Cast<string>()
                        .ToList();
    }

    public bool Exists(string articleNumber) =>
        File.Exists(GetArticlePath(articleNumber));

    public ArticleData? Load(string articleNumber)
    {
        var path = GetArticlePath(articleNumber);
        if (!File.Exists(path))
        {
            _logger.LogWarning("Artikel {Number} nicht gefunden ({Path}).", articleNumber, path);
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ArticleData>(json, JsonOptions);
    }

    public void Save(ArticleData article)
    {
        if (string.IsNullOrWhiteSpace(article.ArticleNumber))
            throw new ArgumentException("ArticleNumber ist Pflicht.", nameof(article));

        var dir = GetArticleDirectory(article.ArticleNumber);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "templates"));
        Directory.CreateDirectory(Path.Combine(dir, "thumbnails"));

        article.LastModified = DateTime.Now;
        var json = JsonSerializer.Serialize(article, JsonOptions);
        File.WriteAllText(GetArticlePath(article.ArticleNumber), json);
        _logger.LogInformation("Artikel {Number} gespeichert.", article.ArticleNumber);
    }

    public string GetArticleDirectory(string articleNumber) =>
        Path.Combine(ArticleRoot, articleNumber);

    public string GetArticlePath(string articleNumber) =>
        Path.Combine(GetArticleDirectory(articleNumber), "article.json");

    public string GetFlatFieldPath(string articleNumber) =>
        Path.Combine(GetArticleDirectory(articleNumber), "flatfield.bmp");

    public string GetTemplatesDirectory(string articleNumber) =>
        Path.Combine(GetArticleDirectory(articleNumber), "templates");
}
