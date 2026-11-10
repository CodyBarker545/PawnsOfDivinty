using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Core.Abilities;

public enum ProvokingRoarEffectRejectionReason
{
    None, NotNigel, CasterDefeated, MissingCasterPosition, TargetingInformationRequired
}

public enum ProvokingRoarRecipientOutcome
{
    Affected, AlliedCharacter, AlreadyActivated, Defeated, OutsideRadius, TargetingRejected
}

/// <summary>A recipient decision captured at the time of evaluation, not a status application.</summary>
public sealed record ProvokingRoarRecipientResult
{
    public string CharacterId { get; }
    public ProvokingRoarRecipientOutcome Outcome { get; }
    public AbilityTargetingResult? Targeting { get; }

    internal ProvokingRoarRecipientResult(Character character, ProvokingRoarRecipientOutcome outcome,
        AbilityTargetingResult? targeting = null)
    {
        CharacterId = character.Id;
        Outcome = outcome;
        Targeting = targeting;
    }
}

/// <summary>Immutable area selection for this round. It does not assert that a taunt has been applied.</summary>
public sealed record ProvokingRoarEffectResult
{
    public string CasterId { get; }
    public GridPosition? Center { get; }
    public int RoundNumber { get; }
    public ProvokingRoarEffectRejectionReason RejectionReason { get; }
    public IReadOnlyList<ProvokingRoarRecipientResult> Recipients { get; }
    public IReadOnlyList<string> AffectedCharacterIds { get; }
    public bool IsAreaResolved => RejectionReason == ProvokingRoarEffectRejectionReason.None;

    internal ProvokingRoarEffectResult(Character caster, int round, ProvokingRoarEffectRejectionReason reason,
        IEnumerable<ProvokingRoarRecipientResult>? recipients = null)
    {
        CasterId = caster.Id;
        Center = caster.TacticalPosition;
        RoundNumber = round;
        RejectionReason = reason;
        var snapshot = recipients?.ToArray() ?? Array.Empty<ProvokingRoarRecipientResult>();
        Recipients = Array.AsReadOnly(snapshot);
        AffectedCharacterIds = Array.AsReadOnly(snapshot.Where(r => r.Outcome == ProvokingRoarRecipientOutcome.Affected)
            .Select(r => r.CharacterId).ToArray());
    }
}

/// <summary>Finds Roar's confirmed recipients without inventing taunt state or movement rules.</summary>
public static class ProvokingRoarEffect
{
    /// <remarks>
    /// The roar is centered on Nigel, not on a selected tile or enemy. Terrain/LOS/elevation
    /// requirements come from the caller. Unknown rules or missing eligible-enemy positions
    /// leave the whole area unresolved, rather than silently applying only part of it.
    /// The resulting restriction would last until this round ends, not for a timed-status tick.
    /// </remarks>
    public static ProvokingRoarEffectResult Evaluate(Character caster, IEnumerable<Character> participants, int round,
        AbilityTargetingConstraints targetingRules, IReadOnlyDictionary<string, bool>? mapTargetLegality = null)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(targetingRules);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        var roster = participants.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var instances = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        foreach (var character in roster)
        {
            if (character is null) throw new ArgumentException("The roster cannot contain null characters.", nameof(participants));
            if (!instances.Add(character) || !ids.Add(character.Id))
                throw new ArgumentException("Roster instances and instance IDs must be unique.", nameof(participants));
        }
        if (caster.Definition?.Id != "nigel") return Rejected(ProvokingRoarEffectRejectionReason.NotNigel);
        if (caster.IsDefeated) return Rejected(ProvokingRoarEffectRejectionReason.CasterDefeated);
        if (!caster.TacticalPosition.HasValue) return Rejected(ProvokingRoarEffectRejectionReason.MissingCasterPosition);
        var ability = Nigel.Definition.Abilities.Single(a => a.Id == Nigel.ProvokingRoarId);
        var constraints = new AbilityTargetingConstraints(AbilityTargetType.Character, false, AbilityTargetTeam.Enemy,
            true, targetingRules.RequiresMapValidation, targetingRules.RequiresLineOfSight, targetingRules.RequiresElevationRules);
        var results = new List<ProvokingRoarRecipientResult>();
        bool unresolved = targetingRules.TargetType != AbilityTargetType.Character;
        foreach (var character in roster)
        {
            if (character.TeamId == caster.TeamId) { Add(ProvokingRoarRecipientOutcome.AlliedCharacter); continue; }
            if (character.IsDefeated) { Add(ProvokingRoarRecipientOutcome.Defeated); continue; }
            if (character.HasActedThisRound) { Add(ProvokingRoarRecipientOutcome.AlreadyActivated); continue; }
            bool? mapLegal = mapTargetLegality is not null && mapTargetLegality.TryGetValue(character.Id, out bool legal)
                ? legal : null;
            var targeting = AbilityTargeting.Validate(caster, caster.TacticalPosition, ability,
                new(AbilityTargetType.Character, character.TacticalPosition, character), constraints, mapTargetLegal: mapLegal);
            if (targetingRules.TargetType != AbilityTargetType.Character)
                targeting = new(targetingRules.TargetType.HasValue ? AbilityTargetingRejectionReason.InvalidTargetType
                    : AbilityTargetingRejectionReason.UnresolvedTargetingRule,
                    caster.TacticalPosition, character.TacticalPosition, targeting.Distance);
            var outcome = targeting.IsLegal ? ProvokingRoarRecipientOutcome.Affected
                : targeting.RejectionReason == AbilityTargetingRejectionReason.OutsideMaximumRange
                    ? ProvokingRoarRecipientOutcome.OutsideRadius : ProvokingRoarRecipientOutcome.TargetingRejected;
            if (outcome == ProvokingRoarRecipientOutcome.TargetingRejected) unresolved = true;
            results.Add(new(character, outcome, targeting));

            void Add(ProvokingRoarRecipientOutcome excluded) => results.Add(new(character, excluded));
        }
        return new(caster, round, unresolved ? ProvokingRoarEffectRejectionReason.TargetingInformationRequired
            : ProvokingRoarEffectRejectionReason.None, results);

        ProvokingRoarEffectResult Rejected(ProvokingRoarEffectRejectionReason reason) => new(caster, round, reason);
    }
}
