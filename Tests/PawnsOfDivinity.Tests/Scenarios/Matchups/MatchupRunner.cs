using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Tests.Scenarios.Matchups;

/// <summary>Runs fresh fixtures through the existing scenario runners or explicitly labeled low-level combat.</summary>
public sealed class MatchupRunner
{
    public MatchupResult Run(MatchupDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CombatScenario scenario;
        try { scenario = MatchupScenarioFactory.Create(definition); }
        catch (ArgumentException error)
        {
            return new(definition, MatchupStatus.Rejected, null, null, Array.Empty<MatchupObservation>(), Array.Empty<ScenarioRoundResult>(),
                new[] { new MatchupBlocker(MatchupBlockerCategory.UnknownEquipmentData, "construction", error.Message,
                    nameof(CombatScenarioBuilder), "Correct malformed fixture inputs; no gameplay was executed.") },
                Array.Empty<ScenarioEvent>(), Array.Empty<ScenarioRandomDraw>());
        }
        var before = scenario.Capture(); var observations = new List<MatchupObservation>();
        var events = new List<ScenarioEvent>(); var rounds = new List<ScenarioRoundResult>();
        if (definition.Mode == MatchupMode.IsolatedCombat)
        {
            var resolver = new CombatExchangeResolver(new(new HitRoller(scenario.Runner.Accuracy)));
            foreach (var attack in definition.IsolatedAttacks)
            {
                var start = scenario.Capture(); CombatExchangeResult? combat = null;
                var status = MatchupStatus.Completed; string reason = "";
                try
                {
                    if (!scenario.Characters.TryGetValue(attack.ActorId, out var actor) || !scenario.Characters.TryGetValue(attack.TargetId, out var target))
                    { status = MatchupStatus.Rejected; reason = "UnknownParticipant"; }
                    else if (actor.IsDefeated || target.IsDefeated || actor.TeamId == target.TeamId)
                    { status = MatchupStatus.Rejected; reason = "LivingOpponentsRequired"; }
                    else if (!scenario.AttackInputs.TryGetValue(actor.Id, out var opening) || !scenario.AttackInputs.TryGetValue(target.Id, out var counter))
                    { status = MatchupStatus.UnresolvedRule; reason = "ExplicitAttackAndCounterConfigurationRequired"; }
                    else if (actor.Statuses.HasStatus(StatusType.Disarm) && opening.Configuration.Category != WeaponCategory.Unarmed ||
                             target.Statuses.HasStatus(StatusType.Disarm) && counter.Configuration.Category != WeaponCategory.Unarmed)
                    { status = MatchupStatus.UnresolvedRule; reason = "ExplicitUnarmedConfigurationRequired"; }
                    else
                    {
                        var a = opening.Configuration; var d = counter.Configuration;
                        var request = new CombatExchangeRequest(actor, target, a.Type, a.Power, a.Category, d.Category, a.BaseAccuracy,
                            d.Type, d.Power, d.BaseAccuracy, d.MinimumRange, d.MaximumRange,
                            checked((int)actor.TacticalPosition!.Value.ManhattanDistanceTo(target.TacticalPosition!.Value)),
                            attack.PermitCounters, false, attack.PermitFollowUps,
                            Sanctuary: SanctuaryDefenseContext.FromActivations(scenario.Activations));
                        combat = resolver.Resolve(request);
                    }
                }
                catch (SanctuaryDefenseUnresolvedException error) { status = MatchupStatus.UnresolvedRule; reason = error.Result.Reason.ToString(); }
                catch (Exception error) { status = MatchupStatus.UnexpectedFailure; reason = $"{error.GetType().Name}: {error.Message}"; }
                var after = scenario.Capture();
                if (status != MatchupStatus.Completed && start.ToCanonicalJson() != after.ToCanonicalJson()) status = MatchupStatus.PartiallyApplied;
                var observation = new MatchupObservation(attack.Id, attack.ActorId, attack.TargetId, true, true, status, reason, start, after, combat);
                observations.Add(observation);
                // Isolated events report their scope explicitly; no activation, action consumption or ability finalization is implied.
                void Emit(ScenarioEventType type, string actor, string? target, int? amount = null, string? detail = null)
                    => events.Add(new(events.Count + 1, actor, target, type, amount, detail, start.Round, start.Phase));
                Emit(ScenarioEventType.ActionRequested, attack.ActorId, attack.TargetId, detail: "Isolated combat comparison");
                if (combat is not null)
                {
                    void Strike(string actor, string target, StrikeResult strike) => Emit(strike.Hit ? ScenarioEventType.Hit : ScenarioEventType.Miss,
                        actor, target, strike.Damage, "Isolated combat comparison");
                    Strike(attack.ActorId, attack.TargetId, combat.OpeningStrike);
                    if (combat.CounterStrike is { } counter) Strike(attack.TargetId, attack.ActorId, counter);
                    if (combat.FollowUpStrike is { } follow) Strike(combat.FollowUpBy == FollowUpParticipant.Attacker ? attack.ActorId : attack.TargetId,
                        combat.FollowUpBy == FollowUpParticipant.Attacker ? attack.TargetId : attack.ActorId, follow);
                }
                foreach (var (id, old) in start.Characters)
                {
                    var current = after.Characters[id];
                    if (old.ShieldHP > current.ShieldHP) Emit(ScenarioEventType.ShieldChangeObserved, attack.ActorId, id, old.ShieldHP - current.ShieldHP);
                    if (old.CurrentHealth > current.CurrentHealth) Emit(ScenarioEventType.HealthChangeObserved, attack.ActorId, id, old.CurrentHealth - current.CurrentHealth);
                    if (old.CurrentHealth > 0 && current.CurrentHealth == 0) Emit(ScenarioEventType.CharacterDefeated, attack.ActorId, id);
                }
                if (status != MatchupStatus.Completed) Emit(status == MatchupStatus.Rejected ? ScenarioEventType.ActionRejected
                    : status == MatchupStatus.UnresolvedRule ? ScenarioEventType.ActionUnresolved : ScenarioEventType.ActionFailed, attack.ActorId, attack.TargetId, detail: reason);
                if (status is not MatchupStatus.Completed and not MatchupStatus.Rejected) break;
            }
        }
        else if (definition.Rounds.Count != 0)
        {
            var result = new MultiRoundScenarioRunner(scenario).Run(definition.Rounds.ToArray());
            rounds.AddRange(result.Rounds); events.AddRange(result.Events);
            foreach (var round in rounds)
            {
                // The activation-phase snapshot spans actions. Counting it again would duplicate all action damage.
                foreach (var phase in round.Phases.Where(p => p.Phase != RoundPhase.AlternatingActivations))
                    observations.Add(new($"round:{round.Round}:{phase.Phase}", null, null, false, false,
                        Map(phase.Outcome, phase.Reason), phase.Reason, phase.Before, phase.After));
                observations.AddRange(round.Actions.Select(FromAction));
                if (round.Outcome != ScenarioOutcome.Completed && !observations.Any(o => o.Status != MatchupStatus.Completed && o.Reason == round.Reason))
                    observations.Add(new($"round:{round.Round}:interruption", null, null, false, false,
                        Map(round.Outcome, round.Reason), round.Reason, result.After, result.After));
            }
        }
        else
        {
            foreach (var action in definition.Actions)
            {
                var result = scenario.Runner.Execute(action);
                observations.Add(FromAction(result)); events.AddRange(result.Events);
                if (result.Outcome is ScenarioOutcome.Unresolved or ScenarioOutcome.PartiallyApplied or ScenarioOutcome.UnexpectedFailure) break;
            }
        }
        var blockers = observations.Where(o => o.Status is MatchupStatus.UnresolvedRule or MatchupStatus.HarnessUnsupported
            or MatchupStatus.PartiallyApplied or MatchupStatus.UnexpectedFailure).Select(Blocker).ToArray();
        var overall = observations.Select(o => o.Status).OrderByDescending(Priority).FirstOrDefault();
        return new(definition, overall, before, scenario.Capture(), observations, rounds, blockers, events, scenario.Runner.Draws);
    }
    private static MatchupObservation FromAction(ScenarioExecutionResult result)
        => new(result.Action.Id, result.Action.ActorId, result.Action.TargetId, true,
            result.Action.Kind == ScenarioActionKind.Attack || result.Action.AbilityId == "frostbite_arrow",
            Map(result.Outcome, result.Reason), result.Reason, result.Before, result.After,
            result.Production?.Combat, result.Production?.HPHealed, result.Production?.AbilityFinalized ?? false);
    private static MatchupStatus Map(ScenarioOutcome outcome, string reason) => outcome switch
    {
        ScenarioOutcome.Completed => MatchupStatus.Completed,
        ScenarioOutcome.Rejected => MatchupStatus.Rejected,
        ScenarioOutcome.Unresolved when reason.StartsWith("HarnessUnsupported:", StringComparison.Ordinal) => MatchupStatus.HarnessUnsupported,
        ScenarioOutcome.Unresolved => MatchupStatus.UnresolvedRule,
        ScenarioOutcome.PartiallyApplied => MatchupStatus.PartiallyApplied,
        _ => MatchupStatus.UnexpectedFailure
    };
    private static int Priority(MatchupStatus status) => status switch
    { MatchupStatus.PartiallyApplied => 5, MatchupStatus.UnexpectedFailure => 4, MatchupStatus.HarnessUnsupported => 3, MatchupStatus.UnresolvedRule => 2, MatchupStatus.Rejected => 1, _ => 0 };
    private static MatchupBlocker Blocker(MatchupObservation observation)
    {
        var category = observation.Status == MatchupStatus.HarnessUnsupported ? MatchupBlockerCategory.MissingHarnessAdapter
            : observation.Status is MatchupStatus.PartiallyApplied or MatchupStatus.UnexpectedFailure ? MatchupBlockerCategory.IncompleteObservability
            : observation.Reason is "ExplicitAttackAndCounterConfigurationRequired" or "ExplicitUnarmedConfigurationRequired" ? MatchupBlockerCategory.UnknownEquipmentData
            : observation.Reason is "MissingRequiredTargetInformation" or "MissingTacticalPosition" or "UnresolvedLineOfSight" or "UnresolvedElevation"
                ? MatchupBlockerCategory.UnsupportedMapOrMovement : MatchupBlockerCategory.UnresolvedGameplayRule;
        return new(category, observation.Id, observation.Reason,
            observation.Status == MatchupStatus.HarnessUnsupported ? nameof(ScenarioRunner) : "Production action/combat and scenario lifecycle",
            category == MatchupBlockerCategory.MissingHarnessAdapter ? "Add a focused adapter for the existing production executor."
            : category == MatchupBlockerCategory.UnknownEquipmentData ? "Supply explicit fixture inputs; canonical values require design approval."
            : category == MatchupBlockerCategory.IncompleteObservability ? "Inspect the recorded failure and state. Do not retry partial execution."
            : "Resolve the documented rule or supply confirmed context; do not invent gameplay behavior.");
    }
}
