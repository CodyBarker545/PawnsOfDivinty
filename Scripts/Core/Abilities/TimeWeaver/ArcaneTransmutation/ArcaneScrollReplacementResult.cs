using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Equipment;

namespace PawnsOfDivinity.Core.Abilities;

public enum ArcaneScrollReplacementRejectionReason
{
    None,
    CasterDefeated,
    NotTimeWeaver,
    EquippedScrollRequired,
    CurrentLevelUnresolved,
    CatalogRequired,
    NoEligibleAlternative,
    InvalidRandomIndex,
    InvalidPreviewContext,
    EquippedScrollChanged,
    ReplacementRejected
}

/// <summary>An immutable candidate preview or equipment replacement outcome, not a completed ability cast.</summary>
public sealed record ArcaneScrollReplacementResult
{
    public string CharacterId { get; }
    public ScrollDefinition? OriginalScroll { get; }
    public string? OriginalScrollId => OriginalScroll?.Id;
    public ScrollDefinition? ReplacementScroll { get; }
    public string? ReplacementScrollId => ReplacementScroll?.Id;
    public IReadOnlyList<ScrollDefinition> Candidates { get; }
    public IReadOnlyList<string> EligibleCandidateIds { get; }
    public int EligibleCandidateCount => Candidates.Count;
    public int? SelectedIndex { get; }
    public ScrollDefinition? SelectedScroll => SelectedIndex is int index && index >= 0 && index < Candidates.Count
        ? Candidates[index] : null;
    public ArcaneScrollReplacementRejectionReason RejectionReason { get; }
    public ScrollReplacementResult? ReplacementValidation { get; }
    public bool CanReplace => RejectionReason == ArcaneScrollReplacementRejectionReason.None && Candidates.Count > 0;
    public bool Succeeded => ReplacementValidation?.WasApplied == true;
    internal Character Caster { get; }

    internal ArcaneScrollReplacementResult(Character caster, ScrollDefinition? original,
        IEnumerable<ScrollDefinition>? candidates = null,
        ArcaneScrollReplacementRejectionReason reason = ArcaneScrollReplacementRejectionReason.None,
        ScrollDefinition? replacement = null, int? selectedIndex = null, ScrollReplacementResult? validation = null)
    {
        Caster = caster;
        CharacterId = caster.Id;
        OriginalScroll = original;
        ReplacementScroll = replacement;
        var snapshot = candidates?.ToArray() ?? Array.Empty<ScrollDefinition>();
        Candidates = Array.AsReadOnly(snapshot);
        EligibleCandidateIds = Array.AsReadOnly(snapshot.Select(scroll => scroll.Id).ToArray());
        RejectionReason = reason;
        SelectedIndex = selectedIndex;
        ReplacementValidation = validation;
    }
}
