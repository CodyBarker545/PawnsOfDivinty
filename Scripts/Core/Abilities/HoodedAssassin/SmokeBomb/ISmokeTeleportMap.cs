namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Read-only caller-supplied map queries. Null means required information is unavailable.</summary>
/// <remarks>Queries must not mutate the match. Occupancy includes all occupants, not just enemies.</remarks>
public interface ISmokeTeleportMap
{
    bool? IsValidPosition(GridPosition position);
    bool? CanTeleportTo(GridPosition position);
    bool? IsOccupied(GridPosition position);
}
