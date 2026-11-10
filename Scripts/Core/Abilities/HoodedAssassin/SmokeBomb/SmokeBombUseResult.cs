using System;
using System.Collections.Generic;
using PawnsOfDivinity.Core.Terrain;

namespace PawnsOfDivinity.Core.Abilities;

public enum SmokeBombRejectionReason
{
    None,
    NotAssassin,
    MissingPosition,
    UnsupportedActionMetadata,
    TeleportRequired
}

/// <summary>Immutable preview or cast outcome. Only CreatedSmoke represents an actual placement.</summary>
public sealed record SmokeBombUseResult
{
    public string CasterId { get; }
    public string AbilityId { get; }
    public int RoundUsed { get; }
    public GridPosition? Center { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public SmokeBombRejectionReason RejectionReason { get; }
    public AbilityTargetingResult? Targeting { get; }
    public SmokePlacementValidationResult? Placement { get; }
    public SmokeArea? CreatedSmoke { get; }
    public string? CreatedEffectId => CreatedSmoke?.Id;
    public IReadOnlyList<GridPosition> CoveredTiles => CreatedSmoke?.Tiles ?? Placement?.ProposedArea?.Tiles ?? Array.Empty<GridPosition>();
    public AbilityUseResult? Finalization { get; }
    public SmokeTeleportValidationResult? TeleportValidation { get; }
    public SmokeTeleportResult? Teleport { get; }
    public GridPosition? FinalPosition { get; }
    internal SmokeVisibilityResult? PreparedVisibility { get; }
    public bool CanExecute => Eligibility.IsAllowed && RejectionReason == SmokeBombRejectionReason.None
        && Targeting?.IsLegal == true && Placement?.IsLegal == true && TeleportValidation?.IsLegal == true;
    public bool Succeeded => CreatedSmoke is not null && Teleport?.Succeeded == true && Finalization?.Succeeded == true;

    internal SmokeBombUseResult(AbilityUse use, AbilityEligibilityResult eligibility,
        SmokeBombRejectionReason reason = SmokeBombRejectionReason.None, AbilityTargetingResult? targeting = null,
        SmokePlacementValidationResult? placement = null, SmokeArea? createdSmoke = null, AbilityUseResult? finalization = null,
        SmokeTeleportValidationResult? teleportValidation = null, SmokeTeleportResult? teleport = null,
        SmokeVisibilityResult? preparedVisibility = null)
    {
        CasterId = use.CharacterId;
        AbilityId = use.AbilityId;
        RoundUsed = use.RoundNumber;
        Center = targeting?.SourcePosition ?? use.Character.TacticalPosition;
        FinalPosition = use.Character.TacticalPosition;
        Eligibility = eligibility;
        RejectionReason = reason;
        Targeting = targeting;
        Placement = placement;
        CreatedSmoke = createdSmoke;
        Finalization = finalization;
        TeleportValidation = teleportValidation;
        Teleport = teleport;
        PreparedVisibility = preparedVisibility;
    }
}
