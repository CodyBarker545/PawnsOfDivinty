using System;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Rolls once to decide whether a strike hits.</summary>
public sealed class HitRoller
{
    private readonly IRandomRollProvider _randomProvider;

    public HitRoller(IRandomRollProvider randomProvider)
    {
        ArgumentNullException.ThrowIfNull(randomProvider);
        _randomProvider = randomProvider;
    }

    /// <summary>The strike hits when the roll is below the hit chance.</summary>
    /// <remarks>Still take one roll at 0% and 100%, so each valid strike uses one roll.</remarks>
    /// <param name="hitChance">The final hit chance, from 0 to 100.</param>
    /// <exception cref="ArgumentOutOfRangeException">The hit chance is outside 0-100. No roll is taken.</exception>
    /// <exception cref="InvalidOperationException">The random provider returned a value outside 0-99.</exception>
    public bool Roll(int hitChance)
    {
        if (hitChance < 0 || hitChance > 100)
            throw new ArgumentOutOfRangeException(nameof(hitChance), hitChance,
                "Hit chance must be between 0 and 100 inclusive.");

        int roll = _randomProvider.NextRoll();
        if (roll < 0 || roll > 99)
            throw new InvalidOperationException(
                $"Random roll provider returned {roll}; expected a value between 0 and 99 inclusive.");

        return roll < hitChance;
    }
}
