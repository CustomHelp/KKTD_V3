namespace KKTD_V3.Models;

/// <summary>
/// Ein Erscheinungsmuster eines Teils (z.B. "liegend" oder "stehend") mit Flächen-Toleranzen.
/// </summary>
public sealed class PatternData
{
    public string Name { get; set; } = string.Empty;
    public int AreaPx { get; set; }
    public double AreaTolerancePercent { get; set; } = 10;
    public int AreaMinPx { get; set; }
    public int AreaMaxPx { get; set; }
}
