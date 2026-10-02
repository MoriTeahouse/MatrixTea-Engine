// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

public sealed record RhythmPlayEvent<TNote>
    where TNote : class
{
    public TNote Note { get; init; } = default!;

    public RhythmJudgementResult Judgment { get; init; } = default!;

    public int Column { get; init; }

    public double NoteTimeSeconds { get; init; }

    public bool IsHit => Judgment.CountsAsHit;
}
