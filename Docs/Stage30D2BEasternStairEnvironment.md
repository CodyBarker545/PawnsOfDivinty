# Stage 3.0D.2B — Eastern staircase environment integration

Subsequent focused correction: [fort-to-lower-lane visual integration](CliffsideFortStairVisualIntegration.md) aligns both lower approaches and the eastern stair presentation with the side-gate masonry. The measurements below remain the original Stage3.0D.2B delivery record.

Completed 2026-11-16; stopped for user visual review. The eastern placeholder now has weathered stone slabs, recessed masonry risers, broken low retaining cheeks, restrained moss, small erosion marks, sparse grass and rock framing. The western staircase, connected cliffs and lower terrace were reused without editing their files or rebuilding their geometry. No Meshy or other paid generation was used.

## Placement and construction

The staircase occupies its existing world-space opening: **X27.25…29.75, Z22.25…24.75**. Tile columns24–25 /rows15–16 retain their original transforms, metadata, movement and elevations. Both columns preserve the ten tread tops: **−0.12, −0.36, −0.60, −0.74, −0.86, −0.96, −1.08, −1.20, −1.44, −1.68**. Upper terrain remains Y0; lower logical terrain remains Y−1.8, with the unchanged lower visual surface at Y−1.787.

Live Blender MCP extracted three connected slab components from the western source's existing stone mesh. Their beveled/chipped geometry, UVs and vertex colors were normalized into a small reusable library. **Thirty tread slabs, ten buried cheek pieces and seven landing fragments share those three mesh datablocks**. Staggered widths, different donor assignments, limited moss tint, cracks and an asymmetric rock/grass arrangement distinguish the eastern composition. This is not a mirrored copy of the western assembly.

A newly authored closed backing fills the stair mass beneath the slabs. Four short erosion marks are included in that mesh. The initial dark/recessed risers made the slabs appear detached. One focused riser correction brought the backing closer to the stone tops, matched its tint to the masonry and recessed its fronts0.025 units to eliminate near-coplanar striping. Final tread top heights did not change. Three tiny grass clumps are merged into one mesh. Three framing instances reuse existing optimized `FractureRib` and `SeamBoulder` meshes; their vertices stay outside the full two-column corridor and their bases are buried.

Only local paving fragments bridge the stair mouths. The three upper fragments sit at Y0.018; four lower fragments sit at Y−1.776, 0.011 above the finished dirt. No surrounding ground is regenerated, recolored or replaced. All decorative meshes have no collision or navigation.

## Files, materials and provenance

| Deliverable | Path |
|---|---|
| Editable source with linked slabs | `SourceAssets/Environment/EasternStair/EasternStair.blend` |
| Library export, three meshes | `Assets/Levels/Environment/EasternStair/SlabLibrary.glb` |
| Backing/erosion and grass export | `Assets/Levels/Environment/EasternStair/EasternDetails.glb` |
| Runtime scene | `Assets/Levels/Environment/EasternStair/EasternStair.tscn` |
| Exact placements and source hashes | `SourceAssets/Environment/EasternStair/Composition.json` |
| Task-start scene and protected hashes | `SourceAssets/Environment/EasternStair/Baseline/` |
| Hidden treads and grid adjustments | `SourceAssets/Environment/EasternStair/{HiddenLegacyNodes,GridVisualAdjustments}.json` |
| Scope/export audit | `SourceAssets/Environment/EasternStair/Validation.json` |

The runtime root is an identity `EasternStairEnvironment` instance in `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`. The native scene embeds three shared slab resources and two detail meshes, and references the existing rock scenes. The GLBs are repeatable import/bake sources; runtime does not load duplicate slab-library geometry alongside the embedded meshes.

All materials are existing western resources: **`SliceMaterial3.tres`** for slabs/backing, **`SliceMaterial2.tres`** for the reused cliff rocks and **`SliceMaterial1.tres`** for grass. They retain linear vertex-color albedo, roughness0.9, metallic0, normal strength0.2 and the same two shared512×512 maps. **Zero new textures or material resources** were created. Both new GLBs contain zero embedded images.

The stone donors were authored for Stage3.0B. The rock library and original `Grass_Common_Short.gltf` derive from Quaternius's Stylized Nature MegaKit Standard. Its preserved `SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/License_Standard.txt` explicitly covers the free68-model edition under **CC0-1.0**. The license, existing inventory clearance and exact source hashes were checked. Original packs and sources are unchanged; no held third-party source was promoted.

## Integration and optional grid

All **20 original eastern `Tread0…4` nodes remain present and are hidden only visually**. Their original geometry, transforms, material and gameplay parents are retained for authored-height/picking checks and rollback.

Inspection found that two old eastern row-15 longitudinal grid wires used the whole cell's Y−0.6 center height, burying parts beneath higher treads. Only that local visual profile was fitted: `Column_24_15` and `Column_25_15` now describe the first tread, eight additional box segments describe the remaining four treads, and `Row_24_15` is lifted to the first tread. The existing runtime batcher still compiles the grid into **one shared surface**. G input, default visibility, logical cell coordinates, western wires and independent movement/target indicators are untouched. Thirty exposed top-face samples pass clearance checks; three samples at riser/nosing edges are excluded because a wire endpoint can meet an adjacent vertical riser.

The scope audit compares every original scene node against the task-start scene: the only existing-node changes are **20 visibility properties and three eastern grid visual transforms**. Nine nodes are added: eight grid segments and the environment instance. No original nodes are removed. Rollback removes those additions, restores the20 tread visibilities and three grid transforms from the baseline. Use the recorded changes rather than restoring an old full scene over later work.

`McpSessionBefore.blend` preserves the prior connected Blender session. `EasternStair.blend1` is a normal previous-revision backup. Neither is used at runtime. Both GLBs have complete headers/buffers and distinct hashes; no interrupted export is used.

## Visual evidence

Blender MCP modeled and inspected the source, and Windows computer control inspected the Blender viewport and running Godot staircase. Godot rendered these same-camera before/after images at1280×720:

| View | Before | Final |
|---|---|---|
| Original tactical camera and HUD | [before](../artifacts/stage30d2b/before_tactical.png) | [after](../artifacts/stage30d2b/after_tactical.png) |
| Both staircases | [before](../artifacts/stage30d2b/before_both_stairs.png) | [after](../artifacts/stage30d2b/after_both_stairs.png) |
| Eastern close-up, grid OFF | [before](../artifacts/stage30d2b/before_east_close.png) | [after](../artifacts/stage30d2b/after_east_close.png) |
| Eastern close-up, grid ON | [before](../artifacts/stage30d2b/before_east_grid_on.png) | [after](../artifacts/stage30d2b/after_east_grid_on.png) |
| Lower dirt transition | [before](../artifacts/stage30d2b/before_lower_transition.png) | [after](../artifacts/stage30d2b/after_lower_transition.png) |

![Final eastern staircase](../artifacts/stage30d2b/after_east_close.png)

Final review shows recognizable slate steps embedded between the existing cliff faces, no floating framing rocks or ground gaps, and paving dissolving into the finished dirt. Both stairs use compatible values and weathering, while their joints and details differ. Units, team markers and HUD remain readable in the unchanged normal tactical view; movement/selection/target independence passes native tests. `--stay` verification windows intentionally freeze gameplay for visual review and are not ordinary play sessions.

Remaining limitations: surrounding fort/camp architecture, central terrain and southern perimeter still contain greybox forms outside this stage. Small bevels and fine erosion marks mainly benefit close-up inspection; slab repetition remains perceptible at close range. Riser edges can naturally interrupt tiny portions of grid wires, although exposed tread interiors remain clear. User art acceptance, human match readability and target-hardware frame-time profiling remain pending.

## Validation and optimization

| Check | Final result / evidence in `artifacts/stage30d2b/` |
|---|---|
| C# solution build | Passed, zero warnings/errors — `build.log` |
| Stair/elevation/movement/layout/picking/grid tests | **110 passed, zero failed/skipped** — `tests_final.log`, `tests/stage30d2b_final.trx` |
| Eastern and western movement commits | Both columns, ascent/descent pass existing regression cases; cost/path/acknowledged destination preserved |
| Complete Cliffside smoke | Passed combat, abilities, movement, captures/transfers, objectives/VP, victory, timer, HUD, camera/grid, authored round-trip and free testing — `smoke_final.log` |
| Native all-cell picking /stairs /round-trip | **4,800** checks across eight camera orientations pass — `layout.log` |
| Imported stair surfaces | **20** tread-center samples pass; maximum height error below0.000001 units — `final_review.log`, `after_geometry.json` |
| Grid presentation |30 exposed top-face clearance checks pass; native G/rebinding/text-focus/indicator tests pass; one batched grid surface retained |
| Collision /side clearance | No collision/navigation nodes; framing rocks outside the legal corridor |
| Preservation audit | **361 protected files unchanged**;20 tread visibility +3 grid visual transform changes only — `Validation.json` |

The exported600-cell gameplay layout remains byte-identical to Stage3.0D.2A and earlier verified stages: SHA256 **`D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86`**. Board30×20,86 blocked /514 walkable cells, ten original fixtures remain unchanged. No gameplay C# or existing test source was edited.

| Geometry | Unique triangles | Instance-expanded triangles |
|---|---:|---:|
| Three reused slab templates,47 uses |276 |4,324 |
| Backing/erosion |108 |108 |
| Three merged grass clumps |465 |465 |
| Existing fracture/boulder library,3 uses |144 |220 |
| **Total** |**993** |**5,117** |

Only849 unique triangles are newly stored in this scene; the144 rock-library triangles are shared with the existing cliff. Runtime has **seven unique mesh resources, eight surfaces/render instances, three existing materials**. The47 slab objects in Blender become three MultiMesh draw batches. New GLBs total **94,692bytes**; the packed native scene is about126KiB. Extra texture-memory content is **zero**.

Same angled both-stair view, Compatibility/OpenGL, GTX1660Ti,1280×720,120 sampled frames after90 settling frames: median total draw calls **2,221 before →2,201 after**; renderer primitives686,313→691,803. Whole-scene counters include shadows, units and HUD; they are not unique mesh counts or an isolated FPS benchmark. No frame-time regression claim is made.

Headless editor import retains the previously documented dummy-texture/CanvasItem/ObjectDB shutdown diagnostics. The first headless scene bake omitted MultiMesh instance buffers; the baker now rejects headless use and saves through the real renderer. Final native visual, smoke and input-test runs pass without runtime resource errors. Earlier failed inspection logs are superseded by `final_review.log`.

## Reproduction and stop point

Tools: `BuildStage30D2BEasternStair.py` (live Blender or background), `BakeStage30D2BScene.gd` (real renderer required), `Stage30D2BScene.py --integrate`, `VerifyStage30D2B.gd` and `AuditStage30D2B.py`. Import GLBs to completion before baking, then integrate and validate. Builders are for intentional asset revisions, not routine validation; do not regenerate completed western/lower assets. The integration script edits only its recorded local nodes and is repeatable.

Existing dirty worktree changes were preserved. SourceAssets, GLBs, screenshots and logs follow the existing ignore policy and require project-asset backup alongside Git. No commit, paid call or next-stage work was performed.
