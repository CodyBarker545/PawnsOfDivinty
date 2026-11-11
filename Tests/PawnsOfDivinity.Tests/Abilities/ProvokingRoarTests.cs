using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Core.Turns.Restrictions;
using Xunit;
using NigelDefinition = PawnsOfDivinity.Core.Characters.Definitions.Nigel;

namespace PawnsOfDivinity.Tests.Abilities;

public class ProvokingRoarTests
{
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character,
        requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);
    private static AbilityTargetingConstraints AttackRules => new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy,
        true, false, false, false);

    [Fact]
    public void RestrictionPreservesEffectAndInstanceIdentityAndConfirmedRoundEndLifetime()
    {
        var s = new Setup();
        var restriction = s.Restriction();
        Assert.Equal("restriction", restriction.Id);
        Assert.Equal("roar_event", restriction.EffectId);
        Assert.Equal(s.Nigel.Id, restriction.SourceCharacterId);
        Assert.Equal(s.Actor.Id, restriction.RestrictedCharacterId);
        Assert.Equal(2, restriction.ApplicationRound);
        Assert.Equal(2, restriction.ExpiresAtEndOfRound);
        Assert.Equal(TauntMainActionRule.AttackSourceOrEndActivation, restriction.MainActionRule);
        Assert.False(restriction.IsActiveInRound(1));
        Assert.True(restriction.IsActiveInRound(2));
        Assert.False(restriction.IsActiveInRound(3));
        Assert.True(s.Manager.ActionRestrictions.Apply(restriction).Applied);
        Assert.Same(restriction, s.Manager.ActionRestrictions.GetActive(s.Actor.Id));
        Assert.False(s.Actor.HasActedThisRound);
        Assert.True(s.Manager.CanUseMainAction); // Budget availability is separate from target permission.
    }

    [Theory]
    [InlineData("requiredAttack", TauntActionOutcome.Allowed)]
    [InlineData("otherAttack", TauntActionOutcome.MustAttackSource)]
    [InlineData("nonAttack", TauntActionOutcome.MustAttackSource)]
    [InlineData("noTarget", TauntActionOutcome.MustAttackSource)]
    [InlineData("noContext", TauntActionOutcome.AttackContextRequired)]
    [InlineData("wrongSource", TauntActionOutcome.SourceContextMismatch)]
    [InlineData("movementPending", TauntActionOutcome.MovementDecisionRequired)]
    [InlineData("movementUnknown", TauntActionOutcome.MovementDecisionRequired)]
    [InlineData("attackUnknown", TauntActionOutcome.AttackLegalityUnresolved)]
    [InlineData("noLegalAttack", TauntActionOutcome.MustEndActivation)]
    [InlineData("sourceUnknown", TauntActionOutcome.SourceApplicabilityUnresolved)]
    [InlineData("sourceUnavailable", TauntActionOutcome.Allowed)]
    public void MainActionPolicyChecksOnlyConfirmedAttackChoices(string proposalKind, TauntActionOutcome expected)
    {
        var s = new Setup();
        s.Apply();
        var context = proposalKind switch
        {
            "wrongSource" => new TauntAttackAvailability(s.OtherNigel.Id, true, true, true),
            "movementPending" => new(s.Nigel.Id, false, true, true),
            "movementUnknown" => new(s.Nigel.Id, null, true, true),
            "attackUnknown" => new(s.Nigel.Id, true, true, null),
            "noLegalAttack" => new(s.Nigel.Id, true, true, false),
            "sourceUnknown" => new(s.Nigel.Id, true, null, true),
            "sourceUnavailable" => new(s.Nigel.Id, true, false, false),
            _ => s.Availability()
        };
        var proposal = new MainActionProposal(proposalKind != "nonAttack",
            proposalKind == "otherAttack" ? s.Other.Id : proposalKind == "noTarget" ? null : s.Nigel.Id,
            proposalKind == "noContext" ? null : context);
        var result = s.Manager.EvaluateMainAction(s.Actor, proposal);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(s.Actor.Id, result.CharacterId);
        Assert.Equal("restriction", result.RestrictionId);
        Assert.Equal(s.Nigel.Id, result.SourceCharacterId);
        Assert.Equal(expected == TauntActionOutcome.Allowed, result.IsAllowed);
        s.AssertUnchanged();
    }

    [Fact]
    public void RecordedMovementUsesExistingBudgetButDoesNotRequireAPathOrAMandatoryMove()
    {
        var s = new Setup();
        s.Apply();
        var before = s.Actor.TacticalPosition;
        s.Manager.RecordMovement(); // Represents caller-completed movement, not movement performed by the policy.
        var proposal = new MainActionProposal(true, s.Nigel.Id, new(s.Nigel.Id, false, true, true));
        Assert.True(s.Manager.EvaluateMainAction(s.Actor, proposal).IsAllowed);
        Assert.Equal(before, s.Actor.TacticalPosition);
        Assert.True(s.Manager.MovementUsed);
        Assert.False(s.Manager.MainActionUsed);
        var noMove = new Setup();
        noMove.Apply();
        Assert.True(noMove.Manager.EvaluateMainAction(noMove.Actor, noMove.Proposal()).IsAllowed);
        Assert.False(noMove.Manager.MovementUsed); // Explicit caller decision to finish/decline movement.
    }

    [Fact]
    public void ProhibitedOrUnspecifiedMainActionsCannotUseTheOldCompletionPath()
    {
        var s = new Setup();
        s.Apply();
        Assert.Throws<InvalidOperationException>(() => s.Manager.CompleteMainAction());
        var wrongTarget = new MainActionProposal(true, s.Other.Id, s.Availability());
        Assert.Equal(TauntActionOutcome.MustAttackSource, s.Manager.TryCompleteMainAction(s.Actor, wrongTarget).Outcome);
        Assert.Throws<InvalidOperationException>(() => s.Manager.CompleteMainAction(wrongTarget));
        s.AssertUnchanged();
    }

    [Fact]
    public void PermittedCompletionUsesExactlyTheExistingMainActionAndAlternation()
    {
        var s = new Setup();
        s.Apply();
        var result = s.Manager.TryCompleteMainAction(s.Actor, s.Proposal());
        Assert.True(result.IsAllowed);
        Assert.True(s.Manager.MainActionUsed);
        Assert.False(s.Manager.UtilityActionUsed);
        Assert.False(s.Manager.MovementUsed);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.True(s.Actor.HasActedThisRound);
        Assert.Equal(1, s.Manager.NextTeamId);
        Assert.Equal(52, s.Nigel.CurrentHealth); // Completion records no attack or damage.
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.Equal(TauntActionOutcome.WrongActiveCharacter, s.Manager.TryCompleteMainAction(s.Actor, s.Proposal()).Outcome);
    }

    [Fact]
    public void NoLegalAttackReturnsFallbackAndEndsOnlyOnExplicitRequest()
    {
        var s = new Setup();
        s.Apply();
        var availability = new TauntAttackAvailability(s.Nigel.Id, true, true, false);
        var checkedAction = s.Manager.EvaluateMainAction(s.Actor, new(true, s.Nigel.Id, availability));
        Assert.True(checkedAction.RequiresEndActivation);
        Assert.Same(s.Actor, s.Manager.ActiveCharacter);
        var ended = s.Manager.EndActivationForTaunt(s.Actor, availability);
        Assert.True(ended.ActivationEnded);
        Assert.False(s.Manager.MainActionUsed);
        Assert.False(s.Manager.MovementUsed);
        Assert.False(s.Manager.UtilityActionUsed);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.True(s.Actor.HasActedThisRound);
        Assert.Equal(1, s.Manager.NextTeamId);
        Assert.Equal(new GridPosition(6, 5), s.Actor.TacticalPosition);
        Assert.Equal(52, s.Nigel.CurrentHealth);
    }

    [Fact]
    public void FallbackDoesNotEndBeforeMovementDecisionOrWhenRequiredAttackExists()
    {
        var s = new Setup();
        s.Apply();
        Assert.Equal(TauntActionOutcome.MovementDecisionRequired,
            s.Manager.EndActivationForTaunt(s.Actor, new(s.Nigel.Id, false, true, false)).Outcome);
        Assert.Equal(TauntActionOutcome.MustAttackSource,
            s.Manager.EndActivationForTaunt(s.Actor, s.Availability()).Outcome);
        s.AssertUnchanged();
    }

    [Fact]
    public void UtilityActionsRemainAvailableWithoutSatisfyingOrClearingTaunt()
    {
        var s = new Setup();
        s.Apply();
        Assert.Equal(TauntActionOutcome.Allowed, s.Manager.EvaluateUtilityAction(s.Actor).Outcome);
        s.Manager.RecordUtilityAction();
        Assert.Throws<InvalidOperationException>(() => s.Manager.RecordUtilityAction());
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.False(s.Manager.MainActionUsed);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.Actor.Id));
    }

    [Fact]
    public void WrongActiveCharacterCannotSpendOrEndAnothersActivation()
    {
        var s = new Setup();
        s.Apply();
        Assert.Equal(TauntActionOutcome.WrongActiveCharacter, s.Manager.EvaluateMainAction(s.Other, s.Proposal()).Outcome);
        Assert.Equal(TauntActionOutcome.WrongActiveCharacter, s.Manager.TryCompleteMainAction(s.Other, s.Proposal()).Outcome);
        Assert.Equal(TauntActionOutcome.WrongActiveCharacter,
            s.Manager.EndActivationForTaunt(s.Other, new(s.Nigel.Id, true, true, false)).Outcome);
        s.AssertUnchanged();
    }

    [Fact]
    public void UnrelatedCharactersKeepNormalBudgetsAndChoices()
    {
        var s = new Setup();
        s.Apply();
        s.Manager.EndActivationForTaunt(s.Actor, new(s.Nigel.Id, true, true, false));
        s.Manager.StartActivation(s.Nigel);
        Assert.True(s.Manager.EvaluateMainAction(s.Nigel).IsAllowed);
        s.Manager.CompleteActivation();
        s.Manager.StartActivation(s.Other);
        Assert.True(s.Manager.EvaluateMainAction(s.Other).IsAllowed);
        Assert.True(s.Manager.EvaluateUtilityAction(s.Other).IsAllowed);
        s.Manager.RecordUtilityAction();
        s.Manager.CompleteMainAction();
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.True(s.Manager.MainActionUsed);
    }

    [Fact]
    public void DuplicateEffectRecipientsAreRejectedButOneEffectMayRestrictDifferentRecipients()
    {
        var s = new Setup();
        s.Apply();
        Assert.Equal(TauntApplicationOutcome.DuplicateRestrictionId,
            s.Manager.ActionRestrictions.Apply(s.Restriction()).Outcome);
        Assert.Equal(TauntApplicationOutcome.DuplicateEffectRecipient,
            s.Manager.ActionRestrictions.Apply(s.Restriction(id: "different_id")).Outcome);
        var second = new TauntRestriction("second_recipient", "roar_event", s.Nigel.Id, s.Other.Id, 2, 2);
        Assert.True(s.Manager.ActionRestrictions.Apply(second).Applied);
        Assert.Equal(2, s.Manager.ActionRestrictions.Snapshots.Count);
        Assert.False(s.Other.HasActedThisRound);
    }

    [Fact]
    public void OverlappingSourcesPreserveTheFirstActiveSource()
    {
        var s = new Setup();
        s.Apply();
        var overlapping = new TauntRestriction("other_roar", "other_event", s.OtherNigel.Id, s.Actor.Id, 2, 2);
        Assert.Equal(TauntApplicationOutcome.PreservedFirstSource, s.Manager.ActionRestrictions.Apply(overlapping).Outcome);
        Assert.Equal(s.Nigel.Id, Assert.Single(s.Manager.ActionRestrictions.Snapshots).SourceCharacterId);
        s.AssertUnchanged();
    }

    [Theory]
    [InlineData(null, TauntApplicationOutcome.LifetimeUnresolved)]
    [InlineData(3, TauntApplicationOutcome.UnsupportedLifetime)]
    public void UnresolvedOrUnconfirmedLifetimesNeverCreateActiveRestrictions(int? expiration, TauntApplicationOutcome expected)
    {
        var s = new Setup();
        var restriction = s.Restriction(expiration: expiration);
        Assert.Equal(expected, s.Manager.ActionRestrictions.Apply(restriction).Outcome);
        Assert.Empty(s.Manager.ActionRestrictions.Snapshots);
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.Actor.Id));
        Assert.True(s.Manager.EvaluateMainAction(s.Actor).IsAllowed);
        if (expiration is null)
            Assert.Equal(TauntActionOutcome.LifetimeUnresolved,
                TauntActionPolicy.Evaluate(s.Actor, restriction, s.Nigel, 2, false, s.Proposal()).Outcome);
        s.AssertUnchanged();
    }

    [Fact]
    public void ConfirmedRoundEndLifetimeDoesNotBecomeAPermanentOrNextRoundTaunt()
    {
        var s = new Setup();
        s.Apply();
        var snapshot = Assert.Single(s.Manager.ActionRestrictions.Snapshots);
        while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        Assert.Equal(3, s.Manager.CurrentRound);
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.Actor.Id));
        Assert.True(TauntActionPolicy.Evaluate(s.Actor, snapshot, s.Nigel, 3, false, null).IsAllowed);
        Assert.Equal(2, snapshot.ExpiresAtEndOfRound);
        Assert.Equal(38, s.Actor.CurrentHealth);
        Assert.False(s.Actor.HasActedThisRound); // This tracker did not run preparation or activation resets.
    }

    [Fact]
    public void SourceDefeatReleasesTauntWithoutTheForcedEndFallback()
    {
        var s = new Setup();
        s.Apply();
        s.Nigel.TakeDamage(int.MaxValue);
        Assert.True(s.Manager.EvaluateMainAction(s.Actor, s.Proposal()).ReleasesTaunt);
        Assert.True(s.Manager.ReleaseInvalidTauntSource(s.Actor, new(s.Nigel.Id, true, false, false)).IsAllowed);
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.Actor.Id));
        Assert.Same(s.Actor, s.Manager.ActiveCharacter);
        Assert.False(s.Manager.MainActionUsed);
    }

    [Fact]
    public void DefeatedRestrictedCharacterCannotActAndDoesNotReceiveNewRestrictions()
    {
        var s = new Setup();
        s.Apply();
        s.Actor.TakeDamage(int.MaxValue);
        Assert.Equal(TauntActionOutcome.CharacterDefeated, s.Manager.EvaluateMainAction(s.Actor, s.Proposal()).Outcome);
        var another = new Setup();
        another.Actor.TakeDamage(int.MaxValue);
        Assert.Equal(TauntApplicationOutcome.RecipientUnavailable,
            another.Manager.ActionRestrictions.Apply(another.Restriction()).Outcome);
        Assert.Empty(another.Manager.ActionRestrictions.Snapshots);
    }

    [Fact]
    public void ApprovedActionCanCompleteAfterItsRequiredTargetDiesWithoutReauthorizingAnotherAction()
    {
        var s = new Setup();
        s.Apply();
        var approval = s.Manager.EvaluateMainAction(s.Actor, s.Proposal());
        Assert.True(approval.IsAllowed);
        // Represents the caller's separately resolved legal attack, not an automatic policy effect.
        s.Nigel.TakeDamage(int.MaxValue);
        Assert.True(s.Manager.CompleteApprovedMainAction(s.Actor, approval).IsAllowed);
        Assert.True(s.Manager.MainActionUsed);
        Assert.True(s.Actor.HasActedThisRound);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.Equal(TauntActionOutcome.ApprovalContextChanged, s.Manager.CompleteApprovedMainAction(s.Actor, approval).Outcome);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("character")]
    [InlineData("round")]
    [InlineData("rejected")]
    public void ApprovalCannotBeReusedForAnotherContext(string mismatch)
    {
        var s = new Setup();
        s.Apply();
        var approval = s.Manager.EvaluateMainAction(s.Actor, mismatch == "rejected"
            ? new(true, s.Other.Id, s.Availability()) : s.Proposal());
        var manager = s.Manager;
        var actor = s.Actor;
        if (mismatch == "manager")
        {
            var other = new Setup();
            manager = other.Manager;
            actor = other.Actor;
        }
        if (mismatch == "character") actor = s.Other;
        if (mismatch == "round")
        {
            while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
            s.Lifecycle.StartNextRound();
        }
        Assert.Equal(TauntActionOutcome.ApprovalContextChanged, manager.CompleteApprovedMainAction(actor, approval).Outcome);
        Assert.False(manager.MainActionUsed);
        Assert.False(s.Actor.HasActedThisRound);
    }

    [Fact]
    public void AbilityExecutorsCannotBypassTauntOrExistingSilenceAndCooldownChecks()
    {
        var s = new Setup();
        s.Apply();
        var use = new AbilityUse(s.Actor, TimeWeaver.ArcaneTransmutationId, s.Manager);
        Assert.Equal(AbilityRejectionReason.ActionRestrictionUnresolved, AbilityEligibility.Evaluate(s.Actor, use.AbilityId, s.Manager).RejectionReason);
        Assert.Equal(AbilityRejectionReason.ActionRestrictionUnresolved, AbilityUseFinalizer.FinalizeSuccessfulUse(use).Eligibility.RejectionReason);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        Assert.Equal(AbilityRejectionReason.MetadataUnresolved, AbilityEligibility.Evaluate(s.Actor, use.AbilityId, s.Manager).RejectionReason);
        s.Actor.Statuses.RemoveStatus(StatusType.Silence);
        s.Actor.Cooldowns.RecordUse(use.AbilityId, 2, 3);
        Assert.Equal(AbilityRejectionReason.OnCooldown, AbilityEligibility.Evaluate(s.Actor, use.AbilityId, s.Manager).RejectionReason);
        Assert.False(s.Manager.MainActionUsed);
    }

    [Fact]
    public void LegalUtilityAbilityFinalizesWithoutSatisfyingTheRequiredMainAction()
    {
        var definition = TimeWeaver.Definition;
        var fixture = new CharacterDefinition(definition.Id, definition.Name, definition.Description, definition.BaseStats,
            definition.Abilities.Append(new AbilityDefinition("utility_fixture", "Utility fixture", "Test only",
                AbilityClassification.Active, false, AbilityActionType.UtilityAction, reuseInterval: 0)));
        var s = new Setup(actorDefinition: fixture);
        s.Apply();
        var use = new AbilityUse(s.Actor, "utility_fixture", s.Manager);
        Assert.True(AbilityUseFinalizer.FinalizeSuccessfulUse(use).Succeeded);
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Single(s.Actor.Cooldowns.Cooldowns);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.Actor.Id));
    }

    [Fact]
    public void NewlyAppliedRestrictionInvalidatesAnEarlierUnrestrictedActionApproval()
    {
        var s = new Setup();
        var approval = s.Manager.EvaluateMainAction(s.Actor);
        Assert.True(approval.IsAllowed);
        s.Apply();
        Assert.Equal(TauntActionOutcome.ApprovalContextChanged, s.Manager.CompleteApprovedMainAction(s.Actor, approval).Outcome);
        s.AssertUnchanged();
    }

    [Fact]
    public void DisarmIsNotRemovedAndAttackAvailabilityIsSuppliedRatherThanGuessed()
    {
        var s = new Setup();
        s.Apply();
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        Assert.Equal(TauntActionOutcome.MustEndActivation,
            s.Manager.EvaluateMainAction(s.Actor, new(true, s.Nigel.Id, new(s.Nigel.Id, true, true, false))).Outcome);
        // A caller may instead supply a legal existing unarmed attack. No Power/range is invented here.
        Assert.True(s.Manager.EvaluateMainAction(s.Actor, s.Proposal()).IsAllowed);
        Assert.True(s.Actor.Statuses.HasStatus(StatusType.Disarm));
        Assert.Equal(1, s.Actor.Statuses.TimedStatuses[StatusType.Disarm]);
    }

    [Fact]
    public void CanonicalRoarSelectsAppliesAndFinalizesWithNonMutatingPreview()
    {
        var s = new Setup(startNigel: true);
        var executor = new ProvokingRoarAbilityExecutor();
        var use = new AbilityUse(s.Nigel, Nigel.ProvokingRoarId, s.Manager);
        var preview = executor.Preview(use, Rules);
        Assert.Equal(new[] { s.Actor.Id }, preview.Effect!.AffectedCharacterIds);
        Assert.Equal(2, preview.ExpiresAtEndOfRound);
        Assert.Equal(ProvokingRoarUseRejectionReason.None, preview.RejectionReason);
        Assert.Empty(s.Manager.ActionRestrictions.Snapshots);
        var execution = executor.Execute(use, Rules);
        Assert.True(execution.Succeeded);
        Assert.Equal(preview.RejectionReason, execution.RejectionReason);
        Assert.Single(s.Manager.ActionRestrictions.Snapshots);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Single(s.Nigel.Cooldowns.Cooldowns);
        Assert.Null(s.Manager.ActiveCharacter);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("effect")]
    [InlineData("source")]
    [InlineData("recipient")]
    [InlineData("self")]
    [InlineData("round")]
    [InlineData("expiry")]
    public void InvalidRestrictionInputsAreRejectedBeforeStorage(string invalid)
    {
        var s = new Setup();
        Assert.ThrowsAny<ArgumentException>(() => new TauntRestriction(invalid == "id" ? " " : "id",
            invalid == "effect" ? " " : "effect", invalid == "source" ? " " : s.Nigel.Id,
            invalid == "recipient" ? " " : invalid == "self" ? s.Nigel.Id : s.Actor.Id,
            invalid == "round" ? 0 : 2, invalid == "expiry" ? 1 : 2));
        Assert.Empty(s.Manager.ActionRestrictions.Snapshots);
        s.AssertUnchanged();
    }

    [Theory]
    [InlineData("unknownSource")]
    [InlineData("unknownRecipient")]
    [InlineData("wrongRound")]
    [InlineData("deadSource")]
    [InlineData("ally")]
    [InlineData("alreadyActed")]
    public void InvalidApplicationsDoNotConsumeIdentityOrChangeState(string invalid)
    {
        var s = new Setup();
        if (invalid == "deadSource") s.Nigel.TakeDamage(int.MaxValue);
        if (invalid == "alreadyActed") s.Actor.EndActivation();
        var restriction = new TauntRestriction("candidate", "event", invalid == "unknownSource" ? "unknown" : s.Nigel.Id,
            invalid == "unknownRecipient" ? "unknown" : invalid == "ally" ? s.OtherNigel.Id : s.Actor.Id,
            invalid == "wrongRound" ? 1 : 2, invalid == "wrongRound" ? 1 : 2);
        if (invalid.StartsWith("unknown", StringComparison.Ordinal))
            Assert.Throws<ArgumentException>(() => s.Manager.ActionRestrictions.Apply(restriction));
        else Assert.False(s.Manager.ActionRestrictions.Apply(restriction).Applied);
        Assert.Empty(s.Manager.ActionRestrictions.Snapshots);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Equal(38, s.Actor.CurrentHealth);
    }

    [Fact]
    public void SnapshotsAndRepeatedPolicyChecksAreImmutableAndChangeNoUnrelatedState()
    {
        var s = new Setup();
        s.Actor.SetShieldHP(6);
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        HealingApplicator.Apply(0, s.Actor);
        var stats = s.Actor.BaseStats;
        var position = s.Actor.TacticalPosition;
        s.Apply();
        var snapshots = s.Manager.ActionRestrictions.Snapshots;
        var first = s.Manager.EvaluateMainAction(s.Actor, s.Proposal());
        for (int repeat = 0; repeat < 5; repeat++)
            Assert.Equal(first, s.Manager.EvaluateMainAction(s.Actor, s.Proposal()));
        Assert.Throws<NotSupportedException>(() => ((IList<TauntRestriction>)snapshots).Clear());
        s.Manager.ActionRestrictions.Apply(new("other_restriction", "same_roar", s.Nigel.Id, s.Other.Id, 2, 2));
        Assert.Single(snapshots);
        Assert.Equal(2, s.Manager.ActionRestrictions.Snapshots.Count);
        Assert.Equal(position, s.Actor.TacticalPosition);
        Assert.Equal(38, s.Actor.CurrentHealth);
        Assert.Equal(6, s.Actor.ShieldHP);
        Assert.Equal(2, s.Actor.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(1, s.Actor.HealsReceivedThisRound);
        Assert.Same(stats, s.Actor.BaseStats);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.False(s.Manager.MainActionUsed);
        foreach (var type in new[] { typeof(TauntRestriction), typeof(TauntApplicationResult), typeof(TauntActionResult),
            typeof(TauntAttackAvailability), typeof(MainActionProposal) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void NullInputsAndWrongLifecyclePhaseAreRejectedWithoutMutation()
    {
        var s = new Setup();
        s.Apply();
        Assert.Throws<ArgumentNullException>(() => s.Manager.ActionRestrictions.Apply(null!));
        Assert.Throws<ArgumentNullException>(() => s.Manager.EvaluateMainAction(null!));
        Assert.Throws<ArgumentNullException>(() => s.Manager.EndActivationForTaunt(s.Actor, null!));
        Assert.Throws<ArgumentException>(() => new TauntAttackAvailability(" ", true, true, true));
        Assert.Throws<ArgumentException>(() => new MainActionProposal(true, " "));
        s.Lifecycle.AdvancePhase();
        Assert.Equal(TauntActionOutcome.WrongPhase, s.Manager.TryCompleteMainAction(s.Actor, s.Proposal()).Outcome);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Same(s.Actor, s.Manager.ActiveCharacter);
    }

    [Fact]
    public void FinalizedCastPreviewReportsRecipientsConsequencesAndOwnershipWithoutMutation()
    {
        var s = new CastingSetup();
        s.Y.SetTacticalPosition(new(6, 6)); // Exactly radius 2 from A.
        var result = s.Executor.Preview(s.UseA, Rules, effectId: "cast_a");
        Assert.True(result.CanExecute);
        Assert.Equal(s.A.Id, result.CasterId);
        Assert.Equal(NigelDefinition.ProvokingRoarId, result.AbilityId);
        Assert.Equal(3, result.RoundNumber);
        Assert.Equal(2, result.Radius);
        Assert.Equal(3, result.ExpiresAtEndOfRound);
        Assert.Equal(6, result.ExpectedReadyRound);
        Assert.Equal(AbilityActionType.MainAction, result.RequiredAction);
        Assert.Equal(new[] { s.X.Id, s.Y.Id }, result.EnemiesInsideRadius);
        Assert.Equal(new[] { s.X.Id, s.Y.Id }, result.EligibleRecipientIds);
        Assert.Equal(result.EligibleRecipientIds, result.ProspectiveRecipientIds);
        Assert.Empty(result.NewlyTauntedRecipientIds);
        Assert.Empty(result.ExcludedExistingTauntRecipientIds);
        Assert.All(result.ControllingRestrictions, r => Assert.Equal(s.A.Id, r.SourceCharacterId));
        Assert.Empty(s.Manager.ActionRestrictions.Snapshots);
        Assert.Empty(s.A.Cooldowns.Cooldowns);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Same(s.A, s.Manager.ActiveCharacter);
        Assert.Equal(52, s.A.CurrentHealth);
        Assert.Equal(38, s.X.CurrentHealth);
    }

    [Fact]
    public void FinalizedCastAppliesOneValidatedBatchAndOneMainActionWithReuseThree()
    {
        var s = new CastingSetup();
        s.Y.SetTacticalPosition(new(6, 6));
        s.X.SetShieldHP(6);
        s.X.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var positions = s.Roster.Select(c => c.TacticalPosition).ToArray();
        var hp = s.Roster.Select(c => c.CurrentHealth).ToArray();
        var result = s.CastA();
        Assert.True(result.Succeeded);
        Assert.False(result.CanExecute);
        Assert.True(result.Restrictions!.WasApplied);
        Assert.Equal(new[] { s.X.Id, s.Y.Id }, result.NewlyTauntedRecipientIds);
        Assert.Equal(2, s.Manager.ActionRestrictions.Snapshots.Count);
        foreach (var character in new[] { s.X, s.Y })
        {
            var restriction = s.Manager.ActionRestrictions.GetActive(character.Id)!;
            Assert.Equal(s.A.Id, restriction.SourceCharacterId);
            Assert.Equal("cast_a", restriction.EffectId);
            Assert.Equal(3, restriction.ApplicationRound);
            Assert.Equal(3, restriction.ExpiresAtEndOfRound);
            Assert.False(character.HasActedThisRound);
        }
        Assert.True(s.Manager.MainActionUsed);
        Assert.False(s.Manager.MovementUsed);
        Assert.False(s.Manager.UtilityActionUsed);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.Equal(6, result.Finalization!.ReadyRound);
        Assert.Equal(3, s.A.Cooldowns.Cooldowns[NigelDefinition.ProvokingRoarId].ReuseInterval);
        Assert.Equal(positions, s.Roster.Select(c => c.TacticalPosition));
        Assert.Equal(hp, s.Roster.Select(c => c.CurrentHealth));
        Assert.Equal(6, s.X.ShieldHP);
        Assert.Equal(2, s.X.Statuses.TimedStatuses[StatusType.Burn]);
        var cooldown = s.A.Cooldowns.Cooldowns[NigelDefinition.ProvokingRoarId];
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, s.CastA().Eligibility.RejectionReason);
        Assert.Same(cooldown, s.A.Cooldowns.Cooldowns[NigelDefinition.ProvokingRoarId]);
        Assert.Equal(2, s.Manager.ActionRestrictions.Snapshots.Count);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("activated")]
    [InlineData("defeated")]
    [InlineData("allPreviouslyTaunted")]
    public void ZeroNewRecipientsRemainExplicitWithoutSpendingACast(string condition)
    {
        var s = new CastingSetup();
        if (condition == "empty") s.X.SetTacticalPosition(new(10, 10));
        if (condition == "activated") s.X.EndActivation();
        if (condition == "defeated") s.X.TakeDamage(int.MaxValue);
        if (condition == "allPreviouslyTaunted")
            s.Manager.ActionRestrictions.Apply(new("prior", "prior_cast", s.B.Id, s.X.Id, 3, 3));
        int before = s.Manager.ActionRestrictions.Snapshots.Count;
        var result = s.CastA();
        Assert.Equal(ProvokingRoarUseRejectionReason.NoRecipientsUseRuleUnresolved, result.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Equal(before, s.Manager.ActionRestrictions.Snapshots.Count);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Same(s.A, s.Manager.ActiveCharacter);
        Assert.Empty(s.A.Cooldowns.Cooldowns);
        if (condition == "allPreviouslyTaunted") Assert.Equal(new[] { s.X.Id }, result.ExcludedExistingTauntRecipientIds);
    }

    [Fact]
    public void SecondRoarPreservesFirstSourceWithoutRefreshAndAppliesToAnotherEnemy()
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        var first = s.Manager.ActionRestrictions.GetActive(s.X.Id)!;
        s.SelectB();
        var useB = new AbilityUse(s.B, NigelDefinition.ProvokingRoarId, s.Manager);
        var preview = s.Executor.Preview(useB, Rules, effectId: "cast_b");
        Assert.Equal(new[] { s.X.Id, s.Y.Id }, preview.EligibleRecipientIds);
        Assert.Equal(new[] { s.X.Id }, preview.ExcludedExistingTauntRecipientIds);
        Assert.Equal(new[] { s.Y.Id }, preview.ProspectiveRecipientIds);
        Assert.Same(first, s.Manager.ActionRestrictions.GetActive(s.X.Id));
        var result = s.Executor.Execute(useB, Rules, effectId: "cast_b");
        Assert.True(result.Succeeded);
        Assert.Equal(new[] { s.Y.Id }, result.NewlyTauntedRecipientIds);
        Assert.Same(first, s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.Equal(s.A.Id, first.SourceCharacterId);
        Assert.Equal(3, first.ExpiresAtEndOfRound);
        Assert.Equal(s.B.Id, s.Manager.ActionRestrictions.GetActive(s.Y.Id)!.SourceCharacterId);
        Assert.Equal(new[] { s.A.Id, s.B.Id }, result.ControllingRestrictions.Select(r => r.SourceCharacterId));
        Assert.Equal(2, s.Manager.ActionRestrictions.Snapshots.Count);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Equal(6, s.B.Cooldowns.Cooldowns[NigelDefinition.ProvokingRoarId].ReadyRound);
    }

    [Fact]
    public void SecondRoarWithOnlyPreviouslyControlledEnemiesDoesNotRefreshOrSpend()
    {
        var s = new CastingSetup();
        s.Y.SetTacticalPosition(new(10, 10));
        Assert.True(s.CastA().Succeeded);
        var first = s.Manager.ActionRestrictions.GetActive(s.X.Id)!;
        s.SelectB();
        var result = s.Executor.Execute(new(s.B, NigelDefinition.ProvokingRoarId, s.Manager), Rules, effectId: "cast_b");
        Assert.Equal(ProvokingRoarUseRejectionReason.NoRecipientsUseRuleUnresolved, result.RejectionReason);
        Assert.Same(first, s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.False(s.Manager.MainActionUsed);
        Assert.Empty(s.B.Cooldowns.Cooldowns);
        Assert.Single(s.Manager.ActionRestrictions.Snapshots);
    }

    [Theory]
    [InlineData("duplicateId")]
    [InlineData("duplicateRecipient")]
    [InlineData("unknownParticipant")]
    [InlineData("nullEntry")]
    public void RestrictionBatchRejectsAllPredictableFailuresBeforeAnyStorageMutation(string invalid)
    {
        var s = new CastingSetup();
        var first = new TauntRestriction("first", "event", s.A.Id, s.X.Id, 3, 3);
        TauntRestriction? second = invalid switch
        {
            "duplicateId" => new("first", "event", s.A.Id, s.Y.Id, 3, 3),
            "duplicateRecipient" => new("second", "event", s.A.Id, s.X.Id, 3, 3),
            "unknownParticipant" => new("second", "event", s.A.Id, "missing", 3, 3),
            _ => null
        };
        if (invalid is "unknownParticipant" or "nullEntry")
            Assert.Throws<ArgumentException>(() => s.Manager.ActionRestrictions.ApplyBatch(new[] { first, second! }));
        else
        {
            var result = s.Manager.ActionRestrictions.ApplyBatch(new[] { first, second! });
            Assert.False(result.IsValid);
            Assert.False(result.WasApplied);
        }
        Assert.Empty(s.Manager.ActionRestrictions.Snapshots);
        Assert.Empty(s.A.Cooldowns.Cooldowns);
        Assert.False(s.Manager.MainActionUsed);
    }

    [Fact]
    public void DuplicateProspectiveRestrictionIdBlocksTheEntireCast()
    {
        var s = new CastingSetup();
        var preview = s.Executor.Preview(s.UseA, Rules, effectId: "cast_a");
        var prospective = Assert.Single(preview.Restrictions!.Results).Restriction;
        var existing = new TauntRestriction(prospective.Id, "unrelated_event", s.B.Id, s.Y.Id, 3, 3);
        Assert.True(s.Manager.ActionRestrictions.Apply(existing).Applied);
        var result = s.CastA();
        Assert.Equal(ProvokingRoarUseRejectionReason.RestrictionStorageRejected, result.RejectionReason);
        Assert.Same(existing, Assert.Single(s.Manager.ActionRestrictions.Snapshots));
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.False(s.Manager.MainActionUsed);
        Assert.Empty(s.A.Cooldowns.Cooldowns);
    }

    [Fact]
    public void CastExecutionRevalidatesPositionsLifeActivationAndPriorControl()
    {
        var s = new CastingSetup();
        s.Y.SetTacticalPosition(new(6, 6));
        var preview = s.Executor.Preview(s.UseA, Rules, effectId: "cast_a");
        Assert.Equal(new[] { s.X.Id, s.Y.Id }, preview.ProspectiveRecipientIds);
        s.X.EndActivation();
        s.Y.TakeDamage(int.MaxValue);
        var result = s.CastA();
        Assert.Equal(ProvokingRoarUseRejectionReason.NoRecipientsUseRuleUnresolved, result.RejectionReason);
        Assert.Empty(s.Manager.ActionRestrictions.Snapshots);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Equal(new[] { s.X.Id, s.Y.Id }, preview.ProspectiveRecipientIds);
        var moved = new CastingSetup();
        Assert.True(moved.Executor.Preview(moved.UseA, Rules).CanExecute);
        moved.X.SetTacticalPosition(new(10, 10));
        Assert.False(moved.CastA().Succeeded);
        Assert.Empty(moved.Manager.ActionRestrictions.Snapshots);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TauntedUtilityPreservesRequiredAttackAndSupportsEitherLegalAttackOrFallback(bool attackAvailable)
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        s.Manager.StartActivation(s.X);
        Assert.True(s.Manager.CanMove);
        Assert.True(s.Manager.EvaluateUtilityAction(s.X).IsAllowed);
        var utility = new AbilityUse(s.X, "utility_fixture", s.Manager);
        Assert.True(AbilityUseFinalizer.FinalizeSuccessfulUse(utility).Succeeded);
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.False(s.Manager.MainActionUsed);
        Assert.True(s.Manager.CanMove);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.Throws<InvalidOperationException>(() => s.Manager.RecordUtilityAction());
        var availability = new TauntAttackAvailability(s.A.Id, true, true, attackAvailable);
        if (attackAvailable)
            Assert.True(s.Manager.TryCompleteMainAction(s.X, new(true, s.A.Id, availability)).IsAllowed);
        else Assert.True(s.Manager.EndActivationForTaunt(s.X, availability).ActivationEnded);
        Assert.Equal(attackAvailable, s.Manager.MainActionUsed);
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.False(s.Manager.MovementUsed);
        Assert.True(s.X.HasActedThisRound);
        Assert.Null(s.Manager.ActiveCharacter);
    }

    [Fact]
    public void TauntCannotBeBypassedByVoluntarilyEndingWhileTheAttackDecisionIsUnresolved()
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        s.Manager.StartActivation(s.X);
        Assert.Throws<InvalidOperationException>(() => s.Manager.CompleteActivation());
        Assert.False(s.X.HasActedThisRound);
        Assert.Same(s.X, s.Manager.ActiveCharacter);
        Assert.True(s.Manager.EndActivationForTaunt(s.X, new(s.A.Id, true, true, false)).ActivationEnded);
        Assert.False(s.Manager.UtilityActionUsed); // Utility is optional, not a prerequisite to the fallback.
        Assert.False(s.Manager.MainActionUsed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidSourceReleasesOnlyThatTauntAndPreservesSpentOpportunities(bool sourceDies)
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        s.Manager.ActionRestrictions.Apply(new("other_control", "other_roar", s.B.Id, s.Y.Id, 3, 3));
        s.Manager.StartActivation(s.X);
        s.Manager.RecordMovement();
        s.Manager.RecordUtilityAction();
        if (sourceDies) s.A.TakeDamage(int.MaxValue);
        var availability = new TauntAttackAvailability(s.A.Id, true, false, false);
        var preview = s.Manager.EvaluateMainAction(s.X, new(false, requiredAttack: availability));
        Assert.True(preview.IsAllowed);
        Assert.True(preview.ReleasesTaunt);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.X.Id)); // Evaluation is non-mutating.
        Assert.True(s.Manager.ReleaseInvalidTauntSource(s.X, availability).IsAllowed);
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.Equal(s.B.Id, s.Manager.ActionRestrictions.GetActive(s.Y.Id)!.SourceCharacterId);
        Assert.True(s.Manager.MovementUsed);
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Same(s.X, s.Manager.ActiveCharacter);
        Assert.False(s.X.HasActedThisRound);
        Assert.True(s.Manager.EvaluateMainAction(s.X).IsAllowed);
        Assert.False(s.Manager.CanMove);
        Assert.False(s.Manager.CanUseUtilityAction);
    }

    [Fact]
    public void LivingButUnreachableSourceUsesFallbackWhileUnknownValidityRemainsUnresolved()
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        s.Manager.StartActivation(s.X);
        var unknown = s.Manager.EvaluateMainAction(s.X, new(true, s.A.Id, new(s.A.Id, true, null, null)));
        Assert.Equal(TauntActionOutcome.SourceApplicabilityUnresolved, unknown.Outcome);
        var unreachable = s.Manager.EvaluateMainAction(s.X, new(true, s.A.Id, new(s.A.Id, true, true, false)));
        Assert.True(unreachable.RequiresEndActivation);
        Assert.False(unreachable.ReleasesTaunt);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.Same(s.X, s.Manager.ActiveCharacter);
        Assert.False(s.Manager.MainActionUsed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealRoarAllowsOrdinaryPhysicalOrMagicalAttackAgainstItsSource(bool magical)
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        s.Manager.StartActivation(s.X);
        s.X.SetTacticalPosition(new(7, 5)); // Explicit ordinary movement arrangement, not a policy-selected path.
        s.Manager.RecordMovement();
        var intent = new AttackIntent(s.X, s.A, "ordinary_attack", s.Manager, true, AttackRules,
            () => new(s.A.Id, true, true, true));
        var attack = new AttackConfiguration(magical ? AttackType.Magical : AttackType.Physical,
            magical ? WeaponCategory.MagicScroll : WeaponCategory.Axe, 4, 100, 1, 2);
        var request = new CombatExchangeRequest(s.X, s.A, attack.Type, attack.Power, attack.Category, WeaponCategory.Axe,
            100, AttackType.Physical, 0, 100, 1, 1, 2, true, false);
        var rolls = new FixedRolls();
        var result = new AttackActionExecutor(new(new(new HitRoller(rolls)))).Execute(intent, request, attack);
        Assert.True(result.Completed);
        Assert.Equal(magical ? 13 : 1, result.Combat!.OpeningStrike.Damage);
        Assert.Equal(1, rolls.Calls);
        Assert.True(s.Manager.MainActionUsed);
        Assert.True(s.Manager.MovementUsed);
        Assert.Empty(s.X.Cooldowns.Cooldowns);
    }

    [Fact]
    public void NewAttackAfterSourceDeathReleasesControlBeforeHittingThePlayersChosenOtherTarget()
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        s.Manager.StartActivation(s.X);
        s.Manager.RecordUtilityAction();
        s.A.TakeDamage(int.MaxValue);
        var intent = new AttackIntent(s.X, s.B, "ordinary_attack", s.Manager, true, AttackRules,
            () => new(s.A.Id, true, false, false));
        var request = new CombatExchangeRequest(s.X, s.B, AttackType.Magical, 4, WeaponCategory.MagicScroll,
            WeaponCategory.Axe, 100, AttackType.Physical, 0, 100, 1, 1, 2, true, false);
        var rolls = new FixedRolls();
        var executor = new AttackActionExecutor(new(new(new HitRoller(rolls))));
        var attack = new AttackConfiguration(AttackType.Magical, WeaponCategory.MagicScroll, 4, 100, 1, 2);
        Assert.True(executor.Preview(intent, request, attack).CanExecute);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        var result = executor.Execute(intent, request, attack);
        Assert.True(result.Completed);
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.Single(s.Manager.ActionRestrictions.EndedSnapshots);
        Assert.Equal(TauntEndReason.InvalidSource, s.Manager.ActionRestrictions.EndedSnapshots[0].Reason);
        Assert.Equal(39, s.B.CurrentHealth);
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Equal(1, rolls.Calls);
    }

    [Fact]
    public void CleanupExpiresCurrentRoundTauntsIdempotentlyAndPreservesOtherState()
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        s.X.SetShieldHP(6);
        s.X.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.X.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("unrelated_buff", s.A.Id, s.X.Id,
            TemporaryStat.Resistance, 2, 3, 4) });
        HealingApplicator.Apply(0, s.X);
        var stats = s.X.BaseStats;
        Assert.Throws<InvalidOperationException>(() => s.Manager.ProcessTauntCleanup());
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        s.FinishActivationsAndEnterCleanup();
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        var expired = s.Manager.ProcessTauntCleanup();
        Assert.Single(expired);
        Assert.Equal(TauntEndReason.Cleanup, expired[0].Reason);
        Assert.Equal(3, expired[0].RoundNumber);
        Assert.Empty(s.Manager.ProcessTauntCleanup());
        Assert.Equal(38, s.X.CurrentHealth);
        Assert.Equal(6, s.X.ShieldHP);
        Assert.Equal(2, s.X.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(1, s.X.HealsReceivedThisRound);
        Assert.Single(s.X.TemporaryModifiers.Active);
        Assert.Same(stats, s.X.BaseStats);
        s.Lifecycle.AdvancePhase();
        Assert.Throws<InvalidOperationException>(() => s.Manager.ProcessTauntCleanup());
        s.Lifecycle.StartNextRound();
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.Equal(4, s.Manager.CurrentRound);
        Assert.Equal(2, s.X.Statuses.TimedStatuses[StatusType.Burn]); // No timed-status orchestration was invoked.
    }

    [Fact]
    public void ExpiredFirstSourceDoesNotBlockAFutureRoarAndCleanupCannotUseAnotherLifecycle()
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        var fake = new RoundLifecycleManager(3);
        while (fake.CurrentPhase != RoundPhase.Cleanup) fake.AdvancePhase();
        Assert.Throws<InvalidOperationException>(() => s.Manager.ActionRestrictions.ExpireAtCleanup(fake));
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        s.FinishActivationsAndEnterCleanup();
        s.Manager.ProcessTauntCleanup();
        s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        while (s.Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) s.Lifecycle.AdvancePhase();
        // Arrange ordinary new-round readiness; expiration itself did not reset anyone's activation.
        foreach (var character in s.Roster) character.ResetActivation();
        var future = new TauntRestriction("future", "future_roar", s.B.Id, s.X.Id, 4, 4);
        Assert.True(s.Manager.ActionRestrictions.Apply(future).Applied);
        Assert.Same(future, s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.Equal(s.B.Id, future.SourceCharacterId);
    }

    [Fact]
    public void CastSnapshotsAndBatchCollectionsRemainImmutableAfterOtherCastsAndCleanup()
    {
        var s = new CastingSetup();
        var preview = s.Executor.Preview(s.UseA, Rules, effectId: "cast_a");
        var cast = s.CastA();
        Assert.Throws<NotSupportedException>(() => ((IList<string>)preview.ProspectiveRecipientIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TauntApplicationResult>)cast.Restrictions!.Results).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TauntRestriction>)cast.ControllingRestrictions).Clear());
        s.SelectB();
        s.Executor.Execute(new(s.B, NigelDefinition.ProvokingRoarId, s.Manager), Rules, effectId: "cast_b");
        Assert.Equal(new[] { s.X.Id }, cast.NewlyTauntedRecipientIds);
        Assert.Equal(new[] { s.X.Id }, preview.ProspectiveRecipientIds);
        Assert.Empty(preview.NewlyTauntedRecipientIds);
        s.FinishActivationsAndEnterCleanup();
        s.Manager.ProcessTauntCleanup();
        Assert.Equal(s.A.Id, cast.ControllingRestrictions[0].SourceCharacterId);
        foreach (var type in new[] { typeof(ProvokingRoarUseResult), typeof(TauntBatchResult), typeof(TauntEndResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void RealRoarAttackAbilityCanDefeatItsSourceAndFinalizeWithoutRetrospectiveRejection()
    {
        var s = new CastingSetup();
        s.Y.SetTacticalPosition(new(5, 4));
        Assert.True(s.CastA().Succeeded);
        s.Manager.StartActivation(s.Y);
        s.A.TakeDamage(44);
        var intent = new AttackIntent(s.Y, s.A, HoodedAssassin.ShadowStrikeId, s.Manager, true, AttackRules,
            () => new(s.A.Id, true, true, true));
        var use = new AbilityUse(s.Y, HoodedAssassin.ShadowStrikeId, s.Manager, intent);
        var smoke = new SmokePlacementService();
        var visibility = SmokeVisibility.Evaluate(s.Y, s.Y.TacticalPosition!.Value, smoke,
            new[] { (s.A, s.A.TacticalPosition!.Value), (s.B, s.B.TacticalPosition!.Value) });
        var rolls = new FixedRolls();
        var executor = new ShadowStrikeAbilityExecutor(new(new(new(new HitRoller(rolls))), smoke));
        var request = new CombatExchangeRequest(s.Y, s.A, AttackType.Physical, 5, WeaponCategory.Sword, WeaponCategory.Axe,
            100, AttackType.Physical, 0, 100, 1, 1, 1, true, false);
        var result = executor.Execute(use, request, visibility, 1, 1, AttackRules);
        Assert.True(result.Succeeded);
        Assert.True(s.A.IsDefeated);
        Assert.Equal(1, rolls.Calls);
        Assert.Equal(8, result.Strike!.ActualHPDamage);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Equal(3, result.Finalization!.ReadyRound);
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.Y.Id));
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, executor.Execute(use, request, visibility, 1, 1, AttackRules).Eligibility.RejectionReason);
        Assert.Equal(1, rolls.Calls);
        Assert.Single(s.Y.Cooldowns.Cooldowns);
    }

    [Fact]
    public void EndedInvalidSourceDoesNotPreventCompletionOfAnAlreadyApprovedAction()
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        s.Manager.StartActivation(s.X);
        var availability = new TauntAttackAvailability(s.A.Id, true, true, true);
        var approval = s.Manager.EvaluateMainAction(s.X, new(true, s.A.Id, availability));
        Assert.True(approval.IsAllowed);
        s.A.TakeDamage(int.MaxValue); // Caller-reported effect of this already initiated legal attack.
        Assert.True(s.Manager.ReleaseInvalidTauntSource(s.X, new(s.A.Id, true, false, false)).IsAllowed);
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.True(s.Manager.CompleteApprovedMainAction(s.X, approval).IsAllowed);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Equal(TauntActionOutcome.ApprovalContextChanged, s.Manager.CompleteApprovedMainAction(s.X, approval).Outcome);
    }

    [Fact]
    public void LaterValidRoarCanClaimAnEnemyAfterTheFirstSourceDies()
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        var original = s.Manager.ActionRestrictions.GetActive(s.X.Id)!;
        s.SelectB();
        s.A.TakeDamage(int.MaxValue);
        var use = new AbilityUse(s.B, NigelDefinition.ProvokingRoarId, s.Manager);
        var preview = s.Executor.Preview(use, Rules, effectId: "replacement_roar");
        Assert.Contains(s.X.Id, preview.ProspectiveRecipientIds);
        Assert.Same(original, s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.Empty(s.Manager.ActionRestrictions.EndedSnapshots); // Preview did not release or transfer control.
        var result = s.Executor.Execute(use, Rules, effectId: "replacement_roar");
        Assert.True(result.Succeeded);
        Assert.Equal(s.B.Id, s.Manager.ActionRestrictions.GetActive(s.X.Id)!.SourceCharacterId);
        Assert.Contains(s.Manager.ActionRestrictions.EndedSnapshots, e => e.RestrictionId == original.Id && e.Reason == TauntEndReason.InvalidSource);
    }

    [Fact]
    public void ReleasedSourceRestoresAnOrdinaryNonAttackMainActionWithoutGivingAnotherOpportunity()
    {
        var s = new CastingSetup();
        Assert.True(s.CastA().Succeeded);
        s.Manager.StartActivation(s.X);
        s.A.TakeDamage(int.MaxValue);
        var use = new AbilityUse(s.X, "main_fixture", s.Manager);
        Assert.True(AbilityEligibility.Evaluate(s.X, use.AbilityId, s.Manager).IsAllowed);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        // As with all direct finalizer tests, the caller reports a separately resolved legal effect.
        var result = AbilityUseFinalizer.FinalizeSuccessfulUse(use);
        Assert.True(result.Succeeded);
        Assert.Null(s.Manager.ActionRestrictions.GetActive(s.X.Id));
        Assert.True(s.Manager.MainActionUsed);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.Equal(4, result.ReadyRound);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, AbilityUseFinalizer.FinalizeSuccessfulUse(use).Eligibility.RejectionReason);
    }

    private sealed class FixedRolls : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }

    private sealed class CastingSetup
    {
        public Character A { get; } = CharacterFactory.Create(NigelDefinition.Definition, "a", 1);
        public Character B { get; } = CharacterFactory.Create(NigelDefinition.Definition, "b", 1);
        public Character X { get; }
        public Character Y { get; } = CharacterFactory.Create(HoodedAssassin.Definition, "y", 2);
        public Character Spectator { get; } = CharacterFactory.Create(GentleGuardian.Definition, "spectator", 2);
        public Character[] Roster => new[] { B, A, X, Y, Spectator };
        public RoundLifecycleManager Lifecycle { get; } = new(3);
        public ActivationManager Manager { get; }
        public AbilityUse UseA { get; }
        public ProvokingRoarAbilityExecutor Executor { get; } = new();

        public CastingSetup()
        {
            var definition = TimeWeaver.Definition;
            var fixture = new CharacterDefinition(definition.Id, definition.Name, definition.Description, definition.BaseStats,
                definition.Abilities.Append(new AbilityDefinition("utility_fixture", "Utility fixture", "Bookkeeping test only",
                    AbilityClassification.Active, false, AbilityActionType.UtilityAction, reuseInterval: 2))
                .Append(new AbilityDefinition("main_fixture", "Main fixture", "Bookkeeping test only",
                    AbilityClassification.Active, false, AbilityActionType.MainAction, reuseInterval: 1)));
            X = CharacterFactory.Create(fixture, "x", 2);
            A.SetTacticalPosition(new(5, 5));
            B.SetTacticalPosition(new(5, 6));
            X.SetTacticalPosition(new(6, 5));
            Y.SetTacticalPosition(new(7, 6));
            Spectator.SetTacticalPosition(new(12, 12));
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Manager = new(new[] { B, A }, new[] { X, Y, Spectator }, 1, Lifecycle); // A's first cast must beat roster order.
            Manager.StartActivation(A);
            UseA = new(A, NigelDefinition.ProvokingRoarId, Manager);
        }

        public ProvokingRoarUseResult CastA() => Executor.Execute(UseA, Rules, effectId: "cast_a");
        public void SelectB()
        {
            Manager.StartActivation(Spectator);
            Manager.CompleteActivation();
            Manager.StartActivation(B);
        }
        public void FinishActivationsAndEnterCleanup()
        {
            while (!Manager.IsRoundComplete)
            {
                if (Manager.ActiveCharacter is null)
                    Manager.StartActivation(Roster.First(c => c.TeamId == Manager.NextTeamId && c.CanAct));
                var active = Manager.ActiveCharacter!;
                var restriction = Manager.ActionRestrictions.GetActive(active.Id);
                if (restriction is null) Manager.CompleteActivation();
                else Manager.EndActivationForTaunt(active, new(restriction.SourceCharacterId, true, true, false));
            }
            while (Lifecycle.CurrentPhase != RoundPhase.Cleanup) Lifecycle.AdvancePhase();
        }
    }

    private sealed class Setup
    {
        public Character Nigel { get; } = CharacterFactory.Create(NigelDefinition.Definition, "source", 1);
        public Character OtherNigel { get; } = CharacterFactory.Create(NigelDefinition.Definition, "other_source", 1);
        public Character Actor { get; }
        public Character Other { get; } = CharacterFactory.Create(HoodedAssassin.Definition, "other", 2);
        public RoundLifecycleManager Lifecycle { get; } = new(2);
        public ActivationManager Manager { get; }

        public Setup(bool startNigel = false, CharacterDefinition? actorDefinition = null)
        {
            Actor = CharacterFactory.Create(actorDefinition ?? TimeWeaver.Definition, "actor", 2);
            Nigel.SetTacticalPosition(new(5, 5));
            OtherNigel.SetTacticalPosition(new(5, 6));
            Actor.SetTacticalPosition(new(6, 5));
            Other.SetTacticalPosition(new(11, 11));
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Manager = new(new[] { Nigel, OtherNigel }, new[] { Actor, Other }, startNigel ? 1 : 2, Lifecycle);
            Manager.StartActivation(startNigel ? Nigel : Actor);
        }

        public TauntRestriction Restriction(string id = "restriction", int? expiration = 2)
            => new(id, "roar_event", Nigel.Id, Actor.Id, 2, expiration);
        public void Apply() => Assert.True(Manager.ActionRestrictions.Apply(Restriction()).Applied);
        public TauntAttackAvailability Availability() => new(Nigel.Id, true, true, true);
        public MainActionProposal Proposal() => new(true, Nigel.Id, Availability());
        public void AssertUnchanged()
        {
            Assert.Equal(38, Actor.CurrentHealth);
            Assert.Equal(52, Nigel.CurrentHealth);
            Assert.Equal(new GridPosition(6, 5), Actor.TacticalPosition);
            Assert.Equal(new GridPosition(5, 5), Nigel.TacticalPosition);
            Assert.False(Actor.HasActedThisRound);
            Assert.False(Manager.MainActionUsed);
            Assert.False(Manager.UtilityActionUsed);
            Assert.False(Manager.MovementUsed);
            Assert.Empty(Actor.Cooldowns.Cooldowns);
        }
    }
}
