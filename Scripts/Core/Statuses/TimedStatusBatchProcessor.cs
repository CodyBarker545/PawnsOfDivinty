using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>Coordinates timed-status updates after this round's Poison and delayed attacks finish.</summary>
public sealed class TimedStatusBatchProcessor
{
    private readonly RoundLifecycleManager _lifecycle;

    public TimedStatusBatchProcessor(RoundLifecycleManager lifecycle)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        _lifecycle = lifecycle;
    }

    /// <summary>Snapshots all timers, then asks each character's tracker to advance once.</summary>
    /// <remarks>
    /// Results include every supplied character, even one with no timed statuses. Defeat
    /// already removes personal negative statuses through Character.TakeDamage, so those
    /// removals are not reported as expirations here. Poison never uses this countdown.
    /// Empty batches count as processed. Invalid input does not consume the lifecycle guard.
    /// This synchronous operation requires participants not to change concurrently.
    /// No phase advancement, activation reset or healing-history reset happens here.
    /// </remarks>
    public TimedStatusBatchResult Process(IEnumerable<Character> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        _lifecycle.ValidateTimedStatusExecution();
        int round = _lifecycle.CurrentRound;
        var roster = participants.ToArray();
        var instances = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in roster)
        {
            if (character is null) throw new ArgumentException("Participants cannot contain null characters.", nameof(participants));
            if (!instances.Add(character) || !ids.Add(character.Id))
                throw new ArgumentException("Participants must have unique instances and instance IDs.", nameof(participants));
        }

        var scheduled = roster.Select(character => (Character: character,
            Before: character.Statuses.TimedStatuses.ToDictionary(pair => pair.Key, pair => pair.Value))).ToArray();
        if (_lifecycle.CurrentRound != round)
            throw new InvalidOperationException("The round changed while preparing timed-status updates.");
        _lifecycle.RecordTimedStatusExecution();

        var entries = new List<TimedStatusUpdate>();
        foreach (var update in scheduled)
        {
            update.Character.Statuses.AdvanceTimedStatuses();
            entries.Add(new TimedStatusUpdate(update.Character.Id, update.Before, update.Character.Statuses.TimedStatuses));
        }
        return new TimedStatusBatchResult(round, entries);
    }
}
