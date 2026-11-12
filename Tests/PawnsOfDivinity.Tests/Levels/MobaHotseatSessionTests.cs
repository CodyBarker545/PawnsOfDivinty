using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class MobaHotseatSessionTests
{
    private static MobaHotseatSession Create(int startingPlayer=1)=>new(AuthoredBetaScene.LegacyLayout,startingPlayer);
    private static MobaBetaLayout Edit(Func<AuthoredElementData,AuthoredElementData> edit)=>new(
        AuthoredBetaScene.LegacyData with {Elements=AuthoredBetaScene.LegacyData.Elements.Select(edit).ToArray()});
    private static Character Ready(MobaHotseatSession s)=>s.Characters.Values.First(c=>s.CanSelect(c.Id));
    private static void End(MobaHotseatSession s,Character c)
    {
        Assert.Equal(BattlefieldOutcome.Completed,s.Select(c.Id).Outcome);
        Assert.Equal(BattlefieldOutcome.Completed,s.EndSelectedActivation().Outcome);
    }
    private static void CompleteRound(MobaHotseatSession s)
    {
        int round=s.Round;
        for(int n=0;n<s.Characters.Count&&s.Round==round;n++)End(s,Ready(s));
        Assert.Equal(round+1,s.Round);
    }
    [Fact] public void LegacyThreeVsThreeCreatesSixProductionCharactersWithStableBindings()
    {
        var s=Create();Assert.Equal(6,s.Characters.Count);
        foreach(var fixture in AuthoredBetaScene.LegacyLayout.Fixtures)
        {
            var c=s.Characters[fixture.CharacterId];
            Assert.Same(CharacterRegistry.GetCharacter(fixture.DefinitionId),c.Definition);
            Assert.Equal(fixture.Cell,c.TacticalPosition);Assert.Equal(fixture.TeamId,c.TeamId);
            Assert.Equal(c.BaseStats.MaxHealth,c.CurrentHealth);Assert.False(c.HasActedThisRound);
        }
    }
    [Fact] public void DefinitionMappingIsExplicitAndDoesNotAssumeFixtureIdIsClassId()
    {
        var s=new MobaHotseatSession(Edit(e=>e.Id=="hooded_assassin"?e with {DefinitionId="nigel"}:e));
        var c=s.Characters["hooded_assassin"];
        Assert.Equal("nigel",c.Definition!.Id);Assert.Equal("Nigel",c.Name);Assert.Equal(52,c.CurrentHealth);
    }
    [Fact] public void NodeEnumerationOrderDoesNotDetermineTeamsOrBinding()
    {
        var data=AuthoredBetaScene.LegacyData;
        var s=new MobaHotseatSession(new(data with {Elements=data.Elements.Reverse().ToArray()}));
        Assert.Equal(1,s.CurrentPlayer);Assert.Equal("hooded_assassin",s.Characters["hooded_assassin"].Definition!.Id);
    }
    [Theory] [InlineData(1)] [InlineData(2)]
    public void StartingPlayerIsExplicit(int first)
    {var s=Create(first);Assert.Equal(first,s.CurrentPlayer);CompleteRound(s);Assert.Equal(first,s.CurrentPlayer);}
    [Fact] public void SelectionSwitchAndDeselectionConsumeNothing()
    {
        var s=Create();var team=s.Characters.Values.Where(c=>c.TeamId==1).ToArray();
        Assert.Equal(BattlefieldOutcome.Completed,s.Select(team[0].Id).Outcome);
        Assert.Equal(BattlefieldOutcome.Completed,s.Select(team[1].Id).Outcome);
        Assert.Same(team[1],s.SelectedCharacter);Assert.Null(s.Activations.ActiveCharacter);
        s.Select(null);Assert.Null(s.SelectedCharacter);Assert.Equal(1,s.CurrentPlayer);
        Assert.All(s.Characters.Values,c=>Assert.False(c.HasActedThisRound));
    }
    [Fact] public void OpponentAndUnknownSelectionAreRejectedWithoutChangingState()
    {
        var s=Create();var friendly=Ready(s);s.Select(friendly.Id);
        Assert.Equal(BattlefieldOutcome.Rejected,s.Select("missing").Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.Select(s.Characters.Values.First(c=>c.TeamId==2).Id).Outcome);
        Assert.Same(friendly,s.SelectedCharacter);Assert.Equal(1,s.Round);Assert.Equal(1,s.CurrentPlayer);
    }
    [Fact] public void EndWithoutSelectionDoesNothing()
    {var s=Create();Assert.Equal(BattlefieldOutcome.Rejected,s.EndSelectedActivation().Outcome);Assert.Equal(1,s.Round);Assert.Equal(1,s.CurrentPlayer);}
    [Fact] public void EndUsesProductionActivationManagerAndPassesControl()
    {
        var s=Create();var c=Ready(s);End(s,c);
        Assert.True(c.HasActedThisRound);Assert.False(c.CanAct);Assert.Equal(2,s.CurrentPlayer);
        Assert.Null(s.SelectedCharacter);Assert.Null(s.Activations.ActiveCharacter);
        Assert.False(s.Activations.MovementUsed);Assert.False(s.Activations.MainActionUsed);
    }
    [Fact] public void UsedUnitCannotBeSelectedAgainInSameRound()
    {
        var s=Create();var used=Ready(s);End(s,used);End(s,Ready(s));
        Assert.Equal(1,s.CurrentPlayer);Assert.Equal(BattlefieldOutcome.Rejected,s.Select(used.Id).Outcome);
        Assert.False(s.Capture(used.Id).CanActivate);
    }
    [Fact] public void DefeatedUnitsCannotBeSelectedOrEnded()
    {
        var s=Create();var c=Ready(s);s.Select(c.Id);c.TakeDamage(c.CurrentHealth);
        Assert.Equal(BattlefieldOutcome.Rejected,s.Select(c.Id).Outcome);
        Assert.Equal(BattlefieldOutcome.Rejected,s.EndSelectedActivation().Outcome);
        Assert.False(c.HasActedThisRound);Assert.Null(s.Activations.ActiveCharacter);Assert.Equal(1,s.Round);
    }
    [Fact] public void PlayersAlternateAndRoundAutomaticallyResetsReadiness()
    {
        var s=Create();var order=new List<int>();var ids=new HashSet<string>();
        for(int i=0;i<6;i++){var c=Ready(s);order.Add(c.TeamId);Assert.True(ids.Add(c.Id));End(s,c);}
        Assert.Equal(new[]{1,2,1,2,1,2},order);Assert.Equal(2,s.Round);Assert.Equal(1,s.CurrentPlayer);
        Assert.All(s.Characters.Values,c=>Assert.False(c.HasActedThisRound));
    }
    [Fact] public void ExhaustedTeamIsSkippedByExistingManagerPolicy()
    {
        var s=Create();foreach(var c in s.Characters.Values.Where(c=>c.TeamId==1).Skip(1))c.TakeDamage(c.CurrentHealth);
        var order=new List<int>();while(s.Round==1){var c=Ready(s);order.Add(c.TeamId);End(s,c);}
        Assert.Equal(new[]{1,2,2,2},order);Assert.Equal(2,s.Round);
        Assert.All(s.Characters.Values.Where(c=>c.IsDefeated),c=>Assert.False(c.CanAct));
    }
    [Fact] public void ResetDoesNotHealReviveMoveOrReplaceCharacters()
    {
        var s=Create();var c=Ready(s);c.TakeDamage(5);int hp=c.CurrentHealth;var cell=c.TacticalPosition;
        CompleteRound(s);Assert.Same(c,s.Characters[c.Id]);Assert.Equal(hp,c.CurrentHealth);Assert.Equal(cell,c.TacticalPosition);
        CompleteRound(s);Assert.Equal(3,s.Round);Assert.Equal(hp,c.CurrentHealth);
    }
    [Fact] public void SnapshotReadsCurrentProductionState()
    {
        var s=Create();var c=Ready(s);var before=s.Capture(c.Id);c.TakeDamage(7);var after=s.Capture(c.Id);
        Assert.Equal(before.HP-7,after.HP);Assert.Equal(c.BaseStats.MaxHealth,after.MaxHP);
        Assert.Equal(c.EffectiveMovement,after.Movement);Assert.True(after.CanActivate);
    }
    [Fact] public void FullyDefeatedRosterWaitsWithoutInfiniteRoundAdvancement()
    {
        var s=Create();foreach(var c in s.Characters.Values)c.TakeDamage(c.CurrentHealth);
        Assert.Null(s.CurrentPlayer);Assert.Equal(1,s.Round);Assert.Equal(BattlefieldOutcome.Rejected,s.EndSelectedActivation().Outcome);
    }
    [Fact] public void InvalidDefinitionReportsStableFixtureId()
    {
        var error=Assert.Throws<ArgumentException>(()=>new MobaHotseatSession(Edit(e=>e.Id=="hooded_assassin"?e with {DefinitionId="Assassin"}:e)));
        Assert.Contains("hooded_assassin",error.Message);Assert.Contains("DefinitionId",error.Message);
    }
    [Fact] public void InvalidTeamIsRejected() => Assert.Throws<ArgumentException>(()=>
        new MobaHotseatSession(Edit(e=>e.Id=="hooded_assassin"?e with {TeamId=3}:e)));
    [Theory] [InlineData(0)] [InlineData(3)] [InlineData(-1)]
    public void InvalidStartingPlayerIsRejected(int player)=>Assert.Throws<ArgumentOutOfRangeException>(()=>Create(player));
    [Fact] public void MissingPlayerRosterIsRejected()
    {
        var data=AuthoredBetaScene.LegacyData;
        var map=new MobaBetaLayout(data with {Elements=data.Elements.Where(e=>e.Kind!="Fixture"||e.TeamId==1).ToArray()});
        Assert.Throws<ArgumentException>(()=>new MobaHotseatSession(map));
    }
    private static AuthoredLevelData AddSlot(AuthoredLevelData data,string id,int player,int x,int y)
    {
        var template=data.Elements.First(e=>e.Kind=="Fixture");var p=data.Board.CellCenter(new(x,y));
        return data with {Elements=data.Elements.Append(template with {Id=id,Path="CharacterFixtures/"+id,TeamId=player,X=p.X,Z=p.Z}).ToArray()};
    }
    [Fact] public void FourSlotsPerSideAreSupportedWhenAuthoredFixturesExist()
    {
        var data=AddSlot(AddSlot(AuthoredBetaScene.LegacyData,"fourth_blue",1,2,13),"fourth_red",2,27,13);
        var s=new MobaHotseatSession(new(data));Assert.Equal(8,s.Characters.Count);CompleteRound(s);
    }
    [Fact] public void SixthSlotIsRejected()
    {
        var data=AddSlot(AddSlot(AddSlot(AuthoredBetaScene.LegacyData,"fourth_blue",1,0,0),"fifth_blue",1,1,0),"sixth_blue",1,2,0);
        var error=Assert.Throws<ArgumentException>(()=>new MobaHotseatSession(new(data)));
        Assert.Contains("1–5",error.Message);
    }
}
