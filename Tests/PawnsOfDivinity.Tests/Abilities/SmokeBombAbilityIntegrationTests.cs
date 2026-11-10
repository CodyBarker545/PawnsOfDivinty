using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class SmokeBombAbilityIntegrationTests
{
    [Fact]
    public void ValidCast_CreatesCenteredFootprintAndFinalizesMainActionAtRoundTwo()
    {
        var s = new Setup();
        var checkedTiles = new List<GridPosition>();
        var result = s.Execute(s.Use, "smoke-1", tile => { checkedTiles.Add(tile); return true; });

        Assert.True(result.Succeeded);
        Assert.Equal(s.Actor.Id, result.CasterId);
        Assert.Equal("smoke_bomb", result.AbilityId);
        Assert.Equal(2, result.RoundUsed);
        Assert.Equal("smoke-1", result.CreatedEffectId);
        Assert.Equal(new GridPosition(7, 7), result.Center);
        Assert.Equal(Square(new(7, 7)), result.CoveredTiles);
        Assert.Equal(result.CoveredTiles, checkedTiles);
        var area = Assert.Single(s.Smoke.ActiveAreas);
        Assert.Same(area, result.CreatedSmoke);
        Assert.Equal(s.Actor.Id, area.OwnerCharacterId);
        Assert.Equal(2, area.CreationRound);
        Assert.Equal(3, area.ExpirationRound);
        Assert.Equal(new GridPosition(8, 7), s.Actor.TacticalPosition); // The cast completes its teleport before finalization.
        Assert.Equal(new GridPosition(7, 7), result.Teleport!.OldPosition);
        Assert.Equal(new GridPosition(8, 7), result.Teleport.NewPosition);
        Assert.Equal(s.Actor.Id, result.Teleport.Validation.CharacterId);
        Assert.True(result.Teleport.Visibility!.IsInvisible);
        Assert.False(s.Activations.MovementUsed);
        Assert.False(s.Activations.UtilityActionUsed);
        Assert.True(s.Activations.MainActionUsed);
        Assert.True(s.Actor.HasActedThisRound);
        Assert.Null(s.Activations.ActiveCharacter);
        Assert.False(s.Activations.CanMove);
        Assert.False(s.Activations.CanUseUtilityAction);
        Assert.Equal(5, result.Finalization!.ReadyRound);
        var cooldown = s.Actor.Cooldowns.Cooldowns["smoke_bomb"];
        Assert.Equal(2, cooldown.RoundUsed);
        Assert.Equal(3, cooldown.ReuseInterval);
        Assert.Equal(5, cooldown.ReadyRound);
        Assert.Single(s.Actor.Cooldowns.Cooldowns);
        Assert.False(s.Actor.Cooldowns.IsReady("smoke_bomb", 4));
        Assert.True(s.Actor.Cooldowns.IsReady("smoke_bomb", 5));
    }

    [Fact]
    public void Preview_ValidatesAllTilesWithoutReservingIdentityOrChangingState()
    {
        var s = new Setup();
        var seen = new List<GridPosition>();
        var preview = s.Preview(s.Use, "preview-id", tile => { seen.Add(tile); return true; });
        Assert.True(preview.CanExecute);
        Assert.False(preview.Succeeded);
        Assert.Null(preview.CreatedSmoke);
        Assert.Null(preview.CreatedEffectId);
        Assert.Null(preview.Finalization);
        Assert.Equal(Square(new(7, 7)), seen);
        Assert.Equal(preview.CoveredTiles, seen);
        s.AssertUntouched();
        Assert.False(s.Visibility().IsInSmoke);
        Assert.True(s.Execute(s.Use, "preview-id", _ => true).Succeeded);
    }

    [Fact]
    public void InvalidTile_RejectsWholePlacementAndLeavesExistingCloudIntact()
    {
        var s = new Setup();
        var existing = s.Smoke.Place("existing", "other-owner", new(7, 7), 2, _ => true);
        var rejected = new GridPosition(9, 9);
        var result = s.Execute(s.Use, "new", p => p != rejected);
        Assert.False(result.Succeeded);
        Assert.Equal(SmokePlacementRejectionReason.IllegalTile, result.Placement!.RejectionReason);
        Assert.Equal(rejected, result.Placement.RejectedTile);
        Assert.Same(existing, Assert.Single(s.Smoke.ActiveAreas));
        Assert.False(s.Smoke.IsActive("new"));
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
        Assert.False(s.Actor.SmokeReveal.RevealedByAttack);
        // Rejected placement did not reserve its ID.
        Assert.True(s.Execute(s.Use, "new", _ => true).Succeeded);
    }

    [Fact]
    public void Execute_RechecksMapAfterPreview()
    {
        var s = new Setup();
        bool mapAllows = true;
        Assert.True(s.Preview(s.Use, "smoke", _ => mapAllows).CanExecute);
        mapAllows = false;
        var result = s.Execute(s.Use, "smoke", _ => mapAllows);
        Assert.Equal(SmokePlacementRejectionReason.IllegalTile, result.Placement!.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void Execute_RechecksCurrentCenterInsteadOfReusingPreviewPosition()
    {
        var s = new Setup();
        var preview = s.Preview(s.Use, "smoke", _ => true);
        s.Actor.SetTacticalPosition(new(10, 10));
        var result = s.Execute(s.Use, "smoke", _ => true);
        Assert.True(result.Succeeded);
        Assert.Equal(new GridPosition(7, 7), preview.Center);
        Assert.Equal(new GridPosition(10, 10), result.Center);
        Assert.Equal(Square(new(10, 10)), result.CoveredTiles);
        Assert.Equal(new GridPosition(11, 10), s.Actor.TacticalPosition);
    }

    [Fact]
    public void Execute_RechecksCooldownAfterPreview()
    {
        var s = new Setup();
        Assert.True(s.Preview(s.Use, "smoke", _ => true).CanExecute);
        s.Actor.Cooldowns.RecordUse("smoke_bomb", 2, 3);
        var result = s.Execute(s.Use, "smoke", _ => true);
        Assert.Equal(AbilityRejectionReason.OnCooldown, result.Eligibility.RejectionReason);
        Assert.Empty(s.Smoke.ActiveAreas);
        Assert.True(s.Activations.CanUseMainAction);
        Assert.Equal(5, s.Actor.Cooldowns.Cooldowns["smoke_bomb"].ReadyRound);
    }

    [Fact]
    public void DuplicateIdIncludingExpiredIds_IsRejectedWithoutBookkeeping()
    {
        var s = new Setup();
        s.Smoke.Place("duplicate", "other", new(2, 2), 1, _ => true);
        var duplicate = s.Execute(s.Use, "duplicate", _ => throw new Exception("Duplicate IDs should fail before map queries."));
        Assert.Equal(SmokePlacementRejectionReason.DuplicateEffectId, duplicate.Placement!.RejectionReason);
        Assert.Single(s.Smoke.ActiveAreas);
        s.Smoke.ExpireAtRoundEnd(2, RoundPhase.Cleanup);
        var expiredId = s.Execute(s.Use, "duplicate", _ => true);
        Assert.Equal(SmokePlacementRejectionReason.DuplicateEffectId, expiredId.Placement!.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void IdentityClaimedSincePreview_IsRechecked()
    {
        var s = new Setup();
        Assert.True(s.Preview(s.Use, "claimed", _ => true).CanExecute);
        var other = s.Smoke.Place("claimed", "other", new(2, 2), 2, _ => true);
        var result = s.Execute(s.Use, "claimed", _ => true);
        Assert.Equal(SmokePlacementRejectionReason.DuplicateEffectId, result.Placement!.RejectionReason);
        Assert.Same(other, Assert.Single(s.Smoke.ActiveAreas));
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
    }

    [Theory]
    [InlineData("dead", AbilityRejectionReason.CharacterDefeated)]
    [InlineData("inactive", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("ended", AbilityRejectionReason.CharacterNotActive)]
    [InlineData("usedBudget", AbilityRejectionReason.ActionUnavailable)]
    [InlineData("cooldown", AbilityRejectionReason.OnCooldown)]
    [InlineData("silence", AbilityRejectionReason.MetadataUnresolved)]
    [InlineData("unknownAbility", AbilityRejectionReason.AbilityNotOwned)]
    [InlineData("shadowStrike", AbilityRejectionReason.UnsupportedAbility)]
    [InlineData("passive", AbilityRejectionReason.NotManuallyActivatable)]
    public void IneligibleAbilityUse_RejectsBeforePlacementQueries(string invalid, AbilityRejectionReason expected)
    {
        var s = new Setup();
        switch (invalid)
        {
            case "dead": s.Actor.TakeDamage(int.MaxValue); break;
            case "inactive": s.Activations.CompleteActivation(); break;
            case "ended": s.Activations.CompleteMainAction(); break;
            case "usedBudget": s.Actor.EndActivation(); break;
            case "cooldown": s.Actor.Cooldowns.RecordUse("smoke_bomb", 2, 3); break;
            case "silence": s.Actor.Statuses.ApplyTimedStatus(StatusType.Silence, 1); break;
            case "unknownAbility": s.Use = new(s.Actor, "missing", s.Activations); break;
            case "shadowStrike": s.Use = new(s.Actor, "shadow_strike", s.Activations); break;
            case "passive": s.Use = new(s.Actor, "smoke_veil", s.Activations); break;
        }
        int hp = s.Actor.CurrentHealth;
        var result = s.Execute(s.Use, "smoke", _ => throw new Exception("Ineligible requests must not query placement."));
        Assert.Equal(expected, result.Eligibility.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Empty(s.Smoke.ActiveAreas);
        Assert.Equal(hp, s.Actor.CurrentHealth);
        Assert.Equal(invalid == "cooldown" ? 1 : 0, s.Actor.Cooldowns.Cooldowns.Count);
        Assert.False(s.Actor.SmokeReveal.RevealedByAttack);
    }

    [Fact]
    public void WrongCharacter_DoesNotOwnRegisteredSmokeBomb()
    {
        var s = new Setup(actor: CharacterFactory.Create("nigel", 1)!);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute(s.Use, "smoke", _ => true).Eligibility.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void CopiedAbilityOnAnotherCharacter_DoesNotMakeThatCharacterAnAssassin()
    {
        var original = CharacterRegistry.GetCharacter("nigel")!;
        var actor = CharacterFactory.Create(new CharacterDefinition(original.Id, original.Name, original.Description,
            original.BaseStats, HoodedAssassin.Definition.Abilities), "copy", 1);
        var s = new Setup(actor: actor);
        var result = s.Execute(s.Use, "smoke", _ => true);
        Assert.Equal(SmokeBombRejectionReason.NotAssassin, result.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void UnassignedAbilityCollection_DoesNotGainSmokeBombByCharacterIdAlone()
    {
        var template = HoodedAssassin.Definition;
        var actor = CharacterFactory.Create(new CharacterDefinition(template.Id, template.Name, template.Description, template.BaseStats), "no-abilities", 1);
        var s = new Setup(actor: actor);
        Assert.Equal(AbilityRejectionReason.AbilityNotOwned, s.Execute(s.Use, "smoke", _ => true).Eligibility.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void MissingPosition_IsRejectedWithoutQueryingMap()
    {
        var s = new Setup(setPosition: false);
        var result = s.Execute(s.Use, "smoke", _ => throw new Exception("No center is available."));
        Assert.Equal(SmokeBombRejectionReason.MissingPosition, result.RejectionReason);
        s.AssertUntouched();
    }

    [Theory]
    [InlineData(RoundPhase.Shop)]
    [InlineData(RoundPhase.Preparation)]
    [InlineData(RoundPhase.Cleanup)]
    public void WrongLifecyclePhase_IsRejected(RoundPhase phase)
    {
        var s = new Setup(phase: phase);
        var result = s.Execute(s.Use, "smoke", _ => true);
        Assert.Equal(AbilityRejectionReason.WrongPhase, result.Eligibility.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void UseFromAnEarlierRound_IsRejected()
    {
        var s = new Setup();
        while (s.Lifecycle.CurrentPhase != RoundPhase.ObjectiveScoring) s.Lifecycle.AdvancePhase();
        s.Lifecycle.StartNextRound();
        while (s.Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) s.Lifecycle.AdvancePhase();
        var result = s.Execute(s.Use, "smoke", _ => true);
        Assert.Equal(AbilityRejectionReason.UseContextChanged, result.Eligibility.RejectionReason);
        s.AssertUntouched();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void InvalidEffectIds_ThrowBeforeMutation(string? id)
    {
        var s = new Setup();
        Assert.ThrowsAny<ArgumentException>(() => s.Execute(s.Use, id!, _ => true));
        s.AssertUntouched();
    }

    [Fact]
    public void MalformedProgrammingInputs_ThrowWithoutMutation()
    {
        var s = new Setup();
        Assert.Throws<ArgumentNullException>(() => new SmokeBombAbilityExecutor(null!));
        Assert.Throws<ArgumentNullException>(() => s.Preview(null!, "id", _ => true));
        Assert.Throws<ArgumentNullException>(() => s.Execute(s.Use, "id", null!));
        s.AssertUntouched();
    }

    [Theory]
    [InlineData("cooldown")]
    [InlineData("coordinates")]
    public void ArithmeticOverflow_IsRejectedBeforePlacementOrBookkeeping(string invalid)
    {
        var s = new Setup(round: invalid == "cooldown" ? int.MaxValue - 2 : 2);
        if (invalid == "coordinates") s.Actor.SetTacticalPosition(new(int.MaxValue, 7));
        Assert.Throws<OverflowException>(() => s.Execute(s.Use, "smoke", _ => true));
        s.AssertUntouched();
    }

    [Fact]
    public void MapQueryException_LeavesStateUntouched()
    {
        var s = new Setup();
        Assert.Throws<InvalidOperationException>(() => s.Execute(s.Use, "smoke", _ => throw new InvalidOperationException("Map unavailable")));
        s.AssertUntouched();
    }

    [Theory]
    [InlineData("action")]
    [InlineData("reuse")]
    [InlineData("range")]
    public void RequiredUnknownMetadata_RemainsAnExplicitRejection(string missing)
    {
        var ability = new AbilityDefinition("smoke_bomb", "Smoke Bomb", "Test-only unresolved metadata",
            AbilityClassification.Active, requiredAction: missing == "action" ? null : AbilityActionType.MainAction,
            range: missing == "range" ? null : new(AbilityRangeKind.ManhattanDistance, 0, 0),
            reuseInterval: missing == "reuse" ? null : 3);
        var original = HoodedAssassin.Definition;
        var actor = CharacterFactory.Create(new CharacterDefinition(original.Id, original.Name, original.Description,
            original.BaseStats, new[] { ability }), "unresolved", 1);
        var s = new Setup(actor: actor);
        var result = s.Execute(s.Use, "smoke", _ => true);
        Assert.False(result.CanExecute);
        if (missing == "range") Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, result.Targeting!.RejectionReason);
        else Assert.Equal(AbilityRejectionReason.MetadataUnresolved, result.Eligibility.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void Casting_MakesCoverageAvailableWithoutAssigningInvisibility()
    {
        var s = new Setup();
        Assert.False(s.Visibility().IsInvisible);
        Assert.Equal(0, SmokeVeil.GetEvasionModifier(s.Actor, new(7, 7), s.Smoke));
        Assert.True(s.Execute(s.Use, "smoke", _ => true).Succeeded);
        Assert.True(s.Visibility().IsInvisible);
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(s.Actor, new(7, 7), s.Smoke));
        Assert.False(s.Actor.SmokeReveal.RevealedByAttack);
    }

    [Theory]
    [InlineData("adjacency")]
    [InlineData("attackReveal")]
    public void Casting_PreservesExistingRevealRulesAndSmokeVeil(string reveal)
    {
        var s = new Setup();
        if (reveal == "adjacency") s.Enemy.SetTacticalPosition(new(9, 7));
        else s.Actor.SmokeReveal.RecordAttack();
        Assert.True(s.Execute(s.Use, "smoke", _ => true).Succeeded);
        var visibility = s.Visibility();
        Assert.False(visibility.IsInvisible);
        Assert.Equal(reveal == "adjacency", visibility.EnemyAdjacent);
        Assert.Equal(reveal == "attackReveal", s.Actor.SmokeReveal.RevealedByAttack);
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(s.Actor, new(7, 7), s.Smoke));
    }

    [Fact]
    public void OwnerDeath_DoesNotRemoveSmoke_AndCleanupRemainsExplicit()
    {
        var s = new Setup();
        var result = s.Execute(s.Use, "smoke", _ => true);
        s.Actor.TakeDamage(int.MaxValue);
        Assert.True(s.Smoke.IsActive("smoke"));
        Assert.True(s.Smoke.IsCovered(new(7, 7)));
        Assert.Empty(s.Smoke.ExpireAtRoundEnd(2, RoundPhase.Cleanup).ExpiredAreas);
        Assert.True(s.Smoke.IsActive("smoke"));
        var expired = s.Smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Single(expired.ExpiredAreas);
        Assert.Empty(s.Smoke.ActiveAreas);
        Assert.True(result.CreatedSmoke!.IsActive); // A historical snapshot stays unchanged.
        Assert.Equal(25, result.CoveredTiles.Count);
        Assert.Equal(5, s.Actor.Cooldowns.Cooldowns["smoke_bomb"].ReadyRound);
    }

    [Fact]
    public void IndependentAssassins_CanCreateOverlappingCloudsWithoutStackingEvasion()
    {
        var smoke = new SmokePlacementService();
        var first = new Setup(smoke: smoke);
        var second = new Setup(smoke: smoke);
        Assert.True(first.Execute(first.Use, "first", _ => true).Succeeded);
        Assert.Empty(second.Actor.Cooldowns.Cooldowns);
        Assert.True(second.Execute(second.Use, "second", _ => true).Succeeded);
        Assert.Equal(2, smoke.ActiveAreas.Count);
        Assert.Equal(new[] { "first", "second" }, smoke.GetCoveringAreas(new(7, 7)).Select(a => a.Id));
        Assert.Equal(new[] { first.Actor.Id, second.Actor.Id }, smoke.GetOwnersAt(new(7, 7)));
        Assert.Equal(25, smoke.GetCoveredPositions().Count);
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(first.Actor, new(7, 7), smoke));
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(second.Actor, new(7, 7), smoke));
    }

    [Fact]
    public void RepeatedExecutionAndFinalization_DoNotDuplicateSmokeOrBookkeeping()
    {
        var s = new Setup();
        Assert.True(s.Execute(s.Use, "first", _ => true).Succeeded);
        var duplicate = s.Execute(s.Use, "second", _ => true);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, duplicate.Eligibility.RejectionReason);
        Assert.Equal(AbilityRejectionReason.AlreadyFinalized, AbilityUseFinalizer.FinalizeSuccessfulUse(s.Use).Eligibility.RejectionReason);
        Assert.Equal("first", Assert.Single(s.Smoke.ActiveAreas).Id);
        Assert.Single(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void Cast_PreservesHealthShieldsStatusesHistoryAndStats_WhileTeleporting()
    {
        var s = new Setup();
        s.Actor.TakeDamage(5);
        s.Actor.SetShieldHP(6);
        s.Actor.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        s.Actor.Statuses.ApplyPoison();
        HealingApplicator.Apply(0, s.Actor);
        var stats = s.Actor.BaseStats;
        var result = s.Execute(s.Use, "smoke", _ => true);
        Assert.True(result.Succeeded);
        Assert.Equal(33, s.Actor.CurrentHealth);
        Assert.Equal(6, s.Actor.ShieldHP);
        Assert.Equal(2, s.Actor.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(s.Actor.Statuses.IsPoisoned);
        Assert.Equal(0, s.Actor.Statuses.PoisonTicksOccurred);
        Assert.Equal(1, s.Actor.HealsReceivedThisRound);
        Assert.Same(stats, s.Actor.BaseStats);
        Assert.Equal(new GridPosition(8, 7), s.Actor.TacticalPosition);
        Assert.Equal(52, s.Enemy.CurrentHealth);
        Assert.False(s.Enemy.HasActedThisRound);
    }

    [Fact]
    public void Results_AreImmutableSnapshots_AndTileOrderIsDeterministic()
    {
        var s = new Setup();
        var first = s.Preview(s.Use, "smoke", _ => true);
        var second = s.Preview(s.Use, "smoke", _ => true);
        Assert.Equal(first.CoveredTiles, second.CoveredTiles);
        var result = s.Execute(s.Use, "smoke", _ => true);
        Assert.Throws<NotSupportedException>(() => ((IList<GridPosition>)result.CoveredTiles).Clear());
        Assert.All(typeof(SmokeBombUseResult).GetProperties(), p => Assert.Null(p.SetMethod));
        // The placement result's service context is internal; all public properties are immutable values/snapshots.
        Assert.All(typeof(SmokePlacementValidationResult).GetProperties(), p => Assert.Null(p.SetMethod));
        s.Actor.SetTacticalPosition(new(1, 1));
        s.Smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Equal(new GridPosition(7, 7), result.Center);
        Assert.Equal(25, result.CoveredTiles.Count);
        Assert.Equal("smoke", result.CreatedEffectId);
    }

    [Fact]
    public void PlacementOnlyOverloads_RequireTeleportAndDoNotCast()
    {
        var s = new Setup();
        var preview = s.Executor.Preview(s.Use, "placement-only", _ => true);
        var execution = s.Executor.Execute(s.Use, "placement-only", _ => true);
        Assert.Equal(SmokeBombRejectionReason.TeleportRequired, preview.RejectionReason);
        Assert.Equal(SmokeBombRejectionReason.TeleportRequired, execution.RejectionReason);
        Assert.False(preview.CanExecute);
        Assert.False(execution.Succeeded);
        Assert.Null(execution.Teleport);
        s.AssertUntouched();
    }

    [Fact]
    public void ProposedSmoke_EnablesPreviewWithoutCreatingCoverageOrReservingIdentity()
    {
        var s = new Setup();
        var preview = s.Preview(s.Use, "proposed", _ => true);
        Assert.True(preview.CanExecute);
        Assert.True(preview.TeleportValidation!.IsLegal);
        Assert.Equal(new GridPosition(8, 7), preview.TeleportValidation.DestinationPosition);
        Assert.False(s.Smoke.IsCovered(new(8, 7)));
        Assert.False(s.Smoke.IsActive("proposed"));
        Assert.Equal(new GridPosition(7, 7), s.Actor.TacticalPosition);
        Assert.Null(preview.Teleport);
        Assert.False(s.Visibility().IsInvisible);
        s.AssertUntouched();
        Assert.True(s.Execute(s.Use, "proposed", _ => true).Succeeded);
    }

    [Theory]
    [InlineData("occupied", SmokeTeleportRejectionReason.DestinationOccupied)]
    [InlineData("illegal", SmokeTeleportRejectionReason.IllegalDestination)]
    [InlineData("invalidSource", SmokeTeleportRejectionReason.InvalidSourcePosition)]
    [InlineData("invalidDestination", SmokeTeleportRejectionReason.InvalidDestinationPosition)]
    [InlineData("unknownMap", SmokeTeleportRejectionReason.MissingMapInformation)]
    public void TeleportValidationFailure_DoesNotPartiallyPlaceTheNewCloud(string invalid,
        SmokeTeleportRejectionReason reason)
    {
        var s = new Setup();
        switch (invalid)
        {
            case "occupied": s.Map.Occupied = true; break;
            case "illegal": s.Map.Legal = false; break;
            case "invalidSource": s.Map.Valid = p => p != new GridPosition(7, 7); break;
            case "invalidDestination": s.Map.Valid = p => p != new GridPosition(8, 7); break;
            case "unknownMap": s.Map.Legal = null; break;
        }
        var result = s.Execute(s.Use, "new", _ => true);
        Assert.Equal(reason, result.TeleportValidation!.RejectionReason);
        Assert.False(result.Succeeded);
        Assert.Equal(new GridPosition(7, 7), s.Actor.TacticalPosition);
        s.AssertUntouched();
        // Failed teleport validation must not reserve the effect ID either.
        s.Map.Valid = _ => true;
        s.Map.Occupied = false;
        s.Map.Legal = true;
        Assert.True(s.Execute(s.Use, "new", _ => true).Succeeded);
    }

    [Fact]
    public void MissingDestinationAndMap_AreExplicitFailuresWithoutMutation()
    {
        var s = new Setup();
        var missingDestination = s.Executor.Execute(s.Use, "new", _ => true, null, s.Map,
            Array.Empty<(Character, GridPosition)>());
        Assert.Equal(SmokeTeleportRejectionReason.MissingDestinationPosition, missingDestination.TeleportValidation!.RejectionReason);
        var missingMap = s.Executor.Execute(s.Use, "new", _ => true, new(8, 7), null,
            Array.Empty<(Character, GridPosition)>());
        Assert.Equal(SmokeTeleportRejectionReason.MissingMapInformation, missingMap.TeleportValidation!.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void Execute_RevalidatesDestinationOccupancyAfterSuccessfulCombinedPreview()
    {
        var s = new Setup();
        var preview = s.Preview(s.Use, "new", _ => true);
        Assert.True(preview.CanExecute);
        s.Map.Occupied = true;
        var result = s.Execute(s.Use, "new", _ => true);
        Assert.Equal(SmokeTeleportRejectionReason.DestinationOccupied, result.TeleportValidation!.RejectionReason);
        Assert.True(preview.TeleportValidation!.IsLegal);
        s.AssertUntouched();
        Assert.Equal(new GridPosition(7, 7), s.Actor.TacticalPosition);
    }

    [Fact]
    public void ChangedSource_IsCheckedAgainstCurrentMapInsteadOfTrustingOldPreview()
    {
        var s = new Setup();
        var preview = s.Preview(s.Use, "new", _ => true);
        s.Actor.SetTacticalPosition(new(10, 10));
        s.Map.Valid = p => p != new GridPosition(10, 10);
        var result = s.Execute(s.Use, "new", _ => true);
        Assert.Equal(SmokeTeleportRejectionReason.InvalidSourcePosition, result.TeleportValidation!.RejectionReason);
        Assert.Equal(new GridPosition(7, 7), preview.Center);
        Assert.Equal(new GridPosition(10, 10), s.Actor.TacticalPosition);
        s.AssertUntouched();
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("nullCharacter")]
    [InlineData("nullRoster")]
    public void InvalidVisibilityRoster_IsRejectedBeforeCloudOrPositionChanges(string invalid)
    {
        var s = new Setup();
        IEnumerable<(Character, GridPosition)> roster = invalid switch
        {
            "duplicate" => new[] { (s.Enemy, new GridPosition(12, 12)), (s.Enemy, new GridPosition(12, 12)) },
            "nullCharacter" => new[] { ((Character)null!, new GridPosition(12, 12)) },
            _ => null!
        };
        Assert.ThrowsAny<ArgumentException>(() => s.Executor.Execute(s.Use, "new", _ => true, new(8, 7), s.Map, roster));
        Assert.Equal(new GridPosition(7, 7), s.Actor.TacticalPosition);
        s.AssertUntouched();
    }

    [Fact]
    public void MapAndVisibilityQueries_FinishBeforeAnyEffectOrBookkeepingMutation()
    {
        var s = new Setup();
        int queries = 0;
        bool Unchanged()
        {
            Assert.Empty(s.Smoke.ActiveAreas);
            Assert.Equal(new GridPosition(7, 7), s.Actor.TacticalPosition);
            Assert.Same(s.Actor, s.Activations.ActiveCharacter);
            Assert.True(s.Activations.CanUseMainAction);
            Assert.Empty(s.Actor.Cooldowns.Cooldowns);
            queries++;
            return true;
        }
        s.Map.Valid = _ => Unchanged();
        IEnumerable<(Character, GridPosition)> Roster()
        {
            Unchanged();
            yield return (s.Enemy, s.Enemy.TacticalPosition!.Value);
        }
        var actorReference = s.Actor;
        var result = s.Executor.Execute(s.Use, "new", _ => Unchanged(), new(8, 7), s.Map, Roster());
        Assert.True(result.Succeeded);
        Assert.True(queries >= 28); // 25 footprint tiles, two map positions and the roster.
        Assert.Same(actorReference, s.Actor);
        Assert.Equal(new GridPosition(8, 7), actorReference.TacticalPosition);
    }

    [Fact]
    public void OutsideNewCloud_RequiresCrossCloudDecisionEvenWhenAnOldOwnCloudCoversBothPositions()
    {
        var s = new Setup();
        var old = s.Smoke.Place("old", s.Actor.Id, new(8, 7), 2, _ => true);
        // New footprint ends at X=9. Old footprint extends to X=10 and also covers the caster.
        var result = s.Executor.Execute(s.Use, "new", _ => true, new(10, 7), s.Map,
            Array.Empty<(Character, GridPosition)>());
        Assert.Equal(SmokeTeleportRejectionReason.UnresolvedCrossAreaRule, result.TeleportValidation!.RejectionReason);
        Assert.Same(old, Assert.Single(s.Smoke.ActiveAreas));
        Assert.Equal(new GridPosition(7, 7), s.Actor.TacticalPosition);
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
    }

    [Theory]
    [InlineData("crossCloud", SmokeTeleportRejectionReason.UnresolvedCrossAreaRule)]
    [InlineData("distance", SmokeTeleportRejectionReason.UnresolvedDistanceLimit)]
    [InlineData("owner", SmokeTeleportRejectionReason.UnresolvedOwnerRule)]
    public void UnresolvedExistingCloudAccess_DoesNotCommitTheProposedCloud(string unresolved,
        SmokeTeleportRejectionReason reason)
    {
        var s = new Setup();
        var old = s.Smoke.Place("old", s.Enemy.Id, new(20, 20), 2, _ => true);
        var constraints = unresolved switch
        {
            "distance" => new SmokeTeleportConstraints(allowCrossArea: true),
            "owner" => new SmokeTeleportConstraints(allowCrossArea: true, maximumDistance: 30),
            _ => null
        };
        var result = s.Executor.Execute(s.Use, "new", _ => true, new(20, 20), s.Map,
            Array.Empty<(Character, GridPosition)>(), constraints);
        Assert.Equal(reason, result.TeleportValidation!.RejectionReason);
        Assert.Same(old, Assert.Single(s.Smoke.ActiveAreas));
        Assert.Empty(s.Actor.Cooldowns.Cooldowns);
        Assert.True(s.Activations.CanUseMainAction);
        Assert.Equal(new GridPosition(7, 7), s.Actor.TacticalPosition);
    }

    [Fact]
    public void ExplicitPrototypeExistingCloudPermissions_DoNotBecomeAnExtraActionOrCooldown()
    {
        var s = new Setup();
        var old = s.Smoke.Place("old", s.Enemy.Id, new(20, 20), 2, _ => true);
        // These caller-supplied examples exercise unresolved permissions, not canonical Smoke Bomb rules.
        var constraints = new SmokeTeleportConstraints(allowCrossArea: true, allowOtherOwners: true, maximumDistance: 30);
        var result = s.Executor.Execute(s.Use, "new", _ => true, new(20, 20), s.Map,
            Array.Empty<(Character, GridPosition)>(), constraints);
        Assert.True(result.Succeeded);
        Assert.Equal(new GridPosition(20, 20), s.Actor.TacticalPosition);
        Assert.Equal(new GridPosition(7, 7), result.Center);
        Assert.Equal(2, s.Smoke.ActiveAreas.Count);
        Assert.Contains(old, s.Smoke.ActiveAreas);
        Assert.Equal(5, Assert.Single(s.Actor.Cooldowns.Cooldowns).Value.ReadyRound);
        Assert.False(s.Activations.MovementUsed);
        Assert.False(s.Activations.UtilityActionUsed);
    }

    [Fact]
    public void ExpiredExistingCloud_CannotSupplyCoverageForAnOutsideDestination()
    {
        var s = new Setup();
        s.Smoke.Place("expired", s.Actor.Id, new(20, 20), 1, _ => true);
        s.Smoke.ExpireAtRoundEnd(2, RoundPhase.Cleanup);
        var result = s.Executor.Execute(s.Use, "new", _ => true, new(20, 20), s.Map,
            Array.Empty<(Character, GridPosition)>(), new(allowCrossArea: true, maximumDistance: 30));
        Assert.Equal(SmokeTeleportRejectionReason.DestinationOutsideSmoke, result.TeleportValidation!.RejectionReason);
        s.AssertUntouched();
        // The cast's own new smoke remains sufficient, even though every old effect expired.
        Assert.True(s.Execute(s.Use, "new", _ => true).Succeeded);
    }

    [Theory]
    [InlineData(null, SmokeTeleportRejectionReason.UnresolvedSamePositionRule)]
    [InlineData(false, SmokeTeleportRejectionReason.SamePositionNotAllowed)]
    public void SamePositionChoice_RemainsExplicit(bool? allow, SmokeTeleportRejectionReason expected)
    {
        var s = new Setup();
        var result = s.Executor.Execute(s.Use, "new", _ => true, new(7, 7), s.Map,
            Array.Empty<(Character, GridPosition)>(), new(allowSamePosition: allow));
        Assert.Equal(expected, result.TeleportValidation!.RejectionReason);
        s.AssertUntouched();
    }

    [Fact]
    public void ExistingMovementAndUtilityUsage_IsPreservedRatherThanChargedAgain()
    {
        var s = new Setup();
        s.Activations.RecordMovement();
        s.Activations.RecordUtilityAction();
        var result = s.Execute(s.Use, "new", _ => true);
        Assert.True(result.Succeeded);
        Assert.True(s.Activations.MovementUsed);
        Assert.True(s.Activations.UtilityActionUsed);
        Assert.True(s.Activations.MainActionUsed);
        Assert.Single(s.Actor.Cooldowns.Cooldowns);
    }

    [Fact]
    public void CastTeleport_DoesNotAuthorizeIndependentTeleportsBeforeOrAfterTheCast()
    {
        var s = new Setup();
        s.Smoke.Place("old", s.Actor.Id, new(7, 7), 2, _ => true);
        var coordinator = new SmokeTeleportActionCoordinator(new(s.Smoke), s.Activations);
        var independent = coordinator.Preview(s.Actor, new(8, 7), s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.UnresolvedActionCost, independent.RejectionReason);
        Assert.True(s.Execute(s.Use, "new", _ => true).Succeeded);
        var afterCast = coordinator.Preview(s.Actor, new(9, 7), s.Map);
        Assert.Equal(SmokeTeleportActionRejectionReason.CharacterNotActive, afterCast.RejectionReason);
        Assert.Equal(new GridPosition(8, 7), s.Actor.TacticalPosition);
    }

    private static GridPosition[] Square(GridPosition center) => Enumerable.Range(-2, 5)
        .SelectMany(y => Enumerable.Range(-2, 5).Select(x => new GridPosition(center.X + x, center.Y + y))).ToArray();

    private sealed class Setup
    {
        public Character Actor { get; }
        public Character Enemy { get; } = CharacterFactory.Create("nigel", 2)!;
        public SmokePlacementService Smoke { get; }
        public SmokeBombAbilityExecutor Executor { get; }
        public RoundLifecycleManager Lifecycle { get; }
        public ActivationManager Activations { get; }
        public AbilityUse Use { get; set; }

        public Setup(Character? actor = null, SmokePlacementService? smoke = null, bool setPosition = true,
            int round = 2, RoundPhase phase = RoundPhase.AlternatingActivations)
        {
            Actor = actor ?? CharacterFactory.Create("hooded_assassin", 1)!;
            Smoke = smoke ?? new();
            Executor = new(Smoke);
            Lifecycle = new(round);
            while (Lifecycle.CurrentPhase != phase) Lifecycle.AdvancePhase();
            Activations = new(new[] { Actor }, new[] { Enemy }, 1, Lifecycle);
            Activations.StartActivation(Actor);
            Use = new(Actor, "smoke_bomb", Activations);
            if (setPosition) Actor.SetTacticalPosition(new(7, 7));
            Enemy.SetTacticalPosition(new(12, 12));
        }

        public TeleportMap Map { get; } = new();
        private GridPosition Destination => Actor.TacticalPosition is { } p ? new(p.X + 1, p.Y) : new(8, 7);
        public SmokeBombUseResult Preview(AbilityUse use, string id, Func<GridPosition, bool> canCover)
            => Executor.Preview(use, id, canCover, Destination, Map,
                new[] { (Enemy, Enemy.TacticalPosition!.Value) });
        public SmokeBombUseResult Execute(AbilityUse use, string id, Func<GridPosition, bool> canCover)
            => Executor.Execute(use, id, canCover, Destination, Map,
                new[] { (Enemy, Enemy.TacticalPosition!.Value) });

        public SmokeVisibilityResult Visibility() => SmokeVisibility.Evaluate(Actor, Actor.TacticalPosition!.Value,
            Smoke, new[] { (Enemy, Enemy.TacticalPosition!.Value) });
        public void AssertUntouched()
        {
            Assert.Empty(Smoke.ActiveAreas);
            Assert.Empty(Actor.Cooldowns.Cooldowns);
            Assert.False(Actor.SmokeReveal.RevealedByAttack);
            Assert.False(Activations.MainActionUsed);
            Assert.Same(Actor, Activations.ActiveCharacter);
            Assert.True(Activations.CanUseMainAction);
        }
    }

    private sealed class TeleportMap : ISmokeTeleportMap
    {
        public Func<GridPosition, bool?> Valid { get; set; } = _ => true;
        public bool? Legal { get; set; } = true;
        public bool? Occupied { get; set; } = false;
        public bool? IsValidPosition(GridPosition position) => Valid(position);
        public bool? CanTeleportTo(GridPosition position) => Legal;
        public bool? IsOccupied(GridPosition position) => Occupied;
    }

}
