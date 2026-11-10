using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>Keeps timed negative statuses and the separate progression of persistent Poison.</summary>
/// <remarks>
/// This tracks status state, not damage, stat penalties or action restrictions.
/// Poison has a separate lifetime and escalation rule, so it cannot use this countdown API.
/// </remarks>
public sealed class StatusTracker
{
    private readonly Dictionary<StatusType, int> _timedStatuses = new();

    /// <summary>A read-only view of active timed statuses and their remaining rounds.</summary>
    public IReadOnlyDictionary<StatusType, int> TimedStatuses { get; }

    public bool IsPoisoned { get; private set; }
    /// <summary>Completed Poison ticks in this sequence; zero before the first tick or after clearing.</summary>
    public int PoisonTicksOccurred { get; private set; }

    /// <summary>Starts Poison without damage. Reapplication leaves the current sequence alone.</summary>
    public void ApplyPoison() => IsPoisoned = true;

    /// <summary>Removes only Poison and resets its progression. Safe to call when absent.</summary>
    public void ClearPoison()
    {
        IsPoisoned = false;
        PoisonTicksOccurred = 0;
    }

    /// <summary>Removes one selected negative status. Returns false when it was already absent.</summary>
    public bool RemoveStatus(StatusType status)
    {
        ValidateStatus(status);
        if (status != StatusType.Poison) return _timedStatuses.Remove(status);

        bool removed = IsPoisoned;
        ClearPoison();
        return removed;
    }

    /// <summary>Removes all active negative statuses and returns a read-only snapshot in enum order.</summary>
    public IReadOnlyList<StatusType> RemoveAllNegativeStatuses()
    {
        var removed = new List<StatusType>();
        foreach (var status in Enum.GetValues<StatusType>())
        {
            if (RemoveStatus(status)) removed.Add(status);
        }
        return removed.AsReadOnly();
    }

    // Only the processor records a completed tick. Callers cannot set or skip progression.
    internal void RecordPoisonTick()
    {
        if (!IsPoisoned) throw new InvalidOperationException("Cannot record a tick when Poison is inactive.");
        PoisonTicksOccurred = checked(PoisonTicksOccurred + 1);
    }

    public StatusTracker()
    {
        // A wrapper prevents callers from casting the view back to our mutable dictionary.
        TimedStatuses = new ReadOnlyDictionary<StatusType, int>(_timedStatuses);
    }

    /// <summary>Applies a timed status or refreshes its remaining rounds to the supplied duration.</summary>
    /// <param name="status">An ordinary timed status; Poison is not supported here.</param>
    /// <param name="duration">Positive duration in global rounds, including the application round.</param>
    public void ApplyTimedStatus(StatusType status, int duration)
    {
        ValidateStatus(status);
        if (status == StatusType.Poison)
            throw new ArgumentException("Poison does not use an ordinary timed duration.", nameof(status));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);

        // One entry per type: reapplication replaces the timer instead of adding another copy.
        _timedStatuses[status] = duration;
    }

    /// <summary>Checks active timed statuses and persistent Poison.</summary>
    public bool HasStatus(StatusType status)
    {
        ValidateStatus(status);
        return status == StatusType.Poison ? IsPoisoned : _timedStatuses.ContainsKey(status);
    }

    /// <summary>Decreases each timer by one and removes statuses that reach zero.</summary>
    /// <remarks>
    /// TimedStatusBatchProcessor calls this once at round start, after Poison and delayed
    /// damage. A two-round status applied in R2 ticks to 1 at R3 start and expires at R4 start.
    /// This method does not advance the round or resolve either damage stage itself.
    /// </remarks>
    public void AdvanceTimedStatuses()
    {
        // Copy the keys so we can remove expired entries while walking the list.
        foreach (var status in _timedStatuses.Keys.ToArray())
        {
            int remaining = _timedStatuses[status] - 1;
            if (remaining == 0) _timedStatuses.Remove(status);
            else _timedStatuses[status] = remaining;
        }
    }

    private static void ValidateStatus(StatusType status)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status type.");
    }
}
