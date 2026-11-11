using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Tests.Scenarios;

public sealed record ScenarioCheckpoint(string Name, ScenarioSnapshot State);
public sealed record ScenarioPhaseResult(RoundPhase Phase, ScenarioSnapshot Before, ScenarioSnapshot After,
    ScenarioOutcome Outcome, string Reason, PoisonBatchResult? Poison = null, DelayedAttackBatchResult? Delayed = null,
    TimedStatusBatchResult? Statuses = null, RoundPreparationResult? Preparation = null, RoundCleanupResult? Cleanup = null);

/// <summary>Metrics count each reported outcome once. Missing strike details after exceptions are not estimated.</summary>
public sealed record MultiRoundMetrics(int RoundsExecuted, int ActivationsAttempted, int ActivationsCompleted,
    int AttacksAttempted, int Hits, int Misses, long HPDamage, long ShieldDamage, long Healing, int Defeats,
    int StatusApplications, int CompletedAbilities, int RejectedActions, int UnresolvedActions, int PartiallyAppliedActions,
    long StartingHP, long EndingHP)
{
    internal static MultiRoundMetrics From(IReadOnlyList<ScenarioRoundResult> rounds, ScenarioSnapshot before, ScenarioSnapshot after)
    {
        var events = rounds.SelectMany(r => r.Events).ToArray();
        var actions = rounds.SelectMany(r => r.Actions).ToArray();
        long Amount(params ScenarioEventType[] types) => events.Where(e => types.Contains(e.Type)).Sum(e => (long)(e.Amount ?? 0));
        return new(rounds.Count(r => r.Outcome == ScenarioOutcome.Completed),
            actions.Count(a => a.Action.Kind == ScenarioActionKind.StartActivation),
            events.Count(e => e.Type == ScenarioEventType.ActivationEnded),
            actions.Count(a => a.Action.Kind == ScenarioActionKind.Attack || a.Action.AbilityId == "frostbite_arrow"),
            events.Count(e => e.Type == ScenarioEventType.Hit), events.Count(e => e.Type == ScenarioEventType.Miss),
            Amount(ScenarioEventType.HPDamage, ScenarioEventType.HealthChangeObserved),
            Amount(ScenarioEventType.ShieldDamage, ScenarioEventType.ShieldChangeObserved), Amount(ScenarioEventType.Healing),
            events.Count(e => e.Type == ScenarioEventType.CharacterDefeated),
            events.Count(e => e.Type == ScenarioEventType.StatusApplied),
            actions.Count(a => a.Production?.AbilityFinalized == true), actions.Count(a => a.Outcome == ScenarioOutcome.Rejected),
            actions.Count(a => a.Outcome == ScenarioOutcome.Unresolved), actions.Count(a => a.Outcome == ScenarioOutcome.PartiallyApplied),
            before.Characters.Values.Sum(c => (long)c.CurrentHealth), after.Characters.Values.Sum(c => (long)c.CurrentHealth));
    }
}

public sealed record ScenarioRoundResult
{
    public int Round { get; }
    public ScenarioOutcome Outcome { get; }
    public string Reason { get; }
    public IReadOnlyList<ScenarioPhaseResult> Phases { get; }
    public IReadOnlyList<ScenarioCheckpoint> Checkpoints { get; }
    public IReadOnlyList<ScenarioExecutionResult> Actions { get; }
    public IReadOnlyList<ScenarioEvent> Events { get; }
    public IReadOnlyList<ScenarioRandomDraw> RandomDraws { get; }
    internal ScenarioRoundResult(int round, ScenarioOutcome outcome, string reason, IEnumerable<ScenarioPhaseResult> phases,
        IEnumerable<ScenarioCheckpoint> checkpoints, IEnumerable<ScenarioExecutionResult> actions,
        IEnumerable<ScenarioEvent> events, IEnumerable<ScenarioRandomDraw> draws)
    {
        Round = round; Outcome = outcome; Reason = reason;
        Phases = Array.AsReadOnly(phases.ToArray()); Checkpoints = Array.AsReadOnly(checkpoints.ToArray());
        Actions = Array.AsReadOnly(actions.ToArray()); Events = Array.AsReadOnly(events.ToArray());
        RandomDraws = Array.AsReadOnly(draws.ToArray());
    }
}

public sealed record MultiRoundScenarioResult
{
    public IReadOnlyList<ScenarioRoundResult> Rounds { get; }
    public ScenarioSnapshot Before { get; }
    public ScenarioSnapshot After { get; }
    public IReadOnlyList<ScenarioEvent> Events { get; }
    public MultiRoundMetrics Metrics { get; }
    public string? Interruption => Rounds.LastOrDefault() is { Outcome: not ScenarioOutcome.Completed } last ? last.Reason : null;
    internal MultiRoundScenarioResult(IEnumerable<ScenarioRoundResult> rounds, ScenarioSnapshot before, ScenarioSnapshot after)
    {
        Rounds = Array.AsReadOnly(rounds.ToArray()); Before = before; After = after;
        Events = Array.AsReadOnly(Rounds.SelectMany(r => r.Events).ToArray()); Metrics = MultiRoundMetrics.From(Rounds, before, after);
    }
}
