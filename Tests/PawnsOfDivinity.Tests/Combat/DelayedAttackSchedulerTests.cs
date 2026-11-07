using System.Reflection;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class DelayedAttackSchedulerTests
{
    [Fact]
    public void Schedule_StoresValidatedSnapshot_AndAssignsSequenceWithoutMutatingInput()
    {
        var attack = Attack("first");
        var scheduler = new DelayedAttackScheduler();

        var scheduled = scheduler.Schedule(attack);

        Assert.Single(scheduler.PendingAttacks);
        Assert.Equal("first", scheduled.Id);
        Assert.Equal("source", scheduled.SourceCharacterId);
        Assert.Equal("target", scheduled.TargetCharacterId);
        Assert.Equal(2, scheduled.ScheduledRound);
        Assert.Equal(3, scheduled.ResolutionRound);
        Assert.Equal(0L, attack.InsertionSequence);
        Assert.Equal(1L, scheduled.InsertionSequence);
        Assert.Same(attack.Strike, scheduled.Strike);
        Assert.Same(scheduled, scheduler.PendingAttacks[0]);
    }

    [Fact]
    public void DueAttacks_UseInsertionOrder_NotIdsResolutionRoundsOrSchedulingRoundValues()
    {
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("z", 2, 4));
        scheduler.Schedule(Attack("future", 2, 9));
        scheduler.Schedule(Attack("a", 1, 3));
        scheduler.Schedule(Attack("same-round", 2, 4));

        var due = scheduler.GetDueAttacks(4);

        Assert.Equal(new[] { "z", "a", "same-round" }, due.Select(attack => attack.Id));
        Assert.Equal(new long[] { 1, 3, 4 }, due.Select(attack => attack.InsertionSequence));
        Assert.Equal(new[] { "z", "future", "a", "same-round" }, scheduler.PendingAttacks.Select(attack => attack.Id));
        Assert.Equal(4, scheduler.PendingAttacks.Count); // Identifying due attacks is not consumption.
        Assert.Equal(due, scheduler.GetDueAttacks(4));
    }

    [Fact]
    public void FutureAttacks_RemainPending_AndBecomeDueWithoutReordering()
    {
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("first", 2, 3));
        scheduler.Schedule(Attack("second", 2, 5));

        Assert.Empty(scheduler.GetDueAttacks(2));
        Assert.Equal(new[] { "first" }, scheduler.GetDueAttacks(3).Select(attack => attack.Id));
        Assert.Equal(new[] { "first", "second" }, scheduler.GetDueAttacks(5).Select(attack => attack.Id));
        Assert.Equal(2, scheduler.PendingAttacks.Count);
    }

    [Fact]
    public void DuplicateId_IsRejectedWithoutChangingQueueOrConsumingASequence()
    {
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("duplicate"));

        Assert.Throws<ArgumentException>(() => scheduler.Schedule(Attack("duplicate", 3, 4)));

        Assert.Single(scheduler.PendingAttacks);
        Assert.Equal(2L, scheduler.Schedule(Attack("next")).InsertionSequence);
    }

    [Theory]
    [InlineData("id", null)]
    [InlineData("id", "")]
    [InlineData("id", "  ")]
    [InlineData("source", null)]
    [InlineData("source", "")]
    [InlineData("source", "\t")]
    [InlineData("target", null)]
    [InlineData("target", "")]
    [InlineData("target", "\r\n")]
    public void InvalidIds_AreRejected(string field, string? invalid)
        => Assert.Throws<ArgumentException>(() => new DelayedAttack(
            field == "id" ? invalid! : "id", field == "source" ? invalid! : "source",
            field == "target" ? invalid! : "target", 2, 3, Strike()));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(int.MinValue, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(3, 2)]
    public void InvalidRounds_AreRejected(int scheduledRound, int resolutionRound)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Attack("invalid", scheduledRound, resolutionRound));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(2, 7)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void GeneralSnapshots_AcceptExplicitValidResolutionRounds(int scheduledRound, int resolutionRound)
    {
        var attack = Attack("valid", scheduledRound, resolutionRound);

        Assert.Equal(scheduledRound, attack.ScheduledRound);
        Assert.Equal(resolutionRound, attack.ResolutionRound);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(4, 5)]
    [InlineData(int.MaxValue - 1, int.MaxValue)]
    public void TemporalIgnitionSnapshot_UsesConfirmedFollowingRoundDelay(int scheduledRound, int expected)
    {
        var strike = Strike();

        var attack = DelayedAttack.CreateTemporalIgnition("ignition", "source", "target", scheduledRound, strike);

        Assert.Equal(expected, attack.ResolutionRound);
        Assert.Same(strike, attack.Strike); // Timing helper neither rolls nor adds an ability damage bonus.
    }

    [Fact]
    public void TemporalIgnitionDelay_RejectsOverflow()
        => Assert.Throws<OverflowException>(() => DelayedAttack.CreateTemporalIgnition(
            "ignition", "source", "target", int.MaxValue, Strike()));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TemporalIgnitionDelay_RejectsInvalidSchedulingRound(int round)
        => Assert.Throws<ArgumentOutOfRangeException>(() => DelayedAttack.CreateTemporalIgnition(
            "ignition", "source", "target", round, Strike()));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void DueQueries_RejectInvalidRound(int round)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedAttackScheduler().GetDueAttacks(round));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StoredOutcomes_PreserveHitMissAndDamage_WithoutMoreRandomness(bool hit)
    {
        var provider = new CountingRoll(hit ? 0 : 99);
        var attacker = CharacterFactory.Create("time_weaver", 1)!;
        var target = CharacterFactory.Create("nigel", 2)!;
        var strike = new SingleStrikeResolver(new HitRoller(provider)).ResolveMagicalStrike(
            attacker.BaseStats, target.BaseStats, 4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 80);
        var scheduler = new DelayedAttackScheduler();

        scheduler.Schedule(DelayedAttack.CreateTemporalIgnition("cast", attacker.Id, target.Id, 2, strike));
        target.SetShieldHP(6);
        target.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        target.EndActivation();
        var stored = scheduler.GetDueAttacks(3)[0];

        Assert.Equal(hit, stored.Strike.Hit);
        Assert.Equal(hit ? 13 : 0, stored.Strike.Damage);
        Assert.Same(strike, stored.Strike);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(target.BaseStats.MaxHealth, target.CurrentHealth);
        Assert.Equal(6, target.ShieldHP);
        Assert.True(target.HasActedThisRound);
        Assert.Equal(1, target.Statuses.TimedStatuses[StatusType.Silence]);
    }

    [Fact]
    public void Snapshot_RejectsNullStrikeAndNegativeStoredDamage()
    {
        Assert.Throws<ArgumentNullException>(() => new DelayedAttack("id", "source", "target", 1, 2, null!));
        // Existing calculators cannot produce negative damage. Exercise the storage boundary
        // against a malformed internal result without changing combat APIs just for this test.
        var malformed = (StrikeResult)Activator.CreateInstance(typeof(StrikeResult),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { true, 100, -1, WeaponTriangleOutcome.Neutral }, null)!;

        Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedAttack("id", "source", "target", 1, 2, malformed));
    }

    [Fact]
    public void QueueAndDueExposure_AreReadOnlySnapshots_AndAttacksHaveGetOnlyProperties()
    {
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(Attack("first"));
        var pending = scheduler.PendingAttacks;
        var due = scheduler.GetDueAttacks(3);
        scheduler.Schedule(Attack("second"));

        Assert.Single(pending);
        Assert.Single(due);
        Assert.Equal(2, scheduler.PendingAttacks.Count);
        foreach (var snapshot in new[] { pending, due })
        {
            var list = Assert.IsAssignableFrom<IList<DelayedAttack>>(snapshot);
            Assert.Throws<NotSupportedException>(() => list.Clear());
            Assert.Throws<NotSupportedException>(() => list.Add(Attack("extra")));
            Assert.Throws<NotSupportedException>(() => list[0] = Attack("replacement"));
        }
        Assert.All(typeof(DelayedAttack).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.DoesNotContain(typeof(DelayedAttack).GetProperties(), property => property.PropertyType == typeof(Character));
    }

    [Fact]
    public void DifferentSourcesAndTargets_ScheduleIndependently_WithoutCharacterStateChanges()
    {
        var source = CharacterFactory.Create("time_weaver", 1)!;
        var otherSource = CharacterFactory.Create("time_weaver", 1)!;
        var target = CharacterFactory.Create("nigel", 2)!;
        target.TakeDamage(5);
        target.SetShieldHP(6);
        target.Statuses.ApplyPoison();
        var stats = target.BaseStats;
        var scheduler = new DelayedAttackScheduler();
        scheduler.Schedule(new DelayedAttack("a", source.Id, target.Id, 2, 3, Strike()));
        scheduler.Schedule(new DelayedAttack("b", otherSource.Id, source.Id, 2, 3, Strike()));

        var due = scheduler.GetDueAttacks(3);

        Assert.Equal(new[] { source.Id, otherSource.Id }, due.Select(attack => attack.SourceCharacterId));
        Assert.Equal(new[] { target.Id, source.Id }, due.Select(attack => attack.TargetCharacterId));
        Assert.Equal(47, target.CurrentHealth);
        Assert.Equal(6, target.ShieldHP);
        Assert.True(target.Statuses.IsPoisoned);
        Assert.Equal(0, target.Statuses.PoisonTicksOccurred);
        Assert.False(target.HasActedThisRound);
        Assert.Same(stats, target.BaseStats);
        Assert.Equal(source.BaseStats.MaxHealth, source.CurrentHealth);
        Assert.True(source.CanAct);
    }

    [Fact]
    public void NullSnapshot_IsRejectedWithoutQueueMutation()
    {
        var scheduler = new DelayedAttackScheduler();

        Assert.Throws<ArgumentNullException>(() => scheduler.Schedule(null!));

        Assert.Empty(scheduler.PendingAttacks);
        Assert.Equal(1L, scheduler.Schedule(Attack("first")).InsertionSequence);
    }

    private static DelayedAttack Attack(string id, int scheduledRound = 2, int resolutionRound = 3)
        => new(id, "source", "target", scheduledRound, resolutionRound, Strike());

    private static StrikeResult Strike()
    {
        var attacker = CharacterFactory.Create("time_weaver", 1)!;
        var defender = CharacterFactory.Create("nigel", 2)!;
        return new SingleStrikeResolver(new HitRoller(new CountingRoll(0))).ResolveMagicalStrike(
            attacker.BaseStats, defender.BaseStats, 4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 100);
    }

    private sealed class CountingRoll : IRandomRollProvider
    {
        private readonly int _value;
        public CountingRoll(int value) { _value = value; }
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return _value; }
    }
}
