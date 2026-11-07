using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Checks whether a defender can counter, following spec v0.2, sections 4.1-4.2.</summary>
public static class CounterattackEligibility
{
    /// <summary>Returns whether the surviving defender is allowed to counter and can reach the attacker.</summary>
    /// <param name="defender">The defender, after the opening damage has been applied.</param>
    /// <param name="distance">The Manhattan distance between the characters; must be zero or greater.</param>
    /// <param name="minimumRange">The closest distance the defender's available attack can reach.</param>
    /// <param name="maximumRange">The farthest distance that attack can reach.</param>
    /// <param name="incomingAttackPermitsCounters">False for attacks that forbid counters, such as AoE or Temporal Ignition.</param>
    /// <param name="counterattacksSuppressed">True when something prevents the defender from countering.</param>
    /// <remarks>
    /// A character can counter even if it already acted this round. The caller supplies the
    /// available attack range; equipment, sight and elevation checks belong elsewhere.
    /// Zero distance is allowed if it falls within the supplied range.
    /// </remarks>
    public static bool CanCounter(
        Character defender,
        int distance,
        int minimumRange,
        int maximumRange,
        bool incomingAttackPermitsCounters,
        bool counterattacksSuppressed)
    {
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentOutOfRangeException.ThrowIfNegative(distance);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRange);
        if (maximumRange < minimumRange)
            throw new ArgumentOutOfRangeException(nameof(maximumRange), maximumRange,
                "Maximum range must be greater than or equal to minimum range.");

        return !defender.IsDefeated
            && incomingAttackPermitsCounters
            && !counterattacksSuppressed
            && distance >= minimumRange
            && distance <= maximumRange;
    }
}
