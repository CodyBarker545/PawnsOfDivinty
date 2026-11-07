using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class HealingCalculatorTests
{
    [Theory]
    [InlineData("gentle_guardian", 11)]
    [InlineData("time_weaver", 9)]
    public void HealingLight_DocumentedWisdom_ProducesExpectedAmount(string characterId, int expected)
    {
        var definition = CharacterRegistry.GetCharacter(characterId);
        Assert.NotNull(definition);

        // Checking the formula does not give this character the Healing Light ability.
        Assert.Equal(expected, HealingCalculator.CalculateHealingLight(definition.BaseStats));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(17, 10)]
    [InlineData(18, 11)]
    [InlineData(19, 11)]
    [InlineData(int.MaxValue - 1, 1073741825)]
    [InlineData(int.MaxValue, 1073741825)]
    public void HealingLight_DividesWisdomBeforeAddingTwo(int wisdom, int expected)
    {
        var stats = new CharacterStats(1, 0, 0, 0, 0, wisdom, 0, 0);

        Assert.Equal(expected, HealingCalculator.CalculateHealingLight(stats));
    }

    [Fact]
    public void HealingLight_OtherAttributesDoNotAffectAmount()
    {
        var lowStats = new CharacterStats(1, 0, 0, 0, 0, 18, 0, 0);
        var highStats = new CharacterStats(
            int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue,
            int.MaxValue, 18, int.MaxValue, int.MaxValue);

        Assert.Equal(11, HealingCalculator.CalculateHealingLight(lowStats));
        Assert.Equal(11, HealingCalculator.CalculateHealingLight(highStats));
    }

    [Fact]
    public void HealingLight_NullStats_AreRejected()
    {
        var error = Assert.Throws<ArgumentNullException>(() => HealingCalculator.CalculateHealingLight(null!));

        Assert.Equal("healerStats", error.ParamName);
    }

    [Fact]
    public void HealingLight_LeavesStatsAndBothCharactersRuntimeStateUnchanged()
    {
        var healer = CharacterFactory.Create("gentle_guardian", 1);
        var recipient = CharacterFactory.Create("hooded_assassin", 1);
        Assert.NotNull(healer);
        Assert.NotNull(recipient);
        healer.TakeDamage(5);
        healer.EndActivation();
        recipient.TakeDamage(1);
        var healerStats = healer.BaseStats;
        var healerSnapshot = healerStats with { };
        var recipientSnapshot = recipient.BaseStats with { };

        // Only one HP is missing, but calculating still returns the full base amount.
        Assert.Equal(11, HealingCalculator.CalculateHealingLight(healerStats));

        Assert.Same(healerStats, healer.BaseStats);
        Assert.Equal(healerSnapshot, healer.BaseStats);
        Assert.Equal(recipientSnapshot, recipient.BaseStats);
        Assert.Equal(39, healer.CurrentHealth);
        Assert.Equal(37, recipient.CurrentHealth);
        Assert.True(healer.HasActedThisRound);
        Assert.False(recipient.HasActedThisRound);
    }
}
