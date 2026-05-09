using System.Threading.Tasks;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services;

/// <summary>
/// Steuert den Teach-In Workflow (siehe §11 KKTD_ProjectMarkdown_V3.md).
/// </summary>
public sealed class TeachService
{
    private readonly ArticleManager _articles;
    private readonly ILogger<TeachService> _logger;

    public TeachService(ArticleManager articles, ILogger<TeachService> logger)
    {
        _articles = articles;
        _logger = logger;
    }

    public Task StartCaptureAsync(ArticleData article)
    {
        // TODO: Aufnahme-Phase – Bilder auf Disk schreiben (kein Rechnen).
        return Task.CompletedTask;
    }

    public Task ProcessCapturedAsync(ArticleData article)
    {
        // TODO: Verarbeitungs-Phase – Segmente extrahieren, Templates erzeugen, Zonen zuordnen.
        return Task.CompletedTask;
    }

    public ZonePlan ComputeZonePlan(ArticleData article)
    {
        var partLengthMm = article.Dimensions.LengthMm;
        if (partLengthMm <= 0) partLengthMm = 30;
        double zoneSizeMm = 1.5 * partLengthMm;
        double overlapMm = article.Zones.SegmentOverlapMm;
        double stepMm = zoneSizeMm - overlapMm;
        const double fovMm = 300; // Förderrichtung
        int zoneCount = (int)System.Math.Ceiling(fovMm / stepMm);
        return new ZonePlan(zoneCount, zoneSizeMm, overlapMm, stepMm);
    }
}

public sealed record ZonePlan(int ZoneCount, double ZoneSizeMm, double OverlapMm, double StepMm);
