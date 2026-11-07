using System.Linq;
using Xunit;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Tests.Characters;

public class CharacterRegistryTests
{
    [Theory]
    [InlineData("hooded_assassin", "Hooded Assassin")]
    [InlineData("gentle_guardian", "Gentle Guardian")]
    [InlineData("time_weaver", "Time Weaver")]
    [InlineData("nigel", "Nigel")]
    [InlineData("gravebound_colossus", "Gravebound Colossus")]
    [InlineData("beothuk_spirit_guardian", "Beothuk Spirit Guardian")]
    public void PlayableRoster_HasExpectedIdentityAndIndependentFactoryInstances(string id, string name)
    {
        var definition = CharacterRegistry.GetCharacter(id);
        Assert.NotNull(definition);
        Assert.Equal(id, definition.Id);
        Assert.Equal(name, definition.Name);

        var first = CharacterFactory.Create(id, teamId: 1);
        var second = CharacterFactory.Create(id, teamId: 2);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(name, first.Name);
        Assert.Equal(name, second.Name);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Same(definition.BaseStats, first.BaseStats);
        Assert.Same(first.BaseStats, second.BaseStats);
        Assert.Equal(definition.BaseStats.MaxHealth, first.CurrentHealth);
        Assert.Equal(definition.BaseStats.MaxHealth, second.CurrentHealth);

        first.TakeDamage(first.CurrentHealth);
        first.EndActivation();

        Assert.True(first.IsDefeated);
        Assert.True(first.HasActedThisRound);
        Assert.Equal(definition.BaseStats.MaxHealth, second.CurrentHealth);
        Assert.False(second.IsDefeated);
        Assert.True(second.CanAct);
    }

    [Theory]
    [InlineData("assassin")]
    [InlineData("healer")]
    [InlineData("dark_mage")]
    [InlineData("komodo_warrior")]
    [InlineData("iron_guardian")]
    [InlineData("shadow_ninja")]
    public void PlayableRoster_LegacyIdsAreNotRegistered(string legacyId)
    {
        Assert.Null(CharacterRegistry.GetCharacter(legacyId));
        Assert.Null(CharacterFactory.Create(legacyId, teamId: 1));
    }

    [Fact]
    public void Registry_ContainsSixCharacters()
    {
        var characters = CharacterRegistry.GetAllCharacters();

        Assert.Equal(6, characters.Count());
    }

    [Theory]
    [InlineData("nigel")]
    [InlineData("beothuk_spirit_guardian")]
    [InlineData("hooded_assassin")]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("gravebound_colossus")]
    public void Registry_CanFindAllCharacters(string characterId)
    {
        // Act
        var character = CharacterRegistry.GetCharacter(characterId);

        // Assert
        Assert.NotNull(character);
        Assert.Equal(characterId, character.Id);
    }

    [Fact]
    public void Registry_ReturnsNullForInvalidId()
    {
        var character = CharacterRegistry.GetCharacter("invalid_character");

        Assert.Null(character);
    }

    [Fact]
    public void Registry_ReturnsAllCharacterDefinitions()
    {
        var characters = CharacterRegistry.GetAllCharacters().ToList();

        Assert.NotEmpty(characters);

        foreach (var character in characters)
        {
            Assert.NotNull(character);
        }
    }

    [Fact]
    public void Registry_CharactersHaveUniqueNames()
    {
        var characters = CharacterRegistry.GetAllCharacters().ToList();

        var uniqueNames = characters
            .Select(c => c.Name)
            .Distinct()
            .Count();

        Assert.Equal(characters.Count, uniqueNames);
    }

    [Fact]
    public void Registry_CharactersHaveUniqueIds()
    {
        var characters = CharacterRegistry.GetAllCharacters().ToList();

        Assert.Equal(characters.Count, characters.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Registry_MissingLookupId_ReturnsNull(string? id)
    {
        Assert.Null(CharacterRegistry.GetCharacter(id));
    }

    [Theory]
    [InlineData("HOODED_ASSASSIN")]
    [InlineData("Hooded_Assassin")]
    [InlineData(" hooded_assassin ")]
    public void Registry_LookupRequiresExactId_ReturnsNullForDifferentText(string id)
    {
        Assert.Null(CharacterRegistry.GetCharacter(id));
    }

    public static IEnumerable<object[]> ExpectedDefinitions()
    {
        yield return new object[] { Nigel.Definition };
        yield return new object[] { BeothukSpiritGuardian.Definition };
        yield return new object[] { HoodedAssassin.Definition };
        yield return new object[] { GentleGuardian.Definition };
        yield return new object[] { TimeWeaver.Definition };
        yield return new object[] { GraveboundColossus.Definition };
    }

    [Theory]
    [MemberData(nameof(ExpectedDefinitions))]
    public void Registry_LookupReturnsExpectedDefinition_CompatibleWithFactory(CharacterDefinition expected)
    {
        var definition = CharacterRegistry.GetCharacter(expected.Id);
        Assert.NotNull(definition);
        Assert.Equal(expected, definition);
        Assert.Same(definition, CharacterRegistry.GetAllCharacters().Single(c => c.Id == expected.Id));

        var character = CharacterFactory.Create(definition, "instance_" + expected.Id, 1);

        Assert.Equal("instance_" + expected.Id, character.Id);
        Assert.Equal(expected.Name, character.Name);
        Assert.Equal(1, character.TeamId);
        Assert.Same(definition.BaseStats, character.BaseStats);
        Assert.Equal(expected.BaseStats.MaxHealth, character.CurrentHealth);
    }

    [Fact]
    public void Registry_EnumeratedCollection_RejectsMutation()
    {
        var before = CharacterRegistry.GetAllCharacters().ToList();
        var collection = Assert.IsAssignableFrom<ICollection<CharacterDefinition>>(CharacterRegistry.GetAllCharacters());

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(before[0]));
        Assert.Throws<NotSupportedException>(() => collection.Remove(before[0]));
        Assert.Throws<NotSupportedException>(() => collection.Clear());

        Assert.Equal(before, CharacterRegistry.GetAllCharacters().ToList());
        foreach (var definition in before)
            Assert.Same(definition, CharacterRegistry.GetCharacter(definition.Id));
    }
}
