namespace MatrixTea.Engine.Core;

/// <summary>
/// Small runtime shell that owns the clock, scene stack, and pacing policy.
/// </summary>
public sealed class EngineApplication
{
    private readonly FramePacer _framePacer;

    public EngineApplication(EngineOptions options, IEngineClock? clock = null)
    {
        Context = new EngineContext(options, clock ?? new HighResolutionEngineClock());
        Scenes = new SceneStack();
        _framePacer = new FramePacer(options.TargetUpdateStep);
    }

    public EngineContext Context { get; }

    public SceneStack Scenes { get; }

    public double Interpolation => _framePacer.Interpolation;

    public int Pump(TimeSpan frameDelta)
    {
        Context.Metrics.RecordFrame(frameDelta);
        _framePacer.Add(frameDelta);
        return _framePacer.Consume(Context.Options.MaxUpdatesPerFrame);
    }

    public void Update(TimeSpan step)
    {
        Scenes.Update(Context, step);
        Context.Metrics.RecordUpdate(step);
    }

    public void Render(IRenderSurface surface)
    {
        Scenes.Render(Context, surface);
        Context.Metrics.RecordRender();
    }
}