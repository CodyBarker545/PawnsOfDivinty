using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Stores delayed attacks in insertion order without rolling, applying damage or advancing rounds.</summary>
public sealed class DelayedAttackScheduler
{
    private readonly List<DelayedAttack> _pending = new();
    private readonly HashSet<string> _scheduledIds = new(StringComparer.Ordinal);
    private long _lastSequence;

    /// <summary>A read-only snapshot; later insertions do not alter an earlier view.</summary>
    public IReadOnlyList<DelayedAttack> PendingAttacks => Array.AsReadOnly(_pending.ToArray());

    /// <summary>Stores a sequenced copy. IDs remain reserved after consumption to preserve attack identity.</summary>
    public DelayedAttack Schedule(DelayedAttack attack)
    {
        ArgumentNullException.ThrowIfNull(attack);
        attack.Validate();
        ValidateCanSchedule(attack.Id);
        long sequence = checked(_lastSequence + 1);
        var scheduled = attack.WithInsertionSequence(sequence);
        _pending.Add(scheduled);
        _scheduledIds.Add(scheduled.Id);
        _lastSequence = sequence;
        return scheduled;
    }

    /// <summary>Checks ID availability and ordering capacity before a casting caller consumes randomness.</summary>
    /// <remarks>This does not reserve the ID. Keep validation and scheduling synchronous on the match thread.</remarks>
    public void ValidateCanSchedule(string attackId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attackId);
        if (_scheduledIds.Contains(attackId))
            throw new ArgumentException("A delayed attack with this ID has already been scheduled.", nameof(attackId));
        _ = checked(_lastSequence + 1);
    }

    // The processor consumes only prevalidated entries. Keeping this internal prevents
    // callers from accidentally removing attacks without recording a resolution or cancellation.
    internal void Consume(string id) => _pending.RemoveAll(attack => StringComparer.Ordinal.Equals(attack.Id, id));

    /// <summary>Finds attacks scheduled for this round or earlier, preserving insertion order.</summary>
    /// <remarks>
    /// Overdue entries remain visible rather than being silently lost. This query does not
    /// remove anything. DelayedAttackProcessor consumes/cancels entries,
    /// look up living targets, and apply stored outcomes against shields present at resolution.
    /// </remarks>
    public IReadOnlyList<DelayedAttack> GetDueAttacks(int round)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        return Array.AsReadOnly(_pending.Where(attack => attack.ResolutionRound <= round).ToArray());
    }
}
