# Work on PawnsOfDivinity in Visual Studio

Use Visual Studio with the **.NET desktop development** workload and the Godot **.NET** editor. This project targets .NET 8 and Godot 4.7.2; `global.json` selects an installed .NET 8 SDK.

## Open and inspect the code

1. Open `PawnsOfDivinity.sln` from the project root using **File → Open → Project/Solution**.
2. In Solution Explorer, expand **PawnsOfDivinity → Scripts**.
3. Open `Battlefield.cs` for grid creation, click input, camera controls, selection and the smoke checks.
4. Open `TacticalUnit.cs` for character animation and movement.
5. Open `GridPathfinder.cs` for the grid route calculation.

Press **Ctrl+Shift+B** to build. Open **View → Error List** for compiler errors and warnings. Double-click an entry to go to the source line. Use **View → Output**, then choose **Build**, to see the build log. A successful build checks compilation; it does not replace running the gameplay checks.

## Run and debug the game

1. Right-click **PawnsOfDivinity** in Solution Explorer and choose **Set as Startup Project** if needed.
2. Set the configuration to **Debug**.
3. In the launch-profile dropdown beside the green Start button, choose **PawnsOfDivinity Game**.
4. Press **F5** to build and run under the debugger. **Ctrl+F5** runs without debugging.
5. Click a character and then an empty tile. WASD moves the camera, the mouse wheel zooms, and right-click deselects.

The project is a Godot class library. Visual Studio launches the Godot executable, which loads the compiled C# assembly and runs the scene. It does not run the DLL as a console application.

`Properties/launchSettings.json` already points to the .NET Godot executable found on this computer. If you move Godot, open **Project → Properties → Debug → Open debug launch profiles UI**, choose the profile and update **Executable**. Select the main `.exe`, not `_console.exe`. Keep **Working directory** as `.` and **Command line arguments** as `--path .`. The game must run from the project root, beside `project.godot`.

Both profiles enable native debugging as recommended by Godot's Visual Studio guide. If Visual Studio reports a missing native debugger, install its C++ debugging tools through Visual Studio Installer, or turn off native debugging when only managed C# debugging is needed.

To inspect a running method, click in the left gutter or press **F9** to set a breakpoint, then press F5. For example, put one in `Battlefield.MoveSelected`, click a unit and then a tile, and inspect `_selected` and `cell`. Use **F10** to step over, **F11** to step into and **F5** to continue. **Shift+F5** stops the game.

## Run the checks

Choose the **PawnsOfDivinity Checks** launch profile and press F5. This starts Godot headlessly, runs the C# checks and exits. The checks verify imported models and idle/walk clips, character colliders, selection, independent movement, occupied-tile rejection and pathfinding. A failure exits with code 1; success prints `GODOT C# SMOKE TEST PASSED`.

You can also run them in **View → Terminal**, starting in the project root:

```powershell
dotnet build PawnsOfDivinity.sln
& 'D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --smoke-test
```

The console executable is useful for terminal logs; the Visual Studio debugging profiles use the main executable.

## Edit levels

Use Godot's editor to edit `Scenes/Battlefield.tscn`, not Visual Studio's debugger. Open `Scenes/StartingLevel.tscn`, select Terrain3D to sculpt, or place models under LevelProps. Save the scene and terrain before running. Visual Studio is for C# code and debugging; Godot is for scenes, imports, lighting and level layout. The new terrain-aware map rules read elevations and bridge decks; props with blocks_movement metadata obstruct routes. See STARTING_LEVEL.md for editing details.

In Godot, choose **Editor → Editor Settings → Dotnet → Editor → External Editor → Visual Studio** if you want C# scripts to open there when double-clicked.

Official references: [Godot C# and Visual Studio setup](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html#visual-studio-windows-only) and [Microsoft's .NET debug-profile settings](https://learn.microsoft.com/en-us/visualstudio/debugger/project-settings-for-csharp-debug-configurations-dotnetcore).

## Character unit tests

Run `dotnet test Tests/PawnsOfDivinity.Tests/PawnsOfDivinity.Tests.csproj` from the repository root. The xUnit project runs independently of a live Godot editor and is excluded from the runtime game assembly. See [repository layout](REPOSITORY_LAYOUT.md).
