namespace PawnsOfDivinity.Core.Abilities;

public enum AbilityTargetingRejectionReason
{
    None = 0,
    OutsideMaximumRange = 1,
    InsideMinimumRange = 2,
    InvalidTargetType = 3,
    InvalidSelfTarget = 4,
    WrongTeam = 5,
    TargetDefeated = 6,
    MissingRequiredTargetInformation = 7,
    UnresolvedTargetingRule = 8,
    UnresolvedRange = 9,
    UnresolvedRadiusGeometry = 10,
    UnresolvedLineOfSight = 11,
    UnresolvedElevation = 12,
    MapTargetIllegal = 13
}
