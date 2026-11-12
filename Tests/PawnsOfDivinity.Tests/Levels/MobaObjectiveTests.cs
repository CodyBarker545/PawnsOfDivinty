using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaObjectiveTests
{
    private sealed class Hits : IRandomRollProvider {public int NextRoll()=>0;}
    private static MobaHotseatSession Match(DevelopmentScoringProfile? profile=null,int first=1)=>new(AuthoredBetaScene.Layout,first,new Hits(),scoringProfile:profile);
    private static void Place(MobaHotseatSession s,string id,int x,int y)=>s.Characters[id].SetTacticalPosition(new(x,y));
    private static void Move(MobaHotseatSession s,string id,int x,int y)
    {
        Assert.Equal(BattlefieldOutcome.Completed,s.Select(id).Outcome);var r=s.MoveTo(new(x,y));Assert.Equal(MovementOutcome.Completed,r.Outcome);
        Assert.Equal(BattlefieldOutcome.Completed,s.AcknowledgeVisualMovement(r.RequestId,r.FinalPosition!.Value).Outcome);
    }
    private static void FinishRound(MobaHotseatSession s)
    {
        int round=s.Round;if(s.SelectedCharacter is not null)s.EndSelectedActivation();
        for(int guard=0;s.Round==round&&!s.Victory.IsFinished&&guard<20;guard++)
        {var actor=s.Characters.Values.First(c=>s.CanSelect(c.Id));s.Select(actor.Id);s.EndSelectedActivation();}
        Assert.True(s.Round==round+1||s.Victory.IsFinished);
    }
    private static MobaHotseatSession CaptureRed(DevelopmentScoringProfile? profile=null)
    {var s=Match(profile);Place(s,"hooded_assassin",19,10);Move(s,"hooded_assassin",20,10);return s;}

    [Fact] public void InitialCastleOwnersFollowMetadataAndCenterIsNeutralWithNoCampObjectives()
    {
        var s=Match();Assert.Equal(4,s.Control.AllZones().Count(z=>z.Id!=s.Control.ObjectiveId));
        foreach(var c in AuthoredBetaScene.Layout.Castles)Assert.Equal(c.InitialTeamId,s.Control.Castle(c.Id).OwnerTeamId);
        Assert.Null(s.Control.Objective.OwnerTeamId);Assert.False(s.Control.Objective.Contested);Assert.Equal(0,s.Victory.BlueVP);
        Assert.DoesNotContain(s.Control.AllZones(),z=>AuthoredBetaScene.Layout.Camps.Any(c=>c.Id==z.Id));
    }
    [Fact] public void CaptureOccursAtCompletedDomainMovementNotPreviewOrVisualAcknowledgment()
    {
        var s=Match();Place(s,"hooded_assassin",19,10);s.Select("hooded_assassin");
        var p=s.PreviewMovement(new(20,10));Assert.NotNull(p);Assert.Equal(2,s.Control.Castle("red_forward").OwnerTeamId);
        var r=s.CommitMovement(p);Assert.Equal(MovementOutcome.Completed,r.Outcome);Assert.True(s.IsPresentationBusy);
        Assert.Equal(1,s.Control.Castle("red_forward").OwnerTeamId);Assert.True(s.Activations.CanUseMainAction);
        Assert.Single(s.TerritoryHistory,h=>h.Contains("captured red_forward"));
        Assert.Equal(MovementOutcome.Rejected,s.CommitMovement(p).Outcome);Assert.Single(s.TerritoryHistory,h=>h.Contains("captured red_forward"));
    }
    [Fact] public void InvalidDestinationAndEndingActivationDoNotCreateACapture()
    {
        var s=Match();Place(s,"hooded_assassin",20,10);s.Select("hooded_assassin");
        Assert.Equal(MovementOutcome.Rejected,s.MoveTo(new(18,9)).Outcome);Assert.Equal(2,s.Control.Castle("red_forward").OwnerTeamId);
        FinishRound(s);Assert.Equal(2,s.Control.Castle("red_forward").OwnerTeamId);
    }
    [Fact] public void PersistentOwnerRemainsAfterItsLastOccupantLeaves()
    {
        var s=CaptureRed();FinishRound(s);Move(s,"hooded_assassin",19,10);
        Assert.Equal(1,s.Control.Castle("red_forward").OwnerTeamId);Assert.Null(s.Control.Castle("red_forward").OccupyingTeamId);
    }
    [Fact] public void PassingThroughCaptureCellsWithoutStoppingDoesNotCapture()
    {
        var s=Match();Place(s,"hooded_assassin",19,10);Move(s,"hooded_assassin",22,10);
        Assert.Equal(2,s.Control.Castle("red_forward").OwnerTeamId);
    }
    [Fact] public void CaptureUsesOnlyLivingCaptureZoneOccupantsNotDeploymentOrFootprintDefenders()
    {
        var s=CaptureRed();Assert.Equal(1,s.Control.Castle("red_forward").OwnerTeamId);
        Assert.Equal(5,s.Characters.Values.Count(c=>c.TeamId==2&&!c.IsDefeated));Assert.False(s.Control.Castle("red_forward").Contested);
    }
    [Fact] public void LastDefenderDefeatImmediatelyCapturesAndDuplicateAttackDoesNotRescore()
    {
        var s=Match(new(1,2,3,0));Place(s,"hooded_assassin",19,10);Place(s,"time_weaver",21,10);
        Move(s,"hooded_assassin",20,10);Assert.True(s.Control.Castle("red_forward").Contested);Assert.Equal(2,s.Control.Castle("red_forward").OwnerTeamId);
        var target=s.Characters["time_weaver"];target.TakeDamage(target.CurrentHealth-1);s.BeginAttackTargeting();
        var result=s.ExecuteBasicAttack("last-defender",target.Id);Assert.Equal(BattlefieldOutcome.Completed,result.Outcome);
        Assert.Equal(1,s.Control.Castle("red_forward").OwnerTeamId);Assert.False(s.Control.Castle("red_forward").Contested);
        Assert.Equal(3,s.Victory.Pending(1));Assert.Equal(0,s.Victory.BlueVP);
        s.ExecuteBasicAttack("last-defender",target.Id);Assert.Equal(3,s.Victory.Pending(1));
        FinishRound(s);Assert.Equal(6,s.Victory.BlueVP);Assert.Equal(0,s.Victory.Pending(1));Assert.All(s.Victory.Awards,a=>Assert.True(a.Settled));
    }
    [Fact] public void RemainingLivingDefenderKeepsCastleContestedAfterAnotherDefenderDies()
    {
        var s=Match();Place(s,"hooded_assassin",19,10);Place(s,"time_weaver",21,10);Place(s,"nigel",20,11);
        Move(s,"hooded_assassin",20,10);s.Characters["time_weaver"].TakeDamage(37);s.BeginAttackTargeting();s.ExecuteBasicAttack("one-defender","time_weaver");
        Assert.True(s.Control.Castle("red_forward").Contested);Assert.Equal(2,s.Control.Castle("red_forward").OwnerTeamId);
    }
    [Fact] public void KillingDefenderFromOutsideDoesNotCaptureAnEmptyCastle()
    {
        var s=Match();Place(s,"beothuk_spirit_guardian",18,10);Place(s,"time_weaver",20,10);s.Characters["time_weaver"].TakeDamage(37);
        s.Select("beothuk_spirit_guardian");s.BeginAttackTargeting();s.ExecuteBasicAttack("ranged-kill","time_weaver");
        Assert.Equal(2,s.Control.Castle("red_forward").OwnerTeamId);Assert.Null(s.Control.Castle("red_forward").OccupyingTeamId);
    }
    [Fact] public void HomesRemainFixedAndNeverEarnForwardCastleHoldPoints()
    {
        var s=Match(first:2);Place(s,"nigel",0,10);Move(s,"nigel",1,10);Assert.Equal(1,s.Control.Castle("blue_home").OwnerTeamId);
        Assert.False(s.CanBeginTransfer);FinishRound(s);Assert.Equal(0,s.Victory.RedVP);
    }
    [Fact] public void NeutralForwardCastleCanBeAuthoredAndCapturedWithoutChangingHomeRules()
    {
        var data=AuthoredBetaScene.Data;var neutral=new MobaBetaLayout(data with {RequireDeploymentSpawns=false,Elements=data.Elements.Select(e=>e.Id=="red_forward"?e with {TeamId=0}:e).ToArray()});
        var s=new MobaHotseatSession(neutral,random:new Hits());Assert.Null(s.Control.Castle("red_forward").OwnerTeamId);
        Place(s,"hooded_assassin",19,10);Move(s,"hooded_assassin",20,10);Assert.Equal(1,s.Control.Castle("red_forward").OwnerTeamId);
    }
    [Fact] public void DevelopmentCenterControlFollowsOccupationContestsAndVacates()
    {
        var s=Match();Place(s,"hooded_assassin",14,9);Move(s,"hooded_assassin",14,10);Assert.Equal(1,s.Control.Objective.OwnerTeamId);
        s.EndSelectedActivation();Place(s,"nigel",16,10);Move(s,"nigel",15,10);Assert.True(s.Control.Objective.Contested);Assert.Null(s.Control.Objective.OwnerTeamId);
        FinishRound(s);Move(s,"hooded_assassin",14,9);Assert.Equal(2,s.Control.Objective.OwnerTeamId);Assert.False(s.Control.Objective.Contested);
    }
    [Fact] public void NeutralCampOccupationDoesNotBecomeAnObjectiveOrAwardVp()
    {
        var s=Match();Place(s,"hooded_assassin",7,4);Move(s,"hooded_assassin",8,4);FinishRound(s);
        Assert.Null(s.Control.Objective.OwnerTeamId);Assert.Equal(0,s.Victory.BlueVP);Assert.Empty(s.Victory.Awards);
    }
    [Fact] public void EndRoundObjectiveAwardsAreConfiguredAndDoNotRepeatWithinOneRound()
    {
        var s=Match(new(0,0,0,4));Place(s,"hooded_assassin",14,9);Move(s,"hooded_assassin",14,10);
        Assert.Equal(0,s.Victory.BlueVP);FinishRound(s);Assert.Equal(4,s.Victory.BlueVP);Assert.Single(s.Victory.Awards);
        FinishRound(s);Assert.Equal(8,s.Victory.BlueVP);Assert.Equal(2,s.Victory.Awards.Select(a=>a.EventId).Distinct().Count());
    }
    [Fact] public void ContestedObjectiveAndUnoccupiedOwnedCastlesAwardNoHoldVp()
    {
        var s=Match();Place(s,"hooded_assassin",14,9);Move(s,"hooded_assassin",14,10);s.EndSelectedActivation();
        Place(s,"nigel",16,10);Move(s,"nigel",15,10);FinishRound(s);Assert.Equal(0,s.Victory.BlueVP);Assert.Equal(0,s.Victory.RedVP);
    }
    [Fact] public void CounterDefeatCreditsTheActualCounteringTeamOnly()
    {
        var s=Match();Place(s,"hooded_assassin",14,10);Place(s,"nigel",15,10);s.Characters["hooded_assassin"].TakeDamage(37);
        s.Select("hooded_assassin");s.BeginAttackTargeting();var result=s.ExecuteBasicAttack("counter","nigel");
        Assert.True(result.Combat!.AttackerDefeated);Assert.Equal(0,s.Victory.Pending(1));Assert.Equal(1,s.Victory.Pending(2));
    }
    [Fact] public void VictoryAtFifteenStopsAllGameplayButRetainsHistoryAndPermitsFreshReset()
    {
        var s=Match(new(1,0,0,15));Place(s,"hooded_assassin",14,9);Move(s,"hooded_assassin",14,10);FinishRound(s);
        Assert.True(s.Victory.IsFinished);Assert.Equal(1,s.Victory.WinnerTeamId);Assert.Equal(15,s.Victory.BlueVP);Assert.Equal(1,s.Round);
        Assert.Equal(RoundPhase.ObjectiveScoring,s.Lifecycle.CurrentPhase);var score=s.Victory.BlueVP;
        Assert.Equal(BattlefieldOutcome.Rejected,s.Select("gentle_guardian").Outcome);Assert.Equal(BattlefieldOutcome.Rejected,s.Select(null).Outcome);
        Assert.Equal(MovementOutcome.Rejected,s.MoveTo(new(14,9)).Outcome);Assert.Equal(BattlefieldOutcome.Rejected,s.EndSelectedActivation().Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.BeginAttackTargeting().Outcome);Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteBasicAttack("after","nigel").Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.BeginTransferTargeting().Outcome);Assert.Equal(score,s.Victory.BlueVP);
        var fresh=s.CreateResetSession();Assert.False(fresh.Victory.IsFinished);Assert.Equal(0,fresh.Victory.BlueVP);Assert.Empty(fresh.Victory.Awards);Assert.Empty(fresh.TerritoryHistory);
        Assert.Equal(1,fresh.Round);Assert.Equal(s.Victory.Profile,fresh.Victory.Profile);Assert.Equal(10,fresh.Characters.Count);Assert.Null(fresh.Control.Objective.OwnerTeamId);
    }
    [Theory] [InlineData(DevelopmentTiePolicy.HigherScoreThenDraw)] [InlineData(DevelopmentTiePolicy.Draw)]
    public void SimultaneousEqualThresholdUsesExplicitDevelopmentDrawPolicy(DevelopmentTiePolicy policy)
    {
        var s=Match(new(0,0,15,0,policy));Move(s,"gentle_guardian",8,11);s.EndSelectedActivation();Move(s,"time_weaver",21,11);FinishRound(s);
        Assert.Equal(15,s.Victory.BlueVP);Assert.Equal(15,s.Victory.RedVP);Assert.True(s.Victory.IsDraw);
    }
    [Fact] public void NoRoundLimitIsIntroducedWhenScoringIsConfiguredToZero()
    {var s=Match(new(0,0,0,0));for(int i=0;i<25;i++)FinishRound(s);Assert.Equal(26,s.Round);Assert.False(s.Victory.IsFinished);}
    [Fact] public void InvalidDevelopmentPointsFailClearly()
    {Assert.Throws<ArgumentOutOfRangeException>(()=>Match(new(-1)));Assert.Throws<ArgumentOutOfRangeException>(()=>Match(new(CentralHoldPoints:1001)));}

    [Fact] public void CapturedCastleImmediatelyAllowsMoveThenTransferAtAnyDistanceAndEndsMainAction()
    {
        var s=CaptureRed();Assert.True(s.CanBeginTransfer);Assert.Equal(BattlefieldOutcome.Completed,s.BeginTransferTargeting().Outcome);
        var preview=s.PreviewTransfer("blue_home",new(1,12));Assert.True(preview.CanExecute);var p=preview.Plan!;
        Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteTransfer("transfer",p).Outcome);Assert.Equal(new GridPosition(1,12),s.Characters[p.ActorId].TacticalPosition);
        Assert.True(s.Characters[p.ActorId].HasActedThisRound);Assert.True(s.Activations.MainActionUsed);Assert.Equal(2,s.CurrentPlayer);
        Assert.Equal(BattlefieldOutcome.AlreadyProcessed,s.ExecuteTransfer("transfer",p).Outcome);Assert.Equal(1,s.Control.Castle("red_forward").OwnerTeamId);
    }
    [Theory] [InlineData("red_forward",20,13)] [InlineData("red_home",27,12)] [InlineData("blue_home",0,0)] [InlineData("unknown",1,12)]
    public void SameCastleEnemyDestinationInvalidDeploymentAndUnknownCastleRejectWithoutResources(string castle,int x,int y)
    {
        var s=CaptureRed();s.BeginTransferTargeting();Assert.False(s.PreviewTransfer(castle,new(x,y)).CanExecute);
        Assert.False(s.Activations.MainActionUsed);Assert.False(s.Characters["hooded_assassin"].HasActedThisRound);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void ContestedOriginOrDestinationRejectsStaleTransferBeforeSpending(bool origin)
    {
        var s=CaptureRed();s.BeginTransferTargeting();var p=s.PreviewTransfer("blue_home",new(1,12)).Plan!;
        Place(s,"nigel",origin?21:1,10);
        if(!origin)Place(s,"gentle_guardian",2,10);
        Assert.False(s.PreviewTransfer("blue_home",new(1,12)).CanExecute);Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteTransfer("stale",p).Outcome);
        Assert.Equal(new GridPosition(20,10),s.SelectedCharacter!.TacticalPosition);Assert.False(s.Activations.MainActionUsed);
    }
    [Fact] public void TransferRevalidatesOccupiedTileChangedOriginRoundAndOwnership()
    {
        var s=CaptureRed();s.BeginTransferTargeting();var p=s.PreviewTransfer("blue_home",new(1,12)).Plan!;
        Place(s,"gentle_guardian",1,12);Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteTransfer("occupied",p).Outcome);
        Place(s,"gentle_guardian",8,12);Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteTransfer("round",p with {Round=2}).Outcome);
        Place(s,"hooded_assassin",19,10);Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteTransfer("origin",p).Outcome);
        Assert.False(s.Activations.MainActionUsed);
    }
    [Fact] public void TransferNeedsCaptureZoneMainActionAndCompletedPresentation()
    {
        var s=Match();s.Select("hooded_assassin");Assert.Equal(BattlefieldOutcome.Rejected,s.BeginTransferTargeting().Outcome);
        Place(s,"hooded_assassin",19,10);var move=s.MoveTo(new(20,10));Assert.Equal(MovementOutcome.Completed,move.Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.BeginTransferTargeting().Outcome);s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition!.Value);
        Assert.Equal(BattlefieldOutcome.Completed,s.BeginTransferTargeting().Outcome);Assert.True(s.PreviewTransfer("blue_home",new(1,12)).CanExecute);
    }
    [Fact] public void RepeatedResetRestoresOwnershipAndScoreWithoutDuplicatedEvents()
    {
        var s=CaptureRed(new(0,3,0,0));
        for(int i=0;i<4;i++)
        {
            FinishRound(s);Assert.Equal(3,s.Victory.BlueVP);s=s.CreateResetSession(new Hits());
            Assert.Equal(2,s.Control.Castle("red_forward").OwnerTeamId);Assert.Equal(0,s.Victory.BlueVP);Assert.Empty(s.Victory.Awards);Assert.Empty(s.TerritoryHistory);
            Place(s,"hooded_assassin",19,10);Move(s,"hooded_assassin",20,10);Assert.Equal(3,s.Victory.Pending(1));
        }
    }
    [Theory] [InlineData(DevelopmentTiePolicy.HigherScoreThenDraw,1)] [InlineData(DevelopmentTiePolicy.Draw,null)]
    public void UnequalSimultaneousThresholdIsSettledAsOneBatchAndRetainsCombatHistory(DevelopmentTiePolicy policy,int? winner)
    {
        var s=Match(new(1,0,15,0,policy));Move(s,"gentle_guardian",8,11);s.EndSelectedActivation();Move(s,"time_weaver",21,11);s.EndSelectedActivation();
        Place(s,"hooded_assassin",14,10);Place(s,"nigel",15,10);s.Characters["nigel"].TakeDamage(51);
        s.Select("hooded_assassin");s.BeginAttackTargeting();s.ExecuteBasicAttack("victory-kill","nigel");FinishRound(s);
        Assert.Equal(16,s.Victory.BlueVP);Assert.Equal(15,s.Victory.RedVP);Assert.Equal(winner,s.Victory.WinnerTeamId);Assert.Single(s.CombatHistory.Records);
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteBasicAttack("victory-kill","nigel").Outcome);Assert.Single(s.CombatHistory.Records);
        var forged=new CastleTransferPlan("hooded_assassin",s.Round,"blue_forward","blue_home",new(8,10),new(1,12));
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteTransfer("after-victory",forged).Outcome);
        Assert.Equal(3,s.Victory.Awards.Count);Assert.All(s.Victory.Awards,a=>Assert.True(a.Settled));
    }
    [Fact] public void VacatedOwnedCastleDoesNotScoreAfterTheCaptureRound()
    {
        var s=CaptureRed(new(0,0,2,0));FinishRound(s);Assert.Equal(2,s.Victory.BlueVP);
        Move(s,"hooded_assassin",19,10);FinishRound(s);Assert.Equal(2,s.Victory.BlueVP);Assert.Equal(1,s.Control.Castle("red_forward").OwnerTeamId);
    }
    [Fact] public void DefenderWalkingOutOfAContestedZoneImmediatelyLeavesItCapturedByRemainingOccupier()
    {
        var s=Match(first:2);Place(s,"hooded_assassin",20,10);Place(s,"nigel",21,10);
        Assert.True(s.Control.Castle("red_forward").Contested);Move(s,"nigel",22,10);
        Assert.False(s.Control.Castle("red_forward").Contested);Assert.Equal(1,s.Control.Castle("red_forward").OwnerTeamId);
    }
    [Fact] public void RejectedTransferMayBeRetriedWithSameIdAfterOccupancyIsCorrectedWithoutResourceLoss()
    {
        var s=CaptureRed();s.BeginTransferTargeting();var plan=s.PreviewTransfer("blue_home",new(1,12)).Plan!;
        Place(s,"gentle_guardian",1,12);Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteTransfer("retry",plan).Outcome);Assert.False(s.Activations.MainActionUsed);
        Place(s,"gentle_guardian",8,12);Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteTransfer("retry",plan).Outcome);
    }
}
