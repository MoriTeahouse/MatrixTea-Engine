// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Drawing = System.Drawing;

namespace MatrixTea.Engine.Desktop;

/// <summary>Glyph cache whose size depends on characters and font sizes, not changing scores or colors.</summary>
public sealed class GlyphTextRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Dictionary<(char Character, int Size), Glyph> _glyphs = new();
    private readonly Dictionary<int, Drawing.Font> _fonts = new();
    private sealed record Glyph(Texture2D Texture, float Advance);
    public GlyphTextRenderer(GraphicsDevice device) => _device = device;
    public int CachedGlyphs => _glyphs.Count;
    public Vector2 Measure(string text, int size = 20)
    {
        size=Math.Clamp(size,10,72);
        float width = 0, maximum = 0; int lines = 1;
        foreach (char character in text)
        {
            if (character == '\n') { maximum = Math.Max(maximum, width); width = 0; lines++; }
            else width += Get(character, size).Advance;
        }
        return new(Math.Max(maximum, width), lines * size * 1.4f);
    }
    public void Draw(SpriteBatch batch, string text, Vector2 position, Color color, int size = 20, float scale = 1)
    {
        size=Math.Clamp(size,10,72);
        if(!float.IsFinite(scale)||scale<=0)throw new ArgumentOutOfRangeException(nameof(scale));
        float x = position.X, y = position.Y;
        foreach (char character in text)
        {
            if (character == '\n') { x = position.X; y += size * 1.4f * scale; continue; }
            var glyph = Get(character, size);
            batch.Draw(glyph.Texture, new Vector2(x, y), null, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            x += glyph.Advance * scale;
        }
    }
    public string Wrap(string text, float maximumWidth, int size = 20)
    {
        size=Math.Clamp(size,10,72);
        if(!float.IsFinite(maximumWidth)||maximumWidth<=0)throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        var result = new System.Text.StringBuilder(); float width = 0;
        foreach (char character in text)
        {
            if (character == '\n') { result.Append(character); width = 0; continue; }
            float advance = Get(character, size).Advance;
            if (width > 0 && width + advance > maximumWidth) { result.Append('\n'); width = 0; }
            result.Append(character); width += advance;
        }
        return result.ToString();
    }
    private Glyph Get(char character, int size)
    {
        size = Math.Clamp(size, 10, 72);
        if (_glyphs.TryGetValue((character, size), out var glyph)) return glyph;
        if (!_fonts.TryGetValue(size, out var font)) _fonts.Add(size, font = new Drawing.Font("Microsoft JhengHei", size, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Pixel));
        using var measureBitmap = new Drawing.Bitmap(1, 1); using var measure = Drawing.Graphics.FromImage(measureBitmap);
        using var format = (Drawing.StringFormat)Drawing.StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= Drawing.StringFormatFlags.MeasureTrailingSpaces;
        var measured = measure.MeasureString(character.ToString(), font, 100, format);
        int width = Math.Max(1, (int)Math.Ceiling(measured.Width) + 4), height = size * 2;
        using var bitmap = new Drawing.Bitmap(width, height); using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(Drawing.Color.Transparent); graphics.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(character.ToString(), font, Drawing.Brushes.White, 0, 0, format);
        using var stream = new MemoryStream(); bitmap.Save(stream, Drawing.Imaging.ImageFormat.Png); stream.Position = 0;
        glyph = new(Texture2D.FromStream(_device, stream), Math.Max(1, measured.Width));
        _glyphs.Add((character, size), glyph); return glyph;
    }
    public void Dispose() { foreach (var glyph in _glyphs.Values) glyph.Texture.Dispose(); foreach (var font in _fonts.Values) font.Dispose(); _glyphs.Clear(); _fonts.Clear(); }
}
