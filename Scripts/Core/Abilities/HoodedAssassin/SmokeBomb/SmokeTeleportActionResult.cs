using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Abilities;

public enum SmokeTeleportActionRejectionReason
{
    None,
    WrongPhase,
    CasterDefeated,
    NotAssassin,
    CharacterNotActive,
    ActionUnavailable,
    TeleportIllegal,
    UnresolvedActionCost,
    InvalidPreviewContext,
    RoundChanged,
    SourcePositionChanged
}

/// <summary>A decision and snapshots of position and existing budgets; it does not grant a free teleport.</summary>
public sealed record SmokeTeleportActionResult
{
    public string CharacterId { get; }
    public int RoundNumber { get; }
    public GridPosition? SourcePosition { get; }
    public GridPosition? DestinationPosition { get; }
    public GridPosition? ResultingPosition { get; }
    public SmokeTeleportActionRejectionReason RejectionReason { get; }
    public SmokeTeleportValidationResult? TeleportValidation { get; }
    public bool Succeeded => RejectionReason == SmokeTeleportActionRejectionReason.None;
    public bool PositionChanged => SourcePosition != ResultingPosition;
    // These describe the manager's existing budget, not actions consumed by this request.
    public bool MovementUsed { get; }
    public bool UtilityActionUsed { get; }
    public bool MainActionUsed { get; }

    internal SmokeTeleportActionCoordinator Coordinator { get; }
    internal Character Caster { get; }
    internal SmokeTeleportConstraints Constraints { get; }

    internal SmokeTeleportActionResult(SmokeTeleportActionCoordinator coordinator, Character caster,
        ActivationManager activations, GridPosition? destination, SmokeTeleportConstraints constraints,
        SmokeTeleportActionRejectionReason reason, SmokeTeleportValidationResult? validation = null)
    {
        Coordinator = coordinator;
        Caster = caster;
        Constraints = constraints;
        CharacterId = caster.Id;
        RoundNumber = activations.CurrentRound;
        SourcePosition = caster.TacticalPosition;
        DestinationPosition = destination;
        ResultingPosition = caster.TacticalPosition;
        RejectionReason = reason;
        TeleportValidation = validation;
        MovementUsed = activations.MovementUsed;
        UtilityActionUsed = activations.UtilityActionUsed;
        MainActionUsed = activations.MainActionUsed;
    }
}
