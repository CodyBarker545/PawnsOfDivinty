using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Tests.Scenarios;

/// <summary>Real production state plus fixture adapters. No parallel HP, position, turn or cooldown authority.</summary>
public sealed class CombatScenario
{
    public IReadOnlyDictionary<string, Character> Characters { get; }
    public IReadOnlyDictionary<string, ScenarioAttackInput> AttackInputs { get; }
    public RoundLifecycleManager Lifecycle { get; }
    public ActivationManager Activations { get; }
    public WailingRampartService Walls { get; } = new();
    public SpiritFrostService Ice { get; } = new();
    public SmokePlacementService Smoke { get; } = new();
    public DelayedAttackScheduler DelayedAttacks { get; } = new();
    public ImpalingSentinelTracker Sentinel { get; }
    public RoundCleanupCoordinator Cleanup { get; }
    internal HashSet<int> RoundAttempts { get; } = new();
    public ScenarioMap Map { get; }
    public ScenarioRunner Runner { get; }
    public MovementActionExecutor Movement => Lifecycle.MovementExecutor!;
    public ScenarioSnapshot InitialSnapshot { get; }
    internal CombatScenario(Dictionary<string, Character> characters, Dictionary<string, ScenarioAttackInput> attacks,
        RoundLifecycleManager lifecycle, ActivationManager activations, int width, int height, IEnumerable<GridPosition> blocked,
        bool? targeting, int[] accuracy, int[] status, int[] selection, IEnumerable<DelayedAttack> delayed)
    {
        Characters = new ReadOnlyDictionary<string, Character>(characters);
        AttackInputs = new ReadOnlyDictionary<string, ScenarioAttackInput>(attacks);
        Lifecycle = lifecycle; Activations = activations;
        Sentinel = new(activations, lifecycle);
        Cleanup = new(lifecycle, activations, Walls, Ice, Smoke, Sentinel);
        Map = new(this, width, height, blocked, targeting);
        foreach (var attack in delayed)
        {
            if (attack.ScheduledRound > lifecycle.CurrentRound || !characters.ContainsKey(attack.TargetCharacterId))
                throw new ArgumentException("Initial delayed attacks must have been scheduled already and reference known targets.");
            DelayedAttacks.Schedule(attack);
        }
        InitialSnapshot = Capture();
        Runner = new(this, accuracy, status, selection);
    }
    public ScenarioSnapshot Capture() => new(this);
    public ScenarioMetrics Metrics
    {
        get
        {
            var results = Runner.Results;
            var strikes = results.Where(r => r.Production?.Combat is not null).SelectMany(r => new[] {
                r.Production!.Combat!.OpeningStrike, r.Production.Combat.CounterStrike, r.Production.Combat.FollowUpStrike }).Where(s => s is not null).ToArray();
            return new(InitialSnapshot.Characters.Values.Sum(c => (long)c.CurrentHealth), Characters.Values.Sum(c => (long)c.CurrentHealth),
                results.Sum(r => r.Before.Characters.Sum(c => (long)Math.Max(0, c.Value.CurrentHealth - r.After.Characters[c.Key].CurrentHealth))),
                results.Sum(r => r.Before.Characters.Sum(c => (long)Math.Max(0, c.Value.ShieldHP - r.After.Characters[c.Key].ShieldHP))),
                strikes.Count(s => s!.Hit), strikes.Count(s => !s!.Hit),
                results.Count(r => r.Outcome == ScenarioOutcome.Completed && r.Production?.Combat is not null),
                results.Count(r => r.Outcome == ScenarioOutcome.Rejected), results.Count(r => r.Outcome == ScenarioOutcome.Unresolved),
                results.Count(r => r.Outcome == ScenarioOutcome.PartiallyApplied), results.Count(r => r.Outcome == ScenarioOutcome.UnexpectedFailure));
        }
    }
}
