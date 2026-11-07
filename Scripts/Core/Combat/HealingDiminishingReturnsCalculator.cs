using System;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Adjusts healing for repeated heals received in one round, following spec v0.2, section 11.</summary>
public static class HealingDiminishingReturnsCalculator
{
    /// <summary>Returns 100%, 75%, 50%, or 25% of the supplied healing amount.</summary>
    /// <param name="baseHealing">Healing before this penalty; must be zero or greater.</param>
    /// <param name="healOrdinal">Which heal the recipient is receiving this round, starting at 1.</param>
    /// <remarks>
    /// Floor rounding is provisional until the specification settles fractional healing.
    /// Small amounts can round to zero. The caller supplies the ordinal; this class neither
    /// tracks healing history nor applies healing or checks the recipient's maximum HP.
    /// </remarks>
    public static int Calculate(int baseHealing, int healOrdinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseHealing);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(healOrdinal);

        int quarters = healOrdinal switch
        {
            1 => 4,
            2 => 3,
            3 => 2,
            _ => 1
        };

        // Multiply as long first so even int.MaxValue healing is safe. With nonnegative
        // inputs, integer division rounds down without floating-point arithmetic.
        return (int)((long)baseHealing * quarters / 4);
    }
}
