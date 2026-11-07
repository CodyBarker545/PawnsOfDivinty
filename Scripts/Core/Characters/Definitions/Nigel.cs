namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class Nigel
{
    public static CharacterDefinition Definition => new(
        Id: "nigel",

        Name: "Nigel",

        Description: "An orc warrior.",

        // Confirmed stats: Pawns_of_Divinity_Specification_v0.2.md, sections 2 and 12.
        BaseStats: new CharacterStats(
            MaxHealth: 52,
            Strength: 20,
            Defense: 11,
            Intelligence: 3,
            Resistance: 8,
            Wisdom: 4,
            Speed: 13,
            Movement: 4
        )
    );
}
