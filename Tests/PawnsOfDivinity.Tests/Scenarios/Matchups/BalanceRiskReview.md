# Balance evidence and risk review — Stage 6.4

These are hypotheses for controlled experiments, not findings that a character is too strong
or weak. No balance changes are recommended from this suite alone. Specification references
use v0.2; unresolved issue IDs refer to `UnresolvedRules.md`.

## What the representative evidence actually says

Both `RepresentativeSuite.md` and the typed JSON export were inspected. Their five results use
the same declared flat-visible/no-flying/no-smoke scope, but different rosters, distances,
targets and numbers of actions. All use explicit baseline accuracy 100 and accuracy rolls 0;
triangle disadvantage can still lower a particular hit chance to 90. All recorded strikes hit.
The zero rolls suppress evidence of misses and do not establish canonical accuracy.

| Fixture | Completed Main Actions | Returned strikes | Actual HP loss across both teams | Shield loss | Actual healing | What executed |
|---|---:|---:|---:|---:|---:|---|
| Nigel–Colossus | 1 | 3 | 12 | 0 | 0 | Opening 4 to Colossus, counter 5 to Nigel, follow-up 3 to Colossus; final HP 47/57 |
| Assassin–Time Weaver | 1 | 3 | 33 | 0 | 0 | Ordinary opening 14 and follow-up 7 to mage; mage counter 12 to Assassin; final HP 26/17. **Not Shadow Strike** |
| Spirit Guardian–Nigel | 1 | 2 | 13 | 0 | 0 | Ground Bow opening 9 plus follow-up 4; range 3 prevents range-1 Axe retaliation; final HP 40/39. **No Frostbite in this representative case** |
| Defensive 2v2 | 3 | 5 | 25 | 6 | 1 | Guardian heals injured Colossus; Nigel attacks protected Guardian; Assassin attacks Colossus; Tank explicitly ends its activation |
| Full-roster 3v3 | 5 | 10 | 120 | 0 | 11 | Assassin/Nigel exchanges, Healing Light, mage/Bow exchanges and explicit Tank End; one scripted round, all six still alive |

“Actions” in the exports also include activation selection/End: 8 in the 2v2 and 12 in the 3v3.
Use the separate Main Action count above when describing the script; do not divide these
different totals into a ranking. Counters and follow-ups do not consume extra Main Actions.

Equipment: Sword 5, Light Magic 3, Fire Scroll 4, Axe 4, Lance 3, Bow 4 Power. The first three
match confirmed starting Powers (§4.1), but every report labels them fixture inputs. Axe/Lance/
Bow 4/3/4 and baseline accuracy 100 are not approved starting balance. Each side's actual
equipment, range, RNG and positions are retained for review rather than silently equalized.

The five selected executions have no unresolved interaction, but this is selection of supported
fixtures, not proof all kit interactions execute. The coverage suite's distance-1 Bow requests
reject normally: all 15 pairs are represented, only ten opening attacks complete there.

### Evidence categories

- **Tested behavior:** production formulas, counter/follow-up sequence, HP/shield accounting,
  live Fury/Winter Hunter/Sanctuary checks, successful-use budgets and immutable results.
- **Quantitative observations:** the table above, observed final states, and matched variants
  with exact input differences. A minimum-damage diagnostic shows Sanctuary can prevent 0 HP
  despite its confirmed +3 DEF.
- **Hypotheses:** the risk register below. They require more experiments and approved rules.
- **Unresolved rules:** fixture Powers, accuracy/pipeline/follow-up interpolation, shield/proc
  timing, ice, flyers, incomplete casts and geometry. Test implementation is not approval.
- **Missing measurements:** individual Fury contribution, counter/follow-up shield absorption,
  complete partial-strike logs and some persistent progression in snapshots. Report null, not zero.

## Risk hypotheses and proposed experiments

| Risk ID | Hypothesis / mechanics | Supporting observation (or absence) | Missing data | Controlled experiment before any adjustment | Would changes be premature? |
|---|---|---|---|---|---|
| B01 — opening burst | Assassin SPD plus opening bonus could concentrate damage before a response (§§4.2,12.1) | Ordinary Assassin fixture: 21 damage to mage, 12 counter damage received; suite does not exercise Shadow Strike | Canonical visibility/approach/reveal reset, approved accuracy, equipment, status/shield eligibility | Same source/target/positions and RNG, compare ordinary attack with supported visible/invisible Shadow Strike; preserve explicit approved pre-approach snapshot and legal movement separately; record counter damage received and paid action | Yes; 33 total is not Assassin-only output and says nothing about wins |
| B02 — defensive layers | Blessing + Sanctuary + shields could create steep physical survivability differences (§§11,12.2,12.5) | 2v2 Guardian takes a 10-damage opening: shield 6, HP 4; Colossus receives Blessing, not its own Sanctuary | Canonical shield cast range/cost exposure, overlaps/source-death policy, approved modifier order | Factorial matched baseline: Blessing off/on, single qualifying Sanctuary off/on, shield 0/6, fixed incoming strike; inspect HP versus shield separately and minimum-damage floor | Yes; initial shield in suite is a fixture and its application did not spend a Main Action |
| B03 — tank matchup sensitivity | High DEF and low SPD may trade per-strike reduction for additional follow-ups (§§2,4.2) | Nigel opening 4, counter 5, follow-up 3; dagger opening/follow-up versus blessed Tank are each 1 in 2v2 | Approved Lance/Axe/Bow Power, magical threat mix, positional opportunity costs | Fixed legal physical and magical attackers against same Tank HP; match Power/accuracy assumptions explicitly, compare reactions and damage floors, then compare movement-enabled positions | Yes; no effective-HP or time-to-kill estimate from isolated strikes |
| B04 — healing throughput | Single-target healing, diminishing returns and action costs may reward/support focus differently (§§3,11,12.2) | 2v2 restores only 1 because target is nearly full; 3v3 restores 11. Those are not competing throughput measurements | Healing rounding/event/no-op rules; canonical incoming damage; shield range; multiple-healer opportunities | Same injured recipient and legal sequence across one and several rounds; log calculated versus actual restoration, ordinal reset and actions spent, including misses by attackers | Yes; compare restoration to the actual incoming sequence, not cross-fixture totals |
| B05 — SPD amplification | Small SPD changes near 1.25/1.75 may cause abrupt follow-up damage jumps (§4.2) | Nigel/Colossus and Assassin/mage get differently scaled follow-ups; current interpolation is provisional | Approved intermediate curve, rounding, minimum and zero-SPD policy; Paralysis amount | Paired explicit-Speed prototype fixtures around thresholds, then registered characters with approved modifiers; separate hit chance, candidate damage and scaled actual damage | Yes until R04 is approved; do not encode a new curve in diagnostics |
| B06 — evasion reliability | Smoke Veil's confirmed 20 points plus triangle could strongly affect exchange reliability (§§4.1,12.1) | Current representative suite has no smoke and only zero rolls; it cannot quantify evasion value | Baseline accuracy/caps/terrain/visibility and automatic action integration | Under an explicit approved baseline, enumerate deterministic rolls 0–99 for one fixed strike in/out of smoke with matched inputs; no stochastic flaky tests or win-rate claims | Yes; exact frequency is conditional on chosen accuracy, not canonical balance |
| B07 — taunt counterplay | Roar can constrain a remaining activation or force End (§12.4) | Existing tests establish required target, Utility permission, first-source priority, invalid-source release and Cleanup expiry; suite uses none | Movement executor/legal attack availability, first player policy and zero-recipient case | Fixed board/activation order and ordinary Movement/Utility options; compare reachable source, valid unreachable source and invalid source; log forced ends and lost opportunities without AI choices | Yes; do not change finalized core while movement context is absent |
| B08 — area denial | Walls/ice/Sentinel zones could create unavoidable or expensive routes (§§3,12.5–12.6) | Placement/lifetime and isolated Sentinel reaction tests exist; no complete movement-enabled encounter evidence | Ice cost/provenance, wall projectile passage, path legality, special triggers, competing reactions | Caller-scripted approved legal routes through/around one zone with matched remaining MOV; record interrupted step, actions retained, HP and expiry; reject unresolved routes | Yes; pathfinding/AI or “slippery” behavior must not be invented |
| B09 — Frostbite/Winter Hunter | Existing Frostbite adds +3 before scaling and could compound with repeated Bow strikes (§12.6) | Matched tests show +3 opening and existing follow-up scaling; representative Spirit/Nigel has no status | Arrow proc eligibility/timing, terrain duration, shields, flying order and canonical Bow Power | Start with timed Frostbite fixture versus identical unstatused target; after R10 approval, compare first inflicting hit versus later strikes with deterministic proc scripts | Yes; do not retroactively give the first inflicting hit +3 |
| B10 — low-HP Fury | Nigel's current missing-HP scaling may create a survivability/offense tradeoff (§12.4) | Full/reduced/healed matched fixtures change damage; 3v3 Nigel's later opening is 18 after prior damage, not evidence of permanent accumulation | Exposed per-strike contribution, approved pipeline/equipment, ability collision/follow-up interactions | Identical attack at fixed current HP values with/without actual prior healing, fixed statuses/ranges, and same RNG; separately test confirmed Knockback Fury-before-half | Yes; no damage-history counter or new bonus formula needed |
| B11 — long Temporal Accumulation | Distinct elements and persistent stacks might change long-encounter scaling (§12.3) | Production progress tests cover persistence and previous-stack use; representative mage exchanges are not long-run stack experiments | Pool/cap and miss/interruption policy, full elemental notification/adapters, progression snapshots | Approved sequence of repeated versus distinct elements with same initial equipment and cast opportunities; inspect old-stack damage snapshot, newly awarded stacks and death persistence | Yes; do not infer a cap or “miss counts as use” from current safeguards |
| B12 — random transformation reach | Range-dependent random alternatives could bias Transmutation effectiveness (§12.3) | Production can select then return unresolved range outcome without commit; matchup runner reports missing adapter | Approved pool/filter/retarget/failure policy and metadata; selection-before-validation RNG contract | Fixed same-level catalog and indexed selection script per alternative, legal/illegal reach variants, no rerolls until approved; record selection, equipment commit, action and cooldown separately | Yes; silently filtering the pool would change probabilities |

## What is ready to measure next

Today: matched arithmetic and supported legal action variants, base versus effective stats,
per-action loss, shield absorption, ordinal healing, reaction occurrence and explicit blockers.
After decisions/integration: realistic positioning, opportunity costs, proc timing and long-form
kit interactions. Competitive evaluation also needs canonical equipment/accuracy, objective
incentives and reproducible legal movement. More deterministic scripts alone cannot supply them.

No strongest/weakest character, win rate, statistical significance, causal balance verdict or
numerical adjustment is claimed by this review.
