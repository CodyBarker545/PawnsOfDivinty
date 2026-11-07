namespace PawnsOfDivinity.Core.Turns;

/// <summary>The round sequence in specification v0.2, section 7.</summary>
public enum RoundPhase
{
    Shop = 0,
    PoisonBatch = 1,
    DelayedAttacks = 2,
    TimedStatusUpdate = 3,
    OtherRoundStartTriggers = 4,
    Preparation = 5,
    AlternatingActivations = 6,
    Cleanup = 7,
    ObjectiveScoring = 8
}
