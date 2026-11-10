using System;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Gameplay;

/// <summary>Current map information for a resolved displacement, independent of teleport rules.</summary>
public interface IForcedMovementMap
{
    bool? IsValidPosition(GridPosition position);
    bool? IsOccupied(GridPosition position);
    /// <summary>
    /// Approves the entire transition, including intervening blockers and applicable terrain/height
    /// rules. Return null when that information is unresolved. This service does not find a path,
    /// skip blockers, choose a shorter distance or execute terrain-entry effects.
    /// </summary>
    bool? CanDisplace(Character target, GridPosition from, GridPosition to);
}

public enum ForcedMovementRejectionReason
{
    None, TargetDefeated, MissingSourcePosition, MissingTargetPosition, DirectionRequired, InvalidDirection,
    DistanceRequired, InvalidDistance, DestinationOverflow, MissingMapInformation, InvalidSourcePosition,
    InvalidTargetPosition, InvalidDestination, DestinationOccupied, IllegalTransition, InvalidValidationContext,
    SourcePositionChanged, TargetPositionChanged
}

public sealed record ForcedMovementValidationResult
{
    public string SourceId { get; }
    public string TargetId { get; }
    public GridPosition? SourcePosition { get; }
    public GridPosition? OriginalTargetPosition { get; }
    public GridPosition? Destination { get; }
    public GridPosition? Direction { get; }
    public int? Distance { get; }
    public ForcedMovementRejectionReason RejectionReason { get; }
    public bool IsLegal => RejectionReason == ForcedMovementRejectionReason.None;
    internal Character Source { get; }
    internal Character Target { get; }
    internal ForcedMovementService Service { get; }

    internal ForcedMovementValidationResult(ForcedMovementService service, Character source, Character target,
        GridPosition? direction, int? distance, GridPosition? destination, ForcedMovementRejectionReason reason)
    {
        Service = service;
        Source = source;
        Target = target;
        SourceId = source.Id;
        TargetId = target.Id;
        SourcePosition = source.TacticalPosition;
        OriginalTargetPosition = target.TacticalPosition;
        Direction = direction;
        Distance = distance;
        Destination = destination;
        RejectionReason = reason;
    }
}

public sealed record ForcedMovementResult
{
    public ForcedMovementValidationResult Validation { get; }
    public GridPosition? OriginalPosition { get; }
    public GridPosition? FinalPosition { get; }
    public bool Displaced => Validation.IsLegal && OriginalPosition != FinalPosition;

    internal ForcedMovementResult(ForcedMovementValidationResult validation, GridPosition? before, GridPosition? after)
    {
        Validation = validation;
        OriginalPosition = before;
        FinalPosition = after;
    }
}

/// <summary>Commits an explicit orthogonal displacement to Character's existing position property.</summary>
public sealed class ForcedMovementService
{
    public ForcedMovementValidationResult Validate(Character source, Character target, GridPosition? direction,
        int? distance, IForcedMovementMap? map)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        GridPosition? destination = null;
        if (target.IsDefeated) return Result(ForcedMovementRejectionReason.TargetDefeated);
        if (!source.TacticalPosition.HasValue) return Result(ForcedMovementRejectionReason.MissingSourcePosition);
        if (!target.TacticalPosition.HasValue) return Result(ForcedMovementRejectionReason.MissingTargetPosition);
        if (!direction.HasValue) return Result(ForcedMovementRejectionReason.DirectionRequired);
        if (Math.Abs((long)direction.Value.X) + Math.Abs((long)direction.Value.Y) != 1)
            return Result(ForcedMovementRejectionReason.InvalidDirection);
        if (!distance.HasValue) return Result(ForcedMovementRejectionReason.DistanceRequired);
        if (distance.Value <= 0) return Result(ForcedMovementRejectionReason.InvalidDistance);
        long x = target.TacticalPosition.Value.X + (long)direction.Value.X * distance.Value;
        long y = target.TacticalPosition.Value.Y + (long)direction.Value.Y * distance.Value;
        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            return Result(ForcedMovementRejectionReason.DestinationOverflow);
        destination = new((int)x, (int)y);
        if (map is null) return Result(ForcedMovementRejectionReason.MissingMapInformation);
        bool? sourceValid = map.IsValidPosition(source.TacticalPosition.Value);
        bool? targetValid = map.IsValidPosition(target.TacticalPosition.Value);
        bool? destinationValid = map.IsValidPosition(destination.Value);
        if (!sourceValid.HasValue || !targetValid.HasValue || !destinationValid.HasValue)
            return Result(ForcedMovementRejectionReason.MissingMapInformation);
        if (!sourceValid.Value) return Result(ForcedMovementRejectionReason.InvalidSourcePosition);
        if (!targetValid.Value) return Result(ForcedMovementRejectionReason.InvalidTargetPosition);
        if (!destinationValid.Value) return Result(ForcedMovementRejectionReason.InvalidDestination);
        bool? occupied = map.IsOccupied(destination.Value);
        bool? transition = map.CanDisplace(target, target.TacticalPosition.Value, destination.Value);
        if (!occupied.HasValue || !transition.HasValue) return Result(ForcedMovementRejectionReason.MissingMapInformation);
        if (occupied.Value) return Result(ForcedMovementRejectionReason.DestinationOccupied);
        if (!transition.Value) return Result(ForcedMovementRejectionReason.IllegalTransition);
        return Result(ForcedMovementRejectionReason.None);

        ForcedMovementValidationResult Result(ForcedMovementRejectionReason reason)
            => new(this, source, target, direction, distance, destination, reason);
    }

    /// <remarks>
    /// Map queries must be read-only and match operations synchronous. Recheck the current
    /// map after any intervening combat rather than treating the preview as a reservation.
    /// This changes one coordinate only; the caller owns effect ordering and any entry triggers.
    /// </remarks>
    public ForcedMovementResult Execute(Character source, Character target, ForcedMovementValidationResult validation,
        IForcedMovementMap? map)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(validation);
        var before = target.TacticalPosition;
        if (!ReferenceEquals(validation.Service, this) || !ReferenceEquals(validation.Source, source)
            || !ReferenceEquals(validation.Target, target)) return Rejected(ForcedMovementRejectionReason.InvalidValidationContext);
        if (!validation.IsLegal) return new(validation, before, before);
        if (source.TacticalPosition != validation.SourcePosition) return Rejected(ForcedMovementRejectionReason.SourcePositionChanged);
        if (before != validation.OriginalTargetPosition) return Rejected(ForcedMovementRejectionReason.TargetPositionChanged);
        var current = Validate(source, target, validation.Direction, validation.Distance, map);
        if (!current.IsLegal) return new(current, before, before);
        target.SetTacticalPosition(current.Destination!.Value);
        return new(current, before, target.TacticalPosition);

        ForcedMovementResult Rejected(ForcedMovementRejectionReason reason)
            => new(new(this, source, target, validation.Direction, validation.Distance, validation.Destination, reason), before, before);
    }
}
