using System;
using System.Threading.Tasks;

namespace KKTD_V3.Services.Camera;

/// <summary>
/// Abstraktion über IDS-Kamera (Online) und BMP-Quelle (Offline).
/// </summary>
public interface ICameraService : IDisposable
{
    bool IsRunning { get; }
    event EventHandler<FrameEventArgs>? FrameReceived;

    Task ConnectAsync();
    Task DisconnectAsync();
    Task StartAsync();
    Task StopAsync();
    Task<byte[]?> CaptureSingleAsync();
}

public sealed class FrameEventArgs : EventArgs
{
    public byte[] Mono8Pixels { get; }
    public int Width { get; }
    public int Height { get; }
    public DateTime TimestampUtc { get; }
    public long FrameNumber { get; }

    public FrameEventArgs(byte[] mono8Pixels, int width, int height, long frameNumber)
    {
        Mono8Pixels = mono8Pixels;
        Width = width;
        Height = height;
        FrameNumber = frameNumber;
        TimestampUtc = DateTime.UtcNow;
    }
}
