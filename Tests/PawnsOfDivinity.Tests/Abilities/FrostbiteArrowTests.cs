using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Core.Turns.Restrictions;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class FrostbiteArrowTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(39, true)]
    [InlineData(40, false)]
    [InlineData(99, false)]
    public void ProcUsesExactFortyPercentWithIndependentStatusRandomness(int roll, bool success)
    {
        var s = new Setup();
        s.Status.Values.Enqueue(roll);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(12, result.Combat!.OpeningStrike.Damage);
        Assert.True(result.Proc!.RollAttempted);
        Assert.Equal(success, result.Proc.ProcSucceeded);
        Assert.Equal(success, s.Target.Statuses.HasStatus(StatusType.Frostbite));
        Assert.Equal(1, s.Accuracy.Calls);
        Assert.Equal(1, s.Status.Calls);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.Equal(2, result.Finalization!.ReadyRound);
    }

    [Fact]
    public void MissConsumesTheMainActionButNeverRollsOrAppliesFrostbite()
    {
        var s = new Setup();
        s.Accuracy.Values.Enqueue(99);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.False(result.Combat!.OpeningStrike.Hit);
        Assert.Equal(0, result.Combat.OpeningStrike.Damage);
        Assert.False(result.Proc!.RollAttempted);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Equal(0, s.Status.Calls);
        Assert.True(s.Manager.MainActionUsed);
    }

    [Fact]
    public void MissingProvisionalDurationRejectsCanonicalExecutionWithoutCosts()
    {
        var s = new Setup();
        var rules = new FrostbiteArrowContext(null, false, false);
        var result = s.Executor.Execute(s.Use, s.Request(), "test_bow", s.Bow, rules);
        Assert.Equal(FrostbiteArrowReason.DurationUnresolved, result.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Equal(0, s.Accuracy.Calls);
        Assert.Equal(0, s.Status.Calls);
        Assert.True(s.Manager.CanUseMainAction);
        Assert.Empty(s.Attacker.Cooldowns.Cooldowns);
    }

    [Fact]
    public void RegistrationRemainsAnEquippedRangeMainActionWithReuseZero()
    {
        var ability = BeothukSpiritGuardian.Definition.Abilities.Single(a => a.Id == BeothukSpiritGuardian.FrostbiteArrowId);
        Assert.Equal(AbilityClassification.Active, ability.Classification);
        Assert.Equal(AbilityActionType.MainAction, ability.RequiredAction);
        Assert.Equal(0, ability.ReuseInterval);
        Assert.Equal(AbilityRangeKind.EquippedWeapon, ability.Range!.Kind);
        Assert.Null(ability.IsSpellTagged);
        foreach (var definition in CharacterRegistry.GetAllCharacters().Where(d => d.Id != "beothuk_spirit_guardian"))
            Assert.DoesNotContain(definition.Abilities, a => a.Id == ability.Id);
    }

    [Fact]
    public void AllOneHundredDeterministicRollValuesProduceExactlyFortySuccesses()
    {
        int successes = 0;
        for (int roll = 0; roll < 100; roll++)
        {
            var s = new Setup();
            s.Status.Values.Enqueue(roll);
            var proc = s.Effect.Resolve(s.Attacker, s.Target, Strike(s), 0, 2, s.Rules);
            if (proc.ProcSucceeded == true) successes++;
            Assert.Equal(1, s.Status.Calls);
            Assert.False(s.Target.Statuses.HasStatus(StatusType.Frostbite));
        }
        Assert.Equal(40, successes);
    }

    [Theory]
    [InlineData(74, 39, true, true)]
    [InlineData(74, 40, true, false)]
    [InlineData(75, 0, false, false)]
    public void AccuracyAndProcThresholdsUseSeparateProviders(int accuracy, int status, bool hit, bool frostbite)
    {
        var s = new Setup();
        s.Bow = new(AttackType.Physical, WeaponCategory.Bow, 4, 75, 2, 3);
        s.Accuracy.Values.Enqueue(accuracy);
        s.Status.Values.Enqueue(status);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(hit, result.Combat!.OpeningStrike.Hit);
        Assert.Equal(frostbite, s.Target.Statuses.HasStatus(StatusType.Frostbite));
        Assert.Equal(1, s.Accuracy.Calls);
        Assert.Equal(hit ? 1 : 0, s.Status.Calls);
    }

    [Fact]
    public void PureEvaluationDoesNotRollApplyOrChangeHealth()
    {
        var s = new Setup();
        var evaluation = s.Effect.Evaluate(s.Attacker, s.Target, Strike(s), 0, 2, s.Rules);
        Assert.True(evaluation.HitEligible);
        Assert.True(evaluation.ApplicationEligible);
        Assert.False(evaluation.RollAttempted);
        Assert.Null(evaluation.ProcSucceeded);
        Assert.False(evaluation.StatusApplied);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Empty(s.Target.Statuses.TimedStatuses);
        Assert.Equal(0, s.Status.Calls);
        Assert.Equal(0, s.Accuracy.Calls);
        Assert.True(s.Manager.CanUseMainAction);
    }

    [Fact]
    public void ProcFoundationCanRollWithoutInventingAnUnapprovedApplicationDuration()
    {
        var s = new Setup();
        var rules = new FrostbiteArrowContext(null, false, false);
        var proc = s.Effect.Resolve(s.Attacker, s.Target, Strike(s), 0, 2, rules);
        Assert.True(proc.HitEligible);
        Assert.True(proc.RollAttempted);
        Assert.True(proc.ProcSucceeded);
        Assert.False(proc.ApplicationEligible);
        Assert.Null(proc.DurationRounds);
        var applied = s.Effect.Apply(proc, 2);
        Assert.Equal(FrostbiteArrowReason.DurationUnresolved, applied.Reason);
        Assert.False(applied.StatusApplied);
        Assert.Empty(s.Target.Statuses.TimedStatuses);
        Assert.Equal(1, s.Status.Calls);
    }

    [Fact]
    public void ApplyingAProcUsesExistingRefreshCoexistenceExpirationAndCleanseRules()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 1);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.Target.Statuses.ApplyPoison();
        var originalMove = s.Target.BaseStats.Movement;
        var proc = s.Effect.Resolve(s.Attacker, s.Target, Strike(s), 0, 2, s.Rules);
        var applied = s.Effect.Apply(proc, 2);
        Assert.Equal(FrostbiteArrowStatusOutcome.Refreshed, applied.StatusOutcome);
        Assert.Equal(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, applied.DurationPolicy);
        Assert.Equal(2, applied.DurationRounds);
        Assert.Equal(2, applied.RoundApplied);
        Assert.Equal(3, s.Target.EffectiveMovement);
        Assert.Equal(originalMove, s.Target.BaseStats.Movement);
        Assert.Equal(2, s.Target.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.True(s.Target.Statuses.IsPoisoned);
        Assert.Equal(2, s.Target.Statuses.TimedStatuses[StatusType.Burn]);
        s.Target.Statuses.AdvanceTimedStatuses(); // R3.
        Assert.Equal(1, s.Target.Statuses.TimedStatuses[StatusType.Frostbite]);
        s.Target.Statuses.AdvanceTimedStatuses(); // R4.
        Assert.Equal(5, s.Target.EffectiveMovement);
        var second = s.Effect.Resolve(s.Attacker, s.Target, Strike(s), 0, 4, s.Rules);
        Assert.True(s.Effect.Apply(second, 4).StatusApplied);
        CleanseProcessor.BasicCleanse(s.Target, StatusType.Frostbite);
        Assert.False(s.Target.Statuses.HasStatus(StatusType.Frostbite));
        Assert.Equal(5, s.Target.EffectiveMovement);
        Assert.True(s.Target.Statuses.IsPoisoned);
    }

    [Fact]
    public void ReplayingAProcOrResolvingTheSameStrikeAgainCannotRefreshOrReroll()
    {
        var s = new Setup();
        var strike = Strike(s);
        var proc = s.Effect.Resolve(s.Attacker, s.Target, strike, 0, 2, s.Rules);
        var applied = s.Effect.Apply(proc, 2);
        s.Target.Statuses.AdvanceTimedStatuses();
        Assert.Same(proc, s.Effect.Resolve(s.Attacker, s.Target, strike, 0, 2, s.Rules));
        var rejected = s.Effect.Apply(proc, 2);
        Assert.Equal(FrostbiteArrowReason.AlreadyApplied, rejected.Reason);
        Assert.Equal(FrostbiteArrowReason.AlreadyApplied, s.Effect.Apply(applied, 2).Reason);
        Assert.Equal(FrostbiteArrowReason.AlreadyApplied, s.Effect.Apply(rejected, 2).Reason);
        Assert.Equal(1, s.Target.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(1, s.Status.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void ShieldAbsorptionLeavesProcEligibilityUnresolvedWithoutARoll(int absorbed)
    {
        var s = new Setup();
        var result = s.Effect.Resolve(s.Attacker, s.Target, Strike(s), absorbed, 2, s.Rules);
        Assert.Equal(FrostbiteArrowReason.ShieldEligibilityUnresolved, result.Reason);
        Assert.False(result.HitEligible);
        Assert.False(result.RollAttempted);
        Assert.Equal(0, s.Status.Calls);
        Assert.Empty(s.Target.Statuses.TimedStatuses);
    }

    [Fact]
    public void MissNoConfirmedHitAndDefeatedTargetsNeverReceiveAStatus()
    {
        var s = new Setup();
        var miss = new SingleStrikeResolver(new(new Rolls())).ResolvePhysicalStrike(s.Attacker.BaseStats, s.Target.BaseStats,
            4, WeaponCategory.Bow, WeaponCategory.MagicScroll, 0);
        Assert.Equal(FrostbiteArrowReason.Miss, s.Effect.Resolve(s.Attacker, s.Target, miss, 0, 2, s.Rules).Reason);
        Assert.Equal(FrostbiteArrowReason.NoConfirmedStrike, s.Effect.Resolve(s.Attacker, s.Target, null, 0, 2, s.Rules).Reason);
        s.Target.TakeDamage(int.MaxValue);
        Assert.Equal(FrostbiteArrowReason.TargetDefeated, s.Effect.Resolve(s.Attacker, s.Target, Strike(s), 0, 2, s.Rules).Reason);
        Assert.Equal(0, s.Status.Calls);
        Assert.Empty(s.Target.Statuses.TimedStatuses);
    }

    [Theory]
    [InlineData("duration", FrostbiteArrowReason.DurationUnresolved)]
    [InlineData("unknownFlying", FrostbiteArrowReason.FlyingContextRequired)]
    [InlineData("flying", FrostbiteArrowReason.FlyingEffectivenessUnresolved)]
    [InlineData("unknownTerrain", FrostbiteArrowReason.TerrainContextRequired)]
    [InlineData("terrain", FrostbiteArrowReason.TerrainInteractionUnresolved)]
    [InlineData("shield", FrostbiteArrowReason.ShieldEligibilityUnresolved)]
    [InlineData("lethal", FrostbiteArrowReason.LethalProcRulesUnresolved)]
    public void UnsupportedCanonicalRequestsAreRejectedBeforeEitherRngOrDamage(string condition, FrostbiteArrowReason reason)
    {
        var s = new Setup();
        var rules = condition switch
        {
            "duration" => new FrostbiteArrowContext(null, false, false),
            "unknownFlying" => new(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, null, false),
            "flying" => new(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, true, false),
            "unknownTerrain" => new(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, false, null),
            "terrain" => new(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, false, true),
            _ => s.Rules
        };
        if (condition == "shield") s.Target.SetShieldHP(6);
        if (condition == "lethal") s.Target.TakeDamage(37);
        int health = s.Target.CurrentHealth;
        var result = s.Executor.Execute(s.Use, s.Request(), "bow", s.Bow, rules);
        Assert.Equal(reason, result.RejectionReason);
        Assert.Equal(health, s.Target.CurrentHealth);
        Assert.Equal(condition == "shield" ? 6 : 0, s.Target.ShieldHP);
        AssertRejected(s);
    }

    [Fact]
    public void CounterAndFollowUpEligibilityArePreservedRatherThanSilentlySuppressed()
    {
        var counter = new Setup(distance: 2);
        var request = counter.Request() with { IncomingAttackPermitsCounters = false, IncomingAttackPermitsFollowUps = false };
        var result = counter.Executor.Execute(counter.Use, request, "bow", counter.Bow, counter.Rules);
        Assert.True(result.Targeting!.IsLegal);
        Assert.Equal(FrostbiteArrowReason.ExchangeTimingUnresolved, result.RejectionReason);
        AssertRejected(counter);
        var followUp = new Setup(targetDefinition: Nigel.Definition);
        var slowTargetRequest = followUp.Request() with { DefenderWeapon = WeaponCategory.Axe, CounterattackType = AttackType.Physical,
            CounterattackMinimumRange = 1, CounterattackMaximumRange = 1 };
        Assert.Equal(FrostbiteArrowReason.ExchangeTimingUnresolved,
            followUp.Executor.Execute(followUp.Use, slowTargetRequest, "bow", followUp.Bow, followUp.Rules).RejectionReason);
        AssertRejected(followUp);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void StandardBowRangeIsTwoToThreeWithNoInventedAdjacentAttack(int distance, bool valid)
    {
        var s = new Setup(distance: distance, targetDefinition: HoodedAssassin.Definition);
        var request = s.Request() with { DefenderWeapon = WeaponCategory.Sword, CounterattackType = AttackType.Physical,
            CounterattackPower = 5, CounterattackMinimumRange = 1, CounterattackMaximumRange = 1 };
        var result = s.Executor.Execute(s.Use, request, "bow", s.Bow, s.Rules);
        Assert.Equal(valid, result.Succeeded);
        if (valid)
        {
            Assert.Equal(12, result.Combat!.OpeningStrike.Damage);
            Assert.False(result.Combat.CounterattackOccurred);
            Assert.False(result.Combat.FollowUpOccurred); // Faster defender cannot reach with its sword.
            Assert.Equal(1, s.Accuracy.Calls);
        }
        else AssertRejected(s);
    }

    [Fact]
    public void ExplicitEquippedRangeIsUsedInsteadOfHardcodingStandardMaximumThree()
    {
        var s = new Setup(distance: 4);
        s.Bow = new(AttackType.Physical, WeaponCategory.Bow, 4, 90, 2, 4); // Caller-resolved test equipment, not canonical balance.
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(4, result.Targeting!.Distance);
        Assert.Equal(4, result.Bow!.MaximumRange);
    }

    [Theory]
    [InlineData("missing", FrostbiteArrowReason.BowRequired)]
    [InlineData("sword", FrostbiteArrowReason.PhysicalBowRequired)]
    [InlineData("magic", FrostbiteArrowReason.PhysicalBowRequired)]
    [InlineData("disarm", FrostbiteArrowReason.BowUnavailable)]
    [InlineData("adjacentConfig", FrostbiteArrowReason.InvalidBowRange)]
    public void EquipmentMustExplicitlyDescribeAnAvailablePhysicalBow(string condition, FrostbiteArrowReason reason)
    {
        var s = new Setup();
        AttackConfiguration? supplied = condition switch
        {
            "missing" => null,
            "sword" => new(AttackType.Physical, WeaponCategory.Sword, 4, 90, 2, 3),
            "magic" => new(AttackType.Magical, WeaponCategory.MagicScroll, 4, 90, 2, 3),
            "adjacentConfig" => new(AttackType.Physical, WeaponCategory.Bow, 4, 90, 1, 3),
            _ => s.Bow
        };
        if (condition == "disarm") s.Attacker.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        Assert.Equal(reason, s.Executor.Execute(s.Use, s.Request(), "bow", supplied, s.Rules).RejectionReason);
        AssertRejected(s);
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("phase", AbilityRejectionReason.WrongPhase)]
    public void SharedEligibilityStillGovernsTheAttack(string condition, AbilityRejectionReason reason)
    {
        var s = new Setup();
        switch (condition)
        {
            case "dead": s.Attacker.TakeDamage(int.MaxValue); break;
            case "inactive": s.Manager.CompleteMainAction(); break;
            case "silence": s.Attacker.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "cooldown": s.Attacker.Cooldowns.RecordUse(BeothukSpiritGuardian.FrostbiteArrowId, 1, 3); break;
            case "phase": s.Lifecycle.AdvancePhase(); break;
        }
        int cooldownCount = s.Attacker.Cooldowns.Cooldowns.Count;
        Assert.Equal(reason, s.Execute().Eligibility.RejectionReason);
        Assert.Equal(cooldownCount, s.Attacker.Cooldowns.Cooldowns.Count);
        Assert.Equal(0, s.Accuracy.Calls);
        Assert.Equal(0, s.Status.Calls);
        Assert.Equal(38, s.Target.CurrentHealth);
    }

    [Fact]
    public void IncorrectOwnerAllyTargetAndMissingPositionsAreRejectedWithoutEffects()
    {
        var wrong = new Setup(attackerDefinition: Nigel.Definition);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, wrong.Execute().Eligibility.RejectionReason);
        var missingSource = new Setup(placeAttacker: false);
        Assert.Equal(AbilityRejectionReason.InvalidTarget, missingSource.Execute().Eligibility.RejectionReason);
        var missingTarget = new Setup(placeTarget: false);
        var dummy = missingTarget.Request() with { Distance = 3 };
        Assert.Equal(AbilityRejectionReason.InvalidTarget,
            missingTarget.Executor.Execute(missingTarget.Use, dummy, "bow", missingTarget.Bow, missingTarget.Rules).Eligibility.RejectionReason);
        var s = new Setup();
        var allyIntent = new AttackIntent(s.Attacker, s.Attacker, BeothukSpiritGuardian.FrostbiteArrowId, s.Manager, true, TargetRules,
            equipmentRange: () => new(AbilityRangeKind.ManhattanDistance, 2, 3));
        var allyUse = new AbilityUse(s.Attacker, BeothukSpiritGuardian.FrostbiteArrowId, s.Manager, allyIntent);
        Assert.False(s.Executor.Execute(allyUse, s.Request() with { Defender = s.Attacker }, "bow", s.Bow, s.Rules).Succeeded);
        AssertRejected(s);
    }

    [Fact]
    public void PreviewIsImmutableNonMutatingAndDoesNotApplyAProc()
    {
        var s = new Setup();
        var preview = s.Executor.Preview(s.Use, s.Request(), "test_bow", s.Bow, s.Rules);
        Assert.True(preview.CanExecute);
        Assert.Equal("test_bow", preview.BowId);
        Assert.Equal(4, preview.Bow!.Power);
        Assert.Equal(3, preview.Targeting!.Distance);
        Assert.Null(preview.Combat);
        Assert.Null(preview.Proc);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Empty(s.Target.Statuses.TimedStatuses);
        AssertRejected(s);
        s.Bow = new(AttackType.Physical, WeaponCategory.Bow, 8, 90, 2, 3);
        Assert.Equal(4, preview.Bow.Power);
        foreach (var type in new[] { typeof(FrostbiteArrowContext), typeof(FrostbiteArrowProcResult), typeof(FrostbiteArrowUseResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void ExecuteRechecksStaleTargetsShieldsAndAttackContext()
    {
        var s = new Setup();
        Assert.True(s.Executor.Preview(s.Use, s.Request(), "bow", s.Bow, s.Rules).CanExecute);
        s.Target.SetTacticalPosition(new(6, 5));
        Assert.Equal(AbilityRejectionReason.InvalidTarget, s.Execute().Eligibility.RejectionReason);
        s.Target.SetTacticalPosition(new(8, 5));
        s.Target.SetShieldHP(6);
        Assert.Equal(FrostbiteArrowReason.ShieldEligibilityUnresolved, s.Execute().RejectionReason);
        AssertRejected(s);
        s.Target.SetShieldHP(0);
        Assert.Equal(FrostbiteArrowReason.ContextMismatch,
            s.Executor.Execute(s.Use, s.Request() with { OpeningPower = 99 }, "bow", s.Bow, s.Rules).RejectionReason);
        AssertRejected(s);
    }

    [Fact]
    public void CompletedAttackConsumesOnlyOneMainActionRecordsReuseZeroAndCannotReplay()
    {
        var s = new Setup();
        s.Manager.RecordMovement();
        s.Manager.RecordUtilityAction();
        var stats = s.Attacker.BaseStats;
        Assert.True(s.Execute().Succeeded);
        Assert.True(s.Manager.MovementUsed);
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.True(s.Manager.MainActionUsed);
        Assert.True(s.Attacker.HasActedThisRound);
        Assert.True(s.Attacker.Cooldowns.IsReady(BeothukSpiritGuardian.FrostbiteArrowId, 2));
        Assert.Equal(0, s.Attacker.Cooldowns.Cooldowns[BeothukSpiritGuardian.FrostbiteArrowId].ReuseInterval);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, s.Execute().Eligibility.RejectionReason);
        Assert.Equal(1, s.Accuracy.Calls);
        Assert.Equal(1, s.Status.Calls);
        Assert.Single(s.Attacker.Cooldowns.Cooldowns);
        Assert.Same(stats, s.Attacker.BaseStats);
    }

    [Fact]
    public void ExistingEffectiveDefenseAndSanctuaryAreUsedByTheSameBowPipeline()
    {
        var protector = CharacterFactory.Create(GraveboundColossus.Definition, "protector", 2);
        protector.SetTacticalPosition(new(6, 5));
        var s = new Setup(extra: new[] { protector });
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(11, result.Defense!.EffectiveDefense);
        Assert.Equal(9, result.Combat!.OpeningStrike.Damage);
        Assert.Equal(29, s.Target.CurrentHealth);
        Assert.Equal(64, protector.CurrentHealth);
        var burned = new Setup();
        burned.Target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        Assert.Equal(15, burned.Execute().Combat!.OpeningStrike.Damage);
        Assert.Equal(8, burned.Target.BaseStats.Defense);
        Assert.Equal(2, burned.Target.Statuses.TimedStatuses[StatusType.Burn]);
    }

    [Fact]
    public void UnresolvedSanctuaryDoesNotSpendAnything()
    {
        var a = CharacterFactory.Create(GraveboundColossus.Definition, "a", 2);
        var b = CharacterFactory.Create(GraveboundColossus.Definition, "b", 2);
        a.SetTacticalPosition(new(6, 5));
        b.SetTacticalPosition(new(6, 5)); // Evaluator-only overlap fixture; no movement rules are asserted.
        var s = new Setup(extra: new[] { a, b });
        var result = s.Execute();
        Assert.Equal(FrostbiteArrowReason.SanctuaryUnresolved, result.RejectionReason);
        Assert.Equal(SanctuaryDefenseReason.OverlapUnresolved, result.Defense!.Reason);
        AssertRejected(s);
    }

    [Fact]
    public void InvalidStatusRngCannotApplyAStatusOrReplayThePartiallyResolvedAttack()
    {
        var s = new Setup();
        s.Status.Values.Enqueue(100);
        Assert.Throws<InvalidOperationException>(() => s.Execute());
        Assert.Equal(26, s.Target.CurrentHealth); // Accuracy/damage already resolved; no rollback is promised.
        Assert.Empty(s.Target.Statuses.TimedStatuses);
        Assert.Equal(FrostbiteArrowReason.AlreadyStarted, s.Execute().RejectionReason);
        Assert.Equal(1, s.Accuracy.Calls);
        Assert.Equal(1, s.Status.Calls);
        Assert.Empty(s.Attacker.Cooldowns.Cooldowns);
    }

    [Fact]
    public void WrongProcOwnerRoundAndTargetIdentityCannotReuseAnApplicationToken()
    {
        var s = new Setup();
        var strike = Strike(s);
        var proc = s.Effect.Resolve(s.Attacker, s.Target, strike, 0, 2, s.Rules);
        var otherEffect = new FrostbiteArrowEffect(new(new Rolls()));
        Assert.Equal(FrostbiteArrowReason.InvalidProcContext, otherEffect.Apply(proc, 2).Reason);
        Assert.Equal(FrostbiteArrowReason.WrongRound, s.Effect.Apply(proc, 3).Reason);
        var otherTarget = CharacterFactory.Create(TimeWeaver.Definition, "other", 2);
        Assert.Equal(FrostbiteArrowReason.InvalidProcContext,
            s.Effect.Resolve(s.Attacker, otherTarget, strike, 0, 2, s.Rules).Reason);
        Assert.Empty(s.Target.Statuses.TimedStatuses);
        Assert.True(s.Effect.Apply(proc, 2).StatusApplied);
        Assert.Empty(otherTarget.Statuses.TimedStatuses);
        Assert.Equal(1, s.Status.Calls);
    }

    [Fact]
    public void NullAndMalformedInputsAreRejectedWithoutAnyEffects()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => new FrostbiteArrowEffect(null!));
        Assert.Throws<ArgumentNullException>(() => new FrostbiteArrowAbilityExecutor(null!, s.Effect));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(null!, s.Request(), "bow", s.Bow, s.Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, null!, "bow", s.Bow, s.Rules));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, s.Request(), "bow", s.Bow, null!));
        foreach (var id in new[] { null, "", " " })
            Assert.ThrowsAny<ArgumentException>(() => s.Executor.Execute(s.Use, s.Request(), id!, s.Bow, s.Rules));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrostbiteArrowContext((FrostbiteArrowDurationPolicy)999, false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Effect.Evaluate(s.Attacker, s.Target, Strike(s), -1, 2, s.Rules));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Effect.Evaluate(s.Attacker, s.Target, Strike(s), 0, 0, s.Rules));
        Assert.Throws<ArgumentException>(() => s.Effect.Evaluate(s.Attacker, s.Target, Strike(s), 13, 2, s.Rules));
        AssertRejected(s);
    }

    [Fact]
    public void TauntApprovesTheRequiredNigelButDoesNotOverrideUnresolvedExchangeTiming()
    {
        var s = new Setup(distance: 2, targetDefinition: Nigel.Definition);
        s.ArrangeRoarFromTarget();
        var preview = s.Executor.Preview(s.Use, s.Request(), "bow", s.Bow, s.Rules);
        Assert.True(preview.Eligibility.IsAllowed);
        Assert.True(preview.Eligibility.ActionApproval!.IsAllowed);
        Assert.Equal(s.Target.Id, preview.Eligibility.ActionApproval.ProposedTargetId);
        Assert.Equal(FrostbiteArrowReason.ExchangeTimingUnresolved, preview.RejectionReason);
        AssertRejected(s);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.Attacker.Id));
    }

    [Fact]
    public void TauntedArcherCannotUseFrostbiteArrowAgainstAnotherLegalTarget()
    {
        var other = CharacterFactory.Create(TimeWeaver.Definition, "other_target", 2);
        other.SetTacticalPosition(new(6, 6));
        var s = new Setup(distance: 2, targetDefinition: Nigel.Definition, extra: new[] { other });
        s.ArrangeRoarFromTarget();
        var intent = new AttackIntent(s.Attacker, other, BeothukSpiritGuardian.FrostbiteArrowId, s.Manager, true, TargetRules,
            requiredAttack: () => new(s.Target.Id, true, true, true),
            equipmentRange: () => new(AbilityRangeKind.ManhattanDistance, 2, 3));
        var use = new AbilityUse(s.Attacker, BeothukSpiritGuardian.FrostbiteArrowId, s.Manager, intent);
        var result = s.Executor.Execute(use, s.Request() with { Defender = other }, "bow", s.Bow, s.Rules);
        Assert.Equal(AbilityRejectionReason.ActionRestricted, result.Eligibility.RejectionReason);
        Assert.Equal(38, other.CurrentHealth);
        AssertRejected(s);
    }

    [Fact]
    public void AttackIntentMustBindTheActualTargetAndCannotBeOmitted()
    {
        var s = new Setup();
        var unbound = new AbilityUse(s.Attacker, BeothukSpiritGuardian.FrostbiteArrowId, s.Manager);
        Assert.Equal(FrostbiteArrowReason.AttackIntentRequired,
            s.Executor.Execute(unbound, s.Request(), "bow", s.Bow, s.Rules).RejectionReason);
        var different = CharacterFactory.Create(TimeWeaver.Definition, "different", 2);
        different.SetTacticalPosition(new(8, 5));
        Assert.Equal(AbilityRejectionReason.UseContextChanged,
            s.Executor.Execute(s.Use, s.Request() with { Defender = different }, "bow", s.Bow, s.Rules).Eligibility.RejectionReason);
        AssertRejected(s);
    }

    [Fact]
    public void MissingOrBlockedMapInformationIsRevalidatedThroughExistingTargeting()
    {
        var s = new Setup(requiresMap: true);
        Assert.Equal(AbilityRejectionReason.InvalidTarget, s.Execute().Eligibility.RejectionReason);
        s.MapTargetLegal = false;
        Assert.Equal(AbilityRejectionReason.InvalidTarget, s.Execute().Eligibility.RejectionReason);
        AssertRejected(s);
        s.MapTargetLegal = true;
        Assert.True(s.Execute().Succeeded);
    }

    [Fact]
    public void InvalidCombatDataIsRejectedBeforeMarkingTheUseStarted()
    {
        var s = new Setup();
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Executor.Execute(s.Use,
            s.Request() with { CounterattackPower = -1 }, "bow", s.Bow, s.Rules));
        AssertRejected(s);
        Assert.True(s.Execute().Succeeded);
    }

    [Fact]
    public void InvalidAccuracyOutputCannotDamageRollStatusOrReplayTheUse()
    {
        var s = new Setup();
        s.Accuracy.Values.Enqueue(100);
        Assert.Throws<InvalidOperationException>(() => s.Execute());
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Equal(0, s.Status.Calls);
        Assert.Empty(s.Target.Statuses.TimedStatuses);
        Assert.Equal(FrostbiteArrowReason.AlreadyStarted, s.Execute().RejectionReason);
        Assert.Equal(1, s.Accuracy.Calls);
    }

    [Fact]
    public void AFailedProcProviderCannotBeRerolledForTheSameStrike()
    {
        var s = new Setup();
        var strike = Strike(s);
        s.Status.Values.Enqueue(-1);
        Assert.Throws<InvalidOperationException>(() => s.Effect.Resolve(s.Attacker, s.Target, strike, 0, 2, s.Rules));
        Assert.Equal(FrostbiteArrowReason.AlreadyStarted, s.Effect.Resolve(s.Attacker, s.Target, strike, 0, 2, s.Rules).Reason);
        Assert.Equal(1, s.Status.Calls);
        Assert.Empty(s.Target.Statuses.TimedStatuses);
    }

    [Fact]
    public void ApplyingAProcRechecksDefeatAndCannotResurrectOrMarkTheDeadTarget()
    {
        var s = new Setup();
        var proc = s.Effect.Resolve(s.Attacker, s.Target, Strike(s), 0, 2, s.Rules);
        s.Target.TakeDamage(int.MaxValue);
        var result = s.Effect.Apply(proc, 2);
        Assert.Equal(FrostbiteArrowReason.TargetDefeated, result.Reason);
        Assert.False(result.ApplicationEligible);
        Assert.False(result.StatusApplied);
        Assert.Empty(s.Target.Statuses.TimedStatuses);
        Assert.True(s.Target.IsDefeated);
    }

    [Fact]
    public void ProcApplicationAndRuntimeCharactersRemainIndependent()
    {
        var s = new Setup();
        var other = new Setup();
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(3, s.Target.EffectiveMovement);
        Assert.Equal(5, other.Target.EffectiveMovement);
        Assert.Equal(38, other.Target.CurrentHealth);
        Assert.Empty(other.Target.Statuses.TimedStatuses);
        Assert.Empty(other.Attacker.Cooldowns.Cooldowns);
        Assert.Equal(0, other.Accuracy.Calls);
        Assert.Equal(0, other.Status.Calls);
    }

    private static AbilityTargetingConstraints TargetRules => new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, false, false, false);
    private static StrikeResult Strike(Setup s) => new SingleStrikeResolver(new(new Rolls())).ResolvePhysicalStrike(
        s.Attacker.BaseStats, s.Target.BaseStats, 4, WeaponCategory.Bow, WeaponCategory.MagicScroll, 100);
    private static void AssertRejected(Setup s)
    {
        Assert.Equal(0, s.Accuracy.Calls);
        Assert.Equal(0, s.Status.Calls);
        Assert.True(s.Manager.CanUseMainAction);
        Assert.Empty(s.Attacker.Cooldowns.Cooldowns);
    }

    private sealed class Rolls : IRandomRollProvider
    {
        public Queue<int> Values { get; } = new();
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return Values.Count == 0 ? 0 : Values.Dequeue(); }
    }

    private sealed class Setup
    {
        public Character Attacker { get; }
        public Character Target { get; }
        public RoundLifecycleManager Lifecycle { get; }
        public ActivationManager Manager { get; }
        public AttackConfiguration Bow { get; set; } = new(AttackType.Physical, WeaponCategory.Bow, 4, 90, 2, 3);
        public AbilityUse Use { get; }
        public Rolls Accuracy { get; } = new();
        public Rolls Status { get; } = new();
        public FrostbiteArrowEffect Effect { get; }
        public FrostbiteArrowAbilityExecutor Executor { get; }
        public FrostbiteArrowContext Rules { get; } = new(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, false, false);
        public bool? MapTargetLegal { get; set; }
        public Func<TauntAttackAvailability?>? RequiredAttack { get; set; }
        public Setup(int distance = 3, CharacterDefinition? attackerDefinition = null, CharacterDefinition? targetDefinition = null,
            bool placeAttacker = true, bool placeTarget = true, Character[]? extra = null, bool requiresMap = false)
        {
            Attacker = CharacterFactory.Create(attackerDefinition ?? BeothukSpiritGuardian.Definition, "archer", 1);
            Target = CharacterFactory.Create(targetDefinition ?? TimeWeaver.Definition, "target", 2);
            if (placeAttacker) Attacker.SetTacticalPosition(new(5, 5));
            if (placeTarget) Target.SetTacticalPosition(new(5 + distance, 5));
            Lifecycle = new(2);
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Manager = new(new[] { Attacker }.Concat((extra ?? Array.Empty<Character>()).Where(c => c.TeamId == 1)),
                new[] { Target }.Concat((extra ?? Array.Empty<Character>()).Where(c => c.TeamId == 2)), 1, Lifecycle);
            Manager.StartActivation(Attacker);
            var targeting = new AbilityTargetingConstraints(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, requiresMap, false, false);
            var intent = new AttackIntent(Attacker, Target, BeothukSpiritGuardian.FrostbiteArrowId, Manager, true, targeting,
                requiredAttack: () => RequiredAttack?.Invoke(),
                equipmentRange: () => new(AbilityRangeKind.ManhattanDistance, Bow.MinimumRange, Bow.MaximumRange),
                mapTargetLegal: () => MapTargetLegal);
            Use = new(Attacker, BeothukSpiritGuardian.FrostbiteArrowId, Manager, intent);
            Effect = new(new HitRoller(Status));
            Executor = new(new(new(new HitRoller(Accuracy))), Effect);
        }
        public CombatExchangeRequest Request() => new(Attacker, Target, AttackType.Physical, Bow.Power, WeaponCategory.Bow,
            WeaponCategory.MagicScroll, Bow.BaseAccuracy, AttackType.Magical, 4, 100, 1, 2,
            Attacker.TacticalPosition.HasValue && Target.TacticalPosition.HasValue
                ? checked((int)Attacker.TacticalPosition.Value.ManhattanDistanceTo(Target.TacticalPosition.Value)) : 3, true, false, true);
        public FrostbiteArrowUseResult Execute() => Executor.Execute(Use, Request(), "test_bow", Bow, Rules);
        public void ArrangeRoarFromTarget()
        {
            Manager.CompleteActivation();
            Attacker.ResetActivation(); // Fixture places Nigel's Roar before the Archer's still-unused activation.
            Manager.StartActivation(Target);
            Assert.True(new ProvokingRoarAbilityExecutor().Execute(new(Target, Nigel.ProvokingRoarId, Manager), TargetRules).Succeeded);
            Manager.StartActivation(Attacker);
            Attacker.SetTacticalPosition(new(4, 5)); // External ordinary movement, not part of Frostbite Arrow.
            RequiredAttack = () => new(Target.Id, true, true, true);
        }
    }
}
