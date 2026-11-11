using System;

namespace PawnsOfDivinity.Core.Turns.Restrictions;

public enum TauntMainActionRule { AttackSourceOrEndActivation }

/// <summary>A named control restriction, separate from ordinary negative statuses.</summary>
public sealed record TauntRestriction
{
    public string Id { get; }
    public string EffectId { get; }
    public string SourceCharacterId { get; }
    public string RestrictedCharacterId { get; }
    public int ApplicationRound { get; }
    public int? ExpiresAtEndOfRound { get; }
    public TauntMainActionRule MainActionRule => TauntMainActionRule.AttackSourceOrEndActivation;

    public TauntRestriction(string id, string effectId, string sourceCharacterId, string restrictedCharacterId,
        int applicationRound, int? expiresAtEndOfRound)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCharacterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(restrictedCharacterId);
        if (sourceCharacterId == restrictedCharacterId) throw new ArgumentException("A taunt source cannot restrict itself.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(applicationRound);
        if (expiresAtEndOfRound.HasValue && expiresAtEndOfRound.Value < applicationRound)
            throw new ArgumentOutOfRangeException(nameof(expiresAtEndOfRound));
        Id = id;
        EffectId = effectId;
        SourceCharacterId = sourceCharacterId;
        RestrictedCharacterId = restrictedCharacterId;
        ApplicationRound = applicationRound;
        ExpiresAtEndOfRound = expiresAtEndOfRound;
    }

    public bool IsActiveInRound(int round)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        return ExpiresAtEndOfRound.HasValue && round >= ApplicationRound && round <= ExpiresAtEndOfRound.Value;
    }
}
