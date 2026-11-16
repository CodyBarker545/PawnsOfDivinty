# Stage 3.0B — Western staircase environment vertical slice

**Constructed, integrated and validated; user visual review pending.** This is a playable scene integration, not an inventory or isolated test asset. Only the western junction was built. No Meshy generation or paid calls occurred.

![Western staircase in Godot](../SourceAssets/Environment/WesternStairSlice/QA/Godot_after_close.png)

## Placement and measured gameplay constraints

The source is `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`. The section covers columns **X2–7, rows Y13–18**, around the existing **X4–5 / Y15–16** stairs. It is instanced as the root child `WesternStairEnvironment`, at identity transform, from `Assets/Levels/Environment/WesternStairSlice/WesternStairSlice.tscn`.

| Measurement | Actual value |
|---|---|
| Gameplay board | 30×20; 1.25-unit cell spacing |
| Section horizontal envelope | World X −0.25…7.25; Z 19.75…27.25 |
| Stair clear width | World X 2.25…4.75, two logical columns; no rock/retaining lip intrudes |
| Upper plateau | Standing Y0 |
| Stair row 15 | Centers Z22.875; standing Y−0.6 |
| Stair row 16 | Centers Z24.125; standing Y−1.2 |
| Lower terrace | Standing Y−1.8 |
| Stair entrance/exit | Upper edge Z22.25; lower edge Z24.75 |
| Logical cliff barrier | Row 15 remains blocked outside the authored stair columns; all original sight flags retained |
| Normal camera | Orthographic size 57.418; original transform retained |

Measurements were taken from native Godot nodes and the current authored scene, before replacing visuals. All four western stair-cell centers match the original heights. The ten tread levels retain the original profile: −0.12, −0.36, −0.60, −0.74, −0.86, −0.96, −1.08, −1.20, −1.44, −1.68. The lower landing finishes at −1.8. This preserves the existing stair-overlay shape as well as the movement graph.

Terrain surfaces join across multiple cells; rock silhouettes and ground blends are not separate tile props. The existing tactical grid overlay remains visible and authoritative. It is not baked into the new meshes or textures.

## Selected existing assets and licensing

All reused geometry comes from the downloaded **Quaternius Stylized Nature MegaKit Standard**, with its bundled **CC0-1.0** license. The original `License_Standard.txt` explicitly permits this edition and identifies its 68-model scope. It remains untouched; a convenience copy is preserved at `SourceAssets/Environment/WesternStairSlice/NatureStandard_CC0.txt`.

| Catalog ID | Original model | Use and adaptation |
|---|---|---|
| `E30-2e0efc8e3` | `Rock_Medium_1` | Large escarpment mass and crown rock; varied proportions/rotation, muted charcoal tint |
| `E30-f5b9a7a8a` | `Rock_Medium_2` | Alternative face mass/crown silhouette; fitted to blocked cliff envelope |
| `E30-2a61f342f` | `Rock_Medium_3` | Third fractured rock form; controlled overlap conceals backing joins |
| `E30-aa013fd24` | `Pebble_Round_1` | Six small cliff-foot/crown accents, low height and subdued contrast |
| `E30-5ab584f0f` | `Grass_Common_Short` | Ten sparse short clumps; simplified shared matte material |
| `E30-b41900693` | `Fern_1` | Four small cliff-adjacent plants; desaturated green |

Exact originals are `SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/glTF/<model>.gltf`, with companion binaries preserved in that pack. `SelectedAssets.json` records exact catalog IDs, source object names, paths, SHA-256 hashes, license document and changes. All six original source-file hashes still match their pre-construction values.

Poly Haven Mountainside/Boulder and Mossy Rock/Stony Dirt Path/Forest Ground were assessed from the Stage 3.0A catalog. They were **not promoted**: their downloads still lack the required provenance/license records, and the model LODs also need stronger simplification/restyling for this small slice. The timestamped ruins sources remain held too. No unknown license was treated as commercial permission. KayKit `stairs_modular_center` and Kenney `stairs-stone` were evaluated as geometry donors; the exact authored tread profile and two-column clearance made fitted Blender steps the appropriate choice. Neither pack was imported wholesale or used in the delivered geometry.

## Terrain construction and source files

- Editable source: `SourceAssets/Environment/WesternStairSlice/WesternStairSlice.blend`.
- Deterministic builder: `Tools/BuildStage30BWesternStair.py`.
- Source maps: `SourceAssets/Environment/WesternStairSlice/Textures/`.
- Runtime GLB: `Assets/Levels/Environment/WesternStairSlice/WesternStairSlice.glb`.
- Runtime scene and four material resources: `Assets/Levels/Environment/WesternStairSlice/`.
- Integration/rollback record: `SourceAssets/Environment/WesternStairSlice/HiddenLegacyNodes.json` and `Baseline/CliffsideArena.tscn`.

The connected upper plateau and lower terrace are fitted surfaces with continuous vertex-color blends between desaturated grass, earth and worn stone. Their outer values ease toward the retained greybox rather than introducing an abrupt bright material border. Irregular paving fragments form the upper apron and sparse lower landing; fragments do not overlap coplanar faces.

The escarpment combines continuous fractured backing with three reusable Nature rock shapes. Rotated source silhouettes are scaled into the safe envelope instead of clipping their vertices into rectangular grid planes. Broad masses overlap, with a varied crown and rock-to-ground overlap beneath the lower floor. Rocks remain within the blocked cliff band. The lower ground extends under the rock foot to eliminate light gaps. This is visual composition; no collision is derived from the source rocks.

Ten stair tiers use staggered, chipped polygon slabs with beveled edges and recessed structural cores. Small retaining cheeks stay outside the full legal two-column corridor. The top and bottom approaches remain clear. No tall tree, large prop, new encounter marker or reward asset was added.

Blender source assemblies are consolidated into four material batches for export. Their component geometry remains editable as mesh islands; the builder and untouched original kit files reproduce individual parts. The source is in world-aligned coordinates, converted from Godot X/Y-up/Z into Blender X/Y/Z-up; export converts back to Godot. The wrapper stays at identity to avoid offset or scale ambiguity.

## Materials and export handling

| Runtime surface | Triangles | Treatment |
|---|---:|---|
| Blended ground | 1,720 | Continuous earth/grass/slate vertex tint with broad, restrained surface variation |
| Rock escarpment | 6,026 | Charcoal/slate forms, restrained crown moss, matte response |
| Steps and paving | 7,268 | Slightly lighter worn stone, staggered joints and softened edges |
| Sparse vegetation | 2,702 | Low desaturated grass/fern forms; no leaf-card transparency dependence |
| **Total** | **17,716** | **Four meshes, four surfaces, four materials** |

GLB size: **1,896,084 bytes**. Two shared **512×512 PNG** runtime maps provide broad albedo variation and restrained tangent normals. Roughness is **0.9**, metallic **0**, normal strength **0.2**. The maps are newly authored for this slice; no downloaded original was retextured. Source/material names remain readable, without gameplay scripts or imported collision.

The glTF export carries the `LandscapeTint` vertex color explicitly as `COLOR_0`. Godot's imported materials initially left vertex-color albedo disabled, causing a white preview. The dedicated slice wrapper therefore supplies four explicit `StandardMaterial3D` overrides with `vertex_color_use_as_albedo = true`, linear vertex-color interpretation and the two shared runtime maps. **Use the wrapper scene, not the GLB alone, for the styled runtime section.** The verifier checks these settings. The GLB retains its embedded source-material maps; stripping those redundant unused material dependencies is a possible later measured optimization, not a prerequisite for this slice.

No pack-wide texture conversion occurred. Tiny photogrammetry noise, shiny stone, vivid foliage and faction ornament were deliberately excluded from this small junction. No runtime performance benchmark is claimed; material batching, low-resolution maps and measured geometry provide a starting point for later profiling.

## Integration, preservation and rollback

Only **56 overlapping decorative mesh nodes** were hidden: local ground surfaces, four cells' original treads, and the four adjacent cliff crags. Every original node remains present, including the `Surface`/`Tread2` meshes used by existing authored-height checks. `HiddenLegacyNodes.json` names each hidden mesh. Existing tile parents, metadata, transforms, elevation, LOS, blockers, forts, castles, objective and characters were not changed.

The dedicated visual hierarchy has **zero CollisionObject3D, CollisionShape3D or NavigationRegion3D nodes**. No automatic collision suffixes, import collision generation or runtime placement script are used. The terrain does not define movement or picking; existing gameplay systems continue to do so.

The baseline is the actual dirty worktree scene at task start, including prior Stage 2.3 work. Do not restore an older Git HEAD over it. For rollback, remove the `WesternStairEnvironment` scene instance and restore the original visibility of the nodes in `HiddenLegacyNodes.json`, consulting `Baseline/CliffsideArena.tscn`. The full local baseline is available for comparison; later unrelated scene work must be preserved.

Original hashes of **324 existing C# source/test files** remain unchanged. Four new movement-commit regression cases were added in `Tests/PawnsOfDivinity.Tests/Levels/Stage30BWesternStairTests.cs`; no existing test or gameplay rule was rewritten. Character models were not edited. The original free-asset archives, extracted sources and license documents remain intact.

## Visual review evidence

- [Blender perspective render of the complete section](../SourceAssets/Environment/WesternStairSlice/QA/BlenderPerspective.png).
- [Godot angled close-up](../SourceAssets/Environment/WesternStairSlice/QA/Godot_after_close.png).
- [Godot normal tactical camera with original units and HUD](../SourceAssets/Environment/WesternStairSlice/QA/Godot_after_tactical.png).
- [Before/after close-up comparison](../SourceAssets/Environment/WesternStairSlice/QA/BeforeAfter.png).
- [Original tactical view](../SourceAssets/Environment/WesternStairSlice/QA/Godot_before_tactical.png).

The Blender render and live Blender camera view were inspected, along with the live Godot close-up and normal tactical captures. Blender MCP was unavailable; isolated background Blender plus authorized computer control was used. No add-on installation or generator call was needed.

The new cliff is visibly fractured and has a varied crown; it no longer reads as four repeated tapered greybox crags. The stairs sit between the rock masses, with distinct worn stone steps and a clear continuous opening. Ground transitions soften the junction and the short vegetation stays subordinate to the route. The normal tactical view retains readable units, team markers, grid and HUD. Fine bevels are mainly a close-up benefit; at normal full-board zoom, larger silhouettes and material values do the work.

Remaining shortcomings: the surrounding fort walls and much of the adjacent terrain are still greybox, so the section's outer boundary remains perceptible. The small paving pieces are relatively clean and could gain broader damage variation after review. Rock triangulation is visible in close-up, and adjoining full-map cliff silhouettes need continuation. The existing visible grid overlay still divides the view into cells. The source renderer and current warm Godot arena lighting differ; this task did not redesign whole-map lighting. User art acceptance and target-hardware timing/memory budgets remain pending.

## Gameplay and geometry validation

| Check | Result / evidence |
|---|---|
| C# build | Passed, zero warnings/errors; `artifacts/stage30b_build.log` |
| Focused authored terrain/stairs/movement/picking tests | **104 passed, 0 failed, 0 skipped**; `artifacts/stage30b_tests/stage30b.trx` |
| Western ascent/descent movement commits | Four new passing cases: both columns, each direction; cost 3, preview/committed path and acknowledged destination match |
| Native scene round-trip and stair-overlay checks | Passed; `artifacts/stage30b_after_layout.log` |
| Camera picking | 4,800 tile-center checks across eight orientations passed |
| Complete Cliffside smoke | Passed; `artifacts/stage30b_smoke.log`; original ten fixtures, 86 blocked / 514 walkable cells, combat, castles, transfer, objective/VP, timer, HUD and free-mode regressions |
| Gameplay data preservation | Before/after native exports byte-identical; SHA-256 below |
| Imported visual standing surfaces | 32 walkable centers ray-tested against actual imported triangles, including four stair cells; maximum discrepancy **0.01800013** units from small paving relief; `artifacts/stage30b_runtime_geometry.json` |
| Decorative collision/navigation | None; verified in imported hierarchy |
| Original code and selected source files | Hash checks pass; `SourceAssets/Environment/WesternStairSlice/Validation.json` |

Gameplay export SHA-256: `D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86`.

The initial test attempt ran before the first GLB import completed and reported resource-loading failures; it was superseded by the final 104-case passing run. Initial paving overlap, cliff-foot gaps and disabled imported vertex-color material usage were corrected before final captures. Headless editor import exits 0 but retains the previously documented dummy-texture/CanvasItem RID, four ObjectDB-instance and resource-at-shutdown diagnostics. Final runtime visual/geometry and smoke logs have no missing-resource or material errors.

## Reproduction and next section

From the repository root:

```text
python Tools/Stage30BScene.py
D:/Blender/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools/BuildStage30BWesternStair.py
python Tools/Stage30BScene.py --integrate
Godot --headless --path . --editor --import --quit
Godot --path . --resolution 1280x720 --script Tools/VerifyStage30B.gd
python Tools/ReportStage30B.py
```

`Godot` denotes the installed Godot 4.7.2 .NET console executable. Run the import to completion before tests or capture. `ReportStage30B.py` additionally requires the documented native before/after layout exports and final build/test/smoke evidence; it does not manufacture those results. Do not rerun `Stage30BScene.py --integrate` after unrelated scene edits without merging them: its deterministic integration is based on this task's saved baseline.

SourceAssets, Blender sources, binary GLBs, screenshots and logs remain local/ignored under existing repository policy. Keep backups with the project assets. The wrapper/material resources, tools, new tests and documentation are versionable.

After user visual review, the next recommended scope is a short adjoining central-to-lower cliff/terrain continuation east of this slice, toward the western fort. Reuse the shared ground/rock materials and adapt transitions to the authored blockers; avoid copying the entire composition at regular intervals. No further section or production stage was started.
