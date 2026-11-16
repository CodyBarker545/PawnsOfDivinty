"""Snapshot and surgically integrate only the eastern decorative staircase."""
from pathlib import Path
import hashlib,json,re,sys,shutil
R=Path(__file__).resolve().parents[1];S=R/'SourceAssets/Environment/EasternStair';B=S/'Baseline';B.mkdir(parents=True,exist_ok=True)
scene=R/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn'
if not (B/'CliffsideArena.tscn').exists():
 shutil.copy2(scene,B/'CliffsideArena.tscn')
 protected={}
 for folder in ['Scripts','Tests','Assets/Levels/Environment/WesternStairSlice','Assets/Levels/Environment/ConnectedCliff','Assets/Levels/Environment/LowerTerrace','SourceAssets/Environment/WesternStairSlice','SourceAssets/Environment/ConnectedCliff','SourceAssets/Environment/LowerTerrace']:
  for p in (R/folder).rglob('*'):
   if p.is_file() and p.suffix in ['.cs','.tscn','.tres','.blend','.glb','.png'] and not any(k in p.parts for k in ['bin','obj','Baseline','QA']):protected[p.relative_to(R).as_posix()]=hashlib.sha256(p.read_bytes()).hexdigest()
 (B/'ProtectedHashes.json').write_text(json.dumps(protected,indent=2))
if '--integrate' in sys.argv:
 # Work against current scene; do not rebuild it from an earlier baseline.
 t=scene.read_text();chunks=re.split(r'(?=\[node )',t);hidden=[]
 for i,c in enumerate(chunks):
  m=re.match(r'\[node name="(Tread\d+)".*parent="Terrain/tile_(24|25)_(15|16)"',c)
  if m:
   name,x,y=m.groups();hidden.append(f'Terrain/tile_{x}_{y}/{name}')
   if not re.search(r'^visible = false$',c,re.M):chunks[i]=c.replace('\n','\nvisible = false\n',1)
 # Preserve InputMap/logical grid. Fit only the two coarse eastern row-15 visual
 # column wires to the already-authored five tread heights; the existing batcher
 # still reduces every BoxMesh to its original one shared runtime surface.
 grid_edits=[]
 for i,c in enumerate(chunks):
  m=re.match(r'\[node name="(Column_(?:24|25)_15|Row_24_15)".*parent="GridOverlay"',c)
  if not m:continue
  name=m[1]
  if name=='Row_24_15':c=c.replace('-0.58700','-0.10700')
  else:
   c=c.replace('-0.58700, 22.87500','-0.10700, 22.37500').replace('0.01200, 1.25000','0.01200, 0.25000')
  chunks[i]=c;grid_edits.append('GridOverlay/'+name)
 t=''.join(chunks)
 for col,x in [(24,27.257),(25,28.507)]:
  for i,h in enumerate([-.12,-.36,-.6,-.74,-.86]):
   if i==0:continue
   name=f'EasternStairGrid_{col}_15_{i}'
   if f'name="{name}"' not in t:t+=f'\n[node name="{name}" type="MeshInstance3D" parent="GridOverlay"]\nposition = Vector3({x}, {h+.013:.5f}, {22.375+i*.25})\nscale = Vector3(0.035, 0.012, 0.25)\nmesh = SubResource("Block")\nmaterial_override = SubResource("Grid")\n'
 (S/'GridVisualAdjustments.json').write_text(json.dumps({'modified':grid_edits,'added_box_segments':8,'reason':'Fit eastern row-15 column wires and upper horizontal wire to unchanged tread profile; no input, logical grid or western geometry change.'},indent=2))
 if 'id="EasternStair"' not in t:
  pos=t.index('[sub_resource');t=t[:pos]+'[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/EasternStair/EasternStair.tscn" id="EasternStair"]\n\n'+t[pos:]
  t+='\n[node name="EasternStairEnvironment" parent="." instance=ExtResource("EasternStair")]\n'
 scene.write_text(t);(S/'HiddenLegacyNodes.json').write_text(json.dumps(hidden,indent=2));assert len(hidden)==20
 print('Integrated eastern staircase; 20 original treads retained hidden; three eastern wire nodes fitted, eight wire segments added.')
bad=[p for p,h in json.loads((B/'ProtectedHashes.json').read_text()).items() if hashlib.sha256((R/p).read_bytes()).hexdigest()!=h]
assert not bad,bad
print('Protected gameplay and completed environment hashes unchanged.')
