// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Input;

public readonly record struct ActionPress(int Action, double TimestampSeconds);

/// <summary>Captures host-frame edges and consumes each press once across fixed updates.</summary>
public sealed class ActionInputBuffer
{
    private readonly bool[] _down;
    private readonly ActionPress[] _queue;
    private int _head, _count;
    private double _lastTimestamp = double.NegativeInfinity;
    public ActionInputBuffer(int actionCount, int capacity = 128)
    {
        if (actionCount is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(actionCount));
        if (capacity < actionCount || capacity > 16384) throw new ArgumentOutOfRangeException(nameof(capacity));
        _down = new bool[actionCount]; _queue = new ActionPress[capacity];
    }
    public int ActionCount => _down.Length;
    public int PendingPresses => _count;
    public long DroppedPresses { get; private set; }
    public bool IsDown(int action) => _down[action];
    public void Capture(ReadOnlySpan<bool> down, double timestampSeconds)
    {
        Validate(down, timestampSeconds);
        for (int action = 0; action < down.Length; action++)
        {
            if (down[action] && !_down[action])
            {
                if (_count == _queue.Length) { _head = (_head + 1) % _queue.Length; _count--; DroppedPresses++; }
                _queue[(_head + _count++) % _queue.Length] = new(action, timestampSeconds);
            }
            _down[action] = down[action];
        }
        _lastTimestamp = timestampSeconds;
    }
    public bool TryReadPress(out ActionPress press)
    {
        if (_count == 0) { press = default; return false; }
        press = _queue[_head]; _head = (_head + 1) % _queue.Length; _count--; return true;
    }
    /// <summary>Clears pending edges and samples held keys without generating presses after focus changes.</summary>
    public void Synchronize(ReadOnlySpan<bool> down, double timestampSeconds)
    {
        Validate(down, timestampSeconds); down.CopyTo(_down); _head = _count = 0; _lastTimestamp = timestampSeconds;
    }
    private void Validate(ReadOnlySpan<bool> down, double timestamp)
    {
        if (down.Length != _down.Length) throw new ArgumentException("Input action count changed.", nameof(down));
        if (!double.IsFinite(timestamp) || timestamp < _lastTimestamp) throw new ArgumentOutOfRangeException(nameof(timestamp));
    }
}
