using Godot;
using System;
using System.Collections.Generic;

/// <summary>Grid rules over the actual Terrain3D surface and authored level props.</summary>
public sealed class TacticalMap
{
    public const int Size = 16;
    public const float CellSize = 1.25f;
    public const float Center = 16f;
    private readonly GodotObject _data;
    private readonly List<(Vector3 Position, Vector2 Size)> _bridges = new();
    private readonly List<(Vector3 Position, float Radius)> _obstacles = new();

    public TacticalMap(Node3D level)
    {
        var terrain = level.GetNode<Node3D>("Terrain3D");
        if (!terrain.IsClass("Terrain3D")) throw new InvalidOperationException("Starting level needs Terrain3D.");
        _data = terrain.Get("data").AsGodotObject();
        foreach (Node3D bridge in level.GetNode("Bridges").GetChildren())
            _bridges.Add((bridge.GlobalPosition, bridge.GetMeta("bridge_size").AsVector2()));
        foreach (Node3D prop in level.GetNode("LevelProps").GetChildren())
            if (prop.GetMeta("blocks_movement", false).AsBool())
                _obstacles.Add((prop.GlobalPosition, prop.GetMeta("blocking_radius", 0.58f).AsSingle()));
    }

    public bool InBounds(Vector2I cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Size && cell.Y < Size;

    public Vector2I WorldToCell(Vector3 point) => new(
        Mathf.RoundToInt((point.X - Center) / CellSize + (Size - 1) / 2f),
        Mathf.RoundToInt((point.Z - Center) / CellSize + (Size - 1) / 2f));

    public Vector3 CellPosition(Vector2I cell)
    {
        var position = new Vector3(Center + (cell.X - (Size - 1) / 2f) * CellSize, 0,
            Center + (cell.Y - (Size - 1) / 2f) * CellSize);
        position.Y = SurfaceHeight(position) + 0.045f;
        return position;
    }

    public float SurfaceHeight(Vector3 position)
    {
        foreach (var bridge in _bridges)
            if (Mathf.Abs(position.X - bridge.Position.X) <= bridge.Size.X / 2f &&
                Mathf.Abs(position.Z - bridge.Position.Z) <= bridge.Size.Y / 2f)
                return bridge.Position.Y;
        return _data.Call("get_height", position).AsSingle();
    }

    public bool Walkable(Vector2I cell)
    {
        if (!InBounds(cell)) return false;
        var point = CellPosition(cell);
        if (!float.IsFinite(point.Y) || point.Y < -0.06f) return false;
        foreach (var obstacle in _obstacles)
            if (new Vector2(point.X - obstacle.Position.X, point.Z - obstacle.Position.Z).Length() < obstacle.Radius + 0.22f)
                return false;
        return true;
    }

    public bool CanTraverse(Vector2I start, Vector2I end)
    {
        if (!Walkable(start) || !Walkable(end)) return false;
        var a = CellPosition(start);
        var b = CellPosition(end);
        if (Mathf.Abs(a.Y - b.Y) > 0.8f) return false;
        // Reject a gap, steep bank or water between two otherwise valid cell centers.
        float previous = a.Y - 0.045f;
        for (int i = 1; i <= 8; i++)
        {
            float height = SurfaceHeight(a.Lerp(b, i / 8f));
            if (!float.IsFinite(height) || height < -0.105f || Mathf.Abs(height - previous) > 0.4f) return false;
            previous = height;
        }
        return true;
    }

    public HashSet<Vector2I> BlockedCells()
    {
        var blocked = new HashSet<Vector2I>();
        for (int x = 0; x < Size; x++)
            for (int z = 0; z < Size; z++)
                if (!Walkable(new Vector2I(x, z))) blocked.Add(new Vector2I(x, z));
        return blocked;
    }

    public List<Vector2I>? FindPath(Vector2I start, Vector2I goal, HashSet<Vector2I> occupied) =>
        GridPathfinder.Find(start, goal, Size, occupied, Walkable, CanTraverse);

    public static bool IsObjective(Vector2I cell) => cell.X is 7 or 8 && cell.Y is 7 or 8;

    public bool RayCell(Vector3 origin, Vector3 direction, out Vector2I cell)
    {
        // Sample the same height data used by movement. This also picks bridge decks
        // without depending on Terrain3D's camera-centered dynamic collision timing.
        float previousDistance = 0;
        bool aboveSurface = false;
        for (float distance = 0; distance < 160; distance += 0.2f)
        {
            var point = origin + direction * distance;
            var candidate = WorldToCell(point);
            if (!InBounds(candidate)) { aboveSurface = false; continue; }
            float height = SurfaceHeight(point);
            if (!float.IsFinite(height)) continue;
            if (point.Y > height) { previousDistance = distance; aboveSurface = true; continue; }
            if (!aboveSurface) continue;
            float low = previousDistance, high = distance;
            for (int i = 0; i < 8; i++)
            {
                float mid = (low + high) / 2f;
                var sample = origin + direction * mid;
                if (sample.Y > SurfaceHeight(sample)) low = mid; else high = mid;
            }
            cell = WorldToCell(origin + direction * high);
            return InBounds(cell);
        }
        cell = default;
        return false;
    }
}
