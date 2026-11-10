namespace PawnsOfDivinity.Core.Abilities;

/// <summary>An immutable manual-use decision. Being allowed does not validate targets or apply effects.</summary>
public sealed record AbilityEligibilityResult
{
    public string CharacterId { get; }
    public string AbilityId { get; }
    public AbilityRejectionReason RejectionReason { get; }
    public bool IsAllowed => RejectionReason == AbilityRejectionReason.None;

    internal AbilityEligibilityResult(string characterId, string abilityId, AbilityRejectionReason rejectionReason)
    {
        CharacterId = characterId;
        AbilityId = abilityId;
        RejectionReason = rejectionReason;
    }
}
