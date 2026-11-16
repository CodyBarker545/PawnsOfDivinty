"""Focused fort-to-lower-lane visual link, built in live Blender; originals untouched."""
import bpy,pathlib,math,json,random
from mathutils import Vector,Quaternion
R=pathlib.Path('D:/SoftwareEng/Game');S=R/'SourceAssets/Environment/FortStairIntegration';A=R/'Assets/Levels/Environment/FortStairIntegration';rng=random.Random(30211)
if not (S/'SessionBefore.blend').exists():bpy.ops.wm.save_as_mainfile(filepath=str(S/'SessionBefore.blend'))
scene=bpy.data.scenes.new('FortStairIntegration');bpy.context.window.scene=scene
for old in list(bpy.data.scenes):
 if old!=scene:bpy.data.scenes.remove(old)
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
with bpy.data.libraries.load(str(R/'SourceAssets/Environment/WesternStairSlice/WesternStairSlice.blend'),link=False) as (a,b):b.materials=['Worn slate steps and masonry']
mat=b.materials[0];parts=[];records=[]
def block(name,x,z,top,w,d,depth,col,bevel=.012):
 bpy.ops.mesh.primitive_cube_add(size=1,location=(x,-z,top-depth/2));o=bpy.context.object;o.name=name;o.scale=(w,d,depth);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  m=o.modifiers.new('Restrained dressed edge','BEVEL');m.width=min(bevel,depth*.18,d*.15);m.segments=1;bpy.ops.object.modifier_apply(modifier=m.name)
 o.data.materials.append(mat);uv=o.data.uv_layers.active or o.data.uv_layers.new(name='SharedStoneUV');attr=o.data.color_attributes.new(name='LandscapeTint',type='FLOAT_COLOR',domain='CORNER');o.data.color_attributes.active_color=attr
 for p in o.data.polygons:
  for li in p.loop_indices:
   co=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(co.x/2.8,-co.y/2.8);attr.data[li].color=(*col,1)
 parts.append(o);records.append({'name':name,'center_x':x,'z':z,'top_y':top,'width':w,'depth_z':d});return o
def paved_rect(label,x0,x1,z0,z1):
 rows=math.ceil((z1-z0)/.62)
 for j in range(rows):
  za=z0+(z1-z0)*j/rows;zb=z0+(z1-z0)*(j+1)/rows
  cuts=[x0,x0+(x1-x0)*(.35 if j%2 else .29),x0+(x1-x0)*(.69 if j%2 else .64),x1] if x1-x0>1 else [x0,x1]
  for k in range(len(cuts)-1):
   xa,xb=cuts[k:k+2];t=rng.uniform(.96,1.04)
   block(f'{label} paving {j}-{k}',(xa+xb)/2,(za+zb)/2,.024,xb-xa-.008,zb-za-.008,.055,(.265*t,.272*t,.247*t),.009)
tops=[-.12,-.36,-.6,-.74,-.86,-.96,-1.08,-1.2,-1.44,-1.68]
for label,center,gate in [('West',3.5,5.375),('East',28.5,26.625)]:
 # Two-cell paved side route meets the actual gate sill and preserves all walkable centers.
 paved_rect(label+' side route',center-1.22,center+1.22,16.03,22.25)
 if label=='East':paved_rect(label+' gate threshold',gate,center-1.22,16.03,18.47)
 else:paved_rect(label+' gate threshold',center+1.22,gate,16.03,18.47)
 for i,h in enumerate(tops):
  # Two long dressed nosing blocks read as a constructed fort stair, retaining the original height profile.
  for side in [-1,1]:block(f'{label} dressed nosing {i}-{side}',center+side*.624,22.25+i*.25+.224,h+.004,1.24,.052,.068,(.30,.307,.277),.006)
  # Capped, low retaining bands sit strictly outside the full 2.5-unit opening.
  for side in [-1,1]:
   x=center+side*1.372;z=22.375+i*.25
   block(f'{label} retaining course {i}-{side}',x,z,h+.13,.24,.244,.36,(.255,.268,.247),.012)
   block(f'{label} retaining coping {i}-{side}',x,z,h+.16,.244,.246,.052,(.335,.327,.289),.007)
 # Broad low shoulders tie into the fort footing rather than ending as loose posts.
 for side in [-1,1]:block(f'{label} landing shoulder {side}',center+side*1.372,22.03,.16,.244,.44,.20,(.30,.307,.277),.01)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name='FortAccessMasonry';bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.export_scene.gltf(filepath=str(A/'FortStairIntegration.glb'),export_format='GLB',use_selection=True,export_materials='NONE',export_animations=False,export_vertex_color='NAME',export_vertex_color_name='LandscapeTint',export_all_vertex_colors=False)
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.region_3d.view_location=(28.5,-20.5,-.6);area.spaces.active.region_3d.view_distance=12;area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),.5);area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(S/'FortStairIntegration.blend'))
o.data.calc_loop_triangles();(S/'Composition.json').write_text(json.dumps({'components':records,'triangles':len(o.data.loop_triangles),'surfaces':1,'materials':1,'new_textures':0,'stair_clear_width':2.5,'paved_approach_width':2.44,'paving_top_y':.024,'tread_nosing_offset':.004},indent=2));print('FORT STAIR MASONRY COMPLETE:',len(records),'components,',len(o.data.loop_triangles),'triangles, one shared material')
