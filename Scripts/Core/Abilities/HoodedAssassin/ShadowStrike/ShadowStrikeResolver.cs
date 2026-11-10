using System;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Terrain;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Shadow Strike's opening bonus, reveal and Disarm, using the normal combat exchange.</summary>
/// <remarks>
/// Spec v0.2 section 12.1, plus the approved pre-approach visibility ruling:
/// capture visibility before approaching, complete movement separately, then resolve at range 1.
/// The snapshot must include all living nearby enemies. This service can check the target's
/// adjacency and current reveal state, but cannot reconstruct movement history or a past roster.
/// Action costs and successful-use bookkeeping belong to the later ability executor.
/// </remarks>
public sealed class ShadowStrikeResolver
{
    private readonly CombatExchangeResolver _combat;
    private readonly SmokePlacementService _smoke;

    public ShadowStrikeResolver(CombatExchangeResolver combat, SmokePlacementService smoke)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(smoke);
        _combat = combat;
        _smoke = smoke;
    }

    /// <param name="weaponMinimumRange">The caller's resolved effective weapon range, not an equipment default.</param>
    /// <param name="weaponMaximumRange">The caller's resolved effective weapon range, inclusive.</param>
    /// <param name="openingVisibility">Explicit visibility at the start of the attack, before any approach.</param>
    public ShadowStrikeEligibilityResult Evaluate(CombatExchangeRequest request,
        SmokeVisibilityResult? openingVisibility, int weaponMinimumRange, int weaponMaximumRange)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Attacker);
        ArgumentNullException.ThrowIfNull(request.Defender);
        ArgumentOutOfRangeException.ThrowIfNegative(weaponMinimumRange);
        if (weaponMaximumRange < weaponMinimumRange)
            throw new ArgumentOutOfRangeException(nameof(weaponMaximumRange), "Maximum range must be at least minimum range.");
        var attacker = request.Attacker;
        var target = request.Defender;
        ShadowStrikeEligibilityResult Decision(ShadowStrikeRejectionReason reason) =>
            new(reason, attacker.TacticalPosition, target.TacticalPosition, openingVisibility);

        if (attacker.IsDefeated) return Decision(ShadowStrikeRejectionReason.AttackerDefeated);
        if (!SmokeVisibility.IsEligibleAssassin(attacker)) return Decision(ShadowStrikeRejectionReason.InvalidAttacker);
        if (target.IsDefeated) return Decision(ShadowStrikeRejectionReason.TargetDefeated);
        if (target.TeamId == attacker.TeamId || target.Id == attacker.Id)
            return Decision(ShadowStrikeRejectionReason.TargetNotEnemy);
        if (attacker.TacticalPosition is not { } source || target.TacticalPosition is not { } destination)
            return Decision(ShadowStrikeRejectionReason.MissingPosition);
        if (source.ManhattanDistanceTo(destination) != 1 || request.Distance != 1
            || weaponMinimumRange > 1 || weaponMaximumRange < 1)
            return Decision(ShadowStrikeRejectionReason.OutsideRange);
        if (request.OpeningAttackType != AttackType.Physical)
            return Decision(ShadowStrikeRejectionReason.RequiresPhysicalAttack);
        if (openingVisibility is null) return Decision(ShadowStrikeRejectionReason.MissingVisibilitySnapshot);
        if (openingVisibility.CharacterId != attacker.Id || openingVisibility.TeamId != attacker.TeamId
            || openingVisibility.RevealedByAttack != attacker.SmokeReveal.RevealedByAttack)
            return Decision(ShadowStrikeRejectionReason.InvalidVisibilitySnapshot);
        if (openingVisibility.IsInvisible && (!openingVisibility.IsInSmoke || openingVisibility.EnemyAdjacent
            || openingVisibility.RevealedByAttack || !_smoke.IsCovered(openingVisibility.Position)
            || openingVisibility.Position.ManhattanDistanceTo(destination) <= 1))
            return Decision(ShadowStrikeRejectionReason.InvalidVisibilitySnapshot);
        return Decision(ShadowStrikeRejectionReason.None);
    }

    /// <summary>Rechecks eligibility, then resolves the exchange without moving anyone or spending actions.</summary>
    /// <remarks>
    /// The flat bonus is added after ordinary physical damage for this focused composition;
    /// it does not establish a universal modifier order. An invisible opening requires an
    /// explicit unarmed fallback for the target, so Disarm never invents Power or accuracy.
    /// Disarm applies on a surviving successful opening hit; no extra status roll is invented.
    /// </remarks>
    public ShadowStrikeResult Resolve(CombatExchangeRequest request, SmokeVisibilityResult? openingVisibility,
        int weaponMinimumRange, int weaponMaximumRange)
    {
        var eligibility = Evaluate(request, openingVisibility, weaponMinimumRange, weaponMaximumRange);
        if (!eligibility.IsAllowed) return new(eligibility, null);

        var effectiveRequest = request with
        {
            IncomingAttackPermitsCounters = true,
            IncomingAttackPermitsFollowUps = true,
            OpeningEvasionModifier = SmokeVeil.GetEvasionModifier(request.Defender, eligibility.TargetPosition!.Value, _smoke),
            CounterattackEvasionModifier = SmokeVeil.GetEvasionModifier(request.Attacker, eligibility.SourcePosition!.Value, _smoke)
        };
        var exchange = _combat.ResolveOpening(effectiveRequest, eligibility.InvisibleOpening ? 5 : 3,
            eligibility.InvisibleOpening, revealAttacker: true);
        return new(eligibility, exchange);
    }
}
