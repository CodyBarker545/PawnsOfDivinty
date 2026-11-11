# Stage 6.4 readiness summary

Analysis/diagnostic stage only. No production gameplay, authoritative specification, balance
value, ability effect, Godot scene or earlier report was modified. Prior uncommitted work was
preserved; nothing was committed. Review completed after inspecting v0.2, current core/prototype
code and the readable/structured Stage 6.3 reports.

## Verification

- Entry baseline: **2,781 passed**, 0 failed, 0 skipped; build 0 warnings/errors.
- Added: **19 behavioral diagnostic cases** in `CombatReadinessTests.cs`.
- Final regression target/result: **2,800 passed**, 0 failed, 0 skipped.
- Final build: 0 warnings, 0 errors. Diff/new-file whitespace checks: passed.

The final figures are recorded after the requested verification commands. Test count is not a
claim that every gameplay mechanic, map interaction or registered ability can execute.

## Readiness verdicts

| Question | Verdict | Evidence and limits |
|---|---|---|
| Ready for isolated combat testing? | **Yes, with explicit inputs and labels** | Production formulas, current effective stats, triangle, hit rolls, shields, reactions and exposed passives can be measured. Low-level comparisons do not prove a legal player action or full encounter |
| Ready for scripted multi-round simulation? | **Yes, for the supported subset** | Actual lifecycle/processors, explicit activation choices, damage persistence, cooldowns, Poison, delayed effects, statuses and expiry work. Shop/Other Triggers/scoring are no-op acknowledgments; unsupported actions and partial failures stop honestly |
| Ready for meaningful competitive balance evaluation? | **No** | Missing canonical Power/accuracy, provisional composition/follow-up policies, missing real movement/positioning economy, restricted kits, no objective/economy loop and incomplete observations. Matched local experiments are valuable but narrower than competitive balance |
| Ready for playable Godot battlefield integration? | **Not as an authoritative playable combat loop** | Existing visuals/pathfinding exist, but core movement/Cleanup execution and identity/coordinate bridge are missing. Godot uses a separate 16×16 visual grid and cell state versus approved 14×14/core Character position. Read-only visual/debug binding could be developed earlier without claiming loop readiness |
| Ready for a complete 6v6 match? | **No** | Three-objective capture/scoring/victory unresolved/unintegrated; shared inventory/shop, normal death loot/buyback/redeployment missing in core; complete kit/map interactions incomplete. One visual shrine is not canonical three-objective match logic |

## What is implemented

Six registered immutable eight-stat definitions and independent factory-created runtime
characters; HP/shield separation and local defeat; base physical STR/DEF and magical INT/RES;
triangle; explicit-input accuracy and independent injectable rolls; ordinary exchange execution
with counter/follow-up eligibility; bounded healing, history and diminishing returns; timed
statuses/Poison/cleanse; action budgets, approvals and absolute cooldowns; lifecycle and round
preparation; guarded Poison/delayed/status batches; match-scoped terrain/effect services;
immutable deterministic scenario/matchup evidence and honest failure classification.

Several rows are implemented only at a **specified component boundary**. For example physical
base arithmetic is confirmed and complete while universal modifier ordering is not; shield HP
absorption is complete while canonical Sanctuary Shield casting range is not.

## Character readiness, not registration count

- **Assassin:** combined Smoke Bomb placement/teleport executor exists; Shadow Strike uses
  approved pre-approach visibility and separate movement. Standalone teleport cost, cloud-network
  permissions, reveal reset and global smoke-aware ordinary targeting/evasion are incomplete.
- **Guardian:** injured-ally Healing Light and finalized Gentle Blessing work. Shield management
  grant/refresh/transfer works, but canonical Shield cast range is unresolved; spell metadata,
  full-health/self-heal and distinct shield sources need decisions where applicable.
- **Time Weaver:** stored Ignition scheduling/resolution works; cast range is absent. Full
  Transmutation production subset exists but selected out-of-range outcomes/catalog context
  and the scenario adapter are incomplete. Previous-stack damage/persistence works; element
  pool/cap/miss/interruption and broader use notifications/snapshots remain limited.
- **Nigel:** finalized Roar core and live Fury work. Empty Roar/spell tag remain open. Knockback
  core damage/no counters/unobstructed push works; confirmed collision 3 is not complete collision
  geometry, and Speed follow-ups/stopping positions remain unresolved.
- **Colossus:** Rampart placement/passage API/expiry and ordinary-step Sentinel reaction subset
  work. Projectile/overlap/special movement/range-2 reactions need rules/integration. Sanctuary's
  exact single-source living condition works; overlap and qualifying source-death scope block.
- **Spirit Guardian:** empty/allied-only ice placement and expiry work. Enemy terrain Frostbite
  duration/cost/provenance and Arrow shield/lethal/reaction/flying sequencing remain restricted.
  Winter Hunter's +3 against already Frostbitten targets is integrated before follow-up scaling.

## What the matchup evidence establishes

All **15 unordered pairings** are representable. At distance 1, **10 opening player attacks
complete and five Bow openings reject adjacent targeting**. They are not 15 completed battles.
The representative suite uses explicit Power 5/3/4/4/3/4 (Sword/Light/Scroll/Axe/Lance/Bow),
baseline accuracy 100 and roll 0. Axe/Lance/Bow Power and accuracy remain fixture choices.

| Representative script | Completed Main Actions | Actual total HP loss | Shield loss | Healing |
|---|---:|---:|---:|---:|
| Nigel–Colossus | 1 | 12 | 0 | 0 |
| Assassin–Time Weaver | 1 | 33 | 0 | 0 |
| Spirit Guardian–Nigel | 1 | 13 | 0 | 0 |
| Defensive 2v2 | 3 | 25 | 6 | 1 |
| Full-roster 3v3 | 5 | 120 | 0 | 11 |

Totals include both teams' losses and reactions. The 33 figure includes 12 mage counter damage;
it is not 33 Assassin damage. Different Main Action counts, distances, shields, target HP and
formation make cross-row strength comparisons invalid. The 2v2's initial shield was fixture
setup, not a paid canonical Shield cast. No win rate, ranking or strongest/weakest conclusion.
The new diagnostics compare persisted JSON figures with fresh typed executions, including
health conservation with recorded healing. Missing contribution/partial-strike data stays unavailable.

## Highest-priority blockers

1. **Authoritative movement and state bridge:** no player-facing core path execution boundary,
   MOV/terrain/occupancy/reaction coordination, or single core-to-Godot unit identity binding.
2. **Production Cleanup orchestration:** reusable expiry APIs exist, but only the harness
   coordinates them with a shared attempt guard. Do not let rendering/animation own expiry.
3. **Canonical combat input decisions:** starting Axe/Lance/Bow and unarmed configuration,
   accuracy, ordinary modifier order and provisional follow-up details.
4. **Frequent kit blockers:** spell tags, Shield/Ignition ranges, ice provenance, Arrow sequencing,
   Sanctuary overlap/death scope, selected-scroll reach and blocked-push/reaction geometry.
5. **Full-match systems:** objective/economy/loot/buyback integration and missing design values.

The §12.5 dead-source persistence parenthetical needs scope clarification, not a silent change
to already confirmed terrain persistence. The visual prototype's 16×16/single-shrine behavior
does not override v0.2's settled 14×14/three-objective direction. Existing finalized Roar,
Blessing, Knockback core and Shadow Strike timing must not be reopened as new questions.

## Recommended next work

**Stage 6.5: production Round Cleanup foundation**, scoped to the existing independent
expiration APIs, shared lifecycle guard and immutable completion/failure report. This can be
implemented without inventing priorities for future interacting triggers. It removes a known
test-only authority before Godot integration while movement decisions are reviewed.

**Then movement execution:** approve a narrow path/occupancy/cost contract, add a pure-core
MovementActionExecutor using existing Character/ActivationManager/map/reaction APIs, and only
then bridge Godot units/coordinates. The existing pathfinder may propose a route; it cannot
approve core budgets, teleport rules, Sentinel or terrain costs by itself.

Approve one small gameplay bundle at a time from `DesignerDecisions.md`. The first useful
answers are D01–D06: path/turn policy, equipment, accuracy, ordering/follow-ups and missing
spell/target metadata. Do not implement every backlog row in one task.

## New files and diagnostic coverage

Created documents:

1. `CombatReadinessReport.md` — mechanic-by-mechanic inventory with spec status, real APIs,
   tests, supported conditions, limitations and prototype impact.
2. `UnresolvedRules.md` — R01–R35 prioritized rule register plus separate E01–E06 engineering gaps.
3. `BalanceRiskReview.md` — observed evidence versus hypotheses, missing data and proposed experiments.
4. `NextImplementationStages.md` — dependencies/deliverables/tests and minimum movement/Cleanup designs.
5. `DesignerDecisions.md` — unapproved options/tradeoffs and a short prioritized decision queue.
6. `Stage6_4_ReadinessSummary.md` — this verdict and verification record.

Created `CombatReadinessTests.cs`: six factory/registration cases, 15-pair availability,
supported production action accounting, forced-transition/map requirements, unsupported
movement, unknown equipment, Sanctuary overlap/dead-source scope, unresolved Shield range,
Silence metadata, missing Transmutation adapter, partial replay protection, shielded Arrow
blocker with explicit provisional-duration opt-in, and stored-report/live-metric consistency.

No production gameplay or existing test infrastructure was modified. The diagnostics record
current boundaries rather than approving provisional choices. When an actual designer decision
changes a boundary, update its diagnostic and reference deliberately.
