using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaMovementPreviewTests
{
    private static MobaHotseatSession Setup()
    {var s=new MobaHotseatSession(AuthoredBetaScene.Layout,2);s.Characters["nigel"].SetTacticalPosition(new(24,8));s.Select("nigel");return s;}
    [Fact] public void GeometricallyNearbyDestinationWithLongCastleDetourIsNotHighlighted()
    {
        var s=Setup();var snapshot=MobaMovementPreview.Capture(s);
        Assert.Equal(4,new GridPosition(24,8).ManhattanDistanceTo(new(20,8)));
        Assert.Null(snapshot.PlanFor(new(20,8)));Assert.Equal(MovementOutcome.Rejected,s.MoveTo(new(20,8)).Outcome);
        var valid=snapshot.PlanFor(new(21,7));Assert.NotNull(valid);Assert.Equal(4,valid.Cost);
        Assert.Equal(MovementOutcome.Completed,s.CommitMovement(valid).Outcome);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void EveryBoardDestinationAgreesWithActualMovementAcceptanceFromSameSnapshot(bool extraOccupant)
    {
        MobaHotseatSession Board(){var s=Setup();if(extraOccupant)s.Characters["time_weaver"].SetTacticalPosition(new(24,7));return s;}
        var snapshot=MobaMovementPreview.Capture(Board());
        for(int y=0;y<AuthoredBetaScene.Layout.Board.Height;y++)for(int x=0;x<AuthoredBetaScene.Layout.Board.Width;x++)
        {
            var destination=new GridPosition(x,y);var s=Board();
            Assert.Equal(snapshot.PlanFor(destination) is not null,s.MoveTo(destination).Outcome==MovementOutcome.Completed);
        }
    }
    [Fact] public void FreshSnapshotTracksSelectionOccupancyMovementAndActivationChanges()
    {
        var s=Setup();var destination=new GridPosition(21,7);Assert.NotNull(MobaMovementPreview.Capture(s).PlanFor(destination));
        s.Characters["time_weaver"].SetTacticalPosition(destination);Assert.Null(MobaMovementPreview.Capture(s).PlanFor(destination));
        s.Select(null);Assert.Empty(MobaMovementPreview.Capture(s).Routes);
        s.Select("nigel");var move=s.MoveTo(new(24,7));Assert.Equal(MovementOutcome.Completed,move.Outcome);
        Assert.Empty(MobaMovementPreview.Capture(s).Routes);s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition!.Value);
        Assert.Empty(MobaMovementPreview.Capture(s).Routes);s.EndSelectedActivation();Assert.Empty(MobaMovementPreview.Capture(s).Routes);
    }
}
