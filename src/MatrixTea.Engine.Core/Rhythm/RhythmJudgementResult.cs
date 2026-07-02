namespace MatrixTea.Engine.Core.Rhythm;

public sealed record RhythmJudgementResult
{
    public JudgementKind Kind { get; init; }

    public double DeltaSeconds { get; init; }

    public int ScoreAwarded { get; init; }

    public bool CountsAsHit => Kind is JudgementKind.Perfect or JudgementKind.Great or JudgementKind.Good;
}