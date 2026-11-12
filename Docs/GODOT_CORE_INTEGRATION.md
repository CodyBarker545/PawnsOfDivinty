# Godot/core battlefield integration — Stage 6.7

The existing `Scenes/Battlefield.tscn` now hosts a local, two-player development encounter
backed by production Character instances. It is an integration slice, not a complete match.
There is no AI, shop, inventory, ability-selection UI, objective scoring, victory, or redeployment.
Specification v0.2 is unchanged.

## Ownership and the bridge classes

`BattlefieldSession` is a match-scoped, plain C# controller in `Scripts/Gameplay/Battlefield/`.
It creates actual registered characters through CharacterFactory and owns one lifecycle,
ActivationManager, MovementActionExecutor, AttackActionExecutor, combat/RNG chain, terrain
services, Sentinel tracker, delayed queue, and production Cleanup coordinator. No static match
state is shared. Character objects still own HP, shields, statuses, tactical position and cooldowns.
The character/equipment dictionaries are read-only to callers; contained Character instances
remain the existing live production objects, not cloned presentation state.

`BattlefieldUnitBinding` holds a session and a stable character instance ID. Its `Capture()`
returns an immutable `BattlefieldUnitState`: identity, team, position, HP/max HP, shields,
effective Movement, statuses and activation eligibility. Old snapshots do not change later.
`BattlefieldSession.Select()` changes inspection only; `StartSelectedActivation()` explicitly
asks the existing ActivationManager to select that eligible character.

`TacticalUnit` has been adapted as the lightweight view rather than introducing a parallel unit
class. Its `Cell` is a read-only projection of the binding's current core position. Its queue
contains only coordinates still to animate, not authoritative movement state. Refresh reads live
HP/shields and updates the label. The existing Assassin model/idle/walk animation is retained;
other registered characters use labeled team-colored capsule placeholders, without reassigning
unrelated prototype models to new identities. Colliders retain identity-based mouse selection.

`Battlefield` owns input modes, the proposed route, highlights, camera, animation requests and
HUD. It never calculates damage, tracks its own health, spends a second budget, or authorizes a
path. The old local shrine-capture calculation is disconnected. Its scene meshes and the level's
spawn markers remain intact as authored scenery; they do not determine core objectives or spawns.

## Board coordinates and the existing level

`BattlefieldGrid.Size` is the one board-dimension constant: **14**. Logical cells are zero-based
`(0,0)` through `(13,13)`. The board remains centered at world `(16,16)` in X/Z, with 1.25 world
units per cell. Cell centers use:

`worldX = 16 + (cellX - 6.5) × 1.25`

and the same expression for cell Y to world Z. World-to-cell uses half-open tile boundaries;
the overall footprint is X/Z `[7.25,24.75)`. Both directions share the pure `BattlefieldGrid`
functions so xUnit can test the mapping without loading Godot.

The former 16×16 grid used the same center and scale. Keeping the middle 14 cells preserves
their exact existing world locations; the Terrain3D data, bridges, props and terrain geometry
were not scaled. TacticalMap continues sampling the authored Terrain3D height, bridge decks,
water/finite-height tests and blocking props. Grid corners and moving visuals follow that same
surface. RayCell rejects out-of-bounds cells. The grid overlay loops over the configured board.

The reproducible encounter uses six distinct, checked cells near the existing central causeway:

| Instance | Registered character | Team | Cell |
| --- | --- | --- | --- |
| a_assassin | Hooded Assassin | 1 | (4,6) |
| a_guardian | Gentle Guardian | 1 | (3,6) |
| a_spirit | Beothuk Spirit Guardian | 1 | (3,7) |
| b_nigel | Nigel | 2 | (8,7) |
| b_weaver | Time Weaver | 2 | (9,7) |
| b_colossus | Gravebound Colossus | 2 | (8,6) |

Construction rejects unknown definitions, duplicate IDs/cells, illegal cells, missing teams and
unsupported team IDs. Injectable terrain, encounter and RNG make the bridge reproducible in tests.

## Terrain adapter and development assumptions

TacticalMap implements `IBattlefieldTerrain`: read-only walkability, height and the existing
prototype transition test. `BattlefieldMapAdapter` translates that evidence into the core
movement and Sentinel contracts. Occupancy always reads current living Characters' tactical
positions, including immediately after a core move and before visual animation finishes.
The existing wall and ice services remain authoritative for their own restrictions.

Canonical terrain/elevation costs are still unresolved. The following **development-only**
exports are explicitly enabled in the battlefield scene and displayed in the HUD:

- `DevelopmentMovementPolicy`: use the existing prototype transition checks (including its
  0.8 center-height and 0.4 sampled-transition thresholds), unit cost per approved step, and
  block occupied traversal. These thresholds are not newly confirmed game rules.
- `DevelopmentOpenTargeting`: permit the development attack-map check without a new line-of-sight
  or elevation algorithm. Production range/team/life/action checks still apply. Existing smoke
  invisibility restrictions remain enforced through SmokeVisibility.
- `DevelopmentInterruptedMovementSpent`: explicitly opt into spending the unsplit opportunity
  when a supported Sentinel reaction interrupts it. Disabling this leaves that case unresolved.

The properties default off in the script; scene data opts in. Turning movement policy off returns
unresolved costs rather than assuming free terrain. Missing/nonfinite height or unsupported
transitions remain unresolved even with the fixture enabled. Enemy ice/Frostbite entry and
multiple-Sentinel priority remain unresolved; no sliding or new terrain penalty is introduced.

Development equipment lives only in the session: Sword Power 5/range 1, Light Power 3/range 1–2,
Fire Scroll Power 4/range 1–2, **fixture** Axe/Lance/Bow Power 4/3/4 with ranges 1/1/2–3, and
**fixture** accuracy 90%. Confirmed Sword/Light/Fire values are reused, not changed. The unresolved
Axe/Lance/Bow balance and universal baseline accuracy are not written into definitions. Existing
provisional follow-up scaling remains in production combat. No unarmed fallback values are invented.
First team is explicitly fixed to team 1 for this development encounter, not a canonical initiative rule.

## Input, movement and visual reconciliation

1. Click a unit to inspect its production identity/state. Opponents and defeated units remain
   inspectable. Selection alone never consumes activation.
2. During Alternating Activations, press **Start Activation** on an eligible next-team unit.
3. Press **Move**, then click individual neighboring tiles to build an explicit route. The UI
   appends clicked coordinates, including invalid proposals, and calls `Movement.Preview()`;
   it does not independently decide legality or run pathfinding. The origin is included.
4. Blue/gold route markers mean the current production preview is Ready; red marks a rejected
   or unresolved proposal. The HUD shows its reason and confirmed cost/allowance. Clear Path
   cancels/restarts the proposal without spending resources.
5. **Confirm Move** creates a unique request ID and invokes `Movement.Execute()` with current
   round/identity. Execution revalidates, commits the actual core steps and spends its budget.
6. `TacticalUnit.Animate()` queues only `TraversedPath`, including a partial/interrupted route,
   and ends at the actual final position. Animation never writes Character.TacticalPosition.

All command buttons and board command clicks are gated while any view animates, preventing
presentation from overlapping player commands. Rejections animate no invented route. A surviving
Sentinel hit stops normal movement but retains other existing action opportunities. Actual
reaction source, hit/incomplete outcome and HP/shield losses appear in the event log. Partial
movement preserves the reached position and damage and locks further commands for manual recovery.
There is no automatic retry or rollback.

The old GridPathfinder/TacticalMap.FindPath helper remains legacy code for other prototype
consumers, but the battlefield and TacticalUnit never invoke it. Ordinary movement is manual.

## Ordinary attacks and error handling

Press **Attack** on the selected active unit. Potential enemy targets are highlighted only when
`PreviewAttack()` passes the production AttackActionExecutor preview. Click an enemy to submit.
The session supplies explicit development AttackConfiguration, live positions, AttackIntent,
enemy/living-target constraints, the map result and a normal CombatExchangeRequest. It does not
infer attack classification from names. Bow minimum range, magical INT/RES, physical STR/DEF,
triangle, effective modifiers, shield-first damage, counters and follow-ups run through existing
production components. Smoke Veil supplies its existing evasion modifiers; direct invisibility
target restrictions are preserved separately from rendering.

An approved attack consumes one Main Action and completes activation through the existing
approval contract, even when its target dies. Result logs distinguish calculated damage from
actual opening HP/shield loss and show actual hits, misses, counters and follow-ups. Labels/HUD
refresh from current core state, never a local subtraction. Preview rolls no randomness.

Invalid targets, used budgets, wrong phases and unsupported contexts return visible rejection or
unresolved reasons. Taunt context not provided by this narrow ordinary-attack UI remains explicitly
unresolved rather than bypassing policy. The UI does not cast Roar or other abilities in this stage.
If production execution throws after effects begin, observed state is retained, the request cannot
replay and `RequiresManualRecovery` locks mutating commands. Inspection remains available. Restart
the development encounter to recover; no speculative rollback/resume is offered.

## Activations, HUD and round progression

The HUD reads selected name/team/HP/max HP/shields/position/effective MOV/statuses and current
Movement/Main/Utility availability. The header reads current round, lifecycle phase, active
character and active/next team. Unavailable actions are disabled. Utility availability is shown;
there is no fake utility button or inventory system.

**End Activation** delegates to ActivationManager. **Advance Phase** processes exactly the
current lifecycle phase and advances once, so no second phase machine is maintained by Godot:

Shop → Poison batch → Delayed attacks → Timed statuses → Other triggers → Preparation →
Alternating Activations → Cleanup → Objective Scoring → next round's Shop.

PoisonBatchProcessor, DelayedAttackProcessor, TimedStatusBatchProcessor and
RoundPreparationCoordinator run on the real roster. Cleanup invokes the registered
RoundCleanupCoordinator. The activation phase cannot finish while eligible/active units remain.
Shop, Other Triggers and Objective Scoring are visibly logged no-op development phases. They do
not claim economy, victory or other unsupported processing. Preparation restores existing
eligibility/healing history without changing HP, position or cooldown deadlines. Phase attempts
are guarded, and failures stop further automatic processing.

The free tactical camera retains WASD pan, wheel zoom, Home recenter and G grid visibility.
Q/E and middle-drag add rotation. Left clicks select/build routes/choose targets and are not
camera drags. HUD controls consume mouse input before battlefield picking.

## Verification and remaining scope

`BattlefieldSessionTests` runs without Godot rendering: real character bindings, grid mapping,
deployment validation, inspection, readonly previews, movement and attack APIs, shields, Bow
range, magical damage, interruption, partial failures/no replay, alternation, actual round-start
processors/Cleanup, independent sessions and explicit development assumptions.

The scene's `--bridge-smoke-test` (also the legacy `--smoke-test` flag) is a separate Godot
runtime check: instantiate all six views, check Terrain3D picking and bindings, preview/execute
the supplied causeway route, animate to its actual final position, resolve a production attack,
finish activations, run Cleanup and prepare the following round. It uses a seeded .NET RNG only
in that test mode and exits nonzero on failure. It is an automated runtime test, not evidence of
manual mouse/camera verification. Actual import/runtime/interactive results are reported separately.

Imported assets and existing level resources are reused. The local, Git-ignored
`github-publish-work/` publication staging directory needs its own `.gdignore` so Godot does not
mistake unrelated repositories/datasets for game resources. This local import boundary does not
delete their contents or change the game specification.

Still outside a complete match: canonical terrain/crossing/interruption rules, several ability
edge cases, full targeting/visibility/elevation policy, equipment/inventory, shops, objectives,
victory, buyback/redeployment, automatic movement/pathfinding and AI. This stage makes the existing
supported core observable/playable locally; it does not finalize those rules.
