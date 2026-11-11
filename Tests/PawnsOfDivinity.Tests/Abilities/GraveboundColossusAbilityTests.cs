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

public class GraveboundColossusAbilityTests
{
    [Fact]
    public void RegisteredColossusKeepsItsExactStatsIdentityAndStartingLanceCategory()
    {
        var character = CharacterFactory.Create("gravebound_colossus", 1)!;
        Assert.Equal("Gravebound Colossus", character.Name);
        Assert.Equal(new CharacterStats(64, 15, 22, 3, 9, 5, 7, 3), character.BaseStats);
        Assert.Same(GraveboundColossus.Definition, character.Definition);
        Assert.Equal(WeaponCategory.Lance, GraveboundColossus.StartingWeaponCategory);
        Assert.Equal(new[] { "wailing_rampart", "impaling_sentinel", "sanctuary_of_the_forgotten" },
            character.Definition!.Abilities.Select(a => a.Id));
        Assert.Equal(new[] { "Wailing Rampart", "Impaling Sentinel", "Sanctuary of the Forgotten" },
            character.Definition.Abilities.Select(a => a.DisplayName));
    }

    [Theory]
    [InlineData("wailing_rampart", AbilityClassification.Active, AbilityRangeKind.ManhattanDistance, 1, 4, 3)]
    [InlineData("impaling_sentinel", AbilityClassification.Active, AbilityRangeKind.EquippedWeapon, null, null, 2)]
    [InlineData("sanctuary_of_the_forgotten", AbilityClassification.Passive, AbilityRangeKind.ManhattanRadius, 0, 2, null)]
    public void KitPreservesConfirmedMetadataAndLeavesUnconfirmedSpellTagsUnknown(string id,
        AbilityClassification classification, AbilityRangeKind rangeKind, int? min, int? max, int? reuse)
    {
        var ability = GraveboundColossus.Definition.Abilities.Single(a => a.Id == id);
        Assert.Equal(classification, ability.Classification);
        Assert.Equal(rangeKind, ability.Range!.Kind);
        Assert.Equal(min, ability.Range.Minimum);
        Assert.Equal(max, ability.Range.Maximum);
        Assert.Equal(reuse, ability.ReuseInterval);
        Assert.Null(ability.IsSpellTagged);
        Assert.Equal(classification == AbilityClassification.Active ? AbilityActionType.MainAction : (AbilityActionType?)null,
            ability.RequiredAction);
        Assert.False(string.IsNullOrWhiteSpace(ability.Description));
    }

    [Fact]
    public void OtherRegisteredCharactersDoNotOwnTheColossusKit()
    {
        foreach (var id in new[] { "hooded_assassin", "gentle_guardian", "time_weaver", "nigel", "beothuk_spirit_guardian" })
            Assert.DoesNotContain(CharacterFactory.Create(id, 1)!.Definition!.Abilities,
                a => GraveboundColossus.Definition.Abilities.Any(own => own.Id == a.Id));
        Assert.Contains("provisional", GraveboundColossus.Definition.Abilities[1].Description);
    }

    [Fact]
    public void PreviewIsImmutableAndDoesNotReserveTilesOrSpendAnything()
    {
        var s = new Setup();
        var original = s.Tiles.ToArray();
        var preview = s.Preview();
        Assert.True(preview.CanExecute);
        Assert.False(preview.Succeeded);
        Assert.Equal(1, preview.Placement!.NearestDistance);
        Assert.Equal(s.Caster.Id, preview.CasterId);
        Assert.Equal(2, preview.RoundNumber);
        Assert.Equal(original, preview.Placement.ProposedArea!.Tiles);
        s.Tiles[0] = new(99, 99);
        Assert.Equal(original, preview.Placement.ProposedArea.Tiles);
        Assert.Throws<NotSupportedException>(() => ((IList<GridPosition>)preview.Placement.ProposedArea.Tiles).Clear());
        foreach (var type in new[] { typeof(WailingRampartArea), typeof(WailingRampartPlacementResult), typeof(WailingRampartUseResult) })
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
        s.AssertUnchanged();
    }

    [Fact]
    public void SuccessfulCastPlacesThreeTilesAndFinalizesOneMainActionAndReuseThree()
    {
        var s = new Setup();
        s.Manager.RecordMovement();
        s.Manager.RecordUtilityAction();
        var cast = s.Execute();
        Assert.True(cast.Succeeded);
        Assert.False(cast.CanExecute);
        Assert.Same(cast.CreatedWall, Assert.Single(s.Walls.ActiveAreas));
        Assert.Equal(s.Caster.Id, cast.CreatedWall!.OwnerCharacterId);
        Assert.Equal(1, cast.CreatedWall.OwnerTeamId);
        Assert.Equal(2, cast.CreatedWall.CreationRound);
        Assert.Equal(3, cast.CreatedWall.ExpirationRound);
        Assert.Equal(3, cast.CreatedWall.Tiles.Count);
        Assert.Equal(5, cast.Finalization!.ReadyRound);
        Assert.False(s.Caster.Cooldowns.IsReady(GraveboundColossus.WailingRampartId, 4));
        Assert.True(s.Caster.Cooldowns.IsReady(GraveboundColossus.WailingRampartId, 5));
        Assert.True(s.Manager.MainActionUsed);
        Assert.True(s.Manager.MovementUsed);
        Assert.True(s.Manager.UtilityActionUsed);
        Assert.True(s.Caster.HasActedThisRound);
        Assert.Null(s.Manager.ActiveCharacter);
        s.AssertHealthAndPositions();
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, s.Execute().Eligibility.RejectionReason);
        Assert.Single(s.Walls.ActiveAreas);
        Assert.Single(s.Caster.Cooldowns.Cooldowns);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 0, true)]
    [InlineData(0, 4, true)]
    [InlineData(4, 0, true)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, false)]
    [InlineData(0, 5, false)]
    [InlineData(5, 0, false)]
    [InlineData(0, 0, false)]
    public void RangeUsesNearestTileWithInclusiveManhattanOneToFour(int x, int y, bool allowed)
    {
        var s = new Setup();
        s.Tiles = new[] { new GridPosition(5 + x, 5 + y), new(6 + x, 5 + y), new(7 + x, 5 + y) };
        var preview = s.Preview();
        Assert.Equal(allowed, preview.CanExecute);
        if (!allowed) Assert.Equal(WailingRampartRejectionReason.OutsideRange, preview.Placement!.RejectionReason);
        s.AssertUnchanged();
    }

    [Fact]
    public void FarEndMayBeBeyondFourWhenNearestTileIsWithinRangeAndInputOrderDoesNotMatter()
    {
        var s = new Setup();
        s.Tiles = new[] { new GridPosition(11, 5), new(9, 5), new(10, 5) };
        var result = s.Execute();
        Assert.True(result.Succeeded);
        Assert.Equal(4, result.Placement!.NearestDistance);
        Assert.Equal(new[] { new GridPosition(9, 5), new(10, 5), new(11, 5) }, result.CreatedWall!.Tiles);
    }

    [Fact]
    public void VerticalWallIsSupported()
    {
        var s = new Setup();
        s.Tiles = new[] { new GridPosition(5, 8), new(5, 6), new(5, 7) };
        Assert.True(s.Execute().Succeeded);
        Assert.Equal(new[] { new GridPosition(5, 6), new(5, 7), new(5, 8) }, s.Walls.ActiveAreas[0].Tiles);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("duplicate")]
    [InlineData("gap")]
    [InlineData("diagonal")]
    [InlineData("corner")]
    public void InvalidFootprintsAreRejectedWithoutMutation(string shape)
    {
        var s = new Setup();
        s.Tiles = shape switch
        {
            "short" => new[] { new GridPosition(6, 5), new(7, 5) },
            "long" => new[] { new GridPosition(6, 5), new(7, 5), new(8, 5), new(9, 5) },
            "duplicate" => new[] { new GridPosition(6, 5), new(6, 5), new(7, 5) },
            "gap" => new[] { new GridPosition(6, 5), new(7, 5), new(9, 5) },
            "diagonal" => new[] { new GridPosition(6, 6), new(7, 7), new(8, 8) },
            _ => new[] { new GridPosition(6, 5), new(7, 5), new(7, 6) }
        };
        Assert.Equal(WailingRampartRejectionReason.InvalidFootprint, s.Execute().Placement!.RejectionReason);
        s.AssertUnchanged();
    }

    [Theory]
    [InlineData("invalid", WailingRampartRejectionReason.InvalidPosition)]
    [InlineData("occupied", WailingRampartRejectionReason.OccupiedTile)]
    [InlineData("terrain", WailingRampartRejectionReason.IllegalTerrain)]
    [InlineData("unknownTerrain", WailingRampartRejectionReason.PlacementRulesUnresolved)]
    [InlineData("unknownBounds", WailingRampartRejectionReason.MissingMapInformation)]
    [InlineData("unknownOccupancy", WailingRampartRejectionReason.MissingMapInformation)]
    [InlineData("invalidCaster", WailingRampartRejectionReason.InvalidPosition)]
    public void EntireFootprintAndCasterAreValidatedBeforeAnyTilesArePlaced(string condition, WailingRampartRejectionReason reason)
    {
        var s = new Setup();
        var last = s.Tiles.Last();
        switch (condition)
        {
            case "invalid": s.Map.Valid = p => p != last; break;
            case "occupied": s.Map.Occupied = p => p == last; break;
            case "terrain": s.Map.Legal = p => p != last; break;
            case "unknownTerrain": s.Map.Legal = p => p == last ? null : true; break;
            case "unknownBounds": s.Map.Valid = p => p == last ? null : true; break;
            case "unknownOccupancy": s.Map.Occupied = p => p == last ? null : false; break;
            default: s.Map.Valid = p => p != s.Caster.TacticalPosition; break;
        }
        Assert.Equal(reason, s.Execute().Placement!.RejectionReason);
        s.AssertUnchanged();
    }

    [Fact]
    public void MissingMapAndCasterPositionRemainExplicitRejections()
    {
        var s = new Setup();
        Assert.Equal(WailingRampartRejectionReason.MissingMapInformation,
            s.Executor.Execute(s.Use, "wall", s.Tiles, null).Placement!.RejectionReason);
        var unplaced = CharacterFactory.Create(GraveboundColossus.Definition, "unplaced", 1);
        Assert.Equal(WailingRampartRejectionReason.MissingCasterPosition,
            s.Walls.ValidatePlacement(unplaced, "unplaced_wall", s.Tiles, 2, s.Map).RejectionReason);
        s.AssertUnchanged();
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("phase", AbilityRejectionReason.WrongPhase)]
    public void NormalEligibilityChecksRunBeforeEffects(string condition, AbilityRejectionReason reason)
    {
        var s = new Setup();
        switch (condition)
        {
            case "dead": s.Caster.TakeDamage(int.MaxValue); break;
            case "inactive": s.Manager.CompleteActivation(); break;
            case "cooldown": s.Caster.Cooldowns.RecordUse(GraveboundColossus.WailingRampartId, 1, 3); break;
            case "silence": s.Caster.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "phase": s.Lifecycle.AdvancePhase(); break;
        }
        var health = s.Caster.CurrentHealth;
        var cooldownCount = s.Caster.Cooldowns.Cooldowns.Count;
        Assert.Equal(reason, s.Execute().Eligibility.RejectionReason);
        Assert.Empty(s.Walls.ActiveAreas);
        Assert.Equal(cooldownCount, s.Caster.Cooldowns.Cooldowns.Count);
        Assert.Equal(health, s.Caster.CurrentHealth);
        Assert.False(s.Manager.MainActionUsed);
    }

    [Fact]
    public void OtherCharactersAndTemplatesWithoutAbilityAreRejected()
    {
        var s = new Setup();
        var outsider = CharacterFactory.Create(Nigel.Definition, "nigel", 1);
        Assert.Equal(WailingRampartRejectionReason.NotColossus,
            s.Walls.ValidatePlacement(outsider, "other", s.Tiles, 2, s.Map).RejectionReason);
        var noKit = new Character("without", new CharacterDefinition("gravebound_colossus", "Colossus", "",
            GraveboundColossus.Definition.BaseStats), 1);
        Assert.Equal(WailingRampartRejectionReason.AbilityNotOwned,
            s.Walls.ValidatePlacement(noKit, "no_kit", s.Tiles, 2, s.Map).RejectionReason);
        var enemyUse = new AbilityUse(s.Enemy, GraveboundColossus.WailingRampartId, s.Manager);
        Assert.Equal(AbilityRejectionReason.CharacterNotActive, s.Executor.Execute(enemyUse, "enemy", s.Tiles, s.Map).Eligibility.RejectionReason);
        s.AssertUnchanged();
    }

    [Fact]
    public void UnsupportedMetadataDoesNotCreateTerrainOrConsumeACost()
    {
        var changed = new AbilityDefinition(GraveboundColossus.WailingRampartId, "Wailing Rampart", "fixture",
            AbilityClassification.Active, false, AbilityActionType.UtilityAction,
            new(AbilityRangeKind.ManhattanDistance, 1, 4), 3);
        var template = new CharacterDefinition("gravebound_colossus", "Colossus", "", GraveboundColossus.Definition.BaseStats, new[] { changed });
        var s = new Setup(definition: template);
        Assert.Equal(WailingRampartRejectionReason.UnsupportedAbilityMetadata, s.Execute().Placement!.RejectionReason);
        Assert.False(s.Manager.UtilityActionUsed);
        s.AssertUnchanged();
    }

    [Fact]
    public void ExecuteRechecksOccupancyAndPositionInsteadOfTrustingPreview()
    {
        var s = new Setup();
        Assert.True(s.Preview().CanExecute);
        s.Map.Occupied = p => p == s.Tiles[1];
        Assert.Equal(WailingRampartRejectionReason.OccupiedTile, s.Execute().Placement!.RejectionReason);
        s.Map.Occupied = _ => false;
        s.Caster.SetTacticalPosition(new(13, 13));
        Assert.Equal(WailingRampartRejectionReason.OutsideRange, s.Execute().Placement!.RejectionReason);
        Assert.Empty(s.Walls.ActiveAreas);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.True(s.Manager.CanUseMainAction);
    }

    [Fact]
    public void WallsRestrictOnlyEnemyOrdinaryMovementAndDoNotChooseAPathOrMoveAnyone()
    {
        var s = new Setup();
        Assert.True(s.Execute().Succeeded);
        var tile = s.Tiles[0];
        Assert.True(s.Walls.CanEnter(s.Caster, tile));
        Assert.True(s.Walls.CanEnter(s.Ally, tile));
        Assert.False(s.Walls.CanEnter(s.Enemy, tile));
        Assert.True(s.Walls.CanEnter(s.Enemy, tile, qualifyingSpecialMovement: true));
        Assert.Null(s.Walls.CanEnter(s.Enemy, tile, qualifyingSpecialMovement: null));
        Assert.True(s.Walls.CanEnter(s.Enemy, new(0, 0)));
        s.AssertHealthAndPositions();
    }

    [Fact]
    public void ExpirationOccursOnlyAtEndOfFollowingRoundCleanupAndIsIdempotent()
    {
        var s = new Setup();
        var area = s.Execute().CreatedWall!;
        var snapshot = s.Walls.ActiveAreas;
        Assert.Empty(s.Walls.ExpireAtRoundEnd(2, RoundPhase.Cleanup));
        Assert.Throws<InvalidOperationException>(() => s.Walls.ExpireAtRoundEnd(3, RoundPhase.AlternatingActivations));
        Assert.False(s.Walls.CanEnter(s.Enemy, s.Tiles[0]));
        Assert.Same(area, Assert.Single(s.Walls.ExpireAtRoundEnd(3, RoundPhase.Cleanup)));
        Assert.Empty(s.Walls.ActiveAreas);
        Assert.Empty(s.Walls.ExpireAtRoundEnd(3, RoundPhase.Cleanup));
        Assert.True(s.Walls.CanEnter(s.Enemy, s.Tiles[0]));
        Assert.Single(snapshot);
        Assert.Equal(3, snapshot[0].ExpirationRound);
        Assert.Throws<NotSupportedException>(() => ((IList<WailingRampartArea>)snapshot).Clear());
    }

    [Fact]
    public void CasterDeathDoesNotCancelCreatedWallOrCooldown()
    {
        var s = new Setup();
        Assert.True(s.Execute().Succeeded);
        s.Caster.TakeDamage(int.MaxValue);
        Assert.Single(s.Walls.ActiveAreas);
        Assert.False(s.Walls.CanEnter(s.Enemy, s.Tiles[0]));
        Assert.False(s.Caster.Cooldowns.IsReady(GraveboundColossus.WailingRampartId, 4));
        Assert.Single(s.Walls.ExpireAtRoundEnd(3, RoundPhase.Cleanup));
    }

    [Fact]
    public void DuplicateIdsAndUnresolvedOverlapsPreserveExistingWallsAndOtherCastersBudgets()
    {
        var walls = new WailingRampartService();
        var a = new Setup(walls: walls);
        Assert.True(a.Execute().Succeeded);
        var b = new Setup(walls: walls, casterId: "colossus_b");
        Assert.Equal(WailingRampartRejectionReason.DuplicateEffectId, b.Execute().Placement!.RejectionReason);
        Assert.Equal(WailingRampartRejectionReason.OverlapUnresolved,
            b.Executor.Execute(b.Use, "other_wall", b.Tiles, b.Map).Placement!.RejectionReason);
        Assert.Single(walls.ActiveAreas);
        Assert.Empty(b.Caster.Cooldowns.Cooldowns);
        Assert.True(b.Manager.CanUseMainAction);
        b.Tiles = new[] { new GridPosition(5, 6), new(5, 7), new(5, 8) };
        Assert.True(b.Executor.Execute(b.Use, "other_wall", b.Tiles, b.Map).Succeeded);
        Assert.Equal(2, walls.ActiveAreas.Count);
        Assert.Equal(new[] { "colossus", "colossus_b" }, walls.ActiveAreas.Select(wall => wall.OwnerCharacterId));
        Assert.Equal(2, walls.ExpireAtRoundEnd(3, RoundPhase.Cleanup).Count);
    }

    [Fact]
    public void NullAndMalformedProgrammingInputsAreRejectedWithoutMutation()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => new WailingRampartAbilityExecutor(null!));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(null!, "wall", s.Tiles, s.Map));
        Assert.Throws<ArgumentNullException>(() => s.Executor.Execute(s.Use, "wall", null!, s.Map));
        foreach (var id in new[] { null, "", " " })
            Assert.ThrowsAny<ArgumentException>(() => s.Executor.Execute(s.Use, id!, s.Tiles, s.Map));
        Assert.Throws<ArgumentNullException>(() => s.Walls.ValidatePlacement(null!, "wall", s.Tiles, 2, s.Map));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Walls.ValidatePlacement(s.Caster, "wall", s.Tiles, 0, s.Map));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Walls.ExpireAtRoundEnd(0, RoundPhase.Cleanup));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Walls.ExpireAtRoundEnd(3, (RoundPhase)999));
        s.AssertUnchanged();
    }

    [Fact]
    public void RoundAndCoordinateOverflowCannotPartiallyApplyACast()
    {
        var s = new Setup(round: int.MaxValue);
        Assert.Throws<OverflowException>(() => s.Execute());
        s.AssertUnchanged();
        var extreme = new Setup();
        extreme.Caster.SetTacticalPosition(new(int.MinValue, int.MinValue));
        extreme.Tiles = new[] { new GridPosition(int.MaxValue - 2, int.MaxValue), new(int.MaxValue - 1, int.MaxValue), new(int.MaxValue, int.MaxValue) };
        Assert.Equal(WailingRampartRejectionReason.OutsideRange, extreme.Execute().Placement!.RejectionReason);
        Assert.Empty(extreme.Walls.ActiveAreas);
    }

    [Fact]
    public void CastingDoesNotChangeEffectiveDefenseResistanceShieldsOrStatuses()
    {
        var s = new Setup();
        s.Caster.TakeDamage(5);
        s.Caster.SetShieldHP(6);
        s.Caster.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        var defense = s.Caster.EffectiveDefense;
        var resistance = s.Caster.EffectiveResistance;
        Assert.True(s.Execute().Succeeded);
        Assert.Equal(59, s.Caster.CurrentHealth);
        Assert.Equal(6, s.Caster.ShieldHP);
        Assert.Equal(defense, s.Caster.EffectiveDefense);
        Assert.Equal(resistance, s.Caster.EffectiveResistance);
        Assert.Equal(2, s.Caster.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.Equal(22, s.Caster.BaseStats.Defense);
    }

    [Fact]
    public void RampartDoesNotSilentlyInventProjectileCollisionOrAlterOrdinaryLanceDamage()
    {
        var s = new Setup();
        var rolls = new FixedRolls();
        var strikes = new SingleStrikeResolver(new HitRoller(rolls));
        // Explicit fixture Power: the basic Lance's canonical Power remains unresolved.
        var before = strikes.ResolvePhysicalStrike(s.Caster.BaseStats, s.Enemy.BaseStats, 4,
            WeaponCategory.Lance, WeaponCategory.Sword, 100);
        Assert.True(s.Execute().Succeeded);
        Assert.Equal(1, rolls.Count); // Wall creation has no randomness dependency.
        var after = strikes.ResolvePhysicalStrike(s.Caster.BaseStats, s.Enemy.BaseStats, 4,
            WeaponCategory.Lance, WeaponCategory.Sword, 100);
        Assert.Equal(before.Damage, after.Damage);
        Assert.Equal(WeaponTriangleOutcome.Advantage, after.TriangleOutcome);
        s.Enemy.SetShieldHP(6);
        int actual = StrikeDamageApplicator.Apply(after, s.Enemy);
        Assert.Equal(Math.Max(0, after.Damage - 6), actual);
    }

    [Theory]
    [InlineData(GraveboundColossus.ImpalingSentinelId, AbilityRejectionReason.UnsupportedAbility)]
    [InlineData(GraveboundColossus.SanctuaryOfTheForgottenId, AbilityRejectionReason.NotManuallyActivatable)]
    public void RegistrationDoesNotExecuteTheOtherTwoAbilities(string abilityId, AbilityRejectionReason reason)
    {
        var s = new Setup();
        var use = new AbilityUse(s.Caster, abilityId, s.Manager);
        Assert.Equal(reason, s.Executor.Execute(use, "wall", s.Tiles, s.Map).Eligibility.RejectionReason);
        s.AssertUnchanged();
    }

    [Fact]
    public void AnActiveCharacterWithoutTheRegisteredAbilityCannotCast()
    {
        var s = new Setup(definition: Nigel.Definition);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute().Eligibility.RejectionReason);
        Assert.Empty(s.Walls.ActiveAreas);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.True(s.Manager.CanUseMainAction);
    }

    [Fact]
    public void CompletingAnotherMainActionLeavesNoOpportunityToCreateAWall()
    {
        var s = new Setup();
        s.Manager.CompleteMainAction();
        Assert.Equal(AbilityRejectionReason.CharacterNotActive, s.Execute().Eligibility.RejectionReason);
        Assert.True(s.Manager.MainActionUsed);
        Assert.True(s.Caster.HasActedThisRound);
        Assert.Empty(s.Walls.ActiveAreas);
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
    }

    [Fact]
    public void ACanonicalRoarPreventsRampartFromBypassingTheRequiredAttack()
    {
        var s = new Setup(enemyDefinition: Nigel.Definition);
        s.Manager.CompleteActivation();
        s.Caster.ResetActivation(); // Fixture begins with Nigel's activation before the Colossus acts.
        s.Manager.StartActivation(s.Enemy);
        var roar = new ProvokingRoarAbilityExecutor();
        var constraints = new AbilityTargetingConstraints(AbilityTargetType.Character,
            requiresMapValidation: false, requiresLineOfSight: false, requiresElevationRules: false);
        Assert.True(roar.Execute(new(s.Enemy, Nigel.ProvokingRoarId, s.Manager), constraints).Succeeded);
        var control = s.Manager.ActionRestrictions.GetActive(s.Caster.Id);
        Assert.NotNull(control);
        s.Manager.StartActivation(s.Caster);
        Assert.Equal(AbilityRejectionReason.ActionRestrictionUnresolved, s.Execute().Eligibility.RejectionReason);
        Assert.Same(control, s.Manager.ActionRestrictions.GetActive(s.Caster.Id));
        Assert.Empty(s.Caster.Cooldowns.Cooldowns);
        Assert.Empty(s.Walls.ActiveAreas);
        Assert.True(s.Manager.CanUseMainAction);
        Assert.Equal(TauntActionOutcome.MustAttackSource, s.Manager.EvaluateMainAction(s.Caster,
            new(false, requiredAttack: new(s.Enemy.Id, true, true, true))).Outcome);
    }

    [Fact]
    public void MapExceptionsCannotLeavePartialTerrainOrBookkeeping()
    {
        var s = new Setup();
        s.Map.Legal = p => p == s.Tiles.Last() ? throw new InvalidOperationException("Map unavailable") : true;
        Assert.Throws<InvalidOperationException>(() => s.Execute());
        s.AssertUnchanged();
    }

    [Fact]
    public void ExpiredIdsStayReservedAndNewIdsCanReuseVacatedTiles()
    {
        var walls = new WailingRampartService();
        var a = new Setup(walls: walls);
        Assert.True(a.Execute().Succeeded);
        walls.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        var b = new Setup(round: 4, walls: walls, casterId: "colossus_b");
        Assert.Equal(WailingRampartRejectionReason.DuplicateEffectId, b.Execute().Placement!.RejectionReason);
        Assert.True(b.Executor.Execute(b.Use, "fresh_wall", b.Tiles, b.Map).Succeeded);
        Assert.Equal(5, Assert.Single(walls.ActiveAreas).ExpirationRound);
        Assert.Empty(walls.ExpireAtRoundEnd(4, RoundPhase.Cleanup));
    }

    private sealed class FixedRolls : IRandomRollProvider
    {
        public int Count { get; private set; }
        public int NextRoll() { Count++; return 0; }
    }

    private sealed class Map : IWailingRampartMap
    {
        public Func<GridPosition, bool?> Valid { get; set; } = p => p.X >= 0 && p.X < 14 && p.Y >= 0 && p.Y < 14;
        public Func<GridPosition, bool?> Occupied { get; set; } = _ => false;
        public Func<GridPosition, bool?> Legal { get; set; } = _ => true;
        public bool? IsValidPosition(GridPosition p) => Valid(p);
        public bool? IsOccupied(GridPosition p) => Occupied(p);
        public bool? CanPlaceWall(Character caster, GridPosition p) => Legal(p);
    }

    private sealed class Setup
    {
        public Character Caster { get; }
        public Character Ally { get; } = CharacterFactory.Create(GentleGuardian.Definition, "ally", 1);
        public Character Enemy { get; }
        public RoundLifecycleManager Lifecycle { get; }
        public ActivationManager Manager { get; }
        public AbilityUse Use { get; }
        public WailingRampartService Walls { get; }
        public WailingRampartAbilityExecutor Executor { get; }
        public Map Map { get; } = new();
        public GridPosition[] Tiles { get; set; } = { new(6, 5), new(7, 5), new(8, 5) };

        public Setup(int round = 2, CharacterDefinition? definition = null, WailingRampartService? walls = null,
            string casterId = "colossus", CharacterDefinition? enemyDefinition = null)
        {
            Caster = CharacterFactory.Create(definition ?? GraveboundColossus.Definition, casterId, 1);
            Enemy = CharacterFactory.Create(enemyDefinition ?? HoodedAssassin.Definition, "enemy", 2);
            Caster.SetTacticalPosition(new(5, 5));
            Ally.SetTacticalPosition(new(4, 5));
            Enemy.SetTacticalPosition(new(5, 4));
            Lifecycle = new(round);
            while (Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) Lifecycle.AdvancePhase();
            Manager = new(new[] { Caster, Ally }, new[] { Enemy }, 1, Lifecycle);
            Manager.StartActivation(Caster);
            Use = new(Caster, GraveboundColossus.WailingRampartId, Manager);
            Walls = walls ?? new();
            Executor = new(Walls);
        }

        public WailingRampartUseResult Preview() => Executor.Preview(Use, "wall", Tiles, Map);
        public WailingRampartUseResult Execute() => Executor.Execute(Use, "wall", Tiles, Map);
        public void AssertUnchanged()
        {
            Assert.Empty(Walls.ActiveAreas);
            Assert.Empty(Caster.Cooldowns.Cooldowns);
            Assert.True(Manager.CanUseMainAction);
            Assert.False(Manager.MainActionUsed);
            Assert.False(Caster.HasActedThisRound);
            AssertHealthAndPositions();
        }
        public void AssertHealthAndPositions()
        {
            Assert.Equal(64, Caster.CurrentHealth);
            Assert.Equal(0, Caster.ShieldHP);
            Assert.Equal(0, Caster.HealsReceivedThisRound);
            Assert.Empty(Caster.Statuses.TimedStatuses);
            Assert.Equal(new GridPosition(5, 5), Caster.TacticalPosition);
            Assert.Equal(new GridPosition(4, 5), Ally.TacticalPosition);
            Assert.Equal(new GridPosition(5, 4), Enemy.TacticalPosition);
            Assert.Equal(38, Enemy.CurrentHealth);
        }
    }
}
