using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Statuses;

public class DisarmTests
{
    // Unarmed Power 1 and accuracy 73 below are caller-supplied test examples, not game balance.
    private static AttackConfiguration Unarmed() => AttackConfiguration.CreateUnarmed(1, 73);

    [Fact]
    public void WithoutDisarm_PreservesSuppliedWeaponConfiguration()
    {
        var character = Unit();
        var weapon = new AttackConfiguration(AttackType.Physical, WeaponCategory.Bow, 7, 80, 2, 3);

        Assert.True(DisarmEligibility.CanUseEquippedWeapon(character));
        Assert.Same(weapon, DisarmEligibility.GetEffectiveAttack(character, weapon, Unarmed()));
    }

    [Fact]
    public void Disarm_ReplacesAllWeaponInputsWithExplicitUnarmedConfiguration()
    {
        var character = Unit();
        var weapon = new AttackConfiguration(AttackType.Magical, WeaponCategory.MagicScroll, 7, 80, 1, 2);
        var fallback = Unarmed();
        character.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);

        var effective = DisarmEligibility.GetEffectiveAttack(character, weapon, fallback);

        Assert.False(DisarmEligibility.CanUseEquippedWeapon(character));
        Assert.Same(fallback, effective);
        Assert.Equal(WeaponCategory.Unarmed, effective.Category);
        Assert.Equal(AttackType.Physical, effective.Type);
        Assert.Equal(1, effective.Power);
        Assert.Equal(73, effective.BaseAccuracy);
        Assert.Equal(1, effective.MinimumRange);
        Assert.Equal(1, effective.MaximumRange);
        Assert.Same(fallback, DisarmEligibility.GetEffectiveAttack(character, fallback));
    }

    [Theory]
    [InlineData(WeaponCategory.Sword)]
    [InlineData(WeaponCategory.Axe)]
    [InlineData(WeaponCategory.Lance)]
    [InlineData(WeaponCategory.Bow)]
    [InlineData(WeaponCategory.MagicScroll)]
    [InlineData(WeaponCategory.Unarmed)]
    public void Unarmed_IsTriangleNeutralInBothDirections(WeaponCategory other)
    {
        foreach (var outcome in new[]
        {
            WeaponTriangle.Evaluate(WeaponCategory.Unarmed, other),
            WeaponTriangle.Evaluate(other, WeaponCategory.Unarmed)
        })
        {
            Assert.Equal(WeaponTriangleOutcome.Neutral, outcome);
            Assert.Equal(0, WeaponTriangle.GetDamageModifier(outcome));
            Assert.Equal(0, WeaponTriangle.GetAccuracyModifier(outcome));
        }
    }

    [Fact]
    public void Exchange_DisarmedAttackerLosesWeaponPowerAccuracyAndTriangleBenefits_IncludingFollowUp()
    {
        var request = Request() with { Attacker = Unit(speed: 20), IncomingAttackPermitsFollowUps = true };
        request.Attacker.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        var provider = new FixedRoll();

        var result = Resolve(request, provider);

        Assert.Equal(11, result.OpeningStrike.Damage);
        Assert.Equal(73, result.OpeningStrike.HitChance);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.OpeningStrike.TriangleOutcome);
        Assert.Equal(19, result.CounterStrike!.Damage); // Axe is neutral against unarmed, with its own Power 9.
        Assert.Equal(FollowUpParticipant.Attacker, result.FollowUpBy);
        Assert.Equal(8, result.FollowUpStrike!.Damage);
        Assert.Equal(3, provider.Calls);
    }

    [Theory]
    [InlineData(WeaponCategory.Bow, 1, true)]
    [InlineData(WeaponCategory.Bow, 2, false)]
    [InlineData(WeaponCategory.Bow, 3, false)]
    [InlineData(WeaponCategory.Axe, 1, true)]
    [InlineData(WeaponCategory.MagicScroll, 1, true)]
    public void DisarmedCounter_UsesOnlyUnarmedPowerAndRange(WeaponCategory weapon, int distance, bool canCounter)
    {
        var request = Request() with
        {
            DefenderWeapon = weapon, Distance = distance,
            CounterattackType = weapon == WeaponCategory.MagicScroll ? AttackType.Magical : AttackType.Physical,
            CounterattackMinimumRange = weapon == WeaponCategory.Bow ? 2 : 1,
            CounterattackMaximumRange = weapon == WeaponCategory.Bow ? 3 : 2
        };
        request.Defender.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        var provider = new FixedRoll();

        var result = Resolve(request, provider);

        Assert.Equal(WeaponTriangleOutcome.Neutral, result.OpeningStrike.TriangleOutcome);
        Assert.Equal(17, result.OpeningStrike.Damage);
        Assert.Equal(canCounter, result.CounterattackOccurred);
        Assert.Equal(canCounter ? 2 : 1, provider.Calls);
        if (canCounter)
        {
            Assert.Equal(11, result.CounterStrike!.Damage);
            Assert.Equal(73, result.CounterStrike.HitChance);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(-1)]
    public void UnarmedOpening_RequiresDistanceOneBeforeAnyRolls(int distance)
    {
        var request = Request() with { Distance = distance };
        request.Attacker.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        var provider = new FixedRoll();

        Assert.Throws<ArgumentOutOfRangeException>(() => Resolve(request, provider));

        Assert.Equal(0, provider.Calls);
        Assert.Equal(200, request.Attacker.CurrentHealth);
        Assert.Equal(200, request.Defender.CurrentHealth);
    }

    [Fact]
    public void ExplicitUnarmedAttack_WorksWithoutDisarmOrFallback()
    {
        var request = Request() with
        {
            AttackerWeapon = WeaponCategory.Unarmed, OpeningPower = 1, OpeningBaseAccuracy = 73,
            AttackerUnarmedAttack = null
        };

        Assert.Equal(11, Resolve(request).OpeningStrike.Damage);
        Assert.Throws<ArgumentOutOfRangeException>(() => Resolve(request with { Distance = 2 }));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void DisarmedDefenderFollowUp_UsesUnarmedInputsAndCounterRange(int distance, bool canFollowUp)
    {
        var request = Request() with
        {
            Defender = Unit(speed: 20), DefenderWeapon = WeaponCategory.Bow,
            CounterattackMinimumRange = 2, CounterattackMaximumRange = 3,
            Distance = distance, IncomingAttackPermitsFollowUps = true
        };
        request.Defender.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);

        var result = Resolve(request);

        Assert.Equal(canFollowUp, result.CounterattackOccurred);
        Assert.Equal(canFollowUp, result.FollowUpOccurred);
        if (canFollowUp)
        {
            Assert.Equal(FollowUpParticipant.Defender, result.FollowUpBy);
            Assert.Equal(11, result.CounterStrike!.Damage);
            Assert.Equal(8, result.FollowUpStrike!.Damage);
            Assert.Equal(73, result.FollowUpStrike.HitChance);
        }
    }

    [Fact]
    public void Expiration_RestoresBowCounterRange()
    {
        var request = Request() with
        {
            DefenderWeapon = WeaponCategory.Bow, CounterattackMinimumRange = 2,
            CounterattackMaximumRange = 3, Distance = 2
        };
        request.Defender.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        Assert.False(Resolve(request).CounterattackOccurred);

        request.Defender.Statuses.AdvanceTimedStatuses();

        var result = Resolve(request);
        Assert.True(result.CounterattackOccurred);
        Assert.Equal(19, result.CounterStrike!.Damage);
        Assert.Equal(80, result.CounterStrike.HitChance);
    }

    [Fact]
    public void OneRoundExpiration_RestoresNormalWeaponAccessAndCombatInputs()
    {
        var request = Request();
        request.Attacker.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        Assert.Equal(11, Resolve(request).OpeningStrike.Damage);

        request.Attacker.Statuses.AdvanceTimedStatuses();

        Assert.True(DisarmEligibility.CanUseEquippedWeapon(request.Attacker));
        var result = Resolve(request);
        Assert.Equal(19, result.OpeningStrike.Damage);
        Assert.Equal(90, result.OpeningStrike.HitChance);
        Assert.Equal(WeaponTriangleOutcome.Advantage, result.OpeningStrike.TriangleOutcome);
    }

    [Fact]
    public void Reapplication_RefreshesOneRoundWithoutStacking()
    {
        var character = Unit();
        character.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        character.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);

        Assert.Single(character.Statuses.TimedStatuses);
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Disarm]);
        Assert.False(DisarmEligibility.CanUseEquippedWeapon(character));
        character.Statuses.AdvanceTimedStatuses();
        Assert.True(DisarmEligibility.CanUseEquippedWeapon(character));
    }

    [Fact]
    public void Disarm_CoexistsWithoutChangingHealthShieldsActivationOrStats()
    {
        var character = Unit();
        var stats = character.BaseStats;
        var snapshot = stats with { };
        character.ConfigureParalysisSpeedReduction(2); // Explicit prototype example.
        foreach (var status in new[] { StatusType.Burn, StatusType.Frostbite, StatusType.Paralysis, StatusType.Silence })
            character.Statuses.ApplyTimedStatus(status, 2);
        character.Statuses.ApplyPoison();
        character.SetShieldHP(6);
        character.EndActivation();
        character.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);

        Assert.False(DisarmEligibility.CanUseEquippedWeapon(character));
        Assert.Equal(1, DisarmEligibility.GetEffectiveAttack(character, Unarmed()).Power);
        Assert.Equal(200, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.True(character.HasActedThisRound);
        Assert.Equal(3, character.EffectiveMovement);
        Assert.Equal(8, character.EffectiveSpeed);
        Assert.Equal(7, character.EffectiveDefense);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(snapshot, character.BaseStats);
        Assert.Equal(5, character.Statuses.TimedStatuses.Count);
        Assert.True(character.Statuses.IsPoisoned);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
    }

    [Fact]
    public void CharactersSharingStats_HaveIndependentDisarmState()
    {
        var first = CharacterFactory.Create("nigel", 1)!;
        var second = CharacterFactory.Create("nigel", 2)!;
        Assert.Same(first.BaseStats, second.BaseStats);
        first.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);

        Assert.False(DisarmEligibility.CanUseEquippedWeapon(first));
        Assert.True(DisarmEligibility.CanUseEquippedWeapon(second));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingUnarmedInputs_AreRejectedBeforeRollsOrDamage(bool attacker)
    {
        var request = Request() with { AttackerUnarmedAttack = null, DefenderUnarmedAttack = null };
        (attacker ? request.Attacker : request.Defender).Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        var provider = new FixedRoll();

        Assert.Throws<InvalidOperationException>(() => Resolve(request, provider));
        Assert.Equal(0, provider.Calls);
        Assert.Equal(200, request.Attacker.CurrentHealth);
        Assert.Equal(200, request.Defender.CurrentHealth);
    }

    [Fact]
    public void NullInputsAndWeaponFallback_AreRejected()
    {
        var weapon = new AttackConfiguration(AttackType.Physical, WeaponCategory.Sword, 5, 90, 1, 1);
        Assert.Throws<ArgumentNullException>(() => DisarmEligibility.CanUseEquippedWeapon(null!));
        Assert.Throws<ArgumentNullException>(() => DisarmEligibility.GetEffectiveAttack(null!, weapon));
        Assert.Throws<ArgumentNullException>(() => DisarmEligibility.GetEffectiveAttack(Unit(), null!));
        Assert.Throws<ArgumentException>(() => DisarmEligibility.GetEffectiveAttack(Unit(), weapon, weapon));
    }

    [Theory]
    [InlineData(-1, 73)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public void InvalidUnarmedPowerOrAccuracy_IsRejected(int power, int accuracy)
        => Assert.Throws<ArgumentOutOfRangeException>(() => AttackConfiguration.CreateUnarmed(power, accuracy));

    [Theory]
    [InlineData(AttackType.Physical, WeaponCategory.Unarmed, 0, 1)]
    [InlineData(AttackType.Physical, WeaponCategory.Unarmed, 1, 2)]
    [InlineData(AttackType.Magical, WeaponCategory.Unarmed, 1, 1)]
    [InlineData(AttackType.Magical, WeaponCategory.Sword, 1, 1)]
    public void InvalidAttackCombinations_AreRejected(AttackType type, WeaponCategory category, int minimum, int maximum)
        => Assert.Throws<ArgumentException>(() => new AttackConfiguration(type, category, 1, 73, minimum, maximum));

    [Theory]
    [InlineData((AttackType)99, WeaponCategory.Sword, 1, 1)]
    [InlineData(AttackType.Physical, (WeaponCategory)6, 1, 1)]
    [InlineData(AttackType.Physical, WeaponCategory.Sword, -1, 1)]
    [InlineData(AttackType.Physical, WeaponCategory.Sword, 2, 1)]
    public void InvalidEnumsOrRange_AreRejected(AttackType type, WeaponCategory category, int minimum, int maximum)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new AttackConfiguration(type, category, 1, 73, minimum, maximum));

    private static Character Unit(int speed = 10) => new("test", "Test character", 1,
        new CharacterStats(200, 20, 10, 12, 8, 4, speed, 5));

    private static CombatExchangeRequest Request() => new(Unit(), Unit(), AttackType.Physical, 7,
        WeaponCategory.Sword, WeaponCategory.Axe, 80, AttackType.Physical, 9, 80, 1, 1, 1, true, false,
        AttackerUnarmedAttack: Unarmed(), DefenderUnarmedAttack: Unarmed());

    private static CombatExchangeResult Resolve(CombatExchangeRequest request, FixedRoll? provider = null)
        => new CombatExchangeResolver(new SingleStrikeResolver(new HitRoller(provider ?? new FixedRoll()))).Resolve(request);

    private sealed class FixedRoll : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }
}
