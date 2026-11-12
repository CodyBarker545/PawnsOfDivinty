using Godot;

namespace PawnsOfDivinity.Gameplay.Levels;

public enum BetaElementKind { Terrain, Castle, CaptureZone, DeploymentZone, Camp, Objective, Obstacle, Entrance, Route, Connection }

/// <summary>Serialized authoring data. The node transform, never a stored absolute cell, locates its footprint.</summary>
[Tool, GlobalClass]
public partial class BetaLevelElement : Node3D
{
    [Export] public string ElementId { get; set; } = "";
    [Export] public BetaElementKind Kind { get; set; }
    [Export] public Godot.Collections.Array<Vector2I> CellOffsets { get; set; } = new() { Vector2I.Zero };
    [Export] public string CastleId { get; set; } = "";
    [Export] public string Role { get; set; } = "";
    [Export] public int InitialTeamId { get; set; }
    [Export] public bool IsHome { get; set; }
    [Export] public bool BlocksMovement { get; set; }
    [Export] public string TerrainTag { get; set; } = "";
}
