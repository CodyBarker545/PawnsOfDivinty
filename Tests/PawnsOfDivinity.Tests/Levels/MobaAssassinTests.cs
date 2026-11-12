using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaAssassinTests
{
    private sealed class Rolls(int value=0) : IRandomRollProvider {public int Calls;public Action? OnRoll;public int NextRoll(){Calls++;OnRoll?.Invoke();return value;}}
    private sealed class Time : IActivationTimeSource {public double NowSeconds {get;set;}}
    private static MobaHotseatSession Match(Rolls? rolls=null,AssassinDevelopmentProfile? profile=null,Time? time=null)=>new(AuthoredBetaScene.Layout,random:rolls??new(),
        timerConfiguration:new(time is not null,30),timeSource:time,assassinProfile:profile??new(true));
    private static void EndRound(MobaHotseatSession s)
    {
        int round=s.Round;if(s.SelectedCharacter is not null)s.EndSelectedActivation();
        while(s.Round==round&&!s.Victory.IsFinished){s.Select(s.Characters.Values.First(c=>s.CanSelect(c.Id)).Id);s.EndSelectedActivation();}
    }
    private static MobaAbilityResult Bomb(MobaHotseatSession s)
    {
        s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Select("hooded_assassin");
        Assert.Equal(BattlefieldOutcome.Completed,s.BeginAssassinAbility(HoodedAssassin.SmokeBombId).Outcome);
        var result=s.ExecuteAssassinAbility("bomb",new(13,10));Assert.Equal(BattlefieldOutcome.Completed,result.Outcome);return result;
    }
    private static void Approach(MobaHotseatSession s,string target="nigel")
    {
        EndRound(s);s.Characters[target].SetTacticalPosition(new(15,10));s.Select("hooded_assassin");Assert.True(s.Visibility("hooded_assassin").IsInvisible);
        var movement=s.MoveTo(new(14,10));Assert.Equal(PawnsOfDivinity.MovementOutcome.Completed,movement.Outcome);
        s.AcknowledgeVisualMovement(movement.RequestId,movement.FinalPosition!.Value);Assert.False(s.Visibility("hooded_assassin").IsInvisible);
        Assert.Equal(BattlefieldOutcome.Completed,s.BeginAssassinAbility(HoodedAssassin.ShadowStrikeId).Outcome);
    }
    [Fact] public void RegistryParametersStayAuthoritativeAndBothTeamCopiesOwnAbilities()
    {
        var s=Match();var bomb=HoodedAssassin.Definition.Abilities.Single(a=>a.Id==HoodedAssassin.SmokeBombId);Assert.Equal(3,bomb.ReuseInterval);Assert.Equal(AbilityActionType.MainAction,bomb.RequiredAction);
        var shadow=HoodedAssassin.Definition.Abilities.Single(a=>a.Id==HoodedAssassin.ShadowStrikeId);Assert.Equal(0,shadow.ReuseInterval);Assert.Equal(1,shadow.Range!.Minimum);
        s.Select("hooded_assassin");Assert.Equal(3,s.AssassinAbilities().Count);s.EndSelectedActivation();s.Select("red_hooded_assassin");Assert.Equal(3,s.AssassinAbilities().Count);
    }
    [Fact] public void SmokePreviewAndCancellationDoNotPlaceTeleportRollOrSpendResources()
    {
        var rolls=new Rolls();var s=Match(rolls);s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.SmokeBombId);
        var preview=s.PreviewAssassinAbility(new(13,10));Assert.True(preview.CanExecute);Assert.Equal(25,preview.Cells.Count);
        Assert.Empty(s.Smoke.ActiveAreas);Assert.Equal(new GridPosition(14,10),s.SelectedCharacter!.TacticalPosition);Assert.False(s.Activations.MainActionUsed);Assert.Equal(0,rolls.Calls);
        s.CancelAssassinTargeting();Assert.Null(s.TargetedAbilityId);Assert.False(s.PreviewAssassinAbility(new(13,10)).CanExecute);Assert.Empty(s.SelectedCharacter.Cooldowns.Cooldowns);
    }
    [Fact] public void SmokeBombCreatesExactCloudTeleportsAndFinalizesOneMainActionAndCooldown()
    {
        var s=Match();Bomb(s);var area=Assert.Single(s.Smoke.ActiveAreas);Assert.Equal(25,area.Tiles.Count);Assert.Equal(1,area.CreationRound);Assert.Equal(2,area.ExpirationRound);
        Assert.True(MobaBetaLayout.Rectangle(12,16,8,12).SetEquals(area.Tiles));var actor=s.Characters["hooded_assassin"];
        Assert.Equal(new GridPosition(13,10),actor.TacticalPosition);Assert.False(s.MovementMap.IsOccupied(new(14,10)));Assert.True(s.MovementMap.IsOccupied(new(13,10)));
        Assert.True(actor.HasActedThisRound);Assert.True(s.Activations.MainActionUsed);Assert.Equal(2,s.CurrentPlayer);Assert.Equal(4,actor.Cooldowns.Cooldowns[HoodedAssassin.SmokeBombId].ReadyRound);
        Assert.True(s.Visibility(actor.Id).IsInvisible);Assert.False(s.IsVisibleToPlayer(actor.Id,2));Assert.True(s.IsVisibleToPlayer(actor.Id,1));
        Assert.Equal(BattlefieldOutcome.AlreadyProcessed,s.ExecuteAssassinAbility("bomb",new(12,10)).Outcome);Assert.Single(s.Smoke.ActiveAreas);
    }
    [Theory] [InlineData(14,10)] [InlineData(20,10)] [InlineData(-1,10)]
    public void SmokeRejectsOccupiedSourceOutsideCloudAndOutsideBoard(int x,int y)
    {
        var s=Match();s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.SmokeBombId);
        Assert.False(s.PreviewAssassinAbility(new(x,y)).CanExecute);Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteAssassinAbility("invalid",new(x,y)).Outcome);
        Assert.Empty(s.Smoke.ActiveAreas);Assert.False(s.Activations.MainActionUsed);Assert.False(s.SelectedCharacter!.HasActedThisRound);
    }
    [Fact] public void SmokeRevalidatesDestinationOccupancyBeforeAnyEffect()
    {
        var s=Match();s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.SmokeBombId);
        Assert.True(s.PreviewAssassinAbility(new(13,10)).CanExecute);s.Characters["gentle_guardian"].SetTacticalPosition(new(13,10));
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteAssassinAbility("occupied",new(13,10)).Outcome);Assert.Empty(s.Smoke.ActiveAreas);Assert.Empty(s.SelectedCharacter!.Cooldowns.Cooldowns);
    }
    [Fact] public void SmokeDoesNotClipEdgesOrPermitTeleportIntoCastleWalls()
    {
        var s=Match();s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.SmokeBombId);
        Assert.False(s.PreviewAssassinAbility(new(6,12)).CanExecute);s.CancelAssassinTargeting();s.Characters["hooded_assassin"].SetTacticalPosition(new(0,0));s.BeginAssassinAbility(HoodedAssassin.SmokeBombId);
        Assert.False(s.PreviewAssassinAbility(new(1,0)).CanExecute);Assert.Empty(s.Smoke.ActiveAreas);
    }
    [Fact] public void DevelopmentSmokeObstaclePolicyCanRejectTheEntireUnclippedCloud()
    {
        var s=Match(profile:new(true,SmokeOverObstacles:false));s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.SmokeBombId);
        Assert.False(s.PreviewAssassinAbility(new(7,11)).CanExecute);Assert.Empty(s.Smoke.ActiveAreas);
    }
    [Fact] public void SmokeExpiresAfterFollowingRoundAndCooldownIsReadyOnlyAtR4()
    {
        var s=Match();Bomb(s);EndRound(s);Assert.Equal(2,s.Round);Assert.Single(s.Smoke.ActiveAreas);s.Select("hooded_assassin");
        Assert.Equal(BattlefieldOutcome.Rejected,s.BeginAssassinAbility(HoodedAssassin.SmokeBombId).Outcome);Assert.False(s.Activations.MainActionUsed);
        EndRound(s);Assert.Equal(3,s.Round);Assert.Empty(s.Smoke.ActiveAreas);Assert.False(s.Characters["hooded_assassin"].Cooldowns.IsReady(HoodedAssassin.SmokeBombId,3));
        EndRound(s);Assert.Equal(4,s.Round);s.Select("hooded_assassin");Assert.Equal(BattlefieldOutcome.Completed,s.BeginAssassinAbility(HoodedAssassin.SmokeBombId).Outcome);
    }
    [Fact] public void CasterDeathDoesNotRemoveCloudBeforeItsProductionExpiration()
    {var s=Match();Bomb(s);s.Characters["hooded_assassin"].TakeDamage(int.MaxValue);EndRound(s);Assert.Single(s.Smoke.ActiveAreas);EndRound(s);Assert.Empty(s.Smoke.ActiveAreas);}
    [Fact] public void HiddenAssassinCannotBeDirectlyTargetedButStillBlocksOccupancy()
    {
        var s=Match();Bomb(s);s.Characters["time_weaver"].SetTacticalPosition(new(15,10));s.Select("time_weaver");s.BeginAttackTargeting();
        Assert.False(s.PreviewAttack("hooded_assassin").CanExecute);Assert.Contains("invisible",s.PreviewAttack("hooded_assassin").Reason);
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteBasicAttack("hidden","hooded_assassin").Outcome);Assert.Null(s.PreviewMovement(new(13,10)));
        var move=s.MoveTo(new(14,10));s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition!.Value);
        Assert.True(s.Visibility("hooded_assassin").EnemyAdjacent);Assert.True(s.IsVisibleToPlayer("hooded_assassin",2));Assert.Equal(70,s.PreviewAttack("hooded_assassin").HitChance);
        var hit=s.ExecuteBasicAttack("revealed","hooded_assassin");Assert.Equal(BattlefieldOutcome.Completed,hit.Outcome);Assert.Equal(70,hit.Combat!.OpeningStrike.HitChance);
        Assert.True(s.Characters["hooded_assassin"].SmokeReveal.RevealedByAttack);Assert.Equal(20,SmokeVeil.GetEvasionModifier(s.Characters["hooded_assassin"],new(13,10),s.Smoke));
    }
    [Fact] public void VisibleShadowStrikeUsesPlusThreeOpeningAndNoDisarm()
    {
        var rolls=new Rolls();var s=Match(rolls);s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Characters["nigel"].SetTacticalPosition(new(15,10));s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.ShadowStrikeId);
        var p=s.PreviewAssassinAbility(targetId:"nigel");Assert.True(p.CanExecute);Assert.False(p.InvisibleOpening);Assert.Equal(0,rolls.Calls);
        var result=s.ExecuteAssassinAbility("visible",targetId:"nigel");Assert.Equal(BattlefieldOutcome.Completed,result.Outcome);Assert.Equal(3,result.Combat!.OpeningDamageBonus);Assert.Equal(p.OpeningDamage,result.Combat.OpeningStrike.Damage);
        Assert.False(result.Combat.OpeningDisarmApplied);Assert.True(result.Combat.FollowUpOccurred);Assert.True(s.Characters["hooded_assassin"].HasActedThisRound);Assert.Equal(2,s.CurrentPlayer);Assert.Single(s.CombatHistory.Records);
    }
    [Fact] public void RealPreApproachSnapshotGivesPlusFiveDisarmAndProductionUnarmedCounter()
    {
        var s=Match();Bomb(s);Approach(s);var p=s.PreviewAssassinAbility(targetId:"nigel");Assert.True(p.CanExecute);Assert.True(p.InvisibleOpening);
        int hp=s.Characters["nigel"].CurrentHealth;var result=s.ExecuteAssassinAbility("invisible",targetId:"nigel");
        Assert.Equal(BattlefieldOutcome.Completed,result.Outcome);Assert.Equal(5,result.Combat!.OpeningDamageBonus);Assert.True(result.Combat.OpeningDisarmApplied);
        Assert.True(s.Characters["nigel"].Statuses.HasStatus(StatusType.Disarm));Assert.Equal(70,result.Combat.CounterStrike!.HitChance);Assert.Equal(15,result.Combat.CounterStrike.Damage); // Existing post-hit Berserker's Fury applies.
        Assert.Equal(hp-result.Combat.DefenderHealthLost,s.Characters["nigel"].CurrentHealth);Assert.True(s.Characters["hooded_assassin"].SmokeReveal.RevealedByAttack);
        EndRound(s);Assert.False(s.Characters["nigel"].Statuses.HasStatus(StatusType.Disarm));Assert.Empty(s.Smoke.ActiveAreas);
    }
    [Fact] public void InvisibleOpeningMissRevealsButDoesNotApplyDisarm()
    {
        var s=Match(new(99));Bomb(s);Approach(s,"red_gentle_guardian");var result=s.ExecuteAssassinAbility("miss",targetId:"red_gentle_guardian");
        Assert.Equal(BattlefieldOutcome.Completed,result.Outcome);Assert.False(result.Combat!.OpeningStrike.Hit);Assert.False(result.Combat.OpeningDisarmApplied);
        Assert.False(s.Characters["red_gentle_guardian"].Statuses.HasStatus(StatusType.Disarm));Assert.True(s.Characters["hooded_assassin"].SmokeReveal.RevealedByAttack);
    }
    [Fact] public void MissingUnarmedConfigurationIsUnresolvedInsteadOfInventingCounterStats()
    {var s=Match(profile:new(false));Bomb(s);Approach(s);Assert.Equal(BattlefieldOutcome.Unresolved,s.PreviewAssassinAbility(targetId:"nigel").Outcome);Assert.False(s.Activations.MainActionUsed);}
    [Fact] public void DevelopmentUnarmedSettingsAreUsedByTheActualDisarmedCounter()
    {var s=Match(profile:new(true,2,75));Bomb(s);Approach(s);var result=s.ExecuteAssassinAbility("custom",targetId:"nigel");Assert.Equal(55,result.Combat!.CounterStrike!.HitChance);Assert.Equal(17,result.Combat.CounterStrike.Damage);}
    [Theory] [InlineData("hooded_assassin")] [InlineData("gentle_guardian")] [InlineData("unknown")]
    public void ShadowRejectsSelfAlliesAndUnknownTargets(string id)
    {var s=Match();s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.ShadowStrikeId);Assert.False(s.PreviewAssassinAbility(targetId:id).CanExecute);Assert.False(s.Activations.MainActionUsed);}
    [Fact] public void OtherHeroesPassiveAndUnknownAbilitiesCannotBeManuallyExecuted()
    {
        var s=Match();s.Select("gentle_guardian");Assert.Empty(s.AssassinAbilities());Assert.Equal(BattlefieldOutcome.Rejected,s.BeginAssassinAbility(HoodedAssassin.SmokeBombId).Outcome);
        s.Select("hooded_assassin");Assert.Equal(BattlefieldOutcome.Rejected,s.BeginAssassinAbility(HoodedAssassin.SmokeVeilId).Outcome);Assert.Equal(BattlefieldOutcome.Rejected,s.BeginAssassinAbility("new_ability").Outcome);
    }
    [Fact] public void SilenceWithUnknownSpellTagsRemainsExplicitlyRejected()
    {var s=Match();s.Select("hooded_assassin");s.SelectedCharacter!.Statuses.ApplyTimedStatus(StatusType.Silence,1);Assert.Equal(BattlefieldOutcome.Rejected,s.BeginAssassinAbility(HoodedAssassin.SmokeBombId).Outcome);Assert.False(s.Activations.MainActionUsed);}
    [Fact] public void AbilitySelectionConsumesClockTimeAndTimeoutCancelsWithoutAnyEffect()
    {
        var time=new Time();var s=Match(time:time);s.Select("hooded_assassin");time.NowSeconds=10;s.BeginAssassinAbility(HoodedAssassin.SmokeBombId);Assert.Equal(20,s.ActivationTimer.RemainingSeconds);
        s.PreviewAssassinAbility(new(7,11));time.NowSeconds=30;Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteAssassinAbility("expired",new(7,11)).Outcome);
        Assert.Equal(1,s.TimeoutCount);Assert.Equal(2,s.CurrentPlayer);Assert.Empty(s.Smoke.ActiveAreas);Assert.Null(s.TargetedAbilityId);Assert.Empty(s.Characters["hooded_assassin"].Cooldowns.Cooldowns);
    }
    [Fact] public void ShadowResolutionPausesClockEvenWhenRollsAdvanceTime()
    {
        var time=new Time();var rolls=new Rolls();var s=Match(rolls,time:time);s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Characters["nigel"].SetTacticalPosition(new(15,10));
        s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.ShadowStrikeId);time.NowSeconds=29.9;rolls.OnRoll=()=>{time.NowSeconds+=100;s.UpdateActivationTimer();};
        Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteAssassinAbility("near-zero",targetId:"nigel").Outcome);Assert.Equal(0,s.TimeoutCount);Assert.Equal(30,s.ActivationTimer.RemainingSeconds);
    }
    [Fact] public void ResetClearsCloudCooldownRevealDisarmAndTargetingAndRetainsDevelopmentInputs()
    {
        var s=Match();Bomb(s);Approach(s);s.ExecuteAssassinAbility("strike",targetId:"nigel");var fresh=s.CreateResetSession();
        Assert.Empty(fresh.Smoke.ActiveAreas);Assert.Equal(s.AssassinProfile,fresh.AssassinProfile);Assert.Null(fresh.TargetedAbilityId);Assert.Empty(fresh.CombatHistory.Records);
        Assert.All(fresh.Characters.Values,c=>{Assert.False(c.SmokeReveal.RevealedByAttack);Assert.False(c.Statuses.HasStatus(StatusType.Disarm));Assert.Empty(c.Cooldowns.Cooldowns);});
    }
    [Fact] public void IndependentAssassinCopiesShareNoCloudCooldownOrRevealState()
    {
        var s=Match();Bomb(s);s.Select("red_hooded_assassin");Assert.Equal(BattlefieldOutcome.Completed,s.BeginAssassinAbility(HoodedAssassin.SmokeBombId).Outcome);
        var destination=s.PreviewAssassinAbility().Cells.First(c=>s.PreviewAssassinAbility(c).CanExecute);s.ExecuteAssassinAbility("red-bomb",destination);
        Assert.Equal(2,s.Smoke.ActiveAreas.Count);Assert.Equal(2,s.Smoke.ActiveAreas.Select(a=>a.OwnerCharacterId).Distinct().Count());
        s.Characters["hooded_assassin"].SmokeReveal.RecordAttack();Assert.False(s.Characters["red_hooded_assassin"].SmokeReveal.RevealedByAttack);
    }
    [Fact] public void SmokeAddsNoMovementCostAndLeavingItRemovesVeilWithoutResettingRevealLatch()
    {
        var s=Match();Bomb(s);EndRound(s);s.Characters["hooded_assassin"].SmokeReveal.RecordAttack();s.Select("hooded_assassin");
        Assert.False(s.Visibility("hooded_assassin").IsInvisible);Assert.Equal(20,SmokeVeil.GetEvasionModifier(s.SelectedCharacter!,new(13,10),s.Smoke));
        var plan=s.PreviewMovement(new(11,10));Assert.NotNull(plan);Assert.Equal(2,plan.Cost);
        var result=s.CommitMovement(plan);s.AcknowledgeVisualMovement(result.RequestId,result.FinalPosition!.Value);
        Assert.Equal(0,SmokeVeil.GetEvasionModifier(s.SelectedCharacter!,new(11,10),s.Smoke));Assert.True(s.SelectedCharacter!.SmokeReveal.RevealedByAttack);
    }
    private sealed class Map(MobaHotseatSession s) : ISmokeTeleportMap
    {public bool? IsValidPosition(GridPosition p)=>s.MovementMap.IsValidPosition(p);public bool? CanTeleportTo(GridPosition p)=>s.MovementMap.IsValidPosition(p);public bool? IsOccupied(GridPosition p)=>s.MovementMap.IsOccupied(p);}
    [Fact] public void SeparateTeleportToExistingSmokeRemainsUnresolvedAndCannotMoveForFree()
    {
        var s=Match();Bomb(s);EndRound(s);s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.ShadowStrikeId);
        var actor=s.SelectedCharacter!;var coordinator=new SmokeTeleportActionCoordinator(new SmokeTeleportService(s.Smoke),s.Activations);
        var preview=coordinator.Preview(actor,new(12,10),new Map(s),new(false,false,false));Assert.Equal(SmokeTeleportActionRejectionReason.UnresolvedActionCost,preview.RejectionReason);
        coordinator.Execute(actor,preview,new Map(s));Assert.Equal(new GridPosition(13,10),actor.TacticalPosition);Assert.False(s.Activations.MainActionUsed);
    }
    [Fact] public void InvisibleLethalOpeningDoesNotDisarmOrCounterWithADeadTarget()
    {
        var s=Match();Bomb(s);Approach(s);s.Characters["nigel"].TakeDamage(51);var result=s.ExecuteAssassinAbility("lethal",targetId:"nigel");
        Assert.True(result.Combat!.DefenderDefeated);Assert.False(result.Combat.OpeningDisarmApplied);Assert.False(result.Combat.CounterattackOccurred);
        Assert.False(s.MovementMap.IsOccupied(new(15,10)));Assert.Equal(1,s.Victory.Pending(1));
    }
    [Fact] public void StaleAbilityAfterSelectionSwitchCannotExecuteAndResetCanReuseRequestId()
    {
        var s=Match();s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.SmokeBombId);s.Select(null);s.Select("hooded_assassin");
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteAssassinAbility("cancelled",new(7,11)).Outcome);Assert.Empty(s.Smoke.ActiveAreas);
        Bomb(s);var fresh=s.CreateResetSession();Bomb(fresh);Assert.Single(fresh.Smoke.ActiveAreas);
    }
    [Fact] public void InvisibleApproachCanLeaveSmokeWhileCurrentCounterEvasionCorrectlyDropsToZero()
    {
        var s=Match();Bomb(s);EndRound(s);s.Characters["nigel"].SetTacticalPosition(new(10,10));s.Select("hooded_assassin");
        var move=s.MoveTo(new(11,10));s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition!.Value);Assert.False(s.Visibility("hooded_assassin").IsInSmoke);
        s.BeginAssassinAbility(HoodedAssassin.ShadowStrikeId);Assert.True(s.PreviewAssassinAbility(targetId:"nigel").InvisibleOpening);
        var result=s.ExecuteAssassinAbility("outside",targetId:"nigel");Assert.Equal(5,result.Combat!.OpeningDamageBonus);Assert.Equal(90,result.Combat.CounterStrike!.HitChance);
    }
    [Fact] public void TimeoutInvalidatesPreApproachAndAbilityHandlesAcrossRounds()
    {
        var t=new Time();var s=Match(time:t);Bomb(s);Approach(s);Assert.True(s.PreviewAssassinAbility(targetId:"nigel").InvisibleOpening);
        t.NowSeconds=30;s.UpdateActivationTimer();Assert.Null(s.TargetedAbilityId);EndRound(s);s.Select("hooded_assassin");s.BeginAssassinAbility(HoodedAssassin.ShadowStrikeId);
        Assert.False(s.PreviewAssassinAbility(targetId:"nigel").InvisibleOpening);Assert.Empty(s.Smoke.ActiveAreas);
    }
}
