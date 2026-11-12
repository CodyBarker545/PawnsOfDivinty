using Godot;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>The scene's single board configuration. Values are serialized explicitly in the map.</summary>
[Tool, GlobalClass]
public partial class BetaBoardDefinition : Resource
{
    [Export] public int Width { get; set; }
    [Export] public int Height { get; set; }
    [Export] public double CellSize { get; set; }
    [Export] public Vector2 WorldCenter { get; set; }
    [Export] public bool RequireMirroredLayout { get; set; }
    [Export] public bool RequireDeploymentSpawns { get; set; }
    public RectangularBoard ToBoard() => new(Width, Height, CellSize, WorldCenter.X, WorldCenter.Y);
}
