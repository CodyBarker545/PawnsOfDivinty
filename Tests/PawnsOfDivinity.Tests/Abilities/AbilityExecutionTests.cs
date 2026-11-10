using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class AbilityExecutionTests
{
    [Theory]
    [InlineData(AbilityActionType.MainAction)]
    [InlineData(AbilityActionType.UtilityAction)]
    public void EligibleActiveAbility_CheckAndSelectionConsumeNothing(AbilityActionType action)
    {
        var setup = Setup(Ability(action: action));

        var result = AbilityEligibility.Evaluate(setup.Actor, "example", setup.Activations);
        _ = new AbilityUse(setup.Actor, "example", setup.Activations);

        Assert.True(result.IsAllowed);
        Assert.Equal(AbilityRejectionReason.None, result.RejectionReason);
        Assert.Equal(setup.Actor.Id, result.CharacterId);
        Assert.Equal("example", result.AbilityId);
        Assert.Empty(setup.Actor.Cooldowns.Cooldowns);
        Assert.True(setup.Activations.CanMove);
        Assert.True(setup.Activations.CanUseUtilityAction);
        Assert.True(setup.Activations.CanUseMainAction);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void CooldownReadiness_UsesCurrentRoundAndExactDeadline(int round, bool ready)
    {
        var setup = Setup(Ability(), round);
        setup.Actor.Cooldowns.RecordUse("example", 2, 3);

        var result = AbilityEligibility.Evaluate(setup.Actor, "example", setup.Activations);

        Assert.Equal(ready, result.IsAllowed);
        Assert.Equal(ready ? AbilityRejectionReason.None : AbilityRejectionReason.OnCooldown, result.RejectionReason);
        Assert.Equal(5, setup.Actor.Cooldowns.Cooldowns["example"].ReadyRound);
    }

    [Theory]
    [InlineData(true, AbilityRejectionReason.BlockedBySilence)]
    [InlineData(false, AbilityRejectionReason.None)]
    [InlineData(null, AbilityRejectionReason.MetadataUnresolved)]
    public void Silence_UsesConfirmedSpellTag_AndRejectsUnresolvedCompatibility(bool? spell, AbilityRejectionReason reason)
    {
        var setup = Setup(Ability(spell: spell));
        setup.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1);

        var result = AbilityEligibility.Evaluate(setup.Actor, "example", setup.Activations);

        Assert.Equal(reason, result.RejectionReason);
    }

    [Fact]
    public void UnknownSpellTag_WithoutSilence_DoesNotInventATagOrBlockAnOtherwiseValidAbility()
    {
        var ability = Ability(spell: null);
        var setup = Setup(ability);

        Assert.True(AbilityEligibility.Evaluate(setup.Actor, "example", setup.Activations).IsAllowed);
        Assert.Null(ability.IsSpellTagged);
    }

    [Theory]
    [InlineData("classification")]
    [InlineData("action")]
    [InlineData("reuse")]
    public void RequiredUnresolvedMetadata_IsRejected(string missing)
    {
        var ability = new AbilityDefinition("example", "Example", "Test metadata.",
            missing == "classification" ? null : AbilityClassification.Active, false,
            missing == "action" ? null : AbilityActionType.MainAction,
            reuseInterval: missing == "reuse" ? null : 3);
        var setup = Setup(ability);

        Assert.Equal(AbilityRejectionReason.MetadataUnresolved,
            AbilityEligibility.Evaluate(setup.Actor, "example", setup.Activations).RejectionReason);
    }

    [Theory]
    [InlineData(AbilityClassification.Passive)]
    [InlineData(AbilityClassification.Reaction)]
    public void PassiveAndReactionAbilities_CannotBeManuallyActivated(AbilityClassification classification)
    {
        var setup = Setup(new AbilityDefinition("example", "Example", "Test metadata.", classification,
            false, AbilityActionType.MainAction, reuseInterval: 0));

        Assert.Equal(AbilityRejectionReason.NotManuallyActivatable,
            AbilityEligibility.Evaluate(setup.Actor, "example", setup.Activations).RejectionReason);
    }

    [Fact]
    public void NoActionRequirement_IsNotAssumedToBeAFreeActiveAbility()
    {
        var setup = Setup(Ability(action: AbilityActionType.None));

        Assert.Equal(AbilityRejectionReason.UnsupportedActionRequirement,
            AbilityEligibility.Evaluate(setup.Actor, "example", setup.Activations).RejectionReason);
    }

    [Fact]
    public void DefeatedAndNonActiveCharacters_AreRejected()
    {
        var setup = Setup(Ability());
        Assert.Equal(AbilityRejectionReason.CharacterNotActive,
            AbilityEligibility.Evaluate(setup.Other, "example", setup.Activations).RejectionReason);
        setup.Actor.TakeDamage(int.MaxValue);
        Assert.Equal(AbilityRejectionReason.CharacterDefeated,
            AbilityEligibility.Evaluate(setup.Actor, "example", setup.Activations).RejectionReason);
    }

    [Fact]
    public void UnknownIdsAndDisplayNames_AreNotOwnedAbilities_AndFactoryRetainsDefinition()
    {
        var setup = Setup(Ability());
        Assert.NotNull(setup.Actor.Definition);
        Assert.Equal("example", setup.Actor.Definition.Abilities[0].Id);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned,
            AbilityEligibility.Evaluate(setup.Actor, "unknown", setup.Activations).RejectionReason);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned,
            AbilityEligibility.Evaluate(setup.Actor, "Example", setup.Activations).RejectionReason);
    }

    [Fact]
    public void LegacyStatsOnlyCharacter_DoesNotAcquireAbilitiesFromItsName()
    {
        var legacy = new Character("legacy", "Example", 1, Stats());
        var manager = new ActivationManager(new[] { legacy }, new[] { CharacterFactory.Create("nigel", 2)! }, 1);
        manager.StartActivation(legacy);

        Assert.Null(legacy.Definition);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, AbilityEligibility.Evaluate(legacy, "example", manager).RejectionReason);
    }

    [Fact]
    public void UsedUtilityBudget_IsRejected_WithoutCooldownChanges()
    {
        var setup = Setup(Ability(action: AbilityActionType.UtilityAction));
        var use = new AbilityUse(setup.Actor, "example", setup.Activations);
        setup.Activations.RecordUtilityAction();

        var result = AbilityUseFinalizer.FinalizeSuccessfulUse(use);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityRejectionReason.ActionUnavailable, result.Eligibility.RejectionReason);
        Assert.Empty(setup.Actor.Cooldowns.Cooldowns);
        Assert.True(setup.Activations.CanMove);
        Assert.True(setup.Activations.CanUseMainAction);
    }

    [Fact]
    public void CompletedMainAction_PreventsFurtherManualUse()
    {
        var setup = Setup(Ability());
        var use = new AbilityUse(setup.Actor, "example", setup.Activations);
        setup.Activations.CompleteMainAction();

        var result = AbilityUseFinalizer.FinalizeSuccessfulUse(use);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityRejectionReason.CharacterNotActive, result.Eligibility.RejectionReason);
        Assert.Empty(setup.Actor.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData(AbilityActionType.MainAction)]
    [InlineData(AbilityActionType.UtilityAction)]
    public void SuccessfulFinalization_RecordsCooldownAndConsumesOnlyRequiredAction(AbilityActionType action)
    {
        var setup = Setup(Ability(action: action));
        var use = new AbilityUse(setup.Actor, "example", setup.Activations);

        var result = AbilityUseFinalizer.FinalizeSuccessfulUse(use); // Caller reports its effect has already succeeded.

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RoundNumber);
        Assert.Equal(5, result.ReadyRound);
        Assert.Equal(3, setup.Actor.Cooldowns.Cooldowns["example"].ReuseInterval);
        Assert.Equal(action == AbilityActionType.MainAction, setup.Actor.HasActedThisRound);
        Assert.Equal(action == AbilityActionType.MainAction, setup.Activations.MainActionUsed);
        Assert.Equal(action == AbilityActionType.UtilityAction, setup.Activations.UtilityActionUsed);
        if (action == AbilityActionType.UtilityAction)
        {
            Assert.Same(setup.Actor, setup.Activations.ActiveCharacter);
            Assert.True(setup.Activations.CanMove);
            Assert.True(setup.Activations.CanUseMainAction);
        }
        else
        {
            Assert.Null(setup.Activations.ActiveCharacter);
            Assert.Equal(2, setup.Activations.NextTeamId);
        }
    }

    [Fact]
    public void RepeatedFinalizationOfSameUse_IsRejected_EvenWithZeroReuse()
    {
        var setup = Setup(Ability(action: AbilityActionType.UtilityAction, reuse: 0));
        var use = new AbilityUse(setup.Actor, "example", setup.Activations);
        Assert.True(AbilityUseFinalizer.FinalizeSuccessfulUse(use).Succeeded);
        var cooldown = setup.Actor.Cooldowns.Cooldowns["example"];

        var repeated = AbilityUseFinalizer.FinalizeSuccessfulUse(use);

        Assert.False(repeated.Succeeded);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, repeated.Eligibility.RejectionReason);
        Assert.Same(cooldown, setup.Actor.Cooldowns.Cooldowns["example"]);
    }

    [Fact]
    public void FailedFinalization_RechecksSilence_AndConsumesNothing()
    {
        var setup = Setup(Ability(spell: true));
        var use = new AbilityUse(setup.Actor, "example", setup.Activations);
        Assert.True(AbilityEligibility.Evaluate(setup.Actor, "example", setup.Activations).IsAllowed);
        setup.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1);

        var result = AbilityUseFinalizer.FinalizeSuccessfulUse(use);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityRejectionReason.BlockedBySilence, result.Eligibility.RejectionReason);
        Assert.Empty(setup.Actor.Cooldowns.Cooldowns);
        Assert.True(setup.Activations.CanUseMainAction);
        Assert.False(setup.Actor.HasActedThisRound);
    }

    [Fact]
    public void PredictableDeadlineOverflow_LeavesCooldownAndActionUnchanged()
    {
        var setup = Setup(Ability(reuse: 1), int.MaxValue);
        var use = new AbilityUse(setup.Actor, "example", setup.Activations);

        Assert.Throws<OverflowException>(() => AbilityUseFinalizer.FinalizeSuccessfulUse(use));

        Assert.Empty(setup.Actor.Cooldowns.Cooldowns);
        Assert.True(setup.Activations.CanUseMainAction);
        Assert.False(setup.Activations.MainActionUsed);
        Assert.Same(setup.Actor, setup.Activations.ActiveCharacter);
    }

    [Fact]
    public void Finalization_RechecksCooldownChangesMadeAfterSelection()
    {
        var setup = Setup(Ability());
        var use = new AbilityUse(setup.Actor, "example", setup.Activations);
        setup.Actor.Cooldowns.RecordUse("example", 2, 3);
        var previous = setup.Actor.Cooldowns.Cooldowns["example"];

        var result = AbilityUseFinalizer.FinalizeSuccessfulUse(use);

        Assert.Equal(AbilityRejectionReason.OnCooldown, result.Eligibility.RejectionReason);
        Assert.Same(previous, setup.Actor.Cooldowns.Cooldowns["example"]);
        Assert.True(setup.Activations.CanUseMainAction);
        Assert.False(setup.Actor.HasActedThisRound);
    }

    [Fact]
    public void SameIdImpostor_CannotConsumeTheActiveInstancesAction()
    {
        var setup = Setup(Ability());
        var impostor = CharacterFactory.Create(setup.Actor.Definition!, setup.Actor.Id, 1);

        var result = AbilityUseFinalizer.FinalizeSuccessfulUse(new AbilityUse(impostor, "example", setup.Activations));

        Assert.Equal(AbilityRejectionReason.CharacterNotActive, result.Eligibility.RejectionReason);
        Assert.Empty(impostor.Cooldowns.Cooldowns);
        Assert.Empty(setup.Actor.Cooldowns.Cooldowns);
        Assert.True(setup.Activations.CanUseMainAction);
    }

    [Fact]
    public void UseFromPreviousRound_IsRejectedWithoutMutation()
    {
        var setup = Setup(Ability());
        var use = new AbilityUse(setup.Actor, "example", setup.Activations);
        while (setup.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) setup.Lifecycle.AdvancePhase();
        setup.Lifecycle.StartNextRound();

        var result = AbilityUseFinalizer.FinalizeSuccessfulUse(use);

        Assert.Equal(AbilityRejectionReason.UseContextChanged, result.Eligibility.RejectionReason);
        Assert.Empty(setup.Actor.Cooldowns.Cooldowns);
        Assert.True(setup.Activations.CanUseMainAction);
    }

    [Fact]
    public void SharedDefinition_HasIndependentExecutionCooldownsAcrossCharacters()
    {
        var setup = Setup(Ability());
        Assert.Same(setup.Actor.Definition, setup.Other.Definition);
        Assert.True(AbilityUseFinalizer.FinalizeSuccessfulUse(new AbilityUse(setup.Actor, "example", setup.Activations)).Succeeded);
        setup.Activations.StartActivation(setup.Other);

        Assert.True(AbilityEligibility.Evaluate(setup.Other, "example", setup.Activations).IsAllowed);
        Assert.Empty(setup.Other.Cooldowns.Cooldowns);
        Assert.False(setup.Actor.Cooldowns.IsReady("example", 2));
    }

    [Fact]
    public void SuccessfulBookkeeping_LeavesHpShieldsStatusesHistoryAndBaseStatsUntouched()
    {
        var setup = Setup(Ability(action: AbilityActionType.UtilityAction));
        setup.Actor.TakeDamage(5);
        HealingApplicator.Apply(1, setup.Actor);
        setup.Actor.SetShieldHP(6);
        setup.Actor.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        setup.Actor.Statuses.ApplyPoison();
        var stats = setup.Actor.BaseStats;
        var snapshot = stats with { };

        var result = AbilityUseFinalizer.FinalizeSuccessfulUse(new AbilityUse(setup.Actor, "example", setup.Activations));

        Assert.True(result.Succeeded);
        Assert.Equal(96, setup.Actor.CurrentHealth);
        Assert.Equal(6, setup.Actor.ShieldHP);
        Assert.Equal(1, setup.Actor.HealsReceivedThisRound);
        Assert.Equal(2, setup.Actor.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.True(setup.Actor.Statuses.IsPoisoned);
        Assert.Equal(0, setup.Actor.Statuses.PoisonTicksOccurred);
        Assert.Same(stats, setup.Actor.BaseStats);
        Assert.Equal(snapshot, setup.Actor.BaseStats);
        Assert.All(typeof(AbilityEligibilityResult).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(AbilityUseResult).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void InvalidProgrammingInputs_AreRejected()
    {
        var setup = Setup(Ability());
        Assert.Throws<ArgumentNullException>(() => AbilityEligibility.Evaluate(null!, "example", setup.Activations));
        Assert.Throws<ArgumentNullException>(() => AbilityEligibility.Evaluate(setup.Actor, "example", null!));
        Assert.Throws<ArgumentException>(() => AbilityEligibility.Evaluate(setup.Actor, " ", setup.Activations));
        Assert.Throws<ArgumentNullException>(() => AbilityUseFinalizer.FinalizeSuccessfulUse(null!));
        Assert.Throws<ArgumentNullException>(() => new AbilityUse(null!, "example", setup.Activations));
        Assert.Throws<ArgumentNullException>(() => new AbilityUse(setup.Actor, "example", null!));
    }

    private static AbilityDefinition Ability(AbilityActionType action = AbilityActionType.MainAction, bool? spell = false, int reuse = 3)
        => new("example", "Example", "Prototype execution metadata, not a roster ability.",
            AbilityClassification.Active, spell, action, reuseInterval: reuse);
    private static CharacterStats Stats() => new(100, 20, 10, 12, 8, 4, 10, 5);
    private static (Character Actor, Character Other, ActivationManager Activations, RoundLifecycleManager Lifecycle)
        Setup(AbilityDefinition ability, int round = 2)
    {
        var definition = new CharacterDefinition("test", "Test", "Test template.", Stats(), new[] { ability });
        var actor = CharacterFactory.Create(definition, "actor", 1);
        var other = CharacterFactory.Create(definition, "other", 2);
        var lifecycle = new RoundLifecycleManager(round);
        while (lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) lifecycle.AdvancePhase();
        var activations = new ActivationManager(new[] { actor }, new[] { other }, 1, lifecycle);
        activations.StartActivation(actor);
        return (actor, other, activations, lifecycle);
    }
}
