using System;
using System.Threading.Tasks;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;
using TwinCAT.Ads;

namespace KKTD_V3.Services;

/// <summary>
/// Lokale TwinCAT-ADS-Verbindung: Encoder lesen, Ausschleuser/Geschwindigkeit/Dichte schreiben.
/// Bricht beim Verbinden nicht ab, wenn Variablen (noch) nicht im PLC-Symbolverzeichnis sind —
/// fehlende Handles bleiben 0 und die zugehörigen Reads/Writes werden im Log gemeldet, statt
/// die Anwendung zu killen.
/// </summary>
public sealed class AdsService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly ILogger<AdsService> _logger;
    private readonly AdsClient _client = new();

    private uint _encoderHandle;
    private uint _speedHandle;
    private uint _densityHandle;
    private readonly uint[] _ejectorHandles = new uint[6];

    private bool _connected;
    private bool _encoderResolved;
    private bool _speedResolved;
    private bool _densityResolved;
    private readonly bool[] _ejectorResolved = new bool[6];

    public AdsService(AppSettings settings, ILogger<AdsService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsConnected => _connected;

    public void Connect()
    {
        if (_connected) return;

        try
        {
            var amsId = AmsNetId.Parse(_settings.Ads.AmsNetId);
            _client.Connect(amsId, _settings.Ads.Port);
            _connected = true;
            _logger.LogInformation("ADS verbunden — {AmsNetId}:{Port}.",
                _settings.Ads.AmsNetId, _settings.Ads.Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ADS-Verbindung zu {AmsNetId}:{Port} fehlgeschlagen — Anwendung läuft ohne Encoder/Ausschleuser weiter.",
                _settings.Ads.AmsNetId, _settings.Ads.Port);
            return;
        }

        _encoderResolved = TryCreateHandle(_settings.Ads.EncoderVariableName, out _encoderHandle);
        _speedResolved = TryCreateHandle(_settings.Ads.SpeedVariableName, out _speedHandle);
        _densityResolved = TryCreateHandle(_settings.Ads.DensityVariableName, out _densityHandle);

        for (int i = 0; i < _ejectorHandles.Length; i++)
        {
            _ejectorResolved[i] = TryCreateHandle(
                $"{_settings.Ads.EjectorVariablePrefix}{i + 1}",
                out _ejectorHandles[i]);
        }
    }

    private bool TryCreateHandle(string symbolName, out uint handle)
    {
        try
        {
            handle = _client.CreateVariableHandle(symbolName);
            return true;
        }
        catch (Exception ex)
        {
            handle = 0;
            _logger.LogWarning("ADS-Symbol {Symbol} nicht gefunden ({Reason}) — wird übersprungen.",
                symbolName, ex.Message);
            return false;
        }
    }

    public double ReadEncoderMm()
    {
        if (!_connected || !_encoderResolved) return 0;
        try
        {
            return _client.ReadAny<double>(_encoderHandle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ADS-Encoder-Read fehlgeschlagen.");
            return 0;
        }
    }

    public void SetEjector(int laneOneBased, bool open)
    {
        if (laneOneBased < 1 || laneOneBased > _ejectorHandles.Length)
            throw new ArgumentOutOfRangeException(nameof(laneOneBased));
        if (!_connected || !_ejectorResolved[laneOneBased - 1]) return;
        try
        {
            _client.WriteAny(_ejectorHandles[laneOneBased - 1], open);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ADS-Ejector-Write Bahn {Lane} fehlgeschlagen.", laneOneBased);
        }
    }

    public void SetConveyorSpeed(double mmPerSec)
    {
        if (!_connected || !_speedResolved) return;
        try { _client.WriteAny(_speedHandle, mmPerSec); }
        catch (Exception ex) { _logger.LogWarning(ex, "ADS-Speed-Write fehlgeschlagen."); }
    }

    public void SetRequestedDensity(double density)
    {
        if (!_connected || !_densityResolved) return;
        try { _client.WriteAny(_densityHandle, density); }
        catch (Exception ex) { _logger.LogWarning(ex, "ADS-Density-Write fehlgeschlagen."); }
    }

    public Task DisconnectAsync()
    {
        if (!_connected) return Task.CompletedTask;
        try { _client.Disconnect(); } catch { /* ignore */ }
        _connected = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _client.Dispose(); } catch { /* ignore */ }
    }
}
