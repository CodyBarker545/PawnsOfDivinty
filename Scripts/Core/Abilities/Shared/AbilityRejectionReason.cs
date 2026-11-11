namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Ordinary manual-use failures, separate from invalid programming inputs.</summary>
public enum AbilityRejectionReason
{
    None = 0,
    CharacterDefeated = 1,
    CharacterNotActive = 2,
    AbilityNotOwned = 3,
    NotManuallyActivatable = 4,
    ActionUnavailable = 5,
    OnCooldown = 6,
    BlockedBySilence = 7,
    MetadataUnresolved = 8,
    UnsupportedActionRequirement = 9,
    AlreadyFinalized = 10,
    UseContextChanged = 11,
    WrongPhase = 12,
    UnsupportedAbility = 13,
    ActionRestrictionUnresolved = 14,
    ActionRestricted = 15,
    InvalidTarget = 16
}
