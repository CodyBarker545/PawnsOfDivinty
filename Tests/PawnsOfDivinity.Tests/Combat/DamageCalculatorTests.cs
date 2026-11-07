using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class DamageCalculatorTests
{
    [Theory]
    [InlineData(20, 5, 8, 17)]
    [InlineData(10, 0, 3, 7)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(5, 0, 5, 1)]
    [InlineData(5, 0, 50, 1)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)]
    [InlineData(int.MaxValue, 1, 1, int.MaxValue)]
    [InlineData(int.MaxValue, 0, int.MaxValue, 1)]
    [InlineData(1, 0, int.MaxValue, 1)]
    public void PhysicalDamage_UsesStrengthPowerAndDefense_WithMinimumOne(
        int strength, int power, int defense, int expected)
    {
        Assert.Equal(expected, DamageCalculator.CalculatePhysicalDamage(
            Stats(strength: strength), Stats(defense: defense), power));
    }

    [Theory]
    [InlineData(20, 5, 8, 17)]
    [InlineData(10, 0, 3, 7)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(5, 0, 5, 1)]
    [InlineData(5, 0, 50, 1)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)]
    [InlineData(int.MaxValue, 1, 1, int.MaxValue)]
    [InlineData(int.MaxValue, 0, int.MaxValue, 1)]
    [InlineData(1, 0, int.MaxValue, 1)]
    public void MagicalDamage_UsesIntelligencePowerAndResistance_WithMinimumOne(
        int intelligence, int power, int resistance, int expected)
    {
        Assert.Equal(expected, DamageCalculator.CalculateMagicalDamage(
            Stats(intelligence: intelligence), Stats(resistance: resistance), power));
    }

    [Fact]
    public void PhysicalDamage_IgnoresIntelligenceAndResistance()
    {
        Assert.Equal(14, DamageCalculator.CalculatePhysicalDamage(
            Stats(strength: 17, intelligence: 99), Stats(defense: 8, resistance: 70), 5));
    }

    [Fact]
    public void MagicalDamage_IgnoresStrengthAndDefense()
    {
        Assert.Equal(8, DamageCalculator.CalculateMagicalDamage(
            Stats(strength: 99, intelligence: 17), Stats(defense: 70, resistance: 13), 4));
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, int.MinValue)]
    [InlineData(true, -1)]
    [InlineData(true, int.MinValue)]
    public void NegativePower_ThrowsWithCorrectParameter(bool magical, int power)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Calculate(magical, Stats(), Stats(), power));

        Assert.Equal(magical ? "spellPower" : "weaponPower", error.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullAttacker_Throws(bool magical)
    {
        var error = Assert.Throws<ArgumentNullException>(() => Calculate(magical, null!, Stats(), 0));

        Assert.Equal("attacker", error.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullDefender_Throws(bool magical)
    {
        var error = Assert.Throws<ArgumentNullException>(() => Calculate(magical, Stats(), null!, 0));

        Assert.Equal("defender", error.ParamName);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, 1)]
    [InlineData(true, int.MaxValue)]
    public void ResultLargerThanIntMaxValue_ThrowsInsteadOfWrappingOrClamping(bool magical, int power)
    {
        Assert.Throws<OverflowException>(() => Calculate(magical,
            Stats(strength: int.MaxValue, intelligence: int.MaxValue), Stats(), power));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Calculation_DoesNotChangeInputStatsOrRuntimeHealth(bool magical)
    {
        var attacker = CharacterFactory.Create("hooded_assassin", 1);
        var defender = CharacterFactory.Create("gravebound_colossus", 2);
        Assert.NotNull(attacker);
        Assert.NotNull(defender);
        var attackerSnapshot = attacker.BaseStats with { };
        var defenderSnapshot = defender.BaseStats with { };
        int attackerHealth = attacker.CurrentHealth;
        int defenderHealth = defender.CurrentHealth;

        Calculate(magical, attacker.BaseStats, defender.BaseStats, 5);

        Assert.Equal(attackerSnapshot, attacker.BaseStats);
        Assert.Equal(defenderSnapshot, defender.BaseStats);
        Assert.Equal(attackerHealth, attacker.CurrentHealth);
        Assert.Equal(defenderHealth, defender.CurrentHealth);
        Assert.True(attacker.CanAct);
        Assert.True(defender.CanAct);
    }

    // Use the documented starting Power where it is known. The zero-Power cases
    // just test the formula; Axe, Lance and Bow starting Power is still undecided.
    [Theory]
    [InlineData("hooded_assassin", "time_weaver", false, 5, 14)]
    [InlineData("gentle_guardian", "hooded_assassin", true, 3, 6)]
    [InlineData("time_weaver", "gravebound_colossus", true, 4, 12)]
    [InlineData("nigel", "gentle_guardian", false, 0, 9)]
    [InlineData("gravebound_colossus", "hooded_assassin", false, 0, 7)]
    [InlineData("beothuk_spirit_guardian", "nigel", false, 0, 5)]
    public void DocumentedRoster_ProducesExpectedBaseDamage(
        string attackerId, string defenderId, bool magical, int power, int expected)
    {
        var attacker = CharacterRegistry.GetCharacter(attackerId);
        var defender = CharacterRegistry.GetCharacter(defenderId);
        Assert.NotNull(attacker);
        Assert.NotNull(defender);

        Assert.Equal(expected, Calculate(magical, attacker.BaseStats, defender.BaseStats, power));
    }

    private static CharacterStats Stats(
        int strength = 0, int defense = 0, int intelligence = 0, int resistance = 0) => new(
            MaxHealth: 1, Strength: strength, Defense: defense, Intelligence: intelligence,
            Resistance: resistance, Wisdom: 0, Speed: 0, Movement: 0);

    private static int Calculate(bool magical, CharacterStats attacker, CharacterStats defender, int power) =>
        magical
            ? DamageCalculator.CalculateMagicalDamage(attacker, defender, power)
            : DamageCalculator.CalculatePhysicalDamage(attacker, defender, power);
}
