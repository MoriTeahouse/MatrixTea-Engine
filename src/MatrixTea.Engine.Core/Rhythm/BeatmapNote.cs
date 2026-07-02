namespace MatrixTea.Engine.Core.Rhythm;

public sealed record BeatmapNote
{
    public double Time { get; init; }

    public int Column { get; init; }
}