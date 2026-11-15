using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;
using Xunit.Abstractions;

namespace PawnsOfDivinity.Tests.Levels;

public class MobaCompetitiveRouteTests(ITestOutputHelper output)
{
    private readonly MobaBetaLayout _map = AuthoredBetaScene.Layout;

    private IReadOnlyDictionary<GridPosition,TacticalRoute> Routes(MobaHotseatSession session,
        Character unit, GridPosition start, Func<GridPosition,bool>? allowed = null)
    {
        var movement = session.MovementMap;
        return GridPathfinder.Reachable(start, _map.Board.Width, _map.Board.Height, 600,
            p => movement.IsValidPosition(p) == true && (p == start || !movement.IsOccupied(p))
                && (allowed == null || allowed(p)),
            (a,b) => movement.GetStepCost(unit,a,b));
    }

    [Fact]
    public void AllFiveStartingSlotsHaveEqualCostsToTheirFortRuinsAndFlanks()
    {
        var session = new MobaHotseatSession(_map);
        foreach (var blue in _map.Fixtures.Where(f => f.TeamId == 1).OrderBy(f => f.Cell.Y))
        {
            long[] Costs(BetaCharacterFixture f)
            {
                var unit = session.Characters[f.CharacterId];
                var routes = Routes(session,unit,f.Cell);
                var castle = _map.Castles.Single(c => c.InitialTeamId == f.TeamId);
                int mid = f.TeamId == 1 ? 14 : 15;
                return [castle.CaptureCells.Min(p => routes[p].Cost), _map.Objective.Min(p => routes[p].Cost),
                    routes[new(mid,4)].Cost, routes[new(mid,17)].Cost];
            }
            var red = _map.Fixtures.Single(f => f.Cell == _map.Board.Mirror(blue.Cell));
            var costs = Costs(blue);
            Assert.Equal(costs, Costs(red));
            Assert.InRange(costs[1], 12, 14);
            output.WriteLine($"y={blue.Cell.Y}: {string.Join(", ", costs)}");
        }
        foreach (var castle in _map.Castles)
        {
            var unit = session.Characters.Values.First(c => c.TeamId == castle.InitialTeamId);
            long cost = castle.CaptureCells.Min(start =>
                _map.Objective.Min(p => Routes(session,unit,start)[p].Cost));
            Assert.Equal(5L,cost);
        }
    }

    [Theory]
    [InlineData("blue_gravebound_colossus",4)]
    [InlineData("gravebound_colossus",4)]
    [InlineData("blue_gravebound_colossus",17)]
    [InlineData("gravebound_colossus",17)]
    [InlineData("blue_gravebound_colossus",10)]
    [InlineData("gravebound_colossus",10)]
    public void SlowestUnitTraversesRouteInLegalActivationSizedMoves(string id,int row)
    {
        var s = new MobaHotseatSession(_map, freeTesting:true);
        var unit = s.Characters[id];
        int budget = unit.EffectiveMovement;
        Assert.Equal(CharacterRegistry.GetAllCharacters().Min(d => d.BaseStats.Movement),budget);
        var origin = unit.TacticalPosition!.Value;
        bool blue = unit.TeamId == 1;
        GridPosition entry = new(blue ? 5 : 24,row);
        GridPosition exit = new(blue ? 24 : 5,row);
        if (row == 10) exit = new(blue ? 14 : 15,row);
        var approach = Routes(s,unit,origin)[entry];
        var crossing = Routes(s,unit,entry,p => p.Y == row)[exit];
        var path = approach.Cells.Concat(crossing.Cells.Skip(1)).ToArray();
        for (int i = 0; i < path.Length-1; i += budget)
        {
            s.Select(null);
            Assert.Equal(BattlefieldOutcome.Completed,s.Select(id).Outcome);
            var move = s.MoveTo(path[Math.Min(i+budget,path.Length-1)]);
            Assert.Equal(MovementOutcome.Completed,move.Outcome);
            s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition!.Value);
        }
        Assert.Equal(exit,unit.TacticalPosition);
        output.WriteLine($"{id} row {row}: {approach.Cost + crossing.Cost} MOV");
    }

    [Fact]
    public void ArenaHasTwoClearCellsAtEachFaceAndTheReservationsLeaveAllRoutesOpen()
    {
        var s = new MobaHotseatSession(_map);
        var unit = s.Characters["blue_gravebound_colossus"];
        GridPosition[] planned = [new(9,3),new(20,3),new(9,18),new(20,18),new(12,9),new(17,9)];
        var reserve = planned.ToHashSet();
        Assert.All(reserve, p => {
            Assert.True(_map.Walkable(p));
            Assert.DoesNotContain(_map.Castles,c => c.Footprint.Contains(p));
            Assert.DoesNotContain(_map.SpawnZones.Values,zone => zone.Contains(p));
        });
        var routes = Routes(s,unit,unit.TacticalPosition!.Value,p => !reserve.Contains(p));
        foreach (var p in MobaBetaLayout.Rectangle(13,16,9,12))
            Assert.True(routes.ContainsKey(p));
        foreach (int row in new[]{4,10,17})
            Assert.True(Routes(s,unit,new(5,row),p => p.Y == row && !reserve.Contains(p)).ContainsKey(new(24,row)));
    }
}
