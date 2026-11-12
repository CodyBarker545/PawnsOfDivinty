using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Gameplay;
using Xunit;

namespace PawnsOfDivinity.Tests.Turns;

public class BattlefieldSessionTests
{
    [Fact]
    public void SixRegisteredCharactersHaveStableUniqueBindingsAndLegalDeployment()
    {
        var s = new BattlefieldSession(new Terrain(), Rules());
        Assert.Equal(6, s.Characters.Count);
        Assert.Equal(6, s.Characters.Values.Select(c => c.Definition!.Id).Distinct().Count());
        Assert.Equal(6, s.Characters.Values.Select(c => c.TacticalPosition).Distinct().Count());
        Assert.All(s.Characters.Values, c =>
        {
            Assert.True(BattlefieldGrid.Contains(c.TacticalPosition!.Value));
            var state = new BattlefieldUnitBinding(s, c.Id).Capture();
            Assert.Equal(c.Id, state.Id); Assert.Equal(c.CurrentHealth, state.HP);
            Assert.Equal(c.TacticalPosition, state.Position); Assert.Equal(c.Definition!.Id, state.DefinitionId);
        });
        Assert.Throws<ArgumentException>(() => new BattlefieldUnitBinding(s, "unknown"));
    }

    [Theory]
    [InlineData(0, 0, true)] [InlineData(13, 13, true)]
    [InlineData(14, 13, false)] [InlineData(13, 14, false)] [InlineData(-1, 0, false)]
    public void BoundsAreFourteenByFourteen(int x, int y, bool valid)
    {
        Assert.Equal(14, BattlefieldGrid.Size);
        var s = Duel(); Assert.Equal(valid, s.Map.IsValidPosition(new(x, y)));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(6, 7)] [InlineData(13, 13)]
    public void PureCoordinateMappingMatchesTheCenteredTerrainGrid(int x, int y)
    {
        var cell = new GridPosition(x, y); var world = BattlefieldGrid.CellCenter(cell);
        Assert.Equal(cell, BattlefieldGrid.WorldToCell(world.X, world.Z));
        Assert.Equal(16, (BattlefieldGrid.CellCenter(new(6, 6)).X + BattlefieldGrid.CellCenter(new(7, 7)).X) / 2);
        Assert.False(BattlefieldGrid.Contains(BattlefieldGrid.WorldToCell(25, 16)));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattlefieldGrid.WorldToCell(double.NaN, 0));
    }

    [Theory]
    [InlineData("duplicate-id")] [InlineData("duplicate-position")] [InlineData("outside")]
    [InlineData("unknown-definition")] [InlineData("wrong-team")]
    public void InvalidDeploymentFailsBeforeStartingAPlayableSession(string defect)
    {
        var deployments = new[] {
            new BattlefieldSpawn("actor", "nigel", 1, new(4,6)),
            new BattlefieldSpawn(defect == "duplicate-id" ? "actor" : "target", defect == "unknown-definition" ? "unknown" : "gravebound_colossus",
                defect == "wrong-team" ? 3 : 2, defect == "duplicate-position" ? new(4,6) : defect == "outside" ? new(14,6) : new GridPosition(7,6)) };
        Assert.Throws<ArgumentException>(() => new BattlefieldSession(new Terrain(), Rules(), encounter: deployments));
    }

    [Fact]
    public void InspectionIsNotActivationAndCanInspectOpposingOrDefeatedCharacters()
    {
        var s = Duel(); Ready(s);
        Assert.Equal(BattlefieldOutcome.Completed, s.Select("target").Outcome); Assert.Null(s.Activations.ActiveCharacter);
        Assert.Equal(BattlefieldOutcome.Rejected, s.StartSelectedActivation().Outcome);
        s.Characters["target"].TakeDamage(int.MaxValue);
        Assert.Equal(BattlefieldOutcome.Completed, s.Select("target").Outcome);
        Assert.True(s.Capture("target").Defeated); Assert.Equal(BattlefieldOutcome.Rejected, s.StartSelectedActivation().Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected, s.Select("unknown").Outcome); Assert.Equal("target", s.SelectedCharacterId);
        Assert.True(s.Characters["actor"].CanAct); Assert.False(s.Activations.MainActionUsed);
    }

    [Fact]
    public void BindingReadsLiveStateWhileEarlierSnapshotsStayUnchanged()
    {
        var s = Duel(); var binding = new BattlefieldUnitBinding(s, "actor"); var before = binding.Capture();
        s.Characters["actor"].SetTacticalPosition(new(5,6)); s.Characters["actor"].TakeDamage(7);
        s.Characters["actor"].Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        Assert.Equal(new GridPosition(5,6), binding.Capture().Position);
        Assert.Equal(45, binding.Capture().HP); Assert.Equal(52, before.HP);
        Assert.Empty(before.Statuses); Assert.Single(binding.Capture().Statuses);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)before.Statuses).Add("fake"));
    }

    [Fact]
    public void PreviewConsumesNoPositionBudgetLogOrRandomness()
    {
        var random = new Rolls(); var s = Duel(random: random); Ready(s); Start(s);
        var before = s.Capture("actor"); int log = s.EventLog.Count;
        Assert.Equal(MovementOutcome.Ready, s.PreviewMovement(Move()).Outcome);
        Assert.Equal(before.Position, s.Capture("actor").Position); Assert.Equal(before.HP, s.Capture("actor").HP);
        Assert.False(s.Activations.MovementUsed); Assert.Equal(log, s.EventLog.Count); Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void ExecutedRouteBindsTheActualFinalPositionThenSupportsAProductionAttack()
    {
        var s = Duel(); Ready(s); Start(s);
        var move = s.ExecuteMovement(Move());
        Assert.Equal(MovementOutcome.Completed, move.Outcome);
        Assert.Equal(new GridPosition(6,6), new BattlefieldUnitBinding(s,"actor").Capture().Position);
        Assert.Equal(move.FinalPosition, s.Capture("actor").Position);
        Assert.True(s.Activations.MovementUsed); Assert.True(s.Activations.CanUseMainAction);
        var attack = s.ExecuteAttack("attack", "actor", "target");
        Assert.Equal(BattlefieldOutcome.Completed, attack.Outcome); Assert.NotNull(attack.Combat);
        Assert.Equal(4, attack.Combat!.OpeningStrike.Damage);
        Assert.True(s.Activations.MainActionUsed); Assert.Null(s.Activations.ActiveCharacter);
        Assert.Equal(BattlefieldOutcome.AlreadyProcessed, s.ExecuteAttack("attack", "actor", "target").Outcome);
    }

    [Fact]
    public void RejectedAndStaleMovementLeaveHpShieldsAndBudgetsAlone()
    {
        var terrain = new Terrain(); var s = Duel(terrain: terrain); Ready(s); Start(s);
        Assert.Equal(MovementOutcome.Ready, s.PreviewMovement(Move()).Outcome);
        terrain.Blocked.Add(new(5,6));
        var result = s.ExecuteMovement(Move());
        Assert.Equal(MovementOutcome.Rejected, result.Outcome); Assert.False(s.Activations.MovementUsed);
        Assert.Equal(new GridPosition(4,6), s.Capture("actor").Position); Assert.Equal(52, s.Capture("actor").HP);
        Assert.Equal(0, s.Capture("actor").ShieldHP);
    }

    [Fact]
    public void DevelopmentPolicyIsExplicitAndDisablingItPreservesUnknownRules()
    {
        var s = Duel(rules: new(false,false,null)); Ready(s); Start(s);
        Assert.Contains("DEVELOPMENT FIXTURES", s.Rules.Description);
        Assert.Equal(MovementRuleAuthority.ProvisionalFixture, s.Map.RuleAuthority);
        Assert.Equal("MovementCostUnresolved", s.PreviewMovement(Move()).Reason);
        s.Characters["target"].SetTacticalPosition(new(5,6));
        Assert.Equal(BattlefieldOutcome.Unresolved, s.PreviewAttack("actor","target").Outcome);
        Assert.False(s.Activations.MainActionUsed);
    }

    [Fact]
    public void UnsupportedElevationIsNotSilentlyGivenAFreeTransition()
    {
        var terrain = new Terrain { Transition = null }; var s = Duel(terrain: terrain); Ready(s); Start(s);
        Assert.Equal(MovementOutcome.UnresolvedRule, s.PreviewMovement(Move()).Outcome);
        Assert.False(s.Activations.MovementUsed);
    }

    [Fact]
    public void SentinelInterruptionReturnsTheReachedTileForVisualAnimation()
    {
        var s = Duel(); Ready(s); Start(s);
        s.Sentinel.Register(new("prepared","target",2,1,s.DevelopmentAttacks["target"],SentinelReactionLimit.ProvisionalOnePerPreparation));
        var result = s.ExecuteMovement(new("reaction", "actor",1,new GridPosition[]{new(4,6),new(5,6),new(6,6),new(6,7)}));
        Assert.Equal(MovementOutcome.Interrupted,result.Outcome); Assert.Equal(new GridPosition(6,6),result.FinalPosition);
        Assert.Equal(result.FinalPosition, new BattlefieldUnitBinding(s,"actor").Capture().Position);
        Assert.Equal(3,result.TraversedPath.Count); Assert.Single(result.Reactions); Assert.True(s.Activations.MovementUsed);
        Assert.True(s.Activations.CanUseMainAction); Assert.False(s.RequiresManualRecovery);
        Assert.Equal(MovementOutcome.AlreadyProcessed,s.ExecuteMovement(new("reaction","actor",1,
            new GridPosition[]{new(4,6),new(5,6),new(6,6),new(6,7)})).Outcome);
    }

    [Fact]
    public void AttackPreviewIsReadOnlyAndShieldsReflectRealCombatOutcomes()
    {
        var random = new Rolls(); var s = Duel(random: random); Ready(s); Start(s);
        s.ExecuteMovement(Move()); s.Characters["target"].SetShieldHP(3);
        Assert.Equal(BattlefieldOutcome.Ready,s.PreviewAttack("actor","target").Outcome); Assert.Equal(0,random.Calls);
        var result = s.ExecuteAttack("attack","actor","target");
        Assert.Equal(3,result.Combat!.OpeningShieldAbsorbed);
        Assert.Equal(0,s.Capture("target").ShieldHP);
        Assert.Equal(64-result.Combat.DefenderHealthLost,s.Capture("target").HP);
        Assert.Equal(52-result.Combat.AttackerHealthLost,s.Capture("actor").HP);
        Assert.True(random.Calls>0); Assert.Contains(s.EventLog,e=>e.Contains("Counter"));
    }

    [Theory]
    [InlineData(1,BattlefieldOutcome.Rejected)] [InlineData(2,BattlefieldOutcome.Ready)]
    [InlineData(3,BattlefieldOutcome.Ready)] [InlineData(4,BattlefieldOutcome.Rejected)]
    public void BowTargetingUsesExistingInclusiveRange(int distance, BattlefieldOutcome expected)
    {
        var s = new BattlefieldSession(new Terrain(),Rules(),new Rolls(),new[]{
            new BattlefieldSpawn("actor","beothuk_spirit_guardian",1,new(4,6)),
            new BattlefieldSpawn("target","nigel",2,new(4+distance,6))});
        Ready(s); Start(s); Assert.Equal(expected,s.PreviewAttack("actor","target").Outcome);
    }

    [Fact]
    public void MagicalOrdinaryAttacksUseProductionIntelligenceAndResistance()
    {
        var s = new BattlefieldSession(new Terrain(),Rules(),new Rolls(),new[]{
            new BattlefieldSpawn("actor","time_weaver",1,new(4,6)),new BattlefieldSpawn("target","nigel",2,new(6,6))});
        Ready(s);Start(s);var result=s.ExecuteAttack("magic","actor","target");
        Assert.Equal(BattlefieldOutcome.Completed,result.Outcome);Assert.Equal(13,result.Combat!.OpeningStrike.Damage);
    }

    [Fact]
    public void IllegalTargetsAndDefeatedActorsConsumeNoAttackResources()
    {
        var random=new Rolls();var s=Duel(random:random);Ready(s);Start(s);
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteAttack("far","actor","target").Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteAttack("self","actor","actor").Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteAttack("unknown","actor","missing").Outcome);
        s.Characters["actor"].TakeDamage(int.MaxValue);
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteAttack("dead","actor","target").Outcome);
        Assert.Equal(0,random.Calls);Assert.False(s.Activations.MainActionUsed);
    }

    [Fact]
    public void IncompleteAttackIsNotRolledBackOrReplayedAndTheSessionStopsCommands()
    {
        var random=new Rolls{ThrowAt=2};var s=Duel(random:random);Ready(s);Start(s);s.ExecuteMovement(Move());
        var result=s.ExecuteAttack("partial","actor","target");
        Assert.Equal(BattlefieldOutcome.PartiallyApplied,result.Outcome);Assert.True(s.RequiresManualRecovery);
        Assert.Equal(60,s.Capture("target").HP);Assert.Equal(2,random.Calls);
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteAttack("partial","actor","target").Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.AdvancePhase().Outcome);Assert.Equal(2,random.Calls);
        Assert.Equal(BattlefieldOutcome.Completed,s.Select("target").Outcome);
    }

    [Fact]
    public void AlternationAndPreparationUseOneLifecycleAndProductionCleanup()
    {
        var s=Duel();Ready(s);Start(s);s.ExecuteMovement(Move());
        Assert.Equal(BattlefieldOutcome.Rejected,s.AdvancePhase().Outcome);
        Assert.Equal(BattlefieldOutcome.Completed,s.EndActivation().Outcome);
        s.Select("actor");Assert.Equal(BattlefieldOutcome.Rejected,s.StartSelectedActivation().Outcome);
        s.Select("target");Assert.Equal(BattlefieldOutcome.Completed,s.StartSelectedActivation().Outcome);s.EndActivation();
        Assert.True(s.Activations.IsRoundComplete);
        s.AdvancePhase();Assert.Equal(RoundPhase.Cleanup,s.Lifecycle.CurrentPhase);
        s.AdvancePhase();Assert.Same(s.Cleanup,s.Lifecycle.CleanupCoordinator);Assert.Equal(RoundCleanupStatus.Completed,s.LastCleanup!.Status);
        s.AdvancePhase();Assert.Equal(2,s.Lifecycle.CurrentRound);Ready(s);
        Assert.Equal(2,s.Activations.CurrentRound);Assert.True(s.Capture("actor").CanActivate);
        Assert.Equal(new GridPosition(6,6),s.Capture("actor").Position);
        Assert.Equal(Enum.GetValues<RoundPhase>(),s.ProcessedPhases.Take(9));
        Assert.Equal(BattlefieldOutcome.Completed,s.Select("actor").Outcome);s.StartSelectedActivation();Assert.True(s.Activations.CanMove);
    }

    [Fact]
    public void RoundStartProcessorsApplyRealPoisonAndExpireStatuses()
    {
        var s=Duel();s.Characters["actor"].Statuses.ApplyPoison();s.Characters["actor"].Statuses.ApplyTimedStatus(StatusType.Burn,1);
        s.Characters["actor"].SetShieldHP(1);Ready(s);
        Assert.Equal(51,s.Capture("actor").HP);Assert.Equal(0,s.Capture("actor").ShieldHP);
        Assert.Equal(1,s.Characters["actor"].Statuses.PoisonTicksOccurred);
        Assert.False(s.Characters["actor"].Statuses.HasStatus(StatusType.Burn));
        Assert.Equal(6,s.ProcessedPhases.Count);
    }

    [Fact]
    public void WrongPhaseCannotStartAnActivationAndSessionsDoNotShareRuntimeState()
    {
        var a=Duel();var b=Duel();a.Select("actor");Assert.Equal(BattlefieldOutcome.Rejected,a.StartSelectedActivation().Outcome);
        a.Characters["actor"].TakeDamage(12);a.Characters["actor"].Statuses.ApplyPoison();
        Assert.Equal(52,b.Capture("actor").HP);Assert.False(b.Characters["actor"].Statuses.IsPoisoned);
        Assert.NotSame(a.Characters["actor"],b.Characters["actor"]);Assert.NotSame(a.Lifecycle,b.Lifecycle);
        Assert.Throws<NotSupportedException>(()=>((IDictionary<string,PawnsOfDivinity.Core.Characters.Character>)a.Characters).Clear());
    }

    [Fact]
    public void DirectAttackTargetingKeepsTheExistingSmokeInvisibilityRestriction()
    {
        var s=new BattlefieldSession(new Terrain(),Rules(),new Rolls(),new[]{
            new BattlefieldSpawn("actor","beothuk_spirit_guardian",1,new(4,6)),
            new BattlefieldSpawn("target","hooded_assassin",2,new(7,6))});
        s.Smoke.Place("smoke","target",new(7,6),1,_=>true);Ready(s);Start(s);
        Assert.Equal(BattlefieldOutcome.Rejected,s.PreviewAttack("actor","target").Outcome);
        s.Characters["target"].SmokeReveal.RecordAttack();
        Assert.Equal(BattlefieldOutcome.Ready,s.PreviewAttack("actor","target").Outcome);
        Assert.Equal(20,SmokeVeil.GetEvasionModifier(s.Characters["target"],new(7,6),s.Smoke));
        Assert.False(s.Activations.MainActionUsed);
    }

    private static BattlefieldDevelopmentRules Rules()=>new(true,true,true);
    private static BattlefieldSession Duel(Terrain? terrain=null, BattlefieldDevelopmentRules? rules=null, Rolls? random=null)
        =>new(terrain??new Terrain(),rules??Rules(),random??new Rolls(),new[]{
            new BattlefieldSpawn("actor","nigel",1,new(4,6)),new BattlefieldSpawn("target","gravebound_colossus",2,new(7,6))});
    private static MovementActionRequest Move()=>new("move","actor",1,new GridPosition[]{new(4,6),new(5,6),new(6,6)});
    private static void Ready(BattlefieldSession session)
    { for(int i=0;i<6;i++)Assert.Equal(BattlefieldOutcome.Completed,session.AdvancePhase().Outcome);Assert.Equal(RoundPhase.AlternatingActivations,session.Lifecycle.CurrentPhase); }
    private static void Start(BattlefieldSession session){session.Select("actor");Assert.Equal(BattlefieldOutcome.Completed,session.StartSelectedActivation().Outcome);}
    private sealed class Terrain:IBattlefieldTerrain
    {
        public HashSet<GridPosition> Blocked {get;}=new();public bool? Transition {get;set;}=true;
        public bool? IsWalkable(GridPosition cell)=>BattlefieldGrid.Contains(cell)&&!Blocked.Contains(cell);
        public double? Height(GridPosition cell)=>0;
        public bool? PrototypeTransition(GridPosition from,GridPosition to)=>Transition;
    }
    private sealed class Rolls:IRandomRollProvider
    {
        public int Calls{get;private set;}public int? ThrowAt{get;set;}
        public int NextRoll(){Calls++;if(Calls==ThrowAt)throw new InvalidOperationException("Scripted accuracy exhausted");return 0;}
    }
}
