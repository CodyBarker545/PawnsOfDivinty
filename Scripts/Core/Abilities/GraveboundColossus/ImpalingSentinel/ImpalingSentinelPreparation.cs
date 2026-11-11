using System;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>An explicit prototype choice, not a finalized reaction limit.</summary>
public enum SentinelReactionLimit { ProvisionalOnePerPreparation }
public enum SentinelPreparationStatus { Prepared, Expired, OwnerDefeated, Exhausted }

public enum ImpalingSentinelReason
{
    None, ContextMismatch, NotColossus, AbilityNotOwned, OwnerDefeated, WrongPhase, WrongRound,
    LanceRequired, WeaponUnavailable, MissingPosition, RangeGeometryUnresolved, ReactionLimitUnresolved,
    DuplicatePreparationId, AlreadyPrepared, NotPrepared, PreparationExpired, Stationary, AlliedMovement,
    MoverNotInRoster, TargetDefeated, EventIdentityConflict, TeamMismatch, StalePosition,
    MovementNotCommitted, SpecialMovementUnresolved, MovementPathRequired, NotEnteringRange,
    MissingMapInformation, InvalidPosition, AttackBlocked,
    UnsupportedMetadata, SpellRestrictionUnresolved, ReactionLimitReached, DuplicateReactionEvent,
    CombatInformationRequired, MultipleReactionsOrderingUnresolved, BlockedBySilence
}

/// <summary>A finite preparation snapshot. Current validity is queried through its match tracker.</summary>
public sealed record ImpalingSentinelPreparation
{
    public string Id { get; }
    public string OwnerCharacterId { get; }
    public int OwnerTeamId { get; }
    public string AbilityId => GraveboundColossus.ImpalingSentinelId;
    public int PreparationRound { get; }
    /// <summary>Remainder of the preparation round only; inactive by Cleanup.</summary>
    public int ExpirationRound => PreparationRound;
    public AttackConfiguration Lance { get; }
    public SentinelReactionLimit? ReactionLimit { get; }
    public int ReactionCount { get; }
    public SentinelPreparationStatus Status { get; }

    public ImpalingSentinelPreparation(string id, string ownerCharacterId, int ownerTeamId, int preparationRound,
        AttackConfiguration lance, SentinelReactionLimit? reactionLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerCharacterId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(preparationRound);
        ArgumentNullException.ThrowIfNull(lance);
        if (lance.Type != AttackType.Physical || lance.Category != WeaponCategory.Lance)
            throw new ArgumentException("Sentinel requires a physical Lance configuration.", nameof(lance));
        if (reactionLimit.HasValue && !Enum.IsDefined(reactionLimit.Value))
            throw new ArgumentOutOfRangeException(nameof(reactionLimit));
        Id = id;
        OwnerCharacterId = ownerCharacterId;
        OwnerTeamId = ownerTeamId;
        PreparationRound = preparationRound;
        Lance = lance;
        ReactionLimit = reactionLimit;
        Status = SentinelPreparationStatus.Prepared;
    }

    private ImpalingSentinelPreparation(ImpalingSentinelPreparation preparation, SentinelPreparationStatus status, int reactionCount)
        : this(preparation.Id, preparation.OwnerCharacterId, preparation.OwnerTeamId, preparation.PreparationRound,
            preparation.Lance, preparation.ReactionLimit)
    {
        Status = status;
        ReactionCount = reactionCount;
    }

    internal ImpalingSentinelPreparation WithStatus(SentinelPreparationStatus status)
        => status == Status ? this : new(this, status, ReactionCount);

    internal ImpalingSentinelPreparation ReactionStarted()
        => new(this, SentinelPreparationStatus.Exhausted, checked(ReactionCount + 1));
}
