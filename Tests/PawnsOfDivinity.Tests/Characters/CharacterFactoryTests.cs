using Xunit;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Tests.Characters;

public class CharacterFactoryTests
{
    [Theory]
    [InlineData("nigel")]
    [InlineData("beothuk_spirit_guardian")]
    [InlineData("hooded_assassin")]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("gravebound_colossus")]
    public void Create_RegisteredId_CreatesReadyInstanceWithSharedStats(string characterId)
    {
        var definition = CharacterRegistry.GetCharacter(characterId);
        Assert.NotNull(definition);

        var character = CharacterFactory.Create(characterId, teamId: 2);

        Assert.NotNull(character);
        Assert.False(string.IsNullOrWhiteSpace(character.Id));
        Assert.NotEqual(characterId, character.Id);
        Assert.Equal(definition.Name, character.Name);
        Assert.Equal(2, character.TeamId);
        Assert.Same(definition.BaseStats, character.BaseStats);
        Assert.Equal(definition.BaseStats.MaxHealth, character.CurrentHealth);
        Assert.False(character.HasActedThisRound);
        Assert.False(character.IsDefeated);
        Assert.True(character.CanAct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    [InlineData("unknown_character")]
    [InlineData("HOODED_ASSASSIN")]
    [InlineData(" hooded_assassin ")]
    public void Create_InvalidCharacterId_ReturnsNullLikeRegistry(string? characterId)
    {
        Assert.Null(CharacterRegistry.GetCharacter(characterId));
        Assert.Null(CharacterFactory.Create(characterId, teamId: 1));
    }

    [Fact]
    public void Create_SameDefinitionAndTeam_GeneratesDifferentInstanceIds()
    {
        var first = CharacterFactory.Create("hooded_assassin", teamId: 1);
        var second = CharacterFactory.Create("hooded_assassin", teamId: 1);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Same(first.BaseStats, second.BaseStats);
    }

    [Fact]
    public void Create_InstancesHaveIndependentHealthActivationAndDefeatState()
    {
        var first = CharacterFactory.Create("hooded_assassin", teamId: 1);
        var second = CharacterFactory.Create("hooded_assassin", teamId: 2);
        Assert.NotNull(first);
        Assert.NotNull(second);

        first.EndActivation();

        Assert.True(first.HasActedThisRound);
        Assert.False(first.CanAct);
        Assert.False(second.HasActedThisRound);
        Assert.True(second.CanAct);

        first.TakeDamage(first.BaseStats.MaxHealth);
        second.EndActivation();
        first.ResetActivation();

        Assert.True(first.IsDefeated);
        Assert.Equal(0, first.CurrentHealth);
        Assert.False(first.HasActedThisRound);
        Assert.False(first.CanAct);
        Assert.False(second.IsDefeated);
        Assert.Equal(second.BaseStats.MaxHealth, second.CurrentHealth);
        Assert.True(second.HasActedThisRound);

        second.ResetActivation();
        Assert.True(second.CanAct);
        Assert.False(first.CanAct);
    }

    [Fact]
    public void Create_NullDefinition_ThrowsClearArgumentException()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            CharacterFactory.Create(null!, "instance_1", 1));

        Assert.Equal("definition", error.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Create_InvalidExplicitInstanceId_RejectsInput(string? instanceId)
    {
        var definition = CharacterRegistry.GetCharacter("hooded_assassin");
        Assert.NotNull(definition);

        Assert.Throws<ArgumentException>(() => CharacterFactory.Create(definition, instanceId!, 1));
    }

    [Theory]
    [InlineData("nigel")]
    [InlineData("beothuk_spirit_guardian")]
    [InlineData("hooded_assassin")]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("gravebound_colossus")]
    
    public void Factory_CreatesAllCharacters(string characterId)
    {
        // Arrange
        var definition = CharacterRegistry.GetCharacter(characterId);

        Assert.NotNull(definition);

        // Act
        var character = CharacterFactory.Create(
            definition,
            instanceId: $"player1_{characterId}",
            teamId: 1
        );

        // Assert
        Assert.NotNull(character);
        Assert.Equal($"player1_{characterId}", character.Id);
        Assert.Equal(definition.Name, character.Name);
        Assert.Same(definition.BaseStats, character.BaseStats);
        Assert.Equal(definition.BaseStats.MaxHealth, character.CurrentHealth);
        Assert.Equal(1, character.TeamId);
    }

    [Fact]
    public void Factory_CreatesIndependentCharacters()
    {
        // Arrange
        var definition = CharacterRegistry.GetCharacter("nigel");

        Assert.NotNull(definition);

        var warrior1 = CharacterFactory.Create(
            definition,
            "player1_komodo",
            1
        );

        var warrior2 = CharacterFactory.Create(
            definition,
            "player2_komodo",
            2
        );

        // Act
        warrior1.TakeDamage(20);

        // Assert
        Assert.Equal(32, warrior1.CurrentHealth);
        Assert.Equal(52, warrior2.CurrentHealth);
    }

    [Fact]
    public void Factory_AssignsCorrectTeamId()
    {
        var definition = CharacterRegistry.GetCharacter("hooded_assassin");

        Assert.NotNull(definition);

        var assassin = CharacterFactory.Create(
            definition,
            "player2_assassin",
            2
        );

        Assert.Equal(2, assassin.TeamId);
    }

    [Fact]
    public void Factory_AssignsUniqueInstanceIds()
    {
        var definition = CharacterRegistry.GetCharacter("hooded_assassin");

        Assert.NotNull(definition);

        var assassin1 = CharacterFactory.Create(
            definition,
            "assassin_001",
            1
        );

        var assassin2 = CharacterFactory.Create(
            definition,
            "assassin_002",
            2
        );

        Assert.NotEqual(assassin1.Id, assassin2.Id);
    }

    [Fact]
    public void Factory_NewCharacterStartsAtFullHealth()
    {
        var definition = CharacterRegistry.GetCharacter("gravebound_colossus");

        Assert.NotNull(definition);

        var guardian = CharacterFactory.Create(
            definition,
            "guardian_001",
            1
        );

        Assert.Equal(
            guardian.BaseStats.MaxHealth,
            guardian.CurrentHealth
        );
    }
}
