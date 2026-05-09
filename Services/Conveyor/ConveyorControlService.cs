using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Conveyor;

/// <summary>
/// Regelt Bandgeschwindigkeit und angeforderte Teile-Dichte über ADS,
/// basierend auf gemessener Verarbeitungs-Auslastung und Berührungs-Zähler.
/// </summary>
public sealed class ConveyorControlService
{
    private readonly AdsService _ads;
    private readonly AppSettings _settings;
    private readonly ILogger<ConveyorControlService> _logger;

    public ConveyorControlService(AdsService ads, AppSettings settings, ILogger<ConveyorControlService> logger)
    {
        _ads = ads;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Aktualisiert die Stellgrößen anhand der Pipeline-Statistik des letzten Bildes.</summary>
    public void Update(double processingTimeMs, int touchingCount, int totalParts)
    {
        if (!_ads.IsConnected) return;

        double targetCycleMs = 1000.0 / 4.0; // 4 fps
        double load = processingTimeMs / targetCycleMs;
        double currentSpeed = _settings.Conveyor.MaxSpeedMmPerSec;

        if (load > 0.9 && currentSpeed > _settings.Conveyor.MinSpeedMmPerSec)
            currentSpeed *= 0.9;
        else if (load < 0.5 && currentSpeed < _settings.Conveyor.MaxSpeedMmPerSec)
            currentSpeed *= 1.1;

        currentSpeed = System.Math.Clamp(
            currentSpeed,
            _settings.Conveyor.MinSpeedMmPerSec,
            _settings.Conveyor.MaxSpeedMmPerSec);

        _ads.SetConveyorSpeed(currentSpeed);

        // Dichte: relative Empfehlung 0..1
        double density = 0.6;
        if (touchingCount > 2) density = 0.4;
        else if (totalParts < 4) density = 0.8;
        _ads.SetRequestedDensity(density);
    }
}
