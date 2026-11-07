using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using Xunit;

namespace PawnsOfDivinity.Tests.Statuses;

public class ParalysisTests
{
    // Every penalty below is an explicit prototype example, not finalized character balance.
    [Theory]
    [InlineData(15, 4, 11)]
    [InlineData(15, 0, 15)]
    [InlineData(4, 4, 0)]
    [InlineData(3, 4, 0)]
    [InlineData(0, int.MaxValue, 0)]
    [InlineData(int.MaxValue, int.MaxValue, 0)]
    [InlineData(int.MaxValue, 1, int.MaxValue - 1)]
    public void EffectiveSpeed_UsesConfiguredPenaltyOnlyWhileActive(int speed, int penalty, int expected)
    {
        var character = Unit(speed);
        Assert.Equal(speed, character.EffectiveSpeed);
        character.ConfigureParalysisSpeedReduction(penalty);
        Assert.Equal(speed, character.EffectiveSpeed);

        character.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);

        Assert.Equal(expected, character.EffectiveSpeed);
        Assert.Equal(speed, character.BaseStats.Speed);
        Assert.Equal(5, character.EffectiveMovement);
    }

    [Fact]
    public void ActiveParalysis_RequiresExplicitConfiguration_IncludingExplicitZero()
    {
        var character = Unit(15);
        character.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);

        Assert.Throws<InvalidOperationException>(() => character.EffectiveSpeed);
        character.ConfigureParalysisSpeedReduction(0);
        Assert.Equal(15, character.EffectiveSpeed);
        Assert.Null(typeof(Character).GetProperty(nameof(Character.EffectiveSpeed))!.SetMethod);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativePenalty_IsRejectedWithoutChangingExistingConfiguration(int penalty)
    {
        var character = Unit(15);
        character.ConfigureParalysisSpeedReduction(4);
        character.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => character.ConfigureParalysisSpeedReduction(penalty));

        Assert.Equal(11, character.EffectiveSpeed);
        Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Paralysis]);
    }

    [Fact]
    public void RefreshAndTwoRoundExpiration_RestoreSpeedWithoutStacking()
    {
        var character = Unit(15);
        character.ConfigureParalysisSpeedReduction(4);
        character.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        character.Statuses.AdvanceTimedStatuses();
        Assert.Equal(11, character.EffectiveSpeed);
        character.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        character.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        Assert.Single(character.Statuses.TimedStatuses);
        Assert.Equal(2, character.Statuses.TimedStatuses[StatusType.Paralysis]);
        Assert.Equal(11, character.EffectiveSpeed);
        character.Statuses.AdvanceTimedStatuses();
        Assert.Equal(11, character.EffectiveSpeed);
        character.Statuses.AdvanceTimedStatuses();

        Assert.False(character.Statuses.HasStatus(StatusType.Paralysis));
        Assert.Equal(15, character.EffectiveSpeed);
        Assert.Equal(15, character.BaseStats.Speed);
        character.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        Assert.Equal(11, character.EffectiveSpeed); // Configuration survives expiration; the penalty does not.
    }

    [Fact]
    public void Paralysis_CoexistsWithBurnAndFrostbite_WithoutChangingOtherRuntimeState()
    {
        var character = Unit(15);
        var stats = character.BaseStats;
        var snapshot = stats with { };
        character.TakeDamage(7);
        character.SetShieldHP(6);
        character.EndActivation();
        character.Statuses.ApplyTimedStatus(StatusType.Burn, 3);
        character.Statuses.ApplyTimedStatus(StatusType.Frostbite, 3);

        character.ConfigureParalysisSpeedReduction(4);
        character.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);

        Assert.Equal(11, character.EffectiveSpeed);
        Assert.Equal(3, character.EffectiveMovement);
        Assert.Equal(7, character.EffectiveDefense);
        Assert.Equal(193, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.True(character.HasActedThisRound);
        character.Statuses.AdvanceTimedStatuses();
        character.Statuses.AdvanceTimedStatuses();
        Assert.Equal(15, character.EffectiveSpeed);
        Assert.Equal(3, character.EffectiveMovement);
        Assert.Equal(7, character.EffectiveDefense);
        Assert.Equal(193, character.CurrentHealth);
        Assert.Equal(6, character.ShieldHP);
        Assert.True(character.HasActedThisRound);
        Assert.Same(stats, character.BaseStats);
        Assert.Equal(snapshot, character.BaseStats);
    }

    [Fact]
    public void CharactersSharingStats_HaveIndependentPenaltiesAndTimers()
    {
        var first = CharacterFactory.Create("nigel", 1)!;
        var second = CharacterFactory.Create("nigel", 2)!;
        Assert.Same(first.BaseStats, second.BaseStats);
        first.ConfigureParalysisSpeedReduction(4);
        second.ConfigureParalysisSpeedReduction(2);
        first.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        Assert.Equal(9, first.EffectiveSpeed);
        Assert.Equal(13, second.EffectiveSpeed);
        second.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        first.Statuses.AdvanceTimedStatuses();
        first.Statuses.AdvanceTimedStatuses();

        Assert.Equal(13, first.EffectiveSpeed);
        Assert.Equal(11, second.EffectiveSpeed);
        Assert.Equal(2, second.Statuses.TimedStatuses[StatusType.Paralysis]);
    }

    [Theory]
    [InlineData(20, 16, true, 4, FollowUpParticipant.Attacker, FollowUpParticipant.None)]
    [InlineData(16, 20, false, 4, FollowUpParticipant.Defender, FollowUpParticipant.None)]
    [InlineData(20, 20, false, 4, FollowUpParticipant.None, FollowUpParticipant.Attacker)]
    [InlineData(20, 20, true, 4, FollowUpParticipant.None, FollowUpParticipant.Defender)]
    [InlineData(20, 16, true, 8, FollowUpParticipant.Attacker, FollowUpParticipant.Defender)]
    [InlineData(16, 20, false, 8, FollowUpParticipant.Defender, FollowUpParticipant.Attacker)]
    public void Exchange_EitherParticipantCanGainOrLoseFollowUp(int attackerSpeed, int defenderSpeed,
        bool paralyzeAttacker, int penalty, FollowUpParticipant before, FollowUpParticipant after)
    {
        var attacker = Unit(attackerSpeed);
        var defender = Unit(defenderSpeed);
        var target = paralyzeAttacker ? attacker : defender;
        var request = Request(attacker, defender);
        Assert.Equal(before, Resolve(request).FollowUpBy);

        target.ConfigureParalysisSpeedReduction(penalty);
        target.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        Assert.Equal(after, FollowUpEligibility.Evaluate(attacker.EffectiveSpeed, defender.EffectiveSpeed));
        var provider = new FixedRoll();
        var result = Resolve(request, provider);

        Assert.Equal(after, result.FollowUpBy);
        Assert.Equal(after == FollowUpParticipant.None ? 2 : 3, provider.Calls);
        target.Statuses.AdvanceTimedStatuses();
        target.Statuses.AdvanceTimedStatuses();
        Assert.Equal(before, Resolve(request).FollowUpBy);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FollowUpDamageScaling_UsesEffectiveSpeedRatio_ForEitherParticipant(bool attackerFaster)
    {
        var attacker = Unit(attackerFaster ? 24 : 20);
        var defender = Unit(attackerFaster ? 20 : 24);
        var slower = attackerFaster ? defender : attacker;
        slower.ConfigureParalysisSpeedReduction(8);
        slower.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2); // 24/12 qualifies at the existing 75% tier.

        var result = Resolve(Request(attacker, defender));

        Assert.Equal(attackerFaster ? FollowUpParticipant.Attacker : FollowUpParticipant.Defender, result.FollowUpBy);
        Assert.Equal(15, result.OpeningStrike.Damage);
        Assert.Equal(15, result.CounterStrike!.Damage);
        Assert.Equal(11, result.FollowUpStrike!.Damage);
    }

    [Fact]
    public void MissingPenalty_IsRejectedBeforeExchangeRollsOrHealthChanges()
    {
        var attacker = Unit(20);
        var defender = Unit(16);
        defender.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
        var provider = new FixedRoll();

        Assert.Throws<InvalidOperationException>(() => Resolve(Request(attacker, defender), provider));

        Assert.Equal(0, provider.Calls);
        Assert.Equal(200, attacker.CurrentHealth);
        Assert.Equal(200, defender.CurrentHealth);
    }

    private static Character Unit(int speed) => new("test", "Test character", 1,
        new CharacterStats(200, 20, 10, 12, 8, 4, speed, 5));

    private static CombatExchangeRequest Request(Character attacker, Character defender) => new(
        attacker, defender, AttackType.Physical, 5, WeaponCategory.Bow, WeaponCategory.Bow, 100,
        AttackType.Physical, 5, 100, 1, 3, 1, true, false, true);

    private static CombatExchangeResult Resolve(CombatExchangeRequest request, FixedRoll? provider = null)
        => new CombatExchangeResolver(new SingleStrikeResolver(new HitRoller(provider ?? new FixedRoll()))).Resolve(request);

    private sealed class FixedRoll : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }
}
