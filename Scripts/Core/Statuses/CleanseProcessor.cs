using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>Removes selected or all negative statuses without handling consumables or actions.</summary>
/// <remarks>
/// Absent statuses produce an empty result. Whether a no-op cleanse is a valid item use
/// belongs to later gameplay validation, alongside inventory, Utility Actions and targeting.
/// Shields, positive effects and pending delayed damage are outside this status tracker.
/// </remarks>
public static class CleanseProcessor
{
    /// <summary>Removes only the selected negative status, resetting progression if it is Poison.</summary>
    public static CleanseResult BasicCleanse(Character recipient, StatusType status)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        bool removed = recipient.Statuses.RemoveStatus(status);
        return new CleanseResult(removed ? new[] { status } : Array.Empty<StatusType>());
    }

    /// <summary>Removes all active negative statuses and reports them in stable enum order.</summary>
    public static CleanseResult GreaterCleanse(Character recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return new CleanseResult(recipient.Statuses.RemoveAllNegativeStatuses());
    }
}
