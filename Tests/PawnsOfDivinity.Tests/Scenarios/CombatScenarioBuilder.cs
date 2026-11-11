using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Equipment;
using PawnsOfDivinity.Core.Statuses;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Tests.Scenarios;

/// <summary>Reusable setup instructions. Every Build creates fresh production Character instances.</summary>
public sealed class CombatScenarioBuilder
{
    private sealed class Unit
    {
        public required string DefinitionId;
        public required int Team;
        public required GridPosition Position;
        public int? Health;
        public int Shield;
        public bool Activated;
        public ScenarioAttackInput? Attack;
        public ScrollDefinition? Scroll;
        public List<(StatusType Status, int? Duration, int? Penalty)> Statuses = new();
        public List<TemporaryStatModifier> Modifiers = new();
        public List<(string Ability, int Round, int Interval)> Cooldowns = new();
    }
    private readonly Dictionary<string, Unit> _units = new(StringComparer.Ordinal);
    private readonly HashSet<GridPosition> _blocked = new();
    private readonly List<DelayedAttack> _delayed = new();
    private int _width = 14, _height = 14, _round = 1;
    private int? _startingTeam;
    private string? _active;
    private RoundPhase _phase = RoundPhase.AlternatingActivations;
    private bool? _targeting;
    private int[] _accuracy = Array.Empty<int>(), _status = Array.Empty<int>(), _selection = Array.Empty<int>();

    public CombatScenarioBuilder AddCharacter(string id, string definitionId, int team, GridPosition position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (CharacterRegistry.GetCharacter(definitionId) is null) throw new ArgumentException("A registered character ID is required.", nameof(definitionId));
        if (!_units.TryAdd(id, new Unit { DefinitionId = definitionId, Team = team, Position = position }))
            throw new ArgumentException("Instance IDs must be unique, including across teams.", nameof(id));
        return this;
    }
    public CombatScenarioBuilder WithAttack(string id, string inputId, AttackConfiguration attack)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputId);
        ArgumentNullException.ThrowIfNull(attack);
        if (attack.Category == WeaponCategory.Bow && attack.MinimumRange < 2)
            throw new ArgumentException("Standard Bow fixtures cannot authorize adjacent attacks.", nameof(attack));
        Get(id).Attack = new(inputId, attack); return this;
    }
    public CombatScenarioBuilder WithScroll(string id, ScrollDefinition scroll, int? accuracy = null)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        Get(id).Scroll = scroll;
        if (accuracy.HasValue) WithAttack(id, scroll.Id, scroll.ToAttackConfiguration(accuracy.Value));
        return this;
    }
    public CombatScenarioBuilder WithHealth(string id, int health) { Get(id).Health = health; return this; }
    public CombatScenarioBuilder WithShield(string id, int shield) { Get(id).Shield = shield; return this; }
    public CombatScenarioBuilder AlreadyActivated(string id) { Get(id).Activated = true; return this; }
    public CombatScenarioBuilder WithStatus(string id, StatusType status, int? duration = null, int? paralysisPenalty = null)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (status == StatusType.Poison && duration.HasValue) throw new ArgumentException("Poison does not use a timed duration.");
        if (status != StatusType.Poison && (!duration.HasValue || duration <= 0)) throw new ArgumentException("Timed statuses need an explicit positive duration.");
        if (status == StatusType.Paralysis && (!paralysisPenalty.HasValue || paralysisPenalty < 0))
            throw new ArgumentException("Paralysis requires an explicit nonnegative prototype penalty.");
        Get(id).Statuses.Add((status, duration, paralysisPenalty)); return this;
    }
    public CombatScenarioBuilder WithModifier(string id, TemporaryStatModifier modifier)
    { ArgumentNullException.ThrowIfNull(modifier); Get(id).Modifiers.Add(modifier); return this; }
    public CombatScenarioBuilder WithCooldown(string id, string abilityId, int usedRound, int interval)
    { Get(id).Cooldowns.Add((abilityId, usedRound, interval)); return this; }
    public CombatScenarioBuilder AtRound(int round)
    { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round); _round = round; return this; }
    public CombatScenarioBuilder AtPhase(RoundPhase phase)
    { if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase)); _phase = phase; return this; }
    public CombatScenarioBuilder StartingTeam(int team) { _startingTeam = team; return this; }
    public CombatScenarioBuilder ActiveCharacter(string id) { Get(id); _active = id; return this; }
    public CombatScenarioBuilder MapSize(int width, int height)
    { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height); _width = width; _height = height; return this; }
    public CombatScenarioBuilder BlockTile(GridPosition tile) { _blocked.Add(tile); return this; }
    public CombatScenarioBuilder ConfirmTargeting(bool? legal) { _targeting = legal; return this; }
    public CombatScenarioBuilder Accuracy(params int[] values) { _accuracy = values.ToArray(); return this; }
    public CombatScenarioBuilder StatusRolls(params int[] values) { _status = values.ToArray(); return this; }
    public CombatScenarioBuilder Selection(params int[] values) { _selection = values.ToArray(); return this; }
    public CombatScenarioBuilder WithDelayedAttack(DelayedAttack attack)
    { ArgumentNullException.ThrowIfNull(attack); _delayed.Add(attack); return this; }

    public CombatScenario Build()
    {
        var teams = _units.Values.Select(u => u.Team).Distinct().ToArray();
        if (teams.Length != 2) throw new ArgumentException("A combat scenario requires two nonempty teams.");
        bool Valid(GridPosition p) => p.X >= 0 && p.Y >= 0 && p.X < _width && p.Y < _height;
        if (_blocked.Any(p => !Valid(p))) throw new ArgumentException("Blocked tiles must be inside the fixture map.");
        var occupied = new HashSet<GridPosition>();
        var characters = new Dictionary<string, Character>(StringComparer.Ordinal);
        var attacks = new Dictionary<string, ScenarioAttackInput>(StringComparer.Ordinal);
        foreach (var (id, unit) in _units)
        {
            if (!Valid(unit.Position) || _blocked.Contains(unit.Position) || !occupied.Add(unit.Position))
                throw new ArgumentException("Fixture deployment positions must be legal and distinct.");
            var character = CharacterFactory.Create(CharacterRegistry.GetCharacter(unit.DefinitionId)!, id, unit.Team);
            int health = unit.Health ?? character.BaseStats.MaxHealth;
            if (health < 0 || health > character.BaseStats.MaxHealth) throw new ArgumentOutOfRangeException(nameof(unit.Health));
            if (health == 0 && (unit.Shield != 0 || unit.Statuses.Count != 0 || unit.Modifiers.Count != 0))
                throw new ArgumentException("Defeated fixtures cannot start with personal shields, buffs or negative statuses.");
            character.SetTacticalPosition(unit.Position);
            character.TakeDamage(character.BaseStats.MaxHealth - health); // Before shields, so setup cannot absorb its own HP adjustment.
            character.SetShieldHP(unit.Shield);
            foreach (var status in unit.Statuses)
            {
                if (status.Status == StatusType.Poison) character.Statuses.ApplyPoison();
                else character.Statuses.ApplyTimedStatus(status.Status, status.Duration!.Value);
                if (status.Penalty.HasValue) character.ConfigureParalysisSpeedReduction(status.Penalty.Value);
            }
            if (unit.Scroll is not null) character.EquipScroll(unit.Scroll);
            if (unit.Attack is not null)
            {
                if (unit.Scroll is not null && unit.Attack.Configuration.Type == AttackType.Magical &&
                    (!unit.Scroll.HasResolvedCombatProperties || unit.Scroll.ToAttackConfiguration(unit.Attack.Configuration.BaseAccuracy) != unit.Attack.Configuration))
                    throw new ArgumentException("Scroll state conflicts with the fixture attack configuration.");
                attacks.Add(id, unit.Attack);
            }
            foreach (var modifier in unit.Modifiers)
                if (modifier.ApplicationRound > _round || modifier.ExpirationRound < _round)
                    throw new ArgumentException("Initial temporary modifiers must be active in the fixture round.");
            character.TemporaryModifiers.AddRange(unit.Modifiers);
            foreach (var cooldown in unit.Cooldowns)
            {
                if (cooldown.Round > _round || !character.Definition!.Abilities.Any(a => a.Id == cooldown.Ability))
                    throw new ArgumentException("Cooldown history must describe an owned ability used no later than the fixture round.");
                character.Cooldowns.RecordUse(cooldown.Ability, cooldown.Round, cooldown.Interval);
            }
            if (unit.Activated) character.EndActivation();
            characters.Add(id, character);
        }
        var lifecycle = new RoundLifecycleManager(_round);
        // Fixture arrangement only: no Poison, status or round-preparation work is implied by this phase selection.
        while (lifecycle.CurrentPhase != _phase) lifecycle.AdvancePhase();
        int starting = _startingTeam ?? (_active is null ? teams[0] : characters[_active].TeamId);
        var activations = new ActivationManager(characters.Values.Where(c => c.TeamId == teams[0]), characters.Values.Where(c => c.TeamId == teams[1]), starting, lifecycle);
        if (_active is not null)
        {
            if (_phase != RoundPhase.AlternatingActivations) throw new ArgumentException("An active character requires the activation phase.");
            activations.StartActivation(characters[_active]);
        }
        return new(characters, attacks, lifecycle, activations, _width, _height, _blocked, _targeting, _accuracy, _status, _selection, _delayed);
    }
    private Unit Get(string id) => _units.TryGetValue(id, out var unit) ? unit : throw new ArgumentException($"Unknown fixture character '{id}'.", nameof(id));
}
