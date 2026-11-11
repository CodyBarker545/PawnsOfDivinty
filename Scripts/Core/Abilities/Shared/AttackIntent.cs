using System;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Core.Turns.Restrictions;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>A target and trusted attack classification for one action in one activation context.</summary>
/// <remarks>
/// Providers return current legality information on each preview/execution check. They must be
/// read-only and deterministic for the supplied state. No classification is inferred from names.
/// An equipped range must describe this proposed attack, not an assumed starting weapon.
/// </remarks>
public sealed record AttackIntent
{
    public string CharacterId { get; }
    public string TargetId { get; }
    public string ActionId { get; }
    public bool IsAttack { get; }
    public int RoundNumber { get; }
    public AbilityTargetingConstraints TargetingRules { get; }
    internal Character Character { get; }
    internal Character Target { get; }
    internal ActivationManager Activations { get; }
    internal Func<TauntAttackAvailability?>? RequiredAttack { get; }
    internal Func<AbilityRange?>? EquipmentRange { get; }
    internal Func<bool?>? MapTargetLegal { get; }

    public AttackIntent(Character character, Character target, string actionId, ActivationManager activations,
        bool isAttack, AbilityTargetingConstraints targetingRules, Func<TauntAttackAvailability?>? requiredAttack = null,
        Func<AbilityRange?>? equipmentRange = null, Func<bool?>? mapTargetLegal = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(activations);
        ArgumentNullException.ThrowIfNull(targetingRules);
        Character = character;
        Target = target;
        CharacterId = character.Id;
        TargetId = target.Id;
        ActionId = actionId;
        Activations = activations;
        RoundNumber = activations.CurrentRound;
        IsAttack = isAttack;
        TargetingRules = targetingRules;
        RequiredAttack = requiredAttack;
        EquipmentRange = equipmentRange;
        MapTargetLegal = mapTargetLegal;
    }
}

public enum AttackIntentOutcome { Allowed, ContextChanged, TargetRejected, TauntRejected, TauntContextUnresolved }

public sealed record AttackIntentResult
{
    public string CharacterId { get; }
    public string TargetId { get; }
    public string ActionId { get; }
    public int RoundNumber { get; }
    public AbilityTargetingResult? Targeting { get; }
    public TauntActionResult? TauntApproval { get; }
    public AttackIntentOutcome Outcome { get; }
    public bool IsAllowed => Outcome == AttackIntentOutcome.Allowed;
    internal AttackIntentResult(AttackIntent intent, AttackIntentOutcome outcome,
        AbilityTargetingResult? targeting = null, TauntActionResult? taunt = null)
    {
        CharacterId = intent.CharacterId;
        TargetId = intent.TargetId;
        ActionId = intent.ActionId;
        RoundNumber = intent.RoundNumber;
        Outcome = outcome;
        Targeting = targeting;
        TauntApproval = taunt;
    }
}

/// <summary>Combines existing target legality with the existing Main Action policy, without effects.</summary>
public static class AttackIntentValidation
{
    public static AttackIntentResult Evaluate(AttackIntent intent, AbilityDefinition targetingMetadata)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(targetingMetadata);
        var manager = intent.Activations;
        if (intent.RoundNumber != manager.CurrentRound || intent.ActionId != targetingMetadata.Id
            || !manager.FirstTeam.Concat(manager.SecondTeam).Any(c => ReferenceEquals(c, intent.Target)))
            return new(intent, AttackIntentOutcome.ContextChanged);
        var targeting = AbilityTargeting.Validate(intent.Character, intent.Character.TacticalPosition, targetingMetadata,
            new(AbilityTargetType.Character, intent.Target.TacticalPosition, intent.Target), intent.TargetingRules,
            intent.EquipmentRange?.Invoke(), intent.MapTargetLegal?.Invoke());
        var restriction = manager.ActionRestrictions.GetActive(intent.CharacterId);
        var proposal = new MainActionProposal(intent.IsAttack, intent.TargetId,
            restriction is null ? null : intent.RequiredAttack?.Invoke());
        var policy = manager.EvaluateMainAction(intent.Character, proposal);
        if (!targeting.IsLegal) return new(intent, AttackIntentOutcome.TargetRejected, targeting, policy);
        if (policy.IsAllowed) return new(intent, AttackIntentOutcome.Allowed, targeting, policy);
        bool definiteRejection = policy.Outcome is TauntActionOutcome.MustAttackSource or TauntActionOutcome.MustEndActivation
            or TauntActionOutcome.WrongActiveCharacter or TauntActionOutcome.CharacterDefeated
            or TauntActionOutcome.MainActionUnavailable or TauntActionOutcome.WrongPhase;
        return new(intent, definiteRejection ? AttackIntentOutcome.TauntRejected : AttackIntentOutcome.TauntContextUnresolved,
            targeting, policy);
    }
}
