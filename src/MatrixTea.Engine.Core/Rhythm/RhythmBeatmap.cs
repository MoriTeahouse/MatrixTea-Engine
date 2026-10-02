// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

/// <summary>
/// Shared beatmap contract that matches RhythmClicker JSON with minimal conversion.
/// </summary>
public sealed record RhythmBeatmap
{
    public string Name { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public string AudioFile { get; init; } = string.Empty;

    public float Bpm { get; init; }

    public List<BeatmapNote> Notes { get; init; } = new();

    public List<BeatmapBreakPeriod> Breaks { get; init; } = new();
}
