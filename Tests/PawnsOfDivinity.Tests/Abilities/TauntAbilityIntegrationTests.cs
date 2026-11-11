using System;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Gameplay;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Core.Turns.Restrictions;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class TauntAbilityIntegrationTests
{
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy,
        true, false, false, false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShadowStrikeCanExecuteAgainstNigelWithOrWithoutAnActiveTaunt(bool taunted)
    {
        var s = new Setup();
        if (taunted) s.ApplyTaunt();
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.True(result.Eligibility.AttackIntent!.IsAllowed);
        Assert.True(result.Eligibility.AttackIntent.Targeting!.IsLegal);
        Assert.True(result.Eligibility.AttackIntent.TauntApproval!.IsAllowed);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.Equal(2, s.Actor.Cooldowns.Cooldowns[HoodedAssassin.ShadowStrikeId].ReadyRound);
        Assert.Equal(0, s.Actor.Cooldowns.Cooldowns[HoodedAssassin.ShadowStrikeId].ReuseInterval);
        Assert.Equal(3, s.Rolls.Calls); // Existing opening, counter, follow-up; no taunt roll.
        Assert.Equal(taunted, s.LegalityCalls > 0);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, s.Execute().Eligibility.RejectionReason);
        Assert.Equal(3, s.Rolls.Calls);
    }

    [Fact]
    public void LegalAttackAgainstAnotherTargetIsRejectedBeforeEffectsAndBookkeeping()
    {
        var s = new Setup();
        s.ApplyTaunt();
        s.Use = s.CreateUse(s.OtherTarget);
        var result = s.Executor.Execute(s.Use, s.Request(s.OtherTarget), s.Visibility(), 1, 1, s.TargetRules);
        Assert.Equal(AbilityRejectionReason.ActionRestricted, result.Eligibility.RejectionReason);
        Assert.True(result.Eligibility.AttackIntent!.Targeting!.IsLegal); // Normal legality alone is insufficient.
        Assert.Equal(TauntActionOutcome.MustAttackSource, result.Eligibility.AttackIntent.TauntApproval!.Outcome);
        s.AssertNoEffects();
    }

    [Fact]
    public void ExplicitNonAttackMainActionCannotSatisfyTauntAndNamesDoNotClassifyAttacks()
    {
        var s = new Setup();
        s.ApplyTaunt();
        var nonAttack = s.Intent(s.Nigel, HoodedAssassin.ShadowStrikeId, isAttack: false);
        var result = AbilityEligibility.Evaluate(s.Actor, HoodedAssassin.ShadowStrikeId, s.Manager, nonAttack);
        Assert.Equal(AbilityRejectionReason.ActionRestricted, result.RejectionReason);
        Assert.Equal(TauntActionOutcome.MustAttackSource, result.AttackIntent!.TauntApproval!.Outcome);
        // A confirmed executor also refuses a contradictory trusted classification.
        s.Use = new(s.Actor, HoodedAssassin.ShadowStrikeId, s.Manager, nonAttack);
        Assert.Equal(AbilityRejectionReason.UseContextChanged, s.Execute().Eligibility.RejectionReason);
        s.AssertNoEffects();
    }

    [Fact]
    public void MissingIntentAndMissingLegalAttackContextRemainExplicitRejections()
    {
        var s = new Setup();
        s.ApplyTaunt();
        s.Use = new(s.Actor, HoodedAssassin.ShadowStrikeId, s.Manager);
        Assert.Equal(AbilityRejectionReason.ActionRestrictionUnresolved, s.Execute().Eligibility.RejectionReason);
        var noProvider = new AttackIntent(s.Actor, s.Nigel, HoodedAssassin.ShadowStrikeId, s.Manager, true, Rules);
        s.Use = new(s.Actor, HoodedAssassin.ShadowStrikeId, s.Manager, noProvider);
        var result = s.Execute();
        Assert.Equal(AbilityRejectionReason.ActionRestrictionUnresolved, result.Eligibility.RejectionReason);
        Assert.Equal(TauntActionOutcome.AttackContextRequired, result.Eligibility.AttackIntent!.TauntApproval!.Outcome);
        s.AssertNoEffects();
    }

    [Theory]
    [InlineData("range", AbilityTargetingRejectionReason.OutsideMaximumRange)]
    [InlineData("missingSource", AbilityTargetingRejectionReason.MissingRequiredTargetInformation)]
    [InlineData("mapMissing", AbilityTargetingRejectionReason.MissingRequiredTargetInformation)]
    [InlineData("mapBlocked", AbilityTargetingRejectionReason.MapTargetIllegal)]
    [InlineData("invalidPosition", AbilityTargetingRejectionReason.MapTargetIllegal)]
    [InlineData("los", AbilityTargetingRejectionReason.UnresolvedLineOfSight)]
    [InlineData("defeated", AbilityTargetingRejectionReason.TargetDefeated)]
    [InlineData("ally", AbilityTargetingRejectionReason.WrongTeam)]
    public void TauntDoesNotOverrideNormalTargeting(string invalid, AbilityTargetingRejectionReason expected)
    {
        var s = new Setup(placeActor: invalid != "missingSource");
        s.ApplyTaunt();
        if (invalid == "range") s.Nigel.SetTacticalPosition(new(8, 3));
        if (invalid == "defeated") s.Nigel.TakeDamage(int.MaxValue);
        if (invalid is "mapMissing" or "mapBlocked" or "invalidPosition")
        {
            s.TargetRules = new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, true, false, false);
            s.MapLegal = invalid == "mapMissing" ? null : false;
        }
        if (invalid == "invalidPosition")
        {
            s.Actor.SetTacticalPosition(new(13, 3));
            s.Nigel.SetTacticalPosition(new(14, 3)); // Caller map legality rejects this outside the 14x14 board.
        }
        if (invalid == "los") s.TargetRules = new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, false, true, false);
        var target = invalid == "ally" ? s.Ally : s.Nigel;
        s.Use = s.CreateUse(target);
        int hp = s.Nigel.CurrentHealth;
        var result = s.Executor.Execute(s.Use, s.Request(target), s.Visibility(), 1, 1, s.TargetRules);
        Assert.Equal(AbilityRejectionReason.InvalidTarget, result.Eligibility.RejectionReason);
        Assert.Equal(expected, result.Eligibility.AttackIntent!.Targeting!.RejectionReason);
        if (invalid == "defeated")
            Assert.True(result.Eligibility.AttackIntent.TauntApproval!.ReleasesTaunt);
        Assert.Equal(hp, s.Nigel.CurrentHealth);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silenceUnknownTag", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("wrongPhase", AbilityRejectionReason.WrongPhase)]
    public void OrdinaryEligibilityStillRunsBeforeIntentApproval(string invalid, AbilityRejectionReason expected)
    {
        var s = new Setup();
        s.ApplyTaunt();
        if (invalid == "cooldown") s.Actor.Cooldowns.RecordUse(HoodedAssassin.ShadowStrikeId, 2, 3);
        if (invalid == "silenceUnknownTag") s.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        if (invalid == "inactive") s.Manager.EndActivationForTaunt(s.Actor, new(s.Nigel.Id, true, true, false));
        if (invalid == "wrongPhase") s.Lifecycle.AdvancePhase();
        var result = s.Execute();
        Assert.Equal(expected, result.Eligibility.RejectionReason);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Equal(52, s.Nigel.CurrentHealth);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Equal(invalid == "cooldown" ? 1 : 0, s.Actor.Cooldowns.Cooldowns.Count);
    }

    [Fact]
    public void ConfirmedSpellTagStillUsesExistingSilenceRule()
    {
        var s = new Setup();
        s.ApplyTaunt();
        // Explicit metadata fixture exercises the shared restriction without finalizing any roster spell tag.
        var definition = new CharacterDefinition("fixture", "Spell fixture", "Test only", s.Actor.BaseStats,
            new[] { new AbilityDefinition("spell_fixture", "Spell fixture", "Test only", AbilityClassification.Active,
                true, AbilityActionType.MainAction, new(AbilityRangeKind.ManhattanDistance, 1, 1), 0) });
        var spellCaster = CharacterFactory.Create(definition, "spell_caster", 1);
        spellCaster.SetTacticalPosition(new(4, 3));
        var manager = new ActivationManager(new[] { spellCaster }, new[] { s.Nigel }, 1);
        manager.StartActivation(spellCaster);
        manager.ActionRestrictions.Apply(new("spell_taunt", "roar", s.Nigel.Id, spellCaster.Id, 1, 1));
        spellCaster.Statuses.ApplyTimedStatus(StatusType.Silence, 1);
        var intent = new AttackIntent(spellCaster, s.Nigel, "spell_fixture", manager, true, Rules,
            () => new(s.Nigel.Id, true, true, true));
        Assert.Equal(AbilityRejectionReason.BlockedBySilence, AbilityEligibility.Evaluate(spellCaster, "spell_fixture", manager, intent).RejectionReason);
        Assert.Empty(spellCaster.Cooldowns.Cooldowns);
        Assert.False(manager.MainActionUsed);
    }

    [Fact]
    public void PreEffectApprovalSurvivesNigelsDefeatAndCannotFinalizeAgain()
    {
        var s = new Setup();
        s.ApplyTaunt();
        s.Nigel.TakeDamage(44);
        Assert.True(s.Preview().CanExecute);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.True(s.Nigel.IsDefeated);
        Assert.Equal(1, s.Rolls.Calls);
        Assert.Equal(8, result.Strike!.ActualHPDamage);
        Assert.True(s.Manager.MainActionUsed);
        Assert.True(s.Actor.HasActedThisRound);
        Assert.Equal(2, result.Finalization!.ReadyRound);
        var deadline = s.Actor.Cooldowns.Cooldowns[HoodedAssassin.ShadowStrikeId];
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, s.Execute().Eligibility.RejectionReason);
        Assert.Same(deadline, s.Actor.Cooldowns.Cooldowns[HoodedAssassin.ShadowStrikeId]);
        Assert.Equal(1, s.Rolls.Calls);
    }

    [Fact]
    public void NoLegalRequiredAttackReportsFallbackWithoutAutomaticallyEndingOrAttacking()
    {
        var s = new Setup();
        s.ApplyTaunt();
        s.RequiredAttackLegal = false;
        var result = s.Execute();
        Assert.Equal(AbilityRejectionReason.ActionRestricted, result.Eligibility.RejectionReason);
        Assert.True(result.Eligibility.AttackIntent!.TauntApproval!.RequiresEndActivation);
        Assert.Same(s.Actor, s.Manager.ActiveCharacter);
        s.AssertNoEffects();
        var ended = s.Manager.EndActivationForTaunt(s.Actor, s.Availability());
        Assert.True(ended.ActivationEnded);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.False(s.Manager.MainActionUsed);
        Assert.False(s.Manager.UtilityActionUsed);
        Assert.False(s.Manager.MovementUsed);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData("movement", TauntActionOutcome.MovementDecisionRequired)]
    [InlineData("legality", TauntActionOutcome.AttackLegalityUnresolved)]
    [InlineData("source", TauntActionOutcome.SourceApplicabilityUnresolved)]
    public void UnresolvedInputsRemainVisibleAndDoNotConsumeResources(string missing, TauntActionOutcome expected)
    {
        var s = new Setup();
        s.ApplyTaunt();
        if (missing == "movement") s.MovementCompleted = null;
        if (missing == "legality") s.RequiredAttackLegal = null;
        if (missing == "source") s.SourceApplicable = null;
        var result = s.Execute();
        Assert.Equal(AbilityRejectionReason.ActionRestrictionUnresolved, result.Eligibility.RejectionReason);
        Assert.Equal(expected, result.Eligibility.AttackIntent!.TauntApproval!.Outcome);
        s.AssertNoEffects();
    }

    [Fact]
    public void PreviewAndStaleTargetingAreRevalidatedThroughExistingRangeChecks()
    {
        var s = new Setup();
        s.ApplyTaunt();
        var preview = s.Preview();
        Assert.True(preview.CanExecute);
        Assert.Equal(new GridPosition(5, 3), preview.Eligibility.AttackIntent!.Targeting!.TargetPosition);
        s.AssertNoEffects();
        s.Nigel.SetTacticalPosition(new(7, 3));
        var result = s.Execute();
        Assert.Equal(AbilityRejectionReason.InvalidTarget, result.Eligibility.RejectionReason);
        Assert.Equal(AbilityTargetingRejectionReason.OutsideMaximumRange, result.Eligibility.AttackIntent!.Targeting!.RejectionReason);
        Assert.Equal(new GridPosition(5, 3), preview.Eligibility.AttackIntent.Targeting.TargetPosition);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void LegalityProvidersAreCalledAgainAtExecutionInsteadOfTrustingPreview()
    {
        var s = new Setup();
        s.ApplyTaunt();
        Assert.True(s.Preview().CanExecute);
        int calls = s.LegalityCalls;
        s.RequiredAttackLegal = false;
        var result = s.Execute();
        Assert.True(s.LegalityCalls > calls);
        Assert.True(result.Eligibility.AttackIntent!.TauntApproval!.RequiresEndActivation);
        s.AssertNoEffects();
    }

    [Fact]
    public void ExecutorCannotAttackADifferentTargetThanTheApprovedIntent()
    {
        var s = new Setup();
        s.ApplyTaunt();
        Assert.True(s.Preview().CanExecute);
        var result = s.Executor.Execute(s.Use, s.Request(s.OtherTarget), s.Visibility(), 1, 1, Rules);
        Assert.Equal(AbilityRejectionReason.UseContextChanged, result.Eligibility.RejectionReason);
        s.AssertNoEffects();
    }

    [Fact]
    public void MissingTargetPositionIsRejectedBeforeVisibilityOrCombatResolution()
    {
        var s = new Setup(placeNigel: false);
        s.ApplyTaunt();
        var result = s.Executor.Execute(s.Use, s.Request(s.Nigel), null, 1, 1, Rules);
        Assert.Equal(AbilityRejectionReason.InvalidTarget, result.Eligibility.RejectionReason);
        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation, result.Eligibility.AttackIntent!.Targeting!.RejectionReason);
        s.AssertNoEffects();
    }

    [Fact]
    public void CurrentMapLegalityIsRecheckedAtExecution()
    {
        var s = new Setup();
        s.ApplyTaunt();
        s.TargetRules = new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, true, false, false);
        s.Use = s.CreateUse(s.Nigel);
        Assert.True(s.Preview().CanExecute);
        s.MapLegal = false;
        var result = s.Execute();
        Assert.Equal(AbilityRejectionReason.InvalidTarget, result.Eligibility.RejectionReason);
        Assert.Equal(AbilityTargetingRejectionReason.MapTargetIllegal, result.Eligibility.AttackIntent!.Targeting!.RejectionReason);
        s.AssertNoEffects();
    }

    [Fact]
    public void BrutalKnockbackUsesPreEffectApprovalEvenAfterPushingTheRequiredTargetOutOfRange()
    {
        var actor = CharacterFactory.Create("nigel", 1)!;
        var source = CharacterFactory.Create("nigel", 2)!;
        actor.SetTacticalPosition(new(5, 5));
        source.SetTacticalPosition(new(6, 5));
        var lifecycle = new RoundLifecycleManager(2);
        while (lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) lifecycle.AdvancePhase();
        var manager = new ActivationManager(new[] { actor }, new[] { source }, 1, lifecycle);
        manager.StartActivation(actor);
        manager.ActionRestrictions.Apply(new("restriction", "roar", source.Id, actor.Id, 2, 2));
        var intent = new AttackIntent(actor, source, Nigel.BrutalKnockbackId, manager, true, Rules,
            () => new(source.Id, true, true, true));
        var use = new AbilityUse(actor, Nigel.BrutalKnockbackId, manager, intent);
        var rolls = new Rolls();
        var executor = new BrutalKnockbackAbilityExecutor(new(new(new(rolls)), new ForcedMovementService()));
        var axe = new AttackConfiguration(AttackType.Physical, WeaponCategory.Axe, 5, 100, 1, 1);
        var result = executor.Execute(use, source, axe, WeaponCategory.Axe, Rules, 2, new FlatMap());
        Assert.True(result.Succeeded);
        Assert.Equal(new GridPosition(8, 5), source.TacticalPosition);
        Assert.Equal(4, result.Finalization!.ReadyRound);
        Assert.True(manager.MainActionUsed);
        Assert.Null(manager.ActiveCharacter);
        Assert.Equal(1, rolls.Calls);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized,
            executor.Execute(use, source, axe, WeaponCategory.Axe, Rules, 2, new FlatMap()).Eligibility.RejectionReason);
        Assert.Equal(1, rolls.Calls);
    }

    [Fact]
    public void NonAttackExecutorCannotBorrowATrustedAttackIntentToBypassTaunt()
    {
        var s = new Setup();
        s.ApplyTaunt();
        var intent = s.Intent(s.Nigel, HoodedAssassin.SmokeBombId, isAttack: true);
        var use = new AbilityUse(s.Actor, HoodedAssassin.SmokeBombId, s.Manager, intent);
        var result = new SmokeBombAbilityExecutor(s.Smoke).Execute(use, "cloud", _ => true);
        Assert.Equal(AbilityRejectionReason.ActionRestrictionUnresolved, result.Eligibility.RejectionReason);
        Assert.Empty(s.Smoke.ActiveAreas);
        s.AssertNoEffects();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryPlayerAttackUsesTheSamePolicyAndExistingCombat(bool taunted)
    {
        var s = new Setup();
        if (taunted) s.ApplyTaunt();
        var executor = new AttackActionExecutor(new(new(new HitRoller(s.Rolls))));
        var intent = s.Intent(s.Nigel, "ordinary_attack", true);
        var attack = new AttackConfiguration(AttackType.Physical, WeaponCategory.Sword, 5, 100, 1, 1);
        Assert.True(executor.Preview(intent, s.Request(s.Nigel), attack).CanExecute);
        Assert.Equal(0, s.Rolls.Calls);
        var result = executor.Execute(intent, s.Request(s.Nigel), attack);
        Assert.True(result.Completed);
        Assert.Equal(13, result.Combat!.OpeningStrike.Damage);
        Assert.True(result.Combat.CounterattackOccurred);
        Assert.Equal(2, s.Rolls.Calls);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns); // No ability was used.
    }

    [Fact]
    public void OrdinaryAttackRejectsOtherTargetBeforeAnyCombat()
    {
        var s = new Setup();
        s.ApplyTaunt();
        var intent = s.Intent(s.OtherTarget, "ordinary_attack", true);
        var executor = new AttackActionExecutor(new(new(new HitRoller(s.Rolls))));
        var result = executor.Execute(intent, s.Request(s.OtherTarget), new(AttackType.Physical, WeaponCategory.Sword, 5, 100, 1, 1));
        Assert.False(result.Completed);
        Assert.Equal(TauntActionOutcome.MustAttackSource, result.Validation.TauntApproval!.Outcome);
        s.AssertNoEffects();
    }

    [Fact]
    public void OrdinaryAttackLethalApprovalAlsoSurvivesSourceDeath()
    {
        var s = new Setup();
        s.ApplyTaunt();
        s.Nigel.TakeDamage(44);
        var intent = s.Intent(s.Nigel, "ordinary_attack", true);
        var executor = new AttackActionExecutor(new(new(new HitRoller(s.Rolls))));
        var result = executor.Execute(intent, s.Request(s.Nigel), new(AttackType.Physical, WeaponCategory.Sword, 5, 100, 1, 1));
        Assert.True(result.Completed);
        Assert.True(s.Nigel.IsDefeated);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Equal(1, s.Rolls.Calls);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void OrdinaryAttackStillUsesExistingDisarmAndExplicitUnarmedRange()
    {
        var s = new Setup();
        s.ApplyTaunt();
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        var intent = s.Intent(s.Nigel, "ordinary_attack", true);
        var request = s.Request(s.Nigel) with { AttackerUnarmedAttack = AttackConfiguration.CreateUnarmed(0, 100) };
        var executor = new AttackActionExecutor(new(new(new HitRoller(s.Rolls))));
        var result = executor.Execute(intent, request, new(AttackType.Physical, WeaponCategory.Sword, 5, 100, 1, 1));
        Assert.True(result.Completed);
        Assert.Equal(6, result.Combat!.OpeningStrike.Damage);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.Combat.OpeningStrike.TriangleOutcome);
        Assert.True(s.Actor.Statuses.HasStatus(StatusType.Disarm));
        Assert.Equal(2, s.Rolls.Calls);
    }

    [Fact]
    public void UnrelatedCharactersAndRestrictionsRemainIndependentAndSnapshotsAreImmutable()
    {
        var s = new Setup();
        var other = new Setup();
        s.ApplyTaunt();
        s.RequiredAttackLegal = false;
        s.Actor.SetShieldHP(6);
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var stats = s.Actor.BaseStats;
        var position = s.Actor.TacticalPosition;
        var snapshot = s.Preview();
        Assert.Equal(AbilityRejectionReason.ActionRestricted, snapshot.Eligibility.RejectionReason);
        Assert.True(other.Preview().CanExecute);
        Assert.Empty(other.Manager.ActionRestrictions.Snapshots);
        Assert.Same(stats, s.Actor.BaseStats);
        Assert.Equal(position, s.Actor.TacticalPosition);
        Assert.Equal(38, s.Actor.CurrentHealth);
        Assert.Equal(6, s.Actor.ShieldHP);
        Assert.Equal(2, s.Actor.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Null(s.Actor.EquippedScroll);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        foreach (var type in new[] { typeof(AttackIntent), typeof(AttackIntentResult), typeof(AttackActionResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void IntentsCannotBeAttachedToAnotherCharacterAbilityOrActivation()
    {
        var s = new Setup();
        var intent = s.Intent(s.Nigel, HoodedAssassin.ShadowStrikeId, true);
        Assert.Throws<ArgumentException>(() => new AbilityUse(s.Ally, HoodedAssassin.ShadowStrikeId, s.Manager, intent));
        Assert.Throws<ArgumentException>(() => new AbilityUse(s.Actor, HoodedAssassin.SmokeBombId, s.Manager, intent));
        var other = new Setup();
        Assert.Throws<ArgumentException>(() => new AbilityUse(s.Actor, HoodedAssassin.ShadowStrikeId, other.Manager, intent));
        s.AssertNoEffects();
    }

    [Fact]
    public void CanonicalRoarNowAppliesTheFinalizedRestriction()
    {
        var nigel = CharacterFactory.Create("nigel", 1)!;
        var enemy = CharacterFactory.Create("hooded_assassin", 2)!;
        nigel.SetTacticalPosition(new(5, 5));
        enemy.SetTacticalPosition(new(6, 5));
        var manager = new ActivationManager(new[] { nigel }, new[] { enemy }, 1);
        manager.StartActivation(nigel);
        var result = new ProvokingRoarAbilityExecutor().Execute(new(nigel, Nigel.ProvokingRoarId, manager), Rules);
        Assert.Equal(ProvokingRoarUseRejectionReason.None, result.RejectionReason);
        Assert.True(result.Succeeded);
        Assert.Single(manager.ActionRestrictions.Snapshots);
        Assert.True(manager.MainActionUsed);
        Assert.Single(nigel.Cooldowns.Cooldowns);
    }

    private sealed class Setup
    {
        public Character Actor { get; } = CharacterFactory.Create(HoodedAssassin.Definition, "actor", 1);
        public Character Ally { get; } = CharacterFactory.Create(TimeWeaver.Definition, "ally", 1);
        public Character Nigel { get; } = CharacterFactory.Create(PawnsOfDivinity.Core.Characters.Definitions.Nigel.Definition, "source", 2);
        public Character OtherTarget { get; } = CharacterFactory.Create(TimeWeaver.Definition, "other", 2);
        public RoundLifecycleManager Lifecycle { get; } = new(2);
        public ActivationManager Manager { get; }
        public AbilityUse Use { get; set; }
        public Rolls Rolls { get; } = new();
        public SmokePlacementService Smoke { get; } = new();
        public ShadowStrikeAbilityExecutor Executor { get; }
        public AbilityTargetingConstraints TargetRules { get; set; } = Rules;
        public bool? MapLegal { get; set; } = true;
        public bool? RequiredAttackLegal { get; set; } = true;
        public bool? SourceApplicable { get; set; } = true;
        public bool? MovementCompleted { get; set; } = true;
        public int LegalityCalls { get; private set; }

        public Setup(bool placeActor = true, bool placeNigel = true)
        {
            if (placeActor) Actor.SetTacticalPosition(new(4, 3));
            Ally.SetTacticalPosition(new(3, 3));
            if (placeNigel) Nigel.SetTacticalPosition(new(5, 3));
            OtherTarget.SetTacticalPosition(new(4, 4));
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Manager = new(new[] { Actor, Ally }, new[] { Nigel, OtherTarget }, 1, Lifecycle);
            Manager.StartActivation(Actor);
            Use = CreateUse(Nigel);
            Executor = new(new(new(new(new HitRoller(Rolls))), Smoke));
        }
        public TauntAttackAvailability Availability() => new(Nigel.Id, MovementCompleted, SourceApplicable, RequiredAttackLegal);
        public AttackIntent Intent(Character target, string id, bool isAttack) => new(Actor, target, id, Manager, isAttack, TargetRules,
            () => { LegalityCalls++; return Availability(); }, mapTargetLegal: () => MapLegal);
        public AbilityUse CreateUse(Character target) => new(Actor, HoodedAssassin.ShadowStrikeId, Manager, Intent(target, HoodedAssassin.ShadowStrikeId, true));
        public void ApplyTaunt() => Assert.True(Manager.ActionRestrictions.Apply(new("taunt", "roar", Nigel.Id, Actor.Id, 2, 2)).Applied);
        public CombatExchangeRequest Request(Character target) => new(Actor, target, AttackType.Physical, 5,
            WeaponCategory.Sword, ReferenceEquals(target, Nigel) ? WeaponCategory.Axe : WeaponCategory.MagicScroll, 100,
            ReferenceEquals(target, Nigel) ? AttackType.Physical : AttackType.Magical, 0, 100, 1, 1, 1, true, false);
        public SmokeVisibilityResult Visibility() => SmokeVisibility.Evaluate(Actor, Actor.TacticalPosition ?? new(4, 3), Smoke,
            new[] { (Nigel, Nigel.TacticalPosition!.Value), (OtherTarget, OtherTarget.TacticalPosition!.Value) });
        public ShadowStrikeUseResult Preview() => Executor.Preview(Use, Request(Nigel), Visibility(), 1, 1, TargetRules, mapTargetLegal: MapLegal);
        public ShadowStrikeUseResult Execute() => Executor.Execute(Use, Request(Nigel), Visibility(), 1, 1, TargetRules, mapTargetLegal: MapLegal);
        public void AssertNoEffects()
        {
            Assert.Equal(0, Rolls.Calls);
            Assert.Equal(38, Actor.CurrentHealth);
            Assert.Equal(52, Nigel.CurrentHealth);
            Assert.Equal(38, OtherTarget.CurrentHealth);
            Assert.False(Manager.MainActionUsed);
            Assert.False(Manager.UtilityActionUsed);
            Assert.False(Manager.MovementUsed);
            Assert.Empty(Actor.Cooldowns.Cooldowns);
            Assert.False(Actor.SmokeReveal.RevealedByAttack);
        }
    }

    private sealed class Rolls : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }

    private sealed class FlatMap : IForcedMovementMap
    {
        public bool? IsValidPosition(GridPosition p) => p.X >= 0 && p.X < 14 && p.Y >= 0 && p.Y < 14;
        public bool? IsOccupied(GridPosition p) => false;
        public bool? CanDisplace(Character target, GridPosition from, GridPosition to) => true;
    }
}
