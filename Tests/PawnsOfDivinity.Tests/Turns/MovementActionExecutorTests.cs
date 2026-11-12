using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Turns;

public class MovementActionExecutorTests
{
    [Fact]
    public void CompletePathUsesRealPositionAndOnlyMovementBudget()
    {
        var s = new Setup(); s.Activations.RecordUtilityAction();
        var result = s.Executor.Execute(s.Request());
        Assert.Equal(MovementOutcome.Completed, result.Outcome);
        Assert.Equal(new GridPosition(3, 1), s.Actor.TacticalPosition);
        Assert.Equal(s.Request().Path, result.TraversedPath);
        Assert.Equal(2, result.TraversedCost);
        Assert.True(s.Activations.MovementUsed); Assert.True(s.Activations.UtilityActionUsed);
        Assert.True(s.Activations.CanUseMainAction); Assert.False(s.Activations.MainActionUsed);
        Assert.Equal(MovementRuleAuthority.ProvisionalFixture, result.PolicyAuthority);
    }

    [Theory]
    [InlineData("unknown", "UnknownActor")]
    [InlineData("defeated", "ActorDefeated")]
    [InlineData("phase", "WrongPhase")]
    [InlineData("inactive", "CharacterNotActive")]
    [InlineData("ended", "CharacterNotActive")]
    [InlineData("used", "MovementUnavailable")]
    [InlineData("round", "StaleRound")]
    public void InvalidActivationDoesNotMoveOrSpend(string condition, string reason)
    {
        var s = new Setup();
        switch (condition)
        {
            case "defeated": s.Actor.TakeDamage(int.MaxValue); break;
            case "phase": s.Lifecycle.AdvancePhase(); break;
            case "inactive": case "ended": s.Activations.CompleteActivation(); break;
            case "used": s.Activations.RecordMovement(); break;
        }
        var result = s.Executor.Execute(new("bad", condition == "unknown" ? "missing" : s.Actor.Id,
            condition == "round" ? 2 : 1, s.Request().Path));
        Assert.Equal(MovementOutcome.Rejected, result.Outcome); Assert.Equal(reason, result.Reason);
        Assert.Equal(new GridPosition(1, 1), s.Actor.TacticalPosition); Assert.Equal(0, s.Random.Calls);
        Assert.False(result.MovementConsumed);
    }

    [Theory]
    [InlineData(0, "PathRequiresOriginAndStep")]
    [InlineData(1, "PathRequiresOriginAndStep")]
    [InlineData(2, "StaleOrigin")]
    [InlineData(3, "NonOrthogonalStep")]
    [InlineData(4, "NonOrthogonalStep")]
    [InlineData(5, "InvalidTile")]
    public void MalformedRoutesAreRejectedBeforeMutation(int route, string reason)
    {
        var s = new Setup();
        GridPosition[] path = route switch
        {
            0 => Array.Empty<GridPosition>(), 1 => new[] { new GridPosition(1, 1) },
            2 => new[] { new GridPosition(0, 1), new GridPosition(1, 1) },
            3 => new[] { new GridPosition(1, 1), new GridPosition(2, 2) },
            4 => new[] { new GridPosition(1, 1), new GridPosition(3, 1) },
            _ => new[] { new GridPosition(1, 1), new GridPosition(1, 0), new GridPosition(1, -1) }
        };
        var result = s.Executor.Execute(new("path", s.Actor.Id, 1, path));
        Assert.Equal(reason, result.Reason); Assert.Equal(MovementOutcome.Rejected, result.Outcome);
        Assert.False(s.Activations.MovementUsed); Assert.Empty(s.Tracker.MovementEvents);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)]
    public void EveryTileIsCheckedNotJustEndpoint(int blockedX)
    {
        var s = new Setup(); s.Map.Blocked.Add(new(blockedX, 1));
        Assert.Equal("InvalidTile", s.Executor.Execute(s.Request()).Reason);
        Assert.Equal(new GridPosition(1, 1), s.Actor.TacticalPosition);
        Assert.False(s.Activations.MovementUsed);
    }

    [Theory]
    [InlineData(2, "OccupiedTraversal")]
    [InlineData(3, "OccupiedDestination")]
    public void LivingOccupantsBlockFixtureTraversalAndDestination(int x, string reason)
    {
        var s = new Setup(); s.Owner.SetTacticalPosition(new(x, 1));
        Assert.Equal(reason, s.Executor.Execute(s.Request()).Reason);
        Assert.False(s.Activations.MovementUsed);
    }

    [Fact]
    public void UnknownCostsAndOccupancyDoNotBecomeCanonicalRules()
    {
        var s = new Setup(); s.Map.Cost = (_, _, _) => null;
        Assert.Equal("MovementCostUnresolved", s.Executor.Execute(s.Request()).Reason);
        s.Map.Cost = (_, _, _) => 1; s.Map.Crossing = null; s.Owner.SetTacticalPosition(new(2, 1));
        Assert.Equal(MovementOutcome.UnresolvedRule, s.Executor.Execute(s.Request()).Outcome);
        Assert.False(s.Activations.MovementUsed);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(int.MaxValue)]
    public void InvalidOrExcessiveCostsNeverOverflowOrConsumeMovement(int cost)
    {
        var s = new Setup(); s.Map.Cost = (_, _, _) => cost;
        var result = s.Executor.Execute(s.Request());
        Assert.Equal(cost > 0 ? "MovementAllowanceExceeded" : "InvalidStepCost", result.Reason);
        Assert.False(s.Activations.MovementUsed);
    }

    [Fact]
    public void FrostbiteChangesAvailableMovementWithoutChangingBaseStats()
    {
        var s = new Setup(); s.Actor.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var result = s.Executor.Execute(new("long", s.Actor.Id, 1, new GridPosition[] { new(1, 1), new(2, 1), new(3, 1), new(3, 2) }));
        Assert.Equal("MovementAllowanceExceeded", result.Reason);
        Assert.Equal(4, s.Actor.BaseStats.Movement); Assert.Equal(2, s.Actor.EffectiveMovement);
        Assert.Equal(MovementOutcome.Completed, s.Executor.Execute(s.Request()).Outcome);
    }

    [Fact]
    public void PreviewIsImmutableAndConsumesNeitherStateNorRandomness()
    {
        var s = new Setup(); s.Prepare(); var request = s.ReactionPath();
        var preview = s.Executor.Preview(request);
        Assert.Equal(MovementOutcome.Ready, preview.Outcome); Assert.Single(preview.PossibleReactionSources);
        Assert.Equal(new GridPosition(1, 1), s.Actor.TacticalPosition);
        Assert.False(s.Activations.MovementUsed); Assert.Equal(0, s.Random.Calls);
        Assert.Empty(s.Tracker.MovementEvents); Assert.Equal(0, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
        var oldFinal = preview.FinalPosition;
        s.Executor.Execute(request);
        Assert.Equal(oldFinal, preview.FinalPosition); Assert.Empty(preview.TraversedPath);
        Assert.Throws<NotSupportedException>(() => ((IList<GridPosition>)preview.ApprovedPath).Add(new(7, 7)));
    }

    [Fact]
    public void ExecuteRechecksBoardAfterPreview()
    {
        var s = new Setup(); Assert.Equal(MovementOutcome.Ready, s.Executor.Preview(s.Request()).Outcome);
        s.Map.Blocked.Add(new(2, 1));
        Assert.Equal(MovementOutcome.Rejected, s.Executor.Execute(s.Request()).Outcome);
        Assert.False(s.Activations.MovementUsed);
    }

    [Fact]
    public void StaleOriginAndMissingPositionAreExplicit()
    {
        var s = new Setup(); s.Actor.SetTacticalPosition(new(0, 1));
        Assert.Equal("StaleOrigin", s.Executor.Execute(s.Request()).Reason);
        s.Map.Blocked.Add(new(0, 1));
        Assert.Equal("InvalidOrigin", s.Executor.Execute(new("invalid", s.Actor.Id, 1, new GridPosition[] { new(0, 1), new(1, 1) })).Reason);
    }

    [Fact]
    public void RequestsCopyPathsAndRejectMalformedProgrammingInputs()
    {
        var path = new List<GridPosition> { new(1, 1), new(2, 1) };
        var request = new MovementActionRequest("id", "actor", 1, path); path.Clear();
        Assert.Equal(2, request.Path.Count);
        Assert.Throws<ArgumentException>(() => new MovementActionRequest(" ", "actor", 1, path));
        Assert.Throws<ArgumentException>(() => new MovementActionRequest("id", " ", 1, path));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovementActionRequest("id", "actor", 0, path));
        Assert.Throws<ArgumentNullException>(() => new MovementActionRequest("id", "actor", 1, null!));
    }

    [Fact]
    public void ReplayAndASecondMovementCannotSpendOrMoveAgain()
    {
        var s = new Setup(); s.Executor.Execute(s.Request());
        var repeat = s.Executor.Execute(s.Request());
        Assert.Equal(MovementOutcome.AlreadyProcessed, repeat.Outcome); Assert.Equal(MovementOutcome.Completed, repeat.PreviousOutcome);
        Assert.Empty(repeat.TraversedPath); Assert.False(repeat.MovementConsumed);
        Assert.Equal("MovementUnavailable", s.Executor.Execute(new("second", s.Actor.Id, 1,
            new GridPosition[] { new(3, 1), new(3, 2) })).Reason);
        Assert.Throws<InvalidOperationException>(() => s.CreateExecutor());
    }

    [Fact]
    public void MatchReplayStateIsIndependent()
    {
        var a = new Setup(); var b = new Setup();
        Assert.Equal(MovementOutcome.Completed, a.Executor.Execute(a.Request()).Outcome);
        Assert.Equal(MovementOutcome.Completed, b.Executor.Execute(b.Request()).Outcome);
        Assert.NotSame(a.Actor, b.Actor);
    }

    [Fact]
    public void ReusingAnIdWithAnotherPathIsAConflictNotAnotherMovement()
    {
        var s = new Setup(); s.Executor.Execute(s.Request());
        var result = s.Executor.Execute(new("move", s.Actor.Id, 1, new GridPosition[] { new(3, 1), new(3, 2) }));
        Assert.Equal(MovementOutcome.Rejected, result.Outcome); Assert.Equal("RequestIdentityConflict", result.Reason);
        Assert.Equal(new GridPosition(3, 1), s.Actor.TacticalPosition); Assert.False(result.MovementConsumed);
    }

    [Theory]
    [InlineData(0, MovementOutcome.Interrupted, 3, 1)]
    [InlineData(99, MovementOutcome.Completed, 3, 2)]
    public void SentinelUsesExistingCombatAndStopsOnlyOnHit(int roll, MovementOutcome outcome, int x, int y)
    {
        var s = new Setup(); s.Prepare(); s.Random.Values.Enqueue(roll); s.Random.Values.Enqueue(99);
        s.Actor.SetShieldHP(2);
        var result = s.Executor.Execute(s.ReactionPath());
        Assert.Equal(outcome, result.Outcome); Assert.Equal(new GridPosition(x, y), result.FinalPosition);
        var reaction = Assert.Single(result.Reactions);
        Assert.Equal(roll == 0, reaction.Result!.Strike!.Hit);
        Assert.Equal(roll == 0 ? 2 : 0, reaction.TargetShieldLost);
        Assert.Equal(1, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
        int calls = s.Random.Calls;
        Assert.Equal(MovementOutcome.AlreadyProcessed, s.Executor.Execute(s.ReactionPath()).Outcome);
        Assert.Equal(calls, s.Random.Calls); Assert.True(s.Activations.MovementUsed);
        Assert.True(s.Activations.CanUseMainAction);
        Assert.Equal(52 - reaction.TargetHPLost, s.Actor.CurrentHealth);
    }

    [Fact]
    public void DefeatStopsAtLastCommittedTileWithoutRevivalOrRollback()
    {
        var s = new Setup(); s.Prepare(); s.Actor.TakeDamage(51); s.Random.Values.Enqueue(0);
        var result = s.Executor.Execute(s.ReactionPath());
        Assert.Equal(MovementOutcome.Interrupted, result.Outcome); Assert.Equal("ActorDefeated", result.Reason);
        Assert.False(result.ActorSurvived); Assert.Equal(new GridPosition(3, 1), s.Actor.TacticalPosition);
        Assert.True(s.Activations.MovementUsed); Assert.False(s.Activations.CanUseMainAction);
    }

    [Fact]
    public void UnresolvedInterruptionCostBlocksBeforeAnyStep()
    {
        var s = new Setup(); s.Prepare(); s.Map.InterruptedMovementConsumesOpportunity = null;
        var result = s.Executor.Execute(s.ReactionPath());
        Assert.Equal(MovementOutcome.UnresolvedRule, result.Outcome);
        Assert.Equal("InterruptedMovementConsumptionUnresolved", result.Reason);
        Assert.Empty(result.TraversedPath); Assert.False(s.Activations.MovementUsed); Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void SimultaneousSentinelsAreNotOrderedByRoster()
    {
        var s = new Setup(); s.Prepare(); s.SecondOwner.SetTacticalPosition(new(3, 2));
        s.Tracker.Register(new("second", s.SecondOwner.Id, 2, 1, s.Lance, SentinelReactionLimit.ProvisionalOnePerPreparation));
        var result = s.Executor.Execute(s.Request());
        Assert.Equal("MultipleReactionsOrderingUnresolved", result.Reason);
        Assert.Equal(new GridPosition(1, 1), s.Actor.TacticalPosition); Assert.False(s.Activations.MovementUsed);
    }

    [Fact]
    public void FailureAfterOneStepPreservesPositionAndCannotResume()
    {
        var s = new Setup(); s.Map.Cost = (actor, _, to) => actor.TacticalPosition == new GridPosition(2, 1) && to == new GridPosition(3, 1)
            ? throw new InvalidOperationException("Map information failed") : 1;
        var result = s.Executor.Execute(s.Request());
        Assert.Equal(MovementOutcome.PartiallyApplied, result.Outcome);
        Assert.Equal(new GridPosition(2, 1), result.FinalPosition); Assert.Equal(1, result.TraversedCost);
        Assert.Equal(2, result.TraversedPath.Count); Assert.True(result.MovementConsumed);
        Assert.Equal(MovementOutcome.AlreadyProcessed, s.Executor.Execute(s.Request()).Outcome);
        Assert.Equal(new GridPosition(2, 1), s.Actor.TacticalPosition);
    }

    [Fact]
    public void ExhaustedRandomnessReportsPartialCommittedStepAndNoReplay()
    {
        var s = new Setup(); s.Prepare(); s.Random.Throw = true;
        var result = s.Executor.Execute(s.ReactionPath());
        Assert.Equal(MovementOutcome.PartiallyApplied, result.Outcome);
        Assert.Equal(new GridPosition(3, 1), result.FinalPosition); Assert.Equal(1, s.Random.Calls);
        Assert.Equal(MovementOutcome.AlreadyProcessed, s.Executor.Execute(s.ReactionPath()).Outcome);
        Assert.Equal(1, s.Random.Calls); Assert.Equal(1, s.Tracker.GetPreparation(s.Owner.Id)!.ReactionCount);
    }

    [Fact]
    public void CounterRollFailureRetainsActualOpeningDamageWithoutFabricatingAnExchange()
    {
        var s = new Setup(); s.Prepare(); s.Random.Values.Enqueue(0); s.Random.ThrowAtCall = 2;
        s.Actor.SetShieldHP(2);
        var result = s.Executor.Execute(s.ReactionPath());
        Assert.Equal(MovementOutcome.PartiallyApplied, result.Outcome);
        var observation = Assert.Single(result.Reactions);
        Assert.Null(observation.Result); Assert.NotNull(observation.Failure);
        Assert.Equal(2, observation.TargetShieldLost);
        Assert.Equal(52 - s.Actor.CurrentHealth, observation.TargetHPLost);
        Assert.True(observation.TargetHPLost > 0);
        Assert.Equal(MovementOutcome.AlreadyProcessed, s.Executor.Execute(s.ReactionPath()).Outcome);
        Assert.Equal(2, s.Random.Calls);
    }

    [Fact]
    public void PreviouslyPublishedStepIdIsRejectedBeforePositionOrBudgetChanges()
    {
        var s = new Setup();
        s.Tracker.RegisterMovementEvent(new("movement:4:move:1", s.Actor.Id, 1, new(1, 1), new(2, 1), SentinelMovementKind.Ordinary, 1, true));
        var result = s.Executor.Execute(s.Request());
        Assert.Equal("MovementEventAlreadyPublished", result.Reason);
        Assert.False(s.Activations.MovementUsed); Assert.Empty(result.TraversedPath);
    }

    [Fact]
    public void MissingOrInvalidReactionInputsFailBeforeMoving()
    {
        var s = new Setup(); s.Prepare(); s.ReactionInputsAvailable = false;
        var result = s.Executor.Execute(s.ReactionPath());
        Assert.Equal("CombatInformationRequired", result.Reason); Assert.Empty(result.TraversedPath);
        Assert.False(s.Activations.MovementUsed); Assert.Equal(0, s.Random.Calls);
        s.ReactionInputsAvailable = true; s.CounterPower = -1;
        result = s.Executor.Execute(s.ReactionPath());
        Assert.Equal(MovementOutcome.Rejected, result.Outcome); Assert.Contains("ArgumentOutOfRangeException", result.Reason);
        Assert.False(s.Activations.MovementUsed); Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void MissingOriginAndMapInformationRemainUnresolved()
    {
        var s = new Setup(placeActor: false);
        Assert.Equal("MissingPosition", s.Executor.Execute(s.Request()).Reason);
        s.Actor.SetTacticalPosition(new(1, 1)); s.Map.ValidKnown = false;
        Assert.Equal(MovementOutcome.UnresolvedRule, s.Executor.Execute(s.Request()).Outcome);
        Assert.False(s.Activations.MovementUsed);
    }

    [Fact]
    public void ExpiredOrDefeatedSentinelsDoNotReact()
    {
        var s = new Setup(); s.Prepare(); s.Owner.TakeDamage(int.MaxValue);
        var result = s.Executor.Execute(s.ReactionPath());
        Assert.Equal(MovementOutcome.Completed, result.Outcome); Assert.Empty(result.Reactions);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void MovementDoesNotChangeOtherRuntimeState()
    {
        var s = new Setup(); s.Actor.SetShieldHP(6); s.Actor.TakeDamage(1);
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Burn, 2); s.Actor.Cooldowns.RecordUse("example", 1, 3);
        int hp = s.Actor.CurrentHealth, shield = s.Actor.ShieldHP;
        var stats = s.Actor.BaseStats;
        Assert.Equal(MovementOutcome.Completed, s.Executor.Execute(s.Request()).Outcome);
        Assert.Equal(hp, s.Actor.CurrentHealth); Assert.Equal(shield, s.Actor.ShieldHP);
        Assert.Same(stats, s.Actor.BaseStats); Assert.Equal(2, s.Actor.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(4, s.Actor.Cooldowns.Cooldowns["example"].ReadyRound);
        Assert.Equal(new GridPosition(4, 1), s.Owner.TacticalPosition); Assert.False(s.Actor.HasActedThisRound);
    }

    [Fact]
    public void PositionDependentSanctuaryFailureIsDetectedBeforeTheReactionStepCommits()
    {
        var s = new Setup(); s.Prepare();
        s.SecondOwner.SetTacticalPosition(new(3, 2)); s.SecondOwner.TakeDamage(int.MaxValue);
        var result = s.Executor.Execute(s.Request());
        Assert.Equal(MovementOutcome.UnresolvedRule, result.Outcome);
        Assert.Contains("SourceDeathUnresolved", result.Reason);
        Assert.Empty(result.TraversedPath); Assert.False(s.Activations.MovementUsed); Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void ApprovedCrossingStillCannotInventAnOccupiedInterruptionPosition()
    {
        var s = new Setup(); s.Owner.SetTacticalPosition(new(2, 1)); s.Map.Crossing = true;
        var result = s.Executor.Execute(s.Request());
        Assert.Equal(MovementOutcome.UnresolvedRule, result.Outcome);
        Assert.Equal("OccupiedCheckpointInterruptionUnresolved", result.Reason);
        Assert.False(s.Activations.MovementUsed);
    }

    [Fact]
    public void ReadOnlyCallbacksCannotReenterMovementExecution()
    {
        var s = new Setup(); MovementActionResult? nested = null;
        s.Map.Cost = (_, _, _) =>
        {
            nested = s.Executor.Execute(new("nested", s.Actor.Id, 1, s.Request().Path));
            return 1;
        };
        Assert.Equal(MovementOutcome.Completed, s.Executor.Execute(s.Request()).Outcome);
        Assert.NotNull(nested); Assert.Equal("MovementReentry", nested.Reason);
        Assert.Equal(new GridPosition(3, 1), s.Actor.TacticalPosition);
        Assert.Equal(2, s.Tracker.MovementEvents.Count);
    }

    [Fact]
    public void ProviderFailureBeforeExecutionIsNotMisreportedAsPartialMovementOrAnUnresolvedRule()
    {
        var s = new Setup(); s.Map.Cost = (_, _, _) => throw new InvalidOperationException("Map adapter unavailable");
        var result = s.Executor.Execute(s.Request());
        Assert.Equal(MovementOutcome.Failed, result.Outcome); Assert.Empty(result.TraversedPath);
        Assert.False(result.MovementConsumed); Assert.False(s.Activations.MovementUsed);
        Assert.Equal(new GridPosition(1, 1), result.FinalPosition);
    }

    private sealed class Map : IOrdinaryMovementMap
    {
        public HashSet<GridPosition> Blocked { get; } = new();
        public Func<Character, GridPosition, GridPosition, int?> Cost { get; set; } = (_, _, _) => 1;
        public bool? Crossing { get; set; } = false;
        public bool ValidKnown { get; set; } = true;
        public MovementRuleAuthority RuleAuthority => MovementRuleAuthority.ProvisionalFixture;
        public string PolicyDescription => "Test-only flat unit steps, blocked living occupants, interrupted opportunity spent.";
        public bool? InterruptedMovementConsumesOpportunity { get; set; } = true;
        public bool? CanTraverseOccupiedTile(Character actor, Character occupant) => Crossing;
        public int? GetStepCost(Character actor, GridPosition from, GridPosition to) => Cost(actor, from, to);
        public bool? IsValidPosition(GridPosition p) => ValidKnown ? p.X >= 0 && p.Y >= 0 && p.X < 14 && p.Y < 14 && !Blocked.Contains(p) : null;
        public bool? CanReact(Character owner, Character mover, SentinelMovementEvent movement) => true;
    }
    private sealed class Random : IRandomRollProvider
    {
        public Queue<int> Values { get; } = new(); public int Calls { get; private set; } public bool Throw { get; set; }
        public int? ThrowAtCall { get; set; }
        public int NextRoll() { Calls++; if (Throw || ThrowAtCall == Calls) throw new InvalidOperationException("No scripted roll"); return Values.Count == 0 ? 99 : Values.Dequeue(); }
    }
    private sealed class Setup : IMovementReactionInputs
    {
        public Character Actor { get; } = CharacterFactory.Create(Nigel.Definition, "actor", 1);
        public Character Owner { get; } = CharacterFactory.Create(GraveboundColossus.Definition, "owner", 2);
        public Character SecondOwner { get; } = CharacterFactory.Create(GraveboundColossus.Definition, "second", 2);
        public RoundLifecycleManager Lifecycle { get; } = new(); public ActivationManager Activations { get; }
        public ImpalingSentinelTracker Tracker { get; } public MovementActionExecutor Executor { get; }
        public Map Map { get; } = new(); public Random Random { get; } = new();
        public AttackConfiguration Lance { get; } = new(AttackType.Physical, WeaponCategory.Lance, 4, 90, 1, 1);
        public bool ReactionInputsAvailable { get; set; } = true;
        public int CounterPower { get; set; }
        public Setup(bool placeActor = true)
        {
            if (placeActor) Actor.SetTacticalPosition(new(1, 1));
            Owner.SetTacticalPosition(new(4, 1)); SecondOwner.SetTacticalPosition(new(10, 10));
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Activations = new(new[] { Actor }, new[] { Owner, SecondOwner }, 1, Lifecycle);
            Tracker = new(Activations, Lifecycle); Activations.StartActivation(Actor); Executor = CreateExecutor();
        }
        public MovementActionExecutor CreateExecutor() => new(Activations, Lifecycle, Map, new(), new(), Tracker, new(new(new HitRoller(Random))), this);
        public MovementActionRequest Request() => new("move", Actor.Id, 1, new GridPosition[] { new(1, 1), new(2, 1), new(3, 1) });
        public MovementActionRequest ReactionPath() => new("reaction", Actor.Id, 1, new GridPosition[] { new(1, 1), new(2, 1), new(3, 1), new(3, 2) });
        public void Prepare() => Tracker.Register(new("prepared", Owner.Id, 2, 1, Lance, SentinelReactionLimit.ProvisionalOnePerPreparation));
        public AttackConfiguration? CurrentLance(string ownerId) => Lance;
        public CombatExchangeRequest? CreateRequest(string ownerId, string moverId, int distance) => !ReactionInputsAvailable ? null : new(
            ownerId == Owner.Id ? Owner : SecondOwner, Actor, AttackType.Physical, 4, WeaponCategory.Lance,
            WeaponCategory.Axe, 90, AttackType.Physical, CounterPower, 0, 1, 1, distance, true, false);
    }
}
