using System;
using System.Linq;
using PawnsOfDivinity.Core.Characters.Definitions;

namespace PawnsOfDivinity.Core.Abilities;

public sealed record SpiritFrostUseResult
{
    public string CasterId { get; }
    public string AbilityId { get; }
    public int RoundNumber { get; }
    public AbilityEligibilityResult Eligibility { get; }
    public SpiritFrostPlacementResult? Placement { get; }
    public SpiritFrostArea? CreatedIce { get; }
    public AbilityUseResult? Finalization { get; }
    public bool Succeeded => Finalization?.Succeeded == true;
    public bool CanExecute => !Succeeded && Eligibility.IsAllowed && Placement?.IsLegal == true;
    internal SpiritFrostUseResult(AbilityUse use, AbilityEligibilityResult eligibility,
        SpiritFrostPlacementResult? placement = null, SpiritFrostArea? ice = null, AbilityUseResult? finalization = null)
    {
        CasterId = use.CharacterId;
        AbilityId = use.AbilityId;
        RoundNumber = use.RoundNumber;
        Eligibility = eligibility;
        Placement = placement;
        CreatedIce = ice;
        Finalization = finalization;
    }
}

/// <summary>Creates the confirmed field only when placement needs no unresolved enemy-status application.</summary>
public sealed class SpiritFrostAbilityExecutor
{
    private readonly SpiritFrostService _ice;
    public SpiritFrostAbilityExecutor(SpiritFrostService ice) => _ice = ice ?? throw new ArgumentNullException(nameof(ice));

    public SpiritFrostUseResult Preview(AbilityUse use, string effectId, GridPosition direction, ISpiritFrostMap? map)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use);
        if (!approval.Succeeded) return new(use, approval.Eligibility);
        if (use.AbilityId != BeothukSpiritGuardian.SpiritFrostId)
            return new(use, new(use.CharacterId, use.AbilityId, AbilityRejectionReason.UnsupportedAbility));
        var ability = AbilityEligibility.FindOwnedAbility(use.Character, use.AbilityId)!;
        if (ability.RequiredAction != AbilityActionType.MainAction || ability.ReuseInterval != 3 ||
            ability.Range?.Kind != AbilityRangeKind.ManhattanDistance || ability.Range.Minimum != 1 || ability.Range.Maximum != 3)
            return new(use, approval.Eligibility, new(_ice, SpiritFrostReason.UnsupportedMetadata));
        var placement = _ice.ValidatePlacement(use.Character, effectId, direction, use.RoundNumber,
            use.Activations.FirstTeam.Concat(use.Activations.SecondTeam), map);
        return new(use, approval.Eligibility, placement);
    }

    /// <remarks>Map queries must be read-only and match operations synchronous; no callback runs after validation.</remarks>
    public SpiritFrostUseResult Execute(AbilityUse use, string effectId, GridPosition direction, ISpiritFrostMap? map)
    {
        var preview = Preview(use, effectId, direction, map);
        if (!preview.CanExecute) return preview;
        var approval = AbilityUseFinalizer.ValidateSuccessfulUse(use, preparingExecution: true);
        if (!approval.Succeeded) return new(use, approval.Eligibility, preview.Placement);
        var area = _ice.PlaceValidated(preview.Placement!);
        return new(use, preview.Eligibility, preview.Placement, area, AbilityUseFinalizer.FinalizeSuccessfulUse(use));
    }
}
