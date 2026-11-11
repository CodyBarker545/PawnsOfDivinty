namespace PawnsOfDivinity.Core.Combat;

/// <summary>Records the strikes, actual health lost and who was defeated when the exchange ended.</summary>
public sealed record CombatExchangeResult
{
    public StrikeResult OpeningStrike { get; }
    /// <summary>Ordinary damage including runtime passives, before an opening-only ability bonus; retained on a miss.</summary>
    public int OpeningBaseDamage { get; }
    /// <summary>An opening-only addition such as Shadow Strike, not the passives already included in ordinary damage.</summary>
    public int OpeningDamageBonus { get; }
    public int OpeningHealthLost { get; }
    public int OpeningShieldAbsorbed { get; }
    public bool OpeningDisarmApplied { get; }
    /// <summary>Total HP lost by the defender across this exchange.</summary>
    public int DefenderHealthLost { get; }
    public bool CounterattackOccurred => CounterStrike is not null;
    public StrikeResult? CounterStrike { get; }
    /// <summary>Total HP lost by the original attacker across this exchange.</summary>
    public int AttackerHealthLost { get; }
    public bool FollowUpOccurred => FollowUpStrike is not null;
    public FollowUpParticipant FollowUpBy { get; }
    public StrikeResult? FollowUpStrike { get; }
    public int FollowUpHealthLost { get; }
    public bool AttackerDefeated { get; }
    public bool DefenderDefeated { get; }
    public bool CounterattacksSuppressed { get; }

    internal CombatExchangeResult(
        StrikeResult openingStrike,
        int defenderHealthLost,
        StrikeResult? counterStrike,
        int attackerHealthLost,
        bool attackerDefeated,
        bool defenderDefeated,
        FollowUpParticipant followUpBy = FollowUpParticipant.None,
        StrikeResult? followUpStrike = null,
        int followUpHealthLost = 0,
        int openingBaseDamage = 0,
        int openingDamageBonus = 0,
        int openingHealthLost = 0,
        int openingShieldAbsorbed = 0,
        bool openingDisarmApplied = false,
        bool counterattacksSuppressed = false)
    {
        OpeningStrike = openingStrike;
        OpeningBaseDamage = openingBaseDamage;
        OpeningDamageBonus = openingDamageBonus;
        OpeningHealthLost = openingHealthLost;
        OpeningShieldAbsorbed = openingShieldAbsorbed;
        OpeningDisarmApplied = openingDisarmApplied;
        CounterattacksSuppressed = counterattacksSuppressed;
        DefenderHealthLost = defenderHealthLost;
        CounterStrike = counterStrike;
        AttackerHealthLost = attackerHealthLost;
        AttackerDefeated = attackerDefeated;
        DefenderDefeated = defenderDefeated;
        FollowUpBy = followUpBy;
        FollowUpStrike = followUpStrike;
        FollowUpHealthLost = followUpHealthLost;
    }
}
