using Godot;
using PawnsOfDivinity.Core.Abilities;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class RectangularPathfinderTests
{
    [Fact] public void RectangularEdgesUseIndependentBounds()
    {
        var routes=GridPathfinder.Reachable(new(29,19),30,20,4,_=>true,(_,_)=>1);
        Assert.Equal(15,routes.Count);Assert.All(routes.Keys,p=>{Assert.InRange(p.X,0,29);Assert.InRange(p.Y,0,19);});
        Assert.Contains(new GridPosition(25,19),routes.Keys);Assert.DoesNotContain(new GridPosition(29,20),routes.Keys);
    }
    [Fact] public void WeightedSearchChoosesCheapestLegalRouteInsteadOfFewestSteps()
    {
        var goal=new GridPosition(2,0);
        var routes=GridPathfinder.Reachable(new(0,0),3,2,4,_=>true,(_,to)=>to==new GridPosition(1,0)?10:1);
        Assert.Equal(4,routes[goal].Cost);
        Assert.Equal(new[]{new GridPosition(0,0),new(0,1),new(1,1),new(2,1),new(2,0)},routes[goal].Cells);
    }
    [Fact] public void EqualCostTieOrderMatchesLegacyEastSouthWestNorth()
    {
        var routes=GridPathfinder.Reachable(new(0,0),3,2,3,_=>true,(_,_)=>1);
        Assert.Equal(new[]{new GridPosition(0,0),new(1,0),new(2,0),new(2,1)},routes[new(2,1)].Cells);
        var legacy=GridPathfinder.Find(new(0,0),new(2,1),3,new HashSet<Vector2I>());
        Assert.Equal(new[]{new Vector2I(1,0),new(2,0),new(2,1)},legacy);
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public void UnknownAndNonPositiveCostsAreExcluded(int value)
    {
        Assert.Single(GridPathfinder.Reachable(new(0,0),3,2,4,_=>true,(_,_)=>value));
        Assert.Single(GridPathfinder.Reachable(new(0,0),3,2,4,_=>true,(_,_)=>null));
    }
    [Fact] public void ZeroBudgetAndInvalidOriginHaveSafeResults()
    {
        Assert.Single(GridPathfinder.Reachable(new(0,0),30,20,0,_=>true,(_,_)=>1));
        Assert.Empty(GridPathfinder.Reachable(new(-1,0),30,20,4,_=>true,(_,_)=>1));
        Assert.Empty(GridPathfinder.Reachable(new(0,0),30,20,4,_=>false,(_,_)=>1));
    }
}
