using MatrixTea.Engine.Core.Input;
using MatrixTea.Engine.Core.Rhythm;
using MatrixTea.Engine.Core.Adventure;
using MatrixTea.Engine.Core.Audio;
using System.Security.Cryptography;
using System.Text;

static class PlatformTests
{
    public static void Run(Action<bool, string> check)
    {
        void Reject<T>(Action action, string message) where T : Exception { try { action(); } catch (T) { check(true, message); return; } throw new Exception(message); }
        var input = new ActionInputBuffer(4, 8); bool[] down = { true, false, true, false };
        input.Capture(down, 1); check(input.TryReadPress(out var first) && first.Action == 0 && first.TimestampSeconds == 1, "first input timestamp");
        check(input.TryReadPress(out var second) && second.Action == 2 && !input.TryReadPress(out _), "chord edges / repeated fixed update");
        input.Capture(down, 2); check(input.PendingPresses == 0 && input.IsDown(2), "held key retriggered");
        input.Synchronize(down, 3); input.Capture(down, 4); check(input.PendingPresses == 0, "focus synchronization retriggered held key");
        Reject<ArgumentOutOfRangeException>(() => input.Capture(down, 3), "backwards input time");
        Reject<ArgumentOutOfRangeException>(() => input.Capture(down, double.NaN), "invalid input timestamp");
        Reject<ArgumentException>(() => input.Capture(new bool[1], 5), "input action count changed");
        for (int i = 0; i < 8; i++) { input.Capture(new bool[4], 5 + i * 2); input.Capture(down, 6 + i * 2); }
        check(input.PendingPresses == 8 && input.DroppedPresses == 8, "bounded input overflow diagnostic");
        var quiet = new ActionInputBuffer(4); var off = new bool[4];
        for (int i = 0; i < 1000; i++) quiet.Capture(off, i);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1000; i < 11000; i++) { quiet.Capture(off, i); quiet.TryReadPress(out _); }
        check(GC.GetAllocatedBytesForCurrentThread() == before, "steady input capture allocates");

        var clock = new PlaybackTimeline(); clock.Start(10, -2);
        check(clock.Position(11) == -1, "negative count-in"); clock.Pause(11.25);
        check(clock.Position(12) == -0.75, "paused song clock advances"); clock.Resume(13);
        check(clock.Position(13.5) == -0.25, "resume includes paused elapsed time");
        clock.Seek(2, 14); check(clock.Position(14) == 2, "explicit seek");
        double earlier = clock.Position(14.1); clock.ObserveAudio(2.05, 14.1);
        check(clock.Position(14.1) >= earlier, "audio observation reversed song time");
        Reject<ArgumentOutOfRangeException>(() => clock.Start(0, 0, 0), "zero rate");
        Reject<ArgumentOutOfRangeException>(() => clock.Seek(double.NaN, 20), "invalid seek");
        check(Math.Abs(PlaybackTimeline.JudgementTime(1, -80) - 0.92) < 1e-10, "input offset sign");
        var calibration = new LatencyCalibration(); calibration.AddTap(0.1, 0); calibration.AddTap(1.1, 1);
        Reject<InvalidOperationException>(() => _ = calibration.RecommendedOffsetMs, "too few calibration samples");
        for (int i = 0; i < 8; i++) calibration.AddTap(i + 2 + 0.080, i + 2);
        check(calibration.IsReady && calibration.RecommendedOffsetMs == -80, "median calibration / offset sign");
        check(!calibration.AddTap(20.9, 20), "calibration outlier accepted");

        var quests = new QuestJournal(new[] { new QuestDefinition("arrive", Array.Empty<string>()), new QuestDefinition("serve", new[] { "arrive", "sound" }), new QuestDefinition("sound", Array.Empty<string>()) });
        check(!quests.Complete("serve"), "locked quest completed"); check(quests.Complete("arrive") && !quests.Complete("arrive"), "quest completion idempotence");
        check(quests.Complete("sound") && quests.Complete("serve"), "quest unlock ordering");
        check(new QuestJournal(new[] { new QuestDefinition("arrive", Array.Empty<string>()) }, quests.ExportCompleted()).IsCompleted("serve"), "future saved IDs lost");
        Reject<ArgumentException>(() => new QuestJournal(new[] { new QuestDefinition("loop", new[] { "loop" }) }), "quest cycle accepted");
        Reject<ArgumentException>(() => new QuestJournal(new[] { new QuestDefinition("child", new[] { "missing" }) }), "missing prerequisite accepted");
        var grid = new CollisionGrid(20, 20, 32, (x, y) => x == 4);
        var position = grid.Move(new(48, 48), 500, 180);
        check(position.X < 120 && Math.Abs(position.Y - 228) < 0.1f && grid.CanStand(position), "collision tunneling / axis sliding");
        check(!grid.CanStand(new(-1, 10)), "world boundary collision");
        Reject<ArgumentOutOfRangeException>(() => grid.Move(position, 4097, 0), "unbounded collision movement");
        Reject<ArgumentOutOfRangeException>(() => grid.CanStand(new(float.NaN, 10)), "NaN world position");

        string root = Path.Combine(Path.GetTempPath(), "MatrixTeaAudioTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string wave = Path.Combine(root, "original.wav"); ProceduralScore.WriteWave(wave, 0.25, 108, 57);
            byte[] bytes = File.ReadAllBytes(wave); check(Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && bytes.Length == 44 + (int)(22050 * 0.25) * 2, "PCM wave header / length");
            ProceduralScore.WriteWave(Path.Combine(root, "same.wav"), 0.25, 108, 57);
            check(SHA256.HashData(bytes).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "same.wav")))), "composition is not deterministic");
            Reject<ArgumentOutOfRangeException>(() => ProceduralScore.WriteWave(wave, double.NaN, 108, 57), "invalid composition duration");
        }
        finally { if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MatrixTeaAudioTests")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true); }
    }
}
