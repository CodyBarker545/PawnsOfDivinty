using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Match-owned Sentinel preparations and movement evidence. Stores no alternate tactical positions.</summary>
public sealed class ImpalingSentinelTracker
{
    private readonly Dictionary<string, Character> _roster;
    private readonly Dictionary<string, ImpalingSentinelPreparation> _preparations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SentinelMovementEvent> _events = new(StringComparer.Ordinal);
    private readonly HashSet<(string PreparationId, string MovementId)> _reactionEvents = new();
    internal ActivationManager Activations { get; }
    internal RoundLifecycleManager Lifecycle { get; }
    public IReadOnlyList<ImpalingSentinelPreparation> Preparations
        => Array.AsReadOnly(_preparations.Values.Select(CurrentSnapshot).ToArray());
    public IReadOnlyList<SentinelMovementEvent> MovementEvents => Array.AsReadOnly(_events.Values.ToArray());

    public ImpalingSentinelTracker(ActivationManager activations, RoundLifecycleManager lifecycle)
    {
        ArgumentNullException.ThrowIfNull(activations);
        ArgumentNullException.ThrowIfNull(lifecycle);
        if (!activations.UsesLifecycle(lifecycle))
            throw new ArgumentException("Sentinel and activations must share the same lifecycle.", nameof(lifecycle));
        Activations = activations;
        Lifecycle = lifecycle;
        _roster = activations.FirstTeam.Concat(activations.SecondTeam).ToDictionary(c => c.Id, StringComparer.Ordinal);
    }

    public ImpalingSentinelPreparation? GetPreparation(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var preparation = _preparations.Values.LastOrDefault(p => p.OwnerCharacterId == ownerId);
        return preparation is null ? null : CurrentSnapshot(preparation);
    }

    public ImpalingSentinelReason ValidatePreparation(ImpalingSentinelPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (!_roster.TryGetValue(preparation.OwnerCharacterId, out var owner)) return ImpalingSentinelReason.ContextMismatch;
        if (owner.TeamId != preparation.OwnerTeamId) return ImpalingSentinelReason.TeamMismatch;
        if (owner.Definition?.Id != "gravebound_colossus") return ImpalingSentinelReason.NotColossus;
        if (!owner.Definition.Abilities.Any(a => a.Id == preparation.AbilityId)) return ImpalingSentinelReason.AbilityNotOwned;
        if (owner.IsDefeated) return ImpalingSentinelReason.OwnerDefeated;
        if (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) return ImpalingSentinelReason.WrongPhase;
        if (preparation.PreparationRound != Lifecycle.CurrentRound) return ImpalingSentinelReason.WrongRound;
        if (!preparation.ReactionLimit.HasValue) return ImpalingSentinelReason.ReactionLimitUnresolved;
        if (preparation.Status != SentinelPreparationStatus.Prepared) return ImpalingSentinelReason.PreparationExpired;
        if (_preparations.ContainsKey(preparation.Id)) return ImpalingSentinelReason.DuplicatePreparationId;
        if (GetPreparation(owner.Id) is { } previous && previous.PreparationRound == Lifecycle.CurrentRound)
            return ImpalingSentinelReason.AlreadyPrepared;
        if (owner.TacticalPosition is null) return ImpalingSentinelReason.MissingPosition;
        if (!DisarmEligibility.CanUseEquippedWeapon(owner)) return ImpalingSentinelReason.WeaponUnavailable;
        // The specification calls out range-2-only Lance geometry as unresolved.
        if (preparation.Lance.MinimumRange != 1) return ImpalingSentinelReason.RangeGeometryUnresolved;
        return ImpalingSentinelReason.None;
    }

    /// <summary>Low-level validated storage. Gameplay preparation uses the executor to pay its action and cooldown.</summary>
    public ImpalingSentinelPreparation Register(ImpalingSentinelPreparation preparation)
    {
        var reason = ValidatePreparation(preparation);
        if (reason != ImpalingSentinelReason.None)
            throw new InvalidOperationException($"Cannot register Sentinel preparation: {reason}.");
        _preparations.Add(preparation.Id, preparation);
        return preparation;
    }

    /// <summary>
    /// Accepts an external movement coordinator's committed evidence once. Registration moves nobody
    /// and resolves no reaction. Repeated previews may inspect that same event without consuming it.
    /// IDs stay reserved; a second publication under the same ID is rejected, even with identical data.
    /// </summary>
    public void RegisterMovementEvent(SentinelMovementEvent movement)
    {
        ArgumentNullException.ThrowIfNull(movement);
        if (!movement.IsCommitted) throw new ArgumentException("Publish movement evidence only after the step commits.", nameof(movement));
        if (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations)
            throw new InvalidOperationException("Ordinary movement evidence belongs to the activation phase.");
        if (_events.ContainsKey(movement.Id)) throw new ArgumentException("The movement event ID was already published.", nameof(movement));
        if (movement.RoundNumber != Lifecycle.CurrentRound) throw new ArgumentException("The movement event is not current.", nameof(movement));
        if (!_roster.TryGetValue(movement.CharacterId, out var mover) || mover.TeamId != movement.TeamId)
            throw new ArgumentException("The moving character must belong to this match and team.", nameof(movement));
        _events.Add(movement.Id, movement);
    }

    /// <summary>Call during death handling or Cleanup to retire inactive preparations. Repeated calls are harmless.</summary>
    public IReadOnlyList<ImpalingSentinelPreparation> RetireInactivePreparations()
    {
        var retired = _preparations.Values.Select(CurrentSnapshot)
            .Where(p => p.Status != SentinelPreparationStatus.Prepared && !ReferenceEquals(p, _preparations[p.Id])).ToArray();
        foreach (var preparation in retired) _preparations[preparation.Id] = preparation;
        return Array.AsReadOnly(retired);
    }

    internal Character? FindCharacter(string id) => _roster.GetValueOrDefault(id);
    internal bool HasConflictingEvent(SentinelMovementEvent movement)
        => _events.TryGetValue(movement.Id, out var registered) && !ReferenceEquals(registered, movement);
    internal bool ReactionWasStarted(string preparationId, string movementId)
        => _reactionEvents.Contains((preparationId, movementId));

    // Record before combat starts, so an exception from a custom random provider cannot allow
    // replay of an exchange that might already have applied damage. This is not rollback.
    internal void RecordReactionStarted(ImpalingSentinelPreparation preparation, SentinelMovementEvent movement)
    {
        var current = GetPreparation(preparation.OwnerCharacterId);
        if (current?.Id != preparation.Id || current.Status != SentinelPreparationStatus.Prepared
            || HasConflictingEvent(movement) || ReactionWasStarted(preparation.Id, movement.Id))
            throw new InvalidOperationException("Sentinel reaction context changed before resolution.");
        _events.TryAdd(movement.Id, movement);
        _reactionEvents.Add((preparation.Id, movement.Id));
        _preparations[preparation.Id] = current.ReactionStarted();
    }

    private ImpalingSentinelPreparation CurrentSnapshot(ImpalingSentinelPreparation preparation)
    {
        if (preparation.Status is SentinelPreparationStatus.OwnerDefeated or SentinelPreparationStatus.Expired) return preparation;
        if (_roster[preparation.OwnerCharacterId].IsDefeated) return preparation.WithStatus(SentinelPreparationStatus.OwnerDefeated);
        if (Lifecycle.CurrentRound > preparation.ExpirationRound ||
            (Lifecycle.CurrentRound == preparation.ExpirationRound && Lifecycle.CurrentPhase >= RoundPhase.Cleanup))
            return preparation.WithStatus(SentinelPreparationStatus.Expired);
        return preparation;
    }
}
