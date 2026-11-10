using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class GentleBlessingTests
{
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character, requiresMapValidation: false,
        requiresLineOfSight: false, requiresElevationRules: false);

    [Fact]
    public void SuccessfulHealing_AppliesBothCanonicalBonusesWithoutChangingHealing()
    {
        var s = new HealingSetup();
        var result = s.Heal();
        Assert.True(result.Succeeded);
        Assert.Equal(11, result.ActualHPRestored);
        Assert.Equal(23, s.Target.CurrentHealth);
        var blessing = Assert.IsType<GentleBlessingResult>(result.GentleBlessing);
        Assert.Equal(GentleBlessingOutcome.Applied, blessing.Outcome);
        Assert.Equal(s.Caster.Id, blessing.SourceCharacterId);
        Assert.Equal(s.Target.Id, blessing.RecipientCharacterId);
        Assert.Equal(2, blessing.ApplicationRound);
        Assert.Equal(3, blessing.ExpirationRound);
        Assert.Equal(2, s.Target.TemporaryModifiers.Active.Count);
        Assert.Equal(s.Target.BaseStats.Defense + 2, s.Target.EffectiveDefense);
        Assert.Equal(s.Target.BaseStats.Resistance + 2, s.Target.EffectiveResistance);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Null(s.Activations.ActiveCharacter);
        Assert.Single(s.Caster.Cooldowns.Cooldowns);
        Assert.Equal(0, s.Caster.Cooldowns.Cooldowns["healing_light"].ReuseInterval);
        Assert.Equal(1, s.Target.HealsReceivedThisRound);
    }

    [Fact]
    public void PreviewAndRejectedAttempts_DoNotProcessThePassive()
    {
        var s = new HealingSetup();
        var preview = s.Executor.Preview(s.Use, s.Target, Rules);
        Assert.Null(preview.GentleBlessing);
        Assert.Equal(GentleBlessingOutcome.NotSuccessful, GentleBlessing.Process(s.Use, s.Target, null).Outcome);
        Assert.Equal(12, s.Target.CurrentHealth);
        Assert.Equal(0, s.Target.HealsReceivedThisRound);
        Assert.Empty(s.Target.TemporaryModifiers.Active);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
        s.Target.SetTacticalPosition(new(100, 100));
        Assert.Null(s.Heal().GentleBlessing);
        s.Target.SetTacticalPosition(new(6, 5));
        Assert.Equal(GentleBlessingOutcome.Applied, s.Heal().GentleBlessing!.Outcome);
    }

    [Fact]
    public void DuplicateProcessing_IsRememberedOnTheUseRatherThanOnAProcessorInstance()
    {
        var s = new HealingSetup();
        var result = s.Heal();
        Assert.Equal(GentleBlessingOutcome.AlreadyProcessed,
            GentleBlessing.Process(s.Use, s.Target, result.Finalization).Outcome);
        Assert.Equal(GentleBlessingOutcome.AlreadyProcessed,
            GentleBlessing.Process(s.Use, s.Target, result.Finalization).Outcome);
        Assert.Equal(23, s.Target.CurrentHealth);
        Assert.Equal(1, s.Target.HealsReceivedThisRound);
        Assert.Equal(2, s.Target.TemporaryModifiers.Active.Count);
        Assert.Single(s.Caster.Cooldowns.Cooldowns);
        Assert.Equal(GentleBlessingOutcome.InvalidUseContext,
            GentleBlessing.Process(s.Use, s.Caster, result.Finalization).Outcome);
    }

    [Fact]
    public void Ownership_IsRequired_AndThePassiveCannotBeActivatedManually()
    {
        var s = new HealingSetup();
        var passive = Assert.Single(s.Caster.Definition!.Abilities, a => a.Id == "gentle_blessing");
        Assert.Equal(AbilityClassification.Passive, passive.Classification);
        Assert.Equal(AbilityRejectionReason.NotManuallyActivatable,
            AbilityEligibility.Evaluate(s.Caster, passive.Id, s.Activations).RejectionReason);
        var other = CharacterFactory.Create(new CharacterDefinition("other_fixture", "Other", "Test only",
            s.Caster.BaseStats, s.Caster.Definition.Abilities), "other", 1);
        var activations = new ActivationManager(new[] { other }, new[] { s.Enemy }, 1);
        activations.StartActivation(other);
        var use = new AbilityUse(other, "healing_light", activations);
        var finalization = AbilityUseFinalizer.FinalizeSuccessfulUse(use);
        Assert.Equal(GentleBlessingOutcome.NotPassiveOwner, GentleBlessing.Process(use, s.Target, finalization).Outcome);
        Assert.Empty(s.Target.TemporaryModifiers.Active);
    }

    [Fact]
    public void MissingPassive_DoesNotTurnSuccessfulHealingIntoFailure()
    {
        var template = GentleGuardian.Definition;
        var definition = new CharacterDefinition(template.Id, template.Name, template.Description, template.BaseStats,
            template.Abilities.Where(a => a.Id != "gentle_blessing"));
        var s = new HealingSetup(CharacterFactory.Create(definition, "no-passive", 1));
        var result = s.Heal();
        Assert.True(result.Succeeded);
        Assert.Equal(GentleBlessingOutcome.NotPassiveOwner, result.GentleBlessing!.Outcome);
        Assert.Equal(23, s.Target.CurrentHealth);
    }

    [Fact]
    public void SanctuaryShield_UnresolvedRangeStillPreventsBothShieldAndPassive()
    {
        var s = new HealingSetup();
        var use = new AbilityUse(s.Caster, "sanctuary_shield", s.Activations);
        var executor = new SanctuaryShieldAbilityExecutor(new());
        Assert.Null(executor.Preview(use, s.Target, Rules).GentleBlessing);
        var result = executor.Execute(use, s.Target, Rules);
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, result.Targeting!.RejectionReason);
        Assert.Null(result.GentleBlessing);
        Assert.Equal(0, s.Target.ShieldHP);
        Assert.Empty(s.Target.TemporaryModifiers.Active);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
    }

    [Fact]
    public void ExpirationOverflow_RejectsOnlyThePassiveWithoutUndoingSuccessfulHealing()
    {
        var s = new HealingSetup(round: int.MaxValue);
        var result = s.Heal();
        Assert.True(result.Succeeded);
        Assert.Equal(GentleBlessingOutcome.Rejected, result.GentleBlessing!.Outcome);
        Assert.Equal(GentleBlessingRejectionReason.ExpirationRoundOverflow, result.GentleBlessing.RejectionReason);
        Assert.Null(result.GentleBlessing.ExpirationRound);
        Assert.Empty(s.Target.TemporaryModifiers.Active);
        Assert.Equal(23, s.Target.CurrentHealth);
    }

    [Fact]
    public void DifferentRecipientsAndHealers_GetIndependentEffects()
    {
        var first = new HealingSetup();
        var second = new HealingSetup();
        Assert.Equal(first.Target.Id, first.Heal().GentleBlessing!.RecipientCharacterId);
        Assert.Equal(second.Target.Id, second.Heal().GentleBlessing!.RecipientCharacterId);
        Assert.Equal(2, first.Target.TemporaryModifiers.Active.Count);
        Assert.Equal(2, second.Target.TemporaryModifiers.Active.Count);
    }

    [Fact]
    public void InvalidTriggerInputs_AndUnrelatedFinalizationsDoNotChangeState()
    {
        var first = new HealingSetup();
        var second = new HealingSetup();
        var a = first.Heal();
        var b = second.Heal();
        Assert.Equal(GentleBlessingOutcome.InvalidUseContext,
            GentleBlessing.Process(first.Use, first.Target, b.Finalization).Outcome);
        Assert.Throws<ArgumentNullException>(() => GentleBlessing.Process(null!, first.Target, a.Finalization));
        Assert.Throws<ArgumentNullException>(() => GentleBlessing.Process(first.Use, null!, a.Finalization));
        Assert.All(typeof(GentleBlessingResult).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(2, first.Target.TemporaryModifiers.Active.Count);
        Assert.Single(first.Caster.Cooldowns.Cooldowns);
    }

    [Fact]
    public void ExplicitGenericBonuses_AffectEffectiveStatsAndPreserveBaseAndOtherState()
    {
        var target = CharacterFactory.Create("nigel", 1)!;
        var stats = target.BaseStats;
        target.SetShieldHP(6);
        target.EndActivation();
        // Explicit generic test values, not canonical Gentle Blessing bonuses.
        var modifiers = new[] { Modifier(target, TemporaryStat.Defense, 5), Modifier(target, TemporaryStat.Resistance, 7) };
        target.TemporaryModifiers.AddRange(modifiers);
        Assert.Equal(16, target.EffectiveDefense);
        Assert.Equal(15, target.EffectiveResistance);
        Assert.Same(stats, target.BaseStats);
        Assert.Equal(11, stats.Defense);
        Assert.Equal(8, stats.Resistance);
        Assert.Equal(stats.Speed, target.EffectiveSpeed);
        Assert.Equal(stats.Movement, target.EffectiveMovement);
        Assert.Equal(52, target.CurrentHealth);
        Assert.Equal(6, target.ShieldHP);
        Assert.True(target.HasActedThisRound);
        Assert.Empty(target.Cooldowns.Cooldowns);
        Assert.Equal(modifiers, target.TemporaryModifiers.Active);
        Assert.Throws<NotSupportedException>(() => ((IList<TemporaryStatModifier>)target.TemporaryModifiers.Active).Clear());
    }

    [Fact]
    public void BurnAndExplicitPositiveDefense_CombineWithoutChangingBaseStats()
    {
        var target = CharacterFactory.Create("nigel", 1)!;
        target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        target.TemporaryModifiers.AddRange(new[] { Modifier(target, TemporaryStat.Defense, 5) });
        Assert.Equal(13, target.EffectiveDefense); // 11 base + 5 explicit bonus - 3 Burn.
        target.Statuses.AdvanceTimedStatuses();
        Assert.Equal(13, target.EffectiveDefense);
        target.Statuses.AdvanceTimedStatuses();
        Assert.Equal(16, target.EffectiveDefense);
        Assert.Equal(11, target.BaseStats.Defense);
        target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Equal(11, target.EffectiveDefense);
    }

    [Fact]
    public void OpeningCountersAndFollowUps_UseEffectiveDefenseAndResistance()
    {
        var attacker = CharacterFactory.Create("time_weaver", 1)!;
        var defender = CharacterFactory.Create("nigel", 2)!;
        attacker.TemporaryModifiers.AddRange(new[] { Modifier(attacker, TemporaryStat.Defense, 4) });
        defender.TemporaryModifiers.AddRange(new[] { Modifier(defender, TemporaryStat.Resistance, 5) });
        var rolls = new FixedRolls();
        var resolver = new CombatExchangeResolver(new(new(rolls)));
        var request = new CombatExchangeRequest(attacker, defender, AttackType.Magical, 4,
            WeaponCategory.MagicScroll, WeaponCategory.Axe, 100,
            AttackType.Physical, 0, 100, 1, 1, 1, true, false, IncomingAttackPermitsFollowUps: true);
        var result = resolver.Resolve(request);
        Assert.Equal(8, result.OpeningStrike.Damage); // 17 INT + 4 Power - (8 RES + 5 bonus).
        Assert.Equal(9, result.CounterStrike!.Damage); // 20 STR - (8 DEF + 4 blessing) + 1 Fury after losing 8 HP.
        Assert.Equal(4, result.FollowUpStrike!.Damage);
        Assert.Equal(3, rolls.Calls);
        Assert.Equal(40, defender.CurrentHealth);
        Assert.Equal(29, attacker.CurrentHealth);
    }

    [Fact]
    public void StatsOnlyMagicKeepsBaseBehavior_AndEffectiveResistanceOverrideIsValidated()
    {
        var a = CharacterFactory.Create("time_weaver", 1)!;
        var d = CharacterFactory.Create("nigel", 2)!;
        d.TemporaryModifiers.AddRange(new[] { Modifier(d, TemporaryStat.Resistance, 5) });
        Assert.Equal(13, DamageCalculator.CalculateMagicalDamage(a.BaseStats, d.BaseStats, 4));
        Assert.Equal(8, DamageCalculator.CalculateMagicalDamage(a.BaseStats, d.BaseStats, 4, d.EffectiveResistance));
        Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.CalculateMagicalDamage(a.BaseStats, d.BaseStats, 4, -1));
        var rolls = new FixedRolls();
        var strike = new SingleStrikeResolver(new(rolls)).ResolveMagicalStrike(a.BaseStats, d.BaseStats, 4,
            WeaponCategory.MagicScroll, WeaponCategory.Axe, 100, effectiveResistance: d.EffectiveResistance);
        Assert.Equal(8, strike.Damage);
        Assert.Equal(1, rolls.Calls);
    }

    [Fact]
    public void ExplicitExpiration_IsAtEndOfCleanupAndIdempotent()
    {
        var target = CharacterFactory.Create("nigel", 1)!;
        target.Statuses.ApplyPoison();
        var original = Modifier(target, TemporaryStat.Resistance, 7);
        target.TemporaryModifiers.AddRange(new[] { original });
        var before = target.TemporaryModifiers.Active;
        Assert.Empty(target.TemporaryModifiers.ExpireAtRoundEnd(2, RoundPhase.Cleanup));
        Assert.Equal(15, target.EffectiveResistance);
        Assert.Throws<ArgumentException>(() => target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.TimedStatusUpdate));
        var expired = Assert.Single(target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup));
        Assert.False(expired.IsActive);
        Assert.Equal(2, expired.ApplicationRound);
        Assert.Equal(3, expired.ExpirationRound);
        Assert.Equal(8, target.EffectiveResistance);
        Assert.Empty(target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup));
        Assert.True(target.Statuses.IsPoisoned);
        Assert.Equal(0, target.Statuses.PoisonTicksOccurred);
        Assert.True(Assert.Single(before).IsActive);
        Assert.All(typeof(TemporaryStatModifier).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void ExpiredResistance_StopsReducingActualCombatDamage()
    {
        var a = CharacterFactory.Create("time_weaver", 1)!;
        var d = CharacterFactory.Create("nigel", 2)!;
        d.TemporaryModifiers.AddRange(new[] { Modifier(d, TemporaryStat.Resistance, 5) });
        var resolver = new CombatExchangeResolver(new(new(new FixedRolls())));
        var request = new CombatExchangeRequest(a, d, AttackType.Magical, 4, WeaponCategory.MagicScroll, WeaponCategory.Axe,
            100, AttackType.Physical, 0, 100, 1, 1, 1, false, false);
        Assert.Equal(8, resolver.Resolve(request).OpeningStrike.Damage);
        d.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Equal(13, resolver.Resolve(request).OpeningStrike.Damage);
    }

    [Fact]
    public void DifferentExplicitDurations_ExpireIndependentlyInDeterministicOrder()
    {
        var target = CharacterFactory.Create("nigel", 1)!;
        var defense = Modifier(target, TemporaryStat.Defense, 5);
        var resistance = new TemporaryStatModifier("later", "source", target.Id, TemporaryStat.Resistance, 7, 2, 4);
        target.TemporaryModifiers.AddRange(new[] { defense, resistance });
        Assert.Equal(defense.Id, Assert.Single(target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup)).Id);
        Assert.Equal(resistance, Assert.Single(target.TemporaryModifiers.Active));
        Assert.Equal(11, target.EffectiveDefense);
        Assert.Equal(15, target.EffectiveResistance);
        Assert.Equal(resistance.Id, Assert.Single(target.TemporaryModifiers.ExpireAtRoundEnd(4, RoundPhase.Cleanup)).Id);
        Assert.Equal(8, target.EffectiveResistance);
        Assert.Throws<ArgumentOutOfRangeException>(() => target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup));
        Assert.Throws<ArgumentException>(() => target.TemporaryModifiers.AddRange(new[] { Modifier(target, TemporaryStat.Defense, 5) }));
    }

    [Fact]
    public void BonusesSurviveSourceDeathButClearOnRecipientDefeat()
    {
        var source = CharacterFactory.Create("gentle_guardian", 1)!;
        var recipient = CharacterFactory.Create("nigel", 1)!;
        recipient.TemporaryModifiers.AddRange(new[]
        {
            new TemporaryStatModifier("explicit", source.Id, recipient.Id, TemporaryStat.Defense, 5, 2, 3)
        });
        source.TakeDamage(int.MaxValue);
        Assert.Equal(16, recipient.EffectiveDefense);
        recipient.TakeDamage(int.MaxValue);
        Assert.Empty(recipient.TemporaryModifiers.Active);
        Assert.Equal(11, recipient.EffectiveDefense);
        Assert.Throws<InvalidOperationException>(() => recipient.TemporaryModifiers.AddRange(new[] { Modifier(recipient, TemporaryStat.Defense, 5) }));
    }

    [Fact]
    public void NegativeStatusProcessingAndCleansing_DoNotRemovePositiveBonuses()
    {
        var target = CharacterFactory.Create("nigel", 1)!;
        target.TemporaryModifiers.AddRange(new[] { Modifier(target, TemporaryStat.Defense, 5) });
        target.Statuses.ApplyTimedStatus(StatusType.Burn, 1);
        target.Statuses.AdvanceTimedStatuses();
        Assert.Equal(16, target.EffectiveDefense);
        target.Statuses.RemoveAllNegativeStatuses();
        Assert.Single(target.TemporaryModifiers.Active);
        Assert.Equal(16, target.EffectiveDefense);
    }

    [Fact]
    public void DuplicateAndOverlappingBonusesAreRejectedWithoutInventingRefreshOrStacking()
    {
        var target = CharacterFactory.Create("nigel", 1)!;
        var first = Modifier(target, TemporaryStat.Defense, 5);
        target.TemporaryModifiers.AddRange(new[] { first });
        Assert.Throws<ArgumentException>(() => target.TemporaryModifiers.AddRange(new[] { first }));
        var overlapping = new TemporaryStatModifier("other", "different-source", target.Id, TemporaryStat.Defense, 7, 2, 3);
        Assert.Throws<NotSupportedException>(() => target.TemporaryModifiers.AddRange(new[] { overlapping }));
        Assert.Single(target.TemporaryModifiers.Active);
        Assert.Equal(16, target.EffectiveDefense);
        target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        target.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("later", "source", target.Id, TemporaryStat.Defense, 7, 4, 5) });
        Assert.Equal(18, target.EffectiveDefense);
    }

    [Fact]
    public void OverflowOrInvalidRecipientInAGroup_IsRejectedBeforeAnyModifierIsStored()
    {
        var target = CharacterFactory.Create("nigel", 1)!;
        var valid = Modifier(target, TemporaryStat.Resistance, 5);
        var overflow = Modifier(target, TemporaryStat.Defense, int.MaxValue);
        Assert.Throws<OverflowException>(() => target.TemporaryModifiers.AddRange(new[] { valid, overflow }));
        Assert.Empty(target.TemporaryModifiers.Active);
        var wrongRecipient = new TemporaryStatModifier("wrong", "source", "another-character", TemporaryStat.Defense, 5, 2, 3);
        Assert.Throws<ArgumentException>(() => target.TemporaryModifiers.AddRange(new[] { valid, wrongRecipient }));
        Assert.Empty(target.TemporaryModifiers.Active);
        target.TemporaryModifiers.AddRange(new[] { valid }); // Failed attempts did not reserve the ID.
        Assert.Equal(13, target.EffectiveResistance);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("source")]
    [InlineData("recipient")]
    [InlineData("stat")]
    [InlineData("value")]
    [InlineData("round")]
    [InlineData("expiration")]
    public void MalformedModifiers_AreRejected(string invalid)
        => Assert.ThrowsAny<ArgumentException>(() => new TemporaryStatModifier(
            invalid == "id" ? " " : "id", invalid == "source" ? "" : "source", invalid == "recipient" ? "" : "recipient",
            invalid == "stat" ? (TemporaryStat)999 : TemporaryStat.Defense, invalid == "value" ? -1 : 5,
            invalid == "round" ? 0 : 2, invalid == "expiration" ? 1 : 3));

    [Fact]
    public void ZeroBonusAndLargeValidBonus_AreSafeAndIndependentAcrossRecipients()
    {
        var first = CharacterFactory.Create("nigel", 1)!;
        var second = CharacterFactory.Create("nigel", 1)!;
        first.TemporaryModifiers.AddRange(new[] { Modifier(first, TemporaryStat.Defense, int.MaxValue - 11) });
        second.TemporaryModifiers.AddRange(new[] { Modifier(second, TemporaryStat.Resistance, 0) });
        Assert.Equal(int.MaxValue, first.EffectiveDefense);
        Assert.Equal(11, second.EffectiveDefense);
        Assert.Equal(8, second.EffectiveResistance);
        Assert.Throws<ArgumentOutOfRangeException>(() => first.TemporaryModifiers.GetBonus((TemporaryStat)999));
        Assert.Throws<ArgumentNullException>(() => first.TemporaryModifiers.AddRange(null!));
    }

    [Fact]
    public void ReapplicationFromAnotherGuardian_RefreshesOnePairWithoutStacking()
    {
        var first = new HealingSetup();
        var original = first.Heal();
        var originalModifiers = first.Target.TemporaryModifiers.Active;
        var second = new HealingSetup(target: first.Target);
        var refreshed = second.Heal();
        Assert.Equal(GentleBlessingOutcome.Refreshed, refreshed.GentleBlessing!.Outcome);
        Assert.Equal(8, refreshed.ActualHPRestored); // Healing history still belongs to the recipient.
        Assert.Equal(2, first.Target.TemporaryModifiers.Active.Count);
        Assert.Equal(13, first.Target.EffectiveDefense);
        Assert.Equal(10, first.Target.EffectiveResistance);
        Assert.Equal(originalModifiers.Select(m => m.Id), refreshed.GentleBlessing.Modifiers.Select(m => m.Id));
        Assert.All(refreshed.GentleBlessing.Modifiers, m => Assert.Equal(second.Caster.Id, m.SourceCharacterId));
        Assert.All(original.GentleBlessing!.Modifiers, m => Assert.Equal(first.Caster.Id, m.SourceCharacterId));
        var third = new HealingSetup(target: first.Target);
        Assert.Equal(GentleBlessingOutcome.Refreshed, third.Heal().GentleBlessing!.Outcome);
        Assert.Equal(13, first.Target.EffectiveDefense);
        Assert.Equal(10, first.Target.EffectiveResistance);
        Assert.Equal(2, first.Target.TemporaryModifiers.Active.Count);
    }

    [Fact]
    public void FollowingRoundReapplication_ExtendsBothDeadlinesAndKeepsStableIds()
    {
        var first = new HealingSetup();
        var applied = first.Heal();
        var snapshot = first.Target.TemporaryModifiers.Active;
        Assert.Empty(first.Target.TemporaryModifiers.ExpireAtRoundEnd(2, RoundPhase.Cleanup));
        first.Caster.ResetActivation();
        first.Target.ResetHealingHistory(); // The existing RoundPreparation coordinator owns these resets in gameplay.
        var nextRound = new HealingSetup(first.Caster, 3, first.Target);
        var result = nextRound.Heal();
        Assert.Equal(GentleBlessingOutcome.Refreshed, result.GentleBlessing!.Outcome);
        Assert.Equal(4, result.GentleBlessing.ExpirationRound);
        Assert.All(result.GentleBlessing.Modifiers, m =>
        {
            Assert.Equal(3, m.ApplicationRound);
            Assert.Equal(4, m.ExpirationRound);
            Assert.Equal(2, m.Value);
        });
        Assert.Equal(snapshot.Select(m => m.Id), result.GentleBlessing.Modifiers.Select(m => m.Id));
        Assert.All(applied.GentleBlessing!.Modifiers, m => Assert.Equal(3, m.ExpirationRound));
        Assert.Empty(first.Target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup));
        Assert.Equal(13, first.Target.EffectiveDefense);
        Assert.Equal(10, first.Target.EffectiveResistance);
        Assert.Equal(2, first.Target.TemporaryModifiers.ExpireAtRoundEnd(4, RoundPhase.Cleanup).Count);
        Assert.Equal(11, first.Target.EffectiveDefense);
        Assert.Equal(8, first.Target.EffectiveResistance);
        Assert.Empty(first.Target.TemporaryModifiers.ExpireAtRoundEnd(4, RoundPhase.Cleanup));
    }

    [Fact]
    public void InitialBlessing_ExpiresTogetherAtFollowingRoundCleanupWithoutChangingStatusesOrCooldowns()
    {
        var s = new HealingSetup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        s.Target.Statuses.ApplyPoison();
        s.Caster.Cooldowns.RecordUse("other", 2, 3);
        var result = s.Heal();
        Assert.Equal(3, result.GentleBlessing!.ExpirationRound);
        Assert.Empty(s.Target.TemporaryModifiers.ExpireAtRoundEnd(2, RoundPhase.Cleanup));
        Assert.Equal(13, s.Target.EffectiveDefense);
        Assert.Equal(10, s.Target.EffectiveResistance);
        var expired = s.Target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Equal(2, expired.Count);
        Assert.All(expired, m => Assert.False(m.IsActive));
        Assert.Equal(11, s.Target.EffectiveDefense);
        Assert.Equal(8, s.Target.EffectiveResistance);
        Assert.Equal(2, s.Target.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.True(s.Target.Statuses.IsPoisoned);
        Assert.Equal(0, s.Target.Statuses.PoisonTicksOccurred);
        Assert.Equal(5, s.Caster.Cooldowns.Cooldowns["other"].ReadyRound);
        Assert.Empty(s.Target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup));
        Assert.Throws<NotSupportedException>(() => ((IList<TemporaryStatModifier>)result.GentleBlessing.Modifiers).Clear());
    }

    [Fact]
    public void TheSameGuardian_CanMaintainBlessingsOnIndependentRecipients()
    {
        var first = new HealingSetup();
        first.Heal();
        first.Target.TemporaryModifiers.ExpireAtRoundEnd(2, RoundPhase.Cleanup);
        first.Caster.ResetActivation();
        var second = new HealingSetup(first.Caster, 3);
        second.Heal();
        Assert.All(first.Target.TemporaryModifiers.Active, m => Assert.Equal(3, m.ExpirationRound));
        Assert.All(second.Target.TemporaryModifiers.Active, m => Assert.Equal(4, m.ExpirationRound));
        Assert.Equal(13, first.Target.EffectiveDefense);
        Assert.Equal(13, second.Target.EffectiveDefense);
        first.Target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Equal(11, first.Target.EffectiveDefense);
        Assert.Equal(13, second.Target.EffectiveDefense);
    }

    [Fact]
    public void CanonicalBonuses_CombineWithBurnAndUseExistingPhysicalTriangleAndShieldRules()
    {
        var s = new HealingSetup();
        s.Heal();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        Assert.Equal(10, s.Target.EffectiveDefense); // 11 base + 2 blessing - 3 Burn.
        var attacker = CharacterFactory.Create("hooded_assassin", 2)!;
        var resolver = new CombatExchangeResolver(new(new(new FixedRolls())));
        var request = new CombatExchangeRequest(attacker, s.Target, AttackType.Physical, 5,
            WeaponCategory.Sword, WeaponCategory.Axe, 100, AttackType.Physical, 0, 100, 1, 1, 1, false, false);
        s.Target.SetShieldHP(6);
        var result = resolver.Resolve(request);
        Assert.Equal(WeaponTriangleOutcome.Advantage, result.OpeningStrike.TriangleOutcome);
        Assert.Equal(14, result.OpeningStrike.Damage); // 17 + 5 - 10 + 2 triangle.
        Assert.Equal(6, result.OpeningShieldAbsorbed);
        Assert.Equal(8, result.OpeningHealthLost);
        Assert.Equal(15, s.Target.CurrentHealth);
        Assert.Equal(11, s.Target.BaseStats.Defense);
        s.Target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Equal(8, s.Target.EffectiveDefense); // Burn is separate and still active.
        Assert.Equal(16, resolver.Resolve(request).OpeningStrike.Damage);
    }

    [Fact]
    public void CanonicalResistance_ReducesMagicUntilBothBonusesExpire()
    {
        var s = new HealingSetup();
        s.Heal();
        var mage = CharacterFactory.Create("time_weaver", 2)!;
        var resolver = new CombatExchangeResolver(new(new(new FixedRolls())));
        var request = new CombatExchangeRequest(mage, s.Target, AttackType.Magical, 4,
            WeaponCategory.MagicScroll, WeaponCategory.Axe, 100, AttackType.Physical, 0, 100, 1, 1, 1, false, false);
        Assert.Equal(11, resolver.Resolve(request).OpeningStrike.Damage);
        s.Target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Equal(13, resolver.Resolve(request).OpeningStrike.Damage);
        Assert.Equal(8, s.Target.BaseStats.Resistance);
    }

    [Fact]
    public void CanonicalBlessing_PersistsAfterCasterDeathAndClearsOnRecipientDefeat()
    {
        var s = new HealingSetup();
        s.Heal();
        s.Caster.TakeDamage(int.MaxValue);
        Assert.Equal(13, s.Target.EffectiveDefense);
        Assert.Equal(10, s.Target.EffectiveResistance);
        s.Target.TakeDamage(int.MaxValue);
        Assert.Empty(s.Target.TemporaryModifiers.Active);
        Assert.Equal(11, s.Target.EffectiveDefense);
        Assert.Equal(8, s.Target.EffectiveResistance);
    }

    [Theory]
    [InlineData("value")]
    [InlineData("stat")]
    [InlineData("recipient")]
    [InlineData("backward")]
    [InlineData("duplicate")]
    public void InvalidGroupedRefresh_DoesNotUpdateOnlyOneStat(string invalid)
    {
        var s = new HealingSetup();
        s.Heal();
        var original = s.Target.TemporaryModifiers.Active;
        var first = original[0];
        var second = original[1];
        var defense = new TemporaryStatModifier(first.Id, "later-source", s.Target.Id, first.Stat, first.Value, 3, 4);
        var resistance = new TemporaryStatModifier(invalid == "duplicate" ? first.Id : second.Id, "later-source",
            invalid == "recipient" ? "wrong-recipient" : s.Target.Id,
            invalid == "stat" ? TemporaryStat.Defense : second.Stat,
            invalid == "value" ? 3 : second.Value, invalid == "backward" ? 1 : 3,
            invalid == "backward" ? 2 : 4);
        Assert.ThrowsAny<ArgumentException>(() => s.Target.TemporaryModifiers.RefreshOrAddRange(new[] { defense, resistance }));
        Assert.Same(first, s.Target.TemporaryModifiers.Active[0]);
        Assert.Same(second, s.Target.TemporaryModifiers.Active[1]);
        Assert.Equal(3, first.ExpirationRound);
        Assert.Equal(3, second.ExpirationRound);
        Assert.Equal(13, s.Target.EffectiveDefense);
        Assert.Equal(10, s.Target.EffectiveResistance);
    }

    [Fact]
    public void ExplicitRefresh_PreservesUnrelatedModifierAndGenericAddStillRejectsDuplicates()
    {
        var target = CharacterFactory.Create("nigel", 1)!;
        var defense = Modifier(target, TemporaryStat.Defense, 5);
        var unrelatedResistance = Modifier(target, TemporaryStat.Resistance, 7);
        target.TemporaryModifiers.AddRange(new[] { defense, unrelatedResistance });
        var refresh = new TemporaryStatModifier(defense.Id, "latest-source", target.Id, defense.Stat, defense.Value, 3, 4);
        target.TemporaryModifiers.RefreshOrAddRange(new[] { refresh });
        Assert.Same(unrelatedResistance, target.TemporaryModifiers.Active[1]);
        Assert.Equal(3, unrelatedResistance.ExpirationRound);
        Assert.Equal(4, target.TemporaryModifiers.Active[0].ExpirationRound);
        Assert.Throws<ArgumentException>(() => target.TemporaryModifiers.AddRange(new[] { refresh }));
        Assert.Equal(16, target.EffectiveDefense);
        Assert.Equal(15, target.EffectiveResistance);
    }

    [Fact]
    public void UnrelatedConflictingModifier_IsPreservedAndDoesNotUndoAHeal()
    {
        var s = new HealingSetup();
        var unrelated = Modifier(s.Target, TemporaryStat.Resistance, 7);
        s.Target.TemporaryModifiers.AddRange(new[] { unrelated });
        var result = s.Heal();
        Assert.True(result.Succeeded);
        Assert.Equal(11, result.ActualHPRestored);
        Assert.Equal(GentleBlessingOutcome.Rejected, result.GentleBlessing!.Outcome);
        Assert.Equal(GentleBlessingRejectionReason.ModifierConflict, result.GentleBlessing.RejectionReason);
        Assert.Same(unrelated, Assert.Single(s.Target.TemporaryModifiers.Active));
        Assert.Equal(11, s.Target.EffectiveDefense); // No partial Defense application.
        Assert.Equal(15, s.Target.EffectiveResistance);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Single(s.Caster.Cooldowns.Cooldowns);
    }

    [Fact]
    public void ExpirationReapplication_UsesFreshIdsRatherThanReusingRemovedGenericIds()
    {
        var first = new HealingSetup();
        first.Heal();
        var oldIds = first.Target.TemporaryModifiers.Active.Select(m => m.Id).ToArray();
        first.Target.TemporaryModifiers.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        first.Caster.ResetActivation();
        first.Target.ResetHealingHistory();
        var later = new HealingSetup(first.Caster, 4, first.Target);
        var result = later.Heal();
        Assert.Equal(GentleBlessingOutcome.Applied, result.GentleBlessing!.Outcome);
        Assert.Equal(5, result.GentleBlessing.ExpirationRound);
        Assert.DoesNotContain(result.GentleBlessing.Modifiers, m => oldIds.Contains(m.Id));
        Assert.Equal(13, first.Target.EffectiveDefense);
    }

    [Theory]
    [InlineData(TemporaryStat.Defense)]
    [InlineData(TemporaryStat.Resistance)]
    public void CanonicalStatOverflow_RejectsBothBonusesButPreservesSuccessfulHealing(TemporaryStat stat)
    {
        var stats = new CharacterStats(52, 20, stat == TemporaryStat.Defense ? int.MaxValue : 11,
            3, stat == TemporaryStat.Resistance ? int.MaxValue : 8, 4, 13, 4);
        var target = CharacterFactory.Create(new CharacterDefinition("overflow-fixture", "Overflow fixture", "Test only", stats),
            "overflow-recipient", 1);
        target.TakeDamage(40);
        var s = new HealingSetup(target: target);
        var result = s.Heal();
        Assert.True(result.Succeeded);
        Assert.Equal(11, result.ActualHPRestored);
        Assert.Equal(GentleBlessingOutcome.Rejected, result.GentleBlessing!.Outcome);
        Assert.Equal(GentleBlessingRejectionReason.StatOverflow, result.GentleBlessing.RejectionReason);
        Assert.Empty(target.TemporaryModifiers.Active);
        Assert.Same(stats, target.BaseStats);
        Assert.Equal(23, target.CurrentHealth);
        Assert.Equal(1, target.HealsReceivedThisRound);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Single(s.Caster.Cooldowns.Cooldowns);
    }

    [Fact]
    public void DuplicateOldUse_CannotRefreshOrShortenLaterBlessing()
    {
        var first = new HealingSetup();
        var result = first.Heal();
        first.Target.TemporaryModifiers.ExpireAtRoundEnd(2, RoundPhase.Cleanup);
        var later = new HealingSetup(round: 3, target: first.Target);
        later.Heal();
        var snapshot = first.Target.TemporaryModifiers.Active;
        Assert.Equal(GentleBlessingOutcome.AlreadyProcessed,
            GentleBlessing.Process(first.Use, first.Target, result.Finalization).Outcome);
        Assert.All(first.Target.TemporaryModifiers.Active, m => Assert.Equal(4, m.ExpirationRound));
        Assert.Equal(snapshot, first.Target.TemporaryModifiers.Active);
    }

    private static TemporaryStatModifier Modifier(Character recipient, TemporaryStat stat, int value)
        => new($"test-{stat}", "explicit-test-source", recipient.Id, stat, value, 2, 3);

    private sealed class FixedRolls : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }

    private sealed class HealingSetup
    {
        public Character Caster { get; }
        public Character Target { get; }
        public Character Enemy { get; } = CharacterFactory.Create("hooded_assassin", 2)!;
        public ActivationManager Activations { get; }
        public AbilityUse Use { get; }
        public HealingLightAbilityExecutor Executor { get; } = new();

        public HealingSetup(Character? caster = null, int round = 2, Character? target = null)
        {
            Caster = caster ?? CharacterFactory.Create("gentle_guardian", 1)!;
            Target = target ?? CharacterFactory.Create("nigel", 1)!;
            Caster.SetTacticalPosition(new(5, 5));
            Target.SetTacticalPosition(new(6, 5));
            if (target is null) Target.TakeDamage(40);
            var lifecycle = new RoundLifecycleManager(round);
            while (lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) lifecycle.AdvancePhase();
            Activations = new(new[] { Caster, Target }, new[] { Enemy }, 1, lifecycle);
            Activations.StartActivation(Caster);
            Use = new(Caster, "healing_light", Activations);
        }

        public HealingLightUseResult Heal() => Executor.Execute(Use, Target, Rules);
    }
}
