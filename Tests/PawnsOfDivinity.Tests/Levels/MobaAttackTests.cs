using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class MobaAttackTests
{
    private sealed class Rolls(int value) : IRandomRollProvider
    { public int Count; public Action? DuringRoll; public int NextRoll(){Count++;DuringRoll?.Invoke();return value;} }
    private static MobaHotseatSession Setup(string actor="gentle_guardian",int distance=2,IRandomRollProvider? rolls=null,bool open=true)
    {
        var s=new MobaHotseatSession(AuthoredBetaScene.Layout,1,rolls??new Rolls(0),open);
        s.Characters[actor].SetTacticalPosition(new(14,10));s.Characters["nigel"].SetTacticalPosition(new(14+distance,10));
        Assert.Equal(BattlefieldOutcome.Completed,s.Select(actor).Outcome);return s;
    }
    [Fact] public void PreviewRequiresAttackModeAndDoesNotRollOrMutateHealthOrActions()
    {
        var rolls=new Rolls(0);var s=Setup(rolls:rolls);var hp=s.Characters["nigel"].CurrentHealth;
        Assert.False(s.PreviewAttack("nigel").CanExecute);Assert.True(s.CanAttack);
        Assert.Equal(BattlefieldOutcome.Completed,s.BeginAttackTargeting().Outcome);
        var p=s.PreviewAttack("nigel");Assert.True(p.CanExecute);Assert.Equal(90,p.HitChance);
        Assert.Equal(2,p.Distance);Assert.True(p.EstimatedOpeningDamage>0);
        Assert.Equal(0,rolls.Count);Assert.Equal(hp,s.Characters["nigel"].CurrentHealth);Assert.False(s.Activations.MainActionUsed);
    }
    [Theory]
    [InlineData("hooded_assassin",1,true)] [InlineData("hooded_assassin",2,false)]
    [InlineData("gentle_guardian",1,true)] [InlineData("gentle_guardian",2,true)] [InlineData("gentle_guardian",3,false)]
    [InlineData("beothuk_spirit_guardian",1,false)] [InlineData("beothuk_spirit_guardian",2,true)]
    [InlineData("beothuk_spirit_guardian",3,true)] [InlineData("beothuk_spirit_guardian",4,false)]
    public void ExistingDevelopmentLoadoutRangeIsSharedByPreviewAndExecution(string actor,int distance,bool legal)
    {
        var s=Setup(actor,distance);s.BeginAttackTargeting();
        Assert.Equal(legal,s.PreviewAttack("nigel").CanExecute);
        Assert.Equal(legal,s.ExecuteBasicAttack("range","nigel").Outcome==BattlefieldOutcome.Completed);
    }
    [Theory] [InlineData("gentle_guardian")] [InlineData("hooded_assassin")] [InlineData("unknown")]
    public void AlliesSelfAndUnknownTargetsAreRejectedWithoutResources(string target)
    {
        var s=Setup();s.BeginAttackTargeting();Assert.False(s.PreviewAttack(target).CanExecute);
        Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteBasicAttack("invalid",target).Outcome);Assert.False(s.Activations.MainActionUsed);
    }
    [Theory] [InlineData(0,true)] [InlineData(99,false)]
    public void ProductionHitMissShieldAndHealthAreAppliedAndMainActionEndsActivation(int roll,bool hit)
    {
        var s=Setup(rolls:new Rolls(roll));var target=s.Characters["nigel"];target.SetShieldHP(5);
        int hp=target.CurrentHealth;s.BeginAttackTargeting();var estimate=s.PreviewAttack("nigel");
        var result=s.ExecuteBasicAttack("hit","nigel");Assert.Equal(BattlefieldOutcome.Completed,result.Outcome);
        Assert.Equal(hit,result.Combat!.OpeningStrike.Hit);Assert.False(result.Combat.CounterattackOccurred);Assert.False(result.Combat.FollowUpOccurred);
        Assert.Equal(hit?Math.Max(0,estimate.EstimatedOpeningDamage!.Value-5):0,result.Combat.DefenderHealthLost);
        Assert.Equal(hp-result.Combat.DefenderHealthLost,target.CurrentHealth);
        Assert.Equal(hit?0:5,target.ShieldHP);Assert.True(s.Characters["gentle_guardian"].HasActedThisRound);
        Assert.True(s.Activations.MainActionUsed);Assert.Null(s.Activations.ActiveCharacter);Assert.Equal(2,s.CurrentPlayer);
        Assert.False(s.CanAttack);Assert.Equal(BattlefieldOutcome.Rejected,s.EndSelectedActivation().Outcome);
        Assert.Contains(hit?"HIT":"MISS",result.Reason);
    }
    [Fact] public void MovementThenAttackUsesCommittedPositionAndRetainsMainActionUntilAttack()
    {
        var s=Setup(distance:3);var move=s.MoveTo(new(15,10));Assert.Equal(MovementOutcome.Completed,move.Outcome);
        Assert.False(s.CanAttack);s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition!.Value);
        Assert.True(s.Activations.CanUseMainAction);s.BeginAttackTargeting();Assert.Equal(2,s.PreviewAttack("nigel").Distance);
        Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteBasicAttack("after-move","nigel").Outcome);
        Assert.Equal(new GridPosition(15,10),s.Characters["gentle_guardian"].TacticalPosition);
    }
    [Fact] public void ExecutionRevalidatesTargetPositionAndDefeat()
    {
        var s=Setup();s.BeginAttackTargeting();Assert.True(s.PreviewAttack("nigel").CanExecute);
        s.Characters["nigel"].SetTacticalPosition(new(18,10));Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteBasicAttack("stale","nigel").Outcome);
        s.Characters["nigel"].SetTacticalPosition(new(16,10));s.Characters["nigel"].TakeDamage(int.MaxValue);
        Assert.False(s.PreviewAttack("nigel").CanExecute);Assert.False(s.MovementMap.IsOccupied(new(16,10)));
        Assert.False(s.CanSelect("nigel"));Assert.False(s.Activations.MainActionUsed);
    }
    [Fact] public void DefeatedActorAndOpponentCannotAttack()
    {
        var s=Setup();Assert.Equal(BattlefieldOutcome.Rejected,s.Select("nigel").Outcome);
        s.SelectedCharacter!.TakeDamage(int.MaxValue);Assert.False(s.CanAttack);
        Assert.Equal(BattlefieldOutcome.Rejected,s.BeginAttackTargeting().Outcome);
    }
    [Fact] public void DuplicateAndReentrantCommandsCannotDealAdditionalDamage()
    {
        var rolls=new Rolls(0);var s=Setup(rolls:rolls);s.BeginAttackTargeting();
        rolls.DuringRoll=()=>{Assert.True(s.IsPresentationBusy);Assert.Equal(BattlefieldOutcome.Rejected,s.ExecuteBasicAttack("nested","nigel").Outcome);};
        Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteBasicAttack("once","nigel").Outcome);
        int hp=s.Characters["nigel"].CurrentHealth,count=rolls.Count;
        Assert.Equal(BattlefieldOutcome.AlreadyProcessed,s.ExecuteBasicAttack("once","nigel").Outcome);
        Assert.Equal(hp,s.Characters["nigel"].CurrentHealth);Assert.Equal(count,rolls.Count);
    }
    [Fact] public void ExistingPipelineAutomaticCounterAndFollowUpAreNotDuplicated()
    {
        var s=Setup("hooded_assassin",1);s.BeginAttackTargeting();var result=s.ExecuteBasicAttack("exchange","nigel");
        Assert.Equal(BattlefieldOutcome.Completed,result.Outcome);Assert.True(result.Combat!.CounterattackOccurred);Assert.True(result.Combat.FollowUpOccurred);
    }
    [Fact] public void UnknownMapTargetingPolicyFailsUnresolvedRatherThanInventingLineOfSight()
    {
        var s=Setup(open:false);s.BeginAttackTargeting();Assert.Equal(BattlefieldOutcome.Unresolved,s.PreviewAttack("nigel").Outcome);
        Assert.Equal(BattlefieldOutcome.Unresolved,s.ExecuteBasicAttack("unknown-policy","nigel").Outcome);Assert.False(s.Activations.MainActionUsed);
    }
    [Theory] [InlineData("nigel",1,true)] [InlineData("nigel",2,false)]
    [InlineData("gravebound_colossus",1,true)] [InlineData("gravebound_colossus",2,false)]
    [InlineData("time_weaver",2,true)] [InlineData("time_weaver",3,false)]
    public void RedFixtureDefinitionsUseExistingAxeLanceAndMagicRanges(string actor,int distance,bool legal)
    {
        var s=new MobaHotseatSession(AuthoredBetaScene.Layout,2,new Rolls(0));
        s.Characters[actor].SetTacticalPosition(new(14,10));s.Characters["gentle_guardian"].SetTacticalPosition(new(14+distance,10));
        s.Select(actor);s.BeginAttackTargeting();Assert.Equal(legal,s.PreviewAttack("gentle_guardian").CanExecute);
        Assert.Equal(legal,s.ExecuteBasicAttack("red","gentle_guardian").Outcome==BattlefieldOutcome.Completed);
    }
    [Fact] public void LethalAttackRemovesTargetEligibilityAndOccupancyWithoutRespawn()
    {
        var s=Setup();var target=s.Characters["nigel"];target.TakeDamage(target.CurrentHealth-1);s.BeginAttackTargeting();
        Assert.Equal(BattlefieldOutcome.Completed,s.ExecuteBasicAttack("lethal",target.Id).Outcome);
        Assert.True(target.IsDefeated);Assert.Equal(0,target.CurrentHealth);Assert.False(s.CanSelect(target.Id));
        Assert.False(s.MovementMap.IsOccupied(target.TacticalPosition!.Value));
    }
    [Fact] public void AttackAndSkippedActivationsFinishRoundAndResetLivingActors()
    {
        var s=Setup();s.BeginAttackTargeting();s.ExecuteBasicAttack("first","nigel");int round=s.Round;
        while(s.Round==round){s.Select(s.Characters.Values.First(c=>s.CanSelect(c.Id)).Id);s.EndSelectedActivation();}
        Assert.Equal(round+1,s.Round);Assert.All(s.Characters.Values,c=>Assert.False(c.HasActedThisRound));
        s.Select("gentle_guardian");Assert.True(s.CanAttack);s.BeginAttackTargeting();Assert.True(s.PreviewAttack("nigel").CanExecute);
    }
    [Fact] public void DisarmWithoutConfiguredUnarmedInputsIsExplicitlyUnresolved()
    {
        var s=Setup();s.SelectedCharacter!.Statuses.ApplyTimedStatus(StatusType.Disarm,2);s.BeginAttackTargeting();
        var p=s.PreviewAttack("nigel");Assert.Equal(BattlefieldOutcome.Unresolved,p.Outcome);Assert.Contains("unarmed",p.Reason);
        Assert.Equal(BattlefieldOutcome.Unresolved,s.ExecuteBasicAttack("disarmed","nigel").Outcome);Assert.False(s.Activations.MainActionUsed);
    }
}
