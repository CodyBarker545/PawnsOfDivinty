using System;
using System.Collections.Generic;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Terrain;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Places Smoke Bomb and teleports within the same Main Action before finalization.</summary>
/// <remarks>
/// Match state must stay on one thread. The map callback only inspects placement legality;
/// it must not change characters, phases, budgets or smoke. No callback runs after validation.
/// This does not add clipping, walkability or reveal-reset rules.
/// </remarks>
public sealed class SmokeBombAbilityExecutor
{
    private readonly SmokePlacementService _smoke;
    private readonly SmokeTeleportService _teleports;
    private static readonly AbilityTargetingConstraints CenterTargetRules = new(AbilityTargetType.Self,
        allowSelf: true, team: AbilityTargetTeam.Ally, requireLivingTarget: true,
        requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);

    public SmokeBombAbilityExecutor(SmokePlacementService smoke)
    {
        ArgumentNullException.ThrowIfNull(smoke);
        _smoke = smoke;
        _teleports = new(smoke);
    }

    /// <summary>Checks a caster-centered placement without creating smoke or reserving the supplied effect ID.</summary>
    public SmokeBombUseResult Preview(AbilityUse use, string effectId, Func<GridPosition, bool> canCoverTile)
    {
        var placement = PreviewPlacement(use, effectId, canCoverTile);
        if (!placement.Eligibility.IsAllowed || placement.RejectionReason != SmokeBombRejectionReason.None
            || placement.Targeting?.IsLegal != true || placement.Placement?.IsLegal != true) return placement;
        return new(use, placement.Eligibility, SmokeBombRejectionReason.TeleportRequired,
            placement.Targeting, placement.Placement);
    }

    private SmokeBombUseResult PreviewPlacement(AbilityUse use, string effectId, Func<GridPosition, bool> canCoverTile)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        ArgumentNullException.ThrowIfNull(canCoverTile);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(use, approval.Eligibility);
        if (use.AbilityId != HoodedAssassin.SmokeBombId)
            return new(use, new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        if (!SmokeVisibility.IsEligibleAssassin(use.Character))
            return new(use, approval.Eligibility, SmokeBombRejectionReason.NotAssassin);
        if (use.Character.TacticalPosition is not { } center)
            return new(use, approval.Eligibility, SmokeBombRejectionReason.MissingPosition);
        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        if (ability.RequiredAction != AbilityActionType.MainAction)
            return new(use, approval.Eligibility, SmokeBombRejectionReason.UnsupportedActionMetadata);

        // Self-centering needs no line of sight to another tile. Smoke-placement map
        // legality is checked for all 25 tiles by the placement service below.
        var targeting = AbilityTargeting.Validate(use.Character, center, ability,
            new AbilityTarget(AbilityTargetType.Self), CenterTargetRules);
        if (!targeting.IsLegal) return new(use, approval.Eligibility, targeting: targeting);
        var placement = _smoke.ValidatePlacement(effectId, use.CharacterId, center, use.RoundNumber, canCoverTile);
        return new(use, approval.Eligibility, targeting: targeting, placement: placement);
    }

    /// <summary>Compatibility overload: a cast without a teleport destination is explicitly rejected.</summary>
    public SmokeBombUseResult Execute(AbilityUse use, string effectId, Func<GridPosition, bool> canCoverTile)
        => Preview(use, effectId, canCoverTile);

    /// <summary>Previews both effects against the proposed cloud without committing either.</summary>
    public SmokeBombUseResult Preview(AbilityUse use, string effectId, Func<GridPosition, bool> canCoverTile,
        GridPosition? destination, ISmokeTeleportMap? map,
        IEnumerable<(Character Character, GridPosition Position)> participants,
        SmokeTeleportConstraints? constraints = null)
    {
        ArgumentNullException.ThrowIfNull(participants);
        var placement = PreviewPlacement(use, effectId, canCoverTile);
        if (!placement.Eligibility.IsAllowed || placement.RejectionReason != SmokeBombRejectionReason.None
            || placement.Targeting?.IsLegal != true || placement.Placement?.IsLegal != true) return placement;
        var teleport = _teleports.PrepareCast(use.Character, destination, map, placement.Placement, participants, constraints);
        return new(use, placement.Eligibility, targeting: placement.Targeting, placement: placement.Placement,
            teleportValidation: teleport.Validation, preparedVisibility: teleport.Visibility);
    }

    /// <summary>Revalidates, places the cloud, teleports, then finalizes the one Main Action and cooldown.</summary>
    public SmokeBombUseResult Execute(AbilityUse use, string effectId, Func<GridPosition, bool> canCoverTile,
        GridPosition? destination, ISmokeTeleportMap? map,
        IEnumerable<(Character Character, GridPosition Position)> participants,
        SmokeTeleportConstraints? constraints = null)
    {
        var preview = Preview(use, effectId, canCoverTile, destination, map, participants, constraints);
        if (!preview.CanExecute) return preview;
        // All map queries are finished. Validate bookkeeping once more before the first mutation.
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(use, approval.Eligibility, targeting: preview.Targeting, placement: preview.Placement);
        var area = _smoke.PlaceValidated(preview.Placement!);
        var teleport = _teleports.CommitPrepared(preview.TeleportValidation!, preview.PreparedVisibility!);
        var finalization = AbilityUseFinalizer.FinalizeSuccessfulUse(use);
        return new(use, preview.Eligibility, targeting: preview.Targeting, placement: preview.Placement,
            createdSmoke: area, finalization: finalization, teleportValidation: preview.TeleportValidation, teleport: teleport);
    }
}
