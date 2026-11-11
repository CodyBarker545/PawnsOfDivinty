using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Coordinates Sanctuary Shield without assigning its unresolved casting range or spell tag.</summary>
/// <remarks>
/// The registered ability currently has null Range, so gameplay casts return UnresolvedRange.
/// A mechanical shield preview does not override that rejection. Match state must not change
/// concurrently; execution revalidates before applying and finalizing without external callbacks.
/// </remarks>
public sealed class SanctuaryShieldAbilityExecutor
{
    private readonly SanctuaryShieldManager _shields;

    public SanctuaryShieldAbilityExecutor(SanctuaryShieldManager shields)
    {
        ArgumentNullException.ThrowIfNull(shields);
        _shields = shields;
    }

    public SanctuaryShieldUseResult Preview(AbilityUse use, Character recipient,
        AbilityTargetingConstraints targetingRules, bool? mapTargetLegal = null)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(targetingRules);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return Result(approval.Eligibility);
        if (use.AbilityId != GentleGuardian.SanctuaryShieldId)
            return Result(new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        if (use.Character.Definition?.Id != "gentle_guardian")
            return Result(approval.Eligibility, reason: SanctuaryShieldAbilityRejectionReason.NotGentleGuardian);
        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        var constraints = new AbilityTargetingConstraints(AbilityTargetType.Character, targetingRules.AllowSelf,
            AbilityTargetTeam.Ally, true, targetingRules.RequiresMapValidation,
            targetingRules.RequiresLineOfSight, targetingRules.RequiresElevationRules);
        var targeting = AbilityTargeting.Validate(use.Character, use.Character.TacticalPosition, ability,
            new(AbilityTargetType.Character, recipient.TacticalPosition, recipient), constraints, mapTargetLegal: mapTargetLegal);
        // Report shield ownership even if range is unresolved, without granting permission to cast.
        var application = _shields.Preview(use.Character, recipient);
        return Result(approval.Eligibility, targeting, application: application);

        SanctuaryShieldUseResult Result(AbilityEligibilityResult eligibility, AbilityTargetingResult? targeting = null,
            SanctuaryShieldAbilityRejectionReason reason = SanctuaryShieldAbilityRejectionReason.None,
            SanctuaryShieldPreview? application = null)
            => new(use.CharacterId, recipient.Id, use.RoundNumber, eligibility, recipient.ShieldHP, targeting, reason, application);
    }

    public SanctuaryShieldUseResult Execute(AbilityUse use, Character recipient,
        AbilityTargetingConstraints targetingRules, bool? mapTargetLegal = null)
    {
        var preview = Preview(use, recipient, targetingRules, mapTargetLegal);
        if (!preview.CanExecute) return preview;
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, preparingExecution: true);
        if (!approval.Succeeded)
            return new(use.CharacterId, recipient.Id, use.RoundNumber, approval.Eligibility,
                recipient.ShieldHP, preview.Targeting, application: preview.Application);
        _shields.Apply(use.Character, recipient);
        var (finalization, blessing) = GuardianSupportiveUseCoordinator.Finalize(use, recipient);
        return new(use.CharacterId, recipient.Id, use.RoundNumber, preview.Eligibility, recipient.ShieldHP,
            preview.Targeting, application: preview.Application, finalization: finalization, gentleBlessing: blessing);
    }
}
