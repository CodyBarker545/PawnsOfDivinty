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
        var preview = Preview(baseHealing, recipient);
        if (!preview.HealOrdinal.HasValue) return preview;
        int restored = recipient.Heal(preview.AdjustedHealing);
        recipient.RecordHealingReceived();
        return new HealingResult(preview.HealOrdinal, baseHealing, preview.AdjustedHealing, restored, recipient.CurrentHealth);
    }

    /// <summary>Predicts the next event without changing HP or consuming a healing ordinal.</summary>
    public static HealingResult Preview(int baseHealing, Character recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentOutOfRangeException.ThrowIfNegative(baseHealing);

        if (recipient.IsDefeated)
            return new HealingResult(null, baseHealing, 0, 0, recipient.CurrentHealth);

        // Check the next ordinal before touching health, so a full counter cannot overflow afterward.
        int ordinal = checked(recipient.HealsReceivedThisRound + 1);
        int adjustedHealing = HealingDiminishingReturnsCalculator.Calculate(baseHealing, ordinal);
        int restored = Math.Min(adjustedHealing, recipient.BaseStats.MaxHealth - recipient.CurrentHealth);
        return new HealingResult(ordinal, baseHealing, adjustedHealing, restored, recipient.CurrentHealth + restored);
    }
}
