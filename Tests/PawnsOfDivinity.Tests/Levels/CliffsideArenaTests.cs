using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class CliffsideArenaTests
{
    private static MobaBetaLayout Map => AuthoredBetaScene.Cliffside.Value;

    [Theory]
    [InlineData("blue_forward",6)]
    [InlineData("red_forward",18)]
    public void ForwardFortHasFourDoubleGatesAndEveryLandingCanReachEveryExit(string id,int left)
    {
        var map = Map;
        var fort = map.Castles.Single(c => c.Id == id);
        var session = new MobaHotseatSession(map);
        var actor = session.Characters.Values.First();
        Assert.Null(session.Control.Castle(id).OwnerTeamId);
        Assert.True(fort.Footprint.SetEquals(MobaBetaLayout.Rectangle(left,left+5,8,14)));
        Assert.True(fort.CaptureCells.SetEquals(MobaBetaLayout.Rectangle(left+2,left+3,10,11)));
        int arrival = left + (id == "blue_forward" ? 1 : 2);
        Assert.True(fort.DeploymentCells.SetEquals(MobaBetaLayout.Rectangle(arrival,arrival+2,12,13)));
        var gates = new Dictionary<string,HashSet<GridPosition>>
        {
            ["west"] = MobaBetaLayout.Rectangle(left,left,10,11),
            ["east"] = MobaBetaLayout.Rectangle(left+5,left+5,10,11),
            ["north"] = MobaBetaLayout.Rectangle(left+2,left+3,8,8),
            ["south"] = MobaBetaLayout.Rectangle(left+2,left+3,14,14)
        };
        foreach (var (name,cells) in gates) Assert.True(fort.Entrances[name].SetEquals(cells));
        var walls = fort.Footprint.Where(p => p.X == left || p.X == left+5 || p.Y is 8 or 14)
            .Except(gates.Values.SelectMany(g => g)).ToHashSet();
        Assert.Equal(14,walls.Count);
        Assert.True(walls.SetEquals(fort.Walls));
        Assert.All(walls,p => Assert.False(map.Walkable(p)));
        foreach (var landing in fort.DeploymentCells)
        {
            var routes = GridPathfinder.Reachable(landing,30,20,42,
                p => fort.Footprint.Contains(p) && map.Walkable(p),
                (a,b) => session.MovementMap.GetStepCost(actor,a,b));
            Assert.True(fort.Footprint.Except(walls).ToHashSet().SetEquals(routes.Keys));
        }
    }

    [Fact]
    public void CentralRuinsLeaveIndependentNorthernDirectAndSouthernApproaches()
    {
        var map = Map;
        var session = new MobaHotseatSession(map);
        var actor = session.Characters["hooded_assassin"];
        Assert.True(map.Objective.SetEquals(MobaBetaLayout.Rectangle(14,15,10,11)));
        Assert.Null(session.Control.Objective.OwnerTeamId);
        Assert.True(map.Reachable(actor.TacticalPosition!.Value).SetEquals(map.Board.Cells.Where(map.Walkable)));
        var center = MobaBetaLayout.Rectangle(12,17,8,14);
        Assert.Equal(34,center.Count(map.Walkable));
        foreach (var cell in map.Objective) Assert.Equal(0,map.Elevations[cell]);

        // These corridors end on separate objective faces and share no approach tiles.
        foreach (bool east in new[] {false,true})
        {
            int X(int x) => east ? 29-x : x;
            GridPosition[][] corridors = [
                [new(X(12),10),new(X(13),10),new(X(14),10)],
                [new(X(14),8),new(X(14),9),new(X(14),10)],
                [new(X(12),12),new(X(13),12),new(X(14),12),new(X(14),11)]
            ];
            foreach (var corridor in corridors)
            {
                var routes = GridPathfinder.Reachable(corridor[0],30,20,10,
                    p => corridor.Contains(p) && map.Walkable(p),
                    (a,b) => session.MovementMap.GetStepCost(actor,a,b));
                Assert.Equal(corridor.Length-1,routes[corridor[^1]].Cost);
                Assert.Contains(corridor[^1],map.Objective);
            }
        }
        Assert.True(map.HasClearSight(new(12,10),new(17,10)));
        Assert.False(map.HasClearSight(new(12,11),new(17,11)));
        foreach (var a in center.Where(map.Walkable))
        foreach (var b in center.Where(map.Walkable))
            Assert.Equal(map.HasClearSight(a,b),map.HasClearSight(map.Board.Mirror(a),map.Board.Mirror(b)));
    }

    [Theory]
    [InlineData(13,8,"ruined_gate")]
    [InlineData(13,11,"broken_wall")]
    [InlineData(13,13,"collapsed_column")]
    [InlineData(12,14,"rock_outcrop")]
    public void SolidRuinsBlockMovementAndSightOnBothSides(int x,int y,string role)
    {
        var map = Map;
        var s = new MobaHotseatSession(map);
        foreach (int col in new[] {x,29-x})
        {
            GridPosition cell = new(col,y);
            var element = map.Source.Elements.Single(e => e.Id == $"ruin_{col}_{y}");
            Assert.Equal(role,element.Role);
            Assert.False(map.Walkable(cell));
            Assert.Contains(cell,map.SightBlockers);
            Assert.Null(s.MovementMap.GetStepCost(s.Characters["hooded_assassin"],new(col,y-1),cell));
        }
    }

    [Fact]
    public void ForestVegetationCostsOneAndOnlyRocksAndWallsCloseForestCells()
    {
        var map = Map;
        var session = new MobaHotseatSession(map);
        var actor = session.Characters["hooded_assassin"];
        var critical = map.Castles.SelectMany(c => c.Footprint).Concat(map.SpawnZones.Values.SelectMany(c => c)).ToHashSet();
        foreach (var (cell,tag) in map.Terrain.Where(e => e.Key.Y <= 7 && !critical.Contains(e.Key)))
        {
            Assert.Contains(tag,new[] {"grass","tree","bush","rock","wall"});
            Assert.Equal(tag is "grass" or "tree" or "bush",map.Walkable(cell));
            foreach (var next in new GridPosition[] {new(cell.X+1,cell.Y),new(cell.X-1,cell.Y),new(cell.X,cell.Y+1),new(cell.X,cell.Y-1)})
                if (map.Walkable(next))
                    Assert.Equal(map.Walkable(cell) ? 1 : (int?)null,session.MovementMap.GetStepCost(actor,next,cell));
        }
        foreach (var tag in new[] {"tree","bush","rock","wall"})
        {
            var cells = map.Terrain.Where(e => e.Value == tag).Select(e => e.Key).ToHashSet();
            Assert.NotEmpty(cells);
            Assert.True(cells.SetEquals(cells.Select(map.Board.Mirror)));
            Assert.Empty(cells.Intersect(critical));
        }
    }

    [Theory]
    [InlineData("west_lane_jungle",4,7,9,false)]
    [InlineData("east_lane_jungle",25,7,9,false)]
    [InlineData("central_forest_clearing",14,4,9,false)]
    [InlineData("west_fort_grove",8,4,8,true)]
    [InlineData("east_fort_grove",21,4,8,true)]
    [InlineData("west_ruins_brush",12,4,9,true)]
    [InlineData("east_ruins_brush",17,4,9,true)]
    public void ForestPassageConnectsToMiddleAtNormalCost(string id,int x,int first,int last,bool covered)
    {
        var map = Map;
        var s = new MobaHotseatSession(map);
        var actor = s.Characters["hooded_assassin"];
        var passage = map.Connections[id];
        var start = new GridPosition(x,first);
        var exit = new GridPosition(x,last);
        var routes = GridPathfinder.Reachable(start,30,20,30,p => passage.Contains(p) && map.Walkable(p),
            (a,b) => s.MovementMap.GetStepCost(actor,a,b));
        Assert.True(passage.SetEquals(routes.Keys));
        Assert.Equal(last-first,routes[exit].Cost);
        foreach (var cell in passage.Where(p => p.Y <= 7))
            Assert.Equal(covered,map.Terrain[cell] is "tree" or "bush");
        var middle = GridPathfinder.Reachable(exit,30,20,30,p => p.Y >= last && p.Y < 15 && map.Walkable(p),
            (a,b) => s.MovementMap.GetStepCost(actor,a,b));
        Assert.All(map.Objective,p => Assert.Contains(p,middle.Keys));
        actor.SetTacticalPosition(start);
        s.Select(actor.Id);
        var move = s.MoveTo(exit);
        Assert.Equal(MovementOutcome.Completed,move.Outcome);
        Assert.Equal(routes[exit].Cells,move.TraversedPath);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void BothUnitsInStartingPairCanReachCampWithOrdinaryMoves(int spawnRow)
    {
        long Travel(int team)
        {
            var map = Map;
            var session = new MobaHotseatSession(map,team);
            var fixture = map.Fixtures.Single(f => f.TeamId == team && f.Cell.Y == spawnRow);
            var actor = session.Characters[fixture.CharacterId];
            GridPosition camp = team == 1 ? new(8,17) : new(21,17);
            var routes = GridPathfinder.Reachable(fixture.Cell,30,20,600,
                p => map.Walkable(p) && (p == fixture.Cell || !session.MovementMap.IsOccupied(p)),
                (a,b) => session.MovementMap.GetStepCost(actor,a,b));
            var route = routes[camp];
            long spent = 0;
            for (int index = 0; index < route.Cells.Count-1;)
            {
                for (int passes = 0; !session.CanSelect(actor.Id); passes++)
                {
                    Assert.InRange(passes,0,19);
                    var ready = session.Characters.Values.First(c => session.CanSelect(c.Id));
                    session.Select(ready.Id);
                    session.EndSelectedActivation();
                }
                session.Select(actor.Id);
                var preview = MobaMovementPreview.Capture(session);
                index = Math.Min(index+actor.EffectiveMovement,route.Cells.Count-1);
                var plan = preview.PlanFor(route.Cells[index]);
                Assert.NotNull(plan);
                Assert.InRange(plan.Cost,1,actor.EffectiveMovement);
                var move = session.CommitMovement(plan);
                Assert.Equal(MovementOutcome.Completed,move.Outcome);
                Assert.Equal(plan.Path,move.TraversedPath);
                spent += plan.Cost;
                session.AcknowledgeVisualMovement(move.RequestId,plan.Destination);
                session.EndSelectedActivation();
            }
            Assert.Equal(camp,actor.TacticalPosition);
            Assert.Equal(route.Cost,spent);
            return spent;
        }
        Assert.Equal(Travel(1),Travel(2));
    }

    [Theory]
    [InlineData(4,5)]
    [InlineData(25,24)]
    public void OccupiedStairPairCannotBePreviewedOrCrossed(int x,int adjacent)
    {
        var s = new MobaHotseatSession(Map);
        var actor = s.Characters["blue_gravebound_colossus"];
        actor.SetTacticalPosition(new(x,14));
        s.Characters["hooded_assassin"].SetTacticalPosition(new(x,15));
        s.Characters["nigel"].SetTacticalPosition(new(adjacent,15));
        s.Select(actor.Id);
        var preview = MobaMovementPreview.Capture(s);
        Assert.DoesNotContain(preview.Routes.Keys,p => p.Y >= 15);
        Assert.Equal(MovementOutcome.Rejected,s.MoveTo(new(x,17)).Outcome);
        Assert.Equal(new GridPosition(x,14),actor.TacticalPosition);
    }

    [Fact]
    public void LowerTerraceHasUniformHeightAndTwoLegalStairRuns()
    {
        var map = Map;
        var session = new MobaHotseatSession(map);
        var actor = session.Characters.Values.First();
        for (int x = 0; x < map.Board.Width; x++)
        {
            bool stair = x is 4 or 5 or 24 or 25;
            for (int y = 14; y < map.Board.Height; y++)
            {
                GridPosition cell = new(x,y);
                int level = y switch { 14 => 0, 15 => stair ? -1 : 0, 16 when stair => -2, _ => -3 };
                Assert.Equal(level * 0.6f, map.SurfaceHeight(cell));
                if (y == 15) Assert.Equal(stair, map.Walkable(cell));
                if (!stair || y >= 17) continue;
                Assert.Equal(1,session.MovementMap.GetStepCost(actor,cell,new(x,y+1)));
                Assert.Equal(1,session.MovementMap.GetStepCost(actor,new(x,y+1),cell));
                if (y is 15 or 16) Assert.Equal("stairs",map.Terrain[cell]);
            }
        }
    }

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
