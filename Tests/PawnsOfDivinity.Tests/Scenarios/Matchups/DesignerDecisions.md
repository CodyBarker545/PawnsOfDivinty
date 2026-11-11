# Designer decision queue — Stage 6.4

These are proposals for review, not changes to Specification v0.2. No recommendation is
approved merely by appearing here. Start with the first questions that block the next slice;
the register retains less urgent edge cases without forcing an exhaustive questionnaire now.
No question asks the designer to choose C# architecture, class names or collection types.

Already settled and **not** up for redesign here: eight base stats; WIS healing/INT magic;
14×14 grid; triangle direction/±2/±10; base minimum 1; 1.25 follow-up threshold and 50/75 endpoints;
150% **total** flying Bow effectiveness; status coexistence/global duration; shield-first Poison;
Gentle Blessing's finalized values/nonstacking/lifetime; Roar's core restrictions, Utility,
first-source priority and same-round expiry; Knockback's finalized damage/collision amount/no
counters; and the approved Shadow Strike pre-approach snapshot ruling.

## D01 — minimum movement and turn contract (P0; R06/R27)

**Question:** Which legal traversal contract should the first authoritative movement slice use?

1. **Flat ordinary slice with explicit legal costs; occupied units block traversal and destination.**
   Smallest deterministic scope; excludes allied crossing/elevation until approved and may be tactically restrictive.
2. **Flat ordinary slice allowing allied traversal but not occupied destinations.** More formation
   flexibility; requires precise crossing/occupancy checks and step reactions.
3. **Full terrain/height/crossing contract first.** More representative; longest dependency chain
   and ice/height questions must be answered before implementation.

Recommended scope: option 1 or 2 as a deliberately limited slice, **not** approval of a unit-step
cost or blocked-body rule. Specify actual costs, whether dead units occupy tiles, and how an
interrupted unsplit opportunity is consumed. Engineering can accept explicit approved paths
without inventing a pathfinder. Impact: MovementActionExecutor and real range/formation tests.

**Separate turn question:** choose an explicit first-team policy each round: alternate first
team (simple), preserve a fixed first team (predictable but potentially asymmetric), or approve
another deterministic initiative rule (more design/implementation). Also approve current
skip-exhausted-team behavior or supply an explicit alternative. No canonical choice recommended
from this fixture data; selecting the first team in a test does not decide gameplay.

## D02 — missing starting equipment and unarmed inputs (P0/P1; R01/R12)

**Question:** What approved starting Axe/Lance/Bow Power and unarmed Power/accuracy should
canonical encounters use? Sword 5, Light 3 and Fire Scroll 4 are already confirmed.

1. Approve the existing **test** Axe/Lance/Bow 4/3/4 as a first playable configuration. Fastest;
   no evidence yet establishes that those values are balanced, and unarmed still needs explicit approval.
2. Supply different designer-selected starting values and unarmed configuration. More deliberate;
   requires fresh controlled reports and no automatic inference from role names.
3. Keep caller-supplied fixture equipment for now. Preserves experiments; does not unblock
   canonical starting equipment or meaningful cross-character balance.

Recommendation: approve a small explicit starting catalog before expanding upgrades; no numeric
default is approved by this review. Impact: configuration/data tests, Disarm/Shadow Strike fallback,
and comparable damage experiments. This does not authorize full inventory or a new weapon formula.

## D03 — ordinary accuracy policy (P0; R02)

1. **Explicit attack/equipment baseline plus named modifiers.** Fits current APIs; designer must
   supply canonical baselines and gameplay floor/cap, including terrain/evasion ordering.
2. **Approved attribute-derived formula.** Potential stat expression; new formula requires design
   and more tuning, not inference from SPD/INT.
3. **Retain fixture-only baselines.** Safe test scope, but no canonical reliability/evasion comparison.

Recommended architecture-compatible option: 1, with actual numbers chosen by the designer.
The current 0–100 clamp is probability safety; decide whether gameplay has narrower caps.
Keep independent per-strike rolls and confirmed triangle percentage points. Impact: nearly every
attack, Smoke Veil experiment and future map modifier. All-zero scripts cannot choose this policy.

## D04 — ordinary damage ordering (P1; R03/R05)

1. **Approve current limited composition as an ordinary baseline**, then explicitly place each
   percent modifier around it. Lowest migration; early triangle clamp can amplify minimum damage.
2. **Keep confirmed base damage minimum, combine ordinary flat modifiers, then apply approved
   percentage stages and final minimum.** More uniform; changes some low-damage existing prototype outcomes.
3. **Explicit per-ability pipelines with a shared ordinary baseline.** Expressive; larger review
   and regression surface and not a reason to create a generic modifier framework.

Recommendation: specify a short order with worked minimum-damage and multiplier examples before
choosing. No universal order can be inferred from the suite. Preserve finalized Knockback
`floor((normal Axe + Fury)/2)` and its collision 3/no extra modifiers as local constraints.
For flying, decide where confirmed 150% total applies and its integer rounding; do not reopen 150% versus 250%.

## D05 — follow-up interpolation and rounding (P1; R04)

**Interpolation options:** retain 50% step until 1.75 (simple/current prototype); interpolate
between 50% and 75% (smoother but must specify exact integer arithmetic); or approve explicit
intermediate tiers (readable, more breakpoints). None changes the settled threshold/endpoints.

**Rounding/minimum options:** floor each scaled strike with minimum positive 1 (current prototype);
floor allowing zero; or specified nearest rounding with a chosen minimum. Each changes low-damage
and shield interactions. Also confirm positive effective SPD versus zero: current capped-75%
behavior versus an explicitly different zero-Speed rule. Recommendation: retain current behavior
only as a named prototype policy pending decision; do not silently make it permanent. Impact:
SPD/Paralysis evidence and survivability. Sentinel's no-follow-up exception is already confirmed.

## D06 — missing spell tags and cast target metadata (P1; R14–R16)

**Spell tags:** approve an explicit yes/no table for the twelve active abilities; or approve a
precise taxonomy and enumerate the twelve under it; or keep tags unresolved and reject affected
Silence cases. Recommendation: explicit table. Names, magic visuals and damage type are insufficient.

**Sanctuary Shield range:** fixed designer-selected Manhattan range, touch range, or another
explicit documented target contract. Each trades reach/support safety; no numeric recommendation
is supported by the suite. Decide self permission and any LOS/map restrictions separately.

**Temporal Ignition range:** equipped-scroll range, fixed designer-selected ability range, or
another explicit cast contract. Equipment-dependent range varies with gear; fixed range is
predictable but can differ from ordinary attacks. Do not assume the scroll decides the ability.

Impact: remove actual null-metadata blockers without changing confirmed shield 6, stored +5,
next-round timing, Main Actions or reuse intervals. An unresolved decision remains blocked.

## D07 — shield layers and Sanctuary aura scope (P1; R09/R25)

**Distinct shield sources:** reject overlap (simple but restrictive); refresh/replace one according
to an approved priority (deterministic but may discard value); or store ordered layers (expressive,
requires exact absorption/source lifetime rules). Recommendation: retain explicit unsupported overlap
until a real second source is designed; approve one-source Sanctuary Shield behavior as already documented.

**Overlapping Sanctuary sources:** nonstacking strongest/single bonus, additive per source, or
explicit capped contribution. Current code returns unresolved. Recommendation for consideration:
nonstacking +3 is simplest and avoids source-count scaling, but is **not** approved or inferred.

**Source death:** first clarify §12.5's unscoped “now CONFIRMED persists” against prior design records.
Possible intended scopes: created terrain/placed effects only; calculated Sanctuary too; or
explicit per-effect persistence. Current code treats qualifying dead-source aura as unresolved.
Do not reopen wall/ice/smoke persistence or silently impose aura death behavior. Impact: physical
combat may be blocked in otherwise ordinary overlapping/dead-source formations.

## D08 — ice and Frostbite lifetime/cost (P1; R08)

1. Status reduces effective MOV; approved normal entry cost remains separate **without another
   duplicate −2 charge**. Simple allowance interpretation; specify immediate remaining-MOV behavior.
2. Terrain entry cost represents the penalty instead of a second full allowance reduction.
   Clear per-step accounting; requires status/provenance presentation and leaving rules.
3. A precise combined remaining-allowance/entry rule with explicit non-double-charging examples.
   Most flexible; greatest implementation and explanation cost.

Recommendation: approve worked examples for entering with different remaining MOV, leaving ice,
ice expiration, caster death and simultaneous arrow Frostbite before choosing. Separately choose
whether terrain Frostbite is coverage-bound, persists for a defined duration after exit, or another
explicit lifetime. Allies moving normally and the six-tile footprint are settled. Impact: Spirit
Frost under enemies and movement through ice; no slippery motion or damage is proposed.

## D09 — Arrow/status eligibility and exchange sequence (P1; R10/R34)

**Sequence options:** proc immediately after each eligible successful strike; or defer proc decisions
until exchange end using recorded strike snapshots. First option can change later movement/bonus
eligibility; deferred handling changes those interactions. Preserve §4.2's per-eligible-strike rule;
the current executor's isolated-opening support is not an approved opening-only ability restriction.
No automatic timing choice is recommended.

**Shield/lethal options:** a hit counts even when fully shield-absorbed; require actual HP loss;
or define source-specific eligibility. For lethal hits, skip status processing on defeated targets
or record eligibility before defeat cleanup; choose whether any proc roll is consumed. No revival.
Preserve 40% on eligible hits and no retroactive Winter Hunter on the first inflicting hit.
Approve or retain provisional two-round arrow duration explicitly. Impact: full counters/follow-ups
and normal shielded targets stop being a permanently restricted Arrow subset.

## D10 — smoke/reveal/standalone teleport (P1; R07/R19–R21)

**Teleport cloud access options:** documented own new cloud only; own active cloud network with explicit
distance; or explicitly permitted shared/other-owner clouds with distance/team rules. Wider access
adds repositioning freedom and more occupancy/reaction cases. Recommendation: keep the documented
combined cast subset first; no unbounded cross-cloud rule inferred.

**Standalone economy options:** no standalone action (cast-and-teleport only); consume Movement;
consume Utility; or consume a separately specified Main Action. Decide interaction with an already
spent movement opportunity. These are choices, not changes to combined Smoke Bomb's existing cost.

**Reveal-reset options:** specified round boundary; specified re-entry/new cast event; or another
explicit visibility event. Keeping the latch indefinitely is a safeguard, not a settled mechanic.
Recommendation: define one reset event that can be tested; no particular timing is approved.
Preserve adjacency reveal, tile AoE vulnerability and approved pre-approach Shadow Strike timing.

## D11 — Transmutation outcomes and elemental progress (P1/P2; R17/R18)

**Selected scroll cannot reach:** filter legal alternatives before random selection (changes pool
probabilities); allow retargeting under explicit commit/action rules (more interaction); or define
failure/equipment/cost behavior after selection (simpler flow but potentially punitive). Recommendation:
no default until designer intent is confirmed. Current execution can spend selection RNG without
committing equipment/costs, and does not reroll automatically.

**Element “used”:** completed successful hit only; completed cast including miss; or an explicit
element-use trigger with separately defined interruption behavior. Decide catalog and cap policy
separately. Recommendation: keep current no-award unresolved safeguard until approved. Previous
stacks affecting the attack, not the newly earned stack, and persistence through death are settled.

## D12 — collision and reaction edge policies (P1; R23/R24)

**Blocked push options:** stop at last legal tile with a precisely defined collision; reject
the entire unsupported displacement before the opening; or an approved obstacle-specific rule.
Tradeoff: flexible board play versus simplicity; no default or wall destruction inferred. Collision
damage 3 and no counters are finalized. Separately approve/forbid Knockback Speed follow-ups
and define timing if approved; don't assume ordinary follow-up position remains valid after push.

**Sentinel count options:** approve current provisional one reaction per preparation; one per
moving enemy; or another explicitly bounded limit. Recommendation for the first slice: request
approval of current one-use policy, without treating current tests as approval. Special movement
can be explicitly excluded, explicitly included by category, or designed per movement source.
Choose ordering of multiple qualifying Colossi if supported; no-follow-up and normal hit-stop
behavior are already confirmed. Impact: authoritative movement interruption and safe repeat handling.

## D13 — complete-match dependencies, not the next coding task (P0 full match; R29–R33)

Verify prior design records first. The existing visual prototype has a single four-cell Oathstone
with claim/contest/retained ownership; it does **not** establish canonical three-objective rules.
Possible complete packages: presence-based capture, end-activation capture, or explicitly staged
capture, each with contest/ownership and end-round scoring specified. No competitive recommendation
is defensible from combat-only fixtures. Approve victory threshold/tiebreak/income and a minimal
economy/catalog separately; defer unsupported content only in a labeled sandbox. Loot/buyback
normal rules already decided in §§8–10 need implementation, not another vote.

## How to answer efficiently

Approve one small bundle or supply the missing prior decision reference. Unanswered items stay
unresolved. Stage 6.5 Cleanup can proceed on existing deadlines while D01–D06 are reviewed; the
next movement/ability stage must name its approved scope and reject unsupported contexts.
