using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Statuses;

public class PoisonBatchProcessorTests
{
    [Fact]
    public void Batch_ProcessesLivingPoisonedCharacters_InSuppliedOrder()
    {
        var first = Poisoned("z");
        var second = Poisoned("a");
        var healthy = Unit("healthy");
        var dead = Poisoned("dead");
        dead.TakeDamage(int.MaxValue);
        var lifecycle = PoisonPhase();

        var result = new PoisonBatchProcessor(lifecycle).Process(new[] { first, healthy, dead, second });

        Assert.Equal(2, result.RoundNumber);
        Assert.Equal(new[] { "z", "a" }, result.Entries.Select(entry => entry.CharacterId));
        Assert.All(result.Entries, entry =>
        {
            Assert.Equal(1, entry.Tick.TickNumber);
            Assert.Equal(2, entry.Tick.CalculatedDamage);
            Assert.Equal(2, entry.Tick.HealthLost);
            Assert.Equal(98, entry.Tick.RemainingHealth);
        });
        Assert.Empty(result.DefeatedCharacterIds);
        Assert.Equal(100, healthy.CurrentHealth);
        Assert.Equal(0, dead.CurrentHealth);
        Assert.Equal(RoundPhase.PoisonBatch, lifecycle.CurrentPhase);
    }

    [Fact]
    public void IndividualEscalationAndReapplication_PersistAcrossRounds()
    {
        var first = Poisoned("first");
        var second = Poisoned("second");
        PoisonProcessor.Process(second);
        PoisonProcessor.Process(second);
        second.Statuses.ApplyPoison();
        var lifecycle = PoisonPhase();
        var processor = new PoisonBatchProcessor(lifecycle);

        var initial = processor.Process(new[] { first, second });
        Assert.Equal(new[] { 1, 3 }, initial.Entries.Select(entry => entry.Tick.TickNumber));
        Assert.Equal(new[] { 2, 6 }, initial.Entries.Select(entry => entry.Tick.CalculatedDamage));
        NextPoisonPhase(lifecycle);
        first.Statuses.ApplyPoison();
        second.Statuses.ApplyPoison();
        var next = processor.Process(new[] { first, second });

        Assert.Equal(3, next.RoundNumber);
        Assert.Equal(new[] { 2, 4 }, next.Entries.Select(entry => entry.Tick.TickNumber));
        Assert.Equal(new[] { 4, 8 }, next.Entries.Select(entry => entry.Tick.CalculatedDamage));
    }

    [Fact]
    public void LethalTick_DoesNotSkipOtherScheduledDamage_AndReportsDefeatedIds()
    {
        var dying = Poisoned("dying");
        dying.TakeDamage(99);
        var partial = Poisoned("partial");
        partial.SetShieldHP(1);
        var shielded = Poisoned("shielded");
        shielded.SetShieldHP(6);

        var result = new PoisonBatchProcessor(PoisonPhase()).Process(new[] { dying, partial, shielded });

        Assert.Equal(new[] { "dying" }, result.DefeatedCharacterIds);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(2, result.Entries[0].Tick.CalculatedDamage);
        Assert.Equal(1, result.Entries[0].Tick.HealthLost);
        Assert.True(result.Entries[0].Tick.Defeated);
        Assert.False(dying.Statuses.IsPoisoned);
        Assert.Equal(0, dying.Statuses.PoisonTicksOccurred);
        Assert.Equal(1, result.Entries[1].Tick.ShieldHPAbsorbed);
        Assert.Equal(1, result.Entries[1].Tick.HealthLost);
        Assert.Equal(2, result.Entries[2].Tick.ShieldHPAbsorbed);
        Assert.Equal(0, result.Entries[2].Tick.HealthLost);
        Assert.Equal(4, shielded.ShieldHP);
        Assert.Equal(1, partial.Statuses.PoisonTicksOccurred);
        Assert.Equal(1, shielded.Statuses.PoisonTicksOccurred);
    }

    [Fact]
    public void GuardianDeath_LeavesAnotherRecipientsScheduledShieldAbsorptionIntact()
    {
        var guardian = CharacterFactory.Create("gentle_guardian", 1)!;
        var recipient = Poisoned("recipient");
        var shields = new SanctuaryShieldManager();
        shields.Apply(guardian, recipient);
        guardian.TakeDamage(43);
        guardian.Statuses.ApplyPoison();

        var result = new PoisonBatchProcessor(PoisonPhase()).Process(new[] { guardian, recipient });

        Assert.True(result.Entries[0].Tick.Defeated);
        Assert.Equal(2, result.Entries[1].Tick.ShieldHPAbsorbed);
        Assert.Equal(100, recipient.CurrentHealth);
        Assert.Same(recipient, shields.GetRecipient(guardian));
    }

    [Theory]
    [InlineData("nullCollection")]
    [InlineData("nullMember")]
    [InlineData("duplicateReference")]
    [InlineData("duplicateId")]
    [InlineData("duplicateUnpoisoned")]
    public void InvalidBatch_IsRejectedBeforeDamage_AndDoesNotConsumeExecution(string invalid)
    {
        var first = Poisoned("first");
        var other = Unit("other");
        IEnumerable<Character> participants = invalid switch
        {
            "nullCollection" => null!,
            "nullMember" => new[] { first, null! },
            "duplicateReference" => new[] { first, first },
            "duplicateId" => new[] { first, Poisoned("first") },
            "duplicateUnpoisoned" => new[] { first, other, other },
            _ => throw new ArgumentException()
        };
        var processor = new PoisonBatchProcessor(PoisonPhase());

        Assert.ThrowsAny<ArgumentException>(() => processor.Process(participants));

        Assert.Equal(100, first.CurrentHealth);
        Assert.Equal(0, first.Statuses.PoisonTicksOccurred);
        Assert.Single(processor.Process(new[] { first }).Entries);
    }

    [Theory]
    [InlineData(1073741823)]
    [InlineData(int.MaxValue)]
    public void OverflowInLaterParticipant_RejectsEntireBatchBeforeMutation(int completedTicks)
    {
        var first = Poisoned("first");
        first.SetShieldHP(6);
        var overflowing = Poisoned("overflow");
        // Set an unreachable-in-practice boundary without performing a billion ticks.
        typeof(StatusTracker).GetProperty(nameof(StatusTracker.PoisonTicksOccurred))!.SetValue(overflowing.Statuses, completedTicks);
        var processor = new PoisonBatchProcessor(PoisonPhase());

        Assert.Throws<OverflowException>(() => processor.Process(new[] { first, overflowing }));

        Assert.Equal(100, first.CurrentHealth);
        Assert.Equal(6, first.ShieldHP);
        Assert.Equal(0, first.Statuses.PoisonTicksOccurred);
        Assert.Equal(100, overflowing.CurrentHealth);
        Assert.Equal(completedTicks, overflowing.Statuses.PoisonTicksOccurred);
        overflowing.Statuses.ClearPoison();
        overflowing.Statuses.ApplyPoison();
        Assert.Equal(2, processor.Process(new[] { first, overflowing }).Entries.Count);
    }

    [Theory]
    [InlineData(RoundPhase.Shop)]
    [InlineData(RoundPhase.DelayedAttacks)]
    [InlineData(RoundPhase.TimedStatusUpdate)]
    [InlineData(RoundPhase.OtherRoundStartTriggers)]
    [InlineData(RoundPhase.Preparation)]
    [InlineData(RoundPhase.AlternatingActivations)]
    [InlineData(RoundPhase.Cleanup)]
    [InlineData(RoundPhase.ObjectiveScoring)]
    public void WrongPhase_IsRejectedWithoutChangingCharacterOrPhase(RoundPhase phase)
    {
        var lifecycle = new RoundLifecycleManager(2);
        while (lifecycle.CurrentPhase != phase) lifecycle.AdvancePhase();
        var character = Poisoned("first");

        Assert.Throws<InvalidOperationException>(() => new PoisonBatchProcessor(lifecycle).Process(new[] { character }));

        Assert.Equal(100, character.CurrentHealth);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
        Assert.Equal(phase, lifecycle.CurrentPhase);
    }

    [Fact]
    public void RepeatedProcessing_IsRejected_EvenThroughAnotherProcessorInstance()
    {
        var lifecycle = PoisonPhase();
        var character = Poisoned("first");
        var processor = new PoisonBatchProcessor(lifecycle);
        processor.Process(new[] { character });

        Assert.Throws<InvalidOperationException>(() => processor.Process(new[] { character }));
        Assert.Throws<InvalidOperationException>(() => new PoisonBatchProcessor(lifecycle).Process(new[] { character }));

        Assert.Equal(98, character.CurrentHealth);
        Assert.Equal(1, character.Statuses.PoisonTicksOccurred);
    }

    [Fact]
    public void EmptyBatch_StillCompletesThePhaseExecution_AndCanRunAgainNextRound()
    {
        var lifecycle = PoisonPhase();
        var processor = new PoisonBatchProcessor(lifecycle);

        var result = processor.Process(Array.Empty<Character>());

        Assert.Empty(result.Entries);
        Assert.Empty(result.DefeatedCharacterIds);
        Assert.Throws<InvalidOperationException>(() => processor.Process(Array.Empty<Character>()));
        NextPoisonPhase(lifecycle);
        Assert.Empty(processor.Process(Array.Empty<Character>()).Entries);
    }

    [Fact]
    public void InputEnumerationFinishesBeforeAnyDamage_AndEnumerationFailureDoesNotConsumePhase()
    {
        var first = Poisoned("first");
        var processor = new PoisonBatchProcessor(PoisonPhase());
        IEnumerable<Character> BrokenInput()
        {
            yield return first;
            Assert.Equal(100, first.CurrentHealth);
            throw new InvalidOperationException("Input enumeration failed.");
        }

        Assert.Throws<InvalidOperationException>(() => processor.Process(BrokenInput()));

        Assert.Equal(100, first.CurrentHealth);
        Assert.Single(processor.Process(new[] { first }).Entries);
    }

    [Fact]
    public void Results_AreImmutableSnapshots_WithStableOrderingAcrossRepeatedSetups()
    {
        var characters = new[] { Poisoned("z"), Poisoned("a") };
        var result = new PoisonBatchProcessor(PoisonPhase()).Process(characters);
        var repeated = new PoisonBatchProcessor(PoisonPhase()).Process(new[] { Poisoned("z"), Poisoned("a") });
        characters[0].TakeDamage(int.MaxValue);

        Assert.Equal(result.Entries.Select(entry => entry.CharacterId), repeated.Entries.Select(entry => entry.CharacterId));
        Assert.Equal(result.Entries.Select(entry => entry.Tick), repeated.Entries.Select(entry => entry.Tick));
        Assert.Equal(98, result.Entries[0].Tick.RemainingHealth);
        Assert.False(result.Entries[0].Tick.Defeated);
        Assert.Empty(result.DefeatedCharacterIds);
        Assert.All(typeof(PoisonBatchResult).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(PoisonBatchEntry).GetProperties(), property => Assert.Null(property.SetMethod));
        var entries = Assert.IsAssignableFrom<IList<PoisonBatchEntry>>(result.Entries);
        var defeated = Assert.IsAssignableFrom<IList<string>>(result.DefeatedCharacterIds);
        Assert.Throws<NotSupportedException>(() => entries.Clear());
        Assert.Throws<NotSupportedException>(() => defeated.Add("not-defeated"));
    }

    [Fact]
    public void Batch_DoesNotChangeActivationStatsHealingHistoryOrTimedStatuses()
    {
        var character = Poisoned("first");
        character.EndActivation();
        character.TakeDamage(5);
        HealingApplicator.Apply(1, character);
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var stats = character.BaseStats;
        var snapshot = stats with { };

        new PoisonBatchProcessor(PoisonPhase()).Process(new[] { character });

        Assert.True(character.HasActedThisRound);
        Assert.Equal(1, character.HealsReceivedThisRound);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(snapshot, character.BaseStats);
        Assert.All(character.Statuses.TimedStatuses.Values, remaining => Assert.Equal(2, remaining));
        Assert.Equal(94, character.CurrentHealth);
    }

    [Fact]
    public void NullLifecycle_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => new PoisonBatchProcessor(null!));

    private static Character Unit(string id) => new(id, id, 1, new CharacterStats(100, 20, 10, 12, 8, 4, 15, 5));
    private static Character Poisoned(string id)
    {
        var character = Unit(id);
        character.Statuses.ApplyPoison();
        return character;
    }

    private static RoundLifecycleManager PoisonPhase()
    {
        var lifecycle = new RoundLifecycleManager(2);
        lifecycle.AdvancePhase();
        return lifecycle;
    }

    private static void NextPoisonPhase(RoundLifecycleManager lifecycle)
    {
        while (lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) lifecycle.AdvancePhase();
        lifecycle.StartNextRound();
        lifecycle.AdvancePhase();
    }
}
