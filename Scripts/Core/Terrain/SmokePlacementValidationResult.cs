using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Core.Terrain;

public enum SmokePlacementRejectionReason
{
    None,
    DuplicateEffectId,
    IllegalTile
}

/// <summary>A proposed footprint, not an effect that has been placed on the battlefield.</summary>
public sealed record SmokePlacementValidationResult
{
    public bool IsLegal => RejectionReason == SmokePlacementRejectionReason.None;
    public SmokePlacementRejectionReason RejectionReason { get; }
    public SmokeArea? ProposedArea { get; }
    public GridPosition? RejectedTile { get; }
    internal SmokePlacementService Service { get; }

    internal SmokePlacementValidationResult(SmokePlacementService service, SmokePlacementRejectionReason reason,
        SmokeArea? proposedArea = null, GridPosition? rejectedTile = null)
    {
        Service = service;
        RejectionReason = reason;
        ProposedArea = proposedArea;
        RejectedTile = rejectedTile;
    }
}
