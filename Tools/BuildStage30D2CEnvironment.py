"""Live Blender: derive a repaired cliff/ground junction; reuse existing runtime stair scenes."""
import bpy,json,pathlib,math,random
from mathutils import Vector,Quaternion,Matrix
R=pathlib.Path('D:/SoftwareEng/Game');S=R/'SourceAssets/Environment/ForwardCastleStairs';A=R/'Assets/Levels/Environment/ForwardCastleStairs';rng=random.Random(30203)
if not (S/'SessionBefore.blend').exists():bpy.ops.wm.save_as_mainfile(filepath=str(S/'SessionBefore.blend'))
scene=bpy.data.scenes.new('ForwardCastleStairs');bpy.context.window.scene=scene
for old in list(bpy.data.scenes):
 if old!=scene:bpy.data.scenes.remove(old)
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
holes=[(7.25,9.75,22.25,24.75),(22.25,24.75,22.25,24.75)]
buffers={k:{'v':[],'f':[],'uv':[],'c':[]} for k in ['CliffRock','AdjacentGround','Understory','LowerGround','CastleMasonry']};preview_materials={}
def load(folder,file,names):
 with bpy.data.libraries.load(str(R/f'SourceAssets/Environment/{folder}/{file}'),link=False) as (a,b):b.objects=[n for n in a.objects if names(n)]
 return b.objects
def interpolate(a,b,t):return tuple(a[i]+(b[i]-a[i])*t for i in range(len(a)))
def split(poly,axis,value,sign):
 inside=[];outside=[]
 for a,b in zip(poly,poly[1:]+poly[:1]):
  da=(a[axis]-value)*sign;db=(b[axis]-value)*sign
  (inside if da>=-1e-7 else outside).append(a)
  if (da>1e-7 and db< -1e-7) or (da< -1e-7 and db>1e-7):
   p=interpolate(a,b,da/(da-db));inside.append(p);outside.append(p)
 return inside,outside
def remove_hole(poly,hole):
 x0,x1,z0,z1=hole;remainder=poly;kept=[]
 for axis,value,sign in [(0,x0,1),(0,x1,-1),(2,z0,1),(2,z1,-1)]:
  remainder,outside=split(remainder,axis,value,sign)
  if len(outside)>=3:kept.append(outside)
  if len(remainder)<3:break
 return kept
def emit(poly,category):
 buf=buffers[category]
 for i in range(1,len(poly)-1):
  tri=[poly[0],poly[i],poly[i+1]]
  if (Vector(tri[1][:3])-Vector(tri[0][:3])).cross(Vector(tri[2][:3])-Vector(tri[0][:3])).length<1e-9:continue
  off=len(buf['v']);buf['v'].extend((p[0],-p[2],p[1]) for p in tri);buf['f'].append((off,off+1,off+2));buf['uv'].extend(p[3:5] for p in tri);buf['c'].extend(p[5:9] for p in tri)
def add_object(o,category,clip=True,offset=0):
 if o.type!='MESH':return
 # Appended datablocks are not in the dependency graph until linked. Compose their
 # authored transforms explicitly so linked rock placements and rubble retain them.
 world=Matrix.LocRotScale(o.location,o.rotation_euler.to_quaternion(),o.scale)
 if o.data.materials:preview_materials.setdefault(category,o.data.materials[0])
 data=o.data;data.calc_loop_triangles();uv=data.uv_layers.active;col=data.color_attributes.get('LandscapeTint');default=(.20,.22,.21,1) if category=='CliffRock' else (.18,.20,.12,1)
 for tri in data.loop_triangles:
  poly=[]
  for li in tri.loops:
   p=world@data.vertices[data.loops[li].vertex_index].co;p.x+=offset;tex=tuple(uv.data[li].uv) if uv else (p.x/2.8,-p.y/2.8);color=tuple(col.data[li if col.domain=='CORNER' else data.loops[li].vertex_index].color) if col else default
   poly.append((p.x,p.z,-p.y,*tex,*color))
  pieces=[poly]
  if clip:
   for hole in holes:pieces=[q for p in pieces for q in remove_hole(p,hole)]
  for p in pieces:emit(p,category)
def dispose(objects):
 for o in objects:bpy.data.objects.remove(o,do_unlink=True)
# Western slice: retain its terrain, rocks and plants, remove the complete old stone stair/paving batch.
objects=load('WesternStairSlice','WesternStairSlice.blend',lambda n:n in ['Weathered charcoal escarpment','Blended moss earth and grass','Desaturated understory'])
for o in objects:add_object(o,{'Weathered charcoal escarpment':'CliffRock','Blended moss earth and grass':'AdjacentGround','Desaturated understory':'Understory'}[o.name.split('.')[0]])
dispose(objects)
objects=load('ConnectedCliff','ConnectedCliff.blend',lambda n:n.startswith('Cliff_') or n in ['ContinuousGeologicalCore','AdjacentGround'])
for o in objects:add_object(o,'AdjacentGround' if o.name.startswith('AdjacentGround') else 'CliffRock')
dispose(objects)
# Preserve the completed lower ground's exact material/UVs. Fill its former stair notches.
objects=load('LowerTerrace','LowerTerrace.blend',lambda n:n in ['ConnectedGround','RestrainedEdgeRubble'])
for o in objects:add_object(o,'LowerGround' if o.name.startswith('ConnectedGround') else 'CliffRock',not o.name.startswith('ConnectedGround'))
dispose(objects)
for x0,x1 in [(2.25,4.75),(27.25,29.75)]:
 poly=[(x,h,z,(x+2.75)/37.5,1-(z-22.8)/5.7,1,1,1,1) for x,h,z in [(x0,-1.787,22.8),(x0,-1.787,24.75),(x1,-1.787,24.75),(x1,-1.787,22.8)]]
 emit(poly,'LowerGround')
# Recessed cliff backing and irregular reused rocks erase the old openings.
donors=load('ConnectedCliff','ConnectedCliff.blend',lambda n:n in ['BroadFaceA','BroadFaceB','LayeredOverhang','SeamBoulder'])
donor_by={o.name.split('.')[0]:o for o in donors}
for x0,x1 in [(2.19,4.81),(27.19,29.81)]:
 for face in [[(x0,-1.94,22.25),(x1,-1.94,22.25),(x1,.002,22.25),(x0,.002,22.25)]]:
  emit([(x,h,z,x/2.8,z/2.8,.14,.16,.17,1) for x,h,z in face],'CliffRock')
 # Carry the existing irregular grassy crown across the repaired opening;
 # avoid a rectangular bare-rock lid that would reveal the former cutout.
 for k in range(7):
  xa=x0+(x1-x0)*k/7;xb=x0+(x1-x0)*(k+1)/7;za=23.14+.10*math.sin(xa*2.3)+.04*math.sin(xa*6.1);zb=23.14+.10*math.sin(xb*2.3)+.04*math.sin(xb*6.1)
  face=[(xa,-1.94,za),(xa,.002,za),(xb,.002,zb),(xb,-1.94,zb)];emit([(x,h,z,x/2.8,z/2.8,.14,.16,.17,1) for x,h,z in face],'CliffRock')
  # Rock backing overlaps the old shoulders, but the coplanar grassy cap must
  # end exactly at the old opening edges to avoid a depth seam on the crown.
  ca=x0+.06+(x1-x0-.12)*k/7;cb=x0+.06+(x1-x0-.12)*(k+1)/7
  cza=23.14+.10*math.sin(ca*2.3)+.04*math.sin(ca*6.1);czb=23.14+.10*math.sin(cb*2.3)+.04*math.sin(cb*6.1)
  face=[(ca,.002,22.25),(cb,.002,22.25),(cb,.002,czb),(ca,.002,cza)];emit([(x,h,z,x/2.8,z/2.8,.166,.207,.101,1) for x,h,z in face],'AdjacentGround')
 for i in range(3):
  source=donor_by[['BroadFaceA','BroadFaceB','LayeredOverhang'][i]];o=source.copy();o.data=source.data;o.location=(x0+.48+i*.82,-22.91,-1.93);o.scale=(1.04,1.01,2.04+rng.random()*.12);o.rotation_euler=(0,0,rng.uniform(-.09,.09));add_object(o,'CliffRock',False);bpy.data.objects.remove(o,do_unlink=True)
 source=donor_by['SeamBoulder'];o=source.copy();o.data=source.data;o.location=((x0+x1)/2,-23.46,-1.86);o.scale=(1.08,.40,.22);o.rotation_euler=(0,0,.13);add_object(o,'CliffRock',False);bpy.data.objects.remove(o,do_unlink=True)
dispose(donors)
# Retain only dressed tread fronts, retaining courses and the last upper paving row;
# relocate them from the old eastern opening to each forward castle's southern gate.
objects=load('FortStairIntegration','FortStairIntegration.blend',lambda n:n=='FortAccessMasonry');o=objects[0];data=o.data;adj=[set() for v in data.vertices]
for e in data.edges:a,b=e.vertices;adj[a].add(b);adj[b].add(a)
seen=set();keep=set()
for v in data.vertices:
 if v.index in seen:continue
 ids=set();todo=[v.index]
 while todo:
  i=todo.pop()
  if i in ids:continue
  ids.add(i);todo.extend(adj[i]-ids)
 seen.update(ids);vv=[o.matrix_world@data.vertices[i].co for i in ids];cx=sum(p.x for p in vv)/len(vv);zmin=min(-p.y for p in vv)
 if cx>25 and zmin>=21.60:keep.update(ids)
bm=__import__('bmesh').new();bm.from_mesh(data);bm.verts.ensure_lookup_table();__import__('bmesh').ops.delete(bm,geom=[v for v in bm.verts if v.index not in keep],context='VERTS');bm.to_mesh(data);bm.free()
for dx in [-20,-5]:add_object(o,'CastleMasonry',False,dx)
dispose(objects)
# Close the short vertical threshold riser between the unchanged gate floor and
# the first descending tread, without changing any authored tread-center height.
for center in [8.5,23.5]:
 x0,x1=center-1.25,center+1.25;z0,z1=22.245,22.281;lo,hi=-.155,.018
 faces=[[(x0,lo,z1),(x1,lo,z1),(x1,hi,z1),(x0,hi,z1)],[(x0,hi,z0),(x0,hi,z1),(x1,hi,z1),(x1,hi,z0)],[(x0,lo,z0),(x0,lo,z1),(x0,hi,z1),(x0,hi,z0)],[(x1,lo,z1),(x1,lo,z0),(x1,hi,z0),(x1,hi,z1)]]
 for face in faces:emit([(x,h,z,x/2.8,z/2.8,.22,.235,.225,1) for x,h,z in face],'CastleMasonry')
models=[];stats={}
for name,buf in buffers.items():
 data=bpy.data.meshes.new(name);data.from_pydata(buf['v'],[],buf['f']);data.update();o=bpy.data.objects.new(name,data);scene.collection.objects.link(o)
 if name in preview_materials:data.materials.append(preview_materials[name])
 uv=data.uv_layers.new(name='RetainedSurfaceUV');attr=data.color_attributes.new(name='LandscapeTint',type='FLOAT_COLOR',domain='CORNER');data.color_attributes.active_color=attr
 for li in range(len(data.loops)):uv.data[li].uv=buf['uv'][li];attr.data[li].color=buf['c'][li]
 models.append(o);stats[name]=len(buf['f'])
bpy.ops.object.select_all(action='DESELECT')
for o in models:o.select_set(True)
bpy.context.view_layer.objects.active=models[0]
bpy.ops.export_scene.gltf(filepath=str(A/'CastleCliffJunction.glb'),export_format='GLB',use_selection=True,export_materials='NONE',export_animations=False,export_vertex_color='NAME',export_vertex_color_name='LandscapeTint',export_all_vertex_colors=False)
# Add linked source stair references for an editable complete Blender preview, not the junction export.
objects=load('EasternStair','EasternStair.blend',lambda n:not n.startswith('Slab_'))
for source in objects:
 if source.type!='MESH':continue
 for side,dx in [('West',-20),('East',-5)]:
  o=source.copy();o.data=source.data;o.name=side+' stair reference '+source.name;scene.collection.objects.link(o);o.location.x+=dx;o.hide_set(False);o.hide_render=False
dispose(objects)
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.region_3d.view_location=(16,-23.5,-.6);area.spaces.active.region_3d.view_distance=37;area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),.6);area.spaces.active.shading.type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT');bpy.ops.wm.save_as_mainfile(filepath=str(S/'ForwardCastleStairs.blend'))
(S/'EnvironmentComposition.json').write_text(json.dumps({'junction_meshes':stats,'junction_triangles':sum(stats.values()),'new_textures':0,'old_holes_closed':[[2.25,4.75],[27.25,29.75]],'new_holes':holes,'reused_native_stair_offsets':[-20,-5],'source_stair_references_excluded_from_export':True},indent=2));print('CASTLE CLIFF JUNCTION COMPLETE',stats)
# Write imported-child overrides from the actual exported node order.
import struct
raw=(A/'CastleCliffJunction.glb').read_bytes();n=struct.unpack_from('<I',raw,12)[0];gltf=json.loads(raw[20:20+n]);names=[gltf['nodes'][i]['name'] for i in gltf['scenes'][0]['nodes']]
materials={'CliffRock':'WesternStairSlice/SliceMaterial2.tres','AdjacentGround':'WesternStairSlice/SliceMaterial0.tres','Understory':'WesternStairSlice/SliceMaterial1.tres','LowerGround':'LowerTerrace/Ground.tres','CastleMasonry':'WesternStairSlice/SliceMaterial3.tres'}
wrapper='[gd_scene format=3]\n\n[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/ForwardCastleStairs/CastleCliffJunction.glb" id="junction"]\n[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/EasternStair/EasternStair.tscn" id="stairs"]\n'
for name,path in materials.items():wrapper+=f'[ext_resource type="Material" path="res://Assets/Levels/Environment/{path}" id="{name}"]\n'
wrapper+='\n[node name="ForwardCastleStairs" type="Node3D"]\nmetadata/stage = "3.0D.2C"\n\n[node name="Junction" parent="." instance=ExtResource("junction")]\n'
for i,name in enumerate(names):wrapper+=f'\n[node name="{name}" parent="Junction" index={i}]\nmaterial_override = ExtResource("{name}")\n'
wrapper+='\n[node name="WestCastleDescent" parent="." instance=ExtResource("stairs")]\nposition = Vector3(-20, 0, 0)\n\n[node name="EastCastleDescent" parent="." instance=ExtResource("stairs")]\nposition = Vector3(-5, 0, 0)\n\n[editable path="Junction"]\n';(A/'ForwardCastleStairs.tscn').write_text(wrapper)
