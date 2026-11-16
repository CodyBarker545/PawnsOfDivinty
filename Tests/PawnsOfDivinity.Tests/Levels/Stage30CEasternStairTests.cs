using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class Stage30CEasternStairTests
{
    [Theory]
    [InlineData(20,14,17)]
    [InlineData(21,14,17)]
    [InlineData(20,17,14)]
    [InlineData(21,17,14)]
    public void CliffExpansionPreservesEasternMovementCommits(int x,int startRow,int endRow)
    {
        var session=new MobaHotseatSession(AuthoredBetaScene.Cliffside.Value);
        var actor=session.Characters["blue_gravebound_colossus"];
        actor.SetTacticalPosition(new GridPosition(x,startRow));
        session.Select(actor.Id);
        var plan=MobaMovementPreview.Capture(session).PlanFor(new GridPosition(x,endRow));
        Assert.NotNull(plan);
        Assert.Equal(3,plan.Cost);
        Assert.Contains(new GridPosition(x,15),plan.Path);
        Assert.Contains(new GridPosition(x,16),plan.Path);
        var result=session.CommitMovement(plan);
        Assert.Equal(MovementOutcome.Completed,result.Outcome);
        Assert.Equal(plan.Path,result.TraversedPath);
        session.AcknowledgeVisualMovement(result.RequestId,plan.Destination);
        Assert.Equal(new GridPosition(x,endRow),actor.TacticalPosition);
    }
}
