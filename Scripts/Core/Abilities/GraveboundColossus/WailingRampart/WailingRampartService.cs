using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Current placement information from the authoritative map, not another map store.</summary>
public interface IWailingRampartMap
{
    bool? IsValidPosition(GridPosition position);
    bool? IsOccupied(GridPosition position);
    /// <summary>
    /// Approve placement using known terrain and any required visibility/height rules.
    /// Return null if those requirements cannot be resolved. Queries must not mutate match state.
    /// </summary>
    bool? CanPlaceWall(Character caster, GridPosition position);
}

public enum WailingRampartRejectionReason
{
    None, NotColossus, AbilityNotOwned, CasterDefeated, MissingCasterPosition, InvalidFootprint,
    OutsideRange, MissingMapInformation, InvalidPosition, OccupiedTile, IllegalTerrain,
    DuplicateEffectId, OverlapUnresolved, UnsupportedAbilityMetadata, PlacementRulesUnresolved
}

public sealed record WailingRampartPlacementResult
{
    public WailingRampartRejectionReason RejectionReason { get; }
    public WailingRampartArea? ProposedArea { get; }
    public GridPosition? RejectedTile { get; }
    public long? NearestDistance { get; }
    public bool IsLegal => RejectionReason == WailingRampartRejectionReason.None;
    internal WailingRampartService Service { get; }

    internal WailingRampartPlacementResult(WailingRampartService service, WailingRampartRejectionReason reason,
        WailingRampartArea? area = null, GridPosition? rejectedTile = null, long? nearestDistance = null)
    {
        Service = service;
        RejectionReason = reason;
        ProposedArea = area;
        RejectedTile = rejectedTile;
        NearestDistance = nearestDistance;
    }
}

/// <summary>Stores only created Ramparts. It does not own base terrain, units or movement paths.</summary>
public sealed class WailingRampartService
{
    private readonly List<WailingRampartArea> _active = new();
    private readonly HashSet<string> _usedIds = new(StringComparer.Ordinal);
    public IReadOnlyList<WailingRampartArea> ActiveAreas => Array.AsReadOnly(_active.ToArray());

    public WailingRampartPlacementResult ValidatePlacement(Character caster, string effectId,
        IEnumerable<GridPosition> tiles, int round, IWailingRampartMap? map)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        var footprint = tiles.ToArray();
        if (caster.Definition?.Id != "gravebound_colossus") return Reject(WailingRampartRejectionReason.NotColossus);
        if (!caster.Definition.Abilities.Any(a => a.Id == GraveboundColossus.WailingRampartId))
            return Reject(WailingRampartRejectionReason.AbilityNotOwned);
        if (caster.IsDefeated) return Reject(WailingRampartRejectionReason.CasterDefeated);
        if (caster.TacticalPosition is not { } source) return Reject(WailingRampartRejectionReason.MissingCasterPosition);
        if (_usedIds.Contains(effectId)) return Reject(WailingRampartRejectionReason.DuplicateEffectId);
        // Three distinct squares in one straight, unbroken row or column.
        if (footprint.Length != 3 || footprint.Distinct().Count() != 3 ||
            !((footprint.All(p => p.Y == footprint[0].Y) && (long)footprint.Max(p => p.X) - footprint.Min(p => p.X) == 2) ||
              (footprint.All(p => p.X == footprint[0].X) && (long)footprint.Max(p => p.Y) - footprint.Min(p => p.Y) == 2)))
            return Reject(WailingRampartRejectionReason.InvalidFootprint);
        long distance = footprint.Min(source.ManhattanDistanceTo);
        if (distance < 1 || distance > 4) return Reject(WailingRampartRejectionReason.OutsideRange, distance: distance);
        var area = new WailingRampartArea(effectId, caster.Id, caster.TeamId, footprint, round);
        if (_active.Any(wall => wall.Tiles.Intersect(area.Tiles).Any()))
            return Reject(WailingRampartRejectionReason.OverlapUnresolved, area);
        if (map is null) return Reject(WailingRampartRejectionReason.MissingMapInformation, area);
        bool? sourceValid = map.IsValidPosition(source);
        if (!sourceValid.HasValue) return Reject(WailingRampartRejectionReason.MissingMapInformation, area, source);
        if (!sourceValid.Value) return Reject(WailingRampartRejectionReason.InvalidPosition, area, source);
        foreach (var tile in area.Tiles)
        {
            bool? valid = map.IsValidPosition(tile);
            bool? occupied = map.IsOccupied(tile);
            bool? legal = map.CanPlaceWall(caster, tile);
            if (!valid.HasValue || !occupied.HasValue)
                return Reject(WailingRampartRejectionReason.MissingMapInformation, area, tile);
            if (!valid.Value) return Reject(WailingRampartRejectionReason.InvalidPosition, area, tile);
            if (occupied.Value) return Reject(WailingRampartRejectionReason.OccupiedTile, area, tile);
            if (!legal.HasValue) return Reject(WailingRampartRejectionReason.PlacementRulesUnresolved, area, tile);
            if (!legal.Value) return Reject(WailingRampartRejectionReason.IllegalTerrain, area, tile);
        }
        return new(this, WailingRampartRejectionReason.None, area, nearestDistance: distance);

        WailingRampartPlacementResult Reject(WailingRampartRejectionReason reason, WailingRampartArea? proposed = null,
            GridPosition? tile = null, long? distance = null) => new(this, reason, proposed, tile, distance);
    }

    // The executor commits immediately after fresh validation and bookkeeping checks.
    // No map callback runs between this storage step and ability finalization.
    internal WailingRampartArea PlaceValidated(WailingRampartPlacementResult placement)
    {
        if (!ReferenceEquals(placement.Service, this) || !placement.IsLegal || placement.ProposedArea is not { } area)
            throw new ArgumentException("A valid placement from this service is required.", nameof(placement));
        if (!_usedIds.Add(area.Id)) throw new ArgumentException("The wall ID was already used.", nameof(placement));
        _active.Add(area);
        return area;
    }

    /// <summary>
    /// Checks only the wall restriction. True does not authorize an otherwise illegal move.
    /// Supply true only for movement already confirmed to bypass walls; null leaves that unresolved.
    /// </summary>
    public bool? CanEnter(Character character, GridPosition position, bool? qualifyingSpecialMovement = false)
    {
        ArgumentNullException.ThrowIfNull(character);
        bool enemyWall = _active.Any(wall => wall.OwnerTeamId != character.TeamId && wall.Tiles.Contains(position));
        if (!enemyWall) return true;
        return qualifyingSpecialMovement;
    }

    /// <summary>Call at the end of Cleanup. Cast R2 survives R3 activations, regardless of caster death.</summary>
    public IReadOnlyList<WailingRampartArea> ExpireAtRoundEnd(int round, RoundPhase phase)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (phase != RoundPhase.Cleanup) throw new InvalidOperationException("Expire Ramparts at the end of Cleanup.");
        var expired = _active.Where(wall => wall.ExpirationRound <= round).ToArray();
        _active.RemoveAll(wall => wall.ExpirationRound <= round);
        return Array.AsReadOnly(expired);
    }
}
