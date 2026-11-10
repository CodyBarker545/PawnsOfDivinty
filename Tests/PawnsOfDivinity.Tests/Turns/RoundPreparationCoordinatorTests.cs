using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Turns;

public class RoundPreparationCoordinatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Preparation_ResetsActivationAndHealingHistory_AndUsesExplicitStartingTeam(int startingTeam)
    {
        var first = Unit(1);
        var second = Unit(2);
        first.EndActivation();
        second.EndActivation();
        HealingApplicator.Apply(0, first);
        HealingApplicator.Apply(0, first);
        HealingApplicator.Apply(0, second);
        var lifecycle = Ready();
        var activations = Manager(first, second, lifecycle);

        var result = new RoundPreparationCoordinator(activations, lifecycle).Process(new[] { second, first }, startingTeam);

        Assert.Equal(3, result.RoundNumber);
        Assert.Equal(startingTeam, result.StartingTeamId);
        Assert.Equal(new[] { second.Id, first.Id }, result.Entries.Select(entry => entry.CharacterId));
        Assert.All(result.Entries, entry => Assert.True(entry.CanAct));
        Assert.True(first.CanAct);
        Assert.True(second.CanAct);
        Assert.Equal(0, first.HealsReceivedThisRound);
        Assert.Equal(0, second.HealsReceivedThisRound);
        Assert.Equal(startingTeam, activations.NextTeamId);
        Assert.Equal(3, activations.CurrentRound);
        Assert.Equal(RoundPhase.Preparation, lifecycle.CurrentPhase);
        lifecycle.AdvancePhase();
        activations.StartActivation(startingTeam == 1 ? first : second);
        Assert.True(activations.CanMove);
        Assert.True(activations.CanUseUtilityAction);
        Assert.True(activations.CanUseMainAction);
    }

    [Fact]
    public void DefeatedCharacters_RemainIneligible_WithoutRevival_AndHistoryResets()
    {
        var first = Unit(1);
        var second = Unit(2);
        HealingApplicator.Apply(0, first);
        first.EndActivation();
        first.Cooldowns.RecordUse("ability", 2, 3);
        first.TakeDamage(int.MaxValue);
        second.EndActivation();
        var lifecycle = Ready();
        var activations = Manager(first, second, lifecycle);

        var result = new RoundPreparationCoordinator(activations, lifecycle).Process(new[] { first, second }, 1);

        Assert.False(result.Entries[0].CanAct);
        Assert.True(result.Entries[1].CanAct);
        Assert.True(first.IsDefeated);
        Assert.Equal(0, first.CurrentHealth);
        Assert.Equal(0, first.HealsReceivedThisRound);
        Assert.False(first.CanAct);
        Assert.Equal(2, activations.NextTeamId);
        Assert.Equal(5, first.Cooldowns.Cooldowns["ability"].ReadyRound);
    }

    [Fact]
    public void Preparation_PreservesHpOwnedShieldsStatusesCooldownDeadlinesAndStats()
    {
        var first = Unit(1);
        var second = Unit(2);
        first.TakeDamage(10);
        HealingApplicator.Apply(1, first);
        first.EndActivation();
        first.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        first.Statuses.ApplyPoison();
        first.Cooldowns.RecordUse("ability", 2, 3);
        var cooldown = first.Cooldowns.Cooldowns["ability"];
        var shields = new SanctuaryShieldManager();
        shields.Apply(second, first);
        var lifecycle = Ready(new[] { first, second }); // Prior phases handle Poison and timed durations themselves.
        var stats = first.BaseStats;
        var snapshot = stats with { };
        int health = first.CurrentHealth;
        int shield = first.ShieldHP;
        int poisonTicks = first.Statuses.PoisonTicksOccurred;
        int burnDuration = first.Statuses.TimedStatuses[StatusType.Burn];

        new RoundPreparationCoordinator(Manager(first, second, lifecycle), lifecycle).Process(new[] { first, second }, 1);

        Assert.Equal(health, first.CurrentHealth);
        Assert.Equal(shield, first.ShieldHP);
        Assert.Same(first, shields.GetRecipient(second));
        Assert.Equal(burnDuration, first.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(poisonTicks, first.Statuses.PoisonTicksOccurred);
        Assert.True(first.Statuses.IsPoisoned);
        Assert.Same(cooldown, first.Cooldowns.Cooldowns["ability"]);
        Assert.False(first.Cooldowns.IsReady("ability", 3));
        Assert.True(first.Cooldowns.IsReady("ability", 5));
        Assert.Same(stats, first.BaseStats);
        Assert.Equal(snapshot, first.BaseStats);
        Assert.Equal(0, first.HealsReceivedThisRound);
        Assert.True(first.CanAct);
    }

    [Fact]
    public void DuplicatePreparation_IsRejectedAcrossCoordinators_WithoutResettingNewState()
    {
        var first = Unit(1);
        var second = Unit(2);
        var lifecycle = Ready();
        var activations = Manager(first, second, lifecycle);
        var coordinator = new RoundPreparationCoordinator(activations, lifecycle);
        coordinator.Process(new[] { first, second }, 1);
        first.EndActivation();
        HealingApplicator.Apply(0, first);

        Assert.Throws<InvalidOperationException>(() => coordinator.Process(new[] { first, second }, 1));
        Assert.Throws<InvalidOperationException>(() => new RoundPreparationCoordinator(activations, lifecycle)
            .Process(new[] { first, second }, 2));

        Assert.True(first.HasActedThisRound);
        Assert.Equal(1, first.HealsReceivedThisRound);
    }

    [Fact]
    public void NextRound_PreparationBecomesAvailable_WithoutDoubleIncrementingRound()
    {
        var first = Unit(1);
        var second = Unit(2);
        var lifecycle = Ready();
        var activations = Manager(first, second, lifecycle);
        var coordinator = new RoundPreparationCoordinator(activations, lifecycle);
        coordinator.Process(new[] { first, second }, 1);
        lifecycle.AdvancePhase();
        activations.StartActivation(first);
        activations.CompleteMainAction();
        activations.StartActivation(second);
        activations.CompleteMainAction();
        while (lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) lifecycle.AdvancePhase();
        lifecycle.StartNextRound();
        EnterPreparation(lifecycle);

        var result = coordinator.Process(new[] { first, second }, 2);

        Assert.Equal(4, result.RoundNumber);
        Assert.Equal(4, lifecycle.CurrentRound);
        Assert.Equal(4, activations.CurrentRound);
        Assert.True(first.CanAct);
        Assert.True(second.CanAct);
        Assert.Equal(2, activations.NextTeamId);
    }

    [Theory]
    [InlineData(RoundPhase.Shop)]
    [InlineData(RoundPhase.PoisonBatch)]
    [InlineData(RoundPhase.DelayedAttacks)]
    [InlineData(RoundPhase.TimedStatusUpdate)]
    [InlineData(RoundPhase.OtherRoundStartTriggers)]
    [InlineData(RoundPhase.AlternatingActivations)]
    [InlineData(RoundPhase.Cleanup)]
    [InlineData(RoundPhase.ObjectiveScoring)]
    public void WrongPhase_IsRejectedWithoutAnyReset(RoundPhase phase)
    {
        var first = Unit(1);
        var second = Unit(2);
        first.EndActivation();
        HealingApplicator.Apply(0, first);
        var lifecycle = new RoundLifecycleManager(3);
        while (lifecycle.CurrentPhase != phase) lifecycle.AdvancePhase();

        Assert.Throws<InvalidOperationException>(() => new RoundPreparationCoordinator(Manager(first, second, lifecycle), lifecycle)
            .Process(new[] { first, second }, 1));

        Assert.True(first.HasActedThisRound);
        Assert.Equal(1, first.HealsReceivedThisRound);
        Assert.Equal(phase, lifecycle.CurrentPhase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MissingEarlierProcessing_IsRejected(int completedCount)
    {
        var first = Unit(1);
        var second = Unit(2);
        first.EndActivation();
        var lifecycle = new RoundLifecycleManager(3);
        lifecycle.AdvancePhase();
        if (completedCount >= 1) new PoisonBatchProcessor(lifecycle).Process(Array.Empty<Character>());
        lifecycle.AdvancePhase();
        if (completedCount >= 2) new DelayedAttackProcessor(new DelayedAttackScheduler(), lifecycle).Process(Array.Empty<Character>());
        lifecycle.AdvancePhase();
        lifecycle.AdvancePhase(); // Other triggers: no effects exist yet.
        lifecycle.AdvancePhase(); // Preparation.

        Assert.Throws<InvalidOperationException>(() => new RoundPreparationCoordinator(Manager(first, second, lifecycle), lifecycle)
            .Process(new[] { first, second }, 1));
        Assert.True(first.HasActedThisRound);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("nullMember")]
    [InlineData("duplicateInstance")]
    [InlineData("duplicateId")]
    [InlineData("missingMember")]
    [InlineData("foreignMember")]
    [InlineData("impostor")]
    [InlineData("invalidTeam")]
    public void InvalidInputs_FailBeforeMutation_AndDoNotConsumeGuard(string invalid)
    {
        var first = Unit(1);
        var second = Unit(2);
        first.EndActivation();
        second.EndActivation();
        HealingApplicator.Apply(0, first);
        var lifecycle = Ready();
        var coordinator = new RoundPreparationCoordinator(Manager(first, second, lifecycle), lifecycle);
        var impostor = new Character(first.Id, first.Name, first.TeamId, first.BaseStats);
        IEnumerable<Character> participants = invalid switch
        {
            "null" => null!,
            "nullMember" => new[] { first, null! },
            "duplicateInstance" => new[] { first, first },
            "duplicateId" => new[] { first, impostor },
            "missingMember" => new[] { first },
            "foreignMember" => new[] { first, Unit(2) },
            "impostor" => new[] { impostor, second },
            _ => new[] { first, second }
        };

        Assert.ThrowsAny<ArgumentException>(() => coordinator.Process(participants, invalid == "invalidTeam" ? 99 : 1));

        Assert.True(first.HasActedThisRound);
        Assert.True(second.HasActedThisRound);
        Assert.Equal(1, first.HealsReceivedThisRound);
        coordinator.Process(new[] { first, second }, 1);
        Assert.True(first.CanAct);
        Assert.Equal(0, first.HealsReceivedThisRound);
    }

    [Fact]
    public void ActiveActivation_IsRejectedWithoutInterruptingOrResettingIt()
    {
        var first = Unit(1);
        var second = Unit(2);
        var lifecycle = Ready();
        var activations = Manager(first, second, lifecycle);
        activations.StartActivation(first);
        activations.RecordMovement();
        HealingApplicator.Apply(0, first);
        var coordinator = new RoundPreparationCoordinator(activations, lifecycle);

        Assert.Throws<InvalidOperationException>(() => coordinator.Process(new[] { first, second }, 1));

        Assert.Same(first, activations.ActiveCharacter);
        Assert.True(activations.MovementUsed);
        Assert.Equal(1, first.HealsReceivedThisRound);
        activations.CompleteActivation();
        coordinator.Process(new[] { first, second }, 1);
        Assert.True(first.CanAct);
    }

    [Fact]
    public void StandaloneOrDifferentLifecycleActivationManager_IsRejectedWithoutReset()
    {
        var first = Unit(1);
        var second = Unit(2);
        first.EndActivation();
        var lifecycle = Ready();
        foreach (var manager in new[]
        {
            new ActivationManager(new[] { first }, new[] { second }, 1),
            Manager(first, second, Ready())
        })
        {
            Assert.Throws<InvalidOperationException>(() => new RoundPreparationCoordinator(manager, lifecycle)
                .Process(new[] { first, second }, 1));
            Assert.True(first.HasActedThisRound);
        }
    }

    [Fact]
    public void Result_IsAnImmutableSnapshot_AndOtherMatchInstancesRemainIndependent()
    {
        var first = Unit(1);
        var second = Unit(2);
        var unrelated = Unit(1);
        unrelated.EndActivation();
        HealingApplicator.Apply(0, unrelated);
        var lifecycle = Ready();
        var result = new RoundPreparationCoordinator(Manager(first, second, lifecycle), lifecycle)
            .Process(new[] { second, first }, 1);
        first.EndActivation();
        first.TakeDamage(int.MaxValue);

        Assert.True(result.Entries[1].CanAct);
        Assert.True(unrelated.HasActedThisRound);
        Assert.Equal(1, unrelated.HealsReceivedThisRound);
        Assert.Equal(unrelated.BaseStats.MaxHealth, unrelated.CurrentHealth);
        Assert.Throws<NotSupportedException>(() => ((IList<PreparedCharacter>)result.Entries).Clear());
        Assert.All(typeof(RoundPreparationResult).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(PreparedCharacter).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void NullDependencies_AreRejected()
    {
        var lifecycle = Ready();
        Assert.Throws<ArgumentNullException>(() => new RoundPreparationCoordinator(null!, lifecycle));
        Assert.Throws<ArgumentNullException>(() => new RoundPreparationCoordinator(Manager(Unit(1), Unit(2), lifecycle), null!));
    }

    private static Character Unit(int team) => CharacterFactory.Create("nigel", team)!;
    private static ActivationManager Manager(Character first, Character second, RoundLifecycleManager lifecycle)
        => new(new[] { first }, new[] { second }, first.TeamId, lifecycle);
    private static RoundLifecycleManager Ready(IEnumerable<Character>? roster = null)
    {
        var lifecycle = new RoundLifecycleManager(3);
        EnterPreparation(lifecycle, roster);
        return lifecycle;
    }
    private static void EnterPreparation(RoundLifecycleManager lifecycle, IEnumerable<Character>? roster = null)
    {
        var participants = roster?.ToArray() ?? Array.Empty<Character>();
        lifecycle.AdvancePhase();
        new PoisonBatchProcessor(lifecycle).Process(participants);
        lifecycle.AdvancePhase();
        new DelayedAttackProcessor(new DelayedAttackScheduler(), lifecycle).Process(participants);
        lifecycle.AdvancePhase();
        new TimedStatusBatchProcessor(lifecycle).Process(participants);
        lifecycle.AdvancePhase(); // Other Triggers has no implemented effects.
        lifecycle.AdvancePhase();
    }
}
