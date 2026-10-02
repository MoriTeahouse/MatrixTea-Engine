// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

public sealed record RhythmScoringProfile
{
    public int PerfectScore { get; init; } = 100;

    public int GreatScore { get; init; } = 75;

    public int GoodScore { get; init; } = 50;
}
