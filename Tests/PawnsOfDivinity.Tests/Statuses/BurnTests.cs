using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Statuses;

public class BurnTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(22, 19)]
    [InlineData(int.MaxValue, int.MaxValue - 3)]
    public void Burn_ReducesEffectiveDefense_WithZeroFloor(int defense, int expected)
    {
        var character = Unit(defense: defense);
        Assert.Equal(defense, character.EffectiveDefense);

        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);

        Assert.Equal(expected, character.EffectiveDefense);
        Assert.Equal(defense, character.BaseStats.Defense);
    }

    [Fact]
    public void RefreshAndExpiration_ChangeTimerWithoutStackingOrDealingDamage()
    {
        var character = Unit();
        character.TakeDamage(7);
        character.SetShieldHP(6);
        character.EndActivation();
        var stats = character.BaseStats;
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 4);
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        character.Statuses.AdvanceTimedStatuses();
        Assert.Equal(7, character.EffectiveDefense);

        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        Assert.Equal(7, character.EffectiveDefense);
        Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Burn]);
        character.Statuses.AdvanceTimedStatuses();
        Assert.Equal(7, character.EffectiveDefense);
        character.Statuses.AdvanceTimedStatuses();

        Assert.False(character.Statuses.HasStatus(StatusType.Burn));
        Assert.True(character.Statuses.HasStatus(StatusType.Silence));
        Assert.Equal(10, character.EffectiveDefense);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(193, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.True(character.HasActedThisRound);
    }

    [Fact]
    public void CharactersSharingBaseStats_HaveIndependentBurnState()
    {
        var first = CharacterFactory.Create("nigel", 1)!;
        var second = CharacterFactory.Create("nigel", 2)!;
        Assert.Same(first.BaseStats, second.BaseStats);

        first.Statuses.ApplyTimedStatus(StatusType.Burn, 2);

        Assert.Equal(8, first.EffectiveDefense);
        Assert.Equal(11, second.EffectiveDefense);
        Assert.Equal(52, first.CurrentHealth);
        Assert.Equal(0, first.ShieldHP);
        Assert.False(first.HasActedThisRound);
    }

    [Theory]
    [InlineData(AttackType.Physical, 15, 18)]
    [InlineData(AttackType.Magical, 17, 17)]
    public void OpeningStrike_UsesEffectiveDefenseOnlyForPhysicalDamage(AttackType type, int normal, int burned)
    {
        var attacker = Unit();
        var defender = Unit();
        var request = Request(attacker, defender) with
        {
            OpeningAttackType = type,
            AttackerWeapon = type == AttackType.Magical ? WeaponCategory.MagicScroll : WeaponCategory.Bow,
            IncomingAttackPermitsCounters = false
        };
        var resolver = Resolver();
        Assert.Equal(normal, resolver.Resolve(request).OpeningStrike.Damage);
        defender.Statuses.ApplyTimedStatus(StatusType.Burn, 2);

        Assert.Equal(burned, resolver.Resolve(request).OpeningStrike.Damage);
        Assert.Equal(8, defender.BaseStats.Resistance);
        defender.Statuses.AdvanceTimedStatuses();
        defender.Statuses.AdvanceTimedStatuses();
        Assert.Equal(normal, resolver.Resolve(request).OpeningStrike.Damage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PhysicalCountersAndFollowUps_UseBurnedTargetsDefense(bool attackerIsFaster)
    {
        var attacker = Unit(speed: attackerIsFaster ? 20 : 10);
        var defender = Unit(speed: attackerIsFaster ? 10 : 20);
        attacker.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        defender.Statuses.ApplyTimedStatus(StatusType.Burn, 2);

        var result = Resolver().Resolve(Request(attacker, defender) with { IncomingAttackPermitsFollowUps = true });

        Assert.Equal(18, result.OpeningStrike.Damage);
        Assert.Equal(18, result.CounterStrike!.Damage);
        Assert.Equal(attackerIsFaster ? FollowUpParticipant.Attacker : FollowUpParticipant.Defender, result.FollowUpBy);
        Assert.Equal(13, result.FollowUpStrike!.Damage); // Existing 75% scaling: floor(18 * 3 / 4).
        Assert.Equal(attackerIsFaster ? 182 : 169, attacker.CurrentHealth);
        Assert.Equal(attackerIsFaster ? 169 : 182, defender.CurrentHealth);
    }

    [Fact]
    public void SinglePhysicalStrike_CanUseEffectiveDefense_WithoutChangingStatsOrHealth()
    {
        var attacker = Unit();
        var defender = Unit();
        defender.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var resolver = new SingleStrikeResolver(new HitRoller(new FixedRoll()));

        var result = resolver.ResolvePhysicalStrike(attacker.BaseStats, defender.BaseStats, 5,
            WeaponCategory.Sword, WeaponCategory.Axe, 90, defender.EffectiveDefense);

        Assert.Equal(20, result.Damage); // 20 + 5 - 7, then triangle +2.
        Assert.Equal(100, result.HitChance);
        Assert.Equal(200, defender.CurrentHealth);
        Assert.Equal(10, defender.BaseStats.Defense);
        Assert.Equal(15, DamageCalculator.CalculatePhysicalDamage(attacker.BaseStats, defender.BaseStats, 5));
    }

    [Fact]
    public void NegativeEffectiveDefense_IsRejectedBeforeRolling()
    {
        var provider = new FixedRoll();
        var unit = Unit();
        var resolver = new SingleStrikeResolver(new HitRoller(provider));

        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ResolvePhysicalStrike(
            unit.BaseStats, unit.BaseStats, 5, WeaponCategory.Bow, WeaponCategory.Bow, 100, -1));

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public void BurnCausedOverflow_IsRejectedBeforeRollsOrHealthChanges()
    {
        var attacker = new Character("large", "Large", 1,
            new CharacterStats(200, int.MaxValue, 10, 20, 8, 0, 10, 4));
        var defender = Unit(defense: 3);
        defender.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var provider = new FixedRoll();

        Assert.Throws<OverflowException>(() => new CombatExchangeResolver(
            new SingleStrikeResolver(new HitRoller(provider))).Resolve(Request(attacker, defender)));

        Assert.Equal(0, provider.Calls);
        Assert.Equal(200, attacker.CurrentHealth);
        Assert.Equal(200, defender.CurrentHealth);
    }

    private static Character Unit(int defense = 10, int speed = 10) => new(
        Guid.NewGuid().ToString(), "Test character", 1, new CharacterStats(200, 20, defense, 20, 8, 0, speed, 4));

    private static CombatExchangeRequest Request(Character attacker, Character defender) => new(
        attacker, defender, AttackType.Physical, 5, WeaponCategory.Bow, WeaponCategory.Bow, 100,
        AttackType.Physical, 5, 100, 1, 3, 1, true, false);

    private static CombatExchangeResolver Resolver() => new(new SingleStrikeResolver(new HitRoller(new FixedRoll())));

    private sealed class FixedRoll : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }
}
