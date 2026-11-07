using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Statuses;

public class StatusTrackerTests
{
    [Fact]
    public void StatusType_ContainsSixDocumentedStatuses_WithoutSleep()
    {
        Assert.Equal(new[]
        {
            StatusType.Frostbite, StatusType.Paralysis, StatusType.Burn,
            StatusType.Poison, StatusType.Silence, StatusType.Disarm
        }, Enum.GetValues<StatusType>());
    }

    [Theory]
    [InlineData(StatusType.Frostbite)]
    [InlineData(StatusType.Paralysis)]
    [InlineData(StatusType.Burn)]
    [InlineData(StatusType.Silence)]
    [InlineData(StatusType.Disarm)]
    public void ApplyTimedStatus_TracksOneActiveStatusAndItsDuration(StatusType status)
    {
        var tracker = new StatusTracker();
        Assert.False(tracker.HasStatus(status));

        tracker.ApplyTimedStatus(status, 2);

        Assert.True(tracker.HasStatus(status));
        Assert.Equal(2, tracker.TimedStatuses[status]);
        Assert.Single(tracker.TimedStatuses);
    }

    [Fact]
    public void DifferentTimedStatuses_Coexist()
    {
        var tracker = new StatusTracker();
        StatusType[] timed = { StatusType.Frostbite, StatusType.Paralysis, StatusType.Burn, StatusType.Silence, StatusType.Disarm };

        foreach (var status in timed) tracker.ApplyTimedStatus(status, 2);

        Assert.Equal(5, tracker.TimedStatuses.Count);
        Assert.All(timed, status => Assert.True(tracker.HasStatus(status)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void Reapplication_RefreshesSuppliedDuration_WithoutStackingOrChangingOtherStatuses(int refreshedDuration)
    {
        var tracker = new StatusTracker();
        tracker.ApplyTimedStatus(StatusType.Frostbite, 5);
        tracker.ApplyTimedStatus(StatusType.Burn, 3);
        tracker.AdvanceTimedStatuses();
        Assert.Equal(4, tracker.TimedStatuses[StatusType.Frostbite]);

        tracker.ApplyTimedStatus(StatusType.Frostbite, refreshedDuration);
        tracker.ApplyTimedStatus(StatusType.Frostbite, refreshedDuration);

        Assert.Equal(2, tracker.TimedStatuses.Count);
        Assert.Equal(refreshedDuration, tracker.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(2, tracker.TimedStatuses[StatusType.Burn]);
    }

    [Fact]
    public void AdvanceTimedStatuses_DecrementsEachAndRemovesOnlyExpiredEntries()
    {
        var tracker = new StatusTracker();
        tracker.ApplyTimedStatus(StatusType.Silence, 1);
        tracker.ApplyTimedStatus(StatusType.Burn, 2);
        tracker.ApplyTimedStatus(StatusType.Disarm, 3);

        tracker.AdvanceTimedStatuses();

        Assert.False(tracker.HasStatus(StatusType.Silence));
        Assert.Equal(1, tracker.TimedStatuses[StatusType.Burn]);
        Assert.Equal(2, tracker.TimedStatuses[StatusType.Disarm]);
        tracker.AdvanceTimedStatuses();
        Assert.False(tracker.HasStatus(StatusType.Burn));
        Assert.Equal(1, tracker.TimedStatuses[StatusType.Disarm]);
    }

    [Fact]
    public void TwoRoundStatus_AppliedInRoundTwo_ExpiresAtRoundFourStart()
    {
        var tracker = new StatusTracker();
        tracker.ApplyTimedStatus(StatusType.Burn, 2); // Applied during R2; no tick on application.
        Assert.Equal(2, tracker.TimedStatuses[StatusType.Burn]);

        tracker.AdvanceTimedStatuses(); // Start R3, after Poison and delayed damage.
        Assert.True(tracker.HasStatus(StatusType.Burn));
        Assert.Equal(1, tracker.TimedStatuses[StatusType.Burn]);

        tracker.AdvanceTimedStatuses(); // Start R4, after those damage stages.
        Assert.False(tracker.HasStatus(StatusType.Burn));
        Assert.Empty(tracker.TimedStatuses);
    }

    [Fact]
    public void AdvanceTimedStatuses_EmptyTracker_IsSafe()
    {
        var tracker = new StatusTracker();

        tracker.AdvanceTimedStatuses();
        tracker.AdvanceTimedStatuses();

        Assert.Empty(tracker.TimedStatuses);
    }

    [Fact]
    public void Characters_SharingStats_HaveIndependentStatusState()
    {
        var first = CharacterFactory.Create("nigel", 1);
        var second = CharacterFactory.Create("nigel", 2);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Same(first.BaseStats, second.BaseStats);
        Assert.NotSame(first.Statuses, second.Statuses);
        first.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        Assert.False(second.Statuses.HasStatus(StatusType.Burn));
        second.Statuses.ApplyTimedStatus(StatusType.Burn, 3);

        first.Statuses.AdvanceTimedStatuses();

        Assert.Equal(1, first.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(3, second.Statuses.TimedStatuses[StatusType.Burn]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidStatus_IsRejectedWithoutChangingEntries(int invalid)
    {
        var tracker = new StatusTracker();
        tracker.ApplyTimedStatus(StatusType.Burn, 2);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => tracker.ApplyTimedStatus((StatusType)invalid, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.HasStatus((StatusType)invalid));

        Assert.Equal("status", error.ParamName);
        Assert.Single(tracker.TimedStatuses);
        Assert.Equal(2, tracker.TimedStatuses[StatusType.Burn]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void InvalidDuration_IsRejectedForNewAndExistingStatuses(int duration)
    {
        var tracker = new StatusTracker();
        tracker.ApplyTimedStatus(StatusType.Burn, 2);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => tracker.ApplyTimedStatus(StatusType.Burn, duration));
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.ApplyTimedStatus(StatusType.Frostbite, duration));

        Assert.Equal("duration", error.ParamName);
        Assert.Single(tracker.TimedStatuses);
        Assert.Equal(2, tracker.TimedStatuses[StatusType.Burn]);
    }

    [Fact]
    public void LargestDuration_DecrementsWithoutOverflow()
    {
        var tracker = new StatusTracker();
        tracker.ApplyTimedStatus(StatusType.Burn, int.MaxValue);

        tracker.AdvanceTimedStatuses();

        Assert.Equal(int.MaxValue - 1, tracker.TimedStatuses[StatusType.Burn]);
    }

    [Fact]
    public void TimedStatuses_ReadOnlyView_CannotBeMutatedByCasting()
    {
        var tracker = new StatusTracker();
        tracker.ApplyTimedStatus(StatusType.Burn, 2);
        var view = tracker.TimedStatuses;
        var dictionary = Assert.IsAssignableFrom<IDictionary<StatusType, int>>(view);

        Assert.True(dictionary.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => dictionary[StatusType.Burn] = 99);
        Assert.Throws<NotSupportedException>(() => dictionary.Add(StatusType.Silence, 1));
        Assert.Throws<NotSupportedException>(() => dictionary.Remove(StatusType.Burn));
        Assert.Throws<NotSupportedException>(() => dictionary.Clear());
        Assert.Equal(2, view[StatusType.Burn]);

        tracker.AdvanceTimedStatuses();
        Assert.Equal(1, view[StatusType.Burn]); // The view still reflects the tracker's current state.
    }

    [Fact]
    public void Poison_IsKnownButCannotBeAppliedWithAnOrdinaryCountdown()
    {
        var tracker = new StatusTracker();
        tracker.ApplyTimedStatus(StatusType.Burn, 2);

        var error = Assert.Throws<ArgumentException>(() => tracker.ApplyTimedStatus(StatusType.Poison, 2));
        tracker.AdvanceTimedStatuses();

        Assert.Equal("status", error.ParamName);
        Assert.False(tracker.HasStatus(StatusType.Poison));
        Assert.False(tracker.TimedStatuses.ContainsKey(StatusType.Poison));
        Assert.Equal(1, tracker.TimedStatuses[StatusType.Burn]);
    }

    [Fact]
    public void StatusChanges_DoNotApplyPenaltiesDamageOrActivationChanges()
    {
        var character = CharacterFactory.Create("nigel", 1);
        Assert.NotNull(character);
        character.TakeDamage(5);
        character.SetShieldHP(6);
        character.EndActivation();
        HealingApplicator.Apply(0, character);
        var stats = character.BaseStats;
        var snapshot = stats with { };
        int health = character.CurrentHealth;
        int heals = character.HealsReceivedThisRound;

        foreach (var status in new[] { StatusType.Frostbite, StatusType.Paralysis, StatusType.Burn, StatusType.Silence, StatusType.Disarm })
            character.Statuses.ApplyTimedStatus(status, 2);
        character.Statuses.AdvanceTimedStatuses();
        character.Statuses.AdvanceTimedStatuses();

        Assert.Empty(character.Statuses.TimedStatuses);
        Assert.Equal(health, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.True(character.HasActedThisRound);
        Assert.Equal(heals, character.HealsReceivedThisRound);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(snapshot, character.BaseStats);
    }

}
