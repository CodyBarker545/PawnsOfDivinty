using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Turns;

public class RoundLifecycleManagerTests
{
    private static readonly RoundPhase[] Phases =
    {
        RoundPhase.Shop, RoundPhase.PoisonBatch, RoundPhase.DelayedAttacks,
        RoundPhase.TimedStatusUpdate, RoundPhase.OtherRoundStartTriggers, RoundPhase.Preparation,
        RoundPhase.AlternatingActivations, RoundPhase.Cleanup, RoundPhase.ObjectiveScoring
    };

    [Fact]
    public void DefaultLifecycle_StartsAtRoundOneShop_WithReadOnlyState()
    {
        var lifecycle = new RoundLifecycleManager();

        Assert.Equal(1, lifecycle.CurrentRound);
        Assert.Equal(RoundPhase.Shop, lifecycle.CurrentPhase);
        Assert.Equal(Phases, Enum.GetValues<RoundPhase>());
        Assert.False(typeof(RoundLifecycleManager).GetProperty(nameof(RoundLifecycleManager.CurrentRound))!.SetMethod!.IsPublic);
        Assert.False(typeof(RoundLifecycleManager).GetProperty(nameof(RoundLifecycleManager.CurrentPhase))!.SetMethod!.IsPublic);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void ExplicitInitialRound_IsPreserved(int initialRound)
    {
        var lifecycle = new RoundLifecycleManager(initialRound);

        Assert.Equal(initialRound, lifecycle.CurrentRound);
        Assert.Equal(RoundPhase.Shop, lifecycle.CurrentPhase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void InvalidInitialRound_IsRejected(int initialRound)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RoundLifecycleManager(initialRound));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothAdvancementMethods_FollowExactDocumentedOrder(bool explicitTarget)
    {
        var lifecycle = new RoundLifecycleManager();
        for (int i = 1; i < Phases.Length; i++)
        {
            if (explicitTarget) lifecycle.AdvanceTo(Phases[i]);
            else lifecycle.AdvancePhase();
            Assert.Equal(Phases[i], lifecycle.CurrentPhase);
            Assert.Equal(1, lifecycle.CurrentRound);
        }

        Assert.Throws<InvalidOperationException>(() => lifecycle.AdvancePhase());
        Assert.Throws<InvalidOperationException>(() => lifecycle.AdvanceTo(RoundPhase.Shop));
        Assert.Equal(RoundPhase.ObjectiveScoring, lifecycle.CurrentPhase);
        Assert.Equal(1, lifecycle.CurrentRound);
    }

    public static IEnumerable<object[]> InvalidTransitions()
    {
        for (int from = 0; from < Phases.Length; from++)
            for (int to = 0; to < Phases.Length; to++)
                if (to != from + 1) yield return new object[] { from, Phases[to] };
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void SkippedBackwardRepeatedAndWrappedTransitions_AreRejected(int phaseIndex, RoundPhase target)
    {
        var lifecycle = AtPhase(phaseIndex);

        Assert.Throws<InvalidOperationException>(() => lifecycle.AdvanceTo(target));

        Assert.Equal(Phases[phaseIndex], lifecycle.CurrentPhase);
        Assert.Equal(1, lifecycle.CurrentRound);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidPhase_IsRejectedWithoutChangingState(int invalid)
    {
        var lifecycle = new RoundLifecycleManager();

        Assert.Throws<ArgumentOutOfRangeException>(() => lifecycle.AdvanceTo((RoundPhase)invalid));

        Assert.Equal(RoundPhase.Shop, lifecycle.CurrentPhase);
        Assert.Equal(1, lifecycle.CurrentRound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void NewRound_CannotStartBeforeFinalPhase(int phaseIndex)
    {
        var lifecycle = AtPhase(phaseIndex);

        Assert.Throws<InvalidOperationException>(() => lifecycle.StartNextRound());

        Assert.Equal(Phases[phaseIndex], lifecycle.CurrentPhase);
        Assert.Equal(1, lifecycle.CurrentRound);
    }

    [Fact]
    public void ExplicitNewRound_IncrementsOnceAndReturnsToShop()
    {
        var lifecycle = AtPhase(8);

        lifecycle.StartNextRound();

        Assert.Equal(2, lifecycle.CurrentRound);
        Assert.Equal(RoundPhase.Shop, lifecycle.CurrentPhase);
        Assert.Throws<InvalidOperationException>(() => lifecycle.StartNextRound());
        Assert.Equal(2, lifecycle.CurrentRound);
    }

    [Fact]
    public void RoundOverflow_IsRejectedBeforeChangingPhase()
    {
        var lifecycle = new RoundLifecycleManager(int.MaxValue);
        for (int i = 1; i < Phases.Length; i++) lifecycle.AdvancePhase();

        Assert.Throws<OverflowException>(() => lifecycle.StartNextRound());

        Assert.Equal(int.MaxValue, lifecycle.CurrentRound);
        Assert.Equal(RoundPhase.ObjectiveScoring, lifecycle.CurrentPhase);
    }

    [Fact]
    public void ConnectedActivationManager_UsesLifecycleRound_AndCannotAdvanceItIndependently()
    {
        var first = Unit(1);
        var second = Unit(2);
        first.EndActivation();
        second.EndActivation();
        var lifecycle = new RoundLifecycleManager(7);
        var activations = new ActivationManager(new[] { first }, new[] { second }, 1, lifecycle);
        Assert.Equal(7, activations.CurrentRound);

        Assert.Throws<InvalidOperationException>(() => activations.StartNextRound(1));
        for (int i = 1; i < Phases.Length; i++) lifecycle.AdvancePhase();
        lifecycle.StartNextRound();

        Assert.Equal(8, lifecycle.CurrentRound);
        Assert.Equal(8, activations.CurrentRound);
        Assert.Equal(RoundPhase.Shop, lifecycle.CurrentPhase);
        Assert.True(first.HasActedThisRound);
        Assert.True(second.HasActedThisRound);
        Assert.True(activations.IsRoundComplete);
    }

    [Fact]
    public void PhaseMarkers_DoNotExecuteEffectsResetCharactersOrCloseAnActiveActivation()
    {
        var first = Unit(1);
        var second = Unit(2);
        first.TakeDamage(7);
        HealingApplicator.Apply(1, first);
        first.SetShieldHP(6);
        first.Statuses.ApplyPoison();
        first.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        second.EndActivation();
        var stats = first.BaseStats;
        var snapshot = stats with { };
        var lifecycle = new RoundLifecycleManager();
        var activations = new ActivationManager(new[] { first }, new[] { second }, 1, lifecycle);
        activations.StartActivation(first);
        activations.RecordUtilityAction();

        foreach (var phase in Phases.Skip(1))
        {
            lifecycle.AdvanceTo(phase);
            Assert.Equal(stats.MaxHealth - 6, first.CurrentHealth);
            Assert.Equal(6, first.ShieldHP);
            Assert.Equal(0, first.Statuses.PoisonTicksOccurred);
            Assert.True(first.Statuses.IsPoisoned);
            Assert.Equal(2, first.Statuses.TimedStatuses[StatusType.Burn]);
            Assert.Equal(1, first.HealsReceivedThisRound);
            Assert.False(first.HasActedThisRound);
            Assert.True(second.HasActedThisRound);
            Assert.Same(first, activations.ActiveCharacter);
            Assert.True(activations.UtilityActionUsed);
            Assert.Same(stats, first.BaseStats);
            Assert.Equal(snapshot, first.BaseStats);
        }
        lifecycle.StartNextRound();
        Assert.Same(first, activations.ActiveCharacter); // Phase tracking alone does not run activation preparation.
        Assert.True(second.HasActedThisRound);
        Assert.Equal(2, activations.CurrentRound);
    }

    [Fact]
    public void RepeatedExplicitTransitions_AreDeterministicAcrossIndependentLifecycles()
    {
        var first = new RoundLifecycleManager();
        var second = new RoundLifecycleManager();
        for (int round = 1; round <= 3; round++)
        {
            foreach (var phase in Phases)
            {
                Assert.Equal(round, first.CurrentRound);
                Assert.Equal(first.CurrentRound, second.CurrentRound);
                Assert.Equal(phase, first.CurrentPhase);
                Assert.Equal(first.CurrentPhase, second.CurrentPhase);
                if (phase != RoundPhase.ObjectiveScoring)
                {
                    first.AdvancePhase();
                    second.AdvancePhase();
                }
            }
            first.StartNextRound();
            second.StartNextRound();
        }
        Assert.Equal(4, first.CurrentRound);
        Assert.Equal(RoundPhase.Shop, first.CurrentPhase);
    }

    private static RoundLifecycleManager AtPhase(int index)
    {
        var lifecycle = new RoundLifecycleManager();
        for (int i = 0; i < index; i++) lifecycle.AdvancePhase();
        return lifecycle;
    }

    private static Character Unit(int team) => CharacterFactory.Create("nigel", team)!;
}
