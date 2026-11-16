# Stage 3.0D.2C — Forward-castle stairs: pre-change plan

Verified against the native authored scene before changing gameplay geometry, 2026-11-16. All coordinates below are zero-based. Castle footprints stay X6–11 /18–23, Y8–14.

```text
                    WEST                              EAST
X columns       4 5 | 8 9                         20 21 |24 25
Y13 courtyard   . . | L L                         L  L  | . .
Y14 upper gate  U U | G G                         G  G  | U U
Y15 cliff       O O | N N                         N  N  | O O
Y16 descent     o o | n n                         n  n  | o o
Y17 lower lane  F F | F F                         F  F  | F F

G = unchanged southern gate + upper landing, level0
L = existing interior courtyard/deployment approach, level0
O -> blocked cliff level0; o -> ordinary lower floor level-3
N = new stair level-1; n = new stair level-2
F = existing lower landing/lane level-3
```

| Feature | West | East |
|---|---|---|
| Old stair cells to retire | X4–5,Y15–16 | X24–25,Y15–16 |
| New stair cells | X8–9,Y15–16 | X20–21,Y15–16 |
| Southern gate/upper landing, unchanged | X8–9,Y14 | X20–21,Y14 |
| Lower landing, unchanged floor level | X8–9,Y17 | X20–21,Y17 |
| Capture footprint, untouched | X8–9,Y10–11 | X20–21,Y10–11 |
| Transfer landings, untouched | X7–9,Y12–13 | X20–22,Y12–13 |

New world stair openings: X7.25…9.75 /22.25…24.75, Z22.25…24.75. The existing south gates align exactly: no castle-footprint or entrance relocation required. Four new row15 cells open while four old row15 cells close; row16 floor elevations swap correspondingly. Sixteen terrain records change; 86 blocked /514 walkable cells remain. Read-only preflight confirms all514 walkable cells remain connected, mirrored terrain/elevation/blockers match, and each gate-to-lower-landing path costs3 legal steps under the existing cardinal/elevation rule.

The lower landings are already part of the neutral camp clearings. Camps are traversable, have no ownership restriction and no NPC blockers; their footprints/mechanics remain unchanged. Capture and transfer regions do not overlap the stairs. Stair access remains physical traversal regardless of fort ownership. Existing sight flags remain unchanged (the cliff terrain currently blocks movement but does not independently block sight).

Update the two old lower-flank connection records to identify the mirrored castle-to-farm corridors; preserve the forest-to-middle connections. Restore continuous cliff/ground at the old openings, remove old stair/paved-route remnants, cut matching openings in the existing connected cliff and lower-ground visual mesh, and relocate reused weathered masonry to the south gates. Keep originals in the task-start backup for rollback. No paid asset generation.
