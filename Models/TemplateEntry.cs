namespace KKTD_V3.Models;

/// <summary>
/// Einzelnes Template eines Teils, zugeordnet zu Bahn + Zone + Rotation + Muster.
/// </summary>
public sealed class TemplateEntry
{
    public string Id { get; set; } = string.Empty;
    public int Lane { get; set; }
    public int Zone { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public double Rotation { get; set; }
    public string File { get; set; } = string.Empty;
    public int AreaPx { get; set; }
}
