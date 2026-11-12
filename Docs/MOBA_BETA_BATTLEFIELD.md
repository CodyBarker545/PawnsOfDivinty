# One-lane MOBA beta battlefield — Stages 6.7A through 6.7B.8A

**Current configuration: fixed 5v5, ten independent runtime characters, first to 15 VP,
no gameplay round limit.** B.6 implements castle capture/transfers and configurable DEVELOPMENT
VP/victory. B.7 adds a configurable development tactical activation timer (enabled, 30 seconds
by default), with untimed hotseat and DEV pause controls. B.8A connects the two Hooded
Assassin fixtures to production Smoke Bomb, Shadow Strike and Smoke Veil. The B.5
request supersedes earlier 4v4 decisions and the 3v3 development slice. Sections describing
A–B.7 retain historical milestone details; use the B.5 roster, B.6 objective rules, B.7 timer
controls and B.8A ability instructions below.

## Purpose and opening the scene

Open `Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn` and run the current scene (F6).
The existing `Scenes/Battlefield.tscn` remains the main scene and retains the Stage 6.6 encounter.
This beta supports hotseat selection, tactical movement and basic attacks through production
Character, ActivationManager, RoundLifecycleManager, MovementActionExecutor and AttackActionExecutor.
It does not start the 14×14 development BattlefieldSession or instantiate a second combat-character implementation.
Stage 6.7B.4 adds live HP bars, ordered resolved-combat feedback/history, defeat presentation
and an in-process development match reset, without changing production combat rules.
Stage 6.7B.5 authors five units per side on mirrored forward deployment tiles and adds
live team-readiness counts. All ten support selection, movement, combat, feedback and reset.
Stage 6.7B.6 adds runtime castle/objective control, approved castle transfers, end-round VP,
victory/command locks and complete ownership/score reset. Permanent scoring balance is unresolved.
Stage 6.7B.7 times the entire activation opportunity, including selection, while preserving
production movement, combat, transfers, scoring and activation completion.

The discovered foundation is `Scenes/Battlefields/prototype/prototype_battlefield.tscn`
(not `prototype_battlfield`). That source has an empty GridMap and a primitive ground plane;
it does not contain a camera or a populated terrain board. The new scene retains it as
`PrototypeFoundation`; an instance-only serialized visibility override hides that legacy primitive.
The continuous underlay and all tactical tiles are now serialized under `Terrain`.
The original prototype file is unchanged.

The new scene follows the existing `Scenes/Battlefields` directory convention. Its environment,
sun and Camera3D configuration were copied from Battlefield.tscn, with reduced light intensity
for greybox color readability. Existing camera equations and key/mouse controls were adapted
from Battlefield.cs. No changes were made to that already-modified production scene or script.

## Coordinate system and configuration

The beta scene's `BoardDefinition` resource is the single dimension configuration: width 30, height 20,
cell size 1.25, centered at world X/Z (16,16), matching the existing scale/center conventions.
Tactical X is world X; tactical Y is world Z; world Y is vertical height.
Bounds are X 0–29 and Y 0–19. All 600 tactical floor surfaces lie at world Y=0.

`RectangularBoard.CellCenter` independently centers each dimension:

```
world X = CenterX + (cell X - (Width  - 1) / 2) * CellSize
world Z = CenterZ + (cell Y - (Height - 1) / 2) * CellSize
```

`WorldToCell` uses floor and half-open tile bounds. The first center is (-2.125,4.125)
and the last is (34.125,27.875). The floor extents are X -2.75–34.75 and Z 3.5–28.5.
Cell meshes meet without gaps; the grid overlay lies just above them.
Width, height, picking, mirror calculations and camera bounds all use the extracted Board.
The authored region coordinates describe this particular map; changing its dimensions alone
does not rescale the authored layout. Validate a redesigned layout after changing dimensions.

The legacy `BattlefieldGrid`, `TacticalMap`, movement adapters and 14×14 specification remain
unchanged. This beta is not a revision to Specification v0.2.

## Regions

All ranges below are inclusive. Team IDs are Blue=1 and Red=2.

| Region | Footprint | Capture (gold) | Deployment (cyan) |
| --- | --- | --- | --- |
| blue_home | X 0–3, Y 9–14 | X 1–2, Y 10–11 | X 1–2, Y 12–13 |
| red_home | X 26–29, Y 9–14 | X 27–28, Y 10–11 | X 27–28, Y 12–13 |
| blue_forward | X 6–11, Y 8–14 | X 8–9, Y 10–11 | X 7–9, Y 12–13 |
| red_forward | X 18–23, Y 8–14 | X 20–21, Y 10–11 | X 20–22, Y 12–13 |

Homes and forts use grey stone floors and team-colored battlement caps. Homes have smaller
footprints; forward forts have a larger enclosure and extra pillars. All occupy flat combat
tiles. Home identity remains its initial team; no capture/destruction behavior is attached.

Forward castles have four two-cell entrances. Blue gaps are west X6/Y10–11, east X11/Y10–11,
north Y8/X8–9 and south Y14/X8–9. Red gaps are their exact horizontal mirrors,
X'=Board.Width-1-X. Red northern/southern openings are X20–21; west/east are X18 and X23.
Walls only appear on explicit blocked cells. Caps and pillars stay inside those cells.
There are no wall-top walkable surfaces, collision bodies or invisible blockers.
Home side openings are four cells wide (Y10–13), preserving fixture access and the lane.

The main lane spans Y9–13 across the entire board. Its light dirt/stone floor remains broad
outside forts and has two-cell gates through forts. Open terrain above/below the lane offers
alternate approaches. The lane crosses both homes and the central objective.

Northern jungle occupies primarily Y1–7, with muted green walkable floor and mirrored tree
obstacles. Every tree occupies one explicit blocked cell. Gaps around small clusters permit
multiple routes; jungle floor has no movement penalty. Each forward fort opens north into
the jungle. Additional lane/jungle connections are recorded at X4–5 and mirrored X24–25,
Y7–9. Existing OathstoneCrossing Terrain3D resources, bridges and decorative props were
inspected but not reused on the beta surface: their heights/water conflict with the required
flat continuous board. They remain intact in StartingLevel.tscn.

Neutral camps are `west_camp` (X8–10/Y3–5) and `east_camp` (X19–21/Y3–5).
Each has nine brown/orange floor cells, an outline, and a floating orange sphere marked
INACTIVE, distinct from player characters. Every camp cell stays walkable, providing room
for multiple attackers. The neutral markers have no collider, AI, rewards or respawn logic.

The southern flank spans Y16–18. Mirrored blocked ruin blocks provide small constrictions
and cover silhouettes; paths around them stay connected. Four explicitly recorded lane/flank
approaches at X5, X12, X17 and X24/Y13–17 provide two connections per half. Y15 and Y19 also
remain open where not otherwise marked; the flank is not the board's only detour.
Cover geometry does not implement new cover rules, penalties or elevation effects.

The central objective is X14–15/Y10–11, four purple floor tiles with an outline and a floating
neutral prism. The marker floats clear of the floor and has no collider. All four cells are
accessible. There is no scoring, capture behavior or victory state.

## Original imported character assets

The user's model correction selects the original imported character geometry under
SourceAssets, rather than the later generated/assembled Blender replacements in Assets.
All six use separate GLBs exported directly from the six current, explicitly selected
Blender files. No new models, rigs or animations were generated for this stage.
Source Blender files and preexisting imported Godot resources remain intact.

Exact source paths, selected scene names and source/runtime SHA-256 checksums are in
`Docs/MOBA_BETA_MODEL_SOURCES.json`. All runtime exports live under `Assets/Characters/MobaBeta/`.

| Character | Source file under SourceAssets/characters | Status |
| --- | --- | --- |
| Hooded Assassin | hooded_assassin/assassin.blend | Current file's sole scene: Inspection_assassin_tpose.001 |
| Gentle Guardian | gental_guardian/guardian.blend | Original guardian main Scene |
| Time Weaver | ash_scholar/mage.blend | Original mage main Scene |
| Nigel | axe_warrior/nigel.blend | Original nigel main Scene; not Komodo |
| Gravebound Colossus | broken_oath_knight/knight.blend | Original knight main Scene; not the generated knight export |
| Beothuk Spirit Guardian | native/native.blend | Original native main Scene |

The six source asset folders are `hooded_assassin`, `gental_guardian` (legacy spelling),
`ash_scholar`, `axe_warrior`, `broken_oath_knight` and `native`.
The explicitly selected original files are `assassin.blend`, `guardian.blend`, `mage.blend`,
`nigel.blend`, `knight.blend` and `native.blend`. Their full paths and checksums are recorded
alongside the runtime exports in the manifest. The current assassin.blend contains a sole
scene named Inspection_assassin_tpose.001; the current file is used exactly as supplied,
rather than inferring its contents from older reports or substituting another filename.
The initial inspection found older inspection GLBs, including a cross-folder Guardian export;
these are available but the final scene uses fresh exports of the six named current files.
The existing Assets directories Assassin, AshScholar, BrokenOathKnight and Komodo contain
later generated replacement models and are deliberately not referenced by this scene.
Ashveil, ForsakenSister and MaskedNinja also exist but are not substitutes for this roster.
Original folders also contain GLB components and Blender projects; no FBX/GLTF was needed.

These original exports have predominantly grey PBR materials with embedded normal maps,
not the painted colors of the replacement models. Missing painted albedo is an asset-content
limitation, not a missing Godot resource reference. Every fixture must retain its serialized
original model instance; missing models now fail validation rather than creating placeholders.
Assets and SourceAssets binaries follow the existing Git ignore policy:
a checkout requires the local asset restore described in the manifest.
`Tools/ExportMobaBetaOriginalModels.py` reproduces the exports in an isolated background
Blender process. It loads only each named character scene, selects its meshes and bound
rig/parents, excludes lights/cameras/bone-display helpers, and exports no animation clips.
It never saves a .blend and verifies original checksums before and after each export.

Historical A–B.4 starting anchors (replaced by the B.5 forward-deployment table below):

| Team | Character | Cell |
| --- | --- | --- |
| Blue | Hooded Assassin | (2,10) |
| Blue | Gentle Guardian | (1,12) |
| Blue | Beothuk Spirit Guardian | (3,12) |
| Red | Nigel | (27,10) |
| Red | Time Weaver | (28,12) |
| Red | Gravebound Colossus | (26,12) |

All six cells are walkable, reachable and unique. These illustrative positions intentionally
follow the requested fixtures rather than mirroring character identities. Models face their
opponents. Their existing normalization, ground alignment, facing, team discs, names and
selection rings are baked into serialized child transforms/resources; runtime does not reset
them. `StaticTacticalCharacter.Bind` binds the scene-derived fixture metadata; BindRuntime
attaches the production Character by stable fixture ID. Labels and indicators reflect its state.
Imported AnimationPlayers/Trees are disabled on the instance and collision layers cleared.
Original resources are not modified. Animation is unnecessary for stationary presentation
fixtures; preserving a source rig does not require playing or authoring any clips.

## Significant C# types and integration boundaries

`RectangularBoard` is pure coordinate math. It enumerates cells, validates independent
width/height bounds, maps centers, inversely maps world X/Z and mirrors X coordinates.
It has no Godot dependency and can be tested without rendering.

`MobaBetaLayout` projects the serialized scene into read-only runtime regions and fixtures;
it has no default map constructor or hardcoded region/obstacle arrays. `BetaCastle` contains stable Id,
InitialTeamId, IsHome, footprint, capture, deployment, wall and named entrance cells.
`BetaCamp` preserves an Id and footprint. Connections names future route/adapter attachment
points. `Obstacles` unifies fort/home walls and nature obstacles. `Walkable` only answers
in-bounds/static-obstacle questions; it is not a canonical movement policy.
`Reachable` is a four-neighbor flood fill for development validation, never a route planner.
`Validate` returns diagnostics rather than mutating gameplay state.

`MobaBetaBattlefield` no longer generates or moves level geometry. Its Tool behavior is limited
to a read-only validation button and configuration warnings. Runtime creates transient
hover/movement overlays, HUD and picking helpers, reads the saved scene through `BetaSceneReader`, and retains camera/input.
Floor meshes, grid overlay, castle walls, camps, objective, obstacles and six original fixtures
are serialized nodes/resources. Opening the scene requires no game run to see these objects.

`Pick` intersects a camera ray with the common Y=0 floor, then calls WorldToCell. Hover,
selection and grid positions therefore share one conversion. Clicking a character's tile
shows its selection ring; right click clears selection. This does not activate a character.

Camera controls retain Battlefield.cs's focus/yaw, (0,24,32) camera offset, LookAt projection,
WASD pan, Q/E rotation, middle-drag rotation, wheel zoom, Home reset and G grid toggle.
Pan clamps and zoom limits now derive from the rectangular board; initial framing covers
the full board. The original camera behavior was embedded inside the production Battlefield
controller, not a reusable camera script. Its equations were adapted locally to avoid
editing the user's existing 14×14 production controller or instantiating its combat session.

Stage 6.7B can consume `battlefield.Layout.Castles`, indexed by stable castle Id. It should
copy InitialTeamId into its own production ownership state, use CaptureCells for occupancy
queries and DeploymentCells for destination validation, and treat IsHome explicitly.
Entrances/Connections support later map adapters. No ownership changes should be stored in
materials or visual fixtures. Stage 6.7B.1 already binds CharacterFactory, CharacterRegistry
and Character.TacticalPosition. Remaining movement/combat integration must explicitly
support the beta rectangular board rather than globally
changing BattlefieldGrid.Size or reusing the existing Terrain3D-specific TacticalMap blindly.

## Validation and verification

The map has 600 cells: 58 blocked, 542 walkable. All walkable cells form one connected region.
Both home/forward layouts, camps and obstacles are horizontal mirrors. Capture/deployment
zones are exact, disjoint, inside their footprints, and unobstructed. All entrances are open.
Main-lane left/right connectivity is checked within the lane band; flank connectivity is
checked within its own band. Both camps connect through the jungle, to the lane and forts.
Every special cell, connection and character starting tile is reachable from Blue's start.

`MobaBetaLayoutTests` retains 19 focused xUnit cases covering rectangular bounds and conversion,
exact/mirrored regions and entrances, disjoint zones, valid starts, lane/flank connectivity,
jungle/camp access and retention of the 14×14 legacy configuration. Existing CharacterStats
and production gameplay code are unchanged by this stage.

Godot modes:

```
Godot --headless --path . Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn -- --beta-smoke-test
Godot --path . Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn -- --capture-beta
```

The smoke mode validates scene construction, model loading, selection toggling and picking
every cell at three yaw angles. It also exercises zoom, middle rotation, grid toggle, pan
and Home reset. Capture mode saves `artifacts/moba_beta_overhead.png` and
`artifacts/moba_beta_castle.png`, then exits. Logs and screenshots are local ignored artifacts.
Stage 6.7A verification: `dotnet test PawnsOfDivinity.sln` passed 2,947 tests, failed 0, skipped 0.
This included 19 beta layout cases and 2,928 tests already present in the working tree
(the supplied 2,894 baseline plus existing uncommitted additions). The no-restore solution
build passed with 0 warnings and 0 errors. `git diff --check` passed. The preexisting tracked
diff is byte-for-byte identical to the before-stage snapshot; prototype, Battlefield.tscn
and Specification v0.2 hashes also match. All six original Blender hashes remain unchanged.

Godot 4.7.2 .NET imported all six final GLBs and ran the new scene both headlessly and with
OpenGL rendering. The final runtime smoke and capture logs contain no errors or warnings.
Visual inspection of the saved overhead/castle views confirmed floor/grid alignment, gates,
colors, obstacle geometry and visible source-model fixtures. No fallback models were used.
Programmatic checks exercised pan displacement, zoom in/out, middle rotation, grid toggle,
Home reset and every cell pick at three rotations. Live desktop inspection confirmed hover
picking. Complete physical keyboard/mouse interaction checks remain manual: desktop automation
paused its input actions when concurrent user input was detected. The game was left open
for inspection rather than interrupting the user's separate prototype editor.

The headless editor import reports the existing nested project at
Scenes/Battlefields/prototype/project.godot, a deprecated physics-interpolation binding,
and shutdown leaks (one dummy texture RID, one CanvasItem RID, four ObjectDB instances,
one resource). Import completes successfully, and final standalone beta runtime/capture logs
are clean. These editor-only diagnostics are retained in artifacts/stage67a_import.log;
their underlying origin was not established or changed in this stage.

## EDITING THE LEVEL IN GODOT

Stage 6.7A.1 verification: **2,970 tests passed; 0 failed; 0 skipped**. This retains all
2,947 existing tests and adds 23 scene-authoring validation cases. The solution build has
0 warnings/errors, and `git diff --check` passes. The preexisting tracked diff is unchanged.
Prototype, production Battlefield scene, Specification v0.2, all six Blender sources and
their runtime GLBs retain their prior checksums. No ignore rules or production gameplay
files were changed, and no commit/push was made.

Godot scene import completed. The standalone runtime smoke and rendered capture logs are
clean; the native round trip verifies original extraction, a moved obstacle surviving save
and reload with updated walkability, and off-grid rejection. Temporary scene/resource wrappers
are explicitly released before the smoke process exits. The headless editor still reports
its previously observed nested-project warning and small shutdown resource leaks; these
are separate from the clean standalone runtime. See `artifacts/stage67a1_import.log` and
`artifacts/stage67a1_smoke.log`. Rendered views are `artifacts/stage67a1_overhead.png` and
`artifacts/stage67a1_castle.png`. Desktop inspection confirmed the editor camera preview
renders the complete authored level before Play, obstacle roots expose their metadata and
transforms in the Inspector, and the root exposes its validation button.

The significant new types are `BetaBoardDefinition` (exported board Resource),
`BetaLevelElement` (exported per-object identity/footprint/classification), `BetaSceneReader`
(non-mutating transform-aware extraction), and the engine-independent `AuthoredLevelData`
snapshot. `BetaSceneRoundTrip` is development validation only. `StaticTacticalCharacter`
now binds serialized originals instead of creating models; its editor Tool mode never resets
authored transforms. `MobaBetaLayout` only accepts extracted data and performs validation.

### Open the authored map

Open the root project `D:/SoftwareEng/Game/project.godot` in Godot 4.7 .NET, then open
`res://Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn`. Do not open the nested
prototype project. Build C# once if the editor has not loaded its custom classes yet.
The geometry and six original models are visible **without pressing Play**. For an immediate
overview, select Camera3D and enable **Preview** in the 3D viewport. For a free editor view,
disable Preview, select `Terrain/ContinuousUnderlay`, then focus that mesh using F with the
viewport focused or **Perspective → Focus Selection**. Scene-tree filtering helps locate
an object without expanding all 600 tiles.
Running the current scene (F6) retains the tactical camera and selection controls.
The project main scene remains the separate production Battlefield.tscn.

The main scene has explicit `Terrain`, `Castles`, `Camps`, `Objectives`, `Obstacles`,
`DeploymentZones`, `CaptureZones`, and `CharacterFixtures` containers, plus `Routes`,
`Connections`, and the saved `GridOverlay`. `Elements/` contains reusable castle,
camp, objective, tree, ruin and team-wall PackedScenes. Their instance transforms are
normal saved Node3D transforms; no script recreates them on load or overwrites edits.

### Authoritative data fields

| Field | Single authoring source |
| --- | --- |
| Dimensions, scale, center, optional symmetry constraint | Root `BoardDefinition` / BetaBoardDefinition resource |
| An element's absolute tactical location | Its composed Node3D transform; names such as tree_3_2 are stable labels, not coordinates |
| Multi-cell footprint | BetaLevelElement `CellOffsets`, relative to its transform-derived anchor cell |
| Stable identity and element type | `ElementId` and `Kind` on BetaLevelElement |
| Castle initial team and home designation | Castle root `InitialTeamId` and `IsHome`; no mutable ownership state here |
| Castle membership | Inherited castle ancestor, or explicit `CastleId` for external capture/deployment roots |
| Capture/deployment cells and entrances | Zone/Entrance nodes, their transforms, offsets and entrance `Role` |
| Static blocking | Union of Terrain/Obstacle nodes whose `BlocksMovement` is true |
| Floor terrain tag | Each Terrain tile's `TerrainTag`; this does not define gameplay movement costs |
| Appearance | Serialized mesh/material resources and visual-child transforms |
| Character identity/team/spawn | StaticTacticalCharacter exported properties and root transform |
| Runtime character definition | Fixture DefinitionId resolved through CharacterRegistry; never inferred from node order/model |
| Character model | The actual serialized original PackedScene instance at `ModelNodePath` |

`BetaSceneReader.Read` composes transforms, resolves castle membership and extracts
`AuthoredLevelData` without modifying nodes. `MobaBetaLayout` validates and projects
that snapshot into runtime cells. There is no hardcoded default map or second obstacle
array in C#. This also means scenery paint is separate from tactical zone identity:
moving a zone or castle does not repaint independent Terrain materials. Update the
affected floor materials deliberately when redesigning a region; colors never decide control.

### Move or place an obstacle

1. Select an **obstacle instance root**, for example `Obstacles/tree_3_2`, in the scene tree.
   Moving only its Trunk/BlockedTree visual child changes appearance, not its blocked cell.
2. Use the transform gizmo with **relative** translation snapping at **1.25 units**.
   Alternatively enter an Inspector Position X or Z delta of exactly 1.25 per tile.
   Keep the metadata root Y=0, rotation=0 and scale=(1,1,1). Visual children may be styled
   separately, but keep blocking silhouettes inside their declared tile footprints.
3. For example, tree_3_2 starts at `(1.625,0,6.625)`. Set its Z to **7.875** to move it
   from tactical (3,2) to (3,3). The label/ElementId can stay `tree_3_2`: it is a stable ID.
   Save and run: (3,2) is now walkable and (3,3) is blocked.
4. To add an obstacle, instance `Elements/Tree.tscn` or `Elements/Ruin.tscn` under Obstacles,
   assign a globally unique ElementId, choose its aligned root position, and set BlocksMovement.
   Duplicating an existing instance requires changing its ElementId before validation.
5. Toggle BlocksMovement off to make that declared footprint decorative. To remove a
   blocker entirely, remove its instance. Both actions affect the next extracted layout.

Cell-center positions are `X=-2.125+1.25*tacticalX`, `Z=4.125+1.25*tacticalY`.
Absolute snapping to multiples of 1.25 about world zero does **not** align to these centers;
use relative snapping from an existing aligned object or the explicit center formula.
Nothing silently snaps a bad placement back into place.

### Edit castles, zones, camps and terrain

Select a castle root under Castles to inspect its ID, team, IsHome and CellOffsets.
Its wall/entrance instances are children in the packed castle scene. Enable **Editable
Children** on the instance to make per-map overrides, or open its PackedScene to intentionally
change every map sharing that element. Move wall instance roots to change wall cells;
change Entrance node offsets/position/Role to describe the actual open gates. Castle walls
must stay inside the castle footprint. Each west/east/north/south entrance needs at least
two connected open cells. Capture and deployment regions must stay in their owning footprint.

Capture/deployment roots are independently selectable under their named top-level containers.
Change their transform and CellOffsets to change their cells. When moving an entire castle,
move its associated capture/deployment roots with it, and adjust the independent Terrain
paint and any route/connection regions as needed. The validator reports stale or blocked
zones rather than automatically relocating them. Preserve castle ElementId values when
reshaping an existing castle so Stage 6.7B bindings remain stable.

Camp and objective roots carry their own footprints and their marker/border visual children.
Move the roots to move that metadata and those visuals together. Update their floor paint
separately. Camps/objective cells must remain accessible and disjoint from other special zones.

Under Terrain, each tile has one metadata root and a Surface mesh. Change TerrainTag for
classification; edit Surface's material for appearance. **Make Unique** on a shared material
before changing one tile's color, or intentionally edit the shared resource for all tiles of
that appearance. Set a tile's BlocksMovement to mark it blocked without an obstacle instance;
give it a matching visible blocked appearance. Avoid duplicate blockers on the same cell.
Terrain must continue covering every board cell exactly once, with flat metadata roots at Y=0.
The persistent grid overlay is a serialized visual; custom changes to board dimensions/scale
also require editing the authored floor and grid geometry, not just the BoardDefinition.

Fixture roots are spawn anchors. Move them by tile increments while keeping original model
children intact. Their ground alignment, scale and facing live in authored visual-child
transforms. There is no placeholder substitution or automatic transform normalization.

### Validate and save a variation

Select the beta root and click **Validate authored level** in the Inspector. Success prints
`AUTHORING VALID`; failure lists actionable node paths/IDs and constraints in Output.
Root configuration warnings use the same reader/validator. Run-time extraction also rejects
invalid authored data and exits with an error before presenting a partial level.
Validation checks alignment, finite transforms, bounds, nonempty footprints, duplicate IDs
and blockers, terrain coverage, castle references, zones, spawn availability, entrances and
whole-board/route connectivity. Validation never moves or corrects objects.

`BoardDefinition.RequireMirroredLayout` is an optional authoring constraint, off by default
so a single-obstacle edit can be tested. The supplied map remains mirrored and regression
tests pin the original layout. Enable it when requiring paired obstacle edits; the validator
then rejects unpaired blockers rather than silently creating a mirrored partner.

Use **Scene → Save Scene As** to save a new `.tscn` in the MobaBeta directory. Make the
BoardDefinition/materials unique before per-variation resource edits, and use instance
overrides instead of editing shared Elements scenes when changes should affect only this
variation. No C# edits are required for new valid positions, blockers, tags, zones or spawns.
Run the variation itself with F6. Existing baseline tests intentionally continue checking
MobaBetaBattlefield.tscn, while runtime validation applies to whichever variation is run.

### Development checks

`AuthoredBetaScene` runs one headless Godot extraction per xUnit process, using GODOT_BIN
or the executable path in `.godot/editor/project_metadata.cfg`. Set GODOT_BIN explicitly
on a new machine. It requires the restored original model assets and a built C# project.
It is separate from core combat tests and does not test graphical rendering.
`AuthoredLevelValidationTests` exercises the extracted data directly, including malformed
edits and snapshot serialization. The runtime smoke invokes `BetaSceneRoundTrip.Verify`:
native PackedScene pack/save/reload, a temporary tree move affecting walkability after save,
and rejection of a bad transform. Only a uniquely named temporary variation is written;
the authored map is never changed by these checks.

## PLAYING THE HOTSEAT SLICE — Stage 6.7B.1 baseline

The movement/lifecycle changes in the Stage 6.7B.2 section below supersede the activation-only
parts of this baseline description. The roster format and explicit identity bindings remain.

### Scope and confirmed format

The format confirmed at B.1 was **fixed 4v4, first to 15 victory points, no round limit**;
the later B.5 request supersedes it with fixed 5v5.
Scoring and victory resolution are not implemented. The current scene has six original
fixtures, three on each side. This stage ships a labeled **3v3 development vertical slice**,
leaving one empty roster slot per player. No fixture/model was invented and the board was
not rearranged. The adapter supports 1–4 authored units per player; tests verify supplied
eight-fixture setup works as 4v4. Full competitive setup still needs two deliberately authored
fixture instances/anchors and approved model choices. This gap does not change the format.

### Controls and visible state

- Run `res://Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn` as the current scene.
- Player 1 (blue) starts by default. Left click a ready friendly **model or floor tile**.
  A white ring marks selection and the bottom HUD shows the production Character's stats.
  Selecting or switching units spends nothing and does not start an activation. Opponent,
  defeated, used and unknown units are rejected; a valid prior selection is retained.
- Right click to deselect (also cancelling a queued click), or click an empty tile to clear.
- The HUD shows name, Player/team, current/max HP, shield, STR, DEF, INT, RES, WIS, SPD,
  effective MOV allowance, registry definition ID and activation status. Character.BaseStats
  supplies base values; existing effective DEF/RES/MOV properties supply adjusted values.
- Click **End Activation (temporary — no movement or attack)**. ActivationManager marks
  that Character used, selection clears, and control passes to the other player when eligible.
  The button is disabled without an eligible selection. Nameplates show READY/USED/DEFEATED;
  used discs dim. The header shows the current player/color, round and actual roster counts.
  Ready units belonging to that player have brighter team discs.
- After all eligible units act, the next round begins and production activation flags reset.
  HP, position and other Character state are not reset. There is no configured round limit,
  scoring or end-match rule. Existing WASD, Q/E, middle drag, wheel, Home and G still work.

An exhausted or defeated team is skipped using the production manager's existing policy.
If every unit is defeated, the UI waits with no eligible player instead of spinning through
empty rounds. No UI action causes damage in this stage; defeat handling is integration-tested.

### Configure roster slots

Each CharacterFixtures root exports **CharacterId**, **DefinitionId**, **TeamId**,
DisplayName and ModelNodePath. CharacterId is the stable instance/fixture key. DefinitionId
is a separate exact case-sensitive registry key. Current definitions are hooded_assassin,
gentle_guardian, beothuk_spirit_guardian, nigel, time_weaver and gravebound_colossus.
Runtime names/stats come from that definition, not DisplayName or model/node names.
Neither node order nor visual identity determines combat class.

TeamId selects Player 1 or 2; each must have 1–4 scene-authored fixtures. Root **StartingPlayer**
selects 1 or 2 and explicitly starts each new round in this temporary loop. This is development
policy, not a competitive first-player rule. Validate authored level now checks registry IDs
and roster setup as well as the existing map constraints. Invalid IDs/team/count fail clearly.

Changing DefinitionId configures the combat template; it does not replace the original model.
Future roster setup must supply approved matching visuals. Existing source GLBs, rigs and
authored transforms remain intact. Team colors use runtime material-instance overrides.

### UI-to-domain architecture

`MobaHotseatSession` is an engine-independent integration adapter. Its constructor accepts
MobaBetaLayout and StartingPlayer, resolves DefinitionId through CharacterRegistry, calls
CharacterFactory with the stable fixture ID, assigns Character.TacticalPosition, validates
rosters, and constructs the production ActivationManager in standalone mode. The read-only
Characters dictionary holds the actual domain instances; it contains no parallel HP/stat state.

- `MaxRosterSlotsPerPlayer` is the four-slot cap; `StartingPlayer` is explicit setup policy.
- `Round` and `CurrentPlayer` read ActivationManager; there is no duplicate round counter.
- `SelectedFixtureId` stores UI selection only; `SelectedCharacter` resolves that identity.
- `CanSelect` checks identity, Character.CanAct and the manager's NextTeamId.
- `Select` sets/clears selection without spending activation. Results reuse the existing
  BattlefieldCommandResult/BattlefieldOutcome types.
- `EndSelectedActivation` revalidates eligibility, calls StartActivation/CompleteActivation,
  and calls the existing StartNextRound when the round completes and living units remain.
  It never records movement or completes a Main Action.
- `Capture` builds a fresh immutable BattlefieldUnitState from the Character for HUD reads.

BattlefieldSession and TacticalUnit were inspected but remain coupled to the 14×14 encounter,
Terrain3D and movement/combat services. They are untouched. Standalone ActivationManager
already supports activation-only rounds. This slice does not advance shop, damage, cleanup
or scoring phases just to reset flags. Full lifecycle/preparation/cleanup integration remains
future work; this temporary loop is not the complete competitive match lifecycle.

`StaticTacticalCharacter.BindRuntime` checks identity/team/position agreement, retains the
Character reference, and adds a layer-2 picking-only body following TacticalUnit's convention.
Its collision mask is zero: it is not map obstacle metadata or a movement rule. RefreshRuntime
updates only the nameplate, ring and instance team material; models/rigs stay static.

`MobaBetaBattlefield.Session` owns the adapter. `_Ready` extracts scene data, creates the
session and binds fixtures by CharacterId. `_UnhandledInput` queues left clicks;
`_PhysicsProcess` sends them to `SelectAt`, which uses a model ray or grid-tile fallback.
Right click calls Select(null). `BuildHud` wires the button to RunCommand →
EndSelectedActivation. `RefreshMatchHud` reads current domain state each frame; RunCommand
shows command results and surfaces domain errors rather than silently resetting state.
No movement, attack or ability buttons are exposed.

### Verification and remaining Stage 6.7B tasks

The full suite passed **2,994 tests, 0 failed, 0 skipped**, including 24 new hotseat cases.
Coverage includes stable/class-ID mapping, node-order independence, configurable first player,
selection/deselection, opponent/used/defeated rejection, alternation, exhausted teams, reset
without healing/moving/replacing characters, roster limits, eight-fixture adapter support,
and snapshots of current production state.
The solution build passed with 0 warnings and 0 errors; `git diff --check` passed. The
preexisting tracked diff remains byte-for-byte unchanged. Godot imported the updated scene
and scripts; its headless editor exit retained the previously observed nested-project,
deprecated interpolation and small resource-leak diagnostics. Standalone hotseat smoke and
OpenGL capture logs contain no errors/warnings. The captured HUD was visually checked for
runtime stats, selected rings, player-color handoff and used-unit indication. Logs are
`artifacts/stage67b1_import.log`, `artifacts/stage67b1_smoke.log` and
`artifacts/stage67b1_capture.log`.

Godot smoke also clicks projected models/tiles, checks runtime HP in the actual HUD, tests
right-click clearing, emits the real End Activation button signal, completes both rosters and
checks the next round. Existing scene round-trip, rectangular picking, camera and grid checks
remain. `--capture-hotseat` saves `artifacts/moba_hotseat_selection.png` and
`artifacts/moba_hotseat_player2.png` to inspect stats/selection and the handoff with a used unit.

Remaining tasks: author the two missing competitive roster fixtures, integrate validated
movement and combat on the rectangular scene-derived map, and connect the complete production
round pipeline. Castle control/transfers, abilities, AI, economy, respawn/buyback, scoring and
victory handling remain unimplemented. No original model/source, production battlefield,
prototype, CharacterStats, specification or ignore rules are changed by this stage.

## TACTICAL MOVEMENT AND PATHFINDING — Stage 6.7B.2

### Playing and manual checks

Run `Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn`. Select an eligible friendly
unit to show **cyan reachable tiles**. Hover a cyan tile for the **gold shortest route**
and its MOV cost/step count. Unavailable destinations show a **red hover tile**. Clicking
a reachable empty tile commits the movement; the model slides along the approved cell
centers. Clicking a friendly model/tile still selects it; right click still deselects.

For Nigel's MOV 4 check, Player 1 can first end one activation without moving, then Player 2
selects Nigel at (27,10). The route to (23,10), the red forward castle's eastern gate, costs
4 via (26,10), (25,10), (24,10). Wall (23,9) is not a destination. Occupied friendly tiles,
occupied enemy tiles, out-of-bounds cells and destinations beyond available MOV are excluded.
Castle walls constrain the routes; movement never cuts diagonally through corners.

Moving **does not end activation** and does not consume the Main Action. The established
rule is one **unsplit movement opportunity** per activation. A shorter move still spends
that opportunity; unused allowance cannot be resumed as a second move. The HUD therefore
shows the unit's effective MOV and `Movement available 0` after moving, not a spendable
remainder. Click **End Activation (skip remaining actions)** when finished. Attacks are
not enabled yet, but the production manager retains Main Action availability.

A legal first move starts and locks that unit's activation. After it moves, selecting another
friendly unit is rejected until End Activation. Deselect/reselect of the active unit is safe
after playback. During playback, movement, selection/deselection and End Activation are locked;
pan, zoom, rotation and grid toggle remain usable. No queued click is replayed after playback.

The HUD shows current authoritative grid position and TRAVELLING while animation catches
up. Nameplates show MOVING during travel. The 3v3 authored fixtures, original models and
starting anchors are unchanged; the four-slot-per-player architecture remains.

### Authority and unresolved movement rules

Specification v0.2 confirms orthogonal movement up to MOV, one unsplit move, and movement
before a Main Action. It leaves general terrain/elevation costs and allied crossing unresolved.
This stage retains the existing prototype's **explicit provisional fixture policy**:
known authored flat floor tags cost 1 per orthogonal step, and every living occupied cell
blocks both traversal and destinations for both teams. This is not a new permanent balance rule.
No friendly pass-through is enabled. Defeated units follow the production executor's existing
non-occupying policy. Character.EffectiveMovement supplies existing effects such as Frostbite;
no MOV penalty is recalculated in the planner or UI.

All authored combat surfaces are still at Y=0. Elevated wall/battlement meshes remain blocked
decoration, not mechanically traversable elevation. No climbing/falling thresholds or costs
were added. Recognized flat visual tags are jungle, lane, flank, stone, camp, objective,
capture and deployment; none gains a new penalty. Unknown tags return unresolved costs and
are excluded from routes rather than silently treated as normal ground. Existing Wailing
Rampart and Spirit Frost queries are reused; unsupported ice interactions still fail closed.
Interrupted movement consumption and reaction/attack targeting rules are not invented. The
movement policy is labeled MovementRuleAuthority.ProvisionalFixture in production results.

### Shared pathfinding and movement commit

`GridPathfinder.Reachable` extends the existing helper to independent width/height and
positive caller-supplied step costs. It uses bounded Dijkstra search with a priority of
`(totalCost, insertionOrder)`. Neighbors are visited east, south, west, north, preserving
legacy tie order. Equal-cost predecessors are not replaced, so repeated queries return
the same route. Only cells within the runtime effective MOV budget are included. Unknown,
zero and negative costs are excluded. `TacticalRoute` stores a read-only path including
its origin and its total cost. The old square `Find` API delegates to this same search
and still returns its legacy origin-excluding route; the 14×14 production board is unchanged.

`MobaMovementMap` implements the existing IOrdinaryMovementMap over the authored layout
and **live Character positions**. IsValidPosition reads rectangular/static walkability;
IsOccupied reads living runtime units; GetStepCost supplies the declared flat policy and
existing terrain-service queries. HasKnownFlatCost distinguishes supported authored tags.
It does not implement visibility or attack legality. Search proposes routes; it does not
write positions or spend resources.

`MobaHotseatSession.ReachableDestinations` searches using this same map and occupied-cell
queries. `PreviewMovement` returns a `MobaMovementPlan` containing actor ID, round, origin,
destination, path and cost, without starting an activation. `MoveTo` is the destination
convenience API. `CommitMovement` rechecks selected identity, current player, round, origin,
occupancy, effective allowance, route and cost against fresh map state. Stale/forged previews
and invalid destinations are rejected before starting an activation. It then calls the
**existing production MovementActionExecutor.Preview and Execute**. That executor revalidates
every step, commits Character.TacticalPosition, spends only the movement opportunity and
returns the actual TraversedPath/TraversedCost. No transform-only movement or second stat,
occupancy or movement-rule implementation was introduced.

`RemainingMovement` means available allowance for the single remaining movement opportunity.
`PendingVisualMovement` retains the production result while its view is travelling;
`IsPresentationBusy` guards commands at the session layer as well as in Godot input.
`AcknowledgeVisualMovement` requires the matching request token and final runtime coordinate
before releasing that guard. Invalid/stale commands preserve state and allow a fresh preview.
An unexpected partially applied/failed executor result retains actual state and freezes further
gameplay through RequiresManualRecovery; no automatic rollback or replay is attempted.

### Round lifecycle reuse

MovementActionExecutor requires a shared production lifecycle, so the adapter now connects
ActivationManager to RoundLifecycleManager. PrepareCurrentRound uses existing Poison batch,
delayed-attack batch, timed-status update and RoundPreparationCoordinator services in their
established order. End of activations uses the existing RoundCleanupCoordinator, then starts
and prepares the next round. Shop, Other Triggers and ObjectiveScoring are acknowledged
extension points only; no shop, economy, scoring or victory service is implemented.
Existing status/preparation/cleanup effects are delegated, not recreated.

The movement executor's required terrain/Sentinel/combat dependencies are private match-local
instances. No ability preparation or attack command is exposed; Sentinel has no prepared
reactions and combat inputs return unknown. Merely supplying those dependencies does not
execute attacks. StartingPlayer remains the explicit development round-start policy.

### Smooth presentation and UI

`StaticTacticalCharacter.Cell` reads Character.TacticalPosition, while Fixture.Cell remains
the immutable **authored spawn**. PresentMovement queues the result's actual TraversedPath
and begins at its original center. AdvanceMovement interpolates at 4 world units/second,
finishing every cell center in order without overshoot; it never calls SetTacticalPosition.
ReconcileToRuntime clears playback and snaps to the domain's final center if recovery is
needed. Root transform, picking body, discs and labels travel together; original model
resources, rigs and animation clips are untouched. No walking/idle animation was required.

The domain commits discretely before visual travel and immediately reserves the destination
for occupancy. Input lock prevents interacting with an in-between visual position.
AdvanceMovementViews reconciles the final center and acknowledges the session token before
reenabling actions. Thus runtime picking, occupancy and HUD all use the same final cell.

UpdateMovementOverlay now captures fresh session routes rather than maintaining a separate
cache fingerprint; it pools transient lime-outline meshes. Hover builds its white path from those routes;
commit always revalidates it. SelectAt preserves model/tile selection and sends empty-tile
clicks to MoveAt. MoveAt sends the session command and passes its returned actual path to
the correct fixture by stable ID. Nothing writes moved positions back into the `.tscn`.

### Verification and remaining work

The full suite passed **3,023 tests, 0 failed, 0 skipped**, retaining all 2,994 previous tests
and adding 29 movement/pathfinding cases. Coverage includes MOV 4, positive weighted costs,
path lengths, deterministic ties and legacy compatibility, independent board edges, castle
gates, living occupancy and defeated occupancy policy, no diagonal steps, effective Frostbite
allowance, unknown terrain, stale/forged plans, opponent legality, one unsplit move, retained
Main Action, token/input locks, live occupancy/position snapshots and round readiness reset.

Godot smoke retains prior authoring/hotseat/camera checks and additionally verifies blocked
hover rendering, Nigel's route/cost overlay, real click-to-move, immediate production position,
an intermediate interpolated transform, locked End/duplicate commands, final reconciliation,
coordinate HUD, retained activation/Main Action and preserved authored spawn coordinates.
`--capture-movement` renders Nigel's 4-point gate route before, during and after travel to
`artifacts/moba_movement_preview.png`, `moba_movement_travelling.png` and
`moba_movement_complete.png`. The source scene is not saved or rearranged by these checks.
The solution build completed with **0 warnings and 0 errors**, and `git diff --check` passed.
Godot import completed with the same headless editor nested-project/deprecation/shutdown
resource diagnostics observed in earlier stages. The final standalone movement smoke and
OpenGL capture logs are clean. All three movement views were visually inspected, including
the 4-point gate route, intermediate travel, disabled End button and final grid position.
Logs: `artifacts/stage67b2_import.log`, `stage67b2_smoke.log`, `stage67b2_capture.log`.
All preexisting tracked changes are preserved. The only newly changed tracked source is
the existing GridPathfinder helper; no production domain executor/turn/stat files were edited.
The beta scene/fixtures, model exports, Blender sources, prototype, production battlefield,
specification and ignore rules were not changed. No commit or push was made.

Remaining work after B.2 (basic attacks are added in B.3 below): abilities and complete action UI, full 4v4 fixture authoring, castle
control/transfers and victory scoring. General terrain/elevation costs and unresolved
interactions still need design decisions. Preserve the future fog-of-war direction of
**simultaneous timed exploration transitioning to alternating PvP engagements**; no fog,
exploration timer, engagement trigger, AI, capture, economy or buyback is implemented here.
The confirmed target remains fixed 4v4, first to 15 VP, with no round limit.

## Stage 6.7B.3 — basic attack targeting and damage

### Controls and action economy

Select a ready friendly unit, optionally click a reachable destination and wait for travel,
then click **Attack**. Red rings identify legal opposing targets. Click an enemy model or
its occupied tile to inspect its range, calculated hit chance and estimated opening damage
on a hit. **Confirm Attack** submits the authoritative command. Preview never rolls RNG,
changes HP, or spends the Main Action. The estimate precedes shield absorption and excludes
later counter/follow-up strikes; it is not guaranteed total HP loss.

Cancel targeting returns to movement selection. Right click also deselects. Entering Attack
chooses the production activation without consuming its Main Action; cancelling does not
release that activation to another unit. End Activation remains available to skip unused
actions. WASD, Q/E/middle drag, wheel, Home, and G retain their existing behavior.

**Production rule takes precedence over the request's final acceptance bullet:** completing
a Main Action automatically ends activation after the combat exchange, including on a miss
(Specification v0.2 section 3, ActivationManager.CompleteApprovedMainAction). Thus a successful
attack passes control automatically; a second manual End Activation is unnecessary and is
disabled until another eligible friendly unit is selected. Movement remains one unsplit
opportunity and may precede the attack. No action-economy rules were changed.

Feedback identifies attacker/target, HIT/MISS, total target HP loss, opening shield absorption,
remaining target HP, and whether a counter/follow-up occurred. HUD/team indicators and
nameplates refresh from the same Characters. Defeated models remain visible with a grey
indicator and DEFEATED label, lose their picking body, cannot act/be targeted, and do not
block movement, matching the existing production living-occupancy policy. No respawn or rewards.

### Integration architecture and existing equipment inputs

`MobaHotseatSession.Attacks.cs` extends the existing session rather than adding combat state:

- `MobaAttackPreview`: outcome/reason, stable actor/target IDs, distance, min/max range,
  hit chance and estimated opening damage. `CanExecute` is true only for a Ready result.
- `CanAttack`: selected living friendly actor, correct activation phase/team, available
  Main Action and no presentation/recovery lock. `BeginAttackTargeting` starts the existing
  activation if necessary. Full target/equipment validation happens in PreviewAttack.
- `InitializeCombat`: shares the production resolver and existing development loadouts.
  Constructor injection of `IRandomRollProvider` supports deterministic tests; normal play
  retains SystemRandomRollProvider.
- `AttackContext`: combines actual runtime Characters/positions with AttackIntent,
  effective equipment through DisarmEligibility, and CombatExchangeRequest. Existing
  Sanctuary context is derived from the full activation roster.
- `PreviewAttack`: calls AttackActionExecutor.Preview and the resolver's existing read-only
  PrepareStrike calculation, including effective defenses, triangle/passive modifiers.
  PrepareStrike's access changed private to internal; its calculation is unchanged.
- `ExecuteBasicAttack`: revalidates, reserves a unique request ID, locks reentrant input,
  invokes AttackActionExecutor.Execute, and reports actual CombatExchangeResult/HP. Repeated
  request IDs return AlreadyProcessed. Unexpected partial resolution preserves applied
  state and freezes the session for recovery rather than replaying damage.
- `AdvanceCompletedRound`: shared by manual End and completed attacks; existing cleanup,
  preparation and per-round reset remain authoritative. No scoring is calculated.

MobaBetaBattlefield builds Attack/Confirm controls. ToggleAttackMode enters/cancels targeting;
RefreshAttackHud derives target rings and estimates from PreviewAttack; SelectAt routes target
clicks by stable fixture ID; ConfirmBasicAttack sends a unique session command and refreshes
UI. Rings are runtime children of existing fixtures. No source geometry, models, authored
spawns, transforms, or scene metadata are rewritten by play.

Equipment comes from **the existing BattlefieldSession.FixtureAttack configuration**, now
internal for reuse, keyed by registry DefinitionId (not visual name/order). No new assignments
or inventory were invented. Existing development inputs are:

| Definition | Category | Power | Accuracy | Range |
| --- | --- | --- | --- | --- |
| hooded_assassin | Sword / physical | 5 | 90 | 1 |
| gentle_guardian | MagicScroll / magical | 3 | 90 | 1–2 |
| time_weaver | MagicScroll / magical | 4 | 90 | 1–2 |
| nigel | Axe / physical | 4 | 90 | 1 |
| gravebound_colossus | Lance / physical | 3 | 90 | 1 |
| beothuk_spirit_guardian | Bow / physical | 4 | 90 | 2–3 |

These are development attack inputs, not a permanent equipment/balance catalog. UI labels
DEV loadouts. Production targeting checks allegiance, life, range, action eligibility and
actual map bounds/walkability. This flat development map uses the existing open-targeting
policy: LOS/FOW and height interactions are unresolved and not enforced. The inspector's
`DevelopmentOpenTargeting` explicitly enables that policy; false makes attacks Unresolved
rather than guessing LOS. Disarm without explicitly configured unarmed power/accuracy also
reports Unresolved. No default unarmed stats are invented. The authoritative combat pipeline
already performs eligible counters/follow-ups, shield/defeat handling and existing passives;
this stage enables those automatic results, without adding ability actions or reaction rules.

### Movement-highlight synchronization fix

The existing pathfinder already used actual shortest traversable costs, including detours
and occupied cells. UI state had an independent refresh/cache path and retained hover state
across commands; filled cyan/gold movement colors also conflicted with authored zones.
`MobaMovementPreview.Capture` now takes one fresh read-only snapshot directly from
Session.ReachableDestinations, and `PlanFor` exposes only those approved candidate routes.
UpdateMovementOverlay uses it for both visible destinations and hover path/cost, every
refresh and immediately after commands. Commit still revalidates the same production rules.
Lime outlines mean reachable, white outlines mean hovered path, and red hover means invalid.
Gold capture, cyan deployment and purple objective floors keep their separate meanings.
The status displays actual path cost/budget, e.g. `4/5 MOV`; invalid tiles have no valid path.

Regression tests put Nigel at (24,8): (20,8) has Manhattan distance 4 but needs a castle-wall
detour exceeding MOV 4, so it is neither highlighted nor accepted. (21,7) costs 4 and is both.
All 600 destinations are checked against fresh actual commands from the same snapshot,
with and without an additional occupied cell. Selection, occupancy, movement and activation
changes are covered. No movement executor or pathfinding rules were changed by this fix.

### Verification and remaining Stage 6.7B work

MobaAttackTests covers all six development loadout range boundaries, friendly/self/unknown
targets, preview purity, hit/miss, shields, actual HP loss, automatic activation completion,
attack after movement, stale target revalidation, death, duplicate/reentrant requests,
existing counters/follow-ups, round reset, unresolved LOS policy and missing unarmed inputs.
MobaMovementPreviewTests covers the highlight regressions above. Godot smoke exercises real
buttons/target clicks, target rings, estimate/HP text, automatic team transition, repeated
confirmation, stale-overlay clearing and all previous movement/camera/authoring checks.

`--capture-attack` stages the original Assassin/Nigel at adjacent central cells **in runtime
memory only**, renders preview/result to `artifacts/moba_attack_preview.png` and
`artifacts/moba_attack_result.png`, and exits. It never saves the scene. Normal F6 retains
all six original 3v3 starting fixtures. Standalone logs: `artifacts/stage67b3_smoke.log`,
`stage67b3_capture.log`, `stage67b3_import.log`.

Final verification: **3,057 tests passed, 0 failed, 0 skipped** (all previous 3,023 retained;
34 added cases). Solution build: **0 warnings, 0 errors**. `git diff --check` passed.
Standalone Godot smoke and graphical OpenGL capture runs exited successfully with clean
runtime logs; both attack images were visually inspected. Editor import completed with
the existing nested-prototype-project warning and headless-editor shutdown RID/ObjectDB/
resource-leak diagnostics; import is not described as warning/error-free. Source hashes
confirm all six original Blender files and runtime model exports, original prototype,
production Battlefield.tscn and Specification v0.2 remain unchanged. Existing tracked diffs
were preserved; the new tracked combat change only exposes PrepareStrike internally.
No commits, pushes, source-model edits or serialized battlefield layout edits were made.

Remaining: ability actions, complete equipment/rule inputs, full four-per-side authored
fixtures, castle control/transfers and victory scoring. The target stays fixed 4v4,
first to 15 VP, no round limit. Fog direction remains simultaneous timed exploration
transitioning to alternating PvP engagements, with none of it implemented here. No AI,
shop, economy, scoring, capture, respawn/buyback, new animations or source-model changes.

## Stage 6.7B.4 — combat feedback, defeat and development reset

### Controls and visible state

Run `Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn` with F6. All living fixtures have
screen-facing, team-colored HP bars above their models, current/max HP, and a shield line
when shields exist. Bars read runtime Characters every refresh and follow movement and
camera pan/rotation/zoom. They ignore mouse input so model/tile selection still works.
The existing upper-left HUD continues to display the current player and round.

Attack/Confirm, movement, End Activation and camera/grid controls are unchanged. Completed
Main Actions still end activation automatically after the production combat sequence.
Actual resolved strikes produce compact floating HIT/MISS, HP loss, COUNTER and FOLLOW-UP
messages. Shield absorption is cyan and defeat is red. These are sequenced visual playback
of an already-completed synchronous exchange; they do not delay or mutate tactical state.
Full names and details are retained in the history, while floating text stays compact.

Click **Combat history (N)** at the upper right to expand/collapse a scrollable chronological
journal. Each exchange records its original round, opening, any eligible counter, any
follow-up, defeat, shield absorption and final participant HP/shields. History is retained
for the current development match and cleared by reset; it is not saved to disk.

Defeated imported models get a runtime-only grey material override, grey team indicator
and DEFEATED nameplate. Their HP bars, selection and picking bodies are disabled, and
they cannot act or be attack targets. They do not block movement under existing production
living-occupancy rules. Reset restores the original material references and living picking
body. Original mesh/material resources, Blender files, rigs and animation sources are intact.

**Reset Match (DEV)** immediately reconstructs a fresh session from the loaded scene-derived
layout and configured starting player/open-targeting policy. This discards the old runtime
match: characters, HP/shields, statuses, cooldowns, round/lifecycle/activations, movement and
request tokens are fresh. The same six visual fixtures rebind by stable IDs, cancel pending
travel and return to the authored initial cells. Selection, target rings, movement overlays,
queued clicks, floating messages and combat history clear. Reset also supports recovery from
partial combat failure; it never replays an old attack. Reset is blocked during synchronous
combat resolution, but allowed during visual movement or a recovery-required session.
Camera position/zoom/rotation and the grid toggle are preserved. Reset is a development
tool, not a respawn, buyback, match-victory or production restart rule. It uses the layout
loaded at launch; to load new editor changes, save and rerun the scene.

### Presentation architecture and actual data sources

`Scripts/Gameplay/Levels/MobaCombatPresentation.cs` is pure C# presentation interpretation:

- `MobaUnitPresentation.Capture(Character)` snapshots stable identity/name, actual current
  and maximum HP, shields and defeat. HealthFraction/HealthText format the health display;
  ShowHealthBar/Pickable derive presentation from existing defeat state, not new eligibility.
- `MobaCombatEvent` records kind, source/target IDs, full history text, actual HP loss,
  shield absorption and hit/miss. FloatingText provides the compact version.
- `MobaCombatRecord` groups events with request ID, originating round and final HP summary.
- `MobaCombatFeedback.Interpret` reads CombatExchangeResult and participant snapshots. It
  preserves opening → counter → follow-up order and inserts defeat after the lethal strike.
  OpeningHealthLost/FollowUpHealthLost and aggregate participant HP loss determine the
  actual strike HP partitions; StrikeResult.Damage is not misreported as HP loss on overkill
  or shields. There is no attack formula, hit roll or damage application here.
- `MobaCombatJournal` stores read-only ordered Records; internal Append records actual
  resolved exchanges and DisplayText formats the chronological history.

The production API reports OpeningShieldAbsorbed directly, but no separate counter/follow-up
shield amounts. Opening absorption is shown at the opening; remaining absorption is the
measured before/after shield delta, clearly labeled **remaining exchange total** at the end
of that record. It is not attributed to a guessed strike. A miss and a shielded hit with
zero HP loss remain distinct. If combat throws before returning a CombatExchangeResult,
the existing partial-failure message/recovery lock and live HP bars reflect actual retained
state; no fabricated complete record is added. A development reset recovers from this state.

`MobaHotseatSession.Attacks.cs` captures snapshots around the existing executor and appends
the returned result **before** subsequent round-start processing can change HP. CombatHistory
is presentation data only. CanResetMatch guards reentrant combat; CreateResetSession creates
an entirely new session with the same layout/options and optionally injected test randomness.
All production damage, shield, counter, follow-up, defeat and turn calculations are unchanged.

`MobaCombatHud.cs` is a runtime CanvasLayer separate from combat logic. Initialize creates
mouse-transparent health widgets and the collapsible RichTextLabel history. Refresh reads
live fixture snapshots and projects their world positions through the existing camera.
Present queues resolved events; _Process floats/fades them in order without changing state.
Clear cancels all queued/visible messages and resets the history panel. No replay is possible
from this UI. HealthVisible/HealthText/HistoryText/HistoryExpanded/FloatingCount support
the native Godot smoke assertions. The journal retains full text; the panel scrolls.

`StaticTacticalCharacter` exposes a Presentation snapshot, remembers original model-material
overrides and applies/restores only runtime defeat styling. BindRuntime safely rebinds to
fresh Characters without creating duplicate picking bodies/material instances; ReconcileToRuntime
cancels old movement and restores the domain cell center. MobaBetaBattlefield owns the DEV
button and ResetMatch orchestration, while ConfirmBasicAttack forwards actual recorded
outcomes to the combat HUD. Authored scene geometry/metadata are never rebuilt or saved.

### Verification and remaining milestones

MobaCombatPresentationTests verifies authoritative HP/max/shields, health/defeat visibility,
opening/counter/attacker-follow-up/defender-follow-up order and participant IDs, actual HP
partitions, misses, fully shielded hits, opening and remaining shield summaries, overkill,
defeat on opening/counter, chronological history, preview/rejection/duplicate purity,
fresh reset identities/HP/statuses/cooldowns/positions/requests, reset after later rounds,
reset during movement, retained starting-player/map policy, reentrant reset rejection and
recovery from a partially applied combat exception. The expanded native Godot smoke also
tests actual HUD history toggle/popups/bars, grey defeat material, disabled picking,
repeated attack/defeat/reset cycles and reset during movement with queued-input cancellation.

`--capture-feedback` temporarily stages the original Assassin and Nigel in the center at
runtime, exercises shield/counter/follow-up feedback, a lethal attack and DEV reset, then
exits. It writes `artifacts/moba_feedback_combat.png`, `moba_feedback_defeat.png` and
`moba_feedback_reset.png`; it does not save altered positions into the scene. Normal F6
uses the original authored 3v3 fixture layout. Verification logs are
`artifacts/stage67b4_import.log`, `stage67b4_smoke.log` and `stage67b4_capture.log`.

Final verification: **3,072 passed, 0 failed, 0 skipped** (15 additional cases; all previous
3,057 retained). Solution build: **0 warnings, 0 errors**. `git diff --check` passed.
Godot standalone smoke and graphical capture runs exited successfully with clean logs;
combat, grey defeat and reset views were visually inspected. Editor import completed with
the existing nested-project warning and headless-editor shutdown RID/ObjectDB/resource-leak
diagnostics; import is not warning/error-free. The tracked diff exactly matches the B.4
pre-stage snapshot: no tracked production changes were added. Hashes confirm the original
prototype, production scene, specification and all six original Blender/runtime models are
unchanged. Authored beta scene geometry was not edited. No commit or push was made.

Remaining milestones: full 4v4 fixture authoring, ability/action UI, complete equipment and
unresolved LOS/terrain inputs, castle control/transfers, scoring and match victory. The
confirmed format is fixed 4v4, first to 15 VP, no round limit. No scoring, new abilities,
AI, economy, buyback, fog of war or animation work is implemented in B.4. The existing
development loadouts/open-targeting limitations from B.3 still apply.

## Stage 6.7B.5 — full 5v5 manual combat battlefield

### Current roster and deployment

The main beta scene is still `Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn`.
It now contains ten serialized StaticTacticalCharacter fixtures: five Blue (TeamId 1),
five Red (TeamId 2). The six original instance IDs remain; four new instances reuse the
original packed model resources. Each team has five distinct definitions. Opposing copies
share immutable CharacterDefinition data but have separate Character, HP, status, cooldown,
position and activation state. No new designs, exports, rigs or import configurations.

| Team | CharacterId (stable instance ID) | DefinitionId | Starting cell | Original model |
| --- | --- | --- | --- | --- |
| Blue | hooded_assassin | hooded_assassin | (7,12) | Assassin.glb |
| Blue | gentle_guardian | gentle_guardian | (8,12) | GentleGuardian.glb |
| Blue | beothuk_spirit_guardian | beothuk_spirit_guardian | (9,12) | SpiritGuardian.glb |
| Blue | blue_time_weaver **added** | time_weaver | (7,13) | TimeWeaver.glb |
| Blue | blue_gravebound_colossus **added** | gravebound_colossus | (9,13) | Colossus.glb |
| Red | nigel | nigel | (22,12) | Nigel.glb |
| Red | time_weaver | time_weaver | (21,12) | TimeWeaver.glb |
| Red | gravebound_colossus | gravebound_colossus | (20,12) | Colossus.glb |
| Red | red_hooded_assassin **added** | hooded_assassin | (22,13) | Assassin.glb |
| Red | red_gentle_guardian **added** | gentle_guardian | (20,13) | GentleGuardian.glb |

All model paths above are under `Assets/Characters/MobaBeta/`. The four added nodes copy
existing model normalization/ground alignment and use the correct team's facing/disc
resources. They remain selectable/editor-visible children under CharacterFixtures.
CharacterId, DefinitionId and TeamId are explicit independent inspector fields. The runtime
factory resolves DefinitionId; it does not infer a class from CharacterId or node order.

All ten initial cells are walkable, unique, inside their own forward-castle deployment
zones, and mirrored by X → 29-X. Each fort leaves its middle rear tile empty: Blue (8,13),
Red (21,13). Existing entrances/routes remain accessible. The original six anchors moved
from the historical home-base examples because some were capture/general floor cells,
not permitted deployment cells. All five now deploy together at the forward castle for
shorter manual approaches. Castle geometry, floor cells, capture/deployment regions,
58 blocked cells, camps, objective, connections and 542-cell walkable component are unchanged.

### Configuration, readiness and interaction

MobaHotseatSession.MaxRosterSlotsPerPlayer is now five. Smaller 1–5-per-side development
layouts remain supported for general/legacy tests, while this authored scene supplies exactly
five each. Production ActivationManager already accepts arbitrary unique instance rosters;
it needed no changes. With everyone alive, five Blue and five Red activations alternate,
then existing cleanup/preparation starts the next round and resets surviving readiness.
Defeated/exhausted sides follow the existing skip policy. No separate turn implementation.

`MobaTeamReadiness` is a read-only TeamId/Ready/Total/Living snapshot. Session.Readiness counts
production Character.CanAct for each team independently of whose turn it is. Thus the HUD
can show `BLUE: 3/5 READY · RED: 4/5 READY`; dead and used units are excluded, while an
active unit remains ready until its production activation completes. This is presentation,
not a timer, scoring condition or new eligibility rule.

BoardDefinition.RequireDeploymentSpawns is serialized true in this scene. BetaSceneReader
extracts it into AuthoredLevelData, and MobaBetaLayout rejects any spawn outside a deployment
zone belonging to its TeamId. Errors identify the fixture, cell and team. Legacy synthetic
3v3 scenarios deliberately set this authoring option false to preserve their historical
coordinates; they are not the live map. Bounds, alignment, overlap, obstacle and connectivity
validation still applies to every layout. The flag survives native PackedScene save/reload.

To edit the roster, move CharacterFixtures nodes with the gizmo on grid-aligned friendly
deployment cells. Copy an existing fixture to add a variation, assign a globally unique
CharacterId, exact registry DefinitionId, correct TeamId, and matching original imported
model/facing/team visual. Do not exceed five per team. Run **Validate authored level** before
F6. To author a different map, Save As a separate scene as described in the editing section;
no C# spawn array needs editing. Changing the board flag is an explicit authoring choice,
not a runtime relaxation of movement rules.

SelectAt now considers all intersected picking boxes plus the occupied ground-cell anchor,
then chooses the closest projected model/ground anchor deterministically. This resolves
front selection boxes obscuring rear units in the denser formation. It still uses existing
physics bodies and authoritative occupancy, and never authorizes an action. Opponent,
defeated, busy and used-unit checks remain in the session. Health widgets stagger only when
their screen rectangles overlap and draw team-colored leader lines back to the units.
World positions and movement coordinates are never adjusted by this presentation layout.

All movement/pathfinding and attacks retain the same services. Living occupancy comes from
the complete ten-character dictionary, including allies and enemies; no per-size movement
algorithm was added. All ten fixtures play the production result's actual path and reconcile
to their own Character by ID. Attacks use the existing DefinitionId-based development
loadouts, production targeting/executor/resolver, shields, automatic counters/follow-ups
and Main Action activation completion. Existing unresolved equipment/LOS policies remain.

### Reset and manual acceptance

Reset Match (DEV) still creates a fresh session from the loaded authored layout. Since that
layout now has ten fixtures, it restores all ten automatically: authored cells/team assignments,
full production HP, initial zero shields/statuses/cooldowns, ready round 1 and configured
starting player. Runtime-only defeat materials restore, and indicators/health/readiness update.
Selection, pending clicks, travel, locks, targeting, feedback/history and old request IDs clear.
Models, picking bodies and signals are reused, not duplicated. Camera/grid state is preserved.

1. Open the main beta scene and press F6. Confirm ten units and both `5/5 READY` counts.
2. Zoom into Blue's forward castle. Click each model or occupied floor tile; verify its
   displayed name, team and production stats, including added Blue Time Weaver/Colossus.
3. Select a reachable empty cell, inspect shortest-path cost, move, then End Activation.
   Repeat for Red, including the added Red Assassin/Guardian. Verify readiness decreases.
4. Use all five units per team in alternation. With no deaths, ten distinct completions
   start round 2 with both readiness counts restored to five.
5. Advance toward the center, choose Attack, inspect a legal enemy, then Confirm. Observe
   actual HP/shields, counters/follow-ups where eligible, history and automatic team transition.
6. Defeat a unit. Verify grey appearance, missing health bar, lower readiness, and rejection
   as an active/living target. Surviving units must continue into later rounds.
7. Pan/rotate/zoom, then Reset Match. All ten return to the table above with fresh state;
   camera framing stays unchanged. Repeat after movement/defeat and verify no duplicate units.

### Tests and native verification

MobaFiveVsFiveTests covers ten unique runtime IDs, five distinct definitions per team, all
six original model resources, every initial cell/team/deployment, mirror symmetry, JSON
and native scene-derived metadata, invalid/enemy deployment, both starting-player ten-turn
sequences, defeated-unit skipping, readiness reset, all ten instances' movement/occupancy
and combat, ally/enemy targeting, independent opposing copies, feedback/result agreement,
five successive fresh resets and rejection of a sixth slot. Existing 3v3/4v4 adapter tests
remain as explicit legacy fixtures sourced from the same native terrain/model metadata.
Their old coordinates are test-only. General gameplay tests and production services are unchanged.

Godot smoke checks model and occupied-tile selection for every fixture in the crowded
formation; ten-unit alternating rounds; every fixture's independent smooth-movement binding
and final world/domain reconciliation; combat, readiness, HP/history, defeat and repeated
reset during travel; single picking body per fixture; preserved camera and authored spawns;
all 600 tile picks at multiple rotations; and native pack/save/reload validation. It writes
no source scene. `--capture-five` captures the real authored ten-unit overhead view and
close views selecting added Blue Time Weaver and Red Gentle Guardian:
`artifacts/moba_5v5_overhead.png`, `moba_5v5_blue_castle.png`, `moba_5v5_red_castle.png`.

Final B.5 verification: **3,102 passed, 0 failed, 0 skipped** (30 additional cases, all
3,072 prior tests retained/adapted). Full solution build: **0 warnings, 0 errors**.
`git diff --check` passed. Godot native save/reopen, ten-unit runtime smoke and graphical
captures passed; all three 5v5 images were visually inspected. Editor import completed
with the existing nested-project warning and shutdown dummy-texture/CanvasItem/ObjectDB/
resource-leak diagnostics; standalone runtime/capture logs are clean. Logs:
`artifacts/stage67b5_tests.log`, `stage67b5_build.log`, `stage67b5_import.log`,
`stage67b5_smoke.log`, `stage67b5_capture.log`.

Preservation comparison against the pre-B.5 scene confirms every non-fixture node block,
all model/material resource declarations and all authored geometry are identical; the only
board-resource addition is RequireDeploymentSpawns. The complete tracked diff matches its
pre-stage snapshot. Original prototype, production scene/specification and all six original
Blender/GLB hashes match. Existing import sources, asset configurations and ignore rules were
not edited. No commit or push was made.

Remaining before subsequent gameplay milestones: objective capture/control, first-to-15 VP
scoring/victory, and timed activation design. No victory detection or activation timer is
implemented. Timer duration/timeout behavior are unresolved; target match length is roughly
20–30 minutes subject to playtesting. Preserve the simultaneous timed exploration/fog-of-war
direction for later work. No fog, AI, new abilities, economy/shop or buyback was added.
Specification v0.2 remains unchanged; these are beta configuration decisions.

## Stage 6.7B.6 — objectives, castles, development VP and victory

### Audit: approved rules versus development decisions

The audit searched all project docs and gameplay scripts. Castle/transfer decisions exist in
this document's approved beta list below; Specification v0.2 section 7 confirms objective
scoring after cleanup at round end but leaves values, detailed objective control, ties and
post-victory effects unresolved. There was no preexisting castle/objective/VP implementation
to duplicate. Production Character, movement, combat, action economy and lifecycle remain
the execution boundary. The approved beta 5v5/15 VP format supersedes older spec roster/map
numbers without editing Specification v0.2.

Approved castle capture now runs against scene-derived CaptureCells:

- A forward castle becomes owned immediately when completed authoritative movement ends
  with living units of exactly one team in its capture zone. Ownership persists when vacated.
  Merely passing through, previewing, hovering or moving a visual model cannot capture.
- Both teams' living capture-zone occupants mean contested; current ownership persists.
  Units elsewhere in the castle footprint/deployment area do not contest. Defeated units
  do not contest. A defender leaving can make the remaining enemy occupation uncontested.
- A production combat defeat of the final defender immediately permits capture by surviving
  occupiers after the complete synchronous exchange. A ranged kill from outside does not
  capture an empty castle. Existing poison/delayed deaths may also release contested control,
  but their unresolved source credit does not invent kill VP.
- Homes retain their initial teams permanently; no home capture/destruction rules. The map
  starts with Blue/Red homes and forward castles owned according to InitialTeamId. Future
  non-home castles may explicitly use InitialTeamId=0 for neutral ownership; home team 0 is
  invalid. Deployment validation still requires each initial fixture to spawn in its own
  friendly deployment region.

Capture timing uses completion of MovementActionExecutor's discrete command. It occurs
before smooth visual playback finishes; input remains locked until the view reconciles.
Capture is not tied to frame rate, tween completion, a UI callback, or a public score button.

Central objective control is explicitly **DEVELOPMENT live occupation**: one living team
controls, both contest with no sole controller, and an empty area is neutral. Unlike castle
ownership, central control does not persist after all occupiers leave. Permanent objective
capture/retention rules remain unresolved. The central zone is the only neutral scoring
objective currently authored. Jungle camps are never objectives and award nothing.

### Configurable DEVELOPMENT scoring

Select the scene root → **Development Scoring** → expand the BetaScoringSettings resource.
Save the scene and rerun to apply changes. The resource is serialized in the main scene;
its immutable snapshot is the current match's scoring authority. Reset retains that profile.
The HUD always labels DEV values (K=defeat, C=capture, F=occupied fort, O=central), and its
tooltip explains them. No value below is a permanent balance decision.

| Inspector field | Default VP | Timing / eligibility |
| --- | --- | --- |
| DefeatPoints | 1 | Actual production combat defeat, credited to the opposing participant, including counters/follow-ups; queued until round end |
| CastleCapturePoints | 0 | Optional bonus for a real forward-castle ownership transition, queued until round end |
| ForwardCastleHoldPoints | 1 | Per forward castle, only if uncontested and occupied by its owning team at round end |
| CentralHoldPoints | 2 | Sole living central objective controller at round end |

All values permit 0–1000; invalid values fail clearly. Zero can disable an award category.
Initial ownership, empty owned castles, homes, deployment cells, neutral camps, hover and
selection earn no VP. Legitimate recaptures are distinct transitions; the default capture
bonus is zero. Dedupe uses movement/attack/transfer tokens, character defeat IDs, capture
event+zone IDs and round+zone hold IDs. Rejected/repeated commands cannot farm the same event.

The existing lifecycle runs cleanup, enters ObjectiveScoring, queues eligible hold awards,
then settles the entire round's pending events once. Pending kill/capture VP is displayed
separately from banked VP. Victory uses confirmed threshold **15**, with no round cap.
Both totals settle simultaneously, avoiding an iteration-order winner. Development TiePolicy:
`HigherScoreThenDraw` (default) awards the higher total if both reach 15 in that batch, with
an equal total producing a draw; `Draw` draws whenever both reach the threshold. A permanent
tiebreaker is unresolved. Scores may exceed 15; they are not clamped or discarded.

On victory/draw, the match stays at ObjectiveScoring with final scores/history intact.
Selection, movement, attacks, transfer targeting/execution and End Activation reject further
gameplay. Camera, grid, inspection/history and DEV reset remain available. No next-round
poison/delayed effects are processed after finish; this freeze is the beta completion policy,
not a finalized post-victory effect rule. Partial combat failures without a returned exchange
result remain recovery-required: actual HP is retained, no fabricated kill award, reset available.

### Approved transfer restrictions and controls

All 16 previously recorded conditions below were retrieved before implementation. Grouped
into the seven restriction categories requested by this stage:

1. Transfers are between two distinct castles; both must be friendly-controlled.
2. The acting unit must stand in its origin castle's friendly capture zone.
3. Neither endpoint may be contested; living capture-zone occupancy blocks incoming/outgoing.
4. Transfer costs the Main Action and ends activation through production ActivationManager.
   Movement into the origin first is permitted if the Main Action remains.
5. The player chooses a walkable, unoccupied destination DeploymentCell.
6. Any friendly castles connect regardless of distance; newly captured castles qualify immediately.
7. Origin/destination ownership, occupancy, selected actor, origin cell, round and deployment
   choice are revalidated on execution. Invalid requests spend no action/movement resources.

Select a ready friendly unit inside an uncontested friendly capture zone. Click **Castle
Transfer**, choose a destination+cell in the dropdown (or click one of those valid deployment
tiles), then **Confirm Transfer**. Cancel targeting/right click preserves the Main Action.
Entering targeting selects/locks the activation just like Attack mode. The original model
snaps to the approved destination; the authoritative Character position changes first.
No walking range/cost is applied to an approved transfer. Existing Main Action restrictions
are consulted; missing active-Taunt context blocks the transfer instead of bypassing it.

Runtime labels above castles show owner and contested status; orange means contested.
Central control has its own label. Hover also reports runtime owner/control. Authored walls,
floors, capture gold, deployment cyan and original team architectural accents remain intact;
the runtime ownership labels are authoritative. Notifications show capture/contest/control,
transfer and round-end settlement. The VP display shows banked/pending totals. Victory
appears on an overlay with a DEV reset button. Existing combat history, HP, movement, camera
and grid controls remain unchanged.

### Significant classes and integration boundaries

- `DevelopmentScoringProfile`: immutable, validated development values, confirmed threshold
  and explicit development tie policy. `BetaScoringSettings.Snapshot` is the inspector/native
  resource boundary; scene values are copied at launch, not changed during an active match.
- `CastleControlService`: owns mutable castle owners privately. Castle/Objective/AllZones
  return immutable snapshots computed with current living Character occupancy. Reconcile
  processes deduplicated authoritative boundaries and emits ownership/contest notifications.
  Only movement/defeat boundaries authorize castle capture; round-end reads cannot invent it.
- `MobaVictoryState`: read-only award ledger, pending/banked VP and terminal winner/draw.
  Internal Queue deduplicates valid events; Settle applies each round once as a batch.
- `MobaHotseatSession.Objectives`: initializes services, reconciles movement/combat/death
  changes, records actual combat defeat credit, queues configured captures/holds and settles
  in the production scoring phase. Core movement/combat formulas are untouched.
- `MobaHotseatSession.Transfers`: BeginTransferTargeting, PreviewTransfer and ExecuteTransfer
  provide the approved action boundary. Immutable CastleTransferPlan carries actor/round,
  both castle IDs, origin and selected deployment cell. Fresh validation precedes production
  Main Action approval, authoritative position update and activation completion. Duplicate
  successful tokens cannot move again; rejected prevalidation consumes nothing.
- `MobaBetaBattlefield.Objectives`: runtime markers, VP/notifications, transfer controls,
  victory overlay and developer smoke/capture helpers. It reads session state; it cannot set
  castle owners or grant VP. No saved geometry is recreated. Main scene changes only add
  the scoring resource/reference. BetaSceneRoundTrip now also verifies that resource survives
  native pack/save/reload.

Reset builds new Characters, control service, score ledger, winner state and event guards
from the original authored layout and current match profile. It clears pending awards,
ownership changes, contested state, central occupation, transfers/selection/travel, notices,
combat history and victory overlay. Original owners, ten starts, HP/statuses/cooldowns and
readiness return; camera framing stays. Marker nodes, buttons, signals and models are reused.

### Manual checks, automated tests and remaining decisions

1. F6 the main scene; confirm Blue/Red 0 VP and their initial castle owners.
2. Move a unit onto its fort's capture tiles to earn occupied-fort VP after completing the
   round. Move another unit into an empty enemy forward capture zone: owner changes immediately.
3. Bring both teams into the same zone: contested indication appears and transfers are blocked.
   Defeat the final defending zone occupant with a surviving attacker inside: owner flips.
4. From a friendly capture zone, use Castle Transfer → choose an unoccupied friendly deployment
   cell → Confirm. Verify the position and automatic activation completion. Occupied/enemy/
   same-castle/contested choices must reject without action costs.
5. Occupy the center uncontested and finish rounds. Default center + one occupied friendly
   fort earns 3 VP/round; five held scoring rounds reaches 15. Empty/contested zones score nothing.
6. Verify the winner overlay and blocked commands, inspect history, then reset. All ten units,
   owners, neutral center and zero VP must return without camera changes. Repeat.

`MobaObjectiveTests` covers capture timing/legality/persistence/path-through, live capture-zone
contesting, last/remaining defenders, ranged and counter defeats, fixed homes/neutral forts,
central changes/camps, configurable pending/end-round awards, duplicate guards, simultaneous
threshold policies, post-victory commands/history, no round limit, transfers (new capture,
distance, Main Action, busy presentation, same/enemy/unknown/contested endpoints, stale round/
origin/occupied destination, retry) and repeated ownership/score reset.

Native Godot smoke retains every B.5 check and adds scoring-resource round trip, capture
markers/notifications, contested UI/transfer blocking, final-defender capture, real transfer
buttons/position, default-profile VP to victory, overlay/action locks and reset of all new
state with stable node counts. `--capture-objectives` stages original units in runtime memory,
captures contested/captured/victory/reset views under `artifacts/moba_objectives_*.png`, then
exits. It does not save staged positions. This demo requires positive fort/central round
awards; disabling both intentionally prevents its occupation-only win demonstration.

Final B.6 verification: **3,138 passed, 0 failed, 0 skipped** (36 additional cases; all
3,102 prior tests retained). Full solution build: **0 warnings, 0 errors**.
`git diff --check` passed. Standalone Godot smoke and all four graphical captures exited
successfully with clean runtime logs; contested/captured/victory/reset views were inspected.
Editor import now loads the scoring resource correctly (the resource requires Tool metadata);
remaining import diagnostics are the existing nested-project warning and dummy-texture/
CanvasItem/ObjectDB/resource shutdown leaks, so import is not described as warning-free.
Logs: `artifacts/stage67b6_tests.log`, `stage67b6_build.log`, `stage67b6_import.log`,
`stage67b6_smoke.log`, `stage67b6_capture.log`.

Preservation: all authored geometry/fixture node blocks and original model/material resource
declarations match the pre-B.6 scene. Only the development scoring resource/reference was
added to the serialized scene. The full tracked diff matches the pre-stage snapshot, and
prototype/production scene/specification plus all six original Blender/GLB hashes match.
No production combat/stat/ability/turn formulas, original assets, ignore rules, commits or
pushes were changed/created.

Unresolved permanent rules remain: objective retention/contest design, exact VP values,
simultaneous-threshold tiebreaking, poison/delayed/environmental defeat credit, post-victory
effect handling and activation timer duration/timeout behavior. Development decisions above
are explicit substitutes for playtesting. No timers, new abilities, AI, economy, shop,
buyback, NPC combat or fog of war were added. Specification v0.2 is unchanged.

## Stage 6.7B.7 — configurable tactical activation timer

### Inspector configuration and hotseat controls

Select `Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn` → root → **Development Timer**.
The serialized BetaActivationTimerSettings resource exposes Enabled and DurationSeconds.
Defaults are enabled / **30 seconds**, for development playtesting; the duration is not a
permanent rule. Any finite positive duration is accepted, including values above the
Inspector's suggested range. Save/rerun to use edited configuration. Native scene round-trip
validation checks timer configuration as well as scoring and map metadata.

The existing readiness row now also displays clock owner, remaining whole seconds (rounded
up), pause reason, **Timer (DEV)** checkbox, and **Pause/Resume (DEV)**. Current round/player
and team readiness retain their existing displays. The clock turns orange-red at the last
min(5 seconds, 25% of configured duration); urgency never changes rules or expires early.
Tooltips show precise remaining/configured time. No extra panel covers combat history.

Uncheck Timer (DEV) for normal untimed local hotseat. Pause/Resume freezes only the countdown,
not committed movement playback or gameplay simulation. Re-enabling/resuming preserves the
same activation's remaining allowance, so toggling does not replenish it. Reset restores
the original Inspector enabled/duration snapshot, clears DEV pause, and gives the initial
player a full allowance. Victory disables these controls until Reset Match.

### Authoritative ownership, time and timeout policy

One session-owned TacticalActivationTimer is authoritative. The default time source uses
Stopwatch monotonic elapsed time, not wall-clock dates or accumulated Godot frame delta.
Tests inject IActivationTimeSource and advance it explicitly without sleeping.

The clock starts when the player is entitled to their next alternating activation, before
selecting a model. Selecting/changing/deselecting, opening Attack or Castle Transfer, and
read-only previews keep the same deadline. Ordinary completion or timeout increments a
session turn serial and gives the next eligible player a fresh allowance. The serial also
distinguishes consecutive activations by the same player when the other side is exhausted.
Round reset uses the production lifecycle; there is still no gameplay round limit.

Every gameplay command samples expiry before accepting its work, then a disposable command
scope pauses charging until synchronous execution/reconciliation completes. A click arriving
after the deadline cannot execute as the next player's command. UpdateActivationTimer is
also polled by Godot for idle selection/targeting time. The UI never decides who forfeits.

On zero, the deterministic local development policy is:

1. Forfeit the already active unit, even if presentation selection was cleared.
2. Otherwise forfeit an eligible selected friendly unit.
3. Otherwise choose the eligible living ready unit with the lowest ordinal CharacterId,
   independent of visual node order. In the initial Blue roster this is Beothuk Spirit Guardian.

Production ActivationManager.ForfeitActivationForTimeout closes the activation through its
existing completion core. This new explicit entry point validates activation phase/active
actor; it does not attack, spend a Main Action, clear taunt, alter HP or change ordinary End
Activation's taunt guard. An expired taunted unit forfeits rather than forcing an AI attack.
No new ability behavior is implemented. Completed movement, capture, damage and scoring
events remain intact. Unconfirmed attack/transfer targets and queued pointer input clear.
The opposing eligible side gets control; the existing exhausted-team policy still applies.

At most one timeout is applied per poll. If the application was stalled for a long interval,
the next player gets their full allowance from that handoff; overshoot is not propagated
through multiple unseen activations. No delayed Godot Timer signal is queued. A match GUID,
turn serial, round/owner binding and synchronous polling prevent old deadlines from spending
a new turn or a reset match. Clock sources must be finite and monotonic; invalid injected
sources fail clearly instead of changing match outcomes.

### Required pauses and safe resumption

- Authoritative command execution, including combat rolls/counters/follow-ups, transfer
  commit, timeout completion, cleanup/scoring and victory transition. An already accepted
  near-zero command finishes; timeout never interrupts a partially committed result.
- Visual movement from committed path until the existing matching movement acknowledgment.
- Combat feedback playback: the Godot presenter obtains a match-scoped CombatClockPresentation
  ticket for the real combat record and acknowledges it after queued floating feedback ends.
  The next player's fresh clock is paused during that playback. Existing input eligibility
  remains authoritative; presentation does not create another combat lock or action rule.
- Development pause/disabled mode, recovery-required state, and phases with no eligible player.
- Victory/draw: clock stops permanently, until a fresh session reset.

Command boundaries and acknowledgments rebase the time sample, excluding the complete paused
interval even when no frame polls occurred. Late/repeated/old-match combat tickets cannot
release a current pause. If a command partially fails, applied state is retained, the recovery
lock pauses timing, and DEV reset restores the match. Reset during movement also cancels the
old playback and countdown; it never transfers that elapsed interval into the new match.

### Significant implementation files and functions

- `TacticalActivationTimer.cs`: ActivationTimerConfiguration validates immutable launch
  settings. IActivationTimeSource/MonotonicActivationTimeSource provide elapsed time.
  TacticalActivationTimer.Pulse owns RemainingSeconds, Enabled/DevelopmentPaused, owner,
  round/TurnSerial, pause reason, urgency and terminal state. It cannot choose or end units.
- `BetaActivationTimerSettings.cs`: Tool/GlobalClass Godot resource; Snapshot supplies the
  pure immutable configuration. It contains no editor/runtime regeneration behavior.
- `MobaHotseatSession.Timer.cs`: UpdateActivationTimer applies expiry policy. TimedCommand
  brackets authoritative operations with nested-safe pause scopes. Development enable/pause
  methods preserve allowance. HoldClockForCombatPresentation/AcknowledgeCombatClockPresentation
  validate renderer completion tickets. LastTimeoutMessage/TimeoutCount are UI notifications.
- Existing session selection, movement, attack, transfer and manual End boundaries use those
  scopes. AdvanceCompletedRound advances the clock serial after actual activation completion.
  CreateResetSession keeps launch configuration, reuses or accepts an injected source, and
  creates a new clock identity and fresh time baseline. No production formulas or costs changed.
- `ActivationManager.ForfeitActivationForTimeout`: the only new production-domain entry point;
  all existing manager code and earlier tracked edits are preserved around this addition.
- `MobaBetaBattlefield.Timer.cs`: readiness-row controls and urgency rendering; polls the
  session, clears stale targeting after a timeout, and acknowledges completed combat playback.
  MobaBetaBattlefield._Process does not calculate expiry or choose forfeited units.

### Manual acceptance and deterministic verification

1. F6 with Inspector timer enabled. Blue starts at 30 seconds before selecting anything.
2. Select/change units and open targeting; verify the same clock continues.
3. Move near zero: the committed path finishes without losing time to animation. After
   reconciliation, the remaining time resumes. An expired pending click must be rejected.
4. Confirm a legal attack or transfer before expiry: it completes safely and ends activation;
   Red gets a fresh clock. Combat feedback pauses that new clock until playback finishes.
5. Let selection/Attack/Transfer time expire. Verify one unit is marked used, unconfirmed
   previews disappear, positions/HP already committed remain, and the next player receives time.
6. Pause/resume and toggle untimed mode; verify no refill. Complete rounds and a 15-VP match;
   victory must stop the clock. Reset restores original configuration, ten fresh units and
   the initial countdown, with the same camera framing.

MobaActivationTimerTests uses injected clocks for initialization/configuration, selection
time, unchanged deadlines, manual/automatic completion, deterministic fallback, large elapsed
intervals, targeting deadlines, movement/combat/transfer pauses near zero, combat tickets,
DEV pause/disable, defeated and exhausted teams, full-round timeout progression, victory,
repeated resets, monotonic-source validation, partial recovery, stale movement, frame-chunk
independence and the production taunt/phase forfeiture guards. No timing test sleeps.

Native smoke retains all previous gameplay checks and exercises real timer/pause/checkbox
controls, timeout cancellation of pointer/attack/transfer input, preserved movement/capture,
combat acknowledgment, victory stop and fresh reset with injected elapsed time. Developer
`--capture-timer` uses an explicitly injected clock for reproducible screenshots of initial,
urgent and timeout/handoff states under `artifacts/moba_timer_*.png`; normal F6 uses Stopwatch.
Neither helper saves staged positions or changes authored configuration.

Final B.7 verification: **3,179 passed, 0 failed, 0 skipped** (41 added cases; all previous
3,138 tests retained). Full solution build: **0 warnings, 0 errors**. `git diff --check`
passed. Native Godot smoke and graphical initial/urgent/timeout captures passed with clean
standalone logs; all three timer images were visually inspected. Editor import accepts the
Tool timer resource and reports only the existing nested-project and shutdown dummy-texture/
CanvasItem/ObjectDB/resource-leak diagnostics; it is not warning/error-free. Logs:
`artifacts/stage67b7_tests.log`, `stage67b7_build.log`, `stage67b7_import.log`,
`stage67b7_smoke.log`, `stage67b7_capture.log`.

Preservation checks confirm every earlier tracked edit remains. ActivationManager's only
addition is ForfeitActivationForTimeout; removing that block reproduces the pre-stage file.
All other tracked diffs match their baseline. All scene geometry/fixture node blocks, model/
material declarations and development scoring values match; only a timer resource/reference
was added. Prototype, production scene, Specification v0.2 and all six original Blender/GLB
hashes match. No model/import/ignore-rule changes, commits or pushes were made.

This remains local hotseat development tooling, not network multiplayer. Server-authoritative
deadlines, latency/grace rules, disconnect/reconnect, pause permissions, animation pause budgets,
and focus/suspend behavior need multiplayer design. There is no automatic focus-loss pause;
use DEV pause when stepping away. Future simultaneous timed exploration/fog of war requires
its own phase timing policy; this countdown applies only to alternating tactical activations.
No AI, networking, fog, economy, new abilities, balance changes or victory-rule changes were added.

## Stage 6.7B.8A — Hooded Assassin abilities

The existing registry already defined all three abilities. Production already supplied smoke
placement/expiry, visibility, teleport validation, Smoke Veil, Shadow Strike combat, Disarm,
cooldown tracking and action finalization. B.8A adds the missing battlefield/session/UI wiring;
it does not replace those calculations or alter CharacterStats or Specification v0.2.
Both original Assassin model fixtures bind by DefinitionId, independently of team or node order.

### Controls and actual parameters

1. Select a ready friendly Hooded Assassin. Click **Abilities** in the top readiness row.
   The panel lists both active abilities and the passive, costs, reuse and ready round/current
   eligibility. Opening it does not stop the activation timer.
2. Choose **Smoke Bomb + in-cloud teleport**, then **Preview Ability**. Purple tint previews
   the complete caster-centered 5×5 footprint; smaller cyan squares mark legal unoccupied
   teleport destinations. Click a destination and **Confirm Ability**. No effect, cooldown
   or action is spent before confirmation. Right click or **Cancel** abandons targeting.
3. Smoke Bomb creates all 25 cells and teleports within that same new cloud as one Main
   Action, ending activation. Self range 0; reuse interval 3 (R1 cast is ready R4); the cloud
   persists through R2 and expires at that round's cleanup. Full clouds extending off-board
   are rejected, never clipped. Wall/occupied tiles cannot be teleport destinations. The
   production constraints reject staying on the occupied source, crossing clouds or using
   another owner's cloud. There is no extra numeric teleport range.
4. Actual smoke is a translucent blue-grey tile tint; authored gold capture, cyan deployment
   and purple objective tiles remain identifiable and grid lines remain visible. Smoke adds
   no movement penalty. Smoke Veil grants **+20 percentage points evasion** to an Assassin
   in smoke, including after reveal; it is applied to real basic/Shadow strikes and counters.
5. Invisibility requires smoke, no orthogonally adjacent living enemy, and no attack reveal
   latch. On the opponent's turn an invisible Assassin's model, nameplate, HP bar and model
   picking body are hidden. It still occupies its authoritative cell and cannot be targeted
   by direct basic attacks or Shadow Strike. Friendly models stay visible. This is the existing
   smoke visibility rule, not a general fog-of-war system or hidden-information hotseat security.
6. On a later activation while smoke remains, move from a genuinely invisible starting cell
   to range 1 of an enemy. Choose **Shadow Strike**, **Preview Ability**, click a highlighted
   enemy, then **Confirm Ability**. The actual pre-movement complete-roster visibility snapshot
   establishes the invisible opening; adjacency at the destination does not erase that history.
   Standing adjacent before the approach gives a visible opening. A snapshot is invalidated
   at activation/round transitions, timeout and reset; no fabricated invisible snapshot is used.
7. Shadow Strike is a range-1 physical Main Action, reuse 0. Visible opening adds **3** damage;
   eligible invisible opening adds **5** and applies **one-round Disarm** on a surviving hit.
   The bonus applies only to the opening, after ordinary physical damage in the existing
   production resolver. Normal counters/follow-ups, shields, Fury and defeat/scoring remain
   authoritative. A miss reveals but does not Disarm. The panel shows hit chance and an opening
   damage estimate; actual exchange outcomes populate the existing floating feedback/history.
8. Successful active use ends activation automatically. Thinking/preview/cancelling uses the
   existing clock. Only committed resolution and real combat feedback playback pause timing;
   timeout clears unconfirmed ability/pointer state without effects or cooldowns. Reset Match
   creates fresh characters, smoke, visibility latch, statuses, cooldowns, handles and request
   identities, restores authored fixture positions and retains camera/configuration.

### Explicit development inputs and unresolved mechanics

Root Inspector **Development Assassin** (`BetaAssassinSettings`, serialized Resource) contains
only unresolved integration inputs, clearly DEV in the UI:

- Enable Development Unarmed Fallback = true in the scene; Power **0**, Accuracy **90**.
  Production defines Disarm's unarmed substitution but does not approve permanent equipment
  Power/accuracy. This explicit physical range-1 fallback lets the actual Disarmed counter
  resolve. The pure session's default has this disabled and rejects an invisible Shadow opening
  as Unresolved when it is required. Existing DEV weapon loadouts remain the B.3 loadouts.
- Development Smoke Over Obstacles = true. Smoke may visually cover blocked cells without
  changing walkability. Toggle false to reject a whole footprint intersecting an obstacle.
  Smoke collision/coverage interaction is not a finalized balance rule.

No permanent numeric parameter was invented. Ability spell tags remain unknown: Silence with
unknown classification is explicitly rejected by production eligibility. General LOS remains
the existing DEV open-targeting policy; smoke does enforce the approved direct-target invisibility
gate. Standalone teleport into existing clouds is deliberately unavailable: production
SmokeTeleportActionCoordinator returns UnresolvedActionCost. Smoke Bomb's integrated teleport
is the approved playable behavior. Production's attack-reveal latch has no approved automatic
re-entry/new-cloud/round reset; this integration does not invent one. Match reset clears it.
Transfers/teleports do not invent a new normal-movement castle capture trigger. Teleport
reconciles occupancy, contesting and central objective control under the existing B.6 adapter.
Original sources/imports, ten fixtures and authored map remain unchanged.

### Significant classes, data and functions

- `AssassinDevelopmentProfile`: immutable, validated pure configuration. `Unarmed` supplies
  the explicit fallback AttackConfiguration or null; it does not change base stats.
  `BetaAssassinSettings.Snapshot` converts exported inspector fields into that profile.
- `MobaHotseatSession.Assassin.cs`: thin authoritative integration. `Smoke` is the same
  SmokePlacementService passed to RoundCleanupCoordinator and both ability executors.
  `Visibility` evaluates production smoke state against the complete positioned roster;
  `IsVisibleToPlayer` answers the renderer's observer question.
- `AssassinAbilities` reads registered metadata/runtime cooldowns. `BeginAssassinAbility`
  validates the selected activation and creates production AbilityUse handles; cancelling
  drops handles without calling a resource finalizer. Shadow handles carry target-specific
  AttackIntent identities. `PreviewAssassinAbility` calls production Preview methods and
  PrepareStrike, never spends actions or rolls randomness, and rechecks current state.
- `CommitMovement` records visibility immediately before an approved movement. `ShadowContext`
  uses that snapshot only in the same activation/round. `ExecuteAssassinAbility` repeats
  validation, executes SmokeBombAbilityExecutor/ShadowStrikeAbilityExecutor, records actual
  combat/control outcomes once, and advances the existing activation lifecycle. Failed partial
  resolution retains real state behind the existing manual-recovery lock. Request IDs prevent
  duplicate committed effects; old handles cannot survive selection/activation/reset changes.
- `ShadowStrikeEligibilityResult.OpeningDamageBonus` exposes the existing authoritative 3/5
  bonus; ShadowStrikeResolver and preview both read it. This is the only production refactor:
  no numerical or eligibility rule changed. Existing production ability executors/finalizers
  own smoke, teleport, cooldown, Main Action completion, damage, reveal and Disarm.
- `AttackContext` now supplies production Smoke Veil evasion and the explicit unarmed fallback
  for both participants. `ApplySmokeAttackReveal` records actual ordinary-attack/counter reveal
  as well as the existing Shadow reveal. `CreateResetSession` carries DEV configuration into
  a fresh runtime; it does not mutate the old characters.
- `MobaBetaBattlefield.Assassin.cs`: builds the ability panel, calls session begin/preview/execute,
  and pools disposable runtime smoke/preview/teleport meshes. `ConfirmAbilityUi` reconciles the
  original model to the authoritative teleport endpoint and presents real combat records.
  It never computes ability effects. The opaque targeting panel sits above projected health
  widgets. `StaticTacticalCharacter.RefreshRuntime` applies observer visibility to original
  meshes/indicators/nameplate/picking; MobaCombatHud applies the same gate to HP bars.

### Testing and manual reproducibility

The normal authored spawn is inside a forward castle. Smoke Bomb requires its full footprint
to remain on-board, and teleport cannot land on walls/occupied cells. Move to a clear central
area before testing; no authored start positions were moved for this stage. For invisible
Shadow testing, cast while the eventual target is more than one tile away, complete the round,
then approach it from invisibility while the cloud remains. Already-adjacent targets receive
the visible +3 case. Opponents can still attack an Assassin once adjacency/attack reveals it;
Veil remains active if its current cell is covered.

`MobaAssassinTests` adds **32 cases** covering metadata/both teams, pure preview/cancel,
full footprint/edge/obstacle policies, occupancy revalidation, teleport synchronization,
Main Action/cooldown/expiry, caster death, invisible targeting and occupancy, Veil in/out of
smoke, real pre-approach visible/invisible bonuses, misses/lethal hits, Disarm/unarmed counters,
unresolved fallback and spell tags, request/handle safety, timeout, resolution pause and reset.
The native smoke test additionally drives the actual HUD buttons, model/collision/HP visibility,
overlays, combat feedback clock ticket and reset; scene round-trip includes the new Resource.

Run `dotnet test PawnsOfDivinity.sln`, `dotnet build PawnsOfDivinity.sln --no-restore` and
`git diff --check`. Godot CLI scene argument is
`Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn`; user arguments after `--`:
`--beta-smoke-test` validates the complete playable scene. `--capture-assassin` stages runtime-only
positions and drives the real ability controls to save preview/smoke/Shadow-preview/result/reset
images under `artifacts/moba_assassin_*.png`, then exits. It never saves staged fixture positions.
Normal F6 starts the authored map and normal monotonic timer.

Final B.8A verification: **3,211 passed, 0 failed, 0 skipped** (32 new cases; all previous
3,179 retained). Solution build: **0 warnings, 0 errors**. `git diff --check` passes, with
Git's existing LF/CRLF conversion notices. Standalone Godot smoke and graphical capture logs
are clean. All five preview/cast/Shadow-preview/result/reset images were visually inspected;
the targeting panel was made opaque and layered above projected health labels. Editor import
succeeds but retains the existing nested-prototype warning and shutdown dummy-texture,
CanvasItem/ObjectDB/resource-leak diagnostics. Logs: `artifacts/stage67b8a_tests.log`,
`stage67b8a_build.log`, `stage67b8a_import.log`, `stage67b8a_smoke.log`,
`stage67b8a_capture.log`, `stage67b8a_preservation.log`.

Preservation checks compare the pre-stage tracked patch, production snapshots, scene snapshot
and original asset hashes. All earlier tracked edits remain; ActivationManager is unchanged
in this stage. Only the shared Shadow opening-bonus property/refactor changes production code.
Scene geometry, fixture transforms, original materials, scoring/timer settings and source
assets match; only the DEV Assassin resource/reference was added to the scene. No original
Blender/import resources, CharacterStats, Specification, ignore rules, commits or pushes changed.

Remaining milestones include other characters' ability integration, permanent equipment/unarmed
balance, spell tags, LOS/terrain interaction rulings, separate smoke-teleport action cost and
reveal renewal policy. No unrelated ability, AI, shop, networking or general fog of war is added.

## Approved beta castle rules — implemented in B.6

These rules were approved before B.6, were not implemented in Stage 6.7A, and are now connected:

- Immediate uncontested occupation after completed movement.
- Immediate capture after the final defender is defeated.
- Ownership persists.
- Contesting uses living units in the capture zone.
- Transfers are castle-to-castle only.
- The origin unit must be in a friendly capture zone.
- The destination must be friendly-controlled.
- Transfer costs the Main Action and ends activation.
- The player selects an unoccupied deployment tile.
- Contested castles block incoming and outgoing transfers.
- Friendly castles may connect regardless of distance.
- Movement into the origin zone followed by transfer is permitted if the Main Action remains.
- Rejected transfers consume no resources.
- Newly captured castles become eligible immediately.
- Origin and destination are revalidated on execution.
- Transfers to the same castle are invalid.

CastleControlService and the session transfer/VP adapters implement the B.6 behavior described
above. There is still no spawn/buyback, neutral combat/AI or economy. Home destruction or
home capture rules are not invented. Unknown terrain interactions remain unresolved.
No Specification v0.2 changes or commits are part of this stage.
