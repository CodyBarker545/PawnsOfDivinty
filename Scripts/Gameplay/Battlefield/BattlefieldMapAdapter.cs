using System;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Gameplay;

/// <summary>Connects authored map observations and live core positions to movement and attacks.</summary>
public sealed class BattlefieldMapAdapter : IOrdinaryMovementMap
{
    private readonly BattlefieldSession _session;
    private readonly IBattlefieldTerrain _terrain;
    public MovementRuleAuthority RuleAuthority => MovementRuleAuthority.ProvisionalFixture;
    public string PolicyDescription => _session.Rules.Description + " Existing prototype transition thresholds, unit step costs, occupied traversal blocked.";
    public bool? InterruptedMovementConsumesOpportunity => _session.Rules.InterruptedMovementConsumesOpportunity;
    internal BattlefieldMapAdapter(BattlefieldSession session, IBattlefieldTerrain terrain)
    { _session = session; _terrain = terrain; }
    public bool? IsValidPosition(GridPosition cell) => !BattlefieldGrid.Contains(cell) ? false : _terrain.IsWalkable(cell);
    public bool IsOccupied(GridPosition cell) => _session.Characters.Values.Any(c => !c.IsDefeated && c.TacticalPosition == cell);
    public bool? CanTraverseOccupiedTile(Character actor, Character occupant) => _session.Rules.EnablePrototypeMovement ? false : null;
    public int? GetStepCost(Character actor, GridPosition from, GridPosition to)
    {
        var fromHeight = _terrain.Height(from); var toHeight = _terrain.Height(to);
        if (!_session.Rules.EnablePrototypeMovement || !fromHeight.HasValue || !toHeight.HasValue
            || !double.IsFinite(fromHeight.Value) || !double.IsFinite(toHeight.Value)) return null;
        // These thresholds remain the authored visual prototype's explicit development policy.
        return _terrain.PrototypeTransition(from, to) == true ? 1 : null;
    }
    public bool? TargetLegal(Character source, Character target)
    {
        if (source.TacticalPosition is not { } from || target.TacticalPosition is not { } to) return null;
        if (IsValidPosition(from) != true || IsValidPosition(to) != true) return false;
        if (!VisibleTarget(source, target, to)) return false;
        return _session.Rules.EnableOpenTargeting ? true : null;
    }
    public bool? CanReact(Character owner, Character mover, SentinelMovementEvent movement)
    {
        if (owner.TacticalPosition is not { } from) return null;
        if (IsValidPosition(from) != true || IsValidPosition(movement.Destination) != true) return false;
        if (!VisibleTarget(owner, mover, movement.Destination)) return false;
        return _session.Rules.EnableOpenTargeting ? true : null;
    }
    private bool VisibleTarget(Character observer, Character target, GridPosition position)
        => SmokeVisibility.CanTargetThroughVisibility(observer,
            SmokeVisibility.Evaluate(target, position, _session.Smoke,
                _session.Characters.Values.Where(c => c.TacticalPosition.HasValue)
                    .Select(c => (c, c.Id == target.Id ? position : c.TacticalPosition!.Value))), false);
}
