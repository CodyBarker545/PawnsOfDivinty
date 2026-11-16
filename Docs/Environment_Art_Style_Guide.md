# Environment art style guide

Stage 2.2 baseline · 2026-11-15 · Pawns of Divinity

## Direction and visual priorities

Dark, stylized medieval fantasy with a hand-painted, miniature-like presentation. Favor weathered medieval architecture, muted low-saturation colors, large readable forms and silhouettes, ancient stone, restrained moss, worn pathways and damaged ruins. Keep material detail moderate and atmospheric lighting controlled. Units, ownership, selection, movement and targeting cues take priority from the tactical camera.

Avoid photorealistic microdetail, bright cartoon materials, glossy masonry, darkness that hides units, and geometry with no visible gameplay-camera benefit. The existing greybox is a functional reference, not the final material or lighting standard. Stage 2.2 calibrates one wall; it does not approve whole-map lighting, shaders or every P2 acceptance item.

## Faction vocabulary

| Faction | Palette and architecture |
|---|---|
| Aether Spire | Cool slate stone, aged pale-grey accents, weathered teal, subtle magical illumination, elegant architectural shapes. |
| Forgebound Ridge | Smoky sandstone, dark iron, oxidized copper, restrained ember-orange accents, industrial and fortified shapes. |

Preserve mirrored competitive terrain and equivalent visual readability. Color and ornament can differ; movement, sight blockers, gate widths, capture floors and transfer clearances remain authored gameplay data. Keep emissive magic subordinate to actionable effects.

## Calibrated stone baseline

| Property | Stage 2.2 value |
|---|---|
| Mortar | `#2B3032` |
| Main stone | `#4D5457` |
| Worn stone | `#707677` |
| Edge accents | `#8B8F8D` |
| Moss tint | `#434B3C`, blended up to 20% |
| Roughness target | 0.80–0.92; exported 8-bit map observed at 204–230 / 255 (approximately 0.80–0.902) |
| Metallic | 0 |
| Normal strength | 0.32 |

These are texture palette anchors, not guaranteed screen colors: the arena's warm sun and cool ambient light change their appearance. Use cool charcoal-grey limestone, dark shallow mortar, subtle moss staining, softly worn edges, reduced fine noise and a matte finish. Other assets should share saturation, scale of detail and material response without sharing identical textures. Copper, iron, foliage and cloth need their own plausible responses; do not apply stone metallic settings to metal.

## Texture workflow and reuse

The wall uses three 1024×1024 runtime maps: `RuntimeColor.png`, `RuntimeRoughness.png` and `RuntimeNormal.png` in `SourceAssets/levels/textures/ModularStoneWall/`. `Stage21Bake/` preserves the earlier bake. `Tools/CalibrateStoneWallMaps.py` grades that bake using palette remapping, occupied-texel-aware blur, restrained moss, roughness clamping and smoothed, renormalized tangent normals. It needs Python, NumPy and Pillow. `Tools/CalibrateStoneWallMaterial.py` reloads and packs these maps into the existing Blender source and exports the existing GLB; it also renders the Blender review. Do not rerun either script merely to verify an unchanged asset.

Base color uses sRGB. Roughness and normals use Non-Color data in Blender. Blender's GLB export packs roughness into the glTF green channel; its encoded PNG need not equal the source PNG byte for byte. Preserve the exported normal scale of 0.32. Check channel semantics and actual pixels before calling a packed map incorrect.

Recommendations, not enforced engine limits:

- Retain 1K for small repeated walls; consider shared 1K–2K tiling or atlased materials for kits. Larger maps need a demonstrated camera-visible benefit and profiling.
- Reuse masonry across straight, end, corner and damaged variants. Use material variants or sparse masks for faction tint and wear rather than unique textures for every copy.
- Consider trim sheets for coping and stairs, a shared ruin/rubble atlas, and separate foliage/prop atlases where UVs and transparency permit. Profile material count and draw calls before consolidation.
- Match visible texel density across adjoining surfaces. Judge seams and repetition at actual gameplay zoom, not only in a close-up.
- Keep normal intensity restrained. Begin related masonry near 0.32, then verify with arena lighting. Avoid noisy normals that erase broad stone shapes or shimmer when zoomed out.

## Meshy briefs and Blender cleanup

No Meshy calls are needed for Stage 2.2. Future generation requires a separate approved asset ticket and spending scope. Prefer precise Blender modeling for modular cliffs, stairs and functional fort shells; reserve generation for distinctive organic or ornamental forms where it adds value.

Future prompt vocabulary: “stylized dark medieval fantasy; broad readable forms; weathered muted stone; shallow mortar; restrained moss; soft worn edges; matte response; moderate detail; isolated asset; no baked directional shadows.” Add exact dimensions, base-center pivot, mating profiles, required variants and exclusions. State faction accents sparingly. Do not request an entire battlefield as one generated mesh.

Keep immutable raw generation, editable `.blend`, source maps and runtime GLB separate. Inspect scale, axes, origin, normals, UVs, disconnected debris, mating ends and material slots. Apply only cleanup authorized for that asset ticket. For this completed wall calibration, geometry, UVs, origin, scale and placement are frozen. Pack or resolve texture paths, remove unwanted material complexity, and export with embedded runtime maps. Keep collision on the existing authoritative gameplay parent rather than generating decorative mesh collision.

## Godot verification gate

Import the GLB in the actual Godot 4.7 .NET project. Check all three maps, normal scale, metallic response, dimensions, transforms and missing resources. Open/run `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`; capture one detail view and the unmodified normal tactical camera with units and HUD. `Tools/VerifyStage22StoneWall.gd` performs that wall review without saving the scene. The camera lives in the runtime HUD's `TacticalWorld` SubViewport, so discover it recursively and capture that viewport for detail images.

Review stone shapes, mortar darkness, separation from ground and neighboring structures, recognizable silhouette, unit contrast and action cues. Do not brighten or darken the whole battlefield to fix one prop. Make at most one focused material correction when a serious issue is demonstrated, then document it. Build and run relevant authored-scene/movement/transfer tests plus a scene-load smoke. Report shutdown warnings and gameplay failures separately from material import success.

Stage 2.2 images and measured results are in [the verification report](Stage22MaterialVerification.md). Broader min/max zoom, moving-unit ability effects, faction comparisons and target-hardware profiling remain future production gates.
