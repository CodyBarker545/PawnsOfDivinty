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

public class BeothukSpiritGuardianAbilityTests
{
    [Fact]
    public void DefinitionHasConfirmedStatsBowAndTheThreeDocumentedAbilities()
    {
        var character = CharacterFactory.Create("beothuk_spirit_guardian", 1)!;
        Assert.Equal(new CharacterStats(40, 16, 9, 6, 12, 13, 18, 5), character.BaseStats);
        Assert.Equal(WeaponCategory.Bow, BeothukSpiritGuardian.StartingWeaponCategory);
        Assert.Equal(2, BeothukSpiritGuardian.StartingMinimumRange);
        Assert.Equal(3, BeothukSpiritGuardian.StartingMaximumRange);
        Assert.Equal(new[] { "spirit_frost", "frostbite_arrow", "winter_hunter" }, character.Definition!.Abilities.Select(a => a.Id));
    }

    [Fact]
    public void ConfirmedEmptyFieldCastCreatesSixTilesAndFinalizesOneMainAction()
    {
        var s = new Setup();
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(6, result.CreatedIce!.Tiles.Count);
        Assert.Equal(2, result.CreatedIce.CreationRound);
        Assert.Equal(3, result.CreatedIce.ExpirationRound);
        Assert.Equal(5, result.Finalization!.ReadyRound);
        Assert.True(s.Manager.MainActionUsed);
        Assert.Null(s.Manager.ActiveCharacter);
        Assert.Equal(40, s.Caster.CurrentHealth);
        Assert.Equal(new GridPosition(5, 5), s.Caster.TacticalPosition);
        Assert.Empty(s.Enemy.Statuses.TimedStatuses);
    }

    [Fact]
    public void EnemyOnProposedIceBlocksTheUnresolvedStatusSequenceBeforeMutation()
    {
        var s = new Setup();
        s.Enemy.SetTacticalPosition(new(5, 6));
        var result = s.Execute();
        Assert.Equal(SpiritFrostReason.TerrainFrostbiteRulesUnresolved, result.Placement!.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Empty(s.Ice.ActiveAreas);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.Empty(s.Enemy.Statuses.TimedStatuses);
        Assert.True(s.Manager.CanUseMainAction);
    }

    [Theory]
    [InlineData("spirit_frost", "Spirit Frost", AbilityClassification.Active, 3)]
    [InlineData("frostbite_arrow", "Frostbite Arrow", AbilityClassification.Active, 0)]
    [InlineData("winter_hunter", "Winter Hunter", AbilityClassification.Passive, null)]
    public void RegisteredKitPreservesDocumentedMetadataAndUnknownSpellTags(string id, string name,
        AbilityClassification classification, int? reuse)
    {
        var ability = BeothukSpiritGuardian.Definition.Abilities.Single(a => a.Id == id);
        Assert.Equal(name, ability.DisplayName);
        Assert.Equal(classification, ability.Classification);
        Assert.Equal(reuse, ability.ReuseInterval);
        Assert.Null(ability.IsSpellTagged);
        Assert.Equal(classification == AbilityClassification.Active ? AbilityActionType.MainAction : (AbilityActionType?)null,
            ability.RequiredAction);
        if (id == "spirit_frost")
        {
            Assert.Equal(AbilityRangeKind.ManhattanDistance, ability.Range!.Kind);
            Assert.Equal(1, ability.Range.Minimum);
            Assert.Equal(3, ability.Range.Maximum);
        }
        else if (id == "frostbite_arrow")
        {
            Assert.Equal(AbilityRangeKind.EquippedWeapon, ability.Range!.Kind);
            Assert.Null(ability.Range.Minimum);
            Assert.Null(ability.Range.Maximum);
            Assert.Contains("40%", ability.Description);
            Assert.Contains("provisional", ability.Description);
            Assert.Contains("150% total", ability.Description);
        }
        else Assert.Null(ability.Range);
    }

    [Fact]
    public void OtherCharactersDoNotOwnTheseAbilitiesAndFactoriesKeepIndependentState()
    {
        var ids = BeothukSpiritGuardian.Definition.Abilities.Select(a => a.Id).ToArray();
        foreach (var definition in CharacterRegistry.GetAllCharacters().Where(d => d.Id != "beothuk_spirit_guardian"))
            Assert.DoesNotContain(definition.Abilities, a => ids.Contains(a.Id));
        var a = CharacterFactory.Create("beothuk_spirit_guardian", 1)!;
        var b = CharacterFactory.Create("beothuk_spirit_guardian", 2)!;
        Assert.NotEqual(a.Id, b.Id);
        Assert.Same(a.BaseStats, b.BaseStats);
        Assert.Same(a.Definition, b.Definition);
        a.TakeDamage(3);
        Assert.Equal(40, b.CurrentHealth);
        Assert.False(b.HasActedThisRound);
    }

    [Theory]
    [InlineData(0, 1, 4, 6, 6, 7)]
    [InlineData(0, -1, 4, 6, 3, 4)]
    [InlineData(1, 0, 6, 7, 4, 6)]
    [InlineData(-1, 0, 3, 4, 4, 6)]
    public void CardinalPlacementHasExactlyTheConfirmedThreeWideTwoDeepFootprint(int dx, int dy, int minX, int maxX, int minY, int maxY)
    {
        var s = new Setup();
        s.Direction = new(dx, dy);
        var result = s.Execute();
        Assert.True(result.Succeeded);
        var tiles = result.CreatedIce!.Tiles;
        Assert.Equal(6, tiles.Distinct().Count());
        Assert.Equal(minX, tiles.Min(p => p.X));
        Assert.Equal(maxX, tiles.Max(p => p.X));
        Assert.Equal(minY, tiles.Min(p => p.Y));
        Assert.Equal(maxY, tiles.Max(p => p.Y));
        Assert.All(tiles, p => Assert.InRange(s.Caster.TacticalPosition!.Value.ManhattanDistanceTo(p), 1, 3));
        Assert.DoesNotContain(s.Caster.TacticalPosition!.Value, tiles);
        Assert.Equal(1, tiles.Min(p => p.ManhattanDistanceTo(s.Caster.TacticalPosition!.Value)));
        Assert.Equal(3, tiles.Max(p => p.ManhattanDistanceTo(s.Caster.TacticalPosition!.Value)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(5, 5)]
    [InlineData(int.MinValue, 0)]
    public void AnArbitraryTargetPositionOrInvalidDirectionCannotReplaceTheDirectionalCast(int x, int y)
    {
        var s = new Setup();
        s.Direction = new(x, y);
        Assert.Equal(SpiritFrostReason.InvalidDirection, s.Execute().Placement!.RejectionReason);
        AssertRejectedWithoutCosts(s);
    }

    [Fact]
    public void PreviewReturnsReadOnlySnapshotsWithoutCreatingOrReservingAnything()
    {
        var s = new Setup();
        var result = s.Preview();
        Assert.True(result.CanExecute);
        Assert.False(result.Succeeded);
        Assert.Equal(s.Caster.Id, result.CasterId);
        Assert.Equal(BeothukSpiritGuardian.SpiritFrostId, result.AbilityId);
        Assert.Equal(2, result.RoundNumber);
        Assert.Empty(s.Ice.ActiveAreas);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.False(s.Manager.MainActionUsed);
        Assert.Throws<NotSupportedException>(() => ((IList<GridPosition>)result.Placement!.ProposedArea!.Tiles).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.Placement!.AffectedCharacterIds).Clear());
        var oldOrigin = result.Placement!.ProposedArea!.Origin;
        s.Caster.SetTacticalPosition(new(7, 5));
        var executed = s.Execute();
        Assert.True(executed.Succeeded);
        Assert.Equal(new GridPosition(7, 5), executed.CreatedIce!.Origin);
        Assert.Equal(new GridPosition(5, 5), oldOrigin);
        foreach (var type in new[] { typeof(SpiritFrostArea), typeof(SpiritFrostPlacementResult), typeof(SpiritFrostUseResult), typeof(SpiritFrostMovementResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("phase", AbilityRejectionReason.WrongPhase)]
    public void ExistingEligibilityChecksRunBeforeTerrainChanges(string condition, AbilityRejectionReason reason)
    {
        var s = new Setup();
        switch (condition)
        {
            case "dead": s.Caster.TakeDamage(int.MaxValue); break;
            case "inactive": s.Manager.CompleteMainAction(); break;
            case "cooldown": s.Caster.Cooldowns.RecordUse(BeothukSpiritGuardian.SpiritFrostId, 1, 3); break;
            case "silence": s.Caster.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "phase": s.Lifecycle.AdvancePhase(); break;
        }
        int cooldownCount = s.Caster.Cooldowns.Cooldowns.Count;
        Assert.Equal(reason, s.Execute().Eligibility.RejectionReason);
        Assert.Empty(s.Ice.ActiveAreas);
        Assert.Equal(cooldownCount, s.Caster.Cooldowns.Cooldowns.Count);
        Assert.Equal(52, s.Enemy.CurrentHealth);
        Assert.Empty(s.Enemy.Statuses.TimedStatuses);
    }

    [Fact]
    public void SpiritFrostDoesNotInventABowRequirementOrADisarmRestriction()
    {
        var s = new Setup();
        s.Caster.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
        Assert.True(s.Execute().Succeeded);
        Assert.Equal(1, s.Caster.Statuses.TimedStatuses[StatusType.Disarm]);
        Assert.Equal(40, s.Caster.CurrentHealth);
    }

    [Fact]
    public void WrongCharacterAndUnownedAbilitiesAreRejected()
    {
        var s = new Setup(definition: Nigel.Definition);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute().Eligibility.RejectionReason);
        AssertRejectedWithoutCosts(s);
        var definition = new CharacterDefinition("wrong_owner", "Wrong", "fixture", BeothukSpiritGuardian.Definition.BaseStats,
            BeothukSpiritGuardian.Definition.Abilities);
        var spoof = new Setup(definition: definition);
        Assert.Equal(SpiritFrostReason.NotSpiritGuardian, spoof.Execute().Placement!.RejectionReason);
        AssertRejectedWithoutCosts(spoof);
    }

    [Theory]
    [InlineData(BeothukSpiritGuardian.FrostbiteArrowId, AbilityRejectionReason.UnsupportedAbility)]
    [InlineData(BeothukSpiritGuardian.WinterHunterId, AbilityRejectionReason.NotManuallyActivatable)]
    public void RegisteringTheRestOfTheKitDoesNotExecuteItsEffects(string id, AbilityRejectionReason reason)
    {
        var s = new Setup();
        var result = s.Executor.Execute(new(s.Caster, id, s.Manager), "ice", s.Direction, s.Map);
        Assert.Equal(reason, result.Eligibility.RejectionReason);
        AssertRejectedWithoutCosts(s);
        Assert.Empty(s.Enemy.Statuses.TimedStatuses);
    }

    [Theory]
    [InlineData("bounds", SpiritFrostReason.InvalidPosition)]
    [InlineData("unknownBounds", SpiritFrostReason.MissingMapInformation)]
    [InlineData("unknownOccupancy", SpiritFrostReason.MissingMapInformation)]
    [InlineData("unresolvedTerrain", SpiritFrostReason.PlacementRulesUnresolved)]
    [InlineData("illegalTerrain", SpiritFrostReason.IllegalTerrain)]
    [InlineData("unknownOccupant", SpiritFrostReason.OccupancyContextMismatch)]
    public void AllSixTilesMustPassCurrentMapChecksBeforeAnyIceIsStored(string condition, SpiritFrostReason reason)
    {
        var s = new Setup();
        var last = new GridPosition(6, 7);
        switch (condition)
        {
            case "bounds": s.Map.Valid = p => p != last; break;
            case "unknownBounds": s.Map.Valid = p => p == last ? null : true; break;
            case "unknownOccupancy": s.Map.Occupied = p => p == last ? null : false; break;
            case "unresolvedTerrain": s.Map.Legal = p => p == last ? null : true; break;
            case "illegalTerrain": s.Map.Legal = p => p != last; break;
            case "unknownOccupant": s.Map.Occupied = p => p == last; break;
        }
        Assert.Equal(reason, s.Execute().Placement!.RejectionReason);
        AssertRejectedWithoutCosts(s);
    }

    [Fact]
    public void MissingPositionsAndMissingMapRemainExplicit()
    {
        var unplaced = new Setup(placeCaster: false);
        Assert.Equal(SpiritFrostReason.MissingCasterPosition, unplaced.Execute().Placement!.RejectionReason);
        var s = new Setup();
        Assert.Equal(SpiritFrostReason.MissingMapInformation, s.Executor.Execute(s.Use, "ice", s.Direction, null).Placement!.RejectionReason);
        var missing = CharacterFactory.Create(GentleGuardian.Definition, "missing", 1);
        Assert.Equal(SpiritFrostReason.MissingParticipantPosition,
            s.Ice.ValidatePlacement(s.Caster, "ice", s.Direction, 2, s.Roster.Append(missing), s.Map).RejectionReason);
        AssertRejectedWithoutCosts(s);
    }

    [Fact]
    public void StalePreviewCannotIgnoreAnEnemyOrAnOccupiedMapDisagreement()
    {
        var s = new Setup();
        Assert.True(s.Preview().CanExecute);
        s.Enemy.SetTacticalPosition(new(5, 6));
        Assert.Equal(SpiritFrostReason.TerrainFrostbiteRulesUnresolved, s.Execute().Placement!.RejectionReason);
        s.Map.Occupied = _ => false;
        Assert.Equal(SpiritFrostReason.OccupancyContextMismatch, s.Execute().Placement!.RejectionReason);
        AssertRejectedWithoutCosts(s);
        Assert.Empty(s.Enemy.Statuses.TimedStatuses);
    }

    [Fact]
    public void AlliedOccupantsAreAllowedWhenTheMapApprovesAndGainNoMovementBonus()
    {
        var s = new Setup();
        s.Ally.SetTacticalPosition(new(5, 6));
        s.Ally.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2); // Independent pre-existing Frostbite.
        s.Ally.SetShieldHP(6);
        var cast = s.Execute();
        Assert.True(cast.Succeeded);
        Assert.Equal(new[] { s.Ally.Id }, cast.Placement!.AffectedCharacterIds);
        var query = s.Ice.EvaluateMovement(s.Ally, s.Ally.TacticalPosition!.Value);
        Assert.Equal(SpiritFrostMovementOutcome.AlliedMovementUnchanged, query.Outcome);
        Assert.Null(query.RequiredStatus);
        Assert.Equal(2, s.Ally.EffectiveMovement); // Existing 4 MOV - 2; ice neither cleanses nor grants +1.
        Assert.Equal(2, s.Ally.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(6, s.Ally.ShieldHP);
        Assert.Equal(44, s.Ally.CurrentHealth);
    }

    [Fact]
    public void EnemyEntryReportsExistingFrostbiteRequirementWithoutChoosingADurationOrDoublePenalty()
    {
        var s = new Setup();
        s.Execute();
        s.Enemy.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        int movement = s.Enemy.EffectiveMovement;
        var result = s.Ice.EvaluateMovement(s.Enemy, new(5, 6));
        Assert.Equal(SpiritFrostMovementOutcome.TerrainFrostbiteRulesUnresolved, result.Outcome);
        Assert.Equal(StatusType.Frostbite, result.RequiredStatus);
        Assert.Equal(movement, s.Enemy.EffectiveMovement);
        Assert.Equal(2, s.Enemy.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(new GridPosition(10, 10), s.Enemy.TacticalPosition);
        Assert.Equal(SpiritFrostMovementOutcome.Unaffected, s.Ice.EvaluateMovement(s.Enemy, new(0, 0)).Outcome);
        s.Enemy.Statuses.AdvanceTimedStatuses();
        s.Enemy.Statuses.AdvanceTimedStatuses();
        Assert.Equal(4, s.Enemy.EffectiveMovement);
    }

    [Fact]
    public void AlreadyFrostbittenEnemiesOnTheFieldStillNeedTheUnresolvedTerrainRefreshRule()
    {
        var s = new Setup();
        s.Enemy.SetTacticalPosition(new(5, 6));
        s.Enemy.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        Assert.Equal(SpiritFrostReason.TerrainFrostbiteRulesUnresolved, s.Execute().Placement!.RejectionReason);
        Assert.Equal(2, s.Enemy.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(2, s.Enemy.EffectiveMovement);
        AssertRejectedWithoutCosts(s);
    }

    [Fact]
    public void IceLifetimeSurvivesCasterDefeatAndExpiresAtFollowingCleanupWithoutCleansing()
    {
        var s = new Setup();
        var cast = s.Execute();
        var snapshot = s.Ice.ActiveAreas;
        s.Caster.TakeDamage(int.MaxValue);
        s.Ally.Statuses.ApplyTimedStatus(StatusType.Frostbite, 2);
        Assert.True(s.Ice.IsCovered(new(5, 6)));
        Assert.Empty(s.Ice.ExpireAtRoundEnd(2, RoundPhase.Cleanup));
        Assert.Throws<InvalidOperationException>(() => s.Ice.ExpireAtRoundEnd(3, RoundPhase.AlternatingActivations));
        Assert.Same(cast.CreatedIce, Assert.Single(s.Ice.ExpireAtRoundEnd(3, RoundPhase.Cleanup)));
        Assert.Empty(s.Ice.ExpireAtRoundEnd(3, RoundPhase.Cleanup));
        Assert.False(s.Ice.IsCovered(new(5, 6)));
        Assert.Single(snapshot);
        Assert.Equal(2, s.Ally.Statuses.TimedStatuses[StatusType.Frostbite]);
        Assert.Equal(5, s.Caster.Cooldowns.Cooldowns[BeothukSpiritGuardian.SpiritFrostId].ReadyRound);
        Assert.Throws<NotSupportedException>(() => ((IList<SpiritFrostArea>)snapshot).Clear());
    }

    [Fact]
    public void DuplicateIdsAndOverlapsAreRejectedWithoutChangingAnotherOwnersIce()
    {
        var service = new SpiritFrostService();
        var first = new Setup(ice: service);
        first.Execute();
        var second = new Setup(ice: service, id: "second");
        Assert.Equal(SpiritFrostReason.DuplicateEffectId, second.Execute().Placement!.RejectionReason);
        Assert.Equal(SpiritFrostReason.OverlapUnresolved, second.Executor.Execute(second.Use, "second_ice", second.Direction, second.Map).Placement!.RejectionReason);
        Assert.Single(service.ActiveAreas);
        Assert.True(second.Manager.CanUseMainAction);
        Assert.Empty(second.Caster.Cooldowns.Cooldowns);
        second.Direction = new(0, -1);
        Assert.True(second.Executor.Execute(second.Use, "second_ice", second.Direction, second.Map).Succeeded);
        Assert.Equal(new[] { "guardian", "second" }, service.ActiveAreas.Select(a => a.OwnerCharacterId));
        Assert.Equal(2, service.ExpireAtRoundEnd(3, RoundPhase.Cleanup).Count);
    }

    [Fact]
    public void FutureIceRemainsPendingAfterOlderIceExpiresAndIdsRemainReserved()
    {
        var service = new SpiritFrostService();
        new Setup(ice: service).Execute();
        var later = new Setup(round: 3, ice: service, id: "later");
        later.Direction = new(0, -1);
        Assert.True(later.Executor.Execute(later.Use, "later_ice", later.Direction, later.Map).Succeeded);
        Assert.Single(service.ExpireAtRoundEnd(3, RoundPhase.Cleanup));
        Assert.Equal("later_ice", Assert.Single(service.ActiveAreas).Id);
        var future = new Setup(round: 4, ice: service);
        Assert.Equal(SpiritFrostReason.DuplicateEffectId, future.Execute().Placement!.RejectionReason);
        Assert.Single(service.ExpireAtRoundEnd(4, RoundPhase.Cleanup));
    }

    [Fact]
    public void SuccessfulCastCannotReplayAndPreservesOrdinaryMovementAndUtilityAccounting()
    {
        var s = new Setup();
        s.Manager.RecordMovement();
        s.Manager.RecordUtilityAction();
        var before = s.Caster.BaseStats;
        Assert.True(s.Execute().Succeeded);
        Assert.True(s.Manager.MovementUsed);
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.True(s.Manager.MainActionUsed);
        Assert.True(s.Caster.HasActedThisRound);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, s.Execute().Eligibility.RejectionReason);
        Assert.Single(s.Ice.ActiveAreas);
        Assert.Single(s.Caster.Cooldowns.Cooldowns);
        Assert.Same(before, s.Caster.BaseStats);
        Assert.Equal(40, s.Caster.CurrentHealth);
        Assert.Equal(0, s.Caster.ShieldHP);
        Assert.Equal(0, s.Caster.HealsReceivedThisRound);
    }

    [Fact]
    public void InvalidProgrammingInputsAndOverflowCannotPartiallyCreateIce()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => new SpiritFrostAbilityExecutor(null!));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(null!, "ice", s.Direction, s.Map));
        foreach (var id in new[] { null, "", " " })
            Assert.ThrowsAny<ArgumentException>(() => s.Executor.Execute(s.Use, id!, s.Direction, s.Map));
        Assert.Throws<ArgumentException>(() => s.Ice.ValidatePlacement(s.Caster, "ice", s.Direction, 2, s.Roster.Append(s.Enemy), s.Map));
        Assert.Throws<ArgumentException>(() => s.Ice.ValidatePlacement(s.Caster, "ice", s.Direction, 2, new[] { s.Caster, null! }, s.Map));
        Assert.Throws<ArgumentException>(() => s.Ice.ValidatePlacement(s.Caster, "ice", s.Direction, 2, new[] { s.Ally, s.Enemy }, s.Map));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Ice.ExpireAtRoundEnd(0, RoundPhase.Cleanup));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Ice.ExpireAtRoundEnd(3, (RoundPhase)999));
        Assert.Throws<OverflowException>(() => new Setup(round: int.MaxValue).Execute());
        s.Caster.SetTacticalPosition(new(int.MaxValue, 5));
        s.Direction = new(1, 0);
        Assert.Equal(SpiritFrostReason.CoordinateOverflow, s.Execute().Placement!.RejectionReason);
        AssertRejectedWithoutCosts(s);
    }

    [Fact]
    public void StandardBowPlayerActionsRetainTwoToThreeRangeAndStrBasedShieldFirstDamage()
    {
        foreach (int distance in new[] { 1, 2, 3, 4 })
        {
            var s = new Setup();
            s.Enemy.SetTacticalPosition(new(5 + distance, 5));
            s.Enemy.SetShieldHP(4);
            var random = new Rolls();
            var combat = new CombatExchangeResolver(new(new HitRoller(random)));
            var intent = new AttackIntent(s.Caster, s.Enemy, "ordinary_bow", s.Manager, true, Rules);
            // Explicit fixture Power; the starting Bow's canonical Power is still unresolved.
            var bow = new AttackConfiguration(AttackType.Physical, BeothukSpiritGuardian.StartingWeaponCategory, 4, 100,
                BeothukSpiritGuardian.StartingMinimumRange, BeothukSpiritGuardian.StartingMaximumRange);
            var request = new CombatExchangeRequest(s.Caster, s.Enemy, AttackType.Physical, 4, WeaponCategory.Bow,
                WeaponCategory.Axe, 100, AttackType.Physical, 0, 100, 1, 1, distance, false, true);
            var result = new AttackActionExecutor(combat).Execute(intent, request, bow);
            Assert.Equal(distance is 2 or 3, result.Completed);
            if (result.Completed)
            {
                Assert.Equal(9, result.Combat!.OpeningStrike.Damage); // STR 16 + fixture Power 4 - DEF 11.
                Assert.Equal(4, result.Combat.OpeningShieldAbsorbed);
                Assert.Equal(5, result.Combat.OpeningHealthLost);
                Assert.Equal(1, random.Calls);
            }
            else { Assert.Equal(52, s.Enemy.CurrentHealth); Assert.Equal(0, random.Calls); }
            Assert.Equal(distance is 2 or 3,
                CounterattackEligibility.CanCounter(s.Caster, distance, 2, 3, true, false));
        }
    }

    [Fact]
    public void TauntStillPreventsTheIceMainActionWithoutSpendingIt()
    {
        var s = new Setup();
        s.Manager.CompleteActivation();
        s.Caster.ResetActivation(); // Fixture arranges Nigel's turn before the Guardian has acted.
        s.Enemy.SetTacticalPosition(new(6, 5));
        s.Manager.StartActivation(s.Enemy);
        Assert.True(new ProvokingRoarAbilityExecutor().Execute(new(s.Enemy, Nigel.ProvokingRoarId, s.Manager), Rules).Succeeded);
        s.Manager.StartActivation(s.Caster);
        Assert.Equal(AbilityRejectionReason.ActionRestrictionUnresolved, s.Execute().Eligibility.RejectionReason);
        AssertRejectedWithoutCosts(s);
        Assert.NotNull(s.Manager.ActionRestrictions.GetActive(s.Caster.Id));
    }

    [Fact]
    public void ExistingWallInteractionMustBeResolvedByTheMapWithoutReplacingEitherTerrainType()
    {
        var wallOwner = CharacterFactory.Create(GraveboundColossus.Definition, "colossus", 1);
        var opponent = CharacterFactory.Create(Nigel.Definition, "opponent", 2);
        wallOwner.SetTacticalPosition(new(5, 5));
        opponent.SetTacticalPosition(new(10, 10));
        var round = new RoundLifecycleManager(2);
        while (round.CurrentPhase != RoundPhase.AlternatingActivations) round.AdvancePhase();
        var manager = new ActivationManager(new[] { wallOwner }, new[] { opponent }, 1, round);
        manager.StartActivation(wallOwner);
        var walls = new WailingRampartService();
        Assert.True(new WailingRampartAbilityExecutor(walls).Execute(new(wallOwner, GraveboundColossus.WailingRampartId, manager),
            "wall", new[] { new GridPosition(4, 6), new(5, 6), new(6, 6) }, new WallMap()).Succeeded);
        var s = new Setup();
        var snapshot = walls.ActiveAreas;
        s.Map.Legal = p => walls.ActiveAreas.Any(a => a.Tiles.Contains(p)) ? null : true;
        Assert.Equal(SpiritFrostReason.PlacementRulesUnresolved, s.Execute().Placement!.RejectionReason);
        AssertRejectedWithoutCosts(s);
        Assert.Equal(snapshot, walls.ActiveAreas);
        Assert.Equal(3, Assert.Single(walls.ActiveAreas).Tiles.Count);
    }

    [Fact]
    public void CreatingIceDoesNotAlterSanctuaryDefenseResistanceOrStoredModifiers()
    {
        var s = new Setup();
        var colossus = CharacterFactory.Create(GraveboundColossus.Definition, "colossus", 1);
        colossus.SetTacticalPosition(new(4, 5));
        s.Ally.SetTacticalPosition(new(3, 5));
        s.Enemy.SetTacticalPosition(new(4, 6));
        var context = new SanctuaryDefenseContext(new[] { colossus, s.Caster, s.Ally, s.Enemy });
        Assert.Equal(12, context.Evaluate(s.Enemy, s.Caster).EffectiveDefense);
        var round = new RoundLifecycleManager(2);
        while (round.CurrentPhase != RoundPhase.AlternatingActivations) round.AdvancePhase();
        var manager = new ActivationManager(new[] { s.Caster, s.Ally, colossus }, new[] { s.Enemy }, 1, round);
        manager.StartActivation(s.Caster);
        var roster = new[] { s.Caster, s.Ally, colossus, s.Enemy };
        var result = s.Executor.Execute(new(s.Caster, BeothukSpiritGuardian.SpiritFrostId, manager), "ice", new(1, 0), new Map(roster));
        Assert.True(result.Succeeded);
        Assert.Equal(12, context.Evaluate(s.Enemy, s.Caster).EffectiveDefense);
        Assert.Equal(12, s.Caster.EffectiveResistance);
        Assert.Equal(9, s.Caster.BaseStats.Defense);
        Assert.Empty(s.Caster.TemporaryModifiers.Active);
        Assert.Equal(0, s.Caster.ShieldHP);
    }

    [Fact]
    public void MapFailureOrBoardEdgeDoesNotLeaveAPartialFieldOrSpendAnything()
    {
        var s = new Setup();
        s.Map.Legal = p => p == new GridPosition(6, 7) ? throw new InvalidOperationException("Map unavailable") : true;
        Assert.Throws<InvalidOperationException>(() => s.Execute());
        AssertRejectedWithoutCosts(s);
        var edge = new Setup();
        edge.Caster.SetTacticalPosition(new(0, 0));
        edge.Direction = new(0, -1);
        var result = edge.Execute();
        Assert.Equal(SpiritFrostReason.InvalidPosition, result.Placement!.RejectionReason);
        Assert.Equal(6, result.Placement.ProposedArea!.Tiles.Count); // No undocumented edge clipping.
        AssertRejectedWithoutCosts(edge);
    }

    [Fact]
    public void UnsupportedActionMetadataDoesNotTurnSpiritFrostIntoAUtilityCast()
    {
        var canonical = BeothukSpiritGuardian.Definition;
        var ability = canonical.Abilities.Single(a => a.Id == BeothukSpiritGuardian.SpiritFrostId);
        var changed = new AbilityDefinition(ability.Id, ability.DisplayName, "Invalid metadata fixture", AbilityClassification.Active,
            requiredAction: AbilityActionType.UtilityAction, range: ability.Range, reuseInterval: 3);
        var template = new CharacterDefinition(canonical.Id, canonical.Name, canonical.Description, canonical.BaseStats, new[] { changed });
        var s = new Setup(definition: template);
        Assert.Equal(SpiritFrostReason.UnsupportedMetadata, s.Execute().Placement!.RejectionReason);
        AssertRejectedWithoutCosts(s);
        Assert.False(s.Manager.UtilityActionUsed);
    }

    private sealed class WallMap : IWailingRampartMap
    {
        public bool? IsValidPosition(GridPosition position) => true;
        public bool? IsOccupied(GridPosition position) => false;
        public bool? CanPlaceWall(Character caster, GridPosition position) => true;
    }

    private static AbilityTargetingConstraints Rules => new(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy,
        true, false, false, false);
    private static void AssertRejectedWithoutCosts(Setup s)
    {
        Assert.Empty(s.Ice.ActiveAreas);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.True(s.Manager.CanUseMainAction);
        Assert.False(s.Manager.MainActionUsed);
    }
    private sealed class Rolls : IRandomRollProvider
    {
        public int Calls { get; private set; }
        public int NextRoll() { Calls++; return 0; }
    }

    private sealed class Map : ISpiritFrostMap
    {
        private readonly Character[] _roster;
        public Func<GridPosition, bool?> Valid { get; set; } = p => p.X >= 0 && p.X < 14 && p.Y >= 0 && p.Y < 14;
        public Func<GridPosition, bool?> Legal { get; set; } = _ => true;
        public Func<GridPosition, bool?> Occupied { get; set; }
        public Map(Character[] roster)
        {
            _roster = roster;
            Occupied = p => _roster.Any(c => !c.IsDefeated && c.TacticalPosition == p);
        }
        public bool? IsValidPosition(GridPosition p) => Valid(p);
        public bool? CanPlaceIce(Character caster, GridPosition p) => Legal(p);
        public bool? IsOccupied(GridPosition p) => Occupied(p);
    }

    private sealed class Setup
    {
        public Character Caster { get; }
        public Character Ally { get; } = CharacterFactory.Create(GentleGuardian.Definition, "ally", 1);
        public Character Enemy { get; } = CharacterFactory.Create(Nigel.Definition, "enemy", 2);
        public Character[] Roster => new[] { Caster, Ally, Enemy };
        public RoundLifecycleManager Lifecycle { get; }
        public ActivationManager Manager { get; }
        public AbilityUse Use { get; }
        public SpiritFrostService Ice { get; }
        public SpiritFrostAbilityExecutor Executor { get; }
        public GridPosition Direction { get; set; } = new(0, 1);
        public Map Map { get; }
        public Setup(int round = 2, CharacterDefinition? definition = null, SpiritFrostService? ice = null, string id = "guardian", bool placeCaster = true)
        {
            Caster = CharacterFactory.Create(definition ?? BeothukSpiritGuardian.Definition, id, 1);
            if (placeCaster) Caster.SetTacticalPosition(new(5, 5));
            Ally.SetTacticalPosition(new(4, 5));
            Enemy.SetTacticalPosition(new(10, 10));
            Lifecycle = new(round);
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Manager = new(new[] { Caster, Ally }, new[] { Enemy }, 1, Lifecycle);
            Manager.StartActivation(Caster);
            Use = new(Caster, BeothukSpiritGuardian.SpiritFrostId, Manager);
            Ice = ice ?? new();
            Executor = new(Ice);
            Map = new(Roster);
        }
        public SpiritFrostUseResult Preview() => Executor.Preview(Use, "ice", Direction, Map);
        public SpiritFrostUseResult Execute() => Executor.Execute(Use, "ice", Direction, Map);
    }
}
