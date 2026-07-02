namespace MatrixTea.Engine.Core.Rhythm;

/// <summary>
/// Owns the runtime note queue for a single rhythm playthrough.
/// </summary>
public sealed class RhythmPlaySession<TNote>
    where TNote : class
{
    private readonly LinkedList<TNote> _notes;
    private readonly Func<TNote, double> _timeSelector;
    private readonly Func<TNote, int> _columnSelector;
    private readonly RhythmJudgementEngine _judgementEngine;
    private readonly RhythmSessionState _state = new();
    private readonly double _missThresholdSeconds;

    public RhythmPlaySession(
        IEnumerable<TNote> notes,
        Func<TNote, double> timeSelector,
        Func<TNote, int> columnSelector,
        RhythmJudgementProfile? judgementProfile = null,
        RhythmScoringProfile? scoringProfile = null,
        double missThresholdSeconds = 0.75)
    {
        _notes = new LinkedList<TNote>(notes);
        _timeSelector = timeSelector;
        _columnSelector = columnSelector;
        _judgementEngine = new RhythmJudgementEngine(judgementProfile, scoringProfile);
        _missThresholdSeconds = Math.Max(missThresholdSeconds, _judgementEngine.Profile.GoodWindow.TotalSeconds);
    }

    public LinkedList<TNote> Notes => _notes;

    public RhythmSessionState State => _state;

    public int Score => _state.Score;

    public int Combo => _state.Combo;

    public int MaxCombo => _state.MaxCombo;

    public int HitCount => _state.HitCount;

    public int MissCount => _state.MissCount;

    public int TotalNotes => _notes.Count + _state.HitCount + _state.MissCount;

    public int MaxScore => TotalNotes * _judgementEngine.Scoring.PerfectScore;

    public bool IsComplete => _notes.Count == 0;

    public RhythmPlayEvent<TNote>? TryHit(double timeSeconds, int column)
    {
        LinkedListNode<TNote>? bestNode = null;
        double bestDelta = double.MaxValue;

        for (LinkedListNode<TNote>? node = _notes.First; node is not null; node = node.Next)
        {
            TNote note = node.Value;
            if (_columnSelector(note) != column)
            {
                continue;
            }

            double delta = Math.Abs(_timeSelector(note) - timeSeconds);
            if (delta <= _judgementEngine.Profile.GoodWindow.TotalSeconds && delta < bestDelta)
            {
                bestDelta = delta;
                bestNode = node;
            }
        }

        if (bestNode is null)
        {
            return null;
        }

        TNote bestNote = bestNode.Value;
        _notes.Remove(bestNode);
        RhythmJudgementResult judgment = _judgementEngine.Judge(_timeSelector(bestNote), timeSeconds);
        _state.Apply(judgment);

        return new RhythmPlayEvent<TNote>
        {
            Note = bestNote,
            Judgment = judgment,
            Column = column,
            NoteTimeSeconds = _timeSelector(bestNote),
        };
    }

    public IReadOnlyList<RhythmPlayEvent<TNote>> CollectMisses(double timeSeconds)
    {
        List<RhythmPlayEvent<TNote>> misses = new();

        for (LinkedListNode<TNote>? node = _notes.First; node is not null;)
        {
            LinkedListNode<TNote>? next = node.Next;
            TNote note = node.Value;
            double noteTime = _timeSelector(note);

            if (timeSeconds - noteTime > _missThresholdSeconds)
            {
                _notes.Remove(node);
                RhythmJudgementResult judgment = new()
                {
                    Kind = JudgementKind.Miss,
                    DeltaSeconds = timeSeconds - noteTime,
                    ScoreAwarded = 0,
                };
                _state.Apply(judgment);
                misses.Add(new RhythmPlayEvent<TNote>
                {
                    Note = note,
                    Judgment = judgment,
                    Column = _columnSelector(note),
                    NoteTimeSeconds = noteTime,
                });
            }

            node = next;
        }

        return misses;
    }
}