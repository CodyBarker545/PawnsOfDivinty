using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Terrain;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Validates and commits a core coordinate change without movement, ability costs or rendering.</summary>
public sealed class SmokeTeleportService
{
    private readonly SmokePlacementService _smoke;

    public SmokeTeleportService(SmokePlacementService smoke)
    {
        ArgumentNullException.ThrowIfNull(smoke);
        _smoke = smoke;
    }

    /// <remarks>
    /// The confirmed Smoke Bomb sequence begins within its newly created cloud and chooses
    /// a legal unoccupied tile in that cloud. No separate numeric range is documented there.
    /// Other owners, different clouds and same-tile selection require explicit caller decisions.
    /// Cross-cloud permission also needs an explicit distance bound; no unlimited range is inferred.
    /// These constraints enable future confirmed/prototype decisions without making them canonical.
    /// This validates the teleport sub-operation, not permission to cast Smoke Bomb at any time.
    /// </remarks>
    public SmokeTeleportValidationResult Validate(Character caster, GridPosition? destination, ISmokeTeleportMap? map,
        SmokeTeleportConstraints? constraints = null)
        => ValidateCore(caster, destination, map, constraints, null);

    private SmokeTeleportValidationResult ValidateCore(Character caster, GridPosition? destination, ISmokeTeleportMap? map,
        SmokeTeleportConstraints? constraints, SmokeArea? proposedSmoke)
    {
        ArgumentNullException.ThrowIfNull(caster);
        constraints ??= new SmokeTeleportConstraints();
        var source = caster.TacticalPosition;
        if (caster.IsDefeated) return Result(SmokeTeleportRejectionReason.CasterDefeated);
        if (!SmokeVisibility.IsEligibleAssassin(caster)) return Result(SmokeTeleportRejectionReason.NotAssassin);
        if (!source.HasValue) return Result(SmokeTeleportRejectionReason.MissingSourcePosition);
        if (!destination.HasValue) return Result(SmokeTeleportRejectionReason.MissingDestinationPosition);
        if (map is null) return Result(SmokeTeleportRejectionReason.MissingMapInformation);
        bool? validSource = map.IsValidPosition(source.Value);
        bool? validDestination = map.IsValidPosition(destination.Value);
        if (!validSource.HasValue || !validDestination.HasValue) return Result(SmokeTeleportRejectionReason.MissingMapInformation);
        if (!validSource.Value) return Result(SmokeTeleportRejectionReason.InvalidSourcePosition);
        if (!validDestination.Value) return Result(SmokeTeleportRejectionReason.InvalidDestinationPosition);
        bool? legal = map.CanTeleportTo(destination.Value);
        bool? occupied = map.IsOccupied(destination.Value);
        if (!legal.HasValue || !occupied.HasValue) return Result(SmokeTeleportRejectionReason.MissingMapInformation);
        if (!legal.Value) return Result(SmokeTeleportRejectionReason.IllegalDestination);
        if (occupied.Value) return Result(SmokeTeleportRejectionReason.DestinationOccupied);

        var sourceAreas = CoveringAreas(source.Value);
        var destinationAreas = CoveringAreas(destination.Value);
        if (sourceAreas.Count == 0) return Result(SmokeTeleportRejectionReason.SourceOutsideSmoke);
        if (destinationAreas.Count == 0) return Result(SmokeTeleportRejectionReason.DestinationOutsideSmoke);
        // The documented cast targets its new cloud. Travelling outside it into older
        // smoke still needs explicit cross-cloud permission and a distance bound, even
        // when an older cloud happens to cover both source and destination.
        if (proposedSmoke is not null && !proposedSmoke.Tiles.Contains(destination.Value))
        {
            if (!constraints.AllowCrossArea.HasValue) return Result(SmokeTeleportRejectionReason.UnresolvedCrossAreaRule);
            if (!constraints.AllowCrossArea.Value) return Result(SmokeTeleportRejectionReason.DifferentSmokeAreasNotAllowed);
            if (!constraints.MaximumDistance.HasValue) return Result(SmokeTeleportRejectionReason.UnresolvedDistanceLimit);
        }
        var common = sourceAreas.Where(area => destinationAreas.Any(other => other.Id == area.Id)).ToArray();
        bool ownAreaAccess = common.Length > 0 ? common.Any(area => area.OwnerCharacterId == caster.Id)
            : sourceAreas.Any(area => area.OwnerCharacterId == caster.Id) && destinationAreas.Any(area => area.OwnerCharacterId == caster.Id);
        if (!ownAreaAccess)
        {
            if (!constraints.AllowOtherOwners.HasValue) return Result(SmokeTeleportRejectionReason.UnresolvedOwnerRule);
            if (!constraints.AllowOtherOwners.Value) return Result(SmokeTeleportRejectionReason.OtherOwnersNotAllowed);
        }
        if (common.Length == 0)
        {
            if (!constraints.AllowCrossArea.HasValue) return Result(SmokeTeleportRejectionReason.UnresolvedCrossAreaRule);
            if (!constraints.AllowCrossArea.Value) return Result(SmokeTeleportRejectionReason.DifferentSmokeAreasNotAllowed);
            if (!constraints.MaximumDistance.HasValue) return Result(SmokeTeleportRejectionReason.UnresolvedDistanceLimit);
        }
        if (source == destination)
        {
            if (!constraints.AllowSamePosition.HasValue) return Result(SmokeTeleportRejectionReason.UnresolvedSamePositionRule);
            if (!constraints.AllowSamePosition.Value) return Result(SmokeTeleportRejectionReason.SamePositionNotAllowed);
        }
        if (constraints.MaximumDistance.HasValue && source.Value.ManhattanDistanceTo(destination.Value) > constraints.MaximumDistance.Value)
            return Result(SmokeTeleportRejectionReason.OutsideMaximumRange);
        return Result(SmokeTeleportRejectionReason.None);

        IReadOnlyList<SmokeArea> CoveringAreas(GridPosition position)
        {
            var active = _smoke.GetCoveringAreas(position);
            return proposedSmoke?.Tiles.Contains(position) == true ? active.Append(proposedSmoke).ToArray() : active;
        }

        SmokeTeleportValidationResult Result(SmokeTeleportRejectionReason reason) => new(caster, _smoke, constraints, source, destination, reason);
    }

    /// <summary>Revalidates a legal proposal and commits only after destination visibility can also be evaluated.</summary>
    /// <remarks>
    /// The map and roster are caller-supplied current information. They must not change concurrently
    /// or mutate match state during queries. Attack reveal is never reset. The surrounding executor
    /// owns any action/cooldown costs and loot or movement-event integration; no such effects run here.
    /// </remarks>
    public SmokeTeleportResult Execute(Character caster, SmokeTeleportValidationResult validation, ISmokeTeleportMap? map,
        IEnumerable<(Character Character, GridPosition Position)> participants)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(participants);
        var oldPosition = caster.TacticalPosition;
        if (!ReferenceEquals(validation.Caster, caster) || !ReferenceEquals(validation.Smoke, _smoke))
            return Rejected(SmokeTeleportRejectionReason.InvalidValidationContext);
        if (!validation.IsLegal) return new SmokeTeleportResult(validation, oldPosition, oldPosition, null);
        if (validation.SourcePosition != oldPosition) return Rejected(SmokeTeleportRejectionReason.SourcePositionChanged);
        var current = Validate(caster, validation.DestinationPosition, map, validation.Constraints);
        if (!current.IsLegal) return new SmokeTeleportResult(current, oldPosition, oldPosition, null);
        var destination = current.DestinationPosition!.Value;
        // Evaluate against the proposed position before committing, so invalid roster data cannot
        // leave a moved character behind. The evaluator already takes explicit coordinates.
        var visibility = SmokeVisibility.Evaluate(caster, destination, _smoke, participants);
        return CommitPrepared(current, visibility);

        SmokeTeleportResult Rejected(SmokeTeleportRejectionReason reason)
            => new(new SmokeTeleportValidationResult(caster, _smoke, validation.Constraints, oldPosition,
                validation.DestinationPosition, reason), oldPosition, oldPosition, null);
    }

    // Only the Smoke Bomb executor uses a proposed footprint. It must belong to this
    // service and caster; no temporary cloud is added to active smoke during preparation.
    internal (SmokeTeleportValidationResult Validation, SmokeVisibilityResult? Visibility) PrepareCast(
        Character caster, GridPosition? destination, ISmokeTeleportMap? map,
        SmokePlacementValidationResult placement, IEnumerable<(Character Character, GridPosition Position)> participants,
        SmokeTeleportConstraints? constraints)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (!ReferenceEquals(placement.Service, _smoke) || !placement.IsLegal
            || placement.ProposedArea is not { } area || area.OwnerCharacterId != caster.Id)
            throw new ArgumentException("A legal proposed cloud for this caster and smoke service is required.", nameof(placement));
        var validation = ValidateCore(caster, destination, map, constraints, area);
        var visibility = validation.IsLegal
            ? SmokeVisibility.EvaluateWithProposedSmoke(caster, destination!.Value, _smoke, participants, area)
            : null;
        return (validation, visibility);
    }

    // All validation and visibility queries are complete before the executor commits its
    // new cloud. The same position update is used by ordinary low-level teleport execution.
    internal SmokeTeleportResult CommitPrepared(SmokeTeleportValidationResult validation, SmokeVisibilityResult visibility)
    {
        var caster = validation.Caster;
        var oldPosition = caster.TacticalPosition;
        var destination = validation.DestinationPosition!.Value;
        caster.SetTacticalPosition(destination);
        return new SmokeTeleportResult(validation, oldPosition, destination, visibility);
    }

}
