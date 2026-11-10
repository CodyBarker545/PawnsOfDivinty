using System;
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

public class BrutalKnockbackTests
{
    // Power and accuracy here are explicit test inputs, not Nigel's finalized basic Axe balance.
    private static AttackConfiguration Axe => new(AttackType.Physical, WeaponCategory.Axe, 5, 90, 1, 1);
    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character,
        requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);

    [Fact]
    public void Registration_HasConfirmedAdjacentMainActionAndReuseTwo()
    {
        var ability = Assert.Single(Nigel.Definition.Abilities, a => a.Id == Nigel.BrutalKnockbackId);
        Assert.Equal("brutal_knockback", ability.Id);
        Assert.Equal("Brutal Knockback", ability.DisplayName);
        Assert.Equal(AbilityClassification.Active, ability.Classification);
        Assert.Equal(AbilityActionType.MainAction, ability.RequiredAction);
        Assert.Equal(AbilityRangeKind.ManhattanDistance, ability.Range!.Kind);
        Assert.Equal(1, ability.Range.Minimum);
        Assert.Equal(1, ability.Range.Maximum);
        Assert.Equal(2, ability.ReuseInterval);
        Assert.Null(ability.IsSpellTagged);
        foreach (var definition in CharacterRegistry.GetAllCharacters().Where(c => c.Id != "nigel"))
            Assert.DoesNotContain(definition.Abilities, a => a.Id == Nigel.BrutalKnockbackId);
    }

    [Theory]
    [InlineData(1, 0, 2, 8, 5)]
    [InlineData(-1, 0, 1, 5, 5)]
    [InlineData(0, 1, 3, 6, 8)]
    [InlineData(0, -1, 2, 6, 3)]
    public void ForcedMovement_CommitsExplicitDisplacementWithoutChoosingAnAbilityDistance(int dx, int dy, int distance, int x, int y)
    {
        var s = new Setup();
        var original = s.Target;
        var validation = s.Movement.Validate(s.Actor, s.Target, new(dx, dy), distance, s.Map);
        Assert.True(validation.IsLegal);
        Assert.Equal(new GridPosition(x, y), validation.Destination);
        Assert.Equal(new GridPosition(6, 5), s.Target.TacticalPosition);
        var result = s.Movement.Execute(s.Actor, s.Target, validation, s.Map);
        Assert.True(result.Displaced);
        Assert.Same(original, s.Target);
        Assert.Equal(new GridPosition(6, 5), result.OriginalPosition);
        Assert.Equal(new GridPosition(x, y), result.FinalPosition);
        Assert.Equal(result.FinalPosition, s.Target.TacticalPosition);
        Assert.False(s.Activations.MovementUsed);
        Assert.False(s.Activations.MainActionUsed);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData("map", ForcedMovementRejectionReason.MissingMapInformation)]
    [InlineData("unknownTransition", ForcedMovementRejectionReason.MissingMapInformation)]
    [InlineData("unknownOccupancy", ForcedMovementRejectionReason.MissingMapInformation)]
    [InlineData("unknownValidity", ForcedMovementRejectionReason.MissingMapInformation)]
    [InlineData("invalidDestination", ForcedMovementRejectionReason.InvalidDestination)]
    [InlineData("occupied", ForcedMovementRejectionReason.DestinationOccupied)]
    [InlineData("wall", ForcedMovementRejectionReason.IllegalTransition)]
    [InlineData("direction", ForcedMovementRejectionReason.InvalidDirection)]
    [InlineData("zeroDirection", ForcedMovementRejectionReason.InvalidDirection)]
    [InlineData("missingDirection", ForcedMovementRejectionReason.DirectionRequired)]
    [InlineData("missingDistance", ForcedMovementRejectionReason.DistanceRequired)]
    [InlineData("zeroDistance", ForcedMovementRejectionReason.InvalidDistance)]
    [InlineData("negativeDistance", ForcedMovementRejectionReason.InvalidDistance)]
    [InlineData("defeated", ForcedMovementRejectionReason.TargetDefeated)]
    public void ForcedMovement_RejectionsPreservePosition(string invalid, ForcedMovementRejectionReason expected)
    {
        var s = new Setup();
        GridPosition? direction = new(1, 0);
        int? distance = 2;
        switch (invalid)
        {
            case "unknownTransition": s.Map.Transition = null; break;
            case "unknownOccupancy": s.Map.Occupied = null; break;
            case "unknownValidity": s.Map.Validity = null; break;
            case "invalidDestination": s.Map.InvalidPosition = new(8, 5); break;
            case "occupied": s.Map.Occupied = true; break;
            case "wall": s.Map.Transition = false; break;
            case "direction": direction = new(1, 1); break;
            case "zeroDirection": direction = new(0, 0); break;
            case "missingDirection": direction = null; break;
            case "missingDistance": distance = null; break;
            case "zeroDistance": distance = 0; break;
            case "negativeDistance": distance = -1; break;
            case "defeated": s.Target.TakeDamage(int.MaxValue); break;
        }
        var validation = s.Movement.Validate(s.Actor, s.Target, direction, distance, invalid == "map" ? null : s.Map);
        Assert.Equal(expected, validation.RejectionReason);
        Assert.False(s.Movement.Execute(s.Actor, s.Target, validation, s.Map).Displaced);
        Assert.Equal(new GridPosition(6, 5), s.Target.TacticalPosition);
        Assert.False(s.Activations.MainActionUsed);
    }

    [Theory]
    [InlineData("source", ForcedMovementRejectionReason.SourcePositionChanged)]
    [InlineData("target", ForcedMovementRejectionReason.TargetPositionChanged)]
    [InlineData("occupancy", ForcedMovementRejectionReason.DestinationOccupied)]
    [InlineData("terrain", ForcedMovementRejectionReason.IllegalTransition)]
    [InlineData("service", ForcedMovementRejectionReason.InvalidValidationContext)]
    [InlineData("character", ForcedMovementRejectionReason.InvalidValidationContext)]
    [InlineData("defeat", ForcedMovementRejectionReason.TargetDefeated)]
    public void ForcedMovement_RechecksContextAndMapAtExecution(string changed, ForcedMovementRejectionReason expected)
    {
        var s = new Setup();
        var validation = s.Movement.Validate(s.Actor, s.Target, new(1, 0), 2, s.Map);
        var target = s.Target;
        var service = s.Movement;
        switch (changed)
        {
            case "source": s.Actor.SetTacticalPosition(new(4, 5)); break;
            case "target": s.Target.SetTacticalPosition(new(7, 5)); break;
            case "occupancy": s.Map.Occupied = true; break;
            case "terrain": s.Map.Transition = false; break;
            case "service": service = new(); break;
            case "character":
                target = CharacterFactory.Create("hooded_assassin", 2)!;
                target.SetTacticalPosition(new(6, 5)); break;
            case "defeat": s.Target.TakeDamage(int.MaxValue); break;
        }
        var before = target.TacticalPosition;
        var result = service.Execute(s.Actor, target, validation, s.Map);
        Assert.Equal(expected, result.Validation.RejectionReason);
        Assert.Equal(before, target.TacticalPosition);
        Assert.False(result.Displaced);
    }

    [Fact]
    public void ForcedMovement_RejectsCoordinateOverflowAndMissingPositions()
    {
        var s = new Setup(placeActor: false);
        Assert.Equal(ForcedMovementRejectionReason.MissingSourcePosition,
            s.Movement.Validate(s.Actor, s.Target, new(1, 0), 1, s.Map).RejectionReason);
        s.Actor.SetTacticalPosition(new(5, 5));
        var missing = CharacterFactory.Create("hooded_assassin", 2)!;
        Assert.Equal(ForcedMovementRejectionReason.MissingTargetPosition,
            s.Movement.Validate(s.Actor, missing, new(1, 0), 1, s.Map).RejectionReason);
        s.Target.SetTacticalPosition(new(int.MaxValue, 5));
        Assert.Equal(ForcedMovementRejectionReason.DestinationOverflow,
            s.Movement.Validate(s.Actor, s.Target, new(1, 0), int.MaxValue, s.Map).RejectionReason);
        Assert.Equal(new GridPosition(int.MaxValue, 5), s.Target.TacticalPosition);
    }

    [Theory]
    [InlineData(WeaponCategory.Sword, 15, 7, 80, WeaponTriangleOutcome.Disadvantage)]
    [InlineData(WeaponCategory.Lance, 19, 9, 100, WeaponTriangleOutcome.Advantage)]
    [InlineData(WeaponCategory.Axe, 17, 8, 90, WeaponTriangleOutcome.Neutral)]
    [InlineData(WeaponCategory.Bow, 17, 8, 90, WeaponTriangleOutcome.Neutral)]
    public void OpeningPreparation_ReusesNormalAxeDamageThenAppliesDocumentedProvisionalHalf(
        WeaponCategory defenderWeapon, int normal, int reduced, int accuracy, WeaponTriangleOutcome triangle)
    {
        var s = new Setup();
        var result = BrutalKnockbackResolver.PrepareOpening(s.Actor, s.Target, Axe, defenderWeapon);
        Assert.True(result.IsPrepared);
        Assert.Equal(normal, result.NormalAxeDamage);
        Assert.Equal(reduced, result.ProvisionalDamage);
        Assert.Equal(accuracy, result.HitChance);
        Assert.Equal(triangle, result.TriangleOutcome);
        Assert.Null(result.Strike);
        Assert.Equal(0, s.Rolls.Calls);
    }

    [Theory]
    [InlineData(0, true, 8)]
    [InlineData(89, true, 8)]
    [InlineData(90, false, 0)]
    [InlineData(99, false, 0)]
    public void IsolatedOpeningStrike_RollsOnceAndDoesNotApplyDamageOrDisplacement(int roll, bool hit, int damage)
    {
        var s = new Setup(roll: roll);
        var result = s.Resolver.ResolveOpeningStrike(s.Actor, s.Target, Axe, WeaponCategory.Bow);
        Assert.Equal(hit, result.Strike!.Hit);
        Assert.Equal(damage, result.Strike.Damage);
        Assert.Equal(1, s.Rolls.Calls);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Equal(new GridPosition(6, 5), s.Target.TacticalPosition);
        Assert.False(s.Activations.MainActionUsed);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void IsolatedOpening_UsesBurnEffectiveDefenseAndDefenderDisarmWithoutMutatingStats()
    {
        var s = new Setup();
        var stats = s.Target.BaseStats;
        s.Target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        var result = BrutalKnockbackResolver.PrepareOpening(s.Actor, s.Target, Axe, WeaponCategory.Sword);
        Assert.Equal(20, result.NormalAxeDamage);
        Assert.Equal(10, result.ProvisionalDamage);
        Assert.Equal(WeaponTriangleOutcome.Neutral, result.TriangleOutcome);
        Assert.Equal(8, stats.Defense);
        Assert.Same(stats, s.Target.BaseStats);
    }

    [Theory]
    [InlineData(0, 8, 30)]
    [InlineData(5, 3, 35)]
    [InlineData(20, 0, 38)]
    public void IsolatedResolvedStrike_CanUseExistingShieldFirstApplicator(int shield, int healthLost, int health)
    {
        var s = new Setup();
        s.Target.SetShieldHP(shield);
        var opening = s.Resolver.ResolveOpeningStrike(s.Actor, s.Target, Axe, WeaponCategory.Bow);
        int actual = StrikeDamageApplicator.Apply(opening.Strike!, s.Target);
        Assert.Equal(healthLost, actual);
        Assert.Equal(health, s.Target.CurrentHealth);
        Assert.Equal(Math.Max(0, shield - 8), s.Target.ShieldHP);
        Assert.Equal(new GridPosition(6, 5), s.Target.TacticalPosition);
    }

    [Fact]
    public void Opening_MinimumDamageAndOverflowUseExistingSafetyRules()
    {
        var s = new Setup();
        var tank = new Character("tank_fixture", "Test tank", 2,
            new CharacterStats(40, 1, 100, 1, 1, 1, 1, 1));
        tank.SetTacticalPosition(new(6, 5));
        Assert.Equal(1, BrutalKnockbackResolver.PrepareOpening(s.Actor, tank, Axe, WeaponCategory.Bow).ProvisionalDamage);
        Assert.Throws<OverflowException>(() => s.Resolver.ResolveOpeningStrike(s.Actor, s.Target,
            new(AttackType.Physical, WeaponCategory.Axe, int.MaxValue, 90, 1, 1), WeaponCategory.Bow));
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Equal(38, s.Target.CurrentHealth);
    }

    [Theory]
    [InlineData("distance", BrutalKnockbackRejectionReason.None)]
    [InlineData("tooFar", BrutalKnockbackRejectionReason.InvalidPushDistance)]
    [InlineData("zero", BrutalKnockbackRejectionReason.InvalidPushDistance)]
    [InlineData("map", BrutalKnockbackRejectionReason.DisplacementNotResolved)]
    [InlineData("height", BrutalKnockbackRejectionReason.DisplacementNotResolved)]
    [InlineData("occupied", BrutalKnockbackRejectionReason.CollisionResolutionUnresolved)]
    [InlineData("wall", BrutalKnockbackRejectionReason.CollisionResolutionUnresolved)]
    [InlineData("edge", BrutalKnockbackRejectionReason.CollisionResolutionUnresolved)]
    [InlineData("clear", BrutalKnockbackRejectionReason.None)]
    public void CanonicalExecution_ReportsUnresolvedRulesWithoutRollingOrCommitting(string condition, BrutalKnockbackRejectionReason expected)
    {
        var s = new Setup();
        int? distance = condition == "distance" ? null : condition == "tooFar" ? 3 : condition == "zero" ? 0 : 2;
        if (condition == "height") s.Map.Transition = null;
        if (condition == "occupied") s.Map.Occupied = true;
        if (condition == "wall") s.Map.Transition = false;
        if (condition == "edge") s.Map.InvalidPosition = new(8, 5);
        var result = s.Executor.Execute(s.Use, s.Target, Axe, WeaponCategory.Bow, Rules, distance,
            condition == "map" ? null : s.Map);
        Assert.Equal(expected, result.Effect!.RejectionReason);
        if (expected == BrutalKnockbackRejectionReason.None)
        {
            Assert.True(result.Succeeded);
            Assert.Equal(1, s.Rolls.Calls);
            Assert.Equal(30, s.Target.CurrentHealth);
            Assert.Equal(new GridPosition(8, 5), s.Target.TacticalPosition);
            return;
        }
        Assert.False(result.Succeeded);
        Assert.False(result.CanExecute);
        Assert.Null(result.CombatOutcome);
        Assert.Null(result.Finalization);
        Assert.Equal(0, result.HealthLost);
        Assert.Equal(0, result.ShieldAbsorbed);
        Assert.False(result.Displaced);
        s.AssertUnchanged();
    }

    [Theory]
    [InlineData(1, 0, 8, 5)]
    [InlineData(-1, 0, 2, 5)]
    [InlineData(0, 1, 5, 8)]
    [InlineData(0, -1, 5, 2)]
    public void KnockbackProposal_IsDirectlyAwayFromNigel(int dx, int dy, int destinationX, int destinationY)
    {
        var s = new Setup();
        s.Target.SetTacticalPosition(new(5 + dx, 5 + dy));
        var result = s.Preview();
        Assert.Equal(new GridPosition(dx, dy), result.Effect!.Displacement!.Direction);
        Assert.Equal(new GridPosition(destinationX, destinationY), result.Effect.Displacement.Destination);
        Assert.True(result.Effect.Displacement.IsLegal);
        s.AssertUnchanged(checkTargetPosition: false);
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("mainUsed", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("phase", AbilityRejectionReason.WrongPhase)]
    [InlineData("roar", AbilityRejectionReason.UnsupportedAbility)]
    [InlineData("passive", AbilityRejectionReason.NotManuallyActivatable)]
    public void AbilityExecution_ReusesEligibility(string invalid, AbilityRejectionReason expected)
    {
        var s = new Setup();
        switch (invalid)
        {
            case "dead": s.Actor.TakeDamage(int.MaxValue); break;
            case "inactive": s.Activations.CompleteActivation(); break;
            case "mainUsed": s.Activations.CompleteMainAction(); break;
            case "cooldown": s.Actor.Cooldowns.RecordUse(Nigel.BrutalKnockbackId, 2, 2); break;
            case "silence": s.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "phase": s.Lifecycle.AdvancePhase(); break;
            case "roar": s.Use = new(s.Actor, Nigel.ProvokingRoarId, s.Activations); break;
            case "passive": s.Use = new(s.Actor, Nigel.BerserkersFuryId, s.Activations); break;
        }
        var result = s.Execute();
        Assert.Equal(expected, result.Eligibility.RejectionReason);
        Assert.Null(result.Effect);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Equal(new GridPosition(6, 5), s.Target.TacticalPosition);
        Assert.Equal(invalid == "cooldown" ? 1 : 0, s.Actor.Cooldowns.Cooldowns.Count);
    }

    [Theory]
    [InlineData("far", AbilityTargetingRejectionReason.OutsideMaximumRange)]
    [InlineData("diagonal", AbilityTargetingRejectionReason.OutsideMaximumRange)]
    [InlineData("ally", AbilityTargetingRejectionReason.WrongTeam)]
    [InlineData("self", AbilityTargetingRejectionReason.InvalidSelfTarget)]
    [InlineData("defeated", AbilityTargetingRejectionReason.TargetDefeated)]
    [InlineData("sourceMissing", AbilityTargetingRejectionReason.MissingRequiredTargetInformation)]
    [InlineData("type", AbilityTargetingRejectionReason.InvalidTargetType)]
    [InlineData("los", AbilityTargetingRejectionReason.UnresolvedLineOfSight)]
    public void AbilityExecution_RejectsInvalidTargetsBeforeCombat(string invalid, AbilityTargetingRejectionReason expected)
    {
        var s = new Setup(placeActor: invalid != "sourceMissing");
        var target = s.Target;
        var rules = Rules;
        if (invalid == "far") target.SetTacticalPosition(new(8, 5));
        if (invalid == "diagonal") target.SetTacticalPosition(new(6, 6));
        if (invalid == "defeated") target.TakeDamage(int.MaxValue);
        if (invalid == "ally") target = s.Ally;
        if (invalid == "self") target = s.Actor;
        if (invalid == "type") rules = new(AbilityTargetType.Tile, requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);
        if (invalid == "los") rules = new(AbilityTargetType.Character, requiresMapValidation: false, requiresLineOfSight: true, requiresElevationRules: false);
        var result = s.Executor.Execute(s.Use, target, Axe, WeaponCategory.Bow, rules, 2, s.Map);
        Assert.Equal(expected, result.Targeting!.RejectionReason);
        Assert.Null(result.Effect);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData("missingAttack", BrutalKnockbackRejectionReason.AttackConfigurationRequired)]
    [InlineData("sword", BrutalKnockbackRejectionReason.PhysicalAxeRequired)]
    [InlineData("magic", BrutalKnockbackRejectionReason.PhysicalAxeRequired)]
    [InlineData("disarm", BrutalKnockbackRejectionReason.Disarmed)]
    [InlineData("rangedOnly", BrutalKnockbackRejectionReason.OutsideEquippedRange)]
    public void Opening_RejectsUnsupportedWeaponInputsWithoutRandomness(string invalid, BrutalKnockbackRejectionReason expected)
    {
        var s = new Setup();
        AttackConfiguration? attack = invalid switch
        {
            "missingAttack" => null,
            "sword" => new(AttackType.Physical, WeaponCategory.Sword, 5, 90, 1, 1),
            "magic" => new(AttackType.Magical, WeaponCategory.MagicScroll, 5, 90, 1, 2),
            "rangedOnly" => new(AttackType.Physical, WeaponCategory.Axe, 5, 90, 2, 3),
            _ => Axe
        };
        if (invalid == "disarm") s.Actor.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        var result = s.Resolver.ResolveOpeningStrike(s.Actor, s.Target, attack, WeaponCategory.Bow);
        Assert.Equal(expected, result.RejectionReason);
        Assert.Null(result.Strike);
        Assert.Equal(0, s.Rolls.Calls);
    }

    [Fact]
    public void PreviewAndExecute_RevalidateMapAndPositionsAndPreserveSnapshots()
    {
        var s = new Setup();
        var first = s.Preview();
        Assert.True(first.Effect!.Displacement!.IsLegal);
        s.Map.Occupied = true;
        Assert.Equal(BrutalKnockbackRejectionReason.CollisionResolutionUnresolved, s.Execute().Effect!.RejectionReason);
        s.Target.SetTacticalPosition(new(10, 5));
        Assert.Equal(AbilityTargetingRejectionReason.OutsideMaximumRange, s.Execute().Targeting!.RejectionReason);
        Assert.Equal(new GridPosition(6, 5), first.OriginalTargetPosition);
        Assert.Equal(new GridPosition(8, 5), first.Effect.Displacement.Destination);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void ForcedMovement_RechecksOccupancyAfterAnIsolatedCombatStateChange()
    {
        var s = new Setup();
        var movement = s.Movement.Validate(s.Actor, s.Target, new(1, 0), 2, s.Map);
        var strike = s.Resolver.ResolveOpeningStrike(s.Actor, s.Target, Axe, WeaponCategory.Bow);
        StrikeDamageApplicator.Apply(strike.Strike!, s.Target);
        s.Map.Occupied = true;
        var result = s.Movement.Execute(s.Actor, s.Target, movement, s.Map);
        Assert.False(result.Displaced);
        Assert.Equal(ForcedMovementRejectionReason.DestinationOccupied, result.Validation.RejectionReason);
        Assert.Equal(30, s.Target.CurrentHealth); // No rollback of a separate, already applied strike.
        Assert.Equal(new GridPosition(6, 5), s.Target.TacticalPosition);
    }

    [Fact]
    public void ValidationSnapshotsAreImmutableAndMovementPreservesUnrelatedRuntimeState()
    {
        var s = new Setup();
        s.Target.SetShieldHP(6);
        s.Target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        HealingApplicator.Apply(0, s.Target);
        s.Target.Cooldowns.RecordUse("fixture_ability", 2, 3);
        var stats = s.Target.BaseStats;
        var otherPosition = s.Ally.TacticalPosition;
        var validation = s.Movement.Validate(s.Actor, s.Target, new(1, 0), 2, s.Map);
        var result = s.Movement.Execute(s.Actor, s.Target, validation, s.Map);
        Assert.True(result.Displaced);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Equal(6, s.Target.ShieldHP);
        Assert.Equal(2, s.Target.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(1, s.Target.HealsReceivedThisRound);
        Assert.Equal(5, s.Target.Cooldowns.Cooldowns["fixture_ability"].ReadyRound);
        Assert.Same(stats, s.Target.BaseStats);
        Assert.Equal(otherPosition, s.Ally.TacticalPosition);
        Assert.False(s.Target.HasActedThisRound);
        Assert.False(s.Activations.MovementUsed);
        foreach (var type in new[] { typeof(ForcedMovementValidationResult), typeof(ForcedMovementResult),
            typeof(BrutalKnockbackOpeningResult), typeof(BrutalKnockbackEffectResult), typeof(BrutalKnockbackUseResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.False(s.Movement.Execute(s.Actor, s.Target, validation, s.Map).Displaced); // Stale proposal cannot move twice.
    }

    [Fact]
    public void WrongCharacterNullsAndCooldownOverflowAreRejected()
    {
        var s = new Setup();
        Assert.Equal(BrutalKnockbackRejectionReason.NotNigel,
            BrutalKnockbackResolver.PrepareOpening(s.Ally, s.Target, Axe, WeaponCategory.Bow).RejectionReason);
        Assert.Throws<ArgumentNullException>(() => s.Movement.Validate(null!, s.Target, new(1, 0), 2, s.Map));
        Assert.Throws<ArgumentNullException>(() => s.Movement.Execute(s.Actor, s.Target, null!, s.Map));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Preview(null!, s.Target, Axe, WeaponCategory.Bow, Rules, 2, s.Map));
        Assert.Throws<ArgumentOutOfRangeException>(() => BrutalKnockbackResolver.PrepareOpening(s.Actor, s.Target, Axe, (WeaponCategory)999));
        var extreme = new Setup(round: int.MaxValue);
        Assert.Throws<OverflowException>(() => extreme.Execute());
        Assert.Empty(extreme.Actor.Cooldowns.Cooldowns);
        Assert.True(extreme.Activations.CanUseMainAction);
    }

    [Theory]
    [InlineData(0, 0, 8)]
    [InlineData(6, 1, 9)]
    [InlineData(26, 5, 11)]
    [InlineData(51, 9, 13)]
    public void FinalizedOpening_AddsFuryExactlyOnceBeforeFloorDivision(int missingHealth, int fury, int reduced)
    {
        var s = new Setup();
        s.Actor.TakeDamage(missingHealth);
        var prepared = s.Preview();
        Assert.True(prepared.CanExecute);
        Assert.Equal(17, prepared.NormalAxeDamage);
        Assert.Equal(fury, prepared.FuryBonus);
        Assert.Equal(reduced, prepared.ReducedOpeningDamage);
        Assert.Equal(0, s.Rolls.Calls);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(reduced, result.CombatOutcome!.Damage);
        Assert.Equal(38 - reduced, s.Target.CurrentHealth);
        Assert.Equal(1, s.Rolls.Calls);
    }

    [Fact]
    public void FinalizedOpening_DesignerExampleElevenPlusFiveProducesEight()
    {
        var target = new Character("example_target", "Test target", 2, new CharacterStats(40, 1, 14, 1, 1, 1, 1, 1));
        var s = new Setup(target: target);
        s.Actor.TakeDamage(26);
        var result = s.Execute();
        Assert.Equal(11, result.NormalAxeDamage);
        Assert.Equal(5, result.FuryBonus);
        Assert.Equal(8, result.ReducedOpeningDamage);
        Assert.Equal(8, result.HealthLost);
    }

    [Fact]
    public void FinalizedOpening_WidenedFuryAdditionCanProduceAValidReducedInt()
    {
        var definition = new CharacterDefinition("nigel", "Overflow fixture", "Test only",
            new CharacterStats(100, int.MaxValue - 5, 1, 1, 1, 1, 1, 1), Nigel.Definition.Abilities);
        var nigel = CharacterFactory.Create(definition, "large_fixture", 1);
        nigel.SetTacticalPosition(new(5, 5));
        nigel.TakeDamage(50);
        var target = new Character("target", "Test target", 2, new CharacterStats(100, 1, 0, 1, 1, 1, 1, 1));
        target.SetTacticalPosition(new(6, 5));
        var result = BrutalKnockbackResolver.PrepareOpening(nigel, target, Axe, WeaponCategory.Bow);
        Assert.Equal(int.MaxValue, result.NormalAxeDamage);
        Assert.Equal(5, result.FuryBonus);
        Assert.Equal((int)(((long)int.MaxValue + 5) / 2), result.FinalOpeningDamage);
        Assert.Equal(100, target.CurrentHealth);
    }

    [Theory]
    [InlineData(0, 0, 8, 30)]
    [InlineData(0, 5, 3, 35)]
    [InlineData(0, 20, 0, 38)]
    [InlineData(99, 5, 0, 38)]
    public void FullAbility_UsesShieldFirstOpeningAndOneRoll(int roll, int shield, int hpLost, int hp)
    {
        var s = new Setup(roll: roll);
        s.Target.SetShieldHP(shield);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(roll == 0, result.CombatOutcome!.Hit);
        Assert.Equal(hpLost, result.HealthLost);
        Assert.Equal(roll == 0 ? Math.Min(shield, 8) : 0, result.ShieldAbsorbed);
        Assert.Equal(hp, s.Target.CurrentHealth);
        Assert.Equal(roll == 0 ? 2 : 0, result.TilesPushed);
        Assert.Equal(roll == 0 ? new GridPosition(8, 5) : new GridPosition(6, 5), result.FinalTargetPosition);
        Assert.Empty(result.CollisionParticipants);
        Assert.Equal(1, s.Rolls.Calls);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Null(s.Activations.ActiveCharacter);
        Assert.Equal(4, s.Actor.Cooldowns.Cooldowns[Nigel.BrutalKnockbackId].ReadyRound);
        Assert.Equal(2, s.Actor.Cooldowns.Cooldowns[Nigel.BrutalKnockbackId].ReuseInterval);
    }

    [Theory]
    [InlineData(1, 0, 8, 5)]
    [InlineData(-1, 0, 2, 5)]
    [InlineData(0, 1, 5, 8)]
    [InlineData(0, -1, 5, 2)]
    public void FullAbility_CompletesTwoTilePushInAllFourCardinalDirections(int dx, int dy, int x, int y)
    {
        var s = new Setup();
        s.Target.SetTacticalPosition(new(5 + dx, 5 + dy));
        var sameTarget = s.Target;
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.True(result.Displaced);
        Assert.Equal(2, result.TilesPushed);
        Assert.Equal(new GridPosition(x, y), s.Target.TacticalPosition);
        Assert.Same(sameTarget, s.Target);
        Assert.Equal(2, result.Effect!.Steps.Count);
        Assert.Equal(new GridPosition(5 + 2 * dx, 5 + 2 * dy), result.Effect.Steps[0].Destination);
        Assert.Equal(new GridPosition(x, y), result.Effect.Steps[1].Destination);
        Assert.False(s.Target.HasActedThisRound);
    }

    [Fact]
    public void FullAbility_AppliesDamageBeforeThePostHitMovementChecksAndValidatesEachTile()
    {
        var s = new Setup();
        var seen = new System.Collections.Generic.List<(GridPosition Position, int Health, GridPosition? TargetPosition)>();
        s.Map.OnPositionQuery = p => seen.Add((p, s.Target.CurrentHealth, s.Target.TacticalPosition));
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Contains(seen, v => v.Position == new GridPosition(7, 5) && v.Health == 38);
        Assert.Contains(seen, v => v.Position == new GridPosition(8, 5) && v.Health == 38);
        Assert.Contains(seen, v => v.Position == new GridPosition(7, 5) && v.Health == 30 && v.TargetPosition == new GridPosition(6, 5));
        Assert.Contains(seen, v => v.Position == new GridPosition(8, 5) && v.Health == 30 && v.TargetPosition == new GridPosition(6, 5));
        Assert.Equal(new GridPosition(8, 5), s.Target.TacticalPosition);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void LethalOpening_SkipsAllPushAndCollisionAndStillFinalizes(int shield)
    {
        var s = new Setup();
        s.Target.TakeDamage(34); // Four HP remain; opening damage 8 is lethal even with the supplied shield.
        s.Target.SetShieldHP(shield);
        s.Map.Occupied = true; // A collision would be unsupported, but this target will not enter the push branch.
        int mapCalls = 0;
        s.Map.OnPositionQuery = _ => mapCalls++;
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(4, result.HealthLost);
        Assert.Equal(shield, result.ShieldAbsorbed);
        Assert.True(s.Target.IsDefeated);
        Assert.False(result.TargetSurvivedOpening);
        Assert.False(result.Displaced);
        Assert.Equal(0, result.TilesPushed);
        Assert.Empty(result.CollisionParticipants);
        Assert.Equal(new[] { s.Target.Id }, result.DefeatedCharacterIds);
        Assert.Equal(new GridPosition(6, 5), s.Target.TacticalPosition);
        Assert.Equal(0, mapCalls);
        Assert.Equal(1, s.Rolls.Calls);
        Assert.True(s.Activations.MainActionUsed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void FullAbility_ExplicitlySuppressesCountersAndLeavesFollowUpsUnresolved(int roll)
    {
        var s = new Setup(roll: roll);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.True(result.CounterattacksSuppressed);
        Assert.True(result.Combat!.CounterattacksSuppressed);
        Assert.False(result.Combat.CounterattackOccurred);
        Assert.False(result.Combat.FollowUpOccurred);
        Assert.True(result.FollowUpsUnresolved);
        Assert.Equal(0, result.Combat.AttackerHealthLost);
        Assert.Equal(52, s.Actor.CurrentHealth);
        Assert.Equal(1, s.Rolls.Calls);
    }

    [Theory]
    [InlineData(7, 5)]
    [InlineData(8, 5)]
    public void OccupiedInterveningOrFinalTile_IsUnresolvedBeforeAnyAttack(int x, int y)
    {
        var s = new Setup();
        s.Map.OccupiedPosition = new(x, y);
        var result = s.Execute();
        Assert.False(result.Succeeded);
        Assert.Equal(BrutalKnockbackRejectionReason.CollisionResolutionUnresolved, result.RejectionReason);
        Assert.Equal(ForcedMovementRejectionReason.DestinationOccupied, result.Effect!.Displacement!.RejectionReason);
        s.AssertUnchanged();
    }

    [Fact]
    public void ShortenedPushIsNotInventedWhenOnlyOneTileWasProposed()
    {
        var s = new Setup();
        var result = s.Executor.Execute(s.Use, s.Target, Axe, WeaponCategory.Bow, Rules, 1, s.Map);
        Assert.Equal(BrutalKnockbackRejectionReason.ShortenedPushRuleUnresolved, result.RejectionReason);
        s.AssertUnchanged();
    }

    [Fact]
    public void ChangingMapAfterOpening_IsExplicitlyUnresolvedAndCannotReplayTheOpening()
    {
        var s = new Setup();
        s.Rolls.OnRoll = () => s.Map.OccupiedPosition = new(7, 5);
        var result = s.Execute();
        Assert.False(result.Succeeded);
        Assert.True(result.Resolution!.PostHitUnresolved);
        Assert.Equal(BrutalKnockbackRejectionReason.CollisionResolutionUnresolved, result.RejectionReason);
        Assert.Equal(8, result.HealthLost);
        Assert.Equal(30, s.Target.CurrentHealth);
        Assert.Equal(new GridPosition(6, 5), s.Target.TacticalPosition);
        Assert.False(s.Activations.MainActionUsed);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.Null(result.Finalization);
        s.Map.OccupiedPosition = null;
        var repeated = s.Execute();
        Assert.Equal(BrutalKnockbackRejectionReason.AlreadyStarted, repeated.RejectionReason);
        Assert.Equal(1, s.Rolls.Calls);
        Assert.Equal(30, s.Target.CurrentHealth);
    }

    [Fact]
    public void FullAbility_ConsumesOnlyMainAndAllowsTargetsOrdinaryMovementLater()
    {
        var s = new Setup();
        s.Activations.RecordMovement();
        s.Activations.RecordUtilityAction();
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.True(s.Activations.MovementUsed);
        Assert.True(s.Activations.UtilityActionUsed);
        Assert.True(s.Activations.MainActionUsed);
        var repeated = s.Execute();
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, repeated.Eligibility.RejectionReason);
        Assert.Equal(1, s.Rolls.Calls);
        s.Activations.StartActivation(s.Target);
        Assert.True(s.Activations.CanMove);
        Assert.True(s.Activations.CanUseMainAction);
    }

    [Fact]
    public void FullAbility_RecordsReuseTwoAndCanBeUsedAgainOnTheReadyRound()
    {
        var s = new Setup();
        Assert.True(s.Execute().Succeeded);
        foreach (int round in new[] { 3, 4 })
        {
            s.Actor.ResetActivation();
            s.Target.SetTacticalPosition(new(6, 5));
            var lifecycle = new RoundLifecycleManager(round);
            while (lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) lifecycle.AdvancePhase();
            var activations = new ActivationManager(new[] { s.Actor }, new[] { s.Target }, 1, lifecycle);
            activations.StartActivation(s.Actor);
            var use = new AbilityUse(s.Actor, Nigel.BrutalKnockbackId, activations);
            var result = s.Executor.Execute(use, s.Target, Axe, WeaponCategory.Bow, Rules, 2, s.Map);
            if (round == 3) Assert.Equal(AbilityRejectionReason.OnCooldown, result.Eligibility.RejectionReason);
            else
            {
                Assert.True(result.Succeeded);
                Assert.Equal(6, result.Finalization!.ReadyRound);
            }
        }
        Assert.Equal(2, s.Rolls.Calls);
    }

    [Fact]
    public void CompletedResultsAreImmutableSnapshotsAndOtherInstancesRemainIndependent()
    {
        var s = new Setup();
        var other = new Setup();
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(new GridPosition(5, 5), result.OriginalAttackerPosition);
        Assert.Equal(new GridPosition(6, 5), result.OriginalTargetPosition);
        s.Target.SetTacticalPosition(new(11, 11));
        Assert.Equal(new GridPosition(8, 5), result.FinalTargetPosition);
        Assert.Equal(8, result.Combat!.DefenderHealthLost);
        Assert.Equal(8, result.HealthLost);
        other.AssertUnchanged();
        foreach (var type in new[] { typeof(BrutalKnockbackResolution), typeof(CollisionDamageResult), typeof(CollisionParticipantResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
        var steps = Assert.IsAssignableFrom<System.Collections.Generic.IList<ForcedMovementValidationResult>>(result.Effect!.Steps);
        Assert.Throws<NotSupportedException>(() => steps.Clear());
    }

    [Theory]
    [InlineData(0, 0, 3, 3)]
    [InlineData(5, 5, 0, 0)]
    [InlineData(1, 2, 2, 1)]
    public void ConfirmedCollisionHelper_AppliesExactlyThreeShieldFirstToEachActualParticipant(int targetShield, int secondShield,
        int targetHpLost, int secondHpLost)
    {
        var s = new Setup();
        s.Actor.TakeDamage(26); // Fury must not affect collision damage.
        s.Target.SetShieldHP(targetShield);
        s.Ally.SetShieldHP(secondShield);
        s.Ally.SetTacticalPosition(new(7, 5)); // Actual supplied second participant; the helper does not infer collision geometry.
        s.Target.TemporaryModifiers.AddRange(new[] { new TemporaryStatModifier("armor_fixture", s.Target.Id,
            s.Target.Id, TemporaryStat.Defense, 100, 2, 3) });
        var secondPosition = s.Ally.TacticalPosition;
        var result = CollisionDamageApplicator.Apply(s.Target, s.Ally);
        Assert.Equal(2, result.Participants.Count);
        Assert.Equal(new[] { s.Target.Id, s.Ally.Id }, result.Participants.Select(p => p.CharacterId));
        Assert.All(result.Participants, p => Assert.Equal(3, p.CalculatedDamage));
        Assert.Equal(targetHpLost, result.Participants[0].HealthLost);
        Assert.Equal(secondHpLost, result.Participants[1].HealthLost);
        Assert.Equal(Math.Min(3, targetShield), result.Participants[0].ShieldAbsorbed);
        Assert.Equal(Math.Min(3, secondShield), result.Participants[1].ShieldAbsorbed);
        Assert.Equal(38 - targetHpLost, s.Target.CurrentHealth);
        Assert.Equal(38 - secondHpLost, s.Ally.CurrentHealth);
        Assert.Equal(secondPosition, s.Ally.TacticalPosition);
        Assert.Equal(26, s.Actor.CurrentHealth);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.False(s.Activations.MainActionUsed);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void ConfirmedCollisionHelper_RecordsBothDefeatsAndDoesNotInventAnotherUnit()
    {
        var s = new Setup();
        s.Target.TakeDamage(36);
        s.Ally.TakeDamage(36);
        var result = CollisionDamageApplicator.Apply(s.Target, s.Ally);
        Assert.Equal(new[] { s.Target.Id, s.Ally.Id }, result.DefeatedCharacterIds);
        Assert.All(result.Participants, p => { Assert.Equal(2, p.HealthLost); Assert.True(p.Defeated); Assert.Equal(0, p.ResultingHealth); });
        var other = new Setup();
        var single = CollisionDamageApplicator.Apply(other.Target);
        Assert.Single(single.Participants);
        Assert.Equal(38, other.Ally.CurrentHealth);
        Assert.Equal(52, other.Actor.CurrentHealth);
    }

    [Theory]
    [InlineData("sameInstance")]
    [InlineData("sameId")]
    [InlineData("defeated")]
    [InlineData("nullTarget")]
    public void CollisionHelper_ValidatesEntireEventBeforeMutation(string invalid)
    {
        var s = new Setup();
        Character? second = invalid == "sameInstance" ? s.Target : invalid == "sameId"
            ? CharacterFactory.Create(HoodedAssassin.Definition, s.Target.Id, 1) : s.Ally;
        if (invalid == "defeated") second.TakeDamage(int.MaxValue);
        Assert.ThrowsAny<ArgumentException>(() => CollisionDamageApplicator.Apply(invalid == "nullTarget" ? null! : s.Target, second));
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Equal(0, s.Rolls.Calls);
    }

    [Fact]
    public void ActualFourteenByFourteenMapEdgeDoesNotInventAStoppingPosition()
    {
        var s = new Setup();
        s.Actor.SetTacticalPosition(new(12, 5));
        s.Target.SetTacticalPosition(new(13, 5));
        var result = s.Execute();
        Assert.Equal(BrutalKnockbackRejectionReason.CollisionResolutionUnresolved, result.RejectionReason);
        Assert.Equal(new GridPosition(14, 5), result.Effect!.Displacement!.Destination);
        Assert.Equal(new GridPosition(13, 5), s.Target.TacticalPosition);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.False(s.Activations.MainActionUsed);
    }

    [Theory]
    [InlineData("nullAttack", BrutalKnockbackRejectionReason.AttackConfigurationRequired)]
    [InlineData("nonAxe", BrutalKnockbackRejectionReason.PhysicalAxeRequired)]
    [InlineData("disarm", BrutalKnockbackRejectionReason.Disarmed)]
    public void FullAbility_InvalidAttackContextDoesNotRollOrConsumeAnything(string invalid, BrutalKnockbackRejectionReason expected)
    {
        var s = new Setup();
        AttackConfiguration? attack = invalid == "nullAttack" ? null : invalid == "nonAxe"
            ? new(AttackType.Physical, WeaponCategory.Sword, 5, 90, 1, 1) : Axe;
        if (invalid == "disarm") s.Actor.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        var result = s.Executor.Execute(s.Use, s.Target, attack, WeaponCategory.Bow, Rules, 2, s.Map);
        Assert.Equal(expected, result.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Equal(0, s.Rolls.Calls);
        Assert.Equal(38, s.Target.CurrentHealth);
        Assert.False(s.Activations.MainActionUsed);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
    }

    private sealed class Map : IForcedMovementMap
    {
        public bool? Validity { get; set; } = true;
        public GridPosition? InvalidPosition { get; set; }
        public bool? Occupied { get; set; } = false;
        public bool? Transition { get; set; } = true;
        public GridPosition? OccupiedPosition { get; set; }
        public Action<GridPosition>? OnPositionQuery { get; set; }
        public bool? IsValidPosition(GridPosition p)
        {
            OnPositionQuery?.Invoke(p);
            return p == InvalidPosition || p.X < 0 || p.X >= 14 || p.Y < 0 || p.Y >= 14 ? false : Validity;
        }
        public bool? IsOccupied(GridPosition p) => p == OccupiedPosition ? true : Occupied;
        public bool? CanDisplace(Character target, GridPosition from, GridPosition to) => Transition;
    }

    private sealed class Rolls(int roll) : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public Action? OnRoll { get; set; }
        public int NextRoll() { Calls++; OnRoll?.Invoke(); return roll; }
    }

    private sealed class Setup
    {
        public Character Actor { get; } = CharacterFactory.Create("nigel", 1)!;
        public Character Target { get; }
        public Character Ally { get; } = CharacterFactory.Create("time_weaver", 1)!;
        public RoundLifecycleManager Lifecycle { get; }
        public ActivationManager Activations { get; }
        public AbilityUse Use { get; set; }
        public Map Map { get; } = new();
        public Rolls Rolls { get; }
        public ForcedMovementService Movement { get; } = new();
        public BrutalKnockbackResolver Resolver { get; }
        public BrutalKnockbackAbilityExecutor Executor { get; }

        public Setup(int roll = 0, int round = 2, bool placeActor = true, Character? target = null)
        {
            Target = target ?? CharacterFactory.Create("hooded_assassin", 2)!;
            if (placeActor) Actor.SetTacticalPosition(new(5, 5));
            Target.SetTacticalPosition(new(6, 5));
            Ally.SetTacticalPosition(new(5, 6));
            Rolls = new(roll);
            Resolver = new(new SingleStrikeResolver(new HitRoller(Rolls)), Movement);
            Executor = new(Resolver);
            Lifecycle = new(round);
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Activations = new(new[] { Actor, Ally }, new[] { Target }, 1, Lifecycle);
            Activations.StartActivation(Actor);
            Use = new(Actor, Nigel.BrutalKnockbackId, Activations);
        }
        public BrutalKnockbackUseResult Preview() => Executor.Preview(Use, Target, Axe, WeaponCategory.Bow, Rules, 2, Map);
        public BrutalKnockbackUseResult Execute() => Executor.Execute(Use, Target, Axe, WeaponCategory.Bow, Rules, 2, Map);
        public void AssertUnchanged(bool checkTargetPosition = true)
        {
            Assert.Equal(0, Rolls.Calls);
            Assert.Equal(38, Target.CurrentHealth);
            Assert.Equal(0, Target.ShieldHP);
            if (checkTargetPosition) Assert.Equal(new GridPosition(6, 5), Target.TacticalPosition);
            Assert.Equal(52, Actor.CurrentHealth);
            Assert.False(Activations.MainActionUsed);
            Assert.False(Activations.MovementUsed);
            Assert.Empty(Actor.Cooldowns.Cooldowns);
            Assert.Empty(Target.Statuses.TimedStatuses);
        }
    }
}
