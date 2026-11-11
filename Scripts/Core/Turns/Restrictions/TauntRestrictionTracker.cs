using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Core.Turns.Restrictions;

public enum TauntApplicationOutcome
{
    Applied, LifetimeUnresolved, UnsupportedLifetime, WrongApplicationRound, DuplicateRestrictionId,
    DuplicateEffectRecipient, SourceUnavailable, RecipientUnavailable, ConflictingTauntUnresolved,
    PreservedFirstSource, WrongApplicationPhase
}

public enum TauntEndReason { InvalidSource, Cleanup }

public sealed record TauntEndResult
{
    public string RestrictionId { get; }
    public string SourceCharacterId { get; }
    public string RestrictedCharacterId { get; }
    public int RoundNumber { get; }
    public TauntEndReason Reason { get; }
    internal TauntEndResult(TauntRestriction restriction, int round, TauntEndReason reason)
    {
        RestrictionId = restriction.Id;
        SourceCharacterId = restriction.SourceCharacterId;
        RestrictedCharacterId = restriction.RestrictedCharacterId;
        RoundNumber = round;
        Reason = reason;
    }
}

public sealed record TauntBatchResult
{
    public IReadOnlyList<TauntApplicationResult> Results { get; }
    public bool IsValid => Results.All(r => r.Applied || r.Outcome == TauntApplicationOutcome.PreservedFirstSource);
    public bool WasApplied { get; }
    internal TauntBatchResult(IEnumerable<TauntApplicationResult> results, bool applied = false)
    {
        Results = Array.AsReadOnly(results.ToArray());
        WasApplied = applied;
    }
}

public sealed record TauntApplicationResult
{
    public TauntRestriction Restriction { get; }
    public TauntApplicationOutcome Outcome { get; }
    public bool Applied => Outcome == TauntApplicationOutcome.Applied;
    public TauntRestriction? ControllingRestriction { get; }
    internal TauntApplicationResult(TauntRestriction restriction, TauntApplicationOutcome outcome, TauntRestriction? controlling = null)
    {
        Restriction = restriction;
        Outcome = outcome;
        ControllingRestriction = controlling ?? (Applied ? restriction : null);
    }
}

/// <summary>Match-owned restriction storage; adding a record does not authorize or execute a Roar cast.</summary>
public sealed class TauntRestrictionTracker
{
    private readonly Dictionary<string, Character> _roster;
    private readonly Func<int> _currentRound;
    private readonly RoundLifecycleManager? _lifecycle;
    private readonly List<TauntRestriction> _restrictions = new();
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly HashSet<(string Effect, string Recipient)> _applications = new();
    private readonly Dictionary<string, TauntEndResult> _ended = new(StringComparer.Ordinal);
    public IReadOnlyList<TauntRestriction> Snapshots => Array.AsReadOnly(_restrictions.ToArray());
    public IReadOnlyList<TauntEndResult> EndedSnapshots => Array.AsReadOnly(_ended.Values.ToArray());

    internal TauntRestrictionTracker(IEnumerable<Character> roster, Func<int> currentRound, RoundLifecycleManager? lifecycle = null)
    {
        _roster = roster.ToDictionary(c => c.Id, StringComparer.Ordinal);
        _currentRound = currentRound;
        _lifecycle = lifecycle;
    }

    /// <remarks>
    /// Low-level restriction storage does not spend a cast. Batches validate every record before
    /// writing, preserve the first active source, and never refresh it. Preview does not retire
    /// invalid sources; that bookkeeping happens only when the batch is committed.
    /// </remarks>
    public TauntApplicationResult Apply(TauntRestriction restriction)
    {
        ArgumentNullException.ThrowIfNull(restriction);
        return ApplyBatch(new[] { restriction }).Results[0];
    }

    public TauntBatchResult PreviewBatch(IEnumerable<TauntRestriction> restrictions)
    {
        ArgumentNullException.ThrowIfNull(restrictions);
        var snapshot = restrictions.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var recipients = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<TauntApplicationResult>();
        foreach (var restriction in snapshot)
        {
            if (restriction is null) throw new ArgumentException("A restriction batch cannot contain null.", nameof(restrictions));
            if (!ids.Add(restriction.Id)) results.Add(new(restriction, TauntApplicationOutcome.DuplicateRestrictionId));
            else if (!recipients.Add(restriction.RestrictedCharacterId)) results.Add(new(restriction, TauntApplicationOutcome.DuplicateEffectRecipient));
            else results.Add(ValidateApplication(restriction));
        }
        return new(results);
    }

    public TauntBatchResult ApplyBatch(IEnumerable<TauntRestriction> restrictions)
    {
        var validation = PreviewBatch(restrictions);
        if (!validation.IsValid) return validation;
        foreach (var result in validation.Results)
        {
            var restriction = result.Restriction;
            _ids.Add(restriction.Id);
            _applications.Add((restriction.EffectId, restriction.RestrictedCharacterId));
            if (!result.Applied) continue;
            var previous = GetActive(restriction.RestrictedCharacterId);
            if (previous is not null && SourceDefinitelyInvalid(previous)) End(previous, TauntEndReason.InvalidSource);
            _restrictions.Add(restriction);
        }
        return new(validation.Results, applied: true);
    }

    private TauntApplicationResult ValidateApplication(TauntRestriction restriction)
    {
        if (!_roster.TryGetValue(restriction.SourceCharacterId, out var source)
            || !_roster.TryGetValue(restriction.RestrictedCharacterId, out var recipient))
            throw new ArgumentException("Both restriction participants must belong to this match roster.", nameof(restriction));
        if (!restriction.ExpiresAtEndOfRound.HasValue) return Result(TauntApplicationOutcome.LifetimeUnresolved);
        if (restriction.ExpiresAtEndOfRound != restriction.ApplicationRound) return Result(TauntApplicationOutcome.UnsupportedLifetime);
        if (restriction.ApplicationRound != _currentRound()) return Result(TauntApplicationOutcome.WrongApplicationRound);
        if (_lifecycle is not null && _lifecycle.CurrentPhase != RoundPhase.AlternatingActivations)
            return Result(TauntApplicationOutcome.WrongApplicationPhase);
        if (_ids.Contains(restriction.Id)) return Result(TauntApplicationOutcome.DuplicateRestrictionId);
        if (_applications.Contains((restriction.EffectId, recipient.Id))) return Result(TauntApplicationOutcome.DuplicateEffectRecipient);
        if (source.IsDefeated || source.Definition?.Id != "nigel"
            || !source.Definition.Abilities.Any(a => a.Id == Nigel.ProvokingRoarId))
            return Result(TauntApplicationOutcome.SourceUnavailable);
        if (recipient.TeamId == source.TeamId || !recipient.CanAct) return Result(TauntApplicationOutcome.RecipientUnavailable);
        var controlling = GetActive(recipient.Id);
        if (controlling is not null && !SourceDefinitelyInvalid(controlling))
            return new(restriction, TauntApplicationOutcome.PreservedFirstSource, controlling);
        return Result(TauntApplicationOutcome.Applied);

        TauntApplicationResult Result(TauntApplicationOutcome outcome) => new(restriction, outcome);
    }

    public TauntRestriction? GetActive(string characterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        if (_lifecycle is not null && _lifecycle.CurrentPhase >= RoundPhase.Cleanup) return null;
        return _restrictions.FirstOrDefault(r => r.RestrictedCharacterId == characterId && !_ended.ContainsKey(r.Id)
            && r.IsActiveInRound(_currentRound()));
    }

    internal bool SourceDefinitelyInvalid(TauntRestriction restriction)
        => !_roster.TryGetValue(restriction.SourceCharacterId, out var source) || source.IsDefeated;

    internal void EndInvalidSource(string restrictionId)
    {
        var restriction = _restrictions.FirstOrDefault(r => r.Id == restrictionId);
        if (restriction is not null) End(restriction, TauntEndReason.InvalidSource);
    }

    internal bool EndedForInvalidSource(string restrictionId)
        => _ended.TryGetValue(restrictionId, out var end) && end.Reason == TauntEndReason.InvalidSource;

    public IReadOnlyList<TauntEndResult> ExpireAtCleanup(RoundLifecycleManager lifecycle)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        if (!ReferenceEquals(lifecycle, _lifecycle) || lifecycle.CurrentRound != _currentRound()
            || lifecycle.CurrentPhase != RoundPhase.Cleanup)
            throw new InvalidOperationException("Taunt expiration requires this match's current Cleanup phase.");
        var expired = _restrictions.Where(r => !_ended.ContainsKey(r.Id)
            && r.ExpiresAtEndOfRound <= lifecycle.CurrentRound).ToArray();
        foreach (var restriction in expired) End(restriction, TauntEndReason.Cleanup);
        return Array.AsReadOnly(expired.Select(r => _ended[r.Id]).ToArray());
    }

    private void End(TauntRestriction restriction, TauntEndReason reason)
    {
        if (!_ended.ContainsKey(restriction.Id)) _ended.Add(restriction.Id, new(restriction, _currentRound(), reason));
    }

    internal Character? FindSource(string id) => _roster.GetValueOrDefault(id);
}
