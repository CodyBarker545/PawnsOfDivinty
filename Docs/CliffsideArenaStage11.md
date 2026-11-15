# Cliffside Arena · Stage 1.1

Open `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn` in Godot 4.7 .NET and press **F6** for a competitive 5v5 match. F5 still opens the original encounter. The arena uses alternating activations and the existing first-to-15-VP scoring profile.

## Before and after

The project startup scene uses Battlefield.cs and TacticalMap.cs to sample a 14×14 Terrain3D encounter. The separate MOBA beta already has the required 30×20 board, authored tile reader, pathfinder, ten character fixtures, camera, HUD, castle control, and scoring. Stage 1.1 uses that implementation in a separate native scene. The beta's two owned forts, spawn-only courtyards and open flat southern flank become two neutral forts, two permanent home hubs and a lower farming terrace with two stairways. All 514 walkable cells belong to one connected component.

## Placement and travel

[Tile manifest](CliffsideArenaTiles.csv) lists all 600 coordinates, terrain tags, elevation levels, movement and sight blockers, spawns, castles, objectives, camps and connections. X runs west to east from 0 to 29. Row 0 is north. Tile centers are at world X = −2.125 + 1.25x and Z = 4.125 + 1.25y. Each elevation level is 0.6 world units. Mirror X about 14.5.

| Feature | West | East |
|---|---|---|
| Home courtyard | X0–3, Y7–13 | X26–29, Y7–13 |
| Five initial units | X2, Y8–12 | X27, Y8–12 |
| Home transfer origin / arrival | X1 / X3, Y9–11 | X28 / X26, Y9–11 |
| Forward fort | X6–11, Y8–14 | X18–23, Y8–14 |
| Forward capture | X8–9, Y10–11 | X20–21, Y10–11 |
| Forward arrival | X7–9, Y12–13 | X20–22, Y12–13 |
| Forest rotation passages | X4–5, Y7–9 | X24–25, Y7–9 |
| Two-wide stairs | X4–5, Y15 | X24–25, Y15 |
| Lower camps | X8–9, Y17–18 | X20–21, Y17–18 |

The north path runs through rows 3–4. The middle terrace has four objective tiles at X14–15, Y10–11, with blocked ruin corners at X13/16 and Y8/13. Trees, ruins and castle wall proxies obstruct direct attack sightlines. The check traces tile centers and excludes corner-only touches. Smoke, range, accuracy, damage and character stats use the existing systems.

The main terrace is level 0. Row 15 is a blocked cliff except at the four stair cells at level −1. Rows 16–19 are level −2. Ordinary movement costs one MOV per legal cardinal step, including stairs. The trip from (14,17) to (14,10) must detour to an end stair. Teleports retain their own eligibility checks.

| Spawn row on either team | Nearest friendly-side fort pad | Nearest objective tile | Team-side lower midpoint |
|---|---:|---:|---:|
| 8 | 8 | 14 | 21 |
| 9 | 7 | 13 | 20 |
| 10 | 6 | 12 | 19 |
| 11 | 6 | 12 | 18 |
| 12 | 7 | 13 | 17 |

Costs are MOV with the other nine units in their opening positions. The lower midpoint is X14 for Blue and X15 for Red at Y17. Forward forts begin neutral. A scout can take the nearby fort, then a tank can step onto the home origin pad and transfer on its own activation. Both ends must be friendly and uncontested, and the arrival tile must be empty. Transfer spends the Main Action and ends the activation.

## Assets for later production

| Placeholder | Placement | Future model and texture |
|---|---|---|
| Faction gatehouses and towers | West and east home courtyards and lateral plinths | Pale stone and teal roofs at Aether Spire. Dark stone, timber and copper roofs at Forgebound Ridge. |
| Fort walls, pads and objective | Forward footprints and central four tiles | Modular masonry, capture glyphs and ownership banners |
| Forest and underbrush | Upper route, with exact blocking tiles in the manifest | Tree canopies, trunks, shrub clusters and grass |
| Ruin blocks | Central corners and lower terrace | Broken columns, wall fragments and weathered stone |
| Cliff caps, cliff faces and stairs | Row 15 and southern outer edge | Modular rock faces, stair treads and landings |
| Camp shelters and sentinel figures | Both lower camp clearings | Neutral encounter models, shelter props and worn earth |
| Terrace floors | All 600 grid cells | Grass, dirt paths, pavers and faction courtyard materials |

Camp figures mark intended encounters. NPC behavior, rewards and farming economy await design. Human match balance and the value of leaving the center to farm remain unverified.

## Verification

The pre-change solution build succeeded without warnings or errors. The sandboxed pre-change test attempt produced no usable totals. The final suite runs all 3,278 existing tests and three cliffside integration tests. All 3,281 passed. Checks cover mirrored terrain and movement costs, reachable objectives, cliff restrictions, forest and stair access, native scene save/reload, floor picking, home immunity and the scout-to-tank transfer sequence.

[Overhead view](../artifacts/stage11_overhead.png) · [Gameplay view](../artifacts/stage11_gameplay.png) · [Full test results](../artifacts/stage11_tests/stage11.trx)
