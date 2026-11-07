using System;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Scales follow-up damage while keeping unsettled balance choices in one place.</summary>
/// <remarks>
/// Spec v0.2, section 4.2 confirms 50% at 1.25x Speed and 75% at 1.75x or higher.
/// Provisional choices: hold at 50% between those thresholds, round down, and keep
/// positive damage at least 1. Zero damage stays zero. Positive Speed against zero
/// uses the 75% cap. None of these extra choices changes the confirmed Speed threshold.
/// </remarks>
public static class ProvisionalFollowUpDamageCalculator
{
    /// <summary>Scales the supplied damage for the participant that qualifies for the follow-up.</summary>
    /// <param name="qualifyingDamage">Damage eligible for scaling, before the follow-up reduction.</param>
    /// <param name="fasterSpeed">The qualifying participant's Speed, regardless of which side it is on.</param>
    /// <param name="slowerSpeed">The other participant's Speed.</param>
    public static int Calculate(int qualifyingDamage, int fasterSpeed, int slowerSpeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qualifyingDamage);
        ArgumentOutOfRangeException.ThrowIfNegative(fasterSpeed);
        ArgumentOutOfRangeException.ThrowIfNegative(slowerSpeed);
        if (FollowUpEligibility.Evaluate(fasterSpeed, slowerSpeed) != FollowUpParticipant.Attacker)
            throw new ArgumentException("The first Speed must qualify for a follow-up against the second.", nameof(fasterSpeed));

        int quarters = (long)fasterSpeed * 4 >= (long)slowerSpeed * 7 ? 3 : 2;
        // Integer division rounds down. Use long for the multiplication, even at int.MaxValue.
        int damage = (int)((long)qualifyingDamage * quarters / 4);
        return qualifyingDamage == 0 ? 0 : Math.Max(1, damage);
    }
}
