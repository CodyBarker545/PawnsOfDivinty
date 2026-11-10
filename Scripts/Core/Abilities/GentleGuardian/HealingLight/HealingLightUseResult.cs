using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

public enum HealingLightRejectionReason
{
    None,
    NotGentleGuardian,
    FullHealthActionRuleUnresolved
}

/// <summary>A value snapshot distinguishing predicted healing, actual restoration and action completion.</summary>
public sealed record HealingLightUseResult
{
    public string CasterId { get; }
    public string TargetId { get; }
    public int RoundNumber { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public AbilityTargetingResult? Targeting { get; }
    public HealingLightRejectionReason RejectionReason { get; }
    public HealingResult? PreviewHealing { get; }
    public HealingResult? Healing { get; }
    public int BaseHealing => PreviewHealing?.BaseHealing ?? 0;
    public int? HealOrdinal => PreviewHealing?.HealOrdinal;
    public int? EffectivenessPercentage => HealOrdinal is { } ordinal
        ? HealingDiminishingReturnsCalculator.GetEffectivenessPercentage(ordinal) : null;
    public int? ExpectedAdjustedHealing => PreviewHealing?.AdjustedHealing;
    public int? ExpectedHPRestored => PreviewHealing?.HealthRestored;
    public int ActualHPRestored => Healing?.HealthRestored ?? 0;
    public int TargetHealthBefore { get; }
    public int TargetHealthAfter { get; }
    public AbilityUseResult? Finalization { get; }
    public GentleBlessingResult? GentleBlessing { get; }
    public bool ActionFinalized => Finalization?.Succeeded == true;
    public bool CanExecute => Eligibility.IsAllowed && Targeting?.IsLegal == true
        && RejectionReason == HealingLightRejectionReason.None && PreviewHealing is not null;
    public bool Succeeded => Healing is not null && ActionFinalized;

    internal HealingLightUseResult(string casterId, string targetId, int round, AbilityEligibilityResult eligibility,
        int hpBefore, int hpAfter, AbilityTargetingResult? targeting = null,
        HealingLightRejectionReason reason = HealingLightRejectionReason.None, HealingResult? previewHealing = null,
        HealingResult? healing = null, AbilityUseResult? finalization = null, GentleBlessingResult? gentleBlessing = null)
    {
        CasterId = casterId;
        TargetId = targetId;
        RoundNumber = round;
        Eligibility = eligibility;
        TargetHealthBefore = hpBefore;
        TargetHealthAfter = hpAfter;
        Targeting = targeting;
        RejectionReason = reason;
        PreviewHealing = previewHealing;
        Healing = healing;
        Finalization = finalization;
        GentleBlessing = gentleBlessing;
    }
}
