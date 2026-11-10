using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class TemporalIgnitionAbilityIntegrationTests
{
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy,
        true, requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);

    [Fact]
    public void Registration_UsesTheThreeDocumentedNamesAndPreservesFactoryCompatibility()
    {
        var template = CharacterRegistry.GetCharacter("time_weaver")!;
        Assert.Same(TimeWeaver.Definition, template);
        Assert.Equal(new[] { "temporal_ignition", "arcane_transmutation", "temporal_accumulation" }, template.Abilities.Select(a => a.Id));
        Assert.Equal(new[] { "Temporal Ignition", "Arcane Transmutation", "Temporal Accumulation" }, template.Abilities.Select(a => a.DisplayName));
        var ignition = template.Abilities[0];
        Assert.Equal(AbilityClassification.Active, ignition.Classification);
        Assert.Equal(AbilityActionType.MainAction, ignition.RequiredAction);
        Assert.Equal(2, ignition.ReuseInterval);
        Assert.Null(ignition.Range);
        var transmutation = template.Abilities[1];
        Assert.Equal(AbilityClassification.Active, transmutation.Classification);
        Assert.Equal(AbilityActionType.MainAction, transmutation.RequiredAction);
        Assert.Equal(0, transmutation.ReuseInterval);
        Assert.Equal(AbilityRangeKind.EquippedWeapon, transmutation.Range!.Kind);
        var passive = template.Abilities[2];
        Assert.Equal(AbilityClassification.Passive, passive.Classification);
        Assert.Null(passive.RequiredAction);
        Assert.Null(passive.Range);
        Assert.Null(passive.ReuseInterval);
        Assert.All(template.Abilities, a => { Assert.Null(a.IsSpellTagged); Assert.NotEmpty(a.Description); });
        Assert.Throws<NotSupportedException>(() => ((IList<AbilityDefinition>)template.Abilities).Clear());
        foreach (var definition in CharacterRegistry.GetAllCharacters())
        {
            var character = CharacterFactory.Create(definition.Id, 1)!;
            Assert.Same(definition, character.Definition);
            Assert.Same(definition.BaseStats, character.BaseStats);
            Assert.Equal(definition.BaseStats.MaxHealth, character.CurrentHealth);
            if (definition.Id != "time_weaver")
                Assert.DoesNotContain(definition.Abilities, a => a.Id is "temporal_ignition" or "arcane_transmutation" or "temporal_accumulation");
        }
    }

    [Fact]
    public void CanonicalPreview_ReportsKnownDamageAndTimingButDoesNotGuessACastingRange()
    {
        var s = new Setup();
        var preview = s.Preview();
        Assert.True(preview.Eligibility.IsAllowed);
        Assert.Equal(s.Actor.Id, preview.CasterId);
        Assert.Equal(s.Target.Id, preview.TargetId);
        Assert.Equal("ignition", preview.DelayedAttackId);
        Assert.Equal(2, preview.RoundCast);
        Assert.Equal(3, preview.ResolutionRound);
        Assert.Equal(18, preview.ExpectedDamageOnHit); // 17 INT + explicit 4 Power - 8 RES, then confirmed +5.
        Assert.Equal(100, preview.FinalHitChance);
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, preview.Targeting!.RejectionReason);
        Assert.False(preview.CanExecute);
        Assert.False(preview.Succeeded);
        Assert.Null(preview.StoredStrike);
        Assert.Null(preview.StoredDamage);
        Assert.Null(preview.ScheduledAttack);
        Assert.Null(preview.Finalization);
        s.AssertUnchanged();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(100)]
    public void SuppliedScrollRange_DoesNotResolveMissingAbilityRange(int distance)
    {
        var s = new Setup();
        s.Target.SetTacticalPosition(new(5 + distance, 5));
        var result = s.Execute();
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, result.Targeting!.RejectionReason);
        Assert.False(result.Succeeded);
        s.AssertUnchanged();
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("ended", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("acted", AbilityRejectionReason.ActionUnavailable)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("unknown", AbilityRejectionReason.AbilityNotOwned)]
    [InlineData("otherActive", AbilityRejectionReason.UnsupportedAbility)]
    [InlineData("passive", AbilityRejectionReason.NotManuallyActivatable)]
    public void AbilityEligibilityRejectsBeforeRollingOrScheduling(string invalid, AbilityRejectionReason expected)
    {
        var s = new Setup();
        switch (invalid)
        {
            case "dead": s.Actor.TakeDamage(int.MaxValue); break;
            case "inactive": s.Activations.CompleteActivation(); break;
            case "ended": s.Activations.CompleteMainAction(); break;
            case "acted": s.Actor.EndActivation(); break;
            case "cooldown": s.Actor.Cooldowns.RecordUse("temporal_ignition", 2, 2); break;
            case "silence": s.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "unknown": s.Use = new(s.Actor, "unknown", s.Activations); break;
            case "otherActive": s.Use = new(s.Actor, "arcane_transmutation", s.Activations); break;
            case "passive": s.Use = new(s.Actor, "temporal_accumulation", s.Activations); break;
        }
        var result = s.Execute();
        Assert.Equal(expected, result.Eligibility.RejectionReason);
        Assert.Null(result.ScheduledAttack);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Empty(s.Scheduler.PendingAttacks);
        Assert.Equal(52, s.Target.CurrentHealth);
        Assert.Equal(0, s.Target.ShieldHP);
        Assert.Equal(invalid == "cooldown" ? 1 : 0, s.Actor.Cooldowns.Cooldowns.Count);
    }

    [Fact]
    public void WrongCharacter_AndCopiedMetadataDoNotGrantTimeWeaverEligibility()
    {
        var s = new Setup(actor: CharacterFactory.Create("nigel", 1)!);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute().Eligibility.RejectionReason);
        s.AssertUnchanged();
        var original = s.Actor.Definition!;
        var copy = CharacterFactory.Create(new CharacterDefinition(original.Id, original.Name, original.Description,
            original.BaseStats, TimeWeaver.Definition.Abilities), "copy", 1);
        var copied = new Setup(actor: copy);
        Assert.Equal(TemporalIgnitionRejectionReason.NotTimeWeaver, copied.Execute().RejectionReason);
        copied.AssertUnchanged();
    }

    [Fact]
    public void AnotherTimeWeaver_CannotSpendTheActiveCharactersAction()
    {
        var s = new Setup();
        var other = CharacterFactory.Create("time_weaver", 1)!;
        other.SetTacticalPosition(new(5, 5));
        var use = new AbilityUse(other, "temporal_ignition", s.Activations);
        var result = s.Executor.Execute(use, s.Target, "attack", s.Attack, WeaponCategory.Axe, Rules);
        Assert.Equal(AbilityRejectionReason.CharacterNotActive, result.Eligibility.RejectionReason);
        s.AssertUnchanged();
        Assert.Empty(other.Cooldowns.Cooldowns);
    }

    [Fact]
    public void RejectedCanonicalTargets_DoNotReceiveDamageOrScheduleAnything()
    {
        var s = new Setup();
        var ally = CharacterFactory.Create("nigel", 1)!;
        ally.SetTacticalPosition(new(6, 5));
        var allyResult = s.Executor.Execute(s.Use, ally, "ally", s.Attack, WeaponCategory.Axe, Rules);
        Assert.False(allyResult.CanExecute);
        // Existing targeting reports unresolved range before checking other target constraints.
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, allyResult.Targeting!.RejectionReason);
        s.Target.TakeDamage(int.MaxValue);
        var defeatedResult = s.Execute();
        Assert.False(defeatedResult.CanExecute);
        Assert.Equal(0, s.Target.CurrentHealth);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Empty(s.Scheduler.PendingAttacks);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void MissingPosition_IsExplicit_AndRepeatedRejectedUseDoesNotFinalize()
    {
        var s = new Setup(setPosition: false);
        for (int i = 0; i < 2; i++)
        {
            var result = s.Execute();
            Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation, result.Targeting!.RejectionReason);
            Assert.Null(result.Finalization);
            Assert.False(result.Succeeded);
        }
        s.AssertUnchanged();
    }

    [Fact]
    public void Preview_RechecksEffectiveResistanceWithoutMutatingStatsOrRolling()
    {
        var s = new Setup();
        var stats = s.Target.BaseStats;
        var first = s.Preview();
        s.Target.TemporaryModifiers.AddRange(new[]
        {
            new TemporaryStatModifier("explicit-res", "source", s.Target.Id, TemporaryStat.Resistance, 7, 2, 3)
        });
        var second = s.Execute();
        Assert.Equal(18, first.ExpectedDamageOnHit);
        Assert.Equal(11, second.ExpectedDamageOnHit);
        Assert.Same(stats, s.Target.BaseStats);
        Assert.Equal(8, stats.Resistance);
        Assert.Equal(15, s.Target.EffectiveResistance);
        s.AssertUnchanged();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 20)]
    public void AccuracyPreview_UsesExplicitInputsWithoutConsumingARoll(int baseAccuracy, int evasion)
    {
        var s = new Setup();
        var attack = new AttackConfiguration(AttackType.Magical, WeaponCategory.MagicScroll, 4, baseAccuracy, 1, 2);
        var result = s.Executor.Preview(s.Use, s.Target, "preview", attack, WeaponCategory.Axe, Rules, evasion);
        Assert.Equal(Math.Max(0, baseAccuracy - evasion), result.FinalHitChance);
        s.AssertUnchanged();
    }

    [Fact]
    public void MissingOrPhysicalCombatConfiguration_IsRejectedWithoutChoosingSpellPower()
    {
        var s = new Setup();
        s.Attack = null;
        Assert.Equal(TemporalIgnitionRejectionReason.CombatConfigurationRequired, s.Execute().RejectionReason);
        s.Attack = AttackConfiguration.CreateUnarmed(0, 100);
        Assert.Equal(TemporalIgnitionRejectionReason.RequiresMagicalAttack, s.Execute().RejectionReason);
        s.AssertUnchanged();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void InvalidDelayedIds_ThrowBeforeRandomnessOrMutation(string? id)
    {
        var s = new Setup();
        Assert.ThrowsAny<ArgumentException>(() => s.Executor.Execute(s.Use, s.Target, id!, s.Attack, WeaponCategory.Axe, Rules));
        s.AssertUnchanged();
    }

    [Fact]
    public void SchedulingReadiness_RejectsDuplicateIdBeforeAnyCastingRoll()
    {
        var s = new Setup();
        var ordinaryStoredStrike = new SingleStrikeResolver(new(new Rolls())).ResolveMagicalStrike(s.Actor.BaseStats,
            s.Target.BaseStats, 4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 100);
        var existing = s.Scheduler.Schedule(DelayedAttack.CreateTemporalIgnition("ignition", s.Actor.Id, s.Target.Id, 2, ordinaryStoredStrike));
        Assert.Throws<ArgumentException>(() => s.Execute());
        Assert.Same(existing, Assert.Single(s.Scheduler.PendingAttacks));
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
    }

    [Fact]
    public void WrongPhaseStaleRoundAndRoundOverflow_RejectBeforeScheduling()
    {
        var s = new Setup();
        s.Lifecycle.AdvancePhase();
        Assert.Equal(AbilityRejectionReason.WrongPhase, s.Execute().Eligibility.RejectionReason);
        while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        Assert.Equal(AbilityRejectionReason.UseContextChanged, s.Execute().Eligibility.RejectionReason);
        s.AssertUnchanged();
        var overflow = new Setup(round: int.MaxValue - 1);
        Assert.Throws<OverflowException>(() => overflow.Execute());
        overflow.AssertUnchanged();
    }

    [Fact]
    public void InvalidCombatInputsAndDamageOverflow_AreCaughtBeforeRolling()
    {
        var s = new Setup();
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Executor.Execute(s.Use, s.Target, "attack", s.Attack,
            (WeaponCategory)999, Rules));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Executor.Execute(s.Use, s.Target, "attack", s.Attack,
            WeaponCategory.Axe, Rules, -1));
        s.Attack = new(AttackType.Magical, WeaponCategory.MagicScroll, int.MaxValue - 10, 100, 1, 2);
        Assert.Throws<OverflowException>(() => s.Execute());
        s.AssertUnchanged();
    }

    [Fact]
    public void ResultsAreImmutableAndPreviewDoesNotConsumeSchedulingIdentity()
    {
        var s = new Setup();
        var first = s.Preview();
        var second = s.Preview();
        Assert.Equal(first.ExpectedDamageOnHit, second.ExpectedDamageOnHit);
        Assert.All(typeof(TemporalIgnitionUseResult).GetProperties(), property => Assert.Null(property.SetMethod));
        s.Scheduler.ValidateCanSchedule("ignition");
        s.AssertUnchanged();
        Assert.Throws<ArgumentNullException>(() => new TemporalIgnitionAbilityExecutor(null!, s.Scheduler));
        Assert.Throws<ArgumentNullException>(() => new TemporalIgnitionAbilityExecutor(new(new(new Rolls())), null!));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(null!, s.Target, "attack", s.Attack, WeaponCategory.Axe, Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, null!, "attack", s.Attack, WeaponCategory.Axe, Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, s.Target, "attack", s.Attack, WeaponCategory.Axe, null!));
    }

    // These tests exercise the already-confirmed low-level delayed lifecycle, not a
    // successful Temporal Ignition ability cast with an invented casting range.
    [Theory]
    [InlineData(0, 13)]
    [InlineData(99, 0)]
    public void ExistingDelayedPipeline_UsesStoredOutcomesCurrentShieldsAndNoSecondRoll(int roll, int damage)
    {
        var s = new Setup();
        var rolls = new Rolls(roll);
        var strike = new SingleStrikeResolver(new(rolls)).ResolveMagicalStrike(s.Actor.BaseStats, s.Target.BaseStats,
            4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 80);
        Assert.Equal(damage, strike.Damage);
        var scheduled = s.Scheduler.Schedule(DelayedAttack.CreateTemporalIgnition("stored", s.Actor.Id, s.Target.Id, 2, strike));
        Assert.Same(strike, scheduled.Strike);
        Assert.Equal(52, s.Target.CurrentHealth);
        s.Target.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("later-res", "source", s.Target.Id,
            TemporaryStat.Resistance, 20, 3, 4) });
        s.Target.SetShieldHP(6);
        s.Actor.TakeDamage(int.MaxValue);
        var lifecycle = DelayedPhase();
        var batch = new DelayedAttackProcessor(s.Scheduler, lifecycle).Process(new[] { s.Actor, s.Target });
        var entry = Assert.Single(batch.Entries);
        Assert.Same(strike, entry.Attack.Strike);
        Assert.Equal(Math.Min(6, damage), entry.ShieldHPAbsorbed);
        Assert.Equal(Math.Max(0, damage - 6), entry.HealthLost);
        Assert.Equal(1, rolls.Calls);
        Assert.Empty(s.Scheduler.PendingAttacks);
    }

    [Fact]
    public void ExistingScheduler_PreservesInsertionOrderFutureEntriesAndDefeatedTargetCancellation()
    {
        var s = new Setup();
        var futureTarget = CharacterFactory.Create("nigel", 2)!;
        var strike = new SingleStrikeResolver(new(new Rolls())).ResolveMagicalStrike(s.Actor.BaseStats, s.Target.BaseStats,
            4, WeaponCategory.MagicScroll, WeaponCategory.Axe, 100);
        s.Scheduler.Schedule(DelayedAttack.CreateTemporalIgnition("first", s.Actor.Id, s.Target.Id, 2, strike));
        s.Scheduler.Schedule(DelayedAttack.CreateTemporalIgnition("future", s.Actor.Id, futureTarget.Id, 3, strike));
        s.Scheduler.Schedule(DelayedAttack.CreateTemporalIgnition("second", s.Actor.Id, s.Target.Id, 2, strike));
        Assert.Equal(new[] { "first", "second" }, s.Scheduler.GetDueAttacks(3).Select(a => a.Id));
        s.Target.TakeDamage(int.MaxValue);
        var batch = new DelayedAttackProcessor(s.Scheduler, DelayedPhase()).Process(new[] { s.Target, futureTarget });
        Assert.Equal(new[] { "first", "second" }, batch.CancelledAttackIds);
        Assert.Equal("future", Assert.Single(s.Scheduler.PendingAttacks).Id);
        Assert.Throws<ArgumentException>(() => s.Scheduler.ValidateCanSchedule("first"));
        Assert.Equal(0, s.Rolls.Calls);
    }

    private static RoundLifecycleManager DelayedPhase()
    {
        var lifecycle = new RoundLifecycleManager(3);
        lifecycle.AdvancePhase();
        new PoisonBatchProcessor(lifecycle).Process(Array.Empty<Character>());
        lifecycle.AdvancePhase();
        return lifecycle;
    }

    private sealed class Rolls(int roll = 0) : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return roll; }
    }

    private sealed class Setup
    {
        public Character Actor { get; }
        public Character Target { get; } = CharacterFactory.Create("nigel", 2)!;
        public RoundLifecycleManager Lifecycle { get; }
        public ActivationManager Activations { get; }
        public AbilityUse Use { get; set; }
        public AttackConfiguration? Attack { get; set; } = new(AttackType.Magical, WeaponCategory.MagicScroll, 4, 100, 1, 2);
        public Rolls Rolls { get; } = new();
        public DelayedAttackScheduler Scheduler { get; } = new();
        public TemporalIgnitionAbilityExecutor Executor { get; }

        public Setup(Character? actor = null, bool setPosition = true, int round = 2)
        {
            Actor = actor ?? CharacterFactory.Create("time_weaver", 1)!;
            if (setPosition) Actor.SetTacticalPosition(new(5, 5));
            Target.SetTacticalPosition(new(6, 5));
            Lifecycle = new(round);
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Activations = new(new[] { Actor }, new[] { Target }, 1, Lifecycle);
            Activations.StartActivation(Actor);
            Use = new(Actor, "temporal_ignition", Activations);
            Executor = new(new(new(Rolls)), Scheduler);
        }

        public TemporalIgnitionUseResult Preview() => Executor.Preview(Use, Target, "ignition", Attack, WeaponCategory.Axe, Rules);
        public TemporalIgnitionUseResult Execute() => Executor.Execute(Use, Target, "ignition", Attack, WeaponCategory.Axe, Rules);
        public void AssertUnchanged()
        {
            Assert.Equal(0, Rolls.Calls);
            Assert.Empty(Scheduler.PendingAttacks);
            Assert.Empty(Actor.Cooldowns.Cooldowns);
            Assert.Equal(52, Target.CurrentHealth);
            Assert.Equal(0, Target.ShieldHP);
            Assert.False(Activations.MainActionUsed);
        }
    }
}
