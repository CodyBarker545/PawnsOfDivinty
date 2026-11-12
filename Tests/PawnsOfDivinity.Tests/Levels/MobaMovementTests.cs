using PawnsOfDivinity;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class MobaMovementTests
{
    private static MobaHotseatSession Nigel()
    {var s=new MobaHotseatSession(AuthoredBetaScene.LegacyLayout,2);Assert.Equal(BattlefieldOutcome.Completed,s.Select("nigel").Outcome);return s;}
    private static void Ack(MobaHotseatSession s)
    {var m=s.PendingVisualMovement!;Assert.Equal(BattlefieldOutcome.Completed,s.AcknowledgeVisualMovement(m.RequestId,m.FinalPosition!.Value).Outcome);}
    [Fact] public void NigelHasExactlyFourMovementPointsAndOrthogonalRoutes()
    {
        var s=Nigel();Assert.Equal(4,s.RemainingMovement);Assert.Null(s.Activations.ActiveCharacter);
        var routes=s.ReachableDestinations();Assert.NotEmpty(routes);
        Assert.Contains(routes,r=>r.Value.Cost==4);
        foreach(var route in routes.Values)
        {
            Assert.InRange(route.Cost,1,4);Assert.Equal(route.Cost,route.Cells.Count-1);
            Assert.Equal(s.SelectedCharacter!.TacticalPosition,route.Cells[0]);
            for(int i=1;i<route.Cells.Count;i++)Assert.Equal(1,route.Cells[i-1].ManhattanDistanceTo(route.Cells[i]));
            Assert.All(route.Cells,p=>Assert.True(AuthoredBetaScene.LegacyLayout.Board.Contains(p)));
        }
        Assert.Null(s.PreviewMovement(new(14,10)));
    }
    [Fact] public void PreviewDoesNotSpendMoveOrStartAnActivation()
    {
        var s=Nigel();var before=s.SelectedCharacter!.TacticalPosition;
        var plan=s.PreviewMovement(new(25,10));Assert.NotNull(plan);Assert.Equal(2,plan.Cost);
        Assert.Equal(before,s.SelectedCharacter.TacticalPosition);Assert.Null(s.Activations.ActiveCharacter);
        Assert.False(s.Activations.MovementUsed);Assert.Equal(2,s.CurrentPlayer);
    }
    [Theory] [InlineData(26,9)] [InlineData(28,12)] [InlineData(26,12)] [InlineData(30,10)] [InlineData(27,20)] [InlineData(-1,10)] [InlineData(14,10)]
    public void BlockedOccupiedOutsideAndDistantDestinationsAreRejectedWithoutSpending(int x,int y)
    {
        var s=Nigel();var origin=s.SelectedCharacter!.TacticalPosition;
        Assert.Null(s.PreviewMovement(new(x,y)));
        var result=s.MoveTo(new(x,y));Assert.Equal(MovementOutcome.Rejected,result.Outcome);
        Assert.Equal(origin,s.SelectedCharacter.TacticalPosition);Assert.Null(s.Activations.ActiveCharacter);
        Assert.False(result.MovementConsumed);Assert.False(s.IsPresentationBusy);
    }
    [Fact] public void LivingAlliesAndEnemiesBlockTraversalAsWellAsDestinations()
    {
        var s=Nigel();s.Characters["time_weaver"].SetTacticalPosition(new(26,10));
        s.Characters["hooded_assassin"].SetTacticalPosition(new(28,10));
        foreach(var route in s.ReachableDestinations().Values)
        {Assert.DoesNotContain(new GridPosition(26,10),route.Cells);Assert.DoesNotContain(new GridPosition(28,10),route.Cells);}
        Assert.False(s.MovementMap.CanTraverseOccupiedTile(s.SelectedCharacter!,s.Characters["time_weaver"]));
    }
    [Fact] public void DefeatedUnitsFollowExistingNonOccupyingProductionPolicy()
    {
        var s=Nigel();var other=s.Characters["time_weaver"];other.SetTacticalPosition(new(26,10));other.TakeDamage(int.MaxValue);
        Assert.False(s.MovementMap.IsOccupied(new(26,10)));Assert.NotNull(s.PreviewMovement(new(26,10)));
    }
    [Fact] public void ProductionExecutorCommitsPositionAndKeepsMainActionAvailable()
    {
        var s=Nigel();var actor=s.SelectedCharacter!;var plan=s.PreviewMovement(new(25,10))!;
        var result=s.CommitMovement(plan);
        Assert.Equal(MovementOutcome.Completed,result.Outcome);Assert.IsType<MovementActionExecutor>(s.Lifecycle.MovementExecutor);
        Assert.Equal(plan.Path,result.TraversedPath);Assert.Equal(plan.Cost,result.TraversedCost);
        Assert.Equal(new GridPosition(25,10),actor.TacticalPosition);Assert.True(s.Activations.MovementUsed);
        Assert.True(s.Activations.CanUseMainAction);Assert.False(actor.HasActedThisRound);Assert.Equal(2,s.CurrentPlayer);
        Assert.True(s.MovementMap.IsOccupied(new(25,10)));Assert.False(s.MovementMap.IsOccupied(new(27,10)));
        Assert.Equal(actor.TacticalPosition,s.Capture(actor.Id).Position);Assert.True(s.IsPresentationBusy);
        Ack(s);Assert.Equal(0,s.RemainingMovement);Assert.Empty(s.ReachableDestinations());
        Assert.Equal(BattlefieldOutcome.Completed,s.EndSelectedActivation().Outcome);Assert.Equal(1,s.CurrentPlayer);
    }
    [Fact] public void MovementCannotBeSplitIntoMultipleSegmentsEvenWhenPointsWereUnused()
    {
        var s=Nigel();Assert.Equal(MovementOutcome.Completed,s.MoveTo(new(26,10)).Outcome);Ack(s);
        Assert.Equal(MovementOutcome.Rejected,s.MoveTo(new(25,10)).Outcome);
        Assert.Equal(new GridPosition(26,10),s.SelectedCharacter!.TacticalPosition);Assert.False(s.SelectedCharacter.HasActedThisRound);
    }
    [Fact] public void PresentationLockRejectsDoubleMovesSelectionChangesAndEnd()
    {
        var s=Nigel();var plan=s.PreviewMovement(new(25,10))!;s.CommitMovement(plan);
        Assert.Equal(MovementOutcome.Rejected,s.CommitMovement(plan).Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.Select(null).Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.Select("time_weaver").Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.EndSelectedActivation().Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.AcknowledgeVisualMovement("wrong",new(25,10)).Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.AcknowledgeVisualMovement(s.PendingVisualMovement!.RequestId,new(24,10)).Outcome);
        Assert.True(s.IsPresentationBusy);Ack(s);Assert.False(s.IsPresentationBusy);
    }
    [Fact] public void StartedActorMustFinishBeforeAnotherFriendlyCanTakeControl()
    {
        var s=Nigel();s.MoveTo(new(26,10));Ack(s);s.Select(null);
        Assert.Equal(BattlefieldOutcome.Rejected,s.Select("time_weaver").Outcome);
        Assert.Equal(BattlefieldOutcome.Completed,s.Select("nigel").Outcome);
        Assert.Equal(BattlefieldOutcome.Completed,s.EndSelectedActivation().Outcome);
    }
    [Fact] public void StalePreviewRevalidatesOccupancyAndCanRecoverWithAnotherDestination()
    {
        var s=Nigel();var plan=s.PreviewMovement(new(25,10))!;
        s.Characters["time_weaver"].SetTacticalPosition(new(25,10));
        Assert.Equal(MovementOutcome.Rejected,s.CommitMovement(plan).Outcome);Assert.Null(s.Activations.ActiveCharacter);
        Assert.Equal(MovementOutcome.Completed,s.MoveTo(new(26,10)).Outcome);
    }
    [Fact] public void ForgedCostAndRouteCannotMoveTheDomainCharacter()
    {
        var s=Nigel();var plan=s.PreviewMovement(new(25,10))!;
        Assert.Equal(MovementOutcome.Rejected,s.CommitMovement(plan with {Cost=1}).Outcome);
        Assert.Equal(MovementOutcome.Rejected,s.CommitMovement(plan with {Path=new[]{plan.Origin,plan.Destination}}).Outcome);
        Assert.False(s.Activations.MovementUsed);Assert.Null(s.Activations.ActiveCharacter);
    }
    [Fact] public void OpponentDefeatedUsedAndWrongRoundPlansAreRejected()
    {
        var s=Nigel();var plan=s.PreviewMovement(new(25,10))!;
        Assert.Equal(MovementOutcome.Rejected,s.CommitMovement(plan with {ActorId="hooded_assassin"}).Outcome);
        Assert.Equal(MovementOutcome.Rejected,s.CommitMovement(plan with {Round=2}).Outcome);
        s.SelectedCharacter!.TakeDamage(int.MaxValue);
        Assert.Equal(MovementOutcome.Rejected,s.CommitMovement(plan).Outcome);
    }
    [Fact] public void FrostbiteUsesProductionEffectiveMovementRatherThanBaseMov()
    {
        var s=Nigel();s.SelectedCharacter!.Statuses.ApplyTimedStatus(StatusType.Frostbite,2);
        Assert.Equal(2,s.RemainingMovement);Assert.All(s.ReachableDestinations().Values,r=>Assert.InRange(r.Cost,1,2));
    }
    [Fact] public void UnknownAuthoredTerrainCostIsUnresolvedRatherThanInvented()
    {
        var data=AuthoredBetaScene.LegacyData;
        var layout=new MobaBetaLayout(data with {Elements=data.Elements.Select(e=>e.Id=="tile_26_10"?e with {TerrainTag="unresolved_mud"}:e).ToArray()});
        var s=new MobaHotseatSession(layout,2);s.Select("nigel");
        Assert.Null(s.PreviewMovement(new(26,10)));Assert.Equal(MovementOutcome.UnresolvedRule,s.MoveTo(new(26,10)).Outcome);
        Assert.Null(s.Activations.ActiveCharacter);
    }
    [Fact] public void CastleWallForcesRouteThroughTheActualGate()
    {
        var s=Nigel();s.SelectedCharacter!.SetTacticalPosition(new(24,10));
        var plan=s.PreviewMovement(new(21,10));Assert.NotNull(plan);
        Assert.Contains(new GridPosition(23,10),plan.Path);Assert.Contains(new GridPosition(22,10),plan.Path);
        Assert.Null(s.PreviewMovement(new(23,9)));Assert.All(plan.Path,p=>Assert.True(AuthoredBetaScene.LegacyLayout.Walkable(p)));
    }
    [Fact] public void NewRoundRestoresMovementWithoutResettingRuntimePosition()
    {
        var s=Nigel();s.MoveTo(new(26,10));Ack(s);s.EndSelectedActivation();
        while(s.Round==1){var c=s.Characters.Values.First(c=>s.CanSelect(c.Id));s.Select(c.Id);s.EndSelectedActivation();}
        Assert.Equal(2,s.CurrentPlayer);
        Assert.Equal(BattlefieldOutcome.Completed,s.Select("nigel").Outcome);Assert.Equal(4,s.RemainingMovement);
        Assert.Equal(new GridPosition(26,10),s.SelectedCharacter!.TacticalPosition);Assert.Equal(RoundPhase.AlternatingActivations,s.Lifecycle.CurrentPhase);
    }
    [Fact] public void TiedRoutesAreDeterministicAcrossRepeatedQueries()
    {
        var s=Nigel();var first=s.ReachableDestinations();
        for(int n=0;n<5;n++)foreach(var entry in s.ReachableDestinations())Assert.Equal(first[entry.Key].Cells,entry.Value.Cells);
    }
}
