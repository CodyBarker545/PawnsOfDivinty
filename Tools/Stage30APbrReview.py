"""Supplement geometry catalogs with actual source shader previews; no source saves."""
import pathlib
prefix=(pathlib.Path(__file__).parent/'Stage30ABlenderCatalog.py').read_text().split('categories=list')[0]
exec(compile(prefix,'Stage30ABlenderCatalog.py','exec'))
from mathutils import Matrix
chosen=[a for a in assets if 'PolyHaven' in a['pack']]+[next(a for a in assets if a['name']==n and k in a['pack']) for n,k in [('CommonTree_1','Nature'),('Pine_1','Nature'),('Bush_Common','Nature'),('Grass_Common_Short','Nature'),('Wall_Broken','Ruins'),('Arch_Gothic','Ruins'),('Crate','Ruins'),('tower-square','Kenney')]]
clean(); s=stage(); s.render.engine='CYCLES'; s.cycles.samples=12; s.cycles.use_denoising=True
s.world.use_nodes=True; bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND'); bg.inputs['Color'].default_value=(.32,.36,.42,1); bg.inputs['Strength'].default_value=.7
bpy.ops.object.light_add(type='SUN',location=(0,-5,15)); bpy.context.object.rotation_euler=(.4,-.5,-.3); bpy.context.object.data.energy=2
for j,a in enumerate(chosen):
    obs=load(a); meshes,lo,hi,mats=inspect(a,obs)
    material=a['name'] not in ('boulder_01_4k','mountainside_4k') and 'PolyHaven' in a['pack']
    if material:
        for o in obs: o.hide_render=True
        bpy.ops.mesh.primitive_uv_sphere_add(segments=40,ring_count=24); o=bpy.context.object
        o.data.materials.append(next(m for m in mats if m.use_nodes)); meshes=[o]; lo=Vector((-1,-1,-1)); hi=Vector((1,1,1))
    else:
        # Poly Haven supplies LOD meshes: display the highest-detail source mesh alone.
        if 'PolyHaven' in a['pack']:
            meshes.sort(key=lambda o:len(o.data.polygons),reverse=True)
            for o in meshes[1:]: o.hide_render=True
            meshes=meshes[:1]
            points=[meshes[0].matrix_world@Vector(c) for c in meshes[0].bound_box]
            lo=Vector([min(c[i] for c in points) for i in range(3)]); hi=Vector([max(c[i] for c in points) for i in range(3)])
    for o in obs:
        if o.type!='MESH': o.hide_render=True
    scale=3.7/max(max(hi-lo),.01); center=(hi+lo)/2; x=(j%5-2)*5.6; y=(1.5-j//5)*7.5
    xf=Matrix.Translation(Vector((x,y,1.9))-center*scale)@Matrix.Scale(scale,4)
    old={o:o.matrix_world.copy() for o in meshes}
    for o in meshes: o.parent=None
    for o in meshes: o.matrix_world=xf@old[o]; o.hide_render=False
    print('PBR',a['name'],flush=True)
for im in bpy.data.images:
    if im.size[0]>1024: im.scale(1024,max(1,int(im.size[1]*1024/im.size[0])))
s.render.filepath=str(QA/'Source_material_review_raw.png'); bpy.ops.render.render(write_still=True)
(MAN/'PbrReview.json').write_text(json.dumps({'assets':[a['id'] for a in chosen],'method':'Cycles 12 samples, source shaders, temporary in-memory image reduction to 1K; normalized size, no source files modified'},indent=2))
