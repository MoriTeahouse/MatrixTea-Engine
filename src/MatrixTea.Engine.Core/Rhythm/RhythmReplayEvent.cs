namespace MatrixTea.Engine.Core.Rhythm;

public sealed record RhythmReplayEvent
{
    public double Time { get; init; }

    public int Column { get; init; }

    public string Judgement { get; init; } = string.Empty;

    public int ScoreGained { get; init; }

    public int ComboAt { get; init; }
}
