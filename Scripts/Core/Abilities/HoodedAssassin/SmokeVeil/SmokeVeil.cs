using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Terrain;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>The confirmed +20 percentage-point evasion while in smoke, even when revealed (spec v0.2, section 12.1).</summary>
public static class SmokeVeil
{
    public static int GetEvasionModifier(Character character, GridPosition position, SmokePlacementService smoke)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(smoke);
        return SmokeVisibility.IsEligibleAssassin(character) && smoke.IsCovered(position) ? 20 : 0;
    }

    /// <summary>Uses the existing accuracy calculator; this does not roll a strike or apply damage.</summary>
    public static int CalculateHitChance(int baseAccuracy, WeaponCategory attackerWeapon, WeaponCategory defenderWeapon,
        Character defender, GridPosition defenderPosition, SmokePlacementService smoke)
        => AccuracyCalculator.CalculateHitChance(baseAccuracy, attackerWeapon, defenderWeapon,
            GetEvasionModifier(defender, defenderPosition, smoke));
}
