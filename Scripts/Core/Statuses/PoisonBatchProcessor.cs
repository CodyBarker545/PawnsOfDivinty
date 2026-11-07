using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>Snapshots and applies one round's Poison batch through the existing individual processor.</summary>
public sealed class PoisonBatchProcessor
{
    private readonly RoundLifecycleManager _lifecycle;

    public PoisonBatchProcessor(RoundLifecycleManager lifecycle)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        _lifecycle = lifecycle;
    }

    /// <summary>Validates all participants and scheduled damage before applying ticks in input order.</summary>
    /// <remarks>
    /// Supply the match participants in a stable order during the round-start Poison phase.
    /// Application timing must leave newly inflicted Poison for the following round's batch.
    /// Empty batches count as processed; invalid batches do not consume execution.
    /// This is synchronous match logic: participants must not change concurrently during processing.
    /// No global death effects, loot or phase advancement run here. Handle the reported defeats
    /// only after this method returns, once every scheduled tick has been applied.
    /// </remarks>
    public PoisonBatchResult Process(IEnumerable<Character> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        _lifecycle.ValidatePoisonBatchExecution();
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

        var scheduled = roster.Where(character => !character.IsDefeated && character.Statuses.IsPoisoned)
            .Select(PoisonProcessor.Prepare).ToArray();
        if (_lifecycle.CurrentRound != round)
            throw new InvalidOperationException("The round changed while preparing the Poison batch.");
        // Reserve execution only after all roster and arithmetic checks succeed.
        _lifecycle.RecordPoisonBatchExecution();
        var entries = new List<PoisonBatchEntry>();
        foreach (var tick in scheduled)
            entries.Add(new PoisonBatchEntry(tick.Character.Id, PoisonProcessor.ApplyPrepared(tick)));
        return new PoisonBatchResult(round, entries);
    }
}
