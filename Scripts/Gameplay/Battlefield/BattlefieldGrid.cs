using System;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Gameplay;

/// <summary>Board coordinates over the existing level, without depending on a Godot scene.</summary>
public static class BattlefieldGrid
{
    public const int Size = 14;
    public const double CellSize = 1.25;
    public const double WorldCenter = 16;
    public static bool Contains(GridPosition cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Size && cell.Y < Size;
    public static (double X, double Z) CellCenter(GridPosition cell)
        => (WorldCenter + (cell.X - (Size - 1) / 2.0) * CellSize,
            WorldCenter + (cell.Y - (Size - 1) / 2.0) * CellSize);
    public static GridPosition WorldToCell(double x, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z)) throw new ArgumentOutOfRangeException(nameof(x));
        return new(checked((int)Math.Floor((x - WorldCenter) / CellSize + Size / 2.0)),
            checked((int)Math.Floor((z - WorldCenter) / CellSize + Size / 2.0)));
    }
}

/// <summary>Read-only authored terrain information. It grants no canonical terrain costs.</summary>
public interface IBattlefieldTerrain
{
    bool? IsWalkable(GridPosition cell);
    double? Height(GridPosition cell);
    bool? PrototypeTransition(GridPosition from, GridPosition to);
}

public sealed record BattlefieldDevelopmentRules(bool EnablePrototypeMovement, bool EnableOpenTargeting,
    bool? InterruptedMovementConsumesOpportunity, int StartingTeamId = 1)
{
    public string Description => $"DEVELOPMENT FIXTURES: movement={EnablePrototypeMovement}; open targeting={EnableOpenTargeting}; " +
        $"interrupted Movement spent={InterruptedMovementConsumesOpportunity?.ToString() ?? "UNRESOLVED"}; first team={StartingTeamId}. " +
        "Axe/Lance/Bow Power 4/3/4; accuracy 90%; provisional follow-ups. Not canonical balance.";
}
