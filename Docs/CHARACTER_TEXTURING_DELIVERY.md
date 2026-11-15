# Character texturing delivery — 2026-11-15

All six existing characters now have textured, rigged Godot assets, packed Blender sources, and separate PBR maps. The Assassin's existing 4K textures were retained and validated. The remaining five received new 2K concept-guided PBR textures through five sequential Meshy Retexture jobs. Exactly 50 credits were used; the verified balance changed from 2,043 to 1,993. No retry, regeneration, remesh, rigging, or animation-generation jobs were submitted.

The user's “finish this now” instruction followed the reviewed five-job/50-credit proposal and was used as authorization to execute that proposal. The API key stayed in Blender add-on preferences and was never written into scripts, reports, model scenes, or job journals.

![Finished roster](../artifacts/character_texturing/finished_roster.png)

## Delivered files

Project root: `D:/SoftwareEng/Game/`.

Each character folder under `SourceAssets/characters/textured_roster/` contains `<name>.blend`, `<name>.glb`, `Animations.tres`, and a `textures/` directory with `base_color.png`, `normal.png`, `roughness.png`, and `metallic.png`. The five newly textured characters also include `metallic_roughness.png`, `integration.json`, and `blender_validation.json`. Blender files have their images packed and retain the five existing animation actions. The manifest records full paths and hashes.

| Character | Delivery folder | Current Godot model | Maps | Material changes |
| --- | --- | --- | --- | --- |
| Hooded Assassin | `SourceAssets/characters/textured_roster/AssassinTextured/` | `Assets/Characters/MobaBeta/AssassinTextured.glb` | Existing 4K PBR | Existing crimson cloth, dark armor, bronze edging and leather retained; no paid retexture |
| Gentle Guardian | `SourceAssets/characters/textured_roster/GentleGuardian/` | `Assets/Characters/MobaBeta/GentleGuardian.glb` | New 2K PBR | Rose-pink and cream cloth, gold patterns/jewelry, dark hair, leather bags |
| Time Weaver | `SourceAssets/characters/textured_roster/TimeWeaver/` | `Assets/Characters/MobaBeta/TimeWeaver.glb` | New 2K PBR | Navy cloth, ivory time motifs, brown leather, weathered metal and blue relic |
| Nigel | `SourceAssets/characters/textured_roster/Nigel/` | `Assets/Characters/MobaBeta/Nigel.glb` | New 2K PBR | Olive skin, scars, dark hair, fur, brown leather, muted red cloth and metal details |
| Gravebound Colossus | `SourceAssets/characters/textured_roster/Colossus/` | `Assets/Characters/MobaBeta/Colossus.glb` | New 2K PBR | Dark shrine armor, aged gold/bronze trim, ivory tabard and funerary symbols |
| Beothuk Spirit Guardian | `SourceAssets/characters/textured_roster/SpiritGuardian/` | `Assets/Characters/MobaBeta/SpiritGuardian.glb` | New 2K PBR | Ochre facial markings, gray fur, hide/leather, bone details, red-brown wraps and flowing cloth patterns |

The runtime GLBs preserve the project's existing arrangement: rigged geometry in the GLB and five gameplay clips in `Assets/Animations/Mixamo/<name>.tres`. They do not add embedded clips or a new runtime animation system. Animated GLB review copies with the same existing clips are available in `artifacts/animations/` and `artifacts/character_texturing/validated_reviews/`; the Assassin review remains in `artifacts/animations/AssassinTextured.glb`.

Primary manifest: [all six deliveries](../SourceAssets/characters/textured_roster/manifest.json).

## Preservation and transfer

No existing rigged mesh was replaced by a Meshy output. Meshy returned a normalized, unrigged model and removed duplicate surface triangles. The transfer validated every original surface triangle against the returned surface, accounting for normalization only in the correspondence calculations. Original vertex positions, topology, normal/tangent attributes, UVs, skin indices/weights, inverse bind matrices, nodes and hierarchy stayed unchanged in the runtime GLBs.

Most UVs correspond directly. Meshy omitted some duplicate UV aliases; the transfer baked texture values from their coincident returned triangles into the original UV regions. Normal samples in those regions were transformed between the corresponding tangent bases. This repaired maps without changing the target UV atlas or adding/removing geometry.

| Character | Original triangles retained | Original vertices retained | Bones retained | UV-alias triangles baked |
| --- | ---: | ---: | ---: | ---: |
| Assassin | 10,346 | 18,622 | 24 | Not applicable; existing maps retained |
| Gentle Guardian | 196,840 | 155,049 | 28 | 108 |
| Time Weaver | 191,325 | 135,409 | 28 | 183 |
| Nigel | 197,516 | 154,444 | 28 | 104 |
| Colossus | 195,629 | 169,154 | 28 | 102 |
| Spirit Guardian | 196,681 | 156,176 | 28 | 112 |

The runtime GLBs were updated by appending image payloads and changing material/image definitions, retaining all original geometry and skin bytes. Updated review GLBs retain the same animation data. Original AnimationLibrary hashes match the audit baseline for all six characters. Battlefield scene references and fixture transforms are unchanged, including the pre-existing character scales.

Principled materials use sRGB base color and Non-Color normal/metallic/roughness data. Normals feed a tangent-space Normal Map node. Godot uses the standard glTF metallic/roughness packing (G roughness, B metallic). New materials were inspected in Blender from front/back/three-quarter angles and rendered in Godot, including the actual battlefield lighting.

## Animation and gameplay verification

All six retain Idle, Walk, Attack, HitReaction and Death, with original durations of approximately 9.933, 1.033, 2.267, 0.967 and 2.567 seconds. No animations were regenerated or retargeted in this stage. No controller, combat calculation, movement rule, gameplay test, or animation-sequencing code was changed.

Blender checks verified unchanged geometry/UV/weight/bone fingerprints across sampled playback, finite bone matrices, and changing poses in each of the five existing clips. Packed sources contain those existing actions. These sampled checks do not claim an exhaustive full-cycle cloth-collision or foot-contact cleanup.

Godot 4.7.2 .NET imported each model and passed per-character animation/battlefield checks before advancing to the next character. The final six-character verification rendered all five states and passed skeletal motion, clip selection, action completion, terminal Death behavior and unchanged model transforms. The final battlefield smoke passed ten authored fixtures, movement/combat, abilities, castle travel, scoring/timers, visibility, targeting/picking, HUD and reset.

Final regression results:

- `dotnet build --no-restore`: passed, zero warnings/errors.
- `dotnet test Tests/PawnsOfDivinity.Tests/PawnsOfDivinity.Tests.csproj --no-build --no-restore`: **3,278 passed, zero failed, zero skipped**.
- `python Tools/AuditCharacterTexturing.py --compare artifacts/character_texturing/baseline.json`: all six protected fingerprints and AnimationLibrary hashes pass.
- `python Tools/VerifyMixamoExports.py`: all seven retained runtime/review pairs, including legacy Assassin, pass data preservation and five-clip checks.
- Original backup checksums pass; all six legacy Blender sources still match their hashes captured at the start of this stage. The previously documented legacy Assassin/Guardian differences from the older source manifest were not overwritten or concealed.

Godot editor imports completed successfully but emitted dummy-renderer/RID/resource cleanup warnings at editor exit. Graphical verification and battlefield runs completed successfully. These editor shutdown warnings are recorded in the import logs rather than represented as a character import failure.

## Visual comparisons and evidence

- [Matched before-and-after sheet](../artifacts/character_texturing/before_after.png): freshly rendered originals and final models with the same camera, pose, lighting and resolution. Assassin is intentionally unchanged.
- [Finished roster](../artifacts/character_texturing/finished_roster.png).
- Individual matched renders: `artifacts/character_texturing/comparison_renders/`.
- Actual battlefield views: `artifacts/character_texturing/battlefield_<name>.png`; camera and HUD visibility changes exist only in the capture process, not saved in the battlefield scene.
- Five final animated galleries: `artifacts/animation_Idle.png`, `animation_Walk.png`, `animation_Attack.png`, `animation_HitReaction.png`, `animation_Death.png`.
- Final logs: `artifacts/character_texturing/verify_final_roster.log`, `battlefield_final_roster.log`, `comparison_final_roster.log`, `battlefield_material_capture.log`.
- Job IDs and submission/results: `artifacts/character_texturing/jobs/<name>/job.json`; aggregate `completed_jobs.json`; verified balance evidence `credit_usage.json`.
- Preservation baseline and final comparison: `artifacts/character_texturing/baseline.json` and `comparison.json`.

## Retained concept and animation discrepancies

These are limitations of the existing geometry, original animation set, or the generated maps; they were retained to honor the preservation requirements.

- **Assassin:** pale face covering rather than the sheet's blond-hair/black-mask treatment; dark back cape rather than the long red emblem cloth; no visible concept dual daggers added. Existing maps were kept as requested for the completed Assassin.
- **Gentle Guardian:** filigree and jewelry patterns approximate the sheet rather than reproduce every motif. The cat familiar and protective crystal/shield effects are not part of this mesh and were not added.
- **Time Weaver:** the existing silhouette/proportions differ from the heavier concept build; back markings are an approximation from the front reference. No staff/orb accessory or time effects added.
- **Nigel:** the existing Idle pose tucks the arms behind/into the torso, making them difficult to see in the front view. The matched original-versus-textured renders show the same pose; Attack samples show the existing arms moving. This pre-existing animation limitation was not repaired by changing the rig or clips. The concept axe was not added, and the existing model/fixture scale was kept despite the concept's 2.3 m annotation.
- **Colossus:** shrine details, engraving and tabard symbol are approximate. The existing mesh lacks the full concept sword/shield arrangement and soul mist. No emissive flame/shader/VFX system was introduced; candle appearance is texture/material only.
- **Spirit Guardian:** markings, fur colors and hide motifs follow the sheet but do not reproduce every decoration. The concept bow/spear/quiver arrangement and spirit effects were not added where absent from the current mesh.
- Shared pre-existing clip limitations remain: one generic downward melee attack is used by the current presentation, some Idle/other poses have clothing/body intersections, and the common Walk may slide at different movement speeds. The stage changes materials, not animation design or gameplay sequencing.

## Backups and reusable tools

Backups: `artifacts/character_texturing/backups/audit_baseline_001/`. It contains the six original runtime GLBs, original libraries/textures, battlefield scene reference, provenance manifests, current original Assassin FBX/maps, and the five pre-texturing animated review exports. `manifest.json` verifies the original 29 backed-up files; `backup_integrity.json` records that verification and the added review backups. Original concept sheets and all original editable Blender files remain in their original locations.

Reusable tools:

- `PrepareTexturingReferences.py`: crop reference views into separate files, preserving sheets.
- `CharacterRetextureJobs.py`: guarded single-job submission, journal, polling and downloads; API key stays in Blender memory. It refuses duplicate submissions and enforces the five-job cap.
- `IntegrateCharacterTextures.py`: surface/UV correspondence, duplicate-UV texture bake, original-GLB material graft and existing-animation review reconstruction.
- `ValidateTexturedCharacterBlender.py`: Principled/PBR inspection, geometry/skin preservation, existing clip samples and packed `.blend` writing.
- `DeployCharacterTextures.py`: promote validated materials to existing resource paths while preserving the original review backup.
- `AuditCharacterTexturing.py`, `VerifyMixamoExports.py`: preservation and animation checks.
- `CaptureCharacterTextureComparison.gd`, `CaptureTexturedBattlefield.gd`: matched original/final and real-lighting visual evidence without saved gameplay changes.
- `PackageCharacterTexturing.py`: collect all six deliveries, verify original sources/backups, and build comparison images.

Large local model/texture/source assets remain excluded by the repository's existing `.gitignore`; no Git commit or push was performed. The delivered assets are present in this workspace.
