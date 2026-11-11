# Production Round Cleanup — Stage 6.5

`Scripts/Core/Turns/RoundCleanupCoordinator.cs` and `RoundCleanupResult.cs` use the requested
**`PawnsOfDivinity` namespace**. Existing lifecycle classes stay in `PawnsOfDivinity.Core.Turns`.
This stage promotes the scenario harness's existing expiration orchestration into production.
It adds no new effect, duration, damage rule, phase, movement or Godot behavior.

## Ownership and public boundary

Construct one coordinator with the match's existing `RoundLifecycleManager`, connected
`ActivationManager`, `WailingRampartService`, `SpiritFrostService`, `SmokePlacementService`
and `ImpalingSentinelTracker`. It captures the activation manager's full read-only roster.
Activation and Sentinel must reference the same lifecycle and activation manager.

The constructor validates all non-null dependencies and context before registering itself.
`RoundLifecycleManager.CleanupCoordinator` exposes that one registered instance read-only.
Constructing a second coordinator for the same lifecycle throws rather than replacing its
execution guard. There is no static/global state, second round counter or second phase machine.
Each match must supply its own characters and service instances; do not share mutable terrain
services between matches. Plain terrain services do not themselves carry a universal match ID.

```csharp
var cleanup = new RoundCleanupCoordinator(lifecycle, activations, walls, ice, smoke, sentinel);

// After the match's activations and explicit transition to Cleanup:
RoundCleanupResult result = cleanup.Process(lifecycle.CurrentRound);
if (result.Status == RoundCleanupStatus.Completed)
    lifecycle.AdvancePhase(); // Caller acknowledgement moves to ObjectiveScoring.
```

`Process(requestedRound)` returns structured rejection for a nonpositive/noncurrent round,
wrong phase, or unfinished activations. Those rejections invoke no expiration services and
do not reserve the attempt. Cleanup requires `ActivationManager.IsRoundComplete`, including
no active activation. A caller can correct readiness or phase and submit a valid request later.
Malformed constructor inputs still throw ordinary argument/context exceptions.

The coordinator **never advances phases**. `RoundLifecycleManager` retains its established
nine-phase order and public advancement APIs. It does not automatically execute Cleanup,
Shop, scoring or any round-start processor merely because a phase changes.

## Supported operations and timing

The following order matches the previous harness and is a deterministic technical traversal
of currently independent services. It is **not** a newly approved universal gameplay priority
for future interacting end-of-round triggers.

| Operation | Existing production API | Required context | Actual returned evidence | Timing and repeat behavior |
|---|---|---|---|---|
| Personal temporary modifiers, in captured roster order | `TemporaryStatModifiers.ExpireAtRoundEnd(round, Cleanup)` | Each character's own service | Actual modifier ID/source/recipient | Uses each modifier's existing expiration; Blessing ends at following Cleanup. Repeated direct calls return no new expirations; backwards cleanup is rejected |
| Wailing Rampart | `WailingRampartService.ExpireAtRoundEnd(round, Cleanup)` | Match wall service | Actual area ID/owner | Existing cast + following-round deadline; creator death does not remove persistent walls. Repeat removes nothing further |
| Spirit Frost | `SpiritFrostService.ExpireAtRoundEnd(round, Cleanup)` | Match ice service | Actual area ID/owner | Existing cast + following-round deadline; creator death does not remove persistent ice. No new enemy Frostbite rule |
| Smoke | `SmokePlacementService.ExpireAtRoundEnd(round, Cleanup)` | Match smoke service | `ExpiredAreas` IDs/owners | Existing following-round Cleanup deadline; creator death does not remove smoke early. Repeat returns empty |
| Taunt | `ActivationManager.ProcessTauntCleanup()` → tracker `ExpireAtCleanup` | Connected match lifecycle | Actual restriction ID/source/recipient | Existing application-round Cleanup; first-source priority and invalid-source handling unchanged. Already-ended records are not ended again |
| Sentinel | `ImpalingSentinelTracker.RetireInactivePreparations()` | Tracker already bound to match/lifecycle | Actual preparation ID/owner/retirement status | Existing current-round Cleanup or owner-defeat retirement. Result retains `SentinelStatus`; retirement is not always a newly timed expiration |

Personal modifiers produce one service-step result per character because each character owns
an independent expiration service. The other five categories each produce one step result.
A valid empty-effects batch still calls the services and completes; it is not silently skipped.

Other effects deliberately remain elsewhere:

- Poison and delayed attacks run in their existing round-start phases.
- Timed negative statuses decrement **after** those damage stages, not in Cleanup.
- Activation and received-healing history reset at Preparation, not Cleanup.
- Cooldowns use absolute deadlines; no counter is reset or decremented here.
- Ordinary shields do not expire just because the round ends.
- Attack-reveal reset, general Cleanup/death triggers, loot expiry, objective scoring and future
  inter-effect ordering are not invented or added. There is no generic hook/event framework.

## Results, guards and failure behavior

`RoundCleanupResult` captures requested/current round, actual lifecycle phase, `Status`, `Reason`,
read-only `Services`, and any previous attempt's round/status. `CleanupServiceResult` records
service category, character scope when relevant, `Completed`/`Failed`/`NotStarted`, actual effect
identities, and exception type/message when a call throws. No exception object, stack trace,
mutable Character reference or mutable service collection is stored.

For a completed step, `Effects` is a copied read-only list; its count can legitimately be zero.
For a failed or unstarted step, effects/count are **null**, not invented zero measurements.
A throwing step sets `MutationMayHaveOccurred`: absence of a returned list is not proof that
it changed nothing. Failure information is diagnostic, not a rollback promise.

Outcomes:

- `Completed`: all supported operations returned successfully.
- `RejectedWrongPhase`, `RejectedInvalidRound`, `RejectedActivationsIncomplete`: no service was invoked.
- `AlreadyProcessed`: successful same-round attempt already exists; zero new invocations.
- `PartiallyApplied`: at least one earlier service completed, then a later service threw.
  Even an empty completed expiration can update its service's internal cleanup bookkeeping.
- `Failed`: first service threw without a known completed predecessor; its mutation state may
  still be unknown. This does not mean rollback or guaranteed nonmutation.
- `ManualResolutionRequired`: previous failed/incomplete attempt or reentrant execution prevents
  another invocation. No automatic resume/reset API is supplied in this stage.

The attempt is reserved before the first service call. Successful steps remain applied if
another throws; the failed step is identified, all remaining operations are `NotStarted`, and
no later service is automatically invoked. The coordinator catches the failure and reports it,
not success. It retains that failure so retries cannot replay earlier or remaining work.
Advancing the lifecycle externally to a later round does not clear a failed attempt: subsequent
Cleanup remains blocked for manual resolution. There is no transactional rollback or retry loop.

On `AlreadyProcessed`/`ManualResolutionRequired`, `Services` retains the **original attempt's
evidence**, not newly executed effects. `PreviousAttemptRound/Status` identifies that evidence;
`ServicesInvokedThisRequest` is zero. Consumers must not emit new expiration events from cached
evidence. Invalid round/phase requests are checked before returning cached evidence.
After successful cleanup, a new current round can execute independently; old-round requests
cannot execute during a different round.

## Limits of the guarantee

Exactly-once invocation applies to this **registered synchronous coordinator boundary** for
its supplied match state. Existing expiration APIs remain public for compatibility and isolated
tests. They can be called directly before/after the coordinator, and callers can also advance
the lifecycle without consulting its result. The coordinator cannot globally prevent those
out-of-band operations, service sharing, or concurrent mutations. Do not claim global exactly-once
effect creation/expiration or automatic match recovery. All production callers should use the
registered boundary and honor its failure result.

The existing services have no cross-effect callbacks in these operations. If future effects
introduce ordering-dependent gameplay, that dependency needs an approved rule and an explicitly
supported coordinator contract. Completion here means these six supported categories completed,
not that unknown end triggers, Shop, deaths/loot or victory have been implemented.

## Scenario harness changes

`CombatScenario.Cleanup` constructs the production coordinator once beside its Sentinel and
other match services. The redundant scenario `CleanupAttempts` guard was removed; the separate
scenario round-script replay guard remains because it protects a different operation.

`MultiRoundScenarioRunner` invokes `Cleanup.Process()` and projects only the production result's
completed-step effect identities into the existing modifier/terrain/taunt/Sentinel events.
`ScenarioPhaseResult.Cleanup` retains the immutable production result. Cached duplicate evidence
does not generate new expiration events. Partial/failed cleanup stops phase advancement and
retains known expiration events and actual snapshots. Existing attacks, RNG, duration rules,
metrics and round-start processing are unchanged.

## Verification and scope

`RoundCleanupCoordinatorTests` uses real production characters/services and actual ability casts
for Blessing, wall, ice, Roar and Sentinel setup. Its flat map supplies explicit legality only;
it does not mock gameplay effects. Failure tests deliberately call a real modifier expiration
service in a future round, then demonstrate its backwards-round exception during coordinator
execution. That out-of-band fault verifies partial preservation and no retry without adding a
public failure-injection framework.

Additional multi-round tests verify typed production results, exactly one known expiration
event, separate Poison/status phases, later-round cleanup, truthful partial outcomes, retained
due terrain after failure, zero cleanup randomness and no replay through a replacement runner.
All earlier wall/ice/smoke/taunt/Sentinel and matchup regressions remain required.

No specification or historical Stage 6.3/6.4 evidence was rewritten. Those reports describe their
earlier baseline; this document records the Stage 6.5 resolution of the Cleanup integration gap.
Movement execution, Godot/core state binding and unresolved gameplay decisions remain separate work.
