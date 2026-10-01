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
