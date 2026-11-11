using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>A created ice footprint, independent of base terrain and any character's Frostbite timer.</summary>
public sealed record SpiritFrostArea
{
    public string Id { get; }
    public string OwnerCharacterId { get; }
    public int OwnerTeamId { get; }
    public GridPosition Origin { get; }
    public GridPosition Direction { get; }
    public IReadOnlyList<GridPosition> Tiles { get; }
    public int CreationRound { get; }
    public int ExpirationRound { get; }

    internal SpiritFrostArea(string id, string ownerId, int teamId, GridPosition origin, GridPosition direction,
        IEnumerable<GridPosition> tiles, int round)
    {
        Id = id;
        OwnerCharacterId = ownerId;
        OwnerTeamId = teamId;
        Origin = origin;
        Direction = direction;
        Tiles = Array.AsReadOnly(tiles.OrderBy(p => p.Y).ThenBy(p => p.X).ToArray());
        CreationRound = round;
        ExpirationRound = checked(round + 1);
    }
}
