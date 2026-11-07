using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Statuses;

namespace PawnsOfDivinity.Core.Combat;

/// <summary>Selects attack inputs permitted by Disarm, without applying damage or consuming actions.</summary>
public static class DisarmEligibility
{
    public static bool CanUseEquippedWeapon(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return !character.Statuses.HasStatus(StatusType.Disarm);
    }

    /// <summary>Preserves the supplied attack unless Disarm requires an explicitly configured unarmed fallback.</summary>
    /// <remarks>A true weapon-access check is not a full attack eligibility check.</remarks>
    public static AttackConfiguration GetEffectiveAttack(Character character, AttackConfiguration supplied,
        AttackConfiguration? unarmedFallback = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(supplied);
        if (unarmedFallback is not null && unarmedFallback.Category != WeaponCategory.Unarmed)
            throw new ArgumentException("The fallback must be an unarmed attack.", nameof(unarmedFallback));
        if (CanUseEquippedWeapon(character) || supplied.Category == WeaponCategory.Unarmed) return supplied;
        return unarmedFallback
            ?? throw new InvalidOperationException("Disarm requires explicit unarmed Power and accuracy before resolving combat.");
    }
}
