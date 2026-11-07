using System;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Adds the weapon triangle bonus or penalty to a supplied hit chance.</summary>
/// <remarks>
/// For now, this is just the base chance plus the triangle adjustment, clamped to 0-100.
/// The full accuracy formula and gameplay caps are still undecided in spec v0.2,
/// sections 4.1-4.2. This method only calculates the chance; HitRoller handles the roll.
/// </remarks>
public static class AccuracyCalculator
{
    /// <summary>Returns the hit chance as a whole-number percentage.</summary>
    /// <param name="baseAccuracy">The base hit chance, from 0 to 100.</param>
    /// <param name="attackerWeapon">The attacker's weapon category.</param>
    /// <param name="defenderWeapon">The defender's weapon category.</param>
    /// <exception cref="ArgumentOutOfRangeException">The base chance is outside 0-100, or a weapon category is not recognized.</exception>
    public static int CalculateHitChance(
        int baseAccuracy,
        WeaponCategory attackerWeapon,
        WeaponCategory defenderWeapon)
    {
        if (baseAccuracy < 0 || baseAccuracy > 100)
            throw new ArgumentOutOfRangeException(nameof(baseAccuracy), baseAccuracy,
                "Base accuracy must be between 0 and 100 inclusive.");

        var outcome = WeaponTriangle.Evaluate(attackerWeapon, defenderWeapon);
        int modifier = WeaponTriangle.GetAccuracyModifier(outcome);

        return Math.Clamp(baseAccuracy + modifier, 0, 100);
    }
}
