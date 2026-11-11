using System;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Connects ability permission, target checks, Shadow Strike and successful-use bookkeeping.</summary>
/// <remarks>
/// Use one thread for match state. Execute rechecks everything and runs combat and finalization
/// synchronously. A broken random provider can still throw after an earlier strike changed HP;
/// this is not a rollback mechanism, and providers must not mutate match state themselves.
/// Approach movement remains outside this service. Supply the complete pre-approach snapshot.
/// </remarks>
public sealed class ShadowStrikeAbilityExecutor
{
    private readonly ShadowStrikeResolver _strikes;

    public ShadowStrikeAbilityExecutor(ShadowStrikeResolver strikes)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        _strikes = strikes;
    }

    /// <summary>Selection and target preview never consume actions, cooldowns or random rolls.</summary>
    /// <remarks>
    /// The caller explicitly supplies applicable map, line-of-sight and elevation requirements.
    /// Unknown or required unsupported rules remain rejections in AbilityTargeting. Combat Power,
    /// accuracy, categories and fallback attacks are validated by the resolver before its first roll.
    /// </remarks>
    public ShadowStrikeUseResult Preview(AbilityUse use, CombatExchangeRequest combat,
        SmokeVisibilityResult? openingVisibility, int weaponMinimumRange, int weaponMaximumRange,
        AbilityTargetingConstraints targetingRules, bool? mapTargetLegal = null)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(targetingRules);
        if (!ReferenceEquals(use.Character, combat.Attacker))
            throw new ArgumentException("The combat attacker must be the character using the ability.", nameof(combat));
        ArgumentNullException.ThrowIfNull(combat.Defender);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, combat.Defender, isAttack: true);
        if (!approval.Succeeded) return new(approval.Eligibility);
        if (use.AbilityId != HoodedAssassin.ShadowStrikeId)
            return new(new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));

        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        var target = new AbilityTarget(AbilityTargetType.Character, combat.Defender.TacticalPosition, combat.Defender);
        var targeting = AbilityTargeting.Validate(use.Character, use.Character.TacticalPosition,
            ability, target, targetingRules, mapTargetLegal: mapTargetLegal);
        if (!targeting.IsLegal) return new(approval.Eligibility, targeting);
        var strikeEligibility = _strikes.Evaluate(combat, openingVisibility, weaponMinimumRange, weaponMaximumRange);
        if (strikeEligibility.IsAllowed)
            CombatExchangeResolver.ValidateRequest(combat with { Sanctuary = SanctuaryDefenseContext.FromActivations(use.Activations),
                IncomingAttackPermitsCounters = true, IncomingAttackPermitsFollowUps = true });
        return new(approval.Eligibility, targeting, strikeEligibility);
    }

    public ShadowStrikeUseResult Execute(AbilityUse use, CombatExchangeRequest combat,
        SmokeVisibilityResult? openingVisibility, int weaponMinimumRange, int weaponMaximumRange,
        AbilityTargetingConstraints targetingRules, bool? mapTargetLegal = null)
    {
        var preview = Preview(use, combat, openingVisibility, weaponMinimumRange, weaponMaximumRange,
            targetingRules, mapTargetLegal);
        if (!preview.CanExecute) return preview;
        // Deadline overflow is checked before combat. There is no targeting callback or
        // player input between this approval, the exchange and finalization.
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, combat.Defender, isAttack: true, preparingExecution: true);
        if (!approval.Succeeded) return new(approval.Eligibility, preview.Targeting, preview.CombatEligibility);
        var strike = _strikes.Resolve(combat with { Sanctuary = SanctuaryDefenseContext.FromActivations(use.Activations) },
            openingVisibility, weaponMinimumRange, weaponMaximumRange);
        if (!strike.Resolved) return new(preview.Eligibility, preview.Targeting, strike.Eligibility, strike);
        var finalization = AbilityUseFinalizer.FinalizeResolvedMainAction(use, approval);
        return new(preview.Eligibility, preview.Targeting, strike.Eligibility, strike, finalization);
    }
}
