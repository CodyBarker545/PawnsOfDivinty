using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class WeaponTriangleTests
{
    [Theory]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Sword, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Axe, WeaponTriangleOutcome.Advantage)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Lance, WeaponTriangleOutcome.Disadvantage)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Bow, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.MagicScroll, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Sword, WeaponTriangleOutcome.Disadvantage)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Axe, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Lance, WeaponTriangleOutcome.Advantage)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Bow, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.MagicScroll, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Sword, WeaponTriangleOutcome.Advantage)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Axe, WeaponTriangleOutcome.Disadvantage)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Lance, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Bow, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.MagicScroll, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.Sword, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.Axe, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.Lance, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.Bow, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.MagicScroll, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.Sword, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.Axe, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.Lance, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.Bow, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.MagicScroll, WeaponTriangleOutcome.Neutral)]
    public void Evaluate_AllCategoryPairs_ReturnsDocumentedOutcome(
        WeaponCategory attacker, WeaponCategory defender, WeaponTriangleOutcome expected)
    {
        Assert.Equal(expected, WeaponTriangle.Evaluate(attacker, defender));
    }

    [Theory]
    [InlineData(WeaponTriangleOutcome.Advantage, 2, 10)]
    [InlineData(WeaponTriangleOutcome.Disadvantage, -2, -10)]
    [InlineData(WeaponTriangleOutcome.Neutral, 0, 0)]
    public void Outcome_ExposesDamageAndAccuracyPercentagePoints(
        WeaponTriangleOutcome outcome, int damage, int accuracyPercentagePoints)
    {
        Assert.Equal(damage, WeaponTriangle.GetDamageModifier(outcome));
        Assert.Equal(accuracyPercentagePoints, WeaponTriangle.GetAccuracyModifier(outcome));
    }

    public static IEnumerable<object[]> CategoryPairs()
    {
        foreach (var attacker in Enum.GetValues<WeaponCategory>())
            foreach (var defender in Enum.GetValues<WeaponCategory>())
                yield return new object[] { attacker, defender };
    }

    [Theory]
    [MemberData(nameof(CategoryPairs))]
    public void Evaluate_ReversingMatchup_ReversesOutcomeAndModifiers(
        WeaponCategory attacker, WeaponCategory defender)
    {
        var forward = WeaponTriangle.Evaluate(attacker, defender);
        var reversed = WeaponTriangle.Evaluate(defender, attacker);
        var expected = forward switch
        {
            WeaponTriangleOutcome.Advantage => WeaponTriangleOutcome.Disadvantage,
            WeaponTriangleOutcome.Disadvantage => WeaponTriangleOutcome.Advantage,
            _ => WeaponTriangleOutcome.Neutral
        };

        Assert.Equal(expected, reversed);
        Assert.Equal(-WeaponTriangle.GetDamageModifier(forward), WeaponTriangle.GetDamageModifier(reversed));
        Assert.Equal(-WeaponTriangle.GetAccuracyModifier(forward), WeaponTriangle.GetAccuracyModifier(reversed));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(6, true)]
    [InlineData(int.MinValue, true)]
    [InlineData(int.MaxValue, true)]
    [InlineData(-1, false)]
    [InlineData(6, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(int.MaxValue, false)]
    public void Evaluate_InvalidCategory_ThrowsEvenAgainstNeutralCategory(int invalid, bool invalidAttacker)
    {
        var attacker = invalidAttacker ? (WeaponCategory)invalid : WeaponCategory.MagicScroll;
        var defender = invalidAttacker ? WeaponCategory.Bow : (WeaponCategory)invalid;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => WeaponTriangle.Evaluate(attacker, defender));

        Assert.Equal(invalidAttacker ? "attacker" : "defender", error.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Modifiers_InvalidOutcome_Throws(int invalid)
    {
        var outcome = (WeaponTriangleOutcome)invalid;

        var damageError = Assert.Throws<ArgumentOutOfRangeException>(() => WeaponTriangle.GetDamageModifier(outcome));
        var accuracyError = Assert.Throws<ArgumentOutOfRangeException>(() => WeaponTriangle.GetAccuracyModifier(outcome));
        Assert.Equal("outcome", damageError.ParamName);
        Assert.Equal("outcome", accuracyError.ParamName);
    }
}
