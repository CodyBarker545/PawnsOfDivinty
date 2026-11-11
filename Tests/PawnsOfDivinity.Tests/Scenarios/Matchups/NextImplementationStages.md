# Recommended implementation sequence after Stage 6.4

These are proposed stages, not authorization to implement them. Keep each stage small, reuse
current architecture, add deterministic xUnit coverage, run the full suite/build/diff check,
explain the public API and stop for review. No gameplay default in the decision questionnaire
is approved here. Priorities refer to `UnresolvedRules.md`.

## Recommended Stage 6.5: production Round Cleanup foundation

**Why first:** movement is the largest playable-loop blocker, but its legal path/terrain contract
needs design input. Cleanup can be moved out of test-only orchestration using already-existing
expiry contracts while those decisions are reviewed. This is a bounded engineering stage, not
a reason to postpone movement or invent Cleanup-trigger priorities.

| Item | Proposal |
|---|---|
| Objective | One match-owned production coordinator for existing independent Cleanup expirations |
| Dependencies | `RoundLifecycleManager`, roster/ActivationManager, current modifier/terrain/taunt/Sentinel services |
| Likely files | Proposed `Scripts/Core/Turns/RoundCleanupCoordinator.cs` and immutable result; minimal lifecycle guard extension; scenario runner adapter; focused turn tests |
| Tests | Wrong phase, wrong roster/lifecycle, duplicate/recreated coordinator, empty cleanup, staggered expirations, owner death, shield/HP/status/deadline preservation, partial-failure reporting and no retry |
| Deliverable | A production authority both Godot and test harness can invoke once; test-only Cleanup no longer owns gameplay orchestration |
| Designer decision before coding | None for existing independent expiry deadlines. If interacting triggers or ambiguous source-death gameplay are encountered, stop that case explicitly; R28 does not permit a universal invented order |

### Minimum Cleanup contract, not an implementation

1. Match-scoped dependencies share the same lifecycle and exact production roster. No static
   global service or second round counter. Coordinator owns processing, caller owns phase advance.
2. Require current Cleanup phase and stable round. Validate all predictable input/ownership/
   arithmetic conditions before state changes. Do not run start-of-round timed-status processing.
3. Reuse `TemporaryStatModifiers.ExpireAtRoundEnd`, `WailingRampartService.ExpireAtRoundEnd`,
   `SpiritFrostService.ExpireAtRoundEnd`, `SmokePlacementService.ExpireAtRoundEnd`,
   `ActivationManager.ProcessTauntCleanup` and `ImpalingSentinelTracker.RetireInactivePreparations`.
4. Confirmed boundaries: Roar and Sentinel finish in application-round Cleanup; terrain and
   Blessing last through the following round's Cleanup; ordinary shield HP is not round-expired.
   Timed negative statuses decrement at round start after damage, never in this batch.
5. Existing services are independently idempotent. Prefer an explicit immutable `AlreadyProcessed`
   result for a repeated successful coordinator call. The completion/attempt guard must belong
   to the lifecycle/match so a replacement coordinator cannot bypass it. Do not silently execute twice.
6. A prevalidated deterministic traversal of independent expirations is an engineering choice,
   not a settled order for future interacting end triggers. The specification confirms the phase
   boundary, not a universal order among every effect. Unsupported interdependencies remain explicit.
7. Reserve attempted execution before the first irreversible operation. If an unexpected error
   occurs, report completed suboperations and actual snapshots, mark the attempt incomplete,
   and prevent automatic retry. No rollback framework or invented death/loot/objective processing.
8. Return immutable expired IDs/source IDs/recipient IDs, previous/resulting snapshots and outcome.
   Do not change HP, shields, healing history, cooldowns or activation budgets as incidental cleanup.

## Core playable-loop work

| Proposed stage | Objective / deliverable | Dependencies and likely systems | Required tests | Designer prerequisite |
|---|---|---|---|---|
| 6.6 — ordinary MovementActionExecutor foundation | Execute an explicit supplied legal path through the existing Character, one unsplit opportunity and supported reactions | R06 approved scoped path/cost/occupancy contract; Core grid/Character, ActivationManager, map contract, wall/ice checks, Sentinel evaluator/resolver, immutable movement results | Actor/phase/budget, path start and orthogonal steps, total approved cost, occupied/blocked tiles, stale preview, hit/miss reaction interruption, defeat, no resume, no duplicate event/command, unknown context, actual partial position | D01 plus any included ice/reaction rules. Start with explicitly supported flat/ordinary paths; return unresolved for others, do not infer prototype height constants |
| 6.7 — approved combat configuration and turn policy | Configure missing starting Power, unarmed parameters, accuracy/caps, metadata and approved follow-up/composition policies without parallel formulas | Designer answers D02–D06; existing calculators/definitions/configuration, AbilityEligibility, ActivationManager, tests | Approved boundary values and intervals, mixed modifiers, zero SPD, Silence classifications, first-team/exhaustion behavior, unchanged existing confirmed rules | R01–R04,R12,R14,R27 require decisions. Split this proposal into one approved small coding task per independent change |
| 6.8 — support/terrain rule completion | Make Shield/Ignition casts and enemy ice behavior executable under explicit approved targeting/provenance | D06–D09; existing support/Ignition executors, StatusTracker, map/movement boundary, Sanctuary context | Legal/illegal self/ally/enemy/range, shield ownership/overlap, duplicate healing/history, ice entry/leave/expiry without double penalty, source-death/overlap cases | R08–R16,R25 as applicable. Do not broaden one approval into all listed mechanics |

### Minimum movement execution contract, not an implementation

Inputs: actor and activation identity, expected start position, selected destination, explicit
ordered path, current map/occupancy evidence, approved cost and terrain rules, round/lifecycle
and stable command/event IDs. A UI or existing pathfinder may propose a path; the core validates
it. No new pathfinding algorithm, AI, elevation value or arbitrary terrain multiplier is needed.

Preview should be non-mutating and consume no RNG. Validate current living actor, activation
phase, unused Movement, exact source position, path/destination consistency, orthogonal steps,
legal traversal and destination occupancy. Sum only approved costs safely and compare against
current `EffectiveMovement`. Return unknown-rule context as unresolved, not “walkable by default.”
Do not substitute Manhattan source/destination distance for the actual path cost around blockers.

Execution must revalidate current state and record one movement opportunity through existing
ActivationManager accounting. Define commit/decline/interruption timing from the approved
one-opportunity contract; do not create free partial movement or a second budget. Recheck each
step if a reaction changes the world. Update **Character.TacticalPosition**, not a second unit
position store. Publish supported committed adjacent ordinary movement evidence to Sentinel;
reuse its reaction resolver, current visibility/equipment and provisional policy only if opted in.
A hit stops further normal movement while the surviving mover retains its Main Action; a miss
permits continuing the approved path. Do not manufacture teleport or Knockback triggers.

Return old/requested/final positions, actual committed path, approved/consumed cost evidence,
movement opportunity outcome, triggered reaction results, interruption/defeat and explicit
unresolved/partial reasons. Reserve command and reaction identities so a later failure cannot
replay damage or committed steps. No transactional rollback infrastructure. A move interrupted
after one step must report that actual position; a rejected preflight changes none.

The current APIs do not provide all of this. In particular `ForcedMovementService` commits a
caller-resolved displacement and `SmokeTeleportService` commits a teleport; neither is an
ordinary movement action executor. `RecordMovement` records accounting only. Prototype
`TacticalUnit.MoveTo` animates a visual path without core MOV/activation authority.

## Ability completion and observability

| Proposed stage | Objective / deliverable | Dependencies and likely systems | Required tests | Designer prerequisite |
|---|---|---|---|---|
| 6.9 — supported full exchange effects | Complete Arrow proc/reaction sequencing, approved flying effectiveness and bounded Knockback/Sentinel geometry | Existing per-ability executors and exchange/movement result contracts, no new damage engine | Shield-only/lethal hits, opening/counter/follow-up eligibility and exact rolls, no retroactive Winter Hunter, special movement unresolved where not approved, no duplicate collision/reaction | D04/D05/D09/D12 and flying decisions; separate Arrow, flying, Knockback and Sentinel tasks |
| 6.10 — remaining action adapters and progression | Adapt supported production Smoke Bomb/Shadow Strike/Transmutation/Knockback operations to harness; complete approved reveal/scroll/element rules | Explicit map/visibility/catalog/equipment inputs; existing ability finalization; temporal progression snapshot projection | Preview/no RNG, stale context, selected out-of-range scroll, committed equipment then exception, approved attack survives target death, persistent stacks and explicit unavailable metrics | D10/D11 and any R22 empty-Roar ruling before enabling those cases; adapter-only supported subsets need no new gameplay approval |
| 6.11 — battlefield visibility and measurement | Add approved LOS/elevation/visibility adapter and expose needed immutable result details | Approved map rules; current SmokeVisibility/Veil/AbilityTargeting; exchange results | Smoke-aware ordinary targeting/evasion, tile AoE distinction, legal visibility changes after movement, precise shield attribution and partial logs without double counting | R07/R26 if implementing gameplay; result instrumentation itself is engineering |

## Designer approval work, in parallel with engineering

| Decision bundle | Scope | Deliverable before dependent coding |
|---|---|---|
| D01–D03 | Minimal ordinary path/occupancy/turn initiative; starting/unarmed equipment; accuracy | Explicit approved values/permissions for a playable flat-grid slice, with unsupported features named |
| D04–D05 | Modifier order and follow-up rounding/interpolation | Numeric examples at threshold, minimum damage, flat/percent interaction and zero Speed |
| D06–D09 | Spell tags/cast targets, shield/aura semantics, ice, status sequencing | Per-ability metadata and event/lifetime examples; no inferred defaults |
| D10–D12 | Smoke/reveal, scroll outcomes/progression, reaction/collision behavior | Bounded resolved cases and cases that must remain blocked |
| D13 | Objective/economy/full match | Verify previous design records, then approve only genuinely missing choices |

These questions concern gameplay, not whether to use a C# enum, coordinator or record.

## Playable Godot prototype, after authoritative action boundaries

| Proposed stage | Objective / deliverable | Dependencies and likely systems | Required tests | Designer prerequisite |
|---|---|---|---|---|
| 6.12 — minimal Godot/core bridge | Bind rendered units to the same factory-created Character and approved 14×14 coordinates | Movement/Cleanup/action APIs; `Battlefield`, `TacticalMap`, `TacticalUnit`, core grid conversion | Coordinate round trip/bounds, identity/team preservation, no duplicate health/position, visuals reflect approved and interrupted results | None for the settled 14×14 size; rendering/content migration is engineering. Do not promote current 16×16 prototype into a new rule |
| 6.13 — action-selection vertical slice | Select actor, preview move/attack/supported ability, execute explicit command, show log/HP/shield/status/round HUD | Core bridge and approved ability subset | Rejected previews have no effects; no budget/cooldown paid by selection; one execution despite animation/UI repeat; no advancement from animation callbacks | Only included gameplay must be approved; no AI or forced tactical choices |
| Later full-match stages | Shared inventory/shop, normal death drops/salvage, buyback, objective capture/scoring/victory and eventually complete 6v6 | §§8–10,13,18 plus D13, authoritative movement/round APIs | Inventory ownership/quantity, one drop not double charge, approved rounding, original respawn/random ties, phase/order, objective/victory tests | Known normal-case rules already C; obtain missing prices/capture/threshold/edge policies only |

## Stop/go gates

- A combat-only Godot sandbox can precede economy/objectives if it is labeled as such; it is
  not the §18 complete prototype or a complete 6v6 match.
- Approve no stats changes until canonical assumptions and realistic positioning experiments
  exist. Stage 6.3's exports remain regression evidence, not balance rankings.
- Do not combine this backlog into one implementation. The recommended next prompt is the
  bounded Stage 6.5 Cleanup foundation, with D01–D06 reviewed in parallel for movement/combat.
