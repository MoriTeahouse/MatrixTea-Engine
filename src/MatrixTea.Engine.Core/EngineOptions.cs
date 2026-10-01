namespace MatrixTea.Engine.Core;

/// <summary>
/// Shared engine settings used by both the core runtime and host adapters.
/// </summary>
public sealed record EngineOptions
{
	public string Title { get; init; } = "MatrixTea Engine";

	public TimeSpan TargetUpdateStep { get; init; } = TimeSpan.FromSeconds(1d / 120d);

	public int MaxUpdatesPerFrame { get; init; } = 4;

	public bool UseHighResolutionTiming { get; init; } = true;

	public static EngineOptions Create(string title) => new() { Title = title };
}
