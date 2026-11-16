# Pawns of Divinity — 30×20 Environment Production Roadmap

Version: 2.2 | 2026-11-16 | Production status below is evidence-based; remaining P-stages are planning scope.

## Current delivery status — Stage 2.3A

Latest delivery: **Stage3.0D.2C forward-castle staircase relocation complete; stopped for visual review.** The playable descents now align with the unchanged southern gates: west X8–9 /east X20–21,Y15–16. Old X4–5 /24–25 openings are blocked and repaired with continuous cliff/ground. Sixteen terrain records and two connection records change; castle footprints/capture/transfer regions, camps, fixtures and combat rules remain unchanged. All514 walkable cells remain connected and mirrored. Gate-to-lane travel costs3 steps on both sides. Build,118 tests,16 animated stair traversals, full smoke,4,800 picking checks and154 standing samples pass. See [pre-change coordinate plan](Stage30D2CLayoutPlan.md) and [final layout, art, balance notes and captures](Stage30D2CForwardCastleStairs.md). No following stage has begun.

Previous construction records: [Stage3.0D.2B eastern stair assets](Stage30D2BEasternStairEnvironment.md), [fort-side visual correction](CliffsideFortStairVisualIntegration.md), and [Stage3.0D.2A lower ground](Stage30D2ALowerTerraceGround.md). Stage3.0D.2C supersedes their stair locations while preserving the source assets/materials. Unused licensing holds and remaining upper/central/perimeter/camp art scope remain in force. The section anchor is retained for existing roadmap links.

This status section supersedes the stale “first task” examples and provisional coordinate estimates below. Preserve the original P0–P10 planning structure; numbered delivery stages 1, 2.1, 2.2 and 2.3A track work actually performed and do not imply every P-stage gate has passed. The [v1.1 roadmap](PawnsOfDivinity_Environment_Production_Roadmap_v1.1.md) retains the approved forward-fort audit and expanded art-style gate. Use this file for current completion status, [the art guide](Environment_Art_Style_Guide.md) for material direction, [the Stage 2.2 report](Stage22MaterialVerification.md) for prior acceptance evidence, and [Stage 2.3A cliff documentation](Stage23ModularCliff.md) for the first cliff pair.

### Stage 1 — Greybox and gameplay terrain

Verified on the native `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn` through the final 72 focused tests, Godot extraction and rendered load. The board is 30×20, zero-based X0–29 / Y0–19, north at Y0; mirror X about 14.5. Tile centers: X = −2.125 + 1.25x, Z = 4.125 + 1.25y. Elevation levels are 0.6 world units. The final scene load reports 86 blocked / 514 walkable cells and ten original fixtures. Gameplay data, not decorative meshes, remains authoritative.

| Feature | Verified delivery | Remaining scope |
|---|---|---|
| Lower farming terrace | Rows16–19 at level−3 (Y−1.8), except relocated X8–9 /X20–21 stair cells at row16 remain level−2; existing camp clearings X8–9 /X20–21,Y17–18 retained. Stage3.0D.2C verifies access through both southern castle gates | Camp NPC behavior, rewards, farming economy and human balance remain pending |
| Cliff elevation and stairs | Stage3.0D.2C: row15 blocks descent except X8–9 /X20–21 at level−1, followed by level−2 at row16; upper gate row14 and lower landing row17 unchanged. Old X4–5 /X24–25 stair openings are closed and repaired. Both directions/columns, castle traversal, grid alignment, standing surfaces and4,800 rotated picking checks pass | Human match/balance review, southern outer cliff and other remaining environment art |
| Upper forest paths | Row 3 east–west trail and seven connections to middle verified | Final art and uniform vegetation sight policy pending |
| Traversable trees and bushes | 36 tree / 50 bush tiles; ordinary legal steps cost one MOV, living occupancy preserved | Existing per-cell sight flags retained; terrain tag alone does not define sight policy |
| Impassable rocks and walls | Mirrored blocking cells and relevant movement/sight behavior checked | Final replacement meshes pending |
| Central battlefield and ruins | Four objective cells X14–15/Y10–11; independent approaches, mirrored ruin blocking and sight checks pass | Final ruin kit, objective art and competitive balance pending |
| Forward fort greybox | West X6–11/Y8–14 and east X18–23/Y8–14; each 14 wall cells, four double gates, capture and six landing cells; routes verified | Kit replacement and broader structural/art approval gates retained |
| Home castle transfers | Non-capturable home hubs retained; scout capture → later colossus transfer tested; existing eligibility/action rules preserved; complete broad smoke passes | Human match validation pending |

Historical live movement and transfer observations are in [Stage 1.1](CliffsideArenaStage11.md), [Stage 1.3A](CliffsideForestStage13A.md) and the v1.1 forward-fort audit. Do not treat greybox completion as final art or balance approval.

### Stage 2.1 — One modular wall

Complete as an asset pipeline proof: Meshy → Blender → embedded-texture GLB → Godot. One reusable wall has 1,034 runtime triangles, one material and three 1K maps. Dimensions: length 1.25 × depth 0.77 × height 1.085 world units, base-center pivot. One test placement at west fort tile (7,8), beneath the existing movement/sight-blocking parent. Source: `SourceAssets/levels/sources/ModularStoneWall/ModularStoneWall.blend`; maps: `SourceAssets/levels/textures/ModularStoneWall/`; runtime GLB and wrapper: `Assets/Levels/Props/ModularStoneWall/`.

See [asset documentation](Stage21ModularStoneWall.md) for task IDs, matching end profiles, historical test results and pipeline details. Known limitations: high-density generation required cleanup; live Blender export timed out and the saved copy was processed in background Blender; generation charge was not itemized; source/runtime binaries are locally stored outside Git. Stage 2.2 generated no additional models and spent zero Meshy credits.

### Stage 2.2 — Dark-fantasy material calibration

**Stage 2.2 accepted.** Material work, import, visual review, documentation, picking regression, corrected castle-transfer setup and scene-aware free-testing validation are complete. Both full broad smoke runs pass. Do not begin Stage 2.3 automatically; broader art-production gates below retain their separate scope.

- Selected dark stylized medieval fantasy: muted colors, weathered readable forms, matte materials and controlled lighting. Faction details and reusable workflow are in [Environment_Art_Style_Guide.md](Environment_Art_Style_Guide.md).
- Recalibrated the existing wall textures: mortar `#2B3032`, main stone `#4D5457`, worn stone `#707677`, edges `#8B8F8D`, moss `#434B3C` up to 20%; roughness target 0.80–0.92, metallic 0, normal strength 0.32. Exported roughness map observed at approximately 0.80–0.902.
- Blender source and runtime GLB were updated during the earlier material pass. Reproducible `Tools/CalibrateStoneWallMaps.py` and `Tools/CalibrateStoneWallMaterial.py` were reused unchanged. Geometry/UV/index bytes and node transforms match the saved baseline; placement, dimensions and gameplay metadata remain intact.
- Final Godot import scan exited 0. Runtime verified all three 1K maps and material settings, with no missing maps/material errors. [Close-up](../artifacts/stage22_close.png) and [normal tactical view](../artifacts/stage22_tactical.png) inspected: readable forms and ground separation, dark recesses acceptable, units/team markers visible. No material correction needed.
- Acceptance follow-up build: 0 warnings / 0 errors. Relevant tests: **98 passed, 0 failed, 0 skipped**, superseding the initial 72-test result. Rendered material/scene-load review passed with ten fixtures. Highest-first elevation picking incorrectly selected an overlapping upper neighbor; candidate selection now uses nearest projected tile center. All 4,800 tile-center checks across eight angles pass, including the original (4,15)/−1.3 case and mirrored stairs. Rendered native picking/stair-preview verification also exited 0. This defect was present in `0550ee3` elevation picking, before wall calibration.
- Castle-transfer smoke correction: the previous test assumed the neutral west fort was friendly. The smoke now captures it through normal movement, alternates activations, captures the east origin and executes the valid transfer. Contested/reset assertions use authored ownership. Scene owners, home immunity, eligibility restrictions and Main Action rules are unchanged. Build: 0 warnings/errors; **117 focused tests passed**, including two new neutral/enemy destination cases. Native castle-transfer, 15-VP victory/reset and subsequent HUD/model checks pass.
- Final free-testing correction: the shared smoke now validates the current scene's authored mode, explicitly exercises free mode, and restores competitive mode for remaining regression checks. Cliffside remains `FreeTesting = false`; the original beta remains true. No scene defaults or gameplay-mode rules changed. Build: 0 warnings/errors; **170 free-testing/activation/timer/5v5/objective tests passed**, 0 failed/skipped.
- Both complete broad smoke runs exit 0: `artifacts/stage22_free_testing_cliffside_smoke.log` and `artifacts/stage22_free_testing_beta_smoke.log`. Picking, castle transfer, scoring/victory, competitive timers, free mode, HUD/model targeting and final camera/grid checks all pass. No remaining Stage 2.2 acceptance blocker is known; [the report](Stage22MaterialVerification.md) records evidence and commands.
- Previously observed import diagnostics remain documented: ignored nested prototype project and editor shutdown leaks (CanvasItem/dummy texture RIDs, four ObjectDB instances, one resource). They were not repaired by these smoke corrections; the final runtime smoke logs contain no warnings/errors.

### Stage 2.3A — First modular cliff face and upper edge cap

**Complete:** one deterministic Blender pair and one visual proof at tile (14,15), world (15.375,0,22.875). Actual terrace drop is 1.8 world units (level 0 to −3); combined envelope is X1.25 × Y1.85 × Z1.25, including a 0.05 skirt below the lower floor. Face: 106 triangles; cap: 46; pair: 152. Three matte slate materials, roughness 0.88, metallic 0, no texture dependencies. Shared upper-center pivot; repeat at 1.25 along X with exposed face toward +Z. [Asset specification, sources, paths and limitations](Stage23ModularCliff.md).

Editable source: `SourceAssets/levels/sources/ModularCliff/ModularCliff.blend`; GLBs and individual/pair `.tscn` wrappers: `Assets/Levels/Props/ModularCliff/`. Builder: `Tools/BuildModularCliff.py`; rendered geometry/seam verifier: `Tools/VerifyStage23Cliff.gd`. Only the old Surface and separate decorative crag at this one tile are hidden. Their metadata and bounds references are retained; no gameplay code, other cliff placements, castles or stairs changed. Native gameplay exports before/after are byte-identical.

Verification: Godot import exit 0; Blender manifold/profile checks and Godot imported dimensions, UVs/normals, materials, cap interface and two-copy seam passed; close-up, tactical and isolated seam screenshots inspected. Build: 0 warnings/errors. **100 focused tests passed**, 0 failed/skipped. Native stair/picking checks and complete Cliffside broad smoke passed. Historical editor shutdown diagnostics remain documented; no missing materials/textures. Zero Meshy calls/credits.

This does not complete the whole cliff kit or authorize mass placement. Before extending it, produce separate compatible corners, end/legacy adapters, damaged/stratum alternatives and stair-transition tickets. The outer southern cliff has a different height. Preserve the existing layout and validate each variant. No further production was started.

### Stage 2.3B — Refine the existing cliff pair

**Complete:** replaced the regular full-width band appearance with unequal fractured rock plates, recessed backing, varied chamfer/depth and restrained weathering on the existing face/cap at (14,15). No additional variants or placements. All original dimensions, end mating segments, top Y0, face/cap attachment Y−0.18 and 1.8 terrace drop remain intact. The source pair now totals **888 triangles** (face 704, cap 184), with one textured material per GLB and two runtime surfaces per pair.

Blender MCP and Meshy availability were checked. Direct Blender refinement plus deterministic shared 512px albedo/normal/roughness maps was chosen because textures alone would not fix the bands, and generated geometry would require rebuilding the exact modular boundaries. No Meshy job or credit spending occurred. Original Stage 2.3A files are preserved under `SourceAssets/levels/sources/ModularCliff/Versions/Stage23A/`; final source/exports under `Versions/Stage23B/` are promoted to the existing paths. Refiner: `Tools/RefineModularCliff.py`. The initial MCP export lost its connection; isolated background Blender completed the operation and MCP verified the final source. [Current specification, map values, method comparison and evidence](Stage23ModularCliff.md).

Validation: all dimensions/UVs/normals/maps, original mating geometry, cap interface and two-/three-copy joins checked. Close-up and normal tactical captures inspected. Import exit 0; build 0 warnings/errors; **100 focused tests passed**, 0 failed/skipped; complete Cliffside smoke passed. Gameplay JSON remains byte-identical to Stage 2.3A, so movement, LOS, collision metadata and layout are unchanged. No gameplay or battlefield scene edits were made in 2.3B. Historical editor shutdown diagnostics remain recorded.

Measured tradeoff: +736 triangles per pair, surfaces reduced from 3 to 2, GLB total 316,180 bytes including embedded maps. Frame-time differences were not benchmarked. Repeated close-up rock motifs remain a known limitation of one asset; no alternate models, adapters or mass placement were started.

### Stage 3.0A — Asset extraction, discovery and connected-world inventory

**Inventory complete; ready for review.** Thirteen ZIP archives passed CRC/path/type/space checks and were extracted without changing the originals; one standalone HDRI was copied and hash-verified. The two timestamped archives contain 91 individual ruins-related Blender files and their two foliage textures, strongly matching Ultimate Modular Ruins, but original download provenance remains unverified. Nature Standard contains 68 models, KayKit 216, Kenney 76, and the Poly Haven downloads add two models, six materials and one HDRI. Blender inspected all 459 source candidates: **453 model candidates / 407 exact local-geometry families**, six material libraries, no import failures. Alternate export formats and LOD objects are not counted as independent models.

Deliverables: [inventory and production plan](Stage30AEnvironmentAssetInventory.md), local `SourceAssets/Environment/Manifests/AssetInventory.json`, `AssetRequirements.json` (107 requirement rows), `AssetLicenses.json`, per-file/ZIP hash and extraction audit, and [27 labeled visual sheets with searchable index](../SourceAssets/Environment/QA/ContactSheets/index.html). Original sources/documents are preserved under `SourceAssets/Environment/ThirdParty/`; `.gdignore` prevents bulk Godot import. SourceAssets and QA remain locally stored/ignored under existing repository policy; back them up. Reproduction scripts are `Tools/Stage30ADiscover.py`, `Tools/Stage30ABlenderCatalog.py`, `Tools/Stage30APbrReview.py` and `Tools/Stage30AFinalize.py`.

Licensing review is complete **with holds, not universal clearance**. KayKit, Kenney and Nature Standard have preserved bundled CC0 evidence. The timestamped ruins archives and all nine Poly Haven resource roots lack bundled license records; publisher CC0 statements are linked in the report, but these sources remain review-required and cannot be promoted until provenance/license records are resolved. No unknown license was assumed permissive. No Meshy generation, paid API call, retexturing, runtime export, scene/gameplay edit or mass import occurred.

**World-building direction:** gameplay grid and visual environment are separate systems. Organic, connected visual terrain may span multiple cells while authored movement, collision, elevation, LOS, objective, fort footprints, legal stairs and home transfers stay authoritative. Blender connects/adapts terrain, cliffs, foundations and stairs. Reuse downloaded free assets before expensive generation; Meshy is available only for distinctive missing ornament/organic forms where reuse and Blender are insufficient, under a later separately authorized ticket. No mandatory generation is currently justified.

The first world-building milestone is a **connected terrain foundation**, followed by integrated cliffs, forest, ruins, forts, faction architecture and distant scenery. The next recommended scoped section is the west central/lower terrace junction around existing X4–5 stairs, a short adjoining cliff run and Y0/−1.8 connected ground. Preserve row15 −0.6 / row16 −1.2 stair levels, camp access and existing blockers. Foundations and transitions precede decoration; shared fort shells precede faction accents; backdrop/water/fog follow foreground silhouettes. This supersedes the older recommendation to start solely with another isolated cliff adapter. Stop after inventory review; no construction is authorized by Stage 3.0A.

### Stage 3.0B — Western staircase connected environment slice

**Constructed and playable; awaiting user visual review.** One dedicated visual scene replaces the overlapping greybox appearance around X4–5 stairs, spanning X2–7/Y13–18. Connected upper/lower terrain, fractured rock masses, ten tiers of worn stone steps, sparse paving, grass and ferns are integrated at the original world coordinates. Upper Y0, stair centers −0.6/−1.2, lower Y−1.8 and full two-column clearance remain intact. No further section was begun.

Only six bundled-CC0 Nature Standard sources were reused: three medium rocks, one pebble, short grass and fern. Exact source IDs/hashes, license evidence, placements and adaptation are in [the Stage 3.0B report](Stage30BWesternStairEnvironment.md) and local `SourceAssets/Environment/WesternStairSlice/SelectedAssets.json`. Poly Haven and timestamped ruins licensing/provenance holds remain unresolved; those sources were not promoted. No Meshy or paid calls occurred.

Editable Blender source: `SourceAssets/Environment/WesternStairSlice/WesternStairSlice.blend`. Runtime wrapper: `Assets/Levels/Environment/WesternStairSlice/WesternStairSlice.tscn`; GLB in the same folder. Four meshes/materials/surfaces, **17,716 triangles**, GLB **1,896,084 bytes**, two shared 512px runtime maps. Explicit wrapper materials enable the exported vertex tint; use the wrapper rather than the unstyled GLB alone. Builder, integration and verifier: `Tools/BuildStage30BWesternStair.py`, `Tools/Stage30BScene.py`, `Tools/VerifyStage30B.gd`. Fifty-six old visual meshes remain hidden/recoverable, and the task-start scene baseline is preserved locally. No collision or navigation was added.

Validation: build 0 warnings/errors; **104 focused tests passed**, including four western up/down movement-commit cases; native stair/picking checks and full Cliffside smoke passed. All 32 local walkable centers checked against imported triangles, maximum paving relief 0.01800013 units. Before/after gameplay exports are byte-identical (SHA-256 `D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86`). Existing C# code/tests and character models remain unchanged; four new test cases were added. Historical headless editor shutdown diagnostics remain documented separately from runtime success.

Inspected Blender render/live source view, Godot close-up and normal tactical view. [Before/after comparison](../SourceAssets/Environment/WesternStairSlice/QA/BeforeAfter.png); [normal tactical camera](../SourceAssets/Environment/WesternStairSlice/QA/Godot_after_tactical.png). The stairs read as embedded in fractured rock, with muted connected ground; surrounding architecture remains greybox and fine details recede at full-board zoom. Target-hardware profiling and user visual acceptance remain pending. Stop for visual review before extending the adjacent cliff/terrain toward the western fort.

### Stage 3.0C — Connected cliffside environment expansion

**Construction and required validation complete, 2026-11-16; stop for visual review.** The main row-15 escarpment now connects the western slice to the central boundary and eastern staircase, with short end completions. Both stair corridors and their authored standing heights remain intact. Adjacent ground is limited to the upper edge and immediate cliff foot; forest, castles, farming mechanics and the southern row-19 perimeter were not rebuilt.

Five optimized reusable rock meshes are derived from three bundled-CC0 Nature Standard models. **37 linked rock instances** share **592 unique rock triangles**, rendering **4,377 rock triangles** across all placements before culling. The core and ground add 2,640 unique triangles: **3,232 unique / 7,017 instance-expanded triangles** for the expansion, across 39 mesh instances/surfaces. The original 17,716-triangle western slice remains a single unchanged assembly. Twenty original eastern treads were restyled with one shared slate material; no eastern stair geometry was added. Two existing western materials and their two 512px maps are reused; new GLBs embed no images. No Meshy request or paid call occurred.

Editable source: `SourceAssets/Environment/ConnectedCliff/ConnectedCliff.blend`. Exact linked placements and source/license/hash evidence: `Composition.json` in that folder. Runtime composition: `Assets/Levels/Environment/ConnectedCliff/ConnectedCliff.tscn`, instanced once at identity as `ConnectedCliffEnvironment`. Five rock GLBs plus one core/ground GLB total **195,440 bytes**. Eighty-nine replaced decorative nodes are hidden/recoverable; the prior cliff proof is retained. No collision/navigation or gameplay nodes were added or removed.

Resumed validation: C# build zero warnings/errors; **108 focused tests passed**, including committed ascent/descent on both columns of each staircase. Native round-trip, stair overlays and 4,800 rotated-camera picking checks passed; full Cliffside smoke passed again. Sixty adjacent walkable centers passed imported-mesh standing-height checks. Native gameplay exports are byte-identical, SHA-256 `D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86`. All 333 protected code and western asset files remain hash-identical to the stage baseline. Four eastern movement tests were added during construction; existing gameplay code/tests were not rewritten.

Normal-camera profiler counters: **3,009 → 2,907 draw calls** versus Stage 3.0B, while rendered primitives rose **583,888 → 588,040**. Instancing saves mesh storage, not rendered triangles. The resumed final profile uses the same 1280×720 cameras, VSync-disabled setting, 2.2-second warm-up and 120 samples as the earlier baselines. Timing and whole-renderer video-memory readings span sessions; the higher memory counter is documented, and no guaranteed FPS or texture-memory improvement is claimed.

The interrupted work already contained the assets, source render, integration and passing tests/smoke. Resume audited the source read-only, finished the missing warmed-up final profile, refreshed validation, and completed [the report](Stage30CConnectedCliffEnvironment.md), rollback evidence and [before/after review images](../SourceAssets/Environment/ConnectedCliff/QA/BeforeAfter.png). No unfinished export, active render or pending generation task was found; backup `.blend` files were preserved. No completed asset was rebuilt on resume.

[Full angled cliff](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_full_cliff.png), [western transition](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_west.png), [central close-up](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_central.png), [eastern transition](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_east.png), [normal tactical view](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_tactical.png). The main escarpment is coherent and both openings remain clear; low-poly facets and reuse remain visible up close. The eastern stair geometry is still simpler than the western stairs, and the southern outer cliff remains greybox. These are explicit future review choices, not work started in Stage 3.0C. **Do not begin Stage 3.0D automatically.**

### Stage 3.0D.1 — Toggleable tactical grid overlay

**Complete:** `toggle_tactical_grid` is an InputMap action, bound to physical G by default. Cliffside sets `TacticalGridInitiallyVisible = false`; the shared property defaults true so the original beta keeps its existing grid behavior. Key repeats/releases and unbound modifier chords do not toggle; UI-consumed events and focused text editors are respected. Existing F1 help includes “G — Toggle Grid.” No HUD redesign or persistent-settings system was added.

Audit found the lines under the dedicated `GridOverlay` node, not baked into terrain. Cliffside's 141 authored grid-line meshes are consolidated into one runtime mesh/surface, preserving their authored elevation/stair profile with a 0.025-unit lift. Originals remain recoverable, and the beta's existing single renderer is untouched. Selection, movement/path/hover, targeting, capture/ownership and ability visuals remain independent. No terrain/character assets, gameplay coordinates or rules were changed; no Blender work or paid calls occurred.

Validation: build zero warnings/errors; **110 focused tests passed**, including two native-scene grid/input cases covering both defaults, physical G, rebinding, text focus, alignment, indicator independence and camera input. Full Cliffside and original-beta smokes pass. Native stair/picking checks pass, and gameplay exports remain byte-identical to Stage 3.0C (SHA-256 `D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86`). Inspected matching [OFF](../artifacts/stage30d1_CliffsideArena_off.png)/[ON](../artifacts/stage30d1_CliffsideArena_on.png) captures, [selected movement feedback](../artifacts/stage30d1_CliffsideArena_selected_close_off.png) and [targeting with grid hidden](../artifacts/stage30d1_CliffsideArena_target_off.png).

[Implementation, verification, scene compatibility and limitations](Stage30D1TacticalGridOverlay.md). Greybox color patches remain visible where untextured terrain is still square; this stage removes optional boundary lines rather than rebuilding those surfaces. Stop at Stage 3.0D.1. Ground texturing and subsequent environment stages remain separately scoped work.

### Stage 3.0D.2C — Forward-castle stairs and cliff repair

Complete after a documented pre-change coordinate/connectivity audit. Stair access is now built into each unchanged southern forward-castle gate: X8–9 /X20–21,Y15–16. Four former row15 stair cells become blocked cliff; four new row15 cells open; the corresponding row16 elevations change. Sixteen terrain and two route-connection records change, while687 other native metadata records remain identical. All514 walkable cells remain connected, with86 blockers. Neutral/enemy fort ownership does not restrict physical passage.

Reused stair MultiMeshes, dressed masonry and clipped/repaired connected cliff geometry form the new junction; old stair assemblies and grids remain hidden for rollback. Lower material maps and all original asset files retain their hashes. Validation:118 focused tests,16 actual animated floor-click traversals, full Cliffside smoke,4,800 picking checks,154 standing centers and60 exposed stair-grid samples pass. The intentionally changed layout hash is `B77CB74DCCE5B9DF8A1F888C11593FBDAEAE3AF214D28D461AD01D963FAE8758`. See [full report, path-cost implications and required captures](Stage30D2CForwardCastleStairs.md). Stop for user review; no subsequent ticket has begun.

### Upcoming environment production — independent asset tickets

Recommended order below respects the existing art-approval and forward-fort vertical-slice gates. The material guide is a baseline, not evidence that every expanded P2 gate in v1.1 is approved. Prepare one small asset ticket at a time; do not produce whole families in one call. No generation is authorized by this table. All Meshy spending needs its own scope and approval. Preserve existing collision, elevation, routes and mirrored terrain.

| Priority / family | Intended location and purpose | Required variants / role | Production choice | Dependencies and validation |
|---|---|---|---|---|
| 1. Cliff wall and edge | Row 15 below middle, southern terrace edge and non-playable perimeter; communicate elevation barrier | First straight face + cap complete at (14,15). Next: independent end/stair adapters, convex/concave corners, stratum/damaged alternatives and bottom trim. Visual only; existing map cliff is blocking | Blender procedural; no Meshy needed | Preserve tested 1.25 spacing/top datum; actual central drop 1.8. Separate height ticket for southern outside cliff. No holes, hidden ramps or changed stairs; seam, mirrored view and gameplay-export checks |
| 2. Stairs | Existing X4–5 / X24–25, Y15 links; make descent readable | Exact two-wide run and landing first; later trim/rail variants. Traversable via current graph; trim decorative | Blender preferred; Meshy not needed | Cliff interfaces and picking regression resolved; preserve all legal steps, occupancy and landing clearance; click/pick at camera rotations |
| 3. Ground and transitions | Upper forest, main terrace, lower farm path and courtyards; clarify legal surfaces | Shared grass, dirt, paver materials and transition strips. Traversable surfaces; overlays decorative | Godot materials + Blender planar work; Meshy not needed | Style baseline and current surface heights; seamless joins, ground/unit contrast, unchanged metadata and floor picking |
| 4. Fort kit | Existing west/east fort parent anchors; replace greybox without redesign | Straight/end wall, corner, gate, tower; one west-fort proof before mirrored rollout. Visual children of blocking wall parents; gates stay open | Blender shells preferred; Meshy optional ornament only | v1.1 fort approval and P3 slice gate; all four two-wide gates, 14 blockers, capture visibility and six arrivals unchanged; movement/LOS and transfer checks |
| 5. Forest vegetation | Upper rows and existing seven connectors; readable traversable cover | Tree A/B and bush first; later small tree/grass variants. Tree/bush cells traversable; decorative art has no new collision | Blender reusable forms/atlases preferred; Meshy optional distinctive tree, not required | Approved vegetation sight policy before changing flags; retain existing flags meanwhile; paths, units and action cues visible; atlas alpha/shadow review |
| 6. Rocks and ruins | Authored forest blockers, central approaches and lower ruins; communicate obstruction/cover | Boulder/cluster and broken wall/column first; later arch/outcrop variants. Blocking only on existing blocker cells; small debris decorative | Blender procedural rocks and modular ruins preferred; Meshy optional unique forms | Functional footprint tickets; mirrored blockers/LOS, three central approach families and objective access unchanged |
| 7. Neutral farming camp | X8–9 / X20–21, Y17–18; identify neutral clearings | Marker, shelter/ruin ring, cache and small debris as separate tickets. Decorative unless gameplay design explicitly assigns blocking | Blender kit reuse preferred; Meshy optional special prop | NPC/reward design pending; reserve combat/spawn clearance, symmetric access and no accidental blocker cells |
| 8. Faction architecture | Home hubs, then fort accents; distinguish home transfer hubs and ownership | Shared gatehouse/wall/tower shells, then Aether roof/banner/spire and Ridge copper/iron/timber details. Decorative shells outside retained gameplay anchors | Blender modular kit preferred; Meshy optional ornament | Shared fort proof and faction review; equivalent silhouettes/readability, home non-capturable behavior and transfer clearance preserved |
| 9. Lighting and atmosphere | Whole arena and backdrop; unify materials without hiding play | Baseline sun/ambient review, then restrained haze, background and selective magic as separate tasks. Decorative only | Godot lighting/VFX; Blender low-detail backdrop if needed; Meshy not needed | Representative approved surfaces/units; actual camera min/normal/max review, effects and ownership contrast, performance measurements |
| 10. Final optimization and verification | Full map and shared runtime library | Material/atlas consolidation, visibility/LOD variants only where measurements justify. Preserve every collision role | Godot profiling + Blender cleanup; Meshy not needed | Completed kits, defined target hardware and budgets; memory/frame time, ten-unit play, camera picking, movement/LOS symmetry and comparative screenshots |

The adjoining central/eastern continuation is delivered in Stage 3.0C. After its visual review, choose a separately authorized next scope, such as the southern outer cliff or eastern stair refinement. The original modular pair and compatibility-adapter tickets remain available; the connected composition does not certify a complete modular kit. Do not regenerate the pair, duplicate the full western slice, or begin another stage automatically.

## Production brief

Transform the existing 30×20 Godot 4.7 .NET playable greybox into the approved cliffside fantasy arena visual concept. Preserve competitive 5v5, alternating activations, first to 15 VP, symmetrical tactical geometry, forest-to-middle rotations, central objective, two forward forts, two non-capturable home transfer hubs, and isolated lower farming terrace. The Aether Spire (left, pale stone/teal) and Forgebound Ridge (right, darker stone/timber/copper) should be visually distinct with equivalent traversal and sightline geometry.

**Non-negotiables**: Do not replace TacticalMap rules or alter pathfinding merely to match illustration; keep logic colliders separate from decorative geometry; preserve castle transfer restrictions, including contested transfers blocked and a Main Action cost. No paid Meshy generation without an explicit asset manifest, estimate, and approval. Never commit API keys. Use locally available MCP only after checking its actual tools and health. Preserve existing combat scene in version control.

## Ground truth and grid convention

The image is a stylized illustrated view, not a dimensioned orthographic plan. Positions below are provisional conceptual zones, not actual tile coordinates. Before building, Codex must inspect Scenes/Battlefield.tscn, Scripts/Gameplay/Battlefield/Battlefield.cs, TacticalMap.cs, prototype_battlfield and source scenes. Record world-to-grid scale, tile anchor/centering, elevation units, row orientation, non-walkable edges, terrain metadata, central objective, forts, home castles, capture footprints, movement geometry, sightline obstructions and import structure. Coordinate examples use **X 1–30, Y 1–20 for planning only**; code should retain the engine's actual indexing convention. Use a CSV or JSON map manifest to lock placement after audit.

### Region inventory (provisional)

| Region ID | Planned region | Gameplay | Visual/model placement |
|---|---|---|---|
| R01 | Aether home castle, left edge, approximately X1–5 | Uncapturable home transfer hub, spawn | White-stone gate, 2–3 towers, teal roofs, courtyard, spawn insignia; external architecture may extend beyond grid |
| R02 | West middle corridor, approx X5–12 | Advance toward left fort; cover | Stone pavers, low walls, fallen blocks, ruin pillars |
| R03 | Left forward fort, approx X7–11 | Capturable arrival/transfer destination | Raised-looking (not necessarily elevated) capture dais, symmetric physical footprint, blue banner/signifiers |
| R04 | Central field, approx X12–19 | Main objective, tactical cover | Central objective monument, broken columns, low stone rubble, strategic sightline interruptions |
| R05 | Right forward fort, approx X20–24 | Capturable transfer destination | Matched fort geometry with orange-red decorations |
| R06 | East middle corridor, approx X19–26 | Mirrored advance | Same traversable cover as R02; altered material/architectural language |
| R07 | Forgebound home castle, right edge X26–30 | Uncapturable home transfer hub | Industrial gatehouse, copper roof, timber scaffolds, chimneys; extras outside gameplay grid |
| R08 | Upper forest across map, upper rows | Flanks and rotations to middle | Grass, light forest, boulders, bushes, mirrored stair/forest pass openings |
| R09 | North-to-middle rotation passages, mirrored | Shortcuts | Mirrored traversal widths, grass clearing, broken wall or tree gate |
| R10 | Lower cliff wall beneath middle lane | Separates middle and bottom | Modular cliff wall and edge trim, non-walkable except intended stairs |
| R11 | West and east lower stairs | Main access to bottom lane | Mirror-matched stair runs, railings outside valid movement paths |
| R12 | Lower terrace, bottom rows | Deliberate high-commitment farm path | Dirt/stone road, grass, cliffs, two or more neutral camp clearings if design confirms, scattered ruins |
| R13 | Backdrop beyond tactical bounds | Visual only | Mountains, waterfalls, distant valley, skyline, bridge silhouettes, clouds |

### Placement data schema

For every instance, store: `instance_id`, `asset_id`, `region_id`, `grid_anchor` or `world_transform`, `rotation`, `scale`, `collision_role` (none/decorative/authoritative), `blocks_los`, `blocks_movement`, `elevation_role`, `faction_theme`, `lod_group`, `source_version` and `review_status`. Engine-derived gameplay flags remain authoritative and must not be inferred from decorative mesh collisions.

## Detailed asset manifest

The list is an initial scope target. Asset IDs are stable references for Codex scripts, Blender collections and Godot scenes. 'Meshy' implies concept/model generation followed by Blender review; 'Blender' implies parameterized, manually modeled or procedural geometry; 'Hybrid' may use both.

| Asset IDs | Models (unique base families) | Primary placement | Production method | Textures / variations | Gameplay implications |
|---|---|---|---|---|---|
| TER-001–004 | flat ground, raised ledge, corner/edge ground, transition slope | all lanes | Blender geometry + Godot materials | MAT-GRASS, MAT-DIRT, MAT-STONE-PATH, blends | must align to tile heights |
| CLF-001–005 | straight cliff, convex corner, concave corner, cliff cap, cliff bottom | southern edge of central terrace, outer arena, lower terrace | Blender modular | MAT-CLIFF, moss overlay, variation decals | decorative collider only outside playable edges; no hidden walkable slopes |
| STR-001–004 | straight staircase, wide staircase, stair landing, end trim | forest-middle and middle-lower links | Blender precise | MAT-STAIR-LIGHT/DARK | exact step traversal governed by map graph; no unintended shortcuts |
| PTH-001–004 | cobble patch, broken pavers, worn dirt strip, stone curb | middle, bases, farm road | Blender planar decals/meshes | MAT-COBBLE, MAT-DIRT | non-blocking |
| COV-001–006 | low wall straight/end/corner, fallen block, upright ruin, broken arch | mirrored middle cover, top passes | Blender for functional footprint; Meshy optional decoration | MAT-LIMESTONE/MAT-RIDGE-STONE | every LOS/cover shape needs mirrored gameplay definition |
| RUI-001–004 | broken column, column base, ruined arch, broken statue | central objective and camp ruins | Meshy + Blender cleanup | STONE base + moss/wear masks | if gameplay relevant use simple agreed collision |
| CAP-001–005 | capture floor glyph, dais, flag socket, marker, capture VFX anchor | both forward forts and objective | Blender + Godot | faction decals, emissive motif | clear ownership/state feedback; clear arrival space |
| CAS-001–006 | modular gatehouse, wall segment, wall corner, arch, tower shaft, tower crown | both home bases | Blender shells; Meshy ornamental parts | faction masonry variants | avoid line-of-sight occlusion at gameplay camera |
| AET-001–005 | teal roof kit, slender spire, blue cloth banner, ornate cornice, luminous detail | Aether home, left fort | Hybrid | MAT-AETHER-ROOF, MAT-LIMESTONE, MAT-BLUE-CLOTH | predominantly decorative, castle transfer visual clarity |
| FOR-001–006 | copper roof kit, timber framing, chimney, scaffold, gear/hoist, orange banner | Forgebound home, right fort | Hybrid | MAT-COPPER, MAT-TIMBER, MAT-IRON, MAT-ORANGE-CLOTH | smoke decorative only; never conceal combat cues |
| VEG-001–006 | broadleaf tree A/B, small tree, shrub, tall grass clump, flowering ground patch | upper forest, edges, limited mid patches | Meshy prototype + Blender optimized variants | bark/leaf atlases, MAT-GRASS | tactical bushes distinct from decorative foliage |
| ROC-001–004 | boulder large/medium, rock cluster, pebble cluster | forest, cliffs, lower route | Meshy or Blender procedural | MAT-ROCK, hue variants | large rocks only block if data-defined |
| FAR-001–005 | camp ruin ring, neutral camp marker, supply cache, worn arch, camp debris | lower terrace | Hybrid | MAT-STONE, MAT-WOOD, dirt | reserved NPC spawn and combat spaces need clearance |
| DEC-001–006 | banners, lantern, urn, broken tiles, wall ivy, flowers | all lanes with density limits | Hybrid/procedural | cloth, stone, emissive, foliage | decorative collision off |
| BKG-001–006 | mountain tiers, distant town silhouettes, distant bridge, waterfall falls, sky/fog cards, cloud strata | outside grid | Blender/low-detail scenery and Godot VFX | simplified gradients/tiling | no gameplay collision; optimized |

The inventory represents about 65–75 candidate components/variants, but many are generated variants from fewer base mesh families. Start with 40–55 unique base meshes, validate reuse before expanding. Do not produce every variant immediately.

## Texture and material production manifest

Use shared, repeatable materials as the default. Build one style-consistent art bible (palette, roughness ranges, texel density, wear amount, brightness and silhouette guidelines) before batch generation. Prefer 1K–2K tiled or atlased textures and reserve larger maps for hero objects only after profiling.

| Material ID | Maps | Primary uses | Production notes |
|---|---|---|---|
| MAT-GRASS | basecolor, normal, roughness, AO optional | playable green terrain | variation via ground mask, vertex blending or decals; not every tile a separate material |
| MAT-DIRT | basecolor, normal, roughness | forest clearings, farm lane | seamless, worn edges, blend with grass |
| MAT-COBBLE | basecolor, normal, roughness, AO optional | central lane, castle plazas | chipped beige-gray stones; moderate contrast |
| MAT-LIMESTONE | basecolor, normal, roughness | Aether castle and left fort | pale warm ivory, edges and dirt variation |
| MAT-RIDGE-STONE | basecolor, normal, roughness | Forgebound castle and right fort | darker muted stone, comparable readability |
| MAT-CLIFF | basecolor, normal, roughness | southern cliff walls, perimeter | larger-scale strata and moss decals; avoid repetition |
| MAT-STAIR | basecolor, normal, roughness | staircase modules | shared stone variants matching adjacent masonry |
| MAT-TIMBER | basecolor, normal, roughness | Ridge scaffold, roofs, campsite | coarse plank/beam grain |
| MAT-AETHER-ROOF | basecolor, normal, roughness, optional metallic | teal ceramic or metal roof | strong teal silhouette without glowing highlights |
| MAT-COPPER | basecolor, normal, metallic, roughness | Forgebound rooftops, metal fitting | copper/orange with muted patina; metal map meaningful |
| MAT-IRON | basecolor, normal, metallic, roughness | railings, gears, hardware | dark steel details |
| MAT-BARK | basecolor, normal, roughness | tree trunks | atlas or tiling, LOD friendly |
| MAT-FOLIAGE | basecolor, alpha or cutout, optional normals | tree canopies, bushes | foliage atlas, controllable shadows and transparency |
| MAT-BLUE-CLOTH | basecolor, normal optional, roughness | Aether banners | state-themed blue/teal variations |
| MAT-ORANGE-CLOTH | basecolor, normal optional, roughness | Ridge banners | state-themed amber/orange variations |
| MAT-NEUTRAL-OBJECTIVE | basecolor, normal, roughness, emissive mask | central objective and capturable glyphs | emissive overlay separated from base stone |
| MAT-DETAIL-ATLAS | basecolor, normal optional, roughness | rubble, props, trim | consolidate draw calls |

For bespoke Meshy assets, preserve source GLB and textures, then clean geometry/UVs in Blender; bake or remap to the approved material library. Meshy's retexture API may preserve original UVs, but results need visual QA. Keep roughness and metal response consistent; disable metallic for vegetation and stone. Avoid many unique 4K textures on small props.

### Texture quality tests

Check: tiling seams; texel density; directional lighting baked into albedo; normal orientation; channel conventions; alpha edges; roughness consistency; Godot import color space; texture memory; material count per asset; edge legibility from tactical zoom; identification of actionable bushes, cover and capture zones.

## Codex → Meshy API → Blender MCP → Godot pipeline

1. **Discover tools**: Codex checks its configured Blender MCP and Meshy access, Blender version, Godot editor/headless commands, actual installed API endpoints; no assumptions about server names. Never log secret keys.
2. **Asset ticket**: Codex writes a YAML/JSON asset brief with ID, role, region, reference crop(s), model prompt, dimensions in Godot meters, pivot, target triangle budget, target texture set, collision role and review checkpoint.
3. **Model strategy decision**: precise modular stairs/terrain/cover in Blender; unique castle ornament/statue/prop via Meshy; use image-to-3D with cropped asset reference where useful; text-to-3D for simple uniquely named objects. Full map concept image should not be used to generate a complete arena mesh.
4. **Preview without spending unnecessarily**: create a basic mesh preview/task where supported; inspect silhouette and topology before texturing. Meshy task costs and availability depend on account, model, and options.
5. **Retrieve outputs**: record task ID, prompt, model version, credits, downloaded model URL/hash and provenance in manifest; use versioned sources.
6. **Blender MCP cleanup**: import GLB; inspect and fix nonmanifold or duplicate geometry, orientation, real-world scale, transforms, UVs, normals, pivots; remove baked ground shadows/unwanted basing; split material slots; generate simplified collision mesh and LOD if needed.
7. **Material preparation**: apply shared tiling materials to modular assets, bake bespoke maps for hero props, validate render under common light rig. Keep master `.blend` and exported `.glb` separate.
8. **Export**: one asset family per GLB or carefully managed multi-object modular kit; consistent axis and transform, no external file assumptions, deterministic naming.
9. **Godot import**: asset becomes an inherited or wrapper `.tscn`; attach simple decoration collision only if required; instantiate through map manifests; avoid editing core gameplay nodes.
10. **Automated checks**: file/import validity, scale bounds, triangle limits, missing maps, duplicate material count, collision role correctness, nav passability, line-of-sight symmetry, scene load, .NET compilation, xUnit suite and screenshot comparison.
11. **Visual QA gate**: render from actual tactical camera, evaluate characters and action indicators against background; reject or revise before batch generation.
12. **Promotion**: update asset registry and map placement manifest; commit reviewed version; retain source and credit ledger.

**References**: Meshy Image-to-3D https://docs.meshy.ai/en/api/image-to-3d ; Meshy Text-to-3D https://docs.meshy.ai/en/api/text-to-3d ; Meshy Retexture https://docs.meshy.ai/en/api/retexture ; Godot GLB import https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/available_formats.html ; Godot 4.7 export https://docs.godotengine.org/en/4.7/tutorials/assets_pipeline/exporting_3d_scenes.html

## Repository structure (proposed, Codex should adapt to existing structure)

```text
SourceAssets/Environment/
  References/                 # approved concept and cropped asset references
  Manifests/                  # asset_registry.json, placements.json, cost_ledger.json
  Blender/                    # editable master source .blend
  Meshy/                      # immutable raw generation .glb + task metadata
  Textures/Shared/            # common tileable and atlased textures
  Textures/Hero/              # approved bespoke masks and maps
  Exports/                    # cleaned game-ready GLBs
  Screenshots/QA/             # Godot tactical camera review images
Game/Assets/Environment/
  Shared/                     # tile, stairs, cliff, ruins, foliage wrappers
  AetherSpire/                # themed structures and material instances
  ForgeboundRidge/
  Background/
Game/Scenes/Maps/
  CliffsideArena/             # visual scene + map manifest, separate from game state
Game/Tests/Environment/       # import/grid/symmetry smoke validations
```

Relative paths should follow actual repo organization after inspection; `Game/` may not be the real project root.

## Production stages and acceptance gates

### P0 — Baseline audit and backups

Tasks: identify Godot project root, existing greybox and coordinate convention; inspect scenes; run current C# tests; record before screenshots; locate any reusable stone, trees, terrain and castle models; verify MCP/tool access; approve per-task spending cap. Artifacts: `CurrentMapAudit.md`, `GridManifest.json`, baseline captures, test results, `AssetRegistry.json`.

Gate: source-of-truth graph, all fort/hub/objective positions, current rules and backup scene verified.

### P1 — Geometry and visual blockout

Tasks: prove top route can rotate into middle; reserve bottom lane stair access; establish cliff height; normalize floor/steps/tile scale; place proxy fort/base and core ruins. All collision is authored or checked against TacticalMap; decorations not authoritative. Build mirrored traversal tests. Gate: symmetry and reachability tests pass; 5v5 camera is usable. No expensive asset generation.

### P2 — Vertical slice: ONE forward fort and adjacent stairs/cliff

Create and polish: CAP-001 capture glyph, CAP-002 small dais, COV-001 low wall, RUI-001 column, STR-001 stair, CLF-001 cliff straight, TER-001 grass and PTH-001 cobble, one faction banner, one material per essential surface. The slice is an end-to-end pipeline test. Gate: visual quality from Godot camera, stable collision/grid, correct scale, art budget, reproducible model export, character readability.

### P3 — Modular terrain and cliff kit

Build all TER, CLF, STR and PTH families; build material blends and instancing; extend top forest and lower route; fill external cliff perimeter. Gate: no holes, stepping/LOS test parity, acceptable draw calls, no accidental shortcut routes.

### P4 — Functional shared gameplay props

Build cover COV, ruins RUI and capture CAP; annotate every blocking prop with tactical policy; mirror gameplay proxy positions; central objective silhouette; keep visual faction dressing independent. Gate: mirrored cover/LOS masks and fort arrival clearance verified.

### P5 — Aether Spire left base and left fort

Build CAS core + AET variants; create walls, gate, primary 2–3 tower silhouettes, teal roof and banner; model farther architectural ornaments outside grid. Gate: visually readable home vs fort, no camera obstruction, home non-capturable while transfer-capable.

### P6 — Forgebound Ridge right base and right fort

Reuse CAS core with FOR variants; match collision, fort capture and playable stairs; copper roof, timber/iron scaffold and smokestack purely ornamental. Gate: geometry equivalence checks, same movement and LOS, visually distinct base.

### P7 — Forest, farming camps and environment storytelling

Produce VEG, ROC, FAR; keep actionable bush distinct from decoration; mark neutral camp spawn/clearance and rewards as gameplay-design-dependent until verified. Build upper lane through-paths and mirrored rotation connectors. Gate: no foliage concealment of units, paths, camp accessibility and objective locations validated.

### P8 — Lighting, landscape and effects

Implement stylized directional light, contact shadow/ambient occlusion, neutral fog, distance haze, low-detail hills and waterfalls, muted objective glow and faction highlights. Keep effects below tactical UI priority; screenshot at min/nominal/max camera distances. Gate: readability, stable performance on target hardware, consistent material response.

### P9 — Integration, balancing and optimization

Add LOD / visibility ranges, MultiMesh where appropriate, merged/reused materials, optimized texture sizes and simplified collision. Test all 10 units, ability terrain changes, movement highlights, capture animations, combat animation and camera behavior. Compare distances/sightline symmetry and VP pacing in playtests (concept art itself does not validate balance). Gate: compile/test passes; documented FPS and memory measurements on defined target hardware; approved comparative screenshot board.

## Ordering and dependencies

P0 → P1 → P2 (proof) → P3/P4 (functional kit) → P5/P6 (bases) → P7 (vegetation/camps) → P8 (polish) → P9 (verification). Do not mass-generate castles before P2 approval. Functional/critical geometry first; ornament last.

## Model QA rubric (every generated asset)

- Right visual silhouette and approved faction/theme
- Correct scale, transforms, axis, origin/pivot and orientation
- Game-view visible detail; no needlessly tiny details
- Acceptable polycount, no hidden high-density interior faces
- UVs without egregious stretching/overlap where unique UV expected
- Consistent PBR material and correct color spaces
- Simple collision (or collision off) matching defined role
- No unexpected navigation or LOS changes
- Proper origin file, generation job ID, prompt and version metadata
- Clean .blend and deterministic .glb export, checked inside Godot

## Suggested first automated Codex task

```text
Begin Stage P0 of Pawns of Divinity's 30x20 Cliffside Arena Environment Roadmap. You are working in the actual Godot 4.7 .NET repository and may use configured Blender MCP and Meshy API only after verifying the connections. Do not change game rules, replace the existing battlefield scene, delete content, or spend Meshy credits.

1. Inspect the repository, main Godot scenes, Scripts/Gameplay/Battlefield/Battlefield.cs, TacticalMap.cs, prototype_battlfield, all environment assets, castle-transfer implementation and tests.
2. Determine the actual 30x20 grid axes/origin/tile size, elevation, scene layers, navigation and LOS representation, collision and material pipeline. Record current positions and footprints of home transfer hubs, forward forts, central objective, staircases, forest routes and lower farming camps. Identify unclear or unimplemented parts without inventing them.
3. Cross-check design intent: mirrored competitive gameplay, left Aether Spire and right Forgebound Ridge visual identities, upper forest rotations to middle, lower commitment farming lane, 5v5 and 15 VP alternating activations. Preserve current castle transfer mechanics and home non-capturable status.
4. Assess Blender MCP actions and Meshy API readiness, but do not call paid generation. Determine whether tools can import, clean, UV/texture, save .blend, export .glb, and capture screenshots. Report blockers precisely.
5. Create an asset registry and map placement manifest schema. Propose exact repository-relative source/export/import paths adapted to this project. Prioritize a forward-fort vertical slice.
6. Run compilation and current tests, summarize results, take baseline screenshots where tools permit, and write a Stage P0 audit plus prioritized Stage P1/P2 tasks.
7. Stop before modeling and request review of the map audit, unique base asset list and estimated Meshy job cost. Do not expose API secrets.
```

## Decisions to revisit only after testing

- Exact location and tile footprints of both farm camps, castle/fort capture platforms, forest stair transitions and center ruins
- Whether forest bushes are mechanically actionable cover, evasion terrain, or purely decoration
- Whether low cliff walls block both LOS and projectiles or only movement
- Neutral camp gold/VP rewards and respawn rules
- Platform/target hardware performance budget and art texture/triangle budget
- Final detailed sky/lighting treatment and unit/UI contrast

## Completion definition

The 30x20 map feels like the concept art from the real Godot gameplay camera, has a modular source asset library with texture consistency and documented placements, runs through reproducible Codex/Blender/Meshy/Godot production steps, preserves fully mirrored competitive geometry and gameplay logic, and passes automated plus manual 5v5 playtests. 
