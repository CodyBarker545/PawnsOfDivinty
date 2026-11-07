using System;

namespace PawnsOfDivinity.Core.Statuses;

/// <summary>Calculates the 2, 4, 6, 8... Poison sequence from spec v0.2, section 6.1.</summary>
public static class PoisonDamageCalculator
{
    /// <summary>Returns damage for a one-based tick number without changing character state.</summary>
    public static int Calculate(int tickNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickNumber);
        return checked(tickNumber * 2);
    }
}
