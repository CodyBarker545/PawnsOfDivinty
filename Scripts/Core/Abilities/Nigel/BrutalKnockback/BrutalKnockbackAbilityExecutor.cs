using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Gameplay;

namespace PawnsOfDivinity.Core.Abilities;

public sealed record BrutalKnockbackUseResult
{
    public string AttackerId { get; }
    public string TargetId { get; }
    public string AbilityId { get; }
    public int RoundNumber { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public AbilityTargetingResult? Targeting { get; }
    public BrutalKnockbackEffectResult? Effect { get; }
    public GridPosition? OriginalTargetPosition { get; }
    public GridPosition? OriginalAttackerPosition { get; }
    public GridPosition? FinalTargetPosition { get; }
    public StrikeResult? CombatOutcome { get; }
    public BrutalKnockbackResolution? Resolution { get; }
    public CombatExchangeResult? Combat => Resolution?.Combat;
    public int? NormalAxeDamage => Effect?.Opening.NormalAxeDamage;
    public int FuryBonus => Effect?.Opening.FuryBonus ?? 0;
    public int? ReducedOpeningDamage => Effect?.Opening.ProvisionalDamage;
    public int TilesPushed => Resolution?.TilesPushed ?? 0;
    public bool TargetSurvivedOpening => Resolution?.TargetSurvivedOpening == true;
    public bool CounterattacksSuppressed => Combat?.CounterattacksSuppressed ?? true;
    public bool FollowUpsUnresolved => true;
    public IReadOnlyList<CollisionParticipantResult> CollisionParticipants => Resolution?.Collision?.Participants
        ?? Array.Empty<CollisionParticipantResult>();
    public IReadOnlyList<string> DefeatedCharacterIds => Resolution?.DefeatedCharacterIds ?? Array.Empty<string>();
    public BrutalKnockbackRejectionReason RejectionReason { get; }
    public int HealthLost { get; }
    public int ShieldAbsorbed { get; }
    public bool Displaced => OriginalTargetPosition != FinalTargetPosition;
    public AbilityUseResult? Finalization { get; }
    public bool CanExecute => !Succeeded && Resolution is null && Eligibility.IsAllowed && Targeting?.IsLegal == true
        && Effect?.CanExecute == true && RejectionReason == BrutalKnockbackRejectionReason.None;
    public bool Succeeded => Finalization?.Succeeded == true;

    internal BrutalKnockbackUseResult(AbilityUse use, Character target, AbilityEligibilityResult eligibility,
        AbilityTargetingResult? targeting = null, BrutalKnockbackEffectResult? effect = null,
        BrutalKnockbackResolution? resolution = null, AbilityUseResult? finalization = null,
        BrutalKnockbackRejectionReason reason = BrutalKnockbackRejectionReason.None)
    {
        AttackerId = use.CharacterId;
        TargetId = target.Id;
        AbilityId = use.AbilityId;
        RoundNumber = use.RoundNumber;
        Eligibility = eligibility;
        Targeting = targeting;
        Effect = effect;
        OriginalAttackerPosition = resolution?.OriginalAttackerPosition ?? use.Character.TacticalPosition;
        OriginalTargetPosition = resolution?.OriginalTargetPosition ?? target.TacticalPosition;
        FinalTargetPosition = resolution?.FinalTargetPosition ?? target.TacticalPosition;
        Resolution = resolution;
        CombatOutcome = resolution?.Combat?.OpeningStrike;
        HealthLost = resolution?.Combat?.OpeningHealthLost ?? 0;
        ShieldAbsorbed = resolution?.Combat?.OpeningShieldAbsorbed ?? 0;
        Finalization = finalization;
        RejectionReason = reason != BrutalKnockbackRejectionReason.None ? reason
            : resolution?.RejectionReason ?? effect?.RejectionReason ?? BrutalKnockbackRejectionReason.None;
    }
}

/// <summary>Coordinates the finalized opening/push core with existing targeting and ability bookkeeping.</summary>
public sealed class BrutalKnockbackAbilityExecutor
{
    private readonly BrutalKnockbackResolver _resolver;

    public BrutalKnockbackAbilityExecutor(BrutalKnockbackResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    public BrutalKnockbackUseResult Preview(AbilityUse use, Character target, AttackConfiguration? axe,
        WeaponCategory defenderWeapon, AbilityTargetingConstraints targetingRules, int? pushDistance,
        IForcedMovementMap? map, int evasionModifier = 0, bool? mapTargetLegal = null)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetingRules);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(use, target, approval.Eligibility);
        if (use.AbilityId != Nigel.BrutalKnockbackId)
            return new(use, target, new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        if (use.BrutalKnockbackStarted)
            return new(use, target, approval.Eligibility, reason: BrutalKnockbackRejectionReason.AlreadyStarted);
        if (!use.Activations.FirstTeam.Concat(use.Activations.SecondTeam).Any(c => ReferenceEquals(c, target)))
            throw new ArgumentException("The target must belong to the current match roster.", nameof(target));
        var constraints = new AbilityTargetingConstraints(targetingRules.TargetType, false, AbilityTargetTeam.Enemy,
            true, targetingRules.RequiresMapValidation, targetingRules.RequiresLineOfSight, targetingRules.RequiresElevationRules);
        var targeting = AbilityTargeting.Validate(use.Character, use.Character.TacticalPosition,
            AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!,
            new(AbilityTargetType.Character, target.TacticalPosition, target), constraints, mapTargetLegal: mapTargetLegal);
        if (!targeting.IsLegal) return new(use, target, approval.Eligibility, targeting);
        var effect = _resolver.Preview(use.Character, target, axe, defenderWeapon, pushDistance, map, evasionModifier);
        return new(use, target, approval.Eligibility, targeting, effect);
    }

    /// <remarks>
    /// Prevalidates bookkeeping and the full supported push before rolling. Damage precedes
    /// movement, and a valid miss or lethal opening still completes the action. A later map
    /// change can leave an explicitly unresolved post-hit result; applied damage is not rolled
    /// back, and the same use handle cannot start another opening. Arbitrary provider/query
    /// exceptions after combat begins are not transactional; match operations are synchronous.
    /// </remarks>
    public BrutalKnockbackUseResult Execute(AbilityUse use, Character target, AttackConfiguration? axe,
        WeaponCategory defenderWeapon, AbilityTargetingConstraints targetingRules, int? pushDistance,
        IForcedMovementMap? map, int evasionModifier = 0, bool? mapTargetLegal = null)
    {
        var preview = Preview(use, target, axe, defenderWeapon, targetingRules, pushDistance, map, evasionModifier, mapTargetLegal);
        if (!preview.CanExecute) return preview;
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(use, target, approval.Eligibility, preview.Targeting, preview.Effect);
        var resolution = _resolver.Resolve(use.Character, target, axe, defenderWeapon, pushDistance, map, evasionModifier);
        if (resolution.Combat is not null) use.MarkBrutalKnockbackStarted();
        var finalization = resolution.Completed ? AbilityUseFinalizer.FinalizeSuccessfulUse(use) : null;
        return new(use, target, preview.Eligibility, preview.Targeting, resolution.Preparation, resolution, finalization);
    }
}
