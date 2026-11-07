using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Calculates base damage using spec v0.2, section 4.2.</summary>
public static class DamageCalculator
{
    /// <summary>Calculates STR + weapon Power - DEF, with a minimum of 1 damage.</summary>
    /// <param name="attacker">The attacker's base stats.</param>
    /// <param name="defender">The defender's base stats.</param>
    /// <param name="weaponPower">Weapon Power; zero is allowed, negative values are not.</param>
    /// <param name="effectiveDefense">Optional nonnegative combat Defense; omitted calls use base DEF.</param>
    /// <exception cref="ArgumentNullException">Attacker or defender stats were not supplied.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Weapon Power is negative.</exception>
    /// <exception cref="OverflowException">The result is too large for an int.</exception>
    public static int CalculatePhysicalDamage(CharacterStats attacker, CharacterStats defender, int weaponPower,
        int? effectiveDefense = null)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentOutOfRangeException.ThrowIfNegative(weaponPower);
        if (effectiveDefense.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(effectiveDefense.Value, nameof(effectiveDefense));

        // Use long before adding: defense may bring a large total back into the int range.
        long damage = (long)attacker.Strength + weaponPower - (effectiveDefense ?? defender.Defense);
        return checked((int)Math.Max(1L, damage));
    }

    /// <summary>Calculates INT + spell Power - RES, with a minimum of 1 damage.</summary>
    /// <param name="attacker">The attacker's base stats.</param>
    /// <param name="defender">The defender's base stats.</param>
    /// <param name="spellPower">Spell Power; zero is allowed, negative values are not.</param>
    /// <exception cref="ArgumentNullException">Attacker or defender stats were not supplied.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spell Power is negative.</exception>
    /// <exception cref="OverflowException">The result is too large for an int.</exception>
    public static int CalculateMagicalDamage(CharacterStats attacker, CharacterStats defender, int spellPower)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentOutOfRangeException.ThrowIfNegative(spellPower);

        long damage = (long)attacker.Intelligence + spellPower - defender.Resistance;
        return checked((int)Math.Max(1L, damage));
    }
}
