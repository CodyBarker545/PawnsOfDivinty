# Cliffside fort-to-lower-lane visual correction

Completed 2026-11-16 as a focused correction after Stage3.0D.2B; no new production stage started.

The lower descents now share the fort-side entrance's rectangular masonry, pale coping and broad sill proportions. Both sides have paved approaches that meet the existing side-gate sill, continue beside the defended edge and terminate at the upper stair landing. Long dressed tread fronts and capped retaining courses make the descent read as part of that constructed access route. The eastern stone was warmed/lightened slightly to bridge the fort masonry and the weathered cliff stairs. The lower dirt, cliffs, forts and western source assembly were not rebuilt.

## What changed

- Side-gate reference: `red_forward.tscn` /`blue_forward.tscn` use a2.44-unit gate sill; `FortCurtainWall.tscn` /`FortCornerTower.tscn` establish rectangular stone, lighter coping and darker footing.
- Both approaches are2.44 units wide, on the existing outside-fort walkable corridors. A short lateral branch meets each actual side-gate sill at world X5.375 /26.625, Z16.03…18.47. The corridor continues to Z22.25 at the existing stair head; no new route is implied across blocked terrain.
- The full stair opening remains2.5 units wide. Dressed two-piece nosings sit0.004 units above the original tread tops, with low capped masonry strictly outside the opening. Original ten-step profiles are preserved.
- Eastern tread bodies are shortened at the front beneath the new nosings, using the existing three MultiMesh resources. All tread centers retain their exact original height. A derived material reuses the existing two stone maps; no texture or purchased content was added.
- Paved approach tops are Y0.024, clearing the existing thin paving while remaining below the optional grid. Logical Y0 and all gameplay surfaces/metadata remain unchanged. Lower terrace source/runtime files and fort scenes retain their hashes.

## Source, runtime and rollback

- Editable source: `SourceAssets/Environment/FortStairIntegration/FortStairIntegration.blend`.
- One-batch export and wrapper: `Assets/Levels/Environment/FortStairIntegration/FortStairIntegration.{glb,tscn}`.
- Eastern material: `Assets/Levels/Environment/FortStairIntegration/DressedStairStone.tres`.
- Existing eastern native scene updated: `Assets/Levels/Environment/EasternStair/EasternStair.tscn`.
- Root placement: independent identity `FortStairIntegration` instance in `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`.
- Task-start scene/eastern scene/hash records: `SourceAssets/Environment/FortStairIntegration/Baseline/`.
- Construction and audit records: `Composition.json`, `Validation.json` in that source folder. `SessionBefore.blend` preserves the previous Blender working session.

Rollback removes the new root/ext-resource and restores the snapshotted eastern native scene. Keep any later unrelated edits. The original western/eastern Blender sources and GLBs remain unchanged. Rerunning the older eastern-scene bake restores its original appearance; reapply `FitFortStairIntegration.gd` afterward to retain this correction. `BuildFortStairIntegration.py` authors only the new masonry, `IntegrateFortStair.py` adds its independent root, and `FitFortStairIntegration.gd` intentionally requires the real renderer to preserve MultiMesh buffers.

## Inspection and validation

Live Blender MCP constructed/exported the masonry. Blender viewport and rendered Godot images were inspected; computer control brought the completed Godot close-up into view. The result visually continues the side-access route into the jungle-farm descent, with a consistent masonry width, dressed nosings and retaining edges. It preserves natural cliff framing and the existing lower dirt transition. Fort/camp greybox architecture outside this connection remains unchanged.

Same-camera captures are in `artifacts/fort_stair_integration/`:

| View | Before | After |
|---|---|---|
| Eastern stair detail | [before](../artifacts/fort_stair_integration/before_east_close.png) | [after](../artifacts/fort_stair_integration/after_east_close.png) |
| Fort-side route and landing | [before](../artifacts/fort_stair_integration/before_east_access_route.png) | [after](../artifacts/fort_stair_integration/after_east_access_route.png) |
| Western side route | [before](../artifacts/fort_stair_integration/before_west_access_route.png) | [after](../artifacts/fort_stair_integration/after_west_access_route.png) |
| Both stairs | [before](../artifacts/fort_stair_integration/before_both_stairs.png) | [after](../artifacts/fort_stair_integration/after_both_stairs.png) |
| Normal tactical camera | [before](../artifacts/fort_stair_integration/before_tactical.png) | [after](../artifacts/fort_stair_integration/after_tactical.png) |
| Grid ON | [before](../artifacts/fort_stair_integration/before_east_grid_on.png) | [after](../artifacts/fort_stair_integration/after_east_grid_on.png) |

Before views use the task-start eastern native-scene copy, so the comparison is not contaminated by current material/instance edits. `--stay` is a frozen review window; normal scene gameplay remains independently playable.

Build passed with zero warnings/errors; **110** focused stair/elevation/movement/picking/grid tests passed. Full Cliffside smoke passed, including combat, castle capture/transfers, HUD, timer, objectives and movement. Native round-trip/stair-preview checks and **4,800** tile-picking checks passed. Imported geometry checks verify20 eastern tread centers,20 approach centers at Y0.024, western standing-center clearance and30 exposed grid top-face samples. There are **zero new collision/navigation nodes**. Evidence: `build.log`, `tests.log`, `tests/fort_stair_integration.trx`, `smoke.log`, `layout.log`, `final_review.log`, `after_geometry.json`.

The600-cell gameplay export retains SHA256 `D86240AF4AC21BCE33222F929B1EF56351EA23811FB63DD49BFEC845C7B83F86`. All **1,803 original battlefield node blocks are unchanged**; only one independent root was appended. Of372 protected files,370 retain their hashes; the only two revised originals are the battlefield scene and eastern native visual scene. No gameplay C#, input configuration, tactical coordinates, elevation, collision, capture or transfer data changed.

The additional masonry is **8,712 triangles, one mesh/surface, one existing material**,813,796-byte GLB, zero embedded images. Its198 editable source pieces are joined into one runtime draw batch. The eastern shade variation adds one material resource using existing maps. Same both-stair view engine counters:2,201→2,205 median total draw calls;691,803→705,795 rendered primitives. These whole-scene/pass counters are not an isolated frame-time benchmark. Headless import retained the known editor shutdown RID/ObjectDB diagnostics; final visual and gameplay validation passed without runtime resource errors.

No paid generation, asset regeneration of completed sources, commit or unrelated stage work was performed. Source assets, binaries and captures follow existing ignore policy and need project-asset backup alongside Git.
