# Deterministic combat scenarios

This is test infrastructure around production player actions, not another combat engine.
All instances come from `CharacterFactory` and registered definitions. Power and accuracy in
these tests are explicit fixture inputs, not starting equipment or balance decisions.

## Components

- `CombatScenarioBuilder`: reusable setup instructions. `Build()` creates fresh characters,
  validates teams, IDs and deployment, then uses public APIs for health, shields, statuses,
  scrolls, temporary modifiers and cooldown history. Health is arranged before shields.
- `CombatScenario`: owns those real characters, the lifecycle/activation managers, match-scoped
  wall/ice/smoke services, a map adapter and runner. `Capture()` reads current production state.
  `Metrics` aggregates actual HP/shield loss and returned strike results.
- `ScenarioMap`: bounds, blocked tiles, live occupancy, wall/ice placement interfaces and
  caller-confirmed targeting/individual displacement decisions. Positions are always read
  from `Character.TacticalPosition`. Unknown map decisions return null. This implements no
  line-of-sight, pathfinding, elevation or collision rules.
- `ScenarioRunner`: `Preview()` checks current production permissions without reserving an
  action ID. `Execute()` checks again, invokes a supported executor and records immutable
  results. Request IDs cannot be executed twice, including failed or partially applied uses.
- `ScriptedScenarioRandom`: implements the existing roll/index provider interfaces. Accuracy,
  status procs and selection have separate scripts and one chronological draw log. There is
  no fallback RNG. Invalid values and exhausted scripts fail explicitly.
- `ScenarioSnapshot` / `ScenarioCharacterSnapshot`: copied dictionaries and read-only lists
  of value snapshots; no mutable Character references. Base stats, scroll metadata and other
  already-immutable production values can safely be shared. Snapshot Defense is the character's
  attack-independent effective Defense; Sanctuary is evaluated live by production combat.
- `ScenarioAction`, `ScenarioExecutionResult`, `ScenarioProductionOutcome`, `ScenarioEvent`
  and `ScenarioMetrics`: immutable request/outcome/report data. Production outcomes retain
  the immutable combat exchange result, but project ability-use tokens into scalar results
  so runtime Character references do not leak into reports.

## Supported player actions

- Ordinary physical/magical attacks use `AttackActionExecutor`, live targeting, action budgets,
  taunt approval and `CombatExchangeResolver`. Normal counters and one follow-up remain enabled.
- Healing Light, Provoking Roar and Frostbite Arrow use their production ability executors,
  which reuse eligibility and successful-use finalization. Frostbite Arrow requires explicit
  duration/flying/terrain context and retains all production sequencing blockers.
- Activation selection/completion use `ActivationManager`. Taunt fallback completion requires
  caller-supplied attack availability; the harness does not choose movement or attacks.

Unadapted abilities return `Unresolved` with `HarnessUnsupported`, not fake success. Physical
equipment is an explicit `ScenarioAttackInput`, not a fabricated weapon inventory. Actual
scroll equipment uses `EquipScroll`; a replaced scroll's current properties are read at action
time. Disarmed actors require an explicit unarmed configuration; no Power/accuracy is invented.

Setting a fixture round/phase arranges an initial condition only. It does **not** run earlier
phases, establish completed phase guards, advance cooldowns, tick statuses or prepare a round.
The initial harness requires exactly two nonempty teams and legal, distinct deployment tiles.

## Outcomes and event limits

`Ready` is preview-only. Execution distinguishes `Completed`, `Rejected`, `Unresolved`,
`PartiallyApplied` and `UnexpectedFailure`. Production rule exceptions such as ambiguous
Sanctuary overlap are unresolved; missing adapter support is identified separately in the reason.
Before/after snapshots are captured even when execution throws. There is no rollback or retry.
A partial action's earlier HP/shield changes remain visible and its request ID stays spent.

Events reconstruct the logical strike sequence from actual returned production results.
Opening shield absorption is exact. Production does not expose separate counter/follow-up
shield absorption, so remaining shield loss is explicitly an end-of-action observation.
If production throws without returning its combat result, the report records observed health/
shield changes and failure, **not** invented hits, misses or finalization. Thus hit/miss and
completed-attack metrics count only reported strikes/exchanges; partial actions may have
unreported strike details. RNG draws have their own exact chronological log rather than being
interleaved with reconstructed combat events. Metrics count actual HP loss, not overkill.

## Tests and next extension

`CombatScenarioTests` covers the physical duel, Sanctuary with real Gentle Blessing and shields,
Winter Hunter before/after cleanse, Fury at current/healed HP, Roar and taunt enforcement,
rejected abilities, unresolved rules, magical attacks, Frostbite procs, deterministic replay,
snapshots, ordering, map decisions, cooldown/action budgets and failures after partial damage.

Additional stages can compose more explicit actions and add adapters for existing ability
executors. They should not bypass those executors to simulate effects. Arcane Transmutation has
a scripted selection provider ready for injection, but no selection/catalog execution adapter
in this stage. Flying effectiveness, smoke visibility, terrain interactions, cooldown metadata
under Silence and unsupported geometry must retain their production/context boundaries.
There is no movement planner, balance ranking or victory logic.

## Stage 6.2: scripted rounds

`ScenarioRoundPlan(round, startingTeamId, activations...)` is an immutable script of explicit
`ScenarioActivationPlan(actorId, actions...)` choices. The caller supplies the starting team
each round because the game has not finalized that selection rule. RNG scripts still belong
to `CombatScenarioBuilder` and cover the complete timeline; round results slice the actual draw
log rather than reseeding or duplicating random state. Action IDs must be unique across rounds.

Build a multi-round fixture with `.AtPhase(RoundPhase.Shop)` and no preselected actor. The
Stage 6.1 default of arranging an activation-phase fixture is unchanged. Initial statuses and
Poison are treated as pre-existing before the first processed batch, not newly cast in that
batch. For a status applied during R2, use a production executor during R2, or an explicitly
labeled isolated fixture after R2's round-start processing. A two-round status then decrements
at R3 start and expires at R4 start.

`CombatScenario` now owns a real `DelayedAttackScheduler` and `ImpalingSentinelTracker` alongside
its existing lifecycle and activation managers. `WithDelayedAttack()` arranges immutable stored
outcomes in a fresh queue for every Build. Public production APIs can arrange initial smoke,
shields and other supported fixtures. No externally supplied activation manager is cloned or
rebound to different characters: the coordinator uses this scenario's existing managers.
Production APIs reject inserting a Roar outside its application-round activation phase, so
Roar scenarios cast it there instead of fabricating a taunt at Shop.

### Coordinator and results

- `MultiRoundScenarioRunner.Run()` executes ordered round plans and stops on the first
  interrupted round. `RunRound()` begins at Shop, processes round start, delegates every planned
  action to the existing `ScenarioRunner`, then acknowledges Cleanup and objective scoring.
- `BeginRound()` pauses at Activations; `RunActivations()` and `CompleteRound()` expose narrow
  checkpoints for isolated service tests. `CaptureCheckpoint()` records explicitly labeled
  fixture boundaries. Aggregate `Result` is available after the pending round is completed or
  reported interrupted, not while unreported work is still pending.
- `ScenarioPhaseResult` holds before/after snapshots and the existing immutable Poison,
  delayed-attack, timed-status or preparation result when available. `ScenarioCheckpoint`
  labels round-start, after-start, after-activation, Cleanup, end and interruption snapshots.
- `ScenarioRoundResult` copies phase/action outcomes, checkpoints, events and draws for one
  attempt, including an interrupted attempt. `MultiRoundScenarioResult` holds the whole run,
  final snapshots, ordered timeline, metrics and an explicit interruption reason.
- `MultiRoundMetrics` counts completed rounds, activation/attack attempts, completed activations,
  reported hits/misses, HP/shield damage, healing, defeats, status applications, finalized
  abilities, rejections, unresolved and partial actions, and starting/ending HP. It derives
  one aggregate from round events/actions; it does not add an action's damage again from its
  snapshots. The original `CombatScenario.Metrics` remains the Stage 6.1 action-only report.

Production `RoundLifecycleManager` controls every phase transition and owns the only round
number. `RoundPreparationCoordinator` resets activation eligibility and healing history, not
HP, shields or cooldowns. Cooldowns continue using absolute ready-round deadlines. The runner
never calls `ActivationManager.StartNextRound()` and never invents a cooldown decrement.
It rejects skipped rounds, wrong initial phases and invalid starting teams before processing.

Poison, delayed attacks and timed statuses use their production processors in that exact order,
including lifecycle-level duplicate guards. Snapshot Poison ticks, oldest-first delayed damage,
shield absorption, cancellation for defeated targets and status-duration arithmetic remain
production responsibilities. The scheduler stores existing outcomes; no Temporal Ignition
range is invented. Shop, Other Triggers and Objective Scoring are explicit no-op acknowledgments,
not implemented gameplay systems. No loot, death-trigger orchestration or scoring is simulated.

The activation manager selects eligibility and team alternation. The harness supplies only the
script's requested actor. Invalid/defeated/duplicate selections are rejected; their actions are
not executed. A rejected action may be followed by another explicitly planned action or End.
An unresolved/partial/unexpected action stops execution immediately. A script leaving an
activation open or eligible actors unused is unresolved, not automatically completed.

Since Stage 6.5, `CombatScenario.Cleanup` is the production `PawnsOfDivinity.RoundCleanupCoordinator`,
registered once on the existing lifecycle. The multi-round runner calls it instead of coordinating
individual expiration APIs or keeping its own Cleanup guard. `ScenarioPhaseResult.Cleanup` retains
the immutable production outcome, and events use only its actually completed operations.
Successful repeats return `AlreadyProcessed`; failed/partial attempts require manual resolution
and cannot automatically continue even by advancing to another round. Replayed evidence emits
no new expiration events. Round-script attempts remain separately guarded by the scenario.
The preserved traversal order of independent expirations is not a gameplay priority for future
interacting triggers. Direct service APIs remain public, so this is not a global exactly-once
guarantee against out-of-band calls. See `Docs/ROUND_CLEANUP.md` for ownership, phase/readiness
validation, per-step results, failure semantics and remaining integration limits.

### Focused additional adapters and fixture boundaries

Wailing Rampart, Spirit Frost and Impaling Sentinel preparation use their actual ability
executors. They preserve ownership, eligibility, map decisions, action consumption, cooldowns,
and unresolved placement/reaction-policy results. Sentinel requires an explicit
`ProvisionalOnePerPreparation` choice; no permanent reaction limit is chosen.

`ConfirmedUtility` and `ConfirmedMovement` are **bookkeeping adapters only**. They require an
explicit true legality input from an external coordinator and invoke existing action-budget
APIs. They do not move characters or claim a consumable/ability effect occurred. Missing legality
is unresolved; repeated use is rejected. Utility still does not satisfy or clear a taunt.

The Sanctuary Shield test calls its supported management API directly because casting range is
unresolved. Position-change/status-application/cleanse fixtures are explicitly labeled isolated
production setup, not player commands. The Sentinel movement/reaction service test supplies a
committed adjacent step and calls the production reaction resolver directly. Its snapshot and
RNG state are observable, but no fabricated player action or reaction metrics are inserted into
the multi-round timeline. Event/metric completeness applies to runner-managed operations;
external fixture operations must not be mistaken for recorded gameplay execution.

### Multi-round test coverage and remaining limits

`MultiRoundScenarioTests` covers:

1. Three-round Nigel/Colossus combat, live Fury, alternation and persistent HP.
2. Roar used R2, rejected R3/R4, ready R5; zero-interval healing still needs a new activation.
3. Four Poison batches (2/4/6/8), shields, defeat and cancellation of dead targets' queued attacks.
4. Stored delayed hit/miss outcomes, insertion order, live shields and unchanged damage under RES modifiers.
5. Timed-status duration, refresh, coexistence, effective-stat restoration and Poison cleanse reset.
6. Gentle Blessing plus Sanctuary and shields, live position changes, refresh/transfer and Cleanup expiration.
7. Wall/ice/smoke lifetimes, owner death, idempotent expiry and unresolved terrain Frostbite.
8. Roar Utility permission, required targeting, first-source priority, source invalidation and Cleanup expiry.
9. Sentinel preparation, action/cooldown cost, explicit provisional policy, ordinary reaction evidence,
   duplicate event protection, unresolved special movement and Cleanup retirement.
10. Wrong/duplicate/defeated actors, incomplete plans, phase guards, failed RNG, partial damage,
    replay prevention, immutable data, deterministic independent runs and stable chronological events.

Events now include round and phase while keeping the Stage 6.1 constructor parameters compatible.
Batch events use actual processor results; failed batches record observations only. Finalization
and attack events still come from `ScenarioRunner`. Whole-run events receive one contiguous
sequence without modifying the original action results. Direct-service fixture mutations are
not silently converted into gameplay events.

Unadapted abilities, unresolved spell tags under Silence, flying-effectiveness rounding,
Sanctuary overlap, unsupported knockback geometry, terrain Frostbite, Sentinel special movement
and competing-reaction ordering remain outside successful scripted execution. No AI, pathfinding,
shop/economy, redeployment, objectives, victory, rankings or Godot integration is added.


## Stage 6.6 — production ordinary movement

`ScenarioActionKind.Movement` supplies an ordered `Tiles` route **including its origin**.
The runner invokes the match-owned `MovementActionExecutor`; snapshots read the same
`Character.TacticalPosition` used by subsequent attacks and occupancy queries. Preview is
non-mutating. Movement events represent actual committed steps and Sentinel results; damage
observations are not counted again when the reaction already reports them.

The fixture must explicitly set `scenario.Map.UseProvisionalFlatMovement = true` to supply
flat unit-step costs and blocked living occupancy. These are provisional fixture rules, not
new Specification v0.2 decisions. The default map leaves movement costs unresolved. A path
with a prospective Sentinel also needs an explicit
`scenario.Map.InterruptedMovementConsumesOpportunity = true` fixture policy, configured
Lance/counter inputs and the existing provisional reaction-limit opt-in.

A Sentinel hit returns `ScenarioOutcome.Interrupted`, preserves the reached tile/damage and
spent Movement, and leaves remaining actions available if the mover survived. An explicit
multi-round script may continue; it does not automatically select an attack or end activation.
Unresolved rules, partial failures and unexpected failures retain the existing script-stop
behavior. Cleanup and next-round preparation still use their production coordinators.

`ConfirmedMovement` remains an externally confirmed **bookkeeping-only fixture action** for
older tests; it never means the new executor moved a character. See
`Docs/MOVEMENT_EXECUTION.md` for API details, unapproved rules and the Godot/core authority
boundary. `MovementScenarioTests` covers movement-to-attack, occupancy, terrain, interruption
and successive rounds. No matchup balance conclusions are changed by these fixture policies.
