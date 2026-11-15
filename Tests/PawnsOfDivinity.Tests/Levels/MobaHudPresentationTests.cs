using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Equipment;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaHudPresentationTests
{
    private static MobaHotseatSession Match()=>new(AuthoredBetaScene.Layout,timerConfiguration:new(false,30));
    [Theory] [InlineData(1280,720)] [InlineData(1920,1080)] [InlineData(2560,1440)]
    public void HudReservesABoundedBottomAndMajorityBattlefield(double width,double height)
    {
        var m=MobaHudMetrics.For(width,height);Assert.InRange(m.Bottom,228,280);Assert.True(m.BattlefieldHeight>height*.55);
        Assert.False(m.ContainsBattlefield(10,20));Assert.False(m.ContainsBattlefield(10,height-20));
        Assert.True(m.ContainsBattlefield(width/2,(height-m.Bottom+m.Top)/2));Assert.False(m.ContainsBattlefield(width,100));
    }
    [Theory] [InlineData(1279,720)] [InlineData(1280,719)] [InlineData(double.NaN,720)]
    public void UnsupportedLogicalSizesFailExplicitly(double width,double height)=>Assert.Throws<ArgumentOutOfRangeException>(()=>MobaHudMetrics.For(width,height));
    [Fact] public void EmptySelectionHasNeutralPortraitDetailsAndInventoryWithoutSlotsOrLevels()
    {
        var s=Match();var p=MobaHudPresentation.Capture(s);Assert.Equal("No unit selected",p.Name);Assert.Equal(0,p.HealthFraction);Assert.Equal("—",p.Health);
        Assert.DoesNotContain("Level",p.Stats);Assert.DoesNotContain("slots",p.Inventory);Assert.Null(s.SelectedFixtureId);
    }
    [Fact] public void SelectionDisplaysActualRuntimeStatsHpShieldAndStatusesWithoutMutatingState()
    {
        var s=Match();s.Select("hooded_assassin");var c=s.SelectedCharacter!;c.TakeDamage(7);c.SetShieldHP(4);c.Statuses.ApplyTimedStatus(StatusType.Disarm,1);
        var p=MobaHudPresentation.Capture(s);Assert.Equal(c.Name,p.Name);Assert.Equal("BLUE TEAM",p.Team);Assert.Equal("31 / 38 HP · SHD 4",p.Health);
        Assert.Equal(31f/38,p.HealthFraction);Assert.Contains("STR 17",p.Stats);Assert.Contains("MOV 6",p.Stats);Assert.Contains("Disarm",p.Effects);
        Assert.Equal(31,c.CurrentHealth);Assert.False(c.HasActedThisRound);Assert.Null(s.Activations.ActiveCharacter);
    }
    [Fact] public void EquipmentUsesExistingScrollDataAndDoesNotInventItemCapacity()
    {
        var s=Match();s.Select("blue_time_weaver");s.SelectedCharacter!.EquipScroll(new ScrollDefinition("test","Authored test scroll"));
        var p=MobaHudPresentation.Capture(s);Assert.Contains("Authored test scroll",p.Inventory);Assert.Contains("unavailable",p.Inventory);Assert.DoesNotContain("slots",p.Inventory);
    }
    [Fact] public void MainActionCooldownIsShownFromProductionTrackerAndResetClearsHudState()
    {
        var s=Match();s.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));s.Select("hooded_assassin");s.BeginAssassinAbility("smoke_bomb");
        Assert.True(s.ExecuteAssassinAbility("hud-bomb",new(13,10)).Outcome==PawnsOfDivinity.Gameplay.BattlefieldOutcome.Completed);
        while(s.Round==1){s.Select(s.Characters.Values.First(c=>s.CanSelect(c.Id)).Id);s.EndSelectedActivation();}
        s.Select("hooded_assassin");Assert.Contains("Smoke Bomb: R4",MobaHudPresentation.Capture(s).Effects);
        var fresh=s.CreateResetSession();var p=MobaHudPresentation.Capture(fresh);Assert.Equal("No unit selected",p.Name);Assert.Null(fresh.SelectedFixtureId);Assert.Empty(fresh.CombatHistory.Records);
    }
    [Fact] public void TeamAndReadinessFollowTheRuntimeSelectionAndActivation()
    {
        var s=Match();s.Select("hooded_assassin");s.EndSelectedActivation();s.Select("nigel");var p=MobaHudPresentation.Capture(s);
        Assert.Equal("RED TEAM",p.Team);Assert.Equal("READY",p.Readiness);s.BeginAttackTargeting();Assert.Equal("ACTIVE",MobaHudPresentation.Capture(s).Readiness);
    }
}
