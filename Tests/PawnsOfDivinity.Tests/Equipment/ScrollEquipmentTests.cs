using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Equipment;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Equipment;

public class ScrollEquipmentTests
{
    [Fact]
    public void Definition_StoresExplicitConfirmedStartingScrollPropertiesWithoutCreatingACatalog()
    {
        var scroll = FireFixture();
        Assert.Equal("fire_fixture", scroll.Id); // Fixture ID, not a new canonical item registry.
        Assert.Equal("Level 1 Fire Scroll", scroll.DisplayName);
        Assert.Equal(1, scroll.Level);
        Assert.Equal("fire", scroll.ElementId);
        Assert.Equal(4, scroll.Power);
        Assert.Equal(1, scroll.MinimumRange);
        Assert.Equal(2, scroll.MaximumRange);
        Assert.True(scroll.HasResolvedCombatProperties);
        Assert.Equal(FireFixture(), scroll);
        Assert.All(typeof(ScrollDefinition).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void Definition_RejectsInvalidIdsNamesAndSuppliedElementIds(string? invalid)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ScrollDefinition(invalid!, "Scroll"));
        Assert.ThrowsAny<ArgumentException>(() => new ScrollDefinition("id", invalid!));
        if (invalid is not null)
            Assert.ThrowsAny<ArgumentException>(() => new ScrollDefinition("id", "Scroll", elementId: invalid));
    }

    [Theory]
    [InlineData("level")]
    [InlineData("power")]
    [InlineData("minimum")]
    [InlineData("maximum")]
    public void Definition_RejectsNegativeKnownValues(string invalid)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ScrollDefinition("id", "Explicit fixture",
            level: invalid == "level" ? -1 : 1, power: invalid == "power" ? -1 : 4,
            minimumRange: invalid == "minimum" ? -1 : 1, maximumRange: invalid == "maximum" ? -1 : 2));

    [Fact]
    public void Definition_RejectsReversedRangeButAllowsEqualAndUnresolvedBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScrollDefinition("id", "Fixture", minimumRange: 3, maximumRange: 2));
        var pointRange = new ScrollDefinition("point", "Explicit range fixture", 1, power: 0, minimumRange: 0, maximumRange: 0);
        Assert.True(pointRange.HasResolvedCombatProperties);
        Assert.Equal(0, pointRange.ToAttackConfiguration(0).MinimumRange);
        var unresolved = new ScrollDefinition("partial", "Unresolved fixture", minimumRange: 1);
        Assert.Null(unresolved.MaximumRange);
        Assert.False(unresolved.HasResolvedCombatProperties);
    }

    [Fact]
    public void Definition_LeavesUnresolvedMetadataNullAndDoesNotInventAccuracy()
    {
        var unresolved = new ScrollDefinition("unknown_fixture", "Unresolved fixture");
        Assert.Null(unresolved.Level);
        Assert.Null(unresolved.ElementId);
        Assert.Null(unresolved.Power);
        Assert.Null(unresolved.MinimumRange);
        Assert.Null(unresolved.MaximumRange);
        Assert.False(unresolved.HasResolvedCombatProperties);
        Assert.Throws<InvalidOperationException>(() => unresolved.ToAttackConfiguration(90));
        Assert.Throws<ArgumentOutOfRangeException>(() => FireFixture().ToAttackConfiguration(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => FireFixture().ToAttackConfiguration(101));
    }

    [Fact]
    public void Definition_IntroducesNoArbitraryLevelCapAndHandlesLargeMetadataWithoutArithmetic()
    {
        var scroll = new ScrollDefinition("large_fixture", "Explicit metadata fixture", int.MaxValue,
            power: int.MaxValue, minimumRange: int.MaxValue, maximumRange: int.MaxValue);
        var attack = scroll.ToAttackConfiguration(100);
        Assert.Equal(int.MaxValue, scroll.Level);
        Assert.Equal(int.MaxValue, attack.Power);
        Assert.Equal(int.MaxValue, attack.MaximumRange);
    }

    [Fact]
    public void NewCharacters_AreUnequippedWithoutChangingFactoryOrRosterStats()
    {
        foreach (var definition in CharacterRegistry.GetAllCharacters())
        {
            var actor = CharacterFactory.Create(definition.Id, 1)!;
            Assert.Null(actor.EquippedScroll);
            Assert.Same(definition.BaseStats, actor.BaseStats);
            Assert.Equal(definition.BaseStats.MaxHealth, actor.CurrentHealth);
        }
    }

    [Fact]
    public void Equip_IsAnExplicitSingleAssignmentWithReadOnlyPublicState()
    {
        var actor = Actor();
        var scroll = FireFixture();
        actor.EquipScroll(scroll);
        Assert.Same(scroll, actor.EquippedScroll);
        var property = typeof(Character).GetProperty(nameof(Character.EquippedScroll))!;
        Assert.False(property.SetMethod!.IsPublic);
        Assert.Throws<InvalidOperationException>(() => actor.EquipScroll(AlternativeFixture()));
        Assert.Same(scroll, actor.EquippedScroll);
    }

    [Fact]
    public void Equip_CanStoreIncompleteMetadataWithoutClaimingItCanAttackOrTransmute()
    {
        var actor = Actor();
        var unknown = new ScrollDefinition("unknown", "Unresolved fixture");
        actor.EquipScroll(unknown);
        Assert.Same(unknown, actor.EquippedScroll);
        Assert.Equal(ScrollReplacementRejectionReason.CurrentLevelUnresolved,
            actor.ValidateScrollReplacement(AlternativeFixture()).RejectionReason);
        Assert.Throws<InvalidOperationException>(() => unknown.ToAttackConfiguration(100));
    }

    [Fact]
    public void ReplacementPreview_IsNonMutatingAndPermanentReplacementPreservesIdentity()
    {
        var actor = Actor();
        var reference = actor;
        string id = actor.Id;
        var stats = actor.BaseStats;
        var old = FireFixture();
        var replacement = AlternativeFixture();
        actor.EquipScroll(old);
        var preview = actor.ValidateScrollReplacement(replacement);
        Assert.True(preview.IsCompatible);
        Assert.False(preview.WasApplied);
        Assert.Equal(old.Id, preview.PreviousScrollId);
        Assert.Equal(replacement.Id, preview.ProposedScrollId);
        Assert.Same(old, actor.EquippedScroll);
        var result = actor.ReplaceScroll(replacement);
        Assert.True(result.IsCompatible);
        Assert.True(result.WasApplied);
        Assert.Same(replacement, actor.EquippedScroll);
        Assert.Same(reference, actor);
        Assert.Equal(id, actor.Id);
        Assert.Same(stats, actor.BaseStats);
        actor.ResetActivation();
        actor.ResetHealingHistory();
        actor.TemporaryModifiers.ExpireAtRoundEnd(20, RoundPhase.Cleanup);
        Assert.Same(replacement, actor.EquippedScroll); // No temporary duration or expiration was introduced.
        Assert.False(preview.WasApplied);
    }

    [Theory]
    [InlineData("unequipped", ScrollReplacementRejectionReason.NoEquippedScroll)]
    [InlineData("missing", ScrollReplacementRejectionReason.ReplacementRequired)]
    [InlineData("same", ScrollReplacementRejectionReason.SameScroll)]
    [InlineData("sameIdOtherObject", ScrollReplacementRejectionReason.SameScroll)]
    [InlineData("level", ScrollReplacementRejectionReason.DifferentLevel)]
    [InlineData("unknownCurrentLevel", ScrollReplacementRejectionReason.CurrentLevelUnresolved)]
    [InlineData("unknownNewLevel", ScrollReplacementRejectionReason.ReplacementLevelUnresolved)]
    [InlineData("unknownPower", ScrollReplacementRejectionReason.CombatMetadataUnresolved)]
    [InlineData("unknownMinimum", ScrollReplacementRejectionReason.CombatMetadataUnresolved)]
    [InlineData("unknownMaximum", ScrollReplacementRejectionReason.CombatMetadataUnresolved)]
    public void InvalidReplacement_ReportsReasonAndLeavesEquipmentUnchanged(string invalid, ScrollReplacementRejectionReason expected)
    {
        var actor = Actor();
        ScrollDefinition? proposed = invalid switch
        {
            "missing" => null,
            "same" or "sameIdOtherObject" => FireFixture(),
            "level" => new("different_level", "Level test fixture", 2, power: 4, minimumRange: 1, maximumRange: 2),
            "unknownNewLevel" => new("unknown_level", "Unknown level fixture", power: 4, minimumRange: 1, maximumRange: 2),
            "unknownPower" => new("unknown_power", "Unknown Power fixture", 1, minimumRange: 1, maximumRange: 2),
            "unknownMinimum" => new("unknown_min", "Unknown minimum fixture", 1, power: 4, maximumRange: 2),
            "unknownMaximum" => new("unknown_max", "Unknown maximum fixture", 1, power: 4, minimumRange: 1),
            _ => AlternativeFixture()
        };
        if (invalid != "unequipped") actor.EquipScroll(invalid == "unknownCurrentLevel"
            ? new("current_unknown", "Unknown current level fixture", power: 4, minimumRange: 1, maximumRange: 2) : FireFixture());
        if (invalid == "same") proposed = actor.EquippedScroll;
        var before = actor.EquippedScroll;
        var preview = actor.ValidateScrollReplacement(proposed);
        Assert.Equal(expected, preview.RejectionReason);
        Assert.False(preview.IsCompatible);
        var result = actor.ReplaceScroll(proposed);
        Assert.Equal(expected, result.RejectionReason);
        Assert.False(result.WasApplied);
        Assert.Same(before, actor.EquippedScroll);
    }

    [Fact]
    public void IndependentCharacters_MayShareImmutableDefinitionsWithoutSharingEquippedState()
    {
        var first = Actor();
        var second = Actor();
        var shared = FireFixture();
        first.EquipScroll(shared);
        second.EquipScroll(shared); // Definition metadata is shared; inventory item ownership is outside this stage.
        var replacement = AlternativeFixture();
        first.ReplaceScroll(replacement);
        Assert.Same(replacement, first.EquippedScroll);
        Assert.Same(shared, second.EquippedScroll);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Replacement_RevalidatesCurrentStateRatherThanTrustingAnEarlierPreview()
    {
        var actor = Actor();
        actor.EquipScroll(FireFixture());
        var replacement = AlternativeFixture();
        var preview = actor.ValidateScrollReplacement(replacement);
        Assert.True(preview.IsCompatible);
        Assert.True(actor.ReplaceScroll(replacement).WasApplied);
        var repeated = actor.ReplaceScroll(replacement);
        Assert.Equal(ScrollReplacementRejectionReason.SameScroll, repeated.RejectionReason);
        Assert.False(repeated.WasApplied);
        Assert.Equal("fire_fixture", preview.PreviousScrollId);
        Assert.False(preview.WasApplied);
    }

    [Fact]
    public void ResolvedCombatProperties_ReuseExistingConfigurationWithoutExecutingAnAttack()
    {
        var actor = Actor();
        actor.EquipScroll(FireFixture());
        var attack = actor.EquippedScroll!.ToAttackConfiguration(87); // Explicit example accuracy, not canonical balance.
        Assert.Equal(AttackType.Magical, attack.Type);
        Assert.Equal(WeaponCategory.MagicScroll, attack.Category);
        Assert.Equal(4, attack.Power);
        Assert.Equal(1, attack.MinimumRange);
        Assert.Equal(2, attack.MaximumRange);
        Assert.Equal(87, attack.BaseAccuracy);
        Assert.Equal(38, actor.CurrentHealth);
        Assert.Equal(0, actor.TemporalAccumulation.StackCount);
        Assert.Empty(actor.Cooldowns.Cooldowns);
        Assert.False(actor.HasActedThisRound);
    }

    [Fact]
    public void UnknownElement_DoesNotInventOneOrBlockPureMagicalCombatConfiguration()
    {
        var scroll = new ScrollDefinition("unresolved_element", "Element metadata fixture", 1, power: 4, minimumRange: 1, maximumRange: 2);
        Assert.Null(scroll.ElementId);
        Assert.True(ScrollReplacementValidation.Validate(FireFixture(), scroll).IsCompatible);
        Assert.Equal(4, scroll.ToAttackConfiguration(100).Power);
        // Element-dependent effects must still handle missing metadata; no element is inferred from the name.
    }

    [Fact]
    public void Replacement_PreservesProgressStatusesCooldownsBudgetsHealthAndBaseStats()
    {
        var actor = Actor();
        var enemy = CharacterFactory.Create("nigel", 2)!;
        var rolls = new CountingRoll();
        var strike = new SingleStrikeResolver(new(rolls)).ResolveMagicalStrike(actor.BaseStats, enemy.BaseStats,
            4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 100);
        TemporalAccumulation.Process(actor, "completed-fire-use", "fire", strike);
        actor.TakeDamage(5);
        actor.SetShieldHP(6);
        actor.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        actor.Statuses.ApplyPoison();
        actor.Cooldowns.RecordUse("other", 2, 3);
        HealingApplicator.Apply(0, actor);
        var activations = new ActivationManager(new[] { actor }, new[] { enemy }, 1);
        activations.StartActivation(actor);
        activations.RecordMovement();
        activations.RecordUtilityAction();
        var stats = actor.BaseStats;
        actor.EquipScroll(FireFixture());
        var replacement = AlternativeFixture();
        var preview = actor.ValidateScrollReplacement(replacement);
        Assert.True(preview.IsCompatible);
        Assert.True(actor.ReplaceScroll(replacement).WasApplied);
        Assert.Equal(1, rolls.Calls); // Equip, preview and replacement consumed no additional randomness.
        Assert.Equal(1, actor.TemporalAccumulation.StackCount);
        Assert.Equal(new[] { "fire" }, actor.TemporalAccumulation.KnownElements);
        Assert.Equal(33, actor.CurrentHealth);
        Assert.Equal(6, actor.ShieldHP);
        Assert.Equal(1, actor.HealsReceivedThisRound);
        Assert.Equal(2, actor.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(actor.Statuses.IsPoisoned);
        Assert.Equal(0, actor.Statuses.PoisonTicksOccurred);
        Assert.Equal(5, actor.Cooldowns.Cooldowns["other"].ReadyRound);
        Assert.Single(actor.Cooldowns.Cooldowns);
        Assert.Same(stats, actor.BaseStats);
        Assert.Same(actor, activations.ActiveCharacter);
        Assert.True(activations.MovementUsed);
        Assert.True(activations.UtilityActionUsed);
        Assert.False(activations.MainActionUsed);
        Assert.False(actor.HasActedThisRound);
        Assert.Equal(52, enemy.CurrentHealth);
    }

    [Fact]
    public void SnapshotResults_AreImmutableAndKeepOldIdentitiesAfterFurtherReplacement()
    {
        var actor = Actor();
        actor.EquipScroll(FireFixture());
        var result = actor.ReplaceScroll(AlternativeFixture());
        actor.ReplaceScroll(FireFixture());
        Assert.Equal("fire_fixture", result.PreviousScrollId);
        Assert.Equal("alternative_fixture", result.ProposedScrollId);
        Assert.True(result.WasApplied);
        Assert.All(typeof(ScrollReplacementResult).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Equal("fire_fixture", actor.EquippedScroll!.Id);
    }

    [Fact]
    public void NullEquip_IsAProgrammingErrorAndDoesNotChangeExistingEquipment()
    {
        var actor = Actor();
        Assert.Throws<ArgumentNullException>(() => actor.EquipScroll(null!));
        Assert.Null(actor.EquippedScroll);
        var scroll = FireFixture();
        actor.EquipScroll(scroll);
        Assert.Throws<ArgumentNullException>(() => actor.EquipScroll(null!));
        Assert.Same(scroll, actor.EquippedScroll);
    }

    private static Character Actor() => CharacterFactory.Create("time_weaver", 1)!;
    private static ScrollDefinition FireFixture() => new("fire_fixture", "Level 1 Fire Scroll", 1, "fire", 4, 1, 2);
    // A compatibility fixture, not a new canonical scroll type, element or balance entry.
    private static ScrollDefinition AlternativeFixture() => new("alternative_fixture", "Alternative test fixture", 1,
        power: 4, minimumRange: 1, maximumRange: 2);

    private sealed class CountingRoll : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }
}
