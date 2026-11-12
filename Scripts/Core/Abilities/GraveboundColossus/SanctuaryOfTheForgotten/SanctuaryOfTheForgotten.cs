using System;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Core.Abilities;

public enum SanctuaryReason
{
    Applies, NotPassiveOwner, RecipientNotAlly, SourceExcluded, RecipientDefeated, AttackerNotEnemy,
    AttackerDefeated, OutsideRadius, AttackerNotAdjacent, MissingPosition, SourceDeathUnresolved
}

/// <summary>The condition for one incoming attack, captured without storing a buff on its recipient.</summary>
public sealed record SanctuaryEvaluation
{
    public string SourceId { get; }
    public string RecipientId { get; }
    public string AttackerId { get; }
    public GridPosition? SourcePosition { get; }
    public GridPosition? RecipientPosition { get; }
    public GridPosition? AttackerPosition { get; }
    public long? RecipientDistance { get; }
    public long? AttackerDistance { get; }
    public bool? ConditionSatisfied { get; }
    public SanctuaryReason Reason { get; }
    public bool IsResolved => Reason is not (SanctuaryReason.MissingPosition or SanctuaryReason.SourceDeathUnresolved);
    public bool BonusApplies => Reason == SanctuaryReason.Applies;
    public int? DefenseBonus => IsResolved ? (BonusApplies ? 3 : 0) : null;

    internal SanctuaryEvaluation(Character source, Character recipient, Character attacker, SanctuaryReason reason,
        long? recipientDistance, long? attackerDistance, bool? condition, Func<Character, GridPosition?>? positions = null)
    {
        SourceId = source.Id;
        RecipientId = recipient.Id;
        AttackerId = attacker.Id;
        SourcePosition = positions is null ? source.TacticalPosition : positions(source);
        RecipientPosition = positions is null ? recipient.TacticalPosition : positions(recipient);
        AttackerPosition = positions is null ? attacker.TacticalPosition : positions(attacker);
        RecipientDistance = recipientDistance;
        AttackerDistance = attackerDistance;
        ConditionSatisfied = condition;
        Reason = reason;
    }
}

/// <summary>Section 12.5: protects nearby allies against enemies adjacent to the protecting Colossus.</summary>
public static class SanctuaryOfTheForgotten
{
    public const int DefenseBonus = 3;
    public const int Radius = 2;

    public static SanctuaryEvaluation Evaluate(Character source, Character recipient, Character attacker)
        => EvaluateAt(source, recipient, attacker, c => c.TacticalPosition);

    // Movement preflight evaluates a proposed checkpoint without moving a runtime character.
    internal static SanctuaryEvaluation EvaluateAt(Character source, Character recipient, Character attacker,
        Func<Character, GridPosition?> positions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(attacker);
        long? recipientDistance = null, attackerDistance = null;
        if (!IsPassiveOwner(source)) return Result(SanctuaryReason.NotPassiveOwner, false);
        if (source.Id == recipient.Id) return Result(SanctuaryReason.SourceExcluded, false);
        if (source.TeamId != recipient.TeamId) return Result(SanctuaryReason.RecipientNotAlly, false);
        if (recipient.IsDefeated) return Result(SanctuaryReason.RecipientDefeated, false);
        if (attacker.TeamId == source.TeamId) return Result(SanctuaryReason.AttackerNotEnemy, false);
        if (attacker.IsDefeated) return Result(SanctuaryReason.AttackerDefeated, false);
        if (positions(source) is not { } center || positions(recipient) is not { } target)
            return Result(SanctuaryReason.MissingPosition, null);
        recipientDistance = center.ManhattanDistanceTo(target);
        if (recipientDistance > Radius) return Result(SanctuaryReason.OutsideRadius, false);
        if (positions(attacker) is not { } incoming) return Result(SanctuaryReason.MissingPosition, null);
        attackerDistance = center.ManhattanDistanceTo(incoming);
        if (attackerDistance != 1) return Result(SanctuaryReason.AttackerNotAdjacent, false);
        // Section 10 preserves effects placed on other characters, but does not explicitly
        // settle this continuously calculated aura after source death. Do not guess persistence.
        if (source.IsDefeated) return Result(SanctuaryReason.SourceDeathUnresolved, true);
        return Result(SanctuaryReason.Applies, true);

        SanctuaryEvaluation Result(SanctuaryReason reason, bool? condition)
            => new(source, recipient, attacker, reason, recipientDistance, attackerDistance, condition, positions);
    }

    internal static bool IsPassiveOwner(Character source) => source.Definition?.Id == "gravebound_colossus"
        && source.Definition.Abilities.Any(a => a.Id == GraveboundColossus.SanctuaryOfTheForgottenId
            && a.Classification == AbilityClassification.Passive);

    /// <summary>Full-match callers supply a context; legacy two-character arithmetic keeps its existing behavior.</summary>
    internal static int DefenseForStrike(Character attacker, Character recipient, SanctuaryDefenseContext? context)
        => context is null ? recipient.EffectiveDefense : context.RequireEffectiveDefense(attacker, recipient);

    public static SanctuaryDefenseResult EvaluateDefense(Character attacker, Character recipient, SanctuaryDefenseContext? context)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(recipient);
        return context?.Evaluate(attacker, recipient)
            ?? new(attacker.Id, recipient.Id, recipient.EffectiveDefense, SanctuaryDefenseReason.MissingContext, Array.Empty<SanctuaryEvaluation>());
    }
}
