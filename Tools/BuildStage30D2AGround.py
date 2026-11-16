"""Author the dedicated collision-free lower terrace mesh and editable Blender source."""
import bpy,math,random,json,pathlib
from mathutils import Quaternion,Vector
R=pathlib.Path(__file__).resolve().parents[1];S=R/'SourceAssets/Environment/LowerTerrace';A=R/'Assets/Levels/Environment/LowerTerrace'
bpy.ops.wm.read_factory_settings(use_empty=True)
mat=bpy.data.materials.new('Connected earth, worn path and moss');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.94
for fn,channel in [('LowerTerrainColor.png','Base Color'),('LowerTerrainNormal.png','Normal')]:
 im=bpy.data.images.load(str(A/fn));im.pack();tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im
 if channel=='Normal':
  im.colorspace_settings.name='Non-Color';nm=mat.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.16;mat.node_tree.links.new(tex.outputs['Color'],nm.inputs['Color']);mat.node_tree.links.new(nm.outputs['Normal'],bs.inputs[channel])
 else:mat.node_tree.links.new(tex.outputs['Color'],bs.inputs[channel])
verts=[];faces=[];lookup={}
xs=[-2.75+i*.25 for i in range(151)];zs=[22.8,23.05,23.3,23.5,23.75,24,24.25,24.5,24.75]+[25+i*.25 for i in range(15)]
def vert(x,z):
 key=(x,z)
 if key not in lookup:lookup[key]=len(verts);verts.append((x,-z,-1.787))
 return lookup[key]
for xa,xb in zip(xs,xs[1:]):
 for za,zb in zip(zs,zs[1:]):
  if za<24.75 and (2.25<=xa<4.75 or 27.25<=xa<29.75):continue
  a,b,c,d=vert(xa,za),vert(xb,za),vert(xb,zb),vert(xa,zb);faces.extend([(a,c,b),(a,d,c)])
me=bpy.data.meshes.new('Continuous lower landscape');me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new('ConnectedGround',me);bpy.context.collection.objects.link(o);me.materials.append(mat)
uv=me.uv_layers.new(name='LandscapeUV')
for loop in me.loops:
 v=me.vertices[loop.vertex_index].co;uv.data[loop.index].uv=((v.x+2.75)/37.5,1-(-v.y-22.8)/5.7)
# Sparse, low edge stones; one joined mesh, absent from the open route and stair approaches.
rng=random.Random(30201);stones=[]
stone=bpy.data.materials.new('Muted edge rubble');stone.diffuse_color=(.20,.22,.21,1);stone.use_nodes=True;stone.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.20,.22,.21,1);stone.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.94
for i in range(62):
 x=rng.uniform(-2.5,34.5);z=rng.uniform(23.5,24.95)
 if any(abs(x-s)<1.6 for s in [3.5,28.5]):continue
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=(x,-z,-1.78));p=bpy.context.object;p.scale=(rng.uniform(.06,.18),rng.uniform(.05,.13),rng.uniform(.025,.075));p.rotation_euler.z=rng.random()*6.28;stones.append(p)
bpy.ops.object.select_all(action='DESELECT')
for p in stones:p.select_set(True)
bpy.context.view_layer.objects.active=stones[0];bpy.ops.object.join();rubble=bpy.context.object;rubble.name='RestrainedEdgeRubble';rubble.data.materials.append(stone)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
for obj in [o,rubble]:obj['decorative_only']=True
# Save a useful top-oblique material-preview viewport for direct source inspection.
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_distance=31;area.spaces.active.region_3d.view_location=(16,-25.5,-1.8);area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),.42);area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(S/'LowerTerrace.blend'))
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(A/'LowerTerrace.glb'),export_format='GLB',use_selection=True,export_materials='NONE')
stats={'ground_triangles':len(faces),'rubble_triangles':sum(len(p.vertices)-2 for p in rubble.data.polygons),'mesh_instances':2,'surfaces':2,'materials':2,'textures':[{'size':[2048,512],'format':'RGB8','mipmapped_uncompressed_gpu_bytes':5592405}]*2,'standing_y':-1.787,'logical_floor_y':-1.8,'collision_nodes':0}
(S/'Construction.json').write_text(json.dumps(stats,indent=2));print(stats)
