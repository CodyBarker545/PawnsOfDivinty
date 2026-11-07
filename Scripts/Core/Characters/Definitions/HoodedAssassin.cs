using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class HoodedAssassin
{
    public static CharacterDefinition Definition => new(
        Id: "hooded_assassin",
        Name: "Hooded Assassin",

        Description: "A hooded assassin.",

        // Confirmed stats: Pawns_of_Divinity_Specification_v0.2.md, sections 2 and 12.
        BaseStats: new CharacterStats(
            MaxHealth: 38,
            Strength: 17,
            Defense: 8,
            Intelligence: 5,
            Resistance: 9,
            Wisdom: 4,
            Speed: 24,
            Movement: 6
        )
    );
}