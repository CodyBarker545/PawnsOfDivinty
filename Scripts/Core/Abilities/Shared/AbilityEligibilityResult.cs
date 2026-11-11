namespace PawnsOfDivinity.Core.Abilities;

/// <summary>An immutable manual-use decision, including target/taunt checks when an attack intent is supplied.</summary>
public sealed record AbilityEligibilityResult
{
    public string CharacterId { get; }
    public string AbilityId { get; }
    public AbilityRejectionReason RejectionReason { get; }
    public bool IsAllowed => RejectionReason == AbilityRejectionReason.None;
    public AttackIntentResult? AttackIntent { get; }
    public PawnsOfDivinity.Core.Turns.Restrictions.TauntActionResult? ActionApproval { get; }

    internal AbilityEligibilityResult(string characterId, string abilityId, AbilityRejectionReason rejectionReason,
        AttackIntentResult? attackIntent = null, PawnsOfDivinity.Core.Turns.Restrictions.TauntActionResult? actionApproval = null)
    {
        CharacterId = characterId;
        AbilityId = abilityId;
        RejectionReason = rejectionReason;
        AttackIntent = attackIntent;
        ActionApproval = actionApproval ?? attackIntent?.TauntApproval;
    }
}
