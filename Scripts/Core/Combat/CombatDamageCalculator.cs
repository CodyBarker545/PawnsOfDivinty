using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Adds the weapon triangle bonus or penalty to base physical damage.</summary>
/// <remarks>
/// This order is provisional: calculate base damage first, add the triangle adjustment,
/// then keep the result at least 1. The base calculation already has its own minimum of 1.
/// Spec v0.2, section 4 confirms the formulas but leaves the full modifier order undecided.
/// </remarks>
public static class CombatDamageCalculator
{
    /// <summary>Calculates physical damage without changing anyone's health.</summary>
    /// <param name="attacker">The attacker's base stats.</param>
    /// <param name="defender">The defender's base stats.</param>
    /// <param name="weaponPower">Weapon Power; zero is allowed, negative values are not.</param>
    /// <param name="attackerWeapon">The attacker's weapon category, used for the triangle matchup.</param>
    /// <param name="defenderWeapon">The defender's weapon category, used for the triangle matchup.</param>
    /// <param name="effectiveDefense">Optional combat Defense, passed through to the base calculator.</param>
    /// <remarks>
    /// Weapon categories only affect the triangle here. Even MagicScroll uses the physical
    /// formula in this method; magical damage has its own calculator. The existing
    /// calculators check the inputs and report damage that is too large for an int.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Attacker or defender stats were not supplied.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Power is negative, or a weapon category is not recognized.</exception>
    /// <exception cref="OverflowException">Base or final damage is too large for an int.</exception>
    public static int CalculatePhysicalDamage(
        CharacterStats attacker,
        CharacterStats defender,
        int weaponPower,
        WeaponCategory attackerWeapon,
        WeaponCategory defenderWeapon,
        int? effectiveDefense = null)
    {
        int baseDamage = DamageCalculator.CalculatePhysicalDamage(attacker, defender, weaponPower, effectiveDefense);
        var outcome = WeaponTriangle.Evaluate(attackerWeapon, defenderWeapon);
        int triangleModifier = WeaponTriangle.GetDamageModifier(outcome);

        long finalDamage = (long)baseDamage + triangleModifier;
        return checked((int)Math.Max(1L, finalDamage));
    }
}
