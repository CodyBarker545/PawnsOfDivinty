using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity;

/// <summary>Validates supplied routes and commits ordinary steps to Character.TacticalPosition.</summary>
public sealed class MovementActionExecutor
{
    private readonly ActivationManager _activations;
    private readonly RoundLifecycleManager _lifecycle;
    private readonly IOrdinaryMovementMap _map;
    private readonly WailingRampartService _walls;
    private readonly SpiritFrostService _ice;
    private readonly ImpalingSentinelTracker _sentinel;
    private readonly ImpalingSentinelReactionResolver _reactions;
    private readonly IMovementReactionInputs _inputs;
    private readonly Dictionary<string, Character> _roster;
    private readonly Dictionary<string, MovementOutcome> _attempts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MovementActionRequest> _requests = new(StringComparer.Ordinal);
    private bool _executing;

    public MovementActionExecutor(ActivationManager activations, RoundLifecycleManager lifecycle,
        IOrdinaryMovementMap map, WailingRampartService walls, SpiritFrostService ice,
        ImpalingSentinelTracker sentinel, CombatExchangeResolver combat, IMovementReactionInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(activations); ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(walls);
        ArgumentNullException.ThrowIfNull(ice); ArgumentNullException.ThrowIfNull(sentinel);
        ArgumentNullException.ThrowIfNull(combat); ArgumentNullException.ThrowIfNull(inputs);
        if (!activations.UsesLifecycle(lifecycle) || !ReferenceEquals(sentinel.Activations, activations)
            || !ReferenceEquals(sentinel.Lifecycle, lifecycle)) throw new ArgumentException("Movement services must share one match.");
        if (!Enum.IsDefined(map.RuleAuthority) || string.IsNullOrWhiteSpace(map.PolicyDescription))
            throw new ArgumentException("Movement policy must identify its authority and scope.", nameof(map));
        _activations = activations; _lifecycle = lifecycle; _map = map; _walls = walls; _ice = ice;
        _sentinel = sentinel; _reactions = new(sentinel, combat); _inputs = inputs;
        _roster = activations.FirstTeam.Concat(activations.SecondTeam).ToDictionary(c => c.Id, StringComparer.Ordinal);
        lifecycle.RegisterMovementExecutor(this);
    }

    public MovementActionResult Preview(MovementActionRequest request) => Run(request, false);
    public MovementActionResult Execute(MovementActionRequest request) => Run(request, true);

    private MovementActionResult Run(MovementActionRequest request, bool execute)
    {
        ArgumentNullException.ThrowIfNull(request);
        _roster.TryGetValue(request.ActorId, out var actor);
        var start = actor?.TacticalPosition;
        var traversed = new List<GridPosition>();
        var possible = new List<string>();
        var observations = new List<MovementReactionObservation>();
        long? approvedCost = null;
        long traversedCost = 0;
        bool consumed = false;
        MovementActionResult Result(MovementOutcome outcome, string reason, MovementOutcome? previous = null)
            => new(request, outcome, reason, start, actor, approvedCost.HasValue ? request.Path : Array.Empty<GridPosition>(),
                traversed, approvedCost, traversedCost, consumed, _map, possible, observations, previous);
        if (_attempts.TryGetValue(request.Id, out var prior))
        {
            var original = _requests[request.Id];
            if (original.ActorId != request.ActorId || original.Round != request.Round || !original.Path.SequenceEqual(request.Path))
                return Result(MovementOutcome.Rejected, "RequestIdentityConflict", prior);
            return Result(MovementOutcome.AlreadyProcessed, "RequestAlreadyAttempted", prior);
        }
        if (_executing) return Result(MovementOutcome.Rejected, "MovementReentry");
        _executing = true;
        bool reserved = false;
        try
        {
            if (actor is null) return Result(MovementOutcome.Rejected, "UnknownActor");
            if (actor.IsDefeated) return Result(MovementOutcome.Rejected, "ActorDefeated");
            if (request.Round != _lifecycle.CurrentRound) return Result(MovementOutcome.Rejected, "StaleRound");
            if (_lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) return Result(MovementOutcome.Rejected, "WrongPhase");
            if (!ReferenceEquals(_activations.ActiveCharacter, actor) || !actor.CanAct)
                return Result(MovementOutcome.Rejected, "CharacterNotActive");
            if (!_activations.CanMove) return Result(MovementOutcome.Rejected, "MovementUnavailable");
            if (start is null) return Result(MovementOutcome.UnresolvedRule, "MissingPosition");
            if (request.Path.Count < 2) return Result(MovementOutcome.Rejected, "PathRequiresOriginAndStep");
            if (request.Path[0] != start) return Result(MovementOutcome.Rejected, "StaleOrigin");
            var validOrigin = _map.IsValidPosition(start.Value);
            if (validOrigin != true) return Result(validOrigin is null ? MovementOutcome.UnresolvedRule : MovementOutcome.Rejected, "InvalidOrigin");

            var steps = new List<(int Cost, string? Owner)>();
            long total = 0;
            for (int i = 1; i < request.Path.Count; i++)
            {
                if (_sentinel.MovementEvents.Any(e => e.Id == Event(i).Id))
                    return Result(MovementOutcome.Rejected, "MovementEventAlreadyPublished");
                var failure = ValidateStep(actor, request.Path[i - 1], request.Path[i], i == request.Path.Count - 1, out int cost);
                if (failure is not null) return Result(failure.Value.Outcome, failure.Value.Reason);
                total = checked(total + cost);
                if (total > actor.EffectiveMovement) return Result(MovementOutcome.Rejected, "MovementAllowanceExceeded");
                var reaction = PlanReaction(actor, Event(i), request.Path[i]);
                if (reaction.Error is not null) return Result(MovementOutcome.UnresolvedRule, reaction.Error);
                if (reaction.Owner is not null)
                {
                    possible.Add(reaction.Owner);
                    if (_map.InterruptedMovementConsumesOpportunity != true)
                        return Result(MovementOutcome.UnresolvedRule, "InterruptedMovementConsumptionUnresolved");
                }
                steps.Add((cost, reaction.Owner));
            }
            approvedCost = total;
            if (!execute) return Result(MovementOutcome.Ready, "PathValidated");
            _attempts.Add(request.Id, MovementOutcome.PartiallyApplied);
            _requests.Add(request.Id, request);
            reserved = true; _executing = true;
            traversed.Add(start.Value);
            for (int i = 1; i < request.Path.Count; i++)
            {
                // A prior reaction may have changed the board. Never use the preview as permission.
                if (actor.TacticalPosition != request.Path[i - 1]) return Finish(MovementOutcome.PartiallyApplied, "PositionChangedDuringMovement");
                var failure = ValidateStep(actor, request.Path[i - 1], request.Path[i], i == request.Path.Count - 1, out int cost);
                if (failure is not null || cost != steps[i - 1].Cost)
                    return Finish(consumed ? MovementOutcome.PartiallyApplied : MovementOutcome.Rejected, failure?.Reason ?? "StepCostChanged");
                var plan = PlanReaction(actor, Event(i), request.Path[i]);
                if (plan.Error is not null) return Finish(consumed ? MovementOutcome.PartiallyApplied : MovementOutcome.UnresolvedRule, plan.Error);
                if (plan.Owner is not null && _map.InterruptedMovementConsumesOpportunity != true)
                    return Finish(consumed ? MovementOutcome.PartiallyApplied : MovementOutcome.UnresolvedRule, "InterruptedMovementConsumptionUnresolved");
                if (!consumed) { _activations.RecordMovement(); consumed = true; }
                actor.SetTacticalPosition(request.Path[i]);
                traversed.Add(request.Path[i]); traversedCost += cost;
                var movement = Event(i);
                _sentinel.RegisterMovementEvent(movement);
                if (plan.Owner is not null)
                {
                    int hp = actor.CurrentHealth, shield = actor.ShieldHP;
                    var source = _roster[plan.Owner];
                    int sourceHP = source.CurrentHealth, sourceShield = source.ShieldHP;
                    ImpalingSentinelReactionResult result;
                    try
                    {
                        result = _reactions.Execute(plan.Owner, movement, _inputs.CurrentLance(plan.Owner),
                            _inputs.CreateRequest(plan.Owner, actor.Id, plan.Distance), _map);
                    }
                    catch (Exception error)
                    {
                        observations.Add(new(movement.Id, i, plan.Owner, actor.Id, null, hp - actor.CurrentHealth,
                            shield - actor.ShieldHP, sourceHP - source.CurrentHealth, sourceShield - source.ShieldHP,
                            $"{error.GetType().Name}: {error.Message}"));
                        throw;
                    }
                    observations.Add(new(movement.Id, i, plan.Owner, actor.Id, result, hp - actor.CurrentHealth,
                        shield - actor.ShieldHP, sourceHP - source.CurrentHealth, sourceShield - source.ShieldHP));
                    if (!result.Resolved) return Finish(MovementOutcome.PartiallyApplied, result.RejectionReason.ToString());
                    if (actor.IsDefeated || result.MovementMayContinue == false)
                        return Finish(MovementOutcome.Interrupted, actor.IsDefeated ? "ActorDefeated" : "SentinelHit");
                }
            }
            return Finish(MovementOutcome.Completed, "MovementCompleted");

            SentinelMovementEvent Event(int i) => new($"movement:{request.Id.Length}:{request.Id}:{i}", actor.Id, actor.TeamId,
                request.Path[i - 1], request.Path[i], SentinelMovementKind.Ordinary, request.Round, true);
            MovementActionResult Finish(MovementOutcome outcome, string reason)
            { _attempts[request.Id] = outcome; return Result(outcome, reason); }
        }
        catch (Exception error)
        {
            var outcome = consumed ? MovementOutcome.PartiallyApplied
                : error is SanctuaryDefenseUnresolvedException ? MovementOutcome.UnresolvedRule
                : error is ArgumentException or OverflowException ? MovementOutcome.Rejected : MovementOutcome.Failed;
            if (reserved) _attempts[request.Id] = outcome;
            return Result(outcome, $"{error.GetType().Name}: {error.Message}");
        }
        finally { _executing = false; }
    }

    private (MovementOutcome Outcome, string Reason)? ValidateStep(Character actor, GridPosition from, GridPosition to, bool destination, out int cost)
    {
        cost = 0;
        if (from.ManhattanDistanceTo(to) != 1) return (MovementOutcome.Rejected, "NonOrthogonalStep");
        var valid = _map.IsValidPosition(to);
        if (valid != true) return (valid is null ? MovementOutcome.UnresolvedRule : MovementOutcome.Rejected, "InvalidTile");
        foreach (var other in _roster.Values.Where(c => c.Id != actor.Id && !c.IsDefeated && c.TacticalPosition == to))
        {
            if (destination) return (MovementOutcome.Rejected, "OccupiedDestination");
            var crossing = _map.CanTraverseOccupiedTile(actor, other);
            if (crossing != true) return (crossing is null ? MovementOutcome.UnresolvedRule : MovementOutcome.Rejected, "OccupiedTraversal");
            // A reaction or provider failure could stop on this tile. Shared occupancy at an
            // interrupted checkpoint has no approved rule, even if crossing itself was supplied.
            return (MovementOutcome.UnresolvedRule, "OccupiedCheckpointInterruptionUnresolved");
        }
        if (_walls.CanEnter(actor, to) != true) return (MovementOutcome.Rejected, "WallBlocksMovement");
        if (_ice.EvaluateMovement(actor, to).Outcome == SpiritFrostMovementOutcome.TerrainFrostbiteRulesUnresolved)
            return (MovementOutcome.UnresolvedRule, "TerrainFrostbiteRulesUnresolved");
        var value = _map.GetStepCost(actor, from, to);
        if (!value.HasValue) return (MovementOutcome.UnresolvedRule, "MovementCostUnresolved");
        if (value <= 0) return (MovementOutcome.UnresolvedRule, "InvalidStepCost");
        cost = value.Value;
        return null;
    }

    private (string? Owner, int Distance, string? Error) PlanReaction(Character actor, SentinelMovementEvent movement, GridPosition projected)
    {
        string? owner = null; int distance = 0;
        var evaluator = new ImpalingSentinelTriggerEvaluator(_sentinel);
        foreach (var preparation in _sentinel.Preparations)
        {
            var lance = _inputs.CurrentLance(preparation.OwnerCharacterId);
            var trigger = evaluator.EvaluateStep(preparation.OwnerCharacterId, movement, lance, _map, projected);
            if (trigger.RejectionReason is ImpalingSentinelReason.MissingMapInformation or ImpalingSentinelReason.SpellRestrictionUnresolved
                or ImpalingSentinelReason.MissingPosition or ImpalingSentinelReason.LanceRequired or ImpalingSentinelReason.ContextMismatch)
                return (null, 0, trigger.RejectionReason.ToString());
            if (!trigger.IsEligible) continue;
            if (owner is not null) return (null, 0, "MultipleReactionsOrderingUnresolved");
            owner = preparation.OwnerCharacterId; distance = checked((int)trigger.DistanceAfter!.Value);
            var preview = _reactions.PreviewStep(owner, movement, lance, _inputs.CreateRequest(owner, actor.Id, distance), _map, projected);
            if (!preview.CanExecute) return (null, 0, preview.RejectionReason.ToString());
        }
        return (owner, distance, null);
    }
}
