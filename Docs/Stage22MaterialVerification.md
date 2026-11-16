# Stage 2.2 — Material verification

Verified 2026-11-15 in Godot 4.7.2 .NET, GL Compatibility, NVIDIA GTX 1660 Ti.

**Status: Stage 2.2 accepted.** Material calibration, import, visual review, documentation and required acceptance checks are complete. Rotated-camera picking, castle-transfer smoke setup and scene-aware free-testing validation are verified. Both complete broad smoke runs (Cliffside and original MOBA beta) pass. Do not start Stage 2.3 automatically.

## Preserved asset and placement

The existing wall remains at zero-based west forward fort tile (7,8), world position (6.625,0,14.125), beneath `blue_forward_wall_7_8/Wall`. Wrapper transform is identity; imported local bounds are (-0.625,0,-0.385) with size (1.25,1.085,0.77), expressed as Godot X/Y/Z. Physical length × depth × height remains **1.25 × 0.77 × 1.085** world units.

Comparison against `artifacts/stage22_baseline.glb.bak` confirms identical mesh definitions, geometry/index/UV accessor bytes and node transforms. Movement and sight flags remain true on the parent. The initial material verification made no scene, layout, character, collision or gameplay-code edits. The acceptance follow-up changes only tile-picking candidate selection and its verification; scene geometry and gameplay rules remain unchanged.

Source: `SourceAssets/levels/sources/ModularStoneWall/ModularStoneWall.blend`. Maps: `SourceAssets/levels/textures/ModularStoneWall/`. Runtime: `Assets/Levels/Props/ModularStoneWall/ModularStoneWall.glb`, wrapped by `ModularStoneWall.tscn`. Palette and workflow: [art guide](Environment_Art_Style_Guide.md). Source and runtime assets and `artifacts/` are locally stored and ignored by Git; retain them with project backups when handing off.

## Import and visual result

The final headless editor import scan completed with exit code 0. The runtime loaded `Weathered charcoal limestone` with all three 1024×1024 maps, normal enabled at approximately 0.32 and metallic 0. GLB albedo and normal pixels match the calibrated source maps. Packed green-channel roughness matches the source, with values 204–230. No missing texture or material-load errors occurred. Evidence: `artifacts/stage22_asset_audit.log` and `artifacts/stage22_visual.log`.

- [Close-up](../artifacts/stage22_close.png): broad stone shapes and softened edges remain legible; fine relief is restrained. Mortar is dark in recesses but does not overwhelm the courses. The cool grey wall separates from warm paving and green vegetation. Existing primitive fort pieces are warmer and smoother; replacing them is future kit work.
- [Normal tactical camera](../artifacts/stage22_tactical.png): camera size 57.418 at 1280×720, using the arena's fitted default camera and HUD. The wall is recognizable beside the west fort's north gate, though individual mortar joints appropriately recede at this distance. Units and team markers remain visible on both sides. The greybox floor and structures have not been globally darkened.
- The material fits the selected muted dark-fantasy direction. No serious material readability issue justified an adjustment; no cosmetic iteration or asset re-export was performed. This is a static opening-state review, not a full moving-unit/VFX or lighting approval.

The close-up changes only the runtime camera for capture and saves its tactical viewport image; the authored scene is never saved. Existing Blender render: `artifacts/stage22_blender.png`.

## Validation evidence

| Check | Result | Evidence |
|---|---|---|
| Editor import scan | Exit 0; shutdown diagnostics below | `artifacts/stage22_final_import.log` |
| C# solution build, no restore | Passed, 0 warnings / 0 errors | `artifacts/stage22_final_build.log` |
| Focused xUnit tests | 72 passed, 0 failed, 0 skipped | `artifacts/stage22_tests/stage22_final.trx`, `artifacts/stage22_final_tests.log` |
| Actual authored arena extraction | Passed through focused tests | Same TRX; invokes Godot on the native scene |
| Rendered material and scene-load smoke | Passed, exit 0; 30×20, 86 blocked / 514 walkable, 10 original fixtures | `artifacts/stage22_visual.log` |
| Native scene round-trip in broad smoke | Passed before subsequent failure, including acceptance rerun | `artifacts/stage22_acceptance_smoke.log` |
| Original broad gameplay smoke | Historically failed picking in headless and rendered runs | `artifacts/stage22_smoke.log`, `artifacts/stage22_smoke_rendered.log` |
| Acceptance follow-up build | Passed, 0 warnings / 0 errors | `artifacts/stage22_acceptance_build.log` |
| Acceptance focused tests | 98 passed, 0 failed, 0 skipped; includes native picking/stair verification | `artifacts/stage22_tests/stage22_acceptance.trx`, `artifacts/stage22_acceptance_tests.log` |
| Rotated-camera picking | 4,800 tile-center clicks passed; all 600 cells at eight angles | `artifacts/stage22_acceptance_smoke.log`, `artifacts/stage22_acceptance_rendered_picking.log` |
| Rendered native-scene acceptance check | Exit 0; picking, existing stair-preview checks and scene export passed at 1280×720 | `artifacts/stage22_acceptance_rendered_picking.log` |
| Acceptance broad gameplay smoke | Picking passed; failed later at friendly transfer destination assertion | `artifacts/stage22_acceptance_smoke.log` |
| Castle-transfer follow-up build | Passed, 0 warnings / 0 errors | `artifacts/stage22_transfer_build.log` |
| Castle-transfer focused tests | 117 passed, 0 failed, 0 skipped, including two new neutral/enemy destination cases | `artifacts/stage22_tests/stage22_transfer.trx`, `artifacts/stage22_transfer_tests.log` |
| Corrected castle-transfer native smoke | Passed: captured destination, transfer/activation completion, contested behavior and ownership reset | `artifacts/stage22_transfer_smoke.log` |
| Earlier castle-transfer follow-up broad smoke | Passed castle checks and later HUD/model checks; failed at free-testing default assertion, now resolved | `artifacts/stage22_transfer_smoke.log` |
| Final free-testing follow-up build | Passed, 0 warnings / 0 errors | `artifacts/stage22_free_testing_build.log` |
| Final free-testing/activation focused tests | 170 passed, 0 failed, 0 skipped | `artifacts/stage22_tests/stage22_free_testing.trx`, `artifacts/stage22_free_testing_tests.log` |
| Complete Cliffside broad smoke | Passed, exit 0; authored FreeTesting=False, explicit free-mode checks and competitive restoration | `artifacts/stage22_free_testing_cliffside_smoke.log` |
| Complete original-beta broad smoke | Passed, exit 0; authored FreeTesting=True, explicit free-mode checks and competitive restoration | `artifacts/stage22_free_testing_beta_smoke.log` |

Focused filter: `FullyQualifiedName~CliffsideArenaTests|FullyQualifiedName~AuthoredLevelValidationTests|FullyQualifiedName~MobaMovementTests|FullyQualifiedName~CastleTransfer`. The previously saved 26-test run was inspected; the final 72-test run covers additional authored-level and movement cases. A full-suite rerun was not needed for the material change.

### Acceptance follow-up — rotated-camera picking

The original failure was reproduced before editing code (`artifacts/stage22_acceptance_reproduce.log`). Diagnostic output (`artifacts/stage22_picking_diagnostic.log`) shows the screen point (683.1541,400.11755) is inside the battlefield. At yaw −1.3 its ray intersects elevation 0 inside (3,15), then elevation −1 at (2.875,−0.6,22.875), the exact center of stair (4,15). Projection and world-to-grid conversion were correct. The old picker stopped at the first valid elevation, so the higher neighbor won whenever projected footprints overlapped. No physics collision query is involved in floor picking.

History: `0550ee3` introduced the highest-first multi-elevation picker, and `4268e77` retained it unchanged while adding stair preview work. The defect predates this acceptance fix and was present in the elevation-handling code before wall calibration; no material logic participates in picking. The exact historical first failing scene revision was not reconstructed.

The minimal correction in `Scripts/Gameplay/Levels/MobaBetaBattlefield.cs` retains ray/elevation/grid candidate generation and selects the candidate whose projected tile center is nearest the click. This resolves tactical selection ambiguity instead of letting elevation order decide. It applies equally to blocking and walkable cells and contains no tile, angle or faction exception. UI input guards, camera controls, pathfinding, movement, elevations, terrain meshes and collision rules are unchanged. In areas where elevation projections overlap, ownership of off-center clicks follows this nearest-center policy; it is a tactical tile picker, not visible-triangle raycasting.

Regression coverage now runs the real picker against all 600 tile centers at yaw 0, ±0.8, ±1.3, ±2.3 and π: 4,800 checks, including (4,15), both stair rows at X4/5/24/25, their mirrored views, ordinary ground and blocked terrain. Camera yaw and zoom are restored after verification. `Tests/PawnsOfDivinity.Tests/Levels/AuthoredBetaScene.cs` requests `--verify-tile-picking` alongside the existing stair-preview checks, so the focused tests execute the regression against the serialized native arena. Both headless broad smoke and the rendered targeted run passed picking.

Final focused filter: `FullyQualifiedName~CliffsideArenaTests|FullyQualifiedName~AuthoredLevelValidationTests|FullyQualifiedName~MobaMovementTests|FullyQualifiedName~MobaMovementPreviewTests|FullyQualifiedName~MobaUnitPickingTests|FullyQualifiedName~MobaBetaLayoutTests|FullyQualifiedName~CastleTransfer`. All 98 tests passed; this supersedes the earlier 72-test acceptance baseline without claiming a full-suite run.

### Castle-transfer smoke correction

The earlier broad smoke failed `Friendly castle transfer offers unoccupied authored deployment choices` in `MobaBetaBattlefield.Objectives.cs`, formerly line 130. Its demonstration captured only `red_forward`, then expected a friendly transfer to `blue_forward` at (7,12). Cliffside starts both forts neutral (`InitialTeamId = 0`), so rejecting that destination was correct. The original MOBA-beta objective smoke dates to `3c5b147` and assumes an already-owned friendly fort. This was an outdated test setup, not a transfer-rule defect.

The smoke now requests an explicit destination-capture setup: stage the Blue Guardian at (8,9), move normally to (8,10), finish its activation, pass the Red activation, then move the Blue Assassin into the east capture zone. Normal production movement establishes ownership; no owner assignment or eligibility bypass is used. The UI then offers (7,12) as a legal west landing and executes the transfer. The setup is opt-in for smoke; existing capture-only demonstrations retain their default behavior. Contested and reset assertions now compare against authored initial ownership instead of assuming Red owns the east fort.

Only the development smoke fixture and regression tests changed. Castle control still excludes home hubs from capture; both endpoints must be friendly and uncontested, and landings must be valid and empty. Main Action completion and activation ending are unchanged. The focused suite covers home immunity and home-to-fort transfer, friendly destinations, neutral/enemy rejection without spending resources, contested origin/destination and stale-plan revalidation, landing access, five-per-side play, alternating activations and first-to-15-VP behavior. Two new Cliffside cases assert both forts start neutral, home ownership remains fixed, and a neutral or normally enemy-captured west fort rejects a Blue home transfer.

Final focused filter: `FullyQualifiedName~CliffsideArenaTests|FullyQualifiedName~MobaObjectiveTests|FullyQualifiedName~MobaMovementTests|FullyQualifiedName~MobaHotseatTests|FullyQualifiedName~MobaFive`. All 117 selected cases passed, including native extraction and the existing picking/stair checks. Build passed with zero warnings/errors. No gameplay rule, scene, material or environment asset was changed.

### Free-testing smoke correction and final acceptance

The earlier run (`artifacts/stage22_transfer_smoke.log`) reached `Free testing: authored beta scene defaults to free testing` in `MobaBetaBattlefield.FreeTesting.cs`, formerly line 19. That shared assertion incorrectly required `FreeTesting` true for every scene. Cliffside explicitly sets false; the original MOBA-beta scene intentionally sets true. The failure was a smoke-test assumption, not a gameplay-mode defect.

The smoke now creates its initial verification session using the current scene's exported `FreeTesting` value and verifies reset preservation, timer configuration, timer/turn-control visibility and opposing-team selection eligibility. It then explicitly enables free mode for the existing free-testing movement/combat checks. Finally, it explicitly restores competitive mode for the remainder of the broad regression and verifies turn/timer controls and alternating-team selection are restored. Existing broad-smoke startup intentionally forces competitive mode before these mode-specific checks; normal scene-launch wiring is unchanged. Neither scene resource nor global gameplay behavior was edited.

Both complete broad smoke runs exit 0 and finish with `GODOT MOBA BETA SMOKE PASSED`. Cliffside reports `AUTHORED MODE VERIFIED ... FreeTesting=False`; the original beta reports `... FreeTesting=True`. Each passes 4,800 picking checks, castle transfer, VP/victory/reset, timer and Assassin regressions, HUD/model checks, explicit free-testing checks and final camera/grid controls. The final logs contain no warnings or errors. Cliffside remains 30×20 with 86 blocked/514 walkable cells and ten fixtures; the original beta retains its separate 62 blocked/538 walkable layout and ten fixtures.

The final focused filter is `FullyQualifiedName~MobaFreeTestingTests|FullyQualifiedName~MobaActivationTimerTests|FullyQualifiedName~ActivationManagerTests|FullyQualifiedName~MobaFiveVsFiveTests|FullyQualifiedName~MobaObjectiveTests`. All 170 cases pass, distinguishing normal/free modes, competitive timing, Main Action behavior, alternating 5v5 activations and scoring. The build passes with zero warnings/errors. Earlier focused runs remain recorded separately; counts are not a full-suite total.

Together with the recorded material import, source/geometry audit, rendered visual review, rendered stair checks and prior focused regressions, these passing broad runs satisfy Stage 2.2 acceptance. No remaining acceptance blocker is known. Previously observed editor shutdown diagnostics remain a recorded limitation; no claim is made that the editor leak itself was fixed. Broader art-production gates and target-hardware profiling remain future work, outside Stage 2.2.

Editor import diagnostics: nested `Scenes/Battlefields/prototype/project.godot` ignored; one CanvasItem RID, one dummy texture RID, four ObjectDB instances and one resource still in use at shutdown. These also appeared in the earlier import logs. The final rendered material review exited without these diagnostics. Initial sandbox attempts could not access editor config/.NET assemblies; import and build were rerun successfully with normal host access.

## Reproduce the review

From the repository root with the installed Godot .NET executable:

```text
Godot --headless --path . --editor --import --quit
dotnet build PawnsOfDivinity.sln --no-restore --nologo
Godot --path . --resolution 1280x720 --script Tools/VerifyStage22StoneWall.gd
dotnet test Tests/PawnsOfDivinity.Tests/PawnsOfDivinity.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~CliffsideArenaTests|FullyQualifiedName~AuthoredLevelValidationTests|FullyQualifiedName~MobaMovementTests|FullyQualifiedName~CastleTransfer"
Godot --path . --resolution 1280x720 Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn -- --beta-smoke-test
```

`Godot` denotes the installed 4.7.2 mono executable, not a new repository command. The final command now passes the complete Cliffside broad smoke. To verify the original beta as well, replace the scene argument with `Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn`. For the targeted rendered picking/stair check, run:

```text
Godot --path . --resolution 1280x720 Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn -- --export-authored-layout=D:/SoftwareEng/Game/artifacts/stage22_acceptance_layout.json --verify-tile-picking --verify-stair-preview
```

The layout export argument requires a filesystem path, not a `res://` resource path. No Meshy calls, Blender changes, new models, wall cosmetics or credits were used. Calibration scripts and assets were reused unchanged. No Stage 2.3 or future asset work was started.
