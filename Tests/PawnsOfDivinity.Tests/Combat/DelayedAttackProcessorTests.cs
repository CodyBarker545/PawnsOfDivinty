using System.Reflection;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class DelayedAttackProcessorTests
{
    [Fact]
    public void DueAndOverdueAttacks_ResolveOldestFirst_AndLeaveLivingFutureTargetsPending()
    {
        var target = Unit("target");
        target.SetShieldHP(6);
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("oldest", "target", 2, Strike(4)));
        scheduler.Schedule(Attack("future", "target", 4));
        scheduler.Schedule(Attack("next", "target", 3, Strike(5)));
        var lifecycle = Ready();

        var result = new DelayedAttackProcessor(scheduler, lifecycle).Process(new[] { target });

        Assert.Equal(3, result.RoundNumber);
        Assert.Equal(new[] { "oldest", "next" }, result.ProcessedAttackIds);
        Assert.Equal(new[] { 4, 2 }, result.Entries.Select(entry => entry.ShieldHPAbsorbed));
        Assert.Equal(new[] { 0, 3 }, result.Entries.Select(entry => entry.HealthLost));
        Assert.Equal(new[] { 4, 5 }, result.Entries.Select(entry => entry.Attack.Strike.Damage));
        Assert.Equal(97, target.CurrentHealth);
        Assert.Equal(1, result.RemainingAttackCount);
        Assert.Equal("future", Assert.Single(scheduler.PendingAttacks).Id);
        Assert.Empty(result.DefeatedRecipientIds);
        Assert.Empty(result.CancelledAttackIds);
        Assert.Equal(RoundPhase.DelayedAttacks, lifecycle.CurrentPhase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StoredOutcome_UsesCurrentShields_WithoutRerollOrResistanceRecalculation(bool hit)
    {
        var source = Unit("source");
        var originalTarget = Unit("target");
        var rolls = new CountingRoll(hit ? 0 : 99);
        var strike = new SingleStrikeResolver(new HitRoller(rolls)).ResolveMagicalStrike(
            source.BaseStats, originalTarget.BaseStats, 4, WeaponCategory.MagicScroll, WeaponCategory.Bow, 80);
        Assert.Equal(hit ? 16 : 0, strike.Damage);
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("cast", "target", 3, strike));
        // Represent later recipient stats with the same instance ID. The snapshot stores no Character reference.
        var laterTarget = new Character("target", "Target", 2, new CharacterStats(100, 20, 10, 12, 99, 4, 10, 5));
        laterTarget.SetShieldHP(6);

        var result = new DelayedAttackProcessor(scheduler, Ready()).Process(new[] { source, laterTarget });

        var entry = Assert.Single(result.Entries);
        Assert.Same(strike, entry.Attack.Strike);
        Assert.Equal(hit ? 6 : 0, entry.ShieldHPAbsorbed);
        Assert.Equal(hit ? 10 : 0, entry.HealthLost);
        Assert.Equal(hit ? 90 : 100, laterTarget.CurrentHealth);
        Assert.Equal(1, rolls.Calls);
        Assert.Empty(scheduler.PendingAttacks);
    }

    [Fact]
    public void LethalAttack_CancelsLaterDueAndFutureAttacksAgainstTarget_WithoutSkippingOthers()
    {
        var target = Unit("target");
        target.TakeDamage(95);
        var other = Unit("other");
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("lethal", "target"));
        scheduler.Schedule(Attack("cancel-due", "target"));
        scheduler.Schedule(Attack("other", "other"));
        scheduler.Schedule(Attack("cancel-future", "target", 4));

        var result = new DelayedAttackProcessor(scheduler, Ready()).Process(new[] { target, other });

        Assert.Equal(new[] { "lethal", "cancel-due", "other" }, result.ProcessedAttackIds);
        Assert.Equal(new[] { "target" }, result.DefeatedRecipientIds);
        Assert.Equal(new[] { "cancel-due", "cancel-future" }, result.CancelledAttackIds);
        Assert.Equal(13, result.Entries[0].Attack.Strike.Damage);
        Assert.Equal(5, result.Entries[0].HealthLost);
        Assert.True(result.Entries[1].SkippedDefeatedTarget);
        Assert.Equal(0, result.Entries[1].HealthLost);
        Assert.Equal(87, other.CurrentHealth);
        Assert.Empty(scheduler.PendingAttacks);
    }

    [Fact]
    public void PoisonDefeatedTarget_HasAllQueuedAttacksCancelled()
    {
        var target = Unit("target");
        target.TakeDamage(99);
        target.Statuses.ApplyPoison();
        var lifecycle = new RoundLifecycleManager(3);
        lifecycle.AdvancePhase();
        new PoisonBatchProcessor(lifecycle).Process(new[] { target });
        lifecycle.AdvancePhase();
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("due", "target"));
        scheduler.Schedule(Attack("future", "target", 5));

        var result = new DelayedAttackProcessor(scheduler, lifecycle).Process(new[] { target });

        Assert.True(Assert.Single(result.Entries).SkippedDefeatedTarget);
        Assert.Equal(new[] { "due", "future" }, result.CancelledAttackIds);
        Assert.Empty(result.DefeatedRecipientIds); // This processor did not cause the earlier Poison death.
        Assert.Equal(0, result.RemainingAttackCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CasterDeathOrAbsence_DoesNotCancelStoredAttack(bool includeDeadCaster)
    {
        var target = Unit("target");
        var source = Unit("source");
        source.TakeDamage(int.MaxValue);
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("cast", "target"));

        var result = new DelayedAttackProcessor(scheduler, Ready()).Process(
            includeDeadCaster ? new[] { source, target } : new[] { target });

        Assert.Equal(13, Assert.Single(result.Entries).HealthLost);
        Assert.Equal(87, target.CurrentHealth);
    }

    [Theory]
    [InlineData("missingTarget")]
    [InlineData("duplicateId")]
    [InlineData("duplicateInstance")]
    [InlineData("nullMember")]
    [InlineData("nullRoster")]
    public void InvalidBatch_RejectsBeforeDamageConsumptionOrExecutionGuard(string invalid)
    {
        var target = Unit("target");
        target.SetShieldHP(6);
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("first", "target"));
        scheduler.Schedule(Attack("later", "missing"));
        var processor = new DelayedAttackProcessor(scheduler, Ready());
        IEnumerable<Character> roster = invalid switch
        {
            "missingTarget" => new[] { target },
            "duplicateId" => new[] { target, Unit("target") },
            "duplicateInstance" => new[] { target, target },
            "nullMember" => new[] { target, null! },
            "nullRoster" => null!,
            _ => throw new ArgumentException()
        };

        Assert.ThrowsAny<ArgumentException>(() => processor.Process(roster));

        Assert.Equal(100, target.CurrentHealth);
        Assert.Equal(6, target.ShieldHP);
        Assert.Equal(2, scheduler.PendingAttacks.Count);
        Assert.Equal(2, processor.Process(new[] { target, Unit("missing") }).Entries.Count);
    }

    [Fact]
    public void UnknownFutureTarget_IsNotRequiredUntilDue()
    {
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("future", "not-in-roster", 4));

        var result = new DelayedAttackProcessor(scheduler, Ready()).Process(Array.Empty<Character>());

        Assert.Empty(result.Entries);
        Assert.Equal(1, result.RemainingAttackCount);
    }

    [Fact]
    public void InvalidStoredData_IsRejectedBeforeEarlierValidAttackMutatesState()
    {
        var target = Unit("target");
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("valid", "target"));
        var malformed = Strike();
        scheduler.Schedule(Attack("invalid", "target", 3, malformed));
        // Simulate corrupt stored data without exposing mutation in production APIs.
        typeof(StrikeResult).GetField("<Damage>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(malformed, -1);

        Assert.ThrowsAny<ArgumentException>(() => new DelayedAttackProcessor(scheduler, Ready()).Process(new[] { target }));

        Assert.Equal(100, target.CurrentHealth);
        Assert.Equal(2, scheduler.PendingAttacks.Count);
    }

    [Theory]
    [InlineData(RoundPhase.Shop)]
    [InlineData(RoundPhase.PoisonBatch)]
    [InlineData(RoundPhase.TimedStatusUpdate)]
    [InlineData(RoundPhase.OtherRoundStartTriggers)]
    [InlineData(RoundPhase.Preparation)]
    [InlineData(RoundPhase.AlternatingActivations)]
    [InlineData(RoundPhase.Cleanup)]
    [InlineData(RoundPhase.ObjectiveScoring)]
    public void WrongPhase_IsRejectedWithoutDamageOrConsumption(RoundPhase phase)
    {
        var lifecycle = new RoundLifecycleManager(3);
        while (lifecycle.CurrentPhase != phase) lifecycle.AdvancePhase();
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("cast", "target"));
        var target = Unit("target");

        Assert.Throws<InvalidOperationException>(() => new DelayedAttackProcessor(scheduler, lifecycle).Process(new[] { target }));

        Assert.Equal(100, target.CurrentHealth);
        Assert.Single(scheduler.PendingAttacks);
    }

    [Fact]
    public void PoisonMustBeProcessedBeforeDelayedPhaseExecution()
    {
        var lifecycle = new RoundLifecycleManager(3);
        lifecycle.AdvancePhase();
        lifecycle.AdvancePhase();

        Assert.Throws<InvalidOperationException>(() => new DelayedAttackProcessor(new DelayedAttackScheduler(), lifecycle)
            .Process(Array.Empty<Character>()));
    }

    [Fact]
    public void DuplicatePhaseExecution_IsRejectedAcrossProcessorInstances_AndNextRoundIsAllowed()
    {
        var lifecycle = Ready();
        var scheduler = new DelayedAttackScheduler();
        var target = Unit("target");
        scheduler.Schedule(Attack("first", "target"));
        scheduler.Schedule(Attack("next-round", "target", 4));
        var processor = new DelayedAttackProcessor(scheduler, lifecycle);
        processor.Process(new[] { target });

        Assert.Throws<InvalidOperationException>(() => processor.Process(new[] { target }));
        Assert.Throws<InvalidOperationException>(() => new DelayedAttackProcessor(scheduler, lifecycle).Process(new[] { target }));
        Assert.Equal(87, target.CurrentHealth);
        while (lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) lifecycle.AdvancePhase();
        lifecycle.StartNextRound();
        lifecycle.AdvancePhase();
        new PoisonBatchProcessor(lifecycle).Process(Array.Empty<Character>());
        lifecycle.AdvancePhase();

        Assert.Single(processor.Process(new[] { target }).Entries);
        Assert.Equal(74, target.CurrentHealth);
    }

    [Fact]
    public void Processing_PreservesActivationStatsHealingHistoryAndStatusTimers_AndReturnsImmutableSnapshots()
    {
        var target = Unit("target");
        target.TakeDamage(5);
        HealingApplicator.Apply(1, target);
        target.EndActivation();
        target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var stats = target.BaseStats;
        var snapshot = stats with { };
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("cast", "target"));

        var result = new DelayedAttackProcessor(scheduler, Ready()).Process(new[] { target });

        Assert.Equal(83, target.CurrentHealth);
        Assert.True(target.HasActedThisRound);
        Assert.Equal(1, target.HealsReceivedThisRound);
        Assert.Equal(2, target.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Same(stats, target.BaseStats);
        Assert.Equal(snapshot, target.BaseStats);
        target.TakeDamage(5);
        Assert.Equal(83, result.Entries[0].RemainingHealth);
        Assert.All(typeof(DelayedAttackBatchResult).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(DelayedAttackResolution).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Throws<NotSupportedException>(() => ((IList<DelayedAttackResolution>)result.Entries).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.ProcessedAttackIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.DefeatedRecipientIds).Add("fake"));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.CancelledAttackIds).Clear());
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new DelayedAttackProcessor(null!, Ready()));
        Assert.Throws<ArgumentNullException>(() => new DelayedAttackProcessor(new DelayedAttackScheduler(), null!));
    }

    private static Character Unit(string id) => new(id, id, 1, new CharacterStats(100, 20, 10, 20, 8, 4, 10, 5));
    private static StrikeResult Strike(int damage = 13)
    {
        var attacker = new CharacterStats(100, damage + 10, 10, 20, 8, 4, 10, 5);
        return new SingleStrikeResolver(new HitRoller(new CountingRoll(0))).ResolvePhysicalStrike(
            attacker, Unit("target").BaseStats, 0, WeaponCategory.Bow, WeaponCategory.Bow, 100);
    }
    private static DelayedAttack Attack(string id, string target, int due = 3, StrikeResult? strike = null)
        => new(id, "source", target, 1, due, strike ?? Strike());
    private static RoundLifecycleManager Ready()
    {
        var lifecycle = new RoundLifecycleManager(3);
        lifecycle.AdvancePhase();
        new PoisonBatchProcessor(lifecycle).Process(Array.Empty<Character>());
        lifecycle.AdvancePhase();
        return lifecycle;
    }
    private sealed class CountingRoll : IRandomRollProvider
    {
        private readonly int _value;
        public CountingRoll(int value) { _value = value; }
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return _value; }
    }
}
