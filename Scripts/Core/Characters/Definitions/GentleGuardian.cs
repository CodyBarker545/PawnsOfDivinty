using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class GentleGuardian
{
    public static CharacterDefinition Definition => new(
        Id: "gentle_guardian",

        Name: "Gentle Guardian",

        Description: "A healer.",

        // Confirmed stats: Pawns_of_Divinity_Specification_v0.2.md, sections 2 and 12.
        BaseStats: new CharacterStats(
            MaxHealth: 44,
            Strength: 7,
            Defense: 11,
            Intelligence: 12,
            Resistance: 15,
            Wisdom: 18,
            Speed: 14,
            Movement: 4
        )
    );
}