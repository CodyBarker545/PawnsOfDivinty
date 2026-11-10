using System;

namespace PawnsOfDivinity.Core.Characters;

public enum TemporaryStat { Defense, Resistance }

/// <summary>An immutable bonus with an explicit lifetime ending at Cleanup in ExpirationRound.</summary>
public sealed record TemporaryStatModifier
{
    public string Id { get; }
    public string SourceCharacterId { get; }
    public string RecipientCharacterId { get; }
    public TemporaryStat Stat { get; }
    public int Value { get; }
    public int ApplicationRound { get; }
    public int ExpirationRound { get; }
    public bool IsActive { get; }

    public TemporaryStatModifier(string id, string sourceCharacterId, string recipientCharacterId,
        TemporaryStat stat, int value, int applicationRound, int expirationRound)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCharacterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientCharacterId);
        if (!Enum.IsDefined(stat)) throw new ArgumentOutOfRangeException(nameof(stat));
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(applicationRound);
        if (expirationRound < applicationRound) throw new ArgumentOutOfRangeException(nameof(expirationRound));
        Id = id;
        SourceCharacterId = sourceCharacterId;
        RecipientCharacterId = recipientCharacterId;
        Stat = stat;
        Value = value;
        ApplicationRound = applicationRound;
        ExpirationRound = expirationRound;
        IsActive = true;
    }

    private TemporaryStatModifier(TemporaryStatModifier original, bool active)
    {
        Id = original.Id;
        SourceCharacterId = original.SourceCharacterId;
        RecipientCharacterId = original.RecipientCharacterId;
        Stat = original.Stat;
        Value = original.Value;
        ApplicationRound = original.ApplicationRound;
        ExpirationRound = original.ExpirationRound;
        IsActive = active;
    }

    internal TemporaryStatModifier InactiveSnapshot() => new(this, false);
}
