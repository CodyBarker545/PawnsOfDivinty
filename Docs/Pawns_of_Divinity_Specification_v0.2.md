# Pawns of Divinity
## Consolidated Game Rules & Six-Character Specification
**Version:** 0.2 — Round Lifecycle, Economy & Redeployment Consolidation  
**Date:** November 6, 2026  
**Engine:** Godot 4 / C# (`PawnsOfDivinity`)  
**Testing:** xUnit  
**Source:** Supersedes the supplied Version 0.1 pre-prototype specification; incorporates subsequent Section 8 decisions through Question 249.  
**Status:** Pre-prototype design baseline; not a claim that systems are implemented.

### Decision key
- **CONFIRMED:** Chosen by the designer in the recorded discussions.
- **PROVISIONAL:** Recommended prototype interpretation or balance value not explicitly finalized.
- **UNRESOLVED:** An edge case or number still requiring a decision.

**Change-control principle:** Later confirmed decisions override older text. A recommendation does not become confirmed merely because it appears in this document.

---

# 1. Game identity and presentation

**CONFIRMED:** A competitive 6v6, dark-fantasy tactical game in which two players act as gods controlling mortal champions. Combat favors positioning, frontline/backline formations, flanking and anti-flanking, elevation, neutral objectives, equipment choices, counterattacks and follow-ups. Stylized three-dimensional isometric/three-quarter battlefield; player can pan/move, rotate, and zoom the camera. Tactical effects must be readable. Final title: **Pawns of Divinity**. C# namespace: **PawnsOfDivinity**.

**CONFIRMED match basics:** Two players, six units each, a **14×14 square grid**, **Manhattan (orthogonal) distance**, elevation, fog of war, **alternating unit activations**, **three neutral objectives**, shop, buyback and redeployment. Mirrored routes and multiple attack approaches are design goals. Victory is objective/points-driven; points are awarded **at the end of each round** (Q249).

**UNRESOLVED:** Detailed objective capture/contest rules, score per objective, victory threshold, sudden-death/tiebreaker, first player each round, visibility and line of sight, exact elevation/movement costs and whether allies may be crossed. Earlier objective decisions may exist outside this supplied Version 0.1; do not overwrite those without verifying them.

# 2. Character statistics

**CONFIRMED eight base attributes:** HP (maximum health), STR (physical attack), DEF (physical defense), INT (magical attack), RES (magical defense), WIS (support/healing), SPD (follow-up eligibility), MOV (movement allowance). Keep maximum HP distinct from current HP in match state.

| Character | HP | STR | DEF | INT | RES | WIS | SPD | MOV |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Hooded Assassin | 38 | 17 | 8 | 5 | 9 | 4 | 24 | 6 |
| Gentle Guardian | 44 | 7 | 11 | 12 | 15 | 18 | 14 | 4 |
| Time Weaver (Ash Scholar) | 38 | 6 | 8 | 17 | 13 | 14 | 17 | 5 |
| Nigel | 52 | 20 | 11 | 3 | 8 | 4 | 13 | 4 |
| Gravebound Colossus | 64 | 15 | 22 | 3 | 9 | 5 | 7 | 3 |
| Beothuk Spirit Guardian | 40 | 16 | 9 | 6 | 12 | 13 | 18 | 5 |

# 3. Movement and activation economy

**CONFIRMED:** Orthogonal grid movement, normally up to MOV, without splitting movement into multiple movement segments. Each not-yet-activated living unit receives one activation per round. Players alternate activations. A character may skip movement and/or its main action. Movement may precede or follow an eligible utility consumable, but **cannot resume after the main action**.

**CONFIRMED (Q221, Q235):** Per activation: **one movement opportunity, at most one Utility Action, and at most one Main Action**. A unit can use an eligible cleansing/defensive item as its Utility Action and still attack or use an ability as its Main Action. More powerful healing/offensive consumables may cost the Main Action, to be specified per item. The player can explicitly end activation early; completing a Main Action ends activation automatically after the complete combat sequence. All remaining opportunities are forfeited; unit cannot reactivate that round. Attacks include their eligible counters/follow-ups before activation completion.

**PROVISIONAL implementation:** A defensive utility item and a cleanse use the same once-per-activation utility allowance. A unit cannot use utility items after its Main Action has ended the activation. If a Main Action triggers a lengthy reaction chain, resolve the entire chain before ending the activation.

**UNRESOLVED:** What happens when one team runs out of eligible units first; exact eligibility of item use while Silenced/Paralyzed; movement-path interception, terrain cost and height transitions; whether a unit can take an ordinary action before moving (the established sequence does not allow post-action movement).

# 4. Weapons and combat

## 4.1 Categories and range

**CONFIRMED:** Sword, Axe, Lance, Bow, and magical scrolls/light magic. Physical triangle **Sword > Axe > Lance > Sword**. Advantage **+2 damage and +10 percentage points accuracy**; disadvantage **−2 and −10**; neutral 0. Bows and scrolls are triangle-neutral. Axe and Lance have melee and ranged shop paths and retain their category. Scrolls are infinitely reusable equippable magic weapons; elemental type and level matter.

| Starting equipment | Power | Range | Notes |
|---|---:|---|---|
| Assassin's Daggers (Sword category) | +5 | 1 | Visually daggers |
| Gentle Guardian's Light Magic | +3 | 1–2 | Magical, neutral triangle |
| Time Weaver's Level 1 Fire Scroll | +4 | 1–2 | Magical, infinite casts |
| Nigel's Basic Axe | TBD | 1 | Physical Axe |
| Colossus's Basic Lance and shield | TBD | 1 | Physical Lance |
| Spirit Guardian's Basic Bow | TBD | 2–3 | Physical Bow |

**CONFIRMED:** Standard Bows cannot hit/counter adjacent enemies and deal **150% damage against flying units** (exact rounding/order unresolved). A scroll's level/type/power/range may change with equipment. The Time Weaver's transformed scroll must be distinct and same-level.

## 4.2 Base combat

- **CONFIRMED physical damage:** `max(1, STR + WeaponPower − TargetDEF)` before any separately specified additional modifiers.
- **CONFIRMED magic damage:** `max(1, INT + SpellPower − TargetRES)` before any additional modifiers.
- **CONFIRMED accuracy direction:** ordinary hits are generally 90–100% baseline; weapon triangle, elevation, terrain and evasion modify it; individual strikes roll separately. Exact formula and caps **UNRESOLVED**.
- **CONFIRMED counters:** defender survives opening hit, can reach attacker with currently available weapon/attack, and countering is not specifically suppressed. No counter for AoE or Temporal Ignition.
- **CONFIRMED follow-ups:** if faster SPD ≥ **1.25×** slower SPD, faster unit may make one reduced-damage follow-up. Damage fraction **50% at ratio 1.25**, increasing to **75% at 1.75+**; one follow-up maximum; no new counter to follow-up. Exact interpolation/rounding unresolved.
- **CONFIRMED:** AoE has no counters/follow-ups; single-target active attacks follow normal rules unless specifically exempt. Status rolls apply separately to eligible successful strikes unless the ability says otherwise.
- **BALANCE TARGET:** ordinary attacks frequently defeat typical units in ~2–4 hits; not a fixed rule.

**UNRESOLVED:** Full calculation order for triangle/flat bonuses/percent modifiers/minimum damage/shield, damage multiplier rounding, counter/follow-up ordering in unusual cases, line of sight/elevation formulas.

# 5. Negative statuses and temporary effects — REVISED

**CONFIRMED (Q225–226):** **Different negative statuses coexist simultaneously.** There is **no one-negative-status slot**. A unit can have Poison, Frostbite, Paralysis, Burn, Silence and Disarm at once. A new application of an **identical** status **refreshes its duration**, not its strength; it does not create another −2 MOV or −3 DEF stack. Status priority controls **resolution order**, not permission to apply other statuses. Status effects become active upon application unless their individual rules say otherwise.

| Status | Effect | Duration / timing | Confidence |
|---|---|---|---|
| **Frostbite** | −2 MOV | 2 rounds for arrow application (numeric duration provisional); global rounds | Effect confirmed, duration provisional |
| **Paralysis** | Reduces **SPD**, not MOV | 2 rounds baseline; exact SPD penalty **TBD** | Direction confirmed |
| **Burn** | −3 DEF, **no DoT** | 2 rounds | Confirmed |
| **Poison** | Increasing 2, 4, 6, 8… damage | At **start of each subsequent round**, until cleansed/death | Confirmed |
| **Silence** | Prevents spell-tagged abilities | 1 round | Confirmed |
| **Disarm** | Equipped-weapon benefits unavailable; may use unarmed range-1 attack | 1 round | Confirmed |
| **Sleep** | Effect TBD | TBD | Deferred |

**CONFIRMED (Q236–237):** **Timed negative statuses count global rounds, not affected activations.** On the next round start after a status is applied, its remaining-round counter decreases by 1 **after** Poison and scheduled delayed-damage stages. Remove at zero before the next Activation Phase. A two-round status applied in R2 is active during R2 and R3 and expires at the beginning of R4 (after the damage stages). A one-round status applied in R2 expires at R3 start even if the target had already activated when it was applied. The earlier proposed guarantee of one *affected activation* (Q224) is **superseded** by this later explicit round-based rule.

**CONFIRMED:** Status duration refresh from identical reapplication does not multiply stat penalties. **Poison reapplication** does not reset or reduce its rising tick; Poison already persists until cleansed, so it continues at its current severity (Q226 interpretation). New and existing statuses each track independently. Cleansing removes specified active negative statuses immediately. Shields, buffs, timed damage markers and terrain are distinct from negative statuses.

**Important PROVISIONAL interpretation — Spirit Frost:** Allies traverse ice normally. Enemies standing on/entering Spirit Frost become Frostbitten; their movement is restricted without applying the same −2 penalty twice. The earlier movement example charges a total of **3 movement points for first entry** (normal 1 + 2). Exact interface between a terrain entry cost and −2 MOV, and whether terrain-applied Frostbite expires on leaving the field or persists, remain **UNRESOLVED**. Do **not** implement both full penalties cumulatively as a settled rule.

# 6. Start-of-round Poison and delayed damage — REVISED

**CONFIRMED (Q222–223, Q237–240):** Shop comes before damage. A newly applied Poison does **not** deal damage immediately; its first 2-damage tick occurs at the **beginning of the following round**, before the poisoned unit can cleanse during activation. Poison ticks are evaluated **before** Temporal Ignition. Timed statuses decrement **after** both damage stages.

### 6.1 Poison batch
1. Gather every living Poisoned unit and calculate its scheduled tick (**2, 4, 6, …**).
2. **Apply all Poison ticks together**, snapshotting the batch so unit iteration order cannot change who takes scheduled damage (Q238).
3. Shields absorb Poison **before ordinary HP** (Q239).
4. After the entire batch, process Poison-caused deaths and create their equipment drops.
5. Surviving Poisoned units advance to their next tick (+2); a cleansed unit will not take later Poison ticks.

### 6.2 Delayed attack queue
1. After Poison deaths, resolve surviving targets' pending delayed attacks **one at a time in original application order** (Q240), using a monotonically increasing creation sequence as tie-breaker.
2. Temporal Ignition's **hit roll and damage value were established at casting**, so later changes to RES do not retroactively change the stored damage.
3. On resolution, existing shields absorb the stored damage before HP; then immediately handle any resulting death.
4. Subsequent pending attacks against a defeated target are cancelled. Delayed attacks do not trigger counters/follow-ups and cannot be cleansed as a negative status.
5. If Poison killed a target first, all queued delayed attacks targeting it are cancelled.

**UNRESOLVED:** Source credit and death-trigger ordering when multiple Poison deaths happen together; exact shield-source priority if multiple different shield effects coexist; triggers that listen for HP damage vs shield damage; what to do if another scheduled effect creates a new delayed effect during processing. **PROVISIONAL:** Process the pre-existing delayed-attack queue without allowing same-stage infinite scheduling.

# 7. Full round lifecycle — REVISED IMPLEMENTATION BASELINE

For a global round `R`, use the following sequence. Shop is a distinct pre-combat phase for that numbered round.

| Priority | Phase | Required behavior | Confidence |
|---|---|---|---|
| 1 | **Shop (R)** | Both players buy items, assign gear from shared team inventory, purchase buybacks; cleanses are not applied automatically | Confirmed |
| 2 | **Poison (R)** | All Poison ticks resolve as one batch; shields first; then deaths/loot; advance surviving Poison severity | Confirmed |
| 3 | **Delayed damage (R)** | Oldest applied attack first; shields/HP/death after each attack; cancel invalid pending targets | Confirmed |
| 4 | **Timed status update (R)** | Decrement global-round negative-status durations; remove those reaching zero | Confirmed |
| 5 | **Other start-of-round events** | Apply future scheduled triggers at explicitly assigned priorities | Provisional extension point |
| 6 | **Round preparation (R)** | Place purchased redeployments, determine cooldown readiness, reset activation flags and per-round counters | Placement timing recommended; eligibility confirmed |
| 7 | **Alternating activations (R)** | Move once (up to MOV), optional one Utility Action, optional Main Action, explicit End or auto-End after Main Action | Confirmed |
| 8 | **Round-end cleanup (R)** | Resolve any end-of-round triggers; expire battlefield effects/buffs and eligible loot | Confirmed concept; ordering within this stage provisional |
| 9 | **Objective scoring (R)** | Award points at round end to controlling teams; check victory; proceed to Shop (R+1) | End-round timing confirmed; scoring specifics unresolved |

**Special timing distinctions:**
- Ability cooldown: `NextAvailableRound = RoundUsed + ReuseInterval`, **not** based on activation counts (Q241). An ability used in R2 with reuse 3 is ready in **R5**. An ability with no reuse can be used in later eligible activations (but one Main Action per activation).
- Timed terrain like Spirit Frost, Smoke Bomb, and Wailing Rampart persists through **casting round + following round**, expiring **at the end of the following round** (cast R2 → expires end R3).
- Loot created in R4 persists through **end of R5**, then expires if uncollected (Q233).
- A shield such as Sanctuary Shield is **not** assumed to expire merely because the round ends; follow its ability's own depletion/recast rules.
- **UNRESOLVED:** First-activation player selection each round, side with no eligible units, exact objective ownership rules, post-victory outstanding delayed effects and cleanup, full order of other end-of-round triggers.

# 8. Shop, shared inventory and consumables — REVISED

**CONFIRMED (Q221, Q227–229):** Each player has **one shared team inventory** containing **weapons, scrolls, consumables and eligible purchased items**; not individual character inventories. Each team has a separate treasury. Shop purchases enter its shared inventory. Items may be **assigned/equipped/reassigned only during the Shop Phase**, with explicit ability-based exceptions (Time Weaver's Arcane Transmutation). A weapon may only be assigned to one eligible character at a time and must satisfy category restrictions. Equipped items should still be represented as team-owned but assigned in inventory state.

**CONFIRMED:** During activation, any eligible character can consume an available shared-team consumable. Each use deducts one inventory quantity. One Utility Action per activation, with at most one consumable used in that allowance. A Basic Cleanse removes **one chosen negative status**; a Greater Cleanse removes **all negative statuses**; both cost a Utility Action and allow a later Main Action. A newly bought cleanse cannot prevent that round's pre-activation Poison tick. Neither type removes buffs, shields or pending Temporal Ignition damage.

| Item type | Effect | Action category | Confidence |
|---|---|---|---|
| Basic Cleansing Potion | Remove one selected negative status | Utility | Confirmed |
| Greater Cleansing Potion | Remove all negative statuses | Utility | Confirmed |
| Basic defensive consumable | Item-specific defense effect | Utility | Category intent confirmed; actual items TBD |
| Powerful healing consumable | Item-specific heal | Main Action normally | Provisional item classification |
| Offensive consumable | Item-specific offensive effect | Main Action normally | Provisional item classification |

**UNRESOLVED:** Actual shop prices, starting gold, income sources, purchase limits, inventory stack/capacity limits, sellback rules, whether utility items may target allied units other than the acting character, item-use eligibility while particular statuses are present, scope of defensive items, gear resale vs battlefield recovery. Equipment Power catalog remains TBD.

# 9. Death, equipment drops and gold recovery — REVISED

**CONFIRMED (Q230–234):** When a character dies, **equipped weapons and armor** drop onto its death tile as **one combined loot pile** containing their **original purchase values**, with the original owning team identified. Unequipped team inventory remains in storage. Dropped gear is **not transferred as equipment**; it converts immediately to **shared-team gold** on collection.

- **Allied pickup = 50%** of combined original purchase value.
- **Enemy pickup = 25%** of combined original purchase value.
- **Uncollected pickup = 0%**; the remaining value is removed from the economy.
- To collect, a living unit must **end its movement on the loot tile**. Passing through or stopping adjacent does not count. A legal teleport that ends on the tile counts. Collection is **free** and does not spend Utility or Main Action; the collector may still attack.
- Loot created in round `R` expires at the **end of round `R+1`**, including when generated by start-of-round Poison or Temporal Ignition.
- Team gold increases for the collecting team only. Collected equipment **does not enter a character inventory** and cannot drop again when the collector later dies.
- Gold recovered from an enemy's loot is a new reward to the collector, **not a second charge** against the defeated team's treasury.

**Example:** A dead unit's equipped Axe (200) and armor (150) make a combined 350-value pile. Own team collects → **175 gold**. Enemy collects → **floor(350×0.25)=87 gold** if integer gold rounds down (**rounding convention provisional**). Uncollected → 0. Original 350-gold expenditure has already occurred.

**CONFIRMED at gameplay level:** Loot collection does not give the collector weapons, items or an additional inventory. **TECHNICAL SAFEGUARD ONLY (not a new designed gameplay mechanic):** If a new drop is generated on a tile with existing loot, store distinct provenance/timers internally and award all eligible subrecords when the tile is legally collected; do not invent a mechanic for carried loot to re-drop. Whether multiple on-tile drops can actually arise in normal play is disputed/unproven; tests should handle it safely.

**UNRESOLVED:** Integer rounding policy for 25% payouts and multi-item bundles (round each item or combined pile); exact collection if loot appears under a unit already standing on the tile; whether a unit that chooses to remain still on a loot tile can claim without a new movement end; whether starting equipment has a notional recoverable purchase value; potential loot-tile interaction with height/obstacles.

# 10. Death and redeployment — REVISED

**CONFIRMED (Q242–248):** On death, a character loses **temporary personal debuffs, buffs and personal shield HP**. Cooldown deadlines keep progressing according to global rounds; **permanent stat upgrades and earned permanent ability stacks (including Time Weaver's elemental stacks) persist**. Already-created battlefield effects (Spirit Frost ice, Wailing Rampart walls, Smoke Bomb cloud) **stay on the map until their own expiration**, even if the creator dies. Beneficial effects placed on other living characters are not removed solely because their creator dies; they use their own rules. The dead unit's equipped gear drops, while shared unassigned inventory stays in storage.

**CONFIRMED buyback:** Player may purchase a dead character's return during the **Shop Phase**. The character returns at **100% maximum HP**, with no temporary statuses/buffs/shields; persistent progression and cooldown eligibility remain. Compatible equipment may be assigned during Shop from available shared inventory. Bought-back units are eligible to activate **in that same round**.

**Round Preparation placement** after the current round's Poison/delayed damage is the **recommended implementation sequencing**, consistent with same-round activation; the player purchases buyback during Shop. The revived unit is not exposed to pre-placement damage in that round.

### Respawn placement priority (Q245–248)
1. Use each character's **original match-start deployment tile**, recorded permanently, if legal and free. Do not use the death position.
2. If blocked/occupied, search for **nearest legal tiles in that character's original allied deployment zone**, using Manhattan distance and **randomly** choose among equally nearest eligible positions.
3. If **every legal tile in that deployment zone** is blocked, perform **emergency placement outside the deployment zone**: search the remaining battlefield for the nearest legal tile and randomly select among equally near choices.
4. **UNRESOLVED:** If no legal tile exists anywhere on the board, define a final queue/refund/other policy. Do not invent an automatic success.

**CONFIRMED:** The original preferred tile never changes as the unit moves. Random tile selection should use an injectable RNG for deterministic xUnit tests. Collision, occupancy, height and blocked terrain must be respected; precise legal-tile rules remain to be specified.

# 11. Healing, shields, and related interactions

**CONFIRMED:** Repeated healing received by a unit in one round is diminished: first heal **100%**, second **75%**, third **50%**, fourth/later **25%** (fourth-plus behavior is current baseline; earlier document marked continuing-floor behavior unresolved). Never heal above maximum HP. Exact rounding TBD. Reset the per-round received-healing counter during Round Preparation.

**CONFIRMED Sanctuary Shield:** Gentle Guardian grants **6 temporary shield HP** to an ally, can maintain one shield from that Guardian at a time; recasting transfers it or refreshes same target to 6, no stacking of that ability. Shields absorb incoming ordinary, Poison and delayed damage before HP. Shield is not restored by ordinary healing. A Guardian dying does not automatically remove an existing shield on another surviving ally (Q243 clarification).

**UNRESOLVED:** Shield casting range, simultaneous distinct shield sources, exact shield expiration beyond depletion/recast, shield HP interaction with percentage modifiers, whether a shield blocks on-hit statuses, and special effects that bypass shields.

# 12. Character roster and ability catalog

The six stat distributions in §2 are **CONFIRMED**. Unless flagged, ability mechanics below are first-pass choices from the Version 0.1 character specification. All two-active/one-passive loadouts are designed; the first prototype need only implement one active + one passive initially.

## 12.1 Hooded Assassin — Flanker / Disruption
**Equipment:** +5 Power Daggers (Sword category), range 1. **Stats:** 38/17/8/5/9/4/24/6 (HP/STR/DEF/INT/RES/WIS/SPD/MOV).

- **Active — Smoke Bomb:** 5×5 cloud centered on Assassin, then teleport to a legal unoccupied cloud tile. Invisible inside cloud unless enemy orthogonally adjacent, attacks or exits smoke. AoE still hits hidden unit. Cloud lasts casting round and following round; **reuse 3**.
- **Active — Shadow Strike:** range-1 physical attack; **+3 opening damage if visible**, **+5 and Disarm for 1 round on eligible hit if invisible**; opening bonus not repeated on follow-up. Reveals Assassin; no cooldown; normal counter/follow-up rules.
- **Passive — Smoke Veil:** **+20 percentage points evasion** while inside smoke, even if revealed.
- **Unresolved:** Fog of war, smoke overlap/line-of-sight, legal teleport positions and its pickup interaction, status success roll where relevant.

## 12.2 Gentle Guardian — Healer / Support
**Equipment:** +3 Power Light Magic, range 1–2, INT vs RES. **Stats:** 44/7/11/12/15/18/14/4.

- **Active — Healing Light:** heal ally within Manhattan 3 for `floor(WIS/2)+2` (base 11); diminishing returns apply; no cooldown.
- **Active — Sanctuary Shield:** one allied recipient at a time receives **6 temporary HP**; transfer or refresh to 6; no cooldown.
- **Passive — Gentle Blessing (FINALIZED):** a successful qualifying Healing Light or Sanctuary Shield use grants its recipient **+2 DEF and +2 RES** through the end of Cleanup in the following round. Reapplication refreshes both modifier durations; bonuses never stack. Multiple Gentle Guardians refresh the same recipient's one active Gentle Blessing effect rather than summing their bonuses. Preview and rejected ability attempts do not trigger the passive. It consumes no additional action or cooldown.
- **Unresolved:** Shield range, stacking with other shield sources; healing/buff rounding. Guardian death does not automatically erase a shield on another living ally.

## 12.3 Time Weaver / Ash Scholar — Elemental Mage
**Equipment:** Level 1 Fire Scroll, +4 Power, range 1–2. **Stats:** 38/6/8/17/13/14/17/5.

- **Active — Temporal Ignition:** elemental attack with **+5 bonus damage**. Roll accuracy and calculate/store damage on casting; resolve at **start of following round after Poison**, in global application order; shield at resolution absorbs; target dying first cancels it; no counter/follow-up; not a cleanseable status; **reuse 2**.
- **Active — Arcane Transmutation:** replace currently equipped scroll with a **different random scroll of same level**, permanently, and attack immediately with new scroll and properties; normal counter/follow-up; no cooldown; requires available alternative. **Explicit combat-time equipment-change exception**.
- **Passive — Temporal Accumulation:** each **distinct element previously used** earns a persistent Time Stack; +1 magical damage per previously earned stack, not on the same attack that first earned the stack; reusing an element or upgrading its level gives no new stack. Stacks **survive death/redeployment (Q243)**.
- **Unresolved:** Full elemental pool and limits, stack cap, exactly what counts as element 'used' on a miss/interruption; ordering with damage modifiers.

## 12.4 Nigel — Berserker / Frontline
**Equipment:** Basic Axe range 1, Power TBD; melee/ranged Axe upgrade paths. **Stats:** 52/20/11/3/8/4/13/4.

- **Active — Provoking Roar (FINALIZED):** `provoking_roar`, Active, Main Action, Manhattan radius 2 centered on Nigel; affects living enemies that have **not yet activated** this round; **reuse 3**. Restrictions expire at **Cleanup of the application round** (R3 application expires at R3 Cleanup, not R4 timed-status processing).
  - **FINALIZED — Required Main Action:** after player-controlled ordinary legal movement (or declining movement), the affected unit must target the controlling Nigel with a legal attack-type Main Action if one is available, otherwise explicitly end activation without another Main Action. Do not automatically move, choose an attack, or execute one. A valid but unreachable Nigel invokes this fallback; that is distinct from an invalid source.
  - **FINALIZED — Utility Actions:** ordinary legal Utility Actions are permitted, at most one per activation. Normal budgets, targeting, cooldowns and status restrictions still apply. Utility neither satisfies the required attack nor removes taunt or grants an extra Main Action; it is not compulsory before the fallback.
  - **FINALIZED — First active source:** preserve the first active Roar's source and expiration. Later Roars do not replace, refresh or add another requirement for that recipient; they may affect other eligible enemies and follow normal cast costs. Once the first taunt expires or ends, a future valid Roar may control that recipient. Priority follows application order, not Speed or roster ordering.
  - **FINALIZED — Invalid source before initiation:** if the controlling Nigel dies, is removed or becomes definitively ineligible as an attack recipient, end that taunt and restore normal Main Action targeting. Do not end activation, restore spent Movement/Utility, grant actions, or select a replacement target. Unknown source validity remains unresolved; lack of attack reach alone does not invalidate a living source.
  - **FINALIZED — Attack contract and approval:** any legal ordinary attack or explicitly attack-classified Main Action may satisfy taunt when its target contract identifies the required Nigel. Non-attacks do not satisfy it. Do not infer attack classification from names or count a tile-targeted area effect merely because it includes Nigel. Normal targeting, Silence, cooldown and action rules still apply. An attack legally initiated against Nigel finishes its sequence, effects, Main Action and cooldown once even if Nigel dies or is displaced during resolution; no retrospective rejection or replay.
  - **UNRESOLVED:** whether a cast with zero newly applicable recipients may succeed/spend costs. Keep that case explicit rather than inventing a cast-use rule. Spell tagging remains unconfirmed.
- **Active — Brutal Knockback (FINALIZED core):** Main Action, range 1 adjacent enemy, physical Axe attack, **reuse 2**. On a successful hit, push a surviving target up to 2 tiles cardinally and directly away; an ordinary unobstructed flat-grid push travels 2 tiles. Designer-approved decisions:
  1. **FINALIZED — Fury ordering:** opening damage is `max(1, floor((NormalAxeDamage + BerserkersFuryBonus) / 2))`. Calculate normal Axe damage using existing effective stats and weapon triangle, then add Fury once before halving. Use ordinary accuracy and hit rolls; a miss has no push or collision.
  2. **FINALIZED — Damage first:** validate, roll, apply opening damage shields first, then attempt displacement for a surviving hit target and resolve any supported collision. Complete the effect before finalizing its Main Action and reuse interval 2. A valid miss still completes the use.
  3. **FINALIZED — Lethal opening:** if opening damage defeats the target, stop displacement and collision, preserve the damage/defeat results, and finalize the ability normally.
  4. **FINALIZED — Collision damage:** one event deals exactly **3 damage** to the pushed target and **3** to a second unit actually struck, if any. The second unit is not pushed. Current shields absorb first; no further DEF/RES reduction, Fury, triangle adjustment, follow-up scaling, or accuracy roll applies. Do not invent another participant or another event.
  5. **FINALIZED — Counter suppression:** Brutal Knockback never permits a counterattack, even if the defender survives or remains adjacent. This overrides the ordinary single-target counter rule in §4.2.
- **Passive — Berserker's Fury:** +1 physical damage per complete 10% maximum HP missing: `floor(10 × (MaxHP−CurrentHP)/MaxHP)`; basic attacks, counters, follow-ups and applicable physical abilities. For Brutal Knockback, add Fury before the opening reduction as finalized above.
- **Unresolved:** Provoking Roar's zero-new-recipient cast rule and spell tag as noted above; Brutal Knockback Speed-based follow-ups (the confirmed core executes without a speculative follow-up); stopping positions for blocked pushes, map-edge collision stopping behavior, elevation/falling and terrain-entry effects. Do not infer shortened pushes, diagonal rounding, pushing through units, wall destruction or elevation collision damage. Reject unsupported scenarios explicitly. Flat unobstructed two-tile displacement and the collision damage rule above are confirmed independently of these remaining movement questions.

## 12.5 Gravebound Colossus — Tank / Anti-Flanking
**Equipment:** Basic Lance range 1 + shield, Power TBD; Lance reach upgrades. **Stats:** 64/15/22/3/9/5/7/3.

- **Active — Wailing Rampart:** place **3 contiguous ghostly wall tiles** horizontal/vertical, nearest tile within Manhattan 1–4, not on occupied squares. Blocks ordinary enemy movement; allies pass; special qualifying movement may bypass. Cast round + following round; **reuse 3**. Ranged attacks passing through is **provisional**.
- **Active — Impaling Sentinel:** prepare for remainder of current round; when enemy enters Lance's attack range by movement, reaction Lance attack; on hit stop further normal movement but enemy retains action; miss allows movement to continue; no follow-up; **reuse 2**; **one reaction per use provisional**.
- **Passive — Sanctuary of the Forgotten:** allies within Manhattan 2 of Colossus gain **+3 DEF against attacks from enemies adjacent to Colossus**. Does not benefit Colossus, does not increase RES.
- **Unresolved:** Reaction trigger on teleport/special move, visibility/projectile collision, expiration when caster dies (now CONFIRMED persists), geometry and range-2-only Lance behavior.

## 12.6 Beothuk Spirit Guardian — Physical Archer / Frostbite Terrain
**Equipment:** Basic Bow range 2–3, Power TBD; 150% bonus against flyers. **Stats:** 40/16/9/6/12/13/18/5.

- **Active — Spirit Frost:** directional **3-wide × 2-deep six-tile ice field** directly adjacent north/south/east/west. Allied movement **normal** (previous +1 bonus superseded). Enemy units on/entering ice become Frostbitten (−2 MOV); avoid double charging with terrain entry penalty. Cast round + following round; **reuse 3**.
- **Active — Frostbite Arrow:** ordinary physical Bow attack in equipped range, normal damage, **40% chance to inflict Frostbite on a successful eligible hit**, **no cooldown**, normal counter/follow-up eligibility. Frostbite's arrow-applied two-round duration is currently provisional. Status chance rolls on eligible strikes subject to ability wording.
- **Passive — Winter Hunter:** **+3 physical damage against already Frostbitten targets**, including eligible ordinary attacks, counters, follow-ups and Frostbite Arrow. No extra action. The hit that first inflicts Frostbite does not retroactively earn +3.
- **Unresolved:** Exact ice movement cost mechanism; when terrain Frostbite ends after leaving; what happens to terrain-applied status when ice expires or creator dies; special interaction if the unit had an independent arrow-applied Frostbite.

**Cultural note:** The name Beothuk references a real Indigenous people. The fantasy ice magic and character abilities are invented game fiction, not historical claims about Beothuk beliefs or practices.

# 13. Objectives, map strategy, fog and progression

**CONFIRMED direction:** Three neutral objectives; mirrored routes; tactical elevation and fog-of-war; player viewpoints with free camera pan/orbit/zoom; frontline tank/warrior, backline archer/mage, support healer, flanker/Assassin roles. Allies benefit from walls, ice, shields and formation; enemies can exploit movement restrictions and minimum weapon ranges. **Points are awarded at the END of each round (Q249).**

**UNRESOLVED:** Objective capture conditions, ownership persistence, tie/contest resolution, income from objectives, actual victory score, fog-of-war visibility, elevation effects and line of sight. Shop inventory envisioned: weapons, scrolls, potions, buyback and limited stat boosts. Exact prices, gold income and build limits deferred. Equipment deaths and shared inventory **are no longer deferred**: see §§8–10.

# 14. Explicitly superseded Version 0.1 rules

These older statements **must not be copied into code**:

| Former Version 0.1 or intermediate statement | Correct Version 0.2 decision |
|---|---|
| One negative status per unit | Different negative statuses coexist; identical effects refresh, not stack strength |
| Paralysis reduces MOV | Paralysis reduces **SPD**; numeric reduction TBD |
| Poison ticks at end of round | Poison ticks at **start of next and subsequent rounds**, before cleansing |
| Poison might bypass shields | Shields absorb Poison before HP |
| Poison and delayed damage order unknown | All Poison ticks together → Poison deaths → delayed attacks oldest-first → deaths |
| Statuses expire at end of application/following round | Timed negative statuses decrement **at next round start after damage** |
| Timed negative statuses must affect one character activation | Superseded: **global round** timing even if no eligible activation was affected |
| Ally Spirit Frost gains +1 MOV | Ally movement on Spirit Frost **normal**, no bonus |
| Equipment and consumables stored per character | **One shared team inventory** includes both |
| Gear might return automatically on death | Equipped weapon/armor **drop as loot**; converted to gold on collection |
| Allied pickup 100%; enemy pickup 50% | Allied pickup **50%**; enemy pickup **25%** |
| Loot expires in same round | Loot expires at **end of following round** |
| Redeployment health/timing/location unknown | **Full HP**, purchased in Shop, acts same round, respawns at original start or closest legal random fallback |
| Death resets or pauses cooldown | Cooldown deadlines **continue through death** |
| Death clears created ice/smoke/walls | Created terrain **persists to scheduled expiration** |
| Cooldown behavior unclear | `NextAvailableRound = RoundUsed + ReuseInterval` |
| Objective scoring timing undecided | **End-of-round scoring** |

# 15. Implementation architecture (recommended, not yet written)

Keep combat authority in testable C# classes independent of Godot nodes and animation timing.

| Type/service | Responsibility |
|---|---|
| `CharacterStats` | Immutable eight-stat character baseline |
| `CharacterDefinition` / `CharacterRegistry` | Six canonical characters, class/ability/equipment permission identifiers |
| `UnitState` | HP, position, owner, equipment, statuses, permanent progression, activation state |
| `WeaponDefinition` | Category, Power, range, damage type, level/element |
| `CombatResolver` | Hit, damage, triangle, counter, follow-up, shields |
| `StatusResolver` | Multiple active statuses, refresh, status durations, poison severity, cleanse |
| `MovementResolver` | Tile/path legality, MOV cost, forced movement, teleport, loot stop trigger |
| `TerrainEffectSystem` | Ice, ghost walls, smoke, owner and expiry |
| `AbilityResolver` | Active/passive validation and effects |
| `TeamInventory` / `ShopService` | Shared stock, assignments, purchased consumables, gold, buyback |
| `LootSystem` | Death bundles, ownership, value, collection, expiry |
| `RedeploymentService` | Original tile, legal-zone search, random fallback, full HP |
| `ScheduledEffectQueue` | Temporal Ignition and future delayed effects, insertion order |
| `RoundManager` | Shop → Poison batch → delayed queue → status tick → prep → activations → cleanup → scoring |
| `ObjectiveSystem` | Objective control, end-round scoring, victory |

**Testing:** Use xUnit with injectable RNG (accuracy, Frostbite chance, random respawn tie-breakers). Do not make Godot animations authoritative for damage or round progression.

### Priority test cases
1. Two units Poisoned in same round both take their next ticks even if one dies; shield before HP; deaths after full batch.
2. Poison kills a target before its pending Temporal Ignition; ignition cancelled.
3. Two delayed attacks consume shield and HP sequentially in original creation order; death cancels later attacks.
4. Timed Burn/Frostbite/Disarm applied in R2: at start R3 decrement; at start R4 a two-round effect expires, after damage processing.
5. Distinct statuses coexist; repeated Frostbite refreshes duration without doubling MOV loss.
6. Basic Cleanse removes chosen one; Greater removes all; both use shared inventory quantity and Utility Action, leaving Main Action.
7. Death clears personal effects but keeps cooldowns and Time Weaver stacks; pre-existing ice/wall/smoke persists.
8. Gear drops as one pile; ally 50%, enemy 25%; collector only gains gold, never inherited loot gear.
9. Loot made in R4 survives R4 end and expires R5 end; stopping on tile or legal teleport picks up, crossing does not.
10. Buyback at Shop: full HP, no statuses, cooldown not reset, can act that round; original tile preferred, nearest legal tile randomized in zone, emergency search outside zone.
11. Ability used R2 with reuse 3 is unavailable R3/R4 and ready R5.
12. Score objectives after Round End effects and cleanup, not upon movement onto objective (subject to finalized capture rules).

# 16. Prototype development milestones

1. **Character/equipment data:** `CharacterStats`, six registry entries, basic weapon definitions and xUnit validation.
2. **Core combat:** Physical/magic formulas, ranges, weapon triangle, hit/evasion, counters/follow-ups, temporary shields.
3. **Battlefield vertical slice:** 14×14 grid with elevation, camera, selection, movement, turns, 2–3 test units/side, readable UI.
4. **Round and economy foundation:** `RoundManager`, global timing, Poison/delayed queue, status stack/refresh, Utility/Main Action, team inventory, death/loot, buyback.
5. **First ability + passive for each hero:** Smoke Bomb/Smoke Veil; Healing Light/Gentle Blessing; Temporal Ignition/Temporal Accumulation; Brutal Knockback/Berserker's Fury; Wailing Rampart/Sanctuary; Spirit Frost/Winter Hunter.
6. **Other six active abilities:** Shadow Strike, Sanctuary Shield, Arcane Transmutation, Provoking Roar, Impaling Sentinel, Frostbite Arrow.
7. **Full match:** objectives, fog of war, shop economy balancing, inventory/UI polish, victory conditions, multiplayer considerations.

# 17. Outstanding questions — do not silently finalize

**High priority for code correctness**
- Terrain Frostbite: is the −2 MOV status the **same** cost as first entering ice, or applied to next/remaining MOV differently? Duration while leaving ice/field expiration.
- Full damage calculation and rounding order, including flyer bow 150%, follow-up scaling and weapon triangle.
- How to calculate DEF/RES bonuses at the instant of attack, and shield-source ordering.
- Exact legal tile/path/height/occupancy rules for movement, teleportation and redeployment.
- Status odds/resistance, effect application by follow-up and unarmed Disarm behavior.
- Integer gold rounding on a 25% salvage reward, and handling combined items with odd costs.
- Player first activation and unbalanced number of activatable units.
- If the entire 14×14 map has no legal emergency respawn square: queue, refund, or another handling rule.

**Balance/content deferments**
- Power values for basic Axe, Lance and Bow; complete weapon/scroll upgrade catalog.
- Paralysis SPD reduction; status duration tweaks for Frostbite.
- Exact objective scoring/capture threshold, victory threshold and contest mechanics (check prior authoritative decisions if available).
- Shop starting resources, income, buyback cost, item prices and restrictions.
- Fog of war, line of sight, terrain and elevation combat modifiers.
- Area triggers, death triggers, multiple simultaneous shields, interaction between spawned terrain and special movement.
- Special effect metadata/animations and multiplayer synchronization strategy.

# 18. Acceptance criteria and design principles

The initial playable prototype is ready for playtests when six character definitions load correctly, players can alternate valid activations, damage/range/counters/follow-ups obey the rules, statuses and shields resolve deterministically, representative active/passive abilities work, loot and redeployment handle normal cases, round processing is repeatable, and Godot clearly displays movement/attack/HP/status information. Shop prices, every ability, all animations and polished visuals need not be final.

**Guiding principles:** Positioning over raw damage; flanking has counterplay; character roles remain distinct; equipment purchases create real tradeoffs; area control does not become an indefinite lock; system rules are deterministic and unit-testable; balance through actual playtests rather than endless questionnaires.

---
**End of Version 0.2.**
