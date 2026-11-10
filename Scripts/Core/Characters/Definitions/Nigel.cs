using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class Nigel
{
    public const string ProvokingRoarId = "provoking_roar";
    public const string BrutalKnockbackId = "brutal_knockback";
    public const string BerserkersFuryId = "berserkers_fury";

    public static CharacterDefinition Definition { get; } = new(
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
        ),
        // Sections 3 and 12.4 place these active abilities in the Main Action budget.
        // Neither spell tag is confirmed; null must not be interpreted as non-spell.
        Abilities: new[]
        {
            new AbilityDefinition(ProvokingRoarId, "Provoking Roar",
                "Enemies within Manhattan radius 2 that have not activated must attack Nigel if legally possible " +
                "(after movement if needed) or end without another action, until round end. No forced movement. Reuse 3. " +
                "Taunt and action-restriction details remain unresolved.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                range: new(AbilityRangeKind.ManhattanRadius, 0, 2), reuseInterval: 3),
            new AbilityDefinition(BrutalKnockbackId, "Brutal Knockback",
                "Adjacent physical Axe attack: max(1, floor((normal Axe damage + Fury) / 2)). Apply shield-first damage, " +
                "then push surviving hit targets up to 2 tiles directly away. One collision deals flat shield-first 3 to " +
                "the target and any second unit struck, without moving the second unit. Misses and lethal openings cause " +
                "no push or collision. No counters. Main Action; reuse 2. Follow-ups and blocked-push stopping positions unresolved.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                range: new(AbilityRangeKind.ManhattanDistance, 1, 1), reuseInterval: 2),
            new AbilityDefinition(BerserkersFuryId, "Berserker's Fury",
                "Add floor(10 * (MaxHP - CurrentHP) / MaxHP) physical damage to basic attacks, counters, follow-ups " +
                "and applicable physical abilities. Add Fury before Brutal Knockback's opening damage reduction.",
                AbilityClassification.Passive)
        }
    );
}
