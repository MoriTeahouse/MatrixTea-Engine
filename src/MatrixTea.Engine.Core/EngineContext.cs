namespace MatrixTea.Engine.Core;

/// <summary>
/// Runtime state shared by all scenes and services.
/// </summary>
public sealed class EngineContext
{
    private readonly Dictionary<Type, object> _services = new();

    public EngineContext(EngineOptions options, IEngineClock clock)
    {
        Options = options;
        Clock = clock;
        Metrics = new EngineMetrics();
        Services = new ServiceRegistry(_services);
    }

    public EngineOptions Options { get; }

    public IEngineClock Clock { get; }

    public EngineMetrics Metrics { get; }

    public ServiceRegistry Services { get; }
}