using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

public enum TemporalIgnitionRejectionReason { None, NotTimeWeaver, CombatConfigurationRequired, RequiresMagicalAttack }

/// <summary>Immutable casting preview or outcome; only ScheduledAttack represents a queued attack.</summary>
public sealed record TemporalIgnitionUseResult
{
    public string CasterId { get; }
    public string TargetId { get; }
    public string DelayedAttackId { get; }
    public int RoundCast { get; }
    public int? ResolutionRound { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public AbilityTargetingResult? Targeting { get; }
    public TemporalIgnitionRejectionReason RejectionReason { get; }
    public int? ExpectedDamageOnHit => Prepared?.CandidateDamage;
    public int? FinalHitChance => Prepared?.HitChance;
    public DelayedAttack? ScheduledAttack { get; }
    public StrikeResult? StoredStrike => ScheduledAttack?.Strike;
    public int? StoredDamage => StoredStrike?.Damage;
    public AbilityUseResult? Finalization { get; }
    public TemporalAccumulationResult? Accumulation { get; }
    public bool CanExecute => Eligibility.IsAllowed && Targeting?.IsLegal == true
        && RejectionReason == TemporalIgnitionRejectionReason.None && Prepared.HasValue && ResolutionRound.HasValue;
    public bool Succeeded => ScheduledAttack is not null && Finalization?.Succeeded == true;
    internal (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome)? Prepared { get; }

    internal TemporalIgnitionUseResult(AbilityUse use, string targetId, string attackId, AbilityEligibilityResult eligibility,
        AbilityTargetingResult? targeting = null, TemporalIgnitionRejectionReason reason = TemporalIgnitionRejectionReason.None,
        int? resolutionRound = null, (int HitChance, int CandidateDamage, WeaponTriangleOutcome Outcome)? prepared = null,
        DelayedAttack? attack = null, AbilityUseResult? finalization = null, TemporalAccumulationResult? accumulation = null)
    {
        CasterId = use.CharacterId;
        TargetId = targetId;
        DelayedAttackId = attackId;
        RoundCast = use.RoundNumber;
        Eligibility = eligibility;
        Targeting = targeting;
        RejectionReason = reason;
        ResolutionRound = resolutionRound;
        Prepared = prepared;
        ScheduledAttack = attack;
        Finalization = finalization;
        Accumulation = accumulation;
    }
}
