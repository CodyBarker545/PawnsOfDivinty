using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Statuses;

public class TimedStatusBatchProcessorTests
{
    [Fact]
    public void OneStatus_DecrementsOnce_AndReportsBeforeAfterAndExpired()
    {
        var character = Unit("first");
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var lifecycle = Ready();

        var result = new TimedStatusBatchProcessor(lifecycle).Process(new[] { character });

        Assert.Equal(3, result.RoundNumber);
        var entry = Assert.Single(result.Entries);
        Assert.Equal(character.Id, entry.CharacterId);
        Assert.Equal(2, entry.StatusesBefore[StatusType.Burn]);
        Assert.Equal(1, entry.StatusesAfter[StatusType.Burn]);
        Assert.Empty(entry.ExpiredStatuses);
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(RoundPhase.TimedStatusUpdate, lifecycle.CurrentPhase);
    }

    [Fact]
    public void MultipleCharacters_PreserveRosterOrder_AndCoexistingTimersExpireIndependently()
    {
        var first = Unit("z");
        var second = Unit("a");
        var empty = Unit("empty");
        first.Statuses.ApplyTimedStatus(StatusType.Burn, 1);
        first.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        second.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        second.Statuses.ApplyTimedStatus(StatusType.Disarm, 3);

        var result = new TimedStatusBatchProcessor(Ready()).Process(new[] { first, second, empty });

        Assert.Equal(new[] { "z", "a", "empty" }, result.Entries.Select(entry => entry.CharacterId));
        Assert.Equal(new[] { StatusType.Burn }, result.Entries[0].ExpiredStatuses);
        Assert.Equal(1, result.Entries[0].StatusesAfter[StatusType.Frostbite]);
        Assert.Equal(new[] { StatusType.Silence }, result.Entries[1].ExpiredStatuses);
        Assert.Equal(2, result.Entries[1].StatusesAfter[StatusType.Disarm]);
        Assert.Empty(result.Entries[2].StatusesBefore);
        Assert.Empty(result.Entries[2].StatusesAfter);
        Assert.Empty(result.Entries[2].ExpiredStatuses);
    }

    [Fact]
    public void RoundTwoApplications_OneRoundExpiresInRoundThree_AndTwoRoundsExpireInRoundFour()
    {
        var character = Unit("first");
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1); // Applied during R2.
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var lifecycle = Ready();
        var processor = new TimedStatusBatchProcessor(lifecycle);

        var roundThree = processor.Process(new[] { character });

        Assert.Equal(new[] { StatusType.Silence }, roundThree.Entries[0].ExpiredStatuses);
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(SilenceEligibility.CanUseAbility(character, true));
        NextTimedPhase(lifecycle);
        var roundFour = processor.Process(new[] { character });
        Assert.Equal(4, roundFour.RoundNumber);
        Assert.Equal(new[] { StatusType.Burn }, roundFour.Entries[0].ExpiredStatuses);
        Assert.Empty(character.Statuses.TimedStatuses);
    }

    [Fact]
    public void Reapplication_RefreshesWithoutStacking_AndExtendsExpiration()
    {
        var character = Unit("first");
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var lifecycle = Ready();
        var processor = new TimedStatusBatchProcessor(lifecycle);
        processor.Process(new[] { character });
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        Assert.Equal(7, character.EffectiveDefense);
        NextTimedPhase(lifecycle);

        var refreshed = processor.Process(new[] { character });

        Assert.Equal(2, refreshed.Entries[0].StatusesBefore[StatusType.Burn]);
        Assert.Equal(1, refreshed.Entries[0].StatusesAfter[StatusType.Burn]);
        Assert.Empty(refreshed.Entries[0].ExpiredStatuses);
        Assert.Equal(7, character.EffectiveDefense);
        NextTimedPhase(lifecycle);
        Assert.Equal(new[] { StatusType.Burn }, processor.Process(new[] { character }).Entries[0].ExpiredStatuses);
        Assert.Equal(10, character.EffectiveDefense);
    }

    [Fact]
    public void Expiration_RestoresEffectiveStatsAndSpellWeaponEligibility()
    {
        var character = Unit("first");
        character.ConfigureParalysisSpeedReduction(4); // Explicit prototype example, not finalized balance.
        foreach (var status in Enum.GetValues<StatusType>().Where(status => status != StatusType.Poison).Reverse())
            character.Statuses.ApplyTimedStatus(status, 1); // Each status is in its final remaining round.
        Assert.Equal(7, character.EffectiveDefense);
        Assert.Equal(3, character.EffectiveMovement);
        Assert.Equal(6, character.EffectiveSpeed);
        Assert.False(SilenceEligibility.CanUseAbility(character, true));
        Assert.False(DisarmEligibility.CanUseEquippedWeapon(character));

        var result = new TimedStatusBatchProcessor(Ready()).Process(new[] { character });

        Assert.Equal(Enum.GetValues<StatusType>().Where(status => status != StatusType.Poison), result.Entries[0].ExpiredStatuses);
        Assert.Equal(character.BaseStats.Defense, character.EffectiveDefense);
        Assert.Equal(character.BaseStats.Movement, character.EffectiveMovement);
        Assert.Equal(character.BaseStats.Speed, character.EffectiveSpeed);
        Assert.True(SilenceEligibility.CanUseAbility(character, true));
        Assert.True(DisarmEligibility.CanUseEquippedWeapon(character));
    }

    [Fact]
    public void PoisonStateAndEscalation_AreUnaffectedByTimedProcessing()
    {
        var character = Unit("first");
        character.Statuses.ApplyPoison();
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        PoisonProcessor.Process(character);
        var lifecycle = Ready(new[] { character }); // Poison phase performs its own next tick before this snapshot.
        int ticks = character.Statuses.PoisonTicksOccurred;
        int health = character.CurrentHealth;

        var result = new TimedStatusBatchProcessor(lifecycle).Process(new[] { character });

        Assert.Equal(2, ticks);
        Assert.True(character.Statuses.IsPoisoned);
        Assert.Equal(ticks, character.Statuses.PoisonTicksOccurred);
        Assert.Equal(health, character.CurrentHealth);
        Assert.False(result.Entries[0].StatusesBefore.ContainsKey(StatusType.Poison));
        Assert.False(result.Entries[0].StatusesAfter.ContainsKey(StatusType.Poison));
        Assert.DoesNotContain(StatusType.Poison, result.Entries[0].ExpiredStatuses);
    }

    [Fact]
    public void PoisonDefeat_ClearsPersonalNegativeStatusesBeforeTimedPhase()
    {
        var character = Unit("dead");
        character.TakeDamage(99);
        foreach (var status in Enum.GetValues<StatusType>().Where(status => status != StatusType.Poison))
            character.Statuses.ApplyTimedStatus(status, 2);
        character.Statuses.ApplyPoison();
        var lifecycle = Ready(new[] { character });
        Assert.True(character.IsDefeated);
        Assert.Empty(character.Statuses.TimedStatuses);
        Assert.False(character.Statuses.IsPoisoned);

        var result = new TimedStatusBatchProcessor(lifecycle).Process(new[] { character });

        var entry = Assert.Single(result.Entries);
        Assert.Equal("dead", entry.CharacterId);
        Assert.Empty(entry.StatusesBefore);
        Assert.Empty(entry.StatusesAfter);
        Assert.Empty(entry.ExpiredStatuses); // Death cleanup is not misreported as duration expiration.
        Assert.Equal(0, character.CurrentHealth);
        Assert.False(character.CanAct);
    }

    [Theory]
    [InlineData(RoundPhase.Shop)]
    [InlineData(RoundPhase.PoisonBatch)]
    [InlineData(RoundPhase.DelayedAttacks)]
    [InlineData(RoundPhase.OtherRoundStartTriggers)]
    [InlineData(RoundPhase.Preparation)]
    [InlineData(RoundPhase.AlternatingActivations)]
    [InlineData(RoundPhase.Cleanup)]
    [InlineData(RoundPhase.ObjectiveScoring)]
    public void WrongPhase_IsRejectedWithoutChangingTimers(RoundPhase phase)
    {
        var lifecycle = new RoundLifecycleManager(3);
        while (lifecycle.CurrentPhase != phase) lifecycle.AdvancePhase();
        var character = Unit("first");
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);

        Assert.Throws<InvalidOperationException>(() => new TimedStatusBatchProcessor(lifecycle).Process(new[] { character }));

        Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Burn]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingPhasePrerequisites_AreRejected(bool completedPoison)
    {
        var lifecycle = new RoundLifecycleManager(3);
        lifecycle.AdvancePhase();
        if (completedPoison) new PoisonBatchProcessor(lifecycle).Process(Array.Empty<Character>());
        lifecycle.AdvancePhase();
        lifecycle.AdvancePhase(); // No delayed phase execution.
        var character = Unit("first");
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1);

        var error = Assert.Throws<InvalidOperationException>(() => new TimedStatusBatchProcessor(lifecycle).Process(new[] { character }));

        Assert.Contains(completedPoison ? "delayed" : "Poison", error.Message);
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Silence]);
    }

    [Theory]
    [InlineData("nullRoster")]
    [InlineData("nullMember")]
    [InlineData("duplicateReference")]
    [InlineData("duplicateId")]
    public void InvalidParticipants_DoNotMutateTimersOrConsumeGuard(string invalid)
    {
        var first = Unit("first");
        first.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        IEnumerable<Character> roster = invalid switch
        {
            "nullRoster" => null!,
            "nullMember" => new[] { first, null! },
            "duplicateReference" => new[] { first, first },
            "duplicateId" => new[] { first, Unit("first") },
            _ => throw new ArgumentException()
        };
        var processor = new TimedStatusBatchProcessor(Ready());

        Assert.ThrowsAny<ArgumentException>(() => processor.Process(roster));

        Assert.Equal(2, first.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(1, processor.Process(new[] { first }).Entries[0].StatusesAfter[StatusType.Burn]);
    }

    [Fact]
    public void DuplicateExecution_IsRejectedAcrossProcessors_AndNextRoundIsAvailable()
    {
        var character = Unit("first");
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var lifecycle = Ready();
        var processor = new TimedStatusBatchProcessor(lifecycle);
        processor.Process(new[] { character });

        Assert.Throws<InvalidOperationException>(() => processor.Process(new[] { character }));
        Assert.Throws<InvalidOperationException>(() => new TimedStatusBatchProcessor(lifecycle).Process(new[] { character }));
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Burn]);
        NextTimedPhase(lifecycle);
        Assert.Equal(new[] { StatusType.Burn }, processor.Process(new[] { character }).Entries[0].ExpiredStatuses);
    }

    [Fact]
    public void EmptyBatch_CountsAsExecuted()
    {
        var lifecycle = Ready();
        var processor = new TimedStatusBatchProcessor(lifecycle);

        Assert.Empty(processor.Process(Array.Empty<Character>()).Entries);
        Assert.Throws<InvalidOperationException>(() => new TimedStatusBatchProcessor(lifecycle).Process(Array.Empty<Character>()));
        NextTimedPhase(lifecycle);
        Assert.Empty(processor.Process(Array.Empty<Character>()).Entries);
    }

    [Fact]
    public void ResultCollections_AreImmutableSnapshots_OfBeforeAfterAndExpiration()
    {
        var character = Unit("first");
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        var result = new TimedStatusBatchProcessor(Ready()).Process(new[] { character });
        var entry = Assert.Single(result.Entries);
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 9);
        character.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);

        Assert.Equal(2, entry.StatusesBefore[StatusType.Burn]);
        Assert.Equal(1, entry.StatusesAfter[StatusType.Burn]);
        Assert.Equal(new[] { StatusType.Silence }, entry.ExpiredStatuses);
        Assert.False(entry.StatusesAfter.ContainsKey(StatusType.Disarm));
        foreach (var snapshot in new[] { entry.StatusesBefore, entry.StatusesAfter })
        {
            var dictionary = Assert.IsAssignableFrom<IDictionary<StatusType, int>>(snapshot);
            Assert.Throws<NotSupportedException>(() => dictionary[StatusType.Burn] = 99);
            Assert.Throws<NotSupportedException>(() => dictionary.Clear());
        }
        Assert.Throws<NotSupportedException>(() => ((IList<TimedStatusUpdate>)result.Entries).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<StatusType>)entry.ExpiredStatuses).Add(StatusType.Disarm));
        Assert.All(typeof(TimedStatusUpdate).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(TimedStatusBatchResult).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void Processing_PreservesHealthShieldHealingHistoryActivationStatsAndOtherInstances()
    {
        var first = CharacterFactory.Create("nigel", 1)!;
        var second = CharacterFactory.Create("nigel", 2)!;
        Assert.Same(first.BaseStats, second.BaseStats);
        first.TakeDamage(5);
        HealingApplicator.Apply(1, first);
        first.SetShieldHP(6);
        first.EndActivation();
        first.Statuses.ApplyTimedStatus(StatusType.Burn, 1);
        second.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var stats = first.BaseStats;
        var snapshot = stats with { };

        new TimedStatusBatchProcessor(Ready()).Process(new[] { first });

        Assert.Equal(48, first.CurrentHealth);
        Assert.Equal(6, first.ShieldHP);
        Assert.Equal(1, first.HealsReceivedThisRound);
        Assert.True(first.HasActedThisRound);
        Assert.Same(stats, first.BaseStats);
        Assert.Equal(snapshot, first.BaseStats);
        Assert.Equal(2, second.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(52, second.CurrentHealth);
        Assert.False(second.HasActedThisRound);
    }

    [Fact]
    public void EntireInput_IsEnumeratedBeforeMutatingTimers()
    {
        var character = Unit("first");
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        IEnumerable<Character> BrokenInput()
        {
            yield return character;
            Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Burn]);
            throw new InvalidOperationException("Input enumeration failed.");
        }
        var processor = new TimedStatusBatchProcessor(Ready());

        Assert.Throws<InvalidOperationException>(() => processor.Process(BrokenInput()));
        Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Single(processor.Process(new[] { character }).Entries);
    }

    [Fact]
    public void LargeDuration_DecrementsWithoutOverflow()
    {
        var character = Unit("first");
        character.Statuses.ApplyTimedStatus(StatusType.Burn, int.MaxValue);

        var result = new TimedStatusBatchProcessor(Ready()).Process(new[] { character });

        Assert.Equal(int.MaxValue - 1, result.Entries[0].StatusesAfter[StatusType.Burn]);
    }

    [Fact]
    public void NullLifecycle_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => new TimedStatusBatchProcessor(null!));

    private static Character Unit(string id) => new(id, id, 1, new CharacterStats(100, 20, 10, 20, 8, 4, 10, 5));
    private static RoundLifecycleManager Ready(IEnumerable<Character>? roster = null)
    {
        var lifecycle = new RoundLifecycleManager(3);
        EnterTimedPhase(lifecycle, roster);
        return lifecycle;
    }
    private static void EnterTimedPhase(RoundLifecycleManager lifecycle, IEnumerable<Character>? roster = null)
    {
        var participants = roster?.ToArray() ?? Array.Empty<Character>();
        lifecycle.AdvancePhase();
        new PoisonBatchProcessor(lifecycle).Process(participants);
        lifecycle.AdvancePhase();
        new DelayedAttackProcessor(new DelayedAttackScheduler(), lifecycle).Process(participants);
        lifecycle.AdvancePhase();
    }
    private static void NextTimedPhase(RoundLifecycleManager lifecycle)
    {
        while (lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) lifecycle.AdvancePhase();
        lifecycle.StartNextRound();
        EnterTimedPhase(lifecycle);
    }
}
