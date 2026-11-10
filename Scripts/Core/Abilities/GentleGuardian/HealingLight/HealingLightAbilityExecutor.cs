using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Coordinates Healing Light using existing ability, targeting and recipient healing systems.</summary>
/// <remarks>
/// Match state must not change concurrently. Both preview and execution check current state;
/// execution applies the heal and finalizes synchronously without callbacks or rollback infrastructure.
/// There is no Wisdom modifier in the current rules, so the calculator uses immutable base WIS.
/// </remarks>
public sealed class HealingLightAbilityExecutor
{
    /// <summary>Predicts healing without consuming an action, cooldown or recipient healing event.</summary>
    public HealingLightUseResult Preview(AbilityUse use, Character target, AbilityTargetingConstraints targetingRules,
        bool? mapTargetLegal = null)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetingRules);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return Result(approval.Eligibility);
        if (use.AbilityId != GentleGuardian.HealingLightId)
            return Result(new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        if (use.Character.Definition?.Id != "gentle_guardian")
            return Result(approval.Eligibility, reason: HealingLightRejectionReason.NotGentleGuardian);
        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        // Living allies are confirmed. The caller supplies unresolved self/map/LOS/elevation decisions.
        var constraints = new AbilityTargetingConstraints(AbilityTargetType.Character, targetingRules.AllowSelf,
            AbilityTargetTeam.Ally, true, targetingRules.RequiresMapValidation,
            targetingRules.RequiresLineOfSight, targetingRules.RequiresElevationRules);
        var targeting = AbilityTargeting.Validate(use.Character, use.Character.TacticalPosition, ability,
            new(AbilityTargetType.Character, target.TacticalPosition, target), constraints, mapTargetLegal: mapTargetLegal);
        if (!targeting.IsLegal) return Result(approval.Eligibility, targeting);
        int baseHealing = HealingCalculator.CalculateHealingLight(use.Character.BaseStats);
        var healing = HealingApplicator.Preview(baseHealing, target);
        // Low-level application provisionally counts full-health events. That does not
        // establish whether spending a gameplay action on such a target is allowed.
        var reason = target.CurrentHealth == target.BaseStats.MaxHealth
            ? HealingLightRejectionReason.FullHealthActionRuleUnresolved : HealingLightRejectionReason.None;
        return Result(approval.Eligibility, targeting, reason, healing);

        HealingLightUseResult Result(AbilityEligibilityResult eligibility, AbilityTargetingResult? targeting = null,
            HealingLightRejectionReason reason = HealingLightRejectionReason.None, HealingResult? healing = null)
            => new(use.CharacterId, target.Id, use.RoundNumber, eligibility, target.CurrentHealth, target.CurrentHealth,
                targeting, reason, healing);
    }

    /// <summary>Rechecks eligibility, target position and healing history, then heals and finalizes one use.</summary>
    public HealingLightUseResult Execute(AbilityUse use, Character target, AbilityTargetingConstraints targetingRules,
        bool? mapTargetLegal = null)
    {
        var preview = Preview(use, target, targetingRules, mapTargetLegal);
        if (!preview.CanExecute) return preview;
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded)
            return new(use.CharacterId, target.Id, use.RoundNumber, approval.Eligibility,
                target.CurrentHealth, target.CurrentHealth, preview.Targeting);
        var healing = HealingApplicator.Apply(preview.BaseHealing, target);
        var (finalization, blessing) = GuardianSupportiveUseCoordinator.Finalize(use, target);
        return new(use.CharacterId, target.Id, use.RoundNumber, preview.Eligibility,
            preview.TargetHealthBefore, target.CurrentHealth, preview.Targeting,
            previewHealing: preview.PreviewHealing, healing: healing, finalization: finalization, gentleBlessing: blessing);
    }
}
