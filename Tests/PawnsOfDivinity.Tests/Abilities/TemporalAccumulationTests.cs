using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class TemporalAccumulationTests
{
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy,
        true, requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);

    [Fact]
    public void FirstConfirmedElement_EarnsOnePersistentStackAfterTheAttack()
    {
        var s = new Setup();
        var stats = s.Actor.BaseStats;
        var strike = s.Strike();
        Assert.Equal(13, strike.Damage);
        var result = TemporalAccumulation.Process(s.Actor, "first-use", "fire", strike);
        Assert.Equal(TemporalAccumulationOutcome.StackEarned, result.Outcome);
        Assert.Equal(s.Actor.Id, result.CharacterId);
        Assert.Equal(0, result.PreviousStacks);
        Assert.Equal(0, result.MagicalDamageBonusForThisUse);
        Assert.Equal(1, result.ResultingStacks);
        Assert.Equal(new[] { "fire" }, result.KnownElements);
        Assert.Equal(1, TemporalAccumulation.GetMagicalDamageBonus(s.Actor));
        Assert.Equal(13, strike.Damage); // Awarding a stack does not rewrite the completed attack.
        Assert.Same(stats, s.Actor.BaseStats);
        Assert.Equal(17, stats.Intelligence);
        s.AssertNoActionOrHealthChanges();
    }

    [Fact]
    public void LaterUses_GetOnlyPreviouslyEarnedStacks_AndReusingAnElementDoesNotAddAnother()
    {
        var s = new Setup();
        TemporalAccumulation.Process(s.Actor, "fire-use", "fire", s.Strike());
        var repeated = s.Strike();
        Assert.Equal(14, repeated.Damage);
        var sameElement = TemporalAccumulation.Process(s.Actor, "fire-again", "fire", repeated);
        Assert.Equal(TemporalAccumulationOutcome.ElementAlreadyKnown, sameElement.Outcome);
        Assert.Equal(1, sameElement.ResultingStacks);
        // The second distinct ID is an explicit fixture, not a newly defined canonical element.
        var different = TemporalAccumulation.Process(s.Actor, "other-use", "other_element_fixture", s.Strike());
        Assert.Equal(1, different.MagicalDamageBonusForThisUse);
        Assert.Equal(2, different.ResultingStacks);
        Assert.Equal(15, s.Strike().Damage);
        Assert.Equal(new[] { "fire", "other_element_fixture" }, s.Actor.TemporalAccumulation.KnownElements);
    }

    [Fact]
    public void IncreasedScrollPower_DoesNotCreateAnotherStackForTheSameElement()
    {
        var s = new Setup();
        TemporalAccumulation.Process(s.Actor, "first", "fire", s.Strike());
        var higherPowerStrike = s.Strike(power: 6); // Explicit combat fixture, not an added equipment upgrade.
        Assert.Equal(16, higherPowerStrike.Damage);
        var result = TemporalAccumulation.Process(s.Actor, "higher-level-use", "fire", higherPowerStrike);
        Assert.Equal(TemporalAccumulationOutcome.ElementAlreadyKnown, result.Outcome);
        Assert.Equal(1, s.Actor.TemporalAccumulation.StackCount);
    }

    [Fact]
    public void RepeatedEvent_GrantsNoDuplicateBenefitAndKeepsItsOriginalBonusSnapshot()
    {
        var s = new Setup();
        var firstStrike = s.Strike();
        var original = TemporalAccumulation.Process(s.Actor, "first", "fire", firstStrike);
        TemporalAccumulation.Process(s.Actor, "second", "other_element_fixture", s.Strike());
        var duplicate = TemporalAccumulation.Process(s.Actor, "first", "different_id_attempt", firstStrike);
        Assert.Equal(TemporalAccumulationOutcome.AlreadyProcessed, duplicate.Outcome);
        Assert.Equal(0, duplicate.MagicalDamageBonusForThisUse);
        Assert.Equal(2, duplicate.ResultingStacks);
        Assert.Equal(1, original.ResultingStacks);
        Assert.Equal(new[] { "fire" }, original.KnownElements);
        Assert.Equal(2, s.Actor.TemporalAccumulation.StackCount);
        s.AssertNoActionOrHealthChanges();
    }

    [Theory]
    [InlineData("miss")]
    [InlineData("interruption")]
    public void AmbiguousUses_ReportUnresolvedWithoutAwardingAStack(string outcome)
    {
        var s = new Setup();
        var miss = outcome == "miss" ? s.Strike(accuracy: 0) : null;
        var result = TemporalAccumulation.Process(s.Actor, "ambiguous", "fire", miss, interrupted: outcome == "interruption");
        Assert.Equal(TemporalAccumulationOutcome.UnresolvedMissOrInterruption, result.Outcome);
        Assert.Equal(0, result.ResultingStacks);
        Assert.Empty(s.Actor.TemporalAccumulation.KnownElements);
        var attemptedReplay = TemporalAccumulation.Process(s.Actor, "ambiguous", "fire", s.Strike());
        Assert.Equal(TemporalAccumulationOutcome.AlreadyProcessed, attemptedReplay.Outcome);
        Assert.Equal(0, attemptedReplay.ResultingStacks);
        s.AssertNoActionOrHealthChanges();
    }

    [Fact]
    public void NoCompletedUseOrMissingElement_IsExplicitWithoutConsumingAnEvent()
    {
        var s = new Setup();
        Assert.Equal(TemporalAccumulationOutcome.NotCompleted, TemporalAccumulation.Process(s.Actor, "pending", "fire", null).Outcome);
        var strike = s.Strike();
        Assert.Equal(TemporalAccumulationOutcome.ElementRequired, TemporalAccumulation.Process(s.Actor, "pending", null, strike).Outcome);
        Assert.Equal(0, s.Actor.TemporalAccumulation.StackCount);
        Assert.Equal(TemporalAccumulationOutcome.StackEarned, TemporalAccumulation.Process(s.Actor, "pending", "fire", strike).Outcome);
    }

    [Fact]
    public void Ownership_IsRequiredAndThePassiveIsNotManuallyActivatable()
    {
        var s = new Setup();
        Assert.Equal(AbilityRejectionReason.NotManuallyActivatable,
            AbilityEligibility.Evaluate(s.Actor, "temporal_accumulation", s.Activations).RejectionReason);
        var other = CharacterFactory.Create("nigel", 1)!;
        var result = TemporalAccumulation.Process(other, "event", "fire", s.Strike());
        Assert.Equal(TemporalAccumulationOutcome.NotPassiveOwner, result.Outcome);
        Assert.Equal(0, TemporalAccumulation.GetMagicalDamageBonus(other));
        var original = s.Actor.Definition!;
        var withoutPassive = CharacterFactory.Create(new CharacterDefinition(original.Id, original.Name, original.Description,
            original.BaseStats, original.Abilities.Where(a => a.Id != "temporal_accumulation")), "without-passive", 1);
        Assert.Equal(TemporalAccumulationOutcome.NotPassiveOwner,
            TemporalAccumulation.Process(withoutPassive, "event", "fire", s.Strike()).Outcome);
        s.AssertNoActionOrHealthChanges();
    }

    [Fact]
    public void SeparateCharacterInstances_DoNotShareElementsOrEventHistory()
    {
        var first = new Setup();
        var second = new Setup();
        TemporalAccumulation.Process(first.Actor, "same-event-id", "fire", first.Strike());
        Assert.Equal(0, second.Actor.TemporalAccumulation.StackCount);
        Assert.Equal(TemporalAccumulationOutcome.StackEarned,
            TemporalAccumulation.Process(second.Actor, "same-event-id", "fire", second.Strike()).Outcome);
        Assert.Equal(1, first.Actor.TemporalAccumulation.StackCount);
        Assert.Equal(1, second.Actor.TemporalAccumulation.StackCount);
    }

    [Fact]
    public void ProgressPersistsAcrossDefeatAndActivationOrHealingResets()
    {
        var s = new Setup();
        TemporalAccumulation.Process(s.Actor, "fire", "fire", s.Strike());
        s.Actor.TakeDamage(int.MaxValue);
        Assert.True(s.Actor.IsDefeated);
        Assert.Equal(1, s.Actor.TemporalAccumulation.StackCount);
        s.Actor.ResetActivation();
        s.Actor.ResetHealingHistory();
        Assert.Equal(1, TemporalAccumulation.GetMagicalDamageBonus(s.Actor));
        Assert.Equal(new[] { "fire" }, s.Actor.TemporalAccumulation.KnownElements);
        // Redeployment is not implemented; retaining this same runtime Character retains its progress.
        Assert.True(s.Actor.IsDefeated);
    }

    [Fact]
    public void RoundPreparationAndCleanup_DoNotExpirePermanentProgress()
    {
        var s = new Setup();
        TemporalAccumulation.Process(s.Actor, "first", "fire", s.Strike());
        s.Activations.CompleteActivation();
        s.Actor.TemporaryModifiers.ExpireAtRoundEnd(2, RoundPhase.Cleanup);
        while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        var roster = new[] { s.Actor, s.Target };
        s.Lifecycle.AdvancePhase();
        new PoisonBatchProcessor(s.Lifecycle).Process(roster);
        s.Lifecycle.AdvancePhase();
        new DelayedAttackProcessor(new(), s.Lifecycle).Process(roster);
        s.Lifecycle.AdvancePhase();
        new TimedStatusBatchProcessor(s.Lifecycle).Process(roster);
        s.Lifecycle.AdvancePhase();
        s.Lifecycle.AdvancePhase();
        new RoundPreparationCoordinator(s.Activations, s.Lifecycle).Process(roster, 1);
        Assert.Equal(1, s.Actor.TemporalAccumulation.StackCount);
        Assert.Equal(1, TemporalAccumulation.GetMagicalDamageBonus(s.Actor));
    }

    [Fact]
    public void MagicalExchange_UsesOnePreexistingBonusSnapshotForOpeningAndFollowUp()
    {
        var s = new Setup();
        TemporalAccumulation.Process(s.Actor, "known-fire", "fire", s.Strike());
        var resolver = new CombatExchangeResolver(new(new(s.Rolls)));
        var request = new CombatExchangeRequest(s.Actor, s.Target, AttackType.Magical, 4,
            WeaponCategory.MagicScroll, WeaponCategory.Axe, 100, AttackType.Physical, 0, 100,
            1, 1, 1, true, false, IncomingAttackPermitsFollowUps: true);
        var result = resolver.Resolve(request);
        Assert.Equal(14, result.OpeningStrike.Damage);
        Assert.Equal(14, result.CounterStrike!.Damage); // Nigel lost 14/52 HP: +2 Fury on his physical counter.
        Assert.Equal(7, result.FollowUpStrike!.Damage);
        Assert.Equal(1, s.Actor.TemporalAccumulation.StackCount); // No element metadata was supplied for this exchange.
        Assert.Equal(4, s.Rolls.Calls); // One setup strike and the three existing exchange strikes.
    }

    [Fact]
    public void StoredDelayedDamage_IsNotRecalculatedAfterNewStacksResistanceChangesOrCasterDeath()
    {
        var s = new Setup();
        var storedStrike = s.Strike();
        var scheduler = new DelayedAttackScheduler();
        var stored = scheduler.Schedule(DelayedAttack.CreateTemporalIgnition("stored", s.Actor.Id, s.Target.Id, 2, storedStrike));
        TemporalAccumulation.Process(s.Actor, "stored", "fire", storedStrike);
        TemporalAccumulation.Process(s.Actor, "different-element", "other_element_fixture", s.Strike());
        Assert.Equal(2, s.Actor.TemporalAccumulation.StackCount);
        Assert.Equal(13, stored.Strike.Damage);
        s.Target.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("later-res", "source", s.Target.Id,
            TemporaryStat.Resistance, 10, 3, 4) });
        s.Target.SetShieldHP(6);
        s.Actor.TakeDamage(int.MaxValue);
        var lifecycle = new RoundLifecycleManager(3);
        lifecycle.AdvancePhase();
        new PoisonBatchProcessor(lifecycle).Process(new[] { s.Actor, s.Target });
        lifecycle.AdvancePhase();
        var batch = new DelayedAttackProcessor(scheduler, lifecycle).Process(new[] { s.Actor, s.Target });
        var entry = Assert.Single(batch.Entries);
        Assert.Same(storedStrike, entry.Attack.Strike);
        Assert.Equal(6, entry.ShieldHPAbsorbed);
        Assert.Equal(7, entry.HealthLost);
        Assert.Equal(2, s.Rolls.Calls);
        Assert.Equal(2, s.Actor.TemporalAccumulation.StackCount);
    }

    [Fact]
    public void TimeWeaverAbilityPreviewsAndRejections_DoNotBypassMissingRulesOrEarnStacks()
    {
        var s = new Setup();
        TemporalAccumulation.Process(s.Actor, "previous", "fire", s.Strike());
        var scheduler = new DelayedAttackScheduler();
        var ignition = new TemporalIgnitionAbilityExecutor(new(new(s.Rolls)), scheduler);
        var use = new AbilityUse(s.Actor, "temporal_ignition", s.Activations);
        var attack = new AttackConfiguration(AttackType.Magical, WeaponCategory.MagicScroll, 4, 100, 1, 2);
        var preview = ignition.Preview(use, s.Target, "next", attack, WeaponCategory.Axe, Rules, elementId: "fire");
        Assert.Equal(19, preview.ExpectedDamageOnHit); // Ordinary 13 + existing stack 1 + Ignition bonus 5.
        Assert.Null(preview.Accumulation);
        var rejected = ignition.Execute(use, s.Target, "next", attack, WeaponCategory.Axe, Rules, elementId: "fire");
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, rejected.Targeting!.RejectionReason);
        Assert.Null(rejected.Accumulation);
        Assert.Empty(scheduler.PendingAttacks);
        var transmutation = new ArcaneTransmutationAbilityExecutor().Execute(
            new AbilityUse(s.Actor, "arcane_transmutation", s.Activations),
            new(AbilityTargetType.Character, s.Target.TacticalPosition, s.Target), Rules);
        Assert.Equal(ArcaneTransmutationEffectOutcome.EquippedScrollRequired, transmutation.Effect!.Outcome);
        Assert.Equal(1, s.Actor.TemporalAccumulation.StackCount);
        Assert.Equal(1, s.Rolls.Calls);
        s.AssertNoActionOrHealthChanges();
    }

    [Fact]
    public void PhysicalDamageAndOtherStats_DoNotReceiveTheMagicalBonus()
    {
        var s = new Setup();
        var stats = s.Actor.BaseStats;
        TemporalAccumulation.Process(s.Actor, "fire", "fire", s.Strike());
        Assert.Equal(1, DamageCalculator.CalculatePhysicalDamage(stats, s.Target.BaseStats, 4));
        Assert.Same(stats, s.Actor.BaseStats);
        Assert.Equal(stats.Defense, s.Actor.EffectiveDefense);
        Assert.Equal(stats.Resistance, s.Actor.EffectiveResistance);
        Assert.Equal(stats.Speed, s.Actor.EffectiveSpeed);
        Assert.Equal(stats.Movement, s.Actor.EffectiveMovement);
        Assert.Empty(s.Actor.TemporaryModifiers.Active);
    }

    [Theory]
    [InlineData("event")]
    [InlineData("element")]
    [InlineData("nullOwner")]
    [InlineData("contradictory")]
    public void InvalidEventInputs_AreRejectedBeforeProgressChanges(string invalid)
    {
        var s = new Setup();
        var strike = s.Strike();
        Assert.ThrowsAny<ArgumentException>(() => TemporalAccumulation.Process(invalid == "nullOwner" ? null! : s.Actor,
            invalid == "event" ? " " : "event", invalid == "element" ? " " : "fire", strike, interrupted: invalid == "contradictory"));
        Assert.Equal(0, s.Actor.TemporalAccumulation.StackCount);
        Assert.Empty(s.Actor.TemporalAccumulation.KnownElements);
        Assert.Equal(TemporalAccumulationOutcome.StackEarned, TemporalAccumulation.Process(s.Actor, "event", "fire", strike).Outcome);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void InvalidOrOverflowingFlatBonus_IsRejectedBeforeRandomness(int bonus)
    {
        var s = new Setup();
        if (bonus < 0)
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Resolver.ResolveMagicalStrike(s.Actor.BaseStats, s.Target.BaseStats,
                4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 100, magicalDamageBonus: bonus));
        else
            Assert.Throws<OverflowException>(() => s.Resolver.ResolveMagicalStrike(s.Actor.BaseStats, s.Target.BaseStats,
                4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 100, magicalDamageBonus: bonus));
        Assert.Equal(0, s.Rolls.Calls);
        s.AssertNoActionOrHealthChanges();
    }

    [Fact]
    public void ResultAndCollectionSnapshots_AreImmutableAndNoExtraRandomRollIsConsumed()
    {
        var s = new Setup();
        var first = TemporalAccumulation.Process(s.Actor, "fire", "fire", s.Strike());
        Assert.Equal(1, s.Rolls.Calls);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)first.KnownElements).Clear());
        var progress = s.Actor.TemporalAccumulation.KnownElements;
        TemporalAccumulation.Process(s.Actor, "second", "other_element_fixture", s.Strike());
        Assert.Equal(new[] { "fire" }, progress);
        Assert.Equal(new[] { "fire" }, first.KnownElements);
        Assert.Equal(1, first.ResultingStacks);
        Assert.All(typeof(TemporalAccumulationResult).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.All(typeof(TemporalAccumulationProgress).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    private sealed class Rolls : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }

    private sealed class Setup
    {
        public Character Actor { get; } = CharacterFactory.Create("time_weaver", 1)!;
        public Character Target { get; } = CharacterFactory.Create("nigel", 2)!;
        public RoundLifecycleManager Lifecycle { get; } = new(2);
        public ActivationManager Activations { get; }
        public Rolls Rolls { get; } = new();
        public SingleStrikeResolver Resolver { get; }

        public Setup()
        {
            Actor.SetTacticalPosition(new(5, 5));
            Target.SetTacticalPosition(new(6, 5));
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Activations = new(new[] { Actor }, new[] { Target }, 1, Lifecycle);
            Activations.StartActivation(Actor);
            Resolver = new(new(Rolls));
        }

        public StrikeResult Strike(int power = 4, int accuracy = 100) => Resolver.ResolveMagicalStrike(Actor.BaseStats,
            Target.BaseStats, power, WeaponCategory.MagicScroll, WeaponCategory.Axe, accuracy,
            magicalDamageBonus: TemporalAccumulation.GetMagicalDamageBonus(Actor));

        public void AssertNoActionOrHealthChanges()
        {
            Assert.Equal(38, Actor.CurrentHealth);
            Assert.Equal(52, Target.CurrentHealth);
            Assert.Equal(0, Actor.ShieldHP);
            Assert.Equal(0, Target.ShieldHP);
            Assert.Empty(Actor.Cooldowns.Cooldowns);
            Assert.False(Actor.HasActedThisRound);
            Assert.False(Activations.MainActionUsed);
            Assert.False(Activations.UtilityActionUsed);
            Assert.False(Activations.MovementUsed);
        }
    }
}
