namespace PawnsOfDivinity.Core.Abilities;

/// <summary>An immutable position-update result. A successful teleport does not imply invisibility.</summary>
public sealed record SmokeTeleportResult
{
    public SmokeTeleportValidationResult Validation { get; }
    public bool Succeeded => Validation.IsLegal;
    public GridPosition? OldPosition { get; }
    public GridPosition? NewPosition { get; }
    public SmokeVisibilityResult? Visibility { get; }

    internal SmokeTeleportResult(SmokeTeleportValidationResult validation, GridPosition? oldPosition,
        GridPosition? newPosition, SmokeVisibilityResult? visibility)
    {
        Validation = validation;
        OldPosition = oldPosition;
        NewPosition = newPosition;
        Visibility = visibility;
    }
}
