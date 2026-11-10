using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Gameplay;

public sealed record CollisionParticipantResult
{
    public string CharacterId { get; }
    public int CalculatedDamage { get; }
    public int ShieldAbsorbed { get; }
    public int HealthLost { get; }
    public int ResultingHealth { get; }
    public bool Defeated { get; }
    internal CollisionParticipantResult(Character character, int damage, int shieldAbsorbed, int healthLost)
    {
        CharacterId = character.Id;
        CalculatedDamage = damage;
        ShieldAbsorbed = shieldAbsorbed;
        HealthLost = healthLost;
        ResultingHealth = character.CurrentHealth;
        Defeated = character.IsDefeated;
    }
}

public sealed record CollisionDamageResult
{
    public IReadOnlyList<CollisionParticipantResult> Participants { get; }
    public IReadOnlyList<string> DefeatedCharacterIds { get; }
    internal CollisionDamageResult(IEnumerable<CollisionParticipantResult> participants)
    {
        var snapshot = participants.ToArray();
        Participants = Array.AsReadOnly(snapshot);
        DefeatedCharacterIds = Array.AsReadOnly(snapshot.Where(p => p.Defeated).Select(p => p.CharacterId).ToArray());
    }
}

/// <summary>Applies one confirmed collision event, without deciding collision geometry or stopping positions.</summary>
public static class CollisionDamageApplicator
{
    public const int Damage = 3;

    /// <remarks>
    /// The caller must have established an actual collision and supplies only its actual participants.
    /// Each invocation is one event, not an idempotent command. No armor, Fury, triangle, scaling or
    /// accuracy roll is involved, and the second unit is never moved by this helper.
    /// </remarks>
    public static CollisionDamageResult Apply(Character pushedTarget, Character? secondUnit = null)
    {
        ArgumentNullException.ThrowIfNull(pushedTarget);
        if (pushedTarget.IsDefeated || secondUnit?.IsDefeated == true)
            throw new ArgumentException("Collision participants must be living characters.");
        if (secondUnit is not null && (ReferenceEquals(pushedTarget, secondUnit) || pushedTarget.Id == secondUnit.Id))
            throw new ArgumentException("Collision participants must have distinct instances and IDs.", nameof(secondUnit));
        var participants = secondUnit is null ? new[] { pushedTarget } : new[] { pushedTarget, secondUnit };
        var results = new List<CollisionParticipantResult>();
        foreach (var character in participants)
        {
            int shieldBefore = character.ShieldHP;
            int lost = character.TakeDamage(Damage);
            results.Add(new(character, Damage, shieldBefore - character.ShieldHP, lost));
        }
        return new(results);
    }
}
