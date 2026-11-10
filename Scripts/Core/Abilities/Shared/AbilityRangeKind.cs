namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Distinguishes numeric Manhattan range metadata from equipment-dependent range.</summary>
public enum AbilityRangeKind
{
    ManhattanDistance = 0,
    ManhattanRadius = 1,
    EquippedWeapon = 2,
    UnspecifiedRadius = 3
}
