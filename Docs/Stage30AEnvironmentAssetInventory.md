# Stage 3.0A — Environment asset discovery and production inventory

Inventory and visual catalog complete; license review complete with explicit holds. **No runtime promotion, generation, scene construction or gameplay changes.** Review date: 2026-11-15 (user timezone). SourceAssets and QA binaries are local/ignored by the repository's existing policy; back them up with the archives. The scripts and this report are versionable.

## Ground truth and concept

Reviewed `References/CliffsideArenaConcept.png`, the current `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`, both native forward-fort scenes, the roadmap, style guide, Stage 2.3 documentation and authored Cliffside tests. The illustration shows connected rocky plateaus, upper forest, a broad central stone battlefield, paired open forts, pale/teal western and industrial/copper eastern home architecture, a lower ruin-lined farming route and decorative valley/waterfalls. Its printed grid is conceptual, not placement data.

The gameplay grid and visible environment are separate systems. Organic visual meshes may span cells. Board: 30×20, 1.25 spacing; tile center X=-2.125+1.25x, Z=4.125+1.25y. Central floor Y0; lower terrace Y-1.8, with existing stair exceptions. Stairs X4–5 / X24–25: row15 Y-0.6, row16 Y-1.2, row17 Y-1.8. Preserve legal movement, collision, LOS and elevation flags. Trees/bushes stay traversable; no evasion is implemented.

Forward forts come from authored data: west X6–11/Y8–14, east X18–23/Y8–14; each has 14 wall blockers and four double gates. Capture cells west X8–9/Y10–11, east X20–21/Y10–11. Six arrivals west X7–9/Y12–13, east X20–22/Y12–13. Central objective X14–15/Y10–11. Lower camp clearings X8–9 and X20–21/Y17–18. Home castles remain on the existing blue_home/red_home scene anchors, with no new footprints or transfer rules.

## Downloads, extraction and provenance

13 ZIP archives extracted into 12 archive destination folders; the two timestamped ZIPs share one ruins-pack root so their texture relationship is retained. One standalone `mossy_forest_4k.exr` copied/hash-verified separately. There are 13 resource roots in total (four collection packs, eight individual Poly Haven resources, one HDRI). Original downloads and all documents remain unchanged.

All ZIP members passed full CRC checks before extraction. The extractor checks absolute/drive/UNC paths, parent traversal, Windows reserved names, trailing-dot/space aliases, case collisions, symlinks and special entries, destination containment, encryption, expansion size/ratio and free space. No bundled executable, shortcut, installer, add-on or script was executed. Background Blender used factory startup and disabled automatic script execution. Extracted files have per-file SHA-256 values; originals have archive SHA-256 values and byte counts in `ExtractionAudit.json`. Available disk exceeded 620 GB; total unpacked bytes are recorded there. Repeat extraction verifies existing content rather than overwriting differing files.

| Download | Unpacked files | Destination |
|---|---:|---|
| `Blends-20261116T005836Z-1-001.zip` | 91 | `SourceAssets/Environment/ThirdParty/Quaternius/UltimateModularRuins` |
| `boulder_01_4k.blend.zip` | 4 | `SourceAssets/Environment/ThirdParty/PolyHaven/Rocks/boulder_01` |
| `brown_mud_4k.blend.zip` | 5 | `SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/brown_mud` |
| `castle_wall_varriation_4k.blend.zip` | 5 | `SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/castle_wall_varriation` |
| `cobblestone_floor_001_4k.blend.zip` | 5 | `SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/cobblestone_floor_001` |
| `forrest_ground_01_4k.blend.zip` | 5 | `SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/forrest_ground_01` |
| `KayKit_Dungeon_Pack_1.1_FREE.zip` | 1303 | `SourceAssets/Environment/ThirdParty/KayKit/DungeonPack` |
| `kenney_castle-kit.zip` | 397 | `SourceAssets/Environment/ThirdParty/Kenney/CastleKit` |
| `mossy_forest_4k.exr` | 1 | `SourceAssets/Environment/ThirdParty/PolyHaven/Lighting/mossy_forest` |
| `mossy_rock_4k.blend.zip` | 5 | `SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/mossy_rock` |
| `mountainside_4k.blend.zip` | 4 | `SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/mountainside` |
| `stony_dirt_path_4k.blend.zip` | 5 | `SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/stony_dirt_path` |
| `Stylized Nature MegaKit[Standard].zip` | 454 | `SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit` |
| `Textures-20261116T005834Z-1-001.zip` | 2 | `SourceAssets/Environment/ThirdParty/Quaternius/UltimateModularRuins` |

The timestamped Blends archive really contains **91 individual Blender sources**: broken walls, Gothic/round arches, columns, stairs, floor pieces, bushes, trees, props and animal statues. The Textures archive contains `Leaf_Texture.png` and `Bark_Texture.jpg`; packed source references corroborate this pairing. This strongly matches the [Ultimate Modular Ruins pack](https://quaternius.com/packs/ultimatemodularruins.html), whose publisher lists CC0 and 90 models. The 91-versus-90 difference and lack of original receipt/bundled license are recorded rather than silently treating identity as cryptographically verified. Keep this pack review-required until pack provenance is confirmed. No animated character/action was found in these 91 files.

The Nature archive is **Standard: 68 models**, confirmed by its included `License_Standard.txt`; it is not the full 116-model Source edition and contains no ready-made Godot shader project. KayKit has 216 canonical model names; Kenney has 76. Alternate formats and FBX variants remain preserved and do not inflate these counts. Six Poly Haven libraries contain material-preview sphere/plane geometry; these are not terrain assets. Boulder and Mountainside each contain four LODs.

## Licensing review

| Pack/resource | Status | Evidence / action |
|---|---|---|
| KayKit Dungeon 1.1 | CC0 bundled | Original License.txt retained; Kay Lousberg credit optional |
| Kenney Castle Kit 2.0 | CC0 bundled | Original License.txt retained; Kenney credit optional |
| Quaternius Nature Standard | CC0 bundled | Original License_Standard.txt explicitly identifies 68/116 edition |
| Timestamped ruins archives | REVIEW REQUIRED | Publisher CC0 statement and strong content match; no bundled license or recovered receipt; no promotion |
| Eight Poly Haven ZIP resources and HDRI | REVIEW REQUIRED | No bundled license documents. [Publisher policy](https://polyhaven.com/license) declares assets CC0; exact asset-page references recorded. Obtain pack-specific provenance/license record before promotion |

`AssetLicenses.json` records per-root status, source/reference URLs, documents, optional attribution and promotion permission. URL references are publisher evidence, not recovered original download URLs. No unknown license is assumed to permit commercial use. This completes the review with explicit unresolved holds; it does not claim every source is cleared.

## Measured inventory

**453 canonical model candidates**, **6 material resources**, one HDRI. **407 exact local mesh-signature families** group identical geometry across named/material variants; 453 source names are useful production variants, not a claim of 453 distinct silhouettes. Mesh signatures are conservative and do not normalize vertex order or transforms. Every source imported successfully into Blender 5.2.2 LTS; no unresolved image dependencies remained after unique filename resolution or packed-image fallback. UV layer names, source objects/collections, material slots, texture paths/dimensions, bounds, triangle counts, modifiers and actions are in the manifest. No animation actions were found. Authored source units are not assumed to equal Godot metres; dimensions are Blender world XYZ before normalization, Z-up; convert to Godot Y-up during a future export ticket.

| Model category | Candidates | Useful production direction |
|---|---:|---|
| Castle and fort architecture | 141 | Reuse modular walls/towers/stairs with fit and style changes |
| Props and environmental decoration | 140 | Use supplies/banners; avoid unnecessary interior detail |
| Rocks and cliff formations | 30 | Stylistic rocks/pebbles and high-detail cliff sources |
| Ruins and broken structures | 86 | Broken walls, columns and arches; license hold applies |
| Trees and forest vegetation | 56 | Living/dead trees, shrubs, grass and flowers |

Category counts describe discovery, not approval. The prop pool includes many low-priority dungeon interiors and banner variants. Geometry is reusable/instanceable after export, except LOD alternatives must not be stacked. Counts are based on source meshes before modifiers; displacement/subdivision modifiers and alpha foliage can have a greater render cost.

Poly Haven boulder: LOD0 66,122; LOD1 33,060; LOD2 16,530; LOD3 8,264 triangles. Mountainside: LOD0 153,472; LOD1 76,735; LOD2 38,366; LOD3 19,182. Use LOD3 only as an initial comparison, or simplify further for tactical use. Do not export all four LOD objects at once. Mountainside is a rock/cliff formation, not a complete mountain landscape. Mossy Rock is a texture, not a rock model.

## Most useful exact sources and proposed destinations

These are review shortlists, not final game selections. Every candidate has an exact source and proposed destination in `AssetInventory.json`; no runtime files were written. Native source object lists and glTF node names distinguish source names from Blender's temporary collision-renamed inspection objects.

| Candidate / catalog ID | Exact source objects/nodes | Original source | Proposed destination |
|---|---|---|---|
| CommonTree_1 / `E30-7d75b138c` | CommonTree_1 | `SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/glTF/CommonTree_1.gltf` | `Assets/Levels/Environment/E30-7d75b138c/CommonTree_1.glb` |
| Pine_1 / `E30-5acb704b7` | Pine_1 | `SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/glTF/Pine_1.gltf` | `Assets/Levels/Environment/E30-5acb704b7/Pine_1.glb` |
| Bush_Common / `E30-37a830100` | Bush_Common | `SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/glTF/Bush_Common.gltf` | `Assets/Levels/Environment/E30-37a830100/Bush_Common.glb` |
| Grass_Common_Short / `E30-5ab584f0f` | Grass_Common_Short | `SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/glTF/Grass_Common_Short.gltf` | `Assets/Levels/Environment/E30-5ab584f0f/Grass_Common_Short.glb` |
| Rock_Medium_1 / `E30-2e0efc8e3` | Rock_Medium_1 | `SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/glTF/Rock_Medium_1.gltf` | `Assets/Levels/Environment/E30-2e0efc8e3/Rock_Medium_1.glb` |
| Pebble_Round_1 / `E30-aa013fd24` | Pebble_Round_1 | `SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/glTF/Pebble_Round_1.gltf` | `Assets/Levels/Environment/E30-aa013fd24/Pebble_Round_1.glb` |
| wall-half-modular / `E30-b7fcd4a9a` | wall-half-modular | `SourceAssets/Environment/ThirdParty/Kenney/CastleKit/Models/GLB format/wall-half-modular.glb` | `Assets/Levels/Environment/E30-b7fcd4a9a/wall-half-modular.glb` |
| wall-corner / `E30-dca14f98f` | wall-corner | `SourceAssets/Environment/ThirdParty/Kenney/CastleKit/Models/GLB format/wall-corner.glb` | `Assets/Levels/Environment/E30-dca14f98f/wall-corner.glb` |
| tower-square-base / `E30-11723cd71` | tower-square-base | `SourceAssets/Environment/ThirdParty/Kenney/CastleKit/Models/GLB format/tower-square-base.glb` | `Assets/Levels/Environment/E30-11723cd71/tower-square-base.glb` |
| tower-square-top / `E30-fae357e43` | tower-square-top | `SourceAssets/Environment/ThirdParty/Kenney/CastleKit/Models/GLB format/tower-square-top.glb` | `Assets/Levels/Environment/E30-fae357e43/tower-square-top.glb` |
| wall_scaffold / `E30-69c49676f` | wall_scaffold | `SourceAssets/Environment/ThirdParty/KayKit/DungeonPack/KayKit_Dungeon_Pack_1.1_FREE/Assets/gltf/wall_scaffold.gltf` | `Assets/Levels/Environment/E30-69c49676f/wall_scaffold.glb` |
| stairs_modular_center / `E30-088c8bbf6` | stairs_modular_center | `SourceAssets/Environment/ThirdParty/KayKit/DungeonPack/KayKit_Dungeon_Pack_1.1_FREE/Assets/gltf/stairs_modular_center.gltf` | `Assets/Levels/Environment/E30-088c8bbf6/stairs_modular_center.glb` |
| barrel_large / `E30-8b10b32c5` | barrel_large | `SourceAssets/Environment/ThirdParty/KayKit/DungeonPack/KayKit_Dungeon_Pack_1.1_FREE/Assets/gltf/barrel_large.gltf` | `Assets/Levels/Environment/E30-8b10b32c5/barrel_large.glb` |
| box_large / `E30-16888094d` | box_large | `SourceAssets/Environment/ThirdParty/KayKit/DungeonPack/KayKit_Dungeon_Pack_1.1_FREE/Assets/gltf/box_large.gltf` | `Assets/Levels/Environment/E30-16888094d/box_large.glb` |
| banner_blue / `E30-7162238d7` | banner_blue | `SourceAssets/Environment/ThirdParty/KayKit/DungeonPack/KayKit_Dungeon_Pack_1.1_FREE/Assets/gltf/banner_blue.gltf` | `Assets/Levels/Environment/E30-7162238d7/banner_blue.glb` |
| Wall_Broken / `E30-cc029d7d5` | Wall_Broken | `SourceAssets/Environment/ThirdParty/Quaternius/UltimateModularRuins/Blends/Wall_Broken.blend` | `Assets/Levels/Environment/E30-cc029d7d5/Wall_Broken.glb` |
| Arch_Gothic / `E30-e60c9d7f4` | Arch_Gothic | `SourceAssets/Environment/ThirdParty/Quaternius/UltimateModularRuins/Blends/Arch_Gothic.blend` | `Assets/Levels/Environment/E30-e60c9d7f4/Arch_Gothic.glb` |
| Column_Round_Short / `E30-f836dd875` | Column_Round_Short | `SourceAssets/Environment/ThirdParty/Quaternius/UltimateModularRuins/Blends/Column_Round_Short.blend` | `Assets/Levels/Environment/E30-f836dd875/Column_Round_Short.glb` |
| Cart / `E30-90f3b94ed` | Cart | `SourceAssets/Environment/ThirdParty/Quaternius/UltimateModularRuins/Blends/Cart.blend` | `Assets/Levels/Environment/E30-90f3b94ed/Cart.glb` |
| mountainside_4k / `E30-1cf8d9003` | mountainside_LOD3, mountainside_LOD2, mountainside_LOD1, mountainside_LOD0 | `SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/mountainside/mountainside_4k.blend` | `Assets/Levels/Environment/E30-1cf8d9003/mountainside_4k.glb` |

Near-immediate geometry reuse: KayKit boxes/barrels, Nature grass/pebbles and dead trees after palette/scale checks. Blender adaptation: Kenney fort walls/corners/towers and roofs; KayKit stairs/scaffolds/banners; Nature living foliage; ruins wall/arch/column sources once license hold clears. None is declared ready for final Godot use without material and clearance review.

## Requirements and production choices

`AssetRequirements.json` contains **107 explicit rows** covering all requested A–J types, classification, source IDs, placement region, production notes, dependency and license gate. The following table is the reviewable human copy.

| Region / requirement | Choice | Source IDs |
|---|---|---|
| A / Large cliff faces | MODIFY | E30-1cf8d9003 |
| A / Irregular cliff sections | MODIFY | E30-1cf8d9003 |
| A / Convex cliff corners | CREATE IN BLENDER | New support geometry / effect |
| A / Concave cliff corners | CREATE IN BLENDER | New support geometry / effect |
| A / Natural cliff-top transitions | CREATE IN BLENDER | New support geometry / effect |
| A / Cliff-to-ground transitions | CREATE IN BLENDER | New support geometry / effect |
| A / Rocky overhang details | COMBINE | E30-2e0efc8e3, E30-f5b9a7a8a, E30-2a61f342f, E30-d26e6fb76 |
| A / Large boulders | MODIFY | E30-d26e6fb76, E30-33187003f |
| A / Medium boulders | MODIFY | E30-2e0efc8e3, E30-f5b9a7a8a, E30-2a61f342f |
| A / Small rock clusters | COMBINE | E30-aa013fd24, E30-b71138912, E30-003ab02f4, E30-d89955817 |
| A / Mossy rocks | COMBINE | E30-30bac3bc7, E30-2e0efc8e3, E30-f5b9a7a8a, E30-2a61f342f |
| A / Scattered stones | AVAILABLE | E30-aa013fd24, E30-b71138912, E30-003ab02f4 |
| A / Cliff-adjacent vegetation | COMBINE | E30-37a830100, E30-879672b79, E30-5ab584f0f |
| A / Stair-to-cliff transitions | CREATE IN BLENDER | New support geometry / effect |
| B / Forest grass | COMBINE | E30-d1d1e89ff, E30-5ab584f0f |
| B / Dark soil | MODIFY | E30-23fbd8faa, E30-55d9a5936, E30-6267267bd |
| B / Mossy dirt | COMBINE | E30-d1d1e89ff, E30-30bac3bc7 |
| B / Mud | MODIFY | E30-23fbd8faa |
| B / Cobblestone | MODIFY | E30-72cc32646 |
| B / Broken flagstones | MODIFY | E30-752aed864, E30-726d9d881, E30-d650efa66, E30-e9b443834, E30-754550490 |
| B / Castle courtyard paving | COMBINE | E30-95f4f4bb6, E30-e2f59ea9a, E30-1bfd26144 |
| B / Worn paths | MODIFY | E30-f0538b505 |
| B / Grass-to-dirt transitions | CREATE IN BLENDER | New support geometry / effect |
| B / Stone-to-earth transitions | CREATE IN BLENDER | New support geometry / effect |
| B / Cliff-edge ground | CREATE IN BLENDER | New support geometry / effect |
| B / Lower terrace ground | COMBINE | E30-f0538b505, E30-23fbd8faa |
| B / Rubble and debris overlays | COMBINE | E30-ad01f6a67, E30-aa013fd24, E30-b71138912, E30-003ab02f4, E30-dc5d0d499, E30-cc029d7d5 |
| C / Multiple conifer variants | MODIFY | E30-5acb704b7, E30-1a1b4bb87, E30-3cbff262a |
| C / Multiple deciduous tree variants | MODIFY | E30-7d75b138c, E30-9f6397809, E30-3613948fa |
| C / Small trees and saplings | MODIFY | E30-7f1b74f84, E30-7d75b138c, E30-9f6397809, E30-3613948fa |
| C / Dead or damaged trees | AVAILABLE | E30-72152390f, E30-07aa96e9a, E30-bb9d92db4 |
| C / Tree trunks and roots | MODIFY | E30-0db6cfe39, E30-4cdba78d7, E30-3e367a2d9, E30-3aa9301be |
| C / Bush variants | MODIFY | E30-37a830100, E30-879672b79, E30-1e74883a6 |
| C / Dense shrub clusters | COMBINE | E30-37a830100, E30-879672b79, E30-a5722100d |
| C / Grass clumps | AVAILABLE | E30-c8f8fb620, E30-5ab584f0f, E30-deda07fc7, E30-435edfe63 |
| C / Fallen logs | MODIFY | E30-8fd44d5f9 |
| C / Tree stumps | MODIFY | E30-0db6cfe39 |
| C / Forest-floor debris | COMBINE | E30-14f2de355, E30-ebd442358, E30-912ee1178, E30-7e2439a59, E30-ba6a478c6, E30-aa013fd24, E30-b71138912, E30-003ab02f4 |
| C / Rock clusters | COMBINE | E30-2e0efc8e3, E30-f5b9a7a8a, E30-2a61f342f, E30-aa013fd24, E30-b71138912, E30-003ab02f4 |
| D / Broken stone pillars | MODIFY | E30-ab8f25bdb, E30-f836dd875, E30-1e4eabc74, E30-15e2ed8cf, E30-55e6b018f |
| D / Ruined columns | MODIFY | E30-ab8f25bdb, E30-d58929fc4, E30-f836dd875, E30-dc83f2287, E30-9bbdc08eb |
| D / Collapsed arches | MODIFY | E30-fe97b6d56, E30-e60c9d7f4, E30-07da7c216 |
| D / Damaged stone walls | MODIFY | E30-dc5d0d499, E30-cc029d7d5, E30-82278a548 |
| D / Scattered rubble | COMBINE | E30-40d20c7f1, E30-ad01f6a67, E30-aa013fd24, E30-b71138912, E30-003ab02f4 |
| D / Ancient masonry | COMBINE | E30-dc5d0d499, E30-cc029d7d5, E30-ab8f25bdb, E30-d58929fc4, E30-f836dd875 |
| D / Broken monuments | MODIFY | E30-dc46235b3, E30-15b66b8d0 |
| D / Central objective structure | CREATE IN BLENDER | E30-f46362d0f, E30-f836dd875 |
| D / Weathered stone pathways | COMBINE | E30-d650efa66, E30-e9b443834, E30-754550490, E30-72cc32646 |
| D / Sparse vegetation through ruins | COMBINE | E30-5ab584f0f, E30-b41900693 |
| E / Stone wall sections | MODIFY | E30-b7fcd4a9a, E30-33147aa86, E30-6fe785493, E30-a0488649a |
| E / Wall corners | MODIFY | E30-dca14f98f, E30-ac8c13d9f, E30-25a935621 |
| E / Gate structures | MODIFY | E30-a78776831, E30-c43f23e91, E30-fecc43395, E30-a1e4f8dc1 |
| E / Towers | COMBINE | E30-11723cd71, E30-854e3c051, E30-8d97a0bf4, E30-fae357e43, E30-ed0e53cf5, E30-f1f4a7709 |
| E / Battlements | MODIFY | E30-fae357e43, E30-ed0e53cf5, E30-f1f4a7709, E30-33147aa86, E30-6fe785493, E30-a0488649a |
| E / Stone stair details | MODIFY | E30-f56d6c8c7, E30-c5ab6cc2c, E30-f3dacea69, E30-d035ac52a, E30-088c8bbf6 |
| E / Courtyard floors | COMBINE | E30-95f4f4bb6, E30-e2f59ea9a |
| E / Capture-floor architecture | CREATE IN BLENDER | E30-f46362d0f |
| E / Faction banners | MODIFY | E30-7162238d7, E30-c7ac7f574, E30-4b17ea842 |
| E / Defensive architectural details | COMBINE | E30-55e6b018f, E30-9c9db6e0a, E30-448218f67, E30-f33d6b56a |
| F / Elegant stone towers | MODIFY | E30-6f3ebdccf, E30-d2a053c09, E30-daf2539db, E30-f7a52352e, E30-d5b3f04bc, E30-fae357e43 |
| F / Slender spires | CREATE IN BLENDER | E30-80e3946d2, E30-f5c11e08a |
| F / Arched entrances | MODIFY | E30-549c0123c, E30-e60c9d7f4, E30-07da7c216 |
| F / Pale slate architecture | MODIFY | E30-33147aa86, E30-6fe785493, E30-a0488649a, E30-f7a52352e, E30-d5b3f04bc, E30-fae357e43 |
| F / Teal roofs | MODIFY | E30-80e3946d2, E30-f5c11e08a, E30-22daa6e64 |
| F / Stone railings | MODIFY | E30-e65acdb39, E30-9c9db6e0a, E30-448218f67, E30-f33d6b56a |
| F / Banners | MODIFY | E30-7162238d7, E30-1ea09b95a, E30-d0a43e7bd, E30-1feb833fe |
| F / Magical ornamentation | GENERATE WITH MESHY | New support geometry / effect |
| F / Architectural foundations | CREATE IN BLENDER | E30-11723cd71, E30-854e3c051, E30-8d97a0bf4 |
| F / Decorative statues | MODIFY | E30-dc46235b3, E30-15b66b8d0 |
| G / Heavy fortified towers | COMBINE | E30-f7a52352e, E30-d5b3f04bc, E30-fae357e43, E30-c3f983faf |
| G / Industrial gatehouses | COMBINE | E30-a1e4f8dc1, E30-549c0123c |
| G / Timber structures | MODIFY | E30-f65fd3889, E30-c627760a3, E30-3e386c8b7, E30-e9b083193, E30-2d53a9cf0 |
| G / Dark stone walls | MODIFY | E30-33147aa86, E30-6fe785493, E30-a0488649a |
| G / Copper roofs | MODIFY | E30-22daa6e64 |
| G / Chimneys | CREATE IN BLENDER | New support geometry / effect |
| G / Iron railings | MODIFY | E30-fecc43395, E30-889d09a57, E30-382d08fa6, E30-242de771a |
| G / Scaffolding | MODIFY | E30-69c49676f, E30-712b4b6c7 |
| G / Forge-themed decorations | CREATE IN BLENDER | E30-8b10b32c5, E30-ff111bdfe, E30-55e6b018f |
| G / Mechanical architectural details | GENERATE WITH MESHY | New support geometry / effect |
| H / Worn dirt roads | MODIFY | E30-f0538b505 |
| H / Scattered stone ruins | COMBINE | E30-dc5d0d499, E30-cc029d7d5, E30-f836dd875 |
| H / Small camp structures | COMBINE | E30-69c49676f, E30-3e386c8b7, E30-e9b083193, E30-2d53a9cf0, E30-a0488649a, E30-b51f0fdd6, E30-a5cd910c9 |
| H / Crates | AVAILABLE | E30-16888094d, E30-d719a7ed5, E30-400861811 |
| H / Barrels | AVAILABLE | E30-8b10b32c5, E30-ff111bdfe, E30-1c054eb37, E30-39bf0f918 |
| H / Supply caches | COMBINE | E30-16888094d, E30-1c054eb37, E30-39bf0f918, E30-1ae26404e, E30-f49dcc346, E30-286ae1d3f |
| H / Broken carts | MODIFY | E30-90f3b94ed |
| H / Campfire or fire pit props | CREATE IN BLENDER | E30-aa013fd24, E30-b71138912, E30-003ab02f4, E30-8fd44d5f9 |
| H / Neutral encounter markers | COMBINE | E30-f836dd875, E30-b3f105b78, E30-7577b0851, E30-a5223f93b |
| H / Rocks and environmental cover | COMBINE | E30-2e0efc8e3, E30-f5b9a7a8a, E30-2a61f342f, E30-d89955817 |
| H / Sparse vegetation | AVAILABLE | E30-5ab584f0f, E30-37a830100, E30-879672b79 |
| I / Main staircase structures | CREATE IN BLENDER | E30-c2214d26f, E30-f56d6c8c7, E30-c5ab6cc2c |
| I / Stair landings | CREATE IN BLENDER | E30-95f4f4bb6, E30-e2f59ea9a |
| I / Stone steps | MODIFY | E30-088c8bbf6, E30-be82de804, E30-9b97f2eb2, E30-c2214d26f |
| I / Broken step variations | MODIFY | E30-088c8bbf6 |
| I / Stair sidewalls | MODIFY | E30-d567abaf2, E30-a3b520151 |
| I / Terrain-to-stair transitions | CREATE IN BLENDER | New support geometry / effect |
| I / Cliff-to-stair transitions | CREATE IN BLENDER | New support geometry / effect |
| J / Distant mountains | COMBINE | E30-1cf8d9003 |
| J / Valley terrain | CREATE IN BLENDER | New support geometry / effect |
| J / Cliff perimeter scenery | COMBINE | E30-1cf8d9003, E30-2e0efc8e3, E30-f5b9a7a8a, E30-2a61f342f |
| J / Distant castle or town silhouettes | COMBINE | E30-f7a52352e, E30-d5b3f04bc, E30-fae357e43, E30-33147aa86, E30-6fe785493, E30-a0488649a |
| J / Waterfalls | CREATE IN BLENDER | New support geometry / effect |
| J / Water surfaces | CREATE IN BLENDER | New support geometry / effect |
| J / Atmospheric fog and haze | CREATE IN BLENDER | New support geometry / effect |
| J / Sky and cloud scenery | CREATE IN BLENDER | New support geometry / effect |
| J / Distant vegetation silhouettes | COMBINE | E30-5acb704b7, E30-1a1b4bb87, E30-3cbff262a, E30-7d75b138c, E30-9f6397809, E30-3613948fa |
| S / Dedicated reward items, siege armies and interior dressing | NOT NEEDED | New support geometry / effect |

No mandatory Meshy job is justified yet. Only distinctive Aether ornament and Ridge mechanical ornament are optional generation candidates if reused statues/kit pieces and simple Blender work fail art review. Produce these late; no paid calls or generations occurred. Precision terrain connections, cliff corners/top/bottom adapters, roots, stair joins/landings, foundations, objective/capture-floor inlays, chimney shapes and fire-pit rings are Blender work. Waterfalls/water/fog/sky require future Godot shaders/effects and simple support geometry, not expensive static model generation.

Avoid for this battlefield: siege engines, bear traps, dungeon spikes, coins/keys, tiny bottles/plates, beds, tables and interior bookcases. Their files remain preserved. Avoid unmodified bright saturated palettes, glossy photogrammetry, 4K EXR maps on small props, disconnected tile-looking slabs, and all-source-LOD exports. Forest HDRI is obstructed and unsuitable as the arena's distant open sky; use it as a lighting reference only. No crop/farming reward asset or gameplay is inferred from the concept's lower route.

## Visual and material consistency

Clay contact sheets reveal silhouettes and geometry independently of incompatible source shading. Nature has broad clustered leaves and faceted rocks, fitting the miniature style after desaturation; conifers, common/dead/twisted trees give enough starting variation. Leaf cards require alpha/shadow testing and tactical-camera overdraw review. Kenney's clean toy-like towers and walls need less saturated materials, softer edge treatment, variation and foundation adapters. KayKit's rounded blocks and warm timber can supply stairs, scaffolds and props; reserve contrasting material detail for larger forms. Ruins have useful broken openings and Gothic curves but must be graded against the Stage 2.2 matte slate baseline once cleared.

Use charcoal/slate anchors, desaturated forest green, weathered earth and restrained moss; stone roughness near 0.80–0.92, metallic 0, normals initially near the calibrated 0.32 baseline. Teal/pale Aether and copper/iron/timber Ridge accents remain subordinate to units and ownership cues. Do not change every source to one shared stone material: foliage, cloth, wood and metal retain appropriate responses. Existing sources are unchanged; no batch retexturing was performed. The source-material review shows particularly dark leaf-card patches, a red-toned bush and very clean warm Kenney stone: inspect leaf normals/transparency and seasonal texture choice before promotion, then grade the palette. Some ruins use constant materials without UVs; create UVs before applying stone textures or trim sheets. Manifold/collision readiness is not certified by discovery renders.

For Poly Haven preserve all 4K originals, including EXR normal/roughness and displacement. Recommend shared 1K–2K ground maps and 512px–1K small prop maps, then compare at actual min/normal/max gameplay zoom. Bake/convert normal and roughness semantics carefully (Non-Color, GL normal direction); use runtime PNG/compressed textures and pack roughness channels appropriately. Avoid displacement tessellation for small tactical props. Reuse atlases/trim sheets and profile draw calls and alpha coverage before consolidation. No new engine performance budget is asserted.

## Catalog and review limits

[Open searchable visual catalog](../SourceAssets/Environment/QA/ContactSheets/index.html). It includes all source candidates, category sheets with names and IDs, six original ground diffuse-map swatches, and a supplemental Cycles source-material sheet. Normalized geometry previews are for shape comparison, not measured scale or final material acceptance. The source shader sheet uses unmodified materials, in-memory 1K image reduction and 12 Cycles samples. Read actual dimensions/LOD data from JSON. The mountainside was initially grouped with material downloads by filename; the final manifest corrects it to rocks, and the source-shader sheet shows its real geometry. Clay pages retain original batch membership so their cell references stay reproducible.

All sheets were checked in a thumbnail overview plus representative full pages. Source colors need grading; packed foliage images remain usable despite Blender warning about their unavailable external duplicate path. No live Godot import or battlefield camera acceptance was performed, intentionally: final material/clearance/performance approval belongs to a later single-section integration ticket. Tactical suitability here is an assessment of broad shapes against the concept and existing camera evidence, not a claim of game-tested art.

- [Castle_and_fort_architecture_01.png](../SourceAssets/Environment/QA/ContactSheets/Castle_and_fort_architecture_01.png)
- [Castle_and_fort_architecture_02.png](../SourceAssets/Environment/QA/ContactSheets/Castle_and_fort_architecture_02.png)
- [Castle_and_fort_architecture_03.png](../SourceAssets/Environment/QA/ContactSheets/Castle_and_fort_architecture_03.png)
- [Castle_and_fort_architecture_04.png](../SourceAssets/Environment/QA/ContactSheets/Castle_and_fort_architecture_04.png)
- [Castle_and_fort_architecture_05.png](../SourceAssets/Environment/QA/ContactSheets/Castle_and_fort_architecture_05.png)
- [Castle_and_fort_architecture_06.png](../SourceAssets/Environment/QA/ContactSheets/Castle_and_fort_architecture_06.png)
- [Castle_and_fort_architecture_07.png](../SourceAssets/Environment/QA/ContactSheets/Castle_and_fort_architecture_07.png)
- [Castle_and_fort_architecture_08.png](../SourceAssets/Environment/QA/ContactSheets/Castle_and_fort_architecture_08.png)
- [Ground_materials_01.png](../SourceAssets/Environment/QA/ContactSheets/Ground_materials_01.png)
- [Props_and_environmental_decoration_01.png](../SourceAssets/Environment/QA/ContactSheets/Props_and_environmental_decoration_01.png)
- [Props_and_environmental_decoration_02.png](../SourceAssets/Environment/QA/ContactSheets/Props_and_environmental_decoration_02.png)
- [Props_and_environmental_decoration_03.png](../SourceAssets/Environment/QA/ContactSheets/Props_and_environmental_decoration_03.png)
- [Props_and_environmental_decoration_04.png](../SourceAssets/Environment/QA/ContactSheets/Props_and_environmental_decoration_04.png)
- [Props_and_environmental_decoration_05.png](../SourceAssets/Environment/QA/ContactSheets/Props_and_environmental_decoration_05.png)
- [Props_and_environmental_decoration_06.png](../SourceAssets/Environment/QA/ContactSheets/Props_and_environmental_decoration_06.png)
- [Props_and_environmental_decoration_07.png](../SourceAssets/Environment/QA/ContactSheets/Props_and_environmental_decoration_07.png)
- [Props_and_environmental_decoration_08.png](../SourceAssets/Environment/QA/ContactSheets/Props_and_environmental_decoration_08.png)
- [Rocks_and_cliff_formations_01.png](../SourceAssets/Environment/QA/ContactSheets/Rocks_and_cliff_formations_01.png)
- [Ruins_and_broken_structures_01.png](../SourceAssets/Environment/QA/ContactSheets/Ruins_and_broken_structures_01.png)
- [Ruins_and_broken_structures_02.png](../SourceAssets/Environment/QA/ContactSheets/Ruins_and_broken_structures_02.png)
- [Ruins_and_broken_structures_03.png](../SourceAssets/Environment/QA/ContactSheets/Ruins_and_broken_structures_03.png)
- [Ruins_and_broken_structures_04.png](../SourceAssets/Environment/QA/ContactSheets/Ruins_and_broken_structures_04.png)
- [Ruins_and_broken_structures_05.png](../SourceAssets/Environment/QA/ContactSheets/Ruins_and_broken_structures_05.png)
- [Source_material_review.png](../SourceAssets/Environment/QA/ContactSheets/Source_material_review.png)
- [Trees_and_forest_vegetation_01.png](../SourceAssets/Environment/QA/ContactSheets/Trees_and_forest_vegetation_01.png)
- [Trees_and_forest_vegetation_02.png](../SourceAssets/Environment/QA/ContactSheets/Trees_and_forest_vegetation_02.png)
- [Trees_and_forest_vegetation_03.png](../SourceAssets/Environment/QA/ContactSheets/Trees_and_forest_vegetation_03.png)

## First connected world-building milestone and dependencies

Recommended first section: **the west central-to-lower terrace junction**, centered on existing X4–5 stairs and extending into a short adjacent cliff/ground run. Build one connected terrain foundation across several cells, with the Y0 central floor, authored intermediate stair levels and Y-1.8 lower floor. Preserve the legal stair corridor and camp access. Use a quiet ground surface first; fit cliff top, bottom and stair adapters before decorating. This establishes organic connectivity and exact gameplay alignment in one reviewable slice, without constructing the whole map.

1. Snapshot gameplay data and authored anchors; define mesh envelope and visual picking/clearance checks.
2. Connected terrain foundation and shared dirt/grass/stone transitions.
3. Integrated cliff faces, convex/concave boundaries, lower skirt and exact stair junctions. Reuse the existing 2.3B cliff material/pair where suitable; precision adapters remain local Blender work.
4. Upper forest border and sparse lower vegetation; retain traversability and existing LOS flags.
5. Central ruin kit and objective-floor composition after ruins provenance clears; avoid covering approach lanes.
6. One fort shell at authored wall parents and capture/arrival floor, then mirrored fort review. Home-castle foundations depend on terrain, shared walls/towers, faction shape review and retained transfer clearance.
7. Faction roofs, railings, banners, scaffolds and optional ornaments; decorative valley terrain, castle silhouettes, water and haze follow the foreground silhouette.
8. Profile LODs/texture memory/overdraw and review actual tactical camera with units before any wider rollout.

## Reproduction and preservation

`python Tools/Stage30ADiscover.py` validates/extracts sources and rebuilds canonical source candidates; `D:/Blender/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools/Stage30ABlenderCatalog.py` inspects and renders clay batches; run `Tools/Stage30APbrReview.py` with the same Blender flags for the material review; `python Tools/Stage30AFinalize.py` rebuilds the labeled catalog/manifests/report. Run in that order. Scripts never save extracted sources or export runtime assets. The environment root has `.gdignore` to prevent mass Godot import. No scenes/scripts/tests/characters were edited by Stage 3.0A; the pre-existing dirty worktree was preserved. Preservation checks and complete source-file verification are recorded separately in `Stage30AValidation.json`.

Stop here for review. Connected terrain production and final model selection are the next separately scoped task.
