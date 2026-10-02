// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.Security.Cryptography;
using System.Text.Json;
using MatrixTea.Engine.Core.IO;
using MatrixTea.Engine.Packaging;

namespace MatrixTea.Editor.Core;

/// <summary>Transactional edit history. Invalid edits leave the model and history unchanged.</summary>
public sealed class ProjectDocument
{
    private readonly List<string> undo = [], redo = [];
    private string saved;
    public ProjectModel Model { get; private set; }
    public string? FilePath { get; private set; }
    public bool IsDirty => Serialize() != saved;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public ProjectDocument(ProjectModel model) { Model = model.Clone(); saved = Serialize(); }
    public string Serialize() => JsonSerializer.Serialize(Model, ProjectModel.Json);
    public ProjectDocument Snapshot() => new(Model) { FilePath = FilePath };
    public void Edit(Action<ProjectModel> edit)
    {
        string before = Serialize(); var candidate = Model.Clone(); edit(candidate); candidate.Validate();
        if (JsonSerializer.Serialize(candidate, ProjectModel.Json) == before) return;
        undo.Add(before); if (undo.Count > 64) undo.RemoveAt(0); redo.Clear(); Model = candidate;
    }
    public void Undo() { if (!CanUndo) return; redo.Add(Serialize()); Model = ProjectModel.Parse(undo[^1]); undo.RemoveAt(undo.Count - 1); }
    public void Redo() { if (!CanRedo) return; undo.Add(Serialize()); Model = ProjectModel.Parse(redo[^1]); redo.RemoveAt(redo.Count - 1); }
    public void Save(string path)
    {
        Model.Validate(); string full = Path.GetFullPath(path);
        if (!full.EndsWith(".mtproject", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("專案副檔名必須為 .mtproject。");
        if (FilePath != null && Path.GetDirectoryName(full) != Path.GetDirectoryName(FilePath) && Model.Chart.AudioPath.Length > 0) throw new IOException("含有音樂的專案請連同 assets 資料夾搬移，不能只另存專案檔。");
        if (Model.Chart.AudioPath.Length > 0 && !File.Exists(ProjectModel.AssetPath(Path.GetDirectoryName(full)!, Model.Chart.AudioPath))) throw new FileNotFoundException("找不到專案音樂。");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!); string json = Serialize(); AtomicFile.WriteText(full, json); FilePath = full; saved = json;
    }
    public static ProjectDocument Open(string path)
    {
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("專案檔超過 8 MB。");
        var document = new ProjectDocument(ProjectModel.Parse(File.ReadAllText(path))) { FilePath = Path.GetFullPath(path) }; return document;
    }
    public void ImportAudio(string source)
    {
        if (FilePath == null) throw new IOException("請先儲存專案，再匯入音樂。");
        var file = new FileInfo(source);
        if (!file.Exists || file.Length is <= 0 or > 300 * 1024 * 1024 || file.Attributes.HasFlag(FileAttributes.ReparsePoint) || !new[] { ".wav", ".mp3" }.Contains(file.Extension.ToLowerInvariant())) throw new InvalidDataException("請選擇小於 300 MB 的 WAV／MP3。");
        using var input = file.OpenRead(); string hash = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        string relative = "assets/" + hash + file.Extension.ToLowerInvariant();
        string target = ProjectModel.AssetPath(Path.GetDirectoryName(FilePath)!, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (!File.Exists(target)) { input.Position = 0; using var output = new FileStream(target, FileMode.CreateNew); input.CopyTo(output); }
        else { using var existing = File.OpenRead(target); if (Convert.ToHexString(SHA256.HashData(existing)).ToLowerInvariant() != hash) throw new InvalidDataException("已存在的資源驗證失敗。"); }
        Edit(p => p.Chart.AudioPath = relative);
    }
    /// <summary>Exports a content pack, not a compiled game executable.</summary>
    public void ExportAtr(string destination, CancellationToken cancellation = default)
    {
        Model.Validate(); string job = Path.Combine(Path.GetTempPath(), "MatrixTeaEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(job);
        try
        {
            File.WriteAllText(Path.Combine(job, "project.mtproject"), Serialize());
            File.WriteAllText(Path.Combine(job, "matrixtea-content.json"), "{\"schemaVersion\":1,\"kind\":\"editor-content\",\"project\":\"project.mtproject\"}");
            if (Model.Chart.AudioPath.Length > 0)
            {
                if (FilePath == null) throw new IOException("請先儲存專案。");
                string source = ProjectModel.AssetPath(Path.GetDirectoryName(FilePath)!, Model.Chart.AudioPath);
                string target = ProjectModel.AssetPath(job, Model.Chart.AudioPath); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            AtrArchive.Pack(job, destination, cancellation);
        }
        finally
        {
            string ownedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MatrixTeaEditor")) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(job).StartsWith(ownedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(job)) Directory.Delete(job, true);
        }
    }
}
