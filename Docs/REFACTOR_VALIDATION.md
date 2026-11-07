# Organizational refactor verification

Baseline: clean Git checkpoint `79e3879` (`Checkpoint game code without large 3D assets`).
No gameplay architecture or character definitions were changed.

| Check | Result |
| --- | --- |
| Baseline xUnit project | 63 passed |
| Relocated solution build | Passed, zero warnings and errors |
| Relocated standalone xUnit project | 63 passed |
| C# sources and original UID sidecars | 23 files unchanged, allowing Git/Windows line-ending normalization |
| Original SourceAssets and Assets files | All 1,577 original files verified against pre-move SHA-256 and size |
| Production source/runtime character exports | Seven byte-identical pairs; embedded GLB dependencies |
| Asset synchronization | 23 exports validated; zero copies needed |
| Synchronization fixture checks | Missing source aborts before writes; ValidateOnly and WhatIf preserve files/directories; copying preserves bytes; second run skips identical exports |
| Python syntax | All 84 current Python utilities parsed successfully |
| Local game resource paths | All 14 literal paths in Scripts, Scenes and small runtime resources resolve |
| Godot headless editor import | Completed and refreshed moved resources; original gameplay UID sidecars retained |
| Headless gameplay smoke test | Passed before and after refactor |
| SourceAssets Git tracking | No SourceAssets files tracked |
| Moved development tools | None excluded by Git ignore rules |

Build/test checks used the installed .NET 8 SDK and Godot 4.7.2 .NET. Sandboxed
xUnit could not connect to its test host, so successful test runs were performed
outside the sandbox. Godot checks also ran outside the sandbox.

The existing physics-interpolation deprecation warning appeared in both gameplay
runs. The headless editor also reported cleanup/resource-leak messages on exit;
it completed import and exited with code zero. The subsequent game smoke process
exited successfully without resource-path errors.

No model-building, rig-editing, texture-painting or asset-regeneration tools were
executed as validation. Their file paths and syntax were reviewed, but tools that
require particular Blender scenes, MPFB installations or historical inputs still
require those dependencies. Known path exceptions and missing historical inputs
are described in [REPOSITORY_LAYOUT.md](REPOSITORY_LAYOUT.md).

The ignored local `artifacts/repository_refactor/` holds the baseline inventory,
checksum manifest, complete migration map, Godot logs and synchronization fixtures.
`Docs/repository-moves.json` is the portable map of source/tool/export moves.

Changes are left uncommitted for review. All large local assets remain on disk.
