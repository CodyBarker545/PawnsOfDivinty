using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Applies a resolved strike through Character's shield-first damage handling.</summary>
public static class StrikeDamageApplicator
{
    /// <summary>Returns how much health the defender actually lost.</summary>
    /// <remarks>
    /// A hit goes through Character.TakeDamage; a miss leaves health alone.
    /// Call this once per strike. Calling it again applies the same result again;
    /// this class does not keep track of which results have already been applied.
    /// </remarks>
    public static int Apply(StrikeResult strike, Character defender)
    {
        ArgumentNullException.ThrowIfNull(strike);
        ArgumentNullException.ThrowIfNull(defender);

        return strike.Hit ? defender.TakeDamage(strike.Damage) : 0;
    }
}
