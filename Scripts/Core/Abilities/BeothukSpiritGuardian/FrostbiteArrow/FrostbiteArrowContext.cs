using System;

namespace PawnsOfDivinity.Core.Abilities;

public enum FrostbiteArrowDurationPolicy { ProvisionalTwoRounds }

/// <summary>Explicit caller knowledge; no flying classification, terrain provenance or duration is guessed.</summary>
public sealed record FrostbiteArrowContext
{
    public FrostbiteArrowDurationPolicy? DurationPolicy { get; }
    public bool? TargetIsFlying { get; }
    public bool? TargetHasTerrainFrostbite { get; }
    public FrostbiteArrowContext(FrostbiteArrowDurationPolicy? durationPolicy, bool? targetIsFlying, bool? targetHasTerrainFrostbite)
    {
        if (durationPolicy.HasValue && !Enum.IsDefined(durationPolicy.Value)) throw new ArgumentOutOfRangeException(nameof(durationPolicy));
        DurationPolicy = durationPolicy;
        TargetIsFlying = targetIsFlying;
        TargetHasTerrainFrostbite = targetHasTerrainFrostbite;
    }
}

public enum FrostbiteArrowReason
{
    None, NoConfirmedStrike, Miss, NoProc, NotSpiritGuardian, AbilityNotOwned, AttackerDefeated, TargetDefeated,
    TargetNotEnemy, ShieldEligibilityUnresolved, DurationUnresolved, TerrainContextRequired, TerrainInteractionUnresolved,
    AlreadyApplied, InvalidProcContext, WrongRound, BowRequired, PhysicalBowRequired, BowUnavailable, InvalidBowRange,
    AttackIntentRequired, ContextMismatch, InvalidTarget, UnsupportedMetadata, FlyingContextRequired,
    FlyingEffectivenessUnresolved, ExchangeTimingUnresolved, LethalProcRulesUnresolved, SanctuaryUnresolved, AlreadyStarted
}
