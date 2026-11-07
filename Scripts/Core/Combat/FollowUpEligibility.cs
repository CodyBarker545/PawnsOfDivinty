using System;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Compares Speed using the 1.25x threshold in spec v0.2, section 4.2.</summary>
public static class FollowUpEligibility
{
    /// <summary>Returns the faster participant if its Speed is at least 1.25 times the other's.</summary>
    /// <remarks>
    /// Zero-Speed policy is provisional: positive Speed qualifies against zero; two zeros tie.
    /// This only checks Speed. Attack permissions, such as no follow-ups for AoE, and the
    /// limit of one follow-up per exchange are checked by the exchange resolver.
    /// </remarks>
    public static FollowUpParticipant Evaluate(int attackerSpeed, int defenderSpeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attackerSpeed);
        ArgumentOutOfRangeException.ThrowIfNegative(defenderSpeed);

        // Multiply as long before comparing so large speeds cannot overflow an int.
        // Checking which side is faster first also keeps equal speeds, including zero, neutral.
        if (attackerSpeed > defenderSpeed && (long)attackerSpeed * 4 >= (long)defenderSpeed * 5)
            return FollowUpParticipant.Attacker;
        if (defenderSpeed > attackerSpeed && (long)defenderSpeed * 4 >= (long)attackerSpeed * 5)
            return FollowUpParticipant.Defender;

        return FollowUpParticipant.None;
    }
}
