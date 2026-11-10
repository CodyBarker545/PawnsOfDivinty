using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Persistent progress owned by one Character; neither defeat nor round preparation resets it.</summary>
public sealed class TemporalAccumulationProgress
{
    private readonly List<string> _elements = new();
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _events = new(StringComparer.Ordinal);
    public int StackCount => _elements.Count;
    public IReadOnlyList<string> KnownElements => Array.AsReadOnly(_elements.ToArray());
    internal TemporalAccumulationProgress() { }
    internal int? PreviousStacksFor(string eventId) => _events.TryGetValue(eventId, out int previous) ? previous : null;

    internal bool Record(string eventId, string elementId, bool award)
    {
        bool earned = award && !_known.Contains(elementId);
        if (earned) _ = checked(StackCount + 1);
        _events.Add(eventId, StackCount);
        if (earned) { _known.Add(elementId); _elements.Add(elementId); }
        return earned;
    }
}

public enum TemporalAccumulationOutcome
{
    StackEarned, ElementAlreadyKnown, AlreadyProcessed, NotPassiveOwner,
    NotCompleted, ElementRequired, UnresolvedMissOrInterruption
}

/// <summary>Records progress after a completed elemental attack; its bonus snapshot belongs to that attack.</summary>
public sealed record TemporalAccumulationResult
{
    public string CharacterId { get; }
    public string EventId { get; }
    public string? ElementId { get; }
    public TemporalAccumulationOutcome Outcome { get; }
    public int PreviousStacks { get; }
    public int ResultingStacks { get; }
    public int MagicalDamageBonusForThisUse => PreviousStacks;
    public IReadOnlyList<string> KnownElements { get; }

    internal TemporalAccumulationResult(Character character, string eventId, string? elementId,
        TemporalAccumulationOutcome outcome, int previousStacks)
    {
        CharacterId = character.Id;
        EventId = eventId;
        ElementId = elementId;
        Outcome = outcome;
        PreviousStacks = previousStacks;
        ResultingStacks = character.TemporalAccumulation.StackCount;
        KnownElements = character.TemporalAccumulation.KnownElements;
    }
}

public static class TemporalAccumulation
{
    /// <summary>Returns the bonus already earned before an attack starts; it does not award a stack.</summary>
    public static int GetMagicalDamageBonus(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return OwnsPassive(character) ? character.TemporalAccumulation.StackCount : 0;
    }

    /// <summary>Call once after the whole elemental use completes, not during preview or delayed-damage resolution.</summary>
    /// <remarks>
    /// eventId identifies the whole attack/use. elementId identifies its element independently
    /// of scroll level; no canonical elemental pool is invented. The supplied strike is the
    /// completed cast/attack outcome from the existing resolver, not an invented ability event.
    /// The attack must have used a bonus snapshot from before this call. Ambiguous miss and
    /// interruption events are remembered but award nothing until their rules are finalized.
    /// This never applies damage, consumes actions, clears progress or imposes an invented cap.
    /// </remarks>
    public static TemporalAccumulationResult Process(Character character, string eventId, string? elementId,
        StrikeResult? completedStrike, bool interrupted = false)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (elementId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        if (interrupted && completedStrike is not null)
            throw new ArgumentException("An interrupted use cannot also supply a completed strike.", nameof(completedStrike));
        int previous = GetMagicalDamageBonus(character);
        if (!OwnsPassive(character)) return Result(TemporalAccumulationOutcome.NotPassiveOwner);
        if (character.TemporalAccumulation.PreviousStacksFor(eventId) is { } recordedPrevious)
        {
            previous = recordedPrevious;
            return Result(TemporalAccumulationOutcome.AlreadyProcessed);
        }
        if (elementId is null) return Result(TemporalAccumulationOutcome.ElementRequired);
        if (completedStrike is null && !interrupted) return Result(TemporalAccumulationOutcome.NotCompleted);
        if (interrupted || !completedStrike!.Hit)
        {
            character.TemporalAccumulation.Record(eventId, elementId, award: false);
            return Result(TemporalAccumulationOutcome.UnresolvedMissOrInterruption);
        }
        bool earned = character.TemporalAccumulation.Record(eventId, elementId, award: true);
        return Result(earned ? TemporalAccumulationOutcome.StackEarned : TemporalAccumulationOutcome.ElementAlreadyKnown);

        TemporalAccumulationResult Result(TemporalAccumulationOutcome outcome) => new(character, eventId, elementId, outcome, previous);
    }

    private static bool OwnsPassive(Character character) => character.Definition?.Id == "time_weaver"
        && character.Definition.Abilities.Any(a => a.Id == TimeWeaver.TemporalAccumulationId
            && a.Classification == AbilityClassification.Passive);
}
