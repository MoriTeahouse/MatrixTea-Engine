namespace MatrixTea.Engine.Core;

/// <summary>
/// Accumulates real time and emits fixed update steps for deterministic logic.
/// </summary>
public sealed class FramePacer
{
    private TimeSpan _accumulator;

    public FramePacer(TimeSpan targetStep)
    {
        if (targetStep <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(targetStep));
        }

        TargetStep = targetStep;
    }

    public TimeSpan TargetStep { get; }

    public double Interpolation => TargetStep.Ticks == 0
        ? 0d
        : Math.Clamp(_accumulator.TotalMilliseconds / TargetStep.TotalMilliseconds, 0d, 1d);

    public void Add(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            return;
        }

        _accumulator += delta > TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : delta;
    }

    public int Consume(int maxSteps)
    {
        if (maxSteps <= 0)
        {
            return 0;
        }

        int steps = 0;
        while (_accumulator >= TargetStep && steps < maxSteps)
        {
            _accumulator -= TargetStep;
            steps++;
        }

        // Discard whole overdue steps instead of retaining a catch-up backlog.
        if (_accumulator >= TargetStep)
            _accumulator = TimeSpan.FromTicks(_accumulator.Ticks % TargetStep.Ticks);
        return steps;
    }
}