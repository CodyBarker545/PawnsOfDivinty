using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity;

public enum MovementRuleAuthority { Confirmed, ProvisionalFixture }
public enum MovementOutcome { Ready, Completed, Rejected, UnresolvedRule, Interrupted, PartiallyApplied, AlreadyProcessed, Failed }

/// <summary>Read-only map queries and explicitly supplied rules. Null means that a rule is unavailable.</summary>
public interface IOrdinaryMovementMap : ISentinelReactionMap
{
    MovementRuleAuthority RuleAuthority { get; }
    string PolicyDescription { get; }
    bool? CanTraverseOccupiedTile(Character actor, Character occupant);
    /// <summary>
    /// Return a positive cost only for a transition supported by the identified policy,
    /// including its terrain/height constraints. Return null for unsupported transitions.
    /// Queries must be read-only and must not re-enter match execution.
    /// </summary>
    int? GetStepCost(Character actor, GridPosition from, GridPosition to);
    bool? InterruptedMovementConsumesOpportunity { get; }
}

/// <summary>The route includes its origin. It proposes coordinates, never an authoritative cost.</summary>
public sealed record MovementActionRequest
{
    public string Id { get; }
    public string ActorId { get; }
    public int Round { get; }
    public IReadOnlyList<GridPosition> Path { get; }
    public MovementActionRequest(string id, string actorId, int round, IEnumerable<GridPosition> path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        ArgumentNullException.ThrowIfNull(path);
        Id = id; ActorId = actorId; Round = round; Path = Array.AsReadOnly(path.ToArray());
    }
}

/// <summary>Resolved combat inputs, supplied without inventing equipment Power or accuracy.</summary>
public interface IMovementReactionInputs
{
    AttackConfiguration? CurrentLance(string ownerId);
    CombatExchangeRequest? CreateRequest(string ownerId, string moverId, int distance);
}

public sealed record MovementReactionObservation(string EventId, int StepIndex, string SourceId, string TargetId,
    ImpalingSentinelReactionResult? Result, int TargetHPLost, int TargetShieldLost,
    int SourceHPLost, int SourceShieldLost, string? Failure = null);

public sealed record MovementActionResult
{
    public string RequestId { get; }
    public string ActorId { get; }
    public MovementOutcome Outcome { get; }
    public string Reason { get; }
    public GridPosition? StartingPosition { get; }
    public GridPosition? RequestedDestination { get; }
    public GridPosition? FinalPosition { get; }
    public IReadOnlyList<GridPosition> ApprovedPath { get; }
    public IReadOnlyList<GridPosition> TraversedPath { get; }
    public long? ApprovedCost { get; }
    public long TraversedCost { get; }
    public bool MovementConsumed { get; }
    public bool ActorSurvived { get; }
    public MovementRuleAuthority PolicyAuthority { get; }
    public string PolicyDescription { get; }
    public IReadOnlyList<string> PossibleReactionSources { get; }
    public IReadOnlyList<MovementReactionObservation> Reactions { get; }
    public MovementOutcome? PreviousOutcome { get; }
    internal MovementActionResult(MovementActionRequest request, MovementOutcome outcome, string reason,
        GridPosition? start, Character? actor, IEnumerable<GridPosition> approved, IEnumerable<GridPosition> traversed,
        long? cost, long traversedCost, bool consumed, IOrdinaryMovementMap map, IEnumerable<string> possible,
        IEnumerable<MovementReactionObservation> reactions, MovementOutcome? previous = null)
    {
        RequestId = request.Id; ActorId = request.ActorId; Outcome = outcome; Reason = reason;
        StartingPosition = start; RequestedDestination = request.Path.Count == 0 ? null : request.Path[^1];
        FinalPosition = actor?.TacticalPosition; ActorSurvived = actor is not null && !actor.IsDefeated;
        ApprovedPath = Array.AsReadOnly(approved.ToArray()); TraversedPath = Array.AsReadOnly(traversed.ToArray());
        ApprovedCost = cost; TraversedCost = traversedCost; MovementConsumed = consumed;
        PolicyAuthority = map.RuleAuthority; PolicyDescription = map.PolicyDescription;
        PossibleReactionSources = Array.AsReadOnly(possible.Distinct(StringComparer.Ordinal).ToArray());
        Reactions = Array.AsReadOnly(reactions.ToArray()); PreviousOutcome = previous;
    }
}
