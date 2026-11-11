# Stage 6.3 matchup experiments

This test-only layer builds on the Stage 6.1/6.2 harness. It changes no production rules,
character definitions, abilities or balance. A completed experiment means the requested
operations completed; it is not a complete match, win, ranking or time-to-kill estimate.

## Inputs and execution

`MatchupParticipant` records a registered definition ID, explicit runtime ID/team/position,
optional equipment fixture, initial HP/shields, status setup, modifiers, cooldown history
and supported scroll metadata. Collections are copied. Stats always come from the registry.
`FixtureHealing` is an explicit pre-encounter public `Character.Heal()` setup operation for
Fury experiments, not a Healing Light ability, action payment or recorded healing metric.

`MatchupDefinition` copies participants, assumptions, RNG scripts and exactly one script form:
isolated attacks, direct scenario actions, or multi-round plans. It rejects conflicting instance
IDs, mixed modes, ambiguous script forms and duplicated action IDs. Assumptions are mandatory.
All supplied equipment is labeled **TEST FIXTURE VALUES** in reports. There are no fallback
Power, accuracy or weapon assignments. The same definition can be explicitly run again to
create an independent experiment; it never retries a partial command on the original state.

`MatchupScenarioFactory.Create()` passes inputs to `CombatScenarioBuilder` and public production
APIs. `Pairings()` reads and sorts registered IDs to generate the current 15 unordered pairs.
`DefinePairings()` accepts an explicit equipment dictionary, distance and RNG script, then
creates one requested player attack per pairing. Missing dictionary entries remain missing.
It does not plan movement, pick winning actions or force a full battle to complete.

`MatchupRunner.Run()` uses one of two clearly separated modes:

- **IsolatedCombat:** invokes `CombatExchangeResolver` with real runtime characters, live
  effective stats and Sanctuary context. The caller explicitly opts into counters/follow-ups.
  These comparisons do not establish player-action eligibility, spend budgets or validate an
  approach path. The low-level API assumes opening range is resolved by its caller; its result
  must never be presented as an in-range player command or a complete match outcome.
- **ScriptedGameplay:** delegates direct actions to `ScenarioRunner`, or whole plans to
  `MultiRoundScenarioRunner`. Existing targeting, activation order, budgets, cooldowns, Silence,
  taunts and effect execution remain authoritative. Unsupported actions are not substituted.

The runner stops on unresolved, unsupported, partial or unexpected outcomes. Rejected direct
actions may be followed only by explicitly scripted alternatives. If a round contains rejected
actions but otherwise completes, its phase completion remains recorded and the overall matchup
status is **Rejected**, not silently promoted to success. Status severity preserves partial
application before other failures; there is no automatic retry or rollback.

## Analytics and immutable reports

`MatchupObservation` retains immutable snapshots and returned combat data for an operation.
`MatchupResult` holds the definition, classification, observations, round results, final state,
timeline, RNG draws, blockers, per-strike analytics and per-character survivability.
`MatchupReport.ToData()` creates a compact typed export without repeating every operation's full
snapshots. `MatchupReport.Format()` provides readable text. Exported null metrics mean unavailable.

`MatchupMetrics` separates calculated physical/magical strike damage from actual HP loss and
shield loss. Opening/counter/follow-up strikes are visited once. Counter HP loss can be recovered
exactly from the documented attacker-total HP loss minus a defender follow-up's HP loss; it
does not require a new damage calculation. Counter/follow-up shield absorption is **null**, since
production exposes no separate fields. Winter Hunter's exposed contribution is pre-scaling.
Individual Fury contribution is **null**; ordinary damage includes it but does not expose its
separate value. The comparisons test its effect without introducing a second Fury formula.

HP/shield loss is summed from actual per-operation before/after snapshots. The multi-round
activation-phase summary is deliberately excluded because it spans those same actions. Healing
counts only recorded production ability results. No overkill is reported as HP loss. Poison or
other phase HP loss remains observed HP damage, not arbitrarily classified as physical/magical.
When an attack throws without returning its exchange, observed state changes remain available,
but complete hit/miss, calculated-damage and reaction totals are null rather than invented zeroes.

`MatchupSurvivability` records initial/final HP and shields, observed losses, recorded healing,
defeat and its round when an actual event exists. `SupportedStrikesSurvived` counts returned
strikes targeting that character after which it remains alive (including misses). It is not an
effective-HP or time-to-kill formula; it is unavailable when the attack timeline is incomplete.

Isolated combat events retain that explicit scope. Strike participants come from production's
opening/counter/follow-up sequence; aggregate health/shield changes are labeled observations.
Gameplay timelines reuse existing harness events. No new ability or status effects are inferred
from state changes. Reports contain no mutable Character references.

## Comparing variants

`MatchupComparison.DescribeDifferences()` lists every changed input path and its actual values,
excluding only the experiment ID. `Compare()` requires completed experiments in the same mode,
a written rationale, and an exact caller-reviewed list of changed paths. Undeclared equipment,
RNG, position, status, action-plan or assumption differences prevent numerical comparison.
This documents comparability; it does not prove fairness or competitive balance.

`HPDamageDifference` is variant minus baseline observed HP loss across the encounter.
`ObservedMatchedDefenseHPReduction` is available only for an explicitly selected recipient when
changes are limited to that recipient's shield or a Colossus's position. It measures actual
recipient HP-loss reduction, not an assumption that +3 DEF always prevents 3 HP. Other changes,
mixed modes and incomplete experiments do not receive that defensive attribution.

## Test groups and representative suite

`MatchupTests` covers all six confirmed stat records and all 15 pairings; independent factory
state; physical/magical formulas and triangle effects; counters, follow-ups, shields, overkill;
Fury and healing; Winter Hunter before scaling; matched Sanctuary/shield comparisons; 2v2 defense
and elemental pressure; a complete 3v3 activation script; multi-round HP/cooldown/status/death;
taunt enforcement; unavailable metrics; partial execution; deterministic variants; immutable
inputs/reports; unsupported abilities; comparison validation; and report formatting/export.

The pairing coverage fixture uses distance 1. Five Bow-opening requests correctly reject
`InsideMinimumRange`; ten other opening player actions complete. This is 15-pair coverage,
not 15 completed duels. The representative Bow experiment instead explicitly starts at range 3.

`Reports/RepresentativeSuite.md` and `.json` are generated from five actual executed fixtures:
Nigel/Colossus, Assassin/Time Weaver, Spirit Guardian/Nigel, defensive 2v2 and full-roster 3v3.
The test fixture equipment uses Sword 5, Light 3, Fire Scroll 4, Axe 4, Lance 3 and Bow 4 Power,
with explicit 100% baseline accuracy and scripted zero rolls. Axe/Lance/Bow Power and accuracy
are test choices, not newly approved balance. Existing triangle adjustments still apply.

Ordinary regression runs perform no report file writes. Set `PAWNS_MATCHUP_REPORT_DIR` explicitly
and run `RepresentativeControlledSuiteProducesTypedDeterministicReports` to refresh the export.
Stage 6.4 can consume the typed results/export and integration-gap report without replacing the
production executors or treating these fixtures as matchup rankings.
