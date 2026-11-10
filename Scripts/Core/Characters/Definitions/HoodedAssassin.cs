using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Core.Characters.Definitions;

public static class HoodedAssassin
{
    public const string SmokeBombId = "smoke_bomb";
    public const string ShadowStrikeId = "shadow_strike";
    public const string SmokeVeilId = "smoke_veil";
    // Share this immutable template, including its immutable ability collection.
    public static CharacterDefinition Definition { get; } = new(
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
        ),
        // Section 3 makes deliberate active abilities Main Actions. Section 12.1
        // does not confirm spell tags; null keeps Silence compatibility unresolved.
        Abilities: new[]
        {
            new AbilityDefinition(SmokeBombId, "Smoke Bomb",
                "Create a 5×5 cloud centered on the Assassin, then teleport to a legal unoccupied cloud tile. " +
                "Smoke lasts through the following round; invisibility follows the smoke visibility rules.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                range: new AbilityRange(AbilityRangeKind.ManhattanDistance, 0, 0), reuseInterval: 3),
            new AbilityDefinition(ShadowStrikeId, "Shadow Strike",
                "Range-1 physical attack: +3 opening damage when visible, or +5 and one-round Disarm on an eligible " +
                "invisible opening hit. Reveals the Assassin. Normal counters and follow-ups; no repeated opening bonus.",
                AbilityClassification.Active, requiredAction: AbilityActionType.MainAction,
                range: new AbilityRange(AbilityRangeKind.ManhattanDistance, 1, 1), reuseInterval: 0),
            new AbilityDefinition(SmokeVeilId, "Smoke Veil",
                "Gain +20 percentage points evasion while inside smoke, even when revealed.", AbilityClassification.Passive)
        }
    );
}
