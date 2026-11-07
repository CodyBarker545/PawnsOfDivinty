# PawnsOfDivinity

Godot 4.7.2 .NET / C# tactical-game prototype. **This repository root is the game project.** Open this folder in VS Code, `PawnsOfDivinity.sln` in Visual Studio, or `project.godot` in Godot .NET. There is one active Godot project. Python and Blender are optional asset-development tools; the game has no Python runtime dependency.

## Project layout

```text
Game/
  project.godot             Godot project; main scene is Battlefield
  PawnsOfDivinity.sln          Visual Studio solution
  PawnsOfDivinity.csproj       C# game assembly
  Scripts/Core/            Character definitions, stats, registry and factory
  Scripts/Gameplay/        Battlefield, units and movement
  Tests/                   Independent xUnit test project
  Scenes/                  Godot scenes and level layouts
  Assets/
    Characters/            Animated character models used by the game
    Levels/                Trees, rocks and other environment models
  SourceAssets/            Editable Blender files, downloads and licences
  Properties/              Visual Studio run/check profiles
  .vscode/                 VS Code build/run/check profiles
  tools/                   assets/, blender/, validation/ and archive/
  Docs/                    Editor and gameplay guides
  References/Terrain3D/    Preserved third-party demo, excluded from game imports
  artifacts/               Historical previews and verification logs
```

`SourceAssets`, `References`, tooling and documentation have `.gdignore` files so Godot imports only game resources. Editable sources are preserved alongside the game, without importing duplicate Blender files and old previews. Generated `.godot`, `.vs` and package caches are excluded from Git.

## Work in VS Code

1. Open **D:\SoftwareEng\Game** using File → Open Folder.
2. Install the recommended Microsoft **C#** extension.
3. Open files under `Scripts/` and press **Ctrl+Shift+B** to build. Compiler diagnostics appear in Problems.
4. Choose **PawnsOfDivinity Game** in Run and Debug and press **F5**. Set breakpoints with **F9**, step over with **F10**, and stop with **Shift+F5**.
5. Choose **PawnsOfDivinity Checks** to build and run the headless gameplay checks.

The executable path in `.vscode/launch.json` points to Godot .NET installed on this computer. Update it if Godot moves. See [the VS Code guide](Docs/VS_CODE.md).

## Work in Visual Studio

Open `PawnsOfDivinity.sln`, select **Debug → PawnsOfDivinity Game**, then press **F5**. **Ctrl+Shift+B** builds. See [the Visual Studio guide](Docs/VISUAL_STUDIO.md).

## Build levels

Import **D:\SoftwareEng\Game\project.godot** in Godot .NET. Remove stale `godot/` or `oath-breaker/` entries from the Project Manager list if they remain there; those are the old locations.

Open `Scenes/StartingLevel.tscn` to sculpt its Terrain3D terrain or edit its trees, rocks, bridges and central shrine. Add models under `LevelProps`, position and scale them, then save with Ctrl+S. Press F5 to play the main battlefield scene. Godot is the level editor; VS Code and Visual Studio edit/debug C#.

The starting level is Oathstone Crossing: a compact 16x16 map, opposing spawns, raised ground, a creek, two bridges and a claimable central Oathstone. Movement follows Terrain3D heights and respects water, obstacles and bridge decks. Terrain3D is enabled under `addons/terrain_3d`; the original reference remains preserved under `References/Terrain3D`. See [level editing](Docs/LEVEL_EDITING.md) and [the starting-level guide](Docs/STARTING_LEVEL.md).

## Assets

Keep editable work and its textures together under `SourceAssets/`. Production character GLBs live in each character's `exports/` folder. The explicit mapping in `tools/assets/asset-manifest.json` preserves the existing production choices. Run this from the repository root:

```powershell
.\tools\assets\Sync-GodotAssets.ps1
```

This preflights seven character exports and the environment GLBs, then refreshes changed files in `Assets/`. Use `-ValidateOnly` to check inputs without writes or `-WhatIf` to preview copying. Original licences remain with the local source assets.

See [development tools](tools/README.md) and [repository layout and asset exceptions](Docs/REPOSITORY_LAYOUT.md). All `SourceAssets/` files and large runtime assets remain local and ignored by Git. A fresh checkout needs these assets and Terrain3D native binaries restored separately.

## Build and verify

From this root folder:

```powershell
dotnet build PawnsOfDivinity.sln
dotnet test Tests/PawnsOfDivinity.Tests/PawnsOfDivinity.Tests.csproj
python tools/validation/validate_repository.py
& 'D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --smoke-test
```

Checks cover balanced objective routes, bridges, terrain/bridge picking, blocked water and trees, imported animations, occupancy, terrain-following movement and objective claim/contest. A failure exits with code 1.

Click a character, then an empty tile to move. WASD pans, the wheel zooms, G toggles the grid, Home recenters and right-click deselects. Combat, turns, fog, scoring and online play remain future work. See [the game overview](Docs/GAME.md).

## Repository archive

See [publication notes](PUBLICATION.md) for the source inventory, configuration notes, and verification status of this upload.
