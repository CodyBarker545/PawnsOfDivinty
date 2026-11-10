using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Checks activation permission and existing teleport legality without assigning an undocumented action cost.</summary>
/// <remarks>
/// Spec v0.2 sections 3 and 12.1 describe teleportation within Smoke Bomb's cast sequence.
/// They do not assign a separate teleport action to an existing cloud. The current Smoke Bomb
/// executor ends activation after placement, so this coordinator cannot append a free teleport.
/// Even a legal destination is rejected with UnresolvedActionCost until that integration is defined.
/// Caller-supplied teleport constraints remain provisional decisions, not permission to bypass costs.
/// </remarks>
public sealed class SmokeTeleportActionCoordinator
{
    private readonly SmokeTeleportService _teleports;
    private readonly ActivationManager _activations;

    public SmokeTeleportActionCoordinator(SmokeTeleportService teleports, ActivationManager activations)
    {
        ArgumentNullException.ThrowIfNull(teleports);
        ArgumentNullException.ThrowIfNull(activations);
        _teleports = teleports;
        _activations = activations;
    }

    /// <summary>Checks current activation and destination rules without moving, spending actions or recording cooldowns.</summary>
    public SmokeTeleportActionResult Preview(Character caster, GridPosition? destination, ISmokeTeleportMap? map,
        SmokeTeleportConstraints? constraints = null)
    {
        ArgumentNullException.ThrowIfNull(caster);
        constraints ??= new();
        if (!_activations.IsAbilityPhase) return Result(SmokeTeleportActionRejectionReason.WrongPhase);
        if (caster.IsDefeated) return Result(SmokeTeleportActionRejectionReason.CasterDefeated);
        if (!SmokeVisibility.IsEligibleAssassin(caster)) return Result(SmokeTeleportActionRejectionReason.NotAssassin);
        if (!ReferenceEquals(_activations.ActiveCharacter, caster))
            return Result(SmokeTeleportActionRejectionReason.CharacterNotActive);
        if (!caster.CanAct) return Result(SmokeTeleportActionRejectionReason.ActionUnavailable);

        var validation = _teleports.Validate(caster, destination, map, constraints);
        if (!validation.IsLegal) return Result(SmokeTeleportActionRejectionReason.TeleportIllegal, validation);
        // Destination legality is necessary but does not establish how to pay for this action.
        // Do not call the low-level Execute or select a Movement/Utility/Main default here.
        return Result(SmokeTeleportActionRejectionReason.UnresolvedActionCost, validation);

        SmokeTeleportActionResult Result(SmokeTeleportActionRejectionReason reason, SmokeTeleportValidationResult? validation = null)
            => new(this, caster, _activations, destination, constraints, reason, validation);
    }

    /// <summary>Checks the preview's context and revalidates current conditions before considering execution.</summary>
    /// <remarks>
    /// No execution path is enabled while the action-cost rule is unresolved. In particular,
    /// repeated calls cannot move the caster for free or reset attack reveal. Existing low-level
    /// teleport and visibility calculations remain unchanged. Map queries must only inspect state.
    /// </remarks>
    public SmokeTeleportActionResult Execute(Character caster, SmokeTeleportActionResult preview, ISmokeTeleportMap? map)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(preview);
        if (!ReferenceEquals(preview.Coordinator, this) || !ReferenceEquals(preview.Caster, caster))
            return Rejected(SmokeTeleportActionRejectionReason.InvalidPreviewContext);
        if (preview.RoundNumber != _activations.CurrentRound)
            return Rejected(SmokeTeleportActionRejectionReason.RoundChanged);
        if (preview.SourcePosition != caster.TacticalPosition)
            return Rejected(SmokeTeleportActionRejectionReason.SourcePositionChanged);
        return Preview(caster, preview.DestinationPosition, map, preview.Constraints);

        SmokeTeleportActionResult Rejected(SmokeTeleportActionRejectionReason reason)
            => new(this, caster, _activations, preview.DestinationPosition, preview.Constraints, reason);
    }
}
