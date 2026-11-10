using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class SanctuaryShieldAbilityIntegrationTests
{
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character, team: AbilityTargetTeam.Ally,
        requireLivingTarget: true, requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);

    [Fact]
    public void Registration_PreservesConfirmedMetadataAndLeavesRangeAndSpellTagUnresolved()
    {
        var definition = CharacterRegistry.GetCharacter("gentle_guardian")!;
        var ability = Assert.Single(definition.Abilities, a => a.Id == "sanctuary_shield");
        Assert.Equal("Sanctuary Shield", ability.DisplayName);
        Assert.Equal(AbilityClassification.Active, ability.Classification);
        Assert.Equal(AbilityActionType.MainAction, ability.RequiredAction);
        Assert.Equal(0, ability.ReuseInterval);
        Assert.Null(ability.Range);
        Assert.Null(ability.IsSpellTagged);
        Assert.Same(definition, CharacterFactory.Create(definition.Id, 1)!.Definition);
        foreach (var other in CharacterRegistry.GetAllCharacters().Where(c => c.Id != definition.Id))
            Assert.DoesNotContain(other.Abilities, a => a.Id == "sanctuary_shield");
    }

    [Fact]
    public void ActiveGuardianAndAlly_GetAnExplicitUnresolvedRangeResultWithoutApplyingProtection()
    {
        var s = new Setup();
        var preview = s.Preview();
        Assert.True(preview.Eligibility.IsAllowed);
        Assert.Equal(s.Caster.Id, preview.CasterId);
        Assert.Equal(s.Target.Id, preview.RecipientId);
        Assert.Equal(2, preview.RoundNumber);
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, preview.Targeting!.RejectionReason);
        Assert.True(preview.Application!.IsAllowed);
        Assert.Equal(SanctuaryShieldOperation.Grant, preview.Application.Operation);
        Assert.Equal(6, preview.Application.ExpectedShieldHP);
        Assert.False(preview.CanExecute);
        Assert.False(preview.Succeeded);
        Assert.Equal(0, preview.ShieldHPGranted);
        Assert.Equal(0, preview.ResultingShieldHP);
        Assert.Null(preview.Finalization);
        var result = s.Execute();
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, result.Targeting!.RejectionReason);
        s.AssertUnspent();
        Assert.Equal(0, s.Target.ShieldHP);
        Assert.Null(s.Manager.GetRecipient(s.Caster));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    public void NoUnconfirmedRangeBoundaryIsAssumed(int distance)
    {
        var s = new Setup();
        s.Target.SetTacticalPosition(new(5 + distance, 5));
        var result = s.Execute();
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, result.Targeting!.RejectionReason);
        Assert.False(result.CanExecute);
        Assert.Equal(0, s.Target.ShieldHP);
        s.AssertUnspent();
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("ended", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("acted", AbilityRejectionReason.ActionUnavailable)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("unknown", AbilityRejectionReason.AbilityNotOwned)]
    [InlineData("healing", AbilityRejectionReason.UnsupportedAbility)]
    [InlineData("passive", AbilityRejectionReason.NotManuallyActivatable)]
    public void AbilityRejections_DoNotChangeShieldOrOwnership(string invalid, AbilityRejectionReason expected)
    {
        var s = new Setup();
        s.Manager.Apply(s.Caster, s.Target);
        s.Target.TakeDamage(4);
        switch (invalid)
        {
            case "dead": s.Caster.TakeDamage(int.MaxValue); break;
            case "inactive": s.Activations.CompleteActivation(); break;
            case "ended": s.Activations.CompleteMainAction(); break;
            case "acted": s.Caster.EndActivation(); break;
            case "cooldown": s.Caster.Cooldowns.RecordUse("sanctuary_shield", 2, 3); break;
            case "silence": s.Caster.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "unknown": s.Use = new(s.Caster, "unknown", s.Activations); break;
            case "healing": s.Use = new(s.Caster, "healing_light", s.Activations); break;
            case "passive": s.Use = new(s.Caster, "gentle_blessing", s.Activations); break;
        }
        var result = s.Execute();
        Assert.Equal(expected, result.Eligibility.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Equal(2, s.Target.ShieldHP);
        Assert.Same(s.Target, s.Manager.GetRecipient(s.Caster));
        Assert.Equal(invalid == "cooldown" ? 1 : 0, s.Caster.Cooldowns.Cooldowns.Count);
        Assert.Null(result.Finalization);
    }

    [Fact]
    public void WrongCharacter_CannotUseShieldEvenWithCopiedAbilityMetadata()
    {
        var actor = CharacterFactory.Create("nigel", 1)!;
        var s = new Setup(caster: actor);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute().Eligibility.RejectionReason);
        s.AssertUnspent();
        var template = actor.Definition!;
        var copied = CharacterFactory.Create(new CharacterDefinition(template.Id, template.Name, template.Description,
            template.BaseStats, GentleGuardian.Definition.Abilities), "copied", 1);
        var second = new Setup(caster: copied);
        Assert.Equal(SanctuaryShieldAbilityRejectionReason.NotGentleGuardian, second.Execute().RejectionReason);
        second.AssertUnspent();
    }

    [Fact]
    public void AnotherGuardian_CannotSpendTheActiveGuardiansAction()
    {
        var s = new Setup();
        var other = CharacterFactory.Create("gentle_guardian", 1)!;
        other.SetTacticalPosition(new(5, 5));
        var use = new AbilityUse(other, "sanctuary_shield", s.Activations);
        Assert.Equal(AbilityRejectionReason.CharacterNotActive,
            s.Executor.Execute(use, s.Target, Rules).Eligibility.RejectionReason);
        s.AssertUnspent();
    }

    [Fact]
    public void MissingPositions_AreRejectedBeforeAnyShieldChange()
    {
        var s = new Setup();
        var unplaced = CharacterFactory.Create("nigel", 1)!;
        var result = s.Executor.Execute(s.Use, unplaced, Rules);
        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation, result.Targeting!.RejectionReason);
        Assert.Equal(0, unplaced.ShieldHP);
        s.AssertUnspent();
        var source = new Setup(setPosition: false);
        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation, source.Execute().Targeting!.RejectionReason);
        source.AssertUnspent();
    }

    [Fact]
    public void EnemyTarget_IsNotShieldedWhileRangeRemainsUnresolved()
    {
        var s = new Setup();
        s.Enemy.SetTacticalPosition(new(6, 5));
        var result = s.Executor.Execute(s.Use, s.Enemy, Rules);
        Assert.False(result.CanExecute);
        // AbilityTargeting reports missing range before checking team restrictions.
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, result.Targeting!.RejectionReason);
        Assert.Equal(0, s.Enemy.ShieldHP);
        Assert.Null(s.Manager.GetRecipient(s.Caster));
        s.AssertUnspent();
    }

    [Fact]
    public void DefeatedRecipient_IsIneligibleForShieldAssignment()
    {
        var s = new Setup();
        s.Target.TakeDamage(int.MaxValue);
        var result = s.Execute();
        Assert.False(result.CanExecute);
        Assert.Equal(SanctuaryShieldRejectionReason.RecipientDefeated, result.Application!.RejectionReason);
        Assert.Equal(0, s.Target.ShieldHP);
        Assert.True(s.Target.IsDefeated);
        s.AssertUnspent();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void SelfPermissionDoesNotSupplyTheMissingRange(bool? self)
    {
        var s = new Setup();
        var rules = new AbilityTargetingConstraints(allowSelf: self, requiresMapValidation: false,
            requiresLineOfSight: false, requiresElevationRules: false);
        var result = s.Executor.Execute(s.Use, s.Caster, rules);
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, result.Targeting!.RejectionReason);
        Assert.Equal(0, s.Caster.ShieldHP);
        s.AssertUnspent();
    }

    [Fact]
    public void ExistingManager_PreviewsGrantAndRefreshWithoutApplyingEither()
    {
        var s = new Setup();
        var grant = s.Manager.Preview(s.Caster, s.Target);
        Assert.Equal(SanctuaryShieldOperation.Grant, grant.Operation);
        Assert.Equal(6, grant.ExpectedShieldHP);
        Assert.Null(grant.PreviousRecipientId);
        Assert.Equal(0, s.Target.ShieldHP);
        // Exercise only the fully specified low-level shield rule; no ability range is assumed.
        s.Manager.Apply(s.Caster, s.Target);
        s.Target.TakeDamage(4);
        var refresh = s.Preview();
        Assert.Equal(SanctuaryShieldOperation.Refresh, refresh.Application!.Operation);
        Assert.Equal(s.Target.Id, refresh.PreviousRecipientId);
        Assert.Equal(2, refresh.Application.RecipientShieldHP);
        Assert.Equal(6, refresh.Application.ExpectedShieldHP);
        Assert.False(refresh.Refreshed);
        Assert.Equal(2, s.Target.ShieldHP);
        Assert.False(s.Execute().Succeeded);
        Assert.Equal(2, s.Target.ShieldHP);
        s.Manager.Apply(s.Caster, s.Target);
        Assert.Equal(6, s.Target.ShieldHP);
        Assert.Equal(52, s.Target.CurrentHealth);
        s.AssertUnspent();
    }

    [Fact]
    public void ExistingManager_PreviewsTransferAndPreservesPreviousProtectionWhenAbilityCannotCast()
    {
        var s = new Setup();
        var next = CharacterFactory.Create("nigel", 1)!;
        next.SetTacticalPosition(new(7, 5));
        s.Manager.Apply(s.Caster, s.Target);
        var preview = s.Executor.Preview(s.Use, next, Rules);
        Assert.Equal(SanctuaryShieldOperation.Transfer, preview.Application!.Operation);
        Assert.Equal(s.Target.Id, preview.PreviousRecipientId);
        Assert.Equal(6, preview.Application.PreviousRecipientShieldHP);
        Assert.Equal(6, s.Target.ShieldHP);
        Assert.Equal(0, next.ShieldHP);
        Assert.False(s.Executor.Execute(s.Use, next, Rules).Transferred);
        Assert.Equal(6, s.Target.ShieldHP);
        Assert.Same(s.Target, s.Manager.GetRecipient(s.Caster));
        s.AssertUnspent();
        // The existing manager's confirmed transfer remains available independently of casting rules.
        new SanctuaryShieldManager().Apply(s.Caster, next);
        Assert.Equal(0, s.Target.ShieldHP);
        Assert.Equal(6, next.ShieldHP);
        Assert.Same(next, s.Manager.GetRecipient(s.Caster));
        Assert.Equal(6, preview.Application.PreviousRecipientShieldHP);
    }

    [Fact]
    public void DifferentCasters_KeepIndependentRecipientsAndConflictingSourcesStayUnsupported()
    {
        var s = new Setup();
        var otherGuardian = CharacterFactory.Create("gentle_guardian", 1)!;
        var otherTarget = CharacterFactory.Create("nigel", 1)!;
        otherTarget.SetTacticalPosition(new(7, 5));
        s.Manager.Apply(s.Caster, s.Target);
        s.Manager.Apply(otherGuardian, otherTarget);
        var preview = s.Executor.Preview(s.Use, otherTarget, Rules);
        Assert.Equal(SanctuaryShieldRejectionReason.OtherShieldSourceUnsupported, preview.Application!.RejectionReason);
        Assert.False(s.Executor.Execute(s.Use, otherTarget, Rules).Succeeded);
        Assert.Equal(6, s.Target.ShieldHP);
        Assert.Equal(6, otherTarget.ShieldHP);
        Assert.Same(s.Target, s.Manager.GetRecipient(s.Caster));
        Assert.Same(otherTarget, s.Manager.GetRecipient(otherGuardian));
        Assert.Empty(otherGuardian.Cooldowns.Cooldowns);
        s.AssertUnspent();
    }

    [Fact]
    public void UnownedShield_AlsoRejectsReplacementRatherThanInventingStacking()
    {
        var s = new Setup();
        s.Target.SetShieldHP(4);
        var result = s.Execute();
        Assert.Equal(SanctuaryShieldRejectionReason.OtherShieldSourceUnsupported, result.Application!.RejectionReason);
        Assert.Equal(4, s.Target.ShieldHP);
        Assert.Null(s.Manager.GetRecipient(s.Caster));
        s.AssertUnspent();
    }

    [Fact]
    public void ExistingShield_AbsorbsDamageAndPersistsAfterCasterDefeat()
    {
        var s = new Setup();
        s.Manager.Apply(s.Caster, s.Target);
        var stats = s.Target.BaseStats;
        s.Caster.TakeDamage(int.MaxValue);
        Assert.Equal(6, s.Target.ShieldHP);
        Assert.Same(s.Target, s.Manager.GetRecipient(s.Caster));
        Assert.Equal(2, s.Target.TakeDamage(8));
        Assert.Equal(0, s.Target.ShieldHP);
        Assert.Equal(50, s.Target.CurrentHealth);
        Assert.Same(stats, s.Target.BaseStats);
        Assert.Equal(52, stats.MaxHealth);
        Assert.Null(s.Manager.GetRecipient(s.Caster));
    }

    [Fact]
    public void PreviewIsFreshAfterOwnershipChanges_AndRejectedUseNeverTransfers()
    {
        var s = new Setup();
        var first = s.Preview();
        Assert.Equal(SanctuaryShieldOperation.Grant, first.Application!.Operation);
        s.Manager.Apply(s.Caster, s.Target);
        s.Target.TakeDamage(3);
        var result = s.Execute();
        Assert.Equal(SanctuaryShieldOperation.Refresh, result.Application!.Operation);
        Assert.Equal(3, result.Application.RecipientShieldHP);
        Assert.Equal(SanctuaryShieldOperation.Grant, first.Application.Operation);
        Assert.Equal(3, s.Target.ShieldHP);
        s.AssertUnspent();
    }

    [Fact]
    public void StalePositionsAndRecipientDefeat_AreRevalidatedBeforeAnyOperation()
    {
        var s = new Setup();
        var preview = s.Preview();
        s.Target.SetTacticalPosition(new(100, 100));
        s.Target.TakeDamage(int.MaxValue);
        var result = s.Execute();
        Assert.Equal(SanctuaryShieldRejectionReason.RecipientDefeated, result.Application!.RejectionReason);
        Assert.Equal(new GridPosition(100, 100), result.Targeting!.TargetPosition);
        Assert.Equal(new GridPosition(6, 5), preview.Targeting!.TargetPosition);
        Assert.Equal(0, s.Target.ShieldHP);
        s.AssertUnspent();
    }

    [Fact]
    public void WrongPhaseAndStaleRound_AreRejectedBeforeShieldOperations()
    {
        var s = new Setup();
        s.Lifecycle.AdvancePhase();
        Assert.Equal(AbilityRejectionReason.WrongPhase, s.Execute().Eligibility.RejectionReason);
        while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        Assert.Equal(AbilityRejectionReason.UseContextChanged, s.Execute().Eligibility.RejectionReason);
        s.AssertUnspent();
    }

    [Fact]
    public void RepeatedRejectedUse_DoesNotApplyShieldOrFinalizeActions()
    {
        var s = new Setup();
        for (int i = 0; i < 3; i++)
        {
            var result = s.Execute();
            Assert.False(result.ActionFinalized);
            Assert.False(result.Succeeded);
        }
        Assert.Equal(0, s.Target.ShieldHP);
        Assert.Null(s.Manager.GetRecipient(s.Caster));
        s.AssertUnspent();
    }

    [Fact]
    public void PreviewAndRejectedExecution_PreserveAllUnrelatedStateAndSnapshotValues()
    {
        var s = new Setup();
        s.Target.TakeDamage(10);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.Target.Statuses.ApplyPoison();
        HealingApplicator.Apply(0, s.Target);
        s.Target.EndActivation();
        s.Caster.Cooldowns.RecordUse("other", 2, 3);
        s.Manager.Apply(s.Caster, s.Target);
        var stats = s.Target.BaseStats;
        var result = s.Execute();
        Assert.Equal(42, s.Target.CurrentHealth);
        Assert.Equal(6, s.Target.ShieldHP);
        Assert.Equal(1, s.Target.HealsReceivedThisRound);
        Assert.True(s.Target.HasActedThisRound);
        Assert.Equal(2, s.Target.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(s.Target.Statuses.IsPoisoned);
        Assert.Equal(0, s.Target.Statuses.PoisonTicksOccurred);
        Assert.Same(stats, s.Target.BaseStats);
        Assert.Equal(5, s.Caster.Cooldowns.Cooldowns["other"].ReadyRound);
        Assert.True(s.Activations.CanUseMainAction);
        Assert.False(s.Activations.MainActionUsed);
        s.Target.TakeDamage(3);
        Assert.Equal(6, result.ResultingShieldHP);
        Assert.Equal(6, result.Application!.RecipientShieldHP);
        Assert.All(typeof(SanctuaryShieldUseResult).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.All(typeof(SanctuaryShieldPreview).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void NullProgrammingInputs_AreRejectedBeforeMutation()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => new SanctuaryShieldAbilityExecutor(null!));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(null!, s.Target, Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, null!, Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, s.Target, null!));
        Assert.Throws<ArgumentNullException>(() => s.Manager.Preview(null!, s.Target));
        Assert.Throws<ArgumentNullException>(() => s.Manager.Preview(s.Caster, null!));
        s.AssertUnspent();
    }

    private sealed class Setup
    {
        public Character Caster { get; }
        public Character Target { get; } = CharacterFactory.Create("nigel", 1)!;
        public Character Enemy { get; } = CharacterFactory.Create("hooded_assassin", 2)!;
        public RoundLifecycleManager Lifecycle { get; } = new(2);
        public ActivationManager Activations { get; }
        public AbilityUse Use { get; set; }
        public SanctuaryShieldManager Manager { get; } = new();
        public SanctuaryShieldAbilityExecutor Executor { get; }

        public Setup(Character? caster = null, bool setPosition = true)
        {
            Caster = caster ?? CharacterFactory.Create("gentle_guardian", 1)!;
            if (setPosition) Caster.SetTacticalPosition(new(5, 5));
            Target.SetTacticalPosition(new(6, 5));
            Enemy.SetTacticalPosition(new(12, 12));
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Activations = new(new[] { Caster, Target }, new[] { Enemy }, 1, Lifecycle);
            Activations.StartActivation(Caster);
            Use = new(Caster, "sanctuary_shield", Activations);
            Executor = new(Manager);
        }

        public SanctuaryShieldUseResult Preview() => Executor.Preview(Use, Target, Rules);
        public SanctuaryShieldUseResult Execute() => Executor.Execute(Use, Target, Rules);
        public void AssertUnspent()
        {
            Assert.Empty(Caster.Cooldowns.Cooldowns);
            Assert.Same(Caster, Activations.ActiveCharacter);
            Assert.True(Activations.CanUseMainAction);
            Assert.False(Activations.MainActionUsed);
            Assert.False(Activations.MovementUsed);
            Assert.False(Activations.UtilityActionUsed);
        }
    }
}
