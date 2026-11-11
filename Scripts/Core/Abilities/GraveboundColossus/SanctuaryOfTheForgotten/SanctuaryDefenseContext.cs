using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Abilities;

public enum SanctuaryDefenseReason { Resolved, MissingContext, MissingParticipant, MissingPosition, SourceDeathUnresolved, OverlapUnresolved }

public sealed record SanctuaryDefenseResult
{
    public string AttackerId { get; }
    public string RecipientId { get; }
    public int ExistingEffectiveDefense { get; }
    public int? DefenseBonus { get; }
    public int? EffectiveDefense { get; }
    public SanctuaryDefenseReason Reason { get; }
    public bool IsResolved => Reason == SanctuaryDefenseReason.Resolved;
    public IReadOnlyList<SanctuaryEvaluation> Sources { get; }

    internal SanctuaryDefenseResult(string attackerId, string recipientId, int existing, SanctuaryDefenseReason reason,
        IEnumerable<SanctuaryEvaluation> sources, int? bonus = null)
    {
        AttackerId = attackerId;
        RecipientId = recipientId;
        ExistingEffectiveDefense = existing;
        Reason = reason;
        Sources = Array.AsReadOnly(sources.ToArray());
        DefenseBonus = bonus;
        EffectiveDefense = bonus.HasValue ? checked(existing + bonus.Value) : null;
    }
}

/// <summary>Preserves the explicit unresolved result when an existing combat API cannot return an incomplete exchange.</summary>
public sealed class SanctuaryDefenseUnresolvedException : InvalidOperationException
{
    public SanctuaryDefenseResult Result { get; }
    public SanctuaryDefenseUnresolvedException(SanctuaryDefenseResult result)
        : base($"Sanctuary Defense is unresolved: {result?.Reason}.")
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }
}

/// <summary>A fixed match roster of live Character references, not another position or stat store.</summary>
public sealed class SanctuaryDefenseContext
{
    private readonly IReadOnlyDictionary<string, Character> _characters;
    public IReadOnlyList<Character> Participants { get; }

    public SanctuaryDefenseContext(IEnumerable<Character> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        var roster = participants.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var instances = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        foreach (var character in roster)
            if (character is null || !ids.Add(character.Id) || !instances.Add(character))
                throw new ArgumentException("Sanctuary participants must be non-null with unique instances and IDs.", nameof(participants));
        Participants = Array.AsReadOnly(roster);
        _characters = roster.ToDictionary(c => c.Id, StringComparer.Ordinal);
    }

    public static SanctuaryDefenseContext FromActivations(ActivationManager activations)
    {
        ArgumentNullException.ThrowIfNull(activations);
        return new(activations.FirstTeam.Concat(activations.SecondTeam));
    }

    public SanctuaryDefenseResult Evaluate(Character attacker, Character recipient)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(recipient);
        int existing = recipient.EffectiveDefense;
        if (!_characters.TryGetValue(attacker.Id, out var knownAttacker) || !ReferenceEquals(knownAttacker, attacker)
            || !_characters.TryGetValue(recipient.Id, out var knownRecipient) || !ReferenceEquals(knownRecipient, recipient))
            return Result(SanctuaryDefenseReason.MissingParticipant, Array.Empty<SanctuaryEvaluation>());
        var sources = Participants.Where(SanctuaryOfTheForgotten.IsPassiveOwner)
            .Select(source => SanctuaryOfTheForgotten.Evaluate(source, recipient, attacker)).ToArray();
        if (sources.Any(s => s.Reason == SanctuaryReason.MissingPosition)) return Result(SanctuaryDefenseReason.MissingPosition, sources);
        if (sources.Any(s => s.Reason == SanctuaryReason.SourceDeathUnresolved)) return Result(SanctuaryDefenseReason.SourceDeathUnresolved, sources);
        if (sources.Count(s => s.BonusApplies) > 1) return Result(SanctuaryDefenseReason.OverlapUnresolved, sources);
        return new(attacker.Id, recipient.Id, existing, SanctuaryDefenseReason.Resolved, sources,
            sources.Any(s => s.BonusApplies) ? SanctuaryOfTheForgotten.DefenseBonus : 0);

        SanctuaryDefenseResult Result(SanctuaryDefenseReason reason, SanctuaryEvaluation[] sources)
            => new(attacker.Id, recipient.Id, existing, reason, sources);
    }

    internal int RequireEffectiveDefense(Character attacker, Character recipient)
    {
        var result = Evaluate(attacker, recipient);
        if (!result.IsResolved) throw new SanctuaryDefenseUnresolvedException(result);
        return result.EffectiveDefense!.Value;
    }
}
