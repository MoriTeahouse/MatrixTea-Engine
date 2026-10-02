// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core;

/// <summary>
/// Shared engine settings used by both the core runtime and host adapters.
/// </summary>
public sealed record EngineOptions
{
    public string Title { get; init; } = "MatrixTea Engine";

    public TimeSpan TargetUpdateStep { get; init; } = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 120);

    public int MaxUpdatesPerFrame { get; init; } = 4;

    public bool UseHighResolutionTiming { get; init; } = true;

    public TimeSpan MaxFrameDelta { get; init; } = TimeSpan.FromMilliseconds(250);
    public bool PauseWhenInactive { get; init; } = true;
    public bool SynchronizeWithVerticalRetrace { get; init; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title)) throw new ArgumentException("An engine title is required.", nameof(Title));
        if (TargetUpdateStep <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(TargetUpdateStep));
        if (MaxFrameDelta <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaxFrameDelta));
        if (MaxUpdatesPerFrame is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(MaxUpdatesPerFrame));
    }

    public static EngineOptions Create(string title) => new() { Title = title };
}
