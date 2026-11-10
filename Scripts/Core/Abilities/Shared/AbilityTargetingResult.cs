namespace PawnsOfDivinity.Core.Abilities;

/// <summary>A targeting decision containing value snapshots, never mutable map or character state.</summary>
public sealed record AbilityTargetingResult
{
    public bool IsLegal => RejectionReason == AbilityTargetingRejectionReason.None;
    public AbilityTargetingRejectionReason RejectionReason { get; }
    public GridPosition? SourcePosition { get; }
    public GridPosition? TargetPosition { get; }
    public long? Distance { get; }

    internal AbilityTargetingResult(AbilityTargetingRejectionReason rejectionReason, GridPosition? sourcePosition,
        GridPosition? targetPosition, long? distance)
    {
        RejectionReason = rejectionReason;
        SourcePosition = sourcePosition;
        TargetPosition = targetPosition;
        Distance = distance;
    }
}
