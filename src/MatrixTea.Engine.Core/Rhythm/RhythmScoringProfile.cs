namespace MatrixTea.Engine.Core.Rhythm;

public sealed record RhythmScoringProfile
{
    public int PerfectScore { get; init; } = 100;

    public int GreatScore { get; init; } = 75;

    public int GoodScore { get; init; } = 50;
}
