using System;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class MobaBetaLayoutTests
{
    private readonly MobaBetaLayout _map = AuthoredBetaScene.Layout;
    [Fact] public void BetaIsRectangularAndLegacyBoardIsUnchanged()
    {
        Assert.Equal(30,_map.Board.Width);Assert.Equal(20,_map.Board.Height);
        Assert.Equal(600,_map.Board.Cells.Count());Assert.Equal(14,BattlefieldGrid.Size);
        Assert.Equal(BattlefieldGrid.CellSize,_map.Board.CellSize);
        Assert.Empty(_map.Validate());
    }
    [Theory]
    [InlineData(0,0,true)] [InlineData(29,19,true)] [InlineData(30,19,false)]
    [InlineData(29,20,false)] [InlineData(-1,0,false)] [InlineData(0,-1,false)]
    public void IndependentBounds(int x,int y,bool valid) => Assert.Equal(valid,_map.Board.Contains(new(x,y)));
    [Fact] public void EveryCellRoundTripsAtSharedScaleAndIndependentOrigins()
    {
        foreach(var cell in _map.Board.Cells)
        {
            var (x,z)=_map.Board.CellCenter(cell);Assert.Equal(cell,_map.Board.WorldToCell(x,z));
            Assert.Equal(cell,_map.Board.WorldToCell(x+.49*_map.Board.CellSize,z-.49*_map.Board.CellSize));
        }
        var first=_map.Board.CellCenter(new(0,0));
        Assert.Equal(_map.Board.CellSize,_map.Board.CellCenter(new(1,0)).X-first.X);
        Assert.Equal(_map.Board.CellSize,_map.Board.CellCenter(new(0,1)).Z-first.Z);
        Assert.Throws<ArgumentOutOfRangeException>(()=>_map.Board.WorldToCell(double.NaN,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>_map.Board.WorldToCell(0,double.PositiveInfinity));
    }
    [Theory] [InlineData("blue_home","red_home")] [InlineData("blue_forward","red_forward")]
    public void CastleDataAndEntrancesAreMirrored(string blueId,string redId)
    {
        var a=_map.Castles.Single(c=>c.Id==blueId);var b=_map.Castles.Single(c=>c.Id==redId);
        Assert.Equal(1,a.InitialTeamId);Assert.Equal(2,b.InitialTeamId);
        Assert.True(b.Footprint.SetEquals(a.Footprint.Select(_map.Board.Mirror)));
        Assert.True(b.CaptureCells.SetEquals(a.CaptureCells.Select(_map.Board.Mirror)));
        Assert.True(b.DeploymentCells.SetEquals(a.DeploymentCells.Select(_map.Board.Mirror)));
        Assert.True(b.Walls.SetEquals(a.Walls.Select(_map.Board.Mirror)));
        foreach(var entry in a.Entrances)
        {
            string other=entry.Key=="west" ? "east" : entry.Key=="east" ? "west" : entry.Key;
            Assert.True(b.Entrances[other].SetEquals(entry.Value.Select(_map.Board.Mirror)));
        }
    }
    [Theory]
    [InlineData("blue_home",0,3,9,14,1,2,1,2)]
    [InlineData("red_home",26,29,9,14,27,28,27,28)]
    [InlineData("blue_forward",6,11,8,14,8,9,7,9)]
    [InlineData("red_forward",18,23,8,14,20,21,20,22)]
    public void ExactApprovedCastleRegions(string id,int x0,int x1,int y0,int y1,int cx0,int cx1,int dx0,int dx1)
    {
        var c=_map.Castles.Single(c=>c.Id==id);
        Assert.True(c.Footprint.SetEquals(MobaBetaLayout.Rectangle(x0,x1,y0,y1)));
        Assert.True(c.CaptureCells.SetEquals(MobaBetaLayout.Rectangle(cx0,cx1,10,11)));
        Assert.True(c.DeploymentCells.SetEquals(MobaBetaLayout.Rectangle(dx0,dx1,12,13)));
        Assert.False(c.Walls.Overlaps(c.CaptureCells.Concat(c.DeploymentCells)));
        Assert.All(c.Entrances.Values,e=>{Assert.True(e.Count>=2);Assert.All(e,p=>Assert.True(_map.Walkable(p)));});
    }
    [Fact] public void ForwardEntrancesHaveExactTwoCellOpenings()
    {
        var c=_map.Castles.Single(c=>c.Id=="blue_forward");
        Assert.True(c.Entrances["west"].SetEquals(MobaBetaLayout.Rectangle(6,6,10,11)));
        Assert.True(c.Entrances["east"].SetEquals(MobaBetaLayout.Rectangle(11,11,10,11)));
        Assert.True(c.Entrances["north"].SetEquals(MobaBetaLayout.Rectangle(8,9,8,8)));
        Assert.True(c.Entrances["south"].SetEquals(MobaBetaLayout.Rectangle(8,9,14,14)));
    }
    [Fact] public void SpecialRegionsDoNotOverlap()
    {
        var cells=_map.Castles.SelectMany(c=>c.CaptureCells.Concat(c.DeploymentCells))
            .Concat(_map.Camps.SelectMany(c=>c.Cells)).Concat(_map.Objective).ToArray();
        Assert.Equal(cells.Length,cells.Distinct().Count());
        Assert.True(_map.Objective.SetEquals(MobaBetaLayout.Rectangle(14,15,10,11)));
        Assert.True(_map.Camps[0].Cells.SetEquals(MobaBetaLayout.Rectangle(8,10,3,5)));
        Assert.True(_map.Camps[1].Cells.SetEquals(MobaBetaLayout.Rectangle(19,21,3,5)));
    }
    [Fact] public void AllWalkableCellsAndFixturesConnectToBothBases()
    {
        var reachable=_map.Reachable(new(2,10));
        Assert.True(reachable.SetEquals(_map.Board.Cells.Where(_map.Walkable)));
        Assert.Equal(10,_map.Fixtures.Select(f=>f.Cell).Distinct().Count());
        Assert.All(_map.Fixtures,f=>Assert.Contains(f.Cell,reachable));
        Assert.Equal(new GridPosition(20,12),_map.Fixtures.Single(f=>f.CharacterId=="gravebound_colossus").Cell);
    }
    [Fact] public void MainLaneAndSouthernFlankConnectWithinTheirOwnBands()
    {
        Assert.Contains(new GridPosition(29,11),_map.Reachable(new(0,11),_map.MainLane));
        Assert.True(_map.SouthernFlank.Where(_map.Walkable).ToHashSet().SetEquals(_map.Reachable(new(0,17),_map.SouthernFlank)));
        // Multiple lane-to-flank approaches remain on each half, independent of castle gates.
        foreach(int x in new[]{5,12,17,24})
            Assert.All(MobaBetaLayout.Rectangle(x,x,13,17),p=>Assert.True(_map.Walkable(p)));
    }
    [Fact] public void CampsAndNorthernCastleGatesConnectThroughJungle()
    {
        var north=_map.Reachable(new(8,4),_map.Jungle);
        Assert.All(_map.Camps.SelectMany(c=>c.Cells),p=>Assert.Contains(p,north));
        foreach(int x in new[]{8,9,20,21}) Assert.Contains(new GridPosition(x,7),north);
        Assert.True(_map.NatureObstacles.SetEquals(_map.NatureObstacles.Select(_map.Board.Mirror)));
    }
}
