using System.Collections.Generic;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>One read-only UI snapshot from the session's authoritative route query, with no distance algorithm.</summary>
public sealed record MobaMovementPreview(string? ActorId,int Round,GridPosition? Origin,int Allowance,
    IReadOnlyDictionary<GridPosition,TacticalRoute> Routes)
{
    public static MobaMovementPreview Capture(MobaHotseatSession session)=>new(session.SelectedFixtureId,session.Round,
        session.SelectedCharacter?.TacticalPosition,session.RemainingMovement,session.ReachableDestinations());
    public MobaMovementPlan? PlanFor(GridPosition destination)=>ActorId is { } actor&&Origin is { } origin
        &&Routes.TryGetValue(destination,out var route)?new(actor,Round,origin,destination,route.Cells,route.Cost):null;
}
