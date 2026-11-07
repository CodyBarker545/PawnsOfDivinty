# Oathstone Crossing

The main game now loads a compact 16 x 16 tactical battlefield (20 x 20 metres of playable ground) surrounded by a small Terrain3D region. Assassin starts in the west at (1,7); Ashveil starts in the east at (14,8). Both have equal-length routes to the central Oathstone.

## Layout and objective

- A raised stone shrine in the center is reached by the main causeway.
- A winding creek divides the battlefield; north and south bridges offer flanking routes.
- Opposing hillside groves and boulders obstruct routes, while the central approach remains clear.
- Existing pine, oak, small-tree, fern and stone models dress the level. Their older exports had plain white materials, so the level stores bark, leaf and rock overrides without modifying those source GLBs.
- Characters intentionally render larger than realistic scale for readability, with team markers and name labels.

Move either character onto one of the four cells around the Oathstone to claim it. The ring, beacon and HUD show the owner. If both characters occupy the shrine area it becomes contested. Ownership is retained after leaving an uncontested captured shrine. Combat, attacks, turns and victory conditions are not implemented yet.

## Files

- `Scenes/Battlefield.tscn`: main scene, camera, sunlight, environment and C# controller.
- `Scenes/StartingLevel.tscn`: the editable Terrain3D level, props, bridges, river, spawns and objective.
- `Assets/Levels/OathstoneCrossing/TerrainData/`: native Terrain3D height/control/color region data.
- `Assets/Levels/OathstoneCrossing/TerrainAssets.tres`: editable Terrain3D texture palette.
- `Assets/Levels/OathstoneCrossing/TerrainMaterial.tres`: terrain shader settings.
- `Assets/Levels/OathstoneCrossing/Textures/`: copied existing CC0 demo textures and their licence.
- `Scripts/Gameplay/Battlefield/TacticalMap.cs`: terrain heights, water/prop blocking, bridge surfaces, slope checks, click picking and path routes.
- `Scripts/Gameplay/Units/TacticalUnit.cs`: model scale, animation, selection and terrain-following movement.
- `Scripts/Gameplay/Battlefield/Battlefield.cs`: selection, input, path/grid overlays, objective state and gameplay checks.
- `tools/assets/BuildStartingLevel.gd`: optional offline authoring generator. The game loads saved scenes/data; it does not regenerate terrain at startup.

## Editing

Open `Scenes/StartingLevel.tscn` in Godot. Select `Terrain3D` and sculpt or paint it with the add-on. Save with Ctrl+S. Its data directory is already configured. `LevelProps`, `Bridges`, `SpawnPoints` and `Objective` are editable in the same scene.

Terrain height changes are read directly during gameplay. Put obstacle props directly under `LevelProps` and set Boolean metadata `blocks_movement` to true; optional Float metadata `blocking_radius` sets the footprint radius in metres (default 0.58). A prop without this metadata is decorative. Position props after sculpting so their bases rest on the terrain. The runtime grid is centered at world (16,16), has 1.25m cells, and is 16 cells wide. Changing the level's root or Terrain3D transform is not supported by these map rules; move individual props instead.

Bridge surfaces are defined by the position and `bridge_size` Vector2 metadata on each child of `Bridges`. Their wooden deck top must agree with the bridge root's Y coordinate. Spawns use the named Marker3D nodes under `SpawnPoints`. The four center cells and `Objective` node define this prototype's capture site; moving the objective requires updating the capture-cell rule in `TacticalMap`.

The pre-change user scene and project settings were preserved under `SourceAssets/levels/backups/before_oathstone/`. The downloaded demo and reference copies remain untouched.

## Controls and verification

F5 runs the main game. Click a character, then a clear tile. WASD pans, the wheel zooms, G toggles the tactical grid, Home recenters, and right-click deselects.

`dotnet build PawnsOfDivinity.sln` checks compilation. The **PawnsOfDivinity Checks** profile runs gameplay checks covering equal objective routes, both flank bridges, water/prop blocking, terrain and bridge picking, model animations, unit occupancy, height-following movement and objective claim/contest.

For an optional baseline rebuild, run Godot with `--headless --path . --script tools/assets/BuildStartingLevel.gd`. **This overwrites StartingLevel.tscn and its generated terrain resources**, so preserve manual level edits first. The generator is not needed to run or edit the saved level.
