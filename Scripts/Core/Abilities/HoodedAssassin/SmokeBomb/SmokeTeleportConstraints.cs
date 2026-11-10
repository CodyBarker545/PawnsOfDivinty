using System;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Explicit caller decisions for teleport cases not settled by the Smoke Bomb description.</summary>
public sealed record SmokeTeleportConstraints
{
    public bool? AllowCrossArea { get; }
    public bool? AllowSamePosition { get; }
    public bool? AllowOtherOwners { get; }
    public int? MaximumDistance { get; }

    public SmokeTeleportConstraints(bool? allowCrossArea = null, bool? allowSamePosition = null,
        bool? allowOtherOwners = null, int? maximumDistance = null)
    {
        if (maximumDistance.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(maximumDistance.Value, nameof(maximumDistance));
        AllowCrossArea = allowCrossArea;
        AllowSamePosition = allowSamePosition;
        AllowOtherOwners = allowOtherOwners;
        MaximumDistance = maximumDistance;
    }
}
