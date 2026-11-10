using System;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Equipment;

/// <summary>Immutable scroll metadata, separate from ownership, inventory and character runtime state.</summary>
public sealed record ScrollDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public int? Level { get; }
    /// <summary>A stable element ID independent of scroll level; null means unresolved or not applicable.</summary>
    public string? ElementId { get; }
    public int? Power { get; }
    public int? MinimumRange { get; }
    public int? MaximumRange { get; }
    public bool HasResolvedCombatProperties => Power.HasValue && MinimumRange.HasValue && MaximumRange.HasValue;

    public ScrollDefinition(string id, string displayName, int? level = null, string? elementId = null,
        int? power = null, int? minimumRange = null, int? maximumRange = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (level.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(level.Value, nameof(level));
        if (elementId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        if (power.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(power.Value, nameof(power));
        if (minimumRange.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(minimumRange.Value, nameof(minimumRange));
        if (maximumRange.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(maximumRange.Value, nameof(maximumRange));
        if (minimumRange.HasValue && maximumRange.HasValue && maximumRange.Value < minimumRange.Value)
            throw new ArgumentOutOfRangeException(nameof(maximumRange), "Maximum range must be at least minimum range.");
        Id = id;
        DisplayName = displayName;
        Level = level;
        ElementId = elementId;
        Power = power;
        MinimumRange = minimumRange;
        MaximumRange = maximumRange;
    }

    /// <summary>Builds combat inputs without attacking. Accuracy is supplied explicitly because no exact baseline is finalized.</summary>
    public AttackConfiguration ToAttackConfiguration(int baseAccuracy)
    {
        if (!HasResolvedCombatProperties)
            throw new InvalidOperationException("Scroll Power and both range bounds must be resolved before building an attack configuration.");
        return new(AttackType.Magical, WeaponCategory.MagicScroll, Power!.Value, baseAccuracy,
            MinimumRange!.Value, MaximumRange!.Value);
    }
}
