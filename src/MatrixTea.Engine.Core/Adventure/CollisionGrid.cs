// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Adventure;

public readonly record struct WorldPosition(float X, float Y);

/// <summary>Tile-indexed AABB collision with bounded substeps and axis sliding; no whole-map scan.</summary>
public sealed class CollisionGrid
{
    private readonly bool[] _blocked;
    public int Width { get; }
    public int Height { get; }
    public int TileSize { get; }
    public CollisionGrid(int width, int height, int tileSize, Func<int, int, bool> blocked)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        if (width < 1 || height < 1 || width > 4096 || height > 4096 || tileSize < 1 || tileSize > 1024) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width; Height = height; TileSize = tileSize; _blocked = new bool[checked(width * height)];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) _blocked[y * width + x] = blocked(x, y);
    }
    public bool IsBlocked(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height || _blocked[y * Width + x];
    public bool CanStand(WorldPosition position, float radius = 8)
    {
        Validate(position, radius);
        int left = (int)Math.Floor((position.X - radius) / TileSize), right = (int)Math.Floor((position.X + radius) / TileSize);
        int top = (int)Math.Floor((position.Y - radius) / TileSize), bottom = (int)Math.Floor((position.Y + radius) / TileSize);
        for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++) if (IsBlocked(x, y)) return false;
        return true;
    }
    public WorldPosition Move(WorldPosition position, float dx, float dy, float radius = 8)
    {
        Validate(position, radius);
        if (!float.IsFinite(dx) || !float.IsFinite(dy) || Math.Abs(dx) > 4096 || Math.Abs(dy) > 4096) throw new ArgumentOutOfRangeException(nameof(dx));
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / Math.Max(1, Math.Min(TileSize / 4f, radius))));
        float stepX = dx / steps, stepY = dy / steps;
        for (int i = 0; i < steps; i++)
        {
            var next = position with { X = position.X + stepX }; if (CanStand(next, radius)) position = next;
            next = position with { Y = position.Y + stepY }; if (CanStand(next, radius)) position = next;
        }
        return position;
    }
    private void Validate(WorldPosition position, float radius)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || Math.Abs(position.X) > Width * TileSize + 4096 || Math.Abs(position.Y) > Height * TileSize + 4096 || !float.IsFinite(radius) || radius <= 0 || radius > TileSize)
            throw new ArgumentOutOfRangeException(nameof(position));
    }
}
