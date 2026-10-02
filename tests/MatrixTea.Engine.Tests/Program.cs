// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using MatrixTea.Engine.Core;
using MatrixTea.Engine.Core.IO;
using MatrixTea.Engine.Core.Rhythm;
using MatrixTea.Engine.Packaging;
using System.IO.Compression;
using System.Security.Cryptography;

int checks = 0;
void Check(bool condition, string message)
{ if (!condition) throw new Exception(message); checks++; }
void Reject<T>(Action action, string message) where T : Exception
{ try { action(); } catch (T) { checks++; return; } throw new Exception(message); }
using var engine = new EngineApplication(EngineOptions.Create("tests"));
var context = engine.Context; var surface = new Surface();
Reject<ArgumentOutOfRangeException>(() => new EngineApplication(new() { MaxUpdatesPerFrame = 0 }), "invalid options accepted");
Reject<ArgumentNullException>(() => context.Services.AddSingleton<object>(null!), "null service accepted");
Check(context.Options.TargetUpdateStep.Ticks == 83333, "120 Hz tick step");
Check(typeof(FramePacer).GetConstructor(new[] { typeof(TimeSpan) }) != null, "original FramePacer binary constructor removed");
Check(typeof(RhythmPlaySession<Note>).GetConstructor(new[] { typeof(IEnumerable<Note>), typeof(Func<Note, double>), typeof(Func<Note, int>), typeof(RhythmJudgementProfile), typeof(RhythmScoringProfile), typeof(double) }) != null, "original rhythm binary constructor removed");
using (var lowResolution = new EngineApplication(new() { UseHighResolutionTiming = false }))
    Check(lowResolution.Context.Clock is SystemEngineClock, "clock setting ignored");

var random = new Random(123456);
for (int example = 0; example < 6000; example++)
{
    long ticks = random.NextInt64(1, 1000000), accumulated = 0;
    var pacer = new FramePacer(TimeSpan.FromTicks(ticks));
    for (int frame = 0; frame < 4; frame++)
    {
        long delta = random.NextInt64(0, 10000000); int budget = random.Next(1, 12);
        accumulated += Math.Min(delta, 2500000);
        long expectedSteps = Math.Min(accumulated / ticks, budget);
        long remainder = accumulated % ticks;
        long expectedDropped = delta - Math.Min(delta, 2500000) + accumulated - remainder - expectedSteps * ticks;
        pacer.Add(TimeSpan.FromTicks(delta));
        Check(pacer.Consume(budget) == expectedSteps, "pacer update count");
        Check(pacer.LastDroppedTime.Ticks == expectedDropped && Math.Abs(pacer.Interpolation - (double)remainder / ticks) < 1e-12, "pacing time conservation");
        accumulated = remainder;
    }
}
var extreme = new FramePacer(TimeSpan.FromTicks(1), TimeSpan.MaxValue);
extreme.Add(TimeSpan.MaxValue); extreme.Add(TimeSpan.MaxValue);
Check(extreme.Consume(4) == 4 && extreme.Interpolation == 0 && extreme.LastDroppedTime == TimeSpan.MaxValue, "pacer overflow handling");
engine.Pump(TimeSpan.FromSeconds(30));
Check(context.Metrics.DroppedSimulationTime > TimeSpan.FromSeconds(29), "dropped simulation metric");
var metrics = new EngineMetrics(); metrics.RecordFrame(TimeSpan.MaxValue);
Check(metrics.AverageFrameTime > TimeSpan.MaxValue - TimeSpan.FromMilliseconds(1), "metric overflow handling");
for (int index = 0; index < 120; index++) metrics.RecordFrame(TimeSpan.FromMilliseconds(10));
Check(Math.Abs(metrics.AverageFrameTime.TotalMilliseconds - 10) < 0.01, "rolling metrics window");

var events = new List<string>(); var stack = engine.Scenes;
var next = new Probe("next", events);
var initial = new Probe("old", events) { Updating = () => { stack.Replace(next, context); events.Add("callback-complete"); } };
stack.Push(initial, context); stack.Update(context, TimeSpan.Zero);
Check(events.SequenceEqual(new[] { "old:enter", "old:update", "callback-complete", "old:exit", "next:enter" }), "deferred scene ordering changed");
Reject<InvalidOperationException>(() => stack.Push(next, context), "duplicate scene instance accepted");
stack.Clear(context);
var broken = new Probe("broken", events) { Exiting = () => throw new IOException("exit") };
stack.Push(new Probe("lower", events), context); stack.Push(broken, context);
Reject<AggregateException>(() => stack.Clear(context), "exit exception lost");
Check(stack.Count == 0 && events.Contains("lower:exit"), "exit failure interrupted cleanup");
var failedEntry = new Probe("entry-failure", events) { Entering = () => throw new IOException("enter") };
Reject<IOException>(() => stack.Push(failedEntry, context), "entry failure ignored");
Check(stack.Count == 0 && events.Last() == "entry-failure:exit", "partial entry not rolled back");
var quiet = new Probe("quiet", null); stack.Push(quiet, context);
for (int i = 0; i < 20000; i++) { stack.Update(context, TimeSpan.Zero); stack.Render(context, surface); }
long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 20000; i++) { stack.Update(context, TimeSpan.Zero); stack.Render(context, surface); }
Check(GC.GetAllocatedBytesForCurrentThread() == allocatedBefore, "scene hot path allocates");
Exception? threadError = null;
var otherThread = new Thread(() => { try { stack.Pop(context); } catch (Exception ex) { threadError = ex; } });
otherThread.Start(); otherThread.Join();
Check(threadError is InvalidOperationException && stack.Count == 1, "cross-thread mutation accepted");
stack.Clear(context);
var loop = new Probe("loop", null);
loop.Exiting = () => stack.Push(new Probe("replacement", null) { Entering = () => stack.Pop(context), Exiting = loop.Exiting }, context);
stack.Push(loop, context);
Reject<InvalidOperationException>(() => stack.Pop(context), "unbounded scene transition loop");
stack.Clear(context);

var disposed = new List<string>();
using (var lifetime = new EngineApplication(EngineOptions.Create("lifetime")))
{
    var owned = new Owned("first", disposed); var second = new Owned("second", disposed) { Throw = true };
    lifetime.Context.Services.AddOwnedSingleton(owned);
    lifetime.Context.Services.AddOwnedSingleton<IDisposable>(owned);
    lifetime.Context.Services.AddOwnedSingleton(second);
    lifetime.Context.Services.AddSingleton(new Borrowed(disposed));
    lifetime.Scenes.Push(new Probe("owned-scene", null) { Exiting = () => disposed.Add("scene") }, lifetime.Context);
    Reject<AggregateException>(() => lifetime.Dispose(), "owned service failure ignored");
    lifetime.Dispose();
    Check(disposed.SequenceEqual(new[] { "scene", "second", "first" }), "owned resources disposed in wrong order / twice / borrowed");
    Reject<ObjectDisposedException>(() => lifetime.Pump(TimeSpan.Zero), "disposed engine still runs");
}

var judgement = new RhythmJudgementEngine();
Reject<ArgumentException>(() => new RhythmJudgementEngine(new() { GoodWindow = TimeSpan.Zero }), "inverted windows accepted");
Reject<ArgumentOutOfRangeException>(() => judgement.Judge(double.NaN, 1), "NaN note time accepted");
Reject<ArgumentOutOfRangeException>(() => judgement.Judge(1, double.PositiveInfinity), "infinite input accepted");
var overflowing = new RhythmSessionState(); overflowing.Apply(new() { Kind = JudgementKind.Perfect, ScoreAwarded = int.MaxValue });
Reject<OverflowException>(() => overflowing.Apply(new() { Kind = JudgementKind.Perfect, ScoreAwarded = 1 }), "score overflow accepted");
Check(overflowing.Score == int.MaxValue && overflowing.HitCount == 1 && overflowing.Combo == 1, "overflow partially mutated state");
for (int seed = 0; seed < 30; seed++)
{
    var rng = new Random(seed);
    var notes = Enumerable.Range(0, 500).Select(i => new Note(i, rng.NextDouble() * 20, rng.Next(4))).OrderBy(n => n.Time).ThenBy(n => n.Id).ToArray();
    var indexed = new RhythmPlaySession<Note>(notes, n => n.Time, n => n.Column);
    var legacy = new RhythmPlaySession<Note>(notes, n => n.Time, n => n.Column, enableIndexedLookup: false);
    for (int input = 0; input < 300; input++)
    {
        double time = input * 0.08; int column = rng.Next(4);
        var a = indexed.TryHit(time, column); var b = legacy.TryHit(time, column);
        Check(a?.Note.Id == b?.Note.Id && a?.Judgment == b?.Judgment, "indexed lookup changed judgement");
        var x = indexed.CollectMisses(time); var y = legacy.CollectMisses(time);
        Check(x.Select(m => m.Note.Id).SequenceEqual(y.Select(m => m.Note.Id)), "indexed misses differ from chronological legacy chart");
    }
    Check(indexed.Score == legacy.Score && indexed.MaxCombo == legacy.MaxCombo && indexed.MissCount == legacy.MissCount, "indexed final score differs");
}
var tied = new RhythmPlaySession<Note>(new[] { new Note(1, 2.125, 0), new Note(2, 1.875, 0) }, n => n.Time, n => n.Column);
Check(tied.TryHit(2, 0)?.Note.Id == 1, "equal-distance tie changed source order");
var compatibility = new RhythmPlaySession<Note>(new[] { new Note(1, 3, 0) }, n => n.Time, n => n.Column);
var mutable = compatibility.Notes; mutable.Clear(); mutable.AddLast(new Note(2, 4, 1));
Check(!compatibility.UsesIndexedLookup && compatibility.TryHit(4, 1)?.Note.Id == 2, "mutable-list compatibility lost");
var emptyMisses = new RhythmPlaySession<Note>(new[] { new Note(1, 100, 0) }, n => n.Time, n => n.Column);
for (int i = 0; i < 10000; i++) emptyMisses.CollectMisses(0);
allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 10000; i++) emptyMisses.CollectMisses(0);
Check(GC.GetAllocatedBytesForCurrentThread() == allocatedBefore, "empty miss collection allocates");

string root = Path.Combine(Path.GetTempPath(), "MatrixTeaEngineTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
try
{
    string state = Path.Combine(root, "state.json"); AtomicFile.WriteText(state, "one"); AtomicFile.WriteText(state, "two");
    Check(File.ReadAllText(state) == "two" && File.ReadAllText(state + ".bak") == "one", "atomic backup lost");
    Reject<IOException>(() => AtomicFile.Write(state, stream => { stream.WriteByte(1); throw new IOException("serialization"); }), "failed atomic write committed");
    Check(File.ReadAllText(state) == "two", "serialization failure damaged original");
    AtomicFile.WriteTextAsync(state, "three").GetAwaiter().GetResult();
    Check(File.ReadAllText(state) == "three" && File.ReadAllText(state + ".bak") == "two", "async atomic write lost backup");
    using (var interrupted = new CancellationTokenSource())
    {
        Reject<OperationCanceledException>(() => AtomicFile.WriteAsync(state, async (stream, token) => { await stream.WriteAsync(new byte[] { 1 }, token); interrupted.Cancel(); }, interrupted.Token).GetAwaiter().GetResult(), "async cancellation committed");
        Check(File.ReadAllText(state) == "three" && !Directory.EnumerateFiles(root, "*.tmp").Any(), "canceled async write changed original or leaked temp");
    }
    string source = Path.Combine(root, "source"), archive = Path.Combine(root, "game.atr"); Directory.CreateDirectory(Path.Combine(source, "nested"));
    File.WriteAllBytes(Path.Combine(source, "noise.bin"), RandomNumberGenerator.GetBytes(2100000));
    File.WriteAllText(Path.Combine(source, "nested/text.txt"), new string('森', 800000)); File.WriteAllBytes(Path.Combine(source, "empty.bin"), Array.Empty<byte>());
    AtrArchive.Pack(source, archive); string target = Path.Combine(root, "target"); AtrArchive.Extract(archive, target);
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        Check(SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(target, Path.GetRelativePath(source, file))))), "ATR content differs");
    Reject<InvalidDataException>(() => { using var zip = ZipFile.OpenRead(archive); }, "ATR accepted as ZIP");
    Reject<IOException>(() => AtrArchive.Extract(archive, target), "nonempty extraction accepted");
    var bytes = File.ReadAllBytes(archive);
    foreach (int offset in new[] { 0, 16, 41, 49, 53, 70, bytes.Length - 1 })
    {
        var corrupt = (byte[])bytes.Clone(); corrupt[offset] ^= 0x40; string path = Path.Combine(root, "corrupt" + offset + ".atr"); File.WriteAllBytes(path, corrupt);
        Reject<InvalidDataException>(() => AtrArchive.Extract(path, Path.Combine(root, "corrupt" + offset)), "corrupt ATR accepted");
    }
    File.WriteAllBytes(Path.Combine(root, "truncated.atr"), bytes[..^3]);
    Reject<InvalidDataException>(() => AtrArchive.Extract(Path.Combine(root, "truncated.atr"), Path.Combine(root, "truncated")), "truncated ATR accepted");
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    Reject<OperationCanceledException>(() => AtrArchive.Pack(source, Path.Combine(root, "canceled.atr"), cancellation.Token), "pack cancellation ignored");
    Reject<OperationCanceledException>(() => AtrArchive.Extract(archive, Path.Combine(root, "canceled"), cancellation: cancellation.Token), "extract cancellation ignored");
    Check(!Directory.EnumerateFiles(root, "*.payload", SearchOption.AllDirectories).Any(), "ATR temporary payload leaked");
    if (args.Length == 2 && args[0] == "--legacy-atr")
    {
        string legacyTarget = Path.Combine(root, "legacy"); AtrArchive.Extract(args[1], legacyTarget);
        Check(File.Exists(Path.Combine(legacyTarget, "Artelu.exe")), "published ATR1 compatibility failed");
    }
}
finally
{
    string expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MatrixTeaEngineTests")) + Path.DirectorySeparatorChar;
    if (Path.GetFullPath(root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
}
PlatformTests.Run(Check);
Console.WriteLine($"PASS: {checks} engine assertions; scene dispatch, input capture and empty miss polling allocate 0 bytes after warmup.");

sealed class Surface : IRenderSurface { }
sealed record Note(int Id, double Time, int Column);
sealed class Borrowed(List<string> events) : IDisposable { public void Dispose() => events.Add("borrowed"); }
sealed class Owned(string name, List<string> events) : IDisposable
{ public bool Throw { get; init; } public void Dispose() { events.Add(name); if (Throw) throw new IOException(name); } }
sealed class Probe(string name, List<string>? events) : IScene
{
    public string Name => name;
    public Action? Entering { get; set; }
    public Action? Exiting { get; set; }
    public Action? Updating { get; set; }
    public void Enter(EngineContext context) { events?.Add(name + ":enter"); Entering?.Invoke(); }
    public void Exit(EngineContext context) { events?.Add(name + ":exit"); Exiting?.Invoke(); }
    public void Update(EngineContext context, TimeSpan delta) { events?.Add(name + ":update"); Updating?.Invoke(); }
    public void Render(EngineContext context, IRenderSurface surface) { }
}
