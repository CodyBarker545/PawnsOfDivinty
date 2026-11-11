using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class BeothukSpiritGuardian
{
    public const string SpiritFrostId = "spirit_frost";
    public const string FrostbiteArrowId = "frostbite_arrow";
    public const string WinterHunterId = "winter_hunter";
    // Category and range metadata only; starting Bow Power remains TBD in specification 4.1.
    public const WeaponCategory StartingWeaponCategory = WeaponCategory.Bow;
    public const int StartingMinimumRange = 2;
    public const int StartingMaximumRange = 3;

    public static CharacterDefinition Definition { get; } = new(
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
        ),
        Abilities: new[]
        {
            new AbilityDefinition(SpiritFrostId, "Spirit Frost",
                "Main Action: create a directly adjacent directional 3-wide by 2-deep ice field. Allies traverse normally. " +
                "Enemies on or entering ice become Frostbitten (-2 MOV); terrain status duration, movement-cost handling " +
                "and interactions with independent Frostbite remain unresolved. Ice survives caster death and expires at " +
                "the end of the following round's Cleanup. Reuse 3. No Bow requirement is documented for ice creation.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                // Reach of affected tiles, not permission to select any tile in this interval.
                // The dedicated placement service enforces the complete directional footprint.
                range: new(AbilityRangeKind.ManhattanDistance, 1, 3), reuseInterval: 3),
            new AbilityDefinition(FrostbiteArrowId, "Frostbite Arrow",
                "Main Action: ordinary STR-based physical Bow attack in equipped range, with normal counters and follow-ups. " +
                "Successful eligible hits have a 40% Frostbite chance. No cooldown; arrow duration of two rounds is provisional. " +
                "No magical damage conversion. Bow effectiveness is 150% total damage against flyers; rounding/order unresolved.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                range: new(AbilityRangeKind.EquippedWeapon), reuseInterval: 0),
            new AbilityDefinition(WinterHunterId, "Winter Hunter",
                "+3 physical damage against targets already Frostbitten, including eligible ordinary attacks, counters, " +
                "follow-ups and Frostbite Arrow. The hit first applying Frostbite does not retroactively gain this bonus.",
                AbilityClassification.Passive)
        }
    );
}
