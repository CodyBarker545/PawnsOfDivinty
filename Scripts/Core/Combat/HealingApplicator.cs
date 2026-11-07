using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Applies diminishing healing and records the event on its recipient.</summary>
public static class HealingApplicator
{
    /// <summary>Calculates the recipient's next heal, restores HP, then records the event.</summary>
    /// <remarks>
    /// Provisional counting rule: a valid event on a living target counts even if full health
    /// or floor rounding means zero HP is restored. Defeated targets receive no event.
    /// The Wisdom formula, range and round transitions are handled separately.
    /// </remarks>
    public static HealingResult Apply(int baseHealing, Character recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentOutOfRangeException.ThrowIfNegative(baseHealing);

        if (recipient.IsDefeated)
            return new HealingResult(null, baseHealing, 0, 0, recipient.CurrentHealth);

        // Check the next ordinal before touching health, so a full counter cannot overflow afterward.
        int ordinal = checked(recipient.HealsReceivedThisRound + 1);
        int adjustedHealing = HealingDiminishingReturnsCalculator.Calculate(baseHealing, ordinal);
        int restored = recipient.Heal(adjustedHealing);
        recipient.RecordHealingReceived();

        return new HealingResult(ordinal, baseHealing, adjustedHealing, restored, recipient.CurrentHealth);
    }
}
