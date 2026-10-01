using System.Diagnostics;

namespace MatrixTea.Engine.Core;

/// <summary>
/// Small runtime shell that owns the clock, scene stack, and pacing policy.
/// </summary>
public sealed class EngineApplication : IDisposable
{
    private readonly FramePacer _framePacer;
    private bool _disposed;

    public EngineApplication(EngineOptions options, IEngineClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        Context = new EngineContext(options, clock ?? (options.UseHighResolutionTiming ? new HighResolutionEngineClock() : new SystemEngineClock()));
        Scenes = new SceneStack();
        _framePacer = new FramePacer(options.TargetUpdateStep, options.MaxFrameDelta);
    }

    public EngineContext Context { get; }

    public SceneStack Scenes { get; }

    public double Interpolation => _framePacer.Interpolation;

    public int Pump(TimeSpan frameDelta)
    {
        ThrowIfDisposed();
        Context.Metrics.RecordFrame(frameDelta);
        _framePacer.Add(frameDelta);
        int count = _framePacer.Consume(Context.Options.MaxUpdatesPerFrame);
        Context.Metrics.RecordPacing(_framePacer.LastDroppedTime);
        return count;
    }

    public void Update(TimeSpan step)
    {
        ThrowIfDisposed();
        if (step < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(step));
        long started = Stopwatch.GetTimestamp();
        Scenes.Update(Context, step);
        Context.Metrics.RecordUpdate(step, Duration(started));
    }

    public void Render(IRenderSurface surface)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(surface);
        long started = Stopwatch.GetTimestamp();
        Scenes.Render(Context, surface);
        Context.Metrics.RecordRender(Duration(started));
    }

    public void ResetTiming() { ThrowIfDisposed(); _framePacer.Reset(); Context.Clock.Capture(); }
    public void Dispose()
    {
        if (_disposed) return;
        if (Scenes.IsDispatching) throw new InvalidOperationException("Engine disposal must occur outside scene callbacks.");
        _disposed = true;
        var errors = new List<Exception>();
        try { Scenes.Shutdown(Context); } catch (Exception ex) { errors.Add(ex); }
        try { Context.Services.Dispose(); } catch (Exception ex) { errors.Add(ex); }
        if (errors.Count > 0) throw new AggregateException("Engine shutdown failed.", errors);
    }
    private static TimeSpan Duration(long started) => TimeSpan.FromTicks((long)((Stopwatch.GetTimestamp() - started) * (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(EngineApplication)); }
}
