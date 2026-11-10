using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Terrain;

/// <summary>Owns active smoke areas without owning terrain, character visibility or ability execution.</summary>
public sealed class SmokePlacementService
{
    private readonly List<SmokeArea> _active = new();
    private readonly HashSet<string> _usedIds = new(StringComparer.Ordinal);

    public IReadOnlyList<SmokeArea> ActiveAreas => Array.AsReadOnly(_active.ToArray());

    /// <summary>Places the documented 5-by-5 square centered on the supplied current caster position.</summary>
    /// <remarks>
    /// The future executor supplies the caster position, not a remote cloud center or teleport destination.
    /// canCoverTile supplies smoke-placement legality, not automatically walkability or teleport legality.
    /// All 25 positions must be approved before storage. This API does not invent edge clipping,
    /// obstacle exclusions, overlap stacking, visibility or line-of-sight rules. The map callback must
    /// only inspect map information; it must not mutate this service or the match during validation.
    /// </remarks>
    public SmokeArea Place(string effectId, string ownerCharacterId, GridPosition casterPosition,
        int creationRound, Func<GridPosition, bool> canCoverTile)
    {
        var validation = ValidatePlacement(effectId, ownerCharacterId, casterPosition, creationRound, canCoverTile);
        if (validation.RejectionReason == SmokePlacementRejectionReason.DuplicateEffectId)
            throw new ArgumentException("This smoke effect ID has already been used.", nameof(effectId));
        if (!validation.IsLegal)
            throw new ArgumentException("The map did not approve the full smoke footprint.", nameof(casterPosition));
        return PlaceValidated(validation);
    }

    /// <summary>Checks the whole footprint without reserving its ID or adding active coverage.</summary>
    public SmokePlacementValidationResult ValidatePlacement(string effectId, string ownerCharacterId,
        GridPosition casterPosition, int creationRound, Func<GridPosition, bool> canCoverTile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerCharacterId);
        ArgumentNullException.ThrowIfNull(canCoverTile);
        if (_usedIds.Contains(effectId)) return new(this, SmokePlacementRejectionReason.DuplicateEffectId);
        var area = new SmokeArea(effectId, ownerCharacterId, Square(casterPosition), creationRound);
        foreach (var tile in area.Tiles)
            if (!canCoverTile(tile)) return new(this, SmokePlacementRejectionReason.IllegalTile, area, tile);
        return new(this, SmokePlacementRejectionReason.None, area);
    }

    // Commit immediately after validation, on the same match thread, without another map callback.
    // Public execution APIs must obtain fresh validation rather than accepting a stale preview.
    internal SmokeArea PlaceValidated(SmokePlacementValidationResult validation)
    {
        if (!ReferenceEquals(validation.Service, this) || !validation.IsLegal || validation.ProposedArea is not { } area)
            throw new ArgumentException("A legal placement from this smoke service is required.", nameof(validation));
        if (_usedIds.Contains(area.Id)) throw new ArgumentException("This smoke effect ID has already been used.", nameof(validation));
        _active.Add(area);
        _usedIds.Add(area.Id);
        return area;
    }

    public bool IsActive(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        return _active.Any(area => StringComparer.Ordinal.Equals(area.Id, effectId));
    }

    public bool IsCovered(GridPosition position) => _active.Any(area => area.Tiles.Contains(position));

    /// <summary>Preserves each effect's provenance at a tile; overlap does not replace another area's owner.</summary>
    public IReadOnlyList<SmokeArea> GetCoveringAreas(GridPosition position)
        => Array.AsReadOnly(_active.Where(area => area.Tiles.Contains(position)).ToArray());

    /// <summary>Each owner appears once even when several of that character's clouds overlap.</summary>
    public IReadOnlyList<string> GetOwnersAt(GridPosition position)
        => Array.AsReadOnly(_active.Where(area => area.Tiles.Contains(position))
            .Select(area => area.OwnerCharacterId).Distinct(StringComparer.Ordinal).ToArray());

    /// <summary>A deterministic union with each covered tile listed once, regardless of overlap.</summary>
    public IReadOnlyList<GridPosition> GetCoveredPositions()
        => Array.AsReadOnly(_active.SelectMany(area => area.Tiles).Distinct()
            .OrderBy(tile => tile.Y).ThenBy(tile => tile.X).ToArray());

    /// <summary>Call at the end of Cleanup, before objective scoring. Repeated calls are harmless.</summary>
    /// <remarks>
    /// A cast in R2 survives all of R2 and R3's activations, expiring at end of Cleanup in R3.
    /// Overdue effects are also removed. Nothing advances the lifecycle or expires smoke automatically.
    /// Owner death has no effect on this lifetime. Old snapshots remain unchanged after removal.
    /// </remarks>
    public SmokeExpirationResult ExpireAtRoundEnd(int round, RoundPhase phase)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (phase != RoundPhase.Cleanup)
            throw new InvalidOperationException("Smoke expiration must be called at the end of the Cleanup phase.");
        var expired = _active.Where(area => area.ExpirationRound <= round).Select(area => area.ExpiredSnapshot()).ToArray();
        _active.RemoveAll(area => area.ExpirationRound <= round);
        return new SmokeExpirationResult(round, expired);
    }

    private static IEnumerable<GridPosition> Square(GridPosition center)
    {
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
                yield return new GridPosition(checked(center.X + x), checked(center.Y + y));
    }
}
