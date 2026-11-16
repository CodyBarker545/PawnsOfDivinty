"""Deterministic cliff pair. Run with Blender --background --factory-startup --python-exit-code 1 --python this_file."""
import json
import math
from pathlib import Path

import bpy
import bmesh

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'SourceAssets/levels/sources/ModularCliff'
RUNTIME = ROOT / 'Assets/Levels/Props/ModularCliff'
SOURCE.mkdir(parents=True, exist_ok=True)
RUNTIME.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

# Author in Godot coordinates (X right, Y up, +Z exposed south); convert to Blender Z-up.
XS = [-.625, -.34, 0, .32, .625]
FACE = [(-1.85,.60),(-1.52,.59),(-1.43,.49),(-1.10,.60),
        (-1.01,.48),(-.67,.61),(-.58,.51),(-.22,.60),(-.18,.58)]
CAP = [(-.18,.58),(-.055,.625),(0,.56)]

def linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4

def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = tuple(linear(c/255) for c in color) + (1,)
    bsdf.inputs['Roughness'].default_value = .88
    bsdf.inputs['Metallic'].default_value = 0
    return mat

stone = material('Cliff slate - matte broad strata', (86,94,95))
shade = material('Cliff slate - recessed strata', (66,74,76))
weather = material('Cliff slate - weathered edge', (103,111,107))
MATERIALS = [stone, shade, weather]

def profile(rows, i, cap):
    # Identical mating profiles at both X ends; broad internal folds, no random noise.
    points = [(rows[0][0], -.625)]
    for j,(height,front) in enumerate(rows):
        interior = math.sin(math.pi*i/(len(XS)-1))
        if not cap and j not in (0,len(rows)-1):
            height += .025*interior*math.sin(j*1.7+i)
            front -= .035*interior*(.5+.5*math.cos(i+j*1.3))
        points.append((height,front))
    points.append((rows[-1][0], -.625))
    return points

def build(name, rows, cap=False):
    verts=[]
    rings=[]
    for i,x in enumerate(XS):
        ring=[]
        for y,z in profile(rows,i,cap):
            ring.append(len(verts)); verts.append((x,-z,y))
        rings.append(ring)
    count=len(rings[0]); faces=[]
    for a,b in zip(rings,rings[1:]):
        for j in range(count):
            k=(j+1)%count; faces.append((a[j],b[j],b[k],a[k]))
    faces += [tuple(reversed(rings[0])),tuple(rings[-1])]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    for mat in MATERIALS:mesh.materials.append(mat)
    for p in mesh.polygons:
        band=p.index%count
        p.material_index=2 if cap else (1 if band in (2,4,6) else 0)
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    assert all(len(e.link_faces)==2 for e in bm.edges), 'Non-manifold cliff mesh'
    bm.to_mesh(mesh);bm.free();mesh.update()
    uv=mesh.uv_layers.new(name='Cliff planar UV')
    for p in mesh.polygons:
        axis=max(range(3),key=lambda a:abs(p.normal[a]))
        axes=[a for a in range(3) if a!=axis]
        for li in p.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv=(co[axes[0]]/1.25,co[axes[1]]/1.25)
    # End profiles must translate exactly onto their neighbors.
    ends=[sorted((round(v.co.y,6),round(v.co.z,6)) for v in mesh.vertices if abs(v.co.x-x)<1e-6) for x in (-.625,.625)]
    assert ends[0]==ends[1]
    return obj

face=build('ModularCliffFace',FACE)
cap=build('ModularCliffEdgeCap',CAP,True)
report={}
for obj in (face,cap):
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.gltf(filepath=str(RUNTIME/(obj.name+'.glb')),export_format='GLB',use_selection=True,export_animations=False,export_yup=True)
    report[obj.name]={'vertices':len(obj.data.vertices),'triangles':len(obj.data.polygons),'dimensions_blender_xyz':list(obj.dimensions),'end_profiles_match':True,'manifold':True,'material_slots':len(obj.data.materials)}
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'ModularCliff.blend'))
(ROOT/'artifacts/stage23a_blender_metrics.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
