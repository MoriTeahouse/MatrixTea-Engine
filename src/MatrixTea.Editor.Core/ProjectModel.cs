// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.Text.Json;
using MatrixTea.Engine.Core.Adventure;
using MatrixTea.Engine.Core.Rhythm;

namespace MatrixTea.Editor.Core;

public enum TileKind { Grass, Wall, Water, Path, Flowers }
public enum EntityKind { Spawn, Guest, Door, Resource, Sound }
public sealed class ProjectModel
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "新茶室";
    public List<SceneModel> Scenes { get; set; } = [];
    public List<QuestModel> Quests { get; set; } = [];
    public ChartModel Chart { get; set; } = new();
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 32 };
    public ProjectModel Clone() => Parse(JsonSerializer.Serialize(this, Json));
    public static ProjectModel Parse(string json)
    {
        if (json.Length > 8 * 1024 * 1024) throw new InvalidDataException("專案檔超過 8 MB。");
        var model = JsonSerializer.Deserialize<ProjectModel>(json, Json) ?? throw new InvalidDataException("專案資料為空。");
        model.Validate(); return model;
    }
    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("不支援的專案版本；檔案未被修改。");
        Text(Name, 120, "專案名稱");
        if (Scenes == null || Scenes.Count is < 1 or > 32 || Scenes.Any(s => s == null) || Scenes.Select(s => s.Id).Distinct().Count() != Scenes.Count) throw new InvalidDataException("場景需有唯一識別碼，數量為 1–32。");
        if (Quests == null || Quests.Count > 256 || Quests.Any(q => q == null)) throw new InvalidDataException("任務數量異常。");
        foreach (var quest in Quests) { Text(quest.Id, 120, "任務 ID"); Text(quest.Title, 200, "任務標題"); if (quest.Requires == null || quest.Requires.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("任務前置格式錯誤。"); }
        try { _ = CreateJournal(); } catch (ArgumentException ex) { throw new InvalidDataException("任務關係錯誤：" + ex.Message, ex); }
        foreach (var scene in Scenes)
        {
            Text(scene.Id, 120, "場景 ID"); Text(scene.Name, 120, "場景名稱");
            if (scene.Width is < 4 or > 128 || scene.Height is < 4 or > 128 || scene.Tiles == null || scene.Tiles.Length != scene.Width * scene.Height || scene.Tiles.Any(t => !Enum.IsDefined(t))) throw new InvalidDataException("地圖大小或地形資料錯誤。");
            if (scene.Entities == null || scene.Entities.Count > 2048 || scene.Entities.Any(e => e == null) || scene.Entities.Select(e => e.Id).Distinct().Count() != scene.Entities.Count || scene.Entities.Count(e => e.Kind == EntityKind.Spawn) != 1) throw new InvalidDataException("每個場景需有一個出生點，物件 ID 不可重複。");
            foreach (var entity in scene.Entities)
            {
                Text(entity.Id, 120, "物件 ID"); Text(entity.Name, 200, "物件名稱");
                if (!Enum.IsDefined(entity.Kind) || scene.IsBlocked(entity.X, entity.Y)) throw new InvalidDataException("物件必須位於地圖內可走的地形。");
                if (entity.Dialogue == null || entity.Dialogue.Length > 4000 || entity.QuestId == null || (entity.QuestId.Length > 0 && !Quests.Any(q => q.Id == entity.QuestId))) throw new InvalidDataException("物件對話或任務參照錯誤。");
            }
        }
        if (Chart == null || !double.IsFinite(Chart.Bpm) || Chart.Bpm is < 30 or > 300 || !double.IsFinite(Chart.Duration) || Chart.Duration is < 1 or > 600 || !double.IsFinite(Chart.OffsetMs) || Math.Abs(Chart.OffsetMs) > 1000 || Chart.Notes == null || Chart.Notes.Count > 20000 || Chart.Notes.Any(n => n == null || n.Lane is < 0 or > 3 || !double.IsFinite(n.Time) || n.Time < 0 || n.Time > Chart.Duration)) throw new InvalidDataException("譜面參數錯誤（4 軌，最長 600 秒）。");
        if (Chart.Notes.Select(n => (Math.Round(n.Time, 6), n.Lane)).Distinct().Count() != Chart.Notes.Count) throw new InvalidDataException("同一時間與軌道不可重複放置音符。");
        if (Chart.AudioPath == null || (Chart.AudioPath.Length > 0 && (!Chart.AudioPath.StartsWith("assets/", StringComparison.Ordinal) || !new[] { ".wav", ".mp3" }.Contains(Path.GetExtension(Chart.AudioPath).ToLowerInvariant())))) throw new InvalidDataException("音樂必須使用專案 assets 中的 WAV／MP3。");
        if (Chart.AudioPath.Length > 0) _ = AssetPath(Path.GetTempPath(), Chart.AudioPath);
    }
    public QuestJournal CreateJournal() => new(Quests.Select(q => new QuestDefinition(q.Id, q.Requires)));
    public RhythmPlaySession<NoteModel> CreateRhythmSession() => new(Chart.Notes.Select(n => new NoteModel { Time = n.Time, Lane = n.Lane }), n => n.Time, n => n.Lane, missThresholdSeconds: .25);
    public static string AssetPath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Split('/').Any(s => s is "" or "." or ".." || s.Contains(':')) || Path.IsPathRooted(relative)) throw new InvalidDataException("資源路徑不可離開專案。");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("資源路徑越界。");
        for (var node = new FileInfo(path) as FileSystemInfo; node != null; node = new DirectoryInfo(Path.GetDirectoryName(node.FullName)!))
        {
            if (node.Exists && node.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("專案資源不可使用路徑連結。");
            if (node.FullName.TrimEnd(Path.DirectorySeparatorChar).Equals(fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) break;
        }
        return path;
    }
    private static void Text(string? text, int limit, string label) { if (string.IsNullOrWhiteSpace(text) || text.Length > limit) throw new InvalidDataException(label + "不可空白或過長。"); }
    public static ProjectModel Sample()
    {
        var scene = new SceneModel { Id = "garden", Name = "茶室庭園", Width = 24, Height = 18, Tiles = new TileKind[24 * 18] };
        for (int y = 0; y < scene.Height; y++) for (int x = 0; x < scene.Width; x++)
            scene.Tiles[y * scene.Width + x] = x == 0 || y == 0 || x == 23 || y == 17 ? TileKind.Wall : x == 12 && y is > 2 and < 15 && y != 9 ? TileKind.Water : y == 9 ? TileKind.Path : (x + y) % 13 == 0 ? TileKind.Flowers : TileKind.Grass;
        scene.Entities = [new() { Id = "spawn", Name = "出生點", Kind = EntityKind.Spawn, X = 4, Y = 9 }, new() { Id = "host", Name = "茶室主人", Kind = EntityKind.Guest, X = 8, Y = 8, Dialogue = "歡迎來到茶室。沿著石徑走，聽聽庭園的聲音。", QuestId = "greeting" }, new() { Id = "leaf", Name = "晨露茶葉", Kind = EntityKind.Resource, X = 17, Y = 9, Dialogue = "採到了今天的第一片茶葉。", QuestId = "tea" }];
        var model = new ProjectModel { Scenes = [scene], Quests = [new() { Id = "greeting", Title = "拜訪茶室" }, new() { Id = "tea", Title = "收集晨露茶葉", Requires = ["greeting"] }] };
        for (int i = 0; i < 32; i++) model.Chart.Notes.Add(new() { Time = 2 + i * .5, Lane = i % 4 });
        model.Validate(); return model;
    }
}
public sealed class SceneModel
{
    public string Id { get; set; } = "scene";
    public string Name { get; set; } = "場景";
    public int Width { get; set; } = 24;
    public int Height { get; set; } = 18;
    public TileKind[] Tiles { get; set; } = [];
    public List<EntityModel> Entities { get; set; } = [];
    public bool IsBlocked(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height || Tiles[y * Width + x] is TileKind.Wall or TileKind.Water;
    public CollisionGrid CreateCollision() => new(Width, Height, 32, IsBlocked);
}
public sealed class EntityModel
{
    public string Id { get; set; } = "object";
    public string Name { get; set; } = "物件";
    public EntityKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Dialogue { get; set; } = "";
    public string QuestId { get; set; } = "";
    public override string ToString() => $"{Name} · {Kind}";
}
public sealed class QuestModel
{
    public string Id { get; set; } = "quest";
    public string Title { get; set; } = "任務";
    public List<string> Requires { get; set; } = [];
    public override string ToString() => Title;
}
public sealed class ChartModel
{
    public double Bpm { get; set; } = 120;
    public double Duration { get; set; } = 30;
    public double OffsetMs { get; set; }
    public string AudioPath { get; set; } = "";
    public List<NoteModel> Notes { get; set; } = [];
    public double Snap(double time, int divisions = 4) => Math.Clamp(Math.Round(time / (60 / Bpm / divisions)) * (60 / Bpm / divisions), 0, Duration);
}
public sealed class NoteModel { public double Time { get; set; } public int Lane { get; set; } }
