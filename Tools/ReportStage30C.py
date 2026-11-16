"""Validate the finished Stage 3.0C delivery and assemble its review documentation."""
import collections,hashlib,json,pathlib,re,shutil,struct
from PIL import Image,ImageDraw,ImageFont
ROOT=pathlib.Path(__file__).resolve().parents[1];SRC=ROOT/'SourceAssets/Environment/ConnectedCliff';QA=SRC/'QA';RUN=ROOT/'Assets/Levels/Environment/ConnectedCliff'
def read(p):return json.loads(p.read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
composition=read(SRC/'Composition.json');geometry=read(ROOT/'artifacts/stage30c_geometry.json');source_audit=read(SRC/'SourceAudit.json')
protected=read(SRC/'Baseline/ProtectedHashes.json')
assert all(sha(ROOT/p)==h for p,h in protected.items()),'Protected source changed'
assert all(sha(ROOT/a['source'])==a['sha256'] for a in composition['source_assets'])
before=ROOT/'artifacts/stage30c_before_layout.json';after=ROOT/'artifacts/stage30c_resume_layout.json'
assert before.read_bytes()==after.read_bytes()
assert 'Build succeeded.' in (ROOT/'artifacts/stage30c_resume_build.log').read_text()
tests=(ROOT/'artifacts/stage30c_resume_tests.log').read_text()
assert re.search(r'Failed:\s+0',tests) and re.search(r'Passed:\s+108',tests)
assert 'GODOT MOBA BETA SMOKE PASSED' in (ROOT/'artifacts/stage30c_resume_smoke.log').read_text()
assert 'CONNECTED CLIFF GEOMETRY PASSED' in (ROOT/'artifacts/stage30c_after_final.log').read_text()
profiles={m:read(ROOT/f'artifacts/stage30c_{m}_profile.json') for m in ('greybox','stage30b','after')}
assert all(p.get('warmup_ms_per_view')==2200 for p in profiles.values())
assert source_audit['rock_instances']==37 and source_audit['unique_rock_meshes']==5
assert geometry['instance_expanded_triangles']==7017 and geometry['unique_triangles']==3232
hidden=read(SRC/'HiddenLegacyNodes.json');restyled=read(SRC/'RestyledEasternTreads.json')
def nodes(path):
    result={}
    for block in re.split(r'(?=\[node )',path.read_text()):
        if not block.startswith('[node '):continue
        head=block.splitlines()[0];name=re.search(r'name="([^"]+)"',head)[1];parent=re.search(r'parent="([^"]+)"',head)
        result[(parent[1] if parent else '')+'/'+name]=block
    return result
original_nodes=nodes(SRC/'Baseline/CliffsideArena.tscn');current_nodes=nodes(ROOT/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn')
for key,block in original_nodes.items():
    current=current_nodes[key]
    if key in hidden:
        block=re.sub(r'^visible = .*$', '',block,flags=re.M);current=re.sub(r'^visible = .*$', '',current,flags=re.M)
    if key in restyled:
        block=re.sub(r'^material_override = .*$', '',block,flags=re.M);current=re.sub(r'^material_override = .*$', '',current,flags=re.M)
    assert [s.strip() for s in block.splitlines() if s.strip()]==[s.strip() for s in current.splitlines() if s.strip()],key
assert set(current_nodes)-set(original_nodes)=={'./ConnectedCliffEnvironment'}
exports=[]
for p in sorted(RUN.glob('*.glb')):
    data=p.read_bytes();assert data[:4]==b'glTF' and struct.unpack_from('<I',data,8)[0]==len(data)
    n=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+n]);assert not doc.get('images')
    exports.append({'path':p.relative_to(ROOT).as_posix(),'bytes':len(data),'sha256':sha(p),'embedded_images':0})
assert len(exports)==6 and len({e['sha256'] for e in exports})==6
for mode in ('stage30b','after'):
    for view in ('tactical','full_cliff','west','central','east'):
        shutil.copy2(ROOT/f'artifacts/stage30c_{mode}_{view}.png',QA/f'Godot_{mode}_{view}.png')
font=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',24)
comparison=Image.new('RGB',(1280,980),(24,29,34));draw=ImageDraw.Draw(comparison)
for i,mode in enumerate(('stage30b','after')):
    draw.text((18,i*490+12),'BEFORE — Stage 3.0B' if i==0 else 'AFTER — Stage 3.0C connected cliff',font=font,fill='white')
    comparison.paste(Image.open(QA/f'Godot_{mode}_full_cliff.png'),(0,i*490+48))
comparison.save(QA/'BeforeAfter.png')
uses=collections.Counter(p['asset'] for p in composition['placements'])
max_delta=max(abs(s['visual_y']-s['logical_y']) for s in geometry['standing_samples'])
summary={'stage':'3.0C','completed_on':'2026-11-16','status':'construction and validation complete; screenshots ready for user visual review','resumed_without_asset_regeneration':True,'unique_rock_meshes':5,'linked_rock_instances':37,'unique_rock_triangles':592,'instance_expanded_rock_triangles':4377,'unique_transition_triangles':2640,'unique_expansion_triangles':3232,'instance_expanded_expansion_triangles':7017,'expansion_mesh_instances_and_surfaces':39,'expansion_shared_materials':2,'additional_eastern_step_materials':1,'new_texture_images':0,'export_bytes':sum(e['bytes'] for e in exports),'hidden_legacy_nodes':len(hidden),'restyled_original_eastern_treads':len(restyled),'new_eastern_stair_geometry':0,'standing_centers':len(geometry['standing_samples']),'max_standing_delta':max_delta,'focused_tests_passed':108,'build':'passed, zero warnings/errors','full_cliffside_smoke':'passed again on resume','native_gameplay_exports_identical':True,'gameplay_sha256':sha(after),'protected_files_unchanged':len(protected),'original_node_changes_limited_to_recorded_visual_properties':True,'meshy_requests_submitted':0,'paid_calls':0,'exports':exports,'profiles':profiles,'source_audit':source_audit}
(SRC/'Validation.json').write_text(json.dumps(summary,indent=2))
library_table='\n'.join(f"| {a['name']} | {a['source_name']} | {a['optimized_triangles']} | {a['removed_hidden_triangles']} | {uses[a['name']]} | {a['role']} |" for a in composition['library'])
placement_table='\n'.join(f"| {p['id']} | {p['asset']} | ({p['x']:.3f}, {p['h']:.3f}, {p['z']:.3f}) | ({p['sx']:.3f}, {p['sy']:.3f}, {p['sz']:.3f}) | {p['angle']:.3f} | {p['role']} |" for p in composition['placements'])
profile_table='\n'.join(f"| {label} | {v['view']} | {v['draw_calls_median']:,.0f} | {v['rendered_primitives_median']:,.0f} | {v['frame_interval_ms_median']:.3f} | {v['video_memory_bytes_median']/1048576:.2f} |" for mode,label in [('greybox','Pre-3.0B greybox'),('stage30b','Stage 3.0B'),('after','Stage 3.0C')] for v in profiles[mode]['profiles'])
report='''# Stage 3.0C — Connected cliffside environment expansion

Completed 2026-11-16. Construction and required validation are complete; screenshots are ready for user visual review. No Stage 3.0D work was started. No Meshy request or paid call was submitted.

![Connected cliff in Godot](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_full_cliff.png)

## Interruption audit and resumed work

Before interruption, the five-mesh library, linked composition, GLBs, source Blender file, scene integration, eastern tread material and focused tests were already complete. The original run passed the C# build, 108 tests, native stair/picking export and complete Cliffside smoke. Blender MCP had inspected 37 linked placements referencing five mesh datablocks, and saved the final source/render. One focused composition correction had already broken up regularly spaced crown/foot rocks and added cross-fracture shelves; it was not repeated on resume.

The interrupted operation was the last warmed-up performance run: greybox and Stage 3.0B baselines finished, but the Stage 3.0C output log was locked by the prior review process. The final stage report and roadmap update had not been written. Resume reused all completed model assets and materials, audited the saved source read-only, completed the missing warm-up/profile run, refreshed the required validation and packaged the screenshots/documentation. It did not run the asset builder again.

There was no Blender renderer or Godot process left running at resume; only idle Blender MCP server processes remained. All six GLB headers, declared lengths and JSON chunks passed inspection. No partial export or temporary `.tmp`/`.part` file was found. `ConnectedCliff.blend1` is a normal previous-save backup; `QA/PreservedReviewSession.blend` intentionally preserves the earlier live Blender review session. Neither is a runtime duplicate. No Meshy task ID or generation metadata exists because this stage used only existing cleared models and Blender. There was no paid request to query or resubmit.

## Scope, coordinates and preserved behavior

The main escarpment is row 15, between the middle plateau at Y0 and lower terrace at Y−1.8. Existing stair columns remain X4–5 and X24–25, with center heights −0.6 at row 15 and −1.2 at row 16. The grid remains 30×20, spacing 1.25; tile centers are X=−2.125+1.25x and Z=4.125+1.25y.

The unchanged Stage 3.0B western slice supplies its original cliff/terrain around X2–7. New connected compositions cover world X **−2.75…−0.25**, **7.25…27.25**, and **29.75…34.75**, completing the main boundary while leaving both 2.5-unit stair openings clear. Adjacent terrain work is confined to row 14, the blocked cliff crown, and the immediate row-16 cliff foot. The eastern upper landing is filled at its original Y0 datum. Lower debris stays behind normal standing centers.

The eastern staircase retains all 20 original box-mesh tread nodes, original transforms, bounds and heights. A single matte slate material makes those existing steps compatible with the new cliff; **zero eastern stair geometry was added**. A detailed chipped-step reconstruction remains a separate possible future ticket. The western 17,716-triangle assembly exists once and was not duplicated along the map.

The scene change adds one `ConnectedCliffEnvironment` root instance. Eighty-nine previously visible decorative nodes are retained hidden, including the earlier single-tile cliff proof container. Twenty eastern tread material overrides are recorded separately. Every original scene node was compared against the task-start snapshot: changes are limited to those recorded visibility/material properties; no gameplay nodes were deleted or redesigned. No collision or navigation was added.

## Reusable rock library and source licenses

All source geometry derives from `Rock_Medium_1`, `Rock_Medium_2` and `Rock_Medium_3` in the **Quaternius Stylized Nature MegaKit Standard**, under the preserved bundled **CC0-1.0** license. Catalog IDs are `E30-2e0efc8e3`, `E30-f5b9a7a8a` and `E30-2a61f342f`. Exact source paths, object names, hashes and license document are in `Composition.json`; all three original hashes still match. Poly Haven and timestamped ruins sources remain on their prior provenance/license holds and were not promoted.

| Reusable mesh | Source model | Optimized triangles | Buried triangles removed after decimation | Linked uses | Role |
|---|---|---:|---:|---:|---|
LIBRARY_TABLE

Collapse simplification retains broad source silhouettes. Buried northern/back and bottom faces are removed where the continuous geological core conceals them. The sources are decorative open-backed surfaces, not collision solids. Narrow fractures and seam stones use 68 and 76 triangles respectively, keeping less prominent detail cheap. Controlled overlap, nonuniform scale, rotation, irregular grouping and cross-fracture shelves vary the composition without unique geometry or per-placement textures.

## Files and integration

- Editable source: `SourceAssets/Environment/ConnectedCliff/ConnectedCliff.blend`.
- Manifest and exact placements: `SourceAssets/Environment/ConnectedCliff/Composition.json`.
- Five rock GLBs and wrapper scenes: `Assets/Levels/Environment/ConnectedCliff/<library-name>.glb` and `.tscn`.
- Connected core/ground: `Assets/Levels/Environment/ConnectedCliff/CliffTransitions.glb` and `.tscn`.
- Composition scene: `Assets/Levels/Environment/ConnectedCliff/ConnectedCliff.tscn`.
- Eastern material: `Assets/Levels/Environment/ConnectedCliff/EasternStepSlate.tres`.
- Battlefield instance: `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`, root child `ConnectedCliffEnvironment`, identity transform.
- Rollback records: `HiddenLegacyNodes.json`, `RestyledEasternTreads.json`, and `Baseline/CliffsideArena.tscn` under the source directory.
- Audit evidence: `SourceAudit.json`, `Validation.json`, native/profile logs and screenshots listed below.

The builder is `Tools/BuildStage30CConnectedCliff.py`; integration is `Tools/Stage30CScene.py`; Godot verification/profiling is `Tools/VerifyStage30C.gd`. `Tools/AuditStage30CSource.py` reads the existing Blender file without saving it. `Tools/ReportStage30C.py` verifies hashes, native exports, tests and node-property scope before producing this report.

The source Blender scene contains linked copies of the western slice and simple eastern tread references for review only. Those references are excluded from the runtime exports. The six GLBs total **195,440 bytes**, contain no images, and have distinct hashes. Runtime rock instances share five actual Godot `ArrayMesh` resources; they are not unique mesh copies hidden behind identical names.

## Material and texture reuse

Cliff and core use the existing western `SliceMaterial2.tres`; adjacent ground uses `SliceMaterial0.tres`. Both retain the western two shared **512×512** albedo/detail and restrained normal maps, linear vertex tint, metallic 0, roughness 0.9 and normal scale 0.2. The eastern original box treads use one derived slate material with a constant base tint because those primitive meshes do not carry the library's vertex colors. It references the same two maps.

The expansion therefore uses **two existing shared material resources**, plus **one new eastern tread material**. Together with the unchanged western slice, the active environment uses five distinct material resources. No texture image was created, copied per formation or embedded in the new GLBs. Original downloaded textures and western assets remain untouched.

## Geometry storage, rendered work and profiling

| Measurement | Value |
|---|---:|
| Unique reusable rock meshes | 5 |
| Linked rock placements | 37 |
| Unique rock triangles stored | 592 |
| Rock triangles across all placements before culling | 4,377 |
| Unique adjacent-ground triangles | 2,310 |
| Unique geological-core triangles | 330 |
| Total unique expansion triangles | 3,232 |
| Total expansion triangles across all instances before culling | **7,017** |
| Expansion mesh instances / surfaces | 39 / 39 |
| Unchanged western slice | 17,716 triangles, four surfaces |
| Retained eastern treads | 240 triangles, unchanged |
| Combined western + expansion + eastern tread main-pass geometry before culling | 24,973 triangles |

Instancing reduces duplicated mesh storage; it does **not** reduce the 4,377 rock triangles submitted when all 37 instances are visible. Linked `MeshInstance3D` nodes still imply individual surfaces/draw work; no MultiMesh draw-call reduction is assumed. Replacing many old separate decorative surfaces reduced measured draw calls despite adding more detailed geometry.

Godot 4.7.2 Compatibility/OpenGL, GTX 1660 Ti, 1280×720, identical normal and angled camera transforms, VSync disabled, 2.2-second warm-up per view and 120 sampled frames:

| Scene | View | Median draw calls | Median rendered primitives | Median wall frame interval, ms | Renderer video-memory counter, MiB |
|---|---|---:|---:|---:|---:|
PROFILE_TABLE

At the normal tactical view, draw calls fell from **3,009 to 2,907** versus Stage 3.0B (102 fewer, about 3.4%), while renderer primitives rose from **583,888 to 588,040** (4,152 more, about 0.7%). These renderer counters cover the whole battlefield, units, UI and render passes; “primitives” is not a unique triangle-storage count. The explicit mesh triangle table above supplies the geometry count.

The greybox and western-slice baselines were captured before interruption; the final expansion profile was completed on resume. Wall frame intervals and whole-renderer memory are informative snapshots, not a controlled cross-session GPU benchmark or a guaranteed FPS improvement. The final video-memory counter is higher than the western baseline even though the new GLBs embed zero images and reuse existing texture resources; allocation/cache differences were not isolated. No texture-memory saving is claimed. Raw monitor output is retained in `artifacts/stage30c_*_profile.json`. Further batching or LOD work should depend on an actual hardware budget rather than mesh-instance counts alone.

## Visual verification

- [Full angled cliff](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_full_cliff.png).
- [Western staircase transition](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_west.png).
- [Central cliff close-up](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_central.png).
- [Eastern staircase transition](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_east.png).
- [Normal tactical camera with units/HUD](../SourceAssets/Environment/ConnectedCliff/QA/Godot_after_tactical.png).
- [Before/after comparison](../SourceAssets/Environment/ConnectedCliff/QA/BeforeAfter.png).
- [Existing Blender perspective render](../SourceAssets/Environment/ConnectedCliff/QA/BlenderFullCliff.png).

Blender MCP and computer control were used during construction to inspect the saved source and live Godot result. On resume Blender MCP was unavailable, so the saved source was audited read-only in background Blender and the final existing-asset Godot captures were refreshed and inspected. No asset regeneration was needed.

The main row-15 escarpment is continuous across the central boundary, with a matching western palette, varied broad fractures, buried joins, shelves and sparse cliff-foot stones. Both stair routes and their upper approaches remain open. The one focused composition correction removed regular crown/foot repetition before interruption; the saved result was retained. No major original rectangular cliff placeholders remain along this completed row-15 edge. Units, ownership markers, grid and HUD remain readable in the normal tactical capture.

Remaining limits are visible rather than hidden: the **southern outer perimeter at row 19 is still greybox**, as are castles, much of the forest/middle and farming scenery. They are outside this stage. The eastern steps remain simpler than the detailed western stairs. Some low-poly facets and source-family reuse remain apparent in close-up; this is not a final whole-map art or performance approval. No large background, fortress redesign or new gameplay mechanic was added.

## Final gameplay and preservation validation

- Resumed C# build: passed, zero warnings/errors (`artifacts/stage30c_resume_build.log`).
- Focused suite: **108 passed, 0 failed, 0 skipped** (`artifacts/stage30c_tests/stage30c_resume.trx`). This includes committed ascent/descent on both columns of each staircase.
- Native scene round-trip, stair overlays and 4,800 picking checks across eight orientations: passed again (`artifacts/stage30c_resume_layout.log`).
- Complete Cliffside smoke: passed again (`artifacts/stage30c_resume_smoke.log`), including combat, capture/VP, castle transfers, timers, HUD and free-mode regression.
- Native gameplay export is byte-identical to the pre-expansion snapshot: `GAMEPLAY_HASH`.
- Imported geometry: 60 adjacent walkable centers match standing heights within 0.026 units; original eastern tread bounds/overlays are additionally covered by native stair verification. Both stair openings are checked against imported rock vertices. No added collision/navigation objects.
- All **333 protected code and western-source/runtime files** still match the task-start hashes; all original scene node properties match except the recorded decorative visibility and eastern material changes. The three original Nature source files are hash-identical. Existing gameplay rules and unit models were not edited.

Early construction-only checks found a missing upper landing replacement and overly regular rock accents; both had already been corrected and validated before interruption. Historical headless editor shutdown RID/ObjectDB/resource diagnostics remain in the import log, separate from the passing runtime validation. No unfinished export was accepted as final.

## Reproduction, rollback and stop point

For inspection without rebuilding:

```text
Godot --path . --resolution 1280x720 --script Tools/VerifyStage30C.gd -- --cliff-mode=after
python Tools/ReportStage30C.py
```

`Godot` denotes the installed Godot 4.7.2 .NET console executable. The report command requires actual successful resumed build/test/native/smoke evidence. The builder and integration scripts are retained for future authorized edits; they were not rerun on resume. Integration derives from this stage's baseline, so do not run it over unrelated later scene changes without merging those edits.

Rollback is the inverse of this visual change: remove the new `ConnectedCliffEnvironment` root instance, restore recorded node visibility and original eastern tread materials from the task-start snapshot. Preserve unrelated later edits; do not replace the scene with an older Git HEAD. Sources, GLBs, screenshots, backups and manifests remain locally stored/ignored under existing repository policy; back them up with the game assets. Tools, wrapper/material scenes, tests and this documentation are versionable.

Stop at Stage 3.0C for visual review. A future separately authorized stage could address the southern outer perimeter or refine the simpler eastern steps, after choosing which has greater visual benefit. Neither was started.

## Exact linked placements

Positions use Godot world XYZ; yaw is in radians. The same data is machine-readable in `Composition.json`.

| Instance | Shared mesh | World position | Scale XYZ | Yaw | Role |
|---|---|---|---|---:|---|
PLACEMENT_TABLE
'''
for key,value in {'LIBRARY_TABLE':library_table,'PROFILE_TABLE':profile_table,'GAMEPLAY_HASH':sha(after).upper(),'PLACEMENT_TABLE':placement_table}.items():report=report.replace(key,value)
(ROOT/'Docs/Stage30CConnectedCliffEnvironment.md').write_text(report,encoding='utf-8')
print(json.dumps({k:v for k,v in summary.items() if k not in ('exports','profiles','source_audit')},indent=2))
