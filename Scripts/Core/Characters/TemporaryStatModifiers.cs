using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Characters;

/// <summary>Owns explicit defensive bonuses for one recipient, separate from negative status timers.</summary>
public sealed class TemporaryStatModifiers
{
    private readonly Character _recipient;
    private readonly List<TemporaryStatModifier> _active = new();
    private readonly HashSet<string> _usedIds = new(StringComparer.Ordinal);
    private int _lastCleanupRound;
    public IReadOnlyList<TemporaryStatModifier> Active => Array.AsReadOnly(_active.ToArray());

    internal TemporaryStatModifiers(Character recipient) => _recipient = recipient;

    /// <summary>Applies an explicit group now after validating everything. This does not schedule future applications.</summary>
    /// <remarks>No implicit refresh or stacking is supported. The caller supplies its current application round.</remarks>
    public void AddRange(IEnumerable<TemporaryStatModifier> modifiers) => ApplyRange(modifiers, allowRefresh: false);

    /// <summary>Explicitly refreshes matching active IDs without increasing their values; validates the whole group first.</summary>
    /// <remarks>New IDs still follow ordinary validation. Unrelated same-stat bonuses are not overwritten or stacked.</remarks>
    public void RefreshOrAddRange(IEnumerable<TemporaryStatModifier> modifiers) => ApplyRange(modifiers, allowRefresh: true);

    private void ApplyRange(IEnumerable<TemporaryStatModifier> modifiers, bool allowRefresh)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        if (_recipient.IsDefeated) throw new InvalidOperationException("Defeated recipients cannot receive temporary bonuses.");
        var pending = modifiers.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var existingById = _active.ToDictionary(modifier => modifier.Id, StringComparer.Ordinal);
        var replacingIds = allowRefresh ? pending.Where(modifier => modifier is not null)
            .Select(modifier => modifier.Id).ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        var stats = _active.Where(modifier => !replacingIds.Contains(modifier.Id))
            .Select(modifier => modifier.Stat).ToHashSet();
        foreach (var modifier in pending)
        {
            ArgumentNullException.ThrowIfNull(modifier);
            if (!modifier.IsActive || modifier.RecipientCharacterId != _recipient.Id)
                throw new ArgumentException("Modifiers must be active and belong to this recipient.", nameof(modifiers));
            if (!ids.Add(modifier.Id)) throw new ArgumentException("A group cannot contain duplicate modifier IDs.", nameof(modifiers));
            if (allowRefresh && existingById.TryGetValue(modifier.Id, out var existing))
            {
                if (existing.Stat != modifier.Stat || existing.Value != modifier.Value)
                    throw new ArgumentException("A refresh cannot change the affected stat or bonus value.", nameof(modifiers));
                if (modifier.ApplicationRound < existing.ApplicationRound || modifier.ExpirationRound < existing.ExpirationRound)
                    throw new ArgumentException("A refresh cannot move an existing modifier's lifetime backward.", nameof(modifiers));
            }
            else if (_usedIds.Contains(modifier.Id))
                throw new ArgumentException("Modifier IDs cannot be reused after removal.", nameof(modifiers));
            if (!stats.Add(modifier.Stat))
                throw new NotSupportedException("Multiple active bonuses for the same stat require confirmed stacking rules.");
            if (modifier.ApplicationRound <= _lastCleanupRound)
                throw new ArgumentException("Cannot apply a modifier in a round whose Cleanup has already completed.", nameof(modifiers));
            int baseValue = modifier.Stat == TemporaryStat.Defense ? _recipient.BaseStats.Defense : _recipient.BaseStats.Resistance;
            // Check before storing, independently of Burn, so its expiration cannot cause a later overflow.
            _ = checked(baseValue + modifier.Value);
        }
        foreach (var modifier in pending)
        {
            int index = _active.FindIndex(active => active.Id == modifier.Id);
            if (index >= 0) _active[index] = modifier;
            else _active.Add(modifier);
            _usedIds.Add(modifier.Id);
        }
    }

    public int GetBonus(TemporaryStat stat)
    {
        if (!Enum.IsDefined(stat)) throw new ArgumentOutOfRangeException(nameof(stat));
        return _active.FirstOrDefault(modifier => modifier.Stat == stat)?.Value ?? 0;
    }

    /// <summary>Call at the end of Cleanup. Repeated calls return no additional expirations.</summary>
    public IReadOnlyList<TemporaryStatModifier> ExpireAtRoundEnd(int round, RoundPhase phase)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (phase != RoundPhase.Cleanup) throw new ArgumentException("Temporary bonuses expire at end of Cleanup.", nameof(phase));
        if (round < _lastCleanupRound) throw new ArgumentOutOfRangeException(nameof(round));
        var expired = _active.Where(modifier => modifier.ExpirationRound <= round)
            .Select(modifier => modifier.InactiveSnapshot()).ToArray();
        _active.RemoveAll(modifier => modifier.ExpirationRound <= round);
        _lastCleanupRound = round;
        return Array.AsReadOnly(expired);
    }

    internal void ClearOnDefeat() => _active.Clear();
}
