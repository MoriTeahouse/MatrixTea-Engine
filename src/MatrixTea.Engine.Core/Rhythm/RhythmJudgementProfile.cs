namespace MatrixTea.Engine.Core.Rhythm;

public sealed record RhythmJudgementProfile
{
    public TimeSpan PerfectWindow { get; init; } = TimeSpan.FromSeconds(0.05);

    public TimeSpan GreatWindow { get; init; } = TimeSpan.FromSeconds(0.12);

    public TimeSpan GoodWindow { get; init; } = TimeSpan.FromSeconds(0.30);

    public static RhythmJudgementProfile RhythmClickerDefault { get; } = new();
}