using System;
using System.Collections.Generic;

namespace KKTD_V3.Models;

/// <summary>
/// Vollständige Artikel-Definition (Schema laut KKTD_ProjectMarkdown_V3.md §6).
/// Wird als article.json pro Artikelverzeichnis gespeichert.
/// </summary>
public sealed class ArticleData
{
    public string ArticleNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;

    public ArticleDimensions Dimensions { get; set; } = new();
    public bool CanStand { get; set; }

    public List<PatternData> Patterns { get; set; } = new();
    public DetectionConfig Detection { get; set; } = new();
    public QualityConfig Quality { get; set; } = new();
    public ZoneConfig Zones { get; set; } = new();
    public TeachState TeachSettings { get; set; } = new();
    public ConveyorSpeedConfig ConveyorSettings { get; set; } = new();
    public List<TemplateEntry> Templates { get; set; } = new();
}

public sealed class ArticleDimensions
{
    public double LengthMm { get; set; }
    public double WidthMm { get; set; }
}

public sealed class DetectionConfig
{
    public double ContourMatchThresholdCoarse { get; set; } = 0.70;
    public double ContourMatchThresholdFine { get; set; } = 0.90;
    public double AreaTolerancePercent { get; set; } = 10;
    public double TouchDetectionFactor { get; set; } = 1.8;
}

public sealed class QualityConfig
{
    public double BadPartThresholdPercent { get; set; } = 0.01;
    public bool WorkerPresent { get; set; }
}

public sealed class ZoneConfig
{
    public double SegmentOverlapMm { get; set; } = 40;
    public int TemplatesPerZone { get; set; } = 20;
}

public sealed class TeachState
{
    public bool LyingDone { get; set; }
    public bool StandingDone { get; set; }
    public int TotalTeachImages { get; set; }
}

public sealed class ConveyorSpeedConfig
{
    public double OptimalSpeedMmPerSec { get; set; } = 150;
    public int MaxPartsPerImage { get; set; } = 12;
    public bool DensityTooHigh { get; set; }
}
