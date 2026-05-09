namespace KKTD_V3.Models;

/// <summary>
/// Ein wartender Ausschleus-Auftrag, vom Encoder-Watcher abgearbeitet.
/// </summary>
public sealed class EjectorQueueEntry
{
    public int Lane { get; set; }
    public double EncoderOpenMm { get; set; }
    public double EncoderCloseMm { get; set; }
    public bool IsOpen { get; set; }
    public string ReasonText { get; set; } = string.Empty;
}
