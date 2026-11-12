using System;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Engine-independent extraction boundary, also used by headless scene/round-trip tests.</summary>
public sealed record AuthoredElementData
{
    public string Path { get; init; } = "";
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public bool HasIdentityBasis { get; init; } = true;
    public GridPosition[] CellOffsets { get; init; } = { new(0, 0) };
    public string CastleId { get; init; } = "";
    public string Role { get; init; } = "";
    public int TeamId { get; init; }
    public bool IsHome { get; init; }
    public bool BlocksMovement { get; init; }
    public string TerrainTag { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string DefinitionId { get; init; } = "";
    public string? ModelPath { get; init; }
}

public sealed record AuthoredLevelData(RectangularBoard Board, bool RequireMirroredLayout, AuthoredElementData[] Elements,
    bool RequireDeploymentSpawns=false);

public sealed class LevelAuthoringException : Exception
{
    public LevelAuthoringException(System.Collections.Generic.IEnumerable<string> errors)
        : base("Invalid authored battlefield:\n" + string.Join("\n", errors)) { }
}
