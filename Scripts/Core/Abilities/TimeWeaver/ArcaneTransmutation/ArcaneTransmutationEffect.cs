using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Equipment;

namespace PawnsOfDivinity.Core.Abilities;

public enum ArcaneTransmutationEffectOutcome
{
    CasterDefeated,
    NotTimeWeaver,
    EquippedScrollRequired,
    AlternativePoolRequired,
    NoSameLevelAlternative,
    EquipmentAndAttackIntegrationRequired
}

/// <summary>Immutable input validation, not an equipment assignment or a randomly chosen replacement.</summary>
public sealed record ArcaneTransmutationEffectResult
{
    public string CasterId { get; }
    public string? CurrentScrollId { get; }
    public int? CurrentLevel { get; }
    public IReadOnlyList<string> EligibleAlternativeIds { get; }
    public ArcaneTransmutationEffectOutcome Outcome { get; }

    internal ArcaneTransmutationEffectResult(Character caster, string? currentId, int? level,
        ArcaneTransmutationEffectOutcome outcome, IEnumerable<string>? candidates = null)
    {
        CasterId = caster.Id;
        CurrentScrollId = currentId;
        CurrentLevel = level;
        Outcome = outcome;
        EligibleAlternativeIds = Array.AsReadOnly(candidates?.ToArray() ?? Array.Empty<string>());
    }
}

/// <summary>Validates Transmutation inputs and performs only its random equipment-replacement sub-operation.</summary>
public static class ArcaneTransmutationEffect
{
    /// <summary>Builds an immutable compatible candidate snapshot from authoritative equipped state, without randomness.</summary>
    public static ArcaneScrollReplacementResult PreviewReplacement(Character caster, IEnumerable<ScrollDefinition>? catalog)
    {
        ArgumentNullException.ThrowIfNull(caster);
        var current = caster.EquippedScroll;
        if (caster.IsDefeated) return Rejected(ArcaneScrollReplacementRejectionReason.CasterDefeated);
        if (caster.Definition?.Id != "time_weaver") return Rejected(ArcaneScrollReplacementRejectionReason.NotTimeWeaver);
        if (current is null) return Rejected(ArcaneScrollReplacementRejectionReason.EquippedScrollRequired);
        if (!current.Level.HasValue) return Rejected(ArcaneScrollReplacementRejectionReason.CurrentLevelUnresolved);
        if (catalog is null) return Rejected(ArcaneScrollReplacementRejectionReason.CatalogRequired);
        var supplied = catalog.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        // Validate even entries that would be filtered out, so malformed catalogs never get partially accepted.
        foreach (var scroll in supplied)
        {
            if (scroll is null) throw new ArgumentException("Scroll catalogs cannot contain null definitions.", nameof(catalog));
            if (!ids.Add(scroll.Id)) throw new ArgumentException("Scroll catalog IDs must be unique.", nameof(catalog));
        }
        var eligible = supplied.Where(scroll => ScrollReplacementValidation.Validate(current, scroll).IsCompatible).ToArray();
        return new(caster, current, eligible, eligible.Length == 0
            ? ArcaneScrollReplacementRejectionReason.NoEligibleAlternative : ArcaneScrollReplacementRejectionReason.None);

        ArcaneScrollReplacementResult Rejected(ArcaneScrollReplacementRejectionReason reason) => new(caster, current, reason: reason);
    }

    /// <summary>Selects and replaces only. The future combined executor must still perform the immediate attack and finalization.</summary>
    /// <remarks>
    /// This is not a standalone player action. It consumes no action or cooldown and awards no Time Stack.
    /// Catalog enumeration and RNG must not mutate match state, and match operations must not run concurrently.
    /// </remarks>
    public static ArcaneScrollReplacementResult ExecuteReplacement(Character caster, IEnumerable<ScrollDefinition>? catalog,
        IRandomIndexProvider random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return ExecuteValidatedReplacement(PreviewReplacement(caster, catalog), random);
    }

    /// <summary>Rejects stale equipped state and revalidates the current catalog instead of trusting a preview's candidates.</summary>
    public static ArcaneScrollReplacementResult ExecuteReplacement(Character caster, ArcaneScrollReplacementResult preview,
        IEnumerable<ScrollDefinition>? catalog, IRandomIndexProvider random)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(random);
        if (!ReferenceEquals(preview.Caster, caster))
            return new(caster, caster.EquippedScroll, reason: ArcaneScrollReplacementRejectionReason.InvalidPreviewContext);
        if (!ReferenceEquals(caster.EquippedScroll, preview.OriginalScroll))
            return new(caster, preview.OriginalScroll, reason: ArcaneScrollReplacementRejectionReason.EquippedScrollChanged);
        return ExecuteValidatedReplacement(PreviewReplacement(caster, catalog), random);
    }

    private static ArcaneScrollReplacementResult ExecuteValidatedReplacement(ArcaneScrollReplacementResult preview, IRandomIndexProvider random)
    {
        var selection = SelectReplacement(preview, random);
        if (!selection.CanReplace) return selection;
        var applied = preview.Caster.ReplaceScroll(selection.SelectedScroll);
        return new(preview.Caster, preview.OriginalScroll, preview.Candidates,
            applied.WasApplied ? ArcaneScrollReplacementRejectionReason.None : ArcaneScrollReplacementRejectionReason.ReplacementRejected,
            applied.WasApplied ? selection.SelectedScroll : null, selection.SelectedIndex, applied);
    }

    // Selection is separate from assignment because the combined ability must check the
    // chosen scroll's range before it can commit equipment or start combat.
    internal static ArcaneScrollReplacementResult SelectReplacement(ArcaneScrollReplacementResult preview, IRandomIndexProvider random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (!preview.CanReplace) return preview;
        int index = random.NextIndex(preview.EligibleCandidateCount);
        if (index < 0 || index >= preview.EligibleCandidateCount)
            return Result(ArcaneScrollReplacementRejectionReason.InvalidRandomIndex);
        if (preview.Caster.IsDefeated) return Result(ArcaneScrollReplacementRejectionReason.CasterDefeated);
        if (!ReferenceEquals(preview.Caster.EquippedScroll, preview.OriginalScroll))
            return Result(ArcaneScrollReplacementRejectionReason.EquippedScrollChanged);
        var selected = preview.Candidates[index];
        var validation = preview.Caster.ValidateScrollReplacement(selected);
        if (!validation.IsCompatible) return Result(ArcaneScrollReplacementRejectionReason.ReplacementRejected, validation);
        return new(preview.Caster, preview.OriginalScroll, preview.Candidates, selectedIndex: index, validation: validation);

        ArcaneScrollReplacementResult Result(ArcaneScrollReplacementRejectionReason reason, ScrollReplacementResult? validation = null)
            => new(preview.Caster, preview.OriginalScroll, preview.Candidates, reason, selectedIndex: index, validation: validation);
    }

    /// <remarks>
    /// IDs and levels are caller-supplied metadata, not a canonical scroll catalog or authoritative
    /// equipped state. This compatibility API performs metadata validation only; typed scroll
    /// replacement uses PreviewReplacement/ExecuteReplacement above. The immediate attack and
    /// combined ability finalization remain outside this operation.
    /// </remarks>
    public static ArcaneTransmutationEffectResult Evaluate(Character caster, string? currentScrollId = null,
        int? currentLevel = null, IEnumerable<(string Id, int Level)>? availableScrolls = null)
    {
        ArgumentNullException.ThrowIfNull(caster);
        if (currentScrollId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(currentScrollId);
        if (currentLevel.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(currentLevel.Value, nameof(currentLevel));
        if (caster.IsDefeated) return Result(ArcaneTransmutationEffectOutcome.CasterDefeated);
        if (caster.Definition?.Id != "time_weaver") return Result(ArcaneTransmutationEffectOutcome.NotTimeWeaver);
        if (currentScrollId is null || !currentLevel.HasValue) return Result(ArcaneTransmutationEffectOutcome.EquippedScrollRequired);
        if (availableScrolls is null) return Result(ArcaneTransmutationEffectOutcome.AlternativePoolRequired);
        var pool = availableScrolls.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scroll in pool)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scroll.Id);
            ArgumentOutOfRangeException.ThrowIfNegative(scroll.Level, nameof(availableScrolls));
            if (!ids.Add(scroll.Id)) throw new ArgumentException("Scroll IDs must be unique in the supplied pool.", nameof(availableScrolls));
        }
        var candidates = pool.Where(scroll => scroll.Level == currentLevel.Value
            && !StringComparer.Ordinal.Equals(scroll.Id, currentScrollId)).Select(scroll => scroll.Id).ToArray();
        return candidates.Length == 0 ? Result(ArcaneTransmutationEffectOutcome.NoSameLevelAlternative)
            : new(caster, currentScrollId, currentLevel, ArcaneTransmutationEffectOutcome.EquipmentAndAttackIntegrationRequired, candidates);

        ArcaneTransmutationEffectResult Result(ArcaneTransmutationEffectOutcome outcome)
            => new(caster, currentScrollId, currentLevel, outcome);
    }
}
