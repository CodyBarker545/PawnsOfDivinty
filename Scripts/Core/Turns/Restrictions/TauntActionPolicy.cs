using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Turns.Restrictions;

/// <summary>Resolved information from movement/attack legality checks, not a movement plan or an attack command.</summary>
public sealed record TauntAttackAvailability
{
    public string SourceCharacterId { get; }
    public bool? MovementCompleted { get; }
    public bool? SourceApplicable { get; }
    public bool? LegalAttackAvailable { get; }
    public TauntAttackAvailability(string sourceCharacterId, bool? movementCompleted, bool? sourceApplicable, bool? legalAttackAvailable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCharacterId);
        SourceCharacterId = sourceCharacterId;
        MovementCompleted = movementCompleted;
        SourceApplicable = sourceApplicable;
        LegalAttackAvailable = legalAttackAvailable;
    }
}

public sealed record MainActionProposal
{
    public bool IsAttack { get; }
    public string? TargetCharacterId { get; }
    public TauntAttackAvailability? RequiredAttack { get; }
    public MainActionProposal(bool isAttack, string? targetCharacterId = null, TauntAttackAvailability? requiredAttack = null)
    {
        if (targetCharacterId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(targetCharacterId);
        IsAttack = isAttack;
        TargetCharacterId = targetCharacterId;
        RequiredAttack = requiredAttack;
    }
}

public enum TauntActionOutcome
{
    Allowed, WrongActiveCharacter, CharacterDefeated, WrongPhase, MainActionUnavailable,
    AttackContextRequired, SourceContextMismatch, SourceApplicabilityUnresolved, MovementDecisionRequired,
    AttackLegalityUnresolved, MustAttackSource, MustEndActivation, UtilityRuleUnresolved, ActivationEnded,
    LifetimeUnresolved, UtilityActionUnavailable, ApprovalContextChanged
}

public sealed record TauntActionResult
{
    public string CharacterId { get; }
    public string? RestrictionId { get; }
    public string? SourceCharacterId { get; }
    public string? ProposedTargetId { get; }
    public TauntActionOutcome Outcome { get; }
    public bool IsAllowed => Outcome == TauntActionOutcome.Allowed;
    public bool RequiresEndActivation => Outcome == TauntActionOutcome.MustEndActivation;
    public bool ActivationEnded => Outcome == TauntActionOutcome.ActivationEnded;
    public bool ReleasesTaunt { get; }
    internal ActivationManager? Manager { get; }
    internal Character Character { get; }
    internal int? ApprovedRound { get; }
    internal TauntActionResult(Character character, TauntActionOutcome outcome, TauntRestriction? restriction = null,
        string? targetId = null, ActivationManager? manager = null, bool releasesTaunt = false)
    {
        CharacterId = character.Id;
        RestrictionId = restriction?.Id;
        SourceCharacterId = restriction?.SourceCharacterId;
        ProposedTargetId = targetId;
        Outcome = outcome;
        Character = character;
        Manager = manager;
        ApprovedRound = manager?.CurrentRound;
        ReleasesTaunt = releasesTaunt;
    }
}

/// <summary>Checks only the taunt restriction; ordinary attack legality, Silence and Disarm remain separate checks.</summary>
public static class TauntActionPolicy
{
    public static TauntActionResult Evaluate(Character character, TauntRestriction? restriction, Character? source,
        int round, bool movementRecorded, MainActionProposal? proposal)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        if (character.IsDefeated) return Result(TauntActionOutcome.CharacterDefeated);
        if (restriction is not null && restriction.RestrictedCharacterId != character.Id)
            throw new ArgumentException("The restriction belongs to a different character.", nameof(restriction));
        if (restriction is not null && !restriction.ExpiresAtEndOfRound.HasValue) return Result(TauntActionOutcome.LifetimeUnresolved);
        if (restriction is null || !restriction.IsActiveInRound(round)) return Result(TauntActionOutcome.Allowed);
        if (source is null || source.IsDefeated)
            return new(character, TauntActionOutcome.Allowed, restriction, proposal?.TargetCharacterId, releasesTaunt: true);
        var availability = proposal?.RequiredAttack;
        if (availability is null) return Result(TauntActionOutcome.AttackContextRequired);
        if (availability.SourceCharacterId != restriction.SourceCharacterId || source?.Id != restriction.SourceCharacterId)
            return Result(TauntActionOutcome.SourceContextMismatch);
        if (availability.SourceApplicable == false)
            return new(character, TauntActionOutcome.Allowed, restriction, proposal?.TargetCharacterId, releasesTaunt: true);
        if (source.Definition?.Id != "nigel" || !availability.SourceApplicable.HasValue)
            return Result(TauntActionOutcome.SourceApplicabilityUnresolved);
        // The caller can confirm that movement was declined/completed without spending it.
        // No path is chosen and no movement is forced by this policy.
        if (!movementRecorded && availability.MovementCompleted != true) return Result(TauntActionOutcome.MovementDecisionRequired);
        if (!availability.LegalAttackAvailable.HasValue) return Result(TauntActionOutcome.AttackLegalityUnresolved);
        if (!availability.LegalAttackAvailable.Value) return Result(TauntActionOutcome.MustEndActivation);
        if (proposal!.IsAttack && proposal.TargetCharacterId == source.Id) return Result(TauntActionOutcome.Allowed);
        return Result(TauntActionOutcome.MustAttackSource);

        TauntActionResult Result(TauntActionOutcome outcome) => new(character, outcome, restriction, proposal?.TargetCharacterId);
    }
}
