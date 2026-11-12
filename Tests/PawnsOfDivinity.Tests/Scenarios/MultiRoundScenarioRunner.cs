using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Tests.Scenarios;

/// <summary>Drives production round processors and caller-selected actions. It makes no tactical choices.</summary>
public sealed class MultiRoundScenarioRunner
{
    private readonly CombatScenario _scenario;
    private readonly ScenarioSnapshot _initial;
    private readonly List<ScenarioRoundResult> _rounds = new();
    private readonly List<ScenarioEvent> _events = new();
    private readonly List<ScenarioPhaseResult> _phases = new();
    private readonly List<ScenarioCheckpoint> _checkpoints = new();
    private ScenarioRoundPlan? _pending;
    private int _eventStart, _actionStart, _drawStart;
    private ScenarioOutcome _outcome;
    private string _reason = "";
    private bool _halted;
    public MultiRoundScenarioRunner(CombatScenario scenario)
    { ArgumentNullException.ThrowIfNull(scenario); _scenario = scenario; _initial = scenario.Capture(); }
    public MultiRoundScenarioResult Result => _pending is null ? new(_rounds, _initial, _scenario.Capture())
        : throw new InvalidOperationException("Complete the pending round before requesting aggregate results.");

    public MultiRoundScenarioResult Run(params ScenarioRoundPlan[] plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        foreach (var plan in plans)
            if (RunRound(plan).Outcome != ScenarioOutcome.Completed) break;
        return Result;
    }
    public ScenarioRoundResult RunRound(ScenarioRoundPlan plan)
    {
        if (BeginRound(plan) == ScenarioOutcome.Ready) RunActivations(plan.Activations);
        return CompleteRound();
    }

    /// <summary>Stops at Activations so isolated production-service fixtures can be exercised explicitly.</summary>
    public ScenarioOutcome BeginRound(ScenarioRoundPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (_halted) throw new InvalidOperationException("The simulation was interrupted; it cannot be retried automatically.");
        if (_pending is not null) throw new InvalidOperationException("Complete the pending round first.");
        _pending = plan; _eventStart = _events.Count; _actionStart = _scenario.Runner.Results.Count; _drawStart = _scenario.Runner.Draws.Count;
        _phases.Clear(); _checkpoints.Clear(); _outcome = ScenarioOutcome.Ready; _reason = "";
        var life = _scenario.Lifecycle;
        // Validate the plan before advancing a round or reserving any processing.
        if ((plan.StartingTeamId != _scenario.Activations.FirstTeamId && plan.StartingTeamId != _scenario.Activations.SecondTeamId) ||
            _scenario.Activations.ActiveCharacter is not null || _scenario.RoundAttempts.Contains(plan.Round) ||
            !(life.CurrentPhase == RoundPhase.Shop && life.CurrentRound == plan.Round ||
              life.CurrentPhase == RoundPhase.ObjectiveScoring && (long)life.CurrentRound + 1 == plan.Round))
        { Interrupt(ScenarioOutcome.Rejected, "RoundContextInvalidOrAlreadyAttempted"); CaptureCheckpoint("Rejected round"); return _outcome; }
        if (life.CurrentPhase == RoundPhase.ObjectiveScoring) life.StartNextRound();
        _scenario.RoundAttempts.Add(plan.Round);
        CaptureCheckpoint("Round start"); Emit(ScenarioEventType.RoundStarted);
        while (_outcome == ScenarioOutcome.Ready && life.CurrentPhase != RoundPhase.AlternatingActivations)
        {
            if (!ProcessCurrentPhase()) break;
            Advance();
        }
        if (_outcome == ScenarioOutcome.Ready) CaptureCheckpoint("After round-start processing");
        return _outcome;
    }

    public void CaptureCheckpoint(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_pending is null) throw new InvalidOperationException("Begin a round before capturing a round checkpoint.");
        _checkpoints.Add(new(name, _scenario.Capture()));
    }

    public void RunActivations(IEnumerable<ScenarioActivationPlan> activations)
    {
        ArgumentNullException.ThrowIfNull(activations);
        if (_pending is null || _outcome != ScenarioOutcome.Ready || _scenario.Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations)
            throw new InvalidOperationException("Begin a valid round before scripting activations.");
        foreach (var activation in activations)
        {
            ArgumentNullException.ThrowIfNull(activation);
            var selected = Execute(new($"round:{_pending.Round}:select:{_scenario.Runner.Results.Count}", ScenarioActionKind.StartActivation, activation.ActorId));
            if (selected.Outcome == ScenarioOutcome.Rejected) continue;
            if (selected.Outcome != ScenarioOutcome.Completed) break;
            foreach (var action in activation.Actions)
            {
                var result = Execute(action);
                // An interrupted ordinary move is a resolved action, not a broken round.
                // The explicit script may still use the surviving actor's remaining actions.
                if (result.Outcome is ScenarioOutcome.Unresolved or ScenarioOutcome.PartiallyApplied or ScenarioOutcome.UnexpectedFailure)
                { Interrupt(result.Outcome, result.Reason); break; }
            }
            CaptureCheckpoint($"After activation: {activation.ActorId}");
            if (_outcome != ScenarioOutcome.Ready) break;
            if (_scenario.Activations.ActiveCharacter is not null)
            { Interrupt(ScenarioOutcome.Unresolved, "ActivationPlanIncomplete: explicitly finish or supply another action"); break; }
        }
    }
    private ScenarioExecutionResult Execute(ScenarioAction action)
    {
        var result = _scenario.Runner.Execute(action);
        foreach (var entry in result.Events) _events.Add(entry with { Sequence = _events.Count + 1 });
        return result;
    }

    public ScenarioRoundResult CompleteRound()
    {
        if (_pending is null) throw new InvalidOperationException("There is no pending round.");
        if (_outcome == ScenarioOutcome.Ready && (_scenario.Lifecycle.CurrentRound != _pending.Round ||
            _scenario.Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations))
            Interrupt(ScenarioOutcome.Rejected, "RoundContextChanged: lifecycle advanced outside this coordinator");
        if (_outcome == ScenarioOutcome.Ready && !_scenario.Activations.IsRoundComplete)
            Interrupt(ScenarioOutcome.Unresolved, "RoundPlanIncomplete: eligible actors or an active activation remain");
        if (_outcome == ScenarioOutcome.Ready)
        {
            // Activations themselves were recorded by ScenarioRunner; this phase is now acknowledged.
            var state = _scenario.Capture();
            _phases.Add(new(RoundPhase.AlternatingActivations,
                _checkpoints.First(c => c.Name == "After round-start processing").State, state,
                ScenarioOutcome.Completed, "Caller-supplied activations finished"));
            Advance();
            if (ProcessCurrentPhase())
            {
                CaptureCheckpoint("Cleanup"); Advance();
                if (ProcessCurrentPhase()) { CaptureCheckpoint("Round end"); Emit(ScenarioEventType.RoundEnded); _outcome = ScenarioOutcome.Completed; }
            }
        }
        if (_outcome != ScenarioOutcome.Completed) { _halted = true; CaptureCheckpoint("Interrupted"); }
        var result = new ScenarioRoundResult(_pending.Round, _outcome, _reason, _phases, _checkpoints,
            _scenario.Runner.Results.Skip(_actionStart), _events.Skip(_eventStart), _scenario.Runner.Draws.Skip(_drawStart));
        _rounds.Add(result); _pending = null;
        return result;
    }

    private bool ProcessCurrentPhase()
    {
        var before = _scenario.Capture();
        PoisonBatchResult? poison = null; DelayedAttackBatchResult? delayed = null; TimedStatusBatchResult? statuses = null;
        RoundPreparationResult? preparation = null;
        RoundCleanupResult? cleanup = null;
        try
        {
            var life = _scenario.Lifecycle;
            switch (life.CurrentPhase)
            {
                case RoundPhase.Shop:
                case RoundPhase.OtherRoundStartTriggers:
                case RoundPhase.ObjectiveScoring:
                    Emit(ScenarioEventType.PhaseNoOp, detail: "No production gameplay executed for this phase"); break;
                case RoundPhase.PoisonBatch:
                    poison = new PoisonBatchProcessor(life).Process(_scenario.Characters.Values);
                    foreach (var entry in poison.Entries)
                    {
                        Emit(ScenarioEventType.PoisonResolved, target: entry.CharacterId, amount: entry.Tick.CalculatedDamage, detail: $"Tick {entry.Tick.TickNumber}");
                        Damage("", entry.CharacterId, entry.Tick.ShieldHPAbsorbed, entry.Tick.HealthLost, entry.Tick.Defeated);
                    }
                    break;
                case RoundPhase.DelayedAttacks:
                    delayed = new DelayedAttackProcessor(_scenario.DelayedAttacks, life).Process(_scenario.Characters.Values);
                    foreach (var entry in delayed.Entries)
                    {
                        Emit(ScenarioEventType.DelayedAttackResolved, entry.Attack.SourceCharacterId, entry.Attack.TargetCharacterId,
                            entry.Attack.Strike.Damage, entry.Attack.Id);
                        if (!entry.SkippedDefeatedTarget)
                        {
                            Emit(entry.Attack.Strike.Hit ? ScenarioEventType.Hit : ScenarioEventType.Miss,
                                entry.Attack.SourceCharacterId, entry.Attack.TargetCharacterId, entry.Attack.Strike.Damage);
                            Damage(entry.Attack.SourceCharacterId, entry.Attack.TargetCharacterId, entry.ShieldHPAbsorbed,
                                entry.HealthLost, entry.RecipientDefeated);
                        }
                    }
                    foreach (var id in delayed.CancelledAttackIds) Emit(ScenarioEventType.DelayedAttackCancelled, detail: id);
                    break;
                case RoundPhase.TimedStatusUpdate:
                    statuses = new TimedStatusBatchProcessor(life).Process(_scenario.Characters.Values);
                    foreach (var entry in statuses.Entries)
                        foreach (var status in entry.ExpiredStatuses) Emit(ScenarioEventType.StatusExpired, target: entry.CharacterId, detail: status.ToString());
                    break;
                case RoundPhase.Preparation:
                    preparation = new RoundPreparationCoordinator(_scenario.Activations, life).Process(_scenario.Characters.Values, _pending!.StartingTeamId);
                    break;
                case RoundPhase.Cleanup:
                    cleanup = _scenario.Cleanup.Process(life.CurrentRound);
                    RecordCleanupEvents(cleanup);
                    if (cleanup.Status != RoundCleanupStatus.Completed)
                    {
                        var outcome = cleanup.Status == RoundCleanupStatus.PartiallyApplied ? ScenarioOutcome.PartiallyApplied
                            : cleanup.Status is RoundCleanupStatus.Failed or RoundCleanupStatus.ManualResolutionRequired ? ScenarioOutcome.UnexpectedFailure
                            : ScenarioOutcome.Rejected;
                        Interrupt(outcome, $"Cleanup {cleanup.Status}: {cleanup.Reason}");
                        Emit(outcome == ScenarioOutcome.Rejected ? ScenarioEventType.ActionRejected : ScenarioEventType.ActionFailed, detail: _reason);
                        _phases.Add(new(before.Phase, before, _scenario.Capture(), outcome, _reason, Cleanup: cleanup));
                        return false;
                    }
                    break;
                default: throw new InvalidOperationException("This phase is controlled by scripted activations.");
            }
            _phases.Add(new(before.Phase, before, _scenario.Capture(), ScenarioOutcome.Completed, "", poison, delayed, statuses, preparation, cleanup));
            return true;
        }
        catch (Exception error)
        {
            var after = _scenario.Capture();
            var outcome = before.ToCanonicalJson() == after.ToCanonicalJson() ? ScenarioOutcome.UnexpectedFailure : ScenarioOutcome.PartiallyApplied;
            Interrupt(outcome, $"{error.GetType().Name}: {error.Message}");
            // A throwing processor returned no batch details. Record observations only, without claiming rollback.
            foreach (var (id, old) in before.Characters)
            {
                var current = after.Characters[id];
                if (old.CurrentHealth > current.CurrentHealth) Emit(ScenarioEventType.HealthChangeObserved, target: id, amount: old.CurrentHealth - current.CurrentHealth);
                if (old.ShieldHP > current.ShieldHP) Emit(ScenarioEventType.ShieldChangeObserved, target: id, amount: old.ShieldHP - current.ShieldHP);
            }
            Emit(ScenarioEventType.ActionFailed, detail: _reason);
            _phases.Add(new(before.Phase, before, after, outcome, _reason));
            return false;
        }
    }

    private void RecordCleanupEvents(RoundCleanupResult cleanup)
    {
        // Repeated requests retain the original evidence but do not represent new expiration events.
        if (cleanup.Status is RoundCleanupStatus.AlreadyProcessed or RoundCleanupStatus.ManualResolutionRequired) return;
        foreach (var step in cleanup.Services.Where(s => s.Status == CleanupServiceStatus.Completed))
            foreach (var effect in step.Effects!)
            {
                var type = step.Service switch
                {
                    RoundCleanupService.TemporaryModifiers => ScenarioEventType.ModifierExpired,
                    RoundCleanupService.Taunts => ScenarioEventType.TauntExpired,
                    RoundCleanupService.Sentinel => ScenarioEventType.SentinelRetired,
                    _ => ScenarioEventType.TerrainExpired
                };
                // Preserve the previous event shape while taking every identity from the production result.
                Emit(type, step.Service == RoundCleanupService.TemporaryModifiers ? "" : effect.SourceCharacterId,
                    effect.RecipientCharacterId, detail: effect.Id);
            }
    }
    private void Damage(string actor, string target, int shield, int hp, bool defeated)
    {
        if (shield > 0) Emit(ScenarioEventType.ShieldDamage, actor, target, shield);
        if (hp > 0) Emit(ScenarioEventType.HPDamage, actor, target, hp);
        if (defeated) Emit(ScenarioEventType.CharacterDefeated, actor, target);
    }
    private void Interrupt(ScenarioOutcome outcome, string reason) { _outcome = outcome; _reason = reason; }
    private void Advance() { _scenario.Lifecycle.AdvancePhase(); Emit(ScenarioEventType.PhaseAdvanced); }
    private void Emit(ScenarioEventType type, string actor = "", string? target = null, int? amount = null, string? detail = null)
        => _events.Add(new(_events.Count + 1, actor, target, type, amount, detail, _scenario.Lifecycle.CurrentRound, _scenario.Lifecycle.CurrentPhase));
}
