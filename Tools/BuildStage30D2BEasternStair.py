"""Run in live Blender MCP (or background Blender): derive only eastern stair visuals."""
import bpy,bmesh,json,pathlib,math,random,hashlib
from mathutils import Vector,Quaternion
R=pathlib.Path('D:/SoftwareEng/Game');S=R/'SourceAssets/Environment/EasternStair';A=R/'Assets/Levels/Environment/EasternStair'
for p in (S,A):p.mkdir(parents=True,exist_ok=True)
if not (S/'McpSessionBefore.blend').exists():bpy.ops.wm.save_as_mainfile(filepath=str(S/'McpSessionBefore.blend'))
scene=bpy.data.scenes.new('EasternStairConstruction');bpy.context.window.scene=scene
for old in list(bpy.data.scenes):
 if old!=scene:bpy.data.scenes.remove(old)
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
with bpy.data.libraries.load(str(R/'SourceAssets/Environment/WesternStairSlice/WesternStairSlice.blend'),link=False) as (a,b):
 b.objects=['Worn slate steps and masonry'];b.materials=['Worn slate steps and masonry','Weathered charcoal escarpment','Desaturated understory']
donor=b.objects[0];stone,rock,foliage=b.materials
adj=[set() for v in donor.data.vertices]
for e in donor.data.edges:
 a,b=e.vertices;adj[a].add(b);adj[b].add(a)
seen=set();parts=[]
for v in donor.data.vertices:
 if v.index in seen:continue
 todo=[v.index];ids=set()
 while todo:
  i=todo.pop()
  if i in ids:continue
  ids.add(i);todo.extend(adj[i]-ids)
 seen.update(ids);vv=[donor.matrix_world@donor.data.vertices[i].co for i in ids]
 lo=Vector([min(p[j] for p in vv) for j in range(3)]);hi=Vector([max(p[j] for p in vv) for j in range(3)])
 if lo.x>2.24 and hi.x<4.76 and lo.y>=-24.76 and hi.y<=-22.24 and hi.x-lo.x<1.3:parts.append((ids,lo,hi))
assert len(parts)==30
library=[]
for index,pindex in enumerate([0,2,4]):
 ids,lo,hi=parts[pindex];data=donor.data.copy();bm=bmesh.new();bm.from_mesh(data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index not in ids],context='VERTS');bm.to_mesh(data);bm.free()
 data.name='Reused chipped slab '+str(index);size=hi-lo;mid=(lo+hi)*.5
 for v in data.vertices:
  p=donor.matrix_world@v.co;v.co=((p.x-mid.x)/size.x,(p.y-mid.y)/size.y,(p.z-hi.z)/size.z)
 # Low moss tint on one of three source variants; retain source bevels, UVs and vertex colors.
 attr=data.color_attributes.get('LandscapeTint')
 if index==2:
  for poly in data.polygons:
   for li in poly.loop_indices:
    v=data.vertices[data.loops[li].vertex_index].co;t=max(0,min(.30,(v.x+.15)*.45))
    col=attr.data[li].color;attr.data[li].color=(*(col[k]*(1-t)+(.15,.18,.11)[k]*t for k in range(3)),1)
 data.materials.clear();data.materials.append(stone)
 obj=bpy.data.objects.new('Slab_'+chr(65+index),data);scene.collection.objects.link(obj);library.append(obj)
bpy.data.objects.remove(donor,do_unlink=True)
def export(path,objects):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0]
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_materials='NONE',export_animations=False,export_vertex_color='NAME',export_vertex_color_name='LandscapeTint',export_all_vertex_colors=False)
export(A/'SlabLibrary.glb',library)
lib_collection=bpy.data.collections.new('Source templates — excluded from runtime');scene.collection.children.link(lib_collection)
for o in library:
 scene.collection.objects.unlink(o);lib_collection.objects.link(o);o.hide_render=True;o.hide_set(True)
lib_collection.hide_render=True
placements=[];rng=random.Random(30202)
def place(variant,label,x,z,h,width,depth,thickness,angle=0):
 o=bpy.data.objects.new(label,library[variant].data);scene.collection.objects.link(o);o.location=(x,-z,h);o.scale=(width,depth,thickness);o.rotation_euler.z=angle
 placements.append({'variant':variant,'name':label,'position':[x,h,z],'scale':[width,thickness,depth],'angle':-angle});return o
tops=[-.12,-.36,-.6,-.74,-.86,-.96,-1.08,-1.2,-1.44,-1.68]
for i,h in enumerate(tops):
 cuts=[27.25,28.04,28.96,29.75] if i%3==0 else ([27.25,28.20,28.84,29.75] if i%3==1 else [27.25,27.98,28.91,29.75])
 for j in range(3):
  # Exact flat top and tread depth remain authoritative; only the slab joints vary.
  width=cuts[j+1]-cuts[j]-.008;thick=.23 if i<3 or i==8 else .17
  place((i+j*2)%3,f'Eastern tread {i+1:02} slab {j+1}',(cuts[j]+cuts[j+1])*.5,22.375+i*.25,h,width,.246,thick)
# Low, intermittently broken cheeks remain outside the 2.5-unit route.
for side,x in enumerate([27.16,29.84]):
 for i in [0,1,3,4,6]:
  z=22.36+i*.34;h=-.6*(z-21.625)/1.25+.13
  place((i+side)%3,f'Buried cheek {side}-{i}',x,z,min(.04,h),.16,.30,.26)
# Only local landing paving; no replacement of either existing ground surface.
for i,(x,z,w,d) in enumerate([(27.83,22.08,.63,.22),(28.51,21.94,.52,.32),(29.10,22.07,.57,.23)]):place(i%3,f'Upper worn threshold {i}',x,z,.018,w,d,.08,.015*(i-1))
for i,(x,z,w,d) in enumerate([(27.77,24.97,.38,.29),(28.35,24.91,.43,.23),(29.03,25.04,.41,.25),(28.66,25.38,.31,.22)]):place((i+1)%3,f'Lower buried paving {i}',x,z,-1.776,w,d,.075,rng.uniform(-.16,.16))
# A single closed dark structural core under every riser, plus restrained hairline erosion marks.
verts=[];faces=[];colors=[]
def meshface(points,color):
 off=len(verts);verts.extend(points);faces.append(tuple(range(off,off+len(points))));colors.append(color)
def xyz(x,h,z):return (x,-z,h)
profile=[(22.25,-1.84),(24.75,-1.84),(24.75,tops[-1]-.018)]
for i in range(9,-1,-1):
 profile.append((22.25+i*.25,tops[i]-.018))
 if i>0:profile.append((22.25+i*.25,tops[i-1]-.018))
# Recess the backing behind slab fronts, avoiding near-coplanar shadow/depth striping.
profile=[(z-(.025 if z>22.25 else 0),h) for z,h in profile]
for x,reverse in [(27.25,True),(29.75,False)]:
 pts=[xyz(x,h,z) for z,h in profile];meshface(list(reversed(pts)) if reverse else pts,(.19,.205,.195))
for i in range(len(profile)):
 a=profile[i];b=profile[(i+1)%len(profile)];meshface([xyz(27.25,a[1],a[0]),xyz(29.75,a[1],a[0]),xyz(29.75,b[1],b[0]),xyz(27.25,b[1],b[0])],(.19,.205,.195))
for i in [1,3,5,8]:
 x=27.52+(i%3)*.72;z=22.27+i*.25;h=tops[i]+.0008
 pts=[(x,z),(x+.055,z+.07),(x+.025,z+.13),(x+.12,z+.21)]
 for (xa,za),(xb,zb) in zip(pts,pts[1:]):meshface([xyz(xa-.004,h,za),xyz(xa+.004,h,za),xyz(xb+.004,h,zb),xyz(xb-.004,h,zb)],(.07,.08,.073))
data=bpy.data.meshes.new('Eastern riser backing and erosion');data.from_pydata(verts,[],faces);data.update();core=bpy.data.objects.new('RiserCoreAndErosion',data);scene.collection.objects.link(core);data.materials.append(stone)
bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
attr=data.color_attributes.new(name='LandscapeTint',type='FLOAT_COLOR',domain='CORNER');data.color_attributes.active_color=attr;uv=data.uv_layers.new(name='SharedStoneUV')
for p in data.polygons:
 for li in p.loop_indices:
  v=data.vertices[data.loops[li].vertex_index].co;attr.data[li].color=(*colors[p.index],1);uv.data[li].uv=(v.x/2.8,-v.y/2.8)
# Reuse the verified CC0 short-grass mesh, three small groups merged to one surface.
src=R/'SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/glTF/Grass_Common_Short.gltf'
license_file=src.parent.parent/'License_Standard.txt';assert license_file.exists()
before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(src));imports=[o for o in bpy.data.objects if o not in before];meshes=[o for o in imports if o.type=='MESH'];vv=[];ff=[]
for o in meshes:
 off=len(vv);vv.extend([o.matrix_world@v.co for v in o.data.vertices]);ff.extend([tuple(off+i for i in p.vertices) for p in o.data.polygons])
lo=Vector([min(v[k] for v in vv) for k in range(3)]);hi=Vector([max(v[k] for v in vv) for k in range(3)]);size=hi-lo
gverts=[];gfaces=[]
for x,z in [(26.93,24.0),(30.08,24.23),(30.23,24.60)]:
 off=len(gverts);gverts.extend([(x+(v.x-lo.x)/size.x*.23-.115,-z+(v.y-lo.y)/size.y*.23-.115,-1.787+(v.z-lo.z)/size.z*.15) for v in vv]);gfaces.extend([tuple(off+i for i in f) for f in ff])
for o in imports:bpy.data.objects.remove(o,do_unlink=True)
gm=bpy.data.meshes.new('Sparse eastern grass');gm.from_pydata(gverts,[],gfaces);gm.update();grass=bpy.data.objects.new('SparseGrass',gm);scene.collection.objects.link(grass);gm.materials.append(foliage)
attr=gm.color_attributes.new(name='LandscapeTint',type='FLOAT_COLOR',domain='CORNER');gm.color_attributes.active_color=attr;uv=gm.uv_layers.new(name='SharedGrassUV')
for l in gm.loops:attr.data[l.index].color=(.09,.14,.065,1);uv.data[l.index].uv=(0,0)
export(A/'EasternDetails.glb',[core,grass])
# Reference existing optimized cliff mesh data in Blender too; originals remain untouched.
rockplacements=[{'asset':'FractureRib','name':'BuriedEastFracture','position':[30.05,-1.86,23.03],'scale':[.46,1.79,.65],'angle':-.10}, {'asset':'SeamBoulder','name':'WestFootStone','position':[26.91,-1.85,23.61],'scale':[.59,.32,.45],'angle':.09},{'asset':'SeamBoulder','name':'EastFootStone','position':[30.04,-1.84,24.03],'scale':[.39,.16,.30],'angle':-.18}]
with bpy.data.libraries.load(str(R/'SourceAssets/Environment/ConnectedCliff/ConnectedCliff.blend'),link=False) as (a,b):b.objects=['FractureRib','SeamBoulder']
rockdonors={o.name.split('.')[0]:o for o in b.objects}
for p in rockplacements:
 donor=rockdonors[p['asset']];o=bpy.data.objects.new(p['name'],donor.data);scene.collection.objects.link(o);x,h,z=p['position'];sx,sy,sz=p['scale'];o.location=(x,-z,h);o.scale=(sx,sz,sy);o.rotation_euler.z=-p['angle']
for o in b.objects:bpy.data.objects.remove(o,do_unlink=True)
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_location=(28.5,-23.5,-.8);area.spaces.active.region_3d.view_distance=7;area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),.53);area.spaces.active.shading.type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT');scene.world=bpy.data.worlds.new('Eastern source preview');scene.world.use_nodes=True
bpy.ops.wm.save_as_mainfile(filepath=str(S/'EasternStair.blend'))
metrics={}
for o in library+[core,grass]:o.data.calc_loop_triangles();metrics[o.name]=len(o.data.loop_triangles)
doc={'library_triangles':metrics,'slab_placements':placements,'rock_placements':rockplacements,'tread_tops':tops,'new_textures':0,'source_donor':'SourceAssets/Environment/WesternStairSlice/WesternStairSlice.blend','source_donor_sha256':hashlib.sha256((R/'SourceAssets/Environment/WesternStairSlice/WesternStairSlice.blend').read_bytes()).hexdigest(),'grass_source':str(src.relative_to(R)),'grass_sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'grass_license':str(license_file.relative_to(R)),'materials':['SliceMaterial3.tres','SliceMaterial2.tres','SliceMaterial1.tres'],'collision_nodes':0}
(S/'Composition.json').write_text(json.dumps(doc,indent=2));print('EASTERN STAIR SOURCE COMPLETE',metrics,'linked slab uses',len(placements),'existing rock uses',len(rockplacements))
