// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MatrixTea.Editor.Core;
using MatrixTea.Engine.Core.Adventure;

namespace MatrixTea.Editor;

public sealed class SceneView : FrameworkElement
{
    public SceneModel? Scene { get; set; }
    public string? SelectedId { get; set; }
    public WorldPosition? Player { get; set; }
    public Func<int> BrushIndex { get; set; } = () => 0;
    public Action<string>? SelectEntity { get; set; }
    public Action<Dictionary<int, TileKind>>? Paint { get; set; }
    private readonly Dictionary<int, TileKind> stroke = [];
    private bool drawing;
    private double Scale => Scene == null ? 1 : Math.Max(.1, Math.Min((ActualWidth - 24) / Scene.Width, (ActualHeight - 24) / Scene.Height));
    private Point Origin => Scene == null ? new() : new((ActualWidth - Scene.Width * Scale) / 2, (ActualHeight - Scene.Height * Scale) / 2);
    private static readonly Brush[] colors = [ColorBrush("#244940"), ColorBrush("#3A4C55"), ColorBrush("#275F79"), ColorBrush("#ACAB8D"), ColorBrush("#356346")];
    public SceneView()
    {
        Focusable = true;
        MouseLeftButtonDown += (_, e) =>
        {
            if (Scene == null || Player != null) return;
            Focus(); var (x, y) = Cell(e.GetPosition(this)); if (x < 0 || y < 0 || x >= Scene.Width || y >= Scene.Height) return;
            if (BrushIndex() == 0) { var entity = Scene.Entities.LastOrDefault(o => o.X == x && o.Y == y); if (entity != null) SelectEntity?.Invoke(entity.Id); }
            else { drawing = true; CaptureMouse(); AddCell(x, y); }
        };
        MouseMove += (_, e) => { if (drawing) { var (x, y) = Cell(e.GetPosition(this)); AddCell(x, y); } };
        MouseLeftButtonUp += (_, _) => FinishStroke(); LostMouseCapture += (_, _) => { if (drawing) FinishStroke(); };
        SizeChanged += (_, _) => InvalidateVisual();
    }
    private (int, int) Cell(Point point) => ((int)Math.Floor((point.X - Origin.X) / Scale), (int)Math.Floor((point.Y - Origin.Y) / Scale));
    private void AddCell(int x, int y)
    {
        if (Scene == null || x < 0 || y < 0 || x >= Scene.Width || y >= Scene.Height) return;
        var kind = (TileKind)(BrushIndex() - 1); if (kind is TileKind.Wall or TileKind.Water && Scene.Entities.Any(e => e.X == x && e.Y == y)) return;
        stroke[y * Scene.Width + x] = kind; InvalidateVisual();
    }
    private void FinishStroke() { if (!drawing) return; drawing = false; ReleaseMouseCapture(); var cells = new Dictionary<int, TileKind>(stroke); stroke.Clear(); if (cells.Count > 0) Paint?.Invoke(cells); InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); dc.DrawRectangle(ColorBrush("#0B161E"), null, new(0, 0, ActualWidth, ActualHeight)); if (Scene == null) return;
        double s = Scale; Point o = Origin;
        for (int y = 0; y < Scene.Height; y++) for (int x = 0; x < Scene.Width; x++)
        {
            int index = y * Scene.Width + x; TileKind kind = stroke.GetValueOrDefault(index, Scene.Tiles[index]);
            var rect = new Rect(o.X + x * s, o.Y + y * s, s, s); dc.DrawRectangle(colors[(int)kind], new Pen(ColorBrush("#122B31"), .5), rect);
            if (kind == TileKind.Flowers) { dc.DrawEllipse(ColorBrush("#E4B36C"), null, new(rect.X + s * .35, rect.Y + s * .35), s * .08, s * .08); dc.DrawEllipse(ColorBrush("#C79DDD"), null, new(rect.X + s * .65, rect.Y + s * .65), s * .08, s * .08); }
            if (kind == TileKind.Water) dc.DrawLine(new Pen(ColorBrush("#41879D"), 1), new(rect.X + s * .2, rect.Y + s * .55), new(rect.X + s * .8, rect.Y + s * .55));
        }
        foreach (var entity in Scene.Entities)
        {
            Point p = new(o.X + (entity.X + .5) * s, o.Y + (entity.Y + .5) * s);
            dc.DrawEllipse(entity.Kind == EntityKind.Spawn ? ColorBrush("#98DFBC") : ColorBrush("#E4B36C"), new Pen(ColorBrush("#132C31"), 2), p, s * .30, s * .30);
            string text = entity.Kind switch { EntityKind.Spawn => "S", EntityKind.Guest => "G", EntityKind.Door => "D", EntityKind.Resource => "R", _ => "♪" };
            DrawText(dc, text, new(p.X - s * .15, p.Y - s * .25), Math.Max(10, s * .35), ColorBrush("#102C2A"));
            if (entity.Id == SelectedId) dc.DrawRoundedRectangle(null, new Pen(ColorBrush("#FFF1CE"), 2), new(p.X - s * .45, p.Y - s * .45, s * .9, s * .9), 5, 5);
        }
        if (Player is WorldPosition player) dc.DrawEllipse(ColorBrush("#E9F9FF"), new Pen(ColorBrush("#83B5E4"), 3), new(o.X + player.X / 32 * s, o.Y + player.Y / 32 * s), s * .26, s * .26);
    }
    internal static SolidColorBrush ColorBrush(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    internal static void DrawText(DrawingContext dc, string text, Point p, double size, Brush brush) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI, Microsoft JhengHei"), size, brush, 1.0), p);
}
public sealed class ChartView : FrameworkElement
{
    public ChartModel? Chart { get; set; }
    public IEnumerable<NoteModel>? PreviewNotes { get; set; }
    public double ViewStart { get; set; }
    public double? PlayTime { get; set; }
    public Action<int, double, bool>? ToggleNote { get; set; }
    public ChartView()
    {
        Focusable = true; SizeChanged += (_, _) => InvalidateVisual();
        MouseDown += (_, e) => { if (Chart == null || PlayTime != null) return; var p = e.GetPosition(this); int lane = (int)((p.X - 40) / Math.Max(1, (ActualWidth - 55) / 4)); if (lane is < 0 or > 3 || p.Y < 35 || p.Y > ActualHeight - 18) return; ToggleNote?.Invoke(lane, Chart.Snap(ViewStart + (p.Y - 35) / Math.Max(1, ActualHeight - 55) * 8), e.ChangedButton == MouseButton.Right); };
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); dc.DrawRectangle(SceneView.ColorBrush("#0B161E"), null, new(0, 0, ActualWidth, ActualHeight)); if (Chart == null || ActualWidth < 60 || ActualHeight < 60) return;
        double width = (ActualWidth - 55) / 4, height = ActualHeight - 55;
        string[] colors = ["#83D6B2", "#83B5E4", "#C79DDD", "#E4B36C"];
        for (int lane = 0; lane < 4; lane++) { dc.DrawRectangle(SceneView.ColorBrush(lane % 2 == 0 ? "#172D36" : "#132630"), null, new(40 + lane * width, 35, width - 2, height)); SceneView.DrawText(dc, new[] { "D", "F", "J", "K" }[lane], new(40 + lane * width + width / 2 - 6, 8), 15, SceneView.ColorBrush(colors[lane])); }
        double beat = 60 / Chart.Bpm;
        for (double t = Math.Ceiling(ViewStart / beat) * beat; t <= Math.Min(Chart.Duration, ViewStart + 8); t += beat)
        {
            double y = 35 + (t - ViewStart) / 8 * height; dc.DrawLine(new Pen(SceneView.ColorBrush("#36525B"), 1), new(40, y), new(ActualWidth - 15, y)); SceneView.DrawText(dc, t.ToString("0.0", CultureInfo.InvariantCulture), new(0, y - 6), 10, SceneView.ColorBrush("#8BAAAC"));
        }
        foreach (var note in PreviewNotes ?? Chart.Notes)
        {
            if (note.Time < ViewStart || note.Time > ViewStart + 8) continue; double y = 35 + (note.Time - ViewStart) / 8 * height;
            dc.DrawRoundedRectangle(SceneView.ColorBrush(colors[note.Lane]), null, new(48 + note.Lane * width, y - 5, Math.Max(1, width - 18), 10), 4, 4);
        }
        if (PlayTime is double time && time >= ViewStart && time <= ViewStart + 8) { double y = 35 + (time - ViewStart) / 8 * height; dc.DrawLine(new Pen(SceneView.ColorBrush("#F9F0D7"), 2), new(35, y), new(ActualWidth - 10, y)); }
    }
}
