using System;
using System.Threading.Tasks;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;
using TwinCAT.Ads;

namespace KKTD_V3.Services;

/// <summary>
/// Lokale TwinCAT-ADS-Verbindung: Encoder lesen, Ausschleuser/Geschwindigkeit/Dichte schreiben.
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

    public AdsService(AppSettings settings, ILogger<AdsService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsConnected => _connected;

    public void Connect()
    {
        if (_connected) return;

        var amsId = AmsNetId.Parse(_settings.Ads.AmsNetId);
        _client.Connect(amsId, _settings.Ads.Port);

        _encoderHandle = _client.CreateVariableHandle(_settings.Ads.EncoderVariableName);
        _speedHandle = _client.CreateVariableHandle(_settings.Ads.SpeedVariableName);
        _densityHandle = _client.CreateVariableHandle(_settings.Ads.DensityVariableName);
        for (int i = 0; i < _ejectorHandles.Length; i++)
        {
            _ejectorHandles[i] = _client.CreateVariableHandle($"{_settings.Ads.EjectorVariablePrefix}{i + 1}");
        }

        _connected = true;
        _logger.LogInformation("ADS verbunden — {AmsNetId}:{Port}.", _settings.Ads.AmsNetId, _settings.Ads.Port);
    }

    public double ReadEncoderMm()
    {
        EnsureConnected();
        return _client.ReadAny<double>(_encoderHandle);
    }

    public void SetEjector(int laneOneBased, bool open)
    {
        EnsureConnected();
        if (laneOneBased < 1 || laneOneBased > _ejectorHandles.Length)
            throw new ArgumentOutOfRangeException(nameof(laneOneBased));
        _client.WriteAny(_ejectorHandles[laneOneBased - 1], open);
    }

    public void SetConveyorSpeed(double mmPerSec)
    {
        EnsureConnected();
        _client.WriteAny(_speedHandle, mmPerSec);
    }

    public void SetRequestedDensity(double density)
    {
        EnsureConnected();
        _client.WriteAny(_densityHandle, density);
    }

    public Task DisconnectAsync()
    {
        if (!_connected) return Task.CompletedTask;
        try { _client.Disconnect(); } catch { /* ignore */ }
        _connected = false;
        return Task.CompletedTask;
    }

    private void EnsureConnected()
    {
        if (!_connected)
            throw new InvalidOperationException("ADS nicht verbunden. Connect() zuerst aufrufen.");
    }

    public void Dispose()
    {
        try { _client.Dispose(); } catch { /* ignore */ }
    }
}
