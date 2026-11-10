using System;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>An immutable record of one ability's most recent use and absolute ready-round deadline.</summary>
public sealed record AbilityCooldown
{
    public int RoundUsed { get; }
    public int ReuseInterval { get; }
    public int ReadyRound { get; }

    public AbilityCooldown(int roundUsed, int reuseInterval)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roundUsed);
        ArgumentOutOfRangeException.ThrowIfNegative(reuseInterval);
        ReadyRound = checked(roundUsed + reuseInterval);
        RoundUsed = roundUsed;
        ReuseInterval = reuseInterval;
    }
}
