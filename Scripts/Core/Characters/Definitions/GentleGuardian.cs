using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class GentleGuardian
{
    public const string HealingLightId = "healing_light";
    public const string SanctuaryShieldId = "sanctuary_shield";
    public const string GentleBlessingId = "gentle_blessing";

    public static CharacterDefinition Definition { get; } = new(
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
        ),
        // Sections 3 and 12.2: deliberate active abilities use Main Actions.
        // Spell tags and Sanctuary Shield's range are not confirmed.
        Abilities: new[]
        {
            new AbilityDefinition(HealingLightId, "Healing Light",
                "Heal an ally within Manhattan range 3 for floor(WIS / 2) + 2 HP. " +
                "Recipient healing history reduces repeated heals; no cooldown.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                range: new(AbilityRangeKind.ManhattanDistance, 0, 3), reuseInterval: 0),
            new AbilityDefinition(SanctuaryShieldId, "Sanctuary Shield",
                "Grant 6 temporary shield HP to one allied recipient. Recasting refreshes that shield or transfers it; no cooldown.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction, reuseInterval: 0),
            new AbilityDefinition(GentleBlessingId, "Gentle Blessing",
                "Successful supportive abilities grant the recipient +2 DEF and +2 RES through the following round's Cleanup. " +
                "Reapplication refreshes duration without stacking, including across multiple Guardians.", AbilityClassification.Passive)
        }
    );
}
