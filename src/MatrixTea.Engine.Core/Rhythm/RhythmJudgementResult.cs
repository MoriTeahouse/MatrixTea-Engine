// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

public sealed record RhythmJudgementResult
{
    public JudgementKind Kind { get; init; }

    public double DeltaSeconds { get; init; }

    public int ScoreAwarded { get; init; }

    public bool CountsAsHit => Kind is JudgementKind.Perfect or JudgementKind.Great or JudgementKind.Good;
}
