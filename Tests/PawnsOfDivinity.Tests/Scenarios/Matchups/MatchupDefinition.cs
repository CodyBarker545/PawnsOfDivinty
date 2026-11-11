using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Equipment;
using PawnsOfDivinity.Core.Statuses;

namespace PawnsOfDivinity.Tests.Scenarios.Matchups;

public enum MatchupMode { IsolatedCombat, ScriptedGameplay }
public enum MatchupStatus { Completed, Rejected, UnresolvedRule, HarnessUnsupported, PartiallyApplied, UnexpectedFailure }
public enum MatchupBlockerCategory
{ UnresolvedGameplayRule, MissingProductionExecutionApi, MissingHarnessAdapter, UnknownEquipmentData, UnsupportedMapOrMovement, IncompleteObservability }
public sealed record MatchupStatusSetup(StatusType Status, int? Duration = null, int? ParalysisPenalty = null);
public sealed record MatchupCooldown(string AbilityId, int RoundUsed, int ReuseInterval);
public sealed record IsolatedAttack(string Id, string ActorId, string TargetId, bool PermitCounters, bool PermitFollowUps);

/// <summary>Immutable fixture inputs. Stats always come from the registered definition.</summary>
public sealed record MatchupParticipant
{
    public string InstanceId { get; }
    public string DefinitionId { get; }
    public int TeamId { get; }
    public GridPosition Position { get; }
    public ScenarioAttackInput? Equipment { get; }
    public ScrollDefinition? Scroll { get; }
    public int? Health { get; }
    public int Shield { get; }
    public int FixtureHealing { get; }
    public IReadOnlyList<MatchupStatusSetup> Statuses { get; }
    public IReadOnlyList<TemporaryStatModifier> Modifiers { get; }
    public IReadOnlyList<MatchupCooldown> Cooldowns { get; }
    public MatchupParticipant(string instanceId, string definitionId, int teamId, GridPosition position,
        ScenarioAttackInput? equipment = null, int? health = null, int shield = 0,
        IEnumerable<MatchupStatusSetup>? statuses = null, IEnumerable<TemporaryStatModifier>? modifiers = null,
        IEnumerable<MatchupCooldown>? cooldowns = null, ScrollDefinition? scroll = null, int fixtureHealing = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        var definition = CharacterRegistry.GetCharacter(definitionId) ?? throw new ArgumentException("Use a registered definition ID.", nameof(definitionId));
        ArgumentOutOfRangeException.ThrowIfNegative(teamId);
        if (health is < 0 || health > definition.BaseStats.MaxHealth) throw new ArgumentOutOfRangeException(nameof(health));
        ArgumentOutOfRangeException.ThrowIfNegative(shield); ArgumentOutOfRangeException.ThrowIfNegative(fixtureHealing);
        if (equipment is not null) { ArgumentException.ThrowIfNullOrWhiteSpace(equipment.Id); ArgumentNullException.ThrowIfNull(equipment.Configuration); }
        InstanceId = instanceId; DefinitionId = definitionId; TeamId = teamId; Position = position;
        Equipment = equipment; Health = health; Shield = shield; Scroll = scroll; FixtureHealing = fixtureHealing;
        Statuses = Array.AsReadOnly((statuses ?? Array.Empty<MatchupStatusSetup>()).ToArray());
        Modifiers = Array.AsReadOnly((modifiers ?? Array.Empty<TemporaryStatModifier>()).ToArray());
        Cooldowns = Array.AsReadOnly((cooldowns ?? Array.Empty<MatchupCooldown>()).ToArray());
        if (Statuses.Any(s => s is null) || Modifiers.Any(m => m is null) || Cooldowns.Any(c => c is null))
            throw new ArgumentException("Fixture collections cannot contain null entries.");
    }
}

public sealed record MatchupDefinition
{
    public const string EquipmentLabel = "TEST FIXTURE VALUES";
    public string Id { get; }
    public MatchupMode Mode { get; }
    public IReadOnlyList<MatchupParticipant> Participants { get; }
    public IReadOnlyList<string> Assumptions { get; }
    public IReadOnlyList<int> Accuracy { get; }
    public IReadOnlyList<int> StatusRolls { get; }
    public IReadOnlyList<int> Selection { get; }
    public IReadOnlyList<ScenarioAction> Actions { get; }
    public IReadOnlyList<ScenarioRoundPlan> Rounds { get; }
    public IReadOnlyList<IsolatedAttack> IsolatedAttacks { get; }
    public int InitialRound { get; }
    public string? ActiveCharacterId { get; }
    public bool? TargetingLegal { get; }
    public MatchupDefinition(string id, MatchupMode mode, IEnumerable<MatchupParticipant> participants,
        IEnumerable<string> assumptions, IEnumerable<int> accuracy, IEnumerable<ScenarioAction>? actions = null,
        IEnumerable<ScenarioRoundPlan>? rounds = null, IEnumerable<IsolatedAttack>? isolatedAttacks = null,
        int initialRound = 1, string? activeCharacterId = null, bool? targetingLegal = null,
        IEnumerable<int>? statusRolls = null, IEnumerable<int>? selection = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(assumptions); ArgumentNullException.ThrowIfNull(accuracy);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialRound);
        Id = id; Mode = mode; Participants = Array.AsReadOnly(participants.ToArray());
        if (Participants.Any(p => p is null) || Participants.Select(p => p.InstanceId).Distinct(StringComparer.Ordinal).Count() != Participants.Count)
            throw new ArgumentException("Participant identities must be non-null and unique across teams.");
        if (Participants.Select(p => p.TeamId).Distinct().Count() != 2) throw new ArgumentException("Exactly two teams are required.");
        Assumptions = Array.AsReadOnly(assumptions.ToArray());
        if (Assumptions.Count == 0 || Assumptions.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Document the testing assumptions.");
        Accuracy = Array.AsReadOnly(accuracy.ToArray()); StatusRolls = Array.AsReadOnly(statusRolls?.ToArray() ?? Array.Empty<int>());
        Selection = Array.AsReadOnly(selection?.ToArray() ?? Array.Empty<int>());
        Actions = Array.AsReadOnly(actions?.ToArray() ?? Array.Empty<ScenarioAction>());
        Rounds = Array.AsReadOnly(rounds?.ToArray() ?? Array.Empty<ScenarioRoundPlan>());
        IsolatedAttacks = Array.AsReadOnly(isolatedAttacks?.ToArray() ?? Array.Empty<IsolatedAttack>());
        if (Actions.Any(a => a is null) || Rounds.Any(r => r is null) || IsolatedAttacks.Any(a => a is null)) throw new ArgumentException("Scripts cannot contain null.");
        if (mode == MatchupMode.IsolatedCombat && (Actions.Count != 0 || Rounds.Count != 0 || IsolatedAttacks.Count == 0) ||
            mode == MatchupMode.ScriptedGameplay && (IsolatedAttacks.Count != 0 || (Actions.Count == 0) == (Rounds.Count == 0)))
            throw new ArgumentException("Choose isolated attacks, direct gameplay actions, or round plans, without mixing modes.");
        if (Rounds.Count != 0 && activeCharacterId is not null) throw new ArgumentException("Round preparation selects activations from the plan.");
        if (activeCharacterId is not null && !Participants.Any(p => p.InstanceId == activeCharacterId)) throw new ArgumentException("Unknown active actor.");
        var ids = Actions.Select(a => a.Id).Concat(Rounds.SelectMany(r => r.Activations).SelectMany(a => a.Actions).Select(a => a.Id)).Concat(IsolatedAttacks.Select(a => a.Id)).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new ArgumentException("Action IDs must be unique across the experiment.");
        foreach (var attack in IsolatedAttacks)
            if (string.IsNullOrWhiteSpace(attack.ActorId) || string.IsNullOrWhiteSpace(attack.TargetId)) throw new ArgumentException("Isolated attacks need participant IDs.");
        InitialRound = initialRound; ActiveCharacterId = activeCharacterId; TargetingLegal = targetingLegal;
    }
}
