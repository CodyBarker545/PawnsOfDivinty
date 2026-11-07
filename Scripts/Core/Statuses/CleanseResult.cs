using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>A snapshot of the negative statuses actually removed by a cleanse.</summary>
public sealed record CleanseResult
{
    public IReadOnlyList<StatusType> RemovedStatuses { get; }

    internal CleanseResult(IEnumerable<StatusType> removedStatuses)
    {
        // Own the snapshot so later tracker changes cannot alter a recorded result.
        RemovedStatuses = Array.AsReadOnly(removedStatuses.ToArray());
    }
}
