using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Core.Abilities;

public enum ProvokingRoarUseRejectionReason
{
    None, AreaNotResolved, ActionRestrictionIntegrationRequired, NoRecipientsUseRuleUnresolved
}

public sealed record ProvokingRoarUseResult
{
    public string CasterId { get; }
    public int RoundNumber { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public ProvokingRoarEffectResult? Effect { get; }
    public ProvokingRoarUseRejectionReason RejectionReason { get; }
    public AbilityUseResult? Finalization { get; }
    public bool CanExecute => Eligibility.IsAllowed && Effect?.IsAreaResolved == true
        && RejectionReason == ProvokingRoarUseRejectionReason.None;
    public bool Succeeded => Finalization?.Succeeded == true;

    internal ProvokingRoarUseResult(AbilityUse use, AbilityEligibilityResult eligibility,
        ProvokingRoarEffectResult? effect = null, ProvokingRoarUseRejectionReason reason = ProvokingRoarUseRejectionReason.None)
    {
        CasterId = use.CharacterId;
        RoundNumber = use.RoundNumber;
        Eligibility = eligibility;
        Effect = effect;
        RejectionReason = reason;
    }
}

/// <summary>Checks the real roster and bookkeeping, leaving unsupported taunt execution explicitly blocked.</summary>
public sealed class ProvokingRoarAbilityExecutor
{
    public ProvokingRoarUseResult Preview(AbilityUse use, AbilityTargetingConstraints targetingRules,
        IReadOnlyDictionary<string, bool>? mapTargetLegality = null)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(targetingRules);
        // This also checks round/phase, ownership, Silence, budgets and ready-round overflow.
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(use, approval.Eligibility);
        if (use.AbilityId != Nigel.ProvokingRoarId)
            return new(use, new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        var roster = use.Activations.FirstTeam.Concat(use.Activations.SecondTeam);
        var effect = ProvokingRoarEffect.Evaluate(use.Character, roster, use.RoundNumber, targetingRules, mapTargetLegality);
        var reason = !effect.IsAreaResolved ? ProvokingRoarUseRejectionReason.AreaNotResolved
            : effect.AffectedCharacterIds.Count == 0 ? ProvokingRoarUseRejectionReason.NoRecipientsUseRuleUnresolved
                : ProvokingRoarUseRejectionReason.ActionRestrictionIntegrationRequired;
        return new(use, approval.Eligibility, effect, reason);
    }

    /// <summary>Re-evaluates current state; no preview is an authorization to commit a taunt.</summary>
    /// <remarks>
    /// Section 12.4 leaves taunt/action details unresolved. ActivationManager cannot yet enforce
    /// "attack Nigel if legally possible or end" or consult movement legality. Until that contract
    /// exists, no effect is applied and AbilityUseFinalizer must not spend the action or cooldown.
    /// No new status is added to StatusTracker, and no successful cast is claimed.
    /// </remarks>
    public ProvokingRoarUseResult Execute(AbilityUse use, AbilityTargetingConstraints targetingRules,
        IReadOnlyDictionary<string, bool>? mapTargetLegality = null)
        => Preview(use, targetingRules, mapTargetLegality);
}
