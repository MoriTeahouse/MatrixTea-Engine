// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using NAudio.Wave;
using NAudio;

namespace MatrixTea.Engine.Desktop;

/// <summary>Windows streaming playback with device-reported byte position; no decoded-ahead reader clock.</summary>
public sealed class DeviceAudioPlayer : IDisposable
{
    private AudioFileReader? _reader;
    private WaveOutEvent? _output;
    private double _frozenPosition;
    private bool _failed;
    public bool IsAvailable => _output != null && !_failed;
    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => _output?.PlaybackState == PlaybackState.Paused;
    public string? Error { get; private set; }
    public double DurationSeconds => _reader?.TotalTime.TotalSeconds ?? 0;
    public float Volume { get => _reader?.Volume ?? 0; set { if (_reader != null) _reader.Volume = float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0; } }
    public double PositionSeconds
    {
        get
        {
            if (_output == null || _reader == null) return _frozenPosition;
            try { return _frozenPosition = Math.Clamp(_output.GetPosition() / (double)_reader.WaveFormat.AverageBytesPerSecond, 0, DurationSeconds); }
            catch (MmException error) { Error=error.Message; _failed=true; return _frozenPosition; }
        }
    }
    public bool Load(string path, int latencyMs = 45)
    {
        Dispose(); Error = null; _frozenPosition = 0; _failed=false;
        try
        {
            _reader = new AudioFileReader(path);
            _output = new WaveOutEvent { DesiredLatency = Math.Clamp(latencyMs, 30, 200), NumberOfBuffers = 3 };
            _output.Init(_reader); return true;
        }
        catch (Exception ex) when (ex is MmException or IOException or InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        { Dispose(); Error = ex.Message; return false; }
    }
    public void Play() { Control(() => _output?.Play()); }
    public void Pause() { _frozenPosition = PositionSeconds; Control(() => _output?.Pause()); }
    public void Resume() { Control(() => _output?.Play()); }
    public void Stop() { _frozenPosition = PositionSeconds; Control(() => _output?.Stop()); }
    private void Control(Action action)
    {
        try { action(); }
        catch(Exception error) when(error is MmException or InvalidOperationException)
        { Error=error.Message; _failed=true; }
    }
    public void Dispose()
    {
        var output = _output; _output = null;
        try { output?.Stop(); } catch(MmException error) { Error=error.Message; }
        finally { try { output?.Dispose(); } finally { _reader?.Dispose(); _reader = null; } }
    }
}
