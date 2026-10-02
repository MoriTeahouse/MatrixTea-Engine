// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MatrixTea.Editor.Core;
using MatrixTea.Engine.Core.Rhythm;
using MatrixTea.Engine.Core.Audio;
using MatrixTea.Engine.Desktop;
using Microsoft.Win32;

namespace MatrixTea.Editor;

public partial class MainWindow : Window
{
    private ProjectDocument document = new(ProjectModel.Sample());
    private int sceneIndex;
    private string? selectedEntity = "host", selectedQuest;
    private bool refreshing, smoke, exporting;
    private readonly HashSet<Key> keys = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch clock = new();
    private double previous;
    private AdventurePreview? adventure;
    private RhythmPlaySession<NoteModel>? rhythm;
    private DeviceAudioPlayer? audio;
    private bool audioStarted;
    private bool IsPlaying => adventure != null || rhythm != null;
    private SceneModel Scene => document.Model.Scenes[sceneIndex];
    public MainWindow()
    {
        InitializeComponent(); MapView.BrushIndex = () => BrushSelector.SelectedIndex;
        MapView.SelectEntity = id => { selectedEntity = id; Refresh(); };
        MapView.Paint = cells => Edit(p => { foreach (var cell in cells) p.Scenes[sceneIndex].Tiles[cell.Key] = cell.Value; });
        Timeline.ToggleNote = (lane, time, removeOnly) => Edit(p =>
        {
            var existing = p.Chart.Notes.FirstOrDefault(n => n.Lane == lane && Math.Abs(n.Time - time) < .001);
            if (existing != null) p.Chart.Notes.Remove(existing); else if (!removeOnly) p.Chart.Notes.Add(new() { Time = time, Lane = lane });
            p.Chart.Notes = p.Chart.Notes.OrderBy(n => n.Time).ThenBy(n => n.Lane).ToList();
        });
        timer.Tick += Tick; PreviewKeyDown += OnKeyDown; PreviewKeyUp += (_, e) => keys.Remove(e.Key);
        Deactivated += (_, _) => { keys.Clear(); if (IsPlaying) { StopPreview(); Status.Text = "預覽已因切換視窗而停止。"; } };
        Closing += OnClosing; Refresh(); Status.Text = "就緒 · 從庭園範例開始，或開啟 .mtproject 專案。";
    }
    private void Guard(Action action) { try { action(); } catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or JsonException or InvalidOperationException or UnauthorizedAccessException or FormatException or OverflowException) { Status.Text = "無法完成：" + ex.Message; } }
    private void Edit(Action<ProjectModel> edit) { if (IsPlaying || exporting) { Status.Text = "請先停止預覽／等待匯出完成再編輯。"; return; } Guard(() => { document.Edit(edit); Refresh(); Status.Text = "已套用 · Ctrl+Z 復原 / Ctrl+Y 重做"; }); }
    private void Refresh()
    {
        refreshing = true;
        try
        {
            sceneIndex = Math.Clamp(sceneIndex, 0, document.Model.Scenes.Count - 1);
            ProjectTitle.Text = document.Model.Name + (document.IsDirty ? " · 未儲存" : "");
            NameInput.Text = document.Model.Name; SceneNameInput.Text = Scene.Name;
            Scenes.ItemsSource = document.Model.Scenes; Scenes.SelectedIndex = sceneIndex;
            Entities.ItemsSource = Scene.Entities; Entities.SelectedItem = Scene.Entities.FirstOrDefault(e => e.Id == selectedEntity);
            var entity = Entities.SelectedItem as EntityModel; EntityInspector.IsEnabled = entity != null && !IsPlaying;
            EntityName.Text = entity?.Name ?? ""; EntityX.Text = entity?.X.ToString(CultureInfo.InvariantCulture) ?? ""; EntityY.Text = entity?.Y.ToString(CultureInfo.InvariantCulture) ?? ""; EntityDialogue.Text = entity?.Dialogue ?? ""; EntityQuest.Text = entity?.QuestId ?? "";
            Quests.ItemsSource = document.Model.Quests; Quests.SelectedItem = document.Model.Quests.FirstOrDefault(q => q.Id == selectedQuest);
            var quest = Quests.SelectedItem as QuestModel; QuestIdInput.Text = quest?.Id ?? ""; QuestTitleInput.Text = quest?.Title ?? ""; QuestRequires.Text = quest == null ? "" : string.Join(", ", quest.Requires); QuestInspector.IsEnabled = quest != null && !IsPlaying;
            BpmInput.Text = document.Model.Chart.Bpm.ToString(CultureInfo.InvariantCulture); DurationInput.Text = document.Model.Chart.Duration.ToString(CultureInfo.InvariantCulture); OffsetInput.Text = document.Model.Chart.OffsetMs.ToString(CultureInfo.InvariantCulture);
            AudioLabel.Text = document.Model.Chart.AudioPath.Length == 0 ? "無音樂 · 使用節拍時間軸" : Path.GetFileName(document.Model.Chart.AudioPath);
            TimeScroll.Maximum = Math.Max(0, document.Model.Chart.Duration - 8);
            ChartStats.Text = $"{document.Model.Chart.Notes.Count} 音符 · {document.Model.Chart.Bpm:0.##} BPM · {document.Model.Chart.Duration:0.##} 秒 · 顯示 {TimeScroll.Value:0.0}–{TimeScroll.Value + 8:0.0} 秒";
            MapView.Scene = Scene; MapView.SelectedId = selectedEntity; MapView.InvalidateVisual();
            Timeline.Chart = document.Model.Chart; Timeline.ViewStart = TimeScroll.Value; Timeline.InvalidateVisual();
            UndoButton.IsEnabled = document.CanUndo && !IsPlaying; RedoButton.IsEnabled = document.CanRedo && !IsPlaying;
            EntityInspector.Visibility = Workspace.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            ChartInspector.Visibility = Workspace.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            QuestInspector.Visibility = Workspace.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { refreshing = false; }
    }
    private bool ConfirmLeave()
    {
        if (exporting) { Status.Text = "ATR 正在匯出，請等待完成。"; return false; }
        StopPreview(); if (!document.IsDirty || smoke) return true;
        var result = MessageBox.Show(this, "專案尚未儲存。是否先儲存？", "未儲存的變更", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (result == MessageBoxResult.Cancel) return false;
        if (result == MessageBoxResult.Yes) return SaveDocument(); return true;
    }
    private bool SaveDocument()
    {
        string? path = document.FilePath;
        if (path == null) { var dialog = new SaveFileDialog { Filter = "MatrixTea 專案|*.mtproject", FileName = "tea-room.mtproject" }; if (dialog.ShowDialog(this) != true) return false; path = dialog.FileName; }
        bool success = false; Guard(() => { document.Save(path); Refresh(); Status.Text = "已儲存：" + path; success = true; }); return success;
    }
    public void OpenPath(string path) { if (!ConfirmLeave()) return; Guard(() => { var loaded = ProjectDocument.Open(path); document = loaded; sceneIndex = 0; selectedEntity = null; selectedQuest = null; Refresh(); Status.Text = "已開啟：" + path; }); }
    private void New_Click(object sender, RoutedEventArgs e) { if (!ConfirmLeave()) return; document = new(ProjectModel.Sample()); sceneIndex = 0; selectedEntity = "host"; selectedQuest = null; Refresh(); Status.Text = "已建立庭園範例專案。"; }
    private void Open_Click(object sender, RoutedEventArgs e) { var dialog = new OpenFileDialog { Filter = "MatrixTea 專案|*.mtproject" }; if (dialog.ShowDialog(this) == true) OpenPath(dialog.FileName); }
    private void Save_Click(object sender, RoutedEventArgs e) => SaveDocument();
    private void Undo_Click(object sender, RoutedEventArgs e) { if (IsPlaying || exporting) return; document.Undo(); Refresh(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { if (IsPlaying || exporting) return; document.Redo(); Refresh(); }
    private void Names_Click(object sender, RoutedEventArgs e) => Edit(p => { p.Name = NameInput.Text.Trim(); p.Scenes[sceneIndex].Name = SceneNameInput.Text.Trim(); });
    private void Scenes_Changed(object sender, SelectionChangedEventArgs e) { if (refreshing || Scenes.SelectedIndex < 0) return; StopPreview(); sceneIndex = Scenes.SelectedIndex; selectedEntity = null; Refresh(); }
    private void Entities_Changed(object sender, SelectionChangedEventArgs e) { if (refreshing) return; selectedEntity = (Entities.SelectedItem as EntityModel)?.Id; Refresh(); }
    private void Quests_Changed(object sender, SelectionChangedEventArgs e) { if (refreshing) return; selectedQuest = (Quests.SelectedItem as QuestModel)?.Id; Refresh(); }
    private void Workspace_Changed(object sender, SelectionChangedEventArgs e) { if (e.Source != Workspace || !IsInitialized || refreshing) return; StopPreview(); Refresh(); }
    private void TimeScroll_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!IsInitialized || refreshing) return; Timeline.ViewStart = e.NewValue; Timeline.InvalidateVisual(); ChartStats.Text = $"{document.Model.Chart.Notes.Count} 音符 · {document.Model.Chart.Bpm:0.##} BPM · 顯示 {e.NewValue:0.0}–{e.NewValue + 8:0.0} 秒"; }
    private void AddScene_Click(object sender, RoutedEventArgs e) => Edit(p => { var newScene = ProjectModel.Sample().Scenes[0]; newScene.Id = "scene-" + Guid.NewGuid().ToString("N")[..8]; newScene.Name = "新庭園 " + (p.Scenes.Count + 1); foreach (var entity in newScene.Entities) entity.QuestId = ""; p.Scenes.Add(newScene); });
    private void AddEntity_Click(object sender, RoutedEventArgs e) => Edit(p =>
    {
        var scene = p.Scenes[sceneIndex]; int index = Array.FindIndex(scene.Tiles, t => t is TileKind.Grass or TileKind.Path or TileKind.Flowers); if (index < 0) throw new InvalidDataException("沒有可放置物件的地形。");
        string id = "object-" + Guid.NewGuid().ToString("N")[..8]; scene.Entities.Add(new() { Id = id, Name = "新物件", X = index % scene.Width, Y = index / scene.Width, Kind = (EntityKind)(NewEntityKind.SelectedIndex + 1), Dialogue = "一段新的相遇。" }); selectedEntity = id;
    });
    private void ApplyEntity_Click(object sender, RoutedEventArgs e) => Edit(p =>
    {
        var entity = p.Scenes[sceneIndex].Entities.Single(o => o.Id == selectedEntity); entity.Name = EntityName.Text.Trim(); entity.X = int.Parse(EntityX.Text, CultureInfo.InvariantCulture); entity.Y = int.Parse(EntityY.Text, CultureInfo.InvariantCulture); entity.Dialogue = EntityDialogue.Text; entity.QuestId = EntityQuest.Text.Trim();
    });
    private void DeleteEntity_Click(object sender, RoutedEventArgs e) => Edit(p => { var entity = p.Scenes[sceneIndex].Entities.Single(o => o.Id == selectedEntity); if (entity.Kind == EntityKind.Spawn) throw new InvalidDataException("出生點需保留；可修改其位置。"); p.Scenes[sceneIndex].Entities.Remove(entity); });
    private void ApplyChart_Click(object sender, RoutedEventArgs e) => Edit(p => { p.Chart.Bpm = double.Parse(BpmInput.Text, CultureInfo.InvariantCulture); p.Chart.Duration = double.Parse(DurationInput.Text, CultureInfo.InvariantCulture); p.Chart.OffsetMs = double.Parse(OffsetInput.Text, CultureInfo.InvariantCulture); });
    private void Audio_Click(object sender, RoutedEventArgs e)
    {
        if (IsPlaying || exporting) return; if (document.FilePath == null && !SaveDocument()) return;
        var dialog = new OpenFileDialog { Filter = "音樂檔案|*.wav;*.mp3" }; if (dialog.ShowDialog(this) != true) return; Guard(() => { document.ImportAudio(dialog.FileName); Refresh(); Status.Text = "音樂已複製至專案 assets，請儲存專案。"; });
    }
    private void ClearAudio_Click(object sender, RoutedEventArgs e) => Edit(p => p.Chart.AudioPath = "");
    private void AddQuest_Click(object sender, RoutedEventArgs e) => Edit(p => { string id = "quest-" + Guid.NewGuid().ToString("N")[..8]; p.Quests.Add(new() { Id = id, Title = "新任務" }); selectedQuest = id; });
    private void DeleteQuest_Click(object sender, RoutedEventArgs e) => Edit(p => { p.Quests.RemoveAll(q => q.Id == selectedQuest); });
    private void ApplyQuest_Click(object sender, RoutedEventArgs e) => Edit(p =>
    {
        string oldId = selectedQuest ?? ""; string newId = QuestIdInput.Text.Trim(); var quest = p.Quests.Single(q => q.Id == oldId); quest.Id = newId; quest.Title = QuestTitleInput.Text.Trim(); quest.Requires = QuestRequires.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (newId != oldId) { foreach (var q in p.Quests) q.Requires = q.Requires.Select(id => id == oldId ? newId : id).ToList(); foreach (var entity in p.Scenes.SelectMany(s => s.Entities)) if (entity.QuestId == oldId) entity.QuestId = newId; }
        // Selection changes only after a valid transaction commits (Refresh resolves it by ID).
    });
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (IsPlaying || exporting) return; var dialog = new SaveFileDialog { Filter = "MatrixTea ATR 內容封裝|*.atr", FileName = "tea-room-content.atr" }; if (dialog.ShowDialog(this) != true) return;
        var snapshot = document.Snapshot();
        exporting = true; Status.Text = "正在匯出 ATR 內容封裝…";
        try { await Task.Run(() => snapshot.ExportAtr(dialog.FileName)); Status.Text = "ATR 內容封裝已匯出（含專案與音樂，非獨立遊戲 EXE）：" + dialog.FileName; }
        catch (Exception ex) { Status.Text = "匯出失敗：" + ex.Message; } finally { exporting = false; }
    }
    private void Play_Click(object sender, RoutedEventArgs e) { if (IsPlaying) StopPreview(); else StartPreview(); }
    private void StartPreview()
    {
        if (exporting) return;
        Guard(() =>
        {
            document.Model.Validate(); keys.Clear();
            if (Workspace.SelectedIndex == 1)
            {
                if (document.Model.Chart.AudioPath.Length > 0)
                {
                    if (document.FilePath == null) throw new IOException("請先儲存專案。"); audio = new();
                    if (!audio.Load(ProjectModel.AssetPath(Path.GetDirectoryName(document.FilePath)!, document.Model.Chart.AudioPath))) { string error = audio.Error ?? "音訊裝置不可用"; audio.Dispose(); audio = null; throw new IOException(error); }
                    audio.Volume = .6f;
                }
                rhythm = document.Model.CreateRhythmSession(); Timeline.PreviewNotes = rhythm.RemainingNotes; audioStarted = false; TimeScroll.Value = 0;
                Status.Text = "節奏預覽 · 倒數 2 秒 · D F J K · Esc 停止";
            }
            else { Workspace.SelectedIndex = 0; adventure = new(document.Model, sceneIndex); Status.Text = "庭園預覽 · WASD 移動 · Shift 跑步 · E 互動 · Esc 停止"; }
            clock.Restart(); previous = 0; timer.Start(); PlayButton.Content = "■ 停止預覽"; Refresh(); Keyboard.Focus(Workspace.SelectedIndex == 1 ? Timeline : MapView);
        });
    }
    private double SongTime => audio != null && audioStarted ? audio.PositionSeconds + document.Model.Chart.OffsetMs / 1000 : clock.Elapsed.TotalSeconds - 2 + document.Model.Chart.OffsetMs / 1000;
    private void Tick(object? sender, EventArgs e)
    {
        double now = clock.Elapsed.TotalSeconds, delta = now - previous; previous = now;
        if (adventure != null)
        {
            float dx = (keys.Contains(Key.D) ? 1 : 0) - (keys.Contains(Key.A) ? 1 : 0), dy = (keys.Contains(Key.S) ? 1 : 0) - (keys.Contains(Key.W) ? 1 : 0);
            adventure.Move(dx, dy, delta, keys.Contains(Key.LeftShift) || keys.Contains(Key.RightShift)); MapView.Player = adventure.Position; MapView.InvalidateVisual();
        }
        if (rhythm != null)
        {
            if (!audioStarted && now >= 2) { audioStarted = true; audio?.Play(); }
            if (audio != null && !audio.IsAvailable) { string error = audio.Error ?? "音訊裝置中斷"; StopPreview(); Status.Text = error; return; }
            double time = SongTime; rhythm.CollectMisses(time); Timeline.PlayTime = time;
            TimeScroll.Value = Math.Clamp(time - 2, 0, TimeScroll.Maximum); Timeline.ViewStart = TimeScroll.Value; Timeline.InvalidateVisual();
            ChartStats.Text = $"{Math.Max(0, time):0.00} 秒 · 分數 {rhythm.Score} · COMBO {rhythm.Combo} · MISS {rhythm.MissCount}";
            if (time > document.Model.Chart.Duration + 1 || (audioStarted && audio != null && !audio.IsPlaying)) { int score = rhythm.Score, misses = rhythm.MissCount; StopPreview(); Status.Text = $"預覽結束 · 分數 {score} · MISS {misses}"; }
        }
    }
    private void StopPreview()
    {
        timer.Stop(); clock.Stop(); audio?.Dispose(); audio = null; adventure = null; rhythm = null; keys.Clear(); MapView.Player = null; Timeline.PlayTime = null; Timeline.PreviewNotes = null;
        if (IsInitialized) { PlayButton.Content = "▶ 遊玩預覽"; Refresh(); }
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (IsPlaying)
        {
            e.Handled = true; if (e.Key == Key.Escape) { StopPreview(); return; } if (e.IsRepeat) return; keys.Add(e.Key);
            if (e.Key == Key.E && adventure != null) Status.Text = adventure.Interact();
            int lane = e.Key switch { Key.D => 0, Key.F => 1, Key.J => 2, Key.K => 3, _ => -1 };
            if (lane >= 0 && rhythm != null) { var hit = rhythm.TryHit(SongTime, lane); if (hit != null) Status.Text = $"{hit.Judgment.Kind} · {hit.Judgment.DeltaSeconds * 1000:+0.0;-0.0;0} ms"; }
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key == Key.S) { SaveDocument(); e.Handled = true; }
        // Preserve native text-box undo while editing text; document undo applies on the canvas.
        if (e.OriginalSource is TextBox) return;
        if (e.Key == Key.Z) { Undo_Click(sender, e); e.Handled = true; } if (e.Key == Key.Y) { Redo_Click(sender, e); e.Handled = true; }
    }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!ConfirmLeave()) e.Cancel = true; }
    public async Task SmokeAsync(string output)
    {
        smoke = true; output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        document.Edit(p => { p.Name = "森之宿 · 茶室庭園"; p.Scenes[0].Tiles[2 * 24 + 2] = TileKind.Path; }); document.Undo(); document.Redo();
        string project = Path.Combine(output, "sample.mtproject"); document.Save(project); document = ProjectDocument.Open(project); Refresh();
        StartPreview(); if (adventure == null) throw new Exception("Adventure preview failed");
        for (int i = 0; i < 80; i++) adventure.Move(-1, 0, .1);
        if (adventure.Position.X < 40) throw new Exception("Collision preview crossed wall"); StopPreview(); Refresh();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); Capture(Path.Combine(output, "editor-map.png"));
        Workspace.SelectedIndex = 1; StartPreview(); if (rhythm == null) throw new Exception("Rhythm preview failed");
        for (int i = 0; i < 4; i++) if (rhythm.TryHit(2 + i * .5, i) == null) throw new Exception("Rhythm hit failed"); int score = rhythm.Score;
        if (score != 400) throw new Exception("Rhythm score mismatch"); StopPreview(); Refresh();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); Capture(Path.Combine(output, "editor-chart.png"));
        document.ExportAtr(Path.Combine(output, "sample.atr"));
        string before = document.Serialize(); EntityX.Text = "invalid"; ApplyEntity_Click(this, new()); if (document.Serialize() != before || !Status.Text.StartsWith("無法完成")) throw new Exception("Invalid inspector edit was not handled"); Refresh();
        string song = Path.Combine(output, "preview.wav"); ProceduralScore.WriteWave(song, 3, 120, 60, 0); document.ImportAudio(song); document.Save(project); Refresh(); StartPreview();
        await Task.Delay(2400); bool device = audio?.IsAvailable == true && audio.PositionSeconds > .1; StopPreview(); if (!device) throw new Exception("Editor audio device did not advance");
        File.WriteAllText(Path.Combine(output, "editor-smoke.json"), JsonSerializer.Serialize(new { passed = true, collision = true, score, saveLoad = true, undoRedo = true, atrExport = true, deviceAudio = device, invalidInspectorRollback = true, icon = "branding/matrixtea.svg", version = "0.1.0-test.1" }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private void Capture(string path)
    {
        UpdateLayout(); var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
