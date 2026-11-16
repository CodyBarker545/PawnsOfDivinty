# Stage 3.0D.2A — Lower terrace connected ground

Completed 2026-11-16; stopped for visual review. The whole lower farming terrace now has continuous earth, curved worn paths, muted cliff-foot moss and sparse merged stones. Existing cliffs, both stairs, camps, architecture and gameplay systems are preserved. No Meshy requests or paid generation were used.

## Construction and rollback

`Tools/BuildStage30D2ATextures.py` blends the existing Brown Mud, Stony Dirt Path and Mossy Rock diffuse maps into a unique 2048×512 landscape map. Smooth world-space masks curve the main path and connect both staircase mouths; warped source sampling and broad color grading reduce repetition and photographic detail. The second map is a restrained normal derived from the blended color variation, **not** the original high-frequency normal/displacement maps. Material roughness is a constant 0.94, metallic zero, normal strength 0.16. No reflective mud or physical displacement is used.

`Tools/BuildStage30D2AGround.py` builds one connected mesh, X −2.75…34.75 / Z 22.8…28.5, with notches under the two unchanged stair runs until Z24.75. North coverage is tucked beneath the cliff foot. The ground is flat at Y−1.787, 0.013 above logical Y−1.8; there are no misleading slopes or shortcuts. Fifty-two small stones are joined into one second mesh near the cliff foot, clear of stair mouths and the main route.

- Editable packed source: `SourceAssets/Environment/LowerTerrace/LowerTerrace.blend`.
- Runtime: `Assets/Levels/Environment/LowerTerrace/{LowerTerrace.glb,LowerTerrace.tscn,Ground.tres,LowerTerrainColor.png,LowerTerrainNormal.png}`.
- Placement: identity `LowerTerraceEnvironment` instance in `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`; no collision, navigation, gameplay script or metadata dependency.
- Source evidence: `SourceAssets/Environment/LowerTerrace/{Construction,Provenance,HiddenLegacyNodes}.json`.
- Before scene and protected hashes: `SourceAssets/Environment/LowerTerrace/Baseline/`.

Only 78 additional overlapping original `Surface` visuals are hidden, all lower-terrace cells; already hidden prior-stage surfaces remain hidden. All gameplay parents, original floors, standing metadata, stair treads and collision remain present. The lower portions of previous merged cliff/stair ground meshes remain underneath this surface with 0.011 clearance; their upper portions and source files are untouched. Existing landing paving remains visible. Toggling the new scene hides its terrain and rubble; a complete old-ground comparison additionally restores the recorded old surface visibility, or loads the baseline scene. No prior asset is deleted. `LowerTerrace.blend1` is a normal Blender backup, and `BlenderSessionBeforeReview.blend` preserves the previously connected unsaved Blender scene; neither is a runtime asset or an interrupted export.

## Provenance

The existing license manifest was checked before promotion. The three named diffuse sources correspond to publisher pages for [Brown Mud](https://polyhaven.com/a/brown_mud), [Stony Dirt Path](https://polyhaven.com/a/stony_dirt_path), and [Mossy Rock](https://polyhaven.com/a/mossy_rock). Poly Haven explicitly licenses its assets under CC0 and permits commercial use and redistribution. See the [publisher license](https://polyhaven.com/license). Local file paths and SHA256 hashes are recorded in `Provenance.json` and in the manifest's narrowly scoped `stage30d2a_material_clearance` entry. Other asset holds remain in force.

Identity verification uses the named extracted maps and matching publisher asset pages; original download receipts and a publisher digest comparison are unavailable. The public API returned 403, so no exact publisher hash match is claimed. Original archives, extracted maps, and previous inventory records are preserved. No new material download was needed.

## Visual review

Background Blender created/exported the mesh. After the user enabled port9876, live Blender MCP successfully opened and inspected the source and packed the final corrected maps. Windows computer control inspected the material-preview source and the Godot staircase transition. Godot's verification script captured actual rendered views, with the normal tactical camera preserved for the full HUD image.

The first review demonstrated connected terrain but earth highlights were too bright under the existing sun. One focused correction darkened/desaturated the baked map and reduced contrast; geometry, lighting and gameplay were unchanged. Final images are in `artifacts/stage30d2a/`:

| View | Before | Final |
|---|---|---|
| Normal tactical camera with HUD | `before_tactical.png` | `after_tactical.png` |
| Entire lower terrace, grid OFF | `before_lower_off.png` | `after_lower_off.png` |
| Entire lower terrace, grid ON | `before_lower_on.png` | `after_lower_on.png` |
| Texture/material detail | `before_close.png` | `after_close.png` |
| Western landing | `before_west.png` | `after_west.png` |
| Eastern landing | `before_east.png` | `after_east.png` |

The floor's square appearance is eliminated with grid OFF; material boundaries run across logical cells. The optional grid remains legible above the ground (lowest lower-grid top Y−1.768, 0.019 clearance). Existing sparse plants and landing paving integrate with the new ground. Camp figures, square ruin/blocker cubes, regular southern perimeter crags and central/upper greybox surfaces remain visible and are outside this stage. The outer cliff silhouette and camp architecture still need later art work; this stage does not claim to finish them. Broader human match readability and target-hardware profiling remain future review gates.

## Validation and cost

- C# build passed, zero warnings/errors (`build.log`).
- All **110** focused terrain/stair/grid/movement/picking/layout tests passed (`tests.log`, `tests/stage30d2a.trx`), including native grid-input regression checks.
- Full Cliffside smoke passed: movement, combat, abilities, capture/transfers, VP/victory, timer, HUD, targeting, camera/grid and authored round-trip (`smoke.log`).
- Native tile picking passed **4,800** checks at eight camera orientations; authored stair and round-trip checks passed (`layout.log`).
- Exported 600-cell layout SHA256 remains `D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86`, identical to Stage3.0D.1; 86 blocked /514 walkable, ten original fixtures.
- `Tools/VerifyStage30D2A.gd` passed all **116** non-stair lower-floor center support checks within 0.026 tolerance, two surfaces and zero collision/navigation nodes (`final_visual.log`, `after_geometry.json`). Existing stair centers remain validated independently by native tests.
- All **237** snapshotted gameplay/previous environment files retained their hashes. No C# gameplay source was changed for this stage.

The final scene was also opened normally and its lower ground inspected through computer control. A Windows-injected G keystroke did not visibly toggle the grid in that session; a physical keyboard press was not observed. This automation limitation is recorded separately from the passing native InputMap/key-event tests and rendered ON/OFF captures; no grid/input code was altered for the ground stage.

Ground: **6,580 triangles**. Rubble: **1,040 triangles**. Total **7,620**, two mesh instances, two surfaces, two shared materials. GLB:258,868bytes. Two 2048×512 maps; conservative RGBA8+mipmap budget approximately **10.67MiB** combined, with actual import/driver allocation dependent on renderer. Original 4K sources are not runtime textures.

Same lower-lane view on GTX1660Ti /Compatibility renderer /1280×720: engine total draw calls **2,396 before →2,191 after** (205 fewer); rendered primitives615,515→614,805. New geometry contributes two base-pass surfaces, with additional rubble shadow-pass cost dependent on lighting. The decrease comes from hidden old floor surfaces. Measured texture allocation120,064,553→128,453,171bytes (about8MiB increase). Video-memory counters fluctuated across processes and are not a reliable isolated asset budget. These are view-specific counters, not a target-hardware frame-time benchmark; no FPS regression claim is made.

Initial headless editor import emitted the previously known dummy-texture/CanvasItem/ObjectDB shutdown diagnostics; final import and runtime visual/smoke logs were clean. Git work already present before this stage was preserved. SourceAssets, GLBs, PNGs and artifacts follow the existing ignore policy and need asset backup alongside Git. No commit or next-stage work was performed.
