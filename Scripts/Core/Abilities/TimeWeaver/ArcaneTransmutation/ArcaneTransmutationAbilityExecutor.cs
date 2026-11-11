using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Equipment;
using PawnsOfDivinity.Core.Statuses;

namespace PawnsOfDivinity.Core.Abilities;

public enum ArcaneTransmutationRejectionReason
{
    None, ReplacementUnavailable, CombatConfigurationRequired, AccuracyRequired, TargetRejected,
    SelectedRangeOutcomeUnresolved, SelectedElementUnresolved, EquippedScrollChanged, WeaponUseBlocked
}

/// <summary>A snapshot of validation, equipment replacement, combat and successful-use bookkeeping.</summary>
public sealed record ArcaneTransmutationUseResult
{
    public string CasterId { get; }
    public int RoundNumber { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public AbilityTargetingResult? Targeting { get; }
    public ArcaneTransmutationEffectResult? Effect { get; }
    public string? TargetId { get; }
    public ArcaneScrollReplacementResult? Replacement { get; }
    public string? OldScrollId => Replacement?.OriginalScrollId;
    public string? ReplacementScrollId => Replacement?.ReplacementScrollId;
    public ScrollDefinition? SelectedScroll => Replacement?.SelectedScroll;
    public int CandidateCount => Replacement?.EligibleCandidateCount ?? 0;
    public int? SelectedCandidateIndex => Replacement?.SelectedIndex;
    public CombatExchangeResult? Combat { get; }
    public TemporalAccumulationResult? Accumulation { get; }
    public AbilityUseResult? Finalization { get; }
    public ArcaneTransmutationRejectionReason RejectionReason { get; }
    public bool Succeeded => Finalization?.Succeeded == true && Combat is not null && Replacement?.Succeeded == true;
    public bool CanSelect => !Succeeded && Eligibility.IsAllowed && RejectionReason == ArcaneTransmutationRejectionReason.None
        && Replacement?.CanReplace == true && Targeting?.IsLegal == true;

    internal ArcaneTransmutationUseResult(AbilityUse use, AbilityEligibilityResult eligibility,
        AbilityTargetingResult? targeting = null, ArcaneTransmutationEffectResult? effect = null,
        string? targetId = null, ArcaneScrollReplacementResult? replacement = null,
        ArcaneTransmutationRejectionReason reason = ArcaneTransmutationRejectionReason.None,
        CombatExchangeResult? combat = null, TemporalAccumulationResult? accumulation = null, AbilityUseResult? finalization = null)
    {
        CasterId = use.CharacterId;
        RoundNumber = use.RoundNumber;
        Eligibility = eligibility;
        Targeting = targeting;
        Effect = effect;
        TargetId = targetId;
        Replacement = replacement;
        RejectionReason = reason;
        Combat = combat;
        Accumulation = accumulation;
        Finalization = finalization;
    }
}

/// <summary>Coordinates replacement, its immediate exchange, passive progress and one Main Action.</summary>
/// <remarks>
/// Match operations are synchronous and providers must not mutate match state. Predictable
/// failures are checked before equipment or health changes. A provider failure after combat
/// begins can leave earlier effects applied; this service does not provide rollback.
/// </remarks>
public sealed class ArcaneTransmutationAbilityExecutor
{
    private readonly CombatExchangeResolver? _combat;
    private readonly IRandomIndexProvider? _selection;

    // Kept for existing metadata-only callers. Complete execution requires explicit providers.
    public ArcaneTransmutationAbilityExecutor() { }

    public ArcaneTransmutationAbilityExecutor(CombatExchangeResolver combat, IRandomIndexProvider selection)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(selection);
        _combat = combat;
        _selection = selection;
    }

    /// <summary>Checks everything independent of random selection. No candidate is equipped or rolled.</summary>
    /// <remarks>
    /// The supplied request provides the defender's available counterattack and resolved evasion.
    /// Its opening Power, category, accuracy and distance are replaced by authoritative scroll and
    /// position data. Target permissions remain explicit caller input rather than invented defaults.
    /// </remarks>
    public ArcaneTransmutationUseResult Preview(AbilityUse use, CombatExchangeRequest combat,
        AbilityTargetingConstraints targetingRules, IEnumerable<ScrollDefinition>? catalog,
        int? baseAccuracy, string eventId, bool? mapTargetLegal = null,
        ArcaneScrollReplacementResult? expectedEquipment = null)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(combat.Defender);
        ArgumentNullException.ThrowIfNull(targetingRules);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (!ReferenceEquals(use.Character, combat.Attacker))
            throw new ArgumentException("The combat attacker must be the ability user.", nameof(combat));
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, combat.Defender, isAttack: true);
        if (!approval.Succeeded) return Result(approval.Eligibility);
        if (use.AbilityId != TimeWeaver.ArcaneTransmutationId)
            return Result(new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        if (!use.Activations.FirstTeam.Concat(use.Activations.SecondTeam).Any(unit => ReferenceEquals(unit, combat.Defender)))
            return Result(approval.Eligibility, reason: ArcaneTransmutationRejectionReason.TargetRejected);
        if (expectedEquipment is not null && (!ReferenceEquals(expectedEquipment.Caster, use.Character)
            || !ReferenceEquals(expectedEquipment.OriginalScroll, use.Character.EquippedScroll)))
            return Result(approval.Eligibility, reason: ArcaneTransmutationRejectionReason.EquippedScrollChanged);
        var replacement = ArcaneTransmutationEffect.PreviewReplacement(use.Character, catalog);
        if (!replacement.CanReplace)
            return Result(approval.Eligibility, replacement, ArcaneTransmutationRejectionReason.ReplacementUnavailable);
        if (_combat is null || _selection is null)
            return Result(approval.Eligibility, replacement, ArcaneTransmutationRejectionReason.CombatConfigurationRequired);
        if (!baseAccuracy.HasValue)
            return Result(approval.Eligibility, replacement, ArcaneTransmutationRejectionReason.AccuracyRequired);
        if (baseAccuracy < 0 || baseAccuracy > 100) throw new ArgumentOutOfRangeException(nameof(baseAccuracy));
        if (use.Character.Statuses.HasStatus(StatusType.Disarm))
            return Result(approval.Eligibility, replacement, ArcaneTransmutationRejectionReason.WeaponUseBlocked);
        // Range is deliberately deferred until selection. Check current positions, life state,
        // team permissions and map requirements now using an unrestricted distance interval.
        var targeting = ValidateTarget(use, combat, targetingRules, new(AbilityRangeKind.ManhattanDistance, 0, int.MaxValue), mapTargetLegal);
        if (!targeting.IsLegal)
            return new(use, approval.Eligibility, targeting, targetId: combat.Defender.Id, replacement: replacement,
                reason: ArcaneTransmutationRejectionReason.TargetRejected);
        return new(use, approval.Eligibility, targeting, targetId: combat.Defender.Id, replacement: replacement);

        ArcaneTransmutationUseResult Result(AbilityEligibilityResult eligibility, ArcaneScrollReplacementResult? pool = null,
            ArcaneTransmutationRejectionReason reason = ArcaneTransmutationRejectionReason.None)
            => new(use, eligibility, targetId: combat.Defender.Id, replacement: pool, reason: reason);
    }

    public ArcaneTransmutationUseResult Execute(AbilityUse use, CombatExchangeRequest combat,
        AbilityTargetingConstraints targetingRules, IEnumerable<ScrollDefinition>? catalog,
        int? baseAccuracy, string eventId, bool? mapTargetLegal = null,
        ArcaneScrollReplacementResult? expectedEquipment = null)
    {
        var preview = Preview(use, combat, targetingRules, catalog, baseAccuracy, eventId, mapTargetLegal, expectedEquipment);
        if (!preview.CanSelect) return preview;
        var selection = ArcaneTransmutationEffect.SelectReplacement(preview.Replacement!, _selection!);
        if (!selection.CanReplace) return Result(ArcaneTransmutationRejectionReason.ReplacementUnavailable);
        var scroll = selection.SelectedScroll!;
        var targeting = ValidateTarget(use, combat, targetingRules,
            new(AbilityRangeKind.ManhattanDistance, scroll.MinimumRange, scroll.MaximumRange), mapTargetLegal);
        if (!targeting.IsLegal)
        {
            // The specification does not say whether an out-of-range random result should
            // fail, reroll or still equip. Leave it uncommitted until that ruling is supplied.
            bool rangeFailure = targeting.RejectionReason is AbilityTargetingRejectionReason.InsideMinimumRange
                or AbilityTargetingRejectionReason.OutsideMaximumRange;
            return new(use, preview.Eligibility, targeting, targetId: combat.Defender.Id, replacement: selection,
                reason: rangeFailure ? ArcaneTransmutationRejectionReason.SelectedRangeOutcomeUnresolved
                    : ArcaneTransmutationRejectionReason.TargetRejected);
        }
        if (scroll.ElementId is null) return Result(ArcaneTransmutationRejectionReason.SelectedElementUnresolved);
        var attack = scroll.ToAttackConfiguration(baseAccuracy!.Value);
        var resolved = combat with
        {
            OpeningAttackType = attack.Type, OpeningPower = attack.Power, AttackerWeapon = attack.Category,
            OpeningBaseAccuracy = attack.BaseAccuracy, Distance = checked((int)targeting.Distance!.Value),
            IncomingAttackPermitsCounters = true, IncomingAttackPermitsFollowUps = true,
            Sanctuary = SanctuaryDefenseContext.FromActivations(use.Activations)
        };
        CombatExchangeResolver.ValidateRequest(resolved);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, combat.Defender, isAttack: true, preparingExecution: true);
        if (!approval.Succeeded)
            return new(use, approval.Eligibility, targeting, targetId: combat.Defender.Id, replacement: selection);
        if (!ReferenceEquals(use.Character.EquippedScroll, selection.OriginalScroll))
            return Result(ArcaneTransmutationRejectionReason.EquippedScrollChanged);
        var applied = use.Character.ReplaceScroll(scroll);
        if (!applied.WasApplied) return Result(ArcaneTransmutationRejectionReason.ReplacementUnavailable);
        var replacement = new ArcaneScrollReplacementResult(use.Character, selection.OriginalScroll, selection.Candidates,
            replacement: scroll, selectedIndex: selection.SelectedIndex, validation: applied);
        var exchange = _combat!.Resolve(resolved);
        var finalization = AbilityUseFinalizer.FinalizeResolvedMainAction(use, approval);
        // Award progress after the entire exchange, so every strike uses the old stack bonus.
        var accumulation = finalization.Succeeded
            ? TemporalAccumulation.Process(use.Character, eventId, scroll.ElementId, exchange.OpeningStrike) : null;
        return new(use, preview.Eligibility, targeting, targetId: combat.Defender.Id, replacement: replacement,
            combat: exchange, accumulation: accumulation, finalization: finalization);

        ArcaneTransmutationUseResult Result(ArcaneTransmutationRejectionReason reason)
            => new(use, preview.Eligibility, preview.Targeting, targetId: combat.Defender.Id, replacement: selection, reason: reason);
    }

    private static AbilityTargetingResult ValidateTarget(AbilityUse use, CombatExchangeRequest combat,
        AbilityTargetingConstraints rules, AbilityRange range, bool? mapTargetLegal)
    {
        if (ReferenceEquals(combat.Attacker, combat.Defender) || combat.Attacker.Id == combat.Defender.Id)
            return new(AbilityTargetingRejectionReason.InvalidSelfTarget, use.Character.TacticalPosition,
                combat.Defender.TacticalPosition, null);
        var constraints = new AbilityTargetingConstraints(AbilityTargetType.Character, rules.AllowSelf, rules.Team,
            true, rules.RequiresMapValidation, rules.RequiresLineOfSight, rules.RequiresElevationRules);
        if (rules.TargetType != AbilityTargetType.Character)
            return new(rules.TargetType.HasValue ? AbilityTargetingRejectionReason.InvalidTargetType
                : AbilityTargetingRejectionReason.UnresolvedTargetingRule, use.Character.TacticalPosition,
                combat.Defender.TacticalPosition, null);
        return AbilityTargeting.Validate(use.Character, use.Character.TacticalPosition,
            AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!,
            new(AbilityTargetType.Character, combat.Defender.TacticalPosition, combat.Defender), constraints, range, mapTargetLegal);
    }

    /// <remarks>
    /// Preview cannot choose a replacement or borrow the old scroll's range: the immediate
    /// attack must use the randomly transformed scroll's properties. AbilityTargeting reports
    /// the missing resolved range. Self/team/map permissions remain explicit caller input.
    /// </remarks>
    public ArcaneTransmutationUseResult Preview(AbilityUse use, AbilityTarget attackTarget,
        AbilityTargetingConstraints targetingRules, string? currentScrollId = null, int? currentLevel = null,
        IEnumerable<(string Id, int Level)>? availableScrolls = null, bool? mapTargetLegal = null)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(attackTarget);
        ArgumentNullException.ThrowIfNull(targetingRules);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(use, approval.Eligibility);
        if (use.AbilityId != TimeWeaver.ArcaneTransmutationId)
            return new(use, new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        var effect = ArcaneTransmutationEffect.Evaluate(use.Character, currentScrollId, currentLevel, availableScrolls);
        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        var targeting = AbilityTargeting.Validate(use.Character, use.Character.TacticalPosition, ability,
            attackTarget, targetingRules, mapTargetLegal: mapTargetLegal);
        return new(use, approval.Eligibility, targeting, effect);
    }

    /// <summary>Compatibility path for old metadata-only callers; it never commits effects.</summary>
    /// <remarks>
    /// Use the typed combat/catalog overload for complete execution. Bare ID/level tuples
    /// do not contain the Power, range, element or counterattack context needed for a cast.
    /// </remarks>
    public ArcaneTransmutationUseResult Execute(AbilityUse use, AbilityTarget attackTarget,
        AbilityTargetingConstraints targetingRules, string? currentScrollId = null, int? currentLevel = null,
        IEnumerable<(string Id, int Level)>? availableScrolls = null, bool? mapTargetLegal = null)
        => Preview(use, attackTarget, targetingRules, currentScrollId, currentLevel, availableScrolls, mapTargetLegal);
}
