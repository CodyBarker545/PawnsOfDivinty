namespace PawnsOfDivinity.Core.Combat;

/// <summary>Records the strikes, actual health lost and who was defeated when the exchange ended.</summary>
public sealed record CombatExchangeResult
{
    public StrikeResult OpeningStrike { get; }
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

    internal CombatExchangeResult(
        StrikeResult openingStrike,
        int defenderHealthLost,
        StrikeResult? counterStrike,
        int attackerHealthLost,
        bool attackerDefeated,
        bool defenderDefeated,
        FollowUpParticipant followUpBy = FollowUpParticipant.None,
        StrikeResult? followUpStrike = null,
        int followUpHealthLost = 0)
    {
        OpeningStrike = openingStrike;
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
