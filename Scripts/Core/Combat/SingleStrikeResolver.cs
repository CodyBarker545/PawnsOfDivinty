using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Works out one strike without applying its damage to health.</summary>
public sealed class SingleStrikeResolver
{
    private readonly HitRoller _hitRoller;

    /// <summary>Uses the supplied hit roller for each strike.</summary>
    public SingleStrikeResolver(HitRoller hitRoller)
    {
        ArgumentNullException.ThrowIfNull(hitRoller);
        _hitRoller = hitRoller;
    }

    /// <summary>Resolves one physical strike using the current provisional modifier order.</summary>
    /// <remarks>
    /// Calculate damage before rolling so bad inputs or overflow do not waste a roll.
    /// Keep that damage if the strike hits; return zero if it misses.
    /// Runtime callers can supply the defender's EffectiveDefense; stats-only calls use base DEF.
    /// </remarks>
    public StrikeResult ResolvePhysicalStrike(
        CharacterStats attacker,
        CharacterStats defender,
        int weaponPower,
        WeaponCategory attackerWeapon,
        WeaponCategory defenderWeapon,
        int baseAccuracy,
        int? effectiveDefense = null)
        => ResolvePreparedStrike(PreparePhysicalStrike(
            attacker, defender, weaponPower, attackerWeapon, defenderWeapon, baseAccuracy, effectiveDefense));

    // The exchange resolver uses these same checks to prepare both strikes before
    // rolling or applying any damage.
    internal static (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) PreparePhysicalStrike(
        CharacterStats attacker,
        CharacterStats defender,
        int weaponPower,
        WeaponCategory attackerWeapon,
        WeaponCategory defenderWeapon,
        int baseAccuracy,
        int? effectiveDefense = null)
    {
        int hitChance = AccuracyCalculator.CalculateHitChance(baseAccuracy, attackerWeapon, defenderWeapon);
        int candidateDamage = CombatDamageCalculator.CalculatePhysicalDamage(
            attacker, defender, weaponPower, attackerWeapon, defenderWeapon, effectiveDefense);
        var outcome = WeaponTriangle.Evaluate(attackerWeapon, defenderWeapon);

        return (hitChance, candidateDamage, outcome);
    }

    /// <summary>Resolves one magical strike using INT against RES.</summary>
    /// <remarks>
    /// The attacker must use MagicScroll, which is neutral in the weapon triangle.
    /// As with physical strikes, check the damage before taking a roll.
    /// </remarks>
    public StrikeResult ResolveMagicalStrike(
        CharacterStats attacker,
        CharacterStats defender,
        int spellPower,
        WeaponCategory attackerWeapon,
        WeaponCategory defenderWeapon,
        int baseAccuracy)
        => ResolvePreparedStrike(PrepareMagicalStrike(
            attacker, defender, spellPower, attackerWeapon, defenderWeapon, baseAccuracy));

    internal static (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) PrepareMagicalStrike(
        CharacterStats attacker,
        CharacterStats defender,
        int spellPower,
        WeaponCategory attackerWeapon,
        WeaponCategory defenderWeapon,
        int baseAccuracy)
    {
        int hitChance = AccuracyCalculator.CalculateHitChance(baseAccuracy, attackerWeapon, defenderWeapon);
        if (attackerWeapon != WeaponCategory.MagicScroll)
            throw new ArgumentException("Magical strikes require the MagicScroll combat category.", nameof(attackerWeapon));

        int candidateDamage = DamageCalculator.CalculateMagicalDamage(attacker, defender, spellPower);
        var outcome = WeaponTriangle.Evaluate(attackerWeapon, defenderWeapon);

        return (hitChance, candidateDamage, outcome);
    }

    internal StrikeResult ResolvePreparedStrike(
        (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) prepared)
    {
        bool hit = _hitRoller.Roll(prepared.HitChance);
        return new StrikeResult(hit, prepared.HitChance, hit ? prepared.CandidateDamage : 0, prepared.Outcome);
    }
}
