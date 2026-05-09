using System;
using System.Globalization;
using System.IO;
using IniParser;
using IniParser.Model;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services;

/// <summary>
/// Liest / schreibt C:\CHP\KKTD.ini. Defaults werden erstellt, falls die Datei fehlt.
/// </summary>
public sealed class IniService
{
    public const string DefaultIniPath = @"C:\CHP\KKTD.ini";
    private readonly FileIniDataParser _parser = new();
    private readonly ILogger<IniService> _logger;

    public IniService(ILogger<IniService> logger)
    {
        _logger = logger;
    }

    public AppSettings Load(string path = DefaultIniPath)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("INI {Path} fehlt — Defaults werden geschrieben.", path);
            var defaults = new AppSettings();
            InitDefaultLanes(defaults);
            Save(defaults, path);
            return defaults;
        }

        var data = _parser.ReadFile(path);
        var settings = new AppSettings();

        // [General]
        settings.General.Mode = ParseEnum(data["General"]["Mode"], OperatingMode.Online);
        settings.General.OfflineImagePath = data["General"]["OfflineImagePath"] ?? settings.General.OfflineImagePath;
        settings.General.ArticlePath = data["General"]["ArticlePath"] ?? settings.General.ArticlePath;

        // [Camera]
        settings.Camera.Framerate = ParseDouble(data["Camera"]["Framerate"], 4.0);
        settings.Camera.ExposureTimeMs = ParseDouble(data["Camera"]["ExposureTime_ms"], 0.9);
        settings.Camera.PixelFormat = data["Camera"]["PixelFormat"] ?? "Mono8";
        settings.Camera.AnalogGain = ParseDouble(data["Camera"]["AnalogGain"], 1.0);
        settings.Camera.DigitalGain = ParseDouble(data["Camera"]["DigitalGain"], 1.0);
        settings.Camera.Gamma = ParseDouble(data["Camera"]["Gamma"], 1.0);
        settings.Camera.BandwidthLimitBytesPerSec = ParseLong(data["Camera"]["BandwidthLimit_BytesPerSec"], 183_928_574);
        settings.Camera.AutoExposure = ParseOnOff(data["Camera"]["AutoExposure"]);
        settings.Camera.AutoGain = ParseOnOff(data["Camera"]["AutoGain"]);
        settings.Camera.SkipFrames = ParseInt(data["Camera"]["SkipFrames"], 2);

        // [Conveyor]
        settings.Conveyor.DistanceCameraToEjectorMm = ParseDouble(data["Conveyor"]["DistanceCameraToEjector_mm"], 400);
        settings.Conveyor.EjectorPreTriggerMm = ParseDouble(data["Conveyor"]["EjectorPreTrigger_mm"], 5);
        settings.Conveyor.EjectorPostTriggerMm = ParseDouble(data["Conveyor"]["EjectorPostTrigger_mm"], 10);
        settings.Conveyor.EjectorDeadTimeMs = ParseInt(data["Conveyor"]["EjectorDeadTime_ms"], 200);
        settings.Conveyor.MaxSpeedMmPerSec = ParseDouble(data["Conveyor"]["MaxSpeed_mmPerSec"], 200);
        settings.Conveyor.MinSpeedMmPerSec = ParseDouble(data["Conveyor"]["MinSpeed_mmPerSec"], 20);

        // [Lanes]
        settings.Lanes.Count = ParseInt(data["Lanes"]["Count"], 6);
        settings.Lanes.Lanes = new LaneDefinition[settings.Lanes.Count];
        for (int i = 0; i < settings.Lanes.Count; i++)
        {
            int n = i + 1;
            settings.Lanes.Lanes[i] = new LaneDefinition
            {
                Index = n,
                XPx = ParseInt(data["Lanes"][$"Lane{n}_X_px"], 0),
                WidthPx = ParseInt(data["Lanes"][$"Lane{n}_Width_px"], 0)
            };
        }

        // [ADS]
        settings.Ads.AmsNetId = data["ADS"]["AmsNetId"] ?? settings.Ads.AmsNetId;
        settings.Ads.Port = ParseInt(data["ADS"]["Port"], 851);
        settings.Ads.EncoderVariableName = data["ADS"]["EncoderVariableName"] ?? settings.Ads.EncoderVariableName;
        settings.Ads.EjectorVariablePrefix = data["ADS"]["EjectorVariablePrefix"] ?? settings.Ads.EjectorVariablePrefix;
        settings.Ads.SpeedVariableName = data["ADS"]["SpeedVariableName"] ?? settings.Ads.SpeedVariableName;
        settings.Ads.DensityVariableName = data["ADS"]["DensityVariableName"] ?? settings.Ads.DensityVariableName;

        return settings;
    }

    public void Save(AppSettings settings, string path = DefaultIniPath)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var data = new IniData();

        data["General"]["Mode"] = settings.General.Mode.ToString();
        data["General"]["OfflineImagePath"] = settings.General.OfflineImagePath;
        data["General"]["ArticlePath"] = settings.General.ArticlePath;

        data["Camera"]["Framerate"] = F(settings.Camera.Framerate);
        data["Camera"]["ExposureTime_ms"] = F(settings.Camera.ExposureTimeMs);
        data["Camera"]["PixelFormat"] = settings.Camera.PixelFormat;
        data["Camera"]["AnalogGain"] = F(settings.Camera.AnalogGain);
        data["Camera"]["DigitalGain"] = F(settings.Camera.DigitalGain);
        data["Camera"]["Gamma"] = F(settings.Camera.Gamma);
        data["Camera"]["BandwidthLimit_BytesPerSec"] = settings.Camera.BandwidthLimitBytesPerSec.ToString(CultureInfo.InvariantCulture);
        data["Camera"]["AutoExposure"] = settings.Camera.AutoExposure ? "On" : "Off";
        data["Camera"]["AutoGain"] = settings.Camera.AutoGain ? "On" : "Off";
        data["Camera"]["SkipFrames"] = settings.Camera.SkipFrames.ToString(CultureInfo.InvariantCulture);

        data["Conveyor"]["DistanceCameraToEjector_mm"] = F(settings.Conveyor.DistanceCameraToEjectorMm);
        data["Conveyor"]["EjectorPreTrigger_mm"] = F(settings.Conveyor.EjectorPreTriggerMm);
        data["Conveyor"]["EjectorPostTrigger_mm"] = F(settings.Conveyor.EjectorPostTriggerMm);
        data["Conveyor"]["EjectorDeadTime_ms"] = settings.Conveyor.EjectorDeadTimeMs.ToString(CultureInfo.InvariantCulture);
        data["Conveyor"]["MaxSpeed_mmPerSec"] = F(settings.Conveyor.MaxSpeedMmPerSec);
        data["Conveyor"]["MinSpeed_mmPerSec"] = F(settings.Conveyor.MinSpeedMmPerSec);

        data["Lanes"]["Count"] = settings.Lanes.Count.ToString(CultureInfo.InvariantCulture);
        for (int i = 0; i < settings.Lanes.Count; i++)
        {
            int n = i + 1;
            var lane = settings.Lanes.Lanes[i] ?? new LaneDefinition { Index = n };
            data["Lanes"][$"Lane{n}_X_px"] = lane.XPx.ToString(CultureInfo.InvariantCulture);
            data["Lanes"][$"Lane{n}_Width_px"] = lane.WidthPx.ToString(CultureInfo.InvariantCulture);
        }

        data["ADS"]["AmsNetId"] = settings.Ads.AmsNetId;
        data["ADS"]["Port"] = settings.Ads.Port.ToString(CultureInfo.InvariantCulture);
        data["ADS"]["EncoderVariableName"] = settings.Ads.EncoderVariableName;
        data["ADS"]["EjectorVariablePrefix"] = settings.Ads.EjectorVariablePrefix;
        data["ADS"]["SpeedVariableName"] = settings.Ads.SpeedVariableName;
        data["ADS"]["DensityVariableName"] = settings.Ads.DensityVariableName;

        _parser.WriteFile(path, data);
    }

    private static void InitDefaultLanes(AppSettings settings)
    {
        const int totalWidth = 8192;
        int laneWidth = totalWidth / 6;
        for (int i = 0; i < 6; i++)
        {
            settings.Lanes.Lanes[i] = new LaneDefinition
            {
                Index = i + 1,
                XPx = i * laneWidth,
                WidthPx = i == 5 ? totalWidth - i * laneWidth : laneWidth
            };
        }
    }

    private static string F(double v) => v.ToString(CultureInfo.InvariantCulture);

    private static double ParseDouble(string? s, double fallback) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static int ParseInt(string? s, int fallback) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static long ParseLong(string? s, long fallback) =>
        long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static bool ParseOnOff(string? s) =>
        string.Equals(s, "On", StringComparison.OrdinalIgnoreCase);

    private static T ParseEnum<T>(string? s, T fallback) where T : struct =>
        Enum.TryParse<T>(s, true, out var v) ? v : fallback;
}
