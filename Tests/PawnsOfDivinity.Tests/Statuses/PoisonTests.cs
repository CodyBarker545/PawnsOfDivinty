using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Statuses;

public class PoisonTests
{
    [Fact]
    public void ApplyingPoison_StartsAtZeroWithoutImmediateDamageOrCountdown()
    {
        var character = Unit();
        Assert.False(character.Statuses.IsPoisoned);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);

        character.Statuses.ApplyPoison();
        character.Statuses.ApplyPoison();
        character.Statuses.AdvanceTimedStatuses();

        Assert.True(character.Statuses.IsPoisoned);
        Assert.True(character.Statuses.HasStatus(StatusType.Poison));
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
        Assert.Empty(character.Statuses.TimedStatuses);
        Assert.Equal(100, character.CurrentHealth);
        Assert.Throws<ArgumentException>(() => character.Statuses.ApplyTimedStatus(StatusType.Poison, 2));
    }

    [Fact]
    public void FirstThroughFifthTicks_EscalateOncePerInvocation_DespiteReapplication()
    {
        var character = Unit();
        character.Statuses.ApplyPoison();
        int health = 100;

        for (int tick = 1; tick <= 5; tick++)
        {
            character.Statuses.ApplyPoison();
            character.Statuses.ApplyPoison();
            Assert.Equal(tick - 1, character.Statuses.PoisonTicksOccurred);
            var result = PoisonProcessor.Process(character);
            health -= tick * 2;

            Assert.Equal(tick, result.TickNumber);
            Assert.Equal(tick * 2, result.CalculatedDamage);
            Assert.Equal(0, result.ShieldHPAbsorbed);
            Assert.Equal(tick * 2, result.HealthLost);
            Assert.Equal(health, result.RemainingHealth);
            Assert.False(result.Defeated);
            Assert.Equal(tick, character.Statuses.PoisonTicksOccurred);
        }
        Assert.Equal(70, character.CurrentHealth);
    }

    [Fact]
    public void ClearPoison_ResetsProgression_AndReapplicationStartsANewSequence()
    {
        var character = Unit();
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        character.Statuses.ApplyPoison();
        PoisonProcessor.Process(character);
        PoisonProcessor.Process(character);

        character.Statuses.ClearPoison();
        character.Statuses.ClearPoison();

        Assert.False(character.Statuses.HasStatus(StatusType.Poison));
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
        Assert.Equal(94, character.CurrentHealth);
        Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Burn]);
        character.Statuses.ApplyPoison();
        var result = PoisonProcessor.Process(character);
        Assert.Equal(1, result.TickNumber);
        Assert.Equal(2, result.CalculatedDamage);
    }

    [Theory]
    [InlineData(6, 4, 0, 100)]
    [InlineData(2, 0, 0, 100)]
    [InlineData(1, 0, 1, 99)]
    [InlineData(0, 0, 2, 98)]
    public void ShieldsAbsorbBeforeHealth(int shield, int remainingShield, int healthLost, int remainingHealth)
    {
        var character = Unit();
        character.SetShieldHP(shield);
        character.Statuses.ApplyPoison();

        var result = PoisonProcessor.Process(character);

        Assert.Equal(2, result.CalculatedDamage);
        Assert.Equal(shield - remainingShield, result.ShieldHPAbsorbed);
        Assert.Equal(healthLost, result.HealthLost);
        Assert.Equal(remainingShield, character.ShieldHP);
        Assert.Equal(remainingHealth, result.RemainingHealth);
        Assert.Equal(1, character.Statuses.PoisonTicksOccurred);
    }

    [Fact]
    public void LethalTick_ReportsActualLossAndClearsPoison()
    {
        var character = Unit();
        character.TakeDamage(99);
        character.Statuses.ApplyPoison();

        var result = PoisonProcessor.Process(character);

        Assert.Equal(1, result.TickNumber);
        Assert.Equal(2, result.CalculatedDamage);
        Assert.Equal(1, result.HealthLost);
        Assert.Equal(0, result.RemainingHealth);
        Assert.True(result.Defeated);
        Assert.False(character.Statuses.IsPoisoned);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
        Assert.Throws<InvalidOperationException>(() => PoisonProcessor.Process(character));
    }

    [Fact]
    public void DeathFromOtherDamage_AlsoClearsPoison()
    {
        var character = Unit();
        character.Statuses.ApplyPoison();
        PoisonProcessor.Process(character);

        character.TakeDamage(int.MaxValue);

        Assert.False(character.Statuses.IsPoisoned);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
    }

    [Fact]
    public void IndependentCharacters_KeepSeparateProgression_AndDamageDoesNotProcessOtherTargets()
    {
        var first = CharacterFactory.Create("nigel", 1)!;
        var second = CharacterFactory.Create("nigel", 2)!;
        Assert.Same(first.BaseStats, second.BaseStats);
        first.Statuses.ApplyPoison();
        second.Statuses.ApplyPoison();
        PoisonProcessor.Process(first);
        PoisonProcessor.Process(first);

        Assert.Equal(2, first.Statuses.PoisonTicksOccurred);
        Assert.Equal(0, second.Statuses.PoisonTicksOccurred);
        Assert.Equal(52, second.CurrentHealth);
        first.Statuses.ClearPoison();
        Assert.True(second.Statuses.IsPoisoned);
        Assert.Equal(2, PoisonProcessor.Process(second).CalculatedDamage);
    }

    [Fact]
    public void TimedStatusAdvancement_DoesNotChangePoisonProgression()
    {
        var character = Unit();
        character.Statuses.ApplyPoison();
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        PoisonProcessor.Process(character);

        character.Statuses.AdvanceTimedStatuses();
        character.Statuses.AdvanceTimedStatuses();
        character.Statuses.AdvanceTimedStatuses();

        Assert.Empty(character.Statuses.TimedStatuses);
        Assert.True(character.Statuses.HasStatus(StatusType.Poison));
        Assert.Equal(1, character.Statuses.PoisonTicksOccurred);
        Assert.Equal(4, PoisonProcessor.Process(character).CalculatedDamage);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    [InlineData(4, 8)]
    [InlineData(5, 10)]
    [InlineData(1073741823, 2147483646)]
    public void Calculator_ReturnsTwiceTheTickNumber(int tick, int expected)
        => Assert.Equal(expected, PoisonDamageCalculator.Calculate(tick));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Calculator_RejectsNonpositiveTicks(int tick)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PoisonDamageCalculator.Calculate(tick));

    [Theory]
    [InlineData(1073741824)]
    [InlineData(int.MaxValue)]
    public void Calculator_RejectsDamageOutsideIntRange(int tick)
        => Assert.Throws<OverflowException>(() => PoisonDamageCalculator.Calculate(tick));

    [Fact]
    public void CalculationAlone_DoesNotChangeCharacterState()
    {
        var character = Unit();
        character.Statuses.ApplyPoison();
        character.SetShieldHP(6);

        Assert.Equal(10, PoisonDamageCalculator.Calculate(5));
        Assert.Equal(10, PoisonDamageCalculator.Calculate(5));

        Assert.Equal(100, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
    }

    [Fact]
    public void InvalidRecipients_AreRejectedWithoutStateChanges()
    {
        Assert.Throws<ArgumentNullException>(() => PoisonProcessor.Process(null!));
        var character = Unit();
        Assert.Throws<InvalidOperationException>(() => PoisonProcessor.Process(character));
        Assert.Equal(100, character.CurrentHealth);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
    }

    [Fact]
    public void Ticks_LeaveBaseStatsActivationAndOtherStatusesUnchanged()
    {
        var character = Unit();
        var stats = character.BaseStats;
        var snapshot = stats with { };
        character.EndActivation();
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        character.Statuses.ApplyPoison();

        var result = PoisonProcessor.Process(character);

        Assert.Equal(2, result.HealthLost);
        Assert.True(character.HasActedThisRound);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(snapshot, character.BaseStats);
        Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Frostbite]);
    }

    [Fact]
    public void PoisonDepletesOwnedShield_ThroughExistingOwnershipHandling()
    {
        var guardian = CharacterFactory.Create("gentle_guardian", 1)!;
        var recipient = Unit();
        var manager = new SanctuaryShieldManager();
        manager.Apply(guardian, recipient);
        recipient.Statuses.ApplyPoison();

        PoisonProcessor.Process(recipient);
        Assert.Same(recipient, manager.GetRecipient(guardian));
        var result = PoisonProcessor.Process(recipient);

        Assert.Equal(4, result.ShieldHPAbsorbed);
        Assert.Equal(0, result.HealthLost);
        Assert.Null(manager.GetRecipient(guardian));
    }

    [Fact]
    public void PoisonDeath_DoesNotRemoveShieldOnAnotherLivingRecipient()
    {
        var guardian = CharacterFactory.Create("gentle_guardian", 1)!;
        var recipient = Unit();
        var manager = new SanctuaryShieldManager();
        manager.Apply(guardian, recipient);
        guardian.TakeDamage(43);
        guardian.Statuses.ApplyPoison();

        Assert.True(PoisonProcessor.Process(guardian).Defeated);

        Assert.Equal(6, recipient.ShieldHP);
        Assert.Equal(100, recipient.CurrentHealth);
        Assert.Same(recipient, manager.GetRecipient(guardian));
    }

    private static Character Unit() => new("test", "Test character", 1,
        new CharacterStats(100, 20, 10, 12, 8, 4, 15, 5));
}
