using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Consumes the pre-existing due queue using stored strikes and current shield-first health handling.</summary>
public sealed class DelayedAttackProcessor
{
    private readonly DelayedAttackScheduler _scheduler;
    private readonly RoundLifecycleManager _lifecycle;

    public DelayedAttackProcessor(DelayedAttackScheduler scheduler, RoundLifecycleManager lifecycle)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(lifecycle);
        _scheduler = scheduler;
        _lifecycle = lifecycle;
    }

    /// <summary>Validates the complete batch before consuming entries or applying stored damage.</summary>
    /// <remarks>
    /// Spec v0.2 section 6.2 cancels attacks against defeated targets, including future entries.
    /// A skipped due attack still reports its original hit/miss and damage, with zero actual loss.
    /// Prototype policy: missing due targets reject the batch; missing or dead casters do not
    /// invalidate already-created attacks. No revival, retargeting or global death cleanup occurs.
    /// Match state and the queue must not be changed concurrently during this synchronous call.
    /// </remarks>
    public DelayedAttackBatchResult Process(IEnumerable<Character> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        _lifecycle.ValidateDelayedAttackExecution();
        int round = _lifecycle.CurrentRound;
        var roster = new Dictionary<string, Character>(StringComparer.Ordinal);
        foreach (var character in participants.ToArray())
        {
            if (character is null) throw new ArgumentException("Participants cannot contain null characters.", nameof(participants));
            if (!roster.TryAdd(character.Id, character))
                throw new ArgumentException("Character instance IDs must be unique.", nameof(participants));
        }

        var pending = _scheduler.PendingAttacks;
        foreach (var attack in pending)
        {
            attack.Validate();
            if (attack.InsertionSequence <= 0) throw new ArgumentException("A scheduled attack must have a positive insertion sequence.");
        }
        var due = _scheduler.GetDueAttacks(round);
        foreach (var attack in due)
            if (!roster.ContainsKey(attack.TargetCharacterId))
                throw new ArgumentException("Every due attack must reference a known target instance ID.", nameof(participants));
        if (_lifecycle.CurrentRound != round)
            throw new InvalidOperationException("The round changed while validating delayed attacks.");
        _lifecycle.RecordDelayedAttackExecution();

        var entries = new List<DelayedAttackResolution>();
        var defeated = new List<string>();
        var cancelled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in roster.Values.Where(character => character.IsDefeated)) CancelTarget(target.Id);

        foreach (var attack in due)
        {
            var target = roster[attack.TargetCharacterId];
            bool skipped = target.IsDefeated;
            int previousShield = target.ShieldHP;
            int lost = skipped ? 0 : StrikeDamageApplicator.Apply(attack.Strike, target);
            _scheduler.Consume(attack.Id);
            entries.Add(new DelayedAttackResolution(attack, previousShield - target.ShieldHP, lost,
                target.CurrentHealth, target.IsDefeated, skipped));
            if (!skipped && target.IsDefeated)
            {
                defeated.Add(target.Id);
                CancelTarget(target.Id);
            }
        }

        return new DelayedAttackBatchResult(round, entries, defeated,
            pending.Where(attack => cancelled.Contains(attack.Id)).Select(attack => attack.Id),
            _scheduler.PendingAttacks.Count);

        void CancelTarget(string targetId)
        {
            foreach (var attack in _scheduler.PendingAttacks.Where(attack => attack.TargetCharacterId == targetId))
            {
                cancelled.Add(attack.Id);
                _scheduler.Consume(attack.Id);
            }
        }
    }
}
