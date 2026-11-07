using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Calculates Healing Light's base amount from spec v0.2, section 12.2.</summary>
public static class HealingCalculator
{
    /// <summary>Returns floor(WIS / 2) + 2 before repeated-heal penalties or the recipient's HP cap.</summary>
    /// <param name="healerStats">The healer's base stats. Only Wisdom affects the amount.</param>
    public static int CalculateHealingLight(CharacterStats healerStats)
    {
        ArgumentNullException.ThrowIfNull(healerStats);

        // Wisdom cannot be negative, so integer division rounds down as the rule requires.
        // Divide first: even int.MaxValue Wisdom leaves room to add 2 safely.
        return healerStats.Wisdom / 2 + 2;
    }
}
