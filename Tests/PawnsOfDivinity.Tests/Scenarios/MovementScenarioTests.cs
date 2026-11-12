using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Scenarios;

public class MovementScenarioTests
{
    [Fact]
    public void MovementThenAttackUsesTheUpdatedAuthoritativePosition()
    {
        var s = Duel().ActiveCharacter("actor").Build(); EnableFlat(s);
        var move = s.Runner.Execute(Move("move", "actor", new(1, 1), new(2, 1), new(3, 1)));
        Assert.Equal(ScenarioOutcome.Completed, move.Outcome);
        Assert.Equal(new GridPosition(3, 1), move.After.Characters["actor"].Position);
        Assert.True(move.After.MovementUsed); Assert.False(move.After.MainActionUsed);
        var attack = s.Runner.Execute(new("attack", ScenarioActionKind.Attack, "actor", "tank"));
        Assert.Equal(ScenarioOutcome.Completed, attack.Outcome); Assert.NotNull(attack.Production!.Combat);
        Assert.True(attack.After.MainActionUsed); Assert.Null(s.Activations.ActiveCharacter);
        Assert.Equal(new GridPosition(1, 1), move.Before.Characters["actor"].Position);
        Assert.Equal(2, move.Events.Count(e => e.Type == ScenarioEventType.MovementStep));
    }

    [Fact]
    public void EnemyWallRejectsTheWholeRouteBeforePositionChanges()
    {
        var s = Duel().StartingTeam(2).ActiveCharacter("tank").Build(); EnableFlat(s);
        s.Map.TerrainPlacementConfirmed = true;
        var wall = s.Runner.Execute(new("wall", ScenarioActionKind.Ability, "tank", abilityId: "wailing_rampart",
            tiles: new GridPosition[] { new(2, 1), new(2, 2), new(2, 3) }));
        Assert.Equal(ScenarioOutcome.Completed, wall.Outcome);
        s.Runner.Execute(new("start", ScenarioActionKind.StartActivation, "actor"));
        var result = s.Runner.Execute(Move("blocked", "actor", new(1, 1), new(2, 1), new(3, 1)));
        Assert.Equal(ScenarioOutcome.Rejected, result.Outcome); Assert.Equal("WallBlocksMovement", result.Reason);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
        Assert.Empty(result.RandomDraws);
    }

    [Fact]
    public void OccupancyTracksRealMovementWithoutASecondPositionStore()
    {
        var s = Duel().ActiveCharacter("actor").Build(); EnableFlat(s);
        Assert.Equal(ScenarioOutcome.Completed, s.Runner.Execute(Move("first", "actor", new(1, 1), new(2, 1), new(3, 1))).Outcome);
        Assert.True(s.Map.IsOccupied(new(3, 1))); Assert.False(s.Map.IsOccupied(new(1, 1)));
        s.Runner.Execute(new("end", ScenarioActionKind.EndActivation, "actor"));
        s.Runner.Execute(new("start", ScenarioActionKind.StartActivation, "tank"));
        var result = s.Runner.Execute(Move("second", "tank", new(4, 1), new(3, 1)));
        Assert.Equal("OccupiedDestination", result.Reason);
        Assert.Equal(new GridPosition(4, 1), s.Characters["tank"].TacticalPosition);
        Assert.False(s.Activations.MovementUsed);
    }

    [Fact]
    public void EnemyIceIsExplicitlyUnresolvedWithoutApplyingFrostbiteOrMoving()
    {
        var s = new CombatScenarioBuilder().ConfirmTargeting(true).StartingTeam(2)
            .AddCharacter("actor", "nigel", 1, new(1, 1)).AddCharacter("archer", "beothuk_spirit_guardian", 2, new(4, 1))
            .ActiveCharacter("archer").Build(); EnableFlat(s); s.Map.TerrainPlacementConfirmed = true;
        Assert.Equal(ScenarioOutcome.Completed, s.Runner.Execute(new("ice", ScenarioActionKind.Ability,
            "archer", abilityId: "spirit_frost", direction: new(-1, 0))).Outcome);
        s.Runner.Execute(new("start", ScenarioActionKind.StartActivation, "actor"));
        var result = s.Runner.Execute(Move("entry", "actor", new(1, 1), new(2, 1)));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome);
        Assert.Equal("TerrainFrostbiteRulesUnresolved", result.Reason);
        Assert.Equal(result.Before.ToCanonicalJson(), result.After.ToCanonicalJson());
    }

    [Fact]
    public void SentinelInterruptsThroughProductionServicesAndDoesNotEndTheSurvivorsActivation()
    {
        var s = Duel().ActiveCharacter("actor").Accuracy(0, 99).Build(); EnableFlat(s);
        s.Sentinel.Register(new("prepared", "tank", 2, 1, s.AttackInputs["tank"].Configuration,
            SentinelReactionLimit.ProvisionalOnePerPreparation));
        s.Map.InterruptedMovementConsumesOpportunity = true; // Explicit fixture decision, not canonical balance.
        var action = Move("entry", "actor", new(1, 1), new(2, 1), new(3, 1), new(3, 2));
        Assert.Equal(ScenarioOutcome.Ready, s.Runner.Preview(action).Outcome); Assert.Empty(s.Runner.Draws);
        var result = s.Runner.Execute(action);
        Assert.Equal(ScenarioOutcome.Interrupted, result.Outcome);
        Assert.Equal(new GridPosition(3, 1), s.Characters["actor"].TacticalPosition);
        Assert.Single(result.Production!.Movement!.Reactions);
        Assert.True(s.Activations.MovementUsed); Assert.True(s.Activations.CanUseMainAction);
        int draws = s.Runner.Draws.Count;
        Assert.Equal(ScenarioOutcome.Rejected, s.Runner.Execute(action).Outcome);
        Assert.Equal(draws, s.Runner.Draws.Count);
        Assert.Equal(1, s.Sentinel.GetPreparation("tank")!.ReactionCount);
        Assert.Equal(result.Before.Characters["actor"].CurrentHealth - result.After.Characters["actor"].CurrentHealth,
            result.Events.Where(e => e.TargetId == "actor" && e.Type is ScenarioEventType.HPDamage or ScenarioEventType.HealthChangeObserved).Sum(e => e.Amount ?? 0));
        Assert.True(result.Events.First(e => e.Type == ScenarioEventType.Hit).Sequence >
            result.Events.Last(e => e.Type == ScenarioEventType.MovementStep).Sequence);
    }

    [Fact]
    public void RoundPreparationRestoresMovementButPreservesPosition()
    {
        var s = Duel(new(10, 10)).AtPhase(RoundPhase.Shop).Build(); EnableFlat(s);
        var runner = new MultiRoundScenarioRunner(s);
        ScenarioRoundPlan Round(int number, int fromX) => new(number, 1,
            new("actor", Move($"move{number}", "actor", new(fromX, 1), new(fromX + 1, 1)),
                new($"end{number}", ScenarioActionKind.EndActivation, "actor")),
            new("tank", new ScenarioAction($"tankEnd{number}", ScenarioActionKind.EndActivation, "tank")));
        var result = runner.Run(Round(1, 1), Round(2, 2));
        Assert.Null(result.Interruption); Assert.Equal(2, result.Metrics.RoundsExecuted);
        Assert.Equal(new GridPosition(3, 1), result.After.Characters["actor"].Position);
        Assert.Equal(2, result.Rounds.SelectMany(r => r.Actions).Count(a => a.Production?.Movement?.Outcome == MovementOutcome.Completed));
        Assert.Equal(2, result.Events.Count(e => e.Type == ScenarioEventType.MovementStep));
    }

    [Fact]
    public void DefaultScenarioDoesNotSilentlyApproveTheFixtureCostPolicy()
    {
        var s = Duel().ActiveCharacter("actor").Build();
        var result = s.Runner.Execute(Move("unknown", "actor", new(1, 1), new(2, 1)));
        Assert.Equal(ScenarioOutcome.Unresolved, result.Outcome); Assert.Equal("MovementCostUnresolved", result.Reason);
        Assert.False(s.Activations.MovementUsed);
        var external = s.Runner.Execute(new("external", ScenarioActionKind.ConfirmedMovement, "actor", confirmedLegal: true));
        Assert.Equal(ScenarioOutcome.Completed, external.Outcome);
        Assert.Equal("ConfirmedMovementBookkeeping", external.Production!.ResultType);
        Assert.Equal(new GridPosition(1, 1), s.Characters["actor"].TacticalPosition);
    }

    [Fact]
    public void PreviewDoesNotPromiseThatTheDestinationWillRemainUnoccupied()
    {
        var s = Duel().ActiveCharacter("actor").Build(); EnableFlat(s);
        var action = Move("stale", "actor", new(1, 1), new(2, 1), new(3, 1));
        var preview = s.Runner.Preview(action); Assert.Equal(ScenarioOutcome.Ready, preview.Outcome);
        s.Characters["tank"].SetTacticalPosition(new(3, 1));
        var result = s.Runner.Execute(action);
        Assert.Equal(ScenarioOutcome.Rejected, result.Outcome); Assert.Equal("OccupiedDestination", result.Reason);
        Assert.Equal(new GridPosition(4, 1), preview.Before.Characters["tank"].Position);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AlliedCreatedTerrainKeepsItsExistingNormalPassageRule(bool ice)
    {
        string definition = ice ? "beothuk_spirit_guardian" : "gravebound_colossus";
        var s = new CombatScenarioBuilder().ConfirmTargeting(true)
            .AddCharacter("caster", definition, 1, new(4, 1)).AddCharacter("actor", "nigel", 1, new(1, 1))
            .AddCharacter("enemy", "nigel", 2, new(10, 10)).ActiveCharacter("caster").Build();
        EnableFlat(s); s.Map.TerrainPlacementConfirmed = true;
        var cast = ice ? new ScenarioAction("terrain", ScenarioActionKind.Ability, "caster", abilityId: "spirit_frost", direction: new(-1, 0))
            : new ScenarioAction("terrain", ScenarioActionKind.Ability, "caster", abilityId: "wailing_rampart",
                tiles: new GridPosition[] { new(2, 1), new(2, 2), new(2, 3) });
        Assert.Equal(ScenarioOutcome.Completed, s.Runner.Execute(cast).Outcome);
        s.Runner.Execute(new("enemyStart", ScenarioActionKind.StartActivation, "enemy"));
        s.Runner.Execute(new("enemyEnd", ScenarioActionKind.EndActivation, "enemy"));
        s.Runner.Execute(new("actorStart", ScenarioActionKind.StartActivation, "actor"));
        var result = s.Runner.Execute(Move("pass", "actor", new(1, 1), new(2, 1), new(3, 1)));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome); Assert.Equal(2, result.Production!.Movement!.TraversedCost);
        Assert.Empty(result.After.Characters["actor"].TimedStatuses);
        Assert.Equal(ice ? 1 : 0, s.Ice.ActiveAreas.Count); Assert.Equal(ice ? 0 : 1, s.Walls.ActiveAreas.Count);
    }

    [Fact]
    public void SmokeCoverageDoesNotInventAnOrdinaryMovementPenaltyOrRemoveSmoke()
    {
        var s = Duel().ActiveCharacter("actor").Build(); EnableFlat(s);
        var smoke = s.Smoke.Place("smoke", "actor", new(3, 3), 1, p => s.Map.IsValidPosition(p) == true);
        var result = s.Runner.Execute(Move("throughSmoke", "actor", new(1, 1), new(2, 1), new(3, 1)));
        Assert.Equal(ScenarioOutcome.Completed, result.Outcome); Assert.Equal(2, result.Production!.Movement!.TraversedCost);
        Assert.Same(smoke, Assert.Single(s.Smoke.ActiveAreas)); Assert.Empty(s.Runner.Draws);
    }

    [Fact]
    public void ConfiguredMapBoundsAreUsedInsteadOfEitherPrototypeBoardSize()
    {
        var s = new CombatScenarioBuilder().MapSize(4, 4).AddCharacter("actor", "nigel", 1, new(1, 1))
            .AddCharacter("enemy", "nigel", 2, new(3, 3)).ActiveCharacter("actor").Build(); EnableFlat(s);
        var result = s.Runner.Execute(Move("edge", "actor", new(1, 1), new(2, 1), new(3, 1), new(4, 1)));
        Assert.Equal(ScenarioOutcome.Rejected, result.Outcome); Assert.Equal("InvalidTile", result.Reason);
        Assert.Equal(new GridPosition(1, 1), result.After.Characters["actor"].Position);
    }

    private static void EnableFlat(CombatScenario s) => s.Map.UseProvisionalFlatMovement = true;
    private static ScenarioAction Move(string id, string actor, params GridPosition[] path)
        => new(id, ScenarioActionKind.Movement, actor, tiles: path);
    private static CombatScenarioBuilder Duel(GridPosition? tank = null) => new CombatScenarioBuilder().ConfirmTargeting(true)
        .AddCharacter("actor", "nigel", 1, new(1, 1)).AddCharacter("tank", "gravebound_colossus", 2, tank ?? new(4, 1))
        .WithAttack("actor", "fixture_axe", new(AttackType.Physical, WeaponCategory.Axe, 4, 90, 1, 1))
        .WithAttack("tank", "fixture_lance", new(AttackType.Physical, WeaponCategory.Lance, 3, 90, 1, 1))
        .Accuracy(0, 99, 99);
}
