# Stage 3.0D.2C — Forward-castle stairs and cliff repair

Completed 2026-11-16; stopped for visual review. Both playable lower-lane connections now descend directly from the existing southern forward-castle gates. The old home-to-fort corridor stairs are retired, blocked and filled with continuous rocky cliff. This stage intentionally changes the authored traversal graph.

The [zero-based coordinate plan](Stage30D2CLayoutPlan.md) was produced and checked against the native scene before gameplay geometry changed. A read-only connectivity preflight confirmed compatible castle entrances, elevations and mirrored routes; no castle footprint needed moving.

## Authoritative layout

| Feature, zero-based | West | East |
|---|---|---|
| Retired stair cells | X4–5,Y15–16 | X24–25,Y15–16 |
| Unchanged south gate /upper landing | X8–9,Y14 | X20–21,Y14 |
| New descending stair cells | X8–9,Y15–16 | X20–21,Y15–16 |
| Lower landing /farm clearing | X8–9,Y17 | X20–21,Y17 |
| Unchanged castle footprint | X6–11,Y8–14 | X18–23,Y8–14 |
| Unchanged capture cells | X8–9,Y10–11 | X20–21,Y10–11 |
| Unchanged transfer landings | X7–9,Y12–13 | X20–22,Y12–13 |

New world-space stair openings are **X7.25…9.75** and **X22.25…24.75**, both Z22.25…24.75. Each2.5-unit opening matches the existing two-column south gate. World placement still derives from X=−2.125+1.25x, Z=4.125+1.25y.

Exactly **16 terrain records** change. Old row15 stairs become blocked `cliff`, elevation0; old row16 stairs become ordinary `flank`, elevation−3. New row15/16 cells become `stairs`, elevations−1/−2 and unblocked. Upper landing remains level0; lower landing remains level−3. Stair-center heights remain−0.6 /−1.2, with the reused ten-tread profile−0.12,−0.36,−0.60,−0.74,−0.86,−0.96,−1.08,−1.20,−1.44,−1.68.

The old `west_outer_flank` /`east_outer_flank` connection records become `west_forward_castle_jungle` /`east_forward_castle_jungle`. Their footprints now cover both gate columns,Y13–17. Forest-to-middle connection records and the southern-flank route remain unchanged. The map remains30×20,86 blocked /514 walkable cells; all514 walkable cells remain connected.

Castle footprints, all gates, capture/deployment regions, camps, home hubs, spawns, ten character fixtures, objective data and existing sight flags are byte-for-byte unchanged in the native metadata export. All other cliff crossings are blocked. Physical use of the stairs remains independent of fort ownership, including enemy-controlled forts. Home hubs remain non-capturable and transfer-capable. No5v5,15VP, activation, combat, capture or transfer rule changed.

## Routes and competitive implications

The intended route is now **home side → forward castle → southern gate → lower farm lane**. Southern wall cells flank the gate and the surrounding row15 cliff remains blocked, so the upper approach runs through the existing castle opening. Gate-to-lower-landing traversal costs **3 ordinary steps in either direction on both sides**, compared with15 steps from those same gate tiles before relocation.

Static shortest paths from mirrored starting slots to the nearby camp, ignoring living occupancy, are:

| Spawn row, west/east mirrored | Before | After |
|---|---:|---:|
| Y8 |15 |15 |
| Y9 |14 |14 |
| Y10 |13 |13 |
| Y11 |12 |12 |
| Y12 |11 |13 |

Detailed fixture-specific costs are in `SourceAssets/Environment/ForwardCastleStairs/Validation.json`. Tests check mirrored costs from the two gates to every reachable destination, ascent/descent in both columns, blocked former access, and normal scout/colossus movement. The existing all-starting-slot movement tests continue to exercise travel across activations with occupied fixtures present.

Forward castles now provide much shorter farm access once reached. The low starting pair loses its old two-step route advantage; this change is mirrored. Castle control itself does not lock passage, so ownership rules are unchanged. Camps still occupy the existing walkable X8–9 /20–21,Y17–18 clearings immediately beyond the descents; no NPC blocker or camp reward rule was added. Human match/balance review remains necessary despite symmetric path costs.

The existing sight policy is preserved: cliff terrain blocks movement without automatically adding a sight blocker. No new elevation-based LOS rule was introduced. Tests cover old/new crossing sight flags and mirrored sight behavior.

## Environment integration and reuse

Live Blender MCP derived a new junction from existing western-slice, connected-cliff, lower-terrace and fort-masonry sources. It retains their UVs, vertex tints and material direction. Triangles are actually clipped out of the new stair apertures. The old holes receive recessed backing, varied existing rock forms and irregular grassy crowns. Their former floor notches are filled, and old tread/paving assemblies are hidden so they no longer advertise access.

The finished lower floor's original mesh/UVs and material maps are reused; four triangles fill its two former notches. Existing lower-ground, cliff, western/eastern stair and fort-integration source files and runtime assets remain intact. Original material/texture files are unchanged. The derived cliff mesh removes intersecting rubble at the new apertures, while preserving the surrounding terrain and rock language.

Both descents instance the existing optimized eastern native stair scene, translated by−20 and−5 world-X units from its old center28.5. They share its three MultiMesh slab resources, detail meshes and framing rocks. Reused dressed nosings, retaining courses and a short upper apron align with the south gates; closed threshold risers eliminate the gap under the gate floor. Full legal width stays clear. The lower apron meets the retained dirt floor at its existing datum.

The first import exposed missing transforms on unlinked appended rock datablocks; explicit authored transform composition restored their placement. The visual correction also closed the threshold seams and removed coplanar crown overlap. Final captures supersede those intermediate previews.

| Deliverable | Location |
|---|---|
| Editable junction plus stair reference preview | `SourceAssets/Environment/ForwardCastleStairs/ForwardCastleStairs.blend` |
| Runtime junction, five material batches | `Assets/Levels/Environment/ForwardCastleStairs/CastleCliffJunction.glb` |
| Reusable visual wrapper | `Assets/Levels/Environment/ForwardCastleStairs/ForwardCastleStairs.tscn` |
| Battlefield placement | Identity root `ForwardCastleStairs` in `CliffsideArena.tscn` |
| Layout/environment/audit records | `AuthoredChanges.json`, `EnvironmentComposition.json`, `Preflight.json`, `Validation.json` in the source directory |
| Task-start backups | `SourceAssets/Environment/ForwardCastleStairs/Baseline/` |

Materials reuse the western rock/ground/understory/stone resources, the existing lower-ground material and the existing dressed eastern material. Sources retain the previously verified Quaternius Standard CC0 geometry and the already cleared lower-terrain material content. **No new texture, third-party download or paid Meshy request** was needed.

Junction geometry totals **30,905 triangles**: rock12,068; adjacent ground3,815; understory2,702; lower ground6,584; castle masonry5,736. With both instanced stair assemblies, the visible replacement environment has **31,898 unique triangles,41,139 instance-expanded triangles,12 unique meshes and21 surfaces**. The GLB is3,899,056bytes with zero embedded images. Existing maps are shared. A view-specific engine counter reports2,176 total draw calls for the final full-cliff view; no controlled FPS or target-hardware regression claim is made. Hidden legacy resources remain loaded for reversible comparison.

## Gameplay, native geometry and visual validation

| Check | Result /evidence in `artifacts/stage30d2c/` |
|---|---|
| C# solution build | Passed, zero warnings/errors — `build.log` |
| Focused movement/elevation/castle/grid/sight regressions | **118 passed,0 failed/skipped** — `tests_final.log`, `tests/stage30d2c_final.trx` |
| Actual floor-click animation | **16 traversals**: scout and colossus, both columns of both castles, ascent/descent — `animated_traversal.log`, `animated_traversal.json` |
| Full Cliffside smoke | Passed combat, abilities, captures/transfers, VP/victory, timer, HUD, camera/grid, authored round-trip and free testing — `smoke.log` |
| Native stair previews /tile picking | Passed; **4,800** centers across eight camera orientations — `after_layout.log` |
| Imported standing geometry | **154** walkable centers tested against actual triangles within0.026 tolerance — `geometry.json` |
| Cliff repair /new apertures | All four old stair-column rays hit solid cliff faces; no geological triangles occupy the new openings |
| Optional grid |127 authored box segments retain one batched runtime surface;60 exposed tread-grid samples clear the stairs; native G/rebinding/input tests pass |
| Decorative collision/navigation | Zero nodes in the replacement hierarchy |
| Preservation audit | **372 original files unchanged**; only the scene, native verification helper and three existing stair-test fixtures were revised; eight new relocation cases added |

The native verification helper now derives stair columns from authored `TerrainTag` data instead of assuming X4–5 /24–25. This changes verification only. Direct legacy `Surface`/`Tread` visuals are retained under hidden per-tile containers; new hidden floor/tread references match the relocated elevations used for picking, movement overlays and serialization. Old grid segments remain under a hidden legacy container, with new authored wires following the current floor/tread profile and the existing G batcher/input handler.

Native metadata audit: **16 changed terrain records, two replaced connection records and687 unchanged records**. The old layout hash was `D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86`; the intentionally changed layout hash is **`B77CB74DCCE5B9DF8A1F888C11593FBDAEAE3AF214D28D461AD01D963FAE8758`**.

Blender source and Godot output were visually inspected; Windows computer control observed both castle descents in the running battlefield. Required captures:

- [Overhead, both new positions](../artifacts/stage30d2c/overhead.png).
- [Western castle staircase](../artifacts/stage30d2c/west_castle_stairs.png) and [eastern castle staircase](../artifacts/stage30d2c/east_castle_stairs.png).
- [Repaired old western opening](../artifacts/stage30d2c/old_west_repaired.png) and [repaired old eastern opening](../artifacts/stage30d2c/old_east_repaired.png).
- [Normal tactical camera, grid OFF](../artifacts/stage30d2c/tactical_off.png) and [grid ON](../artifacts/stage30d2c/tactical_on.png).
- [Full connected cliff](../artifacts/stage30d2c/full_cliff.png), [stair grid detail](../artifacts/stage30d2c/east_grid_on.png), [unit traversing the stair](../artifacts/stage30d2c/unit_on_stairs.png).

Remaining visual scope: original fort/camp/ruin greybox elements and the southern outer cliff perimeter remain unchanged. The reused descents share stone geometry; asymmetry in nearby rock composition is decorative and does not change legal width or routes. Review windows launched with `--stay` intentionally freeze gameplay; the ordinary battlefield scene remains playable.

Headless editor import retains the documented dummy-texture/CanvasItem/ObjectDB shutdown diagnostics; final runtime/geometry/gameplay checks pass. Backup C# files use `.cs.snapshot` extensions to stay outside the compiler glob. Earlier failed build/import-placement previews are superseded by the final passing logs and `final_complete.log`.

## Reproduction, rollback and stop

Tools: `PlanStage30D2C.py` (pre-change plan/snapshot), `IntegrateStage30D2C.py` (authored relocation from that snapshot), `BuildStage30D2CEnvironment.py` (Blender junction), `VerifyStage30D2C.gd`, `PlaytestStage30D2C.gd`, `AuditStage30D2C.py`. Import GLBs to completion before visual/gameplay checks. Source stair reference objects are preview-only and excluded from the junction export; runtime uses the existing stair PackedScene.

To roll back, restore the five changed original files—the battlefield scene, native verification helper and three existing stair-test files—using `.cs.snapshot` copies for code/tests and preserving later unrelated edits. Remove or adapt the new relocation-specific tests when returning to the prior layout. The derived wrapper/GLB can remain unused. Original completed assets are retained; five previous visual root instances and their old geometry are hidden rather than deleted. Earlier stage reports/verifiers describe their original coordinate layouts; use Stage3.0D.2C validation for the relocated map.

Existing dirty work was preserved; no commit or following production stage was started. SourceAssets, GLBs, screenshots and logs follow the existing ignore policy and need project-asset backup alongside Git.
