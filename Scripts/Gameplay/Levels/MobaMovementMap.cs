using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Scene-derived flat beta observations plus the existing explicit prototype movement policy.</summary>
public sealed class MobaMovementMap : IOrdinaryMovementMap
{
    private readonly MobaBetaLayout _layout;
    private readonly IReadOnlyDictionary<string,Character> _characters;
    private readonly WailingRampartService _walls;
    private readonly SpiritFrostService _ice;
    private static readonly HashSet<string> FlatVisualTags=new(){"jungle","lane","flank","stone","camp","objective","capture","deployment"};
    public MovementRuleAuthority RuleAuthority=>MovementRuleAuthority.ProvisionalFixture;
    public string PolicyDescription=>"BETA DEVELOPMENT POLICY: authored flat cells, cost 1 per orthogonal step; living occupied cells block both teams. Unknown terrain/elevation/ice interactions remain unresolved, not canonical balance.";
    public bool? InterruptedMovementConsumesOpportunity=>null;
    public MobaMovementMap(MobaBetaLayout layout,IReadOnlyDictionary<string,Character> characters,
        WailingRampartService walls,SpiritFrostService ice)
    {_layout=layout;_characters=characters;_walls=walls;_ice=ice;}
    public bool? IsValidPosition(GridPosition cell)=>_layout.Walkable(cell);
    public bool IsOccupied(GridPosition cell)=>_characters.Values.Any(c=>!c.IsDefeated&&c.TacticalPosition==cell);
    public bool? CanTraverseOccupiedTile(Character actor,Character occupant)=>false;
    public bool HasKnownFlatCost(GridPosition cell)=>_layout.Terrain.TryGetValue(cell,out var tag)&&FlatVisualTags.Contains(tag);
    public int? GetStepCost(Character actor,GridPosition from,GridPosition to)
    {
        if(!_layout.Walkable(from)||!_layout.Walkable(to)||from.ManhattanDistanceTo(to)!=1)return null;
        if(!HasKnownFlatCost(from)||!HasKnownFlatCost(to))return null;
        if(_walls.CanEnter(actor,to)!=true||_ice.EvaluateMovement(actor,to).Outcome==SpiritFrostMovementOutcome.TerrainFrostbiteRulesUnresolved)return null;
        return 1;
    }
    // No attack targeting, line-of-sight or reaction rule is invented for this movement-only stage.
    public bool? CanReact(Character owner,Character mover,SentinelMovementEvent movement)=>null;
}
