# Stage 3.0D.1 — Toggleable tactical grid overlay

Completed 2026-11-16. Cliffside starts with grid lines **hidden**; press **G** to show them and again to hide them. The original MOBA beta retains its visible-by-default grid. No terrain, character, movement, pathfinding, elevation or combat rule was changed, and no environment asset, Blender edit or paid call was made.

## Audit and implementation

The visible boundary lines came from the existing `GridOverlay` scene node, not from baked ground textures or terrain shaders. Cliffside had **141 separate box-mesh grid-line objects**, including authored elevated and stair segments. The shared battlefield script already had a hard-coded G toggle, but its default was visible and the key was not an InputMap action. The original beta uses the same battlefield script with one existing grid-line mesh.

The change keeps this dedicated visual responsibility. `MobaBetaBattlefield.TacticalGrid.cs` compiles compatible, same-material Cliffside line meshes into a single runtime `ArrayMesh`/surface under `GridOverlay`. It preserves their authored transforms, normals and stepped elevation profile, with a **0.025-unit upward lift** to clear current ground and paving relief. The original authored line nodes remain recoverable and are hidden only in the runtime instance. The compiled line mesh casts no shadows and has no physics/collision or gameplay metadata. It is created once at scene initialization and is never saved into terrain assets.

The beta's already efficient one-mesh renderer remains unchanged. Unsupported future grid primitives or differing materials fall back to the authored rendering rather than being silently merged incorrectly. There is no per-frame mesh regeneration. Turning the root overlay off renders none of its boundary lines; movement and targeting overlays are not children of this node.

Existing authored line geometry continues to encode the 30×20 board's dimensions/world placement. Verification compares the compiled vertices against transformed authored line bounds, including lower and stair heights, and checks the board edges against `Layout.Board` cell centers and cell size. Logical cell coordinates and elevations are not rebuilt from the visual mesh.

## Input, defaults and UI

- Input action: `toggle_tactical_grid`, defined in `project.godot`.
- Default event: physical **G**, so it can be rebound through the existing Godot InputMap mechanism.
- Runtime handling: `_UnhandledInput`; pressed events toggle once, echo/repeat and release do not toggle. Unbound modifier chords do not trigger the action.
- Text entry: focused `LineEdit`/`TextEdit` controls in the main viewport or the tactical SubViewport prevent a toggle. UI-consumed events remain consumed; no global `_Input` interception was added.
- Scene configuration: exported `TacticalGridInitiallyVisible` defaults to `true` for compatibility. Cliffside explicitly sets it to `false` and hides its authored `GridOverlay` in the scene; runtime initialization applies the scene-level setting. The original beta scene file was not edited.
- Existing F1 help now includes **G — Toggle Grid**. No new HUD panel or settings interface was introduced. This is a per-scene-load default, not a saved player preference; reset/match actions do not forcibly change the player's current grid visibility.

Changing the InputMap binding works without changing the handler; the native test replaces G with J temporarily, verifies both keys, and restores the binding. The help text describes the default G binding; a future rebinding UI can provide dynamic binding labels.

## Independent gameplay indicators

The optional overlay never owns selection rings, movement-range outlines, planned routes, valid/invalid hover indicators, attack target rings, capture/ownership markers or ability visuals. Those existing systems remain on their original parents and keep their materials/visibility. Verification checks non-grid mesh visibility across toggles, active movement/path/hover feedback while the grid is hidden, selected-unit rings, and normal target highlighting. Complete gameplay smoke additionally exercises abilities, capture, transfers, combat, camera controls and HUD behavior.

The user-visible distinction matters: mint cell outlines can still appear with the grid OFF when a unit has legal movement available. They are movement feedback, not the permanent tactical grid.

## Visual verification

The game was launched in Godot 4.7.2 .NET at 1280×720. OFF and ON captures use the same camera, scene and terrain. Selected and targeting captures use temporary runtime verification state only; no fixtures were saved or moved in the authored scene.

- [Grid OFF — clean terrain](../artifacts/stage30d1_CliffsideArena_off.png).
- [Grid ON — tactical boundaries](../artifacts/stage30d1_CliffsideArena_on.png).
- [Selected unit and movement feedback, grid OFF](../artifacts/stage30d1_CliffsideArena_selected_off.png).
- [Selected-unit close-up, grid OFF](../artifacts/stage30d1_CliffsideArena_selected_close_off.png).
- [Targeting feedback, grid OFF](../artifacts/stage30d1_CliffsideArena_target_off.png).

Inspected the matching views and indicator captures. The former permanent lines disappear in OFF mode. In ON mode the lines remain readable over current ground, paving and authored stair heights; depth testing keeps them from painting over characters and structures. No flicker or missing walkable-surface grid segment was observed in this review. The remaining greybox's square color patches and geometry edges are still visible in places; they are not baked grid lines and were intentionally not retextured in this stage. Higher future sculpted ground relief may require adjusting the visual lift/profile; the grid is not an always-on-top x-ray overlay.

## Validation

| Check | Result |
|---|---|
| C# build | Passed, zero warnings/errors; `artifacts/stage30d1_build.log` |
| Focused grid/input/terrain/stair/movement/picking suite | **110 passed, 0 failed, 0 skipped**; `artifacts/stage30d1_tests/stage30d1.trx` |
| Native grid verification — Cliffside and beta | Two new parameterized engine tests pass; defaults, G twice, echo/release, modifier handling, text focus, rebinding, alignment, indicator independence, selection/targeting and camera controls |
| Cliffside grid batching | 141 authored mesh objects → **one visible grid surface** while ON; OFF hides the complete boundary overlay |
| Original beta compatibility | Existing single renderer and initial visible grid retained; native input test and full beta smoke pass |
| Complete Cliffside smoke | Passed; `artifacts/stage30d1_smoke_CliffsideArena.log` |
| Complete original beta smoke | Passed; `artifacts/stage30d1_smoke_MobaBeta.log` |
| Native stair/picking checks | Passed, including 4,800 tile-center checks across eight camera orientations; `artifacts/stage30d1_layout.log` |
| Logical data preservation | Export byte-identical to Stage 3.0C; SHA-256 `D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86` |

`TacticalGridOverlayTests.cs` launches the actual scenes in headless Godot and checks the report from the explicit `--verify-tactical-grid` test path. That path exercises the actual InputMap/viewport event dispatch and existing selection/movement/targeting code; it does not replace normal gameplay handling. `--capture-grid-review` additionally produces the rendered evidence. The verification partial runs only under these explicit launch flags and never saves a scene.

An initial build attempt exposed that `.cs` baseline copies under `artifacts` were included by the project-wide compiler glob; those task-created copies were renamed `.cs.snapshot`. A subsequent compile error from calling the primitive-type API on the base `Mesh` type was corrected to handle `ArrayMesh` and authored `BoxMesh` explicitly. Final builds and all validation pass. Unrelated pre-existing worktree changes were retained.

## Files and stop point

Implementation: `Scripts/Gameplay/Levels/MobaBetaBattlefield.TacticalGrid.cs`, the small initialization/input hooks in `MobaBetaBattlefield.cs`, `project.godot`, the Cliffside scene default and the existing help label in `MobaTacticalHud.cs`. Verification: `MobaBetaBattlefield.TacticalGridVerification.cs` and `Tests/PawnsOfDivinity.Tests/Levels/TacticalGridOverlayTests.cs`. No environment/character asset files or original beta scene were modified.

Stop after Stage 3.0D.1. Ground texturing, terrain reconstruction, HUD redesign and later production stages were not started.
