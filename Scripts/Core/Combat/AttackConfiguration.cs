using System;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Caller-supplied combat inputs, without an equipment item or inventory.</summary>
public sealed record AttackConfiguration
{
    public AttackType Type { get; }
    public WeaponCategory Category { get; }
    public int Power { get; }
    public int BaseAccuracy { get; }
    public int MinimumRange { get; }
    public int MaximumRange { get; }

    public AttackConfiguration(AttackType type, WeaponCategory category, int power, int baseAccuracy,
        int minimumRange, int maximumRange)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        ArgumentOutOfRangeException.ThrowIfNegative(power);
        if (baseAccuracy < 0 || baseAccuracy > 100) throw new ArgumentOutOfRangeException(nameof(baseAccuracy));
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRange);
        if (maximumRange < minimumRange) throw new ArgumentOutOfRangeException(nameof(maximumRange));
        if (category == WeaponCategory.Unarmed &&
            (type != AttackType.Physical || minimumRange != 1 || maximumRange != 1))
            throw new ArgumentException("An unarmed attack must be physical with range exactly 1.");
        if (type == AttackType.Magical && category != WeaponCategory.MagicScroll)
            throw new ArgumentException("Magical attacks require MagicScroll.");

        Type = type;
        Category = category;
        Power = power;
        BaseAccuracy = baseAccuracy;
        MinimumRange = minimumRange;
        MaximumRange = maximumRange;
    }

    /// <summary>Creates range-1 physical input. Power and accuracy are explicit, unresolved balance inputs.</summary>
    public static AttackConfiguration CreateUnarmed(int power, int baseAccuracy)
        => new(AttackType.Physical, WeaponCategory.Unarmed, power, baseAccuracy, 1, 1);
}
