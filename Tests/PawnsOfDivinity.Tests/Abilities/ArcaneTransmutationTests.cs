using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Equipment;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class ArcaneTransmutationTests
{
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy,
        true, requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);

    [Fact]
    public void Registration_DescribesPermanentReplacementAndImmediateAttackWithoutInventingSpellTags()
    {
        var definition = CharacterRegistry.GetCharacter("time_weaver")!;
        var ability = Assert.Single(definition.Abilities, a => a.Id == "arcane_transmutation");
        Assert.Equal("Arcane Transmutation", ability.DisplayName);
        Assert.Equal(AbilityClassification.Active, ability.Classification);
        Assert.Equal(AbilityActionType.MainAction, ability.RequiredAction);
        Assert.Equal(0, ability.ReuseInterval);
        Assert.Equal(AbilityRangeKind.EquippedWeapon, ability.Range!.Kind);
        Assert.Null(ability.Range.Minimum);
        Assert.Null(ability.Range.Maximum);
        Assert.Null(ability.IsSpellTagged);
        Assert.Same(definition, CharacterFactory.Create("time_weaver", 1)!.Definition);
        foreach (var other in CharacterRegistry.GetAllCharacters().Where(c => c.Id != "time_weaver"))
            Assert.DoesNotContain(other.Abilities, a => a.Id == "arcane_transmutation");
    }

    [Fact]
    public void DefaultRequest_ReportsMissingEquippedScrollAndReplacementAttackRange()
    {
        var s = new Setup();
        var preview = s.Preview();
        Assert.True(preview.Eligibility.IsAllowed);
        Assert.Equal(ArcaneTransmutationEffectOutcome.EquippedScrollRequired, preview.Effect!.Outcome);
        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation, preview.Targeting!.RejectionReason);
        Assert.Equal(s.Actor.Id, preview.CasterId);
        Assert.Equal(2, preview.RoundNumber);
        Assert.Empty(preview.Effect.EligibleAlternativeIds);
        var execute = s.Execute();
        Assert.Equal(preview.Eligibility, execute.Eligibility);
        Assert.Equal(preview.Effect.Outcome, execute.Effect!.Outcome);
        Assert.Equal(preview.Targeting, execute.Targeting);
        s.AssertUntouched();
    }

    [Fact]
    public void SameLevelAlternativeValidation_IsPureAndDoesNotPretendToEquipAReplacement()
    {
        var s = new Setup();
        // Explicit metadata fixtures, not a proposed canonical elemental pool.
        var pool = new[] { ("current_fixture", 1), ("higher_fixture", 2), ("alternative_b", 1), ("alternative_a", 1) };
        var result = ArcaneTransmutationEffect.Evaluate(s.Actor, "current_fixture", 1, pool);
        Assert.Equal(ArcaneTransmutationEffectOutcome.EquipmentAndAttackIntegrationRequired, result.Outcome);
        Assert.Equal(new[] { "alternative_b", "alternative_a" }, result.EligibleAlternativeIds);
        Assert.Equal("current_fixture", result.CurrentScrollId);
        Assert.Equal(1, result.CurrentLevel);
        var executed = s.Executor.Execute(s.Use, s.Target, Rules, "current_fixture", 1, pool);
        Assert.Equal(result.Outcome, executed.Effect!.Outcome);
        Assert.Equal(result.EligibleAlternativeIds, executed.Effect.EligibleAlternativeIds);
        s.AssertUntouched();
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("sameId")]
    [InlineData("otherLevel")]
    public void NoDifferentSameLevelScroll_IsAnExplicitRejection(string poolKind)
    {
        var s = new Setup();
        var pool = poolKind switch
        {
            "sameId" => new[] { ("current_fixture", 1) },
            "otherLevel" => new[] { ("other_fixture", 2) },
            _ => Array.Empty<(string, int)>()
        };
        var result = ArcaneTransmutationEffect.Evaluate(s.Actor, "current_fixture", 1, pool);
        Assert.Equal(ArcaneTransmutationEffectOutcome.NoSameLevelAlternative, result.Outcome);
        Assert.Empty(result.EligibleAlternativeIds);
        s.AssertUntouched();
    }

    [Fact]
    public void MissingCurrentLevelOrPool_DoesNotBecomeAStartingScrollDefault()
    {
        var s = new Setup();
        Assert.Equal(ArcaneTransmutationEffectOutcome.EquippedScrollRequired,
            ArcaneTransmutationEffect.Evaluate(s.Actor, "current_fixture").Outcome);
        Assert.Equal(ArcaneTransmutationEffectOutcome.EquippedScrollRequired,
            ArcaneTransmutationEffect.Evaluate(s.Actor, currentLevel: 1).Outcome);
        Assert.Equal(ArcaneTransmutationEffectOutcome.AlternativePoolRequired,
            ArcaneTransmutationEffect.Evaluate(s.Actor, "current_fixture", 1).Outcome);
        s.AssertUntouched();
    }

    [Theory]
    [InlineData("id")]
    [InlineData("level")]
    [InlineData("poolId")]
    [InlineData("poolLevel")]
    [InlineData("duplicate")]
    public void InvalidScrollMetadata_IsRejectedBeforeMutation(string invalid)
    {
        var s = new Setup();
        var pool = invalid == "duplicate" ? new[] { ("duplicate", 1), ("duplicate", 1) }
            : new[] { (invalid == "poolId" ? " " : "alternative", invalid == "poolLevel" ? -1 : 1) };
        Assert.ThrowsAny<ArgumentException>(() => s.Executor.Execute(s.Use, s.Target, Rules,
            invalid == "id" ? " " : "current", invalid == "level" ? -1 : 1, pool));
        s.AssertUntouched();
    }

    [Theory]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("ended", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("acted", AbilityRejectionReason.ActionUnavailable)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("unknown", AbilityRejectionReason.AbilityNotOwned)]
    [InlineData("ignition", AbilityRejectionReason.UnsupportedAbility)]
    [InlineData("passive", AbilityRejectionReason.NotManuallyActivatable)]
    public void AbilityEligibility_IsReusedBeforeEffectValidation(string invalid, AbilityRejectionReason reason)
    {
        var s = new Setup();
        switch (invalid)
        {
            case "inactive": s.Activations.CompleteActivation(); break;
            case "dead": s.Actor.TakeDamage(int.MaxValue); break;
            case "ended": s.Activations.CompleteMainAction(); break;
            case "acted": s.Actor.EndActivation(); break;
            case "cooldown": s.Actor.Cooldowns.RecordUse("arcane_transmutation", 2, 3); break; // Explicit test deadline, not canonical reuse.
            case "silence": s.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "unknown": s.Use = new(s.Actor, "unknown", s.Activations); break;
            case "ignition": s.Use = new(s.Actor, "temporal_ignition", s.Activations); break;
            case "passive": s.Use = new(s.Actor, "temporal_accumulation", s.Activations); break;
        }
        var result = s.Execute();
        Assert.Equal(reason, result.Eligibility.RejectionReason);
        Assert.Null(result.Effect);
        Assert.Null(result.Targeting);
        Assert.Equal(invalid == "cooldown" ? 1 : 0, s.Actor.Cooldowns.Cooldowns.Count);
        Assert.Equal(52, s.Enemy.CurrentHealth);
        Assert.Equal(0, s.Enemy.ShieldHP);
        Assert.Empty(s.Actor.TemporaryModifiers.Active);
    }

    [Fact]
    public void WrongCharacter_CannotAcquireTheEffectByCopyingAbilityMetadata()
    {
        var s = new Setup(actor: CharacterFactory.Create("nigel", 1)!);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute().Eligibility.RejectionReason);
        Assert.Equal(ArcaneTransmutationEffectOutcome.NotTimeWeaver, ArcaneTransmutationEffect.Evaluate(s.Actor).Outcome);
        var source = s.Actor.Definition!;
        var copy = CharacterFactory.Create(new CharacterDefinition(source.Id, source.Name, source.Description,
            source.BaseStats, CharacterRegistry.GetCharacter("time_weaver")!.Abilities), "copy", 1);
        var copied = new Setup(actor: copy);
        Assert.Equal(ArcaneTransmutationEffectOutcome.NotTimeWeaver, copied.Execute().Effect!.Outcome);
        copied.AssertUntouched();
    }

    [Fact]
    public void AnotherTimeWeaver_CannotSpendTheSelectedCharactersAction()
    {
        var s = new Setup();
        var other = CharacterFactory.Create("time_weaver", 1)!;
        var use = new AbilityUse(other, "arcane_transmutation", s.Activations);
        Assert.Equal(AbilityRejectionReason.CharacterNotActive, s.Executor.Execute(use, s.Target, Rules).Eligibility.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void WrongPhaseAndStaleRound_AreRejected()
    {
        var s = new Setup();
        s.Lifecycle.AdvancePhase();
        Assert.Equal(AbilityRejectionReason.WrongPhase, s.Execute().Eligibility.RejectionReason);
        while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        Assert.Equal(AbilityRejectionReason.UseContextChanged, s.Execute().Eligibility.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void InvalidTargetTypeAndMissingPosition_UseExistingTargetingResults()
    {
        var s = new Setup();
        var tile = new AbilityTarget(AbilityTargetType.Tile, new(6, 5));
        Assert.Equal(AbilityTargetingRejectionReason.InvalidTargetType,
            s.Executor.Execute(s.Use, tile, Rules).Targeting!.RejectionReason);
        var missing = new AbilityTarget(AbilityTargetType.Character, character: s.Enemy);
        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation,
            s.Executor.Execute(s.Use, missing, Rules).Targeting!.RejectionReason);
        var unplaced = new Setup(setPosition: false);
        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation, unplaced.Execute().Targeting!.RejectionReason);
        unplaced.AssertUntouched();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    public void ReplacementRange_IsRequiredRatherThanBorrowingTheStartingScrollsRange(int distance)
    {
        var s = new Setup();
        var target = new AbilityTarget(AbilityTargetType.Character, new(5 + distance, 5), s.Enemy);
        var result = s.Executor.Execute(s.Use, target, Rules);
        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation, result.Targeting!.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void PreviewAndExecute_RevalidateInputsWithoutSelectingARandomAlternative()
    {
        var s = new Setup();
        var pool = new[] { ("alternative_fixture", 1) };
        var preview = s.Executor.Preview(s.Use, s.Target, Rules, "current_fixture", 1, pool);
        Assert.Equal(ArcaneTransmutationEffectOutcome.EquipmentAndAttackIntegrationRequired, preview.Effect!.Outcome);
        pool[0] = ("wrong_level", 2);
        var execution = s.Executor.Execute(s.Use, s.Target, Rules, "current_fixture", 1, pool);
        Assert.Equal(ArcaneTransmutationEffectOutcome.NoSameLevelAlternative, execution.Effect!.Outcome);
        Assert.Equal(new[] { "alternative_fixture" }, preview.Effect.EligibleAlternativeIds);
        s.AssertUntouched();
    }

    [Fact]
    public void RejectedExecution_PreservesStatusesModifiersHealthAndHealingHistory()
    {
        var s = new Setup();
        s.Actor.TakeDamage(5);
        s.Actor.SetShieldHP(6);
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.Actor.Statuses.ApplyPoison();
        HealingApplicator.Apply(0, s.Actor);
        s.Actor.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("explicit", "test", s.Actor.Id,
            TemporaryStat.Resistance, 5, 2, 3) });
        var stats = s.Actor.BaseStats;
        var modifiers = s.Actor.TemporaryModifiers.Active;
        s.Execute();
        Assert.Equal(33, s.Actor.CurrentHealth);
        Assert.Equal(6, s.Actor.ShieldHP);
        Assert.Equal(1, s.Actor.HealsReceivedThisRound);
        Assert.Equal(2, s.Actor.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(s.Actor.Statuses.IsPoisoned);
        Assert.Equal(0, s.Actor.Statuses.PoisonTicksOccurred);
        Assert.Equal(modifiers, s.Actor.TemporaryModifiers.Active);
        Assert.Same(stats, s.Actor.BaseStats);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
        Assert.Equal(52, s.Enemy.CurrentHealth);
    }

    [Fact]
    public void ImmutableDeterministicResults_AndRepeatedExecutionDoNotFinalizeAnUnresolvedEffect()
    {
        var s = new Setup();
        var pool = new[] { ("alternative_b", 1), ("alternative_a", 1) };
        var a = s.Executor.Preview(s.Use, s.Target, Rules, "current", 1, pool);
        var b = s.Executor.Execute(s.Use, s.Target, Rules, "current", 1, pool);
        Assert.Equal(a.Effect!.EligibleAlternativeIds, b.Effect!.EligibleAlternativeIds);
        Assert.Equal(a.Effect.Outcome, b.Effect.Outcome);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)a.Effect.EligibleAlternativeIds).Clear());
        Assert.All(typeof(ArcaneTransmutationUseResult).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.All(typeof(ArcaneTransmutationEffectResult).GetProperties(), p => Assert.Null(p.SetMethod));
        s.AssertUntouched();
    }

    [Fact]
    public void NullProgrammingInputs_ThrowBeforeMutation()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => ArcaneTransmutationEffect.Evaluate(null!));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(null!, s.Target, Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, null!, Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, s.Target, null!));
        s.AssertUntouched();
    }

    [Fact]
    public void ReplacementPreview_FiltersThroughExistingCompatibilityRulesInCatalogOrder()
    {
        var actor = EquippedActor();
        var a = Scroll("eligible_a");
        var b = Scroll("eligible_b", element: "other_element_fixture");
        var catalog = new[]
        {
            actor.EquippedScroll!, Scroll("wrong_level", level: 2),
            new ScrollDefinition("unknown_level", "Test fixture", power: 4, minimumRange: 1, maximumRange: 2),
            new ScrollDefinition("unknown_power", "Test fixture", 1, minimumRange: 1, maximumRange: 2),
            a, new ScrollDefinition("unknown_minimum", "Test fixture", 1, power: 4, maximumRange: 2),
            new ScrollDefinition("unknown_maximum", "Test fixture", 1, power: 4, minimumRange: 1), b
        };
        var preview = ArcaneTransmutationEffect.PreviewReplacement(actor, catalog);
        Assert.True(preview.CanReplace);
        Assert.False(preview.Succeeded);
        Assert.Equal(actor.Id, preview.CharacterId);
        Assert.Equal("original_fixture", preview.OriginalScrollId);
        Assert.Equal(new[] { "eligible_a", "eligible_b" }, preview.EligibleCandidateIds);
        Assert.Equal(new[] { a, b }, preview.Candidates);
        Assert.Equal(2, preview.EligibleCandidateCount);
        Assert.Null(preview.ReplacementScrollId);
        Assert.Null(preview.SelectedIndex);
        Assert.Equal("original_fixture", actor.EquippedScroll!.Id);
    }

    [Theory]
    [InlineData(0, "candidate_a")]
    [InlineData(1, "candidate_b")]
    [InlineData(2, "candidate_c")]
    public void EachIndex_SelectsExactlyItsEligibleCandidateWithOneRandomCall(int index, string expected)
    {
        var actor = EquippedActor();
        var reference = actor;
        var stats = actor.BaseStats;
        var catalog = new[] { Scroll("candidate_a"), Scroll("candidate_b"), Scroll("candidate_c") };
        var random = new IndexSource(index);
        var preview = ArcaneTransmutationEffect.PreviewReplacement(actor, catalog);
        Assert.Equal(0, random.Calls);
        var result = ArcaneTransmutationEffect.ExecuteReplacement(actor, preview, catalog, random);
        Assert.True(result.Succeeded);
        Assert.Equal(ArcaneScrollReplacementRejectionReason.None, result.RejectionReason);
        Assert.Equal("original_fixture", result.OriginalScrollId);
        Assert.Equal(expected, result.ReplacementScrollId);
        Assert.Equal(3, result.EligibleCandidateCount);
        Assert.Equal(index, result.SelectedIndex);
        Assert.Same(catalog[index], actor.EquippedScroll);
        Assert.Same(catalog[index], result.ReplacementScroll);
        Assert.Equal(1, random.Calls);
        Assert.Equal(3, random.ExclusiveMaximum);
        Assert.Same(reference, actor);
        Assert.Same(stats, actor.BaseStats);
        Assert.Empty(actor.Cooldowns.Cooldowns);
        Assert.Equal(0, actor.TemporalAccumulation.StackCount);
    }

    [Fact]
    public void SingleEligibleCandidate_StillUsesExactlyOneSelection_AndPoolsAreNotLimitedToOneHundred()
    {
        var singleActor = EquippedActor();
        var one = new IndexSource(0);
        Assert.True(ArcaneTransmutationEffect.ExecuteReplacement(singleActor, new[] { Scroll("only") }, one).Succeeded);
        Assert.Equal(1, one.Calls);
        Assert.Equal(1, one.ExclusiveMaximum);
        var actor = EquippedActor();
        var catalog = Enumerable.Range(0, 101).Select(i => Scroll($"candidate_{i}")).ToArray();
        var last = new IndexSource(100);
        var result = ArcaneTransmutationEffect.ExecuteReplacement(actor, catalog, last);
        Assert.True(result.Succeeded);
        Assert.Equal("candidate_100", result.ReplacementScrollId);
        Assert.Equal(101, last.ExclusiveMaximum);
        Assert.Equal(1, last.Calls);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("original")]
    [InlineData("wrongLevel")]
    [InlineData("unknownLevel")]
    [InlineData("unknownPower")]
    [InlineData("unknownMinimum")]
    [InlineData("unknownMaximum")]
    public void CatalogWithoutCompatibleAlternatives_IsRejectedBeforeRandomness(string invalid)
    {
        var actor = EquippedActor();
        var original = actor.EquippedScroll;
        ScrollDefinition[] catalog = invalid switch
        {
            "empty" => Array.Empty<ScrollDefinition>(),
            "original" => new[] { original! },
            "wrongLevel" => new[] { Scroll("wrong_level", level: 2) },
            "unknownLevel" => new[] { new ScrollDefinition("unknown", "Fixture", power: 4, minimumRange: 1, maximumRange: 2) },
            "unknownPower" => new[] { new ScrollDefinition("unknown", "Fixture", 1, minimumRange: 1, maximumRange: 2) },
            "unknownMinimum" => new[] { new ScrollDefinition("unknown", "Fixture", 1, power: 4, maximumRange: 2) },
            _ => new[] { new ScrollDefinition("unknown", "Fixture", 1, power: 4, minimumRange: 1) }
        };
        var random = new IndexSource(0);
        var result = ArcaneTransmutationEffect.ExecuteReplacement(actor, catalog, random);
        Assert.Equal(ArcaneScrollReplacementRejectionReason.NoEligibleAlternative, result.RejectionReason);
        Assert.Equal(0, result.EligibleCandidateCount);
        Assert.False(result.Succeeded);
        Assert.Same(original, actor.EquippedScroll);
        Assert.Equal(0, random.Calls);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("excludedDuplicate")]
    [InlineData("nullEntry")]
    public void MalformedWholeCatalog_IsRejectedEvenWhenBadEntriesWouldBeExcluded(string invalid)
    {
        var actor = EquippedActor();
        var original = actor.EquippedScroll;
        var duplicate = invalid == "excludedDuplicate" ? original! : Scroll("duplicate");
        var catalog = invalid == "nullEntry" ? new[] { Scroll("valid"), null! }
            : new[] { Scroll("valid"), duplicate, duplicate };
        var random = new IndexSource(0);
        Assert.Throws<ArgumentException>(() => ArcaneTransmutationEffect.ExecuteReplacement(actor, catalog, random));
        Assert.Same(original, actor.EquippedScroll);
        Assert.Equal(0, random.Calls);
        Assert.Empty(actor.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void InvalidRandomIndex_ReportsFailureAndPreservesOriginalEquipment(int index)
    {
        var actor = EquippedActor();
        var original = actor.EquippedScroll;
        var random = new IndexSource(index);
        var result = ArcaneTransmutationEffect.ExecuteReplacement(actor, new[] { Scroll("a"), Scroll("b") }, random);
        Assert.Equal(ArcaneScrollReplacementRejectionReason.InvalidRandomIndex, result.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Null(result.ReplacementScrollId);
        Assert.Equal(1, random.Calls);
        Assert.Same(original, actor.EquippedScroll);
        Assert.Empty(actor.Cooldowns.Cooldowns);
        Assert.False(actor.HasActedThisRound);
    }

    [Theory]
    [InlineData("dead", ArcaneScrollReplacementRejectionReason.CasterDefeated)]
    [InlineData("wrongCharacter", ArcaneScrollReplacementRejectionReason.NotTimeWeaver)]
    [InlineData("unequipped", ArcaneScrollReplacementRejectionReason.EquippedScrollRequired)]
    [InlineData("unknownCurrentLevel", ArcaneScrollReplacementRejectionReason.CurrentLevelUnresolved)]
    [InlineData("nullCatalog", ArcaneScrollReplacementRejectionReason.CatalogRequired)]
    public void InvalidEquippedContext_FailsBeforeSelection(string invalid, ArcaneScrollReplacementRejectionReason reason)
    {
        var actor = invalid == "wrongCharacter" ? CharacterFactory.Create("nigel", 1)! : CharacterFactory.Create("time_weaver", 1)!;
        if (invalid != "unequipped") actor.EquipScroll(invalid == "unknownCurrentLevel"
            ? new ScrollDefinition("unknown_current", "Fixture") : Scroll("original_fixture"));
        if (invalid == "dead") actor.TakeDamage(int.MaxValue);
        var original = actor.EquippedScroll;
        var random = new IndexSource(0);
        var result = ArcaneTransmutationEffect.ExecuteReplacement(actor,
            invalid == "nullCatalog" ? null : new[] { Scroll("candidate") }, random);
        Assert.Equal(reason, result.RejectionReason);
        Assert.Same(original, actor.EquippedScroll);
        Assert.False(result.Succeeded);
        Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void StaleEquippedPreview_IsRejectedBeforeRandomness_AndCannotBeSharedAcrossCharacters()
    {
        var actor = EquippedActor();
        var catalog = new[] { Scroll("a"), Scroll("b") };
        var preview = ArcaneTransmutationEffect.PreviewReplacement(actor, catalog);
        actor.ReplaceScroll(Scroll("changed"));
        var current = actor.EquippedScroll;
        var random = new IndexSource(0);
        var result = ArcaneTransmutationEffect.ExecuteReplacement(actor, preview, catalog, random);
        Assert.Equal(ArcaneScrollReplacementRejectionReason.EquippedScrollChanged, result.RejectionReason);
        Assert.Same(current, actor.EquippedScroll);
        Assert.Equal(0, random.Calls);
        var other = EquippedActor();
        var foreign = ArcaneTransmutationEffect.ExecuteReplacement(other, preview, catalog, random);
        Assert.Equal(ArcaneScrollReplacementRejectionReason.InvalidPreviewContext, foreign.RejectionReason);
        Assert.Equal("original_fixture", other.EquippedScroll!.Id);
        Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void CurrentCatalogAndCasterState_AreRevalidatedInsteadOfTrustingPreview()
    {
        var actor = EquippedActor();
        var catalog = new[] { Scroll("valid") };
        var preview = ArcaneTransmutationEffect.PreviewReplacement(actor, catalog);
        catalog[0] = Scroll("now_ineligible", level: 2);
        var random = new IndexSource(0);
        var result = ArcaneTransmutationEffect.ExecuteReplacement(actor, preview, catalog, random);
        Assert.Equal(ArcaneScrollReplacementRejectionReason.NoEligibleAlternative, result.RejectionReason);
        Assert.Equal(new[] { "valid" }, preview.EligibleCandidateIds);
        Assert.Equal("original_fixture", actor.EquippedScroll!.Id);
        Assert.Equal(0, random.Calls);
        actor.TakeDamage(int.MaxValue);
        Assert.Equal(ArcaneScrollReplacementRejectionReason.CasterDefeated,
            ArcaneTransmutationEffect.ExecuteReplacement(actor, preview, new[] { Scroll("valid") }, random).RejectionReason);
        Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void Replacement_IsPermanentAndIndependentAndItsSnapshotsStayImmutable()
    {
        var actor = EquippedActor();
        var other = EquippedActor();
        var catalog = new[] { Scroll("a"), Scroll("b") };
        var preview = ArcaneTransmutationEffect.PreviewReplacement(actor, catalog);
        var result = ArcaneTransmutationEffect.ExecuteReplacement(actor, preview, catalog, new IndexSource(1));
        Assert.Equal("b", actor.EquippedScroll!.Id);
        Assert.Equal("original_fixture", other.EquippedScroll!.Id);
        actor.ResetActivation();
        actor.TemporaryModifiers.ExpireAtRoundEnd(50, RoundPhase.Cleanup);
        Assert.Equal("b", actor.EquippedScroll.Id);
        Assert.Equal("original_fixture", result.OriginalScrollId);
        Assert.Equal("b", result.ReplacementScrollId);
        Assert.False(preview.Succeeded);
        Assert.Throws<NotSupportedException>(() => ((IList<ScrollDefinition>)result.Candidates).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.EligibleCandidateIds).Clear());
        Assert.All(typeof(ArcaneScrollReplacementResult).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void Replacement_PreservesAllOtherRuntimeStateAndDoesNotPerformAnAttack()
    {
        var s = new Setup();
        var hitRolls = new CountingHitRoll();
        var strike = new SingleStrikeResolver(new(hitRolls)).ResolveMagicalStrike(s.Actor.BaseStats, s.Enemy.BaseStats,
            4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 100);
        TemporalAccumulation.Process(s.Actor, "completed-fire", "fire", strike);
        s.Actor.TakeDamage(5);
        s.Actor.SetShieldHP(6);
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.Actor.Statuses.ApplyPoison();
        HealingApplicator.Apply(0, s.Actor);
        s.Actor.Cooldowns.RecordUse("other", 2, 3);
        s.Activations.RecordMovement();
        s.Activations.RecordUtilityAction();
        s.Actor.EquipScroll(Scroll("original_fixture"));
        var stats = s.Actor.BaseStats;
        var position = s.Actor.TacticalPosition;
        var random = new IndexSource(0);
        var result = ArcaneTransmutationEffect.ExecuteReplacement(s.Actor,
            new[] { Scroll("different_element_fixture", element: "other_element_fixture") }, random);
        Assert.True(result.Succeeded);
        Assert.Equal(1, random.Calls);
        Assert.Equal(1, hitRolls.Calls); // No new strike was resolved.
        Assert.Equal(1, s.Actor.TemporalAccumulation.StackCount);
        Assert.Equal(new[] { "fire" }, s.Actor.TemporalAccumulation.KnownElements);
        Assert.Equal(33, s.Actor.CurrentHealth);
        Assert.Equal(6, s.Actor.ShieldHP);
        Assert.Equal(1, s.Actor.HealsReceivedThisRound);
        Assert.Equal(2, s.Actor.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(s.Actor.Statuses.IsPoisoned);
        Assert.Equal(0, s.Actor.Statuses.PoisonTicksOccurred);
        Assert.Equal(5, s.Actor.Cooldowns.Cooldowns["other"].ReadyRound);
        Assert.Single(s.Actor.Cooldowns.Cooldowns);
        Assert.Same(stats, s.Actor.BaseStats);
        Assert.Equal(position, s.Actor.TacticalPosition);
        Assert.Same(s.Actor, s.Activations.ActiveCharacter);
        Assert.True(s.Activations.MovementUsed);
        Assert.True(s.Activations.UtilityActionUsed);
        Assert.False(s.Activations.MainActionUsed);
        Assert.False(s.Actor.HasActedThisRound);
        Assert.Equal(52, s.Enemy.CurrentHealth);
        Assert.Equal(0, s.Enemy.ShieldHP);
    }

    [Fact]
    public void SystemIndexProvider_UsesAnExplicitReusableSeededGeneratorWithoutStatisticalTests()
    {
        var first = new SystemRandomIndexProvider(new Random(12345));
        var second = new SystemRandomIndexProvider(new Random(12345));
        for (int i = 0; i < 20; i++)
        {
            int value = first.NextIndex(101);
            Assert.InRange(value, 0, 100);
            Assert.Equal(value, second.NextIndex(101));
        }
        Assert.Throws<ArgumentNullException>(() => new SystemRandomIndexProvider(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => first.NextIndex(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => first.NextIndex(-1));
    }

    [Fact]
    public void NullRandomAndThrowingProvider_DoNotModifyEquipment()
    {
        var actor = EquippedActor();
        var original = actor.EquippedScroll;
        var catalog = new[] { Scroll("valid") };
        Assert.Throws<ArgumentNullException>(() => ArcaneTransmutationEffect.ExecuteReplacement(actor, catalog, null!));
        Assert.Throws<ArgumentNullException>(() => ArcaneTransmutationEffect.PreviewReplacement(null!, catalog));
        Assert.Throws<ArgumentNullException>(() => ArcaneTransmutationEffect.ExecuteReplacement(actor, null!, catalog, new IndexSource(0)));
        Assert.Throws<InvalidOperationException>(() => ArcaneTransmutationEffect.ExecuteReplacement(actor, catalog, new ThrowingIndexSource()));
        Assert.Same(original, actor.EquippedScroll);
        Assert.Empty(actor.Cooldowns.Cooldowns);
    }

    private static ScrollDefinition Scroll(string id, int level = 1, string? element = "fire")
        => new(id, "Explicit scroll test fixture", level, element, 4, 1, 2);

    private static Character EquippedActor()
    {
        var actor = CharacterFactory.Create("time_weaver", 1)!;
        actor.EquipScroll(Scroll("original_fixture"));
        return actor;
    }

    private sealed class IndexSource(int index) : IRandomIndexProvider
    {
        public int Calls { get; private set; }
        public int ExclusiveMaximum { get; private set; }
        public int NextIndex(int exclusiveMaximum) { Calls++; ExclusiveMaximum = exclusiveMaximum; return index; }
    }

    private sealed class ThrowingIndexSource : IRandomIndexProvider
    {
        public int NextIndex(int exclusiveMaximum) => throw new InvalidOperationException("Injected provider failure.");
    }

    private sealed class CountingHitRoll : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }

    [Theory]
    [InlineData(0, "replacement_a", 6, "ice", 15)]
    [InlineData(1, "replacement_b", 9, "lightning", 18)]
    public void FullCast_UsesSelectedScrollAndFinalizesExactlyOneMainAction(int index, string id, int power, string element, int damage)
    {
        var s = new FullCastSetup(index);
        var preview = s.Preview();
        Assert.True(preview.CanSelect);
        Assert.Equal(0, s.Selection.Calls);
        Assert.Equal(0, s.Hits.Calls);
        var result = s.Run();
        Assert.True(result.Succeeded);
        Assert.False(result.CanSelect);
        Assert.Equal(s.Actor.Id, result.CasterId);
        Assert.Equal(s.Enemy.Id, result.TargetId);
        Assert.Equal(2, result.RoundNumber);
        Assert.Equal("original_full_fixture", result.OldScrollId);
        Assert.Equal(id, result.ReplacementScrollId);
        Assert.Equal(index, result.SelectedCandidateIndex);
        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(power, result.SelectedScroll!.Power);
        Assert.Equal(element, result.SelectedScroll.ElementId);
        Assert.Equal(1, result.SelectedScroll.MinimumRange);
        Assert.Equal(2, result.SelectedScroll.MaximumRange);
        Assert.Same(result.SelectedScroll, s.Actor.EquippedScroll);
        Assert.Equal(damage, result.Combat!.OpeningStrike.Damage);
        Assert.Equal(damage, result.Combat.DefenderHealthLost);
        Assert.False(result.Combat.CounterattackOccurred);
        Assert.False(result.Combat.FollowUpOccurred);
        Assert.Equal(1, s.Selection.Calls);
        Assert.Equal(1, s.Hits.Calls);
        Assert.True(s.Activations.MainActionUsed);
        Assert.False(s.Activations.MovementUsed);
        Assert.False(s.Activations.UtilityActionUsed);
        Assert.Null(s.Activations.ActiveCharacter);
        Assert.True(s.Actor.HasActedThisRound);
        var cooldown = s.Actor.Cooldowns.Cooldowns["arcane_transmutation"];
        Assert.Equal(2, cooldown.RoundUsed);
        Assert.Equal(0, cooldown.ReuseInterval);
        Assert.Equal(2, cooldown.ReadyRound);
        Assert.Equal(TemporalAccumulationOutcome.StackEarned, result.Accumulation!.Outcome);
        Assert.Equal(0, result.Accumulation.PreviousStacks);
        Assert.Equal(new[] { element }, s.Actor.TemporalAccumulation.KnownElements);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, s.Run().Eligibility.RejectionReason);
        Assert.Equal(1, s.Selection.Calls);
        Assert.Equal(1, s.Hits.Calls);
        s.Actor.ResetActivation();
        Assert.Equal(id, s.Actor.EquippedScroll!.Id);
    }

    [Fact]
    public void FullCast_MissStillReplacesAndFinalizesButUsesExistingUnresolvedPassivePolicy()
    {
        var s = new FullCastSetup(0, 99);
        var result = s.Run();
        Assert.True(result.Succeeded);
        Assert.False(result.Combat!.OpeningStrike.Hit);
        Assert.Equal(0, result.Combat.DefenderHealthLost);
        Assert.Equal(52, s.Enemy.CurrentHealth);
        Assert.Equal("replacement_a", s.Actor.EquippedScroll!.Id);
        Assert.Equal(TemporalAccumulationOutcome.UnresolvedMissOrInterruption, result.Accumulation!.Outcome);
        Assert.Equal(0, s.Actor.TemporalAccumulation.StackCount);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Equal(1, s.Hits.Calls);
    }

    [Theory]
    [InlineData(0, 0, 15)]
    [InlineData(0, 5, 10)]
    [InlineData(30, 0, 0)]
    public void FullCast_UsesCurrentResistanceAndShieldFirstDamage(int shield, int resistanceBonus, int hpLost)
    {
        var s = new FullCastSetup();
        s.Enemy.SetShieldHP(shield);
        if (resistanceBonus > 0)
            s.Enemy.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("res_fixture", s.Enemy.Id,
                s.Enemy.Id, TemporaryStat.Resistance, resistanceBonus, 2, 3) });
        var result = s.Run();
        Assert.Equal(hpLost, result.Combat!.OpeningHealthLost);
        Assert.Equal(Math.Min(shield, result.Combat.OpeningStrike.Damage), result.Combat.OpeningShieldAbsorbed);
        Assert.Equal(52 - hpLost, s.Enemy.CurrentHealth);
    }

    [Fact]
    public void FullCast_PartialShieldAbsorptionReportsHealthDamageSeparately()
    {
        var s = new FullCastSetup();
        s.Enemy.SetShieldHP(6);
        var result = s.Run();
        Assert.Equal(15, result.Combat!.OpeningStrike.Damage);
        Assert.Equal(6, result.Combat.OpeningShieldAbsorbed);
        Assert.Equal(9, result.Combat.OpeningHealthLost);
    }

    [Theory]
    [InlineData("ice", TemporalAccumulationOutcome.ElementAlreadyKnown)]
    [InlineData("fire", TemporalAccumulationOutcome.StackEarned)]
    public void FullCast_PriorStacksStrengthenAttackButNewStacksDoNot(string priorElement, TemporalAccumulationOutcome expected)
    {
        var s = new FullCastSetup();
        var previous = new SingleStrikeResolver(new HitRoller(new CountingHitRoll())).ResolveMagicalStrike(
            s.Actor.BaseStats, s.Enemy.BaseStats, 0, WeaponCategory.MagicScroll, WeaponCategory.Sword, 100);
        TemporalAccumulation.Process(s.Actor, "earlier_use", priorElement, previous);
        var delayed = new DelayedAttack("earlier_delayed", s.Actor.Id, s.Enemy.Id, 1, 3, previous);
        var result = s.Run();
        Assert.Equal(16, result.Combat!.OpeningStrike.Damage);
        Assert.Equal(1, result.Accumulation!.PreviousStacks);
        Assert.Equal(expected, result.Accumulation.Outcome);
        Assert.Equal(priorElement == "ice" ? 1 : 2, s.Actor.TemporalAccumulation.StackCount);
        Assert.Equal(9, delayed.Strike.Damage);
        Assert.Same(previous, delayed.Strike);
        var repeated = TemporalAccumulation.Process(s.Actor, "full_use", "ice", result.Combat.OpeningStrike);
        Assert.Equal(TemporalAccumulationOutcome.AlreadyProcessed, repeated.Outcome);
        Assert.Equal(result.Accumulation.ResultingStacks, repeated.ResultingStacks);
    }

    [Fact]
    public void FullCast_NormalCounterAndFollowUpSequenceUsesOldStackSnapshot()
    {
        var s = new FullCastSetup();
        s.Actor.Statuses.RemoveStatus(StatusType.Paralysis); // Restore documented 17 SPD versus Nigel's 13.
        s.Enemy.SetTacticalPosition(new(6, 5));
        var result = s.Run();
        Assert.True(result.Succeeded);
        Assert.True(result.Combat!.CounterattackOccurred);
        Assert.Equal(FollowUpParticipant.Attacker, result.Combat.FollowUpBy);
        Assert.Equal(15, result.Combat.OpeningStrike.Damage);
        Assert.Equal(7, result.Combat.FollowUpStrike!.Damage); // Existing provisional 50%, floor.
        Assert.Equal(3, s.Hits.Calls);
        Assert.Equal(1, s.Actor.TemporalAccumulation.StackCount);
    }

    [Fact]
    public void FullCast_LethalCounterStillFinalizesAndKeepsPermanentReplacement()
    {
        var s = new FullCastSetup();
        s.Enemy.SetTacticalPosition(new(6, 5));
        s.Actor.TakeDamage(37);
        var result = s.Run();
        Assert.True(result.Succeeded);
        Assert.True(s.Actor.IsDefeated);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Null(s.Activations.ActiveCharacter);
        Assert.Equal("replacement_a", s.Actor.EquippedScroll!.Id);
        Assert.False(result.Combat!.FollowUpOccurred);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void FullCast_SelectedRangeControlsTargetingWithoutReroll(int index, bool success)
    {
        var s = new FullCastSetup(index);
        s.Catalog = new[] { s.Catalog[0], new ScrollDefinition("range_one", "Test", 1, "fire", 7, 1, 1) };
        var result = s.Run();
        Assert.Equal(success, result.Succeeded);
        Assert.Equal(1, s.Selection.Calls);
        if (!success)
        {
            Assert.Equal(ArcaneTransmutationRejectionReason.SelectedRangeOutcomeUnresolved, result.RejectionReason);
            Assert.Equal(AbilityTargetingRejectionReason.OutsideMaximumRange, result.Targeting!.RejectionReason);
            Assert.Equal("range_one", result.SelectedScroll!.Id);
            Assert.Null(result.ReplacementScrollId);
            s.AssertNoCommit();
        }
    }

    [Fact]
    public void FullCast_SelectedMinimumRangeIsAlsoEnforced()
    {
        var s = new FullCastSetup();
        s.Catalog = new[] { new ScrollDefinition("far_only", "Test", 1, "fire", 7, 3, 4) };
        var result = s.Run();
        Assert.Equal(ArcaneTransmutationRejectionReason.SelectedRangeOutcomeUnresolved, result.RejectionReason);
        Assert.Equal(AbilityTargetingRejectionReason.InsideMinimumRange, result.Targeting!.RejectionReason);
        Assert.Equal(1, s.Selection.Calls);
        s.AssertNoCommit();
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("nullCatalog")]
    [InlineData("deadTarget")]
    [InlineData("missingPosition")]
    [InlineData("missingAccuracy")]
    [InlineData("disarm")]
    [InlineData("silence")]
    [InlineData("cooldown")]
    [InlineData("inactive")]
    [InlineData("wrongPhase")]
    [InlineData("foreignTarget")]
    [InlineData("ally")]
    [InlineData("los")]
    [InlineData("map")]
    public void FullCast_PredictableRejectionsDoNotDrawOrCommit(string invalid)
    {
        var s = invalid == "missingPosition" ? new FullCastSetup(new Setup(setPosition: false)) : new FullCastSetup();
        int? accuracy = 90;
        var rules = Rules;
        switch (invalid)
        {
            case "empty": s.Catalog = Array.Empty<ScrollDefinition>(); break;
            case "nullCatalog": s.Catalog = null!; break;
            case "deadTarget": s.Enemy.TakeDamage(int.MaxValue); break;
            case "missingAccuracy": accuracy = null; break;
            case "disarm": s.Actor.Statuses.ApplyTimedStatus(StatusType.Disarm, 1); break;
            case "silence": s.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "cooldown": s.Actor.Cooldowns.RecordUse("arcane_transmutation", 2, 2); break;
            case "inactive": s.Activations.CompleteActivation(); break;
            case "wrongPhase": s.Inner.Lifecycle.AdvancePhase(); break;
            case "foreignTarget":
                var foreign = CharacterFactory.Create("nigel", 2)!;
                foreign.SetTacticalPosition(new(7, 5));
                s.Combat = s.Combat with { Defender = foreign }; break;
            case "ally": rules = new(AbilityTargetType.Character, false, AbilityTargetTeam.Ally, true, false, false, false); break;
            case "los": rules = new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, false, true, false); break;
            case "map": rules = new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, true, false, false); break;
        }
        int hp = s.Enemy.CurrentHealth;
        bool used = s.Actor.HasActedThisRound;
        var result = s.Executor.Execute(s.Inner.Use, s.Combat, rules, s.Catalog, accuracy, "full_use");
        Assert.False(result.Succeeded);
        Assert.Same(s.Original, s.Actor.EquippedScroll);
        Assert.Equal(hp, s.Enemy.CurrentHealth);
        Assert.Equal(used, s.Actor.HasActedThisRound);
        Assert.False(s.Activations.MainActionUsed);
        Assert.Equal(invalid == "cooldown" ? 1 : 0, s.Actor.Cooldowns.Cooldowns.Count);
        Assert.Equal(0, s.Selection.Calls);
        Assert.Equal(0, s.Hits.Calls);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("nullEntry")]
    [InlineData("accuracy")]
    [InlineData("counterPower")]
    [InlineData("counterRange")]
    [InlineData("counterCategory")]
    [InlineData("counterAccuracy")]
    [InlineData("overflow")]
    [InlineData("eventId")]
    public void FullCast_MalformedInputsFailBeforeEquipmentHealthActionsOrHitRolls(string invalid)
    {
        var s = new FullCastSetup();
        int? accuracy = invalid == "accuracy" ? 101 : 90;
        if (invalid == "duplicate") s.Catalog = new[] { s.Catalog[0], s.Catalog[0] };
        if (invalid == "nullEntry") s.Catalog = new[] { s.Catalog[0], null! };
        if (invalid == "counterPower") s.Combat = s.Combat with { CounterattackPower = -1 };
        if (invalid == "counterRange") s.Combat = s.Combat with { CounterattackMinimumRange = 3, CounterattackMaximumRange = 1 };
        if (invalid == "counterCategory") s.Combat = s.Combat with { DefenderWeapon = (WeaponCategory)999 };
        if (invalid == "counterAccuracy") s.Combat = s.Combat with { CounterattackBaseAccuracy = -1 };
        if (invalid == "overflow") s.Catalog = new[] { new ScrollDefinition("overflow", "Test", 1, "fire", int.MaxValue, 1, 2) };
        var failure = Record.Exception(() => s.Executor.Execute(s.Inner.Use, s.Combat, Rules, s.Catalog, accuracy,
            invalid == "eventId" ? " " : "full_use"));
        Assert.NotNull(failure);
        Assert.True(failure is ArgumentException or OverflowException);
        s.AssertNoCommit();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void FullCast_InvalidRandomIndexPreservesAllState(int index)
    {
        var s = new FullCastSetup(index);
        var result = s.Run();
        Assert.Equal(ArcaneScrollReplacementRejectionReason.InvalidRandomIndex, result.Replacement!.RejectionReason);
        Assert.Equal(1, s.Selection.Calls);
        s.AssertNoCommit();
    }

    [Fact]
    public void FullCast_RevalidatesEquipmentAndCurrentTargetPosition()
    {
        var s = new FullCastSetup();
        var preview = s.Preview();
        s.Actor.ReplaceScroll(s.Catalog[1]);
        var changed = s.Executor.Execute(s.Inner.Use, s.Combat, Rules, s.Catalog, 90, "full_use",
            expectedEquipment: preview.Replacement);
        Assert.Equal(ArcaneTransmutationRejectionReason.EquippedScrollChanged, changed.RejectionReason);
        Assert.Equal(0, s.Selection.Calls);
        Assert.Equal("replacement_b", s.Actor.EquippedScroll!.Id);
        var other = new FullCastSetup();
        other.Preview();
        other.Enemy.SetTacticalPosition(new(10, 5));
        var moved = other.Run();
        Assert.Equal(5, moved.Targeting!.Distance);
        Assert.Equal(ArcaneTransmutationRejectionReason.SelectedRangeOutcomeUnresolved, moved.RejectionReason);
        other.AssertNoCommit();
    }

    [Fact]
    public void FullCast_UnknownElementCannotBeInventedForPassiveProcessing()
    {
        var s = new FullCastSetup();
        s.Catalog = new[] { new ScrollDefinition("unknown_element", "Test", 1, null, 6, 1, 2) };
        Assert.Equal(ArcaneTransmutationRejectionReason.SelectedElementUnresolved, s.Run().RejectionReason);
        s.AssertNoCommit();
    }

    [Fact]
    public void FullCast_UnrelatedStateAndOtherCharactersRemainIndependentAndResultsAreSnapshots()
    {
        var s = new FullCastSetup();
        var other = EquippedActor();
        var otherScroll = other.EquippedScroll;
        s.Actor.SetShieldHP(5);
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        HealingApplicator.Apply(0, s.Actor);
        s.Actor.Cooldowns.RecordUse("other_ability", 2, 3);
        s.Activations.RecordMovement();
        s.Activations.RecordUtilityAction();
        var stats = s.Actor.BaseStats;
        var result = s.Run();
        Assert.True(result.Succeeded);
        Assert.Equal(38, s.Actor.CurrentHealth);
        Assert.Equal(5, s.Actor.ShieldHP);
        Assert.True(s.Actor.Statuses.HasStatus(StatusType.Burn));
        Assert.Equal(1, s.Actor.HealsReceivedThisRound);
        Assert.Equal(5, s.Actor.Cooldowns.Cooldowns["other_ability"].ReadyRound);
        Assert.Same(stats, s.Actor.BaseStats);
        Assert.True(s.Activations.MovementUsed);
        Assert.True(s.Activations.UtilityActionUsed);
        Assert.Same(otherScroll, other.EquippedScroll);
        Assert.False(other.HasActedThisRound);
        var snapshot = result.SelectedScroll;
        s.Actor.ReplaceScroll(s.Original);
        s.Enemy.SetTacticalPosition(new(1, 1));
        Assert.Equal("replacement_a", snapshot!.Id);
        Assert.Equal(new GridPosition(7, 5), result.Targeting!.TargetPosition);
        Assert.Equal("replacement_a", result.ReplacementScrollId);
        Assert.All(typeof(ArcaneTransmutationUseResult).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void FullCast_ThrowingSelectionProviderFailsWithoutEquipmentActionsOrHitRolls()
    {
        var s = new FullCastSetup();
        var executor = new ArcaneTransmutationAbilityExecutor(
            new CombatExchangeResolver(new SingleStrikeResolver(new HitRoller(s.Hits))), new ThrowingIndexSource());
        Assert.Throws<InvalidOperationException>(() => executor.Execute(s.Inner.Use, s.Combat, Rules, s.Catalog, 90, "full_use"));
        s.AssertNoCommit();
    }

    [Fact]
    public void FullCast_ResolvedEvasionUsesExistingAccuracyCalculation()
    {
        var s = new FullCastSetup(0, 75);
        s.Combat = s.Combat with { OpeningEvasionModifier = 20 };
        var result = s.Run();
        Assert.True(result.Succeeded);
        Assert.Equal(70, result.Combat!.OpeningStrike.HitChance);
        Assert.False(result.Combat.OpeningStrike.Hit);
    }

    [Fact]
    public void FullCast_DefenderCanCounterAndFollowUpUsingItsOwnAvailableAttack()
    {
        var s = new FullCastSetup();
        s.Actor.ConfigureParalysisSpeedReduction(10); // Explicit test input: 7 SPD against 13.
        s.Enemy.SetTacticalPosition(new(6, 5));
        var result = s.Run();
        Assert.True(result.Succeeded);
        Assert.True(result.Combat!.CounterattackOccurred);
        Assert.Equal(FollowUpParticipant.Defender, result.Combat.FollowUpBy);
        Assert.Equal(3, s.Hits.Calls);
    }

    private sealed class FullCastSetup
    {
        public Setup Inner { get; }
        public Character Actor => Inner.Actor;
        public Character Enemy => Inner.Enemy;
        public ActivationManager Activations => Inner.Activations;
        public ScrollDefinition Original { get; } = new("original_full_fixture", "Test original", 1, "fire", 100, 0, 0);
        public ScrollDefinition[] Catalog { get; set; } = {
            new("replacement_a", "Explicit test fixture A", 1, "ice", 6, 1, 2),
            new("replacement_b", "Explicit test fixture B", 1, "lightning", 9, 1, 2) };
        public CombatExchangeRequest Combat { get; set; }
        public IndexSource Selection { get; }
        public SequenceHitSource Hits { get; }
        public ArcaneTransmutationAbilityExecutor Executor { get; }

        public FullCastSetup(int index = 0, params int[] rolls) : this(new Setup(), index, rolls) { }

        public FullCastSetup(Setup inner, int index = 0, params int[] rolls)
        {
            Inner = inner;
            Actor.EquipScroll(Original);
            Enemy.SetTacticalPosition(new(7, 5));
            // Explicit test-only penalty gives equal effective Speed and isolates the opening.
            Actor.ConfigureParalysisSpeedReduction(4);
            Actor.Statuses.ApplyTimedStatus(StatusType.Paralysis, 2);
            Selection = new(index);
            Hits = new(rolls);
            Executor = new(new CombatExchangeResolver(new SingleStrikeResolver(new HitRoller(Hits))), Selection);
            // Old opening settings and distance must be ignored in favor of the selected scroll and live positions.
            Combat = new(Actor, Enemy, AttackType.Physical, 999, WeaponCategory.Axe, WeaponCategory.Sword, 1,
                AttackType.Physical, 0, 90, 1, 1, 999, false, false);
        }
        public ArcaneTransmutationUseResult Preview() => Executor.Preview(Inner.Use, Combat, Rules, Catalog, 90, "full_use");
        public ArcaneTransmutationUseResult Run() => Executor.Execute(Inner.Use, Combat, Rules, Catalog, 90, "full_use");
        public void AssertNoCommit()
        {
            Assert.Same(Original, Actor.EquippedScroll);
            Assert.Equal(52, Enemy.CurrentHealth);
            Assert.Equal(0, Enemy.ShieldHP);
            Assert.False(Activations.MainActionUsed);
            Assert.Empty(Actor.Cooldowns.Cooldowns);
            Assert.Equal(0, Hits.Calls);
            Assert.Equal(0, Actor.TemporalAccumulation.StackCount);
        }
    }

    private sealed class SequenceHitSource(int[] rolls) : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() => Calls < rolls.Length ? rolls[Calls++] : DefaultRoll();
        private int DefaultRoll() { Calls++; return 0; }
    }

    private sealed class Setup
    {
        public Character Actor { get; }
        public Character Enemy { get; } = CharacterFactory.Create("nigel", 2)!;
        public RoundLifecycleManager Lifecycle { get; } = new(2);
        public ActivationManager Activations { get; }
        public AbilityUse Use { get; set; }
        public ArcaneTransmutationAbilityExecutor Executor { get; } = new();
        public AbilityTarget Target => new(AbilityTargetType.Character, Enemy.TacticalPosition, Enemy);

        public Setup(Character? actor = null, bool setPosition = true)
        {
            Actor = actor ?? CharacterFactory.Create("time_weaver", 1)!;
            if (setPosition) Actor.SetTacticalPosition(new(5, 5));
            Enemy.SetTacticalPosition(new(6, 5));
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Activations = new(new[] { Actor }, new[] { Enemy }, 1, Lifecycle);
            Activations.StartActivation(Actor);
            Use = new(Actor, "arcane_transmutation", Activations);
        }

        public ArcaneTransmutationUseResult Preview() => Executor.Preview(Use, Target, Rules);
        public ArcaneTransmutationUseResult Execute() => Executor.Execute(Use, Target, Rules);
        public void AssertUntouched()
        {
            Assert.Equal(Actor.BaseStats.MaxHealth, Actor.CurrentHealth);
            Assert.Equal(0, Actor.ShieldHP);
            Assert.Empty(Actor.TemporaryModifiers.Active);
            Assert.Empty(Actor.Cooldowns.Cooldowns);
            Assert.Equal(52, Enemy.CurrentHealth);
            Assert.Equal(0, Enemy.ShieldHP);
            Assert.False(Activations.MainActionUsed);
            Assert.False(Activations.UtilityActionUsed);
            Assert.False(Activations.MovementUsed);
        }
    }
}
