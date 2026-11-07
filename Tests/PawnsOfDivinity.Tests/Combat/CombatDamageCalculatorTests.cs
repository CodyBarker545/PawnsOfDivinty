using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class CombatDamageCalculatorTests
{
    [Theory]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Axe, 12)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Lance, 12)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Sword, 12)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Sword, 8)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Axe, 8)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Lance, 8)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Sword, 10)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Axe, 10)]
    [InlineData(WeaponCategory.Lance, WeaponCategory.Lance, 10)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.Bow, 10)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.MagicScroll, 10)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.Sword, 10)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Bow, 10)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.Sword, 10)]
    [InlineData(WeaponCategory.Sword, WeaponCategory.MagicScroll, 10)]
    [InlineData(WeaponCategory.Bow, WeaponCategory.MagicScroll, 10)]
    [InlineData(WeaponCategory.MagicScroll, WeaponCategory.Bow, 10)]
    public void PhysicalDamage_ComposesBaseAndTriangle(
        WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int expected)
    {
        // These stats give 10 base damage: 12 + 3 - 5.
        Assert.Equal(expected, CombatDamageCalculator.CalculatePhysicalDamage(
            Stats(strength: 12), Stats(defense: 5), 3, attackerWeapon, defenderWeapon));
    }

    [Theory]
    [InlineData(0, 100, WeaponCategory.Sword, WeaponCategory.Axe, 3)]
    [InlineData(0, 100, WeaponCategory.Axe, WeaponCategory.Sword, 1)]
    [InlineData(0, 100, WeaponCategory.Bow, WeaponCategory.Sword, 1)]
    [InlineData(2, 0, WeaponCategory.Axe, WeaponCategory.Sword, 1)]
    public void ProvisionalComposition_ClampsBaseBeforeTriangle_AndFinalDamageAfterTriangle(
        int strength, int defense, WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int expected)
    {
        Assert.Equal(expected, CombatDamageCalculator.CalculatePhysicalDamage(
            Stats(strength: strength), Stats(defense: defense), 0, attackerWeapon, defenderWeapon));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(int.MaxValue, true)]
    [InlineData(-1, false)]
    [InlineData(int.MaxValue, false)]
    public void InvalidCategory_IsRejectedEvenWithNeutralOpponent(int value, bool invalidAttacker)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatDamageCalculator.CalculatePhysicalDamage(
            Stats(), Stats(), 0,
            invalidAttacker ? (WeaponCategory)value : WeaponCategory.MagicScroll,
            invalidAttacker ? WeaponCategory.Bow : (WeaponCategory)value));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NullStats_AreRejected(bool nullAttacker)
    {
        var error = Assert.Throws<ArgumentNullException>(() => CombatDamageCalculator.CalculatePhysicalDamage(
            nullAttacker ? null! : Stats(), nullAttacker ? Stats() : null!, 0,
            WeaponCategory.Sword, WeaponCategory.Axe));

        Assert.Equal(nullAttacker ? "attacker" : "defender", error.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativePower_IsRejected(int power)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => CombatDamageCalculator.CalculatePhysicalDamage(
            Stats(), Stats(), power, WeaponCategory.Sword, WeaponCategory.Axe));

        Assert.Equal("weaponPower", error.ParamName);
    }

    [Theory]
    [InlineData(WeaponCategory.Sword, WeaponCategory.Axe, 2, int.MaxValue)]
    [InlineData(WeaponCategory.Axe, WeaponCategory.Sword, 0, int.MaxValue - 2)]
    public void LargeRepresentableDamage_DoesNotOverflow(
        WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int defense, int expected)
    {
        Assert.Equal(expected, CombatDamageCalculator.CalculatePhysicalDamage(
            Stats(strength: int.MaxValue), Stats(defense: defense), 0, attackerWeapon, defenderWeapon));
    }

    [Fact]
    public void TriangleAddition_ExceedingIntMaxValue_Throws()
    {
        Assert.Throws<OverflowException>(() => CombatDamageCalculator.CalculatePhysicalDamage(
            Stats(strength: int.MaxValue), Stats(), 0, WeaponCategory.Sword, WeaponCategory.Axe));
    }

    [Fact]
    public void BaseOverflow_IsNotHiddenByDisadvantage()
    {
        Assert.Throws<OverflowException>(() => CombatDamageCalculator.CalculatePhysicalDamage(
            Stats(strength: int.MaxValue), Stats(), 1, WeaponCategory.Axe, WeaponCategory.Sword));
    }

    [Fact]
    public void DocumentedAssassinVersusNigel_ReturnsThirteen_WithoutMutatingState()
    {
        var attacker = CharacterFactory.Create("hooded_assassin", 1);
        var defender = CharacterFactory.Create("nigel", 2);
        Assert.NotNull(attacker);
        Assert.NotNull(defender);
        var attackerSnapshot = attacker.BaseStats with { };
        var defenderSnapshot = defender.BaseStats with { };
        int attackerHealth = attacker.CurrentHealth;
        int defenderHealth = defender.CurrentHealth;

        int result = CombatDamageCalculator.CalculatePhysicalDamage(
            attacker.BaseStats, defender.BaseStats, 5, WeaponCategory.Sword, WeaponCategory.Axe);

        // The daggers deal 11 base damage against Nigel, then Sword advantage adds 2.
        Assert.Equal(13, result);
        Assert.Equal(attackerSnapshot, attacker.BaseStats);
        Assert.Equal(defenderSnapshot, defender.BaseStats);
        Assert.Equal(attackerHealth, attacker.CurrentHealth);
        Assert.Equal(defenderHealth, defender.CurrentHealth);
        Assert.True(attacker.CanAct);
        Assert.True(defender.CanAct);
    }

    private static CharacterStats Stats(int strength = 0, int defense = 0) => new(
        MaxHealth: 1, Strength: strength, Defense: defense, Intelligence: 99,
        Resistance: 99, Wisdom: 0, Speed: 0, Movement: 0);
}
