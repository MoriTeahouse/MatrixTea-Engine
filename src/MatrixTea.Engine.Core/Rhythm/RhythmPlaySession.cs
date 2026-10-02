// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

/// <summary>Per-column time indexes with a mutable-list compatibility path.</summary>
public sealed class RhythmPlaySession<TNote> where TNote : class
{
    private readonly LinkedList<TNote> _notes;
    private readonly Func<TNote, double> _timeSelector;
    private readonly Func<TNote, int> _columnSelector;
    private readonly RhythmJudgementEngine _judgementEngine;
    private readonly RhythmSessionState _state = new();
    private readonly double _missThresholdSeconds;
    private readonly Dictionary<int, List<Entry>> _columns = new();
    private readonly SortedSet<Entry> _timeline = new(EntryComparer.Instance);
    private bool _indexed;
    private readonly record struct Entry(double Time, long Sequence, int Column, LinkedListNode<TNote>? Node);
    private sealed class EntryComparer : IComparer<Entry>
    {
        public static readonly EntryComparer Instance = new();
        public int Compare(Entry left, Entry right)
        { int time = left.Time.CompareTo(right.Time); return time != 0 ? time : left.Sequence.CompareTo(right.Sequence); }
    }

    public RhythmPlaySession(IEnumerable<TNote> notes, Func<TNote, double> timeSelector,
        Func<TNote, int> columnSelector, RhythmJudgementProfile? judgementProfile = null,
        RhythmScoringProfile? scoringProfile = null, double missThresholdSeconds = 0.75)
        : this(notes, timeSelector, columnSelector, true, judgementProfile, scoringProfile, missThresholdSeconds) { }

    public RhythmPlaySession(IEnumerable<TNote> notes, Func<TNote, double> timeSelector,
        Func<TNote, int> columnSelector, bool enableIndexedLookup,
        RhythmJudgementProfile? judgementProfile = null, RhythmScoringProfile? scoringProfile = null,
        double missThresholdSeconds = 0.75)
    {
        ArgumentNullException.ThrowIfNull(notes); ArgumentNullException.ThrowIfNull(timeSelector);
        ArgumentNullException.ThrowIfNull(columnSelector);
        if (!double.IsFinite(missThresholdSeconds) || missThresholdSeconds < 0) throw new ArgumentOutOfRangeException(nameof(missThresholdSeconds));
        _timeSelector = timeSelector; _columnSelector = columnSelector;
        _judgementEngine = new RhythmJudgementEngine(judgementProfile, scoringProfile);
        _missThresholdSeconds = Math.Max(missThresholdSeconds, _judgementEngine.Profile.GoodWindow.TotalSeconds);
        _notes = new LinkedList<TNote>(); _indexed = enableIndexedLookup; long sequence = 0;
        foreach (var note in notes)
        {
            ArgumentNullException.ThrowIfNull(note);
            double time = timeSelector(note); int column = columnSelector(note);
            if (!double.IsFinite(time)) throw new ArgumentException("Note times must be finite.", nameof(notes));
            var node = _notes.AddLast(note);
            if (_indexed)
            {
                var entry = new Entry(time, sequence++, column, node);
                if (!_columns.TryGetValue(column, out var entries)) _columns.Add(column, entries = new());
                entries.Add(entry); _timeline.Add(entry);
            }
        }
        foreach (var entries in _columns.Values) entries.Sort(EntryComparer.Instance);
        _ = MaxScore; // Fail before play starts if the configured total score cannot be represented.
    }

    /// <summary>Accessing the mutable legacy list permanently disables time indexes.</summary>
    public LinkedList<TNote> Notes
    {
        get { _indexed = false; _columns.Clear(); _timeline.Clear(); return _notes; }
    }
    /// <summary>Enumerates pending notes without exposing list mutation. Timing/column fields remain immutable during indexed play.</summary>
    public IEnumerable<TNote> RemainingNotes => EnumerateRemaining();
    private IEnumerable<TNote> EnumerateRemaining() { foreach (var note in _notes) yield return note; }
    public bool UsesIndexedLookup => _indexed;
    public RhythmSessionState State => _state;
    public int Score => _state.Score;
    public int Combo => _state.Combo;
    public int MaxCombo => _state.MaxCombo;
    public int HitCount => _state.HitCount;
    public int MissCount => _state.MissCount;
    public int TotalNotes => checked(_notes.Count + _state.HitCount + _state.MissCount);
    public int MaxScore => checked(TotalNotes * _judgementEngine.Scoring.PerfectScore);
    public bool IsComplete => _notes.Count == 0;

    public RhythmPlayEvent<TNote>? TryHit(double timeSeconds, int column)
    {
        ValidateTime(timeSeconds);
        LinkedListNode<TNote>? bestNode = null; Entry best = default;
        double bestDelta = double.MaxValue;
        if (_indexed)
        {
            if (!_columns.TryGetValue(column, out var entries) || entries.Count == 0) return null;
            double window = _judgementEngine.Profile.GoodWindow.TotalSeconds;
            double lower = Math.BitDecrement(timeSeconds - window), upper = Math.BitIncrement(timeSeconds + window);
            int start = 0, end = entries.Count;
            while (start < end)
            {
                int middle = start + (end - start) / 2;
                if (entries[middle].Time < lower) start = middle + 1; else end = middle;
            }
            for (int index = start; index < entries.Count && entries[index].Time <= upper; index++)
            {
                var entry = entries[index];
                if (entry.Node!.List != _notes) continue;
                double delta = Math.Abs(entry.Time - timeSeconds);
                if (delta <= window && (delta < bestDelta || (delta == bestDelta && entry.Sequence < best.Sequence)))
                { bestDelta = delta; bestNode = entry.Node; best = entry; }
            }
        }
        else
        {
            for (var node = _notes.First; node != null; node = node.Next)
            {
                if (_columnSelector(node.Value) != column) continue;
                double delta = Math.Abs(_timeSelector(node.Value) - timeSeconds);
                if (delta <= _judgementEngine.Profile.GoodWindow.TotalSeconds && delta < bestDelta)
                { bestDelta = delta; bestNode = node; }
            }
        }
        if (bestNode == null) return null;
        double noteTime = _indexed ? best.Time : _timeSelector(bestNode.Value);
        var judgment = _judgementEngine.Judge(noteTime, timeSeconds);
        _state.Apply(judgment);
        var result = new RhythmPlayEvent<TNote> { Note = bestNode.Value, Judgment = judgment, Column = column, NoteTimeSeconds = noteTime };
        if (_indexed) Remove(best); else _notes.Remove(bestNode);
        return result;
    }

    public IReadOnlyList<RhythmPlayEvent<TNote>> CollectMisses(double timeSeconds)
    {
        ValidateTime(timeSeconds);
        List<RhythmPlayEvent<TNote>>? misses = null;
        if (_indexed)
        {
            while (_timeline.Count > 0)
            {
                var entry = _timeline.Min;
                if (!(timeSeconds - entry.Time > _missThresholdSeconds)) break;
                var result = Miss(entry.Node!.Value, entry.Time, entry.Column, timeSeconds);
                Remove(entry); (misses ??= new()).Add(result);
            }
        }
        else
        {
            for (var node = _notes.First; node != null;)
            {
                var next = node.Next; double time = _timeSelector(node.Value);
                if (timeSeconds - time > _missThresholdSeconds)
                {
                    var result = Miss(node.Value, time, _columnSelector(node.Value), timeSeconds);
                    _notes.Remove(node); (misses ??= new()).Add(result);
                }
                node = next;
            }
        }
        return misses ?? (IReadOnlyList<RhythmPlayEvent<TNote>>)Array.Empty<RhythmPlayEvent<TNote>>();
    }
    private RhythmPlayEvent<TNote> Miss(TNote note, double noteTime, int column, double time)
    {
        var judgment = new RhythmJudgementResult { Kind = JudgementKind.Miss, DeltaSeconds = time - noteTime, ScoreAwarded = 0 };
        _state.Apply(judgment);
        return new RhythmPlayEvent<TNote> { Note = note, Judgment = judgment, Column = column, NoteTimeSeconds = noteTime };
    }
    private void Remove(Entry entry) { _notes.Remove(entry.Node!); _timeline.Remove(entry); }
    private static void ValidateTime(double time) { if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time)); }
}
