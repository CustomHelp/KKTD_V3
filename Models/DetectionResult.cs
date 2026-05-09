using System.Drawing;

namespace KKTD_V3.Models;

public enum DetectionVerdict
{
    GoodPart,
    BorderlinePart,
    ForeignPart,
    Touching,
    Unknown
}

/// <summary>
/// Ergebnis der Pipeline pro extrahiertem Segment.
/// </summary>
public sealed class DetectionResult
{
    public int Lane { get; set; }
    public int Zone { get; set; }
    public Rectangle BoundingBox { get; set; }
    public int AreaPx { get; set; }
    public string? MatchedPattern { get; set; }
    public string? MatchedTemplateId { get; set; }
    public double Score { get; set; }
    public DetectionVerdict Verdict { get; set; } = DetectionVerdict.Unknown;
    public double EncoderAtCaptureMm { get; set; }
    public string? Notes { get; set; }
}
