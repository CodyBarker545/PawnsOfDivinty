using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaCombatPresentationTests
{
    private sealed class Rolls(int value) : IRandomRollProvider
    {public Action? DuringRoll;public int NextRoll(){DuringRoll?.Invoke();return value;}}
    private static MobaHotseatSession Setup(string actor="hooded_assassin",string target="nigel",int player=1,int roll=0)
    {
        var s=new MobaHotseatSession(AuthoredBetaScene.Layout,player,new Rolls(roll));
        s.Characters[actor].SetTacticalPosition(new(14,10));s.Characters[target].SetTacticalPosition(new(15,10));s.Select(actor);s.BeginAttackTargeting();return s;
    }
    [Fact] public void HealthDisplayUsesRuntimeHpMaximumShieldAndDefeatState()
    {
        var s=Setup();var c=s.Characters["nigel"];c.TakeDamage(7);c.SetShieldHP(9);
        var state=MobaUnitPresentation.Capture(c);
        Assert.Equal(c.CurrentHealth,state.HP);Assert.Equal(c.BaseStats.MaxHealth,state.MaxHP);Assert.Equal(9,state.Shield);
        Assert.Equal((double)c.CurrentHealth/c.BaseStats.MaxHealth,state.HealthFraction);Assert.Contains("Shield 9",state.HealthText);
        Assert.True(state.ShowHealthBar);Assert.True(state.Pickable);Assert.False(state.Defeated);
        c.TakeDamage(int.MaxValue);state=MobaUnitPresentation.Capture(c);
        Assert.False(state.ShowHealthBar);Assert.False(state.Pickable);Assert.True(state.Defeated);Assert.Equal(0,state.HealthFraction);
        Assert.DoesNotContain("Shield",state.HealthText);
    }
    [Fact] public void OpeningCounterAndAttackerFollowUpKeepProductionOrderAndActualHealthLoss()
    {
        var s=Setup();var result=s.ExecuteBasicAttack("ordered","nigel");var record=Assert.Single(s.CombatHistory.Records);
        Assert.Equal(new[]{MobaFeedbackKind.Opening,MobaFeedbackKind.Counter,MobaFeedbackKind.FollowUp},record.Events.Select(e=>e.Kind));
        var combat=result.Combat!;Assert.Equal(combat.OpeningHealthLost,record.Events[0].HealthLost);
        Assert.Equal(combat.AttackerHealthLost,record.Events[1].HealthLost);Assert.Equal(combat.FollowUpHealthLost,record.Events[2].HealthLost);
        Assert.Equal(combat.DefenderHealthLost,record.Events.Where(e=>e.TargetId=="nigel").Sum(e=>e.HealthLost));
        Assert.Equal("nigel",record.Events[1].SourceId);Assert.Equal("hooded_assassin",record.Events[2].SourceId);
        Assert.Contains(s.Characters["nigel"].CurrentHealth.ToString(),record.Summary);
    }
    [Fact] public void DefenderFollowUpUsesCorrectParticipantsAndSeparatesCounterHpFromFollowUpHp()
    {
        var s=Setup("nigel","hooded_assassin",2);s.Characters["nigel"].SetShieldHP(5);
        var result=s.ExecuteBasicAttack("defender-follow","hooded_assassin");var events=Assert.Single(s.CombatHistory.Records).Events;
        Assert.Equal(FollowUpParticipant.Defender,result.Combat!.FollowUpBy);
        var counter=Assert.Single(events,e=>e.Kind==MobaFeedbackKind.Counter);var follow=Assert.Single(events,e=>e.Kind==MobaFeedbackKind.FollowUp);
        Assert.Equal("hooded_assassin",follow.SourceId);Assert.Equal("nigel",follow.TargetId);
        Assert.Equal(result.Combat.AttackerHealthLost,counter.HealthLost+follow.HealthLost);
        Assert.Equal(5,events.Sum(e=>e.ShieldAbsorbed));Assert.Contains(events,e=>e.Text.Contains("exchange total"));
    }
    [Theory] [InlineData(0,true)] [InlineData(99,false)]
    public void HitMissAndOpeningShieldFeedbackComeFromActualResults(int roll,bool hit)
    {
        var s=Setup("gentle_guardian","nigel",roll:roll);s.Characters["nigel"].SetTacticalPosition(new(16,10));s.Characters["nigel"].SetShieldHP(5);
        var result=s.ExecuteBasicAttack("shield","nigel");var record=Assert.Single(s.CombatHistory.Records);
        Assert.Equal(hit,record.Events[0].Hit);Assert.Contains(hit?"HIT":"MISS",record.Events[0].Text);
        Assert.Contains(hit?"HIT":"MISS",record.Events[0].FloatingText);
        Assert.Equal(result.Combat!.OpeningShieldAbsorbed,record.Events.Sum(e=>e.ShieldAbsorbed));
        Assert.Equal(result.Combat.DefenderHealthLost,record.Events.Sum(e=>e.HealthLost));
        Assert.Equal(hit?2:1,record.Events.Count);
        if(hit)Assert.Equal("SHIELD -5",record.Events[1].FloatingText);
    }
    [Fact] public void OverkillFeedbackUsesActualHpLostAndDefeatFollowsLethalStrike()
    {
        var s=Setup();var target=s.Characters["nigel"];target.TakeDamage(target.CurrentHealth-1);
        var result=s.ExecuteBasicAttack("lethal","nigel");var events=Assert.Single(s.CombatHistory.Records).Events;
        Assert.Equal(1,events[0].HealthLost);Assert.True(result.Combat!.OpeningStrike.Damage>1);
        Assert.Equal(new[]{MobaFeedbackKind.Opening,MobaFeedbackKind.Defeat},events.Select(e=>e.Kind));
        Assert.False(s.CanSelect(target.Id));Assert.False(MobaUnitPresentation.Capture(target).Pickable);
    }
    [Fact] public void DeathOnCounterIsRecordedBeforeAnyFollowUpAndTargetsTheAttacker()
    {
        var s=Setup();var actor=s.Characters["hooded_assassin"];actor.TakeDamage(actor.CurrentHealth-1);
        var result=s.ExecuteBasicAttack("counter-kill","nigel");var events=Assert.Single(s.CombatHistory.Records).Events;
        Assert.True(result.Combat!.AttackerDefeated);Assert.False(result.Combat.FollowUpOccurred);
        Assert.Equal(MobaFeedbackKind.Counter,events[^2].Kind);Assert.Equal(MobaFeedbackKind.Defeat,events[^1].Kind);
        Assert.Equal(actor.Id,events[^1].TargetId);
    }
    [Fact] public void RejectedAndDuplicateCommandsDoNotInventHistoryAndPreviewAddsNothing()
    {
        var s=Setup();s.PreviewAttack("nigel");Assert.Empty(s.CombatHistory.Records);
        s.ExecuteBasicAttack("invalid","gentle_guardian");Assert.Empty(s.CombatHistory.Records);
        s.ExecuteBasicAttack("once","nigel");s.ExecuteBasicAttack("once","nigel");Assert.Single(s.CombatHistory.Records);
    }
    [Fact] public void MultipleExchangesAreJournaledChronologicallyWithRoundAndPostCombatHp()
    {
        var s=Setup();s.ExecuteBasicAttack("first","nigel");s.Select("nigel");s.BeginAttackTargeting();s.ExecuteBasicAttack("second","hooded_assassin");
        Assert.Equal(new[]{"first","second"},s.CombatHistory.Records.Select(r=>r.RequestId));
        var text=s.CombatHistory.DisplayText();Assert.True(text.IndexOf("#1",StringComparison.Ordinal)<text.IndexOf("#2",StringComparison.Ordinal));
        Assert.Contains("Round 1",text);Assert.Contains("HP",text);
    }
    [Fact] public void ResetCreatesEntirelyFreshDomainStateWithAuthoredPositionsAndEmptyHistory()
    {
        var s=Setup();s.ExecuteBasicAttack("old","nigel");s.Characters["nigel"].Statuses.ApplyPoison();
        s.Characters["nigel"].Statuses.ApplyTimedStatus(StatusType.Frostbite,3);s.Characters["nigel"].SetShieldHP(8);
        s.Characters["nigel"].Cooldowns.RecordUse("test_only_cooldown",s.Round,3);
        s.Characters["gentle_guardian"].TakeDamage(int.MaxValue);
        var fresh=s.CreateResetSession(new Rolls(0));Assert.NotSame(s.Activations,fresh.Activations);Assert.NotSame(s.Lifecycle,fresh.Lifecycle);
        Assert.Empty(fresh.CombatHistory.Records);Assert.Null(fresh.SelectedCharacter);Assert.Null(fresh.Activations.ActiveCharacter);
        Assert.Null(fresh.PendingVisualMovement);Assert.False(fresh.RequiresManualRecovery);Assert.False(fresh.IsPresentationBusy);
        Assert.False(fresh.Activations.MainActionUsed);Assert.False(fresh.Activations.MovementUsed);Assert.Equal(1,fresh.Round);Assert.Equal(1,fresh.CurrentPlayer);
        foreach(var fixture in AuthoredBetaScene.Layout.Fixtures)
        {
            var c=fresh.Characters[fixture.CharacterId];Assert.NotSame(s.Characters[c.Id],c);Assert.Equal(fixture.Cell,c.TacticalPosition);
            Assert.Equal(c.BaseStats.MaxHealth,c.CurrentHealth);Assert.Equal(0,c.ShieldHP);Assert.False(c.IsDefeated);Assert.False(c.HasActedThisRound);
            Assert.False(c.Statuses.IsPoisoned);Assert.Empty(c.Statuses.TimedStatuses);
            Assert.Empty(c.Cooldowns.Cooldowns);
        }
        // Old attempted request IDs, selected actors and HP cannot leak into the reconstructed match.
        fresh.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));fresh.Characters["nigel"].SetTacticalPosition(new(15,10));
        fresh.Select("hooded_assassin");fresh.BeginAttackTargeting();Assert.Equal(BattlefieldOutcome.Completed,fresh.ExecuteBasicAttack("old","nigel").Outcome);
    }
    [Fact] public void ResetDiscardsPendingMovementAndKeepsStartingPlayerAndMapPolicy()
    {
        var s=new MobaHotseatSession(AuthoredBetaScene.Layout,2,new Rolls(0),false);s.Select("nigel");s.MoveTo(s.ReachableDestinations().First().Key);
        Assert.True(s.IsPresentationBusy);var fresh=s.CreateResetSession();Assert.False(fresh.IsPresentationBusy);Assert.Equal(2,fresh.CurrentPlayer);
        Assert.Equal(AuthoredBetaScene.Layout.Fixtures.Single(f=>f.CharacterId=="nigel").Cell,fresh.Characters["nigel"].TacticalPosition);
        fresh.Select("nigel");var origin=fresh.Characters["nigel"].TacticalPosition!.Value;
        fresh.Characters["hooded_assassin"].SetTacticalPosition(new(origin.X,origin.Y-1));fresh.BeginAttackTargeting();
        Assert.Equal(BattlefieldOutcome.Unresolved,fresh.PreviewAttack("hooded_assassin").Outcome);
    }
    [Fact] public void ResetCannotReenterAnInProgressCombatResolution()
    {
        var rolls=new Rolls(0);var s=new MobaHotseatSession(AuthoredBetaScene.Layout,1,rolls);
        s.Characters["gentle_guardian"].SetTacticalPosition(new(14,10));s.Characters["nigel"].SetTacticalPosition(new(16,10));
        s.Select("gentle_guardian");s.BeginAttackTargeting();
        rolls.DuringRoll=()=>{Assert.False(s.CanResetMatch);Assert.Throws<InvalidOperationException>(()=>s.CreateResetSession());};
        Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteBasicAttack("locked","nigel").Outcome);Assert.True(s.CanResetMatch);
    }
    [Fact] public void FullyShieldedHitRemainsAHitWithZeroActualHpLoss()
    {
        var s=Setup("gentle_guardian","nigel");var target=s.Characters["nigel"];target.SetTacticalPosition(new(16,10));target.SetShieldHP(100);
        int hp=target.CurrentHealth;var result=s.ExecuteBasicAttack("absorbed","nigel");var record=Assert.Single(s.CombatHistory.Records);
        Assert.True(record.Events[0].Hit);Assert.Equal(0,record.Events[0].HealthLost);Assert.Equal("HIT -0 HP",record.Events[0].FloatingText);
        Assert.Equal(hp,target.CurrentHealth);Assert.Equal(100-target.ShieldHP,record.Events.Sum(e=>e.ShieldAbsorbed));
        Assert.Equal(result.Combat!.OpeningShieldAbsorbed,record.Events[1].ShieldAbsorbed);
    }
    [Fact] public void ResetAfterLaterRoundRestoresRoundOneAndFreshLifecycleDependencies()
    {
        var s=new MobaHotseatSession(AuthoredBetaScene.Layout,2,new Rolls(0));
        while(s.Round==1){s.Select(s.Characters.Values.First(c=>s.CanSelect(c.Id)).Id);s.EndSelectedActivation();}
        Assert.Equal(2,s.Round);var fresh=s.CreateResetSession();Assert.Equal(1,fresh.Round);Assert.Equal(2,fresh.CurrentPlayer);
        Assert.NotSame(s.Lifecycle.MovementExecutor,fresh.Lifecycle.MovementExecutor);Assert.NotSame(s.MovementMap,fresh.MovementMap);
    }
    private sealed class FailingCounterRolls : IRandomRollProvider
    {private int _count;public int NextRoll()=>++_count==1?0:throw new InvalidOperationException("Injected counter roll failure");}
    [Fact] public void ResetRecoversFromPartialCombatWithoutReplayingOrInventingAnExchangeResult()
    {
        var s=new MobaHotseatSession(AuthoredBetaScene.Layout,1,new FailingCounterRolls());
        s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Characters["nigel"].SetTacticalPosition(new(15,10));
        s.Select("hooded_assassin");s.BeginAttackTargeting();var result=s.ExecuteBasicAttack("partial","nigel");
        Assert.Equal(BattlefieldOutcome.PartiallyApplied,result.Outcome);Assert.True(s.RequiresManualRecovery);
        Assert.True(s.Characters["nigel"].CurrentHealth<52);Assert.Empty(s.CombatHistory.Records);
        var fresh=s.CreateResetSession();Assert.False(fresh.RequiresManualRecovery);Assert.Equal(52,fresh.Characters["nigel"].CurrentHealth);
        Assert.Null(fresh.Activations.ActiveCharacter);Assert.Empty(fresh.CombatHistory.Records);
    }
}
