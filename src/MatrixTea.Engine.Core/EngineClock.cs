using System.Diagnostics;

namespace MatrixTea.Engine.Core;

public interface IEngineClock
{
    TimeSpan Elapsed { get; }

    TimeSpan Delta { get; }

    long Tick { get; }

    void Reset();

    TimeSpan Capture();
}

/// <summary>
/// Stopwatch-backed high precision clock for rhythm gameplay.
/// </summary>
public sealed class HighResolutionEngineClock : IEngineClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _lastCapture;

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public TimeSpan Delta { get; private set; }

    public long Tick => _stopwatch.ElapsedTicks;

    public void Reset()
    {
        _stopwatch.Restart();
        _lastCapture = TimeSpan.Zero;
        Delta = TimeSpan.Zero;
    }

    public TimeSpan Capture()
    {
        TimeSpan now = _stopwatch.Elapsed;
        Delta = now - _lastCapture;
        _lastCapture = now;
        return Delta;
    }
}