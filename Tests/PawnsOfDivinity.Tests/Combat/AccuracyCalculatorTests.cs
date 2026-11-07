using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class AccuracyCalculatorTests
{
    [Theory]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Axe, 100)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Lance, 100)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Sword, 100)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Sword, 80)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Axe, 80)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Lance, 80)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Sword, 90)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Axe, 90)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Lance, 90)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.Bow, 90)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.MagicScroll, 90)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.Sword, 90)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Bow, 90)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.Sword, 90)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.MagicScroll, 90)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.MagicScroll, 90)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.Bow, 90)]
    public void HitChance_AppliesTriangleModifierToSuppliedBase(
        WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int expected)
    {
        Assert.Equal(expected, AccuracyCalculator.CalculateHitChance(90, attackerWeapon, defenderWeapon));
    }

    [Theory]
    [InlineData(0, WeaponCategory.Sword, WeaponCategory.Axe, 10)]
    [InlineData(0, WeaponCategory.Sword, WeaponCategory.Lance, 0)]
    [InlineData(0, WeaponCategory.Bow, WeaponCategory.Sword, 0)]
    [InlineData(100, WeaponCategory.Sword, WeaponCategory.Axe, 100)]
    [InlineData(100, WeaponCategory.Sword, WeaponCategory.Lance, 90)]
    [InlineData(100, WeaponCategory.Bow, WeaponCategory.Sword, 100)]
    [InlineData(95, WeaponCategory.Sword, WeaponCategory.Axe, 100)]
    [InlineData(5, WeaponCategory.Sword, WeaponCategory.Lance, 0)]
    [InlineData(10, WeaponCategory.Sword, WeaponCategory.Lance, 0)]
    public void HitChance_ValidBaseBoundariesAndClamping(
        int baseAccuracy, WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int expected)
    {
        Assert.Equal(expected, AccuracyCalculator.CalculateHitChance(baseAccuracy, attackerWeapon, defenderWeapon));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void HitChance_InvalidBase_IsRejectedRatherThanClamped(int baseAccuracy)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            AccuracyCalculator.CalculateHitChance(baseAccuracy, WeaponCategory.Sword, WeaponCategory.Axe));

        Assert.Equal("baseAccuracy", error.ParamName);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(int.MaxValue, true)]
    [InlineData(-1, false)]
    [InlineData(int.MaxValue, false)]
    public void HitChance_InvalidCategory_IsRejectedEvenAgainstNeutralWeapon(int value, bool invalidAttacker)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AccuracyCalculator.CalculateHitChance(
            100,
            invalidAttacker ? (WeaponCategory)value : WeaponCategory.MagicScroll,
            invalidAttacker ? WeaponCategory.Bow : (WeaponCategory)value));
    }

    [Theory]
    [InlineData(50, WeaponCategory.Sword, WeaponCategory.Axe, 60)]
    [InlineData(50, WeaponCategory.Sword, WeaponCategory.Lance, 40)]
    [InlineData(73, WeaponCategory.Sword, WeaponCategory.Axe, 83)]
    public void HitChance_UsesPercentagePoints_NotPercentageMultiplication(
        int baseAccuracy, WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int expected)
    {
        Assert.Equal(expected, AccuracyCalculator.CalculateHitChance(baseAccuracy, attackerWeapon, defenderWeapon));
    }

    [Fact]
    public void HitChance_RepeatedAndInterleavedCalls_AreDeterministic()
    {
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(73, AccuracyCalculator.CalculateHitChance(63, WeaponCategory.Sword, WeaponCategory.Axe));
            Assert.Equal(53, AccuracyCalculator.CalculateHitChance(63, WeaponCategory.Sword, WeaponCategory.Lance));
            Assert.Equal(63, AccuracyCalculator.CalculateHitChance(63, WeaponCategory.Bow, WeaponCategory.Axe));
        }
    }
}
