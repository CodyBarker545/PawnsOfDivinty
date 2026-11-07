using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>Snapshots the batch's ticks and Poison-caused defeats for later global death handling.</summary>
public sealed record PoisonBatchResult
{
    public int RoundNumber { get; }
    public IReadOnlyList<PoisonBatchEntry> Entries { get; }
    public IReadOnlyList<string> DefeatedCharacterIds { get; }

    internal PoisonBatchResult(int roundNumber, IEnumerable<PoisonBatchEntry> entries)
    {
        RoundNumber = roundNumber;
        var snapshot = entries.ToArray();
        Entries = Array.AsReadOnly(snapshot);
        DefeatedCharacterIds = Array.AsReadOnly(snapshot.Where(entry => entry.Tick.Defeated)
            .Select(entry => entry.CharacterId).ToArray());
    }
}
