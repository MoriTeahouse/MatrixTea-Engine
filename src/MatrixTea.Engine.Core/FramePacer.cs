namespace MatrixTea.Engine.Core;

/// <summary>Integer-tick fixed-step pacing with a bounded catch-up budget.</summary>
public sealed class FramePacer
{
    private long _accumulator;
    public FramePacer(TimeSpan targetStep) : this(targetStep, null) { }
    public FramePacer(TimeSpan targetStep, TimeSpan? maxFrameDelta)
    {
        if (targetStep <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(targetStep));
        MaxFrameDelta = maxFrameDelta ?? TimeSpan.FromMilliseconds(250);
        if (MaxFrameDelta <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxFrameDelta));
        TargetStep = targetStep;
    }
    public TimeSpan TargetStep { get; }
    public TimeSpan MaxFrameDelta { get; }
    public TimeSpan LastDroppedTime { get; private set; }
    public double Interpolation => Math.Clamp((double)_accumulator / TargetStep.Ticks, 0d, 1d);
    public void Add(TimeSpan delta)
    {
        LastDroppedTime = TimeSpan.Zero;
        if (delta <= TimeSpan.Zero) return;
        long accepted = Math.Min(delta.Ticks, MaxFrameDelta.Ticks);
        accepted = Math.Min(accepted, long.MaxValue - _accumulator);
        _accumulator += accepted;
        LastDroppedTime = TimeSpan.FromTicks(delta.Ticks - accepted);
    }
    public int Consume(int maxSteps)
    {
        if (maxSteps <= 0) return 0;
        long available = _accumulator / TargetStep.Ticks;
        int consumed = (int)Math.Min(available, maxSteps);
        long remainder = _accumulator % TargetStep.Ticks;
        long dropped = _accumulator - remainder - consumed * TargetStep.Ticks;
        _accumulator = remainder;
        LastDroppedTime = TimeSpan.FromTicks(SaturatingAdd(LastDroppedTime.Ticks, dropped));
        return consumed;
    }
    public void Reset() { _accumulator = 0; LastDroppedTime = TimeSpan.Zero; }
    internal static long SaturatingAdd(long left, long right) => right > long.MaxValue - left ? long.MaxValue : left + right;
}
