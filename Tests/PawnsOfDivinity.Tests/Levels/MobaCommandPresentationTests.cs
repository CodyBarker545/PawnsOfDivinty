using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns.Restrictions;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaCommandPresentationTests
{
    [Theory]
    [InlineData(false,false,false,false,true,MobaCommandMode.None)]
    [InlineData(true,false,false,false,true,MobaCommandMode.Move)]
    [InlineData(true,true,false,false,true,MobaCommandMode.Attack)]
    [InlineData(true,false,true,false,true,MobaCommandMode.Abilities)]
    [InlineData(true,false,false,true,true,MobaCommandMode.Transfer)]
    [InlineData(true,false,false,false,false,MobaCommandMode.None)]
    public void CommandModeReadsExistingInteractionFlags(bool selected,bool attack,bool abilities,bool transfer,bool move,MobaCommandMode expected)
    {Assert.Equal(expected,MobaCommandPresentation.Mode(selected,attack,abilities,transfer,move));}
    [Theory] [InlineData(MobaCommandMode.Move)] [InlineData(MobaCommandMode.Attack)] [InlineData(MobaCommandMode.Abilities)] [InlineData(MobaCommandMode.Transfer)]
    public void DisabledCommandCanNeverBeActive(MobaCommandMode mode)
    {Assert.Equal(new(false,false),MobaCommandPresentation.State(false,mode,mode));Assert.Equal(new(true,true),MobaCommandPresentation.State(true,mode,mode));}
    [Fact] public void SwitchingModesClearsOtherCommandAccents()
    {
        foreach(var active in new[]{MobaCommandMode.Move,MobaCommandMode.Attack,MobaCommandMode.Abilities,MobaCommandMode.Transfer})
            foreach(var button in new[]{MobaCommandMode.Move,MobaCommandMode.Attack,MobaCommandMode.Abilities,MobaCommandMode.Transfer})
                Assert.Equal(active==button,MobaCommandPresentation.State(true,active,button).Active);
    }
    private static MobaHotseatSession Match()=>new(AuthoredBetaScene.Layout,timerConfiguration:new(false,30));
    [Theory] [InlineData(false)] [InlineData(true)]
    public void TauntProjectionUsesProductionPolicyWithoutStartingOrReleasingAnActivation(bool sourceDefeated)
    {
        var s=Match();s.Select("hooded_assassin");Assert.True(MobaCommandPresentation.CanEndActivation(s));
        var source=s.Characters["nigel"];
        Assert.True(s.Activations.ActionRestrictions.Apply(new TauntRestriction("polish-taunt","effect",source.Id,"hooded_assassin",1,1)).Applied);
        if(sourceDefeated)source.TakeDamage(source.CurrentHealth);
        Assert.Equal(sourceDefeated,MobaCommandPresentation.CanEndActivation(s));Assert.Equal(sourceDefeated,MobaCommandPresentation.CanPreviewAssassinAbility(s,"smoke_bomb"));
        Assert.Null(s.Activations.ActiveCharacter);Assert.NotNull(s.Activations.ActionRestrictions.GetActive("hooded_assassin"));
    }
    [Fact] public void AbilityInspectionDoesNotStartAnActivationOrSpendAnAction()
    {
        var s=Match();s.Select("hooded_assassin");Assert.True(MobaCommandPresentation.CanPreviewAssassinAbility(s,HoodedAssassin.SmokeBombId));
        Assert.True(MobaCommandPresentation.CanPreviewAssassinAbility(s,HoodedAssassin.ShadowStrikeId));Assert.Null(s.Activations.ActiveCharacter);Assert.Empty(s.SelectedCharacter!.Cooldowns.Cooldowns);
        Assert.False(s.Activations.MainActionUsed);
    }
    [Fact] public void SilenceAndUnsupportedAbilitiesDisablePreviewUsingProductionEligibility()
    {
        var s=Match();Assert.False(MobaCommandPresentation.CanPreviewAssassinAbility(s,"smoke_bomb"));s.Select("gentle_guardian");Assert.False(MobaCommandPresentation.CanPreviewAssassinAbility(s,"smoke_bomb"));
        s.Select("hooded_assassin");Assert.False(MobaCommandPresentation.CanPreviewAssassinAbility(s,"smoke_veil"));s.SelectedCharacter!.Statuses.ApplyTimedStatus(StatusType.Silence,2);
        Assert.False(MobaCommandPresentation.CanPreviewAssassinAbility(s,"smoke_bomb"));Assert.False(MobaCommandPresentation.CanPreviewAssassinAbility(s,"shadow_strike"));
        Assert.Null(s.Activations.ActiveCharacter);s.SelectedCharacter.Statuses.RemoveStatus(StatusType.Silence);s.BeginAttackTargeting();
        Assert.True(MobaCommandPresentation.CanPreviewAssassinAbility(s,"shadow_strike"));
    }
    [Fact] public void ActualCooldownDisablesOnlyTheAffectedAbilityAndRoundAndStatusRemainAuthoritative()
    {
        var s=Match();s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Select("hooded_assassin");s.BeginAssassinAbility("smoke_bomb");s.ExecuteAssassinAbility("cooldown",new(13,10));
        while(s.Round==1){s.Select(s.Characters.Values.First(c=>s.CanSelect(c.Id)).Id);s.EndSelectedActivation();}
        s.Select("hooded_assassin");Assert.False(MobaCommandPresentation.CanPreviewAssassinAbility(s,"smoke_bomb"));Assert.True(MobaCommandPresentation.CanPreviewAssassinAbility(s,"shadow_strike"));
        Assert.Null(s.Activations.ActiveCharacter);s.EndSelectedActivation();Assert.False(MobaCommandPresentation.CanPreviewAssassinAbility(s,"shadow_strike"));
    }
}
