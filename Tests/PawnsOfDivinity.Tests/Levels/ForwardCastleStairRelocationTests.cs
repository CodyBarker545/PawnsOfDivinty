using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class ForwardCastleStairRelocationTests
{
    private static MobaBetaLayout Map=>AuthoredBetaScene.Cliffside.Value;

    [Theory]
    [InlineData("blue_forward",8,9)]
    [InlineData("red_forward",20,21)]
    public void SouthGateFeedsBothStairColumnsWithoutOwnershipRequirement(string id,int a,int b)
    {
        var map=Map;var castle=map.Castles.Single(c=>c.Id==id);
        Assert.True(castle.Entrances["south"].SetEquals(MobaBetaLayout.Rectangle(a,b,14,14)));
        foreach(int x in new[]{a,b})
        foreach(bool descending in new[]{true,false})
        {
            var session=new MobaHotseatSession(map);
            Assert.Null(session.Control.Castle(id).OwnerTeamId);
            var actor=session.Characters["blue_gravebound_colossus"];
            actor.SetTacticalPosition(new(x,descending?14:17));session.Select(actor.Id);
            var plan=MobaMovementPreview.Capture(session).PlanFor(new(x,descending?17:14));
            Assert.NotNull(plan);Assert.Equal(3,plan.Cost);
            Assert.Contains(new GridPosition(x,15),plan.Path);Assert.Contains(new GridPosition(x,16),plan.Path);
            var result=session.CommitMovement(plan);Assert.Equal(MovementOutcome.Completed,result.Outcome);
            Assert.Equal(plan.Path,result.TraversedPath);session.AcknowledgeVisualMovement(result.RequestId,plan.Destination);
            Assert.Equal(plan.Destination,actor.TacticalPosition);Assert.Null(session.Control.Castle(id).OwnerTeamId);
        }
    }

    [Fact]
    public void AllOldOpeningsAreBlockedAndOnlyFourNewColumnsCrossTheCliff()
    {
        var map=Map;var session=new MobaHotseatSession(map);var actor=session.Characters.Values.First();
        for(int x=0;x<30;x++)
        {
            bool stair=x is 8 or 9 or 20 or 21;
            Assert.Equal(stair,map.Walkable(new(x,15)));
            foreach(int row in new[]{14,15})
            {
                Assert.Equal(stair,session.MovementMap.GetStepCost(actor,new(x,row),new(x,row+1))==1);
                Assert.Equal(stair,session.MovementMap.GetStepCost(actor,new(x,row+1),new(x,row))==1);
            }
        }
        foreach(int x in new[]{4,5,24,25})
        {
            Assert.Equal("cliff",map.Terrain[new(x,15)]);Assert.Equal(0,map.Elevations[new(x,15)]);
            Assert.Equal(-3,map.Elevations[new(x,16)]);
        }
        Assert.Equal(86,map.Obstacles.Count);Assert.Equal(514,map.Reachable(map.Fixtures[0].Cell).Count);
    }

    [Fact]
    public void CaptureTransferAndCampRegionsRemainUnchangedAndAccessibleFromBothSides()
    {
        var map=Map;
        foreach(var (id,left) in new[]{("blue_forward",6),("red_forward",18)})
        {
            var castle=map.Castles.Single(c=>c.Id==id);
            Assert.True(castle.CaptureCells.SetEquals(MobaBetaLayout.Rectangle(left+2,left+3,10,11)));
            int arrival=id=="blue_forward"?7:20;
            Assert.True(castle.DeploymentCells.SetEquals(MobaBetaLayout.Rectangle(arrival,arrival+2,12,13)));
            foreach(string entrance in new[]{"west","east","south"})
            foreach(var landing in castle.DeploymentCells)
            {
                var reachable=map.Reachable(landing);
                Assert.All(castle.Entrances[entrance],p=>Assert.Contains(p,reachable));
                Assert.All(map.Camps.SelectMany(c=>c.Cells),p=>Assert.Contains(p,reachable));
            }
        }
        Assert.All(map.Castles.Where(c=>c.IsHome),c=>Assert.True(c.InitialTeamId is 1 or 2));
        Assert.True(map.Camps.Single(c=>c.Id=="blue_camp").Cells.SetEquals(MobaBetaLayout.Rectangle(8,9,17,18)));
        Assert.True(map.Camps.Single(c=>c.Id=="red_camp").Cells.SetEquals(MobaBetaLayout.Rectangle(20,21,17,18)));
    }

    [Fact]
    public void FastAndSlowUnitsUseNormalMovesAndMirroredGateToFarmCostsMatch()
    {
        var map=Map;
        foreach(var id in new[]{"hooded_assassin","blue_gravebound_colossus"})
        foreach(int col in new[]{8,21})
        {
            var session=new MobaHotseatSession(map);var actor=session.Characters[id];
            actor.SetTacticalPosition(new(col,14));session.Select(id);
            var preview=MobaMovementPreview.Capture(session);var destination=new GridPosition(col,14+Math.Min(3,actor.EffectiveMovement));
            var plan=preview.PlanFor(destination);Assert.NotNull(plan);Assert.Equal(destination.Y-14,plan.Cost);
            Assert.Equal(MovementOutcome.Completed,session.CommitMovement(plan).Outcome);
        }
        var s=new MobaHotseatSession(map);var a=s.Characters.Values.First();
        var left=GridPathfinder.Reachable(new(8,14),30,20,600,map.Walkable,(p,q)=>s.MovementMap.GetStepCost(a,p,q));
        var right=GridPathfinder.Reachable(new(21,14),30,20,600,map.Walkable,(p,q)=>s.MovementMap.GetStepCost(a,p,q));
        Assert.Equal(left.Count,right.Count);
        foreach(var p in left.Keys)Assert.Equal(left[p].Cost,right[map.Board.Mirror(p)].Cost);
        Assert.Equal(3,left[new(8,17)].Cost);Assert.Equal(3,right[new(21,17)].Cost);
    }

    [Theory]
    [InlineData(8,"blue_forward")]
    [InlineData(20,"red_forward")]
    public void EnemyControlledCastleDoesNotGatePhysicalStairMovement(int x,string castle)
    {
        var session=new MobaHotseatSession(Map,startingPlayer:2);
        var scout=session.Characters["red_hooded_assassin"];scout.SetTacticalPosition(new(x,9));session.Select(scout.Id);
        var capture=session.MoveTo(new(x,10));Assert.Equal(MovementOutcome.Completed,capture.Outcome);
        session.AcknowledgeVisualMovement(capture.RequestId,new(x,10));Assert.Equal(2,session.Control.Castle(castle).OwnerTeamId);
        session.EndSelectedActivation();
        var actor=session.Characters["blue_gravebound_colossus"];actor.SetTacticalPosition(new(x,14));session.Select(actor.Id);
        var plan=MobaMovementPreview.Capture(session).PlanFor(new(x,17));Assert.NotNull(plan);Assert.Equal(3,plan.Cost);
        var move=session.CommitMovement(plan);Assert.Equal(MovementOutcome.Completed,move.Outcome);
        session.AcknowledgeVisualMovement(move.RequestId,plan.Destination);Assert.Equal(2,session.Control.Castle(castle).OwnerTeamId);
    }

    [Fact]
    public void ExistingSightFlagPolicyIsPreservedAtOldAndNewCliffCrossings()
    {
        var map=Map;
        foreach(int x in new[]{4,5,8,9,20,21,24,25})
        {
            Assert.DoesNotContain(new GridPosition(x,15),map.SightBlockers);
            Assert.True(map.HasClearSight(new(x,14),new(x,17)));
            Assert.Equal(map.HasClearSight(new(x,14),new(x,17)),map.HasClearSight(new(29-x,14),new(29-x,17)));
        }
    }
}
