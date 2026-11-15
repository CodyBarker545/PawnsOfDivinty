# Pawns of Divinity — 30×20 Environment Production Roadmap
Version: 1.1 | 2026-11-15 | Planning document, not a verified scene specification.

## Production brief
Use the supplied [Cliffside Arena concept](../References/CliffsideArenaConcept.png) as the visual reference for the whole map. The upper panel shows how the forest rotation, central objective, paired forward forts and lower farming lane relate. The lower panorama shows the stepped cliff profile and the architectural hierarchy from a gameplay angle. Keep each forward fort a low, open stone court with a visible capture floor. Reserve the tallest silhouettes for the home hubs, with pale stone and teal roofs at Aether Spire and warm stone, timber and copper at Forgebound Ridge. Take exact placement, legal routes and sightlines from the playable grid.

Transform the existing 30×20 Godot 4.7 .NET playable greybox into the approved cliffside fantasy arena visual concept. Preserve competitive 5v5, alternating activations, first to 15 VP, symmetrical tactical geometry, forest-to-middle rotations, central objective, two forward forts, two non-capturable home transfer hubs, and isolated lower farming terrace. The Aether Spire (left, pale stone/teal) and Forgebound Ridge (right, darker stone/timber/copper) should be visually distinct with equivalent traversal and sightline geometry. Art direction: hand-painted stylized dark fantasy, miniature-like presentation, simplified meshes, mostly matte PBR materials, controlled lighting, readable silhouettes and high-priority ability/selection cues.

****Non-negotiables****: Do not replace TacticalMap rules or alter pathfinding merely to match illustration; keep logic colliders separate from decorative geometry; preserve castle transfer restrictions, including contested transfers blocked and a Main Action cost. No paid Meshy generation without an explicit asset manifest, estimate, and approval. Never commit API keys. Use locally available MCP only after checking its actual tools and health. Preserve existing combat scene in version control.

## Ground truth and grid convention
The image is a stylized illustrated view, not a dimensioned orthographic plan. Positions below are provisional conceptual zones, not actual tile coordinates. Before building, Codex must inspect Scenes/Battlefield.tscn, Scripts/Gameplay/Battlefield/Battlefield.cs, TacticalMap.cs, prototype_battlfield and source scenes. Record world-to-grid scale, tile anchor/centering, elevation units, row orientation, non-walkable edges, terrain metadata, central objective, forts, home castles, capture footprints, movement geometry, sightline obstructions and import structure. Coordinate examples use ****X 1–30, Y 1–20 for planning only****; code should retain the engine's actual indexing convention. Use a CSV or JSON map manifest to lock placement after audit.

### Forward fort audit and production decision

The following zero-based coordinates supersede the provisional R03 and R05 estimates. The playable scene is `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`. Each column in the diagram represents one tile. Rows increase toward the lower terrace.

```text
          West fort       East fort
      x   6 through 11    18 through 23
y  8      ##GG##          ##GG##
   9      #....#          #....#
  10      G.CC.G          G.CC.G
  11      G.CC.G          G.CC.G
  12      #LLL.#          #.LLL#
  13      #LLL.#          #.LLL#
  14      ##GG##          ##GG##
```

`#` blocks movement and sight. Every other cell is traversable subject to normal occupancy. `G` marks an entrance, `C` a capture tile, `L` a transfer landing and `.` ordinary courtyard floor. Each fort has 14 blocked and 28 traversable tiles. Both start neutral.

**What changed in Stage 1.5A.** Compared with the repository baseline, each packed fort retains the same footprint, 14 scripted wall cells and four two-cell entrance definitions. The arena retains both castle placements and their capture and deployment zones. The art pass changed how the perimeter and usable space read from the camera. It added no movement routes, firing positions, capture options or transfer destinations.

**Replaceable objects per fort.** Ten curtain-wall visuals contain footings, wall bodies, coping and merlons. Four corner towers contain plinths, shafts, crowns, parapets and merlons. Four gate sills, four capture-border strips and six arrival stones are also visual only. Their parent gameplay nodes define obstruction, entrance, capture and deployment independently of these meshes.

**Tactical assessment.** The side gates connect the home approach with the central battlefield. A unit can contest the capture area or occupy a different tile to keep a transfer landing clear. Perimeter blockers interrupt sight and constrain pursuit. The southern gate opens onto the fort rim above the cliff. Descent requires an existing end staircase. The previous Godot playtest verified captures and landings on both sides. The route tests cover all six landings to every gate inside each fort. These checks establish access, while the relative strength of defensive positions remains a competitive playtest question.

**Recommendation awaiting layout approval.** Retain both 6×7 footprints and end primitive detailing here. A tile measures 1.25 Godot units, giving each fort a 7.5×8.75 footprint and each two-tile gate a 2.5-unit opening. After structural approval, use the same wall and corner anchors for the modular kit. Follow Meshy API → Blender MCP (scale, pivots, clearance and export) → GLB → Godot. Attach imported visuals beneath the existing gameplay nodes, keep automatic mesh collision off and verify that units, capture state and all six arrivals remain visible and accessible. The first imported fort is the P3 proof before applying the kit to the mirrored fort.

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
For every instance, store: *`instance_id`*, *`asset_id`*, *`region_id`*, *`grid_anchor`* or *`world_transform`*, *`rotation`*, *`scale`*, *`collision_role`* (none/decorative/authoritative), *`blocks_los`*, *`blocks_movement`*, *`elevation_role`*, *`faction_theme`*, *`lod_group`*, *`source_version`* and *`review_status`*. Engine-derived gameplay flags remain authoritative and must not be inferred from decorative mesh collisions.

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
Use shared, repeatable materials as the default. Build and approve the P2 art bible (palette, roughness ranges, texel density, wear amount, brightness and silhouette guidelines) before batch generation. Favor stylized, hand-painted-looking base color rather than photorealistic textures; do not rely on a shader to fix inconsistent source art. Prefer 1K–2K tiled or atlased textures and reserve larger maps for hero objects only after profiling. Suggested initial budgets: hero characters 2K, small equipment 512–1K, terrain tiles 1K, environment props 256–1K; profile at actual tactical camera distance before fixing limits.

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

### Approved style and contrast hierarchy
- **Environment (lowest contrast):** muted earth, stone, foliage and architectural materials with broad painted color variation, simplified detail and mostly matte roughness. Avoid high-frequency noise that competes with grid or unit silhouettes.
- **Characters (medium to high contrast):** recognizable silhouettes and color blocking at default tactical zoom. Preserve existing rigged/animated meshes; keep character skin, cloth, armor and equipment materials stylistically consistent.
- **Abilities, ownership and tactical indicators (highest contrast):** saturated effects and unmistakable selection, reachable/attack tile, objective and capture-state feedback. Do not rely on hue alone where symbols/shapes help.
- **Faction art direction:** Aether Spire uses pale ivory stone and teal/cool arcane accents; Forgebound Ridge uses dark weathered masonry, timber, iron and warm copper/amber accents. Gameplay collision, dimensions, traversal and sightlines remain mirrored.
- **Avoid:** photorealistic surface noise, strong cel-shading bands, thick outlines everywhere, global pixelation, excessive bloom, deep depth of field and fog that hides action information.

### Shader and lighting library scope
| Library ID | Implementation target | Required behavior | Test gate |
|---|---|---|---|
| SHD-CHAR-STYLIZED | Optional Godot spatial shader alongside baseline StandardMaterial3D | Preserve painted albedo and normal-map response; configurable gentle light softening and restrained rim highlight; compatible with skinned animations | Compare standard/stylized character at tactical zoom and in motion |
| SHD-TERRAIN-BLEND | Shared terrain material/shader | Grass, dirt and rock blending via masks, vertex color or slope/height only where compatible with gameplay geometry | Seamless modular terrain and clear stairs/cover boundaries |
| SHD-MAGIC | Reusable ability material family | Controlled emission, dissolve/edge effects and animated noise; ice, smoke and temporary barriers remain legible | Clear silhouettes and blocked/clear tiles in combat |
| SHD-INDICATOR | Reusable overlay and projected indicator materials | Selection, targeting, movement range, capture zones and team affiliation remain distinct across materials | Readable on both faction halves and all zoom levels |
| LGT-BASE | DirectionalLight3D + WorldEnvironment test rig | Stable direction, soft shadows, restrained ambient occlusion and color grading, minimal glow | Consistent comparisons under same light/camera; no lost movement indicators |

Begin with StandardMaterial3D baselines and a **single** optional SHD-CHAR-STYLIZED proof, not all shaders at once. Build terrain/indicator/magic shaders as their production needs arise. Treat P8's successor lighting stage as whole-map polish; the P2 light rig establishes the baseline earlier.

### Texture quality tests
Check: tiling seams; texel density; directional lighting baked into albedo; normal orientation; channel conventions; alpha edges; roughness consistency; Godot import color space; texture memory; material count per asset; edge legibility from tactical zoom; identification of actionable bushes, cover and capture zones.

## Codex → Meshy API → Blender MCP → Godot pipeline
1. ****Discover tools****: Codex checks its configured Blender MCP and Meshy access, Blender version, Godot editor/headless commands, actual installed API endpoints; no assumptions about server names. Never log secret keys.

2. ****Asset ticket****: Codex writes a YAML/JSON asset brief with ID, role, region, reference crop(s), model prompt, dimensions in Godot meters, pivot, target triangle budget, target texture set, collision role and review checkpoint.

3. ****Model strategy decision****: precise modular stairs/terrain/cover in Blender; unique castle ornament/statue/prop via Meshy; use image-to-3D with cropped asset reference where useful; text-to-3D for simple uniquely named objects. Full map concept image should not be used to generate a complete arena mesh.

4. ****Preview without spending unnecessarily****: create a basic mesh preview/task where supported; inspect silhouette and topology before texturing. Meshy task costs and availability depend on account, model, and options.

5. ****Retrieve outputs****: record task ID, prompt, model version, credits, downloaded model URL/hash and provenance in manifest; use versioned sources.

6. ****Blender MCP cleanup****: import GLB; inspect and fix nonmanifold or duplicate geometry, orientation, real-world scale, transforms, UVs, normals, pivots; remove baked ground shadows/unwanted basing; split material slots; generate simplified collision mesh and LOD if needed.

7. ****Material preparation****: apply shared tiling materials to modular assets, bake bespoke maps for hero props, validate render under common light rig. Keep master *`.blend`* and exported *`.glb`* separate.

8. ****Export****: one asset family per GLB or carefully managed multi-object modular kit; consistent axis and transform, no external file assumptions, deterministic naming.

9. ****Godot import****: asset becomes an inherited or wrapper *`.tscn`*; attach simple decoration collision only if required; instantiate through map manifests; avoid editing core gameplay nodes.

10. ****Automated checks****: file/import validity, scale bounds, triangle limits, missing maps, duplicate material count, collision role correctness, nav passability, line-of-sight symmetry, scene load, .NET compilation, xUnit suite and screenshot comparison.

11. ****Visual QA gate****: render from actual tactical camera, evaluate characters and action indicators against background; reject or revise before batch generation.

12. ****Promotion****: update asset registry and map placement manifest; commit reviewed version; retain source and credit ledger.

****References****: Meshy Image-to-3D https\://docs.meshy.ai/en/api/image-to-3d ; Meshy Text-to-3D https\://docs.meshy.ai/en/api/text-to-3d ; Meshy Retexture https\://docs.meshy.ai/en/api/retexture ; Godot GLB import https\://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/available_formats.html ; Godot 4.7 export https\://docs.godotengine.org/en/4.7/tutorials/assets_pipeline/exporting_3d_scenes.html

## Repository structure (proposed, Codex should adapt to existing structure)
```text

SourceAssets/Environment/

  References/                 # approved concept and cropped asset references

  Manifests/                  # asset_registry.json, placements.json, cost_ledger.json

  Blender/                    # editable master source .blend

  Meshy/                      # immutable raw generation .glb + task metadata

  Textures/Shared/            # common tileable and atlased textures

  Textures/Hero/              # approved bespoke masks and maps

  Exports/                    # cleaned game-ready GLBs

  Screenshots/QA/             # Godot tactical camera review images
  ArtDirection/               # approved style bible, palettes and comparison boards

Game/Assets/Environment/

  Shared/                     # tile, stairs, cliff, ruins, foliage wrappers
  Materials/                  # standardized materials and reusable shader resources

  AetherSpire/                # themed structures and material instances

  ForgeboundRidge/

  Background/

Game/Scenes/ArtTests/         # isolated lighting/material/character vertical style test
Game/Scenes/Maps/

  CliffsideArena/             # visual scene + map manifest, separate from game state

Game/Tests/Environment/       # import/grid/symmetry smoke validations

```

Relative paths should follow actual repo organization after inspection; *`Game/`* may not be the real project root.

## Production stages and acceptance gates
### P0 — Baseline audit and backups
Tasks: identify Godot project root, existing greybox and coordinate convention; inspect scenes; run current C# tests; record before screenshots; locate any reusable stone, trees, terrain and castle models; verify MCP/tool access; approve per-task spending cap. Artifacts: *`CurrentMapAudit.md`*, *`GridManifest.json`*, baseline captures, test results, *`AssetRegistry.json`*.

Gate: source-of-truth graph, all fort/hub/objective positions, current rules and backup scene verified.

### P1 — Geometry and visual blockout
For the forward forts, submit the audited footprint above for approval. Further blockout work requires a specific traversal or sightline problem and a proposed tile diagram.
Tasks: prove top route can rotate into middle; reserve bottom lane stair access; establish cliff height; normalize floor/steps/tile scale; place proxy fort/base and core ruins. All collision is authored or checked against TacticalMap; decorations not authoritative. Build mirrored traversal tests. Gate: symmetry and reachability tests pass; 5v5 camera is usable. No expensive asset generation.

### P2 — Visual foundation: art direction, textures, shaders and lighting
**Objective:** Approve one coherent visual language before P3 production proof and all subsequent asset batches. This is a separate, isolated presentation test, not an excuse to replace the working greybox or alter any tactical data.

**Tasks:**

1. Produce *`ArtDirectionBible.md`* with approved references, silhouettes, faction palette, color/contrast hierarchy, texel density, roughness/metallic behavior, painted detail density, and clear rules for actionable vs decorative foliage.
2. Create *`ArtStyleTest.tscn`*: a contained sample using copied/instanced existing assets—one animated character, grass, stone, cliff, stairs, wall, tree and a small selectable/capturable zone. Keep gameplay colliders and TacticalMap untouched.
3. Build a baseline StandardMaterial3D set and texture examples (hand-painted grass, dirt, cliff stone, masonry, timber/foliage, character cloth/armor) with documented basecolor, normal, roughness and metallic channels; normal maps and metallic maps only where useful.
4. Implement and compare an **optional** soft stylized character shader versus standard Godot PBR, preserving albedo, normal maps and skeleton animation; expose tunable parameters. Reject the shader if it harms clarity or introduces unnecessary maintenance.
5. Set consistent DirectionalLight3D and WorldEnvironment defaults. Use soft shadows, mild ambient occlusion and restrained color grading; keep bloom primarily for magic. Avoid global pixelation and obscuring fog.
6. Create *`MaterialLibraryManifest.json`* and name/version standards for shared textures, shaders, character skins, faction variants and Blender-to-Godot GLB import. Keep raw Meshy outputs and editable .blend sources for reproducibility.
7. Produce camera-matched before/after images at minimum, normal and maximum gameplay zoom, with moving character, foreground foliage, selection circle, movement range, target/LOS indicators and capture-state previews.
8. Compare Aether and Forgebound material variants with identical gameplay geometry. Verify readability, contrast and frame-time impact; record choices and rejected alternatives in *`VisualStyleReview.md`*.

**Gate:** art bible, approved test scene, at least one textured animated character and representative environment surfaces, baseline lighting and reusable material definitions reviewed from the actual Godot tactical camera; movement/LOS unaffected; spell/selection information unmistakable; initial performance measurements recorded. **No mass retexturing, batch Meshy spending or final environment asset generation before this approval.** No paid Meshy calls without separate approval.

### P3 — Vertical slice: ONE forward fort and adjacent stairs/cliff
Use the approved footprint and replace its temporary visual children with imported kit pieces. Check the gate openings, capture feedback and transfer arrivals against the audited layout from the gameplay camera.
Create and polish: CAP-001 capture glyph, CAP-002 small dais, COV-001 low wall, RUI-001 column, STR-001 stair, CLF-001 cliff straight, TER-001 grass and PTH-001 cobble, one faction banner, one material per essential surface. The slice is an end-to-end production-pipeline test using the approved P2 art bible, material library, shader baseline and lighting. Gate: visual quality from Godot camera, stable collision/grid, correct scale, art budget, reproducible model export, character readability.

### P4 — Modular terrain and cliff kit
Build all TER, CLF, STR and PTH families; build material blends and instancing; extend top forest and lower route; fill external cliff perimeter. Gate: no holes, stepping/LOS test parity, acceptable draw calls, no accidental shortcut routes.

### P5 — Functional shared gameplay props
Build cover COV, ruins RUI and capture CAP; annotate every blocking prop with tactical policy; mirror gameplay proxy positions; central objective silhouette; keep visual faction dressing independent. Gate: mirrored cover/LOS masks and fort arrival clearance verified.

### P6 — Aether Spire left base and left fort
Build CAS core + AET variants; create walls, gate, primary 2–3 tower silhouettes, teal roof and banner; model farther architectural ornaments outside grid. Gate: visually readable home vs fort, no camera obstruction, home non-capturable while transfer-capable.

### P7 — Forgebound Ridge right base and right fort
Reuse CAS core with FOR variants; match collision, fort capture and playable stairs; copper roof, timber/iron scaffold and smokestack purely ornamental. Gate: geometry equivalence checks, same movement and LOS, visually distinct base.

### P8 — Forest, farming camps and environment storytelling
Produce VEG, ROC, FAR; keep actionable bush distinct from decoration; mark neutral camp spawn/clearance and rewards as gameplay-design-dependent until verified. Build upper lane through-paths and mirrored rotation connectors. Gate: no foliage concealment of units, paths, camp accessibility and objective locations validated.

### P9 — Lighting, landscape and effects
Extend and tune the approved P2 directional light and WorldEnvironment across the full battlefield; add restrained contact shadow/ambient occlusion, selective non-obscuring fog, distance haze, low-detail hills and waterfalls, muted objective glow and faction highlights. Keep effects below tactical UI priority; screenshot at min/nominal/max camera distances. Gate: readability, stable performance on target hardware, consistent material response.

### P10 — Integration, balancing and optimization
Add LOD / visibility ranges, MultiMesh where appropriate, merged/reused materials, optimized texture sizes and simplified collision. Test all 10 units, ability terrain changes, movement highlights, capture animations, combat animation and camera behavior. Compare distances/sightline symmetry and VP pacing in playtests (concept art itself does not validate balance). Gate: compile/test passes; documented FPS and memory measurements on defined target hardware; approved comparative screenshot board.

## Ordering and dependencies
P0 → P1 → P2 (art-style approval) → P3 (forward-fort production proof) → P4/P5 (functional kits) → P6/P7 (bases) → P8 (vegetation/camps) → P9 (whole-map lighting/polish) → P10 (verification). Do not mass-generate castles before P2 and P3 approval. Functional/critical geometry first; ornament last.

## Model QA rubric (every generated asset)
- Right visual silhouette and approved faction/theme

- Correct scale, transforms, axis, origin/pivot and orientation

- Game-view visible detail; no needlessly tiny details

- Acceptable polycount, no hidden high-density interior faces

- UVs without egregious stretching/overlap where unique UV expected

- Consistent painted-stylized albedo, restrained PBR response, approved material library and correct color spaces

- Simple collision (or collision off) matching defined role

- No unexpected navigation or LOS changes

- Silhouette, team affiliation, selection and effects legible at tactical min/nominal/max zoom

- Proper origin file, generation job ID, prompt and version metadata

- Clean .blend and deterministic .glb export, checked inside Godot

## Suggested first automated Codex task
```text

Begin Stage P0 of Pawns of Divinity's 30x20 Cliffside Arena Environment Roadmap. You are working in the actual Godot 4.7 .NET repository and may use configured Blender MCP and Meshy API only after verifying the connections. Do not change game rules, replace the existing battlefield scene, delete content, or spend Meshy credits.

1. Inspect the repository, main Godot scenes, Scripts/Gameplay/Battlefield/Battlefield.cs, TacticalMap.cs, prototype_battlfield, all environment assets, castle-transfer implementation and tests.

2. Determine the actual 30x20 grid axes/origin/tile size, elevation, scene layers, navigation and LOS representation, collision and material pipeline. Record current positions and footprints of home transfer hubs, forward forts, central objective, staircases, forest routes and lower farming camps. Identify unclear or unimplemented parts without inventing them.

3. Cross-check design intent: mirrored competitive gameplay, left Aether Spire and right Forgebound Ridge visual identities, upper forest rotations to middle, lower commitment farming lane, 5v5 and 15 VP alternating activations. Preserve current castle transfer mechanics and home non-capturable status.

4. Assess Blender MCP actions and Meshy API readiness, but do not call paid generation. Determine whether tools can import, clean, UV/texture, save .blend, export .glb, and capture screenshots. Report blockers precisely.

5. Create an asset registry and map placement manifest schema. Propose exact repository-relative source/export/import paths adapted to this project. Prioritize the P2 visual test scene followed by the P3 forward-fort vertical slice.

6. Run compilation and current tests, summarize results, take baseline screenshots where tools permit, and write a Stage P0 audit plus prioritized Stage P1/P2/P3 tasks.

7. Stop before modeling and request review of the map audit, proposed P2 art-style test, unique base asset list and estimated Meshy job cost. Do not expose API secrets.

```

## Decisions to revisit only after testing
- Exact location and tile footprints of both farm camps, forest stair transitions and center ruins. The forward-fort capture platforms are recorded in the audit above.

- Whether forest bushes are mechanically actionable cover, evasion terrain, or purely decoration

- Whether low cliff walls block both LOS and projectiles or only movement

- Neutral camp gold/VP rewards and respawn rules

- Platform/target hardware performance budget and art texture/triangle budget

- Final detailed sky/lighting treatment and unit/UI contrast

## Completion definition
The 30x20 map matches an approved hand-painted stylized dark-fantasy art bible and feels like the concept art from the real Godot gameplay camera, has a modular source asset library with texture consistency and documented placements, runs through reproducible Codex/Blender/Meshy/Godot production steps, preserves fully mirrored competitive geometry and gameplay logic, has a reproducible Godot material/shader/lighting library and an approved visual test scene, and passes automated plus manual 5v5 playtests.&#x20;
