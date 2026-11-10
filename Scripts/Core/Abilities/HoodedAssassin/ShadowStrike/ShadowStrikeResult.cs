using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Keeps the opening damage breakdown separate from the rest of the exchange.</summary>
public sealed record ShadowStrikeResult
{
    public ShadowStrikeEligibilityResult Eligibility { get; }
    public bool Resolved => Exchange is not null;
    public CombatExchangeResult? Exchange { get; }
    /// <summary>Ordinary damage and selected bonus before the hit roll; a miss still deals zero.</summary>
    public int BaseAttackDamage => Exchange?.OpeningBaseDamage ?? 0;
    public int ShadowStrikeBonus => Exchange?.OpeningDamageBonus ?? 0;
    public int FinalCalculatedDamage => Exchange?.OpeningStrike.Damage ?? 0;
    public int ActualHPDamage => Exchange?.OpeningHealthLost ?? 0;
    public int ShieldAbsorbed => Exchange?.OpeningShieldAbsorbed ?? 0;
    public bool DisarmApplied => Exchange?.OpeningDisarmApplied ?? false;

    internal ShadowStrikeResult(ShadowStrikeEligibilityResult eligibility, CombatExchangeResult? exchange)
    {
        Eligibility = eligibility;
        Exchange = exchange;
    }
}
