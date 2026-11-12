using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Core.Turns.Restrictions;

namespace PawnsOfDivinity.Tests.Scenarios;

/// <summary>Adapts fixture requests to production player actions and records what actually happened.</summary>
public sealed class ScenarioRunner
{
    private readonly CombatScenario _scenario;
    private readonly AttackActionExecutor _attacks;
    private readonly FrostbiteArrowAbilityExecutor _arrows;
    private readonly List<ScenarioExecutionResult> _results = new();
    private readonly List<ScenarioEvent> _events = new();
    private readonly List<ScenarioRandomDraw> _draws = new();
    private readonly HashSet<string> _attempted = new(StringComparer.Ordinal);
    public ScriptedScenarioRandom Accuracy { get; }
    public ScriptedScenarioRandom Status { get; }
    public ScriptedScenarioRandom Selection { get; }
    public IReadOnlyList<ScenarioExecutionResult> Results => Array.AsReadOnly(_results.ToArray());
    public IReadOnlyList<ScenarioEvent> History => Array.AsReadOnly(_events.ToArray());
    public IReadOnlyList<ScenarioRandomDraw> Draws => Array.AsReadOnly(_draws.ToArray());

    internal ScenarioRunner(CombatScenario scenario, int[] accuracy, int[] status, int[] selection)
    {
        _scenario = scenario;
        Accuracy = new("accuracy", accuracy, RecordDraw);
        Status = new("status", status, RecordDraw);
        Selection = new("selection", selection, RecordDraw);
        var combat = new CombatExchangeResolver(new SingleStrikeResolver(new HitRoller(Accuracy)));
        _attacks = new(combat);
        _ = new MovementActionExecutor(scenario.Activations, scenario.Lifecycle, scenario.Map, scenario.Walls,
            scenario.Ice, scenario.Sentinel, combat, new MovementInputs(scenario));
        _arrows = new(combat, new FrostbiteArrowEffect(new HitRoller(Status)));
    }
    private sealed class MovementInputs(CombatScenario scenario) : IMovementReactionInputs
    {
        public AttackConfiguration? CurrentLance(string ownerId) => scenario.AttackInputs.GetValueOrDefault(ownerId)?.Configuration;
        public CombatExchangeRequest? CreateRequest(string ownerId, string moverId, int distance)
        {
            var lance = CurrentLance(ownerId);
            var counter = scenario.AttackInputs.GetValueOrDefault(moverId)?.Configuration;
            if (lance is null || counter is null) return null;
            return new(scenario.Characters[ownerId], scenario.Characters[moverId], lance.Type, lance.Power, lance.Category,
                counter.Category, lance.BaseAccuracy, counter.Type, counter.Power, counter.BaseAccuracy,
                counter.MinimumRange, counter.MaximumRange, distance, true, false);
        }
    }

    private void RecordDraw(string stream, int? value, int maximum, ScenarioDrawOutcome outcome)
        => _draws.Add(new(_draws.Count + 1, stream, value, maximum, outcome));

    /// <summary>Checks current production permissions without reserving an action ID or changing history.</summary>
    public ScenarioExecutionResult Preview(ScenarioAction action) => Run(action, true);
    /// <summary>Executes once. Even a partial or failed attempt cannot be automatically replayed with this ID.</summary>
    public ScenarioExecutionResult Execute(ScenarioAction action) => Run(action, false);

    private sealed record Decision(ScenarioOutcome Outcome, string Reason = "", ScenarioProductionOutcome? Production = null);
    private ScenarioExecutionResult Run(ScenarioAction action, bool preview)
    {
        ArgumentNullException.ThrowIfNull(action);
        var before = _scenario.Capture();
        int eventStart = _events.Count, drawStart = _draws.Count;
        if (!preview) Emit(action.ActorId, action.TargetId, ScenarioEventType.ActionRequested);
        Decision decision;
        try
        {
            if (_attempted.Contains(action.Id)) decision = new(ScenarioOutcome.Rejected, "ActionAlreadyAttempted");
            else
            {
                if (!preview) _attempted.Add(action.Id);
                decision = Dispatch(action, true);
                if (!preview && decision.Outcome == ScenarioOutcome.Ready)
                {
                    if (action.Kind == ScenarioActionKind.Attack || action.AbilityId == "frostbite_arrow")
                        Emit(action.ActorId, action.TargetId, ScenarioEventType.AttackStarted);
                    decision = Dispatch(action, false);
                }
            }
        }
        catch (SanctuaryDefenseUnresolvedException error)
        { decision = new(ScenarioOutcome.Unresolved, error.Result.Reason.ToString()); }
        catch (Exception error)
        { decision = new(ScenarioOutcome.UnexpectedFailure, $"{error.GetType().Name}: {error.Message}"); }
        var after = _scenario.Capture();
        if (preview && (before.ToCanonicalJson() != after.ToCanonicalJson() || _draws.Count != drawStart))
            decision = decision with { Outcome = before.ToCanonicalJson() != after.ToCanonicalJson()
                ? ScenarioOutcome.PartiallyApplied : ScenarioOutcome.UnexpectedFailure, Reason = "Production preview changed state or consumed randomness" };
        if (decision.Outcome != ScenarioOutcome.Completed && decision.Outcome != ScenarioOutcome.Ready && decision.Outcome != ScenarioOutcome.Interrupted &&
            before.ToCanonicalJson() != after.ToCanonicalJson())
            decision = decision with { Outcome = ScenarioOutcome.PartiallyApplied };
        if (!preview) RecordOutcome(action, decision, before, after);
        var result = new ScenarioExecutionResult(action, decision.Outcome, decision.Reason, before, after,
            decision.Production, preview ? Array.Empty<ScenarioEvent>() : _events.Skip(eventStart), _draws.Skip(drawStart));
        if (!preview) _results.Add(result);
        return result;
    }

    private Decision Dispatch(ScenarioAction action, bool preview)
    {
        if (!_scenario.Characters.TryGetValue(action.ActorId, out var actor))
            return new(ScenarioOutcome.Rejected, "UnknownActor");
        var activations = _scenario.Activations;
        if (action.Kind == ScenarioActionKind.Movement)
        {
            if (action.Tiles is null) return new(ScenarioOutcome.Rejected, "ExplicitMovementPathRequired");
            var movementRequest = new MovementActionRequest(action.Id, actor.Id, _scenario.Lifecycle.CurrentRound, action.Tiles);
            var movement = preview ? _scenario.Movement.Preview(movementRequest) : _scenario.Movement.Execute(movementRequest);
            var outcome = movement.Outcome switch
            {
                MovementOutcome.Ready => ScenarioOutcome.Ready,
                MovementOutcome.Completed => ScenarioOutcome.Completed,
                MovementOutcome.Interrupted => ScenarioOutcome.Interrupted,
                MovementOutcome.UnresolvedRule => ScenarioOutcome.Unresolved,
                MovementOutcome.PartiallyApplied => ScenarioOutcome.PartiallyApplied,
                MovementOutcome.Failed => ScenarioOutcome.UnexpectedFailure,
                _ => ScenarioOutcome.Rejected
            };
            return new(outcome, movement.Reason, new(nameof(MovementActionResult), movement: movement));
        }
        if (action.Kind is ScenarioActionKind.ConfirmedUtility or ScenarioActionKind.ConfirmedMovement)
        {
            // Bookkeeping only, for an external coordinator's already-confirmed legal action. No movement/effect is invented.
            if (action.ConfirmedLegal is null) return new(ScenarioOutcome.Unresolved, "ConfirmedActionLegalityRequired");
            if (action.ConfirmedLegal == false) return new(ScenarioOutcome.Rejected, "ActionNotConfirmedLegal");
            if (_scenario.Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations) return new(ScenarioOutcome.Rejected, "WrongPhase");
            if (!ReferenceEquals(activations.ActiveCharacter, actor)) return new(ScenarioOutcome.Rejected, "CharacterNotActive");
            bool utility = action.Kind == ScenarioActionKind.ConfirmedUtility;
            if (utility)
            {
                var permission = activations.EvaluateUtilityAction(actor);
                if (!permission.IsAllowed) return TauntDecision(permission);
            }
            if (utility ? !activations.CanUseUtilityAction : !activations.CanMove) return new(ScenarioOutcome.Rejected, "ActionUnavailable");
            if (!preview) { if (utility) activations.RecordUtilityAction(); else activations.RecordMovement(); }
            return new(preview ? ScenarioOutcome.Ready : ScenarioOutcome.Completed,
                Production: new(utility ? "ConfirmedUtilityBookkeeping" : "ConfirmedMovementBookkeeping"));
        }
        if (action.Kind == ScenarioActionKind.StartActivation)
        {
            if (activations.ActiveCharacter is not null || !actor.CanAct || activations.NextTeamId != actor.TeamId ||
                _scenario.Lifecycle.CurrentPhase != RoundPhase.AlternatingActivations)
                return new(ScenarioOutcome.Rejected, "ActivationUnavailable");
            if (!preview) activations.StartActivation(actor);
            return new(preview ? ScenarioOutcome.Ready : ScenarioOutcome.Completed);
        }
        if (action.Kind == ScenarioActionKind.EndActivation)
        {
            if (!ReferenceEquals(activations.ActiveCharacter, actor)) return new(ScenarioOutcome.Rejected, "CharacterNotActive");
            if (activations.ActionRestrictions.GetActive(actor.Id) is not null && !actor.IsDefeated)
            {
                var permission = activations.EvaluateMainAction(actor, new(false, requiredAttack: action.RequiredAttack));
                if (!permission.RequiresEndActivation && !permission.ReleasesTaunt) return TauntDecision(permission);
                if (!preview)
                {
                    if (permission.RequiresEndActivation) activations.EndActivationForTaunt(actor, action.RequiredAttack!);
                    else
                    {
                        // CompleteActivation already releases a definitively dead source. Other explicit invalidation needs its supplied context.
                        if (action.RequiredAttack is not null) activations.ReleaseInvalidTauntSource(actor, action.RequiredAttack);
                        activations.CompleteActivation();
                    }
                }
            }
            else if (!preview) activations.CompleteActivation();
            return new(preview ? ScenarioOutcome.Ready : ScenarioOutcome.Completed);
        }
        if (action.Kind == ScenarioActionKind.Ability && action.AbilityId == "provoking_roar")
        {
            if (_scenario.Map.TargetingConfirmed is null) return new(ScenarioOutcome.Unresolved, "MissingMapTargetLegality");
            var map = _scenario.Characters.Values.ToDictionary(c => c.Id, c => _scenario.Map.TargetLegal(actor, c) == true);
            var executor = new ProvokingRoarAbilityExecutor();
            var use = new AbilityUse(actor, action.AbilityId, activations);
            var result = preview ? executor.Preview(use, Rules(AbilityTargetTeam.Enemy), map, action.Id)
                : executor.Execute(use, Rules(AbilityTargetTeam.Enemy), map, action.Id);
            if (!result.Eligibility.IsAllowed) return Eligibility(result.Eligibility);
            if (!result.CanExecute && !result.Succeeded)
                return new(ScenarioOutcome.Unresolved, result.RejectionReason.ToString());
            return new(preview ? ScenarioOutcome.Ready : ScenarioOutcome.Completed, Production: new(nameof(ProvokingRoarUseResult),
                readyRound: result.Finalization?.ReadyRound, finalized: result.Succeeded, taunted: result.NewlyTauntedRecipientIds));
        }
        if (action.Kind == ScenarioActionKind.Ability && action.AbilityId == "impaling_sentinel")
        {
            var executor = new ImpalingSentinelAbilityExecutor(_scenario.Sentinel);
            var use = new AbilityUse(actor, action.AbilityId, activations);
            var lance = _scenario.AttackInputs.GetValueOrDefault(actor.Id)?.Configuration;
            var result = preview ? executor.Preview(use, action.Id, lance, action.ReactionLimit) : executor.Execute(use, action.Id, lance, action.ReactionLimit);
            if (!result.Eligibility.IsAllowed) return Eligibility(result.Eligibility);
            if (!result.CanExecute && !result.Succeeded) return new(result.RejectionReason is ImpalingSentinelReason.ReactionLimitUnresolved
                or ImpalingSentinelReason.RangeGeometryUnresolved ? ScenarioOutcome.Unresolved : ScenarioOutcome.Rejected, result.RejectionReason.ToString());
            return new(preview ? ScenarioOutcome.Ready : ScenarioOutcome.Completed, Production: new(nameof(ImpalingSentinelUseResult),
                readyRound: result.Finalization?.ReadyRound, finalized: result.Succeeded, preparationId: result.Succeeded ? result.Preparation?.Id : null));
        }
        if (action.Kind == ScenarioActionKind.Ability && action.AbilityId == "wailing_rampart")
        {
            if (action.Tiles is null) return new(ScenarioOutcome.Unresolved, "ExplicitWallTilesRequired");
            var executor = new WailingRampartAbilityExecutor(_scenario.Walls);
            var use = new AbilityUse(actor, action.AbilityId, activations);
            var result = preview ? executor.Preview(use, action.Id, action.Tiles, _scenario.Map) : executor.Execute(use, action.Id, action.Tiles, _scenario.Map);
            if (!result.Eligibility.IsAllowed) return Eligibility(result.Eligibility);
            if (!result.CanExecute && !result.Succeeded)
            {
                var reason = result.Placement?.RejectionReason;
                bool unresolved = reason is null or WailingRampartRejectionReason.MissingCasterPosition or WailingRampartRejectionReason.MissingMapInformation
                    or WailingRampartRejectionReason.OverlapUnresolved or WailingRampartRejectionReason.UnsupportedAbilityMetadata or WailingRampartRejectionReason.PlacementRulesUnresolved;
                return new(unresolved ? ScenarioOutcome.Unresolved : ScenarioOutcome.Rejected, reason?.ToString() ?? "MissingPlacement");
            }
            return new(preview ? ScenarioOutcome.Ready : ScenarioOutcome.Completed, Production: new(nameof(WailingRampartUseResult),
                readyRound: result.Finalization?.ReadyRound, finalized: result.Succeeded, terrainId: result.CreatedWall?.Id));
        }
        if (action.Kind == ScenarioActionKind.Ability && action.AbilityId == "spirit_frost")
        {
            if (action.Direction is null) return new(ScenarioOutcome.Unresolved, "ExplicitIceDirectionRequired");
            var executor = new SpiritFrostAbilityExecutor(_scenario.Ice);
            var use = new AbilityUse(actor, action.AbilityId, activations);
            var result = preview ? executor.Preview(use, action.Id, action.Direction.Value, _scenario.Map) : executor.Execute(use, action.Id, action.Direction.Value, _scenario.Map);
            if (!result.Eligibility.IsAllowed) return Eligibility(result.Eligibility);
            if (!result.CanExecute && !result.Succeeded)
            {
                var reason = result.Placement?.RejectionReason;
                bool unresolved = reason is null or SpiritFrostReason.MissingCasterPosition or SpiritFrostReason.OverlapUnresolved
                    or SpiritFrostReason.MissingMapInformation or SpiritFrostReason.PlacementRulesUnresolved or SpiritFrostReason.MissingParticipantPosition
                    or SpiritFrostReason.TerrainFrostbiteRulesUnresolved or SpiritFrostReason.UnsupportedMetadata;
                return new(unresolved ? ScenarioOutcome.Unresolved : ScenarioOutcome.Rejected, reason?.ToString() ?? "MissingPlacement");
            }
            return new(preview ? ScenarioOutcome.Ready : ScenarioOutcome.Completed, Production: new(nameof(SpiritFrostUseResult),
                readyRound: result.Finalization?.ReadyRound, finalized: result.Succeeded, terrainId: result.CreatedIce?.Id));
        }
        if (action.Kind == ScenarioActionKind.Ability && action.AbilityId != "healing_light" && action.AbilityId != "frostbite_arrow")
        {
            if (action.AbilityId is null) return new(ScenarioOutcome.Rejected, "AbilityIdRequired");
            var eligibility = AbilityEligibility.Evaluate(actor, action.AbilityId, activations);
            return !eligibility.IsAllowed ? Eligibility(eligibility)
                : new(ScenarioOutcome.Unresolved, $"HarnessUnsupported: no executor adapter for {action.AbilityId}");
        }
        if (action.TargetId is null || !_scenario.Characters.TryGetValue(action.TargetId, out var target))
            return new(ScenarioOutcome.Rejected, "MissingOrUnknownTarget");
        if (action.Kind == ScenarioActionKind.Ability && action.AbilityId == "healing_light")
        {
            var executor = new HealingLightAbilityExecutor();
            var use = new AbilityUse(actor, action.AbilityId, activations);
            var result = preview ? executor.Preview(use, target, Rules(AbilityTargetTeam.Ally), _scenario.Map.TargetLegal(actor, target))
                : executor.Execute(use, target, Rules(AbilityTargetTeam.Ally), _scenario.Map.TargetLegal(actor, target));
            if (!result.Eligibility.IsAllowed) return Eligibility(result.Eligibility);
            if (result.Targeting?.IsLegal != true) return Targeting(result.Targeting);
            if (!result.CanExecute && !result.Succeeded) return new(ScenarioOutcome.Unresolved, result.RejectionReason.ToString());
            return new(preview ? ScenarioOutcome.Ready : ScenarioOutcome.Completed, Production: new(nameof(HealingLightUseResult),
                healed: result.Healing?.HealthRestored, readyRound: result.Finalization?.ReadyRound, finalized: result.Succeeded));
        }
        if (actor.TacticalPosition is null || target.TacticalPosition is null)
            return new(ScenarioOutcome.Unresolved, "MissingTacticalPosition");
        if (!_scenario.AttackInputs.TryGetValue(actor.Id, out var opening) || !_scenario.AttackInputs.TryGetValue(target.Id, out var counter))
            return new(ScenarioOutcome.Unresolved, "ExplicitAttackAndCounterConfigurationRequired");
        var attack = CurrentAttack(actor, opening);
        var defense = CurrentAttack(target, counter);
        if ((actor.Statuses.HasStatus(StatusType.Disarm) && attack.Category != WeaponCategory.Unarmed) ||
            (target.Statuses.HasStatus(StatusType.Disarm) && defense.Category != WeaponCategory.Unarmed))
            return new(ScenarioOutcome.Unresolved, "ExplicitUnarmedConfigurationRequired");
        var intent = new AttackIntent(actor, target, action.AbilityId ?? action.Id, activations, true,
            Rules(AbilityTargetTeam.Enemy), () => action.RequiredAttack,
            () => new(AbilityRangeKind.ManhattanDistance, attack.MinimumRange, attack.MaximumRange),
            () => _scenario.Map.TargetLegal(actor, target));
        var request = new CombatExchangeRequest(actor, target, attack.Type, attack.Power, attack.Category, defense.Category,
            attack.BaseAccuracy, defense.Type, defense.Power, defense.BaseAccuracy, defense.MinimumRange, defense.MaximumRange,
            checked((int)actor.TacticalPosition.Value.ManhattanDistanceTo(target.TacticalPosition.Value)), true, false, true);
        if (action.Kind == ScenarioActionKind.Ability)
        {
            if (action.ArrowContext is null) return new(ScenarioOutcome.Unresolved, "ExplicitFrostbiteArrowContextRequired");
            var use = new AbilityUse(actor, action.AbilityId!, activations, intent);
            var result = preview ? _arrows.Preview(use, request, opening.Id, attack, action.ArrowContext)
                : _arrows.Execute(use, request, opening.Id, attack, action.ArrowContext);
            if (!result.Eligibility.IsAllowed) return Eligibility(result.Eligibility);
            if (result.Targeting is { IsLegal: false }) return Targeting(result.Targeting);
            if (!result.CanExecute && !result.Succeeded)
            {
                bool unresolved = result.RejectionReason is FrostbiteArrowReason.DurationUnresolved or FrostbiteArrowReason.TerrainContextRequired
                    or FrostbiteArrowReason.FlyingContextRequired or FrostbiteArrowReason.FlyingEffectivenessUnresolved
                    or FrostbiteArrowReason.TerrainInteractionUnresolved or FrostbiteArrowReason.ExchangeTimingUnresolved
                    or FrostbiteArrowReason.LethalProcRulesUnresolved or FrostbiteArrowReason.ShieldEligibilityUnresolved or FrostbiteArrowReason.SanctuaryUnresolved;
                return new(unresolved ? ScenarioOutcome.Unresolved : ScenarioOutcome.Rejected, result.RejectionReason.ToString());
            }
            var statuses = result.Proc?.StatusApplied == true
                ? new[] { new ScenarioStatusChange(target.Id, StatusType.Frostbite, result.Proc.StatusOutcome.ToString(), result.Proc.DurationRounds) }
                : Array.Empty<ScenarioStatusChange>();
            return new(preview ? ScenarioOutcome.Ready : ScenarioOutcome.Completed, Production: new(nameof(FrostbiteArrowUseResult),
                result.Combat, readyRound: result.Finalization?.ReadyRound, finalized: result.Succeeded, statuses: statuses));
        }
        var ordinary = preview ? _attacks.Preview(intent, request, attack) : _attacks.Execute(intent, request, attack);
        if (!ordinary.Validation.IsAllowed)
        {
            if (ordinary.Targeting is { IsLegal: false }) return Targeting(ordinary.Targeting);
            if (ordinary.Validation.TauntApproval is { } taunt) return TauntDecision(taunt);
            return new(ScenarioOutcome.Rejected, ordinary.Validation.Outcome.ToString());
        }
        return new(preview ? ScenarioOutcome.Ready : ordinary.Completed ? ScenarioOutcome.Completed : ScenarioOutcome.PartiallyApplied,
            Production: new(nameof(AttackActionResult), ordinary.Combat));
    }
    private static AttackConfiguration CurrentAttack(Character character, ScenarioAttackInput input)
        => character.EquippedScroll is { } scroll && input.Configuration.Type == AttackType.Magical
            ? scroll.ToAttackConfiguration(input.Configuration.BaseAccuracy) : input.Configuration;
    private static AbilityTargetingConstraints Rules(AbilityTargetTeam team)
        => new(AbilityTargetType.Character, false, team, true, true, false, false);
    private static Decision Eligibility(AbilityEligibilityResult result)
        => new(result.RejectionReason is AbilityRejectionReason.MetadataUnresolved or AbilityRejectionReason.ActionRestrictionUnresolved
            ? ScenarioOutcome.Unresolved : ScenarioOutcome.Rejected, result.RejectionReason.ToString());
    private static Decision Targeting(AbilityTargetingResult? result)
    {
        var reason = result?.RejectionReason ?? AbilityTargetingRejectionReason.MissingRequiredTargetInformation;
        return new(reason is AbilityTargetingRejectionReason.MissingRequiredTargetInformation or AbilityTargetingRejectionReason.UnresolvedTargetingRule
            or AbilityTargetingRejectionReason.UnresolvedRange or AbilityTargetingRejectionReason.UnresolvedRadiusGeometry
            or AbilityTargetingRejectionReason.UnresolvedLineOfSight or AbilityTargetingRejectionReason.UnresolvedElevation
            ? ScenarioOutcome.Unresolved : ScenarioOutcome.Rejected, reason.ToString());
    }
    private static Decision TauntDecision(TauntActionResult result)
        => new(result.Outcome is TauntActionOutcome.AttackContextRequired or TauntActionOutcome.SourceContextMismatch
            or TauntActionOutcome.SourceApplicabilityUnresolved or TauntActionOutcome.MovementDecisionRequired
            or TauntActionOutcome.AttackLegalityUnresolved or TauntActionOutcome.LifetimeUnresolved
            ? ScenarioOutcome.Unresolved : ScenarioOutcome.Rejected, result.Outcome.ToString());

    private void Emit(string actor, string? target, ScenarioEventType type, int? amount = null, string? detail = null)
        => _events.Add(new(_events.Count + 1, actor, target, type, amount, detail,
            _scenario.Lifecycle.CurrentRound, _scenario.Lifecycle.CurrentPhase));
    private void RecordOutcome(ScenarioAction action, Decision decision, ScenarioSnapshot before, ScenarioSnapshot after)
    {
        if (decision.Production?.Movement is { } movement)
        {
            for (int i = 1; i < movement.TraversedPath.Count; i++)
            {
                var tile = movement.TraversedPath[i];
                Emit(action.ActorId, null, ScenarioEventType.MovementStep, detail: $"{tile.X},{tile.Y}");
                foreach (var reaction in movement.Reactions.Where(r => r.StepIndex == i))
                {
                    if (reaction.Result?.Strike is { } strike)
                        Emit(reaction.SourceId, reaction.TargetId, strike.Hit ? ScenarioEventType.Hit : ScenarioEventType.Miss, strike.Damage);
                    if (reaction.TargetShieldLost > 0) Emit(reaction.SourceId, reaction.TargetId, ScenarioEventType.ShieldDamage, reaction.TargetShieldLost);
                    if (reaction.TargetHPLost > 0) Emit(reaction.SourceId, reaction.TargetId, ScenarioEventType.HPDamage, reaction.TargetHPLost);
                    if (reaction.Result?.Combat?.CounterStrike is { } counter)
                        Emit(reaction.TargetId, reaction.SourceId, counter.Hit ? ScenarioEventType.Hit : ScenarioEventType.Miss, counter.Damage);
                    if (reaction.SourceShieldLost > 0) Emit(reaction.TargetId, reaction.SourceId, ScenarioEventType.ShieldDamage, reaction.SourceShieldLost);
                    if (reaction.SourceHPLost > 0) Emit(reaction.TargetId, reaction.SourceId, ScenarioEventType.HPDamage, reaction.SourceHPLost);
                }
            }
            if (movement.Outcome == MovementOutcome.Interrupted)
                Emit(action.ActorId, null, ScenarioEventType.MovementInterrupted, detail: movement.Reason);
        }
        var combat = decision.Production?.Combat;
        if (combat is not null)
        {
            void Strike(string actor, string target, StrikeResult strike, int healthLost, int? shieldLost = null)
            {
                Emit(actor, target, strike.Hit ? ScenarioEventType.Hit : ScenarioEventType.Miss, strike.Damage);
                if (shieldLost > 0) Emit(actor, target, ScenarioEventType.ShieldDamage, shieldLost);
                if (healthLost > 0) Emit(actor, target, ScenarioEventType.HPDamage, healthLost);
            }
            Strike(action.ActorId, action.TargetId!, combat.OpeningStrike, combat.OpeningHealthLost, combat.OpeningShieldAbsorbed);
            if (combat.CounterStrike is { } counter)
                Strike(action.TargetId!, action.ActorId, counter, combat.AttackerHealthLost -
                    (combat.FollowUpBy == FollowUpParticipant.Defender ? combat.FollowUpHealthLost : 0));
            if (combat.FollowUpStrike is { } follow)
                Strike(combat.FollowUpBy == FollowUpParticipant.Attacker ? action.ActorId : action.TargetId!,
                    combat.FollowUpBy == FollowUpParticipant.Attacker ? action.TargetId! : action.ActorId, follow, combat.FollowUpHealthLost);
        }
        foreach (var (id, previous) in before.Characters)
        {
            var current = after.Characters[id];
            var movementReactions = decision.Production?.Movement?.Reactions ?? Array.Empty<MovementReactionObservation>();
            int movementShield = movementReactions.Sum(r => (r.TargetId == id ? r.TargetShieldLost : 0) + (r.SourceId == id ? r.SourceShieldLost : 0));
            int movementHP = movementReactions.Sum(r => (r.TargetId == id ? r.TargetHPLost : 0) + (r.SourceId == id ? r.SourceHPLost : 0));
            int observedShield = previous.ShieldHP - current.ShieldHP - movementShield - (combat is not null && id == action.TargetId ? combat.OpeningShieldAbsorbed : 0);
            // Only unattributed changes become observations, so batch metrics do not count reaction damage twice.
            if (observedShield > 0) Emit(action.ActorId, id, ScenarioEventType.ShieldChangeObserved, observedShield);
            int observedHP = previous.CurrentHealth - current.CurrentHealth - movementHP;
            if (combat is null && observedHP > 0)
                Emit(action.ActorId, id, ScenarioEventType.HealthChangeObserved, observedHP);
            if (previous.CurrentHealth > 0 && current.CurrentHealth == 0) Emit(action.ActorId, id, ScenarioEventType.CharacterDefeated);
        }
        if (decision.Production?.HPHealed is { } healed) Emit(action.ActorId, action.TargetId, ScenarioEventType.Healing, healed);
        foreach (var status in decision.Production?.Statuses ?? Array.Empty<ScenarioStatusChange>())
            Emit(action.ActorId, status.TargetId, ScenarioEventType.StatusApplied, status.Duration, status.Status.ToString());
        foreach (var id in decision.Production?.TauntedIds ?? Array.Empty<string>()) Emit(action.ActorId, id, ScenarioEventType.TauntApplied);
        if (decision.Production?.CreatedTerrainId is { } terrainId) Emit(action.ActorId, null, ScenarioEventType.TerrainCreated, detail: terrainId);
        if (decision.Production?.PreparationId is { } preparationId) Emit(action.ActorId, null, ScenarioEventType.SentinelPrepared, detail: preparationId);
        if (decision.Production?.AbilityFinalized == true)
            Emit(action.ActorId, action.TargetId, ScenarioEventType.AbilityFinalized, decision.Production.ReadyRound, action.AbilityId);
        if (before.ActiveCharacterId is null && after.ActiveCharacterId is not null) Emit(action.ActorId, null, ScenarioEventType.ActivationStarted);
        if (before.ActiveCharacterId is not null && after.ActiveCharacterId is null) Emit(action.ActorId, null, ScenarioEventType.ActivationEnded);
        if (decision.Outcome is ScenarioOutcome.Rejected or ScenarioOutcome.Unresolved or ScenarioOutcome.PartiallyApplied or ScenarioOutcome.UnexpectedFailure)
            Emit(action.ActorId, action.TargetId, decision.Outcome == ScenarioOutcome.Rejected ? ScenarioEventType.ActionRejected
                : decision.Outcome == ScenarioOutcome.Unresolved ? ScenarioEventType.ActionUnresolved : ScenarioEventType.ActionFailed, detail: decision.Reason);
    }
}
