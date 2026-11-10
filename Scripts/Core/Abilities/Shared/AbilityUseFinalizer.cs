using System;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Records a caller-reported successful effect without executing that effect.</summary>
public static class AbilityUseFinalizer
{
    /// <remarks>
    /// Call only after the whole effect has resolved. Selection and targeting never call this.
    /// This synchronous section has no user effects or callbacks between validation and mutation.
    /// Eligibility validates the action budget; RecordUse validates cooldown arithmetic before
    /// changing its entry. The already-validated manager action then cannot predictably fail.
    /// This does not roll back external effects, and match state must not change concurrently.
    /// </remarks>
    public static AbilityUseResult FinalizeSuccessfulUse(AbilityUse use)
    {
        var validation = ValidateSuccessfulUse(use);
        return validation.Succeeded ? Commit(use, validation, completingResolvedMainAction: false) : validation;
    }

    /// <summary>Checks bookkeeping, including deadline arithmetic, before an effect can mutate combat state.</summary>
    internal static AbilityUseResult ValidateSuccessfulUse(AbilityUse use)
    {
        ArgumentNullException.ThrowIfNull(use);
        if (use.IsFinalized) return Rejected(AbilityRejectionReason.AlreadyFinalized);
        if (use.RoundNumber != use.Activations.CurrentRound) return Rejected(AbilityRejectionReason.UseContextChanged);
        var eligibility = AbilityEligibility.Evaluate(use.Character, use.AbilityId, use.Activations);
        if (!eligibility.IsAllowed) return new AbilityUseResult(eligibility, use.RoundNumber, null);

        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        var deadline = new AbilityCooldown(use.RoundNumber, ability.ReuseInterval!.Value);
        return new AbilityUseResult(eligibility, use.RoundNumber, deadline.ReadyRound);

        AbilityUseResult Rejected(AbilityRejectionReason reason)
            => new(new AbilityEligibilityResult(use.CharacterId, use.AbilityId, reason), use.RoundNumber, null);
    }

    // The coordinator holds this approval only for its synchronous effect call. This
    // permits finishing an already-started action after a lethal counter, not starting one dead.
    internal static AbilityUseResult FinalizeResolvedMainAction(AbilityUse use, AbilityUseResult approval)
    {
        if (!approval.Succeeded || approval.Eligibility.CharacterId != use.CharacterId
            || approval.Eligibility.AbilityId != use.AbilityId || approval.RoundNumber != use.RoundNumber)
            throw new ArgumentException("Approval must describe this ability use.", nameof(approval));
        if (use.IsFinalized)
            return Rejected(AbilityRejectionReason.AlreadyFinalized);
        if (use.RoundNumber != use.Activations.CurrentRound)
            return Rejected(AbilityRejectionReason.UseContextChanged);
        var eligibility = AbilityEligibility.Evaluate(use.Character, use.AbilityId, use.Activations,
            completingResolvedMainAction: true);
        if (!eligibility.IsAllowed) return new(eligibility, use.RoundNumber, null);
        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        if (ability.RequiredAction != AbilityActionType.MainAction)
            throw new InvalidOperationException("Resolved combat completion requires a Main Action.");
        return Commit(use, new(eligibility, use.RoundNumber, approval.ReadyRound), completingResolvedMainAction: true);

        AbilityUseResult Rejected(AbilityRejectionReason reason)
            => new(new(use.CharacterId, use.AbilityId, reason), use.RoundNumber, null);
    }

    private static AbilityUseResult Commit(AbilityUse use, AbilityUseResult validation, bool completingResolvedMainAction)
    {
        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        use.Character.Cooldowns.RecordUse(ability.Id, use.RoundNumber, ability.ReuseInterval!.Value);
        if (completingResolvedMainAction) use.Activations.CompleteResolvedMainAction();
        else if (ability.RequiredAction == AbilityActionType.MainAction) use.Activations.CompleteMainAction();
        else use.Activations.RecordUtilityAction();
        use.MarkFinalized();
        return validation;
    }
}
