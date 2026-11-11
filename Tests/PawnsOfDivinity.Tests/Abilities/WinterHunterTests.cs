using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Gameplay;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class WinterHunterTests
{
    [Fact]
    public void EvaluationReadsCurrentFrostbiteWithoutChangingAnything()
    {
        var s = new Setup();
        Assert.Equal(0, WinterHunter.Evaluate(s.Archer, s.Target).PhysicalDamageBonus);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var result = WinterHunter.Evaluate(s.Archer, s.Target);
        Assert.True(result.FrostbiteWasActive);
        Assert.True(result.Applies);
        Assert.Equal(3, result.PhysicalDamageBonus);
        Assert.Equal(52, s.Target.CurrentHealth);
        Assert.Empty(s.Archer.TemporaryModifiers.Active);
        Assert.Empty(s.Target.TemporaryModifiers.Active);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void OpeningUsesOneFlatBonusAndReportsItsPreStrikeSnapshot()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var combat = s.Combat.Resolve(s.Request());
        Assert.Equal(12, combat.OpeningStrike.Damage); // STR 16 + fixture Power 4 - DEF 11 + 3.
        Assert.Equal(3, combat.OpeningStrike.WinterHunter!.PhysicalDamageBonus);
        Assert.True(combat.OpeningStrike.WinterHunter.FrostbiteWasActive);
        Assert.Equal(40, s.Target.CurrentHealth);
        Assert.Equal(1, s.Random.Calls);
    }

    [Fact]
    public void MetadataRemainsPassiveWithoutManualActionOrCooldown()
    {
        var s = new Setup();
        var ability = s.Archer.Definition!.Abilities.Single(a => a.Id == BeothukSpiritGuardian.WinterHunterId);
        Assert.Equal(AbilityClassification.Passive, ability.Classification);
        Assert.Null(ability.RequiredAction);
        Assert.Null(ability.ReuseInterval);
        var lifecycle = Activations();
        var manager = new ActivationManager(new[] { s.Archer }, new[] { s.Target }, 1, lifecycle);
        manager.StartActivation(s.Archer);
        Assert.Equal(AbilityRejectionReason.NotManuallyActivatable,
            AbilityEligibility.Evaluate(s.Archer, ability.Id, manager).RejectionReason);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        WinterHunter.Evaluate(s.Archer, s.Target);
        Assert.True(manager.CanMove);
        Assert.True(manager.CanUseUtilityAction);
        Assert.True(manager.CanUseMainAction);
        Assert.Empty(s.Archer.Cooldowns.Cooldowns);
        Assert.Equal(0, s.Random.Calls);
    }

    [Theory]
    [InlineData("hooded_assassin")]
    [InlineData("gentle_guardian")]
    [InlineData("time_weaver")]
    [InlineData("nigel")]
    [InlineData("gravebound_colossus")]
    public void OtherCharactersCannotReceiveWinterHunterFromSomeoneElsesPassive(string id)
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var other = CharacterFactory.Create(id, 1)!;
        Assert.DoesNotContain(other.Definition!.Abilities, a => a.Id == BeothukSpiritGuardian.WinterHunterId);
        var result = WinterHunter.Evaluate(other, s.Target);
        Assert.Equal(WinterHunterReason.NotPassiveOwner, result.Reason);
        Assert.Equal(0, result.PhysicalDamageBonus);
    }

    [Fact]
    public void CorrectCharacterWithoutItsPassiveAlsoReceivesNoBonus()
    {
        var s = new Setup();
        var missing = new Character("missing", new CharacterDefinition("beothuk_spirit_guardian", "Archer", "",
            s.Archer.BaseStats), 1);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        Assert.Equal(WinterHunterReason.NotPassiveOwner, WinterHunter.Evaluate(missing, s.Target).Reason);
    }

    [Theory]
    [InlineData("attacker", WinterHunterReason.AttackerDefeated)]
    [InlineData("target", WinterHunterReason.TargetDefeated)]
    [InlineData("ally", WinterHunterReason.TargetNotEnemy)]
    [InlineData("self", WinterHunterReason.TargetNotEnemy)]
    [InlineData("magic", WinterHunterReason.NonPhysicalAttack)]
    public void InvalidOrNonphysicalCasesReturnZeroWithoutChangingState(string condition, WinterHunterReason reason)
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        if (condition == "attacker") s.Archer.TakeDamage(int.MaxValue);
        if (condition == "target") s.Target.TakeDamage(int.MaxValue);
        var target = condition == "self" ? s.Archer : condition == "ally"
            ? CharacterFactory.Create(Nigel.Definition, "ally", 1) : s.Target;
        var evaluation = WinterHunter.Evaluate(s.Archer, target, condition == "magic" ? AttackType.Magical : AttackType.Physical);
        Assert.Equal(reason, evaluation.Reason);
        Assert.Equal(0, evaluation.PhysicalDamageBonus);
        Assert.False(evaluation.Applies);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void CleanseAndExpirationRemoveEligibilityImmediatelyWithoutASeparateBuffTimer()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var before = WinterHunter.Evaluate(s.Archer, s.Target);
        CleanseProcessor.BasicCleanse(s.Target, StatusType.Frostbite);
        Assert.Equal(0, WinterHunter.GetPhysicalDamageBonus(s.Archer, s.Target));
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 1);
        s.Target.Statuses.AdvanceTimedStatuses();
        Assert.Equal(0, WinterHunter.GetPhysicalDamageBonus(s.Archer, s.Target));
        Assert.True(before.FrostbiteWasActive);
        Assert.Equal(3, before.PhysicalDamageBonus);
        Assert.Empty(s.Archer.TemporaryModifiers.Active);
    }

    [Theory]
    [InlineData(WeaponCategory.Bow, 12, 100)]
    [InlineData(WeaponCategory.Sword, 14, 100)]
    [InlineData(WeaponCategory.Axe, 12, 100)]
    [InlineData(WeaponCategory.Lance, 10, 90)]
    [InlineData(WeaponCategory.Unarmed, 12, 100)]
    public void PassiveIsPhysicalNotBowOnlyAndDoesNotAlterTriangleOrAccuracy(WeaponCategory category, int damage, int accuracy)
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        // Low-level explicit attack configuration, not permission to equip another weapon.
        var result = s.Combat.Resolve(s.Request() with { AttackerWeapon = category, Distance = category == WeaponCategory.Bow ? 3 : 1 });
        Assert.Equal(damage, result.OpeningStrike.Damage);
        Assert.Equal(accuracy, result.OpeningStrike.HitChance);
        Assert.Equal(WeaponTriangle.Evaluate(category, WeaponCategory.Axe), result.OpeningStrike.TriangleOutcome);
        Assert.Equal(3, result.OpeningStrike.WinterHunter!.PhysicalDamageBonus);
        Assert.Equal(1, s.Random.Calls);
    }

    [Theory]
    [InlineData(6, 6, 6)]
    [InlineData(20, 12, 0)]
    public void ShieldsAbsorbTheCalculatedDamageWithoutReceivingAnySeparatePassiveMutation(int shield, int absorbed, int lost)
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        s.Target.SetShieldHP(shield);
        WinterHunter.Evaluate(s.Archer, s.Target);
        Assert.Equal(shield, s.Target.ShieldHP);
        var result = s.Combat.Resolve(s.Request());
        Assert.Equal(12, result.OpeningStrike.Damage);
        Assert.Equal(absorbed, result.OpeningShieldAbsorbed);
        Assert.Equal(lost, result.OpeningHealthLost);
        Assert.Equal(shield - absorbed, s.Target.ShieldHP);
        Assert.Equal(1, s.Random.Calls);
    }

    [Fact]
    public void BurnStoredDefenseAndSanctuaryRemainInTheExistingDefensePath()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.Target.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("defense", "support", s.Target.Id,
            TemporaryStat.Defense, 2, 1, 3) });
        Assert.Equal(13, s.Combat.Resolve(s.Request()).OpeningStrike.Damage); // 16+4-(11+2-3)+3.
        Assert.Equal(11, s.Target.BaseStats.Defense);
        var protectedTarget = new Setup();
        protectedTarget.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var protector = CharacterFactory.Create(GraveboundColossus.Definition, "protector", 2);
        protector.SetTacticalPosition(new(6, 5));
        var context = new SanctuaryDefenseContext(new[] { protectedTarget.Archer, protectedTarget.Target, protector });
        var combat = protectedTarget.Combat.Resolve(protectedTarget.Request() with { Sanctuary = context });
        Assert.Equal(9, combat.OpeningStrike.Damage); // 16+4-(11+3)+3.
        Assert.Equal(3, combat.OpeningStrike.WinterHunter!.PhysicalDamageBonus);
        Assert.Equal(11, protectedTarget.Target.EffectiveDefense);
        Assert.Equal(8, protectedTarget.Target.EffectiveResistance);
    }

    [Fact]
    public void MissDoesNotBecomeDamageOrAddAHitRoll()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        s.Random.Values.Enqueue(99);
        var result = s.Combat.Resolve(s.Request() with { OpeningBaseAccuracy = 90 });
        Assert.False(result.OpeningStrike.Hit);
        Assert.Equal(0, result.OpeningStrike.Damage);
        Assert.True(result.OpeningStrike.WinterHunter!.Applies); // Pre-strike eligibility, not actual HP damage.
        Assert.Equal(52, s.Target.CurrentHealth);
        Assert.Equal(1, s.Random.Calls);
    }

    [Theory]
    [InlineData(true, 12)]
    [InlineData(false, 9)]
    public void AnAlreadyActivatedSpiritGuardianCanUseWinterHunterOnItsCounter(bool frozen, int damage)
    {
        var s = new Setup();
        if (frozen) s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        s.Archer.EndActivation();
        var result = s.Combat.Resolve(CounterRequest(s));
        Assert.True(result.CounterattackOccurred);
        Assert.Equal(damage, result.CounterStrike!.Damage);
        Assert.Equal(frozen, result.CounterStrike.WinterHunter!.FrostbiteWasActive);
        Assert.Equal(frozen ? 3 : 0, result.CounterStrike.WinterHunter.PhysicalDamageBonus);
        Assert.Equal(2, s.Random.Calls);
        Assert.True(s.Archer.HasActedThisRound);
    }

    [Fact]
    public void DefenderFollowUpGetsTheBonusBeforeExistingProvisionalReductionWithoutAnotherCounter()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var result = s.Combat.Resolve(CounterRequest(s) with { IncomingAttackPermitsFollowUps = true });
        Assert.Equal(12, result.CounterStrike!.Damage);
        Assert.Equal(FollowUpParticipant.Defender, result.FollowUpBy);
        Assert.Equal(6, result.FollowUpStrike!.Damage); // Current provisional 50% at 18/13 Speed.
        Assert.Equal(3, result.FollowUpStrike.WinterHunter!.PhysicalDamageBonus);
        Assert.Equal(3, s.Random.Calls); // Opening, counter, one follow-up only.
    }

    [Theory]
    [InlineData(false, 9, 9)]
    [InlineData(true, 12, 6)]
    public void EachFollowUpReevaluatesFrostbiteWithoutChangingThePrecedingSnapshot(bool initiallyFrozen, int opening, int followUp)
    {
        var s = new Setup();
        var slow = new Character("slow", "Slow fixture", 2, new CharacterStats(100, 20, 11, 3, 8, 4, 10, 4));
        if (initiallyFrozen) slow.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        // Test-only between-strike state change; production random providers remain read-only.
        s.Random.OnRoll = call =>
        {
            if (call != 1) return;
            if (initiallyFrozen) CleanseProcessor.BasicCleanse(slow, StatusType.Frostbite);
            else slow.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        };
        var result = s.Combat.Resolve(s.Request() with { Defender = slow, IncomingAttackPermitsFollowUps = true });
        Assert.Equal(opening, result.OpeningStrike.Damage);
        Assert.Equal(followUp, result.FollowUpStrike!.Damage); // Existing 75% cap at Speed 18/10.
        Assert.Equal(initiallyFrozen, result.OpeningStrike.WinterHunter!.FrostbiteWasActive);
        Assert.Equal(!initiallyFrozen, result.FollowUpStrike.WinterHunter!.FrostbiteWasActive);
        Assert.Equal(2, s.Random.Calls);
    }

    [Fact]
    public void CounterCanCleanseBeforeItsFollowUpWithoutLeavingACachedBonus()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        s.Random.OnRoll = call => { if (call == 2) CleanseProcessor.BasicCleanse(s.Target, StatusType.Frostbite); };
        var result = s.Combat.Resolve(CounterRequest(s) with { IncomingAttackPermitsFollowUps = true });
        Assert.Equal(12, result.CounterStrike!.Damage);
        Assert.Equal(4, result.FollowUpStrike!.Damage); // floor(9 * .5), without Winter Hunter.
        Assert.True(result.CounterStrike.WinterHunter!.Applies);
        Assert.False(result.FollowUpStrike.WinterHunter!.Applies);
        Assert.Equal(3, s.Random.Calls);
    }

    [Fact]
    public void MagicalDamageHealingPoisonCollisionAndStoredDamageAreNotBoosted()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var magic = s.Combat.Resolve(s.Request() with { OpeningAttackType = AttackType.Magical, AttackerWeapon = WeaponCategory.MagicScroll, OpeningPower = 20 });
        Assert.Equal(18, magic.OpeningStrike.Damage); // INT 6 + fixture Power 20 - RES 8.
        Assert.Null(magic.OpeningStrike.WinterHunter);
        Assert.Equal(8, HealingCalculator.CalculateHealingLight(s.Archer.BaseStats));
        s.Archer.Statuses.ApplyPoison();
        Assert.Equal(2, PoisonProcessor.Process(s.Archer).CalculatedDamage);
        Assert.Equal(3, Assert.Single(CollisionDamageApplicator.Apply(s.Archer).Participants).CalculatedDamage);
        var stored = new SingleStrikeResolver(new(new Rolls())).ResolvePhysicalStrike(s.Archer.BaseStats, s.Target.BaseStats,
            4, WeaponCategory.Bow, WeaponCategory.Axe, 100);
        Assert.Equal(9, stored.Damage); // Stats-only calculation cannot infer ownership; application never recalculates.
        Assert.Equal(9, StrikeDamageApplicator.Apply(stored, s.Target));
    }

    [Fact]
    public void OtherCharactersPhysicalDamageAndNigelsFuryRemainUnchanged()
    {
        var s = new Setup();
        s.Archer.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        s.Target.TakeDamage(26); // Nigel is missing exactly 50% HP: Fury remains +5.
        var result = s.Combat.Resolve(CounterRequest(s) with { IncomingAttackPermitsCounters = false });
        Assert.Equal(20, result.OpeningStrike.Damage); // STR20 + Power4 - DEF9 + Fury5.
        Assert.Equal(0, result.OpeningStrike.WinterHunter!.PhysicalDamageBonus);
        Assert.Equal(WinterHunterReason.NotPassiveOwner, result.OpeningStrike.WinterHunter.Reason);
    }

    [Fact]
    public void FlatBonusStillUsesTheExistingMinimumAndOverflowSafeguards()
    {
        var s = new Setup();
        var armored = new Character("armored", "High DEF fixture", 2, new CharacterStats(100, 0, 100, 0, 0, 0, 0, 0));
        armored.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        Assert.Equal(4, s.Combat.Resolve(s.Request() with { Defender = armored }).OpeningStrike.Damage); // Existing base floor 1, then +3.
        var huge = new Setup();
        huge.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        Assert.Throws<OverflowException>(() => huge.Combat.Resolve(huge.Request() with { OpeningPower = int.MaxValue - 5 }));
        Assert.Equal(0, huge.Random.Calls);
        Assert.Equal(52, huge.Target.CurrentHealth);
    }

    [Fact]
    public void OrdinaryPlayerBowAttackReceivesTheBonusWithoutUsingFrostbiteArrow()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var manager = new ActivationManager(new[] { s.Archer }, new[] { s.Target }, 1, Activations());
        manager.StartActivation(s.Archer);
        var intent = new AttackIntent(s.Archer, s.Target, "ordinary_bow", manager, true, Rules);
        var result = new AttackActionExecutor(s.Combat).Execute(intent, s.Request() with { IncomingAttackPermitsFollowUps = true },
            new(AttackType.Physical, WeaponCategory.Bow, 4, 100, 2, 3));
        Assert.True(result.Completed);
        Assert.Equal(12, result.Combat!.OpeningStrike.Damage);
        Assert.Equal(6, result.Combat.FollowUpStrike!.Damage);
        Assert.Equal(2, s.Random.Calls);
        Assert.True(manager.MainActionUsed);
        Assert.Empty(s.Archer.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData(false, 12)]
    [InlineData(true, 15)]
    public void FrostbiteArrowOnlyGetsTheBonusForFrostbitePresentBeforeItsStrike(bool alreadyFrozen, int damage)
    {
        var s = new ArrowSetup();
        if (alreadyFrozen) s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 1);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(damage, result.Combat!.OpeningStrike.Damage);
        Assert.Equal(alreadyFrozen, result.Combat.OpeningStrike.WinterHunter!.FrostbiteWasActive);
        Assert.Equal(alreadyFrozen ? 3 : 0, result.Combat.OpeningStrike.WinterHunter.PhysicalDamageBonus);
        Assert.True(result.Proc!.StatusApplied);
        Assert.Equal(2, s.Target.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(3, s.Target.EffectiveMovement);
        Assert.Equal(5, s.Target.BaseStats.Movement);
        Assert.Equal(1, s.Accuracy.Calls);
        Assert.Equal(1, s.Status.Calls);
        Assert.True(s.Manager.MainActionUsed);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(39, true)]
    [InlineData(40, false)]
    [InlineData(99, false)]
    public void WinterHunterDoesNotChangeFrostbiteArrowsProcThresholdOrRngCount(int roll, bool proc)
    {
        var s = new ArrowSetup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 1);
        s.Status.Values.Enqueue(roll);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(15, result.Combat!.OpeningStrike.Damage);
        Assert.Equal(proc, result.Proc!.ProcSucceeded);
        Assert.Equal(proc ? 2 : 1, s.Target.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(1, s.Accuracy.Calls);
        Assert.Equal(1, s.Status.Calls);
    }

    [Fact]
    public void ArrowLethalPrevalidationIncludesTheNewBonusBeforeAnyRollOrMutation()
    {
        var s = new ArrowSetup();
        s.Target.TakeDamage(24); // HP14: ordinary damage12 survives, Winter Hunter damage15 would be lethal.
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var result = s.Execute();
        Assert.Equal(FrostbiteArrowReason.LethalProcRulesUnresolved, result.RejectionReason);
        Assert.Equal(14, s.Target.CurrentHealth);
        Assert.Equal(0, s.Accuracy.Calls);
        Assert.Equal(0, s.Status.Calls);
        Assert.True(s.Manager.CanUseMainAction);
        Assert.Empty(s.Archer.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData("shield", FrostbiteArrowReason.ShieldEligibilityUnresolved)]
    [InlineData("flying", FrostbiteArrowReason.FlyingEffectivenessUnresolved)]
    [InlineData("terrain", FrostbiteArrowReason.TerrainInteractionUnresolved)]
    [InlineData("counter", FrostbiteArrowReason.ExchangeTimingUnresolved)]
    public void WinterHunterDoesNotEnableBlockedArrowCases(string condition, FrostbiteArrowReason reason)
    {
        var s = new ArrowSetup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        if (condition == "shield") s.Target.SetShieldHP(6);
        if (condition == "counter") s.Target.SetTacticalPosition(new(7, 5));
        var context = new FrostbiteArrowContext(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, condition == "flying", condition == "terrain");
        var result = s.Execute(context);
        Assert.Equal(reason, result.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Equal(0, s.Accuracy.Calls);
        Assert.Equal(0, s.Status.Calls);
    }

    [Fact]
    public void EvaluationSnapshotsAndIndependentInstancesCannotStackOrShareABuff()
    {
        var s = new Setup();
        s.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        var stats = s.Archer.BaseStats;
        var before = WinterHunter.Evaluate(s.Archer, s.Target);
        for (int i = 0; i < 4; i++) Assert.Equal(3, WinterHunter.GetPhysicalDamageBonus(s.Archer, s.Target));
        Assert.All(typeof(WinterHunterEvaluation).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Same(stats, s.Archer.BaseStats);
        Assert.Empty(s.Archer.TemporaryModifiers.Active);
        var other = new Setup();
        Assert.Equal(0, WinterHunter.GetPhysicalDamageBonus(other.Archer, other.Target));
        CleanseProcessor.BasicCleanse(s.Target, StatusType.Frostbite);
        Assert.True(before.Applies);
        Assert.Equal(0, WinterHunter.GetPhysicalDamageBonus(s.Archer, s.Target));
    }

    [Fact]
    public void InvalidProgrammingInputsAreRejectedWithoutMutation()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => WinterHunter.Evaluate(null!, s.Target));
        Assert.Throws<ArgumentNullException>(() => WinterHunter.Evaluate(s.Archer, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => WinterHunter.Evaluate(s.Archer, s.Target, (AttackType)999));
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(40, s.Archer.CurrentHealth);
        Assert.Equal(52, s.Target.CurrentHealth);
    }

    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy, true, false, false, false);
    private static RoundLifecycleManager Activations()
    {
        var lifecycle = new RoundLifecycleManager(2);
        while (lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) lifecycle.AdvancePhase();
        return lifecycle;
    }
    private static CombatExchangeRequest CounterRequest(Setup s) => new(s.Target, s.Archer, AttackType.Physical, 4,
        WeaponCategory.Axe, WeaponCategory.Bow, 100, AttackType.Physical, 4, 100, 2, 3, 2, true, false);

    private sealed class ArrowSetup
    {
        public Character Archer { get; } = CharacterFactory.Create(BeothukSpiritGuardian.Definition, "archer", 1);
        public Character Target { get; } = CharacterFactory.Create(TimeWeaver.Definition, "target", 2);
        public Rolls Accuracy { get; } = new();
        public Rolls Status { get; } = new();
        public ActivationManager Manager { get; }
        public AbilityUse Use { get; }
        public FrostbiteArrowAbilityExecutor Executor { get; }
        public ArrowSetup()
        {
            Archer.SetTacticalPosition(new(5, 5));
            Target.SetTacticalPosition(new(8, 5));
            Manager = new(new[] { Archer }, new[] { Target }, 1, Activations());
            Manager.StartActivation(Archer);
            var intent = new AttackIntent(Archer, Target, BeothukSpiritGuardian.FrostbiteArrowId, Manager, true, Rules,
                equipmentRange: () => new(AbilityRangeKind.ManhattanDistance, 2, 3));
            Use = new(Archer, BeothukSpiritGuardian.FrostbiteArrowId, Manager, intent);
            Executor = new(new(new(new HitRoller(Accuracy))), new(new HitRoller(Status)));
        }
        public FrostbiteArrowUseResult Execute(FrostbiteArrowContext? context = null)
        {
            var request = new CombatExchangeRequest(Archer, Target, AttackType.Physical, 4, WeaponCategory.Bow, WeaponCategory.MagicScroll,
                100, AttackType.Magical, 4, 100, 1, 2, checked((int)Archer.TacticalPosition!.Value.ManhattanDistanceTo(Target.TacticalPosition!.Value)), true, false, true);
            return Executor.Execute(Use, request, "fixture_bow", new(AttackType.Physical, WeaponCategory.Bow, 4, 100, 2, 3),
                context ?? new(FrostbiteArrowDurationPolicy.ProvisionalTwoRounds, false, false));
        }
    }

    private sealed class Rolls : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public Queue<int> Values { get; } = new();
        public Action<int>? OnRoll { get; set; }
        public int NextRoll() { Calls++; OnRoll?.Invoke(Calls); return Values.Count == 0 ? 0 : Values.Dequeue(); }
    }
    private sealed class Setup
    {
        public Character Archer { get; } = CharacterFactory.Create(BeothukSpiritGuardian.Definition, "archer", 1);
        public Character Target { get; } = CharacterFactory.Create(Nigel.Definition, "target", 2);
        public Rolls Random { get; } = new();
        public CombatExchangeResolver Combat { get; }
        public Setup()
        {
            Archer.SetTacticalPosition(new(5, 5));
            Target.SetTacticalPosition(new(8, 5));
            Combat = new(new(new HitRoller(Random)));
        }
        public CombatExchangeRequest Request() => new(Archer, Target, AttackType.Physical, 4, WeaponCategory.Bow,
            WeaponCategory.Axe, 100, AttackType.Physical, 4, 100, 1, 1, 3, false, true);
    }
}
