using System;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Core.Abilities;

public enum BerserkersFuryOutcome { NotPassiveOwner, NoMissingHealthBonus, DamageBonusAvailable }

/// <summary>An HP snapshot and its derived bonus, not a granted buff or persistent progress.</summary>
public sealed record BerserkersFuryResult
{
    public string CharacterId { get; }
    public int MaxHealth { get; }
    public int CurrentHealth { get; }
    public int MissingHealth => MaxHealth - CurrentHealth;
    public int PhysicalDamageBonus { get; }
    public BerserkersFuryOutcome Outcome { get; }
    public bool OwnsPassive => Outcome != BerserkersFuryOutcome.NotPassiveOwner;
    public bool IsDefeated => CurrentHealth == 0;
    /// <summary>The general modifier pipeline is still unsettled; ordinary combat uses a limited flat addition.</summary>
    public bool UsesProvisionalNormalDamageComposition => OwnsPassive && PhysicalDamageBonus > 0;

    internal BerserkersFuryResult(Character character, bool ownsPassive, int bonus)
    {
        CharacterId = character.Id;
        MaxHealth = character.BaseStats.MaxHealth;
        CurrentHealth = character.CurrentHealth;
        PhysicalDamageBonus = bonus;
        Outcome = !ownsPassive ? BerserkersFuryOutcome.NotPassiveOwner
            : bonus == 0 ? BerserkersFuryOutcome.NoMissingHealthBonus : BerserkersFuryOutcome.DamageBonusAvailable;
    }
}

/// <summary>Section 12.4: one physical damage per complete ten percent of maximum HP missing.</summary>
public static class BerserkersFury
{
    public static BerserkersFuryResult Evaluate(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        bool ownsPassive = character.Definition?.Id == "nigel" && character.Definition.Abilities.Any(a =>
            a.Id == Nigel.BerserkersFuryId && a.Classification == AbilityClassification.Passive);
        int bonus = ownsPassive ? (int)(10L * (character.BaseStats.MaxHealth - character.CurrentHealth)
            / character.BaseStats.MaxHealth) : 0;
        // At zero HP the formula evaluates to 10; it does not authorize an attack or revival.
        // Existing combat/activation eligibility still rejects defeated characters.
        return new(character, ownsPassive, bonus);
    }

    public static int GetPhysicalDamageBonus(Character character) => Evaluate(character).PhysicalDamageBonus;

    // Before an exchange mutates health, check damage that could be needed by a later
    // living counter/follow-up. This is an arithmetic bound, not an extra gameplay cap.
    internal static void ValidatePotentialDamage(Character character, int damageWithCurrentBonus)
    {
        var current = Evaluate(character);
        if (!current.OwnsPassive) return;
        int maximumLivingBonus = (int)(10L * (current.MaxHealth - 1) / current.MaxHealth);
        _ = checked(damageWithCurrentBonus - current.PhysicalDamageBonus + maximumLivingBonus);
    }
}
