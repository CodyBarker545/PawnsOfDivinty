using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Gameplay;

public sealed record BattlefieldSpawn(string InstanceId, string DefinitionId, int TeamId, GridPosition Position);
public enum BattlefieldOutcome { Ready, Completed, Rejected, Unresolved, PartiallyApplied, Failed, AlreadyProcessed }
public sealed record BattlefieldCommandResult(BattlefieldOutcome Outcome, string Reason);
public sealed record BattlefieldAttackResult(BattlefieldOutcome Outcome, string Reason, CombatExchangeResult? Combat = null);

/// <summary>Immutable presentation data, captured from the actual runtime character.</summary>
public sealed record BattlefieldUnitState(string Id, string DefinitionId, string Name, int TeamId,
    GridPosition? Position, int HP, int MaxHP, int ShieldHP, int Movement, bool Defeated,
    bool HasActed, bool CanActivate, IReadOnlyList<string> Statuses);

/// <summary>A stable identity binding. Position and HP are read from the session, never stored by a view.</summary>
public sealed class BattlefieldUnitBinding
{
    private readonly BattlefieldSession _session;
    public string CharacterId { get; }
    public BattlefieldUnitBinding(BattlefieldSession session, string characterId)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.Characters.ContainsKey(characterId)) throw new ArgumentException("Unknown character identity.", nameof(characterId));
        _session = session; CharacterId = characterId;
    }
    public BattlefieldUnitState Capture() => _session.Capture(CharacterId);
}

/// <summary>One local development encounter using the existing production gameplay services.</summary>
public sealed class BattlefieldSession : IMovementReactionInputs
{
    public BattlefieldDevelopmentRules Rules { get; }
    public IReadOnlyDictionary<string, Character> Characters { get; }
    public IReadOnlyDictionary<string, AttackConfiguration> DevelopmentAttacks { get; }
    public RoundLifecycleManager Lifecycle { get; } = new();
    public ActivationManager Activations { get; }
    public WailingRampartService Walls { get; } = new();
    public SpiritFrostService Ice { get; } = new();
    public SmokePlacementService Smoke { get; } = new();
    public DelayedAttackScheduler DelayedAttacks { get; } = new();
    public ImpalingSentinelTracker Sentinel { get; }
    public RoundCleanupCoordinator Cleanup { get; }
    public MovementActionExecutor Movement { get; }
    public BattlefieldMapAdapter Map { get; }
    public string? SelectedCharacterId { get; private set; }
    public bool RequiresManualRecovery { get; private set; }
    public IReadOnlyList<string> EventLog => Array.AsReadOnly(_log.ToArray());
    public IReadOnlyList<RoundPhase> ProcessedPhases => Array.AsReadOnly(_processed.ToArray());
    public RoundCleanupResult? LastCleanup { get; private set; }
    private readonly AttackActionExecutor _attacks;
    private readonly List<string> _log = new();
    private readonly List<RoundPhase> _processed = new();
    private readonly HashSet<string> _attackAttempts = new(StringComparer.Ordinal);
    private readonly HashSet<(int Round, RoundPhase Phase)> _phaseAttempts = new();

    public static IReadOnlyList<BattlefieldSpawn> DefaultEncounter { get; } = Array.AsReadOnly(new[]
    {
        new BattlefieldSpawn("a_assassin", "hooded_assassin", 1, new(4, 6)),
        new BattlefieldSpawn("a_guardian", "gentle_guardian", 1, new(3, 6)),
        new BattlefieldSpawn("a_spirit", "beothuk_spirit_guardian", 1, new(3, 7)),
        new BattlefieldSpawn("b_nigel", "nigel", 2, new(8, 7)),
        new BattlefieldSpawn("b_weaver", "time_weaver", 2, new(9, 7)),
        new BattlefieldSpawn("b_colossus", "gravebound_colossus", 2, new(8, 6))
    });

    public BattlefieldSession(IBattlefieldTerrain terrain, BattlefieldDevelopmentRules rules,
        IRandomRollProvider? random = null, IEnumerable<BattlefieldSpawn>? encounter = null)
    {
        ArgumentNullException.ThrowIfNull(terrain); ArgumentNullException.ThrowIfNull(rules); Rules = rules;
        var roster = new Dictionary<string, Character>(StringComparer.Ordinal);
        var occupied = new HashSet<GridPosition>();
        foreach (var spawn in encounter ?? DefaultEncounter)
        {
            if (spawn is null || !BattlefieldGrid.Contains(spawn.Position) || terrain.IsWalkable(spawn.Position) != true
                || !occupied.Add(spawn.Position)) throw new ArgumentException("Spawn cells must be known legal and distinct.", nameof(encounter));
            var definition = CharacterRegistry.GetCharacter(spawn.DefinitionId) ?? throw new ArgumentException("Unknown registered character.", nameof(encounter));
            var character = CharacterFactory.Create(definition, spawn.InstanceId, spawn.TeamId);
            character.SetTacticalPosition(spawn.Position);
            if (!roster.TryAdd(character.Id, character)) throw new ArgumentException("Duplicate runtime identity.", nameof(encounter));
        }
        if (roster.Values.Any(c => c.TeamId is not (1 or 2))) throw new ArgumentException("This local encounter uses teams 1 and 2.");
        Characters = new ReadOnlyDictionary<string, Character>(roster);
        DevelopmentAttacks = new ReadOnlyDictionary<string, AttackConfiguration>(roster.Values.ToDictionary(c => c.Id, FixtureAttack));
        Activations = new(roster.Values.Where(c => c.TeamId == 1), roster.Values.Where(c => c.TeamId == 2), rules.StartingTeamId, Lifecycle);
        Sentinel = new(Activations, Lifecycle); Cleanup = new(Lifecycle, Activations, Walls, Ice, Smoke, Sentinel);
        Map = new(this, terrain);
        var combat = new CombatExchangeResolver(new SingleStrikeResolver(new HitRoller(random ?? new SystemRandomRollProvider())));
        _attacks = new(combat); Movement = new(Activations, Lifecycle, Map, Walls, Ice, Sentinel, combat, this);
    }

    internal static AttackConfiguration FixtureAttack(Character character) => character.Definition!.Id switch
    {
        "hooded_assassin" => new(AttackType.Physical, WeaponCategory.Sword, 5, 90, 1, 1),
        "gentle_guardian" => new(AttackType.Magical, WeaponCategory.MagicScroll, 3, 90, 1, 2),
        "time_weaver" => new(AttackType.Magical, WeaponCategory.MagicScroll, 4, 90, 1, 2),
        "nigel" => new(AttackType.Physical, WeaponCategory.Axe, 4, 90, 1, 1),
        "gravebound_colossus" => new(AttackType.Physical, WeaponCategory.Lance, 3, 90, 1, 1),
        "beothuk_spirit_guardian" => new(AttackType.Physical, WeaponCategory.Bow, 4, 90, 2, 3),
        _ => throw new ArgumentException("No explicit development equipment configuration for this character.")
    };

    public BattlefieldUnitState Capture(string id)
    {
        var c = Characters[id];
        var statuses = c.Statuses.TimedStatuses.Select(s => $"{s.Key} ({s.Value})").ToList();
        if (c.Statuses.IsPoisoned) statuses.Add($"Poison (ticks {c.Statuses.PoisonTicksOccurred})");
        return new(c.Id, c.Definition!.Id, c.Name, c.TeamId, c.TacticalPosition, c.CurrentHealth, c.BaseStats.MaxHealth,
            c.ShieldHP, c.EffectiveMovement, c.IsDefeated, c.HasActedThisRound,
            !RequiresManualRecovery && Lifecycle.CurrentPhase == RoundPhase.AlternatingActivations && Activations.ActiveCharacter is null
                && c.CanAct && c.TeamId == Activations.NextTeamId, Array.AsReadOnly(statuses.ToArray()));
    }

    public BattlefieldCommandResult Select(string? id)
    {
        if (id is not null && !Characters.ContainsKey(id)) return new(BattlefieldOutcome.Rejected, "Unknown character.");
        SelectedCharacterId = id;
        return new(BattlefieldOutcome.Completed, id is null ? "Selection cleared." : $"Inspecting {Characters[id].Name}; selection spends no action.");
    }

    public BattlefieldCommandResult StartSelectedActivation()
    {
        if (RequiresManualRecovery) return new(BattlefieldOutcome.Rejected, "Session requires manual recovery; restart the development encounter.");
        if (SelectedCharacterId is null || !Capture(SelectedCharacterId).CanActivate) return new(BattlefieldOutcome.Rejected, "Selected character cannot activate now.");
        Activations.StartActivation(Characters[SelectedCharacterId]);
        return Report(BattlefieldOutcome.Completed, $"Activation: {Characters[SelectedCharacterId].Name}.");
    }

    public MovementActionResult PreviewMovement(MovementActionRequest request) => Movement.Preview(request);
    public MovementActionResult ExecuteMovement(MovementActionRequest request)
    {
        if (RequiresManualRecovery) throw new InvalidOperationException("Session requires manual recovery; movement is locked.");
        var result = Movement.Execute(request);
        if (result.Outcome == MovementOutcome.PartiallyApplied) RequiresManualRecovery = true;
        Log($"Movement {result.Outcome}: {result.Reason}; reached {result.FinalPosition}; cost {result.TraversedCost}.");
        foreach (var reaction in result.Reactions)
            Log($"Sentinel {reaction.SourceId}: {reaction.Result?.Strike?.Hit.ToString() ?? "incomplete"}; HP -{reaction.TargetHPLost}, shield -{reaction.TargetShieldLost}.");
        return result;
    }

    public BattlefieldAttackResult PreviewAttack(string actorId, string targetId) => Attack("preview", actorId, targetId, false);
    public BattlefieldAttackResult ExecuteAttack(string requestId, string actorId, string targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        var result = Attack(requestId, actorId, targetId, true);
        Log($"Attack {result.Outcome}: {result.Reason}");
        if (result.Combat is { } combat)
        {
            Log($"Opening {(combat.OpeningStrike.Hit ? "hit" : "miss")}: {combat.OpeningStrike.Damage} calculated, HP -{combat.OpeningHealthLost}, shield -{combat.OpeningShieldAbsorbed}.");
            if (combat.CounterStrike is { } counter) Log($"Counter {(counter.Hit ? "hit" : "miss")}: {counter.Damage} calculated.");
            if (combat.FollowUpStrike is { } follow) Log($"Follow-up by {combat.FollowUpBy}: {(follow.Hit ? "hit" : "miss")}, {follow.Damage} calculated.");
        }
        return result;
    }

    private BattlefieldAttackResult Attack(string id, string actorId, string targetId, bool execute)
    {
        if (RequiresManualRecovery) return new(BattlefieldOutcome.Rejected, "Session requires manual recovery.");
        if (execute && _attackAttempts.Contains(id)) return new(BattlefieldOutcome.AlreadyProcessed, "Attack request already attempted.");
        if (!Characters.TryGetValue(actorId, out var actor) || !Characters.TryGetValue(targetId, out var target))
            return new(BattlefieldOutcome.Rejected, "Unknown attacker or target.");
        if (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) return new(BattlefieldOutcome.Rejected, "Wrong lifecycle phase.");
        bool started = false;
        long hp = (long)actor.CurrentHealth + target.CurrentHealth, shields = (long)actor.ShieldHP + target.ShieldHP;
        try
        {
            var attack = DevelopmentAttacks[actorId];
            if (actor.TacticalPosition is null || target.TacticalPosition is null) return new(BattlefieldOutcome.Unresolved, "Missing tactical position.");
            var intent = new AttackIntent(actor, target, "ordinary_attack", Activations, true,
                new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, true, false, false),
                mapTargetLegal: () => Map.TargetLegal(actor, target));
            var request = CreateRequest(actorId, targetId, checked((int)actor.TacticalPosition.Value.ManhattanDistanceTo(target.TacticalPosition.Value)))!
                with { IncomingAttackPermitsFollowUps = true };
            var preview = _attacks.Preview(intent, request, attack);
            if (!preview.CanExecute) return new(preview.Validation.Outcome == AttackIntentOutcome.TauntContextUnresolved || preview.Targeting?.RejectionReason is AbilityTargetingRejectionReason.MissingRequiredTargetInformation or AbilityTargetingRejectionReason.UnresolvedTargetingRule or AbilityTargetingRejectionReason.UnresolvedRange or AbilityTargetingRejectionReason.UnresolvedLineOfSight or AbilityTargetingRejectionReason.UnresolvedElevation
                ? BattlefieldOutcome.Unresolved : BattlefieldOutcome.Rejected,
                $"{preview.Validation.Outcome}: {preview.Targeting?.RejectionReason.ToString() ?? preview.Validation.TauntApproval?.Outcome.ToString()}");
            if (!execute) return new(BattlefieldOutcome.Ready, "Legal ordinary attack.");
            _attackAttempts.Add(id); started = true;
            var result = _attacks.Execute(intent, request, attack);
            return new(result.Completed ? BattlefieldOutcome.Completed : BattlefieldOutcome.Rejected, result.Validation.Outcome.ToString(), result.Combat);
        }
        catch (SanctuaryDefenseUnresolvedException error) when (!started)
        { return new(BattlefieldOutcome.Unresolved, error.Result.Reason.ToString()); }
        catch (Exception error)
        {
            if (started) RequiresManualRecovery = true;
            bool changed = hp != (long)actor.CurrentHealth + target.CurrentHealth || shields != (long)actor.ShieldHP + target.ShieldHP || (started && Activations.MainActionUsed);
            return new(changed ? BattlefieldOutcome.PartiallyApplied : started ? BattlefieldOutcome.Failed : BattlefieldOutcome.Unresolved,
                $"{error.GetType().Name}: {error.Message}; actual HP {actor.CurrentHealth}/{target.CurrentHealth}, shields {actor.ShieldHP}/{target.ShieldHP}. No retry.");
        }
    }

    public BattlefieldCommandResult EndActivation()
    {
        if (RequiresManualRecovery) return new(BattlefieldOutcome.Rejected, "Session requires manual recovery.");
        if (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations || Activations.ActiveCharacter is null)
            return new(BattlefieldOutcome.Rejected, "No active activation.");
        try { Activations.CompleteActivation(); return Report(BattlefieldOutcome.Completed, "Activation ended."); }
        catch (InvalidOperationException error) { return Report(BattlefieldOutcome.Unresolved, error.Message); }
    }

    /// <summary>Processes the current phase, then asks the existing lifecycle to advance exactly once.</summary>
    public BattlefieldCommandResult AdvancePhase()
    {
        if (RequiresManualRecovery) return new(BattlefieldOutcome.Rejected, "Session requires manual recovery.");
        if (Lifecycle.CurrentPhase == RoundPhase.AlternatingActivations && !Activations.IsRoundComplete)
            return new(BattlefieldOutcome.Rejected, "Finish all eligible activations before Cleanup.");
        var key = (Lifecycle.CurrentRound, Lifecycle.CurrentPhase);
        if (!_phaseAttempts.Add(key)) return new(BattlefieldOutcome.AlreadyProcessed, "Phase already attempted; no automatic replay.");
        try
        {
            var roster = Characters.Values;
            string detail = "";
            switch (Lifecycle.CurrentPhase)
            {
                case RoundPhase.Shop: detail = "Development no-op: Shop is not implemented."; break;
                case RoundPhase.PoisonBatch: var poison = new PoisonBatchProcessor(Lifecycle).Process(roster); detail = $"Poison processed: {poison.Entries.Count} ticks."; break;
                case RoundPhase.DelayedAttacks: new DelayedAttackProcessor(DelayedAttacks, Lifecycle).Process(roster); break;
                case RoundPhase.TimedStatusUpdate: new TimedStatusBatchProcessor(Lifecycle).Process(roster); break;
                case RoundPhase.OtherRoundStartTriggers: detail = "Development no-op: other triggers are not implemented."; break;
                case RoundPhase.Preparation: new RoundPreparationCoordinator(Activations, Lifecycle).Process(roster, Rules.StartingTeamId); break;
                case RoundPhase.Cleanup:
                    LastCleanup = Cleanup.Process(Lifecycle.CurrentRound);
                    if (LastCleanup.Status != RoundCleanupStatus.Completed) { RequiresManualRecovery = true; return Report(BattlefieldOutcome.Failed, LastCleanup.Reason); }
                    detail = "Production Cleanup completed."; break;
                case RoundPhase.ObjectiveScoring: detail = "Development no-op: objectives and victory are not implemented."; break;
            }
            _processed.Add(Lifecycle.CurrentPhase);
            if (Lifecycle.CurrentPhase == RoundPhase.ObjectiveScoring) Lifecycle.StartNextRound(); else Lifecycle.AdvancePhase();
            return Report(BattlefieldOutcome.Completed, $"{detail} R{Lifecycle.CurrentRound} — {Lifecycle.CurrentPhase}.");
        }
        catch (Exception error)
        { RequiresManualRecovery = true; return Report(BattlefieldOutcome.Failed, $"Phase failed: {error.Message}. State retained; no automatic retry."); }
    }

    private BattlefieldCommandResult Report(BattlefieldOutcome outcome, string reason) { Log(reason); return new(outcome, reason); }
    private void Log(string message) { _log.Add(message); if (_log.Count > 80) _log.RemoveAt(0); }
    public AttackConfiguration? CurrentLance(string ownerId) => DevelopmentAttacks.GetValueOrDefault(ownerId);
    public CombatExchangeRequest? CreateRequest(string ownerId, string moverId, int distance)
    {
        if (!Characters.TryGetValue(ownerId, out var owner) || !Characters.TryGetValue(moverId, out var mover)) return null;
        var attack = DevelopmentAttacks[ownerId]; var counter = DevelopmentAttacks[moverId];
        return new(owner, mover, attack.Type, attack.Power, attack.Category, counter.Category, attack.BaseAccuracy,
            counter.Type, counter.Power, counter.BaseAccuracy, counter.MinimumRange, counter.MaximumRange, distance, true, false,
            OpeningEvasionModifier: mover.TacticalPosition is { } target ? SmokeVeil.GetEvasionModifier(mover, target, Smoke) : 0,
            CounterattackEvasionModifier: owner.TacticalPosition is { } source ? SmokeVeil.GetEvasionModifier(owner, source, Smoke) : 0);
    }
}
