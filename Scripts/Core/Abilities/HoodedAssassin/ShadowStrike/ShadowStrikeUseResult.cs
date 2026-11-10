namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Separates permission to attempt the strike from combat and completed bookkeeping.</summary>
public sealed record ShadowStrikeUseResult
{
    public AbilityEligibilityResult Eligibility { get; }
    public AbilityTargetingResult? Targeting { get; }
    public ShadowStrikeEligibilityResult? CombatEligibility { get; }
    public ShadowStrikeResult? Strike { get; }
    public AbilityUseResult? Finalization { get; }
    public bool CanExecute => Eligibility.IsAllowed && Targeting?.IsLegal == true && CombatEligibility?.IsAllowed == true;
    public bool Succeeded => Strike?.Resolved == true && Finalization?.Succeeded == true;

    internal ShadowStrikeUseResult(AbilityEligibilityResult eligibility, AbilityTargetingResult? targeting = null,
        ShadowStrikeEligibilityResult? combatEligibility = null, ShadowStrikeResult? strike = null,
        AbilityUseResult? finalization = null)
    {
        Eligibility = eligibility;
        Targeting = targeting;
        CombatEligibility = combatEligibility;
        Strike = strike;
        Finalization = finalization;
    }
}
