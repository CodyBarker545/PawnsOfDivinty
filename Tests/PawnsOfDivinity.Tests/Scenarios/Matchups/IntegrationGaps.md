# Stage 6.3 integration gaps

No specification decisions were changed. The following limits distinguish missing inputs,
gameplay decisions, production boundaries and test-harness work.

| Category | Affected experiment / trigger | Existing system and observed boundary | Needed work |
|---|---|---|---|
| Unresolved gameplay rule | Two qualifying Colossi protect the same recipient | `SanctuaryDefenseContext` returns `OverlapUnresolved`; the controlled experiment applies no damage and consumes no RNG | Designer decision on overlapping Sanctuary sources, followed by gameplay tests |
| Unresolved gameplay rule | Healing Light or another unclassified ability attempted while Silenced | Registered spell tags can remain null; ability eligibility does not choose a tag | Confirm metadata; preserve rejection/unresolved results until then |
| Unresolved gameplay rule | Frostbite Arrow involving shield/status timing, flying effectiveness, or terrain Frostbite | Existing ability executor blocks unsupported sequencing; Bow flying damage rounding/order is unresolved | Gameplay design decisions and focused production implementation, not analytics approximations |
| Missing production execution API | A melee unit must approach a Bow user, or a formation requires ordinary movement | The action harness has authoritative positions and confirmed-movement bookkeeping, but no complete ordinary movement execution coordinator | Production movement boundary with confirmed legality and action accounting; no pathfinding or AI added here |
| Missing production execution API | General interacting Cleanup triggers | Existing independent expiration services work; the harness coordinates them in a fixed fixture order with a scenario guard, not a universal Cleanup-effects pipeline | Future lifecycle gameplay work if interacting triggers need ordering |
| Missing scenario harness adapter | `unsupported_roster`: Time Weaver requests Arcane Transmutation | `ScenarioRunner` returns `HarnessUnsupported` before effects/resources. A production executor exists but requires equipment/catalog/selection context not adapted here | A focused adapter with explicit context; do not copy its effects into matchup code |
| Unknown equipment/balance data | Nigel, Colossus or Spirit Guardian has no supplied attack configuration | Specification starting Axe/Lance/Bow Power is TBD; isolated/gameplay experiments report `ExplicitAttackAndCounterConfigurationRequired` | Explicit test inputs for experiments; designer approval for canonical equipment. No values added to definitions |
| Unsupported map or movement interaction | Five distance-1 Bow-opening pairings | Production range validation returns `InsideMinimumRange`. The harness does not move either unit to make the attack succeed | Supply a different explicit legal initial position or a supported future movement command |
| Unsupported map or movement interaction | Missing map targeting confirmation, LOS/elevation, smoke/flying classification, unsupported knockback geometry | The flat fixture contract cannot establish those interactions; existing unresolved or rejection results remain authoritative | Confirm context/rules and add the appropriate production or harness boundary; never infer a route or geometry |
| Incomplete combat result observability | Counter/follow-up absorbs shields | Exchange results expose opening absorption and aggregate final changes, but no separate absorption for later strikes | Nullable report fields now; optional future production result instrumentation |
| Incomplete combat result observability | Individual Fury contribution | Runtime ordinary damage includes Fury, but results do not expose it separately | Report null; matched current-HP experiments show the effect without duplicating its formula |
| Incomplete combat result observability | Scripted RNG exhausts after opening damage | Production throws without returning the completed exchange. Actual HP loss remains visible, but hit/reaction totals are incomplete | Preserve partial state and block automatic retry; inspect the fixture. Add instrumentation only if detailed partial strike logs are required |

## Evidence scope

- Completed direct attacks are not completed battles. Completed round plans are not victory checks.
- Isolated combat comparisons do not pay actions or establish legal player targeting.
- All fixture inputs and RNG scripts are retained; variants must declare their exact differences.
- The representative suite uses no unresolved ability interactions. Unsupported and unresolved
  cases are exercised separately and never converted into wins, losses or misses.
- No time-to-kill estimates, effective-HP formula, win rates, rankings or balance adjustments
  are produced. Future work must resolve the appropriate category rather than tuning around
  a missing rule or hiding it behind an arbitrary fixture value.
