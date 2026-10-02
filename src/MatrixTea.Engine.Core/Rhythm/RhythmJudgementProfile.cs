// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

public sealed record RhythmJudgementProfile
{
    public TimeSpan PerfectWindow { get; init; } = TimeSpan.FromSeconds(0.05);

    public TimeSpan GreatWindow { get; init; } = TimeSpan.FromSeconds(0.12);

    public TimeSpan GoodWindow { get; init; } = TimeSpan.FromSeconds(0.30);

    public static RhythmJudgementProfile RhythmClickerDefault { get; } = new();
}
