using System;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Turns.Restrictions;

namespace PawnsOfDivinity.Core.Turns;

public sealed record AttackActionResult
{
    public string CharacterId { get; }
    public string TargetId { get; }
    public string ActionId { get; }
    public int RoundNumber { get; }
    public AttackIntentResult Validation { get; }
    public AbilityTargetingResult? Targeting => Validation.Targeting;
    public CombatExchangeResult? Combat { get; }
    public TauntActionResult? Completion { get; }
    public bool CanExecute => Combat is null && Validation.IsAllowed;
    public bool Completed => Combat is not null && Completion?.IsAllowed == true;
    internal AttackActionResult(AttackIntent intent, AttackIntentResult validation,
        CombatExchangeResult? combat = null, TauntActionResult? completion = null)
    {
        CharacterId = intent.CharacterId;
        TargetId = intent.TargetId;
        ActionId = intent.ActionId;
        RoundNumber = intent.RoundNumber;
        Validation = validation;
        Combat = combat;
        Completion = completion;
    }
}

/// <summary>Player-action boundary for ordinary attacks; combat calculations remain independently usable.</summary>
/// <remarks>
/// Ability attacks must use their own executor and AbilityUseFinalizer, not this ordinary-attack
/// adapter. All legality providers are read-only; match operations run synchronously. A later
/// random-provider exception can leave earlier damage applied; no rollback is promised.
/// </remarks>
public sealed class AttackActionExecutor
{
    private readonly CombatExchangeResolver _combat;
    public AttackActionExecutor(CombatExchangeResolver combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        _combat = combat;
    }

    public AttackActionResult Preview(AttackIntent intent, CombatExchangeRequest request, AttackConfiguration attack)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(attack);
        if (!intent.IsAttack || !ReferenceEquals(intent.Character, request.Attacker) || !ReferenceEquals(intent.Target, request.Defender))
            return new(intent, new(intent, AttackIntentOutcome.ContextChanged));
        var effective = DisarmEligibility.GetEffectiveAttack(request.Attacker, attack, request.AttackerUnarmedAttack);
        // This metadata is just an adapter to the existing range validator; it registers no ability
        // and supplies no balance defaults. The opening configuration belongs to the caller.
        var metadata = new AbilityDefinition(intent.ActionId, "Ordinary attack", "Caller-configured attack range.",
            range: new(AbilityRangeKind.ManhattanDistance, effective.MinimumRange, effective.MaximumRange));
        var validation = AttackIntentValidation.Evaluate(intent, metadata);
        if (validation.IsAllowed) CombatExchangeResolver.ValidateRequest(PrepareRequest(request, effective, validation)
            with { Sanctuary = SanctuaryDefenseContext.FromActivations(intent.Activations) });
        return new(intent, validation);
    }

    public AttackActionResult Execute(AttackIntent intent, CombatExchangeRequest request, AttackConfiguration attack)
    {
        var preview = Preview(intent, request, attack);
        if (!preview.CanExecute) return preview;
        var started = intent.Activations.BeginApprovedMainAction(intent.Character, preview.Validation.TauntApproval!);
        if (!started.IsAllowed)
            return new(intent, new(intent, AttackIntentOutcome.ContextChanged, preview.Targeting, started));
        var effective = DisarmEligibility.GetEffectiveAttack(request.Attacker, attack, request.AttackerUnarmedAttack);
        var combat = _combat.Resolve(PrepareRequest(request, effective, preview.Validation)
            with { Sanctuary = SanctuaryDefenseContext.FromActivations(intent.Activations) });
        var completed = intent.Activations.CompleteApprovedMainAction(intent.Character, preview.Validation.TauntApproval!);
        return new(intent, preview.Validation, combat, completed);
    }

    private static CombatExchangeRequest PrepareRequest(CombatExchangeRequest request, AttackConfiguration attack,
        AttackIntentResult validation) => request with
    {
        OpeningAttackType = attack.Type, OpeningPower = attack.Power, OpeningBaseAccuracy = attack.BaseAccuracy,
        AttackerWeapon = attack.Category, Distance = checked((int)validation.Targeting!.Distance!.Value)
    };
}
