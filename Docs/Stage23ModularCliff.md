# Stages 2.3A–B — Modular cliff face and upper edge cap

Stage 2.3A integration and Stage 2.3B visual refinement completed 2026-11-15. One straight cliff pair remains at zero-based tile **(14,15)** in `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`. The current assets are the refined Stage 2.3B versions. Original Stage 2.3A sources, exports and captures are preserved. Other cliffs, castles, stairs and routes are unchanged. No Meshy calls or credits were used.

## Measured battlefield and fit

Measurements come from the current native scene, imported mesh bounds, authored layout exports and `LowerTerraceHasUniformHeightAndTwoLegalStairRuns`, rather than the old wall dimensions or historical Stage 1 summaries.

| Measurement | Current value |
|---|---|
| Board | 30×20; X0–29, Y0–19; north is Y0 |
| Tile size / module spacing | 1.25 × 1.25 world units / 1.25 along world X |
| Tile center convention | X = −2.125 + 1.25x; Z = 4.125 + 1.25y |
| Central terrace | Elevation level 0, world Y = 0 |
| Lower farming floor | Level −3, world Y = −1.8; row 16 retains level −2 only at stair cells |
| Visible elevation drop | 1.8 world units |
| Existing box at (14,15) | 1.25 X × 1.85 Y × 1.25 Z; local center (0,−0.925,0), top Y0, bottom −1.85 |
| Existing separate crag overlay | `Terrain/Cliff_14_15`; center (15.375,−1.04889,23.385), scale (1.28,1.89778,0.56) on a seven-sided tapered CylinderMesh |
| Crag's actual world AABB | Minimum (14.67617,−1.99778,23.10246), size approximately (1.397656,1.89778,0.596144) |
| Proof tile origin | World (15.375,0,22.875) |
| Stair junctions | X4–5 and X24–25; row 15 level −1 (Y−0.6), row 16 level −2 (Y−1.2), then row 17 level −3 (Y−1.8) |

The older roadmap's “lower terrace level −2” was stale. The present three-level descent is retained, not redesigned. The new pair fills the tile-box envelope with a 0.05-unit skirt beneath the farming floor. It does not reproduce the old crag's overhang into neighboring tiles. Stair junctions were measured but not replaced; the proof is ten columns from the west stair pair.

## Asset specification

Dimensions below use **Godot X width × Y height × Z depth**. Both assets share an upper tile-center origin at (0,0,0), so assemble them with identity transforms. They occupy negative Y below that pivot; this is deliberate, not a base-center prop convention.

| Asset | Local minimum | Dimensions X/Y/Z | Source vertices | Runtime vertices | Triangles | GLB surfaces/materials |
|---|---|---|---:|---:|---:|---:|
| Straight face | (−0.625,−1.85,−0.625) | 1.25 / 1.67 / 1.235 | 370 | 935 | 704 | 1 |
| Upper edge cap | (−0.625,−0.18,−0.625) | 1.25 / 0.18 / 1.25 | 94 | 150 | 184 | 1 |
| Assembled pair | (−0.625,−1.85,−0.625) | 1.25 / 1.85 / 1.25 | 464 | 1085 | **888** | 2 total |

Each GLB contains one mesh. Runtime vertex counts include normal/UV splits. Source meshes have closed manifold components and recalculated outward normals. The face includes separate closed rock plates over a closed recessed backing; it is a visual mesh, not a boolean-unioned collision solid. Tiling UVs intentionally reuse image space. Ends at X±0.625 retain the original mating geometry with added collinear midpoint vertices. The face meets the cap at Y−0.18. Cap top remains flush with Y0; interior chamfer depth varies slightly. No collision or navigation objects are exported or added by the wrappers.

The original full-width alternating bands were too regular, and flat triangle normals emphasized the tessellation. Stage 2.3B recesses and smooths the backing, adds eight unequal rock plates with chipped, nonuniform fracture outlines and varied depth, and retains planar broad stone faces. No new asset variants were created. Restrained shared textures add slate variation, fractures and subtle moss without photorealistic microdetail. Repeated copies still repeat the same rock pattern; this stage does not claim an entire non-repeating cliff kit.

## Materials and dependencies

One shared textured Principled/PBR material, `Cliff23B weathered dark slate`, is authored in the source. Each separate GLB imports one material/surface; repeated copies reuse their asset resources.

| Property | Stage 2.3B specification |
|---|---|
| Base slate anchor | `#3A4146` (58,65,70); broad low-saturation variation |
| Weather/moss tint | `#414A3A` (65,74,58), restrained mask up to about 22% |
| Albedo | Shared 512×512 sRGB PNG; observed RGB ranges 38–65 / 44–73 / 50–79 |
| Roughness | Shared 512×512 Non-Color PNG, target clamp 0.82–0.92; observed 215–228 / 255, approximately 0.843–0.894 |
| Tangent normal | Shared 512×512 Non-Color PNG; normal strength 0.20 |
| Metallic | 0; no metallic texture |

Maps are generated deterministically from broad periodic variation and fracture masks in `Tools/RefineModularCliff.py`, saved beneath `SourceAssets/levels/textures/ModularCliff/Stage23B/`, packed into the source and embedded in each GLB. There are no unresolved external runtime image paths. The first/last U columns match exactly for albedo and roughness, with at most one 8-bit value difference in normal pixels. World-oriented front UVs continue across the face/cap interface and module repetition. Top/side projections reuse the same set. These are tiling UVs, not a unique baking atlas. Future corner adapters must retain this UV orientation at mating fronts.

The original Stage 2.3A used three constant-color materials (`#565E5F`, `#424A4C`, `#676F6B`), roughness 0.88, metallic 0 and no maps. Those originals remain versioned. Current colors follow the [environment guide](Environment_Art_Style_Guide.md); sun, ambient light and tonemapping alter screen brightness. An albedo encoding error found during review was corrected before final validation.

## Files and reproducible workflow

- Original Stage 2.3A builder: `Tools/BuildModularCliff.py`; **running it overwrites the refined runtime assets**. Use the refinement command below for current outputs.
- Current refiner: `Tools/RefineModularCliff.py`.
- Editable source: `SourceAssets/levels/sources/ModularCliff/ModularCliff.blend`, containing the face and cap in their assembly positions.
- Runtime: `Assets/Levels/Props/ModularCliff/ModularCliffFace.glb` (172,232 bytes) and `ModularCliffEdgeCap.glb` (143,948 bytes).
- Preserved originals: `SourceAssets/levels/sources/ModularCliff/Versions/Stage23A/` contains the original `.blend`, both original GLBs and original builder.
- Versioned final outputs: `SourceAssets/levels/sources/ModularCliff/Versions/Stage23B/` contains the refined `.blend` and both GLBs; the same files are promoted to the existing source/runtime paths.
- Maps: `SourceAssets/levels/textures/ModularCliff/Stage23B/CliffAlbedo.png`, `CliffRoughness.png`, `CliffNormal.png`.
- Individual wrappers: `Assets/Levels/Props/ModularCliff/ModularCliffFace.tscn` and `ModularCliffEdgeCap.tscn`.
- Pair wrapper: `Assets/Levels/Props/ModularCliff/ModularCliff.tscn`.
- Verification and isolated two-copy preview: `Tools/VerifyStage23Cliff.gd`.
- Current metrics: `artifacts/stage23b_blender_metrics.json`, `artifacts/stage23b_mcp_audit.json`, `artifacts/stage23b_texture_metrics.log`. Original metrics/captures retain their `stage23a_` names.

From the repository root:

```text
D:/Blender/blender.exe --background --factory-startup --python-exit-code 1 --python Tools/RefineModularCliff.py
Godot --headless --path . --editor --import --quit
Godot --path . --resolution 1280x720 --script Tools/VerifyStage23Cliff.gd -- --stage23b
```

`Godot` denotes the installed 4.7.2 mono executable. Blender 5.2.2 LTS was used. MCP inspected the source, ran the initial refinement and inspected the final packed source. The live connection dropped during GLB export after the face; that partial attempt was not accepted. The isolated background process completed both exports and the source, then MCP independently confirmed dimensions, manifold components, maps and triangle counts. No original live objects were intentionally replaced or saved over; appended inspection collections are separate. The `.blend`, GLBs, textures, import caches and artifacts are locally stored/ignored by Git under existing project policy; preserve both versions in backups.

## Refinement method decision

Blender MCP was verified current (protocol 16, Blender 5.2.2); Meshy was enabled. The available generation connector exposes `generate_3d` for a textured model, not a dedicated texture-only refinement call.

- **A — direct geometry refinement, selected:** repairs the actual cause (regular ridges and triangle shading), retains the established boundaries, and permits controlled shared maps.
- **B — Meshy textures on unchanged geometry:** could add surface variety, but would leave the repetitive silhouette and bands. No texture-only action was exposed by the configured connector.
- **C — Meshy model candidate adapted in Blender:** could provide more organic forms, but requires paid generation plus retopology, boundary reconstruction and UV adaptation for this small, precisely constrained pair. Direct modeling offered the better fit here.

No Meshy generation job was submitted, no credit estimate was used as approval, and zero credits were spent. Consequently there are no new Meshy task IDs or provider metadata. Any future paid call still requires its expected cost and user approval first.

## Placement and preservation

Instance the pair beneath `Terrain/tile_14_15/ModularCliff` at identity. The original `Terrain/tile_14_15/Surface` and separate `Terrain/Cliff_14_15` are hidden, retained as audit/rollback references. They form the old visual assembly at this one tile; no other cliff placement changes. Keeping the hidden Surface also preserves the existing floor-bounds verification convention. It contributes no new gameplay collision. The tile remains movement-blocking, sight flag false, elevation 0.

For repeated modules, translate exactly 1.25 along X; use identical orientation on a straight run. The exposed face points **Godot +Z (south)**, the top stays at Y0 and local Z±0.625 aligns to tile borders. The isolated preview places two pairs at X±0.625, so their seam is X0. The verifier compares imported end profiles and face/cap interfaces, rather than relying only on screenshots. Future rotations require matching cap/ground interfaces; do not rotate a straight face into a stair opening as a substitute for a transition piece.

The native gameplay JSON before and after integration is byte-identical, SHA-256 `D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86`. Evidence: `artifacts/stage23a_before_layout.json` and `artifacts/stage23a_after_layout.json`. All tile flags, elevation, LOS, movement, spawn, fort, castle and objective data remain intact. No C# gameplay changes were made for Stage 2.3A.

## Validation and visual review

### Stage 2.3B final checks

| Check | Result / evidence |
|---|---|
| Source geometry / MCP inspection | Manifold closed components, exact original mating segments, UVs and three packed 512 maps; `artifacts/stage23b_blender_build.log`, `artifacts/stage23b_mcp_audit.json` |
| Godot import / geometry / textures | Exit 0; all maps, normal strength, dimensions, cap attachment and seams passed; `artifacts/stage23b_import.log`, `artifacts/stage23b_visual.log` |
| Build | Passed, 0 warnings/errors; `artifacts/stage23b_build.log` |
| Focused tests | **100 passed, 0 failed/skipped**; `artifacts/stage23b_tests/stage23b.trx` |
| Complete Cliffside smoke | Passed, exit 0; `artifacts/stage23b_smoke.log` |
| Gameplay data | `artifacts/stage23b_layout.json` is byte-identical to the Stage 2.3A baseline, hash below |

Inspected [close-up](../artifacts/stage23b_close.png), [normal tactical view](../artifacts/stage23b_tactical.png), [two-copy seam](../artifacts/stage23b_seam.png) and [three-copy repetition](../artifacts/stage23b_three.png). The face now reads as unequal fractured rock masses rather than horizontal stacked bands. Color/map continuity has no visible crack across module joins; repeated motifs remain visible in a three-copy close-up because variants are explicitly out of scope. The top stays level and tactical unit/path/HUD readability is preserved. The single proof remains surrounded by older placeholder art.

Cost changes are measured from exported data: 152 → **888 triangles** (+736), 300 → **1085 runtime vertices**, three → **two surfaces per pair**, and 13,880 → **316,180 bytes** for both GLBs together. Three source maps are embedded in each separate GLB, so texture data is duplicated across face/cap exports; repeated copies reuse their GLB resources. No comparative GPU frame-time or memory benchmark was performed, so this is a geometry/material/storage comparison rather than an FPS claim. A later shared external material could deduplicate textures if profiling justifies it; no unrelated runtime refactor was performed.

### Stage 2.3A historical checks

| Check | Result / evidence |
|---|---|
| Blender mesh validation | Closed manifold, matching end cross-sections, UVs and normals; `artifacts/stage23a_blender_build.log` |
| Godot import | Exit 0; both GLBs imported; `artifacts/stage23a_import.log` |
| Imported bounds / material / seam checks | Passed; face 106 triangles, cap 46; `artifacts/stage23a_visual.log` |
| C# solution build | Passed, 0 warnings/errors; `artifacts/stage23a_build.log` |
| Focused xUnit tests | **100 passed, 0 failed, 0 skipped**; `artifacts/stage23a_tests/stage23a.trx` |
| Native stair-preview/picking checks | Passed, including 4,800 center clicks across eight angles; `artifacts/stage23a_after_layout.log` |
| Complete Cliffside broad gameplay smoke | Passed, exit 0; `artifacts/stage23a_smoke.log` |
| Gameplay layout comparison | Before/after exports identical; hash above |

Focused filter: `FullyQualifiedName~CliffsideArenaTests|FullyQualifiedName~AuthoredLevelValidationTests|FullyQualifiedName~MobaMovementTests|FullyQualifiedName~MobaMovementPreviewTests|FullyQualifiedName~MobaUnitPickingTests|FullyQualifiedName~MobaBetaLayoutTests`. These exercise terrain/elevation, route access, mirrored LOS, occupancy, transfers, native scene round-trip and picking. The full broad smoke retains ten fixtures, 86 blocked/514 walkable cells, competitive turns, castle/VP/timer/free-mode checks, HUD/model checks and camera controls.

Screenshots inspected:

- [Close-up in the arena](../artifacts/stage23a_close.png): strata and cap are readable against the farming floor; adjacent legacy crags remain visibly different. The tile's old overlay was found to obscure the first render and was hidden at this single proof location. No cosmetic material iteration was needed.
- [Normal tactical camera](../artifacts/stage23a_tactical.png): fitted camera size 57.418 at 1280×720; the lighter edge separates the terrace while units, HUD and paths remain readable. Fine facets recede at gameplay distance.
- [Isolated two-copy seam preview](../artifacts/stage23a_seam.png): no visible crack at the join, strata continue across the seam, cap remains flush.

Godot compresses imported meshes independently. The measured front seam/interface coordinates differed by about 0.000013 world units between assets; imported checks allow 0.0001. Blender source profiles match exactly. This quantization is visually negligible and did not require geometry or import changes. Recheck if future exports or compression settings change.

## Known limits and next variants

This completes **only the first straight pair and one proof placement**. Do not mass-place it or start another asset family automatically.

- The current face is textured and refined, but repeated copies reuse the same eight rock plates and fracture texture. No additional variants were made. Assess repetition again before any separately authorized full-kit rollout.
- Adjacent legacy crags overhang their tiles and use different profiles/materials. The proof does not claim a finished seam between a new module and every legacy crag; a terminal/adapter piece is needed for mixed-kit boundaries.
- Required future independent tickets: convex and concave corners; end/legacy adapter; west/east stair transitions preserving the two-wide routes; damaged/stratum alternatives; lower-edge/bottom trim; outer farming-terrace cliff height variants. The southern outside cliff has a different height and is not covered by this pair.
- Keep mating end profiles, 1.25 spacing, top datum and blocked-cell policy fixed for compatible variants. Verify each against native gameplay exports and actual tactical views.
- Existing editor shutdown diagnostics (dummy texture/CanvasItem RIDs, four ObjectDB instances, one resource) and the nested prototype-project warning recur during headless import. They are not missing cliff maps or load failures. The final rendered cliff review and broad gameplay smoke have no warnings/errors. No hardware performance budget or full-map art approval is claimed.
