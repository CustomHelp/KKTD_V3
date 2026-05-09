namespace KKTD_V3.Models;

/// <summary>
/// Globale Hardware- / Anlagen-Konfiguration aus C:\CHP\KKTD.ini.
/// </summary>
public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public CameraSettings Camera { get; set; } = new();
    public ConveyorSettings Conveyor { get; set; } = new();
    public LaneSettings Lanes { get; set; } = new();
    public AdsSettings Ads { get; set; } = new();
}

public sealed class GeneralSettings
{
    public OperatingMode Mode { get; set; } = OperatingMode.Online;
    public string OfflineImagePath { get; set; } = @"C:\CHP\Images\";
    public string ArticlePath { get; set; } = @"C:\CHP\Articles\";
}

public enum OperatingMode
{
    Online,
    Offline
}

public sealed class CameraSettings
{
    public double Framerate { get; set; } = 4.0;
    public double ExposureTimeMs { get; set; } = 0.9;
    public string PixelFormat { get; set; } = "Mono8";
    public double AnalogGain { get; set; } = 1.0;
    public double DigitalGain { get; set; } = 1.0;
    public double Gamma { get; set; } = 1.0;
    public long BandwidthLimitBytesPerSec { get; set; } = 183_928_574;
    public bool AutoExposure { get; set; } = false;
    public bool AutoGain { get; set; } = false;
    public int SkipFrames { get; set; } = 2;
}

public sealed class ConveyorSettings
{
    public double DistanceCameraToEjectorMm { get; set; } = 400;
    public double EjectorPreTriggerMm { get; set; } = 5;
    public double EjectorPostTriggerMm { get; set; } = 10;
    public int EjectorDeadTimeMs { get; set; } = 200;
    public double MaxSpeedMmPerSec { get; set; } = 200;
    public double MinSpeedMmPerSec { get; set; } = 20;
}

public sealed class LaneSettings
{
    public int Count { get; set; } = 6;
    public LaneDefinition[] Lanes { get; set; } = new LaneDefinition[6];
}

public sealed class LaneDefinition
{
    public int Index { get; set; }
    public int XPx { get; set; }
    public int WidthPx { get; set; }
}

public sealed class AdsSettings
{
    public string AmsNetId { get; set; } = "127.0.0.1.1.1";
    public int Port { get; set; } = 851;
    public string EncoderVariableName { get; set; } = "MAIN.Encoder_mm";
    public string EjectorVariablePrefix { get; set; } = "MAIN.Ejector_Lane";
    public string SpeedVariableName { get; set; } = "MAIN.ConveyorSpeed_mmPerSec";
    public string DensityVariableName { get; set; } = "MAIN.RequestedDensity";
}
