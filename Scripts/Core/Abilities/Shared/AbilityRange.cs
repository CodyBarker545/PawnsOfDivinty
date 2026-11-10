using System;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Immutable range metadata, not targeting, area geometry or path legality.</summary>
public sealed record AbilityRange
{
    public AbilityRangeKind Kind { get; }
    public int? Minimum { get; }
    public int? Maximum { get; }

    public AbilityRange(AbilityRangeKind kind, int? minimum = null, int? maximum = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (minimum.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(minimum.Value, nameof(minimum));
        if (maximum.HasValue) ArgumentOutOfRangeException.ThrowIfNegative(maximum.Value, nameof(maximum));
        if (minimum.HasValue && maximum.HasValue && maximum.Value < minimum.Value)
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum range cannot be less than minimum range.");
        if (kind == AbilityRangeKind.EquippedWeapon && (minimum.HasValue || maximum.HasValue))
            throw new ArgumentException("Equipment-dependent range does not store fixed numeric bounds.");
        Kind = kind;
        Minimum = minimum;
        Maximum = maximum;
    }
}
