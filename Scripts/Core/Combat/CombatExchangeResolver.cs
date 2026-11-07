using System;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Runs an opening strike, an eligible counterattack, then at most one permitted follow-up.</summary>
public sealed class CombatExchangeResolver
{
    private readonly SingleStrikeResolver _strikeResolver;

    public CombatExchangeResolver(SingleStrikeResolver strikeResolver)
    {
        ArgumentNullException.ThrowIfNull(strikeResolver);
        _strikeResolver = strikeResolver;
    }

    /// <summary>Checks the request and resolves the exchange, leaving activation flags alone.</summary>
    /// <remarks>
    /// Check both strikes before rolling or changing health, including a counter that cannot
    /// happen. Physical damage still uses the provisional order in CombatDamageCalculator.
    /// Follow-ups use the provisional damage scaling from Stage 3.10A. Nothing reacts to
    /// the follow-up: it cannot cause a counter or another follow-up.
    /// If the random provider fails on a later roll, earlier damage stays applied.
    /// There is no rollback here.
    /// </remarks>
    public CombatExchangeResult Resolve(CombatExchangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Attacker);
        ArgumentNullException.ThrowIfNull(request.Defender);
        if (request.Attacker.IsDefeated)
            throw new ArgumentException("The attacker must be alive when an exchange starts.", nameof(request.Attacker));
        if (request.Defender.IsDefeated)
            throw new ArgumentException("The defender must be alive when an exchange starts.", nameof(request.Defender));

        request = WithEffectiveAttacks(request);

        // Check distance and range now, before the opening strike can change health.
        _ = CanCounter(request);
        // Validate configuration before any rolls or damage. Use the same Speed snapshot
        // for eligibility and scaling so a newly qualifying follow-up uses the right ratio.
        int attackerSpeed = request.IncomingAttackPermitsFollowUps ? request.Attacker.EffectiveSpeed : 0;
        int defenderSpeed = request.IncomingAttackPermitsFollowUps ? request.Defender.EffectiveSpeed : 0;
        var opening = PrepareStrike(request.OpeningAttackType,
            request.Attacker, request.Defender, request.OpeningPower,
            request.AttackerWeapon, request.DefenderWeapon, request.OpeningBaseAccuracy);
        var counter = PrepareStrike(request.CounterattackType,
            request.Defender, request.Attacker, request.CounterattackPower,
            request.DefenderWeapon, request.AttackerWeapon, request.CounterattackBaseAccuracy);

        var openingResult = _strikeResolver.ResolvePreparedStrike(opening);
        int defenderHealthLost = StrikeDamageApplicator.Apply(openingResult, request.Defender);

        StrikeResult? counterResult = null;
        int attackerHealthLost = 0;
        if (CanCounter(request))
        {
            counterResult = _strikeResolver.ResolvePreparedStrike(counter);
            attackerHealthLost = StrikeDamageApplicator.Apply(counterResult, request.Attacker);
        }

        StrikeResult? followUpResult = null;
        var followUpBy = FollowUpParticipant.None;
        int followUpHealthLost = 0;
        if (request.IncomingAttackPermitsFollowUps && !request.Attacker.IsDefeated && !request.Defender.IsDefeated)
        {
            var eligible = FollowUpEligibility.Evaluate(attackerSpeed, defenderSpeed);
            if (eligible == FollowUpParticipant.Attacker)
            {
                followUpResult = ResolveFollowUp(opening, attackerSpeed, defenderSpeed);
                followUpHealthLost = StrikeDamageApplicator.Apply(followUpResult, request.Defender);
                defenderHealthLost += followUpHealthLost;
                followUpBy = eligible;
            }
            else if (eligible == FollowUpParticipant.Defender && CanCounter(request))
            {
                followUpResult = ResolveFollowUp(counter, defenderSpeed, attackerSpeed);
                followUpHealthLost = StrikeDamageApplicator.Apply(followUpResult, request.Attacker);
                attackerHealthLost += followUpHealthLost;
                followUpBy = eligible;
            }
        }

        return new CombatExchangeResult(openingResult, defenderHealthLost, counterResult, attackerHealthLost,
            request.Attacker.IsDefeated, request.Defender.IsDefeated, followUpBy, followUpResult, followUpHealthLost);
    }

    private StrikeResult ResolveFollowUp(
        (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) prepared,
        int fasterSpeed,
        int slowerSpeed)
    {
        // Roll the participant's own attack again, but only apply the reduced result.
        var normalStrike = _strikeResolver.ResolvePreparedStrike(prepared);
        int reducedDamage = ProvisionalFollowUpDamageCalculator.Calculate(normalStrike.Damage, fasterSpeed, slowerSpeed);
        return new StrikeResult(normalStrike.Hit, normalStrike.HitChance, reducedDamage, normalStrike.TriangleOutcome);
    }

    private static bool CanCounter(CombatExchangeRequest request) => CounterattackEligibility.CanCounter(
        request.Defender, request.Distance, request.CounterattackMinimumRange, request.CounterattackMaximumRange,
        request.IncomingAttackPermitsCounters, request.CounterattacksSuppressed);

    private static CombatExchangeRequest WithEffectiveAttacks(CombatExchangeRequest request)
    {
        // Ordinary opening range is already checked by the caller. Only unarmed range
        // is enforced here; the wide interval preserves that existing contract.
        var suppliedOpening = request.AttackerWeapon == WeaponCategory.Unarmed
            ? new AttackConfiguration(request.OpeningAttackType, request.AttackerWeapon, request.OpeningPower,
                request.OpeningBaseAccuracy, 1, 1)
            : new AttackConfiguration(request.OpeningAttackType, request.AttackerWeapon, request.OpeningPower,
                request.OpeningBaseAccuracy, 0, int.MaxValue);
        var suppliedCounter = new AttackConfiguration(request.CounterattackType, request.DefenderWeapon,
            request.CounterattackPower, request.CounterattackBaseAccuracy,
            request.CounterattackMinimumRange, request.CounterattackMaximumRange);
        var opening = DisarmEligibility.GetEffectiveAttack(request.Attacker, suppliedOpening, request.AttackerUnarmedAttack);
        var counter = DisarmEligibility.GetEffectiveAttack(request.Defender, suppliedCounter, request.DefenderUnarmedAttack);
        if (opening.Category == WeaponCategory.Unarmed && request.Distance != 1)
            throw new ArgumentOutOfRangeException(nameof(request.Distance), "Unarmed opening attacks require distance 1.");

        // Both combatants use their effective categories when calculating triangle effects.
        return request with
        {
            OpeningAttackType = opening.Type, OpeningPower = opening.Power,
            OpeningBaseAccuracy = opening.BaseAccuracy, AttackerWeapon = opening.Category,
            CounterattackType = counter.Type, CounterattackPower = counter.Power,
            CounterattackBaseAccuracy = counter.BaseAccuracy, DefenderWeapon = counter.Category,
            CounterattackMinimumRange = counter.MinimumRange, CounterattackMaximumRange = counter.MaximumRange
        };
    }

    private static (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) PrepareStrike(
        AttackType attackType, Character attacker, Character defender, int power,
        WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int baseAccuracy) => attackType switch
    {
        AttackType.Physical => SingleStrikeResolver.PreparePhysicalStrike(
            attacker.BaseStats, defender.BaseStats, power, attackerWeapon, defenderWeapon, baseAccuracy, defender.EffectiveDefense),
        AttackType.Magical => SingleStrikeResolver.PrepareMagicalStrike(
            attacker.BaseStats, defender.BaseStats, power, attackerWeapon, defenderWeapon, baseAccuracy),
        _ => throw new ArgumentOutOfRangeException(nameof(attackType), attackType, "Unsupported attack type.")
    };
}
