using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class AbilityCooldownTests
{
    [Fact]
    public void UnusedAbility_IsReady_WithoutCreatingAnEntry()
    {
        var tracker = new AbilityCooldownTracker();

        Assert.True(tracker.IsReady("smoke_bomb", 1));
        Assert.True(tracker.IsReady("future_ability", 100));
        Assert.Empty(tracker.Cooldowns);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void ReuseThree_UsedInRoundTwo_BecomesReadyExactlyInRoundFive(int round, bool expected)
    {
        var tracker = new AbilityCooldownTracker();
        tracker.RecordUse("smoke_bomb", 2, 3);

        Assert.Equal(expected, tracker.IsReady("smoke_bomb", round));
        var entry = tracker.Cooldowns["smoke_bomb"];
        Assert.Equal(2, entry.RoundUsed);
        Assert.Equal(3, entry.ReuseInterval);
        Assert.Equal(5, entry.ReadyRound);
    }

    [Fact]
    public void UseWhileCoolingDown_IsRejectedWithoutReplacingEntry_AndReuseAtDeadlineSucceeds()
    {
        var tracker = new AbilityCooldownTracker();
        tracker.RecordUse("smoke_bomb", 2, 3);
        var previous = tracker.Cooldowns["smoke_bomb"];

        Assert.Throws<InvalidOperationException>(() => tracker.RecordUse("smoke_bomb", 4, 3));
        Assert.Same(previous, tracker.Cooldowns["smoke_bomb"]);
        tracker.RecordUse("smoke_bomb", 5, 3);

        Assert.Equal(5, tracker.Cooldowns["smoke_bomb"].RoundUsed);
        Assert.Equal(8, tracker.Cooldowns["smoke_bomb"].ReadyRound);
        Assert.False(tracker.IsReady("smoke_bomb", 7));
        Assert.True(tracker.IsReady("smoke_bomb", 8));
        Assert.Equal(5, previous.ReadyRound); // The previous entry is an immutable snapshot.
    }

    [Fact]
    public void MultipleAbilities_HaveIndependentDeadlines()
    {
        var tracker = new AbilityCooldownTracker();
        tracker.RecordUse("temporal_ignition", 2, 2);
        tracker.RecordUse("smoke_bomb", 2, 3);

        Assert.True(tracker.IsReady("temporal_ignition", 4));
        Assert.False(tracker.IsReady("smoke_bomb", 4));
        tracker.RecordUse("temporal_ignition", 4, 2);
        Assert.Equal(6, tracker.Cooldowns["temporal_ignition"].ReadyRound);
        Assert.Equal(5, tracker.Cooldowns["smoke_bomb"].ReadyRound);
    }

    [Fact]
    public void SameAbilityId_OnCharactersSharingBaseStats_HasIndependentState()
    {
        var first = Create();
        var second = Create();
        Assert.Same(first.BaseStats, second.BaseStats);
        Assert.NotSame(first.Cooldowns, second.Cooldowns);
        first.Cooldowns.RecordUse("temporal_ignition", 2, 2);
        Assert.True(second.Cooldowns.IsReady("temporal_ignition", 2));
        second.Cooldowns.RecordUse("temporal_ignition", 4, 2);

        Assert.True(first.Cooldowns.IsReady("temporal_ignition", 4));
        Assert.False(second.Cooldowns.IsReady("temporal_ignition", 4));
        Assert.Equal(4, first.Cooldowns.Cooldowns["temporal_ignition"].ReadyRound);
        Assert.Equal(6, second.Cooldowns.Cooldowns["temporal_ignition"].ReadyRound);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void ZeroReuseInterval_IsImmediatelyReady_AndDoesNotImposeActionLimits(int round)
    {
        var tracker = new AbilityCooldownTracker();

        tracker.RecordUse("healing_light", round, 0);
        Assert.True(tracker.IsReady("healing_light", round));
        tracker.RecordUse("healing_light", round, 0);

        Assert.Equal(round, tracker.Cooldowns["healing_light"].ReadyRound);
        Assert.Equal(0, tracker.Cooldowns["healing_light"].ReuseInterval);
        Assert.Single(tracker.Cooldowns);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void InvalidAbilityIds_AreRejectedWithoutMutation(string? id)
    {
        var tracker = new AbilityCooldownTracker();

        Assert.Throws<ArgumentException>(() => tracker.IsReady(id!, 1));
        Assert.Throws<ArgumentException>(() => tracker.RecordUse(id!, 1, 3));

        Assert.Empty(tracker.Cooldowns);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void InvalidRoundNumbers_AreRejected(int round)
    {
        var tracker = new AbilityCooldownTracker();

        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.IsReady("ability", round));
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.RecordUse("ability", round, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityCooldown(round, 3));

        Assert.Empty(tracker.Cooldowns);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeReuseInterval_IsRejectedWithoutChangingExistingCooldown(int interval)
    {
        var tracker = new AbilityCooldownTracker();
        tracker.RecordUse("ability", 2, 3);
        var entry = tracker.Cooldowns["ability"];

        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.RecordUse("ability", 5, interval));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityCooldown(5, interval));

        Assert.Same(entry, tracker.Cooldowns["ability"]);
    }

    [Theory]
    [InlineData(int.MaxValue, 1)]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void ReadyRoundOverflow_IsRejectedWithoutMutation(int round, int interval)
    {
        var tracker = new AbilityCooldownTracker();
        tracker.RecordUse("ability", 2, 3);
        var previous = tracker.Cooldowns["ability"];

        Assert.Throws<OverflowException>(() => tracker.RecordUse("ability", round, interval));
        Assert.Throws<OverflowException>(() => tracker.RecordUse("new_ability", round, interval));

        Assert.Single(tracker.Cooldowns);
        Assert.Same(previous, tracker.Cooldowns["ability"]);
    }

    [Fact]
    public void LargestRepresentableDeadline_IsSupported()
    {
        var tracker = new AbilityCooldownTracker();

        tracker.RecordUse("ability", int.MaxValue - 3, 3);

        Assert.Equal(int.MaxValue, tracker.Cooldowns["ability"].ReadyRound);
        Assert.False(tracker.IsReady("ability", int.MaxValue - 1));
        Assert.True(tracker.IsReady("ability", int.MaxValue));
    }

    [Fact]
    public void CooldownExposure_IsReadOnly_AndEntriesAreImmutable()
    {
        var tracker = new AbilityCooldownTracker();
        tracker.RecordUse("ability", 2, 3);
        var view = tracker.Cooldowns;
        var dictionary = Assert.IsAssignableFrom<IDictionary<string, AbilityCooldown>>(view);

        Assert.Throws<NotSupportedException>(() => dictionary["ability"] = new AbilityCooldown(1, 0));
        Assert.Throws<NotSupportedException>(() => dictionary.Add("new", new AbilityCooldown(1, 0)));
        Assert.Throws<NotSupportedException>(() => dictionary.Remove("ability"));
        Assert.Throws<NotSupportedException>(() => dictionary.Clear());
        Assert.All(typeof(AbilityCooldown).GetProperties(), property => Assert.Null(property.SetMethod));
        tracker.RecordUse("ability", 5, 3);
        Assert.Equal(8, view["ability"].ReadyRound); // Read-only view reflects current entries.
    }

    [Fact]
    public void DefeatAndActivationReset_PreserveCooldownDeadlines_WithoutPausingReadiness()
    {
        var character = Create();
        character.Cooldowns.RecordUse("temporal_ignition", 2, 2);
        var entry = character.Cooldowns.Cooldowns["temporal_ignition"];
        character.EndActivation();
        character.TakeDamage(int.MaxValue);

        character.ResetActivation();

        Assert.True(character.IsDefeated);
        Assert.False(character.CanAct);
        Assert.Same(entry, character.Cooldowns.Cooldowns["temporal_ignition"]);
        Assert.False(character.Cooldowns.IsReady("temporal_ignition", 3));
        Assert.True(character.Cooldowns.IsReady("temporal_ignition", 4));
    }

    [Fact]
    public void RecordingCooldowns_DoesNotChangeHealthShieldsStatusesHistoryActivationOrStats()
    {
        var character = Create();
        character.TakeDamage(5);
        HealingApplicator.Apply(1, character);
        character.SetShieldHP(6);
        character.EndActivation();
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        character.Statuses.ApplyPoison();
        var stats = character.BaseStats;
        var snapshot = stats with { };

        character.Cooldowns.RecordUse("future_ability", 2, 3); // Cooldown bookkeeping is independent of Silence/actions.

        Assert.Equal(stats.MaxHealth - 4, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.True(character.HasActedThisRound);
        Assert.Equal(1, character.HealsReceivedThisRound);
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Silence]);
        Assert.True(character.Statuses.IsPoisoned);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(snapshot, character.BaseStats);
    }

    [Fact]
    public void ReadinessQueries_AreDeterministicAndDoNotDecrementOrReplaceEntries()
    {
        var tracker = new AbilityCooldownTracker();
        tracker.RecordUse("ability", 2, 3);
        var entry = tracker.Cooldowns["ability"];

        for (int i = 0; i < 5; i++)
        {
            Assert.False(tracker.IsReady("ability", 4));
            Assert.True(tracker.IsReady("ability", 5));
            Assert.True(tracker.IsReady("ability", 100));
        }

        Assert.Same(entry, tracker.Cooldowns["ability"]);
        Assert.Equal(5, entry.ReadyRound);
    }

    [Theory]
    [InlineData("hooded_assassin")]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("nigel")]
    [InlineData("gravebound_colossus")]
    [InlineData("beothuk_spirit_guardian")]
    public void Tracker_IsAvailableForEveryCharacter_WithoutAbilityDefinitionDependencies(string id)
    {
        var character = CharacterFactory.Create(id, 1)!;
        character.Cooldowns.RecordUse("prototype_ability", 2, 3); // Example input, not a new character ability.

        Assert.Equal(5, character.Cooldowns.Cooldowns["prototype_ability"].ReadyRound);
    }

    private static Character Create() => CharacterFactory.Create("time_weaver", 1)!;
}
