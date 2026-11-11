using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Tests.Scenarios.Matchups;

public sealed record MatchupPairing(string Id, string FirstDefinitionId, string SecondDefinitionId);

public static class MatchupScenarioFactory
{
    public static IReadOnlyList<MatchupPairing> Pairings()
    {
        var ids = CharacterRegistry.GetAllCharacters().Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var pairs = new List<MatchupPairing>();
        for (int i = 0; i < ids.Length; i++)
            for (int j = i + 1; j < ids.Length; j++) pairs.Add(new($"{ids[i]}__{ids[j]}", ids[i], ids[j]));
        return Array.AsReadOnly(pairs.ToArray());
    }
    /// <summary>Coverage fixtures exercise one requested player attack, not an automatic battle or movement plan.</summary>
    public static IReadOnlyList<MatchupDefinition> DefinePairings(IReadOnlyDictionary<string, ScenarioAttackInput> equipment,
        int distance, IEnumerable<int> accuracy, string assumptions)
    {
        ArgumentNullException.ThrowIfNull(equipment); ArgumentNullException.ThrowIfNull(accuracy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(distance);
        var rolls = accuracy.ToArray();
        return Array.AsReadOnly(Pairings().Select(pair => new MatchupDefinition(pair.Id, MatchupMode.ScriptedGameplay,
            new[] { new MatchupParticipant("first", pair.FirstDefinitionId, 1, new(3, 3), equipment.GetValueOrDefault(pair.FirstDefinitionId)),
                new MatchupParticipant("second", pair.SecondDefinitionId, 2, new(checked(3 + distance), 3), equipment.GetValueOrDefault(pair.SecondDefinitionId)) },
            new[] { assumptions }, rolls, actions: new[] { new ScenarioAction("opening", ScenarioActionKind.Attack, "first", "second") },
            activeCharacterId: "first", targetingLegal: true)).ToArray());
    }
    public static CombatScenario Create(MatchupDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var builder = new CombatScenarioBuilder().AtRound(definition.InitialRound).ConfirmTargeting(definition.TargetingLegal)
            .AtPhase(definition.Rounds.Count == 0 ? RoundPhase.AlternatingActivations : RoundPhase.Shop)
            .Accuracy(definition.Accuracy.ToArray()).StatusRolls(definition.StatusRolls.ToArray()).Selection(definition.Selection.ToArray());
        foreach (var p in definition.Participants)
        {
            builder.AddCharacter(p.InstanceId, p.DefinitionId, p.TeamId, p.Position).WithShield(p.InstanceId, p.Shield);
            if (p.Health.HasValue) builder.WithHealth(p.InstanceId, p.Health.Value);
            if (p.Equipment is { } equipment) builder.WithAttack(p.InstanceId, equipment.Id, equipment.Configuration);
            if (p.Scroll is not null) builder.WithScroll(p.InstanceId, p.Scroll);
            foreach (var s in p.Statuses) builder.WithStatus(p.InstanceId, s.Status, s.Duration, s.ParalysisPenalty);
            foreach (var m in p.Modifiers) builder.WithModifier(p.InstanceId, m);
            foreach (var c in p.Cooldowns) builder.WithCooldown(p.InstanceId, c.AbilityId, c.RoundUsed, c.ReuseInterval);
        }
        if (definition.ActiveCharacterId is not null) builder.ActiveCharacter(definition.ActiveCharacterId);
        var scenario = builder.Build();
        // Explicit setup history for a controlled Fury experiment; this is not a Healing Light action or metric event.
        foreach (var p in definition.Participants.Where(p => p.FixtureHealing != 0)) scenario.Characters[p.InstanceId].Heal(p.FixtureHealing);
        return scenario;
    }
}
