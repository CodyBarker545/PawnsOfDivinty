using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class CounterattackEligibilityTests
{
    [Theory]
    [InlineData("nigel", 0, 1, 1, false)]
    [InlineData("nigel", 1, 1, 1, true)]
    [InlineData("nigel", 2, 1, 1, false)]
    [InlineData("beothuk_spirit_guardian", 0, 2, 3, false)]
    [InlineData("beothuk_spirit_guardian", 1, 2, 3, false)]
    [InlineData("beothuk_spirit_guardian", 2, 2, 3, true)]
    [InlineData("beothuk_spirit_guardian", 3, 2, 3, true)]
    [InlineData("beothuk_spirit_guardian", 4, 2, 3, false)]
    [InlineData("gentle_guardian", 0, 1, 2, false)]
    [InlineData("gentle_guardian", 1, 1, 2, true)]
    [InlineData("gentle_guardian", 2, 1, 2, true)]
    [InlineData("gentle_guardian", 3, 1, 2, false)]
    public void CanCounter_UsesInclusiveAvailableRange(
        string characterId, int distance, int minimumRange, int maximumRange, bool expected)
    {
        var defender = Defender(characterId);

        Assert.Equal(expected, CounterattackEligibility.CanCounter(
            defender, distance, minimumRange, maximumRange,
            incomingAttackPermitsCounters: true, counterattacksSuppressed: false));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public void CanCounter_RequiresIncomingPermissionAndNoSuppression(bool permits, bool suppressed, bool expected)
    {
        // AoE and Temporal Ignition pass false here because they do not allow counters.
        Assert.Equal(expected, CounterattackEligibility.CanCounter(Defender(), 1, 1, 1, permits, suppressed));
    }

    [Fact]
    public void CanCounter_UsesHealthAfterOpeningStrike()
    {
        var defender = Defender();
        defender.TakeDamage(defender.CurrentHealth - 1);

        Assert.True(CounterattackEligibility.CanCounter(defender, 1, 1, 1, true, false));

        defender.TakeDamage(1);

        Assert.True(defender.IsDefeated);
        Assert.False(CounterattackEligibility.CanCounter(defender, 1, 1, 1, true, false));
    }

    [Fact]
    public void CanCounter_AlreadyActivatedLivingDefender_RemainsEligible()
    {
        var defender = Defender();
        defender.EndActivation();
        Assert.False(defender.CanAct);

        Assert.True(CounterattackEligibility.CanCounter(defender, 1, 1, 1, true, false));
        Assert.True(defender.HasActedThisRound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanCounter_DoesNotChangeHealthActivationStatsOrIdentity(bool hasActed)
    {
        var defender = Defender();
        defender.TakeDamage(5);
        if (hasActed) defender.EndActivation();
        var stats = defender.BaseStats;
        var snapshot = stats with { };
        int health = defender.CurrentHealth;
        string id = defender.Id;

        Assert.True(CounterattackEligibility.CanCounter(defender, 1, 1, 1, true, false));
        Assert.False(CounterattackEligibility.CanCounter(defender, 1, 1, 1, true, true));

        Assert.Equal(health, defender.CurrentHealth);
        Assert.Equal(hasActed, defender.HasActedThisRound);
        Assert.Same(stats, defender.BaseStats);
        Assert.Equal(snapshot, defender.BaseStats);
        Assert.Equal(id, defender.Id);
        Assert.Equal("Nigel", defender.Name);
        Assert.Equal(2, defender.TeamId);
    }

    [Theory]
    [InlineData(-1, 1, 1, "distance")]
    [InlineData(int.MinValue, 1, 1, "distance")]
    [InlineData(1, -1, 1, "minimumRange")]
    [InlineData(1, int.MinValue, 1, "minimumRange")]
    [InlineData(1, 0, -1, "maximumRange")]
    [InlineData(1, 2, 1, "maximumRange")]
    [InlineData(1, 1, int.MinValue, "maximumRange")]
    public void CanCounter_InvalidRangeInputs_ThrowEvenWhenCounterAlreadyDisallowed(
        int distance, int minimumRange, int maximumRange, string parameter)
    {
        var defender = Defender();
        defender.TakeDamage(defender.CurrentHealth);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => CounterattackEligibility.CanCounter(
            defender, distance, minimumRange, maximumRange, false, true));

        Assert.Equal(parameter, error.ParamName);
    }

    [Fact]
    public void CanCounter_NullDefender_Throws()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            CounterattackEligibility.CanCounter(null!, 1, 1, 1, false, true));

        Assert.Equal("defender", error.ParamName);
    }

    [Fact]
    public void CanCounter_ZeroDistanceAndZeroRange_AreNotArtificiallyForbidden()
    {
        Assert.True(CounterattackEligibility.CanCounter(Defender(), 0, 0, 0, true, false));
    }

    [Fact]
    public void CanCounter_LargestIntegerRangeAndDistance_AreComparedSafely()
    {
        Assert.True(CounterattackEligibility.CanCounter(
            Defender(), int.MaxValue, int.MaxValue, int.MaxValue, true, false));
    }

    private static Character Defender(string characterId = "nigel")
    {
        var character = CharacterFactory.Create(characterId, teamId: 2);
        Assert.NotNull(character);
        return character;
    }
}
