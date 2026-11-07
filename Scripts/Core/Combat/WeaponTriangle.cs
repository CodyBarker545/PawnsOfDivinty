using System;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Works out the weapon matchup and its damage and accuracy adjustments.</summary>
public static class WeaponTriangle
{
    /// <summary>Sword beats Axe, Axe beats Lance, and Lance beats Sword. Bows, magic and unarmed are neutral.</summary>
    /// <exception cref="ArgumentOutOfRangeException">One of the weapon categories is not recognized.</exception>
    public static WeaponTriangleOutcome Evaluate(WeaponCategory attacker, WeaponCategory defender)
    {
        // Check both categories first, even if one weapon would make the matchup neutral.
        if (!Enum.IsDefined(attacker))
            throw new ArgumentOutOfRangeException(nameof(attacker), attacker, "Unsupported weapon category.");
        if (!Enum.IsDefined(defender))
            throw new ArgumentOutOfRangeException(nameof(defender), defender, "Unsupported weapon category.");

        return (attacker, defender) switch
        {
            (WeaponCategory.Sword, WeaponCategory.Axe) or
            (WeaponCategory.Axe, WeaponCategory.Lance) or
            (WeaponCategory.Lance, WeaponCategory.Sword) => WeaponTriangleOutcome.Advantage,

            (WeaponCategory.Axe, WeaponCategory.Sword) or
            (WeaponCategory.Lance, WeaponCategory.Axe) or
            (WeaponCategory.Sword, WeaponCategory.Lance) => WeaponTriangleOutcome.Disadvantage,

            _ => WeaponTriangleOutcome.Neutral
        };
    }

    /// <summary>Returns the damage bonus or penalty for this matchup.</summary>
    public static int GetDamageModifier(WeaponTriangleOutcome outcome) => outcome switch
    {
        WeaponTriangleOutcome.Advantage => 2,
        WeaponTriangleOutcome.Disadvantage => -2,
        WeaponTriangleOutcome.Neutral => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unsupported triangle outcome.")
    };

    /// <summary>Returns accuracy points to add or subtract, not a multiplier.</summary>
    public static int GetAccuracyModifier(WeaponTriangleOutcome outcome) => outcome switch
    {
        WeaponTriangleOutcome.Advantage => 10,
        WeaponTriangleOutcome.Disadvantage => -10,
        WeaponTriangleOutcome.Neutral => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unsupported triangle outcome.")
    };
}
