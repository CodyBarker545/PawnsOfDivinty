using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Turns.Restrictions;

namespace PawnsOfDivinity.Core.Abilities;

public enum ProvokingRoarUseRejectionReason
{
    None, AreaNotResolved, ActionRestrictionIntegrationRequired, NoRecipientsUseRuleUnresolved, TauntApplicationRulesUnresolved,
    RestrictionStorageRejected
}

public sealed record ProvokingRoarUseResult
{
    public string CasterId { get; }
    public string AbilityId { get; }
    public string EffectId { get; }
    public int Radius => 2;
    public int RoundNumber { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public ProvokingRoarEffectResult? Effect { get; }
    public ProvokingRoarUseRejectionReason RejectionReason { get; }
    public AbilityUseResult? Finalization { get; }
    public TauntBatchResult? Restrictions { get; }
    public IReadOnlyList<string> EligibleRecipientIds => Effect?.AffectedCharacterIds ?? Array.Empty<string>();
    public IReadOnlyList<string> EnemiesInsideRadius => Effect?.EnemyIdsInsideRadius ?? Array.Empty<string>();
    public IReadOnlyList<string> ProspectiveRecipientIds { get; }
    public IReadOnlyList<string> NewlyTauntedRecipientIds { get; }
    public IReadOnlyList<string> ExcludedExistingTauntRecipientIds { get; }
    public IReadOnlyList<TauntRestriction> ControllingRestrictions { get; }
    public int? ExpectedReadyRound { get; }
    public AbilityActionType RequiredAction => AbilityActionType.MainAction;
    public int? ExpiresAtEndOfRound => Effect?.RoundNumber;
    public bool CanExecute => !Succeeded && Eligibility.IsAllowed && Effect?.IsAreaResolved == true
        && Restrictions?.IsValid == true && ProspectiveRecipientIds.Count > 0 && RejectionReason == ProvokingRoarUseRejectionReason.None;
    public bool Succeeded => Finalization?.Succeeded == true;

    internal ProvokingRoarUseResult(AbilityUse use, AbilityEligibilityResult eligibility,
        ProvokingRoarEffectResult? effect = null, ProvokingRoarUseRejectionReason reason = ProvokingRoarUseRejectionReason.None,
        TauntBatchResult? restrictions = null, AbilityUseResult? finalization = null, int? readyRound = null, string? effectId = null)
    {
        CasterId = use.CharacterId;
        AbilityId = use.AbilityId;
        EffectId = effectId ?? $"provoking_roar:{use.CharacterId.Length}:{use.CharacterId}:{use.RoundNumber}";
        RoundNumber = use.RoundNumber;
        Eligibility = eligibility;
        Effect = effect;
        RejectionReason = reason;
        Restrictions = restrictions;
        Finalization = finalization;
        ExpectedReadyRound = readyRound;
        var results = restrictions?.Results ?? Array.Empty<TauntApplicationResult>();
        ProspectiveRecipientIds = Array.AsReadOnly(results.Where(r => r.Applied).Select(r => r.Restriction.RestrictedCharacterId).ToArray());
        NewlyTauntedRecipientIds = restrictions?.WasApplied == true ? ProspectiveRecipientIds : Array.AsReadOnly(Array.Empty<string>());
        ExcludedExistingTauntRecipientIds = Array.AsReadOnly(results.Where(r => r.Outcome == TauntApplicationOutcome.PreservedFirstSource)
            .Select(r => r.Restriction.RestrictedCharacterId).ToArray());
        ControllingRestrictions = Array.AsReadOnly(results.Where(r => r.ControllingRestriction is not null)
            .Select(r => r.ControllingRestriction!).ToArray());
    }
}

/// <summary>Applies a fully validated Roar batch before finalizing its one Main Action.</summary>
public sealed class ProvokingRoarAbilityExecutor
{
    public ProvokingRoarUseResult Preview(AbilityUse use, AbilityTargetingConstraints targetingRules,
        IReadOnlyDictionary<string, bool>? mapTargetLegality = null, string? effectId = null)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(targetingRules);
        if (effectId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        // This also checks round/phase, ownership, Silence, budgets and ready-round overflow.
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(use, approval.Eligibility);
        if (use.AbilityId != Nigel.ProvokingRoarId)
            return new(use, new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        var roster = use.Activations.FirstTeam.Concat(use.Activations.SecondTeam);
        var effect = ProvokingRoarEffect.Evaluate(use.Character, roster, use.RoundNumber, targetingRules, mapTargetLegality);
        if (!effect.IsAreaResolved) return new(use, approval.Eligibility, effect, ProvokingRoarUseRejectionReason.AreaNotResolved, effectId: effectId);
        effectId ??= $"provoking_roar:{use.CharacterId.Length}:{use.CharacterId}:{use.RoundNumber}";
        var proposed = effect.AffectedCharacterIds.Select(id => new TauntRestriction(
            $"{effectId.Length}:{effectId}:{id.Length}:{id}", effectId, use.CharacterId, id, use.RoundNumber, use.RoundNumber));
        var batch = use.Activations.ActionRestrictions.PreviewBatch(proposed);
        var reason = !batch.IsValid ? ProvokingRoarUseRejectionReason.RestrictionStorageRejected
            : !batch.Results.Any(r => r.Applied) ? ProvokingRoarUseRejectionReason.NoRecipientsUseRuleUnresolved
                : ProvokingRoarUseRejectionReason.None;
        return new(use, approval.Eligibility, effect, reason, batch, readyRound: approval.ReadyRound, effectId: effectId);
    }

    /// <summary>Re-evaluates current state; no preview is an authorization to commit a taunt.</summary>
    /// <remarks>
    /// Preview and Execute both inspect current state. After batch and deadline validation,
    /// no callback or player input occurs between storage and finalization. Match state must
    /// remain synchronous; this is not a general rollback system. A zero-new-recipient cast
    /// remains unresolved and never applies a batch or consumes a cost.
    /// </remarks>
    public ProvokingRoarUseResult Execute(AbilityUse use, AbilityTargetingConstraints targetingRules,
        IReadOnlyDictionary<string, bool>? mapTargetLegality = null, string? effectId = null)
    {
        var preview = Preview(use, targetingRules, mapTargetLegality, effectId);
        if (!preview.CanExecute) return preview;
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, preparingExecution: true);
        if (!approval.Succeeded) return new(use, approval.Eligibility, preview.Effect, effectId: preview.EffectId);
        var current = use.Activations.ActionRestrictions.PreviewBatch(preview.Restrictions!.Results.Select(r => r.Restriction));
        if (!current.IsValid || !current.Results.Any(r => r.Applied))
            return new(use, approval.Eligibility, preview.Effect, current.IsValid ? ProvokingRoarUseRejectionReason.NoRecipientsUseRuleUnresolved
                : ProvokingRoarUseRejectionReason.RestrictionStorageRejected, current, effectId: preview.EffectId);
        var applied = use.Activations.ActionRestrictions.ApplyBatch(current.Results.Select(r => r.Restriction));
        if (!applied.WasApplied) return new(use, approval.Eligibility, preview.Effect,
            ProvokingRoarUseRejectionReason.RestrictionStorageRejected, applied, effectId: preview.EffectId);
        var finalization = AbilityUseFinalizer.FinalizeSuccessfulUse(use);
        return new(use, preview.Eligibility, preview.Effect, restrictions: applied, finalization: finalization,
            readyRound: approval.ReadyRound, effectId: preview.EffectId);
    }
}
