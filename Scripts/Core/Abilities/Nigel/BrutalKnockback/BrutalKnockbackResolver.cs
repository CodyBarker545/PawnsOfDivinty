using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Gameplay;
using PawnsOfDivinity.Core.Statuses;

namespace PawnsOfDivinity.Core.Abilities;

public enum BrutalKnockbackRejectionReason
{
    None, NotNigel, AttackerDefeated, TargetDefeated, TargetNotEnemy, MissingPosition, NotAdjacent,
    AttackConfigurationRequired, PhysicalAxeRequired, Disarmed, OutsideEquippedRange,
    PushDistanceSelectionRequired, InvalidPushDistance, DisplacementNotResolved,
    CollisionResolutionUnresolved, DamageDisplacementAndReactionTimingUnresolved, FuryReductionOrderingUnresolved,
    ShortenedPushRuleUnresolved, PostHitDisplacementUnresolved, AlreadyStarted
}

/// <summary>The finalized opening strike calculation, with no damage or movement applied.</summary>
public sealed record BrutalKnockbackOpeningResult
{
    public string AttackerId { get; }
    public string TargetId { get; }
    public BrutalKnockbackRejectionReason RejectionReason { get; }
    public int? NormalAxeDamage { get; }
    public int FuryBonus { get; }
    public int? ProvisionalDamage { get; }
    /// <summary>Finalized reduced damage; ProvisionalDamage remains as a compatibility property for older callers.</summary>
    public int? FinalOpeningDamage => ProvisionalDamage;
    public int? HitChance { get; }
    public WeaponTriangleOutcome? TriangleOutcome { get; }
    public StrikeResult? Strike { get; }
    public bool IsPrepared => RejectionReason == BrutalKnockbackRejectionReason.None;

    internal BrutalKnockbackOpeningResult(Character attacker, Character target, BrutalKnockbackRejectionReason reason,
        int? normalDamage = null, int? damage = null, int? hitChance = null, WeaponTriangleOutcome? triangle = null,
        StrikeResult? strike = null, int furyBonus = 0)
    {
        AttackerId = attacker.Id;
        TargetId = target.Id;
        RejectionReason = reason;
        NormalAxeDamage = normalDamage;
        FuryBonus = furyBonus;
        ProvisionalDamage = damage;
        HitChance = hitChance;
        TriangleOutcome = triangle;
        Strike = strike;
    }
}

public sealed record BrutalKnockbackEffectResult
{
    public BrutalKnockbackOpeningResult Opening { get; }
    public ForcedMovementValidationResult? Displacement { get; }
    public BrutalKnockbackRejectionReason RejectionReason { get; }
    public IReadOnlyList<ForcedMovementValidationResult> Steps { get; }
    public bool LethalOpeningIfHit { get; }
    public bool FollowUpsUnresolved => true;
    public bool CanExecute => RejectionReason == BrutalKnockbackRejectionReason.None;

    internal BrutalKnockbackEffectResult(BrutalKnockbackOpeningResult opening,
        BrutalKnockbackRejectionReason reason, ForcedMovementValidationResult? displacement = null,
        IEnumerable<ForcedMovementValidationResult>? steps = null, bool lethalOpeningIfHit = false)
    {
        Opening = opening;
        RejectionReason = reason;
        Displacement = displacement;
        Steps = Array.AsReadOnly(steps?.ToArray() ?? Array.Empty<ForcedMovementValidationResult>());
        LethalOpeningIfHit = lethalOpeningIfHit;
    }
}

/// <summary>The opening is reported even if a later map change prevents completing the push.</summary>
public sealed record BrutalKnockbackResolution
{
    public BrutalKnockbackEffectResult Preparation { get; }
    public CombatExchangeResult? Combat { get; }
    public ForcedMovementResult? Movement { get; }
    public CollisionDamageResult? Collision { get; }
    public GridPosition? OriginalAttackerPosition { get; }
    public GridPosition? OriginalTargetPosition { get; }
    public GridPosition? FinalTargetPosition { get; }
    public int TilesPushed { get; }
    public bool TargetSurvivedOpening { get; }
    public BrutalKnockbackRejectionReason RejectionReason { get; }
    public bool Completed => Combat is not null && RejectionReason == BrutalKnockbackRejectionReason.None;
    public bool PostHitUnresolved => Combat is not null && !Completed;
    public IReadOnlyList<string> DefeatedCharacterIds { get; }

    internal BrutalKnockbackResolution(BrutalKnockbackEffectResult preparation, Character target,
        GridPosition? source, GridPosition? origin, BrutalKnockbackRejectionReason reason,
        CombatExchangeResult? combat = null, ForcedMovementResult? movement = null)
    {
        Preparation = preparation;
        Combat = combat;
        Movement = movement;
        OriginalAttackerPosition = source;
        OriginalTargetPosition = origin;
        FinalTargetPosition = target.TacticalPosition;
        TilesPushed = origin.HasValue && FinalTargetPosition.HasValue
            ? checked((int)origin.Value.ManhattanDistanceTo(FinalTargetPosition.Value)) : 0;
        TargetSurvivedOpening = combat is not null && !combat.DefenderDefeated;
        RejectionReason = reason;
        DefeatedCharacterIds = Array.AsReadOnly(target.IsDefeated ? new[] { target.Id } : Array.Empty<string>());
    }
}

/// <summary>Resolves the finalized opening and supported push while preserving unresolved map cases.</summary>
public sealed class BrutalKnockbackResolver
{
    private readonly SingleStrikeResolver _strikes;
    private readonly ForcedMovementService _movement;
    private readonly CombatExchangeResolver _combat;

    public BrutalKnockbackResolver(SingleStrikeResolver strikes, ForcedMovementService movement)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        ArgumentNullException.ThrowIfNull(movement);
        _strikes = strikes;
        _movement = movement;
        _combat = new(strikes);
    }

    public static BrutalKnockbackOpeningResult PrepareOpening(Character attacker, Character target,
        AttackConfiguration? axe, WeaponCategory defenderWeapon, int evasionModifier = 0)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        if (attacker.Definition?.Id != "nigel") return Rejected(BrutalKnockbackRejectionReason.NotNigel);
        if (attacker.IsDefeated) return Rejected(BrutalKnockbackRejectionReason.AttackerDefeated);
        if (target.IsDefeated) return Rejected(BrutalKnockbackRejectionReason.TargetDefeated);
        if (attacker.Id == target.Id || attacker.TeamId == target.TeamId) return Rejected(BrutalKnockbackRejectionReason.TargetNotEnemy);
        if (!attacker.TacticalPosition.HasValue || !target.TacticalPosition.HasValue)
            return Rejected(BrutalKnockbackRejectionReason.MissingPosition);
        if (attacker.TacticalPosition.Value.ManhattanDistanceTo(target.TacticalPosition.Value) != 1)
            return Rejected(BrutalKnockbackRejectionReason.NotAdjacent);
        if (axe is null) return Rejected(BrutalKnockbackRejectionReason.AttackConfigurationRequired);
        if (axe.Type != AttackType.Physical || axe.Category != WeaponCategory.Axe)
            return Rejected(BrutalKnockbackRejectionReason.PhysicalAxeRequired);
        if (attacker.Statuses.HasStatus(StatusType.Disarm)) return Rejected(BrutalKnockbackRejectionReason.Disarmed);
        if (axe.MinimumRange > 1 || axe.MaximumRange < 1) return Rejected(BrutalKnockbackRejectionReason.OutsideEquippedRange);
        _ = WeaponTriangle.Evaluate(axe.Category, defenderWeapon); // Validate supplied categories even when Disarm neutralizes one.
        int fury = BerserkersFury.GetPhysicalDamageBonus(attacker);
        var effectiveDefenderWeapon = target.Statuses.HasStatus(StatusType.Disarm) ? WeaponCategory.Unarmed : defenderWeapon;
        var normal = SingleStrikeResolver.PreparePhysicalStrike(attacker.BaseStats, target.BaseStats,
            axe.Power, axe.Category, effectiveDefenderWeapon, axe.BaseAccuracy, target.EffectiveDefense, evasionModifier);
        // Finalized in section 12.4: Fury is added exactly once, before halving.
        // Widen the addition so a valid reduced result is not lost to intermediate int overflow.
        int reduced = checked((int)Math.Max(1L, ((long)normal.CandidateDamage + fury) / 2));
        return new(attacker, target, BrutalKnockbackRejectionReason.None, normal.CandidateDamage, reduced,
            normal.HitChance, normal.Outcome, furyBonus: fury);

        BrutalKnockbackOpeningResult Rejected(BrutalKnockbackRejectionReason reason) => new(attacker, target, reason);
    }

    /// <summary>Low-level strike calculation only, not permission to execute Brutal Knockback.</summary>
    /// <remarks>
    /// Uses exactly one ordinary hit roll and returns zero damage on a miss. No HP, shields,
    /// positions, counters, follow-ups, actions or cooldowns are changed here. The full
    /// ability uses Resolve to apply damage before its validated displacement.
    /// </remarks>
    public BrutalKnockbackOpeningResult ResolveOpeningStrike(Character attacker, Character target,
        AttackConfiguration? axe, WeaponCategory defenderWeapon, int evasionModifier = 0)
    {
        var prepared = PrepareOpening(attacker, target, axe, defenderWeapon, evasionModifier);
        if (!prepared.IsPrepared) return prepared;
        var strike = _strikes.ResolvePreparedStrike((prepared.HitChance!.Value, prepared.ProvisionalDamage!.Value,
            prepared.TriangleOutcome!.Value));
        return new(attacker, target, prepared.RejectionReason, prepared.NormalAxeDamage, prepared.ProvisionalDamage,
            prepared.HitChance, prepared.TriangleOutcome, strike, prepared.FuryBonus);
    }

    public BrutalKnockbackEffectResult Preview(Character attacker, Character target, AttackConfiguration? axe,
        WeaponCategory defenderWeapon, int? pushDistance, IForcedMovementMap? map, int evasionModifier = 0)
    {
        var opening = PrepareOpening(attacker, target, axe, defenderWeapon, evasionModifier);
        if (!opening.IsPrepared) return new(opening, opening.RejectionReason);
        return PlanMovement(attacker, target, opening, pushDistance, map, allowLethalBypass: true);
    }

    private BrutalKnockbackEffectResult PlanMovement(Character attacker, Character target,
        BrutalKnockbackOpeningResult opening, int? pushDistance, IForcedMovementMap? map, bool allowLethalBypass)
    {
        pushDistance ??= 2;
        if (pushDistance < 1 || pushDistance > 2) return new(opening, BrutalKnockbackRejectionReason.InvalidPushDistance);
        if (pushDistance != 2) return new(opening, BrutalKnockbackRejectionReason.ShortenedPushRuleUnresolved);
        // Neither a miss nor a lethal opening can enter the movement branch.
        if (allowLethalBypass && opening.ProvisionalDamage!.Value >= (long)target.CurrentHealth + target.ShieldHP)
            return new(opening, BrutalKnockbackRejectionReason.None, lethalOpeningIfHit: true);
        var source = attacker.TacticalPosition!.Value;
        var origin = target.TacticalPosition!.Value;
        var direction = new GridPosition((int)((long)origin.X - source.X), (int)((long)origin.Y - source.Y));
        var steps = new List<ForcedMovementValidationResult>();
        for (int step = 1; step <= 2; step++)
        {
            var movement = _movement.Validate(attacker, target, direction, step, map);
            steps.Add(movement);
            if (movement.IsLegal) continue;
            bool collision = movement.RejectionReason is ForcedMovementRejectionReason.InvalidDestination
                or ForcedMovementRejectionReason.DestinationOccupied or ForcedMovementRejectionReason.IllegalTransition;
            // Damage is finalized, but a wall/unit/map-edge collision still needs a legal
            // stopping position. Do not choose a shortened push or infer a second participant.
            return new(opening, collision ? BrutalKnockbackRejectionReason.CollisionResolutionUnresolved
                : BrutalKnockbackRejectionReason.DisplacementNotResolved, movement, steps);
        }
        return new(opening, BrutalKnockbackRejectionReason.None, steps[^1], steps);
    }

    /// <summary>Runs only the finalized opening and supported flat two-tile displacement.</summary>
    /// <remarks>
    /// Every intervening tile is checked before the roll and again after shield-first damage.
    /// The existing forced-position operation commits that validated straight displacement;
    /// no teleport permission or speculative terrain-entry effects are used. Map queries and
    /// random providers must not mutate match state. There is no rollback after combat begins.
    /// </remarks>
    public BrutalKnockbackResolution Resolve(Character attacker, Character target, AttackConfiguration? axe,
        WeaponCategory defenderWeapon, int? pushDistance, IForcedMovementMap? map, int evasionModifier = 0)
    {
        var preparation = Preview(attacker, target, axe, defenderWeapon, pushDistance, map, evasionModifier);
        var source = attacker.TacticalPosition;
        var origin = target.TacticalPosition;
        if (!preparation.CanExecute) return new(preparation, target, source, origin, preparation.RejectionReason);
        var opening = preparation.Opening;
        // Counterattack fields are unavailable and unused. Existing permission flags prohibit
        // preparing or rolling a counter; Speed follow-ups remain explicitly unresolved.
        var request = new CombatExchangeRequest(attacker, target, AttackType.Physical, axe!.Power,
            WeaponCategory.Axe, defenderWeapon, axe.BaseAccuracy, AttackType.Physical, 0, 0, 0, 0, 1,
            IncomingAttackPermitsCounters: false, CounterattacksSuppressed: true, IncomingAttackPermitsFollowUps: false);
        var combat = _combat.ResolveSuppressedOpening(request,
            (opening.HitChance!.Value, opening.ProvisionalDamage!.Value, opening.TriangleOutcome!.Value), opening.NormalAxeDamage!.Value);
        if (!combat.OpeningStrike.Hit || target.IsDefeated)
            return new(preparation, target, source, origin, BrutalKnockbackRejectionReason.None, combat);
        if (source != attacker.TacticalPosition || origin != target.TacticalPosition)
            return new(preparation, target, source, origin, BrutalKnockbackRejectionReason.PostHitDisplacementUnresolved, combat);
        var current = PlanMovement(attacker, target, opening, pushDistance, map, allowLethalBypass: false);
        if (!current.CanExecute)
            return new(current, target, source, origin, current.RejectionReason, combat);
        var movement = _movement.Execute(attacker, target, current.Displacement!, map);
        return new(current, target, source, origin, movement.Displaced ? BrutalKnockbackRejectionReason.None
            : BrutalKnockbackRejectionReason.PostHitDisplacementUnresolved, combat, movement);
    }
}
