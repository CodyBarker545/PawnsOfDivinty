using System;
using System.Linq;
using Xunit;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Tests.Characters;

public class CharacterDefinitionTests
{
    private static CharacterStats CreateStats() => new(
        MaxHealth: 30, Strength: 16, Defense: 5, Intelligence: 8,
        Resistance: 6, Wisdom: 5, Speed: 17, Movement: 0);

    [Fact]
    public void Constructor_ValidArguments_PreservesIdentityDescriptionAndStats()
    {
        var stats = CreateStats();

        var definition = new CharacterDefinition(
            Id: "test_character", Name: "Test Character",
            Description: "A test template.", BaseStats: stats);

        Assert.Equal("test_character", definition.Id);
        Assert.Equal("Test Character", definition.Name);
        Assert.Equal("A test template.", definition.Description);
        Assert.Same(stats, definition.BaseStats);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Constructor_MissingId_Throws(string? id)
    {
        var error = Assert.ThrowsAny<ArgumentException>(() =>
            new CharacterDefinition(id!, "Test Character", "Description", CreateStats()));

        Assert.Equal("Id", error.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Constructor_MissingName_Throws(string? name)
    {
        var error = Assert.ThrowsAny<ArgumentException>(() =>
            new CharacterDefinition("test_character", name!, "Description", CreateStats()));

        Assert.Equal("Name", error.ParamName);
    }

    [Fact]
    public void Constructor_NullStats_Throws()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            new CharacterDefinition("test_character", "Test Character", "Description", null!));

        Assert.Equal("BaseStats", error.ParamName);
    }

    [Fact]
    public void Constructor_NullDescription_Throws()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            new CharacterDefinition("test_character", "Test Character", null!, CreateStats()));

        Assert.Equal("Description", error.ParamName);
    }

    [Fact]
    public void Constructor_EmptyDescription_IsAllowed()
    {
        var definition = new CharacterDefinition("test_character", "Test Character", "", CreateStats());

        Assert.Equal("", definition.Description);
    }

    [Fact]
    public void Properties_AreGetOnly_PreventingIdentityOrStatsReplacement()
    {
        var properties = typeof(CharacterDefinition).GetProperties();

        Assert.Equal(4, properties.Length);
        Assert.All(properties, property =>
        {
            Assert.True(property.CanRead);
            Assert.Null(property.SetMethod);
        });
    }

    [Theory]
    [InlineData("nigel")]
    [InlineData("beothuk_spirit_guardian")]
    [InlineData("hooded_assassin")]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("gravebound_colossus")]
    public void RegisteredDefinition_FactoryCreatesInstance_WithoutChangingTemplate(string id)
    {
        var definition = CharacterRegistry.GetCharacter(id);
        Assert.NotNull(definition);
        var stats = definition.BaseStats;

        var character = CharacterFactory.Create(definition, "instance_" + id, 2);

        Assert.Equal("instance_" + id, character.Id);
        Assert.Equal(definition.Name, character.Name);
        Assert.Equal(2, character.TeamId);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(stats.MaxHealth, character.CurrentHealth);

        character.TakeDamage(1);

        Assert.Equal(stats.MaxHealth - 1, character.CurrentHealth);
        Assert.Equal(id, definition.Id);
        Assert.Same(stats, definition.BaseStats);
        Assert.Same(definition, CharacterRegistry.GetCharacter(id));
    }

    // Test 1: Every character has valid statistics

    [Fact]
    public void AllCharacters_HaveValidStats()
    {
        // Arrange
        var characters = CharacterRegistry.GetAllCharacters();

        // Act & Assert
        foreach (var character in characters)
        {
            var stats = character.BaseStats;

            Assert.True(stats.MaxHealth > 0);
            Assert.True(stats.Strength >= 0);
            Assert.True(stats.Defense >= 0);
            Assert.True(stats.Resistance >= 0);
            Assert.True(stats.Speed >= 0);
            Assert.True(stats.Movement >= 0);
            Assert.True(stats.Intelligence >= 0);
            Assert.True(stats.Wisdom >= 0);
        }
    }

    // Test 2: Every character has an ID and name

    [Fact]
    public void AllCharacters_HaveValidIdentity()
    {
        foreach (var character in CharacterRegistry.GetAllCharacters())
        {
            Assert.False(string.IsNullOrWhiteSpace(character.Id));
            Assert.False(string.IsNullOrWhiteSpace(character.Name));
            Assert.False(string.IsNullOrWhiteSpace(character.Description));
        }
    }

    // Test 3: Every character has a unique ID

    [Fact]
    public void AllCharacters_HaveUniqueIds()
    {
        var characters = CharacterRegistry.GetAllCharacters().ToList();

        var uniqueIds = characters
            .Select(character => character.Id)
            .Distinct()
            .Count();

        Assert.Equal(characters.Count, uniqueIds);
    }

    // Confirmed values from specification v0.2, sections 2 and 12.

    [Theory]
    [InlineData("hooded_assassin", 38, 17, 8, 5, 9, 4, 24, 6)]
    [InlineData("gentle_guardian", 44, 7, 11, 12, 15, 18, 14, 4)]
    [InlineData("time_weaver", 38, 6, 8, 17, 13, 14, 17, 5)]
    [InlineData("nigel", 52, 20, 11, 3, 8, 4, 13, 4)]
    [InlineData("gravebound_colossus", 64, 15, 22, 3, 9, 5, 7, 3)]
    [InlineData("beothuk_spirit_guardian", 40, 16, 9, 6, 12, 13, 18, 5)]
    public void Character_HasDocumentedStats(
        string characterId, int maxHealth, int strength, int defense, int intelligence,
        int resistance, int wisdom, int speed, int movement)
    {
        var definition = CharacterRegistry.GetCharacter(characterId);
        Assert.NotNull(definition);

        Assert.Equal(characterId, definition.Id);
        var stats = definition.BaseStats;
        Assert.Equal(maxHealth, stats.MaxHealth);
        Assert.Equal(strength, stats.Strength);
        Assert.Equal(defense, stats.Defense);
        Assert.Equal(intelligence, stats.Intelligence);
        Assert.Equal(resistance, stats.Resistance);
        Assert.Equal(wisdom, stats.Wisdom);
        Assert.Equal(speed, stats.Speed);
        Assert.Equal(movement, stats.Movement);
    }

    // Test 4: Check documented character health

    [Theory]
    [InlineData("nigel", 52)]
    [InlineData("beothuk_spirit_guardian", 40)]
    [InlineData("hooded_assassin", 38)]
    [InlineData("gentle_guardian", 44)]
    [InlineData("time_weaver", 38)]
    [InlineData("gravebound_colossus", 64)]
    public void Character_HasDocumentedMaxHealth(
        string characterId,
        int expectedHealth)
    {
        // Arrange
        var character = CharacterRegistry.GetCharacter(characterId);

        // Assert
        Assert.NotNull(character);

        Assert.Equal(
            expectedHealth,
            character.BaseStats.MaxHealth
        );
    }

    // Test 5: Verify documented Strength

    [Theory]
    [InlineData("nigel", 20)]
    [InlineData("beothuk_spirit_guardian", 16)]
    [InlineData("hooded_assassin", 17)]
    [InlineData("gentle_guardian", 7)]
    [InlineData("time_weaver", 6)]
    [InlineData("gravebound_colossus", 15)]
    public void Character_HasDocumentedStrength(
        string characterId,
        int expectedStrength)
    {
        var character = CharacterRegistry.GetCharacter(characterId);

        Assert.NotNull(character);

        Assert.Equal(
            expectedStrength,
            character.BaseStats.Strength
        );
    }
}
