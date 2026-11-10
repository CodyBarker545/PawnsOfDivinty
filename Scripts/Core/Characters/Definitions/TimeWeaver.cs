using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class TimeWeaver
{
    public const string TemporalIgnitionId = "temporal_ignition";
    public const string ArcaneTransmutationId = "arcane_transmutation";
    public const string TemporalAccumulationId = "temporal_accumulation";

    public static CharacterDefinition Definition { get; } = new(
        Id: "time_weaver",

        Name: "Time Weaver",

        Description: "A mage.",

        // Confirmed stats: Pawns_of_Divinity_Specification_v0.2.md, sections 2 and 12.
        BaseStats: new CharacterStats(
            MaxHealth: 38,
            Strength: 6,
            Defense: 8,
            Intelligence: 17,
            Resistance: 13,
            Wisdom: 14,
            Speed: 17,
            Movement: 5
        ),
        // Sections 3 and 12.3 confirm Main Actions and reuse intervals. Spell tags
        // remain unknown; Temporal Ignition has no explicitly documented cast range.
        Abilities: new[]
        {
            new AbilityDefinition(TemporalIgnitionId, "Temporal Ignition",
                "Elemental attack with +5 bonus damage. Store hit/miss and damage when cast; resolve next round after Poison. " +
                "Current shields absorb stored damage. Target death cancels it; no counters or follow-ups. Reuse 2.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction, reuseInterval: 2),
            new AbilityDefinition(ArcaneTransmutationId, "Arcane Transmutation",
                "Replace the equipped scroll with a different random scroll of the same level, then attack with its properties. " +
                "Requires an available alternative; normal counters and follow-ups; no cooldown.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                range: new(AbilityRangeKind.EquippedWeapon), reuseInterval: 0),
            new AbilityDefinition(TemporalAccumulationId, "Temporal Accumulation",
                "Each distinct element used earns a persistent Time Stack. Previously earned stacks add magical damage; " +
                "the first use does not retroactively benefit. Stacks persist through death and redeployment.", AbilityClassification.Passive)
        }
    );
}
