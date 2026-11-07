namespace PawnsOfDivinity.Core.Combat;

/// <summary>Records calculated healing, actual restoration and the recipient's health afterward.</summary>
public sealed record HealingResult
{
    /// <summary>The event's ordinal, or null when the target was defeated and no event was received.</summary>
    public int? HealOrdinal { get; }
    public int BaseHealing { get; }
    public int AdjustedHealing { get; }
    public int HealthRestored { get; }
    public int ResultingHealth { get; }

    internal HealingResult(int? healOrdinal, int baseHealing, int adjustedHealing, int healthRestored, int resultingHealth)
    {
        HealOrdinal = healOrdinal;
        BaseHealing = baseHealing;
        AdjustedHealing = adjustedHealing;
        HealthRestored = healthRestored;
        ResultingHealth = resultingHealth;
    }
}
