using System;

namespace PawnsOfDivinity.Core.Equipment;

public enum ScrollReplacementRejectionReason
{
    None,
    NoEquippedScroll,
    ReplacementRequired,
    SameScroll,
    CurrentLevelUnresolved,
    ReplacementLevelUnresolved,
    DifferentLevel,
    CombatMetadataUnresolved
}

/// <summary>An immutable compatibility decision. Validation alone never changes equipped state.</summary>
public sealed record ScrollReplacementResult
{
    public string? PreviousScrollId { get; }
    public string? ProposedScrollId { get; }
    public ScrollReplacementRejectionReason RejectionReason { get; }
    public bool IsCompatible => RejectionReason == ScrollReplacementRejectionReason.None;
    public bool WasApplied { get; }

    internal ScrollReplacementResult(ScrollDefinition? current, ScrollDefinition? proposed,
        ScrollReplacementRejectionReason reason, bool wasApplied = false)
    {
        PreviousScrollId = current?.Id;
        ProposedScrollId = proposed?.Id;
        RejectionReason = reason;
        WasApplied = wasApplied;
    }
}

/// <summary>Checks the documented different-ID, same-level requirement for future Arcane Transmutation.</summary>
public static class ScrollReplacementValidation
{
    public static ScrollReplacementResult Validate(ScrollDefinition? current, ScrollDefinition? proposed)
    {
        if (current is null) return Result(ScrollReplacementRejectionReason.NoEquippedScroll);
        if (proposed is null) return Result(ScrollReplacementRejectionReason.ReplacementRequired);
        if (StringComparer.Ordinal.Equals(current.Id, proposed.Id)) return Result(ScrollReplacementRejectionReason.SameScroll);
        if (!current.Level.HasValue) return Result(ScrollReplacementRejectionReason.CurrentLevelUnresolved);
        if (!proposed.Level.HasValue) return Result(ScrollReplacementRejectionReason.ReplacementLevelUnresolved);
        if (current.Level != proposed.Level) return Result(ScrollReplacementRejectionReason.DifferentLevel);
        if (!proposed.HasResolvedCombatProperties) return Result(ScrollReplacementRejectionReason.CombatMetadataUnresolved);
        return Result(ScrollReplacementRejectionReason.None);

        ScrollReplacementResult Result(ScrollReplacementRejectionReason reason) => new(current, proposed, reason);
    }
}
