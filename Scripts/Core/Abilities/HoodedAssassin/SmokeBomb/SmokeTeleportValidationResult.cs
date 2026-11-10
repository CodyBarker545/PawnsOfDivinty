using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Terrain;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>A value snapshot of validation, internally bound to its caster, smoke service and explicit constraints.</summary>
public sealed record SmokeTeleportValidationResult
{
    public string CharacterId { get; }
    public GridPosition? SourcePosition { get; }
    public GridPosition? DestinationPosition { get; }
    public SmokeTeleportRejectionReason RejectionReason { get; }
    public bool IsLegal => RejectionReason == SmokeTeleportRejectionReason.None;
    internal Character Caster { get; }
    internal SmokePlacementService Smoke { get; }
    internal SmokeTeleportConstraints Constraints { get; }

    internal SmokeTeleportValidationResult(Character caster, SmokePlacementService smoke, SmokeTeleportConstraints constraints,
        GridPosition? source, GridPosition? destination, SmokeTeleportRejectionReason reason)
    {
        Caster = caster;
        Smoke = smoke;
        Constraints = constraints;
        CharacterId = caster.Id;
        SourcePosition = source;
        DestinationPosition = destination;
        RejectionReason = reason;
    }
}
