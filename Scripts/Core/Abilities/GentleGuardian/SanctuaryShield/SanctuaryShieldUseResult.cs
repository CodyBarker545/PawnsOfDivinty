using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

public enum SanctuaryShieldAbilityRejectionReason { None, NotGentleGuardian }

/// <summary>Separates ability targeting, predicted shield behavior and completed use.</summary>
public sealed record SanctuaryShieldUseResult
{
    public string CasterId { get; }
    public string RecipientId { get; }
    public int RoundNumber { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public AbilityTargetingResult? Targeting { get; }
    public SanctuaryShieldAbilityRejectionReason RejectionReason { get; }
    public SanctuaryShieldPreview? Application { get; }
    public string? PreviousRecipientId => Application?.PreviousRecipientId;
    /// <summary>The shield value assigned by a completed cast, not the net increase on refresh.</summary>
    public int ShieldHPGranted => Succeeded ? Application!.ExpectedShieldHP : 0;
    public int ResultingShieldHP { get; }
    public bool Refreshed => Succeeded && Application!.Operation == SanctuaryShieldOperation.Refresh;
    public bool Transferred => Succeeded && Application!.Operation == SanctuaryShieldOperation.Transfer;
    public AbilityUseResult? Finalization { get; }
    public GentleBlessingResult? GentleBlessing { get; }
    public bool ActionFinalized => Finalization?.Succeeded == true;
    public bool CanExecute => Eligibility.IsAllowed && Targeting?.IsLegal == true && Application?.IsAllowed == true
        && RejectionReason == SanctuaryShieldAbilityRejectionReason.None;
    public bool Succeeded => ActionFinalized;

    internal SanctuaryShieldUseResult(string casterId, string recipientId, int round,
        AbilityEligibilityResult eligibility, int resultingShieldHP, AbilityTargetingResult? targeting = null,
        SanctuaryShieldAbilityRejectionReason reason = SanctuaryShieldAbilityRejectionReason.None,
        SanctuaryShieldPreview? application = null, AbilityUseResult? finalization = null, GentleBlessingResult? gentleBlessing = null)
    {
        CasterId = casterId;
        RecipientId = recipientId;
        RoundNumber = round;
        Eligibility = eligibility;
        ResultingShieldHP = resultingShieldHP;
        Targeting = targeting;
        RejectionReason = reason;
        Application = application;
        Finalization = finalization;
        GentleBlessing = gentleBlessing;
    }
}
