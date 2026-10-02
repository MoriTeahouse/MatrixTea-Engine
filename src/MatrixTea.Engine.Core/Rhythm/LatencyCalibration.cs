// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

/// <summary>Median tap error with warm-up and outlier rejection. Positive late taps yield negative input offsets.</summary>
public sealed class LatencyCalibration
{
    private readonly List<double> _errors = new();
    private int _warmup;
    public int Samples => _errors.Count;
    public bool IsReady => Samples >= 8;
    public bool AddTap(double tapSeconds, double beatSeconds)
    {
        if (!double.IsFinite(tapSeconds) || !double.IsFinite(beatSeconds)) throw new ArgumentOutOfRangeException(nameof(tapSeconds));
        double error = tapSeconds - beatSeconds;
        if (_warmup++ < 2 || Math.Abs(error) > 0.35 || _errors.Count >= 32) return false;
        _errors.Add(error); return true;
    }
    public int RecommendedOffsetMs
    {
        get
        {
            if (!IsReady) throw new InvalidOperationException("At least eight accepted taps are required.");
            double[] sorted = _errors.ToArray(); Array.Sort(sorted); int middle = sorted.Length / 2;
            double median = sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
            return Math.Clamp((int)Math.Round(-median * 1000), -500, 500);
        }
    }
}
