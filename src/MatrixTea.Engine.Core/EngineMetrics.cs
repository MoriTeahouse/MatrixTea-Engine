namespace MatrixTea.Engine.Core;

/// <summary>
/// Lightweight counters for profiling and diagnostics.
/// </summary>
public sealed class EngineMetrics
{
    public long FrameCount { get; private set; }

    public long UpdateCount { get; private set; }

    public long RenderCount { get; private set; }

    public TimeSpan TotalFrameTime { get; private set; }

    public TimeSpan TotalUpdateTime { get; private set; }

    public void RecordFrame(TimeSpan frameTime)
    {
        FrameCount++;
        TotalFrameTime += frameTime;
    }

    public void RecordUpdate(TimeSpan updateTime)
    {
        UpdateCount++;
        TotalUpdateTime += updateTime;
    }

    public void RecordRender()
    {
        RenderCount++;
    }
}