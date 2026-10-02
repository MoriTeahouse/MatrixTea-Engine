// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

/// <summary>Maps a monotonic host clock to song time, with explicit pause, seek and audio corrections.</summary>
public sealed class PlaybackTimeline
{
    private double _position, _anchor;
    private double _lastRead;
    public bool IsPlaying { get; private set; }
    public double Rate { get; private set; } = 1;
    public double Position(double monotonicSeconds)
    {
        Validate(monotonicSeconds);
        if (monotonicSeconds < _anchor) throw new ArgumentOutOfRangeException(nameof(monotonicSeconds));
        double position = _position + (IsPlaying ? (monotonicSeconds - _anchor) * Rate : 0);
        if (!double.IsFinite(position)) throw new ArgumentOutOfRangeException(nameof(monotonicSeconds));
        _lastRead = Math.Max(_lastRead, position);
        return _lastRead;
    }
    public void Start(double monotonicSeconds, double songSeconds = 0, double rate = 1)
    {
        Validate(monotonicSeconds); Validate(songSeconds);
        if (!double.IsFinite(rate) || rate <= 0 || rate > 4) throw new ArgumentOutOfRangeException(nameof(rate));
        _position = _lastRead = songSeconds; _anchor = monotonicSeconds; Rate = rate; IsPlaying = true;
    }
    public void Pause(double monotonicSeconds) { _position = Position(monotonicSeconds); _anchor = monotonicSeconds; IsPlaying = false; }
    public void Resume(double monotonicSeconds) { Validate(monotonicSeconds); if (monotonicSeconds < _anchor) throw new ArgumentOutOfRangeException(nameof(monotonicSeconds)); _anchor = monotonicSeconds; IsPlaying = true; }
    public void Seek(double songSeconds, double monotonicSeconds) { Validate(songSeconds); Validate(monotonicSeconds); if (monotonicSeconds < _anchor) throw new ArgumentOutOfRangeException(nameof(monotonicSeconds)); _position = _lastRead = songSeconds; _anchor = monotonicSeconds; }
    /// <summary>Reanchors to the audio device playhead. Used only while playing; seek is explicit.</summary>
    public void ObserveAudio(double songSeconds, double monotonicSeconds)
    {
        if (!IsPlaying) return;
        Validate(songSeconds); double predicted = Position(monotonicSeconds);
        // Smooth small device corrections; Position never moves backwards until an explicit Seek/Start.
        _position = Math.Clamp(songSeconds, predicted - 0.02, predicted + 0.02); _anchor = monotonicSeconds;
    }
    public static double JudgementTime(double songSeconds, int offsetMs) => songSeconds + Math.Clamp(offsetMs, -500, 500) / 1000d;
    private static void Validate(double value) { if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
}
