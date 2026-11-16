"""Construct a connected visual-only slice from licensed Nature sources and fitted terrain."""
import bpy,bmesh,math,random,json,pathlib,hashlib
import numpy as np
from mathutils import Vector,Matrix
ROOT=pathlib.Path(__file__).resolve().parents[1]; SRC=ROOT/'SourceAssets/Environment/WesternStairSlice'; RUN=ROOT/'Assets/Levels/Environment/WesternStairSlice'; QA=SRC/'QA'
for p in (SRC,RUN,QA,SRC/'Textures'): p.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True); rng=random.Random(30021)
inventory=json.loads((ROOT/'SourceAssets/Environment/Manifests/AssetInventory.json').read_text())['assets']
selection=[]; objects=[]
def xyz(x,h,z): return (x,-z,h)
def make_image(name,array,noncolor=False):
    im=bpy.data.images.new(name,width=array.shape[1],height=array.shape[0]); im.colorspace_settings.name='Non-Color' if noncolor else 'sRGB'
    im.pixels.foreach_set(array.astype(np.float32).ravel()); im.filepath_raw=str(SRC/'Textures'/(name+'.png')); im.file_format='PNG'; im.save(); im.pack(); return im
n=512; yy,xx=np.mgrid[0:n,0:n]/n
macro=.94+.025*np.sin(xx*math.tau*3+np.sin(yy*math.tau*2))+.012*np.sin(yy*math.tau*7+xx*math.tau)
arr=np.ones((n,n,4)); arr[:,:,:3]=macro[:,:,None]; detail=make_image('WeatheredBroadDetail',arr)
height=.014*np.sin(xx*math.tau*7+np.sin(yy*math.tau*3))+.009*np.sin(yy*math.tau*13)
dx=np.gradient(height,axis=1)*7; dy=np.gradient(height,axis=0)*7
norm=np.dstack((-dx,-dy,np.ones_like(dx)));norm/=np.linalg.norm(norm,axis=2)[:,:,None];arr[:,:,:3]=norm*.5+.5
normal=make_image('RestrainedStoneNormal',arr,True)
def material(name,col):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links
    bs=next(x for x in n if x.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.9;bs.inputs['Metallic'].default_value=0
    vc=n.new('ShaderNodeVertexColor');vc.layer_name='LandscapeTint'
    tex=n.new('ShaderNodeTexImage');tex.image=detail
    mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;l.new(vc.outputs['Color'],mix.inputs[1]);l.new(tex.outputs['Color'],mix.inputs[2]);l.new(mix.outputs[0],bs.inputs['Base Color'])
    tn=n.new('ShaderNodeTexImage');tn.image=normal;nm=n.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.2;l.new(tn.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs['Normal'],bs.inputs['Normal'])
    m.diffuse_color=(*col,1);return m
rock=material('Weathered charcoal escarpment',(.14,.16,.17)); stone=material('Worn slate steps and masonry',(.235,.25,.24));ground=material('Blended moss earth and grass',(.19,.2,.12));foliage=material('Desaturated understory',(.10,.16,.065))
def finish(obj,mat,color,uvscale=2.8):
    obj.data.materials.clear();obj.data.materials.append(mat)
    for p in obj.data.polygons:p.material_index=0
    uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='WorldSurfaceUV')
    tint=obj.data.color_attributes.get('LandscapeTint') or obj.data.color_attributes.new(name='LandscapeTint',type='FLOAT_COLOR',domain='CORNER')
    for old in list(obj.data.color_attributes):
        if old.name!='LandscapeTint':obj.data.color_attributes.remove(old)
    obj.data.color_attributes.active_color=tint
    for p in obj.data.polygons:
        for li in p.loop_indices:
            co=obj.matrix_world@obj.data.vertices[obj.data.loops[li].vertex_index].co
            # Different projections on escarpment faces and standing surfaces.
            uv.data[li].uv=((co.x+co.z*.31)/uvscale,(-co.y if abs(p.normal.z)>.65 else co.z)/uvscale)
            tint.data[li].color=(*(color(co,p) if callable(color) else color),1)
    objects.append(obj);return obj
def mesh(name,verts,faces,mat,color):
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update();o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o)
    bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
    return finish(o,mat,color)
def mix(a,b,t):return tuple(a[i]*(1-t)+b[i]*t for i in range(3))
def ground_color(co,p):
    x,z=co.x,-co.y; lower=co.z<-1
    bend=3.5+.55*math.sin((z-20)*1.1); distance=abs(x-bend)
    t=max(0,min(1,(distance-1.0)/1.25+.1*math.sin(x*3+z)))
    soil=(.23,.185,.12) if lower else (.225,.22,.17); grass=(.135,.18,.082)
    color=mix(soil,grass,t); edge=min(x+.25,7.25-x,z-19.75,27.25-z)
    # Value and hue ease into the retained greybox along this small slice's outer boundary.
    if edge<.65: color=mix((.262,.239,.147) if lower else (.179,.230,.106),color,max(0,edge/.65))
    variation=.96+.045*math.sin(x*2.3+z*1.7);return tuple(c*variation for c in color)
def surface(name,x0,x1,z0,z1,h):
    nx=round((x1-x0)/.25);nz=round((z1-z0)/.25);verts=[];faces=[]
    for j in range(nz+1):
        for i in range(nx+1):
            x=x0+(x1-x0)*i/nx;z=z0+(z1-z0)*j/nz
            if 'cliff crown' in name.lower():
                edge=23.22+.09*math.sin(x*3.5)+.04*math.sin(x*7)
                z=z0+(edge-z0)*j/nz
            verts.append(xyz(x,h+.002,z))
    for j in range(nz):
        for i in range(nx):
            a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    return mesh(name,verts,faces,ground,ground_color)
surface('Connected upper plateau',-.25,7.25,19.75,22.25,0)
surface('West cliff crown',-.25,2.25,22.25,23.5,0)
surface('East cliff crown',4.75,7.25,22.25,23.5,0)
surface('Connected lower path and landing',-.25,7.25,24.75,27.25,-1.8)
surface('Lower west cliff foot',-.25,2.25,22.75,24.75,-1.8)
surface('Lower east cliff foot',4.75,7.25,22.75,24.75,-1.8)

def rock_color(co,p):
    noise=.92+.10*math.sin(co.x*3+co.z*4-co.y)+.035*math.sin(co.y*8)
    base=(.14,.16,.17); moss=max(0,min(.32,(co.z+.6)*.22)) if p.normal.z>.25 else .03
    return tuple(c*noise for c in mix(base,(.15,.19,.10),moss))
def backing(name,x0,x1):
    verts=[];faces=[];count=13
    for j,h in enumerate((-1.9,-1.5,-.8,-.22,0)):
        for i in range(count):
            x=x0+(x1-x0)*i/(count-1);edge=min(i,count-1-i)
            z=23.17-.13*math.sin(i*1.7+j*1.2)-(.08 if j==4 else 0)
            verts.append(xyz(x,h,z))
    for j in range(4):
        for i in range(count-1):
            a=j*count+i;faces.append((a,a+1,a+count+1,a+count))
    mesh(name,verts,faces,rock,rock_color)
backing('Continuous west fractured escarpment',-.25,2.25);backing('Continuous east fractured escarpment',4.75,7.25)

templates={}
def source(name):
    if name in templates:return templates[name]
    a=next(a for a in inventory if a['name']==name and 'StylizedNatureMegaKit' in a['pack']);assert a['license_status']=='CC0-bundled'
    before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(ROOT/a['source']))
    imported=[o for o in bpy.data.objects if o not in before];meshes=[o for o in imported if o.type=='MESH']
    verts=[];faces=[]
    for o in meshes:
        offset=len(verts);verts.extend([o.matrix_world@v.co for v in o.data.vertices]);faces.extend([tuple(offset+i for i in p.vertices) for p in o.data.polygons])
    lo=Vector([min(v[i] for v in verts) for i in range(3)]);hi=Vector([max(v[i] for v in verts) for i in range(3)]);size=hi-lo
    # Store Godot-oriented normalized coordinates, with the source pivot removed.
    normalized=[((v.x-lo.x)/size.x-.5,(v.z-lo.z)/max(size.z,.0001),( -(v.y-lo.y)/size.y+.5)) for v in verts]
    for o in imported:bpy.data.objects.remove(o,do_unlink=True)
    templates[name]=(normalized,faces)
    selection.append({'catalog_id':a['id'],'name':name,'source':a['source'],'source_objects':a['source_objects_exact'],'license':'CC0-1.0','license_document':'SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/License_Standard.txt','source_sha256':hashlib.sha256((ROOT/a['source']).read_bytes()).hexdigest(),'adaptation':'Source geometry normalized, transformed and tinted in derived scene; originals unchanged'})
    return templates[name]
def instance(name,label,x,z,h,sx,sy,sz,angle=0,clamp=None,mat=rock):
    vv,ff=source(name);v=[]
    rotated=[(a*math.cos(angle)-c*math.sin(angle),b,a*math.sin(angle)+c*math.cos(angle)) for a,b,c in vv]
    ax0=min(q[0] for q in rotated);ax1=max(q[0] for q in rotated);az0=min(q[2] for q in rotated);az1=max(q[2] for q in rotated)
    for a,b,c in rotated:
        qx=((a-ax0)/(ax1-ax0)-.5)*sx;qz=((c-az0)/(az1-az0)-.5)*sz
        xx=x+qx;zz=z+qz;hh=h+b*sy
        if clamp:
            # Scale complete source silhouettes into the safe envelope instead of flattening vertices at grid planes.
            xx=x+qx*min(1,2*min(x-clamp[0],clamp[1]-x)/sx)
            zz=z+qz*min(1,2*min(z-clamp[2],clamp[3]-z)/sz)
        v.append(xyz(xx,hh,zz))
    col=rock_color if mat==rock else (lambda co,p:(.09+.015*math.sin(co.x*3),.145,.06))
    return mesh(label,v,ff,mat,col)
for side,(x0,x1) in enumerate(((-.25,2.25),(4.75,7.25))):
    for i in range(4):
        x=x0+.45+(x1-x0-.9)*i/3
        instance('Rock_Medium_'+str((i+side)%3+1),f'Escarpment mass {side}-{i}',x,22.98,-1.91,1.35+rng.random()*.35,2.04+rng.random()*.16,1.05,rng.uniform(-.7,.7),(x0,x1,22.27,23.47))
    for i in range(3):
        instance('Rock_Medium_'+str((i+1)%3+1),f'Weathered cap {side}-{i}',x0+.4+i*.8,23.05,-.12,.9,.17,.65,rng.random(),(x0,x1,22.3,23.48))

# Chipped polygon slabs rather than repeated full-width boxes. Central tread heights exactly match authored gameplay.
def slab(name,x0,x1,z0,z1,top,depth=.13):
    chip=min(.055,(z1-z0)*.15);points=[(x0+chip,z0),(x1-chip,z0),(x1,z0+chip),(x1,z1-chip),(x1-chip,z1),(x0+chip,z1),(x0,z1-chip),(x0,z0+chip)]
    verts=[xyz(x,h,z) for h in (top-depth,top) for x,z in points];faces=[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    tint=.9+rng.random()*.17;o=mesh(name,verts,faces,stone,(.22*tint,.235*tint,.225*tint))
    bevel=o.modifiers.new('Worn edges','BEVEL');bevel.width=.018;bevel.segments=1
    bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.modifier_apply(modifier=bevel.name);o.select_set(False)
    # Rebuild color/UV data on bevel-created loops.
    objects.remove(o);finish(o,stone,(.22*tint,.235*tint,.225*tint));return o
tops=[-.12,-.36,-.6,-.74,-.86,-.96,-1.08,-1.2,-1.44,-1.68]
for i,h in enumerate(tops):
    z0=22.25+i*.25;cuts=[2.25,3.02+(i%2)*.18,3.92-(i%2)*.12,4.75]
    for j in range(3):slab(f'Worn step {i+1:02} slab {j+1}',cuts[j]+.004,cuts[j+1]-.004,z0+.002,z0+.248,h, .23 if i<2 else .17)
    # Closed dark riser backing conceals seams beneath worn nosings.
    slab(f'Embedded stair core {i+1:02}',2.25,4.75,z0,z0+.25,h-.035,1.82+h)
# Low broken retaining lips stay completely outside the legal two-column corridor.
for side in (0,1):
    for i in range(7):
        z=22.25+i*.35;h=-.6*(z-21.625)/1.25
        x0,x1=(2.08,2.245) if side==0 else (4.755,4.92)
        slab(f'Buried stair cheek {side}-{i}',x0,x1,z,z+.34,min(.06,h+.16),.3)
# Weathered paving fragments form an irregular apron that dissolves into the dirt path.
paved=[]
for i in range(50):
    x=rng.uniform(.0,7);z=rng.uniform(19.85,22.15)
    if abs(x-(3.5+.55*math.sin(z)))>1.5 and rng.random()<.55:continue
    w=rng.uniform(.35,.68);d=rng.uniform(.28,.5)
    if any(abs(x-a)<(w+c)/2+.025 and abs(z-b)<(d+e)/2+.025 for a,b,c,e in paved):continue
    paved.append((x,z,w,d));slab(f'Upper ancient paving {i}',max(-.24,x-w/2),min(7.24,x+w/2),z-d/2,z+d/2,.018,.07)
paved=[]
for i in range(11):
    x=rng.uniform(2.55,4.45);z=rng.uniform(24.95,25.9)
    if any(abs(x-a)<.42 and abs(z-b)<.33 for a,b in paved):continue
    paved.append((x,z));slab(f'Lower landing flagstone {i}',x-.19,x+.19,z-.14,z+.14,-1.782,.07)
# Restrained details: no tall rock or plant occupies legal stair treads or landing centers.
for i,(x,z,h) in enumerate([(0.4,23.43,-1.80),(1.75,23.44,-1.80),(5.15,23.44,-1.80),(6.75,23.44,-1.80),(.3,22.8,0),(6.8,22.65,0)]):
    instance('Pebble_Round_1',f'Loose cliff stone {i}',x,z,h,.23,.09,.21,i*.73,mat=rock)
for i,(x,z,h) in enumerate([(0.05,21.8,0),(1.0,22.0,0),(6.5,22.0,0),(7.0,21.4,0),(.1,24.0,-1.8),(1.1,23.7,-1.8),(5.9,23.72,-1.8),(6.9,24.4,-1.8),(1.6,26.5,-1.8),(5.5,26.8,-1.8)]):
    instance('Grass_Common_Short',f'Sparse grass {i}',x,z,h,.23,.16,.23,i*.7,mat=foliage)
for i,(x,z,h) in enumerate([(.45,22.7,0),(6.5,22.7,0),(.65,23.75,-1.8),(6.6,23.8,-1.8)]):
    instance('Fern_1',f'Cliff fern {i}',x,z,h,.30,.19,.3,i*1.3,mat=foliage)

scene=bpy.context.scene
# Merge by material to four runtime meshes/surfaces, keeping detailed source assembly separately in baseline script.
for mat in (rock,stone,ground,foliage):
    batch=[o for o in scene.objects if o.type=='MESH' and o.data.materials and o.data.materials[0]==mat]
    bpy.ops.object.select_all(action='DESELECT')
    for o in batch:o.select_set(True)
    bpy.context.view_layer.objects.active=batch[0];bpy.ops.object.join();batch[0].name=mat.name
models=[o for o in scene.objects if o.type=='MESH']
metrics={}
for o in models:
    o.data.calc_loop_triangles();metrics[o.name]={'triangles':len(o.data.loop_triangles),'vertices':len(o.data.vertices),'materials':len(o.data.materials)}
    assert all(math.isfinite(c) for v in o.data.vertices for c in v.co)
# Export only meshes, without camera, collision, navigation or runtime scripts.
bpy.ops.object.select_all(action='DESELECT')
for o in models:o.select_set(True)
bpy.context.view_layer.objects.active=models[0]
bpy.ops.export_scene.gltf(filepath=str(RUN/'WesternStairSlice.glb'),export_format='GLB',use_selection=True,export_animations=False,export_yup=True,export_vertex_color='NAME',export_vertex_color_name='LandscapeTint',export_all_vertex_colors=False)
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.world=bpy.data.worlds.new('Review ambient');scene.world.use_nodes=True
bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.45,.53,.62,1);bg.inputs['Strength'].default_value=.65
bpy.ops.object.light_add(type='AREA',location=xyz(0,10,20));bpy.context.object.data.energy=1600;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=8
bpy.ops.object.camera_add(location=xyz(11,8,34));cam=bpy.context.object;cam.rotation_euler=(Vector(xyz(3.5,-.5,23.5))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=11.7;scene.camera=cam
scene.render.resolution_x=1600;scene.render.resolution_y=1200;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(QA/'BlenderPerspective.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'WesternStairSlice.blend'))
(SRC/'SelectedAssets.json').write_text(json.dumps({'assets':selection,'excluded':'Poly Haven resources and timestamped ruins remain held for provenance; stairs precision-authored after evaluating KayKit/Kenney dimensions','license_gate':'Only bundled CC0 Nature Standard geometry used','metrics':metrics,'total_triangles':sum(x['triangles'] for x in metrics.values()),'materials':4,'standing_heights':{'upper':0,'row15':-.6,'row16':-1.2,'lower':-1.8},'stair_tread_tops':tops},indent=2))
bpy.ops.render.render(write_still=True)
print('STAGE30B BUILD COMPLETE',json.dumps(metrics),flush=True)
