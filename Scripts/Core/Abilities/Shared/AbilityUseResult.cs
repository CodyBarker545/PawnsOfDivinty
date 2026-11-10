namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Immutable bookkeeping outcome; it makes no claim about target or effect validation.</summary>
public sealed record AbilityUseResult
{
    public AbilityEligibilityResult Eligibility { get; }
    public int RoundNumber { get; }
    public int? ReadyRound { get; }
    public bool Succeeded => Eligibility.IsAllowed;

    internal AbilityUseResult(AbilityEligibilityResult eligibility, int roundNumber, int? readyRound)
    {
        Eligibility = eligibility;
        RoundNumber = roundNumber;
        ReadyRound = readyRound;
    }
}
