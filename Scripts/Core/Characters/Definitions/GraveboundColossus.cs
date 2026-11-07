using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class GraveboundColossus
{
    public static CharacterDefinition Definition => new(
        Id: "gravebound_colossus",

        Name: "Gravebound Colossus",

        Description: "A tank.",

        // Confirmed stats: Pawns_of_Divinity_Specification_v0.2.md, sections 2 and 12.
        BaseStats: new CharacterStats(
            MaxHealth: 64,
            Strength: 15,
            Defense: 22,
            Intelligence: 3,
            Resistance: 9,
            Wisdom: 5,
            Speed: 7,
            Movement: 3
        )
    );
}