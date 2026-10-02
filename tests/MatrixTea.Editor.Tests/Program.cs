// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using MatrixTea.Editor.Core;
using MatrixTea.Engine.Packaging;
using System.Text.Json;

if (args.Length == 2 && args[0] == "--write-sample")
{
    new ProjectDocument(ProjectModel.Sample()).Save(args[1]); Console.WriteLine("EDITOR_SAMPLE_OK"); return;
}

int checks = 0;
void Check(bool valid, string label) { checks++; if (!valid) throw new Exception(label); }
void Reject(Action action, string label) { try { action(); } catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException) { checks++; return; } throw new Exception(label); }
string root = Path.Combine(Path.GetTempPath(), "MatrixTeaEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
try
{
    var doc = new ProjectDocument(ProjectModel.Sample()); string initial = doc.Serialize();
    Check(!doc.IsDirty, "new document clean");
    doc.Edit(p => { for (int x = 2; x < 7; x++) p.Scenes[0].Tiles[2 * 24 + x] = TileKind.Path; });
    Check(doc.IsDirty && doc.CanUndo, "transaction dirty"); doc.Undo(); Check(doc.Serialize() == initial && !doc.IsDirty, "stroke undoes as one transaction"); doc.Redo(); Check(doc.IsDirty, "redo");
    string before = doc.Serialize(); Reject(() => doc.Edit(p => p.Scenes[0].Tiles[9 * 24 + 4] = TileKind.Wall), "cannot block spawn"); Check(before == doc.Serialize(), "invalid edit rolls back");
    Reject(() => doc.Edit(p => p.Quests[0].Requires = ["tea"]), "cycle");
    Reject(() => doc.Edit(p => p.Quests[1].Requires = ["missing"]), "missing prerequisite");
    Reject(() => doc.Edit(p => p.Scenes[0].Entities[1].QuestId = "missing"), "missing entity quest");
    Reject(() => doc.Edit(p => p.Scenes[0].Entities[1].X = 1000), "entity outside map");
    Reject(() => doc.Edit(p => p.Chart.Notes.Add(new() { Time = 2, Lane = 0 })), "duplicate note");
    Reject(() => doc.Edit(p => p.Chart.AudioPath = "assets/../../secret.wav"), "asset traversal");
    Reject(() => ProjectModel.Parse(initial.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2")), "future schema");
    Reject(() => ProjectModel.Parse("{\"schemaVersion\":1,\"scenes\":null}"), "null structure");
    doc.Edit(p => p.Chart.Notes.Clear()); doc.Edit(p => p.Chart.Notes.Add(new() { Time = p.Chart.Snap(2.19), Lane = 2 }));
    Check(doc.Model.Chart.Notes[0].Time == 2.25, "sixteenth grid");
    var rhythm = doc.Model.CreateRhythmSession(); Check(rhythm.TryHit(2.25, 2) != null && rhythm.Score == 100 && rhythm.IsComplete, "real engine judgement");
    var journal = doc.Model.CreateJournal(); Check(!journal.Complete("tea") && journal.Complete("greeting") && journal.Complete("tea"), "real engine quest graph");
    var preview = new AdventurePreview(doc.Model, 0);
    for (int i = 0; i < 300; i++) preview.Move(-1, 0, .1); Check(preview.Position.X >= 40, "preview collision blocks border");
    string path = Path.Combine(root, "sample.mtproject"); doc.Save(path); string saved = File.ReadAllText(path); Check(!doc.IsDirty, "save clears dirty");
    doc.Edit(p => p.Name = "second"); doc.Save(path); Check(File.ReadAllText(path + ".bak") == saved, "atomic prior version");
    Check(ProjectDocument.Open(path).Model.Name == "second", "load");
    string audio = Path.Combine(root, "song.wav"); File.WriteAllBytes(audio, [82, 73, 70, 70, 0, 1, 2, 3]); doc.ImportAudio(audio); doc.Save(path);
    Check(File.Exists(ProjectModel.AssetPath(root, doc.Model.Chart.AudioPath)), "import is project local");
    string archive = Path.Combine(root, "content.atr"); doc.ExportAtr(archive); string extracted = Path.Combine(root, "unpacked"); AtrArchive.Extract(archive, extracted);
    var loaded = ProjectDocument.Open(Path.Combine(extracted, "project.mtproject"));
    Check(loaded.Serialize() == doc.Serialize() && File.Exists(ProjectModel.AssetPath(extracted, loaded.Model.Chart.AudioPath)), "ATR round trip includes referenced asset");
    Reject(() => doc.Save(Path.Combine(root, "elsewhere", "copy.mtproject")), "asset save as prevention");
    for (int i = 0; i < 90; i++) doc.Edit(p => p.Name = "history" + i);
    int undos = 0; while (doc.CanUndo) { doc.Undo(); undos++; } Check(undos == 64, "bounded history");
    Console.WriteLine($"EDITOR_TESTS_OK {checks} checks");
}
finally
{
    string expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MatrixTeaEditorTests")) + Path.DirectorySeparatorChar;
    if (Path.GetFullPath(root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
}
