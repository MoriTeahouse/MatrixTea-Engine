// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

public sealed record RhythmReplay
{
    public string SongId { get; init; } = string.Empty;

    public string Difficulty { get; init; } = string.Empty;

    public string Player { get; init; } = "guest";

    public string PlayedAt { get; init; } = string.Empty;

    public int FinalScore { get; init; }

    public int MaxCombo { get; init; }

    public int Hit { get; init; }

    public int Miss { get; init; }

    public double Accuracy { get; init; }

    public string Grade { get; init; } = string.Empty;

    public List<RhythmReplayEvent> Events { get; init; } = new();
}
