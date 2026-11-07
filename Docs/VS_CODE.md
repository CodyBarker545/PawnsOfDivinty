# VS Code

Open **D:\SoftwareEng\Game** in VS Code. This folder contains `project.godot`, the solution and `.vscode` configuration. Install Microsoft's **C#** extension (`ms-dotnettools.csharp`) when prompted. Use Godot .NET and the .NET 8 SDK selected by `global.json`.

## Edit and build

Open C# files in `Scripts/`. Press Ctrl+Shift+B to run **Build PawnsOfDivinity**, which builds the solution from the project root. Check View → Problems for compiler diagnostics; click a diagnostic to open its source line.

## Run and debug

Open Run and Debug, choose **PawnsOfDivinity Game**, and press F5. The profile builds first, then launches Godot with the project root passed explicitly. F9 toggles a breakpoint, F10 steps over, F11 steps into, F5 continues and Shift+F5 stops. Use the Debug Console for game output.

Choose **PawnsOfDivinity Checks** to build and launch headless checks. Success prints `GODOT C# SMOKE TEST PASSED` and exits; failure exits with code 1. The graphical VS Code debugger itself requires the C# extension and is not covered by these gameplay checks.

Both profiles in `.vscode/launch.json` reference this computer's Godot .NET executable. If you move Godot, update `program` in both profiles. Keep `cwd` and the `--path` argument as `${workspaceFolder}`.

## Level editing

Import the root `project.godot` in Godot .NET. Use Godot's 3D editor for `Scenes/Battlefield.tscn`; VS Code handles C# code. See [level editing](LEVEL_EDITING.md).

In Godot's Editor Settings, choose Dotnet → Editor → External Editor → Visual Studio Code to open C# files there.

Official reference: [Godot C# editor and debugger setup](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html#visual-studio-code).

## Character unit tests

Run `dotnet test Tests/PawnsOfDivinity.Tests/PawnsOfDivinity.Tests.csproj` from the repository root. The xUnit project runs independently of a live Godot editor and is excluded from the runtime game assembly. See [repository layout](REPOSITORY_LAYOUT.md).
