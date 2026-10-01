namespace MatrixTea.Engine.Core.Rhythm;

/// <summary>
/// Evaluates note timing using the same judgments RhythmClicker expects.
/// </summary>
public sealed class RhythmJudgementEngine
{
    public RhythmJudgementEngine(
        RhythmJudgementProfile? profile = null,
        RhythmScoringProfile? scoring = null)
    {
        Profile = profile ?? RhythmJudgementProfile.RhythmClickerDefault;
        Scoring = scoring ?? new RhythmScoringProfile();
        if (Profile.PerfectWindow < TimeSpan.Zero || Profile.GreatWindow < Profile.PerfectWindow || Profile.GoodWindow < Profile.GreatWindow)
            throw new ArgumentException("Judgement windows must be nonnegative and ordered Perfect <= Great <= Good.", nameof(profile));
        if (Scoring.PerfectScore < 0 || Scoring.GreatScore < 0 || Scoring.GoodScore < 0)
            throw new ArgumentException("Score awards must be nonnegative.", nameof(scoring));
    }

    public RhythmJudgementProfile Profile { get; }

    public RhythmScoringProfile Scoring { get; }

    public RhythmJudgementResult Judge(double noteTimeSeconds, double inputTimeSeconds)
    {
        if (!double.IsFinite(noteTimeSeconds)) throw new ArgumentOutOfRangeException(nameof(noteTimeSeconds));
        if (!double.IsFinite(inputTimeSeconds)) throw new ArgumentOutOfRangeException(nameof(inputTimeSeconds));
        double deltaSeconds = inputTimeSeconds - noteTimeSeconds;
        double absoluteDelta = Math.Abs(deltaSeconds);

        if (absoluteDelta <= Profile.PerfectWindow.TotalSeconds)
        {
            return new RhythmJudgementResult
            {
                Kind = JudgementKind.Perfect,
                DeltaSeconds = deltaSeconds,
                ScoreAwarded = Scoring.PerfectScore
            };
        }

        if (absoluteDelta <= Profile.GreatWindow.TotalSeconds)
        {
            return new RhythmJudgementResult
            {
                Kind = JudgementKind.Great,
                DeltaSeconds = deltaSeconds,
                ScoreAwarded = Scoring.GreatScore
            };
        }

        if (absoluteDelta <= Profile.GoodWindow.TotalSeconds)
        {
            return new RhythmJudgementResult
            {
                Kind = JudgementKind.Good,
                DeltaSeconds = deltaSeconds,
                ScoreAwarded = Scoring.GoodScore
            };
        }

        return new RhythmJudgementResult
        {
            Kind = JudgementKind.Miss,
            DeltaSeconds = deltaSeconds,
            ScoreAwarded = 0
        };
    }
}
