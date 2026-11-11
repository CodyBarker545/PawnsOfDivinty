using System;

namespace PawnsOfDivinity.Core.Abilities;

// These distinctions are needed by Sentinel only, not a taxonomy for all game movement.
public enum SentinelMovementKind { Ordinary, Teleport, Forced }

/// <summary>A caller-reported movement segment. Constructing it does not move a Character.</summary>
public sealed record SentinelMovementEvent
{
    public string Id { get; }
    public string CharacterId { get; }
    public int TeamId { get; }
    public GridPosition From { get; }
    public GridPosition Destination { get; }
    public SentinelMovementKind Kind { get; }
    public int RoundNumber { get; }
    public bool IsCommitted { get; }

    public SentinelMovementEvent(string id, string characterId, int teamId, GridPosition from, GridPosition destination,
        SentinelMovementKind kind, int roundNumber, bool isCommitted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roundNumber);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Id = id;
        CharacterId = characterId;
        TeamId = teamId;
        From = from;
        Destination = destination;
        Kind = kind;
        RoundNumber = roundNumber;
        IsCommitted = isCommitted;
    }
}
