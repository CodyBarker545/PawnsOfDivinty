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

public class SanctuaryOfTheForgottenTests
{
    [Fact]
    public void AdjacentEnemyAttackingAnAllyWithinTwoActivatesExactlyThreeDefense()
    {
        var s = new Setup();
        var result = SanctuaryOfTheForgotten.Evaluate(s.Source, s.Recipient, s.Attacker);
        Assert.True(result.BonusApplies);
        Assert.True(result.ConditionSatisfied);
        Assert.Equal(3, result.DefenseBonus);
        Assert.Equal(2, result.RecipientDistance);
        Assert.Equal(1, result.AttackerDistance);
        Assert.Equal(s.Source.Id, result.SourceId);
        Assert.Equal(s.Recipient.Id, result.RecipientId);
        Assert.Equal(11, s.Recipient.EffectiveDefense);
        Assert.Empty(s.Recipient.TemporaryModifiers.Active);
        var defense = s.Context.Evaluate(s.Attacker, s.Recipient);
        Assert.True(defense.IsResolved);
        Assert.Equal(14, defense.EffectiveDefense);
    }

    [Fact]
    public void CombatUsesCurrentAuraWithoutChangingBaseDefenseOrResistance()
    {
        var s = new Setup();
        var result = s.Combat.Resolve(s.Request());
        Assert.Equal(10, result.OpeningStrike.Damage); // 20 + 4 - (11 + 3), neutral triangle.
        Assert.Equal(34, s.Recipient.CurrentHealth);
        Assert.Equal(11, s.Recipient.BaseStats.Defense);
        Assert.Equal(11, s.Recipient.EffectiveDefense);
        Assert.Equal(15, s.Recipient.EffectiveResistance);
        Assert.Equal(1, s.Random.Calls);
    }

    [Fact]
    public void OverlappingEligibleSourcesRemainUnresolvedBeforeAnyRollOrDamage()
    {
        var s = new Setup();
        var other = CharacterFactory.Create(GraveboundColossus.Definition, "other", 1);
        other.SetTacticalPosition(new(6, 6));
        s.Context = new(new[] { s.Source, other, s.Recipient, s.Attacker });
        var result = s.Context.Evaluate(s.Attacker, s.Recipient);
        Assert.Equal(SanctuaryDefenseReason.OverlapUnresolved, result.Reason);
        Assert.False(result.IsResolved);
        Assert.Null(result.EffectiveDefense);
        var error = Assert.Throws<SanctuaryDefenseUnresolvedException>(() => s.Combat.Resolve(s.Request()));
        Assert.Equal(SanctuaryDefenseReason.OverlapUnresolved, error.Result.Reason);
        Assert.Equal(44, s.Recipient.CurrentHealth);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void MetadataIsPassiveWithRadiusTwoAndNoActionCooldownOrOtherOwners()
    {
        var ability = GraveboundColossus.Definition.Abilities.Single(a => a.Id == "sanctuary_of_the_forgotten");
        Assert.Equal("Sanctuary of the Forgotten", ability.DisplayName);
        Assert.Equal(AbilityClassification.Passive, ability.Classification);
        Assert.Equal(AbilityRangeKind.ManhattanRadius, ability.Range!.Kind);
        Assert.Equal(2, ability.Range.Maximum);
        Assert.Null(ability.RequiredAction);
        Assert.Null(ability.ReuseInterval);
        Assert.Contains("+3 DEF", ability.Description);
        foreach (var id in new[] { "hooded_assassin", "gentle_guardian", "time_weaver", "nigel", "beothuk_spirit_guardian" })
            Assert.DoesNotContain(CharacterFactory.Create(id, 1)!.Definition!.Abilities, a => a.Id == ability.Id);
    }

    [Theory]
    [InlineData(5, 5, 0, true)]
    [InlineData(6, 5, 1, true)]
    [InlineData(7, 5, 2, true)]
    [InlineData(6, 6, 2, true)]
    [InlineData(8, 5, 3, false)]
    [InlineData(7, 6, 3, false)]
    public void RecipientRadiusUsesInclusiveManhattanDistance(int x, int y, long distance, bool applies)
    {
        var s = new Setup();
        s.Recipient.SetTacticalPosition(new(x, y));
        var result = SanctuaryOfTheForgotten.Evaluate(s.Source, s.Recipient, s.Attacker);
        Assert.Equal(distance, result.RecipientDistance);
        Assert.Equal(applies, result.BonusApplies);
        Assert.Equal(applies ? 3 : 0, result.DefenseBonus);
        Assert.Equal(11, s.Recipient.BaseStats.Defense);
    }

    [Theory]
    [InlineData(6, 5, true)]
    [InlineData(5, 6, true)]
    [InlineData(4, 5, true)]
    [InlineData(5, 4, true)]
    [InlineData(6, 6, false)]
    [InlineData(7, 5, false)]
    [InlineData(5, 5, false)]
    public void AttackerMustBeOrthogonallyAdjacentToTheSource(int x, int y, bool applies)
    {
        var s = new Setup();
        s.Attacker.SetTacticalPosition(new(x, y));
        var result = SanctuaryOfTheForgotten.Evaluate(s.Source, s.Recipient, s.Attacker);
        Assert.Equal(applies, result.BonusApplies);
        Assert.Equal(applies ? SanctuaryReason.Applies : SanctuaryReason.AttackerNotAdjacent, result.Reason);
    }

    [Fact]
    public void AdjacencyToRecipientOrWallDoesNotReplaceAdjacencyToColossus()
    {
        var s = new Setup();
        s.Attacker.SetTacticalPosition(new(8, 5)); // Adjacent to the recipient, three from the source.
        Assert.Equal(SanctuaryReason.AttackerNotAdjacent, SanctuaryOfTheForgotten.Evaluate(s.Source, s.Recipient, s.Attacker).Reason);
        Assert.Equal(11, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
    }

    [Fact]
    public void SelfEnemiesAndAlliedAttacksDoNotGetABonus()
    {
        var s = new Setup();
        Assert.Equal(SanctuaryReason.SourceExcluded, SanctuaryOfTheForgotten.Evaluate(s.Source, s.Source, s.Attacker).Reason);
        Assert.Equal(SanctuaryReason.RecipientNotAlly, SanctuaryOfTheForgotten.Evaluate(s.Source, s.Attacker, s.Recipient).Reason);
        Assert.Equal(SanctuaryReason.AttackerNotEnemy, SanctuaryOfTheForgotten.Evaluate(s.Source, s.Recipient, s.Recipient).Reason);
        Assert.Equal(22, s.Context.Evaluate(s.Attacker, s.Source).EffectiveDefense);
    }

    [Fact]
    public void ACharacterWithoutTheDocumentedPassiveCannotBeItsSource()
    {
        var s = new Setup();
        Assert.Equal(SanctuaryReason.NotPassiveOwner, SanctuaryOfTheForgotten.Evaluate(s.Recipient, s.Source, s.Attacker).Reason);
        var noAbilities = new Character("no_kit", new CharacterDefinition("gravebound_colossus", "Colossus", "",
            GraveboundColossus.Definition.BaseStats), 1);
        noAbilities.SetTacticalPosition(new(5, 5));
        Assert.Equal(SanctuaryReason.NotPassiveOwner, SanctuaryOfTheForgotten.Evaluate(noAbilities, s.Recipient, s.Attacker).Reason);
    }

    [Fact]
    public void DefeatedRecipientOrAttackerHasNoApplicableIncomingAttackBonus()
    {
        var s = new Setup();
        s.Recipient.TakeDamage(int.MaxValue);
        Assert.Equal(SanctuaryReason.RecipientDefeated, SanctuaryOfTheForgotten.Evaluate(s.Source, s.Recipient, s.Attacker).Reason);
        var other = new Setup();
        other.Attacker.TakeDamage(int.MaxValue);
        Assert.Equal(SanctuaryReason.AttackerDefeated, SanctuaryOfTheForgotten.Evaluate(other.Source, other.Recipient, other.Attacker).Reason);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("recipient")]
    [InlineData("attacker")]
    public void MissingRelevantPositionsRemainExplicitlyUnresolved(string missing)
    {
        var source = CharacterFactory.Create(GraveboundColossus.Definition, "source", 1);
        var recipient = CharacterFactory.Create(GentleGuardian.Definition, "recipient", 1);
        var attacker = CharacterFactory.Create(Nigel.Definition, "attacker", 2);
        if (missing != "source") source.SetTacticalPosition(new(5, 5));
        if (missing != "recipient") recipient.SetTacticalPosition(new(7, 5));
        if (missing != "attacker") attacker.SetTacticalPosition(new(6, 5));
        var result = SanctuaryOfTheForgotten.Evaluate(source, recipient, attacker);
        Assert.Equal(SanctuaryReason.MissingPosition, result.Reason);
        Assert.False(result.IsResolved);
        Assert.Null(result.DefenseBonus);
        Assert.Null(result.ConditionSatisfied);
    }

    [Fact]
    public void BurnAndStoredDefenseBonusesArePreservedBeforeAddingTheAura()
    {
        var s = new Setup();
        s.Recipient.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("existing", s.Source.Id, s.Recipient.Id,
            TemporaryStat.Defense, 4, 1, 3) });
        s.Recipient.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var result = s.Context.Evaluate(s.Attacker, s.Recipient);
        Assert.Equal(12, result.ExistingEffectiveDefense); // 11 + 4 - 3.
        Assert.Equal(15, result.EffectiveDefense);
        Assert.Equal(11, s.Recipient.BaseStats.Defense);
        Assert.Equal(12, s.Recipient.EffectiveDefense);
        Assert.Single(s.Recipient.TemporaryModifiers.Active);
        Assert.Equal(2, s.Recipient.Statuses.TimedStatuses[StatusType.Burn]);
    }

    [Fact]
    public void AuraAddsThreeAfterBurnsExistingZeroFloor()
    {
        var s = new Setup();
        var low = new Character("low", "Low DEF fixture", 1, new CharacterStats(10, 0, 1, 0, 0, 0, 0, 0));
        low.SetTacticalPosition(s.Recipient.TacticalPosition!.Value);
        low.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var context = new SanctuaryDefenseContext(new[] { s.Source, low, s.Attacker });
        Assert.Equal(0, low.EffectiveDefense);
        Assert.Equal(3, context.Evaluate(s.Attacker, low).EffectiveDefense);
    }

    [Fact]
    public void ActualGentleBlessingRemainsStoredWhileSanctuaryIsCalculated()
    {
        var s = new Setup();
        var healer = CharacterFactory.Create(GentleGuardian.Definition, "healer", 1);
        healer.SetTacticalPosition(new(7, 6));
        s.Recipient.TakeDamage(1);
        var lifecycle = ActivationPhase();
        var manager = new ActivationManager(new[] { s.Source, s.Recipient, healer }, new[] { s.Attacker }, 1, lifecycle);
        manager.StartActivation(healer);
        var use = new AbilityUse(healer, GentleGuardian.HealingLightId, manager);
        var result = new HealingLightAbilityExecutor().Execute(use, s.Recipient, Rules(AbilityTargetTeam.Ally));
        Assert.True(result.Succeeded);
        Assert.Equal(13, s.Recipient.EffectiveDefense);
        Assert.Equal(16, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        s.Recipient.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        Assert.Equal(13, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        Assert.Equal(17, s.Recipient.EffectiveResistance); // Blessing's +2 RES only.
        Assert.Equal(2, s.Recipient.TemporaryModifiers.Active.Count);
        Assert.Equal(44, s.Recipient.CurrentHealth);
    }

    [Fact]
    public void MagicalDamageDoesNotReadAuraDefenseOrGainResistance()
    {
        var s = new Setup();
        s.Source.TakeDamage(int.MaxValue); // Would be unresolved for a qualifying physical attack.
        var request = s.Request() with { OpeningAttackType = AttackType.Magical, AttackerWeapon = WeaponCategory.MagicScroll,
            DefenderWeapon = WeaponCategory.Sword, CounterattackType = AttackType.Physical };
        var result = s.Combat.Resolve(request);
        Assert.Equal(1, result.OpeningStrike.Damage);
        Assert.Equal(15, s.Recipient.EffectiveResistance);
        Assert.Equal(1, s.Random.Calls);
    }

    [Fact]
    public void ShieldFirstApplicationUsesReducedDamageAndEvaluationDoesNotTouchHPOrShields()
    {
        var s = new Setup();
        s.Recipient.SetShieldHP(6);
        var before = s.Context.Evaluate(s.Attacker, s.Recipient);
        Assert.Equal(14, before.EffectiveDefense);
        Assert.Equal(44, s.Recipient.CurrentHealth);
        Assert.Equal(6, s.Recipient.ShieldHP);
        var result = s.Combat.Resolve(s.Request());
        Assert.Equal(10, result.OpeningStrike.Damage);
        Assert.Equal(6, result.OpeningShieldAbsorbed);
        Assert.Equal(4, result.OpeningHealthLost);
        Assert.Equal(40, s.Recipient.CurrentHealth);
        Assert.Equal(0, s.Recipient.ShieldHP);
    }

    [Fact]
    public void LivePositionsChangeTheBonusWithoutAnyBuffRemovalOrReapplication()
    {
        var s = new Setup();
        var old = s.Context.Evaluate(s.Attacker, s.Recipient);
        s.Recipient.SetTacticalPosition(new(9, 5));
        Assert.Equal(11, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        s.Recipient.SetTacticalPosition(new(7, 5));
        Assert.Equal(14, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        s.Source.SetTacticalPosition(new(5, 6));
        Assert.Equal(11, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        s.Source.SetTacticalPosition(new(5, 5));
        s.Attacker.SetTacticalPosition(new(8, 5));
        Assert.Equal(11, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        Assert.Equal(14, old.EffectiveDefense);
        Assert.Equal(new GridPosition(5, 5), old.Sources[0].SourcePosition);
        Assert.Empty(s.Recipient.TemporaryModifiers.Active);
    }

    [Fact]
    public void QualifyingSourceDeathDoesNotSilentlyRemoveOrPersistAnUnconfirmedAura()
    {
        var s = new Setup();
        s.Source.TakeDamage(int.MaxValue);
        var result = SanctuaryOfTheForgotten.Evaluate(s.Source, s.Recipient, s.Attacker);
        Assert.True(result.ConditionSatisfied);
        Assert.False(result.IsResolved);
        Assert.Equal(SanctuaryReason.SourceDeathUnresolved, result.Reason);
        Assert.Equal(SanctuaryDefenseReason.SourceDeathUnresolved, s.Context.Evaluate(s.Attacker, s.Recipient).Reason);
        Assert.Throws<SanctuaryDefenseUnresolvedException>(() => s.Combat.Resolve(s.Request()));
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(44, s.Recipient.CurrentHealth);
    }

    [Fact]
    public void DeadOrUnplacedSourcesCannotBlockUnrelatedTeamsAndOutOfRangeAttacks()
    {
        var s = new Setup();
        s.Source.TakeDamage(int.MaxValue);
        s.Recipient.SetTacticalPosition(new(13, 13));
        Assert.True(s.Context.Evaluate(s.Attacker, s.Recipient).IsResolved);
        Assert.Equal(11, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        var unrelated = CharacterFactory.Create(GraveboundColossus.Definition, "unplaced", 2);
        var context = new SanctuaryDefenseContext(new[] { unrelated, s.Recipient, s.Attacker });
        Assert.True(context.Evaluate(s.Attacker, s.Recipient).IsResolved);
    }

    [Fact]
    public void OnlyQualifyingSourcesCountTowardUnresolvedOverlap()
    {
        var s = new Setup();
        var far = CharacterFactory.Create(GraveboundColossus.Definition, "far", 1);
        far.SetTacticalPosition(new(13, 13));
        s.Context = new(new[] { s.Source, far, s.Recipient, s.Attacker });
        var result = s.Context.Evaluate(s.Attacker, s.Recipient);
        Assert.True(result.IsResolved);
        Assert.Equal(3, result.DefenseBonus);
        Assert.Single(result.Sources, source => source.BonusApplies);
        for (int i = 0; i < 4; i++) Assert.Equal(14, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
    }

    [Fact]
    public void CounterattackUsesItsOwnAttackerRecipientAndCurrentAuraConditions()
    {
        var s = new Setup();
        var protector = CharacterFactory.Create(GraveboundColossus.Definition, "counter_source", 2);
        protector.SetTacticalPosition(new(7, 6));
        s.Context = new(new[] { s.Source, s.Recipient, s.Attacker, protector });
        var request = s.Request() with { IncomingAttackPermitsCounters = true, CounterattacksSuppressed = false,
            CounterattackType = AttackType.Physical, CounterattackPower = 20 };
        var result = s.Combat.Resolve(request);
        Assert.Equal(10, result.OpeningStrike.Damage);
        Assert.Equal(13, result.CounterStrike!.Damage); // 7 + fixture Power 20 - (11 + 3).
        Assert.Equal(39, s.Attacker.CurrentHealth);
        Assert.Equal(2, s.Random.Calls);
    }

    [Fact]
    public void CounterReevaluationDoesNotCacheTheOpeningPositions()
    {
        var s = new Setup();
        var protector = CharacterFactory.Create(GraveboundColossus.Definition, "counter_source", 2);
        protector.SetTacticalPosition(new(7, 6));
        s.Context = new(new[] { s.Source, s.Recipient, s.Attacker, protector });
        // Test-only hook simulates changed state between strikes; production providers remain read-only.
        s.Random.BeforeRoll = call => { if (call == 1) protector.SetTacticalPosition(new(13, 13)); };
        var result = s.Combat.Resolve(s.Request() with { IncomingAttackPermitsCounters = true, CounterattacksSuppressed = false,
            CounterattackType = AttackType.Physical, CounterattackPower = 20 });
        Assert.Equal(16, result.CounterStrike!.Damage);
        Assert.Equal(36, s.Attacker.CurrentHealth);
        Assert.Equal(2, s.Random.Calls);
    }

    [Fact]
    public void FollowUpReevaluatesDefenseThenUsesExistingReducedDamageScaling()
    {
        var s = new Setup();
        var fast = new Character("fast", "Fast test attacker", 2, new CharacterStats(52, 20, 11, 3, 8, 4, 30, 4));
        fast.SetTacticalPosition(new(6, 5));
        s.Context = new(new[] { s.Source, s.Recipient, fast });
        s.Random.BeforeRoll = call => { if (call == 1) s.Source.SetTacticalPosition(new(13, 13)); };
        var result = s.Combat.Resolve(s.Request() with { Attacker = fast, IncomingAttackPermitsFollowUps = true });
        Assert.Equal(10, result.OpeningStrike.Damage);
        Assert.True(result.FollowUpOccurred);
        Assert.Equal(9, result.FollowUpStrike!.Damage); // floor((20 + 4 - 11) * 0.75), existing prototype scaling.
        Assert.Equal(25, s.Recipient.CurrentHealth);
        Assert.Equal(2, s.Random.Calls);
    }

    [Fact]
    public void OrdinaryPlayerAttackObtainsTheMatchContextWithoutCallerSuppliedAuraData()
    {
        var s = new Setup();
        var lifecycle = ActivationPhase();
        var manager = new ActivationManager(new[] { s.Source, s.Recipient }, new[] { s.Attacker }, 2, lifecycle);
        manager.StartActivation(s.Attacker);
        var intent = new AttackIntent(s.Attacker, s.Recipient, "ordinary_attack", manager, true, Rules(AbilityTargetTeam.Enemy));
        var executor = new AttackActionExecutor(s.Combat);
        var request = s.Request() with { Sanctuary = null };
        var attack = new AttackConfiguration(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1);
        Assert.True(executor.Preview(intent, request, attack).CanExecute);
        Assert.Equal(0, s.Random.Calls);
        Assert.True(manager.CanUseMainAction);
        var result = executor.Execute(intent, request, attack);
        Assert.True(result.Completed);
        Assert.Equal(10, result.Combat!.OpeningStrike.Damage);
        Assert.True(manager.MainActionUsed);
        Assert.Empty(s.Recipient.Cooldowns.Cooldowns);
    }

    [Fact]
    public void BrutalKnockbackUsesAuraDefenseBeforeItsOwnReductionThenMovementChangesCoverage()
    {
        var s = new Setup();
        var manager = new ActivationManager(new[] { s.Source, s.Recipient }, new[] { s.Attacker }, 2, ActivationPhase());
        manager.StartActivation(s.Attacker);
        var executor = new BrutalKnockbackAbilityExecutor(new(new(new HitRoller(s.Random)), new ForcedMovementService()));
        var result = executor.Execute(new(s.Attacker, Nigel.BrutalKnockbackId, manager), s.Recipient,
            new(AttackType.Physical, WeaponCategory.Axe, 4, 100, 1, 1), WeaponCategory.MagicScroll,
            Rules(AbilityTargetTeam.Enemy), 2, new FlatMap());
        Assert.True(result.Succeeded);
        Assert.Equal(10, result.NormalAxeDamage);
        Assert.Equal(5, result.ReducedOpeningDamage);
        Assert.Equal(new GridPosition(9, 5), s.Recipient.TacticalPosition);
        Assert.Equal(11, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        Assert.Equal(1, s.Random.Calls);
    }

    [Fact]
    public void WailingRampartDoesNotAddAWallProximityRequirementOrStoreAnAuraBuff()
    {
        var s = new Setup();
        var manager = new ActivationManager(new[] { s.Source, s.Recipient }, new[] { s.Attacker }, 1, ActivationPhase());
        manager.StartActivation(s.Source);
        var walls = new WailingRampartService();
        var cast = new WailingRampartAbilityExecutor(walls).Execute(new(s.Source, GraveboundColossus.WailingRampartId, manager),
            "wall", new[] { new GridPosition(5, 6), new(5, 7), new(5, 8) }, new WallMap());
        Assert.True(cast.Succeeded);
        Assert.Equal(14, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        Assert.Single(walls.ActiveAreas);
        Assert.Empty(s.Recipient.TemporaryModifiers.Active);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void ProvokingRoarRestrictsActionsWithoutTurningOffTheDefensivePassive()
    {
        var s = new Setup();
        var manager = new ActivationManager(new[] { s.Source, s.Recipient }, new[] { s.Attacker }, 2, ActivationPhase());
        manager.StartActivation(s.Attacker);
        var roar = new ProvokingRoarAbilityExecutor().Execute(new(s.Attacker, Nigel.ProvokingRoarId, manager), Rules(AbilityTargetTeam.Enemy));
        Assert.True(roar.Succeeded);
        Assert.NotNull(manager.ActionRestrictions.GetActive(s.Source.Id));
        Assert.NotNull(manager.ActionRestrictions.GetActive(s.Recipient.Id));
        var restrictions = manager.ActionRestrictions.Snapshots;
        Assert.Equal(14, s.Context.Evaluate(s.Attacker, s.Recipient).EffectiveDefense);
        Assert.Equal(restrictions, manager.ActionRestrictions.Snapshots);
        Assert.Empty(s.Source.Cooldowns.Cooldowns);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void SentinelReactionUsesTheSameLiveSanctuaryDefensePipeline()
    {
        var s = new Setup();
        var sentinel = CharacterFactory.Create(GraveboundColossus.Definition, "sentinel", 2);
        sentinel.SetTacticalPosition(new(6, 5));
        s.Recipient.SetTacticalPosition(new(8, 5));
        var lifecycle = ActivationPhase();
        var manager = new ActivationManager(new[] { s.Source, s.Recipient }, new[] { sentinel }, 2, lifecycle);
        manager.StartActivation(sentinel);
        var tracker = new ImpalingSentinelTracker(manager, lifecycle);
        var lance = new AttackConfiguration(AttackType.Physical, WeaponCategory.Lance, 4, 100, 1, 1);
        Assert.True(new ImpalingSentinelAbilityExecutor(tracker).Execute(new(sentinel, GraveboundColossus.ImpalingSentinelId, manager),
            "prep", lance, SentinelReactionLimit.ProvisionalOnePerPreparation).Succeeded);
        s.Recipient.SetTacticalPosition(new(7, 5));
        var movement = new SentinelMovementEvent("step", s.Recipient.Id, 1, new(8, 5), new(7, 5), SentinelMovementKind.Ordinary, 2, true);
        var request = new CombatExchangeRequest(sentinel, s.Recipient, AttackType.Physical, 4, WeaponCategory.Lance,
            WeaponCategory.MagicScroll, 100, AttackType.Magical, 3, 100, 1, 2, 1, true, false);
        var result = new ImpalingSentinelReactionResolver(tracker, s.Combat).Execute(sentinel.Id, movement, lance, request, new ReactionMap());
        Assert.True(result.Resolved);
        Assert.Equal(5, result.Strike!.Damage); // 15 + 4 - (11 + 3).
        Assert.Equal(39, s.Recipient.CurrentHealth);
        Assert.False(result.Combat!.FollowUpOccurred);
        Assert.Equal(2, s.Random.Calls);
    }

    [Fact]
    public void MissingContextOrParticipantsAreExplicitRatherThanGuessed()
    {
        var s = new Setup();
        Assert.Equal(SanctuaryDefenseReason.MissingContext, SanctuaryOfTheForgotten.EvaluateDefense(s.Attacker, s.Recipient, null).Reason);
        var incomplete = new SanctuaryDefenseContext(new[] { s.Source, s.Attacker });
        Assert.Equal(SanctuaryDefenseReason.MissingParticipant, incomplete.Evaluate(s.Attacker, s.Recipient).Reason);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void ContextValidationAndOverflowFailBeforeAnyCombatMutation()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => SanctuaryOfTheForgotten.Evaluate(null!, s.Recipient, s.Attacker));
        Assert.Throws<ArgumentNullException>(() => SanctuaryOfTheForgotten.Evaluate(s.Source, null!, s.Attacker));
        Assert.Throws<ArgumentNullException>(() => SanctuaryOfTheForgotten.Evaluate(s.Source, s.Recipient, null!));
        Assert.Throws<ArgumentNullException>(() => new SanctuaryDefenseContext(null!));
        Assert.Throws<ArgumentException>(() => new SanctuaryDefenseContext(new[] { s.Source, s.Source }));
        var duplicate = CharacterFactory.Create(GraveboundColossus.Definition, s.Source.Id, 1);
        Assert.Throws<ArgumentException>(() => new SanctuaryDefenseContext(new[] { s.Source, duplicate }));
        Assert.Throws<ArgumentException>(() => new SanctuaryDefenseContext(new[] { s.Source, null! }));
        var extreme = new Character("extreme", "High DEF fixture", 1, new CharacterStats(10, 0, int.MaxValue, 0, 0, 0, 0, 0));
        extreme.SetTacticalPosition(new(7, 5));
        var context = new SanctuaryDefenseContext(new[] { s.Source, extreme, s.Attacker });
        Assert.Throws<OverflowException>(() => s.Combat.Resolve(s.Request() with { Defender = extreme, Sanctuary = context }));
        Assert.Equal(10, extreme.CurrentHealth);
        Assert.Equal(0, s.Random.Calls);
    }

    [Fact]
    public void EvaluationSnapshotsAreReadOnlyAndConsumeNoActionsCooldownsOrRandomness()
    {
        var s = new Setup();
        var manager = new ActivationManager(new[] { s.Source, s.Recipient }, new[] { s.Attacker }, 2, ActivationPhase());
        manager.StartActivation(s.Attacker);
        var result = s.Context.Evaluate(s.Attacker, s.Recipient);
        Assert.Throws<NotSupportedException>(() => ((IList<SanctuaryEvaluation>)result.Sources).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Character>)s.Context.Participants).Clear());
        foreach (var type in new[] { typeof(SanctuaryEvaluation), typeof(SanctuaryDefenseResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.True(manager.CanUseMainAction);
        Assert.True(manager.CanUseUtilityAction);
        Assert.True(manager.CanMove);
        Assert.Empty(s.Source.Cooldowns.Cooldowns);
        Assert.Empty(s.Recipient.Cooldowns.Cooldowns);
        Assert.Equal(0, s.Random.Calls);
        Assert.Equal(44, s.Recipient.CurrentHealth);
        Assert.Equal(0, s.Recipient.ShieldHP);
        Assert.False(s.Recipient.HasActedThisRound);
    }

    private static AbilityTargetingConstraints Rules(AbilityTargetTeam team) => new(AbilityTargetType.Character,
        false, team, true, false, false, false);
    private static RoundLifecycleManager ActivationPhase()
    {
        var lifecycle = new RoundLifecycleManager(2);
        while (lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) lifecycle.AdvancePhase();
        return lifecycle;
    }
    private sealed class FlatMap : IForcedMovementMap
    {
        public bool? IsValidPosition(GridPosition position) => true;
        public bool? IsOccupied(GridPosition position) => false;
        public bool? CanDisplace(Character character, GridPosition from, GridPosition to) => true;
    }
    private sealed class WallMap : IWailingRampartMap
    {
        public bool? IsValidPosition(GridPosition position) => true;
        public bool? IsOccupied(GridPosition position) => false;
        public bool? CanPlaceWall(Character caster, GridPosition position) => true;
    }
    private sealed class ReactionMap : ISentinelReactionMap
    {
        public bool? IsValidPosition(GridPosition position) => true;
        public bool? CanReact(Character owner, Character mover, SentinelMovementEvent movement) => true;
    }

    private sealed class Rolls : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public Action<int>? BeforeRoll { get; set; }
        public int NextRoll() { Calls++; BeforeRoll?.Invoke(Calls); return 0; }
    }

    private sealed class Setup
    {
        public Character Source { get; } = CharacterFactory.Create(GraveboundColossus.Definition, "source", 1);
        public Character Recipient { get; } = CharacterFactory.Create(GentleGuardian.Definition, "recipient", 1);
        public Character Attacker { get; } = CharacterFactory.Create(Nigel.Definition, "attacker", 2);
        public SanctuaryDefenseContext Context { get; set; }
        public Rolls Random { get; } = new();
        public CombatExchangeResolver Combat { get; }
        public Setup()
        {
            Source.SetTacticalPosition(new(5, 5));
            Recipient.SetTacticalPosition(new(7, 5));
            Attacker.SetTacticalPosition(new(6, 5));
            Context = new(new[] { Source, Recipient, Attacker });
            Combat = new(new(new HitRoller(Random)));
        }
        public CombatExchangeRequest Request() => new(Attacker, Recipient, AttackType.Physical, 4, WeaponCategory.Axe,
            WeaponCategory.MagicScroll, 100, AttackType.Magical, 3, 100, 1, 2, 1, false, true, Sanctuary: Context);
    }
}
