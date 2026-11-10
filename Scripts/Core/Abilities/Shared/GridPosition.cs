using System;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>An orthogonal tile coordinate with no terrain, map bounds or Godot dependency.</summary>
public readonly record struct GridPosition(int X, int Y)
{
    public long ManhattanDistanceTo(GridPosition other)
        => Math.Abs((long)X - other.X) + Math.Abs((long)Y - other.Y);
}
