using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Core.Terrain;

/// <summary>An immutable smoke snapshot. Query the placement service for current active coverage.</summary>
public sealed record SmokeArea
{
    public string Id { get; }
    public string OwnerCharacterId { get; }
    public IReadOnlyList<GridPosition> Tiles { get; }
    public int CreationRound { get; }
    /// <summary>Smoke expires at the end of Cleanup in this round, not at its start.</summary>
    public int ExpirationRound { get; }
    public bool IsActive { get; }

    public SmokeArea(string id, string ownerCharacterId, IEnumerable<GridPosition> tiles, int creationRound)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerCharacterId);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(creationRound);
        int expirationRound = checked(creationRound + 1);
        var snapshot = tiles.ToArray();
        if (snapshot.Distinct().Count() != snapshot.Length)
            throw new ArgumentException("A smoke area cannot contain duplicate positions.", nameof(tiles));
        if (snapshot.Length != 25 || (long)snapshot.Max(tile => tile.X) - snapshot.Min(tile => tile.X) != 4 ||
            (long)snapshot.Max(tile => tile.Y) - snapshot.Min(tile => tile.Y) != 4)
            throw new ArgumentException("Smoke Bomb requires a complete 5-by-5 square.", nameof(tiles));

        Id = id;
        OwnerCharacterId = ownerCharacterId;
        Tiles = Array.AsReadOnly(snapshot.OrderBy(tile => tile.Y).ThenBy(tile => tile.X).ToArray());
        CreationRound = creationRound;
        ExpirationRound = expirationRound;
        IsActive = true;
    }

    private SmokeArea(SmokeArea area, bool isActive)
    {
        Id = area.Id;
        OwnerCharacterId = area.OwnerCharacterId;
        Tiles = area.Tiles;
        CreationRound = area.CreationRound;
        ExpirationRound = area.ExpirationRound;
        IsActive = isActive;
    }

    internal SmokeArea ExpiredSnapshot() => new(this, false);
}
