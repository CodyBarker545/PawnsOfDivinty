using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class SingleStrikeResolverTests
{
    [Theory]
    [InlineData(WeaponCategory.Axe, WeaponTriangleOutcome.Advantage, 100, 13)]
    [InlineData(WeaponCategory.Lance, WeaponTriangleOutcome.Disadvantage, 80, 9)]
    [InlineData(WeaponCategory.Sword, WeaponTriangleOutcome.Neutral, 90, 11)]
    [InlineData(WeaponCategory.Bow, WeaponTriangleOutcome.Neutral, 90, 11)]
    public void PhysicalHit_ComposesAccuracyAndDamage(
        WeaponCategory defenderWeapon, WeaponTriangleOutcome outcome, int chance, int damage)
    {
        var provider = new SequenceProvider(0);
        var resolver = Resolver(provider);

        var result = resolver.ResolvePhysicalStrike(Stats(), Stats(), 5, WeaponCategory.Sword, defenderWeapon, 90);

        Assert.True(result.Hit);
        Assert.Equal(chance, result.HitChance);
        Assert.Equal(damage, result.Damage);
        Assert.Equal(outcome, result.TriangleOutcome);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void PhysicalMiss_ReturnsZeroDamageAndPreservesChanceAndOutcome()
    {
        var provider = new SequenceProvider(80);

        var result = Resolver(provider).ResolvePhysicalStrike(
            Stats(), Stats(), 5, WeaponCategory.Sword, WeaponCategory.Lance, 90);

        Assert.False(result.Hit);
        Assert.Equal(80, result.HitChance);
        Assert.Equal(0, result.Damage);
        Assert.Equal(WeaponTriangleOutcome.Disadvantage, result.TriangleOutcome);
        Assert.Equal(1, provider.CallCount);
    }

    [Theory]
    [InlineData(WeaponCategory.Sword)]
    [InlineData(WeaponCategory.Axe)]
    [InlineData(WeaponCategory.Lance)]
    [InlineData(WeaponCategory.Bow)]
    [InlineData(WeaponCategory.MagicScroll)]
    public void MagicalHit_UsesIntelligenceAndResistance_AndRemainsNeutral(WeaponCategory defenderWeapon)
    {
        var provider = new SequenceProvider(74);

        var result = Resolver(provider).ResolveMagicalStrike(
            Stats(), Stats(), 4, WeaponCategory.MagicScroll, defenderWeapon, 75);

        Assert.True(result.Hit);
        Assert.Equal(75, result.HitChance);
        Assert.Equal(12, result.Damage); // 17 INT + 4 spell Power - 9 RES gives 12 damage.
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.TriangleOutcome);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void MagicalMiss_ReturnsZeroDamage()
    {
        var provider = new SequenceProvider(75);

        var result = Resolver(provider).ResolveMagicalStrike(
            Stats(), Stats(), 4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 75);

        Assert.False(result.Hit);
        Assert.Equal(75, result.HitChance);
        Assert.Equal(0, result.Damage);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.TriangleOutcome);
        Assert.Equal(1, provider.CallCount);
    }

    [Theory]
    [InlineData(false, 0, 0, false)]
    [InlineData(false, 0, 99, false)]
    [InlineData(false, 100, 0, true)]
    [InlineData(false, 100, 99, true)]
    [InlineData(false, 75, 74, true)]
    [InlineData(false, 75, 75, false)]
    [InlineData(true, 0, 0, false)]
    [InlineData(true, 0, 99, false)]
    [InlineData(true, 100, 0, true)]
    [InlineData(true, 100, 99, true)]
    [InlineData(true, 75, 74, true)]
    [InlineData(true, 75, 75, false)]
    public void Strike_UsesFinalChanceAndOneRoll(bool magical, int chance, int roll, bool hit)
    {
        var provider = new SequenceProvider(roll);

        var result = Resolve(Resolver(provider), magical, Stats(), Stats(), 0,
            magical ? WeaponCategory.MagicScroll : WeaponCategory.Bow, WeaponCategory.Bow, chance);

        Assert.Equal(hit, result.Hit);
        Assert.Equal(chance, result.HitChance);
        Assert.Equal(hit ? (magical ? 8 : 6) : 0, result.Damage);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.TriangleOutcome);
        Assert.Equal(1, provider.CallCount);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void NullStats_AreRejectedBeforeRolling(bool magical, bool nullAttacker)
    {
        var provider = new SequenceProvider(0);

        var error = Assert.Throws<ArgumentNullException>(() => Resolve(Resolver(provider), magical,
            nullAttacker ? null! : Stats(), nullAttacker ? Stats() : null!, 0,
            magical ? WeaponCategory.MagicScroll : WeaponCategory.Sword, WeaponCategory.Bow, 0));

        Assert.Equal(nullAttacker ? "attacker" : "defender", error.ParamName);
        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, int.MinValue)]
    [InlineData(true, -1)]
    [InlineData(true, int.MinValue)]
    public void NegativePower_IsRejectedBeforeRolling(bool magical, int power)
    {
        var provider = new SequenceProvider(0);

        Assert.Throws<ArgumentOutOfRangeException>(() => Resolve(Resolver(provider), magical, Stats(), Stats(), power,
            magical ? WeaponCategory.MagicScroll : WeaponCategory.Sword, WeaponCategory.Bow, 0));

        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, int.MinValue)]
    [InlineData(false, 101)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, -1)]
    [InlineData(true, int.MinValue)]
    [InlineData(true, 101)]
    [InlineData(true, int.MaxValue)]
    public void InvalidAccuracy_IsRejectedBeforeRolling(bool magical, int accuracy)
    {
        var provider = new SequenceProvider(0);

        Assert.Throws<ArgumentOutOfRangeException>(() => Resolve(Resolver(provider), magical, Stats(), Stats(), 0,
            magical ? WeaponCategory.MagicScroll : WeaponCategory.Sword, WeaponCategory.Bow, accuracy));

        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData(false, true, -1)]
    [InlineData(false, true, int.MaxValue)]
    [InlineData(false, false, -1)]
    [InlineData(false, false, int.MaxValue)]
    [InlineData(true, true, -1)]
    [InlineData(true, true, int.MaxValue)]
    [InlineData(true, false, -1)]
    [InlineData(true, false, int.MaxValue)]
    public void InvalidCategory_IsRejectedBeforeRolling(bool magical, bool invalidAttacker, int invalid)
    {
        var provider = new SequenceProvider(0);

        Assert.Throws<ArgumentOutOfRangeException>(() => Resolve(Resolver(provider), magical, Stats(), Stats(), 0,
            invalidAttacker ? (WeaponCategory)invalid : (magical ? WeaponCategory.MagicScroll : WeaponCategory.Bow),
            invalidAttacker ? WeaponCategory.Bow : (WeaponCategory)invalid, 0));

        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData(WeaponCategory.Sword)]
    [InlineData(WeaponCategory.Axe)]
    [InlineData(WeaponCategory.Lance)]
    [InlineData(WeaponCategory.Bow)]
    public void MagicalStrike_RequiresMagicScrollCategory_BeforeRolling(WeaponCategory attackerWeapon)
    {
        var provider = new SequenceProvider(0);

        var error = Assert.Throws<ArgumentException>(() => Resolver(provider).ResolveMagicalStrike(
            Stats(), Stats(), 0, attackerWeapon, WeaponCategory.Bow, 0));

        Assert.Equal("attackerWeapon", error.ParamName);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void DamageOverflow_IsRejectedBeforeRolling_EvenForGuaranteedMiss()
    {
        var provider = new SequenceProvider(0);
        var resolver = Resolver(provider);
        var attacker = new CharacterStats(1, int.MaxValue, 0, int.MaxValue, 0, 0, 0, 0);
        var defender = new CharacterStats(1, 0, 0, 0, 0, 0, 0, 0);

        Assert.Throws<OverflowException>(() => resolver.ResolvePhysicalStrike(
            attacker, defender, 1, WeaponCategory.Bow, WeaponCategory.Bow, 0));
        Assert.Throws<OverflowException>(() => resolver.ResolvePhysicalStrike(
            attacker, defender, 0, WeaponCategory.Sword, WeaponCategory.Axe, 0));
        Assert.Throws<OverflowException>(() => resolver.ResolveMagicalStrike(
            attacker, defender, 1, WeaponCategory.MagicScroll, WeaponCategory.Bow, 0));

        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DocumentedRoster_StrikeDoesNotChangeStatsHealthOrActivation(bool magical)
    {
        var attacker = CharacterFactory.Create(magical ? "time_weaver" : "hooded_assassin", 1);
        var defender = CharacterFactory.Create(magical ? "gravebound_colossus" : "nigel", 2);
        Assert.NotNull(attacker);
        Assert.NotNull(defender);
        var attackerSnapshot = attacker.BaseStats with { };
        var defenderSnapshot = defender.BaseStats with { };
        int attackerHealth = attacker.CurrentHealth;
        int defenderHealth = defender.CurrentHealth;
        var provider = new SequenceProvider(0);

        var result = Resolve(Resolver(provider), magical, attacker.BaseStats, defender.BaseStats,
            magical ? 4 : 5, magical ? WeaponCategory.MagicScroll : WeaponCategory.Sword,
            magical ? WeaponCategory.Lance : WeaponCategory.Axe, 90);

        Assert.True(result.Hit);
        Assert.Equal(magical ? 12 : 13, result.Damage);
        Assert.Equal(attackerSnapshot, attacker.BaseStats);
        Assert.Equal(defenderSnapshot, defender.BaseStats);
        Assert.Equal(attackerHealth, attacker.CurrentHealth);
        Assert.Equal(defenderHealth, defender.CurrentHealth);
        Assert.True(attacker.CanAct);
        Assert.True(defender.CanAct);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void RepeatedPhysicalAndMagicalStrikes_ConsumeSeparateRolls()
    {
        var provider = new SequenceProvider(0, 99, 0);
        var resolver = Resolver(provider);

        var first = resolver.ResolvePhysicalStrike(Stats(), Stats(), 0, WeaponCategory.Bow, WeaponCategory.Bow, 50);
        var second = resolver.ResolveMagicalStrike(Stats(), Stats(), 0, WeaponCategory.MagicScroll, WeaponCategory.Bow, 50);
        var third = resolver.ResolveMagicalStrike(Stats(), Stats(), 0, WeaponCategory.MagicScroll, WeaponCategory.Bow, 50);

        Assert.True(first.Hit);
        Assert.False(second.Hit);
        Assert.Equal(0, second.Damage);
        Assert.True(third.Hit);
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public void StrikeResult_IsImmutable()
    {
        var properties = typeof(StrikeResult).GetProperties();

        Assert.Equal(5, properties.Length);
        Assert.Contains(properties, property => property.Name == nameof(StrikeResult.WinterHunter));
        Assert.All(properties, property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void Constructor_NullHitRoller_IsRejected()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new SingleStrikeResolver(null!));

        Assert.Equal("hitRoller", error.ParamName);
    }

    private static CharacterStats Stats() => new(30, 17, 11, 17, 9, 4, 24, 6);

    private static SingleStrikeResolver Resolver(IRandomRollProvider provider) => new(new HitRoller(provider));

    private static StrikeResult Resolve(SingleStrikeResolver resolver, bool magical,
        CharacterStats attacker, CharacterStats defender, int power,
        WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int accuracy) => magical
            ? resolver.ResolveMagicalStrike(attacker, defender, power, attackerWeapon, defenderWeapon, accuracy)
            : resolver.ResolvePhysicalStrike(attacker, defender, power, attackerWeapon, defenderWeapon, accuracy);

    private sealed class SequenceProvider : IRandomRollProvider
    {
        private readonly Queue<int> _rolls;
        public int CallCount { get; private set; }

        public SequenceProvider(params int[] rolls) => _rolls = new Queue<int>(rolls);

        public int NextRoll()
        {
            CallCount++;
            return _rolls.Dequeue();
        }
    }
}
