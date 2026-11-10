using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Tracks ability deadlines for one character without counting activations or decrementing timers.</summary>
public sealed class AbilityCooldownTracker
{
    private readonly Dictionary<string, AbilityCooldown> _cooldowns = new(StringComparer.Ordinal);

    /// <summary>A read-only view keyed by exact stable ability IDs, with immutable entry values.</summary>
    public IReadOnlyDictionary<string, AbilityCooldown> Cooldowns { get; }

    public AbilityCooldownTracker()
    {
        Cooldowns = new ReadOnlyDictionary<string, AbilityCooldown>(_cooldowns);
    }

    /// <summary>An unused ability is ready; a recorded ability is ready at or after its deadline.</summary>
    /// <remarks>This checks cooldown only, not health, Silence, action availability or ability ownership.</remarks>
    public bool IsReady(string abilityId, int currentRound)
    {
        ValidateAbilityId(abilityId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentRound);
        return !_cooldowns.TryGetValue(abilityId, out var cooldown) || currentRound >= cooldown.ReadyRound;
    }

    /// <summary>Records a use only when the existing cooldown permits it in the supplied round.</summary>
    /// <remarks>
    /// The caller supplies the reuse interval; no character-specific balance values live here.
    /// Zero reuse is immediately ready, leaving action limits to the future ability executor.
    /// Validation and checked arithmetic finish before any entry is replaced.
    /// </remarks>
    public void RecordUse(string abilityId, int roundUsed, int reuseInterval)
    {
        ValidateAbilityId(abilityId);
        var cooldown = new AbilityCooldown(roundUsed, reuseInterval);
        if (!IsReady(abilityId, roundUsed))
            throw new InvalidOperationException("The ability is still on cooldown in the supplied round.");
        _cooldowns[abilityId] = cooldown;
    }

    private static void ValidateAbilityId(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            throw new ArgumentException("A stable ability ID is required.", nameof(abilityId));
    }
}
