// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using MatrixTea.Engine.Core;

using var engine = new EngineApplication(EngineOptions.Create("Headless simulation"));
var scene = new SimulationScene();
engine.Scenes.Push(scene, engine.Context);
for (int frame = 0; frame < 60; frame++)
{
    int updates = engine.Pump(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
    for (int update = 0; update < updates; update++) engine.Update(engine.Context.Options.TargetUpdateStep);
}
Console.WriteLine($"Frames={engine.Context.Metrics.FrameCount}, Updates={scene.Updates}, Interpolation={engine.Interpolation:F4}");

sealed class SimulationScene : IScene
{
    public string Name => "Simulation";
    public int Updates { get; private set; }
    public void Enter(EngineContext context) { }
    public void Exit(EngineContext context) { }
    public void Update(EngineContext context, TimeSpan delta) => Updates++;
    public void Render(EngineContext context, IRenderSurface surface) { }
}
