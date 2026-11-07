using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class BeothukSpiritGuardian
{
    public static CharacterDefinition Definition => new(
        Id: "beothuk_spirit_guardian",

        Name: "Beothuk Spirit Guardian",

        Description: "A spirit guardian.",

        // Confirmed stats: Pawns_of_Divinity_Specification_v0.2.md, sections 2 and 12.
        BaseStats: new CharacterStats(
            MaxHealth: 40,
            Strength: 16,
            Defense: 9,
            Intelligence: 6,
            Resistance: 12,
            Wisdom: 13,
            Speed: 18,
            Movement: 5
        )
    );
}