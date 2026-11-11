using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Resolves at cast time and queues the stored outcome, without applying damage or resolving an exchange.</summary>
/// <remarks>
/// Temporal Ignition's registered range is unresolved. Supplied scroll range is not a substitute
/// for missing ability metadata, so canonical casts remain blocked by AbilityTargeting.
/// Power, accuracy and evasion are explicit resolved combat inputs, not equipment defaults.
/// Match state must remain synchronous; random providers must not change match state.
/// </remarks>
public sealed class TemporalIgnitionAbilityExecutor
{
    public const int DamageBonus = 5;
    private readonly SingleStrikeResolver _strikes;
    private readonly DelayedAttackScheduler _scheduler;

    public TemporalIgnitionAbilityExecutor(SingleStrikeResolver strikes, DelayedAttackScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        ArgumentNullException.ThrowIfNull(scheduler);
        _strikes = strikes;
        _scheduler = scheduler;
    }

    public TemporalIgnitionUseResult Preview(AbilityUse use, Character target, string delayedAttackId,
        AttackConfiguration? magicalAttack, WeaponCategory defenderWeapon, AbilityTargetingConstraints targetingRules,
        int defenderEvasionModifier = 0, bool? mapTargetLegal = null, string? elementId = null)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetingRules);
        ArgumentException.ThrowIfNullOrWhiteSpace(delayedAttackId);
        if (elementId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, target, isAttack: true);
        if (!approval.Succeeded) return Result(approval.Eligibility);
        if (use.AbilityId != TimeWeaver.TemporalIgnitionId)
            return Result(new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        if (use.Character.Definition?.Id != "time_weaver")
            return Result(approval.Eligibility, reason: TemporalIgnitionRejectionReason.NotTimeWeaver);
        _scheduler.ValidateCanSchedule(delayedAttackId);
        int resolutionRound = checked(use.RoundNumber + 1);
        if (magicalAttack is null)
            return Result(approval.Eligibility, reason: TemporalIgnitionRejectionReason.CombatConfigurationRequired);
        if (magicalAttack.Type != AttackType.Magical)
            return Result(approval.Eligibility, reason: TemporalIgnitionRejectionReason.RequiresMagicalAttack);
        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        // Target permissions and applicable map/LOS/elevation rules remain explicit caller input.
        var constraints = new AbilityTargetingConstraints(AbilityTargetType.Character, targetingRules.AllowSelf,
            targetingRules.Team, true, targetingRules.RequiresMapValidation,
            targetingRules.RequiresLineOfSight, targetingRules.RequiresElevationRules);
        var targeting = AbilityTargeting.Validate(use.Character, use.Character.TacticalPosition, ability,
            new(AbilityTargetType.Character, target.TacticalPosition, target), constraints,
            new(AbilityRangeKind.ManhattanDistance, magicalAttack.MinimumRange, magicalAttack.MaximumRange), mapTargetLegal);
        var prepared = SingleStrikeResolver.PrepareMagicalStrike(use.Character.BaseStats, target.BaseStats,
            magicalAttack.Power, magicalAttack.Category, defenderWeapon, magicalAttack.BaseAccuracy,
            defenderEvasionModifier, target.EffectiveResistance, TemporalAccumulation.GetMagicalDamageBonus(use.Character));
        // Section 4.2 defines ordinary damage before additional modifiers. This focused
        // flat addition does not establish an ordering for future percentage modifiers.
        prepared.CandidateDamage = checked(prepared.CandidateDamage + DamageBonus);
        return new(use, target.Id, delayedAttackId, approval.Eligibility, targeting,
            resolutionRound: resolutionRound, prepared: prepared);

        TemporalIgnitionUseResult Result(AbilityEligibilityResult eligibility,
            TemporalIgnitionRejectionReason reason = TemporalIgnitionRejectionReason.None)
            => new(use, target.Id, delayedAttackId, eligibility, reason: reason);
    }

    public TemporalIgnitionUseResult Execute(AbilityUse use, Character target, string delayedAttackId,
        AttackConfiguration? magicalAttack, WeaponCategory defenderWeapon, AbilityTargetingConstraints targetingRules,
        int defenderEvasionModifier = 0, bool? mapTargetLegal = null, string? elementId = null)
    {
        var preview = Preview(use, target, delayedAttackId, magicalAttack, defenderWeapon, targetingRules,
            defenderEvasionModifier, mapTargetLegal, elementId);
        if (!preview.CanExecute) return preview;
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, target, isAttack: true, preparingExecution: true);
        if (!approval.Succeeded) return new(use, target.Id, delayedAttackId, approval.Eligibility, preview.Targeting);
        _scheduler.ValidateCanSchedule(delayedAttackId);
        var strike = _strikes.ResolvePreparedStrike(preview.Prepared!.Value);
        var attack = DelayedAttack.CreateTemporalIgnition(delayedAttackId, use.CharacterId, target.Id, use.RoundNumber, strike);
        var scheduled = _scheduler.Schedule(attack);
        var finalization = AbilityUseFinalizer.FinalizeResolvedMainAction(use, approval);
        var accumulation = finalization.Succeeded
            ? TemporalAccumulation.Process(use.Character, delayedAttackId, elementId, strike) : null;
        return new(use, target.Id, delayedAttackId, preview.Eligibility, preview.Targeting,
            resolutionRound: scheduled.ResolutionRound, prepared: preview.Prepared, attack: scheduled, finalization: finalization, accumulation: accumulation);
    }
}
