using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity;

/// <summary>Coordinates only the existing, independent Cleanup expirations for one match.</summary>
/// <remarks>
/// The lifecycle owns this single registered coordinator. Match services must be supplied exclusively
/// for that match; there is no static registry or second round counter. Processing is synchronous,
/// with no concurrent match mutations. Public direct expiration APIs remain caller responsibilities.
/// The technical traversal order is not a priority rule for future interacting end-of-round triggers.
/// </remarks>
public sealed class RoundCleanupCoordinator
{
    private readonly RoundLifecycleManager _lifecycle;
    private readonly ActivationManager _activations;
    private readonly Character[] _roster;
    private readonly WailingRampartService _walls;
    private readonly SpiritFrostService _ice;
    private readonly SmokePlacementService _smoke;
    private readonly ImpalingSentinelTracker _sentinel;
    private RoundCleanupResult? _lastAttempt;
    private bool _processing;

    public RoundCleanupCoordinator(RoundLifecycleManager lifecycle, ActivationManager activations,
        WailingRampartService walls, SpiritFrostService ice, SmokePlacementService smoke, ImpalingSentinelTracker sentinel)
    {
        ArgumentNullException.ThrowIfNull(lifecycle); ArgumentNullException.ThrowIfNull(activations);
        ArgumentNullException.ThrowIfNull(walls); ArgumentNullException.ThrowIfNull(ice);
        ArgumentNullException.ThrowIfNull(smoke); ArgumentNullException.ThrowIfNull(sentinel);
        if (!activations.UsesLifecycle(lifecycle) || !ReferenceEquals(sentinel.Lifecycle, lifecycle)
            || !ReferenceEquals(sentinel.Activations, activations))
            throw new ArgumentException("Cleanup, activation management and Sentinel must belong to the same lifecycle and roster.");
        _roster = activations.FirstTeam.Concat(activations.SecondTeam).ToArray();
        _lifecycle = lifecycle; _activations = activations; _walls = walls; _ice = ice; _smoke = smoke; _sentinel = sentinel;
        // Invalid dependencies never reserve registration. A second coordinator cannot replace this boundary or its guard.
        lifecycle.RegisterCleanupCoordinator(this);
    }

    /// <summary>Processes current-round Cleanup without advancing the lifecycle or retrying an earlier attempt.</summary>
    public RoundCleanupResult Process(int requestedRound)
    {
        RoundCleanupResult Result(RoundCleanupStatus status, string reason = "", IEnumerable<CleanupServiceResult>? steps = null,
            RoundCleanupStatus? previous = null) => new(requestedRound, _lifecycle.CurrentRound, _lifecycle.CurrentPhase, status, reason, steps, previous,
                previous.HasValue ? _lastAttempt?.RequestedRound : null);
        if (requestedRound <= 0 || requestedRound != _lifecycle.CurrentRound)
            return Result(RoundCleanupStatus.RejectedInvalidRound, "Request the positive current lifecycle round.");
        if (_lifecycle.CurrentPhase != RoundPhase.Cleanup)
            return Result(RoundCleanupStatus.RejectedWrongPhase, "Cleanup runs after activations and before objective scoring.");
        if (_processing)
            return Result(RoundCleanupStatus.ManualResolutionRequired, "Cleanup is already being processed; reentrant execution is not supported.");
        if (_lastAttempt is { Status: not RoundCleanupStatus.Completed })
            return Result(RoundCleanupStatus.ManualResolutionRequired,
                "A previous Cleanup attempt failed or was incomplete. Inspect its results; automatic continuation is forbidden.",
                _lastAttempt.Services, _lastAttempt.Status);
        if (_lastAttempt?.RequestedRound == requestedRound)
            return Result(RoundCleanupStatus.AlreadyProcessed, "This round's Cleanup has completed. No services were invoked again.",
                _lastAttempt.Services, _lastAttempt.Status);
        if (!_activations.IsRoundComplete)
            return Result(RoundCleanupStatus.RejectedActivationsIncomplete, "Complete all remaining activations before Cleanup.");

        var operations = new List<(RoundCleanupService Service, string? CharacterId, Func<IEnumerable<CleanupEffect>> Execute)>();
        foreach (var character in _roster)
            operations.Add((RoundCleanupService.TemporaryModifiers, character.Id,
                () => character.TemporaryModifiers.ExpireAtRoundEnd(requestedRound, RoundPhase.Cleanup)
                    .Select(m => new CleanupEffect(m.Id, m.SourceCharacterId, m.RecipientCharacterId))));
        operations.Add((RoundCleanupService.WailingRampart, null, () => _walls.ExpireAtRoundEnd(requestedRound, RoundPhase.Cleanup).Select(a => new CleanupEffect(a.Id, a.OwnerCharacterId))));
        operations.Add((RoundCleanupService.SpiritFrost, null, () => _ice.ExpireAtRoundEnd(requestedRound, RoundPhase.Cleanup).Select(a => new CleanupEffect(a.Id, a.OwnerCharacterId))));
        operations.Add((RoundCleanupService.Smoke, null, () => _smoke.ExpireAtRoundEnd(requestedRound, RoundPhase.Cleanup).ExpiredAreas.Select(a => new CleanupEffect(a.Id, a.OwnerCharacterId))));
        operations.Add((RoundCleanupService.Taunts, null, () => _activations.ProcessTauntCleanup().Select(t => new CleanupEffect(t.RestrictionId, t.SourceCharacterId, t.RestrictedCharacterId))));
        operations.Add((RoundCleanupService.Sentinel, null, () => _sentinel.RetireInactivePreparations().Select(p => new CleanupEffect(p.Id, p.OwnerCharacterId, SentinelStatus: p.Status))));

        var completed = new List<CleanupServiceResult>();
        // Reserve before the first service call, including services that return an empty list but update internal bookkeeping.
        _lastAttempt = Result(RoundCleanupStatus.Failed, "Cleanup was reserved but has not completed.");
        _processing = true;
        try
        {
            for (int index = 0; index < operations.Count; index++)
            {
                var operation = operations[index];
                try
                {
                    var effects = operation.Execute().ToArray();
                    completed.Add(new(operation.Service, operation.CharacterId, CleanupServiceStatus.Completed, effects));
                }
                catch (Exception error)
                {
                    bool earlierStepsCompleted = completed.Count != 0;
                    completed.Add(new(operation.Service, operation.CharacterId, CleanupServiceStatus.Failed,
                        failureType: error.GetType().Name, failureMessage: error.Message));
                    foreach (var pending in operations.Skip(index + 1))
                        completed.Add(new(pending.Service, pending.CharacterId, CleanupServiceStatus.NotStarted));
                    return _lastAttempt = Result(earlierStepsCompleted ? RoundCleanupStatus.PartiallyApplied : RoundCleanupStatus.Failed,
                        $"{operation.Service} failed. No rollback or retry was performed; manual resolution is required.", completed);
                }
            }
            return _lastAttempt = Result(RoundCleanupStatus.Completed, steps: completed);
        }
        finally { _processing = false; }
    }
}
