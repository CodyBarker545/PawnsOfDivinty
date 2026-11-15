# Stage 2.1 — One neutral modular wall

Completed on 2026-11-15. One neutral stone wall is installed at west forward fort tile (7, 8). The [cliffside concept](../References/CliffsideArenaConcept.png) guided the low silhouette.

## Delivered asset and verification

Meshy via Blender MCP produced one geometry preview (`01a1e15b-a83b-738c-858d-f966f87dd559`) and one texture refine (`01a1e15c-77e4-70a0-a566-fe61b8293bd6`). The provider handled the transition automatically and returned a 956,923-face mesh with three 2K textures. The approved ceiling was 30 credits. The connector did not return an itemized charge.

Blender MCP imported the result and saved a GLB copy of the generated model. The live export timed out. The final source and runtime files were rebuilt in background Blender from that saved copy. Run `blender --background --factory-startup --python-exit-code 1 --python Tools/BuildModularStoneWall.py` from the project root to repeat cleanup, UV baking and export without another generation. The script removes small disconnected pieces, closes the mating ends, mirrors the mesh for matching profiles and cuts the ribbed top down to a flat course.

The delivered GLB has 1,034 triangles, one matte limestone material, UVs and 1K colour, roughness and tangent-normal maps. The two end profiles match at all 34 vertices, with a closed manifold surface. In Godot the local bounds run from (-0.625, 0, -0.385) to (0.625, 1.085, 0.385). Translation by one tile aligns the ends, including when both modules share a quarter-turn. Only one instance is placed in the battlefield.

Godot loaded all ten units on the 30×20 map and reported 86 blocked and 514 walkable tiles. The wall's parent retained both movement and sight blocking. The build passed with zero warnings and errors, and all 3,304 tests passed after import. The headless editor reported resource leaks on exit. The running battlefield loaded the wall and saved its [placement screenshot](../artifacts/stage21_wall.png).

## Fit and placement

The arena has 1.25 × 1.25 world-unit tiles. The current straight wall spans 1.25 along Godot X. Its base is 0.77 deep, its shaft 0.54 deep and its highest point 1.085 above the floor. All ten character fixtures measure 1.65 high using the imported mesh bounds and authored battlefield transforms. The proposed wall fits the same envelope, about 66% of standing character height.

Place the pivot at the base centre. Finish both end faces at X ±0.625 with matching cross-sections. Keep the base within Z ±0.385, bottom at Y 0 and top at Y 1.085. Test two copies 1.25 units apart and repeat after a quarter turn. Target 500–1,500 runtime triangles, one neutral matte material and a 1K runtime texture set.

## Meshy request

Use one Meshy-6 text-to-3D preview with remeshing and GLB output. Review its silhouette before refining that same task with 2K PBR textures. The approved budget covers one 20-credit preview and one 10-credit refine. [API pricing](https://docs.meshy.ai/en/api/pricing) · [Request fields](https://docs.meshy.ai/en/api/text-to-3d)

> Single straight modular wall segment for a low cliffside fort in a stylized dark-fantasy tactics game. Length 1.25, height 1.085, maximum depth 0.77 at the integrated stone footing, body depth 0.54. Three broad staggered courses of hand-cut neutral grey limestone with shallow mortar joints and a simple level coping. Both long faces finished. Flat base, identical flush vertical end profiles for end-to-end tiling. Keep edge chips shallow and inside the silhouette. Isolated wall only. Exclude terrain, detached rubble, pillars, towers, crenellations, vegetation, emblems and projecting end caps.

Texture direction: hand-painted neutral warm-grey limestone, broad restrained tonal variation, softened worn edges, dark shallow mortar, matte roughness and zero metallic response. Keep directionally lit shadows out of the base colour.

## File locations and integration workflow

1. Save the raw GLB and task IDs in `SourceAssets/levels/sources/ModularStoneWall/Meshy/`. Record the model version, parameters and actual charges.
2. Import into Blender via MCP. Set dimensions in Blender Z-up, apply transforms, plane-cut and cap mating faces as needed, check normals and UVs, and simplify while preserving the silhouette. Ground and centre the pivot. Save `SourceAssets/levels/sources/ModularStoneWall/ModularStoneWall.blend` and source maps in `SourceAssets/levels/textures/ModularStoneWall/`.
3. Export the Y-up optimized `Assets/Levels/Props/ModularStoneWall/ModularStoneWall.glb` with one PBR material and embedded runtime textures. Wrap it in `ModularStoneWall.tscn` alongside the GLB. Use the model's visual geometry only.
4. In `blue_forward.tscn`, replace only the visual child `blue_forward_wall_7_8/Wall` at (7, 8). Its scripted parent already defines movement and sight blocking. The nearby north gate occupies (8, 8) and (9, 8).
5. Verify dimensions after Godot import, seams in both orientations, unit readability, and clearance at the north gate. Run the build and fort movement tests, inspect the running arena, and save the placement screenshot to `artifacts/stage21_wall.png`.
