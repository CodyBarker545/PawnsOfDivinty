"""Run only in isolated background Blender, with --disable-autoexec."""
import bpy, json, pathlib, math, hashlib, struct, traceback
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[1]
BASE=ROOT/'SourceAssets/Environment'; MAN=BASE/'Manifests'; QA=BASE/'QA/ContactSheets'
QA.mkdir(parents=True,exist_ok=True)
doc=json.loads((MAN/'AssetInventory.json').read_text()); assets=doc['assets']
bpy.context.preferences.filepaths.use_scripts_auto_execute=False
def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)
def load(a):
    p=ROOT/a['source']
    if p.suffix=='.blend':
        with bpy.data.libraries.load(str(p),link=False) as (src,dst):
            a['source_collections']=list(src.collections); a['source_object_names']=list(src.objects); dst.objects=src.objects
        obs=[o for o in dst.objects if o]
        for o in obs: bpy.context.scene.collection.objects.link(o)
    else:
        before=set(bpy.data.objects)
        if p.suffix in ('.glb','.gltf'): bpy.ops.import_scene.gltf(filepath=str(p))
        elif p.suffix=='.obj': bpy.ops.wm.obj_import(filepath=str(p))
        elif p.suffix=='.fbx': bpy.ops.import_scene.fbx(filepath=str(p),use_anim=False)
        obs=[o for o in bpy.data.objects if o not in before]
        a['source_object_names']=[o.name for o in obs]
    return obs
def inspect(a,obs):
    meshes=[o for o in obs if o.type=='MESH']; coords=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
    lo=Vector(tuple(min(c[i] for c in coords) for i in range(3))) if coords else Vector((0,0,0))
    hi=Vector(tuple(max(c[i] for c in coords) for i in range(3))) if coords else Vector((0,0,0))
    tris=0; records=[]; sig=hashlib.sha256()
    for o in meshes:
        m=o.data; m.calc_loop_triangles(); tris+=len(m.loop_triangles)
        records.append({'name':o.name,'dimensions_blender_xyz':list(o.dimensions),'triangles':len(m.loop_triangles),'vertices':len(m.vertices),'uv_layers':[u.name for u in m.uv_layers],'materials':[s.material.name if s.material else None for s in o.material_slots],'modifiers':[x.type for x in o.modifiers]})
        sig.update(struct.pack('<II',len(m.vertices),len(m.polygons)))
        for v in m.vertices: sig.update(struct.pack('<3f',*v.co))
        for t in m.loop_triangles: sig.update(struct.pack('<3I',*t.vertices))
    mats={s.material for o in meshes for s in o.material_slots if s.material}
    # Material-only Poly Haven libraries have no objects: preserve material datablocks as candidates.
    if not mats and '/Terrain/' in a['pack']:
        with bpy.data.libraries.load(str(ROOT/a['source']),link=False) as (src,dst): dst.materials=src.materials
        mats={m for m in dst.materials if m}
    images={n.image for m in mats if m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
    deps=[]
    for im in images:
        raw=im.filepath
        path=pathlib.Path(bpy.path.abspath(raw, start=str((ROOT/a['source']).parent)))
        if not path.exists():
            matches=list((ROOT/a['pack']).rglob(path.name))
            if len(matches)==1: im.filepath=str(matches[0]); path=matches[0]
        deps.append({'image':im.name,'original_reference':raw,'resolved_path':str(path),'packed':bool(im.packed_file),'exists':bool(im.packed_file) or path.exists(),'pixels':list(im.size)})
    a.update(mesh_objects=records,mesh_object_count=len(meshes),contains_multiple_objects=len(meshes)>1,dimensions_blender_xyz=list(hi-lo),triangles=tris,geometry_signature=sig.hexdigest() if meshes else None,materials=[m.name for m in mats],texture_dependencies=deps,animation_actions=[x.name for x in bpy.data.actions],inspection_status='inspected',optimization=('Decimate/LOD and bake review required' if tris>20000 else 'Scale, material slots and tactical silhouette review before export'))
    a['resource_type']='material' if '/Terrain/' in a['pack'] else 'model'
    return meshes,lo,hi,mats
def stage():
    s=bpy.context.scene
    s.render.engine='BLENDER_WORKBENCH'; s.render.resolution_x=1800; s.render.resolution_y=1500; s.render.resolution_percentage=100
    s.world=bpy.data.worlds.new('CatalogWorld'); s.world.color=(.15,.15,.15)
    sh=s.display.shading; sh.light='STUDIO'; sh.color_type='MATERIAL'; sh.show_shadows=True; sh.show_cavity=True; sh.cavity_type='BOTH'; sh.background_type='WORLD'
    bpy.ops.object.camera_add(location=(0,-35,36)); cam=bpy.context.object; cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=31; s.camera=cam
    s.render.image_settings.file_format='PNG'
    return s
categories=list(dict.fromkeys(a['category'] for a in assets))
results=[]; pages=[]
for cat in categories:
    subset=[a for a in assets if a['category']==cat]
    for start in range(0,len(subset),20):
        clean(); s=stage(); page=subset[start:start+20]
        for j,a in enumerate(page):
            try:
                obs=load(a); meshes,lo,hi,mats=inspect(a,obs)
                if a['resource_type']=='material':
                    for o in obs: o.hide_render=True
                    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16); sphere=bpy.context.object
                    if mats: sphere.data.materials.append(sorted(mats,key=lambda m:m.name)[0])
                    meshes=[sphere]; lo=Vector((-1,-1,-1)); hi=Vector((1,1,1))
                for o in obs:
                    if o.type!='MESH': o.hide_render=True
                scale=3.7/max(max(hi-lo),.01); center=(hi+lo)/2
                x=(j%5-2)*5.6; y=(1.5-j//5)*7.5
                # Transform world matrices together, retaining assembly relationships without parent double transform.
                from mathutils import Matrix
                xf=Matrix.Translation(Vector((x,y,1.9))-center*scale)@Matrix.Scale(scale,4)
                original={o:o.matrix_world.copy() for o in meshes}
                for o in meshes: o.parent=None
                for o in meshes: o.matrix_world=xf@original[o]; o.hide_render=False; o.hide_set(False)
                a['preview_page']=f'{cat.replace(" ","_")}_{start//20+1:02}.png'; a['preview_cell']=j
                print(a['id'],a['name'],a['triangles'],flush=True)
            except Exception:
                a['inspection_status']='failed'; a['inspection_error']=traceback.format_exc(); print(a['inspection_error'],flush=True)
            results.append(a)
        s.render.filepath=str(QA/(cat.replace(' ','_')+f'_{start//20+1:02}_raw.png'))
        bpy.ops.render.render(write_still=True)
        pages.append({'category':cat,'raw':str(pathlib.Path(s.render.filepath).relative_to(ROOT)),'assets':[a['id'] for a in page]})
        (MAN/'BlenderInspection.json').write_text(json.dumps({'assets':results,'pages':pages,'blender_version':bpy.app.version_string},indent=2))
print('CATALOG COMPLETE',len(results),flush=True)
