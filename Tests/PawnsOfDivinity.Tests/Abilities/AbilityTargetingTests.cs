using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using Xunit;

namespace PawnsOfDivinity.Tests.Abilities;

public class AbilityTargetingTests
{
    [Theory]
    [InlineData(0, 0, 3, 0, 3)]
    [InlineData(0, 0, 0, 4, 4)]
    [InlineData(1, 2, 4, 6, 7)]
    [InlineData(4, 6, 1, 2, 7)]
    [InlineData(-2, -3, 1, 1, 7)]
    [InlineData(2, 2, 2, 2, 0)]
    public void ManhattanDistance_AddsOrthogonalAxisDifferences(int x, int y, int tx, int ty, long expected)
        => Assert.Equal(expected, new GridPosition(x, y).ManhattanDistanceTo(new GridPosition(tx, ty)));

    [Fact]
    public void ExtremeCoordinates_DoNotOverflowOrInventMapBounds()
    {
        var source = new GridPosition(int.MinValue, int.MinValue);
        var target = new GridPosition(int.MaxValue, int.MaxValue);
        Assert.Equal(8589934590L, source.ManhattanDistanceTo(target));

        var result = AbilityTargeting.Validate(Unit("source", 1), source, Ability(0, int.MaxValue),
            new AbilityTarget(AbilityTargetType.Tile, target), Rules(AbilityTargetType.Tile));

        Assert.Equal(AbilityTargetingRejectionReason.OutsideMaximumRange, result.RejectionReason);
        Assert.Equal(8589934590L, result.Distance);
    }

    [Theory]
    [InlineData(0, AbilityTargetingRejectionReason.InsideMinimumRange)]
    [InlineData(1, AbilityTargetingRejectionReason.InsideMinimumRange)]
    [InlineData(2, AbilityTargetingRejectionReason.None)]
    [InlineData(3, AbilityTargetingRejectionReason.None)]
    [InlineData(4, AbilityTargetingRejectionReason.OutsideMaximumRange)]
    public void InclusiveRange_EnforcesBothBoundaries(int distance, AbilityTargetingRejectionReason expected)
    {
        var result = AbilityTargeting.Validate(Unit("source", 1), new GridPosition(0, 0), Ability(2, 3),
            new AbilityTarget(AbilityTargetType.Tile, new GridPosition(distance, 0)), Rules(AbilityTargetType.Tile));

        Assert.Equal(expected, result.RejectionReason);
        Assert.Equal(expected == AbilityTargetingRejectionReason.None, result.IsLegal);
        Assert.Equal((long)distance, result.Distance);
    }

    [Fact]
    public void CharacterTarget_UsesSuppliedPositionAndLivingConstraint()
    {
        var target = Unit("target", 2);
        var input = new AbilityTarget(AbilityTargetType.Character, new GridPosition(2, 1), target);
        var source = Unit("source", 1);
        Assert.True(AbilityTargeting.Validate(source, new GridPosition(0, 0), Ability(), input, Rules()).IsLegal);
        target.TakeDamage(int.MaxValue);

        var result = AbilityTargeting.Validate(source, new GridPosition(0, 0), Ability(), input, Rules());

        Assert.Equal(AbilityTargetingRejectionReason.TargetDefeated, result.RejectionReason);
    }

    [Theory]
    [InlineData(true, AbilityTargetingRejectionReason.None)]
    [InlineData(false, AbilityTargetingRejectionReason.InvalidSelfTarget)]
    [InlineData(null, AbilityTargetingRejectionReason.UnresolvedTargetingRule)]
    public void SelfTarget_RequiresExplicitPermission(bool? allowSelf, AbilityTargetingRejectionReason expected)
    {
        var source = Unit("source", 1);
        var constraints = new AbilityTargetingConstraints(AbilityTargetType.Self, allowSelf,
            AbilityTargetTeam.Any, true, false, false, false);

        var result = AbilityTargeting.Validate(source, new GridPosition(2, 2), Ability(0, 3),
            new AbilityTarget(AbilityTargetType.Self), constraints);

        Assert.Equal(expected, result.RejectionReason);
        Assert.Equal(new GridPosition(2, 2), result.TargetPosition);
    }

    [Fact]
    public void CharacterFormSelfTarget_CannotBypassPermissionOrClaimAnotherPosition()
    {
        var source = Unit("source", 1);
        var position = new GridPosition(0, 0);
        Assert.Equal(AbilityTargetingRejectionReason.InvalidSelfTarget,
            AbilityTargeting.Validate(source, position, Ability(0, 3),
                new AbilityTarget(AbilityTargetType.Character, position, source), Rules()).RejectionReason);
        Assert.Equal(AbilityTargetingRejectionReason.InvalidSelfTarget,
            AbilityTargeting.Validate(source, position, Ability(0, 3),
                new AbilityTarget(AbilityTargetType.Self, new GridPosition(1, 0)), Rules(AbilityTargetType.Self, allowSelf: true)).RejectionReason);
    }

    [Theory]
    [InlineData(AbilityTargetTeam.Ally, 1, true)]
    [InlineData(AbilityTargetTeam.Ally, 2, false)]
    [InlineData(AbilityTargetTeam.Enemy, 1, false)]
    [InlineData(AbilityTargetTeam.Enemy, 2, true)]
    [InlineData(AbilityTargetTeam.Any, 2, true)]
    public void TeamRestrictions_UseCharacterTeamIds(AbilityTargetTeam team, int targetTeam, bool legal)
    {
        var result = AbilityTargeting.Validate(Unit("source", 1), new GridPosition(0, 0), Ability(),
            new AbilityTarget(AbilityTargetType.Character, new GridPosition(1, 0), Unit("target", targetTeam)), Rules(team: team));

        Assert.Equal(legal, result.IsLegal);
        Assert.Equal(legal ? AbilityTargetingRejectionReason.None : AbilityTargetingRejectionReason.WrongTeam, result.RejectionReason);
    }

    [Fact]
    public void EquipmentRange_RequiresExplicitResolvedManhattanBounds()
    {
        var ability = new AbilityDefinition("example", "Example", "", range: new AbilityRange(AbilityRangeKind.EquippedWeapon));
        var source = Unit("source", 1);
        var target = new AbilityTarget(AbilityTargetType.Tile, new GridPosition(2, 0));
        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation,
            AbilityTargeting.Validate(source, new GridPosition(0, 0), ability, target, Rules(AbilityTargetType.Tile)).RejectionReason);

        var result = AbilityTargeting.Validate(source, new GridPosition(0, 0), ability, target, Rules(AbilityTargetType.Tile),
            new AbilityRange(AbilityRangeKind.ManhattanDistance, 2, 3));

        Assert.True(result.IsLegal);
        Assert.Equal(2L, result.Distance);
    }

    [Theory]
    [InlineData("team")]
    [InlineData("living")]
    [InlineData("map")]
    [InlineData("elevation")]
    public void ApplicableUnspecifiedPermissions_RemainUnresolved(string missing)
    {
        var rules = new AbilityTargetingConstraints(AbilityTargetType.Character, false,
            missing == "team" ? null : AbilityTargetTeam.Any, missing == "living" ? null : true,
            missing == "map" ? null : false, false, missing == "elevation" ? null : false);

        var result = AbilityTargeting.Validate(Unit("source", 1), new GridPosition(0, 0), Ability(),
            new AbilityTarget(AbilityTargetType.Character, new GridPosition(1, 0), Unit("target", 2)), rules);

        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedTargetingRule, result.RejectionReason);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void LivingTargetConstraint_IsExplicit_AndValidationNeverRevives(bool requiresLiving, bool legal)
    {
        var target = Unit("target", 2);
        target.TakeDamage(int.MaxValue);
        var rules = new AbilityTargetingConstraints(AbilityTargetType.Character, false,
            AbilityTargetTeam.Any, requiresLiving, false, false, false);

        var result = AbilityTargeting.Validate(Unit("source", 1), new GridPosition(0, 0), Ability(),
            new AbilityTarget(AbilityTargetType.Character, new GridPosition(1, 0), target), rules);

        Assert.Equal(legal, result.IsLegal);
        Assert.Equal(0, target.CurrentHealth);
        Assert.True(target.IsDefeated);
    }

    [Fact]
    public void ExplicitManhattanRadius_IsSupported_WhileUnspecifiedGeometryRemainsUnresolved()
    {
        var source = Unit("source", 1);
        var target = new AbilityTarget(AbilityTargetType.Tile, new GridPosition(1, 1));
        var radius = new AbilityDefinition("radius", "Radius", "", range: new AbilityRange(AbilityRangeKind.ManhattanRadius, 0, 2));
        Assert.True(AbilityTargeting.Validate(source, new GridPosition(0, 0), radius, target, Rules(AbilityTargetType.Tile)).IsLegal);
        var unknown = new AbilityDefinition("unknown", "Unknown", "", range: new AbilityRange(AbilityRangeKind.UnspecifiedRadius, 0, 2));

        var result = AbilityTargeting.Validate(source, new GridPosition(0, 0), unknown, target, Rules(AbilityTargetType.Tile));

        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRadiusGeometry, result.RejectionReason);
        Assert.Null(result.Distance);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("targetPosition")]
    [InlineData("targetCharacter")]
    public void MissingRequiredTargetData_ReturnsARejection(string missing)
    {
        var target = new AbilityTarget(AbilityTargetType.Character,
            missing == "targetPosition" ? null : new GridPosition(1, 0), missing == "targetCharacter" ? null : Unit("target", 2));

        var result = AbilityTargeting.Validate(Unit("source", 1), missing == "source" ? null : new GridPosition(0, 0), Ability(), target, Rules());

        Assert.Equal(AbilityTargetingRejectionReason.MissingRequiredTargetInformation, result.RejectionReason);
    }

    [Fact]
    public void WrongTargetForm_IsRejected_AndUnspecifiedPermissionsAreNotGuessed()
    {
        var source = Unit("source", 1);
        var tile = new AbilityTarget(AbilityTargetType.Tile, new GridPosition(1, 0));
        Assert.Equal(AbilityTargetingRejectionReason.InvalidTargetType,
            AbilityTargeting.Validate(source, new GridPosition(0, 0), Ability(), tile, Rules()).RejectionReason);
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedTargetingRule,
            AbilityTargeting.Validate(source, new GridPosition(0, 0), Ability(), tile, new AbilityTargetingConstraints()).RejectionReason);
    }

    [Theory]
    [InlineData("minimum")]
    [InlineData("maximum")]
    [InlineData("range")]
    public void UnknownRangeMetadata_IsNotReplacedWithDefaultBounds(string missing)
    {
        var ability = new AbilityDefinition("example", "Example", "", range: missing == "range" ? null :
            new AbilityRange(AbilityRangeKind.ManhattanDistance, missing == "minimum" ? null : 0, missing == "maximum" ? null : 3));
        var result = AbilityTargeting.Validate(Unit("source", 1), new GridPosition(0, 0), ability,
            new AbilityTarget(AbilityTargetType.Tile, new GridPosition(1, 0)), Rules(AbilityTargetType.Tile));

        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedRange, result.RejectionReason);
    }

    [Theory]
    [InlineData(true, AbilityTargetingRejectionReason.UnresolvedLineOfSight)]
    [InlineData(false, AbilityTargetingRejectionReason.None)]
    [InlineData(null, AbilityTargetingRejectionReason.UnresolvedTargetingRule)]
    public void LineOfSightRequirements_DoNotInventAnAlgorithm(bool? requiresSight, AbilityTargetingRejectionReason expected)
    {
        var rules = new AbilityTargetingConstraints(AbilityTargetType.Tile, requiresMapValidation: false,
            requiresLineOfSight: requiresSight, requiresElevationRules: false);

        var result = AbilityTargeting.Validate(Unit("source", 1), new GridPosition(0, 0), Ability(),
            new AbilityTarget(AbilityTargetType.Tile, new GridPosition(1, 0)), rules);

        Assert.Equal(expected, result.RejectionReason);
    }

    [Fact]
    public void ElevationRequirements_RemainUnresolved()
    {
        var rules = new AbilityTargetingConstraints(AbilityTargetType.Tile, requiresMapValidation: false,
            requiresLineOfSight: false, requiresElevationRules: true);
        Assert.Equal(AbilityTargetingRejectionReason.UnresolvedElevation,
            AbilityTargeting.Validate(Unit("source", 1), new GridPosition(0, 0), Ability(),
                new AbilityTarget(AbilityTargetType.Tile, new GridPosition(1, 0)), rules).RejectionReason);
    }

    [Theory]
    [InlineData(null, AbilityTargetingRejectionReason.MissingRequiredTargetInformation)]
    [InlineData(false, AbilityTargetingRejectionReason.MapTargetIllegal)]
    [InlineData(true, AbilityTargetingRejectionReason.None)]
    public void RequiredMapLegality_UsesExplicitExternalInformation(bool? mapLegal, AbilityTargetingRejectionReason expected)
    {
        var rules = new AbilityTargetingConstraints(AbilityTargetType.Tile, requiresMapValidation: true,
            requiresLineOfSight: false, requiresElevationRules: false);
        Assert.Equal(expected, AbilityTargeting.Validate(Unit("source", 1), new GridPosition(0, 0), Ability(),
            new AbilityTarget(AbilityTargetType.Tile, new GridPosition(1, 0)), rules, mapTargetLegal: mapLegal).RejectionReason);
    }

    [Fact]
    public void MalformedInputs_AreExceptions_RatherThanGameplayRejections()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityTarget((AbilityTargetType)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityTargetingConstraints((AbilityTargetType)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityTargetingConstraints(team: (AbilityTargetTeam)99));
        Assert.Throws<ArgumentException>(() => new AbilityTarget(AbilityTargetType.Tile, character: Unit("target", 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityRange(AbilityRangeKind.ManhattanDistance, -1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityRange(AbilityRangeKind.ManhattanDistance, 3, 2));
        var source = Unit("source", 1);
        var target = new AbilityTarget(AbilityTargetType.Tile, new GridPosition(1, 0));
        Assert.Throws<ArgumentNullException>(() => AbilityTargeting.Validate(null!, new GridPosition(0, 0), Ability(), target, Rules()));
        Assert.Throws<ArgumentNullException>(() => AbilityTargeting.Validate(source, new GridPosition(0, 0), null!, target, Rules()));
        Assert.Throws<ArgumentNullException>(() => AbilityTargeting.Validate(source, new GridPosition(0, 0), Ability(), null!, Rules()));
        Assert.Throws<ArgumentNullException>(() => AbilityTargeting.Validate(source, new GridPosition(0, 0), Ability(), target, null!));
    }

    [Fact]
    public void Validation_IsDeterministicImmutableAndDoesNotConsumeOrChangeGameState()
    {
        var source = Unit("source", 1);
        var target = Unit("target", 2);
        target.TakeDamage(5);
        target.SetShieldHP(6);
        target.Statuses.ApplyTimedStatus(StatusType.Burn, 2);
        target.Statuses.ApplyPoison();
        var manager = new ActivationManager(new[] { source }, new[] { target }, 1);
        manager.StartActivation(source);
        var stats = target.BaseStats;
        var input = new AbilityTarget(AbilityTargetType.Character, new GridPosition(1, 0), target);

        var first = AbilityTargeting.Validate(source, new GridPosition(0, 0), Ability(), input, Rules());
        var repeated = AbilityTargeting.Validate(source, new GridPosition(0, 0), Ability(), input, Rules());

        Assert.True(first.IsLegal);
        Assert.Equal(first, repeated);
        Assert.Equal(new GridPosition(0, 0), first.SourcePosition);
        Assert.Equal(new GridPosition(1, 0), first.TargetPosition);
        Assert.All(typeof(AbilityTargetingResult).GetProperties(), property => Assert.Null(property.SetMethod));
        target.TakeDamage(1);
        Assert.Equal(1L, first.Distance);
        Assert.Equal(95, target.CurrentHealth);
        Assert.Equal(5, target.ShieldHP);
        Assert.Equal(2, target.Statuses.TimedStatuses[StatusType.Burn]);
        Assert.True(target.Statuses.IsPoisoned);
        Assert.Equal(0, target.Statuses.PoisonTicksOccurred);
        Assert.Same(stats, target.BaseStats);
        Assert.False(target.HasActedThisRound);
        Assert.Empty(source.Cooldowns.Cooldowns);
        Assert.True(manager.CanMove);
        Assert.True(manager.CanUseUtilityAction);
        Assert.True(manager.CanUseMainAction);
        // No map object or mutable terrain is accepted by this pure validator.
    }

    private static Character Unit(string id, int team) => new(id, id, team, new CharacterStats(100, 20, 10, 12, 8, 4, 10, 5));
    private static AbilityDefinition Ability(int minimum = 0, int maximum = 3)
        => new("example", "Example", "Explicit test constraints, not a roster ability.",
            range: new AbilityRange(AbilityRangeKind.ManhattanDistance, minimum, maximum));
    private static AbilityTargetingConstraints Rules(AbilityTargetType type = AbilityTargetType.Character,
        AbilityTargetTeam team = AbilityTargetTeam.Any, bool allowSelf = false)
        => new(type, allowSelf, team, true, false, false, false);
}
