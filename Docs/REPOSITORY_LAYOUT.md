# Repository structure and asset migration

This is an organizational refactor of the clean checkpoint `79e3879`.
Gameplay logic, character definitions, registries, factories and namespaces are unchanged.
The repository root remains the single active Godot .NET project.

```text
Game/
  project.godot
  PawnsOfDivinity.sln
  PawnsOfDivinity.csproj
  Scripts/
    Core/Characters/           Definitions, stats, registry and factory
    Gameplay/
      Battlefield/            Battlefield.cs and TacticalMap.cs
      Units/                  TacticalUnit.cs
      Movement/               GridPathfinder.cs
  Scenes/                     Existing gameplay and level scenes
  Assets/                     Local runtime models, textures and terrain
  SourceAssets/               Local editable sources and original licences
  Tests/PawnsOfDivinity.Tests/ Independent xUnit project
  tools/
    assets/                   Synchronization, exports and level authoring
    blender/                  Model authoring, rig helpers and MCP bridge
    validation/               Inspections, checks and report generation
    archive/                  Preserved version-specific experiments
  Docs/
  addons/terrain_3d/          Installed third-party dependency
  References/Terrain3D/       Preserved third-party reference project
  demo/                       Original root Terrain3D examples
  artifacts/                  Local previews, logs and inspection results
```

Combat and UI folders should be introduced when those systems have independent
source files. The current HUD stays within Battlefield; this refactor does not
extract gameplay systems merely to populate empty directories.

## Character convention

Every existing character/shared-base folder has `source/`, `exports/`,
`references/`, `inspection/` and `archive/`. Use these for new work:

- `source/`: native Blender projects together with their texture and library dependencies.
- `exports/`: explicit, self-contained GLB/FBX exports.
- `references/`: concept art and visual references.
- `inspection/`: diagnostics and validation output.
- `archive/`: explicitly retired versions, including their dependencies.

All SourceAssets files remain local and ignored by Git. Empty convention folders
are local scaffolding; Git does not store them. This document records the convention
for other checkouts. Each local character also has a `LAYOUT.md`.

### Production export mapping

These seven source exports matched their existing runtime counterparts by SHA-256
before migration. All have embedded buffers/images with no external URIs. Their
bytes, animation data and runtime copies were preserved. No model was selected by age.

| Source under SourceAssets/characters | Runtime under Assets/Characters |
| --- | --- |
| hooded_assassin/exports/assassin_refined.glb | Assassin/Assassin.glb |
| ashveil/exports/ashveil.glb | Ashveil/Ashveil.glb |
| axe_warrior/exports/komodo_axe_warrior.glb | Komodo/Komodo.glb |
| forsaken_sister/exports/forsaken_sister.glb | ForsakenSister/ForsakenSister.glb |
| masked_ninja/exports/masked_ninja.glb | MaskedNinja/MaskedNinja.glb |
| broken_oath_knight/exports/broken_oath_knight.glb | BrokenOathKnight/BrokenOathKnight.glb |
| ash_scholar/exports/ash_scholar.glb | AshScholar/AshScholar.glb |

`tools/assets/asset-manifest.json` is the synchronization source of truth. Source
Blender files are intentionally not renamed to match game definition IDs; source
asset names and gameplay character IDs are separate concepts.

### Dependency exceptions: retained legacy paths

Existing `.blend`/`.blend1` projects and texture directories stay where they were.
Binary projects may contain relative textures, absolute image paths, linked
libraries, or saved script text. Moving only a project could silently detach these.
No Blender project was resaved, overwritten or repacked during this refactor.

For the same reason, these hooded-assassin bundles stay at their original paths:
`blendswap_assassin`, `base`, `face`, `modular`, `orignal_models`, `processed_models`,
`Full_model`, `animated_prototype`, all three `prototype_*` folders and
`inspection_*` bundles. Inspection bundles include disposable `.blend` files,
roundtrip GLBs and historical integrity reports that reference original paths.
Only the checksum-confirmed production GLB left `blendswap_assassin`.

Other existing native sources (including `mage.blend`, `knight.blend`,
`nigel.blend`, `native.blend`), root images, existing inspection bundles,
`_base/Eliber_Rigged_Base_Mesh`, `axe_warrior/New Folder` and
`gental_guardian/orignal_models` are also retained. Images were not assumed to be
disposable previews: a Blender material or reference plane may use them.
Historical JSON/Markdown asset reports were preserved verbatim, so their old
export locations describe the original inspection. Use the mapping above for
current exports. Misspellings remain where correcting them risks binary links.

Authoring tools that edit legacy native projects still write native files at the
legacy location, but their production GLB writers/checkers use `exports/`.
Start a new self-contained project under `source/` when creating future assets.
Existing historical scripts that expect absent `hooded_assassin/outfit/` versions
still require their original inputs; no prototype was substituted automatically.
The default assassin input in `inspect_two_blend_characters.py` is also the
historical `orignal_models/assassin.blend`; the available `assassin_tpose.blend`
is a different explicit input. Supply `SOURCE_PATH`, `CHARACTER`, `OUTPUT_BASE`
and, where needed, `SCENE_NAME` in the Blender execution context for a chosen
inspection. The refactor does not silently substitute either native project.

`demo/` and installed `addons/` remain untouched. The demo's scenes use their
original `res://demo/` paths, and the reference project has its own resource root.
Moving these third-party resources is outside the necessary organizational changes.

## Tests and Godot references

```powershell
dotnet build PawnsOfDivinity.sln
dotnet test Tests/PawnsOfDivinity.Tests/PawnsOfDivinity.Tests.csproj
python tools/validation/validate_repository.py
.\tools\assets\Sync-GodotAssets.ps1 -ValidateOnly
```

The test project remains independent of a running Godot editor. The game project
excludes `Tests/**/*.cs`; `Tests/.gdignore` prevents test resources from importing.
The four moved C# files retain their original `.cs.uid` sidecars. The Battlefield
scene references its new script path. Game namespaces and type names are unchanged.

Run the existing headless smoke checks after building:

```powershell
& '<Godot .NET console executable>' --headless --path . -- --smoke-test
```

## Git and local assets

`.gitignore` excludes all SourceAssets, runtime binary assets, previews, terrain
data, generated caches and third-party `bin/` directories. Small runtime `.tres`,
`.tscn` and shader resources remain tracked. Development tools and documentation
remain tracked; moving a script does not make it an ignored asset.
The previous Assets directory exception also exposed new PNGs to Git. The ignore
rule now excludes Assets contents while allowing directories and the explicit
small-resource exceptions. Previously committed textures, licences and import
settings remain tracked; no existing asset was removed from the index or disk.

A fresh GitHub checkout requires a separate local asset restore, including runtime
GLBs, terrain data/textures and Terrain3D native binaries. SourceAssets are not
distributed by Git. Preserve the project's existing local asset backup process.

`Docs/repository-moves.json` records file moves. The ignored local
`artifacts/repository_refactor/before.json` captures the original 1,577 asset hashes,
resource inventory, ignored paths and per-tool imports/I/O calls. Verify against it:

```powershell
python tools/validation/validate_repository.py --baseline artifacts/repository_refactor/before.json
```
