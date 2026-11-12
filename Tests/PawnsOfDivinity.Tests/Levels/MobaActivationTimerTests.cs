using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using PawnsOfDivinity.Core.Turns.Restrictions;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaActivationTimerTests
{
    private sealed class Time : IActivationTimeSource
    {
        private double _now;public Action? DuringRead;
        public double NowSeconds {get{DuringRead?.Invoke();return _now;}set=>_now=value;}
        public void Advance(double seconds)=>_now+=seconds;
    }
    private sealed class Rolls : IRandomRollProvider {public Action? DuringRoll;public int NextRoll(){DuringRoll?.Invoke();return 0;}}
    private static MobaHotseatSession Match(Time clock,ActivationTimerConfiguration? config=null,int first=1,IRandomRollProvider? rolls=null,DevelopmentScoringProfile? score=null)
        =>new(AuthoredBetaScene.Layout,first,rolls??new Rolls(),scoringProfile:score,timerConfiguration:config,timeSource:clock);
    private static void Tick(MobaHotseatSession s,Time t,double seconds){t.Advance(seconds);s.UpdateActivationTimer();}
    private static void AttackPositions(MobaHotseatSession s)
    {s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Characters["nigel"].SetTacticalPosition(new(15,10));s.Select("hooded_assassin");s.BeginAttackTargeting();}
    [Theory] [InlineData(0)] [InlineData(1000)] [InlineData(-1000)]
    public void CountdownBeginsBeforeSelectionAndDoesNotDependOnClockEpoch(double origin)
    {
        var time=new Time {NowSeconds=origin};var s=Match(time);Assert.Null(s.SelectedCharacter);Assert.Equal(1,s.ActivationTimer.PlayerId);
        Assert.Equal(30,s.ActivationTimer.RemainingSeconds);Assert.Equal(1,s.ActivationTimer.TurnSerial);Assert.Equal(ActivationTimerPause.None,s.ActivationTimer.Pause);
        Tick(s,time,7.5);Assert.Equal(22.5,s.ActivationTimer.RemainingSeconds,8);Assert.Equal(0,s.TimeoutCount);
    }
    [Theory] [InlineData(.5)] [InlineData(15)] [InlineData(90)] [InlineData(7200)]
    public void ConfiguredDurationIsNotLockedToThirty(double duration)
    {var time=new Time();var s=Match(time,new(true,duration));Tick(s,time,duration/2);Assert.Equal(duration/2,s.ActivationTimer.RemainingSeconds,8);}
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidDurationFailsClearly(double duration)=>Assert.Throws<ArgumentOutOfRangeException>(()=>Match(new(),new(true,duration)));
    [Fact] public void SelectionSwitchDeselectionAndAttackPreviewKeepSameDeadline()
    {
        var t=new Time();var s=Match(t);t.Advance(5);s.Select("hooded_assassin");Assert.Equal(25,s.ActivationTimer.RemainingSeconds);
        t.Advance(4);s.Select("gentle_guardian");s.Select(null);s.Select("hooded_assassin");Assert.Equal(21,s.ActivationTimer.RemainingSeconds);
        t.Advance(3);s.BeginAttackTargeting();s.PreviewAttack("nigel");Assert.Equal(18,s.ActivationTimer.RemainingSeconds);Assert.Equal(1,s.ActivationTimer.TurnSerial);
    }
    [Fact] public void ManualEndStartsFreshOpposingClockAndOnlyOnce()
    {
        var t=new Time();var s=Match(t);Tick(s,t,12);s.Select("hooded_assassin");s.EndSelectedActivation();
        Assert.Equal(2,s.CurrentPlayer);Assert.Equal(2,s.ActivationTimer.PlayerId);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);Assert.Equal(2,s.ActivationTimer.TurnSerial);
        s.EndSelectedActivation();Assert.Equal(2,s.ActivationTimer.TurnSerial);Tick(s,t,1);Assert.Equal(29,s.ActivationTimer.RemainingSeconds);
    }
    [Fact] public void TimeoutBeforeSelectionUsesLowestOrdinalEligibleIdNotVisualOrder()
    {
        var t=new Time();var s=Match(t);Tick(s,t,30);Assert.True(s.Characters["beothuk_spirit_guardian"].HasActedThisRound);
        Assert.False(s.Characters["hooded_assassin"].HasActedThisRound);Assert.Equal(1,s.TimeoutCount);Assert.Equal(2,s.CurrentPlayer);
        Assert.Null(s.SelectedCharacter);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);Assert.Contains("beothuk_spirit_guardian",s.LastTimeoutMessage);
    }
    [Fact] public void LargeElapsedIntervalForfeitsOnlyOneTurnWithoutCatchUpOrStaleExpiry()
    {var t=new Time();var s=Match(t);Tick(s,t,300);for(int i=0;i<10;i++)s.UpdateActivationTimer();Assert.Equal(1,s.TimeoutCount);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);}
    [Fact] public void TimeoutConsumesSelectedEligibleUnitAndRejectsDeadlineClickForNewTurn()
    {
        var t=new Time();var s=Match(t);s.Select("blue_time_weaver");t.Advance(30);
        Assert.Equal(BattlefieldOutcome.Rejected,s.Select("nigel").Outcome);Assert.True(s.Characters["blue_time_weaver"].HasActedThisRound);
        Assert.Null(s.SelectedCharacter);Assert.Equal(2,s.CurrentPlayer);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);
    }
    [Fact] public void TimeoutUsesCommittedActiveUnitEvenAfterDeselection()
    {
        var t=new Time();var s=Match(t);s.Select("gentle_guardian");var move=s.MoveTo(s.ReachableDestinations().First().Key);
        s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition!.Value);s.Select(null);Tick(s,t,30);
        Assert.True(s.Characters["gentle_guardian"].HasActedThisRound);Assert.Equal(move.FinalPosition,s.Characters["gentle_guardian"].TacticalPosition);
        Assert.False(s.Characters["beothuk_spirit_guardian"].HasActedThisRound);
    }
    [Fact] public void VisualMovementPausesAllElapsedTimeAndTimeoutAfterwardPreservesPositionAndCapture()
    {
        var t=new Time();var s=Match(t);s.Characters["hooded_assassin"].SetTacticalPosition(new(19,10));s.Select("hooded_assassin");Tick(s,t,29);
        var move=s.MoveTo(new(20,10));Assert.Equal(1,s.ActivationTimer.RemainingSeconds);Assert.Equal(ActivationTimerPause.MovementPresentation,s.ActivationTimer.Pause);
        Tick(s,t,100);Assert.Equal(0,s.TimeoutCount);Assert.Equal(1,s.ActivationTimer.RemainingSeconds);
        s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition!.Value);Assert.Equal(1,s.ActivationTimer.RemainingSeconds);
        Tick(s,t,1);Assert.Equal(2,s.CurrentPlayer);Assert.True(s.Characters["hooded_assassin"].HasActedThisRound);
        Assert.Equal(new GridPosition(20,10),s.Characters["hooded_assassin"].TacticalPosition);Assert.Equal(1,s.Control.Castle("red_forward").OwnerTeamId);
    }
    [Fact] public void ExpiredUnconfirmedAttackCannotRollOrDamageAndTargetingDoesNotResetTimer()
    {
        var t=new Time();var s=Match(t);AttackPositions(s);int hp=s.Characters["nigel"].CurrentHealth;
        t.Advance(30);Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteBasicAttack("late","nigel").Outcome);
        Assert.Equal(hp,s.Characters["nigel"].CurrentHealth);Assert.Empty(s.CombatHistory.Records);Assert.Equal(1,s.TimeoutCount);Assert.Equal(2,s.CurrentPlayer);
    }
    [Fact] public void CombatCannotBeInterruptedByInjectedElapsedTimeOrReentrantClockPolling()
    {
        var t=new Time();var rolls=new Rolls();var s=Match(t,rolls:rolls);AttackPositions(s);Tick(s,t,29.9);
        rolls.DuringRoll=()=>{t.Advance(60);Assert.False(s.UpdateActivationTimer());Assert.Equal(ActivationTimerPause.Command,s.ActivationTimer.Pause);Assert.Equal(0,s.TimeoutCount);};
        Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteBasicAttack("near-zero","nigel").Outcome);
        Assert.True(s.Characters["hooded_assassin"].HasActedThisRound);Assert.Equal(2,s.CurrentPlayer);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);Assert.Equal(0,s.TimeoutCount);
    }
    [Fact] public void CombatPresentationTicketPausesNewPlayerAndRejectsOldAcknowledgments()
    {
        var t=new Time();var s=Match(t);AttackPositions(s);s.ExecuteBasicAttack("hit","nigel");
        var token=s.HoldClockForCombatPresentation("hit")!;Tick(s,t,100);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);Assert.Equal(ActivationTimerPause.CombatPresentation,s.ActivationTimer.Pause);
        var newer=s.HoldClockForCombatPresentation("hit")!;Assert.False(s.AcknowledgeCombatClockPresentation(token));
        Assert.True(s.AcknowledgeCombatClockPresentation(newer));Tick(s,t,2);Assert.Equal(28,s.ActivationTimer.RemainingSeconds);Assert.Equal(0,s.TimeoutCount);
    }
    [Fact] public void TransferTargetingTimeoutCancelsUnconfirmedTransferWithoutMoving()
    {
        var t=new Time();var s=Match(t);s.Characters["hooded_assassin"].SetTacticalPosition(new(8,10));s.Select("hooded_assassin");s.BeginTransferTargeting();
        var plan=s.PreviewTransfer("blue_home",new(1,12)).Plan!;Assert.NotNull(plan);t.Advance(30);
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteTransfer("late",plan).Outcome);Assert.Equal(new GridPosition(8,10),s.Characters["hooded_assassin"].TacticalPosition);
        Assert.Equal(2,s.CurrentPlayer);Assert.True(s.Characters["hooded_assassin"].HasActedThisRound);Assert.False(s.Activations.MainActionUsed);
    }
    [Fact] public void TransferExecutionPausesClockAndHandsOffWithFullNextAllowance()
    {
        var t=new Time();var s=Match(t);s.Characters["hooded_assassin"].SetTacticalPosition(new(8,10));s.Select("hooded_assassin");s.BeginTransferTargeting();
        var plan=s.PreviewTransfer("blue_home",new(1,12)).Plan!;Tick(s,t,29.9);bool observed=false;
        t.DuringRead=()=>{if(!s.CanResetMatch&&s.ActivationTimer.Pause==ActivationTimerPause.Command){observed=true;t.Advance(60);}};
        Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteTransfer("near-zero",plan).Outcome);t.DuringRead=null;
        Assert.True(observed);Assert.Equal(new GridPosition(1,12),s.Characters["hooded_assassin"].TacticalPosition);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);Assert.Equal(0,s.TimeoutCount);
    }
    [Fact] public void DevelopmentPauseResumeAndDisableDoNotReplenishAnActivation()
    {
        var t=new Time();var s=Match(t);Tick(s,t,10);s.SetDevelopmentTimerPaused(true);Tick(s,t,500);Assert.Equal(20,s.ActivationTimer.RemainingSeconds);
        s.SetDevelopmentTimerPaused(false);Tick(s,t,2);Assert.Equal(18,s.ActivationTimer.RemainingSeconds);
        s.SetDevelopmentTimerEnabled(false);Tick(s,t,500);Assert.Equal(18,s.ActivationTimer.RemainingSeconds);Assert.Equal(0,s.TimeoutCount);
        s.SetDevelopmentTimerEnabled(true);Tick(s,t,3);Assert.Equal(15,s.ActivationTimer.RemainingSeconds);
    }
    [Fact] public void InspectorDisabledHotseatStaysUntimedAndResetRestoresItsConfiguration()
    {
        var t=new Time();var s=Match(t,new(false,45));Tick(s,t,1000);Assert.Equal(0,s.TimeoutCount);Assert.Equal(45,s.ActivationTimer.RemainingSeconds);
        s.SetDevelopmentTimerEnabled(true);Tick(s,t,5);Assert.Equal(40,s.ActivationTimer.RemainingSeconds);
        var fresh=s.CreateResetSession();Assert.False(fresh.ActivationTimer.Enabled);Assert.Equal(45,fresh.ActivationTimer.RemainingSeconds);
    }
    [Fact] public void DefeatedUnitsAreNotChosenAndNoEligibleTeamHasNoCountdown()
    {
        var t=new Time();var s=Match(t);s.Characters["beothuk_spirit_guardian"].TakeDamage(int.MaxValue);Tick(s,t,30);
        Assert.True(s.Characters["blue_gravebound_colossus"].HasActedThisRound);Assert.False(s.Characters["beothuk_spirit_guardian"].HasActedThisRound);
        foreach(var c in s.Characters.Values)c.TakeDamage(int.MaxValue);Tick(s,t,100);
        Assert.Equal(ActivationTimerPause.NoEligiblePlayer,s.ActivationTimer.Pause);Assert.Equal(1,s.TimeoutCount);Assert.Equal(1,s.Round);
    }
    [Fact] public void ConsecutiveSamePlayerActivationsStillReceiveDistinctFreshClocks()
    {
        var t=new Time();var s=Match(t);foreach(var c in s.Characters.Values.Where(c=>c.TeamId==2))c.TakeDamage(int.MaxValue);
        Tick(s,t,30);Assert.Equal(1,s.CurrentPlayer);Assert.Equal(2,s.ActivationTimer.TurnSerial);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);
        Tick(s,t,30);Assert.Equal(2,s.TimeoutCount);Assert.Equal(3,s.ActivationTimer.TurnSerial);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);
    }
    [Theory] [InlineData(1)] [InlineData(2)]
    public void TenTimeoutsUseExistingAlternationAndAdvanceRoundNormally(int first)
    {
        var t=new Time();var s=Match(t,first:first);
        for(int i=0;i<10;i++){Assert.Equal(i%2==0?first:3-first,s.CurrentPlayer);Tick(s,t,30);Assert.Equal(i+1,s.TimeoutCount);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);}
        Assert.Equal(2,s.Round);Assert.Equal(first,s.CurrentPlayer);Assert.Equal(11,s.ActivationTimer.TurnSerial);Assert.Equal(5,s.Readiness(1).Ready);Assert.Equal(5,s.Readiness(2).Ready);
    }
    [Fact] public void VictoryStopsClockPermanentlyUntilResetEvenAfterVeryLargeElapsedTime()
    {
        var t=new Time();var s=Match(t,score:new(0,0,0,15));s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));
        while(!s.Victory.IsFinished){s.Select(s.Characters.Values.First(c=>s.CanSelect(c.Id)).Id);s.EndSelectedActivation();}
        Assert.True(s.ActivationTimer.Stopped);Assert.Equal(ActivationTimerPause.Victory,s.ActivationTimer.Pause);Tick(s,t,100000);
        Assert.Equal(0,s.TimeoutCount);Assert.Equal(0,s.ActivationTimer.RemainingSeconds);Assert.Equal(BattlefieldOutcome.Rejected,s.SetDevelopmentTimerEnabled(true).Outcome);
        var reset=s.CreateResetSession();Assert.False(reset.ActivationTimer.Stopped);Assert.Equal(30,reset.ActivationTimer.RemainingSeconds);Assert.Equal(1,reset.ActivationTimer.PlayerId);
    }
    [Fact] public void ResetDropsDevelopmentOverridesAndOldPlaybackTokensWithSharedClockSource()
    {
        var t=new Time();var s=Match(t);AttackPositions(s);s.ExecuteBasicAttack("hit","nigel");var old=s.HoldClockForCombatPresentation("hit")!;
        s.SetDevelopmentTimerPaused(true);s.SetDevelopmentTimerEnabled(false);t.Advance(100);
        var fresh=s.CreateResetSession();Assert.NotEqual(s.ActivationTimer.MatchId,fresh.ActivationTimer.MatchId);Assert.True(fresh.ActivationTimer.Enabled);Assert.False(fresh.ActivationTimer.DevelopmentPaused);
        Assert.False(fresh.AcknowledgeCombatClockPresentation(old));Assert.Equal(0,fresh.TimeoutCount);Assert.Equal(30,fresh.ActivationTimer.RemainingSeconds);
        Tick(fresh,t,1);Assert.Equal(29,fresh.ActivationTimer.RemainingSeconds);Assert.Null(fresh.PendingCombatClockPresentation);
    }
    [Fact] public void BackwardsOrNonFiniteClockFailsWithoutConsumingActivation()
    {
        var t=new Time();var s=Match(t);Tick(s,t,5);t.NowSeconds=4;Assert.Throws<InvalidOperationException>(()=>s.UpdateActivationTimer());Assert.Equal(0,s.TimeoutCount);
        t.NowSeconds=double.NaN;Assert.Throws<InvalidOperationException>(()=>s.UpdateActivationTimer());Assert.Equal(1,s.CurrentPlayer);
    }
    [Fact] public void CountdownUrgencyIsPresentationOnlyAndCannotExpireEarly()
    {var t=new Time();var s=Match(t);Tick(s,t,26);Assert.True(s.ActivationTimer.IsUrgent);Assert.Equal(0,s.TimeoutCount);Tick(s,t,3.99);Assert.Equal(0,s.TimeoutCount);Tick(s,t,.02);Assert.Equal(1,s.TimeoutCount);}
    [Fact] public void TimeoutForfeitsTauntedActivationWithoutInventingAnAttackOrClearingRestriction()
    {
        var t=new Time();var s=Match(t);s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Characters["nigel"].SetTacticalPosition(new(15,10));
        var restriction=new TauntRestriction("timer-test-taunt","test-effect","nigel","hooded_assassin",1,1);
        Assert.True(s.Activations.ActionRestrictions.Apply(restriction).Applied);s.Select("hooded_assassin");s.BeginAttackTargeting();
        Assert.Throws<InvalidOperationException>(()=>s.EndSelectedActivation());int hp=s.Characters["nigel"].CurrentHealth;
        Tick(s,t,30);Assert.Equal(2,s.CurrentPlayer);Assert.Equal(1,s.TimeoutCount);Assert.False(s.RequiresManualRecovery);
        Assert.Equal(hp,s.Characters["nigel"].CurrentHealth);Assert.Empty(s.CombatHistory.Records);Assert.False(s.Activations.MainActionUsed);
        Assert.NotNull(s.Activations.ActionRestrictions.GetActive("hooded_assassin"));
        Assert.Throws<InvalidOperationException>(()=>s.Activations.ForfeitActivationForTimeout());
    }
    [Fact] public void CompletedAttackInvalidatesItsOldDeadlineWithoutChargingTheNextTurn()
    {
        var t=new Time();var s=Match(t);AttackPositions(s);Tick(s,t,29.9);s.ExecuteBasicAttack("completed","nigel");
        Tick(s,t,.2);Assert.Equal(0,s.TimeoutCount);Assert.Equal(2,s.CurrentPlayer);Assert.Equal(29.8,s.ActivationTimer.RemainingSeconds,8);
        Assert.Equal(BattlefieldOutcome.Rejected,s.EndSelectedActivation().Outcome);Assert.Equal(2,s.ActivationTimer.TurnSerial);
    }
    [Fact] public void ExpiredMovementPlanIsRejectedBeforeAnyPositionCommit()
    {
        var t=new Time();var s=Match(t);s.Select("gentle_guardian");var origin=s.SelectedCharacter!.TacticalPosition;
        var plan=s.PreviewMovement(s.ReachableDestinations().First().Key)!;t.Advance(30);var result=s.CommitMovement(plan);
        Assert.Equal(PawnsOfDivinity.MovementOutcome.Rejected,result.Outcome);Assert.Equal(origin,s.Characters["gentle_guardian"].TacticalPosition);
        Assert.Null(s.PendingVisualMovement);Assert.True(s.Characters["gentle_guardian"].HasActedThisRound);Assert.Equal(2,s.CurrentPlayer);
    }
    [Fact] public void ElapsedAccountingIsIndependentOfFrameChunkSizes()
    {
        var a=new Time();var b=new Time();var first=Match(a);var second=Match(b);
        foreach(double delta in new[]{.1,.65,2,6})Tick(first,a,delta);Tick(second,b,8.75);
        Assert.Equal(second.ActivationTimer.RemainingSeconds,first.ActivationTimer.RemainingSeconds,8);
    }
    [Fact] public void TimeoutForfeitEntryPointRejectsNonActivationPhaseWithoutEndingActor()
    {
        var s=Match(new());s.Activations.StartActivation(s.Characters["hooded_assassin"]);s.Lifecycle.AdvancePhase();
        Assert.Throws<InvalidOperationException>(()=>s.Activations.ForfeitActivationForTimeout());Assert.False(s.Characters["hooded_assassin"].HasActedThisRound);
    }
    [Fact] public void PartialCombatRecoveryPausesClockUntilFreshReset()
    {
        var t=new Time();int calls=0;var rolls=new Rolls();var s=Match(t,rolls:rolls);AttackPositions(s);Tick(s,t,29);
        rolls.DuringRoll=()=>{t.Advance(60);if(++calls==2)throw new InvalidOperationException("Injected resolution failure");};
        Assert.Equal(BattlefieldOutcome.PartiallyApplied,s.ExecuteBasicAttack("partial","nigel").Outcome);Assert.True(s.RequiresManualRecovery);
        Tick(s,t,100);Assert.Equal(ActivationTimerPause.Recovery,s.ActivationTimer.Pause);Assert.Equal(0,s.TimeoutCount);
        var fresh=s.CreateResetSession();Assert.False(fresh.RequiresManualRecovery);Assert.Equal(30,fresh.ActivationTimer.RemainingSeconds);
    }
    [Fact] public void RepeatedResetDuringMovementClearsOldAllowanceAndPendingTokens()
    {
        var t=new Time();var s=Match(t);
        for(int i=0;i<5;i++)
        {
            Tick(s,t,10);s.Select("gentle_guardian");var move=s.MoveTo(s.ReachableDestinations().First().Key);Assert.NotNull(s.PendingVisualMovement);
            var previous=s;s=s.CreateResetSession();Assert.NotEqual(previous.ActivationTimer.MatchId,s.ActivationTimer.MatchId);
            Assert.Equal(30,s.ActivationTimer.RemainingSeconds);Assert.Equal(0,s.TimeoutCount);Assert.Null(s.PendingVisualMovement);Assert.Equal(1,s.CurrentPlayer);
            Assert.Equal(BattlefieldOutcome.Rejected,s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition!.Value).Outcome);
        }
    }
    [Fact] public void UnknownPresentationCannotPauseTheClockOrReleaseAnotherMatchesPause()
    {
        var t=new Time();var s=Match(t);Assert.Null(s.HoldClockForCombatPresentation("unknown"));
        Assert.False(s.AcknowledgeCombatClockPresentation(new(Guid.NewGuid(),1)));Tick(s,t,5);
        Assert.Equal(25,s.ActivationTimer.RemainingSeconds);Assert.Equal(ActivationTimerPause.None,s.ActivationTimer.Pause);
    }
}
