"""Compare native before/after metadata, competitive routes, originals and export completeness."""
from pathlib import Path
from collections import deque
import json,hashlib,struct
R=Path(__file__).resolve().parents[1];S=R/'SourceAssets/Environment/ForwardCastleStairs'
before=json.loads((R/'artifacts/stage30d2c/before_layout.json').read_text());after=json.loads((R/'artifacts/stage30d2c/after_layout.json').read_text());old={e['Id']:e for e in before['Elements']};new={e['Id']:e for e in after['Elements']}
assert before['Board']==after['Board'] and before['RequireMirroredLayout']==after['RequireMirroredLayout']
expected={f'tile_{x}_{y}' for x in [4,5,24,25,8,9,20,21] for y in [15,16]}
changes=[key for key in old.keys()&new.keys() if old[key]!=new[key]];assert set(changes)==expected,changes
for key in changes:
 a,b=old[key],new[key]
 assert {k for k in a if a[k]!=b[k]}<= {'TerrainTag','Elevation','BlocksMovement'}
assert old.keys()-new.keys()=={'west_outer_flank','east_outer_flank'}
assert new.keys()-old.keys()=={'west_forward_castle_jungle','east_forward_castle_jungle'}
assert all(old[k]==new[k] for k in old.keys()&new.keys() if k not in expected)
def grid(doc):
 levels={};blocked=set()
 for e in doc['Elements']:
  x=round((e['X']+2.125)/1.25);y=round((e['Z']-4.125)/1.25)
  if e['Kind']=='Terrain':levels[x,y]=e['Elevation']
  if e['Kind'] in ['Terrain','Obstacle'] and e['BlocksMovement']:blocked.update((x+c['X'],y+c['Y']) for c in e['CellOffsets'])
 return levels,blocked
def routes(start,levels,blocked):
 seen={start:0};q=deque([start])
 while q:
  x,y=q.popleft()
  for p in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
   if p in levels and p not in blocked and p not in seen and abs(levels[p]-levels[x,y])<=1:seen[p]=seen[x,y]+1;q.append(p)
 return seen
levels,blocked=grid(after);prev_levels,prev_blocked=grid(before);assert len(blocked)==86 and len(routes((1,8),levels,blocked))==514
assert all(levels[x,y]==levels[29-x,y] and ((x,y) in blocked)==((29-x,y) in blocked) for x,y in levels)
costs=[]
for side,x,camp in [('west',8,(8,17)),('east',21,(21,17))]:
 a=routes((x,14),prev_levels,prev_blocked);b=routes((x,14),levels,blocked);costs.append({'side':side,'gate_to_farm_before':a[camp],'gate_to_farm_after':b[camp]})
assert costs[0]['gate_to_farm_after']==costs[1]['gate_to_farm_after']==3
fixture_costs=[]
for e in after['Elements']:
 if e['Kind']!='Fixture':continue
 start=(round((e['X']+2.125)/1.25),round((e['Z']-4.125)/1.25));camp=(8,17) if e['TeamId']==1 else (21,17)
 fixture_costs.append({'id':e['Id'],'start':start,'camp':camp,'before':routes(start,prev_levels,prev_blocked)[camp],'after':routes(start,levels,blocked)[camp]})
originals=json.loads((S/'Preflight.json').read_text())['original_hashes'];changed=[p for p,h in originals.items() if hashlib.sha256((R/p).read_bytes()).hexdigest()!=h]
allowed={'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn','Scripts/Gameplay/Levels/MobaBetaBattlefield.cs','Tests/PawnsOfDivinity.Tests/Levels/CliffsideArenaTests.cs','Tests/PawnsOfDivinity.Tests/Levels/Stage30BWesternStairTests.cs','Tests/PawnsOfDivinity.Tests/Levels/Stage30CEasternStairTests.cs'}
assert set(changed)==allowed,changed
raw=(R/'Assets/Levels/Environment/ForwardCastleStairs/CastleCliffJunction.glb').read_bytes();assert struct.unpack_from('<I',raw,8)[0]==len(raw);n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);assert len(g['meshes'])==5 and not g.get('images')
report={'terrain_records_changed':sorted(changes),'unchanged_metadata_records':len(old)-len(changes)-2,'connection_ids_replaced':2,'castles_captures_transfers_camps_fixtures_unchanged':True,'blocked':86,'walkable_connected':514,'gate_costs':costs,'fixture_to_farm_costs_without_occupancy':fixture_costs,'protected_originals_unchanged':len(originals)-len(changed),'allowed_changed_originals':changed,'glb_bytes':len(raw),'glb_embedded_images':0,'before_layout_sha256':hashlib.sha256((R/'artifacts/stage30d2c/before_layout.json').read_bytes()).hexdigest(),'after_layout_sha256':hashlib.sha256((R/'artifacts/stage30d2c/after_layout.json').read_bytes()).hexdigest()}
(S/'Validation.json').write_text(json.dumps(report,indent=2));print('STAGE3.0D.2C AUDIT PASSED:',len(changes),'terrain records,2 route connections;',len(originals)-len(changed),'original files unchanged; mirrored514-cell connected map;',costs)
