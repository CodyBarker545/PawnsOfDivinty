using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class StrikeDamageApplicatorTests
{
    [Fact]
    public void Apply_PhysicalHit_ReducesHealthExactlyOnce()
    {
        var attacker = CreateCharacter("hooded_assassin", 1);
        var defender = CreateCharacter("nigel", 2);
        var provider = new CountingRollProvider();
        var strike = Resolver(provider).ResolvePhysicalStrike(
            attacker.BaseStats, defender.BaseStats, 5, WeaponCategory.Sword, WeaponCategory.Axe, 100);

        int lost = StrikeDamageApplicator.Apply(strike, defender);

        Assert.Equal(13, strike.Damage);
        Assert.Equal(13, lost);
        Assert.Equal(39, defender.CurrentHealth);
        Assert.False(defender.IsDefeated);
        Assert.Equal(1, provider.CallCount); // Applying damage must not take another roll.
    }

    [Fact]
    public void Apply_MagicalHit_UsesResolvedDamageWithoutRecalculation()
    {
        var attacker = CreateCharacter("time_weaver", 1);
        var defender = CreateCharacter("gravebound_colossus", 2);
        var strike = Resolver(new CountingRollProvider()).ResolveMagicalStrike(
            attacker.BaseStats, defender.BaseStats, 4, WeaponCategory.MagicScroll, WeaponCategory.Lance, 100);

        Assert.Equal(12, StrikeDamageApplicator.Apply(strike, defender));
        Assert.Equal(52, defender.CurrentHealth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Apply_ZeroDamageMiss_LeavesHealthAndActivationUnchanged(bool hasActed)
    {
        var defender = CreateCharacter("nigel", 2);
        defender.TakeDamage(10);
        if (hasActed) defender.EndActivation();
        var strike = Strike(20, hit: false);

        Assert.False(strike.Hit);
        Assert.Equal(0, strike.Damage);
        Assert.Equal(0, StrikeDamageApplicator.Apply(strike, defender));
        Assert.Equal(42, defender.CurrentHealth);
        Assert.Equal(hasActed, defender.HasActedThisRound);
    }

    [Fact]
    public void Apply_ExactlyLethalHit_StopsAtZero()
    {
        var defender = CreateCharacter("nigel", 2);

        Assert.Equal(52, StrikeDamageApplicator.Apply(Strike(52), defender));
        Assert.Equal(0, defender.CurrentHealth);
        Assert.True(defender.IsDefeated);
    }

    [Fact]
    public void Apply_Overkill_ReturnsRemainingHealthRatherThanCalculatedDamage()
    {
        var defender = CreateCharacter("nigel", 2);
        defender.TakeDamage(45);
        var strike = Strike(int.MaxValue);

        Assert.Equal(int.MaxValue, strike.Damage);
        Assert.Equal(7, StrikeDamageApplicator.Apply(strike, defender));
        Assert.Equal(0, defender.CurrentHealth);
        Assert.True(defender.IsDefeated);
    }

    [Fact]
    public void Apply_RepeatedAfterLethalHit_ReportsNoFurtherHealthLoss()
    {
        var defender = CreateCharacter("nigel", 2);
        var strike = Strike(int.MaxValue);

        Assert.Equal(52, StrikeDamageApplicator.Apply(strike, defender));
        Assert.Equal(0, StrikeDamageApplicator.Apply(strike, defender));
        Assert.Equal(0, StrikeDamageApplicator.Apply(strike, defender));
        Assert.Equal(0, defender.CurrentHealth);
    }

    [Fact]
    public void Apply_AlreadyDefeatedCharacter_ReportsZero()
    {
        var defender = CreateCharacter("nigel", 2);
        defender.TakeDamage(defender.CurrentHealth);

        Assert.Equal(0, StrikeDamageApplicator.Apply(Strike(10), defender));
        Assert.Equal(0, defender.CurrentHealth);
        Assert.True(defender.IsDefeated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_NullDefender_ThrowsEvenForMiss(bool hit)
    {
        var strike = Strike(10, hit);

        var error = Assert.Throws<ArgumentNullException>(() => StrikeDamageApplicator.Apply(strike, null!));

        Assert.Equal("defender", error.ParamName);
    }

    [Fact]
    public void Apply_NullStrike_ThrowsWithoutChangingDefender()
    {
        var defender = CreateCharacter("nigel", 2);

        var error = Assert.Throws<ArgumentNullException>(() => StrikeDamageApplicator.Apply(null!, defender));

        Assert.Equal("strike", error.ParamName);
        Assert.Equal(52, defender.CurrentHealth);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Apply_Hit_DoesNotChangeActivationFlag(bool hasActed, bool lethal)
    {
        var defender = CreateCharacter("nigel", 2);
        if (hasActed) defender.EndActivation();

        StrikeDamageApplicator.Apply(Strike(lethal ? 52 : 1), defender);

        Assert.Equal(hasActed, defender.HasActedThisRound);
        Assert.Equal(lethal, defender.IsDefeated);
        Assert.Equal(!lethal && !hasActed, defender.CanAct);
    }

    [Fact]
    public void Apply_LeavesAttackerOtherInstancesBaseStatsAndStrikeUnchanged()
    {
        var attacker = CreateCharacter("hooded_assassin", 1);
        var defender = CreateCharacter("nigel", 2);
        var other = CreateCharacter("nigel", 2);
        attacker.TakeDamage(5);
        attacker.EndActivation();
        other.EndActivation();
        var attackerSnapshot = attacker.BaseStats with { };
        var defenderSnapshot = defender.BaseStats with { };
        var strike = Resolver(new CountingRollProvider()).ResolvePhysicalStrike(
            attacker.BaseStats, defender.BaseStats, 5, WeaponCategory.Sword, WeaponCategory.Axe, 100);
        var resultSnapshot = strike with { };

        StrikeDamageApplicator.Apply(strike, defender);

        Assert.Equal(33, attacker.CurrentHealth);
        Assert.True(attacker.HasActedThisRound);
        Assert.Equal(52, other.CurrentHealth);
        Assert.True(other.HasActedThisRound);
        Assert.False(other.IsDefeated);
        Assert.Same(defender.BaseStats, other.BaseStats);
        Assert.Equal(attackerSnapshot, attacker.BaseStats);
        Assert.Equal(defenderSnapshot, defender.BaseStats);
        Assert.Equal(resultSnapshot, strike);
        Assert.Equal("Nigel", defender.Name);
        Assert.Equal(2, defender.TeamId);
    }

    [Fact]
    public void Apply_ExplicitSecondCall_AppliesOnceAgainWithoutAnotherRoll()
    {
        var defender = CreateCharacter("nigel", 2);
        var provider = new CountingRollProvider();
        var strike = Strike(10, provider: provider);

        Assert.Equal(10, StrikeDamageApplicator.Apply(strike, defender));
        Assert.Equal(42, defender.CurrentHealth);
        Assert.Equal(10, StrikeDamageApplicator.Apply(strike, defender));
        Assert.Equal(32, defender.CurrentHealth);
        Assert.Equal(1, provider.CallCount);
    }

    [Theory]
    [InlineData(6, 4, 0, 2)]
    [InlineData(6, 6, 0, 0)]
    [InlineData(6, 10, 4, 0)]
    [InlineData(0, 10, 10, 0)]
    public void Apply_ShieldedDefender_ReportsOnlyHealthDamage(int shield, int damage, int hpLost, int remainingShield)
    {
        var defender = CreateCharacter("nigel", 2);
        defender.SetShieldHP(shield);
        var strike = Strike(damage);

        Assert.Equal(hpLost, StrikeDamageApplicator.Apply(strike, defender));
        Assert.Equal(damage, strike.Damage);
        Assert.Equal(52 - hpLost, defender.CurrentHealth);
        Assert.Equal(remainingShield, defender.ShieldHP);
    }

    [Fact]
    public void Apply_Miss_LeavesShieldUntouched()
    {
        var defender = CreateCharacter("nigel", 2);
        defender.SetShieldHP(6);

        Assert.Equal(0, StrikeDamageApplicator.Apply(Strike(10, hit: false), defender));
        Assert.Equal(6, defender.ShieldHP);
        Assert.Equal(52, defender.CurrentHealth);
    }

    private static Character CreateCharacter(string id, int teamId)
    {
        var character = CharacterFactory.Create(id, teamId);
        Assert.NotNull(character);
        return character;
    }

    private static SingleStrikeResolver Resolver(IRandomRollProvider provider) => new(new HitRoller(provider));

    private static StrikeResult Strike(int damage, bool hit = true, CountingRollProvider? provider = null)
    {
        var attacker = new CharacterStats(1, damage, 0, 0, 0, 0, 0, 0);
        var defender = new CharacterStats(1, 0, 0, 0, 0, 0, 0, 0);
        return Resolver(provider ?? new CountingRollProvider()).ResolvePhysicalStrike(
            attacker, defender, 0, WeaponCategory.Bow, WeaponCategory.Bow, hit ? 100 : 0);
    }

    private sealed class CountingRollProvider : IRandomRollProvider
    {
        public int CallCount { get; private set; }

        public int NextRoll()
        {
            CallCount++;
            if (CallCount > 1) throw new InvalidOperationException("Unexpected extra roll.");
            return 0;
        }
    }
}
