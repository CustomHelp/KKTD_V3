using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Ejector;

/// <summary>
/// Encoder-Watcher-Task: alle 5 ms Encoder lesen, Queue prüfen,
/// Ausschleuser via ADS öffnen/schließen. Höchste Priorität — niemals blockieren.
/// </summary>
public sealed class EjectorService : IDisposable
{
    private readonly AdsService _ads;
    private readonly ILogger<EjectorService> _logger;
    private readonly ConcurrentQueue<EjectorQueueEntry>[] _queuesPerLane;
    private CancellationTokenSource? _cts;
    private Task? _watcherTask;

    public EjectorService(AdsService ads, ILogger<EjectorService> logger)
    {
        _ads = ads;
        _logger = logger;
        _queuesPerLane = new ConcurrentQueue<EjectorQueueEntry>[6];
        for (int i = 0; i < 6; i++)
            _queuesPerLane[i] = new ConcurrentQueue<EjectorQueueEntry>();
    }

    public void Enqueue(EjectorQueueEntry entry)
    {
        if (entry.Lane is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(entry));
        _queuesPerLane[entry.Lane - 1].Enqueue(entry);
    }

    public void Start()
    {
        if (_watcherTask is not null) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _watcherTask = Task.Factory.StartNew(() =>
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                Thread.CurrentThread.Name = "Encoder-Watcher";
            }
            catch { /* ignore */ }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_ads.IsConnected)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    var encoderNow = _ads.ReadEncoderMm();

                    for (int laneIdx = 0; laneIdx < _queuesPerLane.Length; laneIdx++)
                    {
                        var queue = _queuesPerLane[laneIdx];
                        // Snapshot über ToArray, Reihenfolge der Queue bleibt erhalten
                        var items = queue.ToArray();
                        foreach (var entry in items)
                        {
                            if (!entry.IsOpen && encoderNow >= entry.EncoderOpenMm)
                            {
                                _ads.SetEjector(entry.Lane, true);
                                entry.IsOpen = true;
                            }
                            if (entry.IsOpen && encoderNow >= entry.EncoderCloseMm)
                            {
                                _ads.SetEjector(entry.Lane, false);
                                // ältester Eintrag entfernen, falls dieser jetzt closed ist
                                queue.TryDequeue(out _);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Encoder-Watcher Fehler.");
                }

                Thread.Sleep(5);
            }
        }, TaskCreationOptions.LongRunning);

        _logger.LogInformation("EjectorService gestartet.");
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        return _watcherTask ?? Task.CompletedTask;
    }

    public void Dispose()
    {
        try { StopAsync().Wait(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
        _cts?.Dispose();
    }
}
