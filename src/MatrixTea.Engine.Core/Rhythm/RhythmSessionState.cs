namespace MatrixTea.Engine.Core.Rhythm;

public sealed class RhythmSessionState
{
    public int Score { get; private set; }

    public int Combo { get; private set; }

    public int MaxCombo { get; private set; }

    public int HitCount { get; private set; }

    public int MissCount { get; private set; }

    public double Accuracy { get; private set; }

    public void Apply(RhythmJudgementResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Kind is < JudgementKind.Perfect or > JudgementKind.Miss || result.ScoreAwarded < 0 || !double.IsFinite(result.DeltaSeconds))
            throw new ArgumentException("Invalid judgement result.", nameof(result));
        if (result.CountsAsHit)
        {
            int score = checked(Score + result.ScoreAwarded), combo = checked(Combo + 1), hits = checked(HitCount + 1);
            Score = score; Combo = combo; HitCount = hits;
            if (Combo > MaxCombo)
            {
                MaxCombo = Combo;
            }
        }
        else
        {
            int misses = checked(MissCount + 1);
            Combo = 0; MissCount = misses;
        }

        long total = (long)HitCount + MissCount;
        Accuracy = total == 0 ? 0d : (double)HitCount / total;
    }
}
