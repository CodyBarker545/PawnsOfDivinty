using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using Xunit;

namespace PawnsOfDivinity.Tests.Combat;

public class HealingApplicatorTests
{
    [Theory]
    [InlineData(1, 11, 12)]
    [InlineData(2, 8, 20)]
    [InlineData(3, 5, 25)]
    [InlineData(4, 2, 27)]
    [InlineData(5, 2, 29)]
    public void Apply_FirstFiveHeals_UseRecipientsNextOrdinal(int ordinal, int adjusted, int resultingHealth)
    {
        var recipient = Recipient("gravebound_colossus");
        Assert.Equal(0, recipient.HealsReceivedThisRound);
        recipient.TakeDamage(63);

        HealingResult? result = null;
        for (int i = 0; i < ordinal; i++)
            result = HealingApplicator.Apply(11, recipient);

        Assert.NotNull(result);
        Assert.Equal(ordinal, result.HealOrdinal);
        Assert.Equal(11, result.BaseHealing);
        Assert.Equal(adjusted, result.AdjustedHealing);
        Assert.Equal(adjusted, result.HealthRestored);
        Assert.Equal(resultingHealth, result.ResultingHealth);
        Assert.Equal(resultingHealth, recipient.CurrentHealth);
        Assert.Equal(ordinal, recipient.HealsReceivedThisRound);
    }

    [Fact]
    public void Apply_FullHealthLivingTarget_StillConsumesOrdinal()
    {
        var recipient = Recipient();

        var first = HealingApplicator.Apply(11, recipient);
        recipient.TakeDamage(20);
        var second = HealingApplicator.Apply(11, recipient);

        Assert.Equal(1, first.HealOrdinal);
        Assert.Equal(11, first.AdjustedHealing);
        Assert.Equal(0, first.HealthRestored);
        Assert.Equal(52, first.ResultingHealth);
        Assert.Equal(2, second.HealOrdinal);
        Assert.Equal(8, second.HealthRestored);
        Assert.Equal(40, recipient.CurrentHealth);
        Assert.Equal(2, recipient.HealsReceivedThisRound);
    }

    [Fact]
    public void Apply_ZeroBaseHealingToLivingTarget_CountsEvent()
    {
        var recipient = Recipient();
        recipient.TakeDamage(20);

        var result = HealingApplicator.Apply(0, recipient);

        Assert.Equal(1, result.HealOrdinal);
        Assert.Equal(0, result.BaseHealing);
        Assert.Equal(0, result.AdjustedHealing);
        Assert.Equal(0, result.HealthRestored);
        Assert.Equal(32, result.ResultingHealth);
        Assert.Equal(1, recipient.HealsReceivedThisRound);
        Assert.Equal(8, HealingApplicator.Apply(11, recipient).AdjustedHealing);
    }

    [Fact]
    public void Apply_HealingRoundedToZero_StillCountsEvent()
    {
        var recipient = Recipient();
        recipient.TakeDamage(20);
        HealingApplicator.Apply(1, recipient);

        var result = HealingApplicator.Apply(1, recipient);

        Assert.Equal(2, result.HealOrdinal);
        Assert.Equal(0, result.AdjustedHealing);
        Assert.Equal(0, result.HealthRestored);
        Assert.Equal(33, recipient.CurrentHealth);
        Assert.Equal(2, recipient.HealsReceivedThisRound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetHealingHistory_RestoresFirstHealWithoutChangingHealthOrActivation(bool hasActed)
    {
        var recipient = Recipient();
        recipient.TakeDamage(40);
        if (hasActed) recipient.EndActivation();
        HealingApplicator.Apply(11, recipient);
        HealingApplicator.Apply(11, recipient);
        Assert.Equal(31, recipient.CurrentHealth);

        recipient.ResetHealingHistory();
        recipient.ResetHealingHistory();

        Assert.Equal(0, recipient.HealsReceivedThisRound);
        Assert.Equal(31, recipient.CurrentHealth);
        Assert.Equal(hasActed, recipient.HasActedThisRound);
        var result = HealingApplicator.Apply(11, recipient);
        Assert.Equal(1, result.HealOrdinal);
        Assert.Equal(11, result.HealthRestored);
        Assert.Equal(42, recipient.CurrentHealth);
    }

    [Fact]
    public void ResetActivation_DoesNotClearHealingHistory()
    {
        var recipient = Recipient();
        HealingApplicator.Apply(0, recipient);
        recipient.EndActivation();

        recipient.ResetActivation();

        Assert.False(recipient.HasActedThisRound);
        Assert.Equal(1, recipient.HealsReceivedThisRound);
    }

    [Fact]
    public void Apply_InstancesShareStatsButKeepIndependentHealingHistory()
    {
        var first = Recipient();
        var second = Recipient();
        first.TakeDamage(20);
        second.TakeDamage(20);
        HealingApplicator.Apply(0, first);
        HealingApplicator.Apply(0, first);

        var result = HealingApplicator.Apply(11, second);

        Assert.Same(first.BaseStats, second.BaseStats);
        Assert.Equal(2, first.HealsReceivedThisRound);
        Assert.Equal(1, second.HealsReceivedThisRound);
        Assert.Equal(1, result.HealOrdinal);
        Assert.Equal(11, result.HealthRestored);
        Assert.Equal(32, first.CurrentHealth);
        Assert.Equal(43, second.CurrentHealth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Apply_DefeatedTarget_DoesNotConsumeOrdinalOrRevive(bool hasHistory)
    {
        var recipient = Recipient();
        if (hasHistory) HealingApplicator.Apply(0, recipient);
        recipient.TakeDamage(recipient.CurrentHealth);

        var result = HealingApplicator.Apply(11, recipient);

        Assert.Null(result.HealOrdinal);
        Assert.Equal(11, result.BaseHealing);
        Assert.Equal(0, result.AdjustedHealing);
        Assert.Equal(0, result.HealthRestored);
        Assert.Equal(0, result.ResultingHealth);
        Assert.True(recipient.IsDefeated);
        Assert.Equal(hasHistory ? 1 : 0, recipient.HealsReceivedThisRound);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(-1, true)]
    [InlineData(int.MinValue, true)]
    public void Apply_NegativeBaseHealing_LeavesHealthAndHistoryUnchanged(int baseHealing, bool defeated)
    {
        var recipient = Recipient();
        HealingApplicator.Apply(0, recipient);
        if (defeated) recipient.TakeDamage(recipient.CurrentHealth);
        int health = recipient.CurrentHealth;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => HealingApplicator.Apply(baseHealing, recipient));

        Assert.Equal("baseHealing", error.ParamName);
        Assert.Equal(health, recipient.CurrentHealth);
        Assert.Equal(1, recipient.HealsReceivedThisRound);
    }

    [Fact]
    public void Apply_NullRecipient_IsRejected()
    {
        var error = Assert.Throws<ArgumentNullException>(() => HealingApplicator.Apply(11, null!));

        Assert.Equal("recipient", error.ParamName);
    }

    [Fact]
    public void Apply_CapsActualRestorationAtMissingHealth()
    {
        var recipient = Recipient();
        recipient.TakeDamage(1);

        var result = HealingApplicator.Apply(11, recipient);

        Assert.Equal(11, result.AdjustedHealing);
        Assert.Equal(1, result.HealthRestored);
        Assert.Equal(52, result.ResultingHealth);
        Assert.Equal(52, recipient.CurrentHealth);
        Assert.Equal(1, recipient.HealsReceivedThisRound);
    }

    [Fact]
    public void Apply_LargestAmount_CalculatesSafelyAndCountsFullHealthEvent()
    {
        var recipient = Recipient("gravebound_colossus");
        recipient.TakeDamage(63);

        var first = HealingApplicator.Apply(int.MaxValue, recipient);
        var second = HealingApplicator.Apply(int.MaxValue, recipient);

        Assert.Equal(int.MaxValue, first.AdjustedHealing);
        Assert.Equal(63, first.HealthRestored);
        Assert.Equal(1610612735, second.AdjustedHealing);
        Assert.Equal(0, second.HealthRestored);
        Assert.Equal(64, recipient.CurrentHealth);
        Assert.Equal(2, recipient.HealsReceivedThisRound);
    }

    [Fact]
    public void Apply_PreservesBaseStatsActivationAndHealerState()
    {
        var healer = Recipient("gentle_guardian");
        var recipient = Recipient("hooded_assassin");
        healer.TakeDamage(5);
        recipient.TakeDamage(20);
        healer.EndActivation();
        recipient.EndActivation();
        var healerStats = healer.BaseStats with { };
        var recipientStats = recipient.BaseStats with { };

        var result = HealingApplicator.Apply(HealingCalculator.CalculateHealingLight(healer.BaseStats), recipient);

        Assert.Equal(11, result.HealthRestored);
        Assert.Equal(29, recipient.CurrentHealth);
        Assert.Equal(39, healer.CurrentHealth);
        Assert.Equal(0, healer.HealsReceivedThisRound);
        Assert.Equal(1, recipient.HealsReceivedThisRound);
        Assert.True(healer.HasActedThisRound);
        Assert.True(recipient.HasActedThisRound);
        Assert.Equal(healerStats, healer.BaseStats);
        Assert.Equal(recipientStats, recipient.BaseStats);
    }

    [Fact]
    public void HealingResult_IsImmutableSnapshot_AndHistoryIsReadOnly()
    {
        var recipient = Recipient();
        recipient.TakeDamage(20);
        var result = HealingApplicator.Apply(11, recipient);
        recipient.TakeDamage(10);

        Assert.Equal(43, result.ResultingHealth);
        Assert.Equal(33, recipient.CurrentHealth);
        Assert.All(typeof(HealingResult).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Null(typeof(Character).GetProperty(nameof(Character.HealsReceivedThisRound))!.SetMethod);
    }

    private static Character Recipient(string id = "nigel")
    {
        var character = CharacterFactory.Create(id, 1);
        Assert.NotNull(character);
        return character;
    }
}
