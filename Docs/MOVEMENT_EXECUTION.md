# Ordinary movement execution — Stage 6.6

`MovementActionExecutor` is the production boundary for an ordinary movement action. It lives
in `Scripts/Core/Gameplay/` under namespace `PawnsOfDivinity` and has no Godot dependencies.
It coordinates existing systems; it does not calculate attacks, find paths, or own a second
copy of position, health, activation budgets, or status durations.

## Authority and public API

| Component | Responsibility |
| --- | --- |
| `Character.TacticalPosition` | Authoritative current position; the executor calls `SetTacticalPosition` only after validation |
| `Character.EffectiveMovement` | Current MOV allowance, including the existing Frostbite penalty |
| `ActivationManager` | Active actor and the one unsplit Movement opportunity; Utility and Main budgets remain independent |
| `RoundLifecycleManager` | Current round and phase; holds the one registered movement executor for this match |
| `IOrdinaryMovementMap` | Bounds, walkability, supported transition costs, occupied-tile policy, and Sentinel map information |
| `WailingRampartService` / `SpiritFrostService` | Existing created-terrain passage restrictions |
| Sentinel tracker, evaluator, and resolver | Prepared reactions, trigger eligibility, combat, interruption, and event replay protection |

Construct one executor per lifecycle with that lifecycle's activation manager, Sentinel tracker,
terrain services, map adapter, combat resolver, and `IMovementReactionInputs`. Constructor
validation rejects mismatched activation/Sentinel lifecycles and a replacement executor.
Map and terrain dependencies must belong exclusively to the same match; their interfaces do
not themselves carry a universal match ID. Use one match thread. Callbacks must only inspect
state, not change it or re-enter execution. There is a re-entry guard, not a concurrency engine.

`MovementActionRequest(id, actorId, round, path)` copies the ordered path, including the
origin and at least one proposed step. IDs must be nonblank and the round positive. It contains
no client-computed cost. The executor resolves the actor from ActivationManager's actual roster.
An empty, stationary, disconnected, stale-origin, or diagonal route is rejected without movement.

`Preview(request)` validates and reports `Ready`, rejection, an unresolved rule, or a provider
failure. `Execute(request)` repeats validation against current state before committing anything.
Neither API trusts an earlier preview. Both return the same immutable `MovementActionResult`
shape; `Ready` is advisory, never a completed action.

## Confirmed rules versus explicit policies

Specification v0.2 §3 confirms orthogonal movement, normally up to MOV, as one unsplit
opportunity, independent of the Utility Action. A completed Main Action ends activation,
so movement is unavailable afterward. The executor requires Alternating Activations, a living
active actor, the current round, and an unused Movement opportunity.

The specification and Stage 6.4 decision queue do **not** approve a universal terrain/height
cost, allied crossing rule, or interrupted-Movement consumption rule. This stage does not
finalize them. The supplied map explicitly identifies `MovementRuleAuthority.Confirmed` or
`ProvisionalFixture`, plus a nonblank policy description. That label is the configuration
provider's assertion, not an approval created by the executor.

`GetStepCost` must return a positive cost only for a supported transition, accounting for all
required terrain/height information. A null return means unresolved; zero or negative costs
are invalid policy data. Sum the actual path costs in checked `long` arithmetic and compare
with `EffectiveMovement`; endpoint Manhattan distance is not a substitute for route cost.
No unit cost, terrain multiplier, height threshold, flight exception, or diagonal permission
is embedded in the production executor.

The scenario adapter requires explicit `UseProvisionalFlatMovement = true` before supplying
unit costs and blocking living occupied traversal. This is a **test-only flat-grid policy**.
By default, its ordinary movement cost is unresolved. Sentinel-capable movement additionally
requires an explicit `InterruptedMovementConsumesOpportunity = true` policy. Null or false
does not authorize a free interrupted segment; that configuration remains unsupported.
No canonical interruption-consumption policy has been approved.

## Route, occupancy, and terrain validation

The whole proposed route is checked before execution: authoritative origin, map legality of
every coordinate, orthogonal adjacency for every step, occupancy, existing wall/ice behavior,
supported costs, allowance, event identities, and prospective Sentinel inputs.

Occupancy reads the managed characters' current `TacticalPosition`, excluding defeated units
as existing scenario occupancy does. Ending on another living character is rejected. Occupied
intermediate tiles are rejected when the supplied policy blocks crossing, or unresolved when
crossing is unknown. Even an explicit crossing permission currently returns
`OccupiedCheckpointInterruptionUnresolved`: a reaction or failure could stop on the occupied
tile, and this stage does not invent shared occupancy or an alternative stopping tile.

Enemy Wailing Rampart tiles block ordinary movement; allied walls retain their existing
passage rule. Allied Spirit Frost retains ordinary movement. Enemy ice returns
`TerrainFrostbiteRulesUnresolved` before mutation; no entry cost, sliding, double MOV penalty,
or automatic Frostbite is introduced. Smoke is not treated as a wall or a new movement cost;
smoke ownership and expiration remain unchanged. Map adapters must still supply any other
required terrain legality. Teleportation and forced displacement keep their separate APIs.

## Stepwise commit and Sentinel reactions

The executor first validates the full path, including prospective range-entry reactions.
It uses small internal projected-position preview helpers in the **existing** Sentinel and
Sanctuary evaluators, rather than temporarily moving characters or duplicating their formulas.
This also checks position-dependent Sanctuary failures before a planned reaction step commits.
The projection exists only for read-only validation; actual combat reads live positions.

Execution then rechecks each step and its reaction context. Before the first coordinate change,
it records the one Movement opportunity through `ActivationManager.RecordMovement`. It does
not spend another opportunity per tile. Once movement has begun, the opportunity cannot be
split or resumed through another ordinary request. For potentially interruptible movement,
this reservation requires the explicit consumption policy above.

Each committed adjacent step updates `Character.TacticalPosition` and publishes one uniquely
identified ordinary `SentinelMovementEvent`. Reactions use the existing Lance configuration,
counter availability, accuracy provider, physical/effective-stat calculations, shields and HP.
`IMovementReactionInputs` must supply actual current Lance and combat inputs; the executor
does not invent Power, accuracy, or unarmed values. Missing or invalid combat configuration
blocks execution before any movement. Requests disable no existing counter rule and preserve
Sentinel's established no-follow-up behavior.

Potential competing Sentinels return an unresolved ordering result; collection order never
selects a winner. The existing explicit `ProvisionalOnePerPreparation` opt-in remains required.
A hit stops at the entered tile; a miss allows further validated steps. Defeat stops movement.
A surviving interrupted mover retains its existing Main/Utility availability; no attack or
activation completion is selected automatically. No teleport/knockback reaction is inferred.

Preflight is conservative: it checks all proposed checkpoints, including ones a future random
hit might make unreachable. It does not predict hits to bypass a later unsupported interaction.

## Results, failures, and replay

`MovementActionResult` captures request/actor IDs, start/requested/final positions, approved
and traversed paths, approved and traversed cost, survival, action consumption, policy provenance,
possible reaction sources and actual immutable reaction observations. Paths are copied/read-only.
The traversed path includes the origin only once execution starts; a rejected or previewed route
does not claim traversal. `MovementConsumed` means consumed by **this invocation**, not that
the actor necessarily has Movement available afterward.

Reaction observations include source and target HP/shield loss. If a later roll fails, the
observation preserves actual damage and its failure text with no fabricated completed exchange.
The final position is always the actual runtime coordinate. Earlier damage and steps stay applied.

Outcomes distinguish `Ready`, `Completed`, `Rejected`, `UnresolvedRule`, `Interrupted`,
`PartiallyApplied`, `AlreadyProcessed`, and `Failed`. A provider exception before gameplay
mutation is a failure; ordinary malformed combat inputs are rejected. Explicit unsupported
gameplay remains unresolved. A failure after Movement has been reserved is partial, even if
the requested destination was not reached. There is no rollback or automatic recovery.

Validated execution reserves a request ID before the first commit. Repeating that request
returns `AlreadyProcessed` with its prior outcome and no new traversal, action consumption,
or reactions. Changing its actor, round or route under the same ID returns
`RequestIdentityConflict`. A partial attempt cannot resume. Pre-execution rejections do not
reserve an ID, so correcting invalid inputs is possible. Sentinel event IDs are derived from
length-delimited request IDs and step indexes and checked before committing the route.
Existing published IDs cannot be republished. The lifecycle prevents a replacement executor
from bypassing request history. Different matches maintain independent histories.

Direct setup/teleport/forced-movement APIs remain available. This guard does not claim that
arbitrary out-of-band calls to `SetTacticalPosition` are player-authorized movement.

## Scenario integration and verification coverage

`ScenarioActionKind.Movement` uses `Tiles` as the complete explicit route and invokes this
production executor through `ScenarioRunner`. `CombatScenario.Movement` exposes the registered
match executor. Results retain the immutable production movement result; snapshots still read
the actual Character. Events report committed steps followed by actual reactions, observed
shield/HP losses, and interruption. Unattributed observations exclude already-reported reaction
damage so round metrics do not count it twice. No random outcome is predicted during Preview.

`ConfirmedMovement` remains a separately labeled **external fixture bookkeeping** facility for
older tests. It records a supplied opportunity only and does not change coordinates. It is not
reported as executed ordinary movement. Multi-round scripts may continue with explicit legal
actions after an interrupted move; unresolved/partial/failed actions still stop the script.
Existing preparation restores opportunities next round without changing position.

`MovementActionExecutorTests` covers route/actor/phase/budget validation, malformed inputs,
cost and occupancy boundaries, Frostbite allowance, stale previews, immutable snapshots,
replay/re-entry ownership, independent matches, Sentinel hits/misses/defeat/competing sources,
missing combat data, projected Sanctuary failure, and failures before/after actual damage.
`MovementScenarioTests` covers movement followed by a real attack, walls, current occupancy,
allied ice, unresolved enemy ice, smoke preservation, configured bounds, Sentinel interruption,
and movement in successive production-prepared rounds.

## Remaining decisions and the Godot bridge

Still unresolved: canonical flat/terrain/elevation costs, allied traversal and occupied
interruption handling, interruption consumption, enemy ice/Frostbite entry semantics, multiple
Sentinel priority, and special movement interactions. Full canonical ordinary movement is
therefore not approved merely because explicit fixture scenarios now execute successfully.
No specification edits or new mechanics are included.

The specification's board is **14×14**; the Godot prototype's `TacticalMap.Size` remains **16**.
The prototype also animates `TacticalUnit.Cell` independently. This stage changes neither.
For the subsequent Godot integration work (the requested Stage 6.7 boundary), bind visual units
to existing Character instance IDs, use specification-aligned configured bounds, translate
coordinates through an adapter, and animate only the returned traversed path/final position.
Supply approved walkability/occupancy/height/cost and reaction context through the core map
interface. The UI/pathfinder may propose a path; it must not authorize it or update a parallel
position before core execution. Rejected/unresolved/partial outcomes must remain visible to
the caller. This document does not authorize that next stage or change the earlier roadmap.
