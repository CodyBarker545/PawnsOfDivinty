using System.Text.Json;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaFiveVsFiveTests
{
    private sealed class HitRolls : IRandomRollProvider {public int NextRoll()=>0;}
    private static MobaHotseatSession Match(int first=1)=>new(AuthoredBetaScene.Layout,first,new HitRolls());
    [Fact] public void AuthoredMatchHasTenIndependentCharactersFiveUniqueDefinitionsPerTeamAndSixOriginalModels()
    {
        var s=Match();var map=AuthoredBetaScene.Layout;Assert.Equal(10,s.Characters.Count);Assert.Equal(10,map.Fixtures.Select(f=>f.CharacterId).Distinct().Count());
        Assert.Equal(6,map.Fixtures.Select(f=>f.DefinitionId).Distinct().Count());Assert.Equal(6,map.Fixtures.Select(f=>f.ModelPath).Distinct().Count());
        foreach(int team in new[]{1,2})
        {
            var units=s.Characters.Values.Where(c=>c.TeamId==team).ToArray();Assert.Equal(5,units.Length);
            Assert.Equal(5,units.Select(c=>c.Definition!.Id).Distinct().Count());Assert.Equal(5,s.Readiness(team).Ready);
            Assert.Equal(5,s.Readiness(team).Total);Assert.Equal(5,s.Readiness(team).Living);
        }
        foreach(var f in map.Fixtures){var c=s.Characters[f.CharacterId];Assert.Same(CharacterRegistry.GetCharacter(f.DefinitionId),c.Definition);Assert.Equal(f.TeamId,c.TeamId);}
    }
    [Theory]
    [InlineData("hooded_assassin",1,7,12)] [InlineData("gentle_guardian",1,8,12)] [InlineData("beothuk_spirit_guardian",1,9,12)]
    [InlineData("blue_time_weaver",1,7,13)] [InlineData("blue_gravebound_colossus",1,9,13)]
    [InlineData("nigel",2,22,12)] [InlineData("time_weaver",2,21,12)] [InlineData("gravebound_colossus",2,20,12)]
    [InlineData("red_hooded_assassin",2,22,13)] [InlineData("red_gentle_guardian",2,20,13)]
    public void EveryAuthoredSpawnIsAnUnoccupiedFriendlyDeploymentCell(string id,int team,int x,int y)
    {
        var map=AuthoredBetaScene.Layout;var f=map.Fixtures.Single(f=>f.CharacterId==id);Assert.Equal(team,f.TeamId);Assert.Equal(new GridPosition(x,y),f.Cell);
        Assert.True(map.Walkable(f.Cell));Assert.Contains(map.Castles,c=>c.InitialTeamId==team&&c.DeploymentCells.Contains(f.Cell));
        Assert.Single(map.Fixtures,other=>other.Cell==f.Cell);
    }
    [Fact] public void SpawnSetsAreHorizontallyMirroredAndDeploymentValidationSurvivesSerialization()
    {
        var map=AuthoredBetaScene.Layout;Assert.True(map.Source.RequireDeploymentSpawns);
        Assert.True(map.Fixtures.Where(f=>f.TeamId==1).Select(f=>map.Board.Mirror(f.Cell)).ToHashSet().SetEquals(map.Fixtures.Where(f=>f.TeamId==2).Select(f=>f.Cell)));
        var restored=new MobaBetaLayout(JsonSerializer.Deserialize<AuthoredLevelData>(JsonSerializer.Serialize(map.Source))!);
        Assert.True(restored.Source.RequireDeploymentSpawns);Assert.Equal(map.Fixtures,restored.Fixtures);
    }
    [Theory] [InlineData(14,10)] [InlineData(21,13)]
    public void NonDeploymentAndEnemyDeploymentSpawnsFailWithActionableFixtureIdentity(int x,int y)
    {
        var data=AuthoredBetaScene.Data;var p=data.Board.CellCenter(new(x,y));
        var error=Assert.Throws<LevelAuthoringException>(()=>new MobaBetaLayout(data with {Elements=data.Elements.Select(e=>e.Id=="blue_time_weaver"?e with {X=p.X,Z=p.Z}:e).ToArray()}));
        Assert.Contains("blue_time_weaver",error.Message);Assert.Contains("deployment zone belonging to team 1",error.Message);
    }
    [Theory] [InlineData(1)] [InlineData(2)]
    public void TenDistinctAlternatingActivationsCompleteARoundAndResetReadiness(int first)
    {
        var s=Match(first);var ids=new HashSet<string>();
        for(int i=0;i<10;i++)
        {
            int team=i%2==0?first:3-first;Assert.Equal(team,s.CurrentPlayer);
            var actor=s.Characters.Values.First(c=>s.CanSelect(c.Id));Assert.Equal(team,actor.TeamId);Assert.True(ids.Add(actor.Id));
            s.Select(actor.Id);s.EndSelectedActivation();
            if(i<9){Assert.True(actor.HasActedThisRound);Assert.Equal(BattlefieldOutcome.Rejected,s.Select(actor.Id).Outcome);Assert.Equal(10-(i+1),s.Readiness(1).Ready+s.Readiness(2).Ready);}
        }
        Assert.Equal(10,ids.Count);Assert.Equal(2,s.Round);Assert.Equal(first,s.CurrentPlayer);
        Assert.Equal(5,s.Readiness(1).Ready);Assert.Equal(5,s.Readiness(2).Ready);Assert.All(s.Characters.Values,c=>Assert.False(c.HasActedThisRound));
    }
    [Fact] public void DefeatedUnitsAreSkippedAndOnlySurvivorsBecomeReadyNextRound()
    {
        var s=Match();foreach(string id in new[]{"blue_time_weaver","nigel","red_gentle_guardian"})s.Characters[id].TakeDamage(int.MaxValue);
        Assert.Equal("BLUE: 4/5 READY",s.Readiness(1).Text);Assert.Equal("RED: 3/5 READY",s.Readiness(2).Text);
        var activated=new List<int>();while(s.Round==1){var c=s.Characters.Values.First(c=>s.CanSelect(c.Id));activated.Add(c.TeamId);s.Select(c.Id);s.EndSelectedActivation();}
        Assert.Equal(new[]{1,2,1,2,1,2,1},activated);Assert.Equal(4,s.Readiness(1).Ready);Assert.Equal(3,s.Readiness(2).Ready);
        Assert.All(s.Characters.Values.Where(c=>c.IsDefeated),c=>Assert.False(c.CanAct));
    }
    [Theory]
    [InlineData("hooded_assassin")] [InlineData("gentle_guardian")] [InlineData("beothuk_spirit_guardian")]
    [InlineData("blue_time_weaver")] [InlineData("blue_gravebound_colossus")]
    [InlineData("nigel")] [InlineData("time_weaver")] [InlineData("gravebound_colossus")]
    [InlineData("red_hooded_assassin")] [InlineData("red_gentle_guardian")]
    public void EveryFixtureSelectsMovesWithAllLivingOccupantsAndCanExecuteProductionCombat(string id)
    {
        var map=AuthoredBetaScene.Layout;var f=map.Fixtures.Single(f=>f.CharacterId==id);var s=Match(f.TeamId);
        Assert.Equal(BattlefieldOutcome.Completed,s.Select(id).Outcome);var actor=s.SelectedCharacter!;
        var occupied=s.Characters.Values.Where(c=>c.Id!=id).Select(c=>c.TacticalPosition!.Value).ToHashSet();Assert.Equal(9,occupied.Count);
        var routes=s.ReachableDestinations();Assert.NotEmpty(routes);
        foreach(var route in routes.Values){Assert.InRange(route.Cost,1,actor.EffectiveMovement);Assert.DoesNotContain(route.Cells.Skip(1),p=>occupied.Contains(p));}
        foreach(var cell in occupied){Assert.True(s.MovementMap.IsOccupied(cell));Assert.Null(s.PreviewMovement(cell));}
        var move=s.MoveTo(routes.First().Key);Assert.Equal(MovementOutcome.Completed,move.Outcome);Assert.Equal(move.FinalPosition,actor.TacticalPosition);
        Assert.True(s.MovementMap.IsOccupied(move.FinalPosition!.Value));Assert.False(s.MovementMap.IsOccupied(f.Cell));
        Assert.Equal(BattlefieldOutcome.Rejected,s.EndSelectedActivation().Outcome);s.AcknowledgeVisualMovement(move.RequestId,move.FinalPosition.Value);
        Assert.True(s.Activations.CanUseMainAction);Assert.Equal(BattlefieldOutcome.Completed,s.EndSelectedActivation().Outcome);
        // Separate unchanged fresh snapshot exercises combat for every independent instance ID.
        s=Match(f.TeamId);actor=s.Characters[id];var target=s.Characters.Values.First(c=>c.TeamId!=f.TeamId);
        actor.SetTacticalPosition(new(14,10));target.SetTacticalPosition(new(f.DefinitionId=="beothuk_spirit_guardian"?16:15,10));
        s.Select(id);s.BeginAttackTargeting();Assert.True(s.PreviewAttack(target.Id).CanExecute);
        Assert.All(s.Characters.Values.Where(c=>c.TeamId==f.TeamId),ally=>Assert.False(s.PreviewAttack(ally.Id).CanExecute));
        var result=s.ExecuteBasicAttack("all-ten-"+id,target.Id);Assert.Equal(BattlefieldOutcome.Completed,result.Outcome);
        Assert.True(actor.HasActedThisRound);Assert.Equal(3-f.TeamId,s.CurrentPlayer);var record=Assert.Single(s.CombatHistory.Records);
        Assert.Equal(id,record.Events[0].SourceId);Assert.Equal(target.Id,record.Events[0].TargetId);
    }
    [Fact] public void OpposingCopiesShareOnlyImmutableDefinitionAndMaintainIndependentCombatState()
    {
        var s=Match();var blue=s.Characters["blue_time_weaver"];var red=s.Characters["time_weaver"];
        Assert.Same(blue.Definition,red.Definition);Assert.NotSame(blue,red);Assert.NotSame(blue.Statuses,red.Statuses);Assert.NotSame(blue.Cooldowns,red.Cooldowns);
        blue.SetTacticalPosition(new(14,10));red.SetTacticalPosition(new(16,10));red.SetShieldHP(5);blue.Statuses.ApplyPoison();
        Assert.False(red.Statuses.IsPoisoned);s.Select(blue.Id);s.BeginAttackTargeting();var outcome=s.ExecuteBasicAttack("copies",red.Id);
        Assert.Equal(BattlefieldOutcome.Completed,outcome.Outcome);Assert.Equal(red.CurrentHealth,MobaUnitPresentation.Capture(red).HP);
        Assert.Equal(outcome.Combat!.DefenderHealthLost,s.CombatHistory.Records[0].Events.Where(e=>e.TargetId==red.Id).Sum(e=>e.HealthLost));
        Assert.True(blue.HasActedThisRound);Assert.False(red.HasActedThisRound);
    }
    [Fact] public void RepeatedResetRestoresAllTenStatesWithoutDuplicateRegistrations()
    {
        var s=Match(2);
        for(int cycle=0;cycle<5;cycle++)
        {
            var previous=s;s.Characters["blue_time_weaver"].TakeDamage(int.MaxValue);s.Characters["red_hooded_assassin"].Statuses.ApplyPoison();
            s.Characters["nigel"].SetShieldHP(4);s.Select("nigel");var plan=s.ReachableDestinations().First();s.MoveTo(plan.Key);
            s=s.CreateResetSession(new HitRolls());Assert.Equal(10,s.Characters.Count);Assert.Equal(10,s.Characters.Values.Distinct().Count());
            Assert.Equal(5,s.Activations.FirstTeam.Count);Assert.Equal(5,s.Activations.SecondTeam.Count);Assert.Equal(1,s.Round);Assert.Equal(2,s.CurrentPlayer);
            Assert.Null(s.SelectedCharacter);Assert.Null(s.PendingVisualMovement);Assert.Empty(s.CombatHistory.Records);Assert.False(s.RequiresManualRecovery);
            foreach(var f in AuthoredBetaScene.Layout.Fixtures)
            {
                var c=s.Characters[f.CharacterId];Assert.NotSame(previous.Characters[c.Id],c);Assert.Equal(f.Cell,c.TacticalPosition);Assert.Equal(f.TeamId,c.TeamId);
                Assert.Equal(c.BaseStats.MaxHealth,c.CurrentHealth);Assert.Equal(0,c.ShieldHP);Assert.False(c.Statuses.IsPoisoned);Assert.Empty(c.Statuses.TimedStatuses);
                Assert.Empty(c.Cooldowns.Cooldowns);Assert.True(c.CanAct);
            }
        }
    }
    [Fact] public void SixthAuthoredSlotIsRejectedButLegacyThreeAndFourPerSideRemainSupported()
    {
        var data=AuthoredBetaScene.Data;var template=data.Elements.Single(e=>e.Id=="hooded_assassin");var p=data.Board.CellCenter(new(8,13));
        var extra=template with {Id="sixth_blue",Path="CharacterFixtures/sixth_blue",X=p.X,Z=p.Z};
        var error=Assert.Throws<ArgumentException>(()=>new MobaHotseatSession(new(data with {Elements=data.Elements.Append(extra).ToArray()})));
        Assert.Contains("1–5",error.Message);Assert.Equal(6,new MobaHotseatSession(AuthoredBetaScene.LegacyLayout).Characters.Count);
    }
}
