namespace PawnsOfDivinity.Core.Abilities;

public enum SmokeTeleportRejectionReason
{
    None,
    CasterDefeated,
    NotAssassin,
    MissingSourcePosition,
    MissingDestinationPosition,
    MissingMapInformation,
    InvalidSourcePosition,
    InvalidDestinationPosition,
    IllegalDestination,
    DestinationOccupied,
    SourceOutsideSmoke,
    DestinationOutsideSmoke,
    UnresolvedCrossAreaRule,
    DifferentSmokeAreasNotAllowed,
    UnresolvedSamePositionRule,
    SamePositionNotAllowed,
    OutsideMaximumRange,
    UnresolvedDistanceLimit,
    UnresolvedOwnerRule,
    OtherOwnersNotAllowed,
    InvalidValidationContext,
    SourcePositionChanged
}
