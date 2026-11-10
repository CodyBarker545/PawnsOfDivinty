using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class CombatExchangeResolverTests
{
    [Fact]
    public void OpeningHitAndCounterHit_ApplyOnceInOrder_WithoutRecursiveCounters()
    {
        var request = Request();
        var provider = new SequenceProvider(0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.True(result.OpeningStrike.Hit);
        Assert.Equal(100, result.OpeningStrike.HitChance);
        Assert.Equal(WeaponTriangleOutcome.Advantage, result.OpeningStrike.TriangleOutcome);
        Assert.Equal(13, result.OpeningStrike.Damage);
        Assert.Equal(13, result.DefenderHealthLost);
        Assert.Equal(39, request.Defender.CurrentHealth);
        Assert.True(result.CounterattackOccurred);
        Assert.NotNull(result.CounterStrike);
        Assert.True(result.CounterStrike.Hit);
        Assert.Equal(80, result.CounterStrike.HitChance);
        Assert.Equal(WeaponTriangleOutcome.Disadvantage, result.CounterStrike.TriangleOutcome);
        Assert.Equal(12, result.CounterStrike.Damage); // Nigel lost 13/52 HP: Fury adds 2 before his counter.
        Assert.Equal(12, result.AttackerHealthLost);
        Assert.Equal(26, request.Attacker.CurrentHealth);
        Assert.False(result.AttackerDefeated);
        Assert.False(result.DefenderDefeated);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public void OpeningMiss_StillPermitsAnEligibleIndependentCounter()
    {
        var request = Request() with { OpeningBaseAccuracy = 70 };
        var provider = new SequenceProvider(80, 79);

        var result = Resolver(provider).Resolve(request);

        Assert.False(result.OpeningStrike.Hit);
        Assert.Equal(0, result.OpeningStrike.Damage);
        Assert.Equal(0, result.DefenderHealthLost);
        Assert.Equal(52, request.Defender.CurrentHealth);
        Assert.True(result.CounterattackOccurred);
        Assert.NotNull(result.CounterStrike);
        Assert.True(result.CounterStrike.Hit);
        Assert.Equal(10, result.AttackerHealthLost);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public void LethalOpening_SkipsCounterAndReportsActualOverkillLoss()
    {
        var request = Request();
        request.Defender.TakeDamage(40);
        var provider = new SequenceProvider(0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(13, result.OpeningStrike.Damage);
        Assert.Equal(12, result.DefenderHealthLost);
        Assert.Equal(0, request.Defender.CurrentHealth);
        Assert.True(result.DefenderDefeated);
        Assert.False(result.CounterattackOccurred);
        Assert.Null(result.CounterStrike);
        Assert.Equal(0, result.AttackerHealthLost);
        Assert.Equal(38, request.Attacker.CurrentHealth);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void CounterMiss_DealsZeroDamageAndConsumesItsOwnRoll()
    {
        var request = Request();
        var provider = new SequenceProvider(0, 80);

        var result = Resolver(provider).Resolve(request);

        Assert.True(result.CounterattackOccurred);
        Assert.NotNull(result.CounterStrike);
        Assert.False(result.CounterStrike.Hit);
        Assert.Equal(0, result.CounterStrike.Damage);
        Assert.Equal(0, result.AttackerHealthLost);
        Assert.Equal(38, request.Attacker.CurrentHealth);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public void CounterCanDefeatOriginalAttacker_WithoutExceedingRemainingHp()
    {
        var request = Request();
        request.Attacker.TakeDamage(30);
        var provider = new SequenceProvider(0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.NotNull(result.CounterStrike);
        Assert.Equal(12, result.CounterStrike.Damage); // Includes Nigel's current +2 Fury.
        Assert.Equal(8, result.AttackerHealthLost);
        Assert.Equal(0, request.Attacker.CurrentHealth);
        Assert.True(result.AttackerDefeated);
        Assert.False(result.DefenderDefeated);
        Assert.Equal(2, provider.CallCount);
    }

    [Theory]
    [InlineData(2, true, false)]
    [InlineData(1, false, false)]
    [InlineData(1, true, true)]
    [InlineData(1, false, true)]
    public void IneligibleCounter_IsSkippedWithoutConsumingAnotherRoll(int distance, bool permits, bool suppressed)
    {
        var request = Request() with
        {
            Distance = distance,
            IncomingAttackPermitsCounters = permits,
            CounterattacksSuppressed = suppressed
        };
        var provider = new SequenceProvider(0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(13, result.DefenderHealthLost);
        Assert.False(result.CounterattackOccurred);
        Assert.Null(result.CounterStrike);
        Assert.Equal(0, result.AttackerHealthLost);
        Assert.Equal(1, provider.CallCount);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void BowCounter_RespectsSuppliedMinimumAndMaximumRange(int distance, bool canCounter)
    {
        var request = Request() with
        {
            Defender = Character("beothuk_spirit_guardian", 2),
            DefenderWeapon = WeaponCategory.Bow,
            CounterattackMinimumRange = 2,
            CounterattackMaximumRange = 3,
            Distance = distance
        };
        var provider = new SequenceProvider(0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(canCounter, result.CounterattackOccurred);
        Assert.Equal(canCounter ? 8 : 0, result.AttackerHealthLost);
        Assert.Equal(canCounter ? 2 : 1, provider.CallCount);
    }

    [Fact]
    public void MagicalOpeningAndCounter_UseNeutralMagicDamage()
    {
        var request = Request() with
        {
            Attacker = Character("time_weaver", 1),
            Defender = Character("gentle_guardian", 2),
            OpeningAttackType = AttackType.Magical,
            CounterattackType = AttackType.Magical,
            AttackerWeapon = WeaponCategory.MagicScroll,
            DefenderWeapon = WeaponCategory.MagicScroll,
            OpeningPower = 4,
            CounterattackPower = 3,
            CounterattackMaximumRange = 2,
            Distance = 2
        };
        var provider = new SequenceProvider(0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(6, result.OpeningStrike.Damage);
        Assert.Equal(6, result.DefenderHealthLost);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.OpeningStrike.TriangleOutcome);
        Assert.NotNull(result.CounterStrike);
        Assert.Equal(2, result.CounterStrike.Damage);
        Assert.Equal(2, result.AttackerHealthLost);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.CounterStrike.TriangleOutcome);
        Assert.Equal(90, result.CounterStrike.HitChance);
        Assert.Equal(2, provider.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedPhysicalAndMagicalStrikes_UseTheirExplicitAttackTypes(bool magicalOpening)
    {
        var request = Request() with
        {
            Defender = Character("gentle_guardian", 2),
            DefenderWeapon = WeaponCategory.MagicScroll,
            CounterattackType = AttackType.Magical,
            CounterattackPower = 3,
            CounterattackMaximumRange = 2
        };
        if (magicalOpening)
            request = request with
            {
                Attacker = request.Defender,
                Defender = request.Attacker,
                AttackerWeapon = WeaponCategory.MagicScroll,
                DefenderWeapon = WeaponCategory.Sword,
                OpeningAttackType = AttackType.Magical,
                OpeningPower = 3,
                CounterattackType = AttackType.Physical,
                CounterattackPower = 5
            };
        var provider = new SequenceProvider(0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(magicalOpening ? 6 : 11, result.DefenderHealthLost);
        Assert.Equal(magicalOpening ? 11 : 6, result.AttackerHealthLost);
        Assert.Equal(2, provider.CallCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Exchange_PreservesActivationFlagsStatsAndUnrelatedInstances(bool attackerActed, bool defenderActed)
    {
        var request = Request();
        var unrelated = Character("nigel", 2);
        unrelated.EndActivation();
        if (attackerActed) request.Attacker.EndActivation();
        if (defenderActed) request.Defender.EndActivation();
        var attackerStats = request.Attacker.BaseStats with { };
        var defenderStats = request.Defender.BaseStats with { };

        var result = Resolver(new SequenceProvider(0, 0)).Resolve(request);

        Assert.True(result.CounterattackOccurred);
        Assert.Equal(attackerActed, request.Attacker.HasActedThisRound);
        Assert.Equal(defenderActed, request.Defender.HasActedThisRound);
        Assert.Equal(attackerStats, request.Attacker.BaseStats);
        Assert.Equal(defenderStats, request.Defender.BaseStats);
        Assert.Same(unrelated.BaseStats, request.Defender.BaseStats);
        Assert.Equal(52, unrelated.CurrentHealth);
        Assert.True(unrelated.HasActedThisRound);
    }

    [Theory]
    [InlineData("nullAttacker")]
    [InlineData("nullDefender")]
    [InlineData("deadAttacker")]
    [InlineData("deadDefender")]
    [InlineData("openingType")]
    [InlineData("counterType")]
    [InlineData("attackerWeapon")]
    [InlineData("defenderWeapon")]
    [InlineData("openingPower")]
    [InlineData("counterPower")]
    [InlineData("openingAccuracyLow")]
    [InlineData("openingAccuracyHigh")]
    [InlineData("counterAccuracyLow")]
    [InlineData("counterAccuracyHigh")]
    [InlineData("distance")]
    [InlineData("minimumRange")]
    [InlineData("maximumRange")]
    [InlineData("openingMagicCategory")]
    [InlineData("counterMagicCategory")]
    [InlineData("openingOverflow")]
    [InlineData("openingTriangleOverflow")]
    [InlineData("counterOverflow")]
    [InlineData("counterTriangleOverflow")]
    [InlineData("counterMagicOverflow")]
    public void InvalidInputs_ConsumeNoRandomnessOrHealth_EvenWhenCounterDisabled(string invalid)
    {
        foreach (bool countersEnabled in new[] { true, false })
        {
            var request = InvalidRequest(invalid) with
            {
                IncomingAttackPermitsCounters = countersEnabled,
                IncomingAttackPermitsFollowUps = true
            };
            int? attackerHealth = request.Attacker?.CurrentHealth;
            int? defenderHealth = request.Defender?.CurrentHealth;
            var provider = new SequenceProvider(0, 0);
            var resolver = Resolver(provider);

            if (invalid.Contains("Overflow"))
                Assert.Throws<OverflowException>(() => resolver.Resolve(request));
            else
                Assert.ThrowsAny<ArgumentException>(() => resolver.Resolve(request));

            Assert.Equal(attackerHealth, request.Attacker?.CurrentHealth);
            Assert.Equal(defenderHealth, request.Defender?.CurrentHealth);
            Assert.Equal(0, provider.CallCount);
        }
    }

    [Fact]
    public void RandomProviderFailureDuringCounter_LeavesAlreadyAppliedOpeningDamage()
    {
        var request = Request();
        var provider = new SequenceProvider(0, 100);

        Assert.Throws<InvalidOperationException>(() => Resolver(provider).Resolve(request));

        Assert.Equal(39, request.Defender.CurrentHealth);
        Assert.Equal(38, request.Attacker.CurrentHealth);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public void ExchangeResult_IsImmutableSnapshot()
    {
        var request = Request();
        var result = Resolver(new SequenceProvider(0)).Resolve(request with { IncomingAttackPermitsCounters = false });
        request.Defender.TakeDamage(request.Defender.CurrentHealth);

        Assert.False(result.DefenderDefeated);
        Assert.True(request.Defender.IsDefeated);
        Assert.All(typeof(CombatExchangeResult).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void NullRequest_IsRejectedWithoutRandomness()
    {
        var provider = new SequenceProvider(0);

        Assert.Throws<ArgumentNullException>(() => Resolver(provider).Resolve(null!));
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void NullStrikeResolver_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CombatExchangeResolver(null!));
    }

    [Theory]
    [InlineData(20, 10, FollowUpParticipant.Attacker, 12)]
    [InlineData(10, 20, FollowUpParticipant.Defender, 9)]
    [InlineData(5, 4, FollowUpParticipant.Attacker, 8)]
    [InlineData(4, 5, FollowUpParticipant.Defender, 6)]
    [InlineData(7, 4, FollowUpParticipant.Attacker, 12)]
    [InlineData(6, 4, FollowUpParticipant.Attacker, 8)]
    [InlineData(20, 20, FollowUpParticipant.None, 0)]
    [InlineData(24, 20, FollowUpParticipant.None, 0)]
    [InlineData(1, 0, FollowUpParticipant.Attacker, 12)]
    [InlineData(0, 0, FollowUpParticipant.None, 0)]
    public void FollowUp_UsesSpeedOfEitherParticipantAndProvisionalScaling(
        int attackerSpeed, int defenderSpeed, FollowUpParticipant expected, int reducedDamage)
    {
        var request = FollowUpRequest(attackerSpeed, defenderSpeed);
        var provider = new SequenceProvider(0, 0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(expected != FollowUpParticipant.None, result.FollowUpOccurred);
        Assert.Equal(expected, result.FollowUpBy);
        Assert.Equal(reducedDamage, result.FollowUpHealthLost);
        Assert.Equal(expected == FollowUpParticipant.None ? 2 : 3, provider.CallCount);
        if (expected == FollowUpParticipant.None)
            Assert.Null(result.FollowUpStrike);
        else
        {
            Assert.NotNull(result.FollowUpStrike);
            Assert.True(result.FollowUpStrike.Hit);
            Assert.Equal(reducedDamage, result.FollowUpStrike.Damage);
            Assert.Equal(expected == FollowUpParticipant.Attacker ? 100 : 80, result.FollowUpStrike.HitChance);
        }
        int defenderLoss = 17 + (expected == FollowUpParticipant.Attacker ? reducedDamage : 0);
        int attackerLoss = 13 + (expected == FollowUpParticipant.Defender ? reducedDamage : 0);
        Assert.Equal(defenderLoss, result.DefenderHealthLost);
        Assert.Equal(attackerLoss, result.AttackerHealthLost);
        Assert.Equal(100 - defenderLoss, request.Defender.CurrentHealth);
        Assert.Equal(100 - attackerLoss, request.Attacker.CurrentHealth);
    }

    [Fact]
    public void FollowUpMiss_ConsumesItsOwnRollAndDealsZero()
    {
        var request = FollowUpRequest(20, 10) with { OpeningBaseAccuracy = 70 };
        var provider = new SequenceProvider(0, 0, 80);

        var result = Resolver(provider).Resolve(request);

        Assert.True(result.FollowUpOccurred);
        Assert.NotNull(result.FollowUpStrike);
        Assert.False(result.FollowUpStrike.Hit);
        Assert.Equal(80, result.FollowUpStrike.HitChance);
        Assert.Equal(0, result.FollowUpStrike.Damage);
        Assert.Equal(0, result.FollowUpHealthLost);
        Assert.Equal(17, result.DefenderHealthLost);
        Assert.Equal(83, request.Defender.CurrentHealth);
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public void OpeningMiss_DoesNotReuseItsRollForTheFollowUp()
    {
        var request = FollowUpRequest(20, 10) with { OpeningBaseAccuracy = 70 };
        var provider = new SequenceProvider(80, 0, 79);

        var result = Resolver(provider).Resolve(request);

        Assert.False(result.OpeningStrike.Hit);
        Assert.NotNull(result.FollowUpStrike);
        Assert.True(result.FollowUpStrike.Hit);
        Assert.Equal(12, result.DefenderHealthLost);
        Assert.Equal(88, request.Defender.CurrentHealth);
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public void CounterMiss_DoesNotStopEligibleDefenderFollowUp()
    {
        var request = FollowUpRequest(10, 20);
        var provider = new SequenceProvider(0, 80, 79);

        var result = Resolver(provider).Resolve(request);

        Assert.NotNull(result.CounterStrike);
        Assert.False(result.CounterStrike.Hit);
        Assert.Equal(FollowUpParticipant.Defender, result.FollowUpBy);
        Assert.NotNull(result.FollowUpStrike);
        Assert.True(result.FollowUpStrike.Hit);
        Assert.Equal(9, result.AttackerHealthLost);
        Assert.Equal(91, request.Attacker.CurrentHealth);
        Assert.Equal(3, provider.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MagicalFollowUp_UsesQualifyingParticipantsOwnSpell(bool attackerFaster)
    {
        var request = FollowUpRequest(attackerFaster ? 20 : 10, attackerFaster ? 10 : 20);
        request = attackerFaster
            ? request with { OpeningAttackType = AttackType.Magical, AttackerWeapon = WeaponCategory.MagicScroll, OpeningPower = 4 }
            : request with { CounterattackType = AttackType.Magical, DefenderWeapon = WeaponCategory.MagicScroll, CounterattackPower = 2 };
        var provider = new SequenceProvider(0, 0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(attackerFaster ? FollowUpParticipant.Attacker : FollowUpParticipant.Defender, result.FollowUpBy);
        Assert.NotNull(result.FollowUpStrike);
        Assert.Equal(attackerFaster ? 13 : 12, result.FollowUpStrike.Damage);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.FollowUpStrike.TriangleOutcome);
        Assert.Equal(attackerFaster ? 31 : 15, result.DefenderHealthLost);
        Assert.Equal(attackerFaster ? 15 : 28, result.AttackerHealthLost);
        Assert.Equal(3, provider.CallCount);
    }

    [Theory]
    [InlineData(20, 10)]
    [InlineData(10, 20)]
    public void FollowUpOverkill_AppliesReducedDamageAndCapsActualLoss(int attackerSpeed, int defenderSpeed)
    {
        var request = FollowUpRequest(attackerSpeed, defenderSpeed);
        bool attackerFaster = attackerSpeed > defenderSpeed;
        if (attackerFaster) request.Defender.TakeDamage(77);
        else request.Attacker.TakeDamage(82);

        var result = Resolver(new SequenceProvider(0, 0, 0)).Resolve(request);

        Assert.NotNull(result.FollowUpStrike);
        Assert.Equal(attackerFaster ? 12 : 9, result.FollowUpStrike.Damage);
        Assert.Equal(attackerFaster ? 6 : 5, result.FollowUpHealthLost);
        Assert.Equal(attackerFaster, result.DefenderDefeated);
        Assert.Equal(!attackerFaster, result.AttackerDefeated);
        Assert.Equal(0, attackerFaster ? request.Defender.CurrentHealth : request.Attacker.CurrentHealth);
        Assert.Equal(attackerFaster ? 23 : 18, attackerFaster ? result.DefenderHealthLost : result.AttackerHealthLost);
    }

    [Theory]
    [InlineData(20, 10, true)]
    [InlineData(10, 20, true)]
    [InlineData(20, 10, false)]
    [InlineData(10, 20, false)]
    public void DefeatedParticipantOrTarget_SkipsAllRemainingFollowUps(int attackerSpeed, int defenderSpeed, bool openingLethal)
    {
        var request = FollowUpRequest(attackerSpeed, defenderSpeed);
        if (openingLethal) request.Defender.TakeDamage(85);
        else request.Attacker.TakeDamage(90);
        var provider = new SequenceProvider(0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.False(result.FollowUpOccurred);
        Assert.Equal(FollowUpParticipant.None, result.FollowUpBy);
        Assert.Null(result.FollowUpStrike);
        Assert.Equal(0, result.FollowUpHealthLost);
        Assert.Equal(openingLethal ? 1 : 2, provider.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FollowUpsDisabledOrAoE_DoNotConsumeFollowUpRoll(bool aoe)
    {
        var request = FollowUpRequest(20, 10) with
        {
            IncomingAttackPermitsFollowUps = false,
            IncomingAttackPermitsCounters = !aoe
        };
        var provider = new SequenceProvider(0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.False(result.FollowUpOccurred);
        Assert.Equal(aoe ? 1 : 2, provider.CallCount);
    }

    [Theory]
    [InlineData(1, true, false, false)]
    [InlineData(2, true, false, true)]
    [InlineData(3, true, false, true)]
    [InlineData(4, true, false, false)]
    [InlineData(2, false, false, false)]
    [InlineData(2, true, true, false)]
    public void DefenderFollowUp_PreservesCounterRangeAndAvailability(
        int distance, bool permitsCounters, bool suppressed, bool expected)
    {
        var request = FollowUpRequest(10, 20) with
        {
            DefenderWeapon = WeaponCategory.Bow,
            Distance = distance,
            CounterattackMinimumRange = 2,
            CounterattackMaximumRange = 3,
            IncomingAttackPermitsCounters = permitsCounters,
            CounterattacksSuppressed = suppressed
        };
        var provider = new SequenceProvider(0, 0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(expected, result.FollowUpOccurred);
        Assert.Equal(expected, result.CounterattackOccurred);
        Assert.Equal(expected ? 3 : 1, provider.CallCount);
    }

    [Fact]
    public void AttackerFollowUp_CanOccurWhenDefenderCounterIsSuppressed()
    {
        var request = FollowUpRequest(20, 10) with { CounterattacksSuppressed = true };
        var provider = new SequenceProvider(0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.False(result.CounterattackOccurred);
        Assert.Equal(FollowUpParticipant.Attacker, result.FollowUpBy);
        Assert.Equal(29, result.DefenderHealthLost);
        Assert.Equal(0, result.AttackerHealthLost);
        Assert.Equal(2, provider.CallCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RosterFollowUp_PreservesStatsAndActivationFlags(bool attackerActed, bool defenderActed)
    {
        var request = Request() with { IncomingAttackPermitsFollowUps = true };
        if (attackerActed) request.Attacker.EndActivation();
        if (defenderActed) request.Defender.EndActivation();
        var attackerStats = request.Attacker.BaseStats with { };
        var defenderStats = request.Defender.BaseStats with { };
        var provider = new SequenceProvider(0, 0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(FollowUpParticipant.Attacker, result.FollowUpBy);
        Assert.NotNull(result.FollowUpStrike);
        Assert.Equal(9, result.FollowUpStrike.Damage);
        Assert.Equal(30, request.Defender.CurrentHealth);
        Assert.Equal(26, request.Attacker.CurrentHealth); // Nigel's counter includes +2 Fury.
        Assert.Equal(attackerStats, request.Attacker.BaseStats);
        Assert.Equal(defenderStats, request.Defender.BaseStats);
        Assert.Equal(attackerActed, request.Attacker.HasActedThisRound);
        Assert.Equal(defenderActed, request.Defender.HasActedThisRound);
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public void LegacyRequest_RequiresExplicitOptInForFollowUps()
    {
        var result = Resolver(new SequenceProvider(0, 0)).Resolve(Request());

        Assert.False(result.FollowUpOccurred);
    }

    [Fact]
    public void ProviderFailureOnFollowUp_LeavesOpeningAndCounterDamageApplied()
    {
        var request = FollowUpRequest(20, 10);
        var provider = new SequenceProvider(0, 0, 100);

        Assert.Throws<InvalidOperationException>(() => Resolver(provider).Resolve(request));

        Assert.Equal(83, request.Defender.CurrentHealth);
        Assert.Equal(87, request.Attacker.CurrentHealth);
        Assert.Equal(3, provider.CallCount);
    }

    private static CombatExchangeRequest FollowUpRequest(int attackerSpeed, int defenderSpeed) => new(
        new Character("test-attacker", "Test Attacker", 1, new CharacterStats(100, 20, 5, 18, 4, 0, attackerSpeed, 1)),
        new Character("test-defender", "Test Defender", 2, new CharacterStats(100, 20, 5, 18, 4, 0, defenderSpeed, 1)),
        AttackType.Physical, 0, WeaponCategory.Sword, WeaponCategory.Axe, 90,
        AttackType.Physical, 0, 90, 1, 1, 1, true, false, IncomingAttackPermitsFollowUps: true);

    [Fact]
    public void Exchange_ShieldsAbsorbOpeningAndCounter_WithoutChangingCalculatedDamage()
    {
        var request = Request();
        request.Attacker.SetShieldHP(6);
        request.Defender.SetShieldHP(6);
        var provider = new SequenceProvider(0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.Equal(13, result.OpeningStrike.Damage);
        Assert.Equal(7, result.DefenderHealthLost);
        Assert.NotNull(result.CounterStrike);
        Assert.Equal(11, result.CounterStrike.Damage); // Only 7 HP passed the shield: Fury adds 1.
        Assert.Equal(5, result.AttackerHealthLost);
        Assert.Equal(45, request.Defender.CurrentHealth);
        Assert.Equal(33, request.Attacker.CurrentHealth);
        Assert.Equal(0, request.Defender.ShieldHP);
        Assert.Equal(0, request.Attacker.ShieldHP);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public void ShieldCanKeepDefenderAliveToCounter()
    {
        var request = Request();
        request.Defender.TakeDamage(42);
        request.Defender.SetShieldHP(6);

        var result = Resolver(new SequenceProvider(0, 0)).Resolve(request);

        Assert.Equal(7, result.DefenderHealthLost);
        Assert.Equal(3, request.Defender.CurrentHealth);
        Assert.False(result.DefenderDefeated);
        Assert.True(result.CounterattackOccurred);
    }

    [Fact]
    public void FollowUp_AlsoUsesRemainingShieldBeforeHp()
    {
        var request = FollowUpRequest(20, 10);
        request.Defender.SetShieldHP(20);
        request.Attacker.SetShieldHP(20);
        var provider = new SequenceProvider(0, 0, 0);

        var result = Resolver(provider).Resolve(request);

        Assert.NotNull(result.FollowUpStrike);
        Assert.Equal(12, result.FollowUpStrike.Damage);
        Assert.Equal(9, result.FollowUpHealthLost);
        Assert.Equal(9, result.DefenderHealthLost);
        Assert.Equal(0, result.AttackerHealthLost);
        Assert.Equal(91, request.Defender.CurrentHealth);
        Assert.Equal(100, request.Attacker.CurrentHealth);
        Assert.Equal(0, request.Defender.ShieldHP);
        Assert.Equal(7, request.Attacker.ShieldHP);
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public void MagicalExchange_ShieldsAbsorbDamageBeforeHp()
    {
        var request = Request() with
        {
            Attacker = Character("time_weaver", 1),
            Defender = Character("gentle_guardian", 2),
            OpeningAttackType = AttackType.Magical,
            OpeningPower = 4,
            AttackerWeapon = WeaponCategory.MagicScroll,
            DefenderWeapon = WeaponCategory.MagicScroll,
            CounterattackType = AttackType.Magical,
            CounterattackPower = 3,
            CounterattackMaximumRange = 2,
            Distance = 2
        };
        request.Defender.SetShieldHP(6);
        request.Attacker.SetShieldHP(1);

        var result = Resolver(new SequenceProvider(0, 0)).Resolve(request);

        Assert.Equal(6, result.OpeningStrike.Damage);
        Assert.Equal(0, result.DefenderHealthLost);
        Assert.Equal(1, result.AttackerHealthLost);
        Assert.Equal(44, request.Defender.CurrentHealth);
        Assert.Equal(37, request.Attacker.CurrentHealth);
        Assert.Equal(0, request.Defender.ShieldHP);
        Assert.Equal(0, request.Attacker.ShieldHP);
    }

    private static CombatExchangeRequest InvalidRequest(string invalid)
    {
        var request = Request();
        var huge = new Character("huge", "Test", 2,
            new CharacterStats(1, int.MaxValue, 0, int.MaxValue, 0, 0, 0, 0));
        return invalid switch
        {
            "nullAttacker" => request with { Attacker = null! },
            "nullDefender" => request with { Defender = null! },
            "deadAttacker" => request with { Attacker = Defeat(request.Attacker) },
            "deadDefender" => request with { Defender = Defeat(request.Defender) },
            "openingType" => request with { OpeningAttackType = (AttackType)99 },
            "counterType" => request with { CounterattackType = (AttackType)(-1) },
            "attackerWeapon" => request with { AttackerWeapon = (WeaponCategory)(-1) },
            "defenderWeapon" => request with { DefenderWeapon = (WeaponCategory)99 },
            "openingPower" => request with { OpeningPower = -1 },
            "counterPower" => request with { CounterattackPower = -1 },
            "openingAccuracyLow" => request with { OpeningBaseAccuracy = -1 },
            "openingAccuracyHigh" => request with { OpeningBaseAccuracy = 101 },
            "counterAccuracyLow" => request with { CounterattackBaseAccuracy = -1 },
            "counterAccuracyHigh" => request with { CounterattackBaseAccuracy = 101 },
            "distance" => request with { Distance = -1 },
            "minimumRange" => request with { CounterattackMinimumRange = -1 },
            "maximumRange" => request with { CounterattackMinimumRange = 2, CounterattackMaximumRange = 1 },
            "openingMagicCategory" => request with { OpeningAttackType = AttackType.Magical },
            "counterMagicCategory" => request with { CounterattackType = AttackType.Magical },
            "openingOverflow" => request with { Attacker = huge, OpeningPower = int.MaxValue },
            "openingTriangleOverflow" => request with { Attacker = huge, OpeningPower = 11 },
            "counterOverflow" => request with { Defender = huge, CounterattackPower = int.MaxValue },
            "counterTriangleOverflow" => request with
            {
                Defender = huge, AttackerWeapon = WeaponCategory.Axe,
                DefenderWeapon = WeaponCategory.Sword, CounterattackPower = 8
            },
            "counterMagicOverflow" => request with
            {
                Defender = huge, DefenderWeapon = WeaponCategory.MagicScroll,
                CounterattackType = AttackType.Magical, CounterattackPower = int.MaxValue
            },
            _ => throw new InvalidOperationException("Unknown test scenario.")
        };
    }

    private static Character Defeat(Character character)
    {
        character.TakeDamage(character.CurrentHealth);
        return character;
    }

    private static CombatExchangeRequest Request() => new(
        Character("hooded_assassin", 1), Character("nigel", 2),
        AttackType.Physical, 5, WeaponCategory.Sword, WeaponCategory.Axe, 90,
        AttackType.Physical, 0, 90, 1, 1, 1, true, false);

    private static Character Character(string id, int team)
    {
        var character = CharacterFactory.Create(id, team);
        Assert.NotNull(character);
        return character;
    }

    private static CombatExchangeResolver Resolver(IRandomRollProvider provider) => new(new SingleStrikeResolver(new HitRoller(provider)));

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
