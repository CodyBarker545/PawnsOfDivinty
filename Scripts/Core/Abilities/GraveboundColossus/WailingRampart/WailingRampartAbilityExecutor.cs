using System;
using System.Collections.Generic;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Core.Abilities;

public sealed record WailingRampartUseResult
{
    public string CasterId { get; }
    public string AbilityId { get; }
    public int RoundNumber { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public WailingRampartPlacementResult? Placement { get; }
    public WailingRampartArea? CreatedWall { get; }
    public AbilityUseResult? Finalization { get; }
    public bool Succeeded => Finalization?.Succeeded == true;
    public bool CanExecute => !Succeeded && Eligibility.IsAllowed && Placement?.IsLegal == true;

    internal WailingRampartUseResult(AbilityUse use, AbilityEligibilityResult eligibility,
        WailingRampartPlacementResult? placement = null, WailingRampartArea? wall = null, AbilityUseResult? finalization = null)
    {
        CasterId = use.CharacterId;
        AbilityId = use.AbilityId;
        RoundNumber = use.RoundNumber;
        Eligibility = eligibility;
        Placement = placement;
        CreatedWall = wall;
        Finalization = finalization;
    }
}

/// <summary>Places the wall, then completes its Main Action and cooldown through the shared finalizer.</summary>
public sealed class WailingRampartAbilityExecutor
{
    private readonly WailingRampartService _walls;

    public WailingRampartAbilityExecutor(WailingRampartService walls)
    {
        ArgumentNullException.ThrowIfNull(walls);
        _walls = walls;
    }

    public WailingRampartUseResult Preview(AbilityUse use, string effectId, IEnumerable<GridPosition> tiles,
        IWailingRampartMap? map)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        ArgumentNullException.ThrowIfNull(tiles);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(use, approval.Eligibility);
        if (use.AbilityId != GraveboundColossus.WailingRampartId)
            return new(use, new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        var placement = _walls.ValidatePlacement(use.Character, effectId, tiles, use.RoundNumber, map);
        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        if (ability.RequiredAction != AbilityActionType.MainAction || ability.ReuseInterval != 3 ||
            ability.Range?.Kind != AbilityRangeKind.ManhattanDistance || ability.Range.Minimum != 1 || ability.Range.Maximum != 4)
            return new(use, approval.Eligibility, new(_walls, WailingRampartRejectionReason.UnsupportedAbilityMetadata));
        return new(use, approval.Eligibility, placement);
    }

    /// <remarks>
    /// Revalidates current positions, occupancy and terrain. Map queries must be read-only and
    /// match operations synchronous, as with smoke placement. No callbacks run after validation.
    /// </remarks>
    public WailingRampartUseResult Execute(AbilityUse use, string effectId, IEnumerable<GridPosition> tiles,
        IWailingRampartMap? map)
    {
        var preview = Preview(use, effectId, tiles, map);
        if (!preview.CanExecute) return preview;
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, preparingExecution: true);
        if (!approval.Succeeded) return new(use, approval.Eligibility, preview.Placement);
        var wall = _walls.PlaceValidated(preview.Placement!);
        var finalization = AbilityUseFinalizer.FinalizeSuccessfulUse(use);
        return new(use, preview.Eligibility, preview.Placement, wall, finalization);
    }
}
