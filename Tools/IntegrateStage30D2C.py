"""Relocate authored stair tiles and grid wires from the verified task-start snapshot."""
from pathlib import Path
import re,json
R=Path(__file__).resolve().parents[1];S=R/'SourceAssets/Environment/ForwardCastleStairs';B=S/'Baseline';P=R/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn'
t=(B/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn').read_text();chunks=re.split(r'(?=\[node )',t);oldcols={4,5,24,25};newcols={8,9,20,21};extra=[];changes=[]
def prop(c,key,value):
 pattern=r'^'+re.escape(key)+r' = .*$'
 return re.sub(pattern,key+' = '+value,c,flags=re.M) if re.search(pattern,c,re.M) else c.rstrip()+'\n'+key+' = '+value+'\n\n'
templates={}
for c in chunks:
 m=re.match(r'\[node name="Tread(\d)".*parent="Terrain/tile_4_(15|16)"',c)
 if m:templates[(int(m[2]),int(m[1]))]=c
for i,c in enumerate(chunks):
 header=c.split('\n')[0];tile=re.match(r'\[node name="tile_(\d+)_(15|16)"',header)
 if tile:
  x,y=map(int,tile.groups())
  if x in oldcols|newcols:
   stair=x in newcols;tag='stairs' if stair else ('cliff' if y==15 else 'flank');level=(-1 if y==15 else -2) if stair else (0 if y==15 else -3)
   c=prop(c,'TerrainTag','"'+tag+'"');c=prop(c,'Elevation',str(level));c=prop(c,'BlocksMovement','true' if not stair and y==15 else 'false');changes.append({'x':x,'y':y,'tag':tag,'elevation':level,'blocked':not stair and y==15})
   extra.append(f'\n[node name="LegacyVisuals" type="Node3D" parent="Terrain/tile_{x}_{y}"]\nvisible = false\n')
   if stair:
    for k in range(5):
     copied=templates[(y,k)];copied=re.sub(r'parent="Terrain/tile_4_'+str(y)+r'"',f'parent="Terrain/tile_{x}_{y}"',copied);copied=prop(copied,'visible','false');extra.append('\n'+copied.strip()+'\n')
   else:
    h=0 if y==15 else -1.8;extra.append(f'\n[node name="Surface" type="MeshInstance3D" parent="Terrain/tile_{x}_{y}"]\nvisible = false\nposition = Vector3(0, {h-.035}, 0)\nscale = Vector3(1.25, 0.07, 1.25)\nmesh = SubResource("Block")\nmaterial_override = SubResource("Rock")\n')
 direct=re.search(r'parent="Terrain/tile_(\d+)_(15|16)"',header)
 if direct:
  x,y=map(int,direct.groups())
  if x in oldcols|newcols:c=c.replace(f'parent="Terrain/tile_{x}_{y}"',f'parent="Terrain/tile_{x}_{y}/LegacyVisuals"',1)
 if 'parent="GridOverlay"' in header:c=c.replace('parent="GridOverlay"','parent="GridOverlay/LegacyLines"',1)
 for side,x in [('west',8),('east',20)]:
  if f'name="{side}_outer_flank"' in header:
   c=c.replace(side+'_outer_flank',side+'_forward_castle_jungle');c=prop(c,'transform',f'Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {-2.125+1.25*x}, 0, 20.375)')
   offsets=', '.join(f'Vector2i({dx}, {dy})' for dx in [0,1] for dy in range(5));c=prop(c,'CellOffsets',f'Array[Vector2i]([{offsets}])')
 if any(f'name="{name}"' in header for name in ['WesternStairEnvironment','ConnectedCliffEnvironment','LowerTerraceEnvironment','EasternStairEnvironment','FortStairIntegration']):c=prop(c,'visible','false')
 chunks[i]=c
# Parents must precede their descendants in text scene loading.
out=''.join(chunks);out=out.replace('[node name="GridOverlay"', '[node name="GridOverlay"',1)
marker=re.search(r'\[node name="GridOverlay"[^\n]*\n(?:[^\[]*)',out);assert marker
out=out[:marker.end()]+'[node name="LegacyLines" type="Node3D" parent="GridOverlay"]\nvisible = false\n\n'+out[marker.end():]
# Put per-tile legacy containers before any original visual child refers to them.
containers=[c for c in extra if 'name="LegacyVisuals"' in c];extra=[c for c in extra if c not in containers]
first_visual=out.index('[node name="Surface"');out=out[:first_visual]+''.join(containers)+'\n'+out[first_visual:]
tops=[-.12,-.36,-.6,-.74,-.86,-.96,-1.08,-1.2,-1.44,-1.68]
def level(x,y):return ((-1 if y==15 else -2) if x in newcols and y in [15,16] else (0 if y<=15 else -3))
grid=[]
def box(name,x,h,z,sx,sz):grid.append(f'\n[node name="{name}" type="MeshInstance3D" parent="GridOverlay"]\nposition = Vector3({x:.5f}, {h+.013:.5f}, {z:.5f})\nscale = Vector3({sx:.5f}, 0.012, {sz:.5f})\nmesh = SubResource("Block")\nmaterial_override = SubResource("Grid")\n')
# These are visual line segments only; use existing shared BoxMesh/material and batcher.
for boundary in range(31):
 x=-2.75+boundary*1.25+.007;col=min(boundary,29);segments=[]
 for y in range(20):
  z0=3.5+y*1.25
  if col in newcols and y in [15,16]:
   for k in range(5):segments.append([z0+k*.25,z0+(k+1)*.25,tops[(y-15)*5+k]])
  else:segments.append([z0,z0+1.25,level(col,y)*.6])
 merged=[]
 for a,b,h in segments:
  if merged and merged[-1][2]==h:merged[-1][1]=b
  else:merged.append([a,b,h])
 for i,(a,b,h) in enumerate(merged):box(f'CastleGridColumn_{boundary}_{i}',x,h,(a+b)/2,.035,b-a)
for boundary in range(21):
 row=min(boundary,19);heights=[(0 if row==15 else tops[5]) if col in newcols and row in [15,16] else level(col,row)*.6 for col in range(30)];start=0
 for end in range(1,31):
  if end==30 or heights[end]!=heights[start]:
   box(f'CastleGridRow_{boundary}_{start}',-2.75+(start+end)*.625,heights[start],3.5+boundary*1.25+.015,(end-start)*1.25,.035);start=end
out+=''.join(extra)+''.join(grid)
pos=out.index('[sub_resource');out=out[:pos]+'[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/ForwardCastleStairs/ForwardCastleStairs.tscn" id="ForwardCastleStairs"]\n\n'+out[pos:];out+='\n[node name="ForwardCastleStairs" parent="." instance=ExtResource("ForwardCastleStairs")]\n'
P.write_text(out);(S/'AuthoredChanges.json').write_text(json.dumps({'terrain':changes,'new_grid_box_segments':len(grid),'new_columns':sorted(newcols),'old_columns':sorted(oldcols),'hidden_legacy_roots':['WesternStairEnvironment','ConnectedCliffEnvironment','LowerTerraceEnvironment','EasternStairEnvironment','FortStairIntegration']},indent=2));print('Relocated16 terrain records; regenerated',len(grid),'grid box segments; original visuals retained hidden.')
