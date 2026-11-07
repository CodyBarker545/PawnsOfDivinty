using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class TimeWeaver
{
    public static CharacterDefinition Definition => new(
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
        )
    );
}