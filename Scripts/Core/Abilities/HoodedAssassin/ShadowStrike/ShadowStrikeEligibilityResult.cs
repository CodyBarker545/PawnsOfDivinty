namespace PawnsOfDivinity.Core.Abilities;

public enum ShadowStrikeRejectionReason
{
    None,
    InvalidAttacker,
    AttackerDefeated,
    TargetDefeated,
    TargetNotEnemy,
    MissingPosition,
    OutsideRange,
    RequiresPhysicalAttack,
    MissingVisibilitySnapshot,
    InvalidVisibilitySnapshot
}

/// <summary>A decision about the strike, not permission to move or spend an action.</summary>
public sealed record ShadowStrikeEligibilityResult
{
    public bool IsAllowed => RejectionReason == ShadowStrikeRejectionReason.None;
    public ShadowStrikeRejectionReason RejectionReason { get; }
    public GridPosition? SourcePosition { get; }
    public GridPosition? TargetPosition { get; }
    public SmokeVisibilityResult? OpeningVisibility { get; }
    public bool InvisibleOpening => IsAllowed && OpeningVisibility!.IsInvisible;

    internal ShadowStrikeEligibilityResult(ShadowStrikeRejectionReason reason, GridPosition? source,
        GridPosition? target, SmokeVisibilityResult? visibility)
    {
        RejectionReason = reason;
        SourcePosition = source;
        TargetPosition = target;
        OpeningVisibility = visibility;
    }
}
