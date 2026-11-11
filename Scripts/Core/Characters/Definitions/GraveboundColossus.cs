using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Combat;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class GraveboundColossus
{
    public const string WailingRampartId = "wailing_rampart";
    public const string ImpalingSentinelId = "impaling_sentinel";
    public const string SanctuaryOfTheForgottenId = "sanctuary_of_the_forgotten";
    // Starting category only: the basic Lance's Power is still TBD in specification 4.1.
    public const WeaponCategory StartingWeaponCategory = WeaponCategory.Lance;

    public static CharacterDefinition Definition { get; } = new(
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
        ),
        // Section 3 puts deliberate active abilities in the Main Action budget.
        // Section 12.5 does not confirm spell tags; leave them unknown.
        Abilities: new[]
        {
            new AbilityDefinition(WailingRampartId, "Wailing Rampart",
                "Place 3 contiguous horizontal or vertical ghostly wall tiles on unoccupied squares; nearest tile " +
                "within Manhattan 1–4. Enemies cannot cross normally; allies pass; qualifying special movement may bypass. " +
                "Persists through the following round's Cleanup, including after caster death. Reuse 3. " +
                "Projectile passage is provisional; terrain/visibility and overlapping-wall interactions remain unresolved.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                range: new(AbilityRangeKind.ManhattanDistance, 1, 4), reuseInterval: 3),
            new AbilityDefinition(ImpalingSentinelId, "Impaling Sentinel",
                "Prepare for the remainder of this round. Enemy movement into equipped Lance range triggers a reaction " +
                "Lance attack. A hit stops further normal movement but leaves the enemy's action; a miss allows movement. " +
                "No follow-up; reuse 2. One reaction per use is provisional; teleport triggers and range-2-only Lance behavior unresolved.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                range: new(AbilityRangeKind.EquippedWeapon), reuseInterval: 2),
            new AbilityDefinition(SanctuaryOfTheForgottenId, "Sanctuary of the Forgotten",
                "Allies within Manhattan 2 gain +3 DEF against attacks from enemies adjacent to the Colossus. " +
                "Does not benefit the Colossus and does not increase RES.",
                AbilityClassification.Passive, range: new(AbilityRangeKind.ManhattanRadius, 0, 2))
        }
    );
}
