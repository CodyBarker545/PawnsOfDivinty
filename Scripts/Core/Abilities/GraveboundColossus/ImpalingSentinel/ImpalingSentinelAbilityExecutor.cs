using System;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

public sealed record ImpalingSentinelUseResult
{
    public AbilityEligibilityResult Eligibility { get; }
    public ImpalingSentinelReason RejectionReason { get; }
    public ImpalingSentinelPreparation? Preparation { get; }
    public AbilityUseResult? Finalization { get; }
    public bool CanExecute => !Succeeded && Eligibility.IsAllowed && RejectionReason == ImpalingSentinelReason.None;
    public bool Succeeded => Finalization?.Succeeded == true;

    internal ImpalingSentinelUseResult(AbilityEligibilityResult eligibility, ImpalingSentinelReason reason = ImpalingSentinelReason.None,
        ImpalingSentinelPreparation? preparation = null, AbilityUseResult? finalization = null)
    {
        Eligibility = eligibility;
        RejectionReason = reason;
        Preparation = preparation;
        Finalization = finalization;
    }
}

/// <summary>Arms Sentinel for this round. Preparing is a Main Action, not the reaction attack itself.</summary>
public sealed class ImpalingSentinelAbilityExecutor
{
    private readonly ImpalingSentinelTracker _tracker;
    public ImpalingSentinelAbilityExecutor(ImpalingSentinelTracker tracker)
        => _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));

    public ImpalingSentinelUseResult Preview(AbilityUse use, string preparationId, AttackConfiguration? lance,
        SentinelReactionLimit? reactionLimit)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentException.ThrowIfNullOrWhiteSpace(preparationId);
        if (reactionLimit.HasValue && !Enum.IsDefined(reactionLimit.Value)) throw new ArgumentOutOfRangeException(nameof(reactionLimit));
        if (!ReferenceEquals(use.Activations, _tracker.Activations)) return Rejected(ImpalingSentinelReason.ContextMismatch);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(approval.Eligibility);
        if (use.AbilityId != GraveboundColossus.ImpalingSentinelId)
            return new(new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        if (lance?.Type != AttackType.Physical || lance.Category != WeaponCategory.Lance)
            return new(approval.Eligibility, ImpalingSentinelReason.LanceRequired);
        var metadata = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        if (metadata.RequiredAction != AbilityActionType.MainAction || metadata.ReuseInterval != 2 || metadata.Range?.Kind != AbilityRangeKind.EquippedWeapon)
            return new(approval.Eligibility, ImpalingSentinelReason.UnsupportedMetadata);
        var preparation = new ImpalingSentinelPreparation(preparationId, use.CharacterId, use.Character.TeamId,
            use.RoundNumber, lance, reactionLimit);
        return new(approval.Eligibility, _tracker.ValidatePreparation(preparation), preparation);

        ImpalingSentinelUseResult Rejected(ImpalingSentinelReason reason)
            => new(new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UseContextChanged), reason);
    }

    /// <remarks>Match operations are synchronous. All validation finishes before storage and finalization.</remarks>
    public ImpalingSentinelUseResult Execute(AbilityUse use, string preparationId, AttackConfiguration? lance,
        SentinelReactionLimit? reactionLimit)
    {
        var preview = Preview(use, preparationId, lance, reactionLimit);
        if (!preview.CanExecute) return preview;
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, preparingExecution: true);
        if (!approval.Succeeded) return new(approval.Eligibility);
        var preparation = _tracker.Register(preview.Preparation!);
        return new(preview.Eligibility, preparation: preparation, finalization: AbilityUseFinalizer.FinalizeSuccessfulUse(use));
    }
}
