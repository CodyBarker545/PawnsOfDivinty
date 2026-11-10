using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class SmokeTests
{
    [Fact]
    public void Placement_CreatesDocumentedCenteredFiveByFive_WithoutConsumingAnything()
    {
        var service = new SmokePlacementService();
        var center = new GridPosition(7, 7);
        var checkedTiles = new List<GridPosition>();

        var area = service.Place("smoke", "assassin", center, 2, tile => { checkedTiles.Add(tile); return true; });

        Assert.Equal("smoke", area.Id);
        Assert.Equal("assassin", area.OwnerCharacterId);
        Assert.Equal(2, area.CreationRound);
        Assert.Equal(3, area.ExpirationRound);
        Assert.True(area.IsActive);
        Assert.Equal(25, area.Tiles.Count);
        Assert.Equal(Square(center), area.Tiles);
        Assert.Equal(area.Tiles, checkedTiles);
        Assert.True(service.IsCovered(center));
        Assert.True(service.IsCovered(new GridPosition(5, 5)));
        Assert.True(service.IsCovered(new GridPosition(9, 9)));
        Assert.False(service.IsCovered(new GridPosition(10, 7)));
        Assert.True(service.IsActive("smoke"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void InvalidIds_AreRejectedBeforeMapQueries(string? invalid)
    {
        var service = new SmokePlacementService();
        int calls = 0;
        bool Check(GridPosition tile) { calls++; return true; }

        Assert.ThrowsAny<ArgumentException>(() => service.Place(invalid!, "owner", new(3, 3), 2, Check));
        Assert.ThrowsAny<ArgumentException>(() => service.Place("id", invalid!, new(3, 3), 2, Check));

        Assert.Equal(0, calls);
        Assert.Empty(service.ActiveAreas);
    }

    [Fact]
    public void InvalidFootprint_RejectsEntirePlacement_AndPreservesExistingSmoke()
    {
        var service = new SmokePlacementService();
        var existing = service.Place("existing", "owner", new(3, 3), 2, _ => true);

        Assert.Throws<ArgumentException>(() => service.Place("new", "other", new(7, 7), 2,
            tile => tile != new GridPosition(9, 9)));

        Assert.Same(existing, Assert.Single(service.ActiveAreas));
        Assert.False(service.IsCovered(new(7, 7)));
        Assert.False(service.IsActive("new"));
        Assert.True(service.Place("new", "other", new(7, 7), 2, _ => true).IsActive);
    }

    [Fact]
    public void MapQueryFailure_DoesNotPartiallyRegisterAnEffect()
    {
        var service = new SmokePlacementService();
        int calls = 0;
        Assert.Throws<InvalidOperationException>(() => service.Place("new", "owner", new(3, 3), 2,
            _ => ++calls < 25 ? true : throw new InvalidOperationException("Map data unavailable.")));

        Assert.Empty(service.ActiveAreas);
        Assert.True(service.Place("new", "owner", new(3, 3), 2, _ => true).IsActive);
    }

    [Fact]
    public void Overlaps_KeepEachAreaAndOwner_WhileCoverageTilesRemainUnique()
    {
        var service = new SmokePlacementService();
        service.Place("first", "owner-one", new(3, 3), 2, _ => true);
        service.Place("second", "owner-two", new(4, 3), 2, _ => true);
        service.Place("third", "owner-one", new(3, 3), 2, _ => true);

        Assert.Equal(new[] { "first", "second", "third" }, service.GetCoveringAreas(new(3, 3)).Select(area => area.Id));
        Assert.Equal(new[] { "owner-one", "owner-two", "owner-one" }, service.GetCoveringAreas(new(3, 3)).Select(area => area.OwnerCharacterId));
        Assert.Equal(new[] { "owner-one", "owner-two" }, service.GetOwnersAt(new(3, 3)));
        var covered = service.GetCoveredPositions();
        Assert.Equal(30, covered.Count);
        Assert.Equal(covered.Count, covered.Distinct().Count());
        Assert.Equal(3, service.ActiveAreas.Count);
    }

    [Fact]
    public void DuplicateEffectIds_AreRejected_EvenAfterExpiration()
    {
        var service = new SmokePlacementService();
        service.Place("same", "owner", new(3, 3), 2, _ => true);

        Assert.Throws<ArgumentException>(() => service.Place("same", "other", new(7, 7), 2, _ => true));
        Assert.Single(service.ActiveAreas);
        service.ExpireAtRoundEnd(3, RoundPhase.Cleanup);
        Assert.Throws<ArgumentException>(() => service.Place("same", "owner", new(3, 3), 4, _ => true));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CleanupBeforeFollowingRound_DoesNotExpireSmoke(int round)
    {
        var service = new SmokePlacementService();
        service.Place("smoke", "owner", new(3, 3), 2, _ => true);

        var result = service.ExpireAtRoundEnd(round, RoundPhase.Cleanup);

        Assert.Empty(result.ExpiredAreas);
        Assert.True(service.IsActive("smoke"));
        Assert.True(service.IsCovered(new(3, 3)));
    }

    [Theory]
    [InlineData(RoundPhase.Shop)]
    [InlineData(RoundPhase.PoisonBatch)]
    [InlineData(RoundPhase.DelayedAttacks)]
    [InlineData(RoundPhase.TimedStatusUpdate)]
    [InlineData(RoundPhase.OtherRoundStartTriggers)]
    [InlineData(RoundPhase.Preparation)]
    [InlineData(RoundPhase.AlternatingActivations)]
    [InlineData(RoundPhase.ObjectiveScoring)]
    public void ExpirationOutsideCleanup_IsRejectedWithoutEarlyRemoval(RoundPhase phase)
    {
        var service = new SmokePlacementService();
        service.Place("smoke", "owner", new(3, 3), 2, _ => true);

        Assert.Throws<InvalidOperationException>(() => service.ExpireAtRoundEnd(3, phase));

        Assert.True(service.IsActive("smoke"));
    }

    [Fact]
    public void FollowingRoundCleanup_ExpiresTogether_PreservesLaterSmoke_AndIsIdempotent()
    {
        var service = new SmokePlacementService();
        service.Place("first", "owner-one", new(3, 3), 2, _ => true);
        service.Place("second", "owner-two", new(3, 3), 2, _ => true);
        service.Place("later", "owner-three", new(4, 3), 3, _ => true);
        Assert.True(service.IsCovered(new(1, 1)));

        var result = service.ExpireAtRoundEnd(3, RoundPhase.Cleanup);

        Assert.Equal(3, result.RoundNumber);
        Assert.Equal(new[] { "first", "second" }, result.ExpiredAreas.Select(area => area.Id));
        Assert.All(result.ExpiredAreas, area => Assert.False(area.IsActive));
        Assert.Equal("later", Assert.Single(service.ActiveAreas).Id);
        Assert.False(service.IsCovered(new(1, 1)));
        Assert.True(service.IsCovered(new(3, 3)));
        Assert.Empty(service.ExpireAtRoundEnd(3, RoundPhase.Cleanup).ExpiredAreas);
        Assert.Equal("later", Assert.Single(service.ExpireAtRoundEnd(4, RoundPhase.Cleanup).ExpiredAreas).Id);
        Assert.Empty(service.ActiveAreas);
    }

    [Fact]
    public void Snapshots_AreImmutable_AndRemainIndependentOfLaterPlacementAndExpiration()
    {
        var service = new SmokePlacementService();
        var original = service.Place("first", "owner", new(3, 3), 2, _ => true);
        var active = service.ActiveAreas;
        var coverage = service.GetCoveredPositions();
        var overlapping = service.GetCoveringAreas(new(3, 3));
        service.Place("later", "other", new(9, 9), 3, _ => true);
        var expiration = service.ExpireAtRoundEnd(3, RoundPhase.Cleanup);

        Assert.Single(active);
        Assert.Single(overlapping);
        Assert.Equal(25, coverage.Count);
        Assert.True(original.IsActive); // An earlier value snapshot, not a mutable live handle.
        Assert.True((original with { }).IsActive);
        Assert.False(service.IsActive(original.Id));
        Assert.False(expiration.ExpiredAreas[0].IsActive);
        Assert.Throws<NotSupportedException>(() => ((IList<GridPosition>)original.Tiles).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<SmokeArea>)active).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<SmokeArea>)overlapping).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<GridPosition>)coverage).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<SmokeArea>)expiration.ExpiredAreas).Clear());
        Assert.All(typeof(SmokeArea).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(SmokeExpirationResult).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void DuplicateOrMalformedAreaTiles_AreRejected_AndInputCollectionsAreCopied()
    {
        var tiles = Square(new(3, 3)).ToArray();
        var area = new SmokeArea("id", "owner", tiles, 2);
        tiles[0] = new(100, 100);
        Assert.DoesNotContain(new GridPosition(100, 100), area.Tiles);
        var duplicates = Square(new(3, 3)).ToArray();
        duplicates[1] = duplicates[0];
        Assert.Throws<ArgumentException>(() => new SmokeArea("duplicate", "owner", duplicates, 2));
        Assert.Throws<ArgumentException>(() => new SmokeArea("empty", "owner", Array.Empty<GridPosition>(), 2));
        Assert.Throws<ArgumentException>(() => new SmokeArea("line", "owner", Enumerable.Range(0, 25).Select(x => new GridPosition(x, 0)), 2));
        Assert.Throws<ArgumentNullException>(() => new SmokeArea("null", "owner", null!, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidRounds_AreRejectedWithoutMutation(int round)
    {
        var service = new SmokePlacementService();
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Place("id", "owner", new(3, 3), round, _ => true));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.ExpireAtRoundEnd(round, RoundPhase.Cleanup));
        Assert.Empty(service.ActiveAreas);
    }

    [Fact]
    public void ArithmeticOverflowAndMissingLegality_AreRejectedBeforePlacement()
    {
        var service = new SmokePlacementService();
        Assert.Throws<OverflowException>(() => service.Place("round", "owner", new(3, 3), int.MaxValue, _ => true));
        Assert.Throws<OverflowException>(() => service.Place("position", "owner", new(int.MaxValue, 0), 2, _ => true));
        Assert.Throws<OverflowException>(() => service.Place("position", "owner", new(0, int.MinValue), 2, _ => true));
        Assert.Throws<ArgumentNullException>(() => service.Place("map", "owner", new(3, 3), 2, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.ExpireAtRoundEnd(3, (RoundPhase)99));
        Assert.Empty(service.ActiveAreas);
    }

    [Fact]
    public void Smoke_IsIndependentOfOwnerDeathAndCharacterState()
    {
        var owner = CharacterFactory.Create("hooded_assassin", 1)!;
        var other = CharacterFactory.Create("nigel", 2)!;
        var manager = new ActivationManager(new[] { owner }, new[] { other }, 1);
        manager.StartActivation(owner);
        owner.TakeDamage(5);
        owner.SetShieldHP(6);
        owner.Statuses.ApplyPoison();
        var stats = owner.BaseStats;
        var service = new SmokePlacementService();

        service.Place("smoke", owner.Id, new(3, 3), 2, _ => true);
        service.ExpireAtRoundEnd(2, RoundPhase.Cleanup);

        Assert.Equal(33, owner.CurrentHealth);
        Assert.Equal(6, owner.ShieldHP);
        Assert.True(owner.Statuses.IsPoisoned);
        Assert.Equal(0, owner.Statuses.PoisonTicksOccurred);
        Assert.Empty(owner.Cooldowns.Cooldowns);
        Assert.False(owner.HasActedThisRound);
        Assert.True(manager.CanMove);
        Assert.True(manager.CanUseUtilityAction);
        Assert.True(manager.CanUseMainAction);
        Assert.Same(stats, owner.BaseStats);
        owner.TakeDamage(int.MaxValue);
        Assert.True(service.IsActive("smoke"));
        Assert.True(service.IsCovered(new(3, 3)));
    }

    private static IEnumerable<GridPosition> Square(GridPosition center)
    {
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
                yield return new GridPosition(center.X + x, center.Y + y);
    }
}
