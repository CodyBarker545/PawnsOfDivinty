using System;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Gameplay;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class BerserkersFuryTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 1)]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(15, 2)]
    [InlineData(16, 3)]
    [InlineData(20, 3)]
    [InlineData(21, 4)]
    [InlineData(26, 5)]
    [InlineData(31, 5)]
    [InlineData(32, 6)]
    [InlineData(37, 7)]
    [InlineData(42, 8)]
    [InlineData(47, 9)]
    [InlineData(51, 9)]
    [InlineData(52, 10)]
    public void Formula_UsesCompleteTenPercentOfMaximumHealthMissing(int damage, int bonus)
    {
        var nigel = CharacterFactory.Create("nigel", 1)!;
        nigel.TakeDamage(damage);
        var result = BerserkersFury.Evaluate(nigel);
        Assert.True(result.OwnsPassive);
        Assert.Equal(nigel.Id, result.CharacterId);
        Assert.Equal(52, result.MaxHealth);
        Assert.Equal(52 - damage, result.CurrentHealth);
        Assert.Equal(damage, result.MissingHealth);
        Assert.Equal(bonus, result.PhysicalDamageBonus);
        Assert.Equal(bonus == 0 ? BerserkersFuryOutcome.NoMissingHealthBonus : BerserkersFuryOutcome.DamageBonusAvailable, result.Outcome);
        Assert.Equal(bonus > 0, result.UsesProvisionalNormalDamageComposition);
    }

    [Theory]
    [InlineData("hooded_assassin")]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("gravebound_colossus")]
    [InlineData("beothuk_spirit_guardian")]
    public void OtherRosterCharacters_DoNotReceiveNigelsBonus(string id)
    {
        var character = CharacterFactory.Create(id, 1)!;
        character.TakeDamage(character.BaseStats.MaxHealth / 2);
        var result = BerserkersFury.Evaluate(character);
        Assert.Equal(BerserkersFuryOutcome.NotPassiveOwner, result.Outcome);
        Assert.False(result.OwnsPassive);
        Assert.Equal(0, result.PhysicalDamageBonus);
    }

    [Fact]
    public void Ownership_RequiresBothNigelIdentityAndRegisteredPassiveClassification()
    {
        var without = new Character("without", new CharacterDefinition("nigel", "Fixture", "", Nigel.Definition.BaseStats), 1);
        var copied = new Character("copy", new CharacterDefinition("other", "Fixture", "", Nigel.Definition.BaseStats, Nigel.Definition.Abilities), 1);
        var wrongClass = new Character("wrongClass", new CharacterDefinition("nigel", "Fixture", "", Nigel.Definition.BaseStats,
            new[] { new AbilityDefinition(Nigel.BerserkersFuryId, "Fixture", "", AbilityClassification.Active) }), 1);
        foreach (var character in new[] { without, copied, wrongClass })
        {
            character.TakeDamage(26);
            Assert.False(BerserkersFury.Evaluate(character).OwnsPassive);
            Assert.Equal(0, BerserkersFury.GetPhysicalDamageBonus(character));
        }
    }

    [Fact]
    public void Evaluation_IsDerivedNotAwardedAndHealingImmediatelyReducesTheBonus()
    {
        var nigel = CharacterFactory.Create("nigel", 1)!;
        nigel.TakeDamage(26);
        var snapshot = BerserkersFury.Evaluate(nigel);
        for (int repeat = 0; repeat < 5; repeat++) Assert.Equal(5, BerserkersFury.GetPhysicalDamageBonus(nigel));
        nigel.Heal(1);
        Assert.Equal(4, BerserkersFury.GetPhysicalDamageBonus(nigel));
        Assert.Equal(5, snapshot.PhysicalDamageBonus);
        Assert.Equal(26, snapshot.CurrentHealth);
        nigel.Heal(int.MaxValue);
        Assert.Equal(0, BerserkersFury.GetPhysicalDamageBonus(nigel));
        Assert.Empty(nigel.TemporaryModifiers.Active);
    }

    [Fact]
    public void DifferentNigelInstancesHaveIndependentBonusesWithoutSharedProgress()
    {
        var first = CharacterFactory.Create("nigel", 1)!;
        var second = CharacterFactory.Create("nigel", 1)!;
        first.TakeDamage(26);
        Assert.Equal(5, BerserkersFury.GetPhysicalDamageBonus(first));
        Assert.Equal(0, BerserkersFury.GetPhysicalDamageBonus(second));
        second.TakeDamage(6);
        Assert.Equal(1, BerserkersFury.GetPhysicalDamageBonus(second));
        first.Heal(26);
        Assert.Equal(0, BerserkersFury.GetPhysicalDamageBonus(first));
        Assert.Equal(1, BerserkersFury.GetPhysicalDamageBonus(second));
        Assert.Same(first.BaseStats, second.BaseStats);
    }

    [Fact]
    public void ShieldsAbsorbingDamage_DoNotCountAsMissingHealth()
    {
        var nigel = CharacterFactory.Create("nigel", 1)!;
        nigel.SetShieldHP(10);
        nigel.TakeDamage(10);
        Assert.Equal(0, BerserkersFury.GetPhysicalDamageBonus(nigel));
        nigel.TakeDamage(6);
        Assert.Equal(1, BerserkersFury.GetPhysicalDamageBonus(nigel));
    }

    [Fact]
    public void DefeatDoesNotStoreProgressOrAuthorizeAnAttack()
    {
        var nigel = CharacterFactory.Create("nigel", 1)!;
        nigel.TakeDamage(int.MaxValue);
        Assert.True(BerserkersFury.Evaluate(nigel).IsDefeated);
        Assert.Equal(10, BerserkersFury.GetPhysicalDamageBonus(nigel)); // Formula at zero HP, not permission to act.
        Assert.False(nigel.CanAct);
        nigel.Heal(100);
        Assert.True(nigel.IsDefeated);
        var request = Exchange(nigel, Fixture("target", 2));
        var rolls = new Rolls();
        Assert.Throws<ArgumentException>(() => Resolver(rolls).Resolve(request));
        Assert.Equal(0, rolls.Calls);
        Assert.Equal(0, BerserkersFury.GetPhysicalDamageBonus(CharacterFactory.Create("nigel", 1)!));
    }

    [Fact]
    public void RoundChangesAndActivationReset_DoNotResetCurrentHealthDerivedBonus()
    {
        var nigel = CharacterFactory.Create("nigel", 1)!;
        var enemy = Fixture("enemy", 2);
        nigel.TakeDamage(26);
        var activations = new ActivationManager(new[] { nigel }, new[] { enemy }, 1);
        activations.StartActivation(nigel);
        activations.CompleteActivation();
        activations.StartActivation(enemy);
        activations.CompleteActivation();
        activations.StartNextRound(1);
        Assert.Equal(2, activations.CurrentRound);
        Assert.Equal(5, BerserkersFury.GetPhysicalDamageBonus(nigel));
        Assert.Equal(26, nigel.CurrentHealth);
        Assert.Empty(nigel.Cooldowns.Cooldowns);
        Assert.Empty(nigel.TemporaryModifiers.Active);
    }

    [Fact]
    public void LargeAndSmallMaximumHealth_UseIntegerSafeArithmetic()
    {
        var large = NigelFixture(maxHealth: int.MaxValue);
        large.TakeDamage(int.MaxValue - 1);
        Assert.Equal(9, BerserkersFury.GetPhysicalDamageBonus(large));
        large.TakeDamage(1);
        Assert.Equal(10, BerserkersFury.GetPhysicalDamageBonus(large));
        var small = NigelFixture(maxHealth: 2);
        small.TakeDamage(1);
        Assert.Equal(5, BerserkersFury.GetPhysicalDamageBonus(small));
        var one = NigelFixture(maxHealth: 1);
        Assert.Equal(0, BerserkersFury.GetPhysicalDamageBonus(one));
    }

    [Theory]
    [InlineData(WeaponCategory.Sword, 8, 80)]
    [InlineData(WeaponCategory.Lance, 12, 100)]
    [InlineData(WeaponCategory.Axe, 10, 90)]
    [InlineData(WeaponCategory.Bow, 10, 90)]
    public void PhysicalOpening_AddsFuryToExistingTriangleDamageWithoutChangingAccuracy(WeaponCategory defenderWeapon, int damage, int accuracy)
    {
        var nigel = NigelFixture();
        nigel.TakeDamage(50);
        var target = Fixture("enemy", 2, defense: 5);
        var request = Exchange(nigel, target) with { DefenderWeapon = defenderWeapon, IncomingAttackPermitsCounters = false };
        var rolls = new Rolls();
        var result = Resolver(rolls).Resolve(request);
        Assert.Equal(damage, result.OpeningStrike.Damage); // Existing normal 5 +/- triangle, plus 5 Fury.
        Assert.Equal(accuracy, result.OpeningStrike.HitChance);
        Assert.Equal(1, rolls.Calls);
        Assert.Equal(50, nigel.CurrentHealth);
        Assert.Empty(nigel.Cooldowns.Cooldowns);
    }

    [Fact]
    public void Counter_UsesHealthAfterTheOpeningHit()
    {
        var nigel = NigelFixture(speed: 10);
        var attacker = Fixture("enemy", 2, strength: 15);
        var request = Exchange(attacker, nigel) with { AttackerWeapon = WeaponCategory.Bow, DefenderWeapon = WeaponCategory.Axe };
        var result = Resolver(new Rolls()).Resolve(request);
        Assert.Equal(15, result.OpeningStrike.Damage);
        Assert.Equal(11, result.CounterStrike!.Damage); // 15% missing grants +1 now, not zero from exchange start.
        Assert.Equal(85, nigel.CurrentHealth);
    }

    [Fact]
    public void AttackerFollowUp_RecalculatesFuryAfterCounterAndThenUsesExistingReduction()
    {
        var nigel = NigelFixture(speed: 20);
        nigel.TakeDamage(19);
        var enemy = Fixture("enemy", 2, speed: 10);
        var request = Exchange(nigel, enemy) with { IncomingAttackPermitsFollowUps = true };
        var rolls = new Rolls();
        var result = Resolver(rolls).Resolve(request);
        Assert.Equal(11, result.OpeningStrike.Damage); // +1 at 19% missing.
        Assert.Equal(10, result.CounterStrike!.Damage);
        Assert.Equal(FollowUpParticipant.Attacker, result.FollowUpBy);
        Assert.Equal(9, result.FollowUpStrike!.Damage); // floor((10 + 2 at 29% missing) * .75).
        Assert.Equal(71, nigel.CurrentHealth);
        Assert.Equal(3, rolls.Calls);
    }

    [Fact]
    public void DefenderFollowUp_ReceivesFuryAndDoesNotTriggerAnotherCounter()
    {
        var attacker = Fixture("enemy", 2, strength: 15, speed: 10);
        var nigel = NigelFixture(speed: 20);
        var request = Exchange(attacker, nigel) with
        {
            AttackerWeapon = WeaponCategory.Bow, DefenderWeapon = WeaponCategory.Axe,
            IncomingAttackPermitsFollowUps = true
        };
        var rolls = new Rolls();
        var result = Resolver(rolls).Resolve(request);
        Assert.Equal(11, result.CounterStrike!.Damage);
        Assert.Equal(FollowUpParticipant.Defender, result.FollowUpBy);
        Assert.Equal(8, result.FollowUpStrike!.Damage);
        Assert.Equal(3, rolls.Calls);
        Assert.Equal(19, result.AttackerHealthLost);
    }

    [Fact]
    public void IncomingShieldAbsorptionKeepsCounterBonusAtZero_AndOutgoingShieldAbsorbsBoostedDamage()
    {
        var nigel = NigelFixture();
        var enemy = Fixture("enemy", 2);
        nigel.SetShieldHP(20);
        var countered = Resolver(new Rolls()).Resolve(Exchange(enemy, nigel) with
            { AttackerWeapon = WeaponCategory.Bow, DefenderWeapon = WeaponCategory.Axe });
        Assert.Equal(0, countered.DefenderHealthLost);
        Assert.Equal(10, countered.CounterStrike!.Damage);
        nigel.SetShieldHP(0);
        nigel.TakeDamage(50);
        var target = Fixture("shielded", 2);
        target.SetShieldHP(6);
        var boosted = Resolver(new Rolls()).Resolve(Exchange(nigel, target) with { IncomingAttackPermitsCounters = false });
        Assert.Equal(15, boosted.OpeningStrike.Damage);
        Assert.Equal(6, boosted.OpeningShieldAbsorbed);
        Assert.Equal(9, boosted.DefenderHealthLost);
    }

    [Fact]
    public void BurnAndDisarmKeepExistingEffectsWhileFuryRemainsAPhysicalBonus()
    {
        var nigel = NigelFixture();
        var target = Fixture("enemy", 2, defense: 5);
        nigel.TakeDamage(50);
        nigel.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var request = Exchange(nigel, target) with
        {
            IncomingAttackPermitsCounters = false, AttackerUnarmedAttack = AttackConfiguration.CreateUnarmed(0, 90)
        };
        var result = Resolver(new Rolls()).Resolve(request);
        Assert.Equal(13, result.OpeningStrike.Damage); // 10 STR - 2 effective DEF + 5 Fury, no weapon benefits.
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.OpeningStrike.TriangleOutcome);
        Assert.Equal(5, target.BaseStats.Defense);
        Assert.Equal(10, nigel.BaseStats.Strength);
    }

    [Fact]
    public void FuryDoesNotStrengthenMagicOrMisses()
    {
        var nigel = NigelFixture();
        nigel.TakeDamage(50);
        var enemy = Fixture("enemy", 2);
        var magical = Exchange(nigel, enemy) with { OpeningAttackType = AttackType.Magical,
            AttackerWeapon = WeaponCategory.MagicScroll, IncomingAttackPermitsCounters = false };
        Assert.Equal(3, Resolver(new Rolls()).Resolve(magical).OpeningStrike.Damage);
        var missed = Resolver(new Rolls(99)).Resolve(Exchange(nigel, enemy) with { IncomingAttackPermitsCounters = false });
        Assert.False(missed.OpeningStrike.Hit);
        Assert.Equal(0, missed.OpeningStrike.Damage);
        Assert.Equal(5, BerserkersFury.GetPhysicalDamageBonus(nigel));
    }

    [Fact]
    public void PassiveCannotBeManuallyUsedAndEvaluationConsumesNoState()
    {
        var nigel = CharacterFactory.Create("nigel", 1)!;
        var enemy = Fixture("enemy", 2);
        nigel.TakeDamage(26);
        nigel.SetShieldHP(6);
        nigel.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        HealingApplicator.Apply(0, nigel);
        var activations = new ActivationManager(new[] { nigel }, new[] { enemy }, 1);
        activations.StartActivation(nigel);
        var use = new AbilityUse(nigel, Nigel.BerserkersFuryId, activations);
        Assert.Equal(AbilityRejectionReason.NotManuallyActivatable, AbilityUseFinalizer.FinalizeSuccessfulUse(use).Eligibility.RejectionReason);
        var stats = nigel.BaseStats;
        var result = BerserkersFury.Evaluate(nigel);
        Assert.Equal(5, result.PhysicalDamageBonus);
        Assert.Equal(26, nigel.CurrentHealth);
        Assert.Equal(6, nigel.ShieldHP);
        Assert.Equal(2, nigel.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(1, nigel.HealsReceivedThisRound);
        Assert.Same(stats, nigel.BaseStats);
        Assert.False(activations.MainActionUsed);
        Assert.False(activations.UtilityActionUsed);
        Assert.False(nigel.HasActedThisRound);
        Assert.Empty(nigel.Cooldowns.Cooldowns);
        Assert.All(typeof(BerserkersFuryResult).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void RoarPreviewAndKnockbackFuryOrderingRemainCompatible()
    {
        var nigel = CharacterFactory.Create("nigel", 1)!;
        var enemy = Fixture("enemy", 2);
        nigel.SetTacticalPosition(new(5, 5));
        enemy.SetTacticalPosition(new(6, 5));
        var activations = new ActivationManager(new[] { nigel }, new[] { enemy }, 1);
        activations.StartActivation(nigel);
        nigel.TakeDamage(26);
        var rules = new AbilityTargetingConstraints(AbilityTargetType.Character, requiresMapValidation: false,
            requiresLineOfSight: false, requiresElevationRules: false);
        var roar = new ProvokingRoarAbilityExecutor().Preview(new(nigel, Nigel.ProvokingRoarId, activations), rules);
        Assert.True(roar.CanExecute);
        var rolls = new Rolls();
        var resolver = new BrutalKnockbackResolver(new(new(rolls)), new ForcedMovementService());
        var axe = new AttackConfiguration(AttackType.Physical, WeaponCategory.Axe, 0, 90, 1, 1);
        var knockback = new BrutalKnockbackAbilityExecutor(resolver).Execute(new(nigel, Nigel.BrutalKnockbackId, activations),
            enemy, axe, WeaponCategory.Bow, rules, 2, null);
        Assert.Equal(BrutalKnockbackRejectionReason.DisplacementNotResolved, knockback.Effect!.RejectionReason);
        var isolated = resolver.ResolveOpeningStrike(nigel, enemy, axe, WeaponCategory.Bow);
        Assert.Equal(BrutalKnockbackRejectionReason.None, isolated.RejectionReason);
        Assert.Equal(5, isolated.FuryBonus);
        Assert.Equal(12, isolated.Strike!.Damage); // floor((20 normal + 5 Fury) / 2).
        Assert.Equal(1, rolls.Calls);
        Assert.Empty(nigel.Cooldowns.Cooldowns);
        Assert.True(activations.CanUseMainAction);
    }

    [Fact]
    public void PotentialCounterOverflowIsRejectedBeforeOpeningDamageOrRandomness()
    {
        var attacker = Fixture("attacker", 2, strength: 90);
        var nigel = NigelFixture(strength: int.MaxValue - 5);
        var request = Exchange(attacker, nigel) with { AttackerWeapon = WeaponCategory.Bow, DefenderWeapon = WeaponCategory.Axe };
        var rolls = new Rolls();
        Assert.Throws<OverflowException>(() => Resolver(rolls).Resolve(request));
        Assert.Equal(0, rolls.Calls);
        Assert.Equal(100, nigel.CurrentHealth);
        Assert.Equal(100, attacker.CurrentHealth);
    }

    [Fact]
    public void InvalidCalculatorInputsDoNotConsumeRandomnessOrMutateState()
    {
        Assert.Throws<ArgumentNullException>(() => BerserkersFury.Evaluate(null!));
        var nigel = NigelFixture();
        var enemy = Fixture("enemy", 2);
        var rolls = new Rolls();
        var strike = new SingleStrikeResolver(new HitRoller(rolls));
        Assert.Throws<ArgumentOutOfRangeException>(() => strike.ResolvePhysicalStrike(nigel.BaseStats, enemy.BaseStats,
            0, WeaponCategory.Axe, WeaponCategory.Bow, 90, physicalDamageBonus: -1));
        Assert.Throws<OverflowException>(() => strike.ResolvePhysicalStrike(nigel.BaseStats, enemy.BaseStats,
            0, WeaponCategory.Axe, WeaponCategory.Bow, 90, physicalDamageBonus: int.MaxValue));
        Assert.Equal(0, rolls.Calls);
        Assert.Equal(100, enemy.CurrentHealth);
    }

    // These explicit stat fixtures isolate thresholds and strike sequencing; they do not change roster balance.
    private static Character NigelFixture(int maxHealth = 100, int strength = 10, int speed = 10)
        => CharacterFactory.Create(new CharacterDefinition("nigel", "Nigel fixture", "Test only",
            new CharacterStats(maxHealth, strength, 0, 3, 0, 0, speed, 1), Nigel.Definition.Abilities), "nigel_fixture", 1);

    private static Character Fixture(string id, int team, int strength = 10, int defense = 0, int speed = 10)
        => new(id, "Test combatant", team, new CharacterStats(100, strength, defense, 0, 0, 0, speed, 1));

    private static CombatExchangeRequest Exchange(Character attacker, Character defender)
        => new(attacker, defender, AttackType.Physical, 0, WeaponCategory.Axe, WeaponCategory.Bow, 90,
            AttackType.Physical, 0, 90, 1, 1, 1, true, false);

    private static CombatExchangeResolver Resolver(Rolls rolls) => new(new(new HitRoller(rolls)));

    private sealed class Rolls(int roll = 0) : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return roll; }
    }
}
