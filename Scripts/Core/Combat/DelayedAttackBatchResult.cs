using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Snapshots processed due attacks, cancellations and newly caused defeats.</summary>
public sealed record DelayedAttackBatchResult
{
    public int RoundNumber { get; }
    public IReadOnlyList<DelayedAttackResolution> Entries { get; }
    public IReadOnlyList<string> ProcessedAttackIds { get; }
    public IReadOnlyList<string> DefeatedRecipientIds { get; }
    /// <summary>Cancelled due and future attacks, in original insertion order.</summary>
    public IReadOnlyList<string> CancelledAttackIds { get; }
    public int RemainingAttackCount { get; }

    internal DelayedAttackBatchResult(int roundNumber, IEnumerable<DelayedAttackResolution> entries,
        IEnumerable<string> defeatedIds, IEnumerable<string> cancelledIds, int remainingAttackCount)
    {
        RoundNumber = roundNumber;
        var snapshot = entries.ToArray();
        Entries = Array.AsReadOnly(snapshot);
        ProcessedAttackIds = Array.AsReadOnly(snapshot.Select(entry => entry.Attack.Id).ToArray());
        DefeatedRecipientIds = Array.AsReadOnly(defeatedIds.ToArray());
        CancelledAttackIds = Array.AsReadOnly(cancelledIds.ToArray());
        RemainingAttackCount = remainingAttackCount;
    }
}
