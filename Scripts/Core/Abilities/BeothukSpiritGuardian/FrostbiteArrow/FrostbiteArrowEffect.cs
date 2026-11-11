using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;

namespace PawnsOfDivinity.Core.Abilities;

public enum FrostbiteArrowStatusOutcome { NotApplied, Applied, Refreshed }

public sealed record FrostbiteArrowProcResult
{
    public string AttackerId { get; }
    public string TargetId { get; }
    public int RoundNumber { get; }
    public bool HitEligible { get; }
    public bool RollAttempted { get; }
    public bool? ProcSucceeded { get; }
    public bool ApplicationEligible { get; }
    public FrostbiteArrowReason Reason { get; }
    public FrostbiteArrowStatusOutcome StatusOutcome { get; }
    public bool StatusApplied => StatusOutcome != FrostbiteArrowStatusOutcome.NotApplied;
    public FrostbiteArrowDurationPolicy? DurationPolicy => Rules.DurationPolicy;
    public int? DurationRounds => Rules.DurationPolicy.HasValue ? 2 : null;
    public int? RoundApplied => StatusApplied ? RoundNumber : null;
    internal FrostbiteArrowEffect Owner { get; }
    internal Character Attacker { get; }
    internal Character Target { get; }
    internal StrikeResult? Strike { get; }
    internal int ShieldAbsorbed { get; }
    internal FrostbiteArrowContext Rules { get; }
    internal object ApplicationToken { get; }

    internal FrostbiteArrowProcResult(FrostbiteArrowEffect owner, Character attacker, Character target, StrikeResult? strike,
        int shieldAbsorbed, int round, FrostbiteArrowContext rules, bool hitEligible, bool applicationEligible,
        FrostbiteArrowReason reason, bool rollAttempted = false, bool? procSucceeded = null,
        FrostbiteArrowStatusOutcome statusOutcome = FrostbiteArrowStatusOutcome.NotApplied, object? applicationToken = null)
    {
        Owner = owner;
        Attacker = attacker;
        Target = target;
        AttackerId = attacker.Id;
        TargetId = target.Id;
        Strike = strike;
        ShieldAbsorbed = shieldAbsorbed;
        RoundNumber = round;
        Rules = rules;
        HitEligible = hitEligible;
        ApplicationEligible = applicationEligible;
        Reason = reason;
        RollAttempted = rollAttempted;
        ProcSucceeded = procSucceeded;
        StatusOutcome = statusOutcome;
        ApplicationToken = applicationToken ?? new object();
    }
}

/// <summary>One strike's status chance, separate from combat accuracy, damage and action bookkeeping.</summary>
public sealed class FrostbiteArrowEffect
{
    public const int ProcChance = 40;
    private readonly HitRoller _statusRoller;
    private readonly HashSet<object> _applied = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<StrikeResult> _startedStrikes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<StrikeResult, FrostbiteArrowProcResult> _resolvedStrikes = new(ReferenceEqualityComparer.Instance);
    public FrostbiteArrowEffect(HitRoller statusRoller)
        => _statusRoller = statusRoller ?? throw new ArgumentNullException(nameof(statusRoller));

    public FrostbiteArrowProcResult Evaluate(Character attacker, Character target, StrikeResult? strike,
        int shieldAbsorbed, int round, FrostbiteArrowContext rules)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegative(shieldAbsorbed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        if (strike is not null && (!strike.Hit && shieldAbsorbed != 0 || shieldAbsorbed > strike.Damage))
            throw new ArgumentException("Shield absorption must belong to this strike.", nameof(shieldAbsorbed));
        if (attacker.Definition?.Id != "beothuk_spirit_guardian") return Result(FrostbiteArrowReason.NotSpiritGuardian);
        if (!attacker.Definition.Abilities.Any(a => a.Id == BeothukSpiritGuardian.FrostbiteArrowId)) return Result(FrostbiteArrowReason.AbilityNotOwned);
        if (attacker.IsDefeated) return Result(FrostbiteArrowReason.AttackerDefeated);
        if (attacker.Id == target.Id || attacker.TeamId == target.TeamId) return Result(FrostbiteArrowReason.TargetNotEnemy);
        if (strike is null) return Result(FrostbiteArrowReason.NoConfirmedStrike);
        if (!strike.Hit) return Result(FrostbiteArrowReason.Miss);
        if (target.IsDefeated) return Result(FrostbiteArrowReason.TargetDefeated);
        if (shieldAbsorbed > 0) return Result(FrostbiteArrowReason.ShieldEligibilityUnresolved);
        // The chance is known even if the caller has not approved an application policy.
        if (!rules.DurationPolicy.HasValue) return Result(FrostbiteArrowReason.DurationUnresolved, true);
        if (!rules.TargetHasTerrainFrostbite.HasValue) return Result(FrostbiteArrowReason.TerrainContextRequired, true);
        if (rules.TargetHasTerrainFrostbite.Value) return Result(FrostbiteArrowReason.TerrainInteractionUnresolved, true);
        return Result(FrostbiteArrowReason.None, true, true);

        FrostbiteArrowProcResult Result(FrostbiteArrowReason reason, bool hitEligible = false, bool applicationEligible = false)
            => new(this, attacker, target, strike, shieldAbsorbed, round, rules, hitEligible, applicationEligible, reason);
    }

    public FrostbiteArrowProcResult Resolve(Character attacker, Character target, StrikeResult? strike,
        int shieldAbsorbed, int round, FrostbiteArrowContext rules)
    {
        var evaluation = Evaluate(attacker, target, strike, shieldAbsorbed, round, rules);
        if (!evaluation.HitEligible) return evaluation;
        if (_resolvedStrikes.TryGetValue(strike!, out var previous))
            return ReferenceEquals(previous.Attacker, attacker) && ReferenceEquals(previous.Target, target)
                && previous.RoundNumber == round && previous.Rules == rules && previous.ShieldAbsorbed == shieldAbsorbed
                ? previous : Copy(evaluation, FrostbiteArrowReason.InvalidProcContext, applicationEligible: false);
        if (!_startedStrikes.Add(strike!)) return Copy(evaluation, FrostbiteArrowReason.AlreadyStarted, applicationEligible: false);
        bool proc = _statusRoller.Roll(ProcChance);
        var result = Copy(evaluation, evaluation.ApplicationEligible ? (proc ? FrostbiteArrowReason.None : FrostbiteArrowReason.NoProc)
            : evaluation.Reason, true, proc);
        _resolvedStrikes.Add(strike!, result);
        return result;
    }

    /// <summary>Applies one approved proc using the existing status tracker. A proc token cannot apply twice.</summary>
    public FrostbiteArrowProcResult Apply(FrostbiteArrowProcResult proc, int currentRound)
    {
        ArgumentNullException.ThrowIfNull(proc);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentRound);
        if (!ReferenceEquals(proc.Owner, this)) return Copy(proc, FrostbiteArrowReason.InvalidProcContext, applicationEligible: false);
        if (currentRound != proc.RoundNumber) return Copy(proc, FrostbiteArrowReason.WrongRound, applicationEligible: false);
        if (proc.StatusApplied || _applied.Contains(proc.ApplicationToken)) return Copy(proc, FrostbiteArrowReason.AlreadyApplied, applicationEligible: false);
        if (proc.ProcSucceeded != true) return Copy(proc, proc.RollAttempted ? FrostbiteArrowReason.NoProc : proc.Reason);
        var current = Evaluate(proc.Attacker, proc.Target, proc.Strike, proc.ShieldAbsorbed, currentRound, proc.Rules);
        if (!current.ApplicationEligible) return Copy(proc, current.Reason, applicationEligible: false);
        bool refresh = proc.Target.Statuses.HasStatus(StatusType.Frostbite);
        _applied.Add(proc.ApplicationToken);
        proc.Target.Statuses.ApplyTimedStatus(StatusType.Frostbite, proc.DurationRounds!.Value);
        return Copy(proc, FrostbiteArrowReason.None, status: refresh ? FrostbiteArrowStatusOutcome.Refreshed : FrostbiteArrowStatusOutcome.Applied);
    }

    private static FrostbiteArrowProcResult Copy(FrostbiteArrowProcResult proc, FrostbiteArrowReason reason,
        bool? rolled = null, bool? success = null, FrostbiteArrowStatusOutcome status = FrostbiteArrowStatusOutcome.NotApplied,
        bool? applicationEligible = null)
        => new(proc.Owner, proc.Attacker, proc.Target, proc.Strike, proc.ShieldAbsorbed, proc.RoundNumber, proc.Rules,
            proc.HitEligible, applicationEligible ?? proc.ApplicationEligible, reason, rolled ?? proc.RollAttempted,
            success ?? proc.ProcSucceeded, status, proc.ApplicationToken);
}
