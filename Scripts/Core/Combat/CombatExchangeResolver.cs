using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Runs an opening strike, an eligible counterattack, then at most one permitted follow-up.</summary>
/// <remarks>
/// This is low-level combat, not permission to perform a player action. Player attacks go through
/// AttackActionExecutor or the applicable ability executor so activation, targeting and taunt
/// approval are checked before effects. Non-player effects and combat tests may use this directly.
/// </remarks>
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
        => ResolveOpening(request, 0, false, false);

    // A prevalidated ability-specific opening can use the ordinary strike and HP application
    // without preparing an unavailable counterattack. The existing request permissions remain
    // authoritative: this path cannot be used to suppress a reaction that was requested.
    internal CombatExchangeResult ResolveSuppressedOpening(CombatExchangeRequest request,
        (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) opening, int ordinaryOpeningDamage)
    {
        if (!request.CounterattacksSuppressed || request.IncomingAttackPermitsCounters || request.IncomingAttackPermitsFollowUps)
            throw new ArgumentException("This path requires explicitly suppressed counters and no follow-up execution.", nameof(request));
        if (request.Attacker.IsDefeated || request.Defender.IsDefeated)
            throw new ArgumentException("Both opening participants must be alive.", nameof(request));
        var strike = _strikeResolver.ResolvePreparedStrike(opening);
        int shields = request.Defender.ShieldHP;
        int lost = StrikeDamageApplicator.Apply(strike, request.Defender);
        return new(strike, lost, null, 0, request.Attacker.IsDefeated, request.Defender.IsDefeated,
            openingBaseDamage: ordinaryOpeningDamage, openingHealthLost: lost,
            openingShieldAbsorbed: shields - request.Defender.ShieldHP, counterattacksSuppressed: true);
    }

    /// <summary>Checks ordinary exchange inputs without rolling or changing either character.</summary>
    internal static void ValidateRequest(CombatExchangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Attacker);
        ArgumentNullException.ThrowIfNull(request.Defender);
        if (request.Attacker.IsDefeated || request.Defender.IsDefeated)
            throw new ArgumentException("Both participants must be alive before an exchange starts.", nameof(request));
        request = WithEffectiveAttacks(request);
        _ = CanCounter(request);
        if (request.IncomingAttackPermitsFollowUps)
        {
            _ = request.Attacker.EffectiveSpeed;
            _ = request.Defender.EffectiveSpeed;
        }
        var opening = PrepareStrike(request.OpeningAttackType, request.Attacker, request.Defender, request.OpeningPower,
            request.AttackerWeapon, request.DefenderWeapon, request.OpeningBaseAccuracy, request.OpeningEvasionModifier, request.Sanctuary);
        var counter = PrepareStrike(request.CounterattackType, request.Defender, request.Attacker, request.CounterattackPower,
            request.DefenderWeapon, request.AttackerWeapon, request.CounterattackBaseAccuracy, request.CounterattackEvasionModifier,
            CanCounter(request) ? request.Sanctuary : null);
        ValidateFutureFury(request, opening.CandidateDamage, counter.CandidateDamage);
    }

    // Shadow Strike uses the existing exchange sequence. These opening-only effects
    // are deliberately separate from the ordinary attack reused for a follow-up.
    internal CombatExchangeResult ResolveOpening(CombatExchangeRequest request, int openingBonus,
        bool disarmOnOpeningHit, bool revealAttacker)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(openingBonus);
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
            request.AttackerWeapon, request.DefenderWeapon, request.OpeningBaseAccuracy, request.OpeningEvasionModifier,
            request.Sanctuary, out var openingWinterHunter);
        var counter = PrepareStrike(request.CounterattackType,
            request.Defender, request.Attacker, request.CounterattackPower,
            request.DefenderWeapon, request.AttackerWeapon, request.CounterattackBaseAccuracy, request.CounterattackEvasionModifier,
            CanCounter(request) ? request.Sanctuary : null);
        ValidateFutureFury(request, opening.CandidateDamage, counter.CandidateDamage);

        // Check the on-hit unarmed configuration before rolling. A miss leaves the
        // equipped attack available; a surviving hit makes Disarm effective immediately.
        var disarmedRequest = request;
        var disarmedCounter = counter;
        var disarmedFollowUp = opening;
        if (disarmOnOpeningHit)
        {
            disarmedRequest = WithUnarmedDefender(request);
            _ = CanCounter(disarmedRequest);
            disarmedCounter = PrepareStrike(disarmedRequest.CounterattackType,
                request.Defender, request.Attacker, disarmedRequest.CounterattackPower,
                disarmedRequest.DefenderWeapon, request.AttackerWeapon,
                disarmedRequest.CounterattackBaseAccuracy, request.CounterattackEvasionModifier,
                CanCounter(disarmedRequest) ? request.Sanctuary : null);
            disarmedFollowUp = PrepareStrike(request.OpeningAttackType,
                request.Attacker, request.Defender, request.OpeningPower,
                request.AttackerWeapon, disarmedRequest.DefenderWeapon,
                request.OpeningBaseAccuracy, request.OpeningEvasionModifier, request.Sanctuary);
            ValidateFutureFury(disarmedRequest, disarmedFollowUp.CandidateDamage, disarmedCounter.CandidateDamage);
        }
        int ordinaryOpeningDamage = opening.CandidateDamage;
        var boostedOpening = (opening.HitChance, CandidateDamage: checked(ordinaryOpeningDamage + openingBonus), opening.Outcome);
        var openingResult = _strikeResolver.ResolvePreparedStrike(boostedOpening, openingWinterHunter);
        if (revealAttacker) request.Attacker.SmokeReveal.RecordAttack();
        int shieldBefore = request.Defender.ShieldHP;
        int openingHealthLost = StrikeDamageApplicator.Apply(openingResult, request.Defender);
        int openingShieldAbsorbed = shieldBefore - request.Defender.ShieldHP;
        int defenderHealthLost = openingHealthLost;
        bool disarmApplied = disarmOnOpeningHit && openingResult.Hit && !request.Defender.IsDefeated;
        if (disarmApplied)
        {
            request.Defender.Statuses.ApplyTimedStatus(StatusType.Disarm, 1);
            request = disarmedRequest;
            counter = disarmedCounter;
            opening = disarmedFollowUp;
        }

        StrikeResult? counterResult = null;
        int attackerHealthLost = 0;
        if (CanCounter(request))
        {
            // Fury depends on HP now, after the opening strike, not at exchange start.
            counter = PrepareStrike(request.CounterattackType, request.Defender, request.Attacker,
                request.CounterattackPower, request.DefenderWeapon, request.AttackerWeapon,
                request.CounterattackBaseAccuracy, request.CounterattackEvasionModifier, request.Sanctuary, out var counterWinterHunter);
            counterResult = _strikeResolver.ResolvePreparedStrike(counter, counterWinterHunter);
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
                opening = PrepareStrike(request.OpeningAttackType, request.Attacker, request.Defender,
                    request.OpeningPower, request.AttackerWeapon, request.DefenderWeapon,
                    request.OpeningBaseAccuracy, request.OpeningEvasionModifier, request.Sanctuary, out var followUpWinterHunter);
                followUpResult = ResolveFollowUp(opening, attackerSpeed, defenderSpeed, followUpWinterHunter);
                followUpHealthLost = StrikeDamageApplicator.Apply(followUpResult, request.Defender);
                defenderHealthLost += followUpHealthLost;
                followUpBy = eligible;
            }
            else if (eligible == FollowUpParticipant.Defender && CanCounter(request))
            {
                counter = PrepareStrike(request.CounterattackType, request.Defender, request.Attacker,
                    request.CounterattackPower, request.DefenderWeapon, request.AttackerWeapon,
                    request.CounterattackBaseAccuracy, request.CounterattackEvasionModifier, request.Sanctuary, out var followUpWinterHunter);
                followUpResult = ResolveFollowUp(counter, defenderSpeed, attackerSpeed, followUpWinterHunter);
                followUpHealthLost = StrikeDamageApplicator.Apply(followUpResult, request.Attacker);
                attackerHealthLost += followUpHealthLost;
                followUpBy = eligible;
            }
        }

        return new CombatExchangeResult(openingResult, defenderHealthLost, counterResult, attackerHealthLost,
            request.Attacker.IsDefeated, request.Defender.IsDefeated, followUpBy, followUpResult, followUpHealthLost,
            ordinaryOpeningDamage, openingBonus, openingHealthLost, openingShieldAbsorbed, disarmApplied,
            request.CounterattacksSuppressed || !request.IncomingAttackPermitsCounters);
    }

    private StrikeResult ResolveFollowUp(
        (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) prepared,
        int fasterSpeed,
        int slowerSpeed,
        WinterHunterEvaluation? winterHunter)
    {
        // Roll the participant's own attack again, but only apply the reduced result.
        // Flat bonuses, including Winter Hunter, are already present before the existing
        // provisional percentage reduction. This does not finalize the universal modifier order.
        var normalStrike = _strikeResolver.ResolvePreparedStrike(prepared, winterHunter);
        int reducedDamage = ProvisionalFollowUpDamageCalculator.Calculate(normalStrike.Damage, fasterSpeed, slowerSpeed);
        return new StrikeResult(normalStrike.Hit, normalStrike.HitChance, reducedDamage, normalStrike.TriangleOutcome, normalStrike.WinterHunter);
    }

    private static CombatExchangeRequest WithUnarmedDefender(CombatExchangeRequest request)
    {
        if (request.DefenderWeapon == WeaponCategory.Unarmed) return request;
        var unarmed = request.DefenderUnarmedAttack
            ?? throw new ArgumentException("An invisible Shadow Strike requires explicit unarmed counter configuration.", nameof(request));
        if (unarmed.Category != WeaponCategory.Unarmed)
            throw new ArgumentException("The defender fallback must be unarmed.", nameof(request));
        return request with
        {
            DefenderWeapon = unarmed.Category, CounterattackType = unarmed.Type,
            CounterattackPower = unarmed.Power, CounterattackBaseAccuracy = unarmed.BaseAccuracy,
            CounterattackMinimumRange = unarmed.MinimumRange, CounterattackMaximumRange = unarmed.MaximumRange
        };
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

    private static void ValidateFutureFury(CombatExchangeRequest request, int openingDamage, int counterDamage)
    {
        if (request.OpeningAttackType == AttackType.Physical && request.IncomingAttackPermitsFollowUps)
            BerserkersFury.ValidatePotentialDamage(request.Attacker, openingDamage);
        if (request.CounterattackType == AttackType.Physical)
            BerserkersFury.ValidatePotentialDamage(request.Defender, counterDamage);
    }

    private static (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) PrepareStrike(
        AttackType attackType, Character attacker, Character defender, int power,
        WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int baseAccuracy, int evasionModifier,
        SanctuaryDefenseContext? sanctuary)
        => PrepareStrike(attackType, attacker, defender, power, attackerWeapon, defenderWeapon, baseAccuracy, evasionModifier, sanctuary, out _);

    private static (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome) PrepareStrike(
        AttackType attackType, Character attacker, Character defender, int power,
        WeaponCategory attackerWeapon, WeaponCategory defenderWeapon, int baseAccuracy, int evasionModifier,
        SanctuaryDefenseContext? sanctuary, out WinterHunterEvaluation? winterHunter)
    {
        winterHunter = attackType == AttackType.Physical ? WinterHunter.Evaluate(attacker, defender) : null;
        return attackType switch
        {
            AttackType.Physical => SingleStrikeResolver.PreparePhysicalStrike(
                attacker.BaseStats, defender.BaseStats, power, attackerWeapon, defenderWeapon, baseAccuracy,
                SanctuaryOfTheForgotten.DefenseForStrike(attacker, defender, sanctuary),
                evasionModifier, checked(BerserkersFury.GetPhysicalDamageBonus(attacker) + winterHunter!.PhysicalDamageBonus)),
            AttackType.Magical => SingleStrikeResolver.PrepareMagicalStrike(
                attacker.BaseStats, defender.BaseStats, power, attackerWeapon, defenderWeapon, baseAccuracy, evasionModifier,
                defender.EffectiveResistance, TemporalAccumulation.GetMagicalDamageBonus(attacker)),
            _ => throw new ArgumentOutOfRangeException(nameof(attackType), attackType, "Unsupported attack type.")
        };
    }
}
