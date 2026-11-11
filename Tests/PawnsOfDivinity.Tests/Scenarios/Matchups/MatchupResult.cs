using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Tests.Scenarios.Matchups;

public sealed record MatchupBlocker(MatchupBlockerCategory Category, string OperationId, string Condition, string ExistingSystem, string NeededWork);
public sealed record MatchupStrike(string OperationId, string ActorId, string TargetId, string Role, AttackType? Type,
    bool Hit, int CalculatedDamage, int HealthLost, int? ShieldAbsorbed, bool TargetSurvived,
    int? WinterHunterBonus, int? BerserkersFuryBonus);
public sealed record MatchupSurvivability(string InstanceId, int StartingHP, int FinalHP, int StartingShields, int FinalShields,
    long ObservedHPLoss, long ObservedShieldLoss, long RecordedHealing, bool Defeated, int? DefeatRound, int? SupportedStrikesSurvived);

/// <summary>An observed operation, retaining immutable production results and snapshots rather than runtime characters.</summary>
public sealed record MatchupObservation(string Id, string? ActorId, string? TargetId, bool IsAction, bool IsAttack,
    MatchupStatus Status, string Reason, ScenarioSnapshot Before, ScenarioSnapshot After,
    CombatExchangeResult? Combat = null, int? Healing = null, bool AbilityFinalized = false);

public sealed record MatchupMetrics(int ActionsAttempted, int CompletedActions, int CompletedMainActions, int AttacksAttempted,
    int CompletedRounds, int? Hits, int? Misses, long? PhysicalCalculatedDamage, long? MagicalCalculatedDamage,
    long ObservedHPDamage, long ObservedShieldLoss, long RecordedHealing, int? Counterattacks, int? FollowUps,
    int CompletedAbilities, bool StrikeDataComplete);

public sealed record MatchupRoundSummary(int Round, ScenarioOutcome Outcome, string Reason);

/// <summary>Compact structured export. Detailed per-operation snapshots remain available on MatchupResult.</summary>
public sealed record MatchupReportData
{
    public MatchupDefinition Definition { get; }
    public MatchupStatus Status { get; }
    public ScenarioSnapshot? Before { get; }
    public ScenarioSnapshot? After { get; }
    public MatchupMetrics? Metrics { get; }
    public IReadOnlyList<MatchupSurvivability> Survivability { get; }
    public IReadOnlyList<MatchupStrike> Strikes { get; }
    public IReadOnlyList<MatchupBlocker> Blockers { get; }
    public IReadOnlyList<ScenarioEvent> Events { get; }
    public IReadOnlyList<ScenarioRandomDraw> RandomDraws { get; }
    public IReadOnlyList<MatchupRoundSummary> Rounds { get; }
    internal MatchupReportData(MatchupResult result)
    {
        Definition = result.Definition; Status = result.Status; Before = result.Before; After = result.After; Metrics = result.Metrics;
        Survivability = result.Survivability; Strikes = result.Strikes; Blockers = result.Blockers; Events = result.Events; RandomDraws = result.RandomDraws;
        Rounds = Array.AsReadOnly(result.Rounds.Select(r => new MatchupRoundSummary(r.Round, r.Outcome, r.Reason)).ToArray());
    }
}

public sealed record MatchupResult
{
    public MatchupDefinition Definition { get; }
    public MatchupStatus Status { get; }
    public ScenarioSnapshot? Before { get; }
    public ScenarioSnapshot? After { get; }
    public IReadOnlyList<ScenarioRoundResult> Rounds { get; }
    public IReadOnlyList<MatchupObservation> Observations { get; }
    public IReadOnlyList<MatchupStrike> Strikes { get; }
    public IReadOnlyList<MatchupSurvivability> Survivability { get; }
    public IReadOnlyList<MatchupBlocker> Blockers { get; }
    public IReadOnlyList<ScenarioEvent> Events { get; }
    public IReadOnlyList<ScenarioRandomDraw> RandomDraws { get; }
    public MatchupMetrics? Metrics { get; }

    internal MatchupResult(MatchupDefinition definition, MatchupStatus status, ScenarioSnapshot? before, ScenarioSnapshot? after,
        IEnumerable<MatchupObservation> observations, IEnumerable<ScenarioRoundResult> rounds,
        IEnumerable<MatchupBlocker> blockers, IEnumerable<ScenarioEvent> events, IEnumerable<ScenarioRandomDraw> draws)
    {
        Definition = definition; Status = status; Before = before; After = after;
        Observations = Array.AsReadOnly(observations.ToArray()); Rounds = Array.AsReadOnly(rounds.ToArray());
        Blockers = Array.AsReadOnly(blockers.ToArray()); Events = Array.AsReadOnly(events.ToArray()); RandomDraws = Array.AsReadOnly(draws.ToArray());
        Strikes = Array.AsReadOnly(Observations.SelectMany(ExtractStrikes).ToArray());
        bool complete = !Observations.Any(o => o.IsAttack && o.Status is MatchupStatus.PartiallyApplied or MatchupStatus.UnexpectedFailure);
        if (before is null || after is null) { Survivability = Array.AsReadOnly(Array.Empty<MatchupSurvivability>()); return; }
        long Loss(string id, bool shield) => Observations.Sum(o => (long)Math.Max(0, shield
            ? o.Before.Characters[id].ShieldHP - o.After.Characters[id].ShieldHP
            : o.Before.Characters[id].CurrentHealth - o.After.Characters[id].CurrentHealth));
        Survivability = Array.AsReadOnly(before.Characters.Select(p => new MatchupSurvivability(p.Key, p.Value.CurrentHealth,
            after.Characters[p.Key].CurrentHealth, p.Value.ShieldHP, after.Characters[p.Key].ShieldHP,
            Loss(p.Key, false), Loss(p.Key, true), Observations.Where(o => o.TargetId == p.Key).Sum(o => (long)(o.Healing ?? 0)),
            after.Characters[p.Key].CurrentHealth == 0,
            Events.FirstOrDefault(e => e.Type == ScenarioEventType.CharacterDefeated && e.TargetId == p.Key)?.Round,
            complete ? Strikes.Count(s => s.TargetId == p.Key && s.TargetSurvived) : null)).ToArray());
        // No returned strike means no calculated-damage measurement, rather than a measured zero-damage strike.
        bool typed = complete && Strikes.Count != 0 && Strikes.All(s => s.Type.HasValue);
        Metrics = new(Observations.Count(o => o.IsAction), Observations.Count(o => o.IsAction && o.Status == MatchupStatus.Completed),
            definition.Mode == MatchupMode.ScriptedGameplay ? Observations.Count(o => o.Status == MatchupStatus.Completed && (o.Combat is not null || o.AbilityFinalized)) : 0,
            Observations.Count(o => o.IsAttack), Rounds.Count(r => r.Outcome == ScenarioOutcome.Completed),
            complete ? Strikes.Count(s => s.Hit) : null, complete ? Strikes.Count(s => !s.Hit) : null,
            typed ? Strikes.Where(s => s.Type == AttackType.Physical).Sum(s => (long)s.CalculatedDamage) : null,
            typed ? Strikes.Where(s => s.Type == AttackType.Magical).Sum(s => (long)s.CalculatedDamage) : null,
            Survivability.Sum(s => s.ObservedHPLoss), Survivability.Sum(s => s.ObservedShieldLoss), Survivability.Sum(s => s.RecordedHealing),
            complete ? Strikes.Count(s => s.Role == "Counter") : null, complete ? Strikes.Count(s => s.Role == "FollowUp") : null,
            Observations.Count(o => o.AbilityFinalized), complete);
    }

    private static IEnumerable<MatchupStrike> ExtractStrikes(MatchupObservation operation)
    {
        if (operation.Combat is not { } combat || operation.ActorId is null || operation.TargetId is null) yield break;
        var health = operation.Before.Characters.ToDictionary(c => c.Key, c => c.Value.CurrentHealth);
        MatchupStrike Read(string actor, string target, string role, StrikeResult strike, int hp, int? shield)
        {
            health[target] -= hp;
            var type = operation.Before.Characters[actor].AttackInput?.Configuration.Type;
            return new(operation.Id, actor, target, role, type, strike.Hit, strike.Damage, hp, shield,
                health[target] > 0, strike.WinterHunter?.PhysicalDamageBonus, null);
        }
        yield return Read(operation.ActorId, operation.TargetId, "Opening", combat.OpeningStrike, combat.OpeningHealthLost, combat.OpeningShieldAbsorbed);
        if (combat.CounterStrike is { } counter)
            yield return Read(operation.TargetId, operation.ActorId, "Counter", counter,
                combat.AttackerHealthLost - (combat.FollowUpBy == FollowUpParticipant.Defender ? combat.FollowUpHealthLost : 0), null);
        if (combat.FollowUpStrike is { } follow)
            yield return Read(combat.FollowUpBy == FollowUpParticipant.Attacker ? operation.ActorId : operation.TargetId,
                combat.FollowUpBy == FollowUpParticipant.Attacker ? operation.TargetId : operation.ActorId,
                "FollowUp", follow, combat.FollowUpHealthLost, null);
    }
}

public static class MatchupReport
{
    public static MatchupReportData ToData(MatchupResult result)
    { ArgumentNullException.ThrowIfNull(result); return new(result); }
    public static string Format(MatchupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var text = new StringBuilder();
        text.AppendLine($"{result.Definition.Id}: {result.Status} ({result.Definition.Mode})");
        text.AppendLine(MatchupDefinition.EquipmentLabel);
        foreach (var p in result.Definition.Participants)
        {
            var a = p.Equipment?.Configuration;
            text.AppendLine($"{p.InstanceId}: {p.DefinitionId}; team {p.TeamId}; position ({p.Position.X},{p.Position.Y}); " +
                (a is null ? "equipment unavailable" : $"{p.Equipment!.Id}, {a.Type}/{a.Category}, Power {a.Power}, accuracy {a.BaseAccuracy}%, range {a.MinimumRange}-{a.MaximumRange}"));
        }
        foreach (var assumption in result.Definition.Assumptions) text.AppendLine($"Assumption: {assumption}");
        if (result.Metrics is { } m)
            text.AppendLine($"Rounds {m.CompletedRounds}; actions {m.CompletedActions}/{m.ActionsAttempted}; Main Actions {m.CompletedMainActions}; " +
                $"hits {Value(m.Hits)}, misses {Value(m.Misses)}; physical {Value(m.PhysicalCalculatedDamage)}, magical {Value(m.MagicalCalculatedDamage)}; " +
                $"HP loss {m.ObservedHPDamage}; shield loss {m.ObservedShieldLoss}; healing {m.RecordedHealing}");
        foreach (var strike in result.Strikes)
            text.AppendLine($"{strike.Role} {strike.ActorId}->{strike.TargetId}: {(strike.Hit ? "hit" : "miss")}; damage {strike.CalculatedDamage}; " +
                $"HP loss {strike.HealthLost}; shield absorption {Value(strike.ShieldAbsorbed)}; " +
                $"Winter Hunter before scaling {Value(strike.WinterHunterBonus)}; Fury contribution {Value(strike.BerserkersFuryBonus)}");
        foreach (var state in result.Survivability)
            text.AppendLine($"{state.InstanceId}: HP {state.StartingHP}->{state.FinalHP}; shields {state.StartingShields}->{state.FinalShields}; defeated {state.Defeated}");
        foreach (var o in result.Observations.Where(o => o.Status != MatchupStatus.Completed)) text.AppendLine($"{o.Id}: {o.Status}: {o.Reason}");
        foreach (var blocker in result.Blockers) text.AppendLine($"Blocker [{blocker.Category}]: {blocker.Condition}");
        text.AppendLine($"Random requests recorded: {result.RandomDraws.Count}");
        text.AppendLine("Controlled fixture evidence; no winner, ranking or competitive balance conclusion.");
        return text.ToString();
    }
    private static string Value<T>(T? value) where T : struct => value?.ToString() ?? "unavailable";
}
