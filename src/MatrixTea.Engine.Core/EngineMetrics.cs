namespace MatrixTea.Engine.Core;

public readonly record struct EngineMetricsSnapshot(long Frames, long Updates, long Renders,
    TimeSpan AverageFrameTime, TimeSpan MaximumFrameTime, TimeSpan LastUpdateDuration,
    TimeSpan LastRenderDuration, TimeSpan DroppedSimulationTime, long OverBudgetFrames);

/// <summary>Allocation-free counters and a 120-frame rolling timing window.</summary>
public sealed class EngineMetrics
{
    private readonly double[] _frameWindow = new double[120];
    private int _position, _samples;
    private double _windowMilliseconds;
    public long FrameCount { get; private set; }
    public long UpdateCount { get; private set; }
    public long RenderCount { get; private set; }
    public TimeSpan TotalFrameTime { get; private set; }
    public TimeSpan TotalUpdateTime { get; private set; }
    public TimeSpan MaximumFrameTime { get; private set; }
    public TimeSpan LastUpdateDuration { get; private set; }
    public TimeSpan LastRenderDuration { get; private set; }
    public TimeSpan DroppedSimulationTime { get; private set; }
    public long OverBudgetFrames { get; private set; }
    public TimeSpan AverageFrameTime
    {
        get
        {
            if (_samples == 0) return TimeSpan.Zero;
            double ticks = Math.Max(0, _windowMilliseconds / _samples * TimeSpan.TicksPerMillisecond);
            return ticks >= long.MaxValue ? TimeSpan.MaxValue : TimeSpan.FromTicks((long)ticks);
        }
    }
    public void RecordFrame(TimeSpan frameTime)
    {
        if (frameTime < TimeSpan.Zero) frameTime = TimeSpan.Zero;
        FrameCount++;
        TotalFrameTime = TimeSpan.FromTicks(FramePacer.SaturatingAdd(TotalFrameTime.Ticks, frameTime.Ticks));
        if (frameTime > MaximumFrameTime) MaximumFrameTime = frameTime;
        _windowMilliseconds -= _frameWindow[_position];
        _frameWindow[_position] = frameTime.TotalMilliseconds;
        _windowMilliseconds += frameTime.TotalMilliseconds;
        _position = (_position + 1) % _frameWindow.Length;
        _samples = Math.Min(_samples + 1, _frameWindow.Length);
    }
    public void RecordUpdate(TimeSpan step) => RecordUpdate(step, TimeSpan.Zero);
    public void RecordUpdate(TimeSpan step, TimeSpan duration)
    {
        if (step < TimeSpan.Zero || duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(step));
        UpdateCount++;
        TotalUpdateTime = TimeSpan.FromTicks(FramePacer.SaturatingAdd(TotalUpdateTime.Ticks, step.Ticks));
        LastUpdateDuration = duration;
    }
    public void RecordRender() => RecordRender(TimeSpan.Zero);
    public void RecordRender(TimeSpan duration) { if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration)); RenderCount++; LastRenderDuration = duration; }
    public void RecordPacing(TimeSpan dropped)
    {
        if (dropped <= TimeSpan.Zero) return;
        OverBudgetFrames++;
        DroppedSimulationTime = TimeSpan.FromTicks(FramePacer.SaturatingAdd(DroppedSimulationTime.Ticks, dropped.Ticks));
    }
    public EngineMetricsSnapshot Snapshot() => new(FrameCount, UpdateCount, RenderCount, AverageFrameTime,
        MaximumFrameTime, LastUpdateDuration, LastRenderDuration, DroppedSimulationTime, OverBudgetFrames);
}
