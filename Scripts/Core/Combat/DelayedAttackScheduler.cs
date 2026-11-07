using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Stores delayed attacks in insertion order without rolling, applying damage or advancing rounds.</summary>
public sealed class DelayedAttackScheduler
{
    private readonly List<DelayedAttack> _pending = new();
    private long _lastSequence;

    /// <summary>A read-only snapshot; later insertions do not alter an earlier view.</summary>
    public IReadOnlyList<DelayedAttack> PendingAttacks => Array.AsReadOnly(_pending.ToArray());

    /// <summary>Stores a sequenced copy and returns it. IDs must be unique within this scheduler.</summary>
    public DelayedAttack Schedule(DelayedAttack attack)
    {
        ArgumentNullException.ThrowIfNull(attack);
        if (_pending.Any(pending => StringComparer.Ordinal.Equals(pending.Id, attack.Id)))
            throw new ArgumentException("A delayed attack with this ID is already scheduled.", nameof(attack));
        long sequence = checked(_lastSequence + 1);
        var scheduled = attack.WithInsertionSequence(sequence);
        _pending.Add(scheduled);
        _lastSequence = sequence;
        return scheduled;
    }

    /// <summary>Finds attacks scheduled for this round or earlier, preserving insertion order.</summary>
    /// <remarks>
    /// Overdue entries remain visible rather than being silently lost. This query does not
    /// remove anything. A future delayed-phase processor must consume/cancel entries,
    /// look up living targets, and apply stored outcomes against shields present at resolution.
    /// </remarks>
    public IReadOnlyList<DelayedAttack> GetDueAttacks(int round)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        return Array.AsReadOnly(_pending.Where(attack => attack.ResolutionRound <= round).ToArray());
    }
}
