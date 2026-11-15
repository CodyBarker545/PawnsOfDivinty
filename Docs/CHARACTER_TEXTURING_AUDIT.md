# Character texturing audit and approval proposal

Completed 2026-11-15: the approved five sequential retexture jobs and six-character integration are finished. See [the delivery report](CHARACTER_TEXTURING_DELIVERY.md) for the final files, verified 50-credit usage, validation, comparisons and retained discrepancies. The audit below is historical; its pending-work statements are superseded by that report.

Update 2026-11-15: Meshy authentication is verified (HTTP 200 from the free balance endpoint), with 2,043 available credits. The key is stored in the Blender MCP add-on preferences, not the legacy scene property checked below. Earlier statements that configuration was missing/unverified are superseded by this correction. Blender MCP is connected. No paid jobs have been submitted; the proposed 50-credit budget still requires user approval.

Audit date: 2026-11-14, America/St_Johns. Status: audit and existing Assassin validation complete; new texturing awaits API configuration and paid-job approval. No Meshy requests submitted. No runtime assets, gameplay code, animation sequences, or tests changed by this stage.

## Connections and operations

Blender MCP initially failed to connect. After Blender was opened, Blender 5.2.2 LTS connected successfully with add-on 2.0, protocol 16 matching server protocol 16. Scene inspection, Python execution, material views and animation-frame views worked. Available operations include scene inspection, full bpy execution, visual inspection, asset search/import, and generation. The generation tool creates new models and is inappropriate for this preservation task; retexture requires the dedicated REST endpoint.

Meshy is enabled in the current Blender scene, but `blendermcp_meshy_api_key` is empty. No Meshy-named environment variable was found in Blender or the execution shell. This verifies an enabled toggle, not authenticated API access. Configure the key privately in Blender; do not paste it into chat. No authentication or credit balance has been verified. Telemetry is off.

## Asset inventory

Paths below are relative to `D:/SoftwareEng/Game/`. All six runtime models have one mesh, one material, TEXCOORD_0, JOINTS_0, WEIGHTS_0, and a skin with inverse bind matrices. Exported vertex counts include vertices split at UV/normal seams and therefore differ from editable Blender vertex counts.

| Character | Current runtime GLB in Assets/Characters/MobaBeta | Export vertices | Triangles | Bones | Current maps |
| --- | --- | ---: | ---: | ---: | --- |
| Hooded Assassin | AssassinTextured.glb | 18,622 | 10,346 | 24 | 4K albedo, tangent normal, packed metallic/roughness |
| Gentle Guardian | GentleGuardian.glb | 155,049 | 196,840 | 28 | 2K normal; gray base factor, metallic 0, roughness 0.5 |
| Time Weaver | TimeWeaver.glb | 135,409 | 191,325 | 28 | 2K normal; gray base factor, metallic 0, roughness 0.5 |
| Nigel | Nigel.glb | 154,444 | 197,516 | 28 | 2K normal; gray base factor, metallic 0, roughness 0.5 |
| Gravebound Colossus | Colossus.glb | 169,154 | 195,629 | 28 | 2K normal; gray base factor, metallic 0, roughness 0.5 |
| Beothuk Spirit Guardian | SpiritGuardian.glb | 156,176 | 196,681 | 28 | 2K normal; gray base factor, metallic 0, roughness 0.5 |

Current gray materials have no albedo or metallic/roughness texture assigned. This is a statement about current materials, not a claim that no other historical texture exists anywhere in SourceAssets.

| Character | Editable source / source vertex count | Concept art under SourceAssets/characters/Concept_Art |
| --- | --- | --- |
| Hooded Assassin | Current textured source: `SourceAssets/characters/hooded_assassin/working_textured_model_assassin/Meshy_AI_Masked_Assassin_Rigged_20261115_animated_mixamo_Running/` FBX and four PNGs, 18,622 vertices. Legacy `hooded_assassin/assassin.blend`, 140,240 vertices, is a different 28-bone asset. New packed review source: `hooded_assassin/texturing_validation/AssassinTextured_Validated.blend`. | Dark Fantasy Assassin Blueprint Sheet.png |
| Gentle Guardian | `SourceAssets/characters/gental_guardian/guardian.blend`, 154,072 | The Gentle Guardian Character Concept Sheet.png; Gentle Guardian_ Character Asset Compendium.png |
| Time Weaver | `SourceAssets/characters/ash_scholar/mage.blend`, 135,337 | Time Weaver Mage Concept Sheet.png |
| Nigel | `SourceAssets/characters/axe_warrior/nigel.blend`, 154,306 | Nigel the Orc_ Warrior Concept Sheet.png |
| Gravebound Colossus | `SourceAssets/characters/broken_oath_knight/knight.blend`, 168,974 | Gravebound Colossus Concept Sheet.png |
| Beothuk Spirit Guardian | `SourceAssets/characters/native/native.blend`, 155,173 | Coastal Spirit Guardian Concept Sheet.png and its (1) variant |

Blender source inspection found UVMap and Principled materials on all six selected source meshes. Sources retain embedded/downloaded Running or Walking actions in some cases; these are not substitutes for the five runtime gameplay clips. The legacy Assassin scene includes two armature objects; its mesh modifier points to one. This does not describe the current textured Assassin's single 24-bone runtime rig.

Both legacy `assassin.blend` and `guardian.blend` differ from the hashes in `MOBA_BETA_MODEL_SOURCES.json`. The other four selected Blender sources match. The older report mentions only Guardian, so do not treat it as current proof for the legacy Assassin. No source manifest was rewritten. Before producing new runtime exports from these two legacy files, reconcile provenance. For texturing, preserve the current audited runtime meshes as masters.

Full node transforms, joint names and parent indices, skin matrices, primitive attributes, topology, UV and weight hashes, material definitions, image dimensions and clip bindings are recorded in `artifacts/character_texturing/baseline.json`. Source bone parent names, mesh UV layers, dimensions, modifiers, material nodes and image paths are in `blender_source_audit.json`. Appended Blender datablock names can have suffixes assigned during inspection; runtime joint names in the baseline are authoritative.

## References and material direction

The original concept PNGs were visually inspected and preserved. No extracted crops or generated replacement references have been created yet. Before uploading, crop clean full-body views into a separate working reference directory; retain every original. Avoid including labels, palette swatches, enlarged portraits or detached weapons in the main style image.

| Character | Available reference views | Texture direction | Geometry / effect limitations to retain and document |
| --- | --- | --- | --- |
| Assassin | Front, side, back, front/back three-quarter; costume breakdown | Crimson cloth, charcoal hood and mask, dark brown leather, dark steel with bronze/gold edging, blond hair | Current asset has pale face covering, dark long back cape instead of red emblem cloth, and no visible dual daggers in reviewed poses. Shape differences and missing accessories cannot be fixed by maps. No weapon added. |
| Gentle Guardian | Front, side, back; fabric, jewelry, satchel and familiar details | Rose pink and cream fabric, gold borders/jewelry, brown leather, pink crystals, dark hair | Cat familiar and shield/crystal effects require separate existing assets or explicitly approved geometry/effects work; do not paint a cat into clothing. Confirm fine ornament silhouettes against mesh before claiming a match. |
| Time Weaver | Front, side, back; staff, relic and tattoos | Navy/blue cloth, ivory geometric time motifs, brown leather, weathered silver/bronze metal | Staff/orb geometry and time effects cannot be created with albedo alone. Preserve existing proportions even where concept silhouette differs. |
| Nigel | Front, both sides, back; clean A-pose front/back, axe closeups | Olive skin, scars, dark hair, brown leather/fur, worn dark steel, muted crimson cloth | Axe, tusks and fur silhouettes need existing geometry. Preserve current height (source about 1.7 m); concept annotates 2.3 m, which is not authorization to rescale. |
| Colossus | Front, side, back; shrine, shield and sword details | Soot-black steel, aged gold, stone/ivory tabard, weathering, cold teal accents | Architectural silhouettes, candles, chains and soul mist cannot all be represented by texture. Existing source height about 2.69 m stays unchanged. Meshy 7 PBR does not produce an emission map. |
| Spirit Guardian | Large front three-quarter hero view, side, back, back with gear; portrait and weapons | Ochre markings, tan hide, gray fur, dark hair, ivory bone, weathered brown leather, restrained teal accents | No isolated orthographic front on the inspected coastal sheet. Bow, quiver, spear and spirit effects need existing geometry/effects. Preserve actual current accessories. |

All styling should follow the existing painterly dark-fantasy sheets. New texture generation does not authorize realistic skin pores, new garment designs, silhouette changes or geometry reconstruction. The above limitations include items to check closely before final visual sign-off; they are not claims of an exhaustive geometry discrepancy review for the five unprocessed characters.

## Actual animation integration

All six are instanced directly by `Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn`. Assassin fixtures reference `AssassinTextured.glb`; `Assassin.glb` is retained as a legacy asset. Runtime GLBs have no embedded clips. The controller loads `Assets/Animations/Mixamo/<model name>.tres`, finds that model's Skeleton3D, and installs/reuses an AnimationPlayer rooted at the skeleton. No AnimationTree is used by this controller.

Each library has Idle (9.933 s), Walk (1.033 s), Attack (2.267 s), HitReaction (0.967 s), and Death (2.567 s). All track bone names resolve against their own runtime skeleton. The textured Assassin uses unprefixed names and its existing reverse-numbered spine hierarchy; the others have Mixamo-style names. Full per-model names and parent indices are recorded rather than assuming rig equivalence. No new retargeting or rebaking is needed for material-only updates.

Original five FBXs are in `SourceAssets/characters/_base/animations/`. Their SHA-256 hashes and timing are recorded in `Assets/Animations/Mixamo/manifest.json`. The source Running action is not a sixth implemented gameplay state. Attack is the already implemented shared melee presentation, even for ranged characters; this stage does not change sequencing.

## Recommended pipeline and acceptance gates

1. Retain the currently textured Assassin without another paid job. Its existing 4K maps work; do not downsample merely to match the proposed 2K target for new work. A paid appearance-correction pass would be a separate optional 10-credit proposal.
2. Keep the checksum-verified runtime asset and AnimationLibrary backup. Use a working copy of the current runtime GLB as Meshy input. Do not re-export an unrelated historical source or send a regeneration/remesh/rigging job.
3. After API setup and approval, process Gentle Guardian first, then Time Weaver, Nigel, Colossus and Spirit Guardian, one at a time. Use clean reference views; single image input for the proposed initial request, or validated front/side/back multiview with explicit Meshy 7 when crops are prepared.
4. Request `ai_model: meshy-7`, `enable_original_uv: true`, `enable_pbr: true`, `texture_resolution: 2k`, `target_formats: [glb]`. There is no paid request in the preparation scripts. Style images take precedence over text in the API; do not assume adding a text prompt will constrain image-based generation.
5. Download to staging. Compare geometry/topology/UV correspondence with the uploaded working copy. Document actual output map sizes; the documented resolution parameter describes base color, so inspect the other returned maps too. Never accept rig/weight preservation solely because a request succeeded.
6. Apply returned maps to the ORIGINAL rigged mesh. If UVs correspond, use its original UVMap. If they differ, perform a separately validated bake from a temporary output mesh to the unchanged original. Stop if reliable correspondence is impossible. Never replace the original rigged mesh with a returned mesh.
7. Principled BSDF: albedo uses sRGB; normal, metallic and roughness use Non-Color. Connect normal through a tangent-space Normal Map node. For glTF packed metal/roughness, G is roughness and B is metallic. Inspect seams, stretch and all five clips. Preserve existing armature, rest transforms, vertex groups, bone hierarchy, object scale and attachments.
8. Prefer changing material/image payloads in the original GLB while retaining geometry, nodes and skin data. A normal Blender GLB re-export may reorder/split vertices; accept it only if validated correspondence and preservation checks succeed. Keep existing resource paths and AnimationLibraries. Do not rebake animations to accommodate a changed exporter hierarchy.
9. Run the baseline comparison, Blender animation review, Godot import and playback, battlefield smoke, build and xUnit suite. Review the new materials under actual battlefield lighting before promoting each character and starting the next. Shader-independent checks alone do not establish visual acceptance.

## Meshy API requirements and credit proposal

Reviewed official [Retexture API documentation](https://docs.meshy.ai/en/api/retexture) and [API pricing](https://docs.meshy.ai/en/api/pricing).

The retexture endpoint accepts GLB, glTF, OBJ, FBX and STL. Public model URLs allow up to 400 MB; data URIs up to 100 MB. Style images support JPG/JPEG/PNG/WebP, up to 20 MB each. Bearer authentication and funded API access are required. The API documents preservation of existing UVs when enabled; it does not establish an end-to-end guarantee for original rig, weights, vertex ordering or animation compatibility. That guarantee must come from our own comparison and applying maps to the original asset. Meshy N1 lacks the requested PBR output, so it is not the proposed model.

| Proposed work | Credits | Submission policy |
| --- | ---: | --- |
| Assassin existing-map validation | 0 | Completed checks; no new Meshy job recommended |
| Gentle Guardian pilot retexture | 10 | First paid request only after key setup and user approval |
| Time Weaver | 10 | After prior character passes |
| Nigel | 10 | After prior character passes |
| Gravebound Colossus | 10 | After prior character passes |
| Beothuk Spirit Guardian | 10 | After prior character passes |
| Recommended total | 50 | Five sequential jobs, no automatic retries |
| Optional Assassin retexture | +10 | Separate approval; total would become 60 |

No unwrap, remesh, rigging, animation, conversion or resize jobs are proposed. Retry generations would add credits and require approval beyond the agreed cap. Dollar cost is not estimated because the user's API credit purchase terms are unknown. Machine-readable proposals: `artifacts/character_texturing/meshy_job_proposals.json`; placeholders make them deliberately unsubmitted request templates.

Meshy access is currently unverified. Blender can apply or bake existing textures and can author materials, but it cannot automatically recover the missing concept-specific albedo from a gray material. No replacement procedural/AI workflow has been substituted. If API access stays unavailable, present a specific Blender-only proposal for approval before authoring new textures.

## Validation performed in this audit

- Runtime baseline audit: six current models pass mesh/skin/UV/weight presence and all five bone-bound clips. A second comparison confirms protected geometry, UVs, weights, nodes, rest matrices and AnimationLibrary hashes remain unchanged.
- `Tools/VerifyMixamoExports.py`: seven retained exports including legacy Assassin pass original binary, material, mesh, skin and node preservation; all five clips retained; Walk stays in place; original FBX checksums pass.
- Blender source inspection: six selected legacy sources inspected without saving them. Hash mismatches above recorded.
- Existing textured Assassin: imported its existing animated review GLB into an isolated Blender scene. All five actions have distinct sampled poses, finite matrices and unchanged geometry/UV/weight/bone fingerprints. Clip durations match the existing manifest. Material angle views and Idle/Attack frame samples inspected. No new texture seam or obvious tearing observed in those samples; full-cycle cloth contacts and close-up UV-stretch review are not exhaustively signed off.
- Packed source saved to `SourceAssets/characters/hooded_assassin/texturing_validation/AssassinTextured_Validated.blend`. This is a review source derived from the current preserved runtime asset and its existing baked clips, not an updated legacy 28-bone Assassin source. Images are packed, five actions retained, original files untouched.
- Godot 4.7.2 .NET headless integration: six models, five states, advancing skeletal poses, action completion and unchanged model transforms pass. Log: `artifacts/character_texturing/godot_animation_verify.log`.
- Existing battlefield headless smoke passes ten fixtures, movement/combat, castles, scoring/timers, abilities, selection/picking, HUD and reset. Log: `artifacts/character_texturing/godot_battlefield_smoke.log`. Headless runs do not establish new rendered material approval under lighting.
- `dotnet build --no-restore`: pass, zero warnings/errors. `dotnet test Tests/PawnsOfDivinity.Tests/PawnsOfDivinity.Tests.csproj --no-build --no-restore`: 3,278 passed, zero failed/skipped. Restricted runs had a build failure without diagnostics, Godot .NET/log initialization failure, and xUnit testhost timeout; permitted reruns outside the sandbox passed.

Backups: `artifacts/character_texturing/backups/audit_baseline_001/`, 29 checksum-verified files including six runtime models, corresponding AnimationLibraries, external runtime normal/PBR maps, scene reference, provenance manifests, and original current Assassin FBX/PBR maps. Editable legacy Blender files have not been modified. The backup manifest contains original relative paths and SHA-256 hashes.

## Deliverables and remaining work

Delivered now: reusable read-only validator (`Tools/AuditCharacterTexturing.py`), local backup/proposal preparer (`Tools/PrepareCharacterTexturingReview.py`), audit JSONs, five-job credit proposal plus optional Assassin record, packed Assassin review source, existing Assassin PBR/model references and fresh regression verification.

Pending approval/configuration: new PBR maps and finished Blender/Godot deliverables for the five gray characters; any optional Assassin concept correction. No before/after texture improvement is claimed because no textures were changed. `artifacts/animation_*.png` are pre-existing visual evidence, not freshly captured results from this audit. Fresh Blender material screenshots were inspected through MCP. Capture matched before/after views and battlefield-lighting renders during each approved texturing pass.
