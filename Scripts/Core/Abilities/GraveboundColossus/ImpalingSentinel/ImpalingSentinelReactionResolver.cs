using System;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Abilities;

public sealed record ImpalingSentinelReactionResult
{
    public ImpalingSentinelTriggerResult Trigger { get; }
    public ImpalingSentinelReason RejectionReason { get; }
    public CombatExchangeResult? Combat { get; }
    public StrikeResult? Strike => Combat?.OpeningStrike;
    public bool CanExecute => !Resolved && Trigger.IsEligible && RejectionReason == ImpalingSentinelReason.None;
    public bool Resolved => Combat is not null;
    /// <summary>A hit stops further normal movement; a miss permits it. This does not move the target.</summary>
    public bool? MovementMayContinue => Strike is null ? null : !Strike.Hit;
    public ImpalingSentinelPreparation? PreparationAfter { get; }
    public bool PreparationRemainsActive => (PreparationAfter ?? Trigger.Preparation)?.Status == SentinelPreparationStatus.Prepared;

    internal ImpalingSentinelReactionResult(ImpalingSentinelTriggerResult trigger, ImpalingSentinelReason reason,
        CombatExchangeResult? combat = null, ImpalingSentinelPreparation? after = null)
    {
        Trigger = trigger;
        RejectionReason = reason;
        Combat = combat;
        PreparationAfter = after;
    }
}

/// <summary>
/// Runs normal single-target Lance combat (section 4.2), with Sentinel's explicit no-follow-up exception.
/// Reports movement interruption to the external coordinator without moving or spending the mover's actions.
/// </summary>
public sealed class ImpalingSentinelReactionResolver
{
    private readonly ImpalingSentinelTracker _tracker;
    private readonly ImpalingSentinelTriggerEvaluator _triggers;
    private readonly CombatExchangeResolver _combat;
    public ImpalingSentinelReactionResolver(ImpalingSentinelTracker tracker, CombatExchangeResolver combat)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _triggers = new(tracker);
    }

    public ImpalingSentinelReactionResult Preview(string ownerId, SentinelMovementEvent movement,
        AttackConfiguration? currentLance, CombatExchangeRequest? request, ISentinelReactionMap? map)
    {
        var trigger = _triggers.Evaluate(ownerId, movement, currentLance, map);
        if (!trigger.IsEligible) return new(trigger, trigger.RejectionReason);
        if (request is null) return new(trigger, ImpalingSentinelReason.CombatInformationRequired);
        if (!ReferenceEquals(request.Attacker, _tracker.FindCharacter(ownerId)) ||
            !ReferenceEquals(request.Defender, _tracker.FindCharacter(movement.CharacterId)) ||
            request.OpeningAttackType != AttackType.Physical || request.AttackerWeapon != WeaponCategory.Lance ||
            request.OpeningPower != currentLance!.Power || request.OpeningBaseAccuracy != currentLance.BaseAccuracy ||
            request.Distance != trigger.DistanceAfter || !request.IncomingAttackPermitsCounters)
            return new(trigger, ImpalingSentinelReason.ContextMismatch);
        // Competing Colossi need an ordering rule. Do not choose by roster order or Speed.
        foreach (var other in _tracker.Preparations)
        {
            if (other.OwnerCharacterId == ownerId || other.Status != SentinelPreparationStatus.Prepared) continue;
            var otherTrigger = _triggers.Evaluate(other.OwnerCharacterId, movement, other.Lance, map);
            if (otherTrigger.IsEligible || otherTrigger.RejectionReason is ImpalingSentinelReason.MissingMapInformation
                    or ImpalingSentinelReason.SpellRestrictionUnresolved)
                return new(trigger, ImpalingSentinelReason.MultipleReactionsOrderingUnresolved);
        }
        CombatExchangeResolver.ValidateRequest(WithoutFollowUps(request));
        return new(trigger, ImpalingSentinelReason.None);
    }

    /// <remarks>
    /// Revalidate immediately before combat. Map queries and random providers must not re-enter
    /// match operations. A provider exception after starting consumes the prototype reaction;
    /// damage already applied is not rolled back and that event cannot be retried.
    /// </remarks>
    public ImpalingSentinelReactionResult Execute(string ownerId, SentinelMovementEvent movement,
        AttackConfiguration? currentLance, CombatExchangeRequest? request, ISentinelReactionMap? map)
    {
        var preview = Preview(ownerId, movement, currentLance, request, map);
        if (!preview.CanExecute) return preview;
        _tracker.RecordReactionStarted(preview.Trigger.Preparation!, movement);
        var combat = _combat.Resolve(WithoutFollowUps(request!));
        return new(preview.Trigger, ImpalingSentinelReason.None, combat, _tracker.GetPreparation(ownerId));
    }

    private CombatExchangeRequest WithoutFollowUps(CombatExchangeRequest request)
        => request with { IncomingAttackPermitsFollowUps = false,
            Sanctuary = SanctuaryDefenseContext.FromActivations(_tracker.Activations) };
}
