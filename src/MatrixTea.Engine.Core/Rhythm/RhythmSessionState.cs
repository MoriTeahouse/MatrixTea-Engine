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
        if (result.CountsAsHit)
        {
            Score += result.ScoreAwarded;
            Combo++;
            HitCount++;
            if (Combo > MaxCombo)
            {
                MaxCombo = Combo;
            }
        }
        else
        {
            Combo = 0;
            MissCount++;
        }

        int total = HitCount + MissCount;
        Accuracy = total == 0 ? 0d : (double)HitCount / total;
    }
}