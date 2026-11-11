using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Abilities;

public interface ISpiritFrostMap
{
    bool? IsValidPosition(GridPosition position);
    bool? IsOccupied(GridPosition position);
    /// <summary>
    /// Current ice-placement legality, including existing walls, terrain and any required visibility/height rules.
    /// Return null when an interaction is unresolved. Queries must not mutate match state.
    /// </summary>
    bool? CanPlaceIce(Character caster, GridPosition position);
}

public enum SpiritFrostReason
{
    None, NotSpiritGuardian, AbilityNotOwned, CasterDefeated, MissingCasterPosition, InvalidDirection,
    CoordinateOverflow, DuplicateEffectId, OverlapUnresolved, MissingMapInformation, InvalidPosition,
    PlacementRulesUnresolved, IllegalTerrain, MissingParticipantPosition, OccupancyContextMismatch,
    TerrainFrostbiteRulesUnresolved, UnsupportedMetadata
}

public sealed record SpiritFrostPlacementResult
{
    public SpiritFrostReason RejectionReason { get; }
    public SpiritFrostArea? ProposedArea { get; }
    public GridPosition? RejectedTile { get; }
    public IReadOnlyList<string> AffectedCharacterIds { get; }
    public bool IsLegal => RejectionReason == SpiritFrostReason.None;
    internal SpiritFrostService Service { get; }
    internal SpiritFrostPlacementResult(SpiritFrostService service, SpiritFrostReason reason,
        SpiritFrostArea? area = null, GridPosition? rejectedTile = null, IEnumerable<string>? affected = null)
    {
        Service = service;
        RejectionReason = reason;
        ProposedArea = area;
        RejectedTile = rejectedTile;
        AffectedCharacterIds = Array.AsReadOnly(affected?.ToArray() ?? Array.Empty<string>());
    }
}

public enum SpiritFrostMovementOutcome { Unaffected, AlliedMovementUnchanged, TerrainFrostbiteRulesUnresolved }

/// <summary>Only the ice interaction, not overall movement permission or a path cost.</summary>
public sealed record SpiritFrostMovementResult
{
    public string CharacterId { get; }
    public GridPosition Position { get; }
    public SpiritFrostMovementOutcome Outcome { get; }
    public IReadOnlyList<string> AreaIds { get; }
    public StatusType? RequiredStatus => Outcome == SpiritFrostMovementOutcome.TerrainFrostbiteRulesUnresolved ? StatusType.Frostbite : null;
    internal SpiritFrostMovementResult(Character character, GridPosition position, SpiritFrostMovementOutcome outcome, IEnumerable<string> areas)
    {
        CharacterId = character.Id;
        Position = position;
        Outcome = outcome;
        AreaIds = Array.AsReadOnly(areas.ToArray());
    }
}

/// <summary>Owns created ice only. Terrain Frostbite application and movement execution are deliberately separate.</summary>
public sealed class SpiritFrostService
{
    private readonly List<SpiritFrostArea> _active = new();
    private readonly HashSet<string> _usedIds = new(StringComparer.Ordinal);
    public IReadOnlyList<SpiritFrostArea> ActiveAreas => Array.AsReadOnly(_active.ToArray());

    public SpiritFrostPlacementResult ValidatePlacement(Character caster, string effectId, GridPosition direction, int round,
        IEnumerable<Character> participants, ISpiritFrostMap? map)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        var roster = participants.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var instances = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        foreach (var character in roster)
            if (character is null || !ids.Add(character.Id) || !instances.Add(character))
                throw new ArgumentException("Ice participants must have unique instances and IDs and cannot contain null.", nameof(participants));
        if (!roster.Any(c => ReferenceEquals(c, caster)))
            throw new ArgumentException("The caster must belong to this roster.", nameof(participants));
        if (caster.Definition?.Id != "beothuk_spirit_guardian") return Reject(SpiritFrostReason.NotSpiritGuardian);
        if (!caster.Definition.Abilities.Any(a => a.Id == BeothukSpiritGuardian.SpiritFrostId)) return Reject(SpiritFrostReason.AbilityNotOwned);
        if (caster.IsDefeated) return Reject(SpiritFrostReason.CasterDefeated);
        if (caster.TacticalPosition is not { } origin) return Reject(SpiritFrostReason.MissingCasterPosition);
        if (Math.Abs((long)direction.X) + Math.Abs((long)direction.Y) != 1) return Reject(SpiritFrostReason.InvalidDirection);
        if (_usedIds.Contains(effectId)) return Reject(SpiritFrostReason.DuplicateEffectId);
        var tiles = new List<GridPosition>();
        for (int depth = 1; depth <= 2; depth++)
            for (int width = -1; width <= 1; width++)
            {
                long x = (long)origin.X + direction.X * depth - direction.Y * width;
                long y = (long)origin.Y + direction.Y * depth + direction.X * width;
                if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
                    return Reject(SpiritFrostReason.CoordinateOverflow);
                tiles.Add(new((int)x, (int)y));
            }
        var area = new SpiritFrostArea(effectId, caster.Id, caster.TeamId, origin, direction, tiles, round);
        if (_active.Any(a => a.Tiles.Intersect(area.Tiles).Any())) return Reject(SpiritFrostReason.OverlapUnresolved, area);
        var living = roster.Where(c => !c.IsDefeated).ToArray();
        if (living.Any(c => !c.TacticalPosition.HasValue)) return Reject(SpiritFrostReason.MissingParticipantPosition, area);
        var occupants = living.Where(c => area.Tiles.Contains(c.TacticalPosition!.Value)).ToArray();
        var affected = occupants.Select(c => c.Id).ToArray();
        if (map is null) return Reject(SpiritFrostReason.MissingMapInformation, area, affected: affected);
        bool? originValid = map.IsValidPosition(origin);
        if (!originValid.HasValue) return Reject(SpiritFrostReason.MissingMapInformation, area);
        if (!originValid.Value) return Reject(SpiritFrostReason.InvalidPosition, area, origin);
        foreach (var tile in area.Tiles)
        {
            bool? valid = map.IsValidPosition(tile);
            bool? occupied = map.IsOccupied(tile);
            bool? legal = map.CanPlaceIce(caster, tile);
            if (!valid.HasValue || !occupied.HasValue) return Reject(SpiritFrostReason.MissingMapInformation, area, tile, affected);
            if (!valid.Value) return Reject(SpiritFrostReason.InvalidPosition, area, tile, affected);
            if (occupied.Value != occupants.Any(c => c.TacticalPosition == tile))
                return Reject(SpiritFrostReason.OccupancyContextMismatch, area, tile, affected);
            if (!legal.HasValue) return Reject(SpiritFrostReason.PlacementRulesUnresolved, area, tile, affected);
            if (!legal.Value) return Reject(SpiritFrostReason.IllegalTerrain, area, tile, affected);
        }
        // Creating ice under an enemy also requires an immediate status sequence. Its duration,
        // removal and interaction with an arrow's Frostbite are not settled, so do not half-cast.
        if (occupants.Any(c => c.TeamId != caster.TeamId))
            return Reject(SpiritFrostReason.TerrainFrostbiteRulesUnresolved, area, affected: affected);
        return new(this, SpiritFrostReason.None, area, affected: affected);

        SpiritFrostPlacementResult Reject(SpiritFrostReason reason, SpiritFrostArea? proposed = null,
            GridPosition? tile = null, IEnumerable<string>? affected = null) => new(this, reason, proposed, tile, affected);
    }

    internal SpiritFrostArea PlaceValidated(SpiritFrostPlacementResult placement)
    {
        if (!ReferenceEquals(placement.Service, this) || !placement.IsLegal || placement.ProposedArea is not { } area)
            throw new ArgumentException("A valid placement from this ice service is required.", nameof(placement));
        if (!_usedIds.Add(area.Id)) throw new ArgumentException("The ice effect ID was already used.", nameof(placement));
        _active.Add(area);
        return area;
    }

    public bool IsCovered(GridPosition position) => _active.Any(a => a.Tiles.Contains(position));

    public SpiritFrostMovementResult EvaluateMovement(Character character, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(character);
        var areas = _active.Where(a => a.Tiles.Contains(position)).ToArray();
        var outcome = areas.Length == 0 ? SpiritFrostMovementOutcome.Unaffected
            : areas.Any(a => a.OwnerTeamId != character.TeamId) ? SpiritFrostMovementOutcome.TerrainFrostbiteRulesUnresolved
                : SpiritFrostMovementOutcome.AlliedMovementUnchanged;
        return new(character, position, outcome, areas.Select(a => a.Id));
    }

    /// <summary>Call at the end of Cleanup. Ice survives its owner's death and the following round's activations.</summary>
    public IReadOnlyList<SpiritFrostArea> ExpireAtRoundEnd(int round, RoundPhase phase)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (phase != RoundPhase.Cleanup) throw new InvalidOperationException("Expire Spirit Frost at the end of Cleanup.");
        var expired = _active.Where(a => a.ExpirationRound <= round).ToArray();
        _active.RemoveAll(a => a.ExpirationRound <= round);
        return Array.AsReadOnly(expired);
    }
}
