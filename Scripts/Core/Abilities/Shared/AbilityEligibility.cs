using System;
using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Checks manual ability use against authoritative metadata and existing runtime systems.</summary>
public static class AbilityEligibility
{
    public static AbilityEligibilityResult Evaluate(Character character, string abilityId, ActivationManager activations)
        => Evaluate(character, abilityId, activations, completingResolvedMainAction: false);

    internal static AbilityEligibilityResult Evaluate(Character character, string abilityId, ActivationManager activations,
        bool completingResolvedMainAction)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(activations);
        ArgumentException.ThrowIfNullOrWhiteSpace(abilityId);

        if (!activations.IsAbilityPhase) return Result(AbilityRejectionReason.WrongPhase);
        if (character.IsDefeated && !completingResolvedMainAction) return Result(AbilityRejectionReason.CharacterDefeated);
        if (!ReferenceEquals(activations.ActiveCharacter, character)) return Result(AbilityRejectionReason.CharacterNotActive);
        var ability = FindOwnedAbility(character, abilityId);
        if (ability is null) return Result(AbilityRejectionReason.AbilityNotOwned);
        if (!ability.Classification.HasValue) return Result(AbilityRejectionReason.MetadataUnresolved);
        if (ability.Classification != AbilityClassification.Active) return Result(AbilityRejectionReason.NotManuallyActivatable);
        if (!ability.RequiredAction.HasValue || !ability.ReuseInterval.HasValue)
            return Result(AbilityRejectionReason.MetadataUnresolved);
        // No free manually activated ability is explicitly confirmed. Passive no-extra-action
        // wording must not turn None into a free active action.
        if (ability.RequiredAction == AbilityActionType.None) return Result(AbilityRejectionReason.UnsupportedActionRequirement);
        bool actionAvailable = ability.RequiredAction == AbilityActionType.MainAction
            ? (completingResolvedMainAction ? activations.CanCompleteResolvedMainAction : activations.CanUseMainAction)
            : activations.CanUseUtilityAction;
        if (!actionAvailable) return Result(AbilityRejectionReason.ActionUnavailable);
        if (!character.Cooldowns.IsReady(abilityId, activations.CurrentRound)) return Result(AbilityRejectionReason.OnCooldown);
        if (!ability.IsSpellTagged.HasValue)
        {
            // Without Silence, either spell classification passes this restriction. Do not
            // replace unknown metadata with false; reject when the distinction matters.
            if (!SilenceEligibility.CanUseAbility(character, true)) return Result(AbilityRejectionReason.MetadataUnresolved);
        }
        else if (!SilenceEligibility.CanUseAbility(character, ability.IsSpellTagged.Value))
            return Result(AbilityRejectionReason.BlockedBySilence);
        return Result(AbilityRejectionReason.None);

        AbilityEligibilityResult Result(AbilityRejectionReason reason) => new(character.Id, abilityId, reason);
    }

    internal static AbilityDefinition? FindOwnedAbility(Character character, string abilityId)
        => character.Definition?.Abilities.FirstOrDefault(ability => StringComparer.Ordinal.Equals(ability.Id, abilityId));
}
