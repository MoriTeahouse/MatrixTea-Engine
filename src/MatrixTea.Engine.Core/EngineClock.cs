// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.Diagnostics;

namespace MatrixTea.Engine.Core;

public interface IEngineClock
{
    TimeSpan Elapsed { get; }

    TimeSpan Delta { get; }

    long Tick { get; }
    long TickFrequency => TimeSpan.TicksPerSecond;

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
    public long TickFrequency => Stopwatch.Frequency;

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

/// <summary>Monotonic millisecond timing for hosts opting out of high-resolution capture.</summary>
public sealed class SystemEngineClock : IEngineClock
{
    private long _origin = Environment.TickCount64;
    private TimeSpan _lastCapture;
    public TimeSpan Elapsed => TimeSpan.FromMilliseconds(Environment.TickCount64 - _origin);
    public TimeSpan Delta { get; private set; }
    public long Tick => Elapsed.Ticks;
    public long TickFrequency => TimeSpan.TicksPerSecond;
    public void Reset() { _origin = Environment.TickCount64; _lastCapture = Delta = TimeSpan.Zero; }
    public TimeSpan Capture() { TimeSpan now = Elapsed; Delta = now - _lastCapture; _lastCapture = now; return Delta; }
}
