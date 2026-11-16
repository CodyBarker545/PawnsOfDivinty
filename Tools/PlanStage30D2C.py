"""Read-only gameplay preflight; snapshot and publish coordinate plan before mutation."""
from pathlib import Path
from collections import deque
import json,hashlib,shutil
R=Path(__file__).resolve().parents[1];S=R/'SourceAssets/Environment/ForwardCastleStairs';B=S/'Baseline';B.mkdir(parents=True,exist_ok=True)
data=json.loads((R/'artifacts/stage30d2c/before_layout.json').read_text());elements=data['Elements'];floors={tuple(map(int,(e['Id'].split('_')[1],e['Id'].split('_')[2]))):e for e in elements if e['Kind']=='Terrain'}
blocked=set()
for e in elements:
 if e['Kind'] in ['Terrain','Obstacle'] and e['BlocksMovement']:
  x=round((e['X']+2.125)/1.25);y=round((e['Z']-4.125)/1.25);blocked.update((x+c['X'],y+c['Y']) for c in e['CellOffsets'])
old={4,5,24,25};new={8,9,20,21};levels={p:e['Elevation'] for p,e in floors.items()};proposed=levels.copy();nb=blocked.copy()
for x in old:nb.add((x,15));proposed[x,15]=0;proposed[x,16]=-3
for x in new:nb.remove((x,15));proposed[x,15]=-1;proposed[x,16]=-2
def paths(start,b,l):
 d={start:0};q=deque([start])
 while q:
  p=q.popleft()
  for a in [(p[0]-1,p[1]),(p[0]+1,p[1]),(p[0],p[1]-1),(p[0],p[1]+1)]:
   if a in l and a not in b and a not in d and abs(l[a]-l[p])<=1:d[a]=d[p]+1;q.append(a)
 return d
assert len(nb)==len(blocked)==86 and len(paths((1,8),nb,proposed))==514
assert all(proposed[x,y]==proposed[29-x,y] and ((x,y) in nb)==((29-x,y) in nb) for x,y in proposed)
costs=[]
for side,gate,camp in [('west',(8,14),(8,17)),('east',(21,14),(21,17))]:
 costs.append({'side':side,'gate_to_farm_before':paths(gate,blocked,levels)[camp],'gate_to_farm_after':paths(gate,nb,proposed)[camp]})
assert costs[0]['gate_to_farm_after']==costs[1]['gate_to_farm_after']==3
for folder in ['Scenes/Battlefields/CliffsideArena','Scripts','Tests','Assets/Levels/Environment','SourceAssets/Environment/WesternStairSlice','SourceAssets/Environment/ConnectedCliff','SourceAssets/Environment/LowerTerrace','SourceAssets/Environment/EasternStair','SourceAssets/Environment/FortStairIntegration']:
 for p in (R/folder).rglob('*'):
  if p.is_file() and p.suffix in ['.tscn','.tres','.cs','.blend','.glb','.png'] and not any(k in p.parts for k in ['bin','obj','Baseline','QA']):
   dst=B/p.relative_to(R)
   if p.suffix=='.cs':dst=dst.with_name(dst.name+'.snapshot')
   if not dst.exists():dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst)
hashes={p.relative_to(B).as_posix().removesuffix('.snapshot'):hashlib.sha256(p.read_bytes()).hexdigest() for p in B.rglob('*') if p.is_file()}
(S/'Preflight.json').write_text(json.dumps({'costs':costs,'blocked_before':86,'blocked_after':86,'reachable_after':514,'original_hashes':hashes},indent=2))
doc='''# Stage 3.0D.2C — Forward-castle stairs: pre-change plan

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
'''
(R/'Docs/Stage30D2CLayoutPlan.md').write_text(doc)
print('PREFLIGHT COMPATIBLE:',costs,';86 blocked,514 reachable;16 terrain records affected. Coordinate plan saved; gameplay files not edited.')
