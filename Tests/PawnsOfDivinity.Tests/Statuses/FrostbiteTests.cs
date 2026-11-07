using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Statuses;

public class FrostbiteTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(6, 4)]
    [InlineData(int.MaxValue, int.MaxValue - 2)]
    public void Frostbite_ReducesMovementByTwo_WithZeroFloor(int movement, int expected)
    {
        var character = Unit(movement);
        Assert.Equal(movement, character.EffectiveMovement);

        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);

        Assert.Equal(expected, character.EffectiveMovement);
        Assert.Equal(movement, character.BaseStats.Movement);
    }

    [Fact]
    public void TwoRoundFrostbite_ExpiresAndRestoresBaseMovement()
    {
        var character = Unit(5);
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2); // Applied during R2.
        Assert.Equal(3, character.EffectiveMovement);

        character.Statuses.AdvanceTimedStatuses(); // R3 start, after damage processing.
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(3, character.EffectiveMovement);
        character.Statuses.AdvanceTimedStatuses(); // R4 start.

        Assert.False(character.Statuses.HasStatus(StatusType.Frostbite));
        Assert.Equal(5, character.EffectiveMovement);
    }

    [Fact]
    public void Reapplication_RefreshesDurationWithoutStackingMovementPenalty()
    {
        var character = Unit(5);
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        character.Statuses.AdvanceTimedStatuses();

        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);

        Assert.Single(character.Statuses.TimedStatuses);
        Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(3, character.EffectiveMovement);
        character.Statuses.AdvanceTimedStatuses();
        Assert.Equal(3, character.EffectiveMovement);
        character.Statuses.AdvanceTimedStatuses();
        Assert.Equal(5, character.EffectiveMovement);
    }

    [Fact]
    public void BurnAndFrostbite_CoexistAndExpireIndependently()
    {
        var character = Unit(5);
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 3);
        Assert.Equal(5, character.EffectiveMovement);
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);

        Assert.Equal(3, character.EffectiveMovement);
        Assert.Equal(7, character.EffectiveDefense);
        character.Statuses.AdvanceTimedStatuses();
        character.Statuses.AdvanceTimedStatuses();

        Assert.Equal(5, character.EffectiveMovement);
        Assert.Equal(7, character.EffectiveDefense);
        Assert.True(character.Statuses.HasStatus(StatusType.Burn));
        character.Statuses.AdvanceTimedStatuses();
        Assert.Equal(10, character.EffectiveDefense);
    }

    [Theory]
    [InlineData(StatusType.Paralysis)]
    [InlineData(StatusType.Silence)]
    [InlineData(StatusType.Disarm)]
    public void OtherTimedStatuses_DoNotChangeEffectiveMovement(StatusType status)
    {
        var character = Unit(5);
        character.Statuses.ApplyTimedStatus(status, 2);

        Assert.Equal(5, character.EffectiveMovement);
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        Assert.Equal(3, character.EffectiveMovement);
        Assert.True(character.Statuses.HasStatus(status));
    }

    [Fact]
    public void SharedBaseStats_DoNotShareFrostbiteState()
    {
        var first = CharacterFactory.Create("hooded_assassin", 1)!;
        var second = CharacterFactory.Create("hooded_assassin", 2)!;
        Assert.Same(first.BaseStats, second.BaseStats);

        first.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);

        Assert.Equal(4, first.EffectiveMovement);
        Assert.Equal(6, second.EffectiveMovement);
        Assert.False(second.Statuses.HasStatus(StatusType.Frostbite));
        first.Statuses.AdvanceTimedStatuses();
        first.Statuses.AdvanceTimedStatuses();
        Assert.Equal(6, first.EffectiveMovement);
    }

    [Fact]
    public void ApplyingReadingAndExpiringFrostbite_LeaveOtherCharacterStateUnchanged()
    {
        var character = Unit(5);
        character.TakeDamage(7);
        character.SetShieldHP(6);
        character.EndActivation();
        var stats = character.BaseStats;
        var snapshot = stats with { };
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        Assert.Equal(3, character.EffectiveMovement);
        Assert.Equal(3, character.EffectiveMovement); // Reading the property never consumes movement or time.
        Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(93, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.True(character.HasActedThisRound);

        character.Statuses.AdvanceTimedStatuses();
        character.Statuses.AdvanceTimedStatuses();

        Assert.Equal(93, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.True(character.HasActedThisRound);
        Assert.Equal(0, character.HealsReceivedThisRound);
        Assert.Equal(10, character.EffectiveDefense);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(snapshot, character.BaseStats);
        Assert.Null(typeof(Character).GetProperty(nameof(Character.EffectiveMovement))!.SetMethod);
    }

    private static Character Unit(int movement) => new("test", "Test character", 1,
        new CharacterStats(100, 20, 10, 12, 8, 4, 15, movement));
}
