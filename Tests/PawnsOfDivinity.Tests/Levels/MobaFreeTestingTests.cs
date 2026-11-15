using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaFreeTestingTests
{
    private sealed class Time:IActivationTimeSource {public double NowSeconds {get;set;}}
    private sealed class Rolls:IRandomRollProvider {public int NextRoll()=>0;}
    private static MobaHotseatSession Free(Time? time=null)=>new(AuthoredBetaScene.Layout,random:new Rolls(),timeSource:time,freeTesting:true);
    private static void Move(MobaHotseatSession s,int x,int y)
    {var r=s.MoveTo(new(x,y));Assert.Equal(PawnsOfDivinity.MovementOutcome.Completed,r.Outcome);Assert.True(r.MovementConsumed);s.AcknowledgeVisualMovement(r.RequestId,r.FinalPosition!.Value);}
    [Fact] public void BothTeamsCanBeSelectedWithoutAnActivationOrHandoff()
    {var s=Free();Assert.Equal(BattlefieldOutcome.Completed,s.Select("nigel").Outcome);Assert.Equal(BattlefieldOutcome.Completed,s.Select("hooded_assassin").Outcome);Assert.Null(s.Activations.ActiveCharacter);Assert.Equal(1,s.Round);}
    [Fact] public void TimerCannotExpireOrBeReconnectedAccidentallyInsideFreeTesting()
    {var t=new Time();var s=Free(t);s.Select("nigel");t.NowSeconds=10000;Assert.False(s.UpdateActivationTimer());Assert.False(s.ActivationTimer.Enabled);Assert.Equal(0,s.TimeoutCount);Assert.Equal("nigel",s.SelectedFixtureId);Assert.Equal(BattlefieldOutcome.Rejected,s.SetDevelopmentTimerEnabled(true).Outcome);}
    [Fact] public void FreeTeamSelectionRetainsMovBudgetAndOneUnsplitMovementOpportunity()
    {
        var s=Free();s.Select("nigel");Assert.Equal(4,s.RemainingMovement);Assert.Null(s.PreviewMovement(new(14,10)));
        Assert.Equal(PawnsOfDivinity.MovementOutcome.Rejected,s.MoveTo(new(14,10)).Outcome);
        Move(s,25,12);Assert.Equal(0,s.RemainingMovement);Assert.True(s.Activations.MovementUsed);Assert.True(s.CanAttack);
        Assert.Equal(PawnsOfDivinity.MovementOutcome.Rejected,s.MoveTo(new(24,12)).Outcome);Assert.False(s.Characters["nigel"].HasActedThisRound);
        s.Select("hooded_assassin");Assert.Equal(6,s.RemainingMovement);Assert.Equal(1,s.Round);
        s.Select("nigel");Assert.Equal(4,s.RemainingMovement);Move(s,24,12);
    }
    [Theory] [InlineData(-1,10)] [InlineData(30,10)] [InlineData(27,11)] [InlineData(26,13)]
    public void BoundsLivingOccupantsAndCastleObstaclesStillRejectInvalidMoves(int x,int y)
    {var s=Free();s.Select("nigel");var before=s.SelectedCharacter!.TacticalPosition;Assert.False(s.MoveTo(new(x,y)).Outcome==PawnsOfDivinity.MovementOutcome.Completed);Assert.Equal(before,s.SelectedCharacter.TacticalPosition);Assert.Null(s.PendingVisualMovement);}
    [Fact] public void PresentationLockPreventsSwitchingOrMovingUntilDomainAndViewAgree()
    {var s=Free();s.Select("nigel");var r=s.MoveTo(new(25,12));Assert.NotNull(s.PendingVisualMovement);Assert.Equal(BattlefieldOutcome.Rejected,s.Select("hooded_assassin").Outcome);Assert.Equal(PawnsOfDivinity.MovementOutcome.Rejected,s.MoveTo(new(24,12)).Outcome);s.AcknowledgeVisualMovement(r.RequestId,r.FinalPosition!.Value);Assert.Equal(BattlefieldOutcome.Completed,s.Select("hooded_assassin").Outcome);}
    [Fact] public void ProductionCombatStillRunsAndTheSameUnitCanActAgainWithoutRoundProgression()
    {var s=Free();s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Characters["nigel"].SetTacticalPosition(new(15,10));s.Select("hooded_assassin");s.BeginAttackTargeting();Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteBasicAttack("free-hit","nigel").Outcome);Assert.Single(s.CombatHistory.Records);Assert.Equal(1,s.Round);Assert.Equal(BattlefieldOutcome.Completed,s.Select("hooded_assassin").Outcome);Assert.True(s.CanAttack);Assert.False(s.Characters["hooded_assassin"].HasActedThisRound);Assert.Equal(0,s.Victory.BlueVP);}
    [Fact] public void ResetKeepsFreeWiringAndRestoresTheAuthoredInitialState()
    {var s=Free();s.Select("nigel");Move(s,25,12);var fresh=s.CreateResetSession();Assert.True(fresh.FreeTesting);Assert.False(fresh.ActivationTimer.Enabled);Assert.Null(fresh.SelectedFixtureId);Assert.Equal(AuthoredBetaScene.Layout.Fixtures.Single(f=>f.CharacterId=="nigel").Cell,fresh.Characters["nigel"].TacticalPosition);Assert.Empty(fresh.CombatHistory.Records);}
    [Fact] public void DeselectAndReselectExplicitlyStartsAnotherTestActivation()
    {var s=Free();s.Select("nigel");Move(s,25,12);s.Select("nigel");Assert.Equal(0,s.RemainingMovement);s.Select(null);s.Select("nigel");Assert.Equal(4,s.RemainingMovement);Assert.False(s.Activations.MovementUsed);}
    [Fact] public void NormalModeRemainsTheSessionDefaultAndStillEnforcesTeamAndMovementLimits()
    {var s=new MobaHotseatSession(AuthoredBetaScene.Layout);Assert.False(s.FreeTesting);Assert.True(s.ActivationTimer.Enabled);Assert.Equal(BattlefieldOutcome.Rejected,s.Select("nigel").Outcome);s.Select("hooded_assassin");Assert.All(s.ReachableDestinations().Values,r=>Assert.True(r.Cost<=6));Assert.Equal(BattlefieldOutcome.Completed,s.EndSelectedActivation().Outcome);Assert.Equal(2,s.CurrentPlayer);}
}
