using System;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

public sealed record FrostbiteArrowUseResult
{
    public string AttackerId { get; }
    public string TargetId { get; }
    public string AbilityId { get; }
    public int RoundNumber { get; }
    public string BowId { get; }
    public AttackConfiguration? Bow { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public AbilityTargetingResult? Targeting { get; }
    public SanctuaryDefenseResult? Defense { get; }
    public CombatExchangeResult? Combat { get; }
    public int HealthLost => Combat?.OpeningHealthLost ?? 0;
    public int ShieldAbsorbed => Combat?.OpeningShieldAbsorbed ?? 0;
    public FrostbiteArrowProcResult? Proc { get; }
    public AbilityUseResult? Finalization { get; }
    public FrostbiteArrowReason RejectionReason { get; }
    public bool Succeeded => Finalization?.Succeeded == true;
    public bool CanExecute => !Succeeded && Combat is null && Eligibility.IsAllowed && Targeting?.IsLegal == true
        && RejectionReason == FrostbiteArrowReason.None;

    internal FrostbiteArrowUseResult(AbilityUse use, CombatExchangeRequest request, string bowId, AttackConfiguration? bow,
        AbilityEligibilityResult eligibility, FrostbiteArrowReason reason = FrostbiteArrowReason.None,
        AbilityTargetingResult? targeting = null, SanctuaryDefenseResult? defense = null,
        CombatExchangeResult? combat = null, FrostbiteArrowProcResult? proc = null, AbilityUseResult? finalization = null)
    {
        AttackerId = use.CharacterId;
        TargetId = request.Defender.Id;
        AbilityId = use.AbilityId;
        RoundNumber = use.RoundNumber;
        BowId = bowId;
        Bow = bow;
        Eligibility = eligibility;
        Targeting = targeting;
        Defense = defense;
        Combat = combat;
        Proc = proc;
        Finalization = finalization;
        RejectionReason = reason;
    }
}

/// <summary>
/// Executes only exchanges where the unresolved status timing cannot change a counter or follow-up sequence.
/// Those attacks are rejected rather than suppressing their normal reactions. Power, ground-target knowledge,
/// terrain provenance and the provisional duration are explicit inputs, not equipment or balance defaults.
/// </summary>
public sealed class FrostbiteArrowAbilityExecutor
{
    private readonly CombatExchangeResolver _combat;
    private readonly FrostbiteArrowEffect _effect;
    public FrostbiteArrowAbilityExecutor(CombatExchangeResolver combat, FrostbiteArrowEffect effect)
    {
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    public FrostbiteArrowUseResult Preview(AbilityUse use, CombatExchangeRequest request, string bowId,
        AttackConfiguration? bow, FrostbiteArrowContext rules)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Attacker);
        ArgumentNullException.ThrowIfNull(request.Defender);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(bowId);
        var initial = new AbilityEligibilityResult(use.CharacterId, use.AbilityId, AbilityRejectionReason.None);
        FrostbiteArrowUseResult Rejected(FrostbiteArrowReason reason) => new(use, request, bowId, bow, initial, reason);
        if (!ReferenceEquals(use.Character, request.Attacker)) return Rejected(FrostbiteArrowReason.ContextMismatch);
        if (use.AttackIntent is null) return Rejected(FrostbiteArrowReason.AttackIntentRequired);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, request.Defender, isAttack: true);
        if (!approval.Succeeded) return new(use, request, bowId, bow, approval.Eligibility,
            targeting: approval.Eligibility.AttackIntent?.Targeting);
        if (use.AbilityId != BeothukSpiritGuardian.FrostbiteArrowId)
            return new(use, request, bowId, bow, new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        if (use.FrostbiteArrowStarted) return Rejected(FrostbiteArrowReason.AlreadyStarted);
        if (use.Character.Definition?.Id != "beothuk_spirit_guardian") return Rejected(FrostbiteArrowReason.NotSpiritGuardian);
        if (!use.AttackIntent.IsAttack) return Rejected(FrostbiteArrowReason.ContextMismatch);
        if (bow is null) return Rejected(FrostbiteArrowReason.BowRequired);
        if (bow.Type != AttackType.Physical || bow.Category != WeaponCategory.Bow) return Rejected(FrostbiteArrowReason.PhysicalBowRequired);
        if (!DisarmEligibility.CanUseEquippedWeapon(use.Character)) return Rejected(FrostbiteArrowReason.BowUnavailable);
        if (bow.MinimumRange < 2) return Rejected(FrostbiteArrowReason.InvalidBowRange);
        var metadata = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        if (metadata.RequiredAction != AbilityActionType.MainAction || metadata.ReuseInterval != 0 || metadata.Range?.Kind != AbilityRangeKind.EquippedWeapon)
            return Rejected(FrostbiteArrowReason.UnsupportedMetadata);
        var intentRules = use.AttackIntent.TargetingRules;
        var constraints = new AbilityTargetingConstraints(intentRules.TargetType, false, AbilityTargetTeam.Enemy, true,
            intentRules.RequiresMapValidation, intentRules.RequiresLineOfSight, intentRules.RequiresElevationRules);
        var targeting = AbilityTargeting.Validate(use.Character, use.Character.TacticalPosition, metadata,
            new(AbilityTargetType.Character, request.Defender.TacticalPosition, request.Defender), constraints,
            new(AbilityRangeKind.ManhattanDistance, bow.MinimumRange, bow.MaximumRange), use.AttackIntent.MapTargetLegal?.Invoke());
        if (!targeting.IsLegal) return new(use, request, bowId, bow, approval.Eligibility, FrostbiteArrowReason.InvalidTarget, targeting);
        if (request.OpeningAttackType != AttackType.Physical || request.AttackerWeapon != WeaponCategory.Bow ||
            request.OpeningPower != bow.Power || request.OpeningBaseAccuracy != bow.BaseAccuracy || request.Distance != targeting.Distance)
            return Rejected(FrostbiteArrowReason.ContextMismatch);
        if (!rules.DurationPolicy.HasValue) return Rejected(FrostbiteArrowReason.DurationUnresolved);
        if (!rules.TargetIsFlying.HasValue) return Rejected(FrostbiteArrowReason.FlyingContextRequired);
        if (rules.TargetIsFlying.Value) return Rejected(FrostbiteArrowReason.FlyingEffectivenessUnresolved);
        if (!rules.TargetHasTerrainFrostbite.HasValue) return Rejected(FrostbiteArrowReason.TerrainContextRequired);
        if (rules.TargetHasTerrainFrostbite.Value) return Rejected(FrostbiteArrowReason.TerrainInteractionUnresolved);
        if (request.Defender.ShieldHP > 0) return Rejected(FrostbiteArrowReason.ShieldEligibilityUnresolved);
        var prepared = WithNormalRules(use, request);
        var defense = prepared.Sanctuary!.Evaluate(use.Character, request.Defender);
        if (!defense.IsResolved) return new(use, request, bowId, bow, approval.Eligibility, FrostbiteArrowReason.SanctuaryUnresolved, targeting, defense);
        CombatExchangeResolver.ValidateRequest(prepared);
        var counterAttack = EffectiveCounter(prepared);
        bool counter = CounterattackEligibility.CanCounter(request.Defender, request.Distance,
            counterAttack.MinimumRange, counterAttack.MaximumRange, true, request.CounterattacksSuppressed);
        var followUp = FollowUpEligibility.Evaluate(use.Character.EffectiveSpeed, request.Defender.EffectiveSpeed);
        if (counter || followUp == FollowUpParticipant.Attacker)
            return new(use, request, bowId, bow, approval.Eligibility, FrostbiteArrowReason.ExchangeTimingUnresolved, targeting, defense);
        var damage = SingleStrikeResolver.PreparePhysicalStrike(use.Character.BaseStats, request.Defender.BaseStats, bow.Power,
            WeaponCategory.Bow, counterAttack.Category, bow.BaseAccuracy, defense.EffectiveDefense, request.OpeningEvasionModifier,
            WinterHunter.GetPhysicalDamageBonus(use.Character, request.Defender));
        if (damage.CandidateDamage >= request.Defender.CurrentHealth)
            return new(use, request, bowId, bow, approval.Eligibility, FrostbiteArrowReason.LethalProcRulesUnresolved, targeting, defense);
        return new(use, request, bowId, bow, approval.Eligibility, targeting: targeting, defense: defense);
    }

    /// <remarks>
    /// Rechecks current input and prevalidates bookkeeping before the first roll. A broken custom
    /// random provider can throw after damage; the started use cannot be replayed and no rollback is promised.
    /// </remarks>
    public FrostbiteArrowUseResult Execute(AbilityUse use, CombatExchangeRequest request, string bowId,
        AttackConfiguration? bow, FrostbiteArrowContext rules)
    {
        var preview = Preview(use, request, bowId, bow, rules);
        if (!preview.CanExecute) return preview;
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, request.Defender, isAttack: true, preparingExecution: true);
        if (!approval.Succeeded) return new(use, request, bowId, bow, approval.Eligibility, targeting: preview.Targeting);
        use.MarkFrostbiteArrowStarted();
        var combat = _combat.Resolve(WithNormalRules(use, request));
        var proc = _effect.Resolve(use.Character, request.Defender, combat.OpeningStrike, combat.OpeningShieldAbsorbed, use.RoundNumber, rules);
        if (proc.ProcSucceeded == true) proc = _effect.Apply(proc, use.RoundNumber);
        // All unsupported application cases were excluded before combat. Do not report a blocked
        // status application as a completed ability if an external dependency violated the synchronous contract.
        if (proc.ProcSucceeded == true && !proc.StatusApplied)
            return new(use, request, bowId, bow, preview.Eligibility, proc.Reason, preview.Targeting, preview.Defense, combat, proc);
        var finalization = AbilityUseFinalizer.FinalizeResolvedMainAction(use, approval);
        return new(use, request, bowId, bow, preview.Eligibility, targeting: preview.Targeting, defense: preview.Defense,
            combat: combat, proc: proc, finalization: finalization);
    }

    private static CombatExchangeRequest WithNormalRules(AbilityUse use, CombatExchangeRequest request)
        => request with { IncomingAttackPermitsCounters = true, IncomingAttackPermitsFollowUps = true,
            Sanctuary = SanctuaryDefenseContext.FromActivations(use.Activations) };

    private static AttackConfiguration EffectiveCounter(CombatExchangeRequest request)
        => DisarmEligibility.GetEffectiveAttack(request.Defender, new(request.CounterattackType, request.DefenderWeapon,
            request.CounterattackPower, request.CounterattackBaseAccuracy, request.CounterattackMinimumRange, request.CounterattackMaximumRange),
            request.DefenderUnarmedAttack);
}
