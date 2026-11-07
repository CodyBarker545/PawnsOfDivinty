using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class ProvisionalFollowUpDamageCalculatorTests
{
    [Theory]
    [InlineData(20, 5, 4, 10)]
    [InlineData(20, 7, 4, 15)]
    [InlineData(20, 8, 4, 15)]
    [InlineData(20, 100, 4, 15)]
    [InlineData(20, 6, 4, 10)]
    [InlineData(20, 34, 20, 10)]
    [InlineData(20, 35, 20, 15)]
    [InlineData(20, 1, 0, 15)]
    [InlineData(0, 5, 4, 0)]
    [InlineData(0, 7, 4, 0)]
    [InlineData(1, 5, 4, 1)]
    [InlineData(1, 7, 4, 1)]
    [InlineData(3, 5, 4, 1)]
    [InlineData(3, 7, 4, 2)]
    [InlineData(int.MaxValue, 5, 4, 1073741823)]
    [InlineData(int.MaxValue, 7, 4, 1610612735)]
    public void Calculate_UsesDocumentedEndpointsAndExplicitProvisionalPolicy(
        int damage, int fasterSpeed, int slowerSpeed, int expected)
    {
        Assert.Equal(expected, ProvisionalFollowUpDamageCalculator.Calculate(damage, fasterSpeed, slowerSpeed));
    }

    [Theory]
    [InlineData(20, 8, 7)]
    [InlineData(20, 4, 4)]
    [InlineData(20, 4, 5)]
    [InlineData(20, 0, 0)]
    [InlineData(20, 0, 1)]
    [InlineData(20, int.MaxValue, int.MaxValue - 1)]
    public void Calculate_SpeedsThatDoNotQualify_AreRejected(int damage, int fasterSpeed, int slowerSpeed)
    {
        Assert.Throws<ArgumentException>(() => ProvisionalFollowUpDamageCalculator.Calculate(damage, fasterSpeed, slowerSpeed));
    }

    [Theory]
    [InlineData(-1, 5, 4, "qualifyingDamage")]
    [InlineData(int.MinValue, 5, 4, "qualifyingDamage")]
    [InlineData(20, -1, 4, "fasterSpeed")]
    [InlineData(20, int.MinValue, 4, "fasterSpeed")]
    [InlineData(20, 5, -1, "slowerSpeed")]
    [InlineData(20, 5, int.MinValue, "slowerSpeed")]
    public void Calculate_NegativeInputs_AreRejected(int damage, int fasterSpeed, int slowerSpeed, string parameter)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ProvisionalFollowUpDamageCalculator.Calculate(damage, fasterSpeed, slowerSpeed));

        Assert.Equal(parameter, error.ParamName);
    }

    [Theory]
    [InlineData(int.MaxValue, 1227133512, 15)]
    [InlineData(int.MaxValue, 1227133513, 10)]
    public void Calculate_LargeSpeeds_UseExactSeventyFivePercentThreshold(int fasterSpeed, int slowerSpeed, int expected)
    {
        Assert.Equal(expected, ProvisionalFollowUpDamageCalculator.Calculate(20, fasterSpeed, slowerSpeed));
    }

    [Fact]
    public void EligibilityAndScaling_DoNotChangeRuntimeStateOrStats()
    {
        var attacker = CharacterFactory.Create("time_weaver", 1);
        var defender = CharacterFactory.Create("hooded_assassin", 2);
        Assert.NotNull(attacker);
        Assert.NotNull(defender);
        attacker.TakeDamage(5);
        defender.EndActivation();
        var attackerStats = attacker.BaseStats with { };
        var defenderStats = defender.BaseStats with { };
        int attackerHealth = attacker.CurrentHealth;
        int defenderHealth = defender.CurrentHealth;

        Assert.Equal(FollowUpParticipant.Defender, FollowUpEligibility.Evaluate(attacker.BaseStats.Speed, defender.BaseStats.Speed));
        Assert.Equal(10, ProvisionalFollowUpDamageCalculator.Calculate(20, defender.BaseStats.Speed, attacker.BaseStats.Speed));

        Assert.Equal(attackerStats, attacker.BaseStats);
        Assert.Equal(defenderStats, defender.BaseStats);
        Assert.Equal(attackerHealth, attacker.CurrentHealth);
        Assert.Equal(defenderHealth, defender.CurrentHealth);
        Assert.False(attacker.HasActedThisRound);
        Assert.True(defender.HasActedThisRound);
    }
}
