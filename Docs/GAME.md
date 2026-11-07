# PawnsOfDivinity Godot and C sharp game

This is the active game project. Open **PawnsOfDivinity.sln** in Visual Studio for code and debugging, or import **project.godot** in the Godot 4.7.2 .NET editor for scene and level editing.

[Visual Studio setup and controls](VISUAL_STUDIO.md)

## Game

The C# prototype contains a 16x16 Terrain3D battlefield with opposing spawns, hills, a creek, two bridges and a central capture objective, Assassin and Ashveil, imported materials and animations, click-to-move, occupied-tile blocking and a tactical camera angled down about 37 degrees. Click a character and then an empty tile. WASD pans, the wheel zooms and right-click deselects.

## Files

- `Scripts/Gameplay/Battlefield/Battlefield.cs`: grid, click input, selection, camera and runtime checks.
- `Scripts/Gameplay/Units/TacticalUnit.cs`: character imports, animation and movement.
- `Scripts/Gameplay/Movement/GridPathfinder.cs`: shortest grid paths.
- `Scenes/Battlefield.tscn`: the native Godot level scene.
- `Assets/Characters/`: the two animated runtime GLBs.
- `Assets/Levels/`: foliage and stone GLBs.
- `Properties/launchSettings.json`: Visual Studio game and headless-check profiles.

Editable Blender sources and original licences live in `SourceAssets/` at the project root. Refresh runtime exports using `tools/assets/Sync-GodotAssets.ps1` from the root. Godot's generated `.godot/` cache and Visual Studio's `.vs/` files are ignored by Git.

## Levels

Open `Scenes/StartingLevel.tscn` in Godot to sculpt terrain and edit its props, bridges, spawns and objective. `Scenes/Battlefield.tscn` loads it and provides the camera, lights and gameplay. Movement reads Terrain3D heights and blocks water and authored obstacles. See [the starting-level guide](STARTING_LEVEL.md).

## Build and checks

Use the Godot .NET editor and a .NET 8 SDK. `global.json` selects .NET 8, and the project uses Godot.NET.Sdk 4.7.2. `NuGet.Config` supports official NuGet and the ignored local package directory used for Godot-bundled packages.

```powershell
dotnet build PawnsOfDivinity.sln
```

In Visual Studio, choose **PawnsOfDivinity Checks** and press F5 to run the headless checks. Choose **PawnsOfDivinity Game** to run the game. The earlier Python/Ursina implementation has been removed.

## Character unit tests

Run `dotnet test Tests/PawnsOfDivinity.Tests/PawnsOfDivinity.Tests.csproj` from the repository root. The xUnit project runs independently of a live Godot editor and is excluded from the runtime game assembly. See [repository layout](REPOSITORY_LAYOUT.md).
