using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>A wall's immutable tiles and lifetime, independent of the caster's later health.</summary>
public sealed record WailingRampartArea
{
    public string Id { get; }
    public string OwnerCharacterId { get; }
    public int OwnerTeamId { get; }
    public IReadOnlyList<GridPosition> Tiles { get; }
    public int CreationRound { get; }
    public int ExpirationRound { get; }

    internal WailingRampartArea(string id, string ownerId, int teamId, IEnumerable<GridPosition> tiles, int round)
    {
        Id = id;
        OwnerCharacterId = ownerId;
        OwnerTeamId = teamId;
        Tiles = Array.AsReadOnly(tiles.OrderBy(p => p.Y).ThenBy(p => p.X).ToArray());
        CreationRound = round;
        ExpirationRound = checked(round + 1);
    }
}
