using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class NigelAbilityTests
{
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character,
        requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);

    [Fact]
    public void RegisteredKit_PreservesDocumentedOrderStableIdsAndUnknownSpellTags()
    {
        var definition = CharacterRegistry.GetCharacter("nigel")!;
        Assert.Same(Nigel.Definition, definition);
        Assert.Equal(new[] { "provoking_roar", "brutal_knockback", "berserkers_fury" }, definition.Abilities.Select(a => a.Id));
        Assert.Equal(new[] { "Provoking Roar", "Brutal Knockback", "Berserker's Fury" }, definition.Abilities.Select(a => a.DisplayName));
        var roar = definition.Abilities[0];
        Assert.Equal(AbilityClassification.Active, roar.Classification);
        Assert.Equal(AbilityActionType.MainAction, roar.RequiredAction);
        Assert.Equal(AbilityRangeKind.ManhattanRadius, roar.Range!.Kind);
        Assert.Equal(0, roar.Range.Minimum);
        Assert.Equal(2, roar.Range.Maximum);
        Assert.Equal(3, roar.ReuseInterval);
        var knockback = definition.Abilities[1];
        Assert.Equal(AbilityClassification.Active, knockback.Classification);
        Assert.Equal(AbilityActionType.MainAction, knockback.RequiredAction);
        Assert.Equal(AbilityRangeKind.ManhattanDistance, knockback.Range!.Kind);
        Assert.Equal(1, knockback.Range.Minimum);
        Assert.Equal(1, knockback.Range.Maximum);
        Assert.Equal(2, knockback.ReuseInterval);
        Assert.Contains("No counters", knockback.Description);
        Assert.DoesNotContain("provisionally", knockback.Description);
        var fury = definition.Abilities[2];
        Assert.Equal(AbilityClassification.Passive, fury.Classification);
        Assert.Null(fury.RequiredAction);
        Assert.Null(fury.ReuseInterval);
        Assert.Null(fury.Range);
        Assert.All(definition.Abilities, ability => { Assert.Null(ability.IsSpellTagged); Assert.NotEmpty(ability.Description); });
        foreach (var other in CharacterRegistry.GetAllCharacters().Where(d => d.Id != "nigel"))
            Assert.DoesNotContain(other.Abilities, a => definition.Abilities.Any(n => n.Id == a.Id));
        var character = CharacterFactory.Create("nigel", 1)!;
        Assert.Same(definition, character.Definition);
        Assert.Equal(52, character.CurrentHealth);
        Assert.Same(definition.BaseStats, character.BaseStats);
    }

    [Theory]
    [InlineData(6, 5, true, 1)]
    [InlineData(7, 5, true, 2)]
    [InlineData(6, 6, true, 2)]
    [InlineData(4, 4, true, 2)]
    [InlineData(5, 3, true, 2)]
    [InlineData(8, 5, false, 3)]
    [InlineData(7, 6, false, 3)]
    [InlineData(int.MinValue, int.MaxValue, false, 4294967295L)]
    public void Roar_UsesInclusiveManhattanRadiusTwo(int x, int y, bool affected, long distance)
    {
        var s = new Setup();
        s.Near.SetTacticalPosition(new(x, y));
        var result = ProvokingRoarEffect.Evaluate(s.Caster, new[] { s.Near }, 2, Rules);
        Assert.True(result.IsAreaResolved);
        var recipient = Assert.Single(result.Recipients);
        Assert.Equal(distance, recipient.Targeting!.Distance);
        Assert.Equal(affected ? ProvokingRoarRecipientOutcome.Affected : ProvokingRoarRecipientOutcome.OutsideRadius, recipient.Outcome);
        Assert.Equal(affected ? new[] { s.Near.Id } : Array.Empty<string>(), result.AffectedCharacterIds);
    }

    [Fact]
    public void Roar_SelectsOnlyLivingUnactivatedEnemiesInRosterOrder()
    {
        var s = new Setup();
        var result = s.Preview();
        Assert.True(result.Eligibility.IsAllowed);
        Assert.True(result.Effect!.IsAreaResolved);
        Assert.Equal(new[] { s.Near.Id, s.Boundary.Id }, result.Effect.AffectedCharacterIds);
        Assert.Equal(s.Roster.Select(c => c.Id), result.Effect.Recipients.Select(c => c.CharacterId));
        Assert.Equal(ProvokingRoarRecipientOutcome.AlliedCharacter, result.Effect.Recipients[0].Outcome);
        Assert.Equal(ProvokingRoarRecipientOutcome.AlliedCharacter, result.Effect.Recipients[1].Outcome);
        Assert.Equal(ProvokingRoarRecipientOutcome.OutsideRadius, result.Effect.Recipients[4].Outcome);
        Assert.Equal(ProvokingRoarRecipientOutcome.AlreadyActivated, result.Effect.Recipients[5].Outcome);
        Assert.Equal(ProvokingRoarRecipientOutcome.Defeated, result.Effect.Recipients[6].Outcome);
        Assert.Equal(new GridPosition(5, 5), result.Effect.Center);
        Assert.Equal(2, result.Effect.RoundNumber);
        Assert.Equal(s.Caster.Id, result.CasterId);
    }

    [Fact]
    public void Roar_PreviewsWithoutMutationThenAppliesAndFinalizes()
    {
        var s = new Setup();
        var preview = s.Preview();
        var execution = s.Execute();
        Assert.Equal(ProvokingRoarUseRejectionReason.None, preview.RejectionReason);
        Assert.Equal(preview.RejectionReason, execution.RejectionReason);
        Assert.Equal(preview.Effect!.AffectedCharacterIds, execution.Effect!.AffectedCharacterIds);
        Assert.True(preview.CanExecute);
        Assert.True(execution.Succeeded);
        Assert.NotNull(execution.Finalization);
        Assert.False(s.Activations.CanUseMainAction);
        Assert.True(s.Activations.MainActionUsed);
        Assert.False(s.Activations.MovementUsed);
        Assert.False(s.Activations.UtilityActionUsed);
        Assert.Null(s.Activations.ActiveCharacter);
        Assert.Single(s.Caster.Cooldowns.Cooldowns);
        Assert.True(s.Caster.HasActedThisRound);
        Assert.All(new[] { s.Near, s.Boundary }, c => Assert.Empty(c.Statuses.TimedStatuses));
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("acted", AbilityRejectionReason.ActionUnavailable)]
    [InlineData("mainUsed", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("unknown", AbilityRejectionReason.AbilityNotOwned)]
    [InlineData("passive", AbilityRejectionReason.NotManuallyActivatable)]
    [InlineData("knockback", AbilityRejectionReason.UnsupportedAbility)]
    [InlineData("phase", AbilityRejectionReason.WrongPhase)]
    [InlineData("staleRound", AbilityRejectionReason.UseContextChanged)]
    [InlineData("finalized", AbilityRejectionReason.AlreadyFinalized)]
    public void Roar_ReusesEligibilityBeforeEvaluatingItsArea(string invalid, AbilityRejectionReason expected)
    {
        var s = new Setup();
        switch (invalid)
        {
            case "dead": s.Caster.TakeDamage(int.MaxValue); break;
            case "inactive": s.Activations.CompleteActivation(); break;
            case "acted": s.Caster.EndActivation(); break;
            case "mainUsed": s.Activations.CompleteMainAction(); break;
            case "cooldown": s.Caster.Cooldowns.RecordUse(Nigel.ProvokingRoarId, 2, 3); break;
            case "silence": s.Caster.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "unknown": s.Use = new(s.Caster, "unknown", s.Activations); break;
            case "passive": s.Use = new(s.Caster, Nigel.BerserkersFuryId, s.Activations); break;
            case "knockback": s.Use = new(s.Caster, Nigel.BrutalKnockbackId, s.Activations); break;
            case "phase": s.Lifecycle.AdvancePhase(); break;
            case "staleRound":
                while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
                s.Lifecycle.StartNextRound(); break;
            case "finalized":
                // Exercise the existing once-only bookkeeping guard; this is not a Roar cast.
                AbilityUseFinalizer.FinalizeSuccessfulUse(s.Use); break;
        }
        int health = s.Caster.CurrentHealth;
        int cooldowns = s.Caster.Cooldowns.Cooldowns.Count;
        bool acted = s.Caster.HasActedThisRound;
        var result = s.Execute();
        Assert.Equal(expected, result.Eligibility.RejectionReason);
        Assert.Null(result.Effect);
        Assert.False(result.Succeeded);
        Assert.Equal(health, s.Caster.CurrentHealth);
        Assert.Equal(cooldowns, s.Caster.Cooldowns.Cooldowns.Count);
        Assert.Equal(acted, s.Caster.HasActedThisRound);
        Assert.Equal(38, s.Near.CurrentHealth);
    }

    [Fact]
    public void Roar_CooldownIsReadyExactlyAtTheDocumentedDeadline()
    {
        var s = new Setup(round: 5);
        s.Caster.Cooldowns.RecordUse(Nigel.ProvokingRoarId, 2, 3);
        Assert.True(s.Caster.Cooldowns.IsReady(Nigel.ProvokingRoarId, 5));
        Assert.False(s.Caster.Cooldowns.IsReady(Nigel.ProvokingRoarId, 4));
        Assert.True(s.Preview().Eligibility.IsAllowed);
        Assert.Equal(5, s.Caster.Cooldowns.Cooldowns[Nigel.ProvokingRoarId].ReadyRound);
        Assert.True(s.Execute().Succeeded);
        Assert.Equal(8, s.Caster.Cooldowns.Cooldowns[Nigel.ProvokingRoarId].ReadyRound);
    }

    [Fact]
    public void Roar_WrongCharacterAndCopiedAbilityCannotAcquireNigelsEffect()
    {
        var s = new Setup(casterDefinition: TimeWeaver.Definition);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute().Eligibility.RejectionReason);
        var copied = new CharacterDefinition("not_nigel", "Copy", "Test metadata only", Nigel.Definition.BaseStats,
            Nigel.Definition.Abilities);
        var other = new Setup(casterDefinition: copied);
        var result = other.Execute();
        Assert.Equal(ProvokingRoarEffectRejectionReason.NotNigel, result.Effect!.RejectionReason);
        Assert.Equal(ProvokingRoarUseRejectionReason.AreaNotResolved, result.RejectionReason);
        Assert.Empty(other.Caster.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("type")]
    [InlineData("unknownType")]
    [InlineData("mapMissing")]
    [InlineData("mapIllegal")]
    [InlineData("los")]
    [InlineData("elevation")]
    [InlineData("unknownRules")]
    public void Roar_MissingTargetingRulesAndPositionsRemainExplicit(string missing)
    {
        var s = new Setup(placeCaster: missing != "source", placeNear: missing != "target");
        var rules = missing switch
        {
            "type" => new AbilityTargetingConstraints(AbilityTargetType.Tile, requiresMapValidation: false,
                requiresLineOfSight: false, requiresElevationRules: false),
            "unknownType" => new AbilityTargetingConstraints(),
            "mapMissing" or "mapIllegal" => new(AbilityTargetType.Character, requiresMapValidation: true,
                requiresLineOfSight: false, requiresElevationRules: false),
            "los" => new(AbilityTargetType.Character, requiresMapValidation: false,
                requiresLineOfSight: true, requiresElevationRules: false),
            "elevation" => new(AbilityTargetType.Character, requiresMapValidation: false,
                requiresLineOfSight: false, requiresElevationRules: true),
            "unknownRules" => new(AbilityTargetType.Character),
            _ => Rules
        };
        var map = missing == "mapIllegal" ? new Dictionary<string, bool> { [s.Near.Id] = false } : null;
        var result = s.Executor.Execute(s.Use, rules, map);
        Assert.False(result.Effect!.IsAreaResolved);
        Assert.False(result.CanExecute);
        Assert.Equal(ProvokingRoarUseRejectionReason.AreaNotResolved, result.RejectionReason);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
        Assert.Equal(38, s.Near.CurrentHealth);
    }

    [Fact]
    public void Roar_UsesConfirmedEnemyPermissionsRatherThanCallerOverrides()
    {
        var s = new Setup();
        var conflictingPermissions = new AbilityTargetingConstraints(AbilityTargetType.Character, true,
            AbilityTargetTeam.Ally, false, false, false, false);
        var result = s.Executor.Preview(s.Use, conflictingPermissions);
        Assert.Equal(new[] { s.Near.Id, s.Boundary.Id }, result.Effect!.AffectedCharacterIds);
        Assert.DoesNotContain(s.Caster.Id, result.Effect.AffectedCharacterIds);
        Assert.DoesNotContain(s.Ally.Id, result.Effect.AffectedCharacterIds);
    }

    [Fact]
    public void Roar_AcceptsExplicitMapLegalityWithoutInventingLineOfSight()
    {
        var s = new Setup();
        var map = new Dictionary<string, bool> { [s.Near.Id] = true, [s.Boundary.Id] = true };
        var result = s.Executor.Preview(s.Use, new(AbilityTargetType.Character,
            requiresMapValidation: true, requiresLineOfSight: false, requiresElevationRules: false), map);
        Assert.True(result.Effect!.IsAreaResolved);
        Assert.Equal(2, result.Effect.AffectedCharacterIds.Count);
        Assert.True(result.CanExecute);
    }

    [Fact]
    public void Roar_RechecksPositionsActivationAndDefeatInsteadOfTrustingAPreview()
    {
        var s = new Setup();
        var before = s.Preview();
        s.Near.SetTacticalPosition(new(20, 20));
        s.Boundary.EndActivation();
        s.Far.SetTacticalPosition(new(6, 5));
        s.Far.TakeDamage(int.MaxValue);
        var after = s.Execute();
        Assert.Empty(after.Effect!.AffectedCharacterIds);
        Assert.Equal(ProvokingRoarUseRejectionReason.NoRecipientsUseRuleUnresolved, after.RejectionReason);
        Assert.Equal(new[] { s.Near.Id, s.Boundary.Id }, before.Effect!.AffectedCharacterIds);
        Assert.Equal(new GridPosition(6, 5), before.Effect.Recipients[2].Targeting!.TargetPosition);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData("duplicateInstance")]
    [InlineData("duplicateId")]
    [InlineData("nullEntry")]
    [InlineData("nullRoster")]
    [InlineData("nullCaster")]
    [InlineData("nullRules")]
    [InlineData("zeroRound")]
    [InlineData("negativeRound")]
    public void Roar_RejectsMalformedInputsWithoutMutation(string invalid)
    {
        var s = new Setup();
        IEnumerable<Character>? participants = s.Roster;
        switch (invalid)
        {
            case "duplicateInstance": participants = new[] { s.Near, s.Near }; break;
            case "duplicateId": participants = new[] { s.Near, CharacterFactory.Create(HoodedAssassin.Definition, s.Near.Id, 2) }; break;
            case "nullEntry": participants = new[] { s.Near, null! }; break;
            case "nullRoster": participants = null; break;
        }
        Assert.ThrowsAny<ArgumentException>(() => ProvokingRoarEffect.Evaluate(invalid == "nullCaster" ? null! : s.Caster,
            participants!, invalid == "zeroRound" ? 0 : invalid == "negativeRound" ? -1 : 2,
            invalid == "nullRules" ? null! : Rules));
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
        Assert.Equal(38, s.Near.CurrentHealth);
    }

    [Fact]
    public void Roar_ResultCollectionsAreReadOnlyAndDeterministic()
    {
        var s = new Setup();
        var first = s.Preview();
        var second = s.Preview();
        Assert.Equal(first.Effect!.Recipients, second.Effect!.Recipients);
        Assert.Equal(first.Effect.AffectedCharacterIds, second.Effect.AffectedCharacterIds);
        var ids = Assert.IsAssignableFrom<IList<string>>(first.Effect.AffectedCharacterIds);
        Assert.Throws<NotSupportedException>(() => ids.Clear());
        var recipients = Assert.IsAssignableFrom<IList<ProvokingRoarRecipientResult>>(first.Effect.Recipients);
        Assert.Throws<NotSupportedException>(() => recipients.Clear());
        foreach (var type in new[] { typeof(ProvokingRoarUseResult), typeof(ProvokingRoarEffectResult), typeof(ProvokingRoarRecipientResult) })
            Assert.All(type.GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void Roar_PreservesHealthShieldsStatusesHistoryBudgetsAndIndependentInstances()
    {
        var s = new Setup();
        var other = CharacterFactory.Create("nigel", 1)!;
        s.Caster.TakeDamage(7);
        s.Caster.SetShieldHP(5);
        s.Near.SetShieldHP(6);
        s.Near.Statuses.ApplyPoison();
        s.Near.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        HealingApplicator.Apply(0, s.Caster);
        s.Activations.RecordMovement();
        s.Activations.RecordUtilityAction();
        var stats = s.Roster.Select(c => c.BaseStats).ToArray();
        var positions = s.Roster.Select(c => c.TacticalPosition).ToArray();
        for (int repeat = 0; repeat < 3; repeat++) Assert.True(s.Preview().CanExecute);
        Assert.True(s.Execute().Succeeded);
        Assert.Equal(45, s.Caster.CurrentHealth);
        Assert.Equal(5, s.Caster.ShieldHP);
        Assert.Equal(38, s.Near.CurrentHealth);
        Assert.Equal(6, s.Near.ShieldHP);
        Assert.True(s.Near.Statuses.IsPoisoned);
        Assert.Equal(2, s.Near.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(1, s.Caster.HealsReceivedThisRound);
        Assert.Equal(stats, s.Roster.Select(c => c.BaseStats));
        Assert.Equal(positions, s.Roster.Select(c => c.TacticalPosition));
        Assert.True(s.Activations.MovementUsed);
        Assert.True(s.Activations.UtilityActionUsed);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Single(s.Caster.Cooldowns.Cooldowns);
        Assert.Equal(52, other.CurrentHealth);
        Assert.Equal(0, other.ShieldHP);
        Assert.False(other.HasActedThisRound);
        Assert.Empty(other.Cooldowns.Cooldowns);
    }

    [Fact]
    public void Roar_RejectsNullExecutionInputsAndCooldownOverflowBeforeAnyEffect()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(null!, Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Preview(s.Use, null!));
        var extreme = new Setup(round: int.MaxValue);
        Assert.Throws<OverflowException>(() => extreme.Execute());
        Assert.Empty(extreme.Caster.Cooldowns.Cooldowns);
        Assert.True(extreme.Activations.CanUseMainAction);
        Assert.Equal(38, extreme.Near.CurrentHealth);
    }

    private sealed class Setup
    {
        public Character Caster { get; }
        public Character Ally { get; } = CharacterFactory.Create(TimeWeaver.Definition, "ally", 1);
        public Character Near { get; } = CharacterFactory.Create(HoodedAssassin.Definition, "near", 2);
        public Character Boundary { get; } = CharacterFactory.Create(TimeWeaver.Definition, "boundary", 2);
        public Character Far { get; } = CharacterFactory.Create(GentleGuardian.Definition, "far", 2);
        public Character Acted { get; } = CharacterFactory.Create(BeothukSpiritGuardian.Definition, "acted", 2);
        public Character Dead { get; } = CharacterFactory.Create(GraveboundColossus.Definition, "dead", 2);
        public Character[] Roster => new[] { Caster, Ally, Near, Boundary, Far, Acted, Dead };
        public RoundLifecycleManager Lifecycle { get; }
        public ActivationManager Activations { get; }
        public AbilityUse Use { get; set; }
        public ProvokingRoarAbilityExecutor Executor { get; } = new();

        public Setup(int round = 2, CharacterDefinition? casterDefinition = null, bool placeCaster = true, bool placeNear = true)
        {
            Caster = CharacterFactory.Create(casterDefinition ?? Nigel.Definition, "caster", 1);
            if (placeCaster) Caster.SetTacticalPosition(new(5, 5));
            Ally.SetTacticalPosition(new(5, 6));
            if (placeNear) Near.SetTacticalPosition(new(6, 5));
            Boundary.SetTacticalPosition(new(6, 6));
            Far.SetTacticalPosition(new(8, 5));
            Acted.SetTacticalPosition(new(4, 5));
            Dead.SetTacticalPosition(new(5, 4));
            Acted.EndActivation();
            Dead.TakeDamage(int.MaxValue);
            Lifecycle = new(round);
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Activations = new(new[] { Caster, Ally }, new[] { Near, Boundary, Far, Acted, Dead }, 1, Lifecycle);
            Activations.StartActivation(Caster);
            Use = new(Caster, Nigel.ProvokingRoarId, Activations);
        }
        public ProvokingRoarUseResult Preview() => Executor.Preview(Use, Rules);
        public ProvokingRoarUseResult Execute() => Executor.Execute(Use, Rules);
    }
}
