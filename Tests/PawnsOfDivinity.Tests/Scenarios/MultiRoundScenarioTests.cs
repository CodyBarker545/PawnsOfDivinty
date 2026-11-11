using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Scenarios;

public class MultiRoundScenarioTests
{
    [Fact]
    public void ThreeRoundDuelUsesLiveFuryAndPersistentHealth()
    {
        var scenario = Duel().Build(); var runner = new MultiRoundScenarioRunner(scenario);
        var result = runner.Run(DuelRound(1), DuelRound(2), DuelRound(3));
        Assert.Null(result.Interruption);
        Assert.Equal(3, result.Metrics.RoundsExecuted);
        Assert.Equal(6, result.Metrics.ActivationsCompleted);
        Assert.Equal(6, result.Metrics.AttacksAttempted);
        Assert.Equal(new[] { "nigel", "tank", "nigel", "tank", "nigel", "tank" },
            result.Events.Where(e => e.Type == ScenarioEventType.ActivationStarted).Select(e => e.ActorId));
        Assert.Equal(new[] { 3, 3, 4 }, result.Rounds.Select(r => r.Actions.Single(a => a.Action.ActorId == "nigel" && a.Production?.Combat is not null).Production!.Combat!.OpeningStrike.Damage));
        Assert.Equal(40, result.After.Characters["nigel"].CurrentHealth);
        Assert.Equal(26, result.After.Characters["tank"].CurrentHealth);
        Assert.Equal(50, result.Metrics.HPDamage);
        Assert.Equal(RoundPhase.ObjectiveScoring, result.After.Phase);
        Assert.Equal(18, result.Metrics.Hits);
        Assert.Equal(18, result.Rounds.Sum(r => r.RandomDraws.Count));
    }

    [Fact]
    public void CompleteRoundVisitsAllNineProductionPhasesAndCapturesBoundaries()
    {
        var scenario = Quiet().Build(); var runner = new MultiRoundScenarioRunner(scenario);
        var round = runner.RunRound(QuietRound(1));
        Assert.Equal(ScenarioOutcome.Completed, round.Outcome);
        Assert.Equal(Enum.GetValues<RoundPhase>(), round.Phases.Select(p => p.Phase));
        Assert.Equal(new[] { "Round start", "After round-start processing", "After activation: nigel", "After activation: tank", "Cleanup", "Round end" }, round.Checkpoints.Select(c => c.Name));
        Assert.Equal(3, round.Events.Count(e => e.Type == ScenarioEventType.PhaseNoOp));
        Assert.All(round.Events, e => { Assert.Equal(1, e.Round); Assert.NotNull(e.Phase); });
    }

    [Fact]
    public void RoarCooldownUsedInRoundTwoIsReadyInRoundFive()
    {
        var scenario = RoarSetup().AtRound(2).Build(); var runner = new MultiRoundScenarioRunner(scenario);
        ScenarioRoundPlan Plan(int round, bool taunted) => new(round, 1,
            new("nigel", new ScenarioAction($"roar{round}", ScenarioActionKind.Ability, "nigel", abilityId: "provoking_roar"),
                End("nigel", $"end{round}")),
            new("enemy", new ScenarioAction($"enemyEnd{round}", ScenarioActionKind.EndActivation, "enemy",
                requiredAttack: taunted ? new("nigel", true, true, false) : null)));
        var result = runner.Run(Plan(2, true), Plan(3, false), Plan(4, false), Plan(5, true));
        Assert.Null(result.Interruption);
        Assert.Equal(2, result.Metrics.CompletedAbilities);
        foreach (var round in result.Rounds.Where(r => r.Round < 5))
            Assert.Equal(5, round.Checkpoints.Last().State.Characters["nigel"].Cooldowns["provoking_roar"].ReadyRound);
        Assert.Equal(new[] { "OnCooldown", "OnCooldown" }, result.Rounds.SelectMany(r => r.Actions)
            .Where(a => a.Action.AbilityId == "provoking_roar" && a.Outcome == ScenarioOutcome.Rejected).Select(a => a.Reason));
        Assert.Equal(8, result.After.Characters["nigel"].Cooldowns["provoking_roar"].ReadyRound);
        Assert.Empty(scenario.Runner.Draws);
    }

    [Fact]
    public void FourSubsequentPoisonStartsEscalateAndAbsorbShieldsFirst()
    {
        // Poison is already present from before R2; the first processed batch is its subsequent round start.
        var scenario = Quiet().AtRound(2).WithStatus("nigel", StatusType.Poison).WithShield("nigel", 5).Build();
        var runner = new MultiRoundScenarioRunner(scenario);
        var result = runner.Run(QuietRound(2), QuietRound(3), QuietRound(4), QuietRound(5));
        Assert.Null(result.Interruption);
        Assert.Equal(new[] { 2, 4, 6, 8 }, result.Rounds.Select(r => r.Phases.Single(p => p.Poison is not null).Poison!.Entries.Single().Tick.CalculatedDamage));
        Assert.Equal(new[] { 52, 51, 45, 37 }, result.Rounds.Select(r => r.Checkpoints.Last().State.Characters["nigel"].CurrentHealth));
        Assert.Equal(4, result.After.Characters["nigel"].PoisonTicks);
        Assert.Equal(15, result.Metrics.HPDamage);
        Assert.Equal(5, result.Metrics.ShieldDamage);
        Assert.Equal(0, result.Metrics.Hits);
    }

    [Fact]
    public void DelayedDamageUsesStoredOutcomeCurrentShieldsAndInsertionOrder()
    {
        var first = new DelayedAttack("first", "nigel", "tank", 1, 3, StoredStrike(13));
        var miss = new DelayedAttack("miss", "nigel", "tank", 1, 3, StoredStrike(13, false));
        var scenario = Quiet().AtRound(2).WithShield("tank", 6).WithDelayedAttack(first).WithDelayedAttack(miss)
            .WithModifier("tank", new("later_resistance", "tank", "tank", TemporaryStat.Resistance, 20, 2, 4)).Build();
        var result = new MultiRoundScenarioRunner(scenario).Run(QuietRound(2), QuietRound(3), QuietRound(4));
        Assert.Null(result.Interruption);
        Assert.Equal(64, result.Rounds[0].Checkpoints.Last().State.Characters["tank"].CurrentHealth);
        var batch = result.Rounds[1].Phases.Single(p => p.Delayed is not null).Delayed!;
        Assert.Equal(new[] { "first", "miss" }, batch.ProcessedAttackIds);
        Assert.Equal(13, batch.Entries[0].Attack.Strike.Damage);
        Assert.Equal(6, batch.Entries[0].ShieldHPAbsorbed);
        Assert.Equal(7, batch.Entries[0].HealthLost);
        Assert.False(batch.Entries[1].Attack.Strike.Hit);
        Assert.Equal(57, result.After.Characters["tank"].CurrentHealth);
        Assert.Empty(result.After.DelayedAttacks);
        Assert.Empty(scenario.Runner.Draws); // Stored-strike fixture creation is separate from resolution.
        Assert.Equal(1, result.Metrics.Hits); Assert.Equal(1, result.Metrics.Misses);
        Assert.Equal(7, result.Metrics.HPDamage); Assert.Equal(6, result.Metrics.ShieldDamage);
    }

    [Fact]
    public void TimedStatusesAppliedInRoundTwoExpireAtCorrectLaterStarts()
    {
        var scenario = Quiet().AtRound(2).Build(); var runner = new MultiRoundScenarioRunner(scenario);
        Assert.Equal(ScenarioOutcome.Ready, runner.BeginRound(QuietRound(2)));
        // Isolated status-application fixture during R2, after that round's decrement has finished.
        var tank = scenario.Characters["tank"];
        tank.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        tank.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        tank.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        tank.ConfigureParalysisSpeedReduction(2); // Explicit prototype penalty, not finalized balance.
        tank.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        runner.RunActivations(QuietRound(2).Activations);
        var applied = runner.CompleteRound();
        Assert.Equal(19, applied.Checkpoints.Last().State.Characters["tank"].EffectiveDefense);
        Assert.Equal(1, applied.Checkpoints.Last().State.Characters["tank"].EffectiveMovement);
        var third = runner.RunRound(QuietRound(3));
        Assert.Equal(1, third.Checkpoints.Last().State.Characters["tank"].TimedStatuses[StatusType.Burn]);
        Assert.DoesNotContain(StatusType.Silence, third.Checkpoints.Last().State.Characters["tank"].TimedStatuses.Keys);
        var fourth = runner.RunRound(QuietRound(4));
        var restored = fourth.Checkpoints.Last().State.Characters["tank"];
        Assert.Empty(restored.TimedStatuses);
        Assert.Equal(22, restored.EffectiveDefense); Assert.Equal(3, restored.EffectiveMovement); Assert.Equal(7, restored.EffectiveSpeed);
        Assert.Equal(22, restored.BaseStats.Defense);
        Assert.Equal(4, runner.Result.Events.Count(e => e.Type == ScenarioEventType.StatusExpired));
    }

    [Fact]
    public void RefreshDoesNotStackAndCleanseKeepsItsExistingPoisonResetBehavior()
    {
        var scenario = Quiet().AtRound(2).WithStatus("tank", StatusType.Poison).Build();
        var runner = new MultiRoundScenarioRunner(scenario);
        runner.BeginRound(QuietRound(2));
        var tank = scenario.Characters["tank"];
        tank.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        runner.RunActivations(QuietRound(2).Activations); runner.CompleteRound();
        runner.BeginRound(QuietRound(3));
        Assert.Equal(2, tank.Statuses.PoisonTicksOccurred);
        tank.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        Assert.Equal(19, tank.EffectiveDefense);
        CleanseProcessor.BasicCleanse(tank, StatusType.Poison);
        Assert.Equal(0, tank.Statuses.PoisonTicksOccurred);
        runner.RunActivations(QuietRound(3).Activations); runner.CompleteRound();
        var fourth = runner.RunRound(QuietRound(4));
        Assert.Equal(1, fourth.Checkpoints.Last().State.Characters["tank"].TimedStatuses[StatusType.Burn]);
        var fifth = runner.RunRound(QuietRound(5));
        Assert.Empty(fifth.Checkpoints.Last().State.Characters["tank"].TimedStatuses);
        Assert.Equal(2, runner.Result.Events.Count(e => e.Type == ScenarioEventType.PoisonResolved));
    }

    [Fact]
    public void HealingBlessingExpiresAtFollowingCleanupAndSanctuaryReevaluatesCurrentPosition()
    {
        var scenario = new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true).AtRound(2)
            .AddCharacter("colossus", "gravebound_colossus", 1, new(5, 5))
            .AddCharacter("guardian", "gentle_guardian", 1, new(7, 5))
            .AddCharacter("healer", "gentle_guardian", 1, new(7, 6))
            .AddCharacter("enemy", "nigel", 2, new(6, 5))
            .WithHealth("guardian", 43)
            .WithAttack("enemy", "axe", new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1))
            .WithAttack("guardian", "scroll", new(AttackType.Magical, WeaponCategory.MagicScroll, 3, 100, 1, 2))
            .Accuracy(Enumerable.Repeat(0, 10).ToArray()).Build();
        // Isolated shield management fixture, not an invented Sanctuary Shield casting range/action.
        var shields = new SanctuaryShieldManager(); shields.Apply(scenario.Characters["healer"], scenario.Characters["guardian"]);
        var runner = new MultiRoundScenarioRunner(scenario);
        var second = runner.RunRound(new(2, 1,
            new("healer", new ScenarioAction("heal", ScenarioActionKind.Ability, "healer", "guardian", "healing_light")),
            new("enemy", new ScenarioAction("attack2", ScenarioActionKind.Attack, "enemy", "guardian")),
            Early("colossus", "c2"), Early("guardian", "g2")));
        Assert.Equal(ScenarioOutcome.Completed, second.Outcome);
        var hit = second.Actions.Single(a => a.Action.Id == "attack2");
        Assert.Equal(8, hit.Production!.Combat!.OpeningStrike.Damage);
        Assert.Equal(6, hit.Production.Combat.OpeningShieldAbsorbed);
        Assert.Equal(2, hit.Production.Combat.OpeningHealthLost);
        Assert.Equal(13, second.Checkpoints.Last().State.Characters["guardian"].EffectiveDefense);
        // Explicit position fixture between rounds: no path, movement budget or movement success is claimed.
        scenario.Characters["colossus"].SetTacticalPosition(new(1, 1));
        var third = runner.RunRound(new(3, 2,
            new("enemy", new ScenarioAction("attack3", ScenarioActionKind.Attack, "enemy", "guardian")),
            Early("colossus", "c3"), Early("healer", "h3"), Early("guardian", "g3")));
        Assert.Equal(ScenarioOutcome.Completed, third.Outcome);
        Assert.Equal(12, third.Actions.Single(a => a.Action.Id == "attack3").Production!.Combat!.OpeningStrike.Damage);
        var end = third.Checkpoints.Last().State.Characters["guardian"];
        Assert.Equal(11, end.EffectiveDefense); Assert.Equal(15, end.EffectiveResistance); Assert.Empty(end.Modifiers);
        Assert.Equal(11, end.BaseStats.Defense);
        Assert.Equal(2, third.Events.Count(e => e.Type == ScenarioEventType.ModifierExpired));
    }

    [Fact]
    public void ShieldManagementRefreshTransferAndOwnerDeathRemainIndependentOfRoundReset()
    {
        var scenario = new CombatScenarioBuilder().AtPhase(RoundPhase.Shop)
            .AddCharacter("guardian", "gentle_guardian", 1, new(0, 0)).AddCharacter("ally", "nigel", 1, new(1, 0))
            .AddCharacter("enemy", "gravebound_colossus", 2, new(6, 6)).Build();
        var shields = new SanctuaryShieldManager(); var guardian = scenario.Characters["guardian"]; var ally = scenario.Characters["ally"];
        shields.Apply(guardian, ally); ally.TakeDamage(2); Assert.Equal(4, ally.ShieldHP);
        shields.Apply(guardian, ally); Assert.Equal(6, ally.ShieldHP);
        shields.Apply(guardian, guardian); Assert.Equal(0, ally.ShieldHP);
        shields.Apply(guardian, ally); guardian.TakeDamage(int.MaxValue);
        var result = new MultiRoundScenarioRunner(scenario).Run(new(1, 1, Early("ally", "a1"), Early("enemy", "e1")),
            new(2, 2, Early("enemy", "e2"), Early("ally", "a2")));
        Assert.Null(result.Interruption); Assert.Equal(6, result.After.Characters["ally"].ShieldHP);
        Assert.Equal(0, result.After.Characters["guardian"].CurrentHealth);
    }

    [Fact]
    public void WallAndIceSurviveCasterDeathsUntilFollowingCleanup()
    {
        var scenario = new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true)
            .AddCharacter("tank", "gravebound_colossus", 1, new(5, 5))
            .AddCharacter("archer", "beothuk_spirit_guardian", 1, new(9, 9))
            .AddCharacter("enemy", "nigel", 2, new(10, 3)).Build();
        scenario.Map.TerrainPlacementConfirmed = true;
        var runner = new MultiRoundScenarioRunner(scenario);
        var first = runner.RunRound(new(1, 1,
            new("tank", new ScenarioAction("wall", ScenarioActionKind.Ability, "tank", abilityId: "wailing_rampart", tiles: new[] { new GridPosition(5, 6), new(6, 6), new(7, 6) })),
            Early("enemy", "e1"),
            new("archer", new ScenarioAction("ice", ScenarioActionKind.Ability, "archer", abilityId: "spirit_frost", direction: new(-1, 0)))));
        Assert.Equal(ScenarioOutcome.Completed, first.Outcome);
        Assert.Single(first.Checkpoints.Last().State.Walls); Assert.Single(first.Checkpoints.Last().State.Ice);
        scenario.Characters["tank"].TakeDamage(int.MaxValue); scenario.Characters["archer"].TakeDamage(int.MaxValue);
        var second = runner.RunRound(new(2, 2, Early("enemy", "e2")));
        var start = second.Checkpoints.Single(c => c.Name == "After round-start processing").State;
        Assert.Single(start.Walls); Assert.Single(start.Ice);
        Assert.Empty(second.Checkpoints.Last().State.Walls); Assert.Empty(second.Checkpoints.Last().State.Ice);
        Assert.Equal(2, second.Events.Count(e => e.Type == ScenarioEventType.TerrainExpired));
        Assert.Empty(scenario.Walls.ExpireAtRoundEnd(2, RoundPhase.Cleanup));
        Assert.Empty(scenario.Ice.ExpireAtRoundEnd(2, RoundPhase.Cleanup));
        var third = runner.RunRound(new(3, 2, Early("enemy", "e3")));
        Assert.DoesNotContain(third.Events, e => e.Type == ScenarioEventType.TerrainExpired);
    }

    [Fact]
    public void RoarAllowsUtilityThenRequiredAttackAndExpiresBeforeNextRoundTargeting()
    {
        var scenario = new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true)
            .AddCharacter("nigel", "nigel", 1, new(5, 5))
            .AddCharacter("enemy", "hooded_assassin", 2, new(6, 5))
            .AddCharacter("other", "time_weaver", 1, new(6, 6))
            .WithAttack("nigel", "axe", new(AttackType.Physical, WeaponCategory.Axe, 0, 100, 1, 1))
            .WithAttack("enemy", "sword", new(AttackType.Physical, WeaponCategory.Sword, 0, 100, 1, 1))
            .WithAttack("other", "scroll", new(AttackType.Magical, WeaponCategory.MagicScroll, 0, 100, 1, 2))
            .Accuracy(Enumerable.Repeat(0, 16).ToArray()).Build();
        var runner = new MultiRoundScenarioRunner(scenario);
        var first = runner.RunRound(new(1, 1,
            new("nigel", new ScenarioAction("roar", ScenarioActionKind.Ability, "nigel", abilityId: "provoking_roar")),
            new("enemy", new ScenarioAction("utility", ScenarioActionKind.ConfirmedUtility, "enemy", confirmedLegal: true),
                new ScenarioAction("secondUtility", ScenarioActionKind.ConfirmedUtility, "enemy", confirmedLegal: true),
                new ScenarioAction("wrong", ScenarioActionKind.Attack, "enemy", "other", requiredAttack: new("nigel", true, true, true)),
                new ScenarioAction("required", ScenarioActionKind.Attack, "enemy", "nigel", requiredAttack: new("nigel", true, true, true))),
            Early("other", "o1")));
        Assert.Equal(ScenarioOutcome.Completed, first.Outcome);
        Assert.Equal(ScenarioOutcome.Completed, first.Actions.Single(a => a.Action.Id == "utility").Outcome);
        Assert.Equal(ScenarioOutcome.Rejected, first.Actions.Single(a => a.Action.Id == "secondUtility").Outcome);
        Assert.Equal("MustAttackSource", first.Actions.Single(a => a.Action.Id == "wrong").Reason);
        Assert.Equal(ScenarioOutcome.Completed, first.Actions.Single(a => a.Action.Id == "required").Outcome);
        Assert.Single(first.Events, e => e.Type == ScenarioEventType.TauntExpired);
        Assert.Empty(first.Checkpoints.Last().State.ActiveTaunts);
        var second = runner.RunRound(new(2, 2, new("enemy", new ScenarioAction("normal", ScenarioActionKind.Attack, "enemy", "other")),
            Early("nigel", "n2"), Early("other", "o2")));
        Assert.Equal(ScenarioOutcome.Completed, second.Outcome);
        Assert.Equal(ScenarioOutcome.Completed, second.Actions.Single(a => a.Action.Id == "normal").Outcome);
    }

    [Fact]
    public void SentinelPreparationUsesProductionExecutorAndCleanupRetiresIt()
    {
        var scenario = SentinelSetup().Build(); var runner = new MultiRoundScenarioRunner(scenario);
        var result = runner.RunRound(new(1, 1,
            new("tank", new ScenarioAction("prepare", ScenarioActionKind.Ability, "tank", abilityId: "impaling_sentinel",
                reactionLimit: SentinelReactionLimit.ProvisionalOnePerPreparation)), Early("enemy", "e1")));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome);
        var use = result.Actions.Single(a => a.Action.Id == "prepare");
        Assert.True(use.Production!.AbilityFinalized); Assert.True(use.After.MainActionUsed);
        Assert.Equal(3, use.Production.ReadyRound);
        Assert.Equal(SentinelPreparationStatus.Prepared, Assert.Single(use.After.SentinelPreparations).Status);
        Assert.Equal(SentinelPreparationStatus.Expired, Assert.Single(result.Checkpoints.Last().State.SentinelPreparations).Status);
        Assert.Single(result.Events, e => e.Type == ScenarioEventType.SentinelRetired);
        Assert.Empty(scenario.Runner.Draws);
    }

    [Fact]
    public void IsolatedSentinelMovementServiceUsesScriptedRngAndRejectsDuplicateEvent()
    {
        var scenario = SentinelSetup().Accuracy(0, 0).Build();
        var runner = new MultiRoundScenarioRunner(scenario);
        runner.BeginRound(new(1, 1));
        runner.RunActivations(new[] { new ScenarioActivationPlan("tank", new ScenarioAction("prepare", ScenarioActionKind.Ability, "tank", abilityId: "impaling_sentinel", reactionLimit: SentinelReactionLimit.ProvisionalOnePerPreparation)) });
        var enemy = scenario.Characters["enemy"]; var tank = scenario.Characters["tank"];
        scenario.Activations.StartActivation(enemy);
        // An external, already-committed single movement step. This is not pathfinding or a player movement adapter.
        var from = enemy.TacticalPosition!.Value; enemy.SetTacticalPosition(new(4, 5));
        var movement = new SentinelMovementEvent("entry", enemy.Id, enemy.TeamId, from, enemy.TacticalPosition.Value, SentinelMovementKind.Ordinary, 1, true);
        var lance = scenario.AttackInputs["tank"].Configuration;
        var reactions = new ImpalingSentinelReactionResolver(scenario.Sentinel, new(new(new HitRoller(scenario.Runner.Accuracy))));
        var teleport = new SentinelMovementEvent("teleport", enemy.Id, enemy.TeamId, from, enemy.TacticalPosition.Value, SentinelMovementKind.Teleport, 1, true);
        Assert.Equal(ImpalingSentinelReason.SpecialMovementUnresolved,
            new ImpalingSentinelTriggerEvaluator(scenario.Sentinel).Evaluate(tank.Id, teleport, lance, scenario.Map).RejectionReason);
        var request = new CombatExchangeRequest(tank, enemy, AttackType.Physical, 4, WeaponCategory.Lance, WeaponCategory.Axe,
            100, AttackType.Physical, 0, 100, 1, 1, 1, true, false, true);
        var result = reactions.Execute(tank.Id, movement, lance, request, scenario.Map);
        Assert.True(result.Resolved); Assert.False(result.MovementMayContinue);
        Assert.False(result.Combat!.FollowUpOccurred); Assert.True(result.Combat.CounterattackOccurred);
        Assert.False(reactions.Execute(tank.Id, movement, lance, request, scenario.Map).Resolved);
        Assert.Equal(2, scenario.Runner.Draws.Count);
        Assert.Equal(1, scenario.Sentinel.GetPreparation(tank.Id)!.ReactionCount);
        scenario.Activations.CompleteActivation();
        Assert.Equal(ScenarioOutcome.Completed, runner.CompleteRound().Outcome);
        // The direct service call is deliberately an isolated fixture: no fabricated player action or combat events are added.
        Assert.Equal(0, runner.Result.Metrics.AttacksAttempted);
    }

    [Fact]
    public void PoisonDefeatRejectsScheduledActorButSurvivorsAndLaterRoundsContinue()
    {
        var scenario = Quiet().WithHealth("nigel", 1).WithStatus("nigel", StatusType.Poison)
            .WithCooldown("nigel", "provoking_roar", 1, 3).Build();
        var result = new MultiRoundScenarioRunner(scenario).Run(QuietRound(1), QuietRound(2));
        Assert.Null(result.Interruption); Assert.Equal(2, result.Metrics.RoundsExecuted);
        Assert.Equal(0, result.After.Characters["nigel"].CurrentHealth);
        Assert.Equal(4, result.After.Characters["nigel"].Cooldowns["provoking_roar"].ReadyRound);
        Assert.Equal(1, result.Metrics.Defeats);
        Assert.Equal(2, result.Metrics.RejectedActions);
        Assert.Equal(2, result.Metrics.ActivationsCompleted);
    }

    [Fact]
    public void UnresolvedOverlapInterruptsWithoutFabricatingDamageOrAdvancingCleanup()
    {
        var scenario = new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true)
            .AddCharacter("nigel", "nigel", 1, new(6, 5)).AddCharacter("guardian", "gentle_guardian", 2, new(7, 5))
            .AddCharacter("a", "gravebound_colossus", 2, new(5, 5)).AddCharacter("b", "gravebound_colossus", 2, new(6, 6))
            .WithAttack("nigel", "axe", new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1))
            .WithAttack("guardian", "scroll", new(AttackType.Magical, WeaponCategory.MagicScroll, 3, 100, 1, 2)).Build();
        var result = new MultiRoundScenarioRunner(scenario).Run(new(1, 1, new ScenarioActivationPlan("nigel", new ScenarioAction("overlap", ScenarioActionKind.Attack, "nigel", "guardian"))), QuietRound(2));
        var round = Assert.Single(result.Rounds); Assert.Equal(ScenarioOutcome.Unresolved, round.Outcome);
        Assert.Contains("OverlapUnresolved", result.Interruption);
        Assert.Equal(0, result.Metrics.HPDamage); Assert.Equal(1, result.Metrics.UnresolvedActions);
        Assert.Equal(RoundPhase.AlternatingActivations, result.After.Phase);
        Assert.Empty(scenario.Runner.Draws);
    }

    [Fact]
    public void RandomFailureStopsSimulationPreservesPartialDamageAndCannotBeRetried()
    {
        var scenario = Duel().Accuracy(0).Build(); var runner = new MultiRoundScenarioRunner(scenario);
        var result = runner.Run(DuelRound(1), DuelRound(2));
        Assert.Equal(ScenarioOutcome.PartiallyApplied, Assert.Single(result.Rounds).Outcome);
        Assert.Equal(61, result.After.Characters["tank"].CurrentHealth);
        Assert.Equal(3, result.Metrics.HPDamage); Assert.Equal(1, result.Metrics.PartiallyAppliedActions);
        Assert.Equal(0, result.Metrics.Hits);
        Assert.Throws<InvalidOperationException>(() => runner.RunRound(DuelRound(1)));
        var action = result.Rounds[0].Actions.Last().Action;
        Assert.Equal("ActionAlreadyAttempted", scenario.Runner.Execute(action).Reason);
        Assert.Equal(2, scenario.Runner.Draws.Count);
    }

    [Fact]
    public void IdenticalPlansHaveIdenticalEventsSnapshotsMetricsAndIndependentState()
    {
        var builder = Duel(); var first = builder.Build(); var second = builder.Build();
        var a = new MultiRoundScenarioRunner(first).Run(DuelRound(1), DuelRound(2));
        var b = new MultiRoundScenarioRunner(second).Run(DuelRound(1), DuelRound(2));
        Assert.Equal(a.After.ToCanonicalJson(), b.After.ToCanonicalJson());
        Assert.Equal(a.Events, b.Events); Assert.Equal(a.Metrics, b.Metrics);
        Assert.Equal(a.Rounds.SelectMany(r => r.RandomDraws), b.Rounds.SelectMany(r => r.RandomDraws));
        var prior = a.Rounds[0].Checkpoints[0].State.ToCanonicalJson();
        first.Characters["tank"].TakeDamage(1);
        Assert.Equal(prior, a.Rounds[0].Checkpoints[0].State.ToCanonicalJson());
        Assert.NotEqual(first.Characters["tank"].CurrentHealth, second.Characters["tank"].CurrentHealth);
        Assert.Equal(Enumerable.Range(1, a.Events.Count).Select(i => (long)i), a.Events.Select(e => e.Sequence));
        Assert.Throws<NotSupportedException>(() => ((IList<ScenarioRoundResult>)a.Rounds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ScenarioCheckpoint>)a.Rounds[0].Checkpoints).Clear());
    }

    [Fact]
    public void DuplicateProcessorsCannotBypassLifecycleGuards()
    {
        var scenario = Quiet().WithStatus("tank", StatusType.Burn, 2).WithStatus("tank", StatusType.Poison).Build();
        var life = scenario.Lifecycle;
        life.AdvancePhase(); new PoisonBatchProcessor(life).Process(scenario.Characters.Values);
        var afterPoison = scenario.Capture().ToCanonicalJson();
        Assert.Throws<InvalidOperationException>(() => new PoisonBatchProcessor(life).Process(scenario.Characters.Values));
        Assert.Equal(afterPoison, scenario.Capture().ToCanonicalJson());
        life.AdvancePhase(); new DelayedAttackProcessor(scenario.DelayedAttacks, life).Process(scenario.Characters.Values);
        Assert.Throws<InvalidOperationException>(() => new DelayedAttackProcessor(scenario.DelayedAttacks, life).Process(scenario.Characters.Values));
        life.AdvancePhase(); new TimedStatusBatchProcessor(life).Process(scenario.Characters.Values);
        Assert.Throws<InvalidOperationException>(() => new TimedStatusBatchProcessor(life).Process(scenario.Characters.Values));
        Assert.Equal(1, scenario.Characters["tank"].Statuses.TimedStatuses[StatusType.Burn]);
    }

    [Fact]
    public void CleanupAndRoundCannotBeReplayedByReplacingTheHarnessRunner()
    {
        var scenario = Quiet().Build(); var runner = new MultiRoundScenarioRunner(scenario);
        Assert.Equal(ScenarioOutcome.Completed, runner.RunRound(QuietRound(1)).Outcome);
        var before = scenario.Capture().ToCanonicalJson();
        var repeat = new MultiRoundScenarioRunner(scenario).RunRound(QuietRound(1));
        Assert.Equal(ScenarioOutcome.Rejected, repeat.Outcome);
        Assert.Equal(before, scenario.Capture().ToCanonicalJson());
        Assert.DoesNotContain(repeat.Phases, p => p.Phase == RoundPhase.Cleanup);
        Assert.Throws<InvalidOperationException>(() => runner.CompleteRound());
    }

    [Fact]
    public void IncompleteActivationScriptDoesNotInventEndsOrSkipEligibleActors()
    {
        var scenario = Quiet().Build();
        var result = new MultiRoundScenarioRunner(scenario).Run(new ScenarioRoundPlan(1, 1, new ScenarioActivationPlan("nigel")));
        Assert.Contains("ActivationPlanIncomplete", result.Interruption);
        Assert.Equal("nigel", result.After.ActiveCharacterId);
        Assert.False(result.After.Characters["nigel"].HasActed);
        var other = Quiet().Build();
        var missing = new MultiRoundScenarioRunner(other).Run(new ScenarioRoundPlan(1, 1, Early("nigel", "end")));
        Assert.Contains("RoundPlanIncomplete", missing.Interruption);
        Assert.Equal(RoundPhase.AlternatingActivations, missing.After.Phase);
    }

    [Fact]
    public void WrongTeamAndDuplicateActivationAreRejectedWithoutChangingAlternation()
    {
        var scenario = Quiet().Build();
        var result = new MultiRoundScenarioRunner(scenario).Run(new ScenarioRoundPlan(1, 1,
            Early("tank", "wrong"), Early("nigel", "n1"), Early("nigel", "duplicate"), Early("tank", "t1")));
        Assert.Null(result.Interruption); Assert.Equal(2, result.Metrics.RejectedActions);
        Assert.Equal(new[] { "nigel", "tank" }, result.Events.Where(e => e.Type == ScenarioEventType.ActivationStarted).Select(e => e.ActorId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidRoundNumbersAreProgrammingErrors(int round)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioRoundPlan(round, 1));

    [Fact]
    public void WrongInitialPhaseSkippedRoundAndInvalidStartingTeamDoNotMutateState()
    {
        var scenario = Quiet().AtPhase(RoundPhase.AlternatingActivations).Build();
        var before = scenario.Capture().ToCanonicalJson();
        Assert.Equal(ScenarioOutcome.Rejected, new MultiRoundScenarioRunner(scenario).RunRound(QuietRound(1)).Outcome);
        Assert.Equal(before, scenario.Capture().ToCanonicalJson());
        var fresh = Quiet().Build();
        Assert.Equal(ScenarioOutcome.Rejected, new MultiRoundScenarioRunner(fresh).RunRound(QuietRound(2)).Outcome);
        Assert.Equal(ScenarioOutcome.Rejected, new MultiRoundScenarioRunner(fresh).RunRound(new(1, 99)).Outcome);
    }

    [Fact]
    public void MissingDelayedTargetRejectsPhaseBeforeDamageAndQueueConsumption()
    {
        var scenario = Quiet().Build();
        scenario.DelayedAttacks.Schedule(new("missing", "nigel", "not_in_roster", 1, 1, StoredStrike(5)));
        var result = new MultiRoundScenarioRunner(scenario).Run(QuietRound(1));
        Assert.Equal(ScenarioOutcome.UnexpectedFailure, Assert.Single(result.Rounds).Outcome);
        Assert.Equal(52, result.After.Characters["nigel"].CurrentHealth);
        Assert.Single(result.After.DelayedAttacks);
        Assert.Equal(RoundPhase.DelayedAttacks, result.After.Phase);
    }

    [Fact]
    public void PoisonDeathCancelsFutureDelayedAttacksAndDoesNotCancelOtherRecipients()
    {
        var scenario = Quiet().WithHealth("nigel", 1).WithStatus("nigel", StatusType.Poison)
            .WithDelayedAttack(new("dead_target", "tank", "nigel", 1, 3, StoredStrike(10)))
            .WithDelayedAttack(new("survivor", "nigel", "tank", 1, 1, StoredStrike(5))).Build();
        var result = new MultiRoundScenarioRunner(scenario).Run(QuietRound(1));
        Assert.Null(result.Interruption); Assert.Empty(result.After.DelayedAttacks);
        Assert.Equal(59, result.After.Characters["tank"].CurrentHealth);
        Assert.Contains(result.Events, e => e.Type == ScenarioEventType.DelayedAttackCancelled && e.Detail == "dead_target");
        Assert.Equal(1, result.Metrics.Defeats);
    }

    [Fact]
    public void ZeroIntervalHealingStillRequiresANewActivationAndPreparationResetsHealingHistory()
    {
        var scenario = new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true)
            .AddCharacter("healer", "gentle_guardian", 1, new(5, 5)).AddCharacter("ally", "nigel", 1, new(6, 5))
            .AddCharacter("enemy", "time_weaver", 2, new(9, 9)).WithHealth("ally", 10).Build();
        ScenarioRoundPlan Plan(int round) => new(round, 1,
            new("healer", new ScenarioAction($"heal{round}", ScenarioActionKind.Ability, "healer", "ally", "healing_light"),
                new ScenarioAction($"duplicateHeal{round}", ScenarioActionKind.Ability, "healer", "ally", "healing_light")),
            Early("enemy", $"enemy{round}"), Early("ally", $"ally{round}"));
        var result = new MultiRoundScenarioRunner(scenario).Run(Plan(1), Plan(2));
        Assert.Null(result.Interruption);
        Assert.Equal(2, result.Metrics.CompletedAbilities); Assert.Equal(2, result.Metrics.RejectedActions);
        Assert.Equal(22, result.Metrics.Healing);
        Assert.Equal(32, result.After.Characters["ally"].CurrentHealth);
        Assert.Equal(1, result.After.Characters["ally"].HealsReceived);
        Assert.Equal(2, result.After.Characters["healer"].Cooldowns["healing_light"].ReadyRound);
        Assert.Equal(result.Metrics.StartingHP + 22, result.Metrics.EndingHP);
    }

    [Fact]
    public void OverlappingRoarsKeepFirstSourceUntilCleanup()
    {
        var scenario = new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true)
            .AddCharacter("a", "nigel", 1, new(5, 5)).AddCharacter("b", "nigel", 1, new(5, 6))
            .AddCharacter("x", "time_weaver", 2, new(6, 5)).AddCharacter("y", "time_weaver", 2, new(6, 7))
            .AddCharacter("z", "time_weaver", 2, new(10, 10)).Build();
        var result = new MultiRoundScenarioRunner(scenario).Run(new ScenarioRoundPlan(1, 1,
            new("a", new ScenarioAction("roarA", ScenarioActionKind.Ability, "a", abilityId: "provoking_roar")), Early("z", "zEnd"),
            new("b", new ScenarioAction("roarB", ScenarioActionKind.Ability, "b", abilityId: "provoking_roar")),
            new("x", new ScenarioAction("xEnd", ScenarioActionKind.EndActivation, "x", requiredAttack: new("a", true, true, false))),
            new("y", new ScenarioAction("yEnd", ScenarioActionKind.EndActivation, "y", requiredAttack: new("b", true, true, false)))));
        Assert.Null(result.Interruption);
        var second = result.Rounds.Single().Actions.Single(a => a.Action.Id == "roarB");
        Assert.Equal(new[] { "y" }, second.Production!.TauntedIds);
        Assert.Equal("a", second.After.ActiveTaunts.Single(t => t.RestrictedCharacterId == "x").SourceCharacterId);
        Assert.Equal("b", second.After.ActiveTaunts.Single(t => t.RestrictedCharacterId == "y").SourceCharacterId);
        Assert.Equal(2, result.Events.Count(e => e.Type == ScenarioEventType.TauntExpired));
    }

    [Fact]
    public void DeadSourceReleasesTauntWithoutRequiringInventedAttackLegality()
    {
        var scenario = RoarSetup().Build(); var runner = new MultiRoundScenarioRunner(scenario);
        runner.BeginRound(new(1, 1));
        runner.RunActivations(new[] { new ScenarioActivationPlan("nigel", new ScenarioAction("roar", ScenarioActionKind.Ability, "nigel", abilityId: "provoking_roar")) });
        scenario.Characters["nigel"].TakeDamage(int.MaxValue); // Isolated invalidation fixture, not a fabricated player attack.
        runner.RunActivations(new[] { Early("enemy", "ordinaryEnd") });
        var round = runner.CompleteRound();
        Assert.Equal(ScenarioOutcome.Completed, round.Outcome);
        Assert.Equal(ScenarioOutcome.Completed, round.Actions.Single(a => a.Action.Id == "ordinaryEnd").Outcome);
        Assert.Empty(round.Checkpoints.Last().State.ActiveTaunts);
        Assert.DoesNotContain(round.Events, e => e.Type == ScenarioEventType.TauntExpired);
    }

    [Fact]
    public void TerrainFrostbiteAndMissingSentinelPolicyRemainExplicitlyUnresolved()
    {
        var scenario = new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true)
            .AddCharacter("archer", "beothuk_spirit_guardian", 1, new(5, 5))
            .AddCharacter("enemy", "nigel", 2, new(6, 5)).Build();
        scenario.Map.TerrainPlacementConfirmed = true;
        var result = new MultiRoundScenarioRunner(scenario).Run(new ScenarioRoundPlan(1, 1,
            new ScenarioActivationPlan("archer", new ScenarioAction("ice", ScenarioActionKind.Ability, "archer", abilityId: "spirit_frost", direction: new(1, 0)))));
        Assert.Contains("TerrainFrostbiteRulesUnresolved", result.Interruption);
        Assert.Empty(result.After.Ice); Assert.Empty(result.After.Characters["enemy"].TimedStatuses);
        Assert.Empty(result.After.Characters["archer"].Cooldowns); Assert.False(result.After.MainActionUsed);
        var sentinel = SentinelSetup().Build();
        var unresolved = new MultiRoundScenarioRunner(sentinel).Run(new ScenarioRoundPlan(1, 1,
            new ScenarioActivationPlan("tank", new ScenarioAction("prepare", ScenarioActionKind.Ability, "tank", abilityId: "impaling_sentinel"))));
        Assert.Contains("ReactionLimitUnresolved", unresolved.Interruption);
        Assert.Empty(unresolved.After.SentinelPreparations);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void InvalidRngInterruptsBeforeAnyDamageAndWithoutAutomaticRetry(int roll)
    {
        var scenario = Duel().Accuracy(roll).Build();
        var runner = new MultiRoundScenarioRunner(scenario);
        var result = runner.Run(DuelRound(1), DuelRound(2));
        Assert.Equal(ScenarioOutcome.UnexpectedFailure, Assert.Single(result.Rounds).Outcome);
        Assert.Equal(0, result.Metrics.HPDamage); Assert.Equal(0, result.Metrics.RoundsExecuted);
        Assert.Equal(ScenarioDrawOutcome.Invalid, Assert.Single(scenario.Runner.Draws).Outcome);
        Assert.Equal(RoundPhase.AlternatingActivations, result.After.Phase);
        Assert.Throws<InvalidOperationException>(() => runner.RunRound(DuelRound(1)));
    }

    [Fact]
    public void PhaseInvalidationCannotAllowConfirmedBookkeepingOutsideActivations()
    {
        var scenario = Quiet().AtPhase(RoundPhase.AlternatingActivations).ActiveCharacter("nigel").Build();
        scenario.Lifecycle.AdvancePhase();
        var result = scenario.Runner.Execute(new("move", ScenarioActionKind.ConfirmedMovement, "nigel", confirmedLegal: true));
        Assert.Equal(ScenarioOutcome.Rejected, result.Outcome); Assert.Equal("WrongPhase", result.Reason);
        Assert.False(result.After.MovementUsed);
    }

    [Fact]
    public void InitialDelayedFixturesAreIndependentAndDuplicateIdsAreRejected()
    {
        var attack = new DelayedAttack("pending", "nigel", "tank", 1, 2, StoredStrike(5));
        var builder = Quiet().WithDelayedAttack(attack);
        var first = builder.Build(); var second = builder.Build();
        new MultiRoundScenarioRunner(first).Run(QuietRound(1), QuietRound(2));
        Assert.Empty(first.DelayedAttacks.PendingAttacks);
        Assert.Single(second.DelayedAttacks.PendingAttacks);
        Assert.Equal(64, second.Characters["tank"].CurrentHealth);
        Assert.Throws<ArgumentException>(() => builder.WithDelayedAttack(attack).Build());
        Assert.Single(second.DelayedAttacks.PendingAttacks);
    }

    [Fact]
    public void SmokeAlsoUsesItsProductionFollowingCleanupExpiration()
    {
        var scenario = Quiet().Build();
        scenario.Smoke.Place("fixture_smoke", "nigel", new(5, 5), 1, p => scenario.Map.IsValidPosition(p) == true);
        var result = new MultiRoundScenarioRunner(scenario).Run(QuietRound(1), QuietRound(2));
        Assert.Single(result.Rounds[0].Checkpoints.Last().State.Smoke);
        Assert.Empty(result.After.Smoke);
        Assert.Single(result.Events, e => e.Type == ScenarioEventType.TerrainExpired && e.Detail == "fixture_smoke");
    }

    [Fact]
    public void ExternalPhaseChangeCannotSkipCoordinatorCleanup()
    {
        var scenario = Quiet().Build(); var runner = new MultiRoundScenarioRunner(scenario);
        runner.BeginRound(QuietRound(1)); runner.RunActivations(QuietRound(1).Activations);
        scenario.Lifecycle.AdvancePhase(); // External code must not advance phases behind the coordinator.
        var result = runner.CompleteRound();
        Assert.Equal(ScenarioOutcome.Rejected, result.Outcome);
        Assert.Contains("RoundContextChanged", result.Reason);
        Assert.Equal(RoundPhase.Cleanup, scenario.Lifecycle.CurrentPhase);
        Assert.DoesNotContain(result.Phases, p => p.Phase == RoundPhase.ObjectiveScoring);
    }

    [Fact]
    public void RoundResultsExposeProductionCleanupAndKeepRoundStartEffectsSeparate()
    {
        var scenario = Quiet().WithShield("tank", 6).WithStatus("tank", StatusType.Poison).WithStatus("tank", StatusType.Burn, 2)
            .WithModifier("tank", new("due_defense", "nigel", "tank", TemporaryStat.Defense, 2, 1, 1)).Build();
        var runner = new MultiRoundScenarioRunner(scenario);
        var result = runner.Run(QuietRound(1), QuietRound(2));
        Assert.Null(result.Interruption); Assert.Same(scenario.Cleanup, scenario.Lifecycle.CleanupCoordinator);
        Assert.All(result.Rounds, round => {
            var phase = Assert.Single(round.Phases, p => p.Phase == RoundPhase.Cleanup);
            Assert.Equal(RoundCleanupStatus.Completed, phase.Cleanup!.Status);
            Assert.Equal(round.Round, phase.Cleanup.RequestedRound);
            Assert.Equal(phase.Before.Characters["tank"].CurrentHealth, phase.After.Characters["tank"].CurrentHealth);
            Assert.Equal(phase.Before.Characters["tank"].ShieldHP, phase.After.Characters["tank"].ShieldHP);
            Assert.Equal(phase.Before.Characters["tank"].PoisonTicks, phase.After.Characters["tank"].PoisonTicks);
            Assert.Equal(phase.Before.Characters["tank"].TimedStatuses.ToArray(), phase.After.Characters["tank"].TimedStatuses.ToArray());
        });
        var expired = Assert.Single(result.Events, e => e.Type == ScenarioEventType.ModifierExpired);
        Assert.Equal("due_defense", expired.Detail); Assert.Equal(1, expired.Round); Assert.Equal(RoundPhase.Cleanup, expired.Phase);
        Assert.Equal(2, result.Events.Count(e => e.Type == ScenarioEventType.PoisonResolved));
        Assert.Single(result.Events, e => e.Type == ScenarioEventType.StatusExpired && e.Phase == RoundPhase.TimedStatusUpdate);
        Assert.Empty(scenario.Runner.Draws);
        var events = result.Events.ToArray();
        Assert.Equal(RoundCleanupStatus.RejectedWrongPhase, scenario.Cleanup.Process(2).Status); // The runner has advanced to scoring.
        Assert.Equal(events, result.Events);
    }

    [Fact]
    public void PartialProductionCleanupKeepsKnownEventsAndBlocksPhaseAdvancementAndReplay()
    {
        var scenario = Quiet().AtRound(2)
            .WithModifier("nigel", new("expired_before_failure", "nigel", "nigel", TemporaryStat.Defense, 2, 1, 2)).Build();
        // Deliberate direct-API misuse injects a real service failure, without a fake expiration implementation.
        scenario.Characters["tank"].TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        scenario.Smoke.Place("due_but_unprocessed", "nigel", new(5, 5), 1, p => scenario.Map.IsValidPosition(p) == true);
        var runner = new MultiRoundScenarioRunner(scenario);
        var round = runner.RunRound(QuietRound(2));
        Assert.Equal(ScenarioOutcome.PartiallyApplied, round.Outcome);
        var phase = Assert.Single(round.Phases, p => p.Phase == RoundPhase.Cleanup);
        Assert.Equal(RoundCleanupStatus.PartiallyApplied, phase.Cleanup!.Status);
        Assert.Contains(phase.Cleanup.Services, step => step.Status == CleanupServiceStatus.Failed && step.CharacterId == "tank");
        Assert.Empty(scenario.Characters["nigel"].TemporaryModifiers.Active); Assert.Single(scenario.Smoke.ActiveAreas);
        Assert.Equal(RoundPhase.Cleanup, scenario.Lifecycle.CurrentPhase);
        Assert.Single(round.Events, e => e.Type == ScenarioEventType.ModifierExpired && e.Detail == "expired_before_failure");
        Assert.DoesNotContain(round.Events, e => e.Type == ScenarioEventType.TerrainExpired || e.Type == ScenarioEventType.RoundEnded);
        Assert.Empty(round.RandomDraws);
        var events = round.Events.ToArray();
        Assert.Equal(RoundCleanupStatus.ManualResolutionRequired, scenario.Cleanup.Process(2).Status);
        Assert.Equal(events, round.Events);
        Assert.Throws<InvalidOperationException>(() => runner.RunRound(QuietRound(2)));
        Assert.Equal(ScenarioOutcome.Rejected, new MultiRoundScenarioRunner(scenario).RunRound(QuietRound(2)).Outcome);
        Assert.Single(scenario.Smoke.ActiveAreas);
    }

    private static CombatScenarioBuilder Quiet() => new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true)
        .AddCharacter("nigel", "nigel", 1, new(5, 5)).AddCharacter("tank", "gravebound_colossus", 2, new(6, 5));
    private static CombatScenarioBuilder Duel() => Quiet()
        .WithAttack("nigel", "fixture_axe", new(AttackType.Physical, WeaponCategory.Axe, 0, 100, 1, 1))
        .WithAttack("tank", "fixture_lance", new(AttackType.Physical, WeaponCategory.Lance, 0, 100, 1, 1))
        .Accuracy(Enumerable.Repeat(0, 64).ToArray());
    private static CombatScenarioBuilder RoarSetup() => new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true)
        .AddCharacter("nigel", "nigel", 1, new(5, 5)).AddCharacter("enemy", "time_weaver", 2, new(6, 5));
    private static CombatScenarioBuilder SentinelSetup() => new CombatScenarioBuilder().AtPhase(RoundPhase.Shop).ConfirmTargeting(true)
        .AddCharacter("tank", "gravebound_colossus", 1, new(5, 5)).AddCharacter("enemy", "nigel", 2, new(3, 5))
        .WithAttack("tank", "lance", new(AttackType.Physical, WeaponCategory.Lance, 4, 100, 1, 1))
        .WithAttack("enemy", "axe", new(AttackType.Physical, WeaponCategory.Axe, 0, 100, 1, 1));
    private static ScenarioAction End(string actor, string id) => new(id, ScenarioActionKind.EndActivation, actor);
    private static ScenarioActivationPlan Early(string actor, string id) => new(actor, End(actor, id));
    private static ScenarioRoundPlan QuietRound(int round) => new(round, 1, Early("nigel", $"n{round}"), Early("tank", $"t{round}"));
    private static ScenarioRoundPlan DuelRound(int round) => new(round, 1,
        new("nigel", new ScenarioAction($"nHit{round}", ScenarioActionKind.Attack, "nigel", "tank")),
        new("tank", new ScenarioAction($"tHit{round}", ScenarioActionKind.Attack, "tank", "nigel")));
    private static StrikeResult StoredStrike(int damage, bool hit = true)
    {
        var stats = new CharacterStats(1, 0, 0, 0, 0, 0, 0, 0);
        var random = new ScriptedScenarioRandom("cast_fixture", new[] { hit ? 0 : 99 }, (_, _, _, _) => { });
        return new SingleStrikeResolver(new HitRoller(random)).ResolveMagicalStrike(stats, stats, damage,
            WeaponCategory.MagicScroll, WeaponCategory.MagicScroll, hit ? 100 : 0);
    }
}
