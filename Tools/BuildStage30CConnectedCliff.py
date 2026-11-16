"""Compact shared cliff library and connected edge composition; isolated Blender only."""
import bpy,bmesh,json,pathlib,random,math,hashlib
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[1]; SRC=ROOT/'SourceAssets/Environment/ConnectedCliff'; RUN=ROOT/'Assets/Levels/Environment/ConnectedCliff'; QA=SRC/'QA'
for p in (SRC,RUN,QA):p.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.name='ConnectedCliffPreview';rng=random.Random(30031)
library_collection=bpy.data.collections.new('Reusable source library');scene.collection.children.link(library_collection)
inventory=json.loads((ROOT/'SourceAssets/Environment/Manifests/AssetInventory.json').read_text())['assets']
source_records=[]
with bpy.data.libraries.load(str(ROOT/'SourceAssets/Environment/WesternStairSlice/WesternStairSlice.blend'),link=False) as (src,dst):
    dst.materials=['Weathered charcoal escarpment','Blended moss earth and grass','Worn slate steps and masonry']
rock_mat,ground_mat,step_mat=dst.materials

def tint_mesh(o,ground=False):
    o.data.materials.clear();o.data.materials.append(ground_mat if ground else rock_mat)
    for p in o.data.polygons:p.material_index=0
    for a in list(o.data.color_attributes):o.data.color_attributes.remove(a)
    tint=o.data.color_attributes.new(name='LandscapeTint',type='FLOAT_COLOR',domain='CORNER');o.data.color_attributes.active_color=tint
    uv=o.data.uv_layers.active or o.data.uv_layers.new(name='SharedStoneUV')
    for p in o.data.polygons:
        for li in p.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co
            if ground:
                x,z,h=v.x,-v.y,v.z
                low=h<-1;distance=abs(z-(24.2 if low else 21.5))
                grass=(.145,.18,.085);soil=(.225,.19,.13)
                patch=.5+.3*math.sin(x*.64+z*.91)+.13*math.sin(x*2.7-z)
                t=max(0,min(1,patch));col=tuple(soil[i]*(1-t)+grass[i]*t for i in range(3))
                # Shared value transition into original outside surfaces and the existing western slice.
                edge=min(abs(z-(24.75 if low else 21)),min(abs(x-7.25),abs(x-34.75),abs(x+.25),abs(x+2.75)))
                if edge<.45:
                    old=(.262,.239,.147) if low else (.179,.230,.106)
                    a=max(0,edge/.45);col=tuple(old[i]*(1-a)+col[i]*a for i in range(3))
            else:
                weather=max(0,min(.3,(v.z-.68)*.7))
                base=(.14,.16,.17);moss=(.145,.18,.105)
                variation=.93+.07*math.sin(v.x*5+v.z*4+v.y*3)
                col=tuple((base[i]*(1-weather)+moss[i]*weather)*variation for i in range(3))
            tint.data[li].color=(*col,1)
            uv.data[li].uv=(v.x/2.8,(-v.y if abs(p.normal.z)>.6 else v.z)/2.8)

def import_source(name):
    a=next(x for x in inventory if x['name']==name and 'StylizedNatureMegaKit' in x['pack']);assert a['license_status']=='CC0-bundled'
    before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(ROOT/a['source']))
    imported=[o for o in bpy.data.objects if o not in before];vv=[];ff=[]
    for o in imported:
        if o.type!='MESH':continue
        off=len(vv);vv.extend([o.matrix_world@v.co for v in o.data.vertices]);ff.extend([tuple(off+i for i in p.vertices) for p in o.data.polygons])
    lo=Vector([min(v[i] for v in vv) for i in range(3)]);hi=Vector([max(v[i] for v in vv) for i in range(3)]);size=hi-lo
    vv=[((v.x-lo.x)/size.x-.5,(v.y-lo.y)/size.y-.5,(v.z-lo.z)/size.z) for v in vv]
    for o in imported:bpy.data.objects.remove(o,do_unlink=True)
    if not any(x['catalog_id']==a['id'] for x in source_records):source_records.append({'catalog_id':a['id'],'name':name,'source':a['source'],'source_objects':a['source_objects_exact'],'sha256':hashlib.sha256((ROOT/a['source']).read_bytes()).hexdigest(),'license':'CC0-1.0','license_document':'SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/License_Standard.txt'})
    return vv,ff

def mesh(name,vv,ff,collection=None):
    data=bpy.data.meshes.new(name);data.from_pydata(vv,[],ff);data.update();o=bpy.data.objects.new(name,data);(collection or scene.collection).objects.link(o);return o

specs=[('BroadFaceA','Rock_Medium_1',.62,'broad cliff face'),('BroadFaceB','Rock_Medium_3',.46,'fractured broad face'),('LayeredOverhang','Rock_Medium_2',.8,'layer and crown transition'),('FractureRib','Rock_Medium_1',.29,'lower-detail narrow fracture'),('SeamBoulder','Rock_Medium_2',.42,'lower-detail foot and seam cover')]
library=[];sources={}
for name,original,ratio,role in specs:
    vv,ff=import_source(original)
    if name=='LayeredOverhang':vv=[(x+.16*(z-.5),y+.06*math.sin(z*7),z) for x,y,z in vv]
    o=mesh(name,vv,ff,library_collection);bpy.context.view_layer.objects.active=o;o.select_set(True)
    before_tris=sum(max(0,len(p.vertices)-2) for p in o.data.polygons)
    mod=o.modifiers.new('Tactical silhouette simplification','DECIMATE');mod.ratio=ratio;bpy.ops.object.modifier_apply(modifier=mod.name)
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    hidden=[f for f in bm.faces if (f.normal.y>.55 and f.calc_center_median().y>.13) or (f.normal.z<-.6 and f.calc_center_median().z<.16)]
    hidden_count=sum(len(f.verts)-2 for f in hidden);bmesh.ops.delete(bm,geom=hidden,context='FACES')
    bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update();tint_mesh(o)
    o.data.calc_loop_triangles();tri=len(o.data.loop_triangles)
    sources[name]=o;library.append({'name':name,'source_name':original,'role':role,'source_triangles':before_tris,'optimized_triangles':tri,'removed_hidden_triangles':hidden_count,'vertices':len(o.data.vertices)})
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(RUN/(name+'.glb')),export_format='GLB',use_selection=True,export_animations=False,export_materials='NONE',export_vertex_color='NAME',export_vertex_color_name='LandscapeTint',export_all_vertex_colors=False)
    # External shared material avoids embedded image copies across library GLBs.
    (RUN/(name+'.tscn')).write_text('[gd_scene format=3]\n\n[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/ConnectedCliff/'+name+'.glb" id="1"]\n[ext_resource type="Material" path="res://Assets/Levels/Environment/WesternStairSlice/SliceMaterial2.tres" id="2"]\n\n[node name="'+name+'" type="Node3D"]\n\n[node name="Geometry" parent="." instance=ExtResource("1")]\n\n[node name="'+name+'" parent="Geometry" index=0]\nmaterial_override = ExtResource("2")\n\n[editable path="Geometry"]\n')
    o.select_set(False);o.hide_render=True;o.hide_set(True)

intervals=[(-2.75,-.25),(7.25,27.25),(29.75,34.75)]
placements=[]
def place(asset,role,x,h,z,sx,sy,sz,angle=0):
    # Fit rotated source bounds, including the sheared overhang, without flattening source vertices.
    start,end=next((a,b) for a,b in intervals if a<=x<=b)
    offsets=[(v.co.x*sx*math.cos(angle)-v.co.y*sz*math.sin(angle),-v.co.x*sx*math.sin(angle)-v.co.y*sz*math.cos(angle)) for v in sources[asset].data.vertices]
    fit=min(1,(x-start-.008)/max(.00001,-min(a for a,b in offsets)),(end-x-.008)/max(.00001,max(a for a,b in offsets)))
    if role!='cliff foot':fit=min(fit,(z-22.26)/max(.00001,-min(b for a,b in offsets)),(23.48-z)/max(.00001,max(b for a,b in offsets)))
    sx*=fit;sz*=fit
    p={'id':'Cliff_'+str(len(placements)+1).zfill(3),'asset':asset,'role':role,'x':round(x,5),'h':round(h,5),'z':round(z,5),'sx':round(sx,5),'sy':round(sy,5),'sz':round(sz,5),'angle':round(angle,5)}
    placements.append(p)
    o=bpy.data.objects.new(p['id'],sources[asset].data);scene.collection.objects.link(o);o.location=(x,-z,h);o.scale=(sx,sz,sy);o.rotation_euler.z=angle
    o['library_asset']=asset;o['placement_id']=p['id']

for start,end in intervals:
    cursor=start;i=0
    while cursor<end-.05:
        span=min(end-cursor,rng.uniform(2.05,3.65));x=cursor+span/2;angle=rng.uniform(-.13,.13)
        sx=min(span+.40,2*min(x-start,end-x)-.02);sz=(1.09-abs(sx*math.sin(angle)))/math.cos(angle)
        # All significant rock volume stays inside the logical cliff band, with open stair corridors.
        asset=rng.choice(['BroadFaceA','BroadFaceB','LayeredOverhang'])
        place(asset,'main face',x,-1.9,22.88,sx,rng.uniform(1.94,2.24),sz,angle)
        crown_x=x+rng.uniform(-.22,.22);crown_sx=min(rng.uniform(1.1,1.8),2*min(crown_x-start,end-crown_x)-.03)
        if i%3!=1:
            place('LayeredOverhang','crown transition',crown_x,-.18,22.93,crown_sx,rng.uniform(.34,.46),.66,rng.uniform(-.12,.12))
        if i%2==0:
            rx=min(end-.25,x+span*.32);place('FractureRib','seam fracture',rx,-1.88,23.15,.34,rng.uniform(1.65,1.94),.49,rng.uniform(-.07,.07))
        # Low foot debris is behind the centers of all legal row-16 cells.
        if i%3!=2:
            place('SeamBoulder','cliff foot',x+rng.uniform(-.2,.2),-1.84,23.48,rng.uniform(.42,1.1),rng.uniform(.14,.24),.36,rng.uniform(-.3,.3))
        cursor+=span;i+=1
for i,(x,h,sx) in enumerate([(10.3,-1.12,1.9),(15.8,-.78,2.2),(20.1,-1.29,1.7),(25.6,-.93,1.8)]):
    place('LayeredOverhang','cross-fracture geological shelf',x,h,23.03,sx,.48,.76,(-.07,.045,-.055,.08)[i])

def edge(x):return 23.12+.13*math.sin(x*.8)+.07*math.sin(x*2.1)
ground_verts=[];ground_faces=[];core_verts=[];core_faces=[]
def strip(start,end,z0,z1,height,cap=False):
    nx=max(1,math.ceil((end-start)/.4));nz=max(1,math.ceil((z1-z0)/.3));off=len(ground_verts)
    for j in range(nz+1):
        for i in range(nx+1):
            x=start+(end-start)*i/nx;z=z0+((edge(x) if cap else z1)-z0)*j/nz;ground_verts.append((x,-z,height))
    for j in range(nz):
        for i in range(nx):
            a=off+j*(nx+1)+i;ground_faces.append((a,a+1,a+nx+2,a+nx+1))
for start,end in intervals:
    strip(start,end,21,22.25,.002);strip(start,end,22.25,23.3,.002,True);strip(start,end,22.8,24.75,-1.798)
    nx=math.ceil((end-start)/.5);off=len(core_verts)
    for h in (-1.92,-1.43,-.72,0):
        for i in range(nx+1):
            x=start+(end-start)*i/nx;z=edge(x)+(.06*math.sin(x*2.6+h*4) if h!=0 else 0);core_verts.append((x,-z,h))
    for j in range(3):
        for i in range(nx):
            a=off+j*(nx+1)+i;core_faces.append((a,a+1,a+nx+2,a+nx+1))
strip(27.25,29.75,21,22.25,.002)
ground=mesh('AdjacentGround',ground_verts,ground_faces);core=mesh('ContinuousGeologicalCore',core_verts,core_faces)
for o,g in ((ground,True),(core,False)):
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update();tint_mesh(o,g)
bpy.ops.object.select_all(action='DESELECT');ground.select_set(True);core.select_set(True);bpy.context.view_layer.objects.active=ground
bpy.ops.export_scene.gltf(filepath=str(RUN/'CliffTransitions.glb'),export_format='GLB',use_selection=True,export_animations=False,export_materials='NONE',export_vertex_color='NAME',export_vertex_color_name='LandscapeTint',export_all_vertex_colors=False)
(RUN/'CliffTransitions.tscn').write_text('[gd_scene format=3]\n\n[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/ConnectedCliff/CliffTransitions.glb" id="1"]\n[ext_resource type="Material" path="res://Assets/Levels/Environment/WesternStairSlice/SliceMaterial0.tres" id="ground"]\n[ext_resource type="Material" path="res://Assets/Levels/Environment/WesternStairSlice/SliceMaterial2.tres" id="rock"]\n\n[node name="CliffTransitions" type="Node3D"]\n\n[node name="Geometry" parent="." instance=ExtResource("1")]\n\n[node name="AdjacentGround" parent="Geometry" index=0]\nmaterial_override = ExtResource("ground")\n\n[node name="ContinuousGeologicalCore" parent="Geometry" index=1]\nmaterial_override = ExtResource("rock")\n\n[editable path="Geometry"]\n')
for o in (ground,core):o.data.calc_loop_triangles()
doc={'library':library,'placements':placements,'source_assets':source_records,'intervals_world_x':intervals,'ground_triangles':len(ground.data.loop_triangles),'core_triangles':len(core.data.loop_triangles),'unique_library_triangles':sum(a['optimized_triangles'] for a in library),'instance_expanded_rock_triangles':sum(next(a['optimized_triangles'] for a in library if a['name']==p['asset']) for p in placements),'materials_reused':['Assets/Levels/Environment/WesternStairSlice/SliceMaterial0.tres','Assets/Levels/Environment/WesternStairSlice/SliceMaterial2.tres'],'new_textures':0}
(SRC/'Composition.json').write_text(json.dumps(doc,indent=2))
# A full editable preview includes linked copies of the unchanged western slice and existing eastern tread profile.
with bpy.data.libraries.load(str(ROOT/'SourceAssets/Environment/WesternStairSlice/WesternStairSlice.blend'),link=False) as (src,dst):dst.objects=['Blended moss earth and grass','Weathered charcoal escarpment','Worn slate steps and masonry','Desaturated understory']
for o in dst.objects:
    scene.collection.objects.link(o);o['preview_only']='Existing western slice; not exported into cliff library'
for i,top in enumerate([-.12,-.36,-.6,-.74,-.86,-.96,-1.08,-1.2,-1.44,-1.68]):
    z=22.25+i*.25
    vv=[(x,-zz,h) for h in (top-.15,top) for zz in (z,z+.25) for x in (27.25,29.75)]
    o=mesh('Eastern existing step reference '+str(i),vv,[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]);tint_mesh(o);o.data.materials[0]=step_mat
    for c in o.data.color_attributes['LandscapeTint'].data:c.color=(.22,.235,.225,1)
    o['preview_only']='Existing authored step profile reference; not exported'
scene.world=bpy.data.worlds.new('Cliff review ambient');scene.world.use_nodes=True;bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.45,.53,.62,1);bg.inputs['Strength'].default_value=.65
bpy.ops.object.light_add(type='AREA',location=(14,-22,12));bpy.context.object.data.energy=5000;bpy.context.object.data.size=22
bpy.ops.object.camera_add(location=(27,-51,18));cam=bpy.context.object;cam.rotation_euler=(Vector((16,-23.3,-.6))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=37;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=1800;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(QA/'BlenderFullCliff.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'ConnectedCliff.blend'))
print('STAGE30C LIBRARY AND COMPOSITION BUILT',json.dumps({k:v for k,v in doc.items() if k not in ('placements','source_assets','library')}),flush=True)
if '--render' in __import__('sys').argv:bpy.ops.render.render(write_still=True)
