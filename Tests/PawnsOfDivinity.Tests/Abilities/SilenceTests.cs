using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class SilenceTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Eligibility_DependsOnlyOnSilenceAndSpellTag(bool silenced, bool isSpellTagged, bool expected)
    {
        var character = Create();
        if (silenced) character.Statuses.ApplyTimedStatus(StatusType.Silence, 1);

        Assert.Equal(expected, SilenceEligibility.CanUseAbility(character, isSpellTagged));
    }

    [Fact]
    public void OneRoundSilence_ExpiresAtNextRoundStart_AndRestoresSpellEligibility()
    {
        var character = Create();
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1); // Applied during R2.
        Assert.False(SilenceEligibility.CanUseAbility(character, true));
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Silence]);

        character.Statuses.AdvanceTimedStatuses(); // Start R3, after Poison and delayed damage.

        Assert.False(character.Statuses.HasStatus(StatusType.Silence));
        Assert.True(SilenceEligibility.CanUseAbility(character, true));
    }

    [Fact]
    public void Reapplication_RefreshesRemainingDurationWithoutStacking()
    {
        var character = Create();
        // Start with a longer supplied timer to verify refresh replaces rather than adds duration.
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 2);
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1);

        Assert.Single(character.Statuses.TimedStatuses);
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Silence]);
        Assert.False(SilenceEligibility.CanUseAbility(character, true));
        character.Statuses.AdvanceTimedStatuses();
        Assert.True(SilenceEligibility.CanUseAbility(character, true));
    }

    [Fact]
    public void Silence_CoexistsWithOtherStatuses_AndExpiresIndependently()
    {
        var character = Create();
        character.ConfigureParalysisSpeedReduction(4); // Prototype example, not finalized balance.
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        character.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        character.Statuses.ApplyPoison();
        Assert.True(SilenceEligibility.CanUseAbility(character, true));
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1);

        Assert.False(SilenceEligibility.CanUseAbility(character, true));
        Assert.True(SilenceEligibility.CanUseAbility(character, false));
        character.Statuses.AdvanceTimedStatuses();

        Assert.True(SilenceEligibility.CanUseAbility(character, true));
        Assert.True(character.Statuses.HasStatus(StatusType.Burn));
        Assert.True(character.Statuses.HasStatus(StatusType.Frostbite));
        Assert.True(character.Statuses.HasStatus(StatusType.Paralysis));
        Assert.True(character.Statuses.IsPoisoned);
        Assert.Equal(0, character.Statuses.PoisonTicksOccurred);
    }

    [Fact]
    public void CharactersSharingStats_HaveIndependentSilenceState()
    {
        var first = Create();
        var second = Create();
        Assert.Same(first.BaseStats, second.BaseStats);
        first.Statuses.ApplyTimedStatus(StatusType.Silence, 1);

        Assert.False(SilenceEligibility.CanUseAbility(first, true));
        Assert.True(SilenceEligibility.CanUseAbility(second, true));
        first.Statuses.AdvanceTimedStatuses();
        Assert.True(SilenceEligibility.CanUseAbility(first, true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullCharacter_IsRejectedEvenForNonSpellAbilities(bool isSpellTagged)
    {
        var error = Assert.Throws<ArgumentNullException>(() => SilenceEligibility.CanUseAbility(null!, isSpellTagged));

        Assert.Equal("character", error.ParamName);
    }

    [Fact]
    public void RepeatedEvaluation_IsDeterministic_AndLeavesRuntimeStateUnchanged()
    {
        var character = Create();
        character.TakeDamage(5);
        character.SetShieldHP(6);
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        var stats = character.BaseStats;
        var snapshot = stats with { };
        int health = character.CurrentHealth;

        for (int i = 0; i < 10; i++)
        {
            Assert.False(SilenceEligibility.CanUseAbility(character, true));
            Assert.True(SilenceEligibility.CanUseAbility(character, false));
        }

        Assert.True(character.CanAct);
        Assert.False(character.HasActedThisRound);
        character.EndActivation();
        Assert.True(SilenceEligibility.CanUseAbility(character, false));
        Assert.True(character.HasActedThisRound); // This evaluator does not grant an activation.
        Assert.Equal(health, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.Equal(1, character.Statuses.TimedStatuses[StatusType.Silence]);
        Assert.Equal(stats.Movement, character.EffectiveMovement);
        Assert.Equal(stats.Speed, character.EffectiveSpeed);
        Assert.Equal(stats.Defense, character.EffectiveDefense);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(snapshot, character.BaseStats);
    }

    [Theory]
    [InlineData("hooded_assassin")]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("nigel")]
    [InlineData("gravebound_colossus")]
    [InlineData("beothuk_spirit_guardian")]
    public void Restriction_IsReusableAcrossTheRoster(string id)
    {
        var character = CharacterFactory.Create(id, 1)!;
        Assert.True(SilenceEligibility.CanUseAbility(character, true));
        character.Statuses.ApplyTimedStatus(StatusType.Silence, 1);

        Assert.False(SilenceEligibility.CanUseAbility(character, true));
        Assert.True(SilenceEligibility.CanUseAbility(character, false));
    }

    private static Character Create() => CharacterFactory.Create("gentle_guardian", 1)!;
}
