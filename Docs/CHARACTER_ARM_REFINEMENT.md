# Character arm animation refinement — 2026-11-15

Corrected arm poses for all six textured characters in Idle, Walk, Attack, HitReaction and the start of Death. Nigel and Time Weaver no longer fold their forearms/hands into the torso in Idle. The other four characters received the corresponding per-rig corrections for more natural elbow and hand placement.

![Corrected roster](../artifacts/arm_pose_refinement/corrected_roster.png)

The cause was a bind-pose mismatch: the shared Mixamo source has horizontal T-pose arm directions, while several fitted target rigs have downward/slanted arm directions. Applying the source's global rest-relative rotation directly to those targets lowered the arms a second time. The correction aligns the target limb direction with the source bind direction before applying the original motion. Fitted clavicles stay unchanged, avoiding a new shoulder-position distortion.

Only upper-arm, forearm and available hand rotation keys changed. The 24-bone Assassin has four affected tracks per clip; the five 28-bone characters have six each. Existing meshes, textures, UVs, skin weights, bone names/parents/rest transforms, model scales and all six runtime GLB files remain unchanged. Spine, shoulder, leg and hip tracks, animation duration, key times, looping, gameplay rules and animation sequencing are preserved.

Death begins with corrected arms and smoothly blends back to the original grounded arm pose as the body settles. The final Death pose is unchanged. No animation generation service or paid job was used.

## Updated assets

- Runtime animation libraries: `Assets/Animations/Mixamo/{AssassinTextured,GentleGuardian,TimeWeaver,Nigel,Colossus,SpiritGuardian}.tres`.
- Packed Blender sources and copied libraries: `SourceAssets/characters/textured_roster/<name>/<name>.blend` and `Animations.tres`.
- Animated GLB reviews: `artifacts/animations/<name>.glb` and `artifacts/character_texturing/validated_reviews/<name>.glb`.
- Delivery manifest: `SourceAssets/characters/textured_roster/manifest.json`, updated with the new animation-library hashes. Runtime model hashes are unchanged.
- Original libraries, animated reviews and delivered Blender sources: `artifacts/arm_pose_refinement/before/<name>/`.

`Tools/BakeMixamoAnimations.py` now performs this limb-direction calibration when baking from the original FBXs, so future bakes retain the correction. `RefineCharacterArmAnimations.py` applies the same correction to the existing baked clips without rebuilding unrelated motion. Repeated refinement derives from the saved originals, preventing accumulated rotations.

## Verification

`python Tools/VerifyCharacterArmRefinement.py --deployed` passes all six characters. It verifies that only the approved arm rotation payloads changed, all other library tracks and clip metadata remain identical, review GLB structure is unchanged, key times match, quaternions remain normalized/continuous, the final Death arm rotations are equivalent, and the runtime textured model hashes are unchanged.

`python Tools/VerifyMixamoExports.py` passes all seven retained model/review pairs, including legacy Assassin. Model/skin/material/node/binary preservation and the five existing clip names pass; Walk remains in place.

Blender reviewed five samples per clip for finite bone transforms; front and three-quarter views covered the corrected Idle poses, with additional multi-frame Nigel Attack review. The six delivered `.blend` files contain the corrected existing actions and packed PBR maps.

Godot 4.7.2 .NET rendered all six characters through Idle, Walk, Attack, HitReaction and Death and passed skeletal motion, action completion, terminal Death and unchanged model transform checks. Matched before/after Idle captures use the same textured model, lighting, camera and animation time, changing only the animation library.

The existing battlefield smoke test passed movement/combat, abilities, castles, scoring/timers, visibility, targeting/picking, HUD and reset. The C# build passed with zero warnings/errors. The full xUnit suite passed **3,278 tests, zero failures/skips**. No gameplay or test code was changed.

Evidence:

- [Matched arm-pose comparison](../artifacts/arm_pose_refinement/before_after.png).
- [Corrected roster](../artifacts/arm_pose_refinement/corrected_roster.png).
- Per-character captures: `artifacts/arm_pose_refinement/comparison_renders/`.
- Reports and staged originals/corrections: `artifacts/arm_pose_refinement/staged/<name>/refinement.json` and `blender_review.json`.
- Godot logs: `artifacts/arm_pose_refinement/godot_animation_verify.log` and `battlefield_smoke.log`.
- Updated five-state visual galleries: `artifacts/animation_*.png`.

This corrects the visible arm-alignment problem. The shared attack choreography, original finger geometry, gait speed/foot sliding, and occasional costume contact are unchanged; it is not a new animation set or an exhaustive cloth-collision cleanup.
