using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class CliffsideArenaTests
{
    private static MobaBetaLayout Map => AuthoredBetaScene.Cliffside.Value;

    [Fact]
    public void LiveMapHasMirroredCostsFromEveryOccupiedStartingSlot()
    {
        var map = Map;
        Assert.Equal((30,20), (map.Board.Width,map.Board.Height));
        Assert.Equal(600,map.Terrain.Count);
        Assert.Equal(10,map.Fixtures.Count);
        Assert.Equal(2,map.Camps.Count);
        Assert.Equal(4,map.Castles.Count);
        var session = new MobaHotseatSession(map);
        foreach (var cell in map.Board.Cells)
        {
            var mirror = map.Board.Mirror(cell);
            Assert.Equal(map.Terrain[cell],map.Terrain[mirror]);
            Assert.Equal(map.Walkable(cell),map.Walkable(mirror));
            Assert.Equal(map.Elevations[cell],map.Elevations[mirror]);
            Assert.Equal(map.SightBlockers.Contains(cell),map.SightBlockers.Contains(mirror));
        }
        foreach (var blue in map.Fixtures.Where(f => f.TeamId == 1))
        {
            var red = map.Fixtures.Single(f => f.TeamId == 2 && f.Cell == map.Board.Mirror(blue.Cell));
            IReadOnlyDictionary<GridPosition,TacticalRoute> Routes(BetaCharacterFixture f) =>
                GridPathfinder.Reachable(f.Cell,30,20,600,
                    p => map.Walkable(p) && (p == f.Cell || !session.MovementMap.IsOccupied(p)),
                    (a,b) => session.MovementMap.GetStepCost(session.Characters[f.CharacterId],a,b));
            var west = Routes(blue);
            var east = Routes(red);
            foreach (var cell in map.Castles.SelectMany(c => c.CaptureCells.Concat(c.DeploymentCells))
                .Concat(map.Objective).Concat(map.Camps.SelectMany(c => c.Cells)).Concat(map.Connections.Values.SelectMany(c => c)))
                Assert.Equal(west[cell].Cost,east[map.Board.Mirror(cell)].Cost);
            Assert.True(west[new(14,17)].Cost > west[new(14,10)].Cost);
        }
    }

    [Fact]
    public void CrossingTheCliffRequiresAnEndStairAndForestRotationsAreOpen()
    {
        var map = Map;
        var session = new MobaHotseatSession(map);
        var actor = session.Characters.Values.First();
        for (int x=0;x<30;x++)
        {
            bool stair = x is 4 or 5 or 24 or 25;
            foreach (int row in new[] {14,15})
            {
                GridPosition a = new(x,row), b = new(x,row+1);
                Assert.Equal(stair,session.MovementMap.GetStepCost(actor,a,b) == 1);
                Assert.Equal(stair,session.MovementMap.GetStepCost(actor,b,a) == 1);
            }
        }
        foreach (int x in new[] {4,25})
            for (int y=4;y<10;y++)
                Assert.Equal(1,session.MovementMap.GetStepCost(actor,new(x,y),new(x,y+1)));
        Assert.False(map.HasClearSight(new(12,8),new(14,8)));
        Assert.True(map.HasClearSight(new(12,10),new(16,10)));
    }

    [Fact]
    public void ScoutCapturesAndColossusTransfersOnLaterActivation()
    {
        var s = new MobaHotseatSession(Map);
        void Move(string id, GridPosition cell)
        {
            Assert.Equal(BattlefieldOutcome.Completed,s.Select(id).Outcome);
            var result = s.MoveTo(cell);
            Assert.Equal(MovementOutcome.Completed,result.Outcome);
            s.AcknowledgeVisualMovement(result.RequestId,cell);
        }
        void Pass()
        {
            var next = s.Characters.Values.First(c => s.CanSelect(c.Id));
            s.Select(next.Id);
            s.EndSelectedActivation();
        }
        Assert.Null(s.Control.Castle("blue_forward").OwnerTeamId);
        Move("hooded_assassin",new(6,10));
        s.EndSelectedActivation();
        while (s.Round == 1) Pass();
        while (!s.CanSelect("hooded_assassin")) Pass();
        Move("hooded_assassin",new(8,10));
        Assert.Equal(1,s.Control.Castle("blue_forward").OwnerTeamId);
        s.EndSelectedActivation();
        while (!s.CanSelect("blue_gravebound_colossus")) Pass();
        Move("blue_gravebound_colossus",new(1,10));
        Assert.Equal(BattlefieldOutcome.Completed,s.BeginTransferTargeting().Outcome);
        var arrival = Map.Castles.Single(c => c.Id == "blue_forward").DeploymentCells.First();
        s.Characters["nigel"].SetTacticalPosition(new(9,10));
        Assert.False(s.PreviewTransfer("blue_forward",arrival).CanExecute);
        s.Characters["nigel"].SetTacticalPosition(new(27,12));
        var plan = s.PreviewTransfer("blue_forward",arrival).Plan!;
        Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteTransfer("stage11-transfer",plan).Outcome);
        Assert.Equal(arrival,s.Characters["blue_gravebound_colossus"].TacticalPosition);
        Assert.True(s.Characters["blue_gravebound_colossus"].HasActedThisRound);
        Assert.True(s.Control.Castle("blue_home").IsHome);
        // An enemy completing movement onto the now-empty home origin cannot flip it.
        s.Characters["nigel"].SetTacticalPosition(new(0,10));
        while (!s.CanSelect("nigel")) Pass();
        Move("nigel",new(1,10));
        Assert.Equal(1,s.Control.Castle("blue_home").OwnerTeamId);
    }
}
