using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>Immutable before/after timers and expirations for one character's status update.</summary>
public sealed record TimedStatusUpdate
{
    public string CharacterId { get; }
    public IReadOnlyDictionary<StatusType, int> StatusesBefore { get; }
    public IReadOnlyDictionary<StatusType, int> StatusesAfter { get; }
    public IReadOnlyList<StatusType> ExpiredStatuses { get; }

    internal TimedStatusUpdate(string characterId, IReadOnlyDictionary<StatusType, int> before,
        IReadOnlyDictionary<StatusType, int> after)
    {
        CharacterId = characterId;
        StatusesBefore = Snapshot(before);
        StatusesAfter = Snapshot(after);
        // Report removals from the tracker's update; do not repeat its duration arithmetic.
        ExpiredStatuses = Array.AsReadOnly(StatusesBefore.Keys.Where(status => !StatusesAfter.ContainsKey(status)).ToArray());
    }

    private static IReadOnlyDictionary<StatusType, int> Snapshot(IReadOnlyDictionary<StatusType, int> statuses)
        => new ReadOnlyDictionary<StatusType, int>(statuses.OrderBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => pair.Value));
}
