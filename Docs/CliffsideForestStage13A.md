# Cliffside Arena · Stage 1.3A

Open the CliffsideArena scene and press **F6**. Coordinates are zero-based, with north at Y0. Row 3 is the continuous east–west forest trail. Seven routes connect it to the middle, either through the open approaches or through groups of traversable trees and bushes.

| Connection | X | Y, inclusive | Exit |
|---|---|---|---|
| Clear west and east entrances | 4–5 and 24–25 | 7–9 | Outside the forward forts |
| Clear central entrance | 14–15 | 4–9 | North face of the objective court |
| West and east fort groves | 8–9 and 20–21 | 4–8 | Paired north fort gates at Y8 |
| West and east brush passages | 12 and 17 | 4–9 | Between the forts and ruins |

The grove and brush routes carry vegetation through Y7. Their southern ends join existing middle-terrace tiles. The two-wide main entrances retain clear approaches from Y6. A unit can turn off a marked route anywhere the neighboring tile is legal.

## Upper tile plan

Each character represents one tile. `T` is tree, `b` is bush and `.` is open grass. All three cost one MOV per legal orthogonal step. `R` is solid rock and `W` is a solid wall. `h` marks existing home terrain outside the forest work.

```text
X  000000000011111111112222222222
   012345678901234567890123456789
0  ..............................
1  .Tbb...TbbT.bTbbTb.TbbT...bbT.
2  .bTTT..bTbbTbbTTbbTbbTb..TTTb.
3  ..............................
4  ........bb.bb....bb.bb........
5  ..T..TTbTbWTb....bTWbTbTT..T..
6  ..T...RRbTWbTR..RTbWTbRR...T..
7  hhhh....bb.bb....bb.bb....hhhh
```

The board contains 36 tree tiles and 50 bush tiles. Vegetation meshes are children of the tile that owns their terrain tag. Their mesh-only placeholders leave room for the unit at the tile center. Living units still block ordinary movement.

Rocks occupy X6, 7, 13, 16, 22 and 23 on Y6. Walls occupy X10 and 19 on Y5–6. The [tile manifest](CliffsideArenaTiles.csv) records every cell and its independent movement and sight flags.

**Sight policy.** The fourteen former solid-tree positions retain their existing sight flags. Added foliage and rock/wall cells use the prior sight state at their positions. A tree's terrain tag alone therefore does not determine whether a shot crosses it. A uniform vegetation sight policy remains a future design decision. Evasion uses the existing combat rules.

The map uses the scene-authored board reader and the MOBA movement adapter. TacticalMap serves the separate Terrain3D prototype. Native scene round-trip, passage movement, mirrored spawn costs, occupied cells, stair traversal, castle transfers and objective regressions are included in the full suite.

[Overhead screenshot](../artifacts/stage13a_overhead.png) · [Angled gameplay screenshot](../artifacts/stage13a_gameplay.png) · [Test results](../artifacts/stage13a_tests/forest.trx)

## Live Godot verification

Launched CliffsideArena from the Godot editor with F6. A three-MOV Time Weaver move from (2,8) passed through the tree at (2,6) and finished on the tree at (2,5). In the existing untimed test mode, the same unit entered the west opening at (5,6). With five MOV available, the adjacent rock at (6,6) rejected a click. A five-MOV path through mixed vegetation then reached the bush at (9,5). With a fresh allowance, the adjacent wall at (10,5) rejected movement. The unit then crossed the grove and entered the west fort at (9,10) for five MOV. The HUD reconciled the move and enabled Castle Transfer. The saved scene launches with normal competitive turns.

[Wall rejection](../artifacts/stage13a_wall_check.png) · [Arrival through the grove](../artifacts/stage13a_grove_exit.png)
