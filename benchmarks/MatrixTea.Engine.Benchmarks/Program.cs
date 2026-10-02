// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using MatrixTea.Engine.Core;
using MatrixTea.Engine.Core.Rhythm;
using MatrixTea.Engine.Packaging;

var results = new List<Comparison>();
using var engine = new EngineApplication(EngineOptions.Create("benchmark"));
var surface = new Surface(); var probe = new Probe();
engine.Scenes.Push(probe, engine.Context);
var baselineScenes = new LegacySceneStack(); baselineScenes.Push(probe, engine.Context);
void CurrentScenes(int count) { for (int i = 0; i < count; i++) { engine.Scenes.Update(engine.Context, TimeSpan.Zero); engine.Scenes.Render(engine.Context, surface); } }
void LegacyScenes(int count) { for (int i = 0; i < count; i++) { baselineScenes.Update(engine.Context, TimeSpan.Zero); baselineScenes.Render(engine.Context, surface); } }
CurrentScenes(20000); LegacyScenes(20000);
results.Add(new("250000 scene update/render pairs", Measure(() => LegacyScenes(250000)), Measure(() => CurrentScenes(250000))));

var chart = Enumerable.Range(0, 100000).Select(i => new Note(i, i * 0.005, i % 4)).ToArray();
var indexed = new RhythmPlaySession<Note>(chart, n => n.Time, n => n.Column);
var legacy = new RhythmPlaySession<Note>(chart, n => n.Time, n => n.Column, enableIndexedLookup: false);
for (int i = 0; i < 100; i++) { indexed.TryHit(i * 0.02, 0); legacy.TryHit(i * 0.02, 0); }
var oldHits = Measure(() => { for (int i = 0; i < 500; i++) legacy.TryHit(300 + i * 0.02, 0); });
var newHits = Measure(() => { for (int i = 0; i < 500; i++) indexed.TryHit(300 + i * 0.02, 0); });
if (indexed.Score != legacy.Score || indexed.HitCount != legacy.HitCount) throw new Exception("Benchmark paths disagree.");
results.Add(new("500 inputs / 100000-note chart", oldHits, newHits));

int baselineIndex = Array.IndexOf(args, "--baseline-assembly");
if (baselineIndex >= 0 && baselineIndex + 1 < args.Length)
{
    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[baselineIndex + 1]));
    var pack = assembly.GetType("MatrixTea.Engine.Packaging.AtrArchive")!.GetMethod("Pack")!;
    string root = Path.Combine(Path.GetTempPath(), "MatrixTeaBenchmarks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        string source = Path.Combine(root, "source"); Directory.CreateDirectory(source);
        byte[] data = new byte[16 * 1024 * 1024]; new Random(20261001).NextBytes(data);
        Array.Fill(data, (byte)65, 0, data.Length / 2); File.WriteAllBytes(Path.Combine(source, "mixed.bin"), data);
        string oldFile = Path.Combine(root, "old.atr"), newFile = Path.Combine(root, "new.atr");
        void OldPack() => pack.Invoke(null, new object[] { source, oldFile, CancellationToken.None });
        void NewPack() => AtrArchive.Pack(source, newFile);
        OldPack(); NewPack();
        results.Add(new("ATR pack / 16 MiB mixed corpus", Measure(OldPack), Measure(NewPack)));
        AtrArchive.Extract(oldFile, Path.Combine(root, "old-output")); AtrArchive.Extract(newFile, Path.Combine(root, "new-output"));
        if (!File.ReadAllBytes(Path.Combine(root, "new-output/mixed.bin")).SequenceEqual(data) || !File.ReadAllBytes(Path.Combine(root, "old-output/mixed.bin")).SequenceEqual(data)) throw new Exception("Benchmark ATR roundtrip failed.");
    }
    finally
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MatrixTeaBenchmarks")) + Path.DirectorySeparatorChar;
        if (Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
    }
}
var report = new
{
    runtime = Environment.Version.ToString(),
    os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    baselineCommit = "9d56e36",
    results
};
Directory.CreateDirectory("artifacts");
File.WriteAllText("artifacts/benchmark-results.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
foreach (var result in results)
    Console.WriteLine($"{result.Name}: {result.Old.Milliseconds:F2} -> {result.Current.Milliseconds:F2} ms; allocated {result.Old.AllocatedBytes:N0} -> {result.Current.AllocatedBytes:N0} bytes");

static Sample Measure(Action action)
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long memory = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
    action();
    double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
    long allocated = GC.GetAllocatedBytesForCurrentThread() - memory;
    return new(milliseconds, allocated);
}
sealed record Sample(double Milliseconds, long AllocatedBytes);
sealed record Comparison(string Name, Sample Old, Sample Current);
sealed record Note(int Id, double Time, int Column);
sealed class Surface : IRenderSurface { }
sealed class Probe : IScene
{
    public string Name => "benchmark";
    public void Enter(EngineContext context) { }
    public void Exit(EngineContext context) { }
    public void Update(EngineContext context, TimeSpan delta) { }
    public void Render(EngineContext context, IRenderSurface surface) { }
}
// Hot-path implementation from 9d56e36, isolated from the production engine.
sealed class LegacySceneStack
{
    private readonly Stack<IScene> _stack = new(); private readonly Queue<Action> _pending = new(); private bool _dispatching;
    private IScene? Current => _stack.Count > 0 ? _stack.Peek() : null;
    public void Push(IScene scene, EngineContext context) => Dispatch(() => { scene.Enter(context); _stack.Push(scene); });
    public void Update(EngineContext context, TimeSpan delta) => Dispatch(() => Current?.Update(context, delta));
    public void Render(EngineContext context, IRenderSurface surface) => Dispatch(() => Current?.Render(context, surface));
    private void Dispatch(Action action)
    {
        if (_dispatching) { _pending.Enqueue(action); return; }
        _dispatching = true;
        try { action(); int transitions = 0; while (_pending.Count > 0) { if (++transitions > 256) throw new InvalidOperationException(); _pending.Dequeue()(); } }
        catch { _pending.Clear(); throw; }
        finally { _dispatching = false; }
    }
}
