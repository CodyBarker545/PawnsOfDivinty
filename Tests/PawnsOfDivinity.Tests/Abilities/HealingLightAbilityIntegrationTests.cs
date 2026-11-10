using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class HealingLightAbilityIntegrationTests
{
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character, team: AbilityTargetTeam.Ally,
        requireLivingTarget: true, requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);

    [Fact]
    public void Registration_PreservesRosterFactoryAndConfirmedGuardianMetadata()
    {
        var definition = CharacterRegistry.GetCharacter("gentle_guardian")!;
        Assert.Same(GentleGuardian.Definition, definition);
        Assert.Equal(new[] { "healing_light", "sanctuary_shield", "gentle_blessing" }, definition.Abilities.Select(a => a.Id));
        Assert.Equal(new[] { "Healing Light", "Sanctuary Shield", "Gentle Blessing" }, definition.Abilities.Select(a => a.DisplayName));
        var heal = definition.Abilities[0];
        Assert.Equal(AbilityClassification.Active, heal.Classification);
        Assert.Equal(AbilityActionType.MainAction, heal.RequiredAction);
        Assert.Equal(0, heal.ReuseInterval);
        Assert.Equal(AbilityRangeKind.ManhattanDistance, heal.Range!.Kind);
        Assert.Equal(0, heal.Range.Minimum);
        Assert.Equal(3, heal.Range.Maximum);
        var shield = definition.Abilities[1];
        Assert.Equal(AbilityClassification.Active, shield.Classification);
        Assert.Equal(AbilityActionType.MainAction, shield.RequiredAction);
        Assert.Equal(0, shield.ReuseInterval);
        Assert.Null(shield.Range);
        var blessing = definition.Abilities[2];
        Assert.Equal(AbilityClassification.Passive, blessing.Classification);
        Assert.Null(blessing.RequiredAction);
        Assert.Null(blessing.ReuseInterval);
        Assert.Null(blessing.Range);
        Assert.All(definition.Abilities, a => { Assert.Null(a.IsSpellTagged); Assert.NotEmpty(a.Description); });
        Assert.Equal(6, CharacterRegistry.GetAllCharacters().Count());
        foreach (var registered in CharacterRegistry.GetAllCharacters())
        {
            var character = CharacterFactory.Create(registered.Id, 1)!;
            Assert.Same(registered, character.Definition);
            Assert.Same(registered.BaseStats, character.BaseStats);
            if (registered.Id != "gentle_guardian")
                Assert.DoesNotContain(registered.Abilities, a => a.Id is "healing_light" or "sanctuary_shield" or "gentle_blessing");
        }
        Assert.Throws<NotSupportedException>(() => ((IList<AbilityDefinition>)definition.Abilities).Clear());
    }

    [Fact]
    public void Preview_PredictsElevenFromWisdomWithoutRecordingAnEventOrAction()
    {
        var s = new Setup();
        var preview = s.Preview();
        Assert.True(preview.CanExecute);
        Assert.False(preview.Succeeded);
        Assert.Equal(s.Caster.Id, preview.CasterId);
        Assert.Equal(s.Target.Id, preview.TargetId);
        Assert.Equal(2, preview.RoundNumber);
        Assert.Equal(11, preview.BaseHealing);
        Assert.Equal(1, preview.HealOrdinal);
        Assert.Equal(100, preview.EffectivenessPercentage);
        Assert.Equal(11, preview.ExpectedAdjustedHealing);
        Assert.Equal(11, preview.ExpectedHPRestored);
        Assert.Equal(0, preview.ActualHPRestored);
        Assert.Equal(12, preview.TargetHealthBefore);
        Assert.Equal(12, preview.TargetHealthAfter);
        Assert.Equal(23, preview.PreviewHealing!.ResultingHealth);
        Assert.Null(preview.Healing);
        Assert.False(preview.ActionFinalized);
        s.AssertUntouched(12);
    }

    [Fact]
    public void SuccessfulHeal_RecordsOneEventAndFinalizesTheMainActionExactlyOnce()
    {
        var s = new Setup();
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(11, result.ActualHPRestored);
        Assert.Equal(12, result.TargetHealthBefore);
        Assert.Equal(23, result.TargetHealthAfter);
        Assert.Equal(23, s.Target.CurrentHealth);
        Assert.Equal(1, s.Target.HealsReceivedThisRound);
        Assert.Equal(0, s.Caster.HealsReceivedThisRound);
        Assert.True(s.Activations.MainActionUsed);
        Assert.False(s.Activations.MovementUsed);
        Assert.False(s.Activations.UtilityActionUsed);
        Assert.Null(s.Activations.ActiveCharacter);
        Assert.True(s.Caster.HasActedThisRound);
        Assert.Equal(2, result.Finalization!.ReadyRound);
        var cooldown = Assert.Single(s.Caster.Cooldowns.Cooldowns).Value;
        Assert.Equal(0, cooldown.ReuseInterval);
        Assert.Equal(2, cooldown.RoundUsed);
        Assert.Equal(2, cooldown.ReadyRound);
        Assert.True(s.Caster.Cooldowns.IsReady("healing_light", 2));
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, s.Execute().Eligibility.RejectionReason);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, AbilityUseFinalizer.FinalizeSuccessfulUse(s.Use).Eligibility.RejectionReason);
        Assert.Equal(23, s.Target.CurrentHealth);
        Assert.Equal(1, s.Target.HealsReceivedThisRound);
    }

    [Theory]
    [InlineData(0, 100, 11)]
    [InlineData(1, 75, 8)]
    [InlineData(2, 50, 5)]
    [InlineData(3, 25, 2)]
    [InlineData(4, 25, 2)]
    public void ReceivedHealOrdinal_DeterminesEffectivenessAndProvisionalFloorRounding(int received, int percentage, int restored)
    {
        var s = new Setup();
        for (int i = 0; i < received; i++) HealingApplicator.Apply(0, s.Target);
        var preview = s.Preview();
        Assert.Equal(received, s.Target.HealsReceivedThisRound);
        Assert.Equal(received + 1, preview.HealOrdinal);
        Assert.Equal(percentage, preview.EffectivenessPercentage);
        Assert.Equal(restored, preview.ExpectedAdjustedHealing);
        var result = s.Execute();
        Assert.Equal(restored, result.ActualHPRestored);
        Assert.Equal(received + 1, s.Target.HealsReceivedThisRound);
    }

    [Fact]
    public void SeveralHealers_ShareTheRecipientsHistoryWithoutSharingTheirCooldowns()
    {
        var target = CharacterFactory.Create("nigel", 1)!;
        target.TakeDamage(40);
        int[] amounts = { 11, 8, 5, 2, 2 };
        var healers = new List<Character>();
        for (int i = 0; i < amounts.Length; i++)
        {
            var s = new Setup(target: target);
            healers.Add(s.Caster);
            Assert.Empty(s.Caster.Cooldowns.Cooldowns);
            Assert.Equal(amounts[i], s.Execute().ActualHPRestored);
            Assert.Equal(i + 1, target.HealsReceivedThisRound);
        }
        Assert.Equal(40, target.CurrentHealth);
        Assert.All(healers, healer => Assert.Single(healer.Cooldowns.Cooldowns));
        Assert.Empty(target.Cooldowns.Cooldowns);
        Assert.Equal(5, healers.Select(h => h.Id).Distinct().Count());
    }

    [Fact]
    public void DifferentRecipients_HaveIndependentReceivedHealingHistory()
    {
        var first = new Setup();
        var second = new Setup();
        HealingApplicator.Apply(0, first.Target);
        Assert.Equal(75, first.Preview().EffectivenessPercentage);
        Assert.Equal(100, second.Preview().EffectivenessPercentage);
        Assert.Equal(8, first.Execute().ActualHPRestored);
        Assert.Equal(11, second.Execute().ActualHPRestored);
        Assert.Equal(2, first.Target.HealsReceivedThisRound);
        Assert.Equal(1, second.Target.HealsReceivedThisRound);
    }

    [Theory]
    [InlineData(5, 6, 1, true)]
    [InlineData(8, 5, 3, true)]
    [InlineData(6, 7, 3, true)]
    [InlineData(9, 5, 4, false)]
    [InlineData(7, 7, 4, false)]
    public void RangeUsesInclusiveManhattanDistance(int x, int y, int distance, bool legal)
    {
        var s = new Setup();
        s.Target.SetTacticalPosition(new(x, y));
        var result = s.Execute();
        Assert.Equal((long)distance, result.Targeting!.Distance);
        Assert.Equal(legal, result.Succeeded);
        if (!legal)
        {
            Assert.Equal(AbilityTargetingRejectionReason.OutsideMaximumRange, result.Targeting.RejectionReason);
            s.AssertUntouched(12);
        }
    }

    [Fact]
    public void HpCap_PreservesCalculatedHealingAndReportsOnlyActualRestoration()
    {
        var s = new Setup();
        s.Target.Heal(39);
        var preview = s.Preview();
        Assert.Equal(11, preview.ExpectedAdjustedHealing);
        Assert.Equal(1, preview.ExpectedHPRestored);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.ActualHPRestored);
        Assert.Equal(52, result.TargetHealthAfter);
        Assert.Equal(1, s.Target.HealsReceivedThisRound);
    }

    [Fact]
    public void FullHealthTarget_ReportsUnresolvedActionRuleWithoutConsumingAnything()
    {
        var s = new Setup();
        s.Target.Heal(int.MaxValue);
        var preview = s.Preview();
        Assert.Equal(HealingLightRejectionReason.FullHealthActionRuleUnresolved, preview.RejectionReason);
        Assert.Equal(0, preview.ExpectedHPRestored);
        Assert.Equal(100, preview.EffectivenessPercentage);
        Assert.False(preview.CanExecute);
        var result = s.Execute();
        Assert.Equal(HealingLightRejectionReason.FullHealthActionRuleUnresolved, result.RejectionReason);
        s.AssertUntouched(52);
    }

    [Fact]
    public void DefeatedRecipient_IsRejectedWithoutRevivalOrAnEvent()
    {
        var s = new Setup();
        s.Target.TakeDamage(int.MaxValue);
        var result = s.Execute();
        Assert.Equal(AbilityTargetingRejectionReason.TargetDefeated, result.Targeting!.RejectionReason);
        s.AssertUntouched(0);
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("ended", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("acted", AbilityRejectionReason.ActionUnavailable)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("unknown", AbilityRejectionReason.AbilityNotOwned)]
    [InlineData("shield", AbilityRejectionReason.UnsupportedAbility)]
    [InlineData("blessing", AbilityRejectionReason.NotManuallyActivatable)]
    public void AbilityRejections_DoNotHealOrFinalize(string invalid, AbilityRejectionReason expected)
    {
        var s = new Setup();
        switch (invalid)
        {
            case "dead": s.Caster.TakeDamage(int.MaxValue); break;
            case "inactive": s.Activations.CompleteActivation(); break;
            case "ended": s.Activations.CompleteMainAction(); break;
            case "acted": s.Caster.EndActivation(); break;
            case "cooldown": s.Caster.Cooldowns.RecordUse("healing_light", 2, 3); break;
            case "silence": s.Caster.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "unknown": s.Use = new(s.Caster, "unknown", s.Activations); break;
            case "shield": s.Use = new(s.Caster, "sanctuary_shield", s.Activations); break;
            case "blessing": s.Use = new(s.Caster, "gentle_blessing", s.Activations); break;
        }
        var result = s.Execute();
        Assert.Equal(expected, result.Eligibility.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Equal(12, s.Target.CurrentHealth);
        Assert.Equal(0, s.Target.HealsReceivedThisRound);
        Assert.Equal(invalid == "cooldown" ? 1 : 0, s.Caster.Cooldowns.Cooldowns.Count);
    }

    [Fact]
    public void WrongCharacter_CannotUseHealingLightOrGainItByCopyingMetadata()
    {
        var other = CharacterFactory.Create("nigel", 1)!;
        var s = new Setup(caster: other);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute().Eligibility.RejectionReason);
        s.AssertUntouched(12);
        var original = other.Definition!;
        var copied = CharacterFactory.Create(new CharacterDefinition(original.Id, original.Name, original.Description,
            original.BaseStats, GentleGuardian.Definition.Abilities), "copied", 1);
        var copy = new Setup(caster: copied);
        Assert.Equal(HealingLightRejectionReason.NotGentleGuardian, copy.Execute().RejectionReason);
        copy.AssertUntouched(12);
    }

    [Fact]
    public void EnemyTarget_IsRejectedEvenIfCallerSuppliesBroaderTeamPermissions()
    {
        var s = new Setup();
        s.Enemy.SetTacticalPosition(new(6, 5));
        s.Enemy.TakeDamage(10);
        var broad = new AbilityTargetingConstraints(AbilityTargetType.Character, true, AbilityTargetTeam.Any,
            false, false, false, false);
        var result = s.Executor.Execute(s.Use, s.Enemy, broad);
        Assert.Equal(AbilityTargetingRejectionReason.WrongTeam, result.Targeting!.RejectionReason);
        Assert.Equal(0, s.Enemy.HealsReceivedThisRound);
        s.AssertUntouched(12);
    }

    [Theory]
    [InlineData(null, AbilityTargetingRejectionReason.UnresolvedTargetingRule)]
    [InlineData(false, AbilityTargetingRejectionReason.InvalidSelfTarget)]
    public void SelfTargeting_IsNotSilentlyInferredFromAllyWording(bool? self, AbilityTargetingRejectionReason reason)
    {
        var s = new Setup();
        s.Caster.TakeDamage(10);
        var rules = new AbilityTargetingConstraints(allowSelf: self, requiresMapValidation: false,
            requiresLineOfSight: false, requiresElevationRules: false);
        var result = s.Executor.Execute(s.Use, s.Caster, rules);
        Assert.Equal(reason, result.Targeting!.RejectionReason);
        Assert.Equal(34, s.Caster.CurrentHealth);
        Assert.Equal(0, s.Caster.HealsReceivedThisRound);
        s.AssertUntouched(12);
    }

    [Fact]
    public void MissingPositionsAndUnresolvedLos_AreRejectedWithoutMutation()
    {
        var s = new Setup();
        var unplaced = CharacterFactory.Create("nigel", 1)!;
        unplaced.TakeDamage(10);
        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation,
            s.Executor.Execute(s.Use, unplaced, Rules).Targeting!.RejectionReason);
        var los = new AbilityTargetingConstraints(requiresMapValidation: false, requiresLineOfSight: true,
            requiresElevationRules: false);
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedLineOfSight,
            s.Executor.Execute(s.Use, s.Target, los).Targeting!.RejectionReason);
        s.AssertUntouched(12);
    }

    [Fact]
    public void Execution_RechecksHistoryInsteadOfApplyingStalePreviewAmounts()
    {
        var s = new Setup();
        var preview = s.Preview();
        HealingApplicator.Apply(0, s.Target);
        var result = s.Execute();
        Assert.Equal(11, preview.ExpectedHPRestored);
        Assert.Equal(1, preview.HealOrdinal);
        Assert.Equal(8, result.ActualHPRestored);
        Assert.Equal(2, result.HealOrdinal);
        Assert.Equal(2, s.Target.HealsReceivedThisRound);
    }

    [Theory]
    [InlineData("position")]
    [InlineData("full")]
    public void Execution_RechecksTargetPositionAndHealthAfterPreview(string change)
    {
        var s = new Setup();
        Assert.True(s.Preview().CanExecute);
        if (change == "position") s.Target.SetTacticalPosition(new(10, 5));
        else s.Target.Heal(int.MaxValue);
        var result = s.Execute();
        Assert.False(result.CanExecute);
        if (change == "position") Assert.Equal(AbilityTargetingRejectionReason.OutsideMaximumRange, result.Targeting!.RejectionReason);
        else Assert.Equal(HealingLightRejectionReason.FullHealthActionRuleUnresolved, result.RejectionReason);
        s.AssertUntouched(change == "position" ? 12 : 52);
    }

    [Fact]
    public void WrongPhaseAndChangedRound_AreRejected()
    {
        var s = new Setup();
        s.Lifecycle.AdvancePhase();
        Assert.Equal(AbilityRejectionReason.WrongPhase, s.Execute().Eligibility.RejectionReason);
        while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        Assert.Equal(AbilityRejectionReason.UseContextChanged, s.Execute().Eligibility.RejectionReason);
        s.AssertUntouched(12);
    }

    [Fact]
    public void RoundPreparation_ResetsHistoryAndPreservesAbsoluteCooldowns()
    {
        var s = new Setup();
        Assert.True(s.Execute().Succeeded);
        s.Caster.Cooldowns.RecordUse("other", 2, 3);
        var roster = new[] { s.Caster, s.Target, s.Enemy };
        while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        s.Lifecycle.AdvancePhase();
        new PoisonBatchProcessor(s.Lifecycle).Process(roster);
        s.Lifecycle.AdvancePhase();
        new DelayedAttackProcessor(new(), s.Lifecycle).Process(roster);
        s.Lifecycle.AdvancePhase();
        new TimedStatusBatchProcessor(s.Lifecycle).Process(roster);
        s.Lifecycle.AdvancePhase();
        s.Lifecycle.AdvancePhase();
        new RoundPreparationCoordinator(s.Activations, s.Lifecycle).Process(roster, 1);
        Assert.Equal(0, s.Target.HealsReceivedThisRound);
        Assert.Equal(5, s.Caster.Cooldowns.Cooldowns["other"].ReadyRound);
        Assert.Equal(2, s.Caster.Cooldowns.Cooldowns["healing_light"].ReadyRound);
        s.Lifecycle.AdvancePhase();
        s.Activations.StartActivation(s.Caster);
        s.Use = new(s.Caster, "healing_light", s.Activations);
        Assert.Equal(100, s.Preview().EffectivenessPercentage);
        Assert.Equal(11, s.Execute().ActualHPRestored);
        Assert.Equal(1, s.Target.HealsReceivedThisRound);
    }

    [Fact]
    public void Healing_PreservesShieldsStatusesAndBaseStats_WhileApplyingGentleBlessing()
    {
        var s = new Setup();
        s.Target.SetShieldHP(6);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.Target.Statuses.ApplyPoison();
        s.Target.EndActivation();
        var stats = s.Target.BaseStats;
        int defense = s.Target.EffectiveDefense;
        int resistance = stats.Resistance;
        var casterStats = s.Caster.BaseStats;
        s.Caster.Cooldowns.RecordUse("other", 2, 3);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(6, s.Target.ShieldHP);
        Assert.True(s.Target.HasActedThisRound);
        Assert.Equal(2, s.Target.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(s.Target.Statuses.IsPoisoned);
        Assert.Equal(0, s.Target.Statuses.PoisonTicksOccurred);
        Assert.Same(stats, s.Target.BaseStats);
        Assert.Same(casterStats, s.Caster.BaseStats);
        Assert.Equal(defense + 2, s.Target.EffectiveDefense);
        Assert.Equal(resistance, s.Target.BaseStats.Resistance);
        Assert.Equal(5, s.Caster.Cooldowns.Cooldowns["other"].ReadyRound);
        Assert.Equal(44, s.Caster.CurrentHealth);
        Assert.Equal(0, s.Caster.ShieldHP);
    }

    [Fact]
    public void ResultsRemainImmutableAfterLaterDamage()
    {
        var s = new Setup();
        var preview = s.Preview();
        var result = s.Execute();
        s.Target.TakeDamage(int.MaxValue);
        Assert.Equal(11, result.ActualHPRestored);
        Assert.Equal(23, result.TargetHealthAfter);
        Assert.Equal(12, preview.TargetHealthAfter);
        Assert.All(typeof(HealingLightUseResult).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void NullProgrammingInputs_AreRejectedBeforeMutation()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(null!, s.Target, Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, null!, Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, s.Target, null!));
        s.AssertUntouched(12);
    }

    [Theory]
    [InlineData("action")]
    [InlineData("reuse")]
    [InlineData("range")]
    public void MissingRequiredMetadata_RejectsExecutionWithoutFallbackValues(string missing)
    {
        var template = GentleGuardian.Definition;
        var ability = new AbilityDefinition("healing_light", "Healing Light", "Unresolved test metadata",
            AbilityClassification.Active, requiredAction: missing == "action" ? null : AbilityActionType.MainAction,
            range: missing == "range" ? null : new(AbilityRangeKind.ManhattanDistance, 0, 3),
            reuseInterval: missing == "reuse" ? null : 0);
        var actor = CharacterFactory.Create(new CharacterDefinition(template.Id, template.Name, template.Description,
            template.BaseStats, new[] { ability }), "unresolved-guardian", 1);
        var s = new Setup(caster: actor);
        var result = s.Execute();
        Assert.False(result.Succeeded);
        if (missing == "range") Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, result.Targeting!.RejectionReason);
        else Assert.Equal(AbilityRejectionReason.MetadataUnresolved, result.Eligibility.RejectionReason);
        s.AssertUntouched(12);
    }

    [Fact]
    public void CooldownArithmetic_IsPrevalidatedBeforeRecipientHealthOrHistoryChanges()
    {
        var template = GentleGuardian.Definition;
        // Test-only interval exercises overflow protection; canonical Healing Light keeps interval zero.
        var ability = new AbilityDefinition("healing_light", "Healing Light", "Overflow fixture", AbilityClassification.Active,
            requiredAction: AbilityActionType.MainAction, range: new(AbilityRangeKind.ManhattanDistance, 0, 3),
            reuseInterval: int.MaxValue);
        var actor = CharacterFactory.Create(new CharacterDefinition(template.Id, template.Name, template.Description,
            template.BaseStats, new[] { ability }), "overflow-guardian", 1);
        var s = new Setup(caster: actor);
        Assert.Throws<OverflowException>(() => s.Execute());
        s.AssertUntouched(12);
    }

    [Fact]
    public void ZeroAdjustedHeal_PreservesExistingProvisionalEventCountingForAnInjuredRecipient()
    {
        var template = GentleGuardian.Definition;
        // WIS 0 is a test fixture, not a roster balance change. Its base heal of 2 floors to zero at 25%.
        var stats = new CharacterStats(44, 7, 11, 12, 15, 0, 14, 4);
        var actor = CharacterFactory.Create(new CharacterDefinition(template.Id, template.Name, template.Description,
            stats, template.Abilities), "zero-heal-guardian", 1);
        var s = new Setup(caster: actor);
        for (int i = 0; i < 3; i++) HealingApplicator.Apply(0, s.Target);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.BaseHealing);
        Assert.Equal(25, result.EffectivenessPercentage);
        Assert.Equal(0, result.ActualHPRestored);
        Assert.Equal(12, s.Target.CurrentHealth);
        Assert.Equal(4, s.Target.HealsReceivedThisRound);
        Assert.True(s.Activations.MainActionUsed);
    }

    [Fact]
    public void AnotherGuardian_CannotSpendTheSelectedGuardiansActivation()
    {
        var s = new Setup();
        var other = CharacterFactory.Create("gentle_guardian", 1)!;
        other.SetTacticalPosition(new(5, 5));
        var use = new AbilityUse(other, "healing_light", s.Activations);
        var result = s.Executor.Execute(use, s.Target, Rules);
        Assert.Equal(AbilityRejectionReason.CharacterNotActive, result.Eligibility.RejectionReason);
        Assert.Empty(other.Cooldowns.Cooldowns);
        s.AssertUntouched(12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EffectivenessMetadata_RejectsInvalidOrdinals(int ordinal)
        => Assert.Throws<ArgumentOutOfRangeException>(() => HealingDiminishingReturnsCalculator.GetEffectivenessPercentage(ordinal));

    private sealed class Setup
    {
        public Character Caster { get; }
        public Character Target { get; }
        public Character Enemy { get; } = CharacterFactory.Create("hooded_assassin", 2)!;
        public RoundLifecycleManager Lifecycle { get; } = new(2);
        public ActivationManager Activations { get; }
        public AbilityUse Use { get; set; }
        public HealingLightAbilityExecutor Executor { get; } = new();

        public Setup(Character? caster = null, Character? target = null)
        {
            Caster = caster ?? CharacterFactory.Create("gentle_guardian", 1)!;
            Target = target ?? CharacterFactory.Create("nigel", 1)!;
            if (target is null) Target.TakeDamage(40);
            Caster.SetTacticalPosition(new(5, 5));
            Target.SetTacticalPosition(new(8, 5));
            Enemy.SetTacticalPosition(new(12, 12));
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Activations = new(new[] { Caster, Target }, new[] { Enemy }, 1, Lifecycle);
            Activations.StartActivation(Caster);
            Use = new(Caster, "healing_light", Activations);
        }

        public HealingLightUseResult Preview() => Executor.Preview(Use, Target, Rules);
        public HealingLightUseResult Execute() => Executor.Execute(Use, Target, Rules);
        public void AssertUntouched(int targetHp)
        {
            Assert.Equal(targetHp, Target.CurrentHealth);
            Assert.Equal(0, Target.HealsReceivedThisRound);
            Assert.Empty(Caster.Cooldowns.Cooldowns);
            Assert.Same(Caster, Activations.ActiveCharacter);
            Assert.True(Activations.CanUseMainAction);
            Assert.False(Activations.MainActionUsed);
        }
    }
}
