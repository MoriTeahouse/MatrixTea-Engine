// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using MatrixTea.Engine.Core.Adventure;

namespace MatrixTea.Editor.Core;

/// <summary>Preview uses the same collision and quest systems as a MatrixTea game.</summary>
public sealed class AdventurePreview
{
    private readonly SceneModel scene;
    private readonly CollisionGrid collision;
    public QuestJournal Journal { get; }
    public WorldPosition Position { get; private set; }
    public AdventurePreview(ProjectModel model, int sceneIndex)
    {
        model.Validate(); scene = model.Clone().Scenes[sceneIndex]; collision = scene.CreateCollision(); Journal = model.CreateJournal();
        var spawn = scene.Entities.Single(e => e.Kind == EntityKind.Spawn); Position = new(spawn.X * 32 + 16, spawn.Y * 32 + 16);
    }
    public void Move(float dx, float dy, double seconds, bool sprint = false)
    {
        if (!float.IsFinite(dx) || !float.IsFinite(dy) || !double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        float length = MathF.Sqrt(dx * dx + dy * dy); if (length == 0) return;
        float distance = (float)Math.Min(seconds, .1) * (sprint ? 160 : 96);
        Position = collision.Move(Position, dx / length * distance, dy / length * distance);
    }
    public string Interact()
    {
        var nearest = scene.Entities.Where(e => e.Kind != EntityKind.Spawn).OrderBy(e => Distance(e)).FirstOrDefault();
        if (nearest == null || Distance(nearest) > 52) return "靠近物件後按 E 互動。";
        string message = string.IsNullOrWhiteSpace(nearest.Dialogue) ? nearest.Name : nearest.Dialogue;
        if (nearest.QuestId.Length > 0)
            message += Journal.Complete(nearest.QuestId) ? "  ✓ 任務完成" : Journal.IsCompleted(nearest.QuestId) ? "  ✓ 已完成" : "  · 前置任務尚未完成";
        return message;
    }
    private double Distance(EntityModel e) => Math.Sqrt(Math.Pow(e.X * 32 + 16 - Position.X, 2) + Math.Pow(e.Y * 32 + 16 - Position.Y, 2));
}
