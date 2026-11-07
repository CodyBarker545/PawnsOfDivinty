using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Statuses;

public class CleanseTests
{
    [Theory]
    [InlineData(StatusType.Burn)]
    [InlineData(StatusType.Frostbite)]
    [InlineData(StatusType.Paralysis)]
    [InlineData(StatusType.Poison)]
    [InlineData(StatusType.Silence)]
    [InlineData(StatusType.Disarm)]
    public void BasicCleanse_RemovesOnlySelectedStatus_PreservingOtherDurationsAndPoisonSeverity(StatusType selected)
    {
        var character = Create();
        ApplyAll(character);
        PoisonProcessor.Process(character);
        var durations = character.Statuses.TimedStatuses.ToDictionary(pair => pair.Key, pair => pair.Value);

        var result = CleanseProcessor.BasicCleanse(character, selected);

        Assert.Equal(new[] { selected }, result.RemovedStatuses);
        Assert.False(character.Statuses.HasStatus(selected));
        foreach (var other in Enum.GetValues<StatusType>().Where(status => status != selected))
        {
            Assert.True(character.Statuses.HasStatus(other));
            if (other != StatusType.Poison) Assert.Equal(durations[other], character.Statuses.TimedStatuses[other]);
        }
        Assert.Equal(selected == StatusType.Poison ? 0 : 1, character.Statuses.PoisonTicksOccurred);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CleansingPoison_ResetsEscalation_AndReapplicationStartsAtTickOne(bool greater)
    {
        var character = Create();
        character.Statuses.ApplyPoison();
        PoisonProcessor.Process(character);
        PoisonProcessor.Process(character);

        var result = greater ? CleanseProcessor.GreaterCleanse(character)
            : CleanseProcessor.BasicCleanse(character, StatusType.Poison);

        Assert.Equal(new[] { StatusType.Poison }, result.RemovedStatuses);
        Assert.False(character.Statuses.IsPoisoned);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
        character.Statuses.ApplyPoison();
        var tick = PoisonProcessor.Process(character);
        Assert.Equal(1, tick.TickNumber);
        Assert.Equal(2, tick.CalculatedDamage);
    }

    [Fact]
    public void GreaterCleanse_RemovesAllStatuses_InStableOrder_AndIsSafeToRepeat()
    {
        var character = Create();
        ApplyAll(character);
        PoisonProcessor.Process(character);

        var result = CleanseProcessor.GreaterCleanse(character);

        Assert.Equal(Enum.GetValues<StatusType>(), result.RemovedStatuses);
        Assert.Empty(character.Statuses.TimedStatuses);
        Assert.All(Enum.GetValues<StatusType>(), status => Assert.False(character.Statuses.HasStatus(status)));
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
        Assert.Empty(CleanseProcessor.GreaterCleanse(character).RemovedStatuses);
        Assert.Equal(Enum.GetValues<StatusType>(), result.RemovedStatuses);
    }

    [Theory]
    [InlineData(StatusType.Burn)]
    [InlineData(StatusType.Frostbite)]
    [InlineData(StatusType.Paralysis)]
    [InlineData(StatusType.Poison)]
    [InlineData(StatusType.Silence)]
    [InlineData(StatusType.Disarm)]
    public void BasicCleanse_AbsentSelectedStatus_IsANoOp(StatusType selected)
    {
        var character = Create();
        var other = selected == StatusType.Burn ? StatusType.Frostbite : StatusType.Burn;
        character.Statuses.ApplyTimedStatus(other, 2);

        var result = CleanseProcessor.BasicCleanse(character, selected);

        Assert.Empty(result.RemovedStatuses);
        Assert.Single(character.Statuses.TimedStatuses);
        Assert.Equal(2, character.Statuses.TimedStatuses[other]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CleansingUnaffectedCharacter_IsANoOp(bool greater)
    {
        var character = Create();

        var result = greater ? CleanseProcessor.GreaterCleanse(character)
            : CleanseProcessor.BasicCleanse(character, StatusType.Poison);

        Assert.Empty(result.RemovedStatuses);
        Assert.Empty(character.Statuses.TimedStatuses);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidStatus_IsRejectedWithoutAnyRemoval(int invalid)
    {
        var character = Create();
        ApplyAll(character);
        PoisonProcessor.Process(character);

        Assert.Throws<ArgumentOutOfRangeException>(() => CleanseProcessor.BasicCleanse(character, (StatusType)invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => character.Statuses.RemoveStatus((StatusType)invalid));

        Assert.All(Enum.GetValues<StatusType>(), status => Assert.True(character.Statuses.HasStatus(status)));
        Assert.Equal(1, character.Statuses.PoisonTicksOccurred);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullRecipient_IsRejected(bool greater)
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
        {
            if (greater) CleanseProcessor.GreaterCleanse(null!);
            else CleanseProcessor.BasicCleanse(null!, StatusType.Burn);
        });
        Assert.Equal("recipient", error.ParamName);
    }

    [Fact]
    public void TrackerRemoval_ReportsActualRemovals_AndDoesNotExposeMutableCollections()
    {
        var tracker = new StatusTracker();
        tracker.ApplyTimedStatus(StatusType.Burn, 2);
        tracker.ApplyPoison();
        Assert.True(tracker.RemoveStatus(StatusType.Burn));
        Assert.False(tracker.RemoveStatus(StatusType.Burn));
        var removed = tracker.RemoveAllNegativeStatuses();

        Assert.Equal(new[] { StatusType.Poison }, removed);
        var list = Assert.IsAssignableFrom<IList<StatusType>>(removed);
        Assert.Throws<NotSupportedException>(() => list[0] = StatusType.Burn);
        Assert.False(tracker.IsPoisoned);
        Assert.Empty(tracker.RemoveAllNegativeStatuses());
    }

    [Fact]
    public void Result_IsAnImmutableSnapshot_IndependentOfLaterStatusChanges()
    {
        var character = Create();
        ApplyAll(character);
        var result = CleanseProcessor.GreaterCleanse(character);
        ApplyAll(character);
        CleanseProcessor.GreaterCleanse(character);

        Assert.Equal(Enum.GetValues<StatusType>(), result.RemovedStatuses);
        Assert.All(typeof(CleanseResult).GetProperties(), property => Assert.Null(property.SetMethod));
        var list = Assert.IsAssignableFrom<IList<StatusType>>(result.RemovedStatuses);
        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.Throws<NotSupportedException>(() => list.Add(StatusType.Burn));
        Assert.Throws<NotSupportedException>(() => list[0] = StatusType.Burn);
    }

    [Theory]
    [InlineData(StatusType.Burn)]
    [InlineData(StatusType.Frostbite)]
    [InlineData(StatusType.Paralysis)]
    public void BasicCleanse_RestoresOnlySelectedEffectiveStat(StatusType selected)
    {
        var character = Create();
        ApplyAll(character);

        CleanseProcessor.BasicCleanse(character, selected);

        Assert.Equal(character.BaseStats.Defense - (selected == StatusType.Burn ? 0 : 3), character.EffectiveDefense);
        Assert.Equal(character.BaseStats.Movement - (selected == StatusType.Frostbite ? 0 : 2), character.EffectiveMovement);
        Assert.Equal(character.BaseStats.Speed - (selected == StatusType.Paralysis ? 0 : 4), character.EffectiveSpeed);
    }

    [Fact]
    public void GreaterCleanse_RestoresEffectiveStatsSpellAndWeaponEligibility()
    {
        var character = Create();
        ApplyAll(character);
        Assert.False(SilenceEligibility.CanUseAbility(character, true));
        Assert.False(DisarmEligibility.CanUseEquippedWeapon(character));

        CleanseProcessor.GreaterCleanse(character);

        Assert.Equal(character.BaseStats.Defense, character.EffectiveDefense);
        Assert.Equal(character.BaseStats.Movement, character.EffectiveMovement);
        Assert.Equal(character.BaseStats.Speed, character.EffectiveSpeed);
        Assert.True(SilenceEligibility.CanUseAbility(character, true));
        Assert.True(DisarmEligibility.CanUseEquippedWeapon(character));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cleanse_PreservesHealthOwnedShieldHealingHistoryActivationAndBaseStats(bool greater)
    {
        var guardian = CharacterFactory.Create("gentle_guardian", 1)!;
        var character = Create();
        character.TakeDamage(10);
        HealingApplicator.Apply(1, character);
        character.EndActivation();
        var manager = new SanctuaryShieldManager();
        manager.Apply(guardian, character);
        ApplyAll(character);
        var stats = character.BaseStats;
        var snapshot = stats with { };

        if (greater) CleanseProcessor.GreaterCleanse(character);
        else CleanseProcessor.BasicCleanse(character, StatusType.Burn);

        Assert.Equal(stats.MaxHealth - 9, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.Same(character, manager.GetRecipient(guardian));
        Assert.Equal(1, character.HealsReceivedThisRound);
        Assert.True(character.HasActedThisRound);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(snapshot, character.BaseStats);
    }

    [Fact]
    public void Cleanse_LeavesOtherCharactersIndependent_AndReturnsDeterministicResults()
    {
        var first = Create();
        var second = Create();
        Assert.Same(first.BaseStats, second.BaseStats);
        ApplyAll(first);
        ApplyAll(second);
        PoisonProcessor.Process(second);

        var firstResult = CleanseProcessor.GreaterCleanse(first);
        Assert.All(Enum.GetValues<StatusType>(), status => Assert.True(second.Statuses.HasStatus(status)));
        Assert.Equal(1, second.Statuses.PoisonTicksOccurred);
        var secondResult = CleanseProcessor.GreaterCleanse(second);

        Assert.Equal(firstResult.RemovedStatuses, secondResult.RemovedStatuses);
        // The processor has no random provider: identical status inputs yield the same ordered removals.
        Assert.Equal(Enum.GetValues<StatusType>(), firstResult.RemovedStatuses);
    }

    private static Character Create() => CharacterFactory.Create("nigel", 1)!;

    private static void ApplyAll(Character character)
    {
        character.ConfigureParalysisSpeedReduction(4); // Explicit prototype example, not finalized balance.
        foreach (var status in Enum.GetValues<StatusType>().Reverse())
        {
            if (status == StatusType.Poison) character.Statuses.ApplyPoison();
            else character.Statuses.ApplyTimedStatus(status, 2);
        }
    }
}
