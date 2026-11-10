using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>A read-only snapshot of one round's timed-status updates in roster order.</summary>
public sealed record TimedStatusBatchResult
{
    public int RoundNumber { get; }
    public IReadOnlyList<TimedStatusUpdate> Entries { get; }

    internal TimedStatusBatchResult(int roundNumber, IEnumerable<TimedStatusUpdate> entries)
    {
        RoundNumber = roundNumber;
        Entries = Array.AsReadOnly(entries.ToArray());
    }
}
