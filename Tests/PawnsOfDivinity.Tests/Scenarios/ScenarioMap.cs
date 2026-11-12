using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Gameplay;

namespace PawnsOfDivinity.Tests.Scenarios;

/// <summary>Flat-grid fixture information. Unknown visibility, terrain and transitions stay unknown.</summary>
public sealed class ScenarioMap : IForcedMovementMap, IWailingRampartMap, ISpiritFrostMap, ISentinelReactionMap, IOrdinaryMovementMap
{
    private readonly CombatScenario _scenario;
    private readonly HashSet<GridPosition> _blocked;
    private readonly Dictionary<(string Id, GridPosition From, GridPosition To), bool?> _transitions = new();
    public MovementRuleAuthority RuleAuthority => MovementRuleAuthority.ProvisionalFixture;
    public string PolicyDescription => "Explicit flat fixture: unit-cost orthogonal steps; living occupants block traversal. No height rules.";
    public bool UseProvisionalFlatMovement { get; set; }
    public bool? InterruptedMovementConsumesOpportunity { get; set; }
    public bool? CanTraverseOccupiedTile(Character actor, Character occupant) => UseProvisionalFlatMovement ? false : null;
    public int? GetStepCost(Character actor, GridPosition from, GridPosition to) => UseProvisionalFlatMovement ? 1 : null;
    public int Width { get; }
    public int Height { get; }
    public bool? TargetingConfirmed { get; set; }
    public bool? TerrainPlacementConfirmed { get; set; }
    internal ScenarioMap(CombatScenario scenario, int width, int height, IEnumerable<GridPosition> blocked, bool? targeting)
    { _scenario = scenario; Width = width; Height = height; _blocked = new(blocked); TargetingConfirmed = targeting; }
    public bool? IsValidPosition(GridPosition p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height && !_blocked.Contains(p);
    public bool? IsOccupied(GridPosition p) => _scenario.Characters.Values.Any(c => !c.IsDefeated && c.TacticalPosition == p);
    public bool? TargetLegal(Character source, Character target)
    {
        if (source.TacticalPosition is not { } from || target.TacticalPosition is not { } to) return null;
        if (IsValidPosition(from) != true || IsValidPosition(to) != true) return false;
        return TargetingConfirmed;
    }
    public bool? CanPlaceWall(Character caster, GridPosition p)
        => IsValidPosition(p) != true ? false : _scenario.Ice.IsCovered(p) ? null : TerrainPlacementConfirmed;
    public bool? CanPlaceIce(Character caster, GridPosition p)
        => IsValidPosition(p) != true ? false : _scenario.Walls.ActiveAreas.Any(a => a.Tiles.Contains(p)) ? null : TerrainPlacementConfirmed;
    public bool? CanReact(Character owner, Character mover, SentinelMovementEvent movement) => TargetLegal(owner, mover);
    public void ConfirmDisplacement(string characterId, GridPosition from, GridPosition to, bool? legal)
        => _transitions[(characterId, from, to)] = legal;
    public bool? CanDisplace(Character target, GridPosition from, GridPosition to)
        => _transitions.TryGetValue((target.Id, from, to), out var legal) ? legal : null;
}
