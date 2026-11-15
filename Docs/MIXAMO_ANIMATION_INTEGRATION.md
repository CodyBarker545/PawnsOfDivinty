# Mixamo animation integration

The six existing MobaBeta models now use a reusable C# controller and five real Mixamo clips. The battlefield's ten authored fixtures (including team duplicates) receive the controller at runtime. Existing model resources and authored scene transforms remain in place. No domain combat, movement, stats, castle, timer or victory rules were changed by this integration.

**Textured Assassin update:** Both Assassin fixtures now reference `Assets/Characters/MobaBeta/AssassinTextured.glb`, exported from the newly downloaded FBX under `working_textured_model_assassin`. Its base color, normal, metallic and roughness textures are embedded. The previous Assassin GLB remains available. The new model has a 24-bone unprefixed rig, including a reverse-numbered spine chain, so it uses its own `AssassinTextured.tres` library and an explicit hierarchy-based bone map in the existing bake script. All five original gameplay animation states remain connected through the same controller.

To reproduce this update, run `Tools/ExportTexturedAssassin.py` in Blender, then `Tools/BakeMixamoAnimations.py -- --character AssassinTextured`. The export normalizes the working rig's centimetre scale and object rotation, resets its bundled Running pose, and retains the downloaded mesh, UVs and material. It never saves changes to the source FBX or textures. Provenance is recorded in `Docs/TEXTURED_ASSASSIN_SOURCE.json`. The source's separate Running action is not substituted for the five chosen gameplay clips.

The textured model's walk cycle was reviewed through Blender MCP with materials visible. Godot rendered all five states and passed skeletal playback/action completion checks; its existing battlefield smoke suite and all 3,278 xUnit tests also passed. The build completed with zero warnings/errors. Original and animated-export data integrity checks passed. The editor import logged a Terrain3D extension DLL copy error, but imported the Assassin successfully; subsequent game and verification scene runs completed without that error. The baseline rig table and original source-checksum notes below describe the earlier six-model integration; the textured Assassin supersedes its Assassin entry only.

## Discovered clips

Original files remain under `SourceAssets/characters/_base/animations/`. Each FBX contains a 99-bone rig and one action originally named `Armature|mixamo.com|Layer0`. Blender may suffix action names when multiple files are loaded together.

| File | Shared state | Duration at 30 fps | Playback |
| --- | --- | --- | --- |
| Breathing Idle.fbx | Idle | 9.933 s | Loop |
| Walking.fbx | Walk | 1.033 s | Loop, in place |
| Standing Melee Attack Downward.fbx | Attack | 2.267 s | One shot |
| Standing React Small From Headshot.fbx | HitReaction | 0.967 s | One shot |
| Standing Death Backward 01.fbx | Death | 2.567 s | One shot, final pose held |

There is no Run clip. The downward melee clip provides the shared attack presentation, including existing ranged attacks; it does not imply a new weapon or combat mechanic.

## Character and rig results

All six models live under `Assets/Characters/MobaBeta/` and are instanced directly in `Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn`. They have 28 bones and no pre-existing AnimationPlayer, AnimationTree or clips. Comparison found 27 names in common with each FBX, but different rest rotations and proportions. Source finger, face and clothing chains cannot simply be copied to these reduced rigs.

The bake transfers global rest-relative rotations and derives local rotations using each target's actual parent hierarchy. It drives 22 body bones and leaves unmatched/helper/end bones at their existing rests. Hip height uses the target/source proportion ratio. No bone is renamed, reparented or added.

Godot's SkeletonProfileHumanoid/BoneMap could map the common humanoid bones, but using it is unnecessary here: the Blender bake already accounts for rest-axis differences without changing the imported rig. This follows the distinction between bone rest and animation transforms described in [Godot's retargeting documentation](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/retargeting_3d_skeletons.html).

| Character | Original runtime model | Result |
| --- | --- | --- |
| Hooded Assassin | Assassin.glb | First validated through Blender MCP; five clips, multi-frame pose review, Godot import and playback passed |
| Gentle Guardian | GentleGuardian.glb | Five clips; Godot playback and rendered samples passed |
| Time Weaver | TimeWeaver.glb | Five clips; Godot playback and rendered samples passed |
| Nigel | Nigel.glb | Five clips; Godot playback and rendered samples passed |
| Gravebound Colossus | Colossus.glb | Five clips; proportion-specific bake, additional Blender attack review and Godot rendered samples passed |
| Beothuk Spirit Guardian | SpiritGuardian.glb | Five clips; Godot playback and rendered samples passed |

No manual rigging blocker was found. Playback remains at the clips' original speed for all six; tactical movement retains its existing visual speed and logical range. Walk feet can slide because the single downloaded gait is not speed-matched to every body size. Clothing/armor self-intersection and frame-perfect foot planting still need an artist's full-cycle review. Sampled views are not a claim of exhaustive visual approval at every frame.

## Reproducible pipeline

`Tools/BakeMixamoAnimations.py` runs in background Blender or through Blender MCP. It creates isolated working scenes and never saves the artist's source `.blend` files.

```powershell
& D:/Blender/blender.exe --background --factory-startup --python Tools/BakeMixamoAnimations.py -- --character Assassin
# After validating Assassin, omit --character Assassin to bake all six.
python Tools/VerifyMixamoExports.py
```

The pipeline produces animated GLB review exports in `artifacts/animations/` and six runtime AnimationLibrary resources in `Assets/Animations/Mixamo/`. Both outputs use the same sampled motion. The GLBs retain the existing mesh, materials, node hierarchy, skin, accessors and original binary bytes; only animation data is appended. Runtime libraries attach those clips to the original models, avoiding duplicate geometry/texture imports or replacing character scene references. `artifacts` is already excluded from Godot's asset scanner. The source FBXs are not copied into a second import location.

`manifest.json` beside the libraries records source SHA-256 hashes, frames, duration and state mapping. `VerifyMixamoExports.py` checks those hashes, original GLB data preservation, clip names, bone-only animation targets, and constant horizontal hip coordinates in Walk.

Walking removes all horizontal hip motion. Stationary clips retain their intentional local body shifts; no clip animates a model/fixture position, rotation or scale. The original gameplay movement code still places the fixture on the grid.

The first Assassin death bake visibly sank into the floor. The final pipeline calculates exact skinned vertex heights from each target's unchanged vertices, weights and inverse bind matrices, and adjusts only Death hip height. A 0.01 model-unit clearance covers interpolation. Maximum corrections before clearance were Assassin 0.150, Guardian 0.141, Time Weaver 0.419, Nigel 0.393, Colossus 0.294 and Spirit Guardian 0.165. This accounts for different silhouettes as well as body proportions. Assassin samples were reimported and measured in Blender after the correction.

## Code and node responsibilities

- `CharacterAnimationState.cs`: pure C# enum and state machine. `SetMoving` selects Walk/Idle without replaying an unchanged state. `Request` queues action clips and makes Death terminal. `Complete` handles only the current one-shot, then plays queued actions or the latest locomotion state. `Reset` clears the queue and restores Idle. Missing optional clips fall back without throwing.
- `CharacterAnimationController.cs`: Godot adapter. `Initialize` finds the model's own skeleton, validates every library bone binding and key count, stops conflicting imported playback through the fixture's existing setup, and configures an AnimationPlayer. `Play` blends only on state changes. `Finished` advances the state machine from AnimationFinished. The public movement/action/reset methods contain no gameplay calculations.
- Runtime `CharacterAnimationController` node: child of the existing fixture, owning presentation state.
- Runtime `CharacterAnimationPlayer` node: child of the existing Skeleton3D, with RootNode bound to that exact skeleton. Existing players can be reused; these six static exports need a new player. The skeleton and player are explicitly enabled because the existing fixture freezes imported model processing.
- `StaticTacticalCharacter.cs`: initializes animation, starts/stops Walk around the existing visual tile queue, turns the existing Facing node toward travel/attack targets, and detects actual HP/death changes during refresh. Binding a fresh runtime character resets animation and authored facing. Defeated units retain the existing gray material and unpickable behavior while Death plays and holds; no new removal/respawn system is added.
- `MobaCombatHud.cs`: forwards the existing resolved opening/counter/follow-up journal events to `PresentAttack`, including misses. HP reductions trigger HitReaction through the fixture. Death takes priority. Repeated actions queue per character; this is not a new damage-synchronized cinematic combat scheduler.
- `CharacterAnimationVerification.cs` and its `.tscn`: standalone Godot integration harness. Loads the six original runtime models, verifies five states and advancing skeletal poses, checks action completion and terminal Death, asserts unchanged model transforms, and optionally captures rendered samples. Camera, light, floor and labels exist only in this verification scene.
- `CharacterAnimationStateTests.cs`: five xUnit cases covering locomotion, no restarts, queued/repeated actions, completion, optional/missing clips, terminal Death and reset.

## Verification performed

- Blender MCP connected to Blender 5.2.2 LTS. Successfully used `get_addon_status`, `get_scene_info`, `execute_blender_code` and `look`; inspected FBXs, target rigs, actions/NLA tracks, animated GLB imports and sampled poses. Ran the reusable Assassin bake and later the six-character batch through MCP.
- The installed MCP add-on reports protocol 14 versus server expectation 16. The required Python execution, inspection and screenshot capabilities worked through available fallbacks. No software was installed and no global settings were changed.
- The computer-use skill's `@oai/sky` interface was available and used to discover/inspect Blender windows. Visual animation evidence came from Blender MCP frame captures and Godot viewport images, rather than a claim of unattended GUI playback inspection.
- Godot 4.7.2 .NET editor import completed. Animated Assassin GLB imported via GLTFDocument with exactly Attack, Death, HitReaction, Idle and Walk.
- Godot six-model integration verification passed after correcting an initial serialization error that produced zero rotation keys. Final libraries use Godot's verified packed transform-key format.
- Rendered and visually inspected Idle, Walk, Attack, HitReaction and near-final Death samples for all six models. Additional multi-frame Blender review covered all Assassin states and the Colossus attack.
- Existing battlefield smoke test passed: ten original fixtures, movement, combat, castle travel, VP/victory/timers, Assassin abilities, smoke/visibility, selection/picking, HUD, free testing and reset.
- `dotnet build --no-restore`: passed, zero warnings/errors. One sandbox compiler run was canceled; the final rerun passed.
- `dotnet test Tests/PawnsOfDivinity.Tests/PawnsOfDivinity.Tests.csproj --no-build --no-restore`: **3,278 passed, 0 failed, 0 skipped** (includes five new animation tests).
- Export integrity script passed for all six models. Every original runtime GLB still matches the existing model-source manifest checksum. Five original `.blend` files also match that older manifest; `guardian.blend` differs from its older recorded hash. This workflow never saved that source file and uses its unchanged runtime GLB, so the stale source-manifest entry was not overwritten.

Completed and verified: reusable integration, all six rigs, automated behavior, sample renders, imports and regression checks. Implemented but not exhaustively visually verified: continuous full-speed battlefield transitions, all clothing/armor contacts and every gait contact frame. Blocked: none.

## Try it in Godot

1. Build the .NET project and open `Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn`.
2. Run the scene. Standing units should breathe. Select a unit and click a legal movement destination: Walk runs during visual travel, facing follows each segment, then Idle resumes.
3. Use the existing Attack/Confirm Attack controls against a legal target. Attacks, counters and follow-ups request Attack; actual HP loss requests HitReaction. A lethal result plays Death, holds the final pose and retains the existing defeated-unit behavior. Use Reset Match to restore living Idle poses and original facing.
4. In normal match mode, respect the existing team/activation restrictions. Free Testing already permits either team; the integration adds no new combat or demonstration mechanics.
5. Run `Scenes/Tests/CharacterAnimationVerification.tscn` for an automatic six-character check. A graphical run writes `artifacts/animation_Idle.png`, `animation_Walk.png`, `animation_Attack.png`, `animation_HitReaction.png` and `animation_Death.png`, then exits with a PASS message. A headless run checks playback without claiming rendered output.

Changed existing files: `StaticTacticalCharacter.cs`, `MobaCombatHud.cs`. New files: three `CharacterAnimation*.cs` classes plus Godot UIDs, the verification scene, the state tests, two pipeline scripts, six `Assets/Animations/Mixamo/*.tres` libraries, local manifest/review exports, and this report. Other pre-existing working-tree changes were preserved.
