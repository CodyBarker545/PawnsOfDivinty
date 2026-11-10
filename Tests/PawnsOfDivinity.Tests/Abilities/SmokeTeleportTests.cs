using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class SmokeTeleportTests
{
    [Fact]
    public void ValidSameCloudTeleport_ChangesOnlyTheSameInstancesPosition_AndReturnsVisibility()
    {
        var setup = Setup();
        var destination = new GridPosition(5, 5);
        var validation = setup.Service.Validate(setup.Assassin, destination, setup.Map);
        Assert.True(validation.IsLegal);
        Assert.Equal(new GridPosition(3, 3), setup.Assassin.TacticalPosition);

        var result = setup.Service.Execute(setup.Assassin, validation, setup.Map, Array.Empty<(Character, GridPosition)>());

        Assert.True(result.Succeeded);
        Assert.Equal(new GridPosition(3, 3), result.OldPosition);
        Assert.Equal(destination, result.NewPosition);
        Assert.Equal(destination, setup.Assassin.TacticalPosition);
        Assert.True(result.Visibility!.IsInvisible);
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(setup.Assassin, destination, setup.Smoke));
        Assert.Single(setup.Smoke.ActiveAreas);
    }

    [Theory]
    [InlineData("outsideSource", SmokeTeleportRejectionReason.SourceOutsideSmoke)]
    [InlineData("outsideDestination", SmokeTeleportRejectionReason.DestinationOutsideSmoke)]
    [InlineData("invalidSource", SmokeTeleportRejectionReason.InvalidSourcePosition)]
    [InlineData("invalidDestination", SmokeTeleportRejectionReason.InvalidDestinationPosition)]
    [InlineData("illegalDestination", SmokeTeleportRejectionReason.IllegalDestination)]
    [InlineData("occupied", SmokeTeleportRejectionReason.DestinationOccupied)]
    [InlineData("unknownMap", SmokeTeleportRejectionReason.MissingMapInformation)]
    [InlineData("missingSource", SmokeTeleportRejectionReason.MissingSourcePosition)]
    [InlineData("missingDestination", SmokeTeleportRejectionReason.MissingDestinationPosition)]
    public void InvalidProposals_ReturnReasonsWithoutMoving(string invalid, SmokeTeleportRejectionReason expected)
    {
        var setup = Setup();
        var actor = setup.Assassin;
        GridPosition? destination = new(4, 3);
        switch (invalid)
        {
            case "outsideSource": actor.SetTacticalPosition(new(10, 10)); break;
            case "outsideDestination": destination = new(10, 10); break;
            case "invalidSource": setup.Map.Valid = position => position != new GridPosition(3, 3); break;
            case "invalidDestination": setup.Map.Valid = position => position != destination; break;
            case "illegalDestination": setup.Map.Legal = false; break;
            case "occupied": setup.Map.Occupied = true; break;
            case "unknownMap": setup.Map.Legal = null; break;
            case "missingSource": actor = CharacterFactory.Create("hooded_assassin", 1)!; break;
            case "missingDestination": destination = null; break;
        }
        var before = actor.TacticalPosition;

        var result = setup.Service.Validate(actor, destination, setup.Map);

        Assert.Equal(expected, result.RejectionReason);
        Assert.Equal(before, actor.TacticalPosition);
    }

    [Fact]
    public void ExpiredSmoke_AndChangedOccupancy_AreRecheckedBeforeExecution()
    {
        var setup = Setup();
        var validation = setup.Service.Validate(setup.Assassin, new(4, 3), setup.Map);
        setup.Map.Occupied = true;
        var occupied = setup.Service.Execute(setup.Assassin, validation, setup.Map, Array.Empty<(Character, GridPosition)>());
        Assert.Equal(SmokeTeleportRejectionReason.DestinationOccupied, occupied.Validation.RejectionReason);
        Assert.Equal(new GridPosition(3, 3), setup.Assassin.TacticalPosition);
        setup.Map.Occupied = false;
        setup.Smoke.ExpireAtRoundEnd(3, RoundPhase.Cleanup);

        var expired = setup.Service.Execute(setup.Assassin, validation, setup.Map, Array.Empty<(Character, GridPosition)>());

        Assert.False(expired.Succeeded);
        Assert.Equal(SmokeTeleportRejectionReason.SourceOutsideSmoke, expired.Validation.RejectionReason);
        Assert.Equal(new GridPosition(3, 3), setup.Assassin.TacticalPosition);
    }

    [Fact]
    public void CrossCloudTravel_IsUnresolvedByDefault_AndRequiresExplicitPermissionAndDistance()
    {
        var setup = Setup();
        setup.Smoke.Place("far", setup.Assassin.Id, new(20, 20), 2, _ => true);
        var destination = new GridPosition(20, 20);
        Assert.Equal(SmokeTeleportRejectionReason.UnresolvedCrossAreaRule,
            setup.Service.Validate(setup.Assassin, destination, setup.Map).RejectionReason);
        Assert.Equal(SmokeTeleportRejectionReason.DifferentSmokeAreasNotAllowed,
            setup.Service.Validate(setup.Assassin, destination, setup.Map, new SmokeTeleportConstraints(allowCrossArea: false)).RejectionReason);
        Assert.Equal(SmokeTeleportRejectionReason.UnresolvedDistanceLimit,
            setup.Service.Validate(setup.Assassin, destination, setup.Map, new SmokeTeleportConstraints(allowCrossArea: true)).RejectionReason);
        // Explicit prototype constraints, not a new canonical teleport range.
        Assert.Equal(SmokeTeleportRejectionReason.OutsideMaximumRange,
            setup.Service.Validate(setup.Assassin, destination, setup.Map, new SmokeTeleportConstraints(allowCrossArea: true, maximumDistance: 33)).RejectionReason);
        var validation = setup.Service.Validate(setup.Assassin, destination, setup.Map,
            new SmokeTeleportConstraints(allowCrossArea: true, maximumDistance: 34));

        Assert.True(validation.IsLegal);
        Assert.True(setup.Service.Execute(setup.Assassin, validation, setup.Map, Array.Empty<(Character, GridPosition)>()).Succeeded);
        Assert.Equal(destination, setup.Assassin.TacticalPosition);
    }

    [Fact]
    public void AnotherOwnersSmoke_RequiresExplicitCallerPermission()
    {
        var assassin = CharacterFactory.Create("hooded_assassin", 1)!;
        assassin.SetTacticalPosition(new(3, 3));
        var smoke = new SmokePlacementService();
        smoke.Place("other", "other_owner", new(3, 3), 2, _ => true);
        var service = new SmokeTeleportService(smoke);
        var map = new Map();

        Assert.Equal(SmokeTeleportRejectionReason.UnresolvedOwnerRule, service.Validate(assassin, new(4, 3), map).RejectionReason);
        Assert.Equal(SmokeTeleportRejectionReason.OtherOwnersNotAllowed,
            service.Validate(assassin, new(4, 3), map, new SmokeTeleportConstraints(allowOtherOwners: false)).RejectionReason);
        Assert.True(service.Validate(assassin, new(4, 3), map, new SmokeTeleportConstraints(allowOtherOwners: true)).IsLegal);
    }

    [Fact]
    public void SamePosition_DoesNotInventAnAllowOrDenyRule_AndStillRequiresUnoccupiedTile()
    {
        var setup = Setup();
        Assert.Equal(SmokeTeleportRejectionReason.UnresolvedSamePositionRule,
            setup.Service.Validate(setup.Assassin, new(3, 3), setup.Map).RejectionReason);
        Assert.Equal(SmokeTeleportRejectionReason.SamePositionNotAllowed,
            setup.Service.Validate(setup.Assassin, new(3, 3), setup.Map, new SmokeTeleportConstraints(allowSamePosition: false)).RejectionReason);
        Assert.True(setup.Service.Validate(setup.Assassin, new(3, 3), setup.Map,
            new SmokeTeleportConstraints(allowSamePosition: true)).IsLegal);
        setup.Map.Occupied = true;
        Assert.Equal(SmokeTeleportRejectionReason.DestinationOccupied,
            setup.Service.Validate(setup.Assassin, new(3, 3), setup.Map, new SmokeTeleportConstraints(allowSamePosition: true)).RejectionReason);
    }

    [Fact]
    public void DefeatedAndNonAssassinCharacters_AreRejected()
    {
        var setup = Setup();
        setup.Assassin.TakeDamage(int.MaxValue);
        Assert.Equal(SmokeTeleportRejectionReason.CasterDefeated, setup.Service.Validate(setup.Assassin, new(4, 3), setup.Map).RejectionReason);
        var other = CharacterFactory.Create("nigel", 1)!;
        other.SetTacticalPosition(new(3, 3));
        Assert.Equal(SmokeTeleportRejectionReason.NotAssassin, setup.Service.Validate(other, new(4, 3), setup.Map).RejectionReason);
    }

    [Fact]
    public void StaleSourceOrDifferentInstance_CannotUseAnEarlierValidation()
    {
        var setup = Setup();
        var validation = setup.Service.Validate(setup.Assassin, new(4, 3), setup.Map);
        setup.Assassin.SetTacticalPosition(new(2, 3));
        Assert.Equal(SmokeTeleportRejectionReason.SourcePositionChanged,
            setup.Service.Execute(setup.Assassin, validation, setup.Map, Array.Empty<(Character, GridPosition)>()).Validation.RejectionReason);
        var other = CharacterFactory.Create("hooded_assassin", 1)!;
        other.SetTacticalPosition(new(3, 3));
        Assert.Equal(SmokeTeleportRejectionReason.InvalidValidationContext,
            setup.Service.Execute(other, validation, setup.Map, Array.Empty<(Character, GridPosition)>()).Validation.RejectionReason);
        Assert.Equal(new GridPosition(3, 3), other.TacticalPosition);
    }

    [Fact]
    public void Teleport_PreservesAttackReveal_AndReusesAdjacencyVisibilityAndSmokeVeil()
    {
        var setup = Setup();
        setup.Smoke.Place("overlap", setup.Assassin.Id, new(3, 3), 2, _ => true);
        var enemy = CharacterFactory.Create("nigel", 2)!;
        setup.Assassin.SmokeReveal.RecordAttack();
        var validation = setup.Service.Validate(setup.Assassin, new(4, 3), setup.Map);

        var result = setup.Service.Execute(setup.Assassin, validation, setup.Map, new[] { (enemy, new GridPosition(5, 3)) });

        Assert.True(result.Succeeded);
        Assert.False(result.Visibility!.IsInvisible);
        Assert.True(result.Visibility.EnemyAdjacent);
        Assert.True(result.Visibility.RevealedByAttack);
        Assert.True(setup.Assassin.SmokeReveal.RevealedByAttack);
        Assert.Equal(20, SmokeVeil.GetEvasionModifier(setup.Assassin, setup.Assassin.TacticalPosition!.Value, setup.Smoke));
    }

    [Fact]
    public void SuccessfulAndFailedExecution_PreserveOtherRuntimeStateAndImmutableSnapshots()
    {
        var setup = Setup();
        var other = CharacterFactory.Create("nigel", 2)!;
        other.SetTacticalPosition(new(8, 8));
        var manager = new ActivationManager(new[] { setup.Assassin }, new[] { other }, 1);
        manager.StartActivation(setup.Assassin);
        setup.Assassin.TakeDamage(5);
        setup.Assassin.SetShieldHP(6);
        setup.Assassin.Statuses.ApplyPoison();
        setup.Assassin.Cooldowns.RecordUse("test", 1, 3);
        var stats = setup.Assassin.BaseStats;
        var validation = setup.Service.Validate(setup.Assassin, new(4, 3), setup.Map);

        var result = setup.Service.Execute(setup.Assassin, validation, setup.Map, new[] { (other, new GridPosition(8, 8)) });

        Assert.True(result.Succeeded);
        setup.Map.Legal = false;
        var denied = setup.Service.Validate(setup.Assassin, new(5, 3), setup.Map);
        Assert.False(setup.Service.Execute(setup.Assassin, denied, setup.Map, Array.Empty<(Character, GridPosition)>()).Succeeded);
        Assert.Equal(new GridPosition(4, 3), setup.Assassin.TacticalPosition);
        Assert.Equal(new GridPosition(3, 3), validation.SourcePosition);
        Assert.Equal(new GridPosition(4, 3), result.NewPosition);
        Assert.Equal(new GridPosition(8, 8), other.TacticalPosition);
        Assert.Equal(33, setup.Assassin.CurrentHealth);
        Assert.Equal(6, setup.Assassin.ShieldHP);
        Assert.True(setup.Assassin.Statuses.IsPoisoned);
        Assert.Equal(0, setup.Assassin.Statuses.PoisonTicksOccurred);
        Assert.Equal(4, setup.Assassin.Cooldowns.Cooldowns["test"].ReadyRound);
        Assert.Same(stats, setup.Assassin.BaseStats);
        Assert.False(setup.Assassin.HasActedThisRound);
        Assert.True(manager.CanMove);
        Assert.True(manager.CanUseMainAction);
        Assert.True(manager.CanUseUtilityAction);
        Assert.Equal(setup.Assassin.Id, setup.Smoke.ActiveAreas[0].OwnerCharacterId);
        Assert.Equal(3, setup.Smoke.ActiveAreas[0].ExpirationRound);
        Assert.All(typeof(SmokeTeleportValidationResult).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(SmokeTeleportResult).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void MissingMapAndMalformedInputs_AreHandledWithoutMutation()
    {
        var setup = Setup();
        Assert.Equal(SmokeTeleportRejectionReason.MissingMapInformation, setup.Service.Validate(setup.Assassin, new(4, 3), null).RejectionReason);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SmokeTeleportConstraints(maximumDistance: -1));
        Assert.Throws<ArgumentNullException>(() => new SmokeTeleportService(null!));
        Assert.Throws<ArgumentNullException>(() => setup.Service.Validate(null!, new(4, 3), setup.Map));
        var validation = setup.Service.Validate(setup.Assassin, new(4, 3), setup.Map);
        Assert.Throws<ArgumentException>(() => setup.Service.Execute(setup.Assassin, validation, setup.Map,
            new[] { ((Character)null!, new GridPosition(9, 9)) }));
        Assert.Equal(new GridPosition(3, 3), setup.Assassin.TacticalPosition);
    }

    private static (Character Assassin, SmokePlacementService Smoke, SmokeTeleportService Service, Map Map) Setup()
    {
        var assassin = CharacterFactory.Create("hooded_assassin", 1)!;
        assassin.SetTacticalPosition(new(3, 3));
        var smoke = new SmokePlacementService();
        smoke.Place("smoke", assassin.Id, new(3, 3), 2, _ => true);
        return (assassin, smoke, new SmokeTeleportService(smoke), new Map());
    }
    private sealed class Map : ISmokeTeleportMap
    {
        public Func<GridPosition, bool?> Valid { get; set; } = _ => true;
        public bool? Legal { get; set; } = true;
        public bool? Occupied { get; set; } = false;
        public bool? IsValidPosition(GridPosition position) => Valid(position);
        public bool? CanTeleportTo(GridPosition position) => Legal;
        public bool? IsOccupied(GridPosition position) => Occupied;
    }
}
