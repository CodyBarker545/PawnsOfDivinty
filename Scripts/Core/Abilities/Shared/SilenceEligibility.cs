using System;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Statuses;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Checks whether Silence blocks an ability's explicit spell classification.</summary>
public static class SilenceEligibility
{
    /// <summary>Returns false only when a spell-tagged ability is attempted while Silenced.</summary>
    /// <remarks>
    /// This is only the Silence check. A true result does not establish range, cooldown,
    /// targeting, resources or action availability. The ability layer supplies the spell tag;
    /// damage type and character stats do not determine it here. No rolls or state changes occur.
    /// </remarks>
    public static bool CanUseAbility(Character character, bool isSpellTagged)
    {
        ArgumentNullException.ThrowIfNull(character);
        return !isSpellTagged || !character.Statuses.HasStatus(StatusType.Silence);
    }
}
