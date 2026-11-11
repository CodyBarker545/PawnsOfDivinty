using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Abilities;

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
    /// A supplied evasion modifier goes through the existing accuracy calculator.
    /// The runtime caller resolves any physicalDamageBonus from passive ownership and current
    /// state. Stats-only calls default to zero because base stats do not identify a character.
    /// </remarks>
    public StrikeResult ResolvePhysicalStrike(
        CharacterStats attacker,
        CharacterStats defender,
        int weaponPower,
        WeaponCategory attackerWeapon,
        WeaponCategory defenderWeapon,
        int baseAccuracy,
        int? effectiveDefense = null,
        int evasionModifier = 0,
        int physicalDamageBonus = 0)
        => ResolvePreparedStrike(PreparePhysicalStrike(
            attacker, defender, weaponPower, attackerWeapon, defenderWeapon, baseAccuracy, effectiveDefense, evasionModifier, physicalDamageBonus));

    // The exchange resolver uses these same checks to prepare both strikes before
    // rolling or applying any damage.
    internal static (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) PreparePhysicalStrike(
        CharacterStats attacker,
        CharacterStats defender,
        int weaponPower,
        WeaponCategory attackerWeapon,
        WeaponCategory defenderWeapon,
        int baseAccuracy,
        int? effectiveDefense = null,
        int evasionModifier = 0,
        int physicalDamageBonus = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(physicalDamageBonus);
        int hitChance = AccuracyCalculator.CalculateHitChance(baseAccuracy, attackerWeapon, defenderWeapon, evasionModifier);
        // Limited provisional composition: add the supplied flat bonus to the existing
        // physical/triangle result, before any follow-up reduction. This does not settle
        // the universal modifier order or Brutal Knockback's distinct reduction rule.
        int candidateDamage = checked(CombatDamageCalculator.CalculatePhysicalDamage(
            attacker, defender, weaponPower, attackerWeapon, defenderWeapon, effectiveDefense) + physicalDamageBonus);
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
        int baseAccuracy,
        int evasionModifier = 0,
        int? effectiveResistance = null,
        int magicalDamageBonus = 0)
        => ResolvePreparedStrike(PrepareMagicalStrike(
            attacker, defender, spellPower, attackerWeapon, defenderWeapon, baseAccuracy, evasionModifier, effectiveResistance, magicalDamageBonus));

    internal static (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) PrepareMagicalStrike(
        CharacterStats attacker,
        CharacterStats defender,
        int spellPower,
        WeaponCategory attackerWeapon,
        WeaponCategory defenderWeapon,
        int baseAccuracy,
        int evasionModifier = 0,
        int? effectiveResistance = null,
        int magicalDamageBonus = 0)
    {
        int hitChance = AccuracyCalculator.CalculateHitChance(baseAccuracy, attackerWeapon, defenderWeapon, evasionModifier);
        if (attackerWeapon != WeaponCategory.MagicScroll)
            throw new ArgumentException("Magical strikes require the MagicScroll combat category.", nameof(attackerWeapon));

        ArgumentOutOfRangeException.ThrowIfNegative(magicalDamageBonus);
        // Existing base damage precedes this flat bonus. This does not define future percentage-modifier ordering.
        int candidateDamage = checked(DamageCalculator.CalculateMagicalDamage(attacker, defender, spellPower, effectiveResistance)
            + magicalDamageBonus);
        var outcome = WeaponTriangle.Evaluate(attackerWeapon, defenderWeapon);

        return (hitChance, candidateDamage, outcome);
    }

    internal StrikeResult ResolvePreparedStrike(
        (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) prepared,
        WinterHunterEvaluation? winterHunter = null)
    {
        bool hit = _hitRoller.Roll(prepared.HitChance);
        return new StrikeResult(hit, prepared.HitChance, hit ? prepared.CandidateDamage : 0, prepared.Outcome, winterHunter);
    }
}
