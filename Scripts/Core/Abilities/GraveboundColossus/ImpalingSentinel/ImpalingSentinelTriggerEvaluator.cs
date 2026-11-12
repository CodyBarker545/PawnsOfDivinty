using System;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Abilities;

public interface ISentinelReactionMap
{
    bool? IsValidPosition(GridPosition position);
    /// <summary>
    /// Current attack legality, including required terrain, visibility and movement-event context.
    /// Return null for missing/unresolved rules, false for a confirmed blocker. Queries must be read-only.
    /// </summary>
    bool? CanReact(Character owner, Character mover, SentinelMovementEvent movement);
}

public sealed record ImpalingSentinelTriggerResult
{
    public string OwnerCharacterId { get; }
    public string AbilityId => Characters.Definitions.GraveboundColossus.ImpalingSentinelId;
    public string MovementEventId { get; }
    public string TargetId { get; }
    public ImpalingSentinelPreparation? Preparation { get; }
    public ImpalingSentinelReason RejectionReason { get; }
    public bool IsEligible => RejectionReason == ImpalingSentinelReason.None;
    /// <summary>A legal proposal can be checked again after the movement coordinator commits its step.</summary>
    public bool MayTriggerAfterCommit => RejectionReason == ImpalingSentinelReason.MovementNotCommitted;
    public long? DistanceBefore { get; }
    public long? DistanceAfter { get; }

    internal ImpalingSentinelTriggerResult(string ownerId, SentinelMovementEvent movement,
        ImpalingSentinelPreparation? preparation, ImpalingSentinelReason reason, long? before = null, long? after = null)
    {
        OwnerCharacterId = ownerId;
        MovementEventId = movement.Id;
        TargetId = movement.CharacterId;
        Preparation = preparation;
        RejectionReason = reason;
        DistanceBefore = before;
        DistanceAfter = after;
    }
}

/// <summary>Recognizes a confirmed entry into Lance range without moving, attacking or consuming preparation.</summary>
public sealed class ImpalingSentinelTriggerEvaluator
{
    private readonly ImpalingSentinelTracker _tracker;
    public ImpalingSentinelTriggerEvaluator(ImpalingSentinelTracker tracker)
        => _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));

    public ImpalingSentinelTriggerResult Evaluate(string ownerId, SentinelMovementEvent movement,
        AttackConfiguration? currentLance, ISentinelReactionMap? map)
        => EvaluateStep(ownerId, movement, currentLance, map, null);

    // A path preview supplies its projected step origin without changing authoritative position.
    internal ImpalingSentinelTriggerResult EvaluateStep(string ownerId, SentinelMovementEvent movement,
        AttackConfiguration? currentLance, ISentinelReactionMap? map, GridPosition? projectedPosition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(movement);
        var preparation = _tracker.GetPreparation(ownerId);
        long? before = null, after = null;
        if (preparation is null) return Result(ImpalingSentinelReason.NotPrepared);
        if (_tracker.ReactionWasStarted(preparation.Id, movement.Id)) return Result(ImpalingSentinelReason.DuplicateReactionEvent);
        if (preparation.Status == SentinelPreparationStatus.OwnerDefeated) return Result(ImpalingSentinelReason.OwnerDefeated);
        if (preparation.Status == SentinelPreparationStatus.Expired) return Result(ImpalingSentinelReason.PreparationExpired);
        if (preparation.Status == SentinelPreparationStatus.Exhausted) return Result(ImpalingSentinelReason.ReactionLimitReached);
        if (_tracker.Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) return Result(ImpalingSentinelReason.WrongPhase);
        if (movement.RoundNumber != _tracker.Lifecycle.CurrentRound) return Result(ImpalingSentinelReason.WrongRound);
        if (_tracker.HasConflictingEvent(movement)) return Result(ImpalingSentinelReason.EventIdentityConflict);
        var owner = _tracker.FindCharacter(ownerId)!;
        var mover = _tracker.FindCharacter(movement.CharacterId);
        if (mover is null) return Result(ImpalingSentinelReason.MoverNotInRoster);
        if (mover.TeamId != movement.TeamId) return Result(ImpalingSentinelReason.TeamMismatch);
        if (mover.TeamId == owner.TeamId) return Result(ImpalingSentinelReason.AlliedMovement);
        if (mover.IsDefeated) return Result(ImpalingSentinelReason.TargetDefeated);
        if (movement.From == movement.Destination) return Result(ImpalingSentinelReason.Stationary);
        if (movement.Kind != SentinelMovementKind.Ordinary) return Result(ImpalingSentinelReason.SpecialMovementUnresolved);
        if ((projectedPosition ?? mover.TacticalPosition) != (movement.IsCommitted ? movement.Destination : movement.From))
            return Result(ImpalingSentinelReason.StalePosition);
        // A start/end pair cannot reveal a longer path's first range entry. Require an explicit
        // adjacent step rather than treating every traversed tile or a whole path as one trigger.
        if (movement.From.ManhattanDistanceTo(movement.Destination) != 1) return Result(ImpalingSentinelReason.MovementPathRequired);
        if (owner.TacticalPosition is not { } position) return Result(ImpalingSentinelReason.MissingPosition);
        if (!DisarmEligibility.CanUseEquippedWeapon(owner)) return Result(ImpalingSentinelReason.WeaponUnavailable);
        var spellTag = owner.Definition!.Abilities.Single(a => a.Id == preparation.AbilityId).IsSpellTagged;
        if (owner.Statuses.HasStatus(StatusType.Silence))
        {
            if (!spellTag.HasValue) return Result(ImpalingSentinelReason.SpellRestrictionUnresolved);
            if (!SilenceEligibility.CanUseAbility(owner, spellTag.Value)) return Result(ImpalingSentinelReason.BlockedBySilence);
        }
        if (currentLance is null) return Result(ImpalingSentinelReason.LanceRequired);
        if (currentLance != preparation.Lance) return Result(ImpalingSentinelReason.ContextMismatch);
        if (!preparation.ReactionLimit.HasValue) return Result(ImpalingSentinelReason.ReactionLimitUnresolved);
        before = position.ManhattanDistanceTo(movement.From);
        after = position.ManhattanDistanceTo(movement.Destination);
        bool InRange(long distance) => distance >= currentLance.MinimumRange && distance <= currentLance.MaximumRange;
        if (InRange(before.Value) || !InRange(after.Value)) return Result(ImpalingSentinelReason.NotEnteringRange);
        if (map is null) return Result(ImpalingSentinelReason.MissingMapInformation);
        foreach (var tile in new[] { position, movement.From, movement.Destination })
        {
            bool? valid = map.IsValidPosition(tile);
            if (!valid.HasValue) return Result(ImpalingSentinelReason.MissingMapInformation);
            if (!valid.Value) return Result(ImpalingSentinelReason.InvalidPosition);
        }
        bool? legal = map.CanReact(owner, mover, movement);
        if (!legal.HasValue) return Result(ImpalingSentinelReason.MissingMapInformation);
        if (!legal.Value) return Result(ImpalingSentinelReason.AttackBlocked);
        return Result(movement.IsCommitted ? ImpalingSentinelReason.None : ImpalingSentinelReason.MovementNotCommitted);

        ImpalingSentinelTriggerResult Result(ImpalingSentinelReason reason)
            => new(ownerId, movement, preparation, reason, before, after);
    }
}
