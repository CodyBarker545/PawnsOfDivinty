using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaHealthBarVisibilityTests
{
    private static readonly MobaUnitPresentation[] Units={new("blue","Blue",38,38,0,false),new("ally","Ally",44,44,5,false),new("enemy","Enemy",52,52,0,false),new("other","Other",30,64,0,false)};
    private static IReadOnlySet<string> Bars(string? selected,string? target=null,IEnumerable<MobaUnitPresentation>? units=null)=>MobaHealthBarVisibility.Resolve(selected,target,units??Units);
    [Fact] public void SelectingShowsOnlySelectedAndChangingSelectionHidesPrevious()
    {Assert.Equal(new[]{"blue"},Bars("blue"));Assert.Equal(new[]{"ally"},Bars("ally"));}
    [Fact] public void EnemyTargetShowsAlongsideSelectedAndSwitchingRemovesPreviousTarget()
    {Assert.True(Bars("blue","enemy").SetEquals(new[]{"blue","enemy"}));Assert.True(Bars("blue","other").SetEquals(new[]{"blue","other"}));}
    [Fact] public void FriendlyTargetApprovedByAnActiveCommandIsNotFilteredByTeam()
    {Assert.True(Bars("blue","ally").SetEquals(new[]{"blue","ally"}));}
    [Fact] public void CancelledOrCompletedTargetingLeavesOnlySelected()
    {Assert.Equal(new[]{"blue"},Bars("blue",null));}
    [Fact] public void DeselectionHidesEverythingIncludingStaleTarget()
    {Assert.Empty(Bars(null));Assert.Empty(Bars(null,"enemy"));}
    [Fact] public void SameSelectedAndTargetIdNeverProducesDuplicateBars()
    {Assert.Equal(new[]{"blue"},Bars("blue","blue"));}
    [Fact] public void RemovingSelectedPresentationAlsoHidesItsStaleTarget()
    {Assert.Empty(Bars("blue","enemy",Units.Where(u=>u.Id!="blue")));}
    [Fact] public void RemovingTargetPresentationKeepsSelectedBarOnly()
    {Assert.Equal(new[]{"blue"},Bars("blue","enemy",Units.Where(u=>u.Id!="enemy")));}
    [Fact] public void DefeatedSelectedUnitCannotKeepTargetBarVisible()
    {Assert.Empty(Bars("blue","enemy",Units.Select(u=>u.Id=="blue"?u with{HP=0,Defeated=true}:u)));}
    [Fact] public void DefeatedTargetIsHiddenImmediately()
    {Assert.Equal(new[]{"blue"},Bars("blue","enemy",Units.Select(u=>u.Id=="enemy"?u with{HP=0,Defeated=true}:u)));}
    [Fact] public void DamageAndShieldsOnUnrelatedUnitsNeverGrantVisibility()
    {Assert.Equal(new[]{"blue"},Bars("blue",units:Units.Select(u=>u with{HP=1,Shield=100})));}
    [Fact] public void UnknownOrEmptyPresentationCannotLeakAnyBars()
    {Assert.Empty(Bars("missing","enemy"));Assert.Equal(new[]{"blue"},Bars("blue","missing"));Assert.Empty(Bars("blue","enemy",Array.Empty<MobaUnitPresentation>()));}
}
