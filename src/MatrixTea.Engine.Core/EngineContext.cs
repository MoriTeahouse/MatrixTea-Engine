// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core;

/// <summary>
/// Runtime state shared by all scenes and services.
/// </summary>
public sealed class EngineContext
{
    private readonly Dictionary<Type, object> _services = new();

    public EngineContext(EngineOptions options, IEngineClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        options.Validate();
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
