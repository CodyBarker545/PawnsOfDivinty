using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class HealingDiminishingReturnsCalculatorTests
{
    [Theory]
    [InlineData(1, 11)]
    [InlineData(2, 8)]
    [InlineData(3, 5)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(10, 2)]
    [InlineData(int.MaxValue, 2)]
    public void Calculate_ElevenBaseHealing_UsesDocumentedPercentages(int healOrdinal, int expected)
    {
        Assert.Equal(expected, HealingDiminishingReturnsCalculator.Calculate(11, healOrdinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void Calculate_ZeroHealing_StaysZero(int healOrdinal)
    {
        Assert.Equal(0, HealingDiminishingReturnsCalculator.Calculate(0, healOrdinal));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 0)]
    [InlineData(1, 3, 0)]
    [InlineData(1, 4, 0)]
    [InlineData(3, 2, 2)]
    [InlineData(3, 3, 1)]
    [InlineData(3, 4, 0)]
    [InlineData(6, 2, 4)]
    [InlineData(6, 3, 3)]
    [InlineData(6, 4, 1)]
    public void Calculate_ProvisionalFloorRounding_DoesNotForceMinimumOne(
        int baseHealing, int healOrdinal, int expected)
    {
        Assert.Equal(expected, HealingDiminishingReturnsCalculator.Calculate(baseHealing, healOrdinal));
    }

    [Theory]
    [InlineData(1, int.MaxValue)]
    [InlineData(2, 1610612735)]
    [InlineData(3, 1073741823)]
    [InlineData(4, 536870911)]
    [InlineData(int.MaxValue, 536870911)]
    public void Calculate_LargestBaseHealing_DoesNotOverflow(int healOrdinal, int expected)
    {
        Assert.Equal(expected, HealingDiminishingReturnsCalculator.Calculate(int.MaxValue, healOrdinal));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Calculate_NegativeHealing_IsRejected(int baseHealing)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            HealingDiminishingReturnsCalculator.Calculate(baseHealing, 1));

        Assert.Equal("baseHealing", error.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Calculate_InvalidOrdinal_IsRejectedEvenForZeroHealing(int healOrdinal)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            HealingDiminishingReturnsCalculator.Calculate(0, healOrdinal));

        Assert.Equal("healOrdinal", error.ParamName);
    }

    [Fact]
    public void Calculate_RepeatedAndOutOfOrderCalls_DoNotTrackHistory()
    {
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(11, HealingDiminishingReturnsCalculator.Calculate(11, 1));
            Assert.Equal(2, HealingDiminishingReturnsCalculator.Calculate(11, 5));
            Assert.Equal(8, HealingDiminishingReturnsCalculator.Calculate(11, 2));
            Assert.Equal(5, HealingDiminishingReturnsCalculator.Calculate(11, 3));
            Assert.Equal(11, HealingDiminishingReturnsCalculator.Calculate(11, 1));
        }
    }

    [Fact]
    public void Calculate_LeavesHealerAndRecipientStateUnchanged()
    {
        var healer = CharacterFactory.Create("gentle_guardian", 1);
        var recipient = CharacterFactory.Create("hooded_assassin", 1);
        Assert.NotNull(healer);
        Assert.NotNull(recipient);
        healer.TakeDamage(5);
        healer.EndActivation();
        recipient.TakeDamage(1);
        var healerSnapshot = healer.BaseStats with { };
        var recipientSnapshot = recipient.BaseStats with { };
        int baseHealing = HealingCalculator.CalculateHealingLight(healer.BaseStats);

        // The recipient is missing only one HP, but this calculates an amount without applying it.
        Assert.Equal(8, HealingDiminishingReturnsCalculator.Calculate(baseHealing, 2));

        Assert.Equal(39, healer.CurrentHealth);
        Assert.Equal(37, recipient.CurrentHealth);
        Assert.True(healer.HasActedThisRound);
        Assert.False(recipient.HasActedThisRound);
        Assert.Equal(healerSnapshot, healer.BaseStats);
        Assert.Equal(recipientSnapshot, recipient.BaseStats);
    }
}
