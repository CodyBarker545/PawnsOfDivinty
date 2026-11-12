using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Equipment;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Terrain;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Core.Turns.Restrictions;

namespace PawnsOfDivinity.Tests.Scenarios;

public enum ScenarioActionKind { Attack, Ability, StartActivation, EndActivation, ConfirmedUtility, ConfirmedMovement, Movement }
public enum ScenarioOutcome { Ready, Completed, Rejected, Unresolved, PartiallyApplied, UnexpectedFailure, Interrupted }
public enum ScenarioEventType
{
    ActionRequested, AttackStarted, Hit, Miss, ShieldDamage, HPDamage, CharacterDefeated,
    StatusApplied, TauntApplied, Healing, AbilityFinalized, ActivationStarted, ActivationEnded,
    ShieldChangeObserved, HealthChangeObserved, ActionRejected, ActionUnresolved, ActionFailed,
    RoundStarted, PhaseAdvanced, PhaseNoOp, PoisonResolved, DelayedAttackResolved, DelayedAttackCancelled,
    StatusExpired, ModifierExpired, TerrainExpired, TauntExpired, SentinelRetired, RoundEnded, TerrainCreated, SentinelPrepared, MovementStep, MovementInterrupted
}

/// <summary>A test request, not a second gameplay command engine. Production still grants or rejects permission.</summary>
public sealed record ScenarioAction
{
    public string Id { get; }
    public ScenarioActionKind Kind { get; }
    public string ActorId { get; }
    public string? TargetId { get; }
    public string? AbilityId { get; }
    public TauntAttackAvailability? RequiredAttack { get; }
    public FrostbiteArrowContext? ArrowContext { get; }
    public IReadOnlyList<GridPosition>? Tiles { get; }
    public GridPosition? Direction { get; }
    public SentinelReactionLimit? ReactionLimit { get; }
    public bool? ConfirmedLegal { get; }
    public ScenarioAction(string id, ScenarioActionKind kind, string actorId, string? targetId = null,
        string? abilityId = null, TauntAttackAvailability? requiredAttack = null, FrostbiteArrowContext? arrowContext = null,
        IEnumerable<GridPosition>? tiles = null, GridPosition? direction = null, SentinelReactionLimit? reactionLimit = null,
        bool? confirmedLegal = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (targetId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        if (abilityId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(abilityId);
        Id = id; Kind = kind; ActorId = actorId; TargetId = targetId; AbilityId = abilityId;
        RequiredAttack = requiredAttack; ArrowContext = arrowContext;
        Tiles = tiles is null ? null : Array.AsReadOnly(tiles.ToArray()); Direction = direction;
        ReactionLimit = reactionLimit; ConfirmedLegal = confirmedLegal;
    }
}

/// <summary>Explicit fixture combat inputs, not a fabricated production weapon item or inventory entry.</summary>
public sealed record ScenarioAttackInput(string Id, AttackConfiguration Configuration);
public sealed record ScenarioEvent(long Sequence, string ActorId, string? TargetId, ScenarioEventType Type, int? Amount = null,
    string? Detail = null, int? Round = null, RoundPhase? Phase = null);
public enum ScenarioDrawOutcome { Valid, Invalid, Exhausted }
public sealed record ScenarioRandomDraw(long Sequence, string Stream, int? Value, int ExclusiveMaximum, ScenarioDrawOutcome Outcome);
public sealed record ScenarioStatusChange(string TargetId, StatusType Status, string Outcome, int? Duration);

public sealed record ScenarioCharacterSnapshot
{
    public string Id { get; }
    public string? DefinitionId { get; }
    public int TeamId { get; }
    public GridPosition? Position { get; }
    public CharacterStats BaseStats { get; }
    public int CurrentHealth { get; }
    public int MaxHealth => BaseStats.MaxHealth;
    public int ShieldHP { get; }
    public int? EffectiveDefense { get; }
    public int? EffectiveResistance { get; }
    public int? EffectiveSpeed { get; }
    public int EffectiveMovement { get; }
    public IReadOnlyDictionary<StatusType, int> TimedStatuses { get; }
    public bool PoisonActive { get; }
    public int PoisonTicks { get; }
    public ScrollDefinition? EquippedScroll { get; }
    public ScenarioAttackInput? AttackInput { get; }
    public IReadOnlyDictionary<string, AbilityCooldown> Cooldowns { get; }
    public IReadOnlyList<TemporaryStatModifier> Modifiers { get; }
    public bool HasActed { get; }
    public bool CanAct { get; }
    public int HealsReceived { get; }
    public IReadOnlyList<string> UnresolvedFields { get; }

    internal ScenarioCharacterSnapshot(Character character, ScenarioAttackInput? attack)
    {
        Id = character.Id; DefinitionId = character.Definition?.Id; TeamId = character.TeamId;
        Position = character.TacticalPosition; BaseStats = character.BaseStats;
        CurrentHealth = character.CurrentHealth; ShieldHP = character.ShieldHP;
        var issues = new List<string>();
        int? Read(string name, Func<int> getter)
        {
            try { return getter(); }
            catch (Exception error) when (error is InvalidOperationException or OverflowException)
            { issues.Add($"{name}: {error.Message}"); return null; }
        }
        // This is attack-independent Defense. Sanctuary's conditional Defense is evaluated by combat.
        EffectiveDefense = Read(nameof(EffectiveDefense), () => character.EffectiveDefense);
        EffectiveResistance = Read(nameof(EffectiveResistance), () => character.EffectiveResistance);
        EffectiveSpeed = Read(nameof(EffectiveSpeed), () => character.EffectiveSpeed);
        EffectiveMovement = character.EffectiveMovement;
        TimedStatuses = new ReadOnlyDictionary<StatusType, int>(character.Statuses.TimedStatuses.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value));
        PoisonActive = character.Statuses.IsPoisoned; PoisonTicks = character.Statuses.PoisonTicksOccurred;
        EquippedScroll = character.EquippedScroll; AttackInput = attack;
        Cooldowns = new ReadOnlyDictionary<string, AbilityCooldown>(character.Cooldowns.Cooldowns.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value));
        Modifiers = Array.AsReadOnly(character.TemporaryModifiers.Active.ToArray());
        HasActed = character.HasActedThisRound; CanAct = character.CanAct; HealsReceived = character.HealsReceivedThisRound;
        UnresolvedFields = Array.AsReadOnly(issues.ToArray());
    }
}

public sealed record ScenarioSnapshot
{
    public int Round { get; }
    public RoundPhase Phase { get; }
    public string? ActiveCharacterId { get; }
    public bool MovementUsed { get; }
    public bool UtilityUsed { get; }
    public bool MainActionUsed { get; }
    public IReadOnlyDictionary<string, ScenarioCharacterSnapshot> Characters { get; }
    public IReadOnlyList<TauntRestriction> ActiveTaunts { get; }
    public IReadOnlyList<WailingRampartArea> Walls { get; }
    public IReadOnlyList<SpiritFrostArea> Ice { get; }
    public IReadOnlyList<SmokeArea> Smoke { get; }
    public IReadOnlyList<DelayedAttack> DelayedAttacks { get; }
    public IReadOnlyList<ImpalingSentinelPreparation> SentinelPreparations { get; }
    internal ScenarioSnapshot(CombatScenario scenario)
    {
        Round = scenario.Lifecycle.CurrentRound; Phase = scenario.Lifecycle.CurrentPhase;
        ActiveCharacterId = scenario.Activations.ActiveCharacter?.Id;
        MovementUsed = scenario.Activations.MovementUsed; UtilityUsed = scenario.Activations.UtilityActionUsed;
        MainActionUsed = scenario.Activations.MainActionUsed;
        Characters = new ReadOnlyDictionary<string, ScenarioCharacterSnapshot>(scenario.Characters.Values.ToDictionary(c => c.Id,
            c => new ScenarioCharacterSnapshot(c, scenario.AttackInputs.GetValueOrDefault(c.Id)), StringComparer.Ordinal));
        ActiveTaunts = Array.AsReadOnly(scenario.Characters.Keys.Select(id => scenario.Activations.ActionRestrictions.GetActive(id))
            .OfType<TauntRestriction>().ToArray());
        Walls = Array.AsReadOnly(scenario.Walls.ActiveAreas.ToArray());
        Ice = Array.AsReadOnly(scenario.Ice.ActiveAreas.ToArray());
        Smoke = Array.AsReadOnly(scenario.Smoke.ActiveAreas.ToArray());
        DelayedAttacks = Array.AsReadOnly(scenario.DelayedAttacks.PendingAttacks.ToArray());
        SentinelPreparations = Array.AsReadOnly(scenario.Sentinel.Preparations.ToArray());
    }
    public string ToCanonicalJson() => JsonSerializer.Serialize(this);
}

/// <summary>Immutable projection of production outcomes; deliberately excludes use tokens and runtime Character references.</summary>
public sealed record ScenarioProductionOutcome
{
    public string ResultType { get; }
    public MovementActionResult? Movement { get; }
    public CombatExchangeResult? Combat { get; }
    public int? HPHealed { get; }
    public int? ReadyRound { get; }
    public bool AbilityFinalized { get; }
    public IReadOnlyList<ScenarioStatusChange> Statuses { get; }
    public IReadOnlyList<string> TauntedIds { get; }
    public string? CreatedTerrainId { get; }
    public string? PreparationId { get; }
    internal ScenarioProductionOutcome(string type, CombatExchangeResult? combat = null, int? healed = null,
        int? readyRound = null, bool finalized = false, IEnumerable<ScenarioStatusChange>? statuses = null, IEnumerable<string>? taunted = null,
        string? terrainId = null, string? preparationId = null, MovementActionResult? movement = null)
    {
        Movement = movement; ResultType = type; Combat = combat; HPHealed = healed; ReadyRound = readyRound; AbilityFinalized = finalized;
        Statuses = Array.AsReadOnly(statuses?.ToArray() ?? Array.Empty<ScenarioStatusChange>());
        TauntedIds = Array.AsReadOnly(taunted?.ToArray() ?? Array.Empty<string>());
        CreatedTerrainId = terrainId; PreparationId = preparationId;
    }
}

public sealed record ScenarioExecutionResult
{
    public ScenarioAction Action { get; }
    public ScenarioOutcome Outcome { get; }
    public string Reason { get; }
    public ScenarioSnapshot Before { get; }
    public ScenarioSnapshot After { get; }
    public ScenarioProductionOutcome? Production { get; }
    public IReadOnlyList<ScenarioEvent> Events { get; }
    public IReadOnlyList<ScenarioRandomDraw> RandomDraws { get; }
    internal ScenarioExecutionResult(ScenarioAction action, ScenarioOutcome outcome, string reason, ScenarioSnapshot before,
        ScenarioSnapshot after, ScenarioProductionOutcome? production, IEnumerable<ScenarioEvent> events, IEnumerable<ScenarioRandomDraw> draws)
    {
        Action = action; Outcome = outcome; Reason = reason; Before = before; After = after; Production = production;
        Events = Array.AsReadOnly(events.ToArray()); RandomDraws = Array.AsReadOnly(draws.ToArray());
    }
}

public sealed record ScenarioMetrics(long StartingHP, long EndingHP, long HPDamage, long ShieldDamage,
    int Hits, int Misses, int CompletedAttacks, int RejectedActions, int UnresolvedActions, int PartialActions, int UnexpectedFailures);
