"""Run with Blender --background --factory-startup --python Tools/BuildModularStoneWall.py."""
from pathlib import Path
import bpy
import bmesh
from mathutils import Matrix


root = Path(__file__).resolve().parents[1]
source = root / "SourceAssets/levels/sources/ModularStoneWall"
textures = root / "SourceAssets/levels/textures/ModularStoneWall"
runtime = root / "Assets/Levels/Props/ModularStoneWall"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source / "Meshy/GeneratedWall.glb"))
high = next(o for o in bpy.context.selected_objects if o.type == 'MESH')
high.name = "Meshy source"
low = high.copy()
low.data = high.data.copy()
bpy.context.collection.objects.link(low)
low.name = "ModularStoneWall"
bpy.ops.object.select_all(action='DESELECT')
low.select_set(True)
bpy.context.view_layer.objects.active = low

# A voxel surface removes the generated mesh's split seams and internal faces.
low.data.remesh_voxel_size = .075
bpy.ops.object.voxel_remesh()
bm = bmesh.new()
bm.from_mesh(low.data)
remaining = set(bm.verts)
islands = []
while remaining:
    stack = [remaining.pop()]
    island = []
    while stack:
        vertex = stack.pop()
        island.append(vertex)
        for edge in vertex.link_edges:
            other = edge.other_vert(vertex)
            if other in remaining:
                remaining.remove(other)
                stack.append(other)
    islands.append(island)
largest = max(islands, key=len)
bmesh.ops.delete(bm, geom=[v for group in islands if group is not largest for v in group], context='VERTS')
bmesh.ops.triangulate(bm, faces=list(bm.faces))
bm.to_mesh(low.data)
bm.free()
decimation = low.modifiers.new("Tactical silhouette", 'DECIMATE')
decimation.ratio = min(1, 980 / len(low.data.polygons))
bpy.ops.object.modifier_apply(modifier=decimation.name)

# Bake a single half. Its reflected copy makes both mating profiles identical.
bm = bmesh.new()
bm.from_mesh(low.data)
left = min(v.co.x for v in bm.verts)
right = max(v.co.x for v in bm.verts)
middle = (left + right) / 2
for cut, keep_right in [(left + (right-left)*.07, True), (middle, False)]:
    sliced = bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
        plane_co=(cut,0,0), plane_no=(1,0,0), dist=.00001, clear_inner=keep_right, clear_outer=not keep_right)
    if keep_right:
        rim = [e for e in sliced['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and e.is_valid and e.is_boundary]
        bmesh.ops.holes_fill(bm, edges=rim, sides=0)
bm.to_mesh(low.data)
bm.free()
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(island_margin=.025)
bpy.ops.object.mode_set(mode='OBJECT')
material = bpy.data.materials.new("Neutral limestone")
material.use_nodes = True
low.data.materials.clear()
low.data.materials.append(material)
nodes = material.node_tree.nodes
links = material.node_tree.links
shader = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
high.select_set(True)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 1
for role, kind in [('Color','DIFFUSE'), ('Roughness','ROUGHNESS'), ('Normal','NORMAL')]:
    img = bpy.data.images.new("Wall_"+role, 1024, 1024, alpha=False)
    if role != "Color":
        img.colorspace_settings.name = 'Non-Color'
    node = nodes.new('ShaderNodeTexImage')
    node.image = img
    nodes.active = node
    options = {'pass_filter': {'COLOR'}} if kind == 'DIFFUSE' else {}
    bpy.ops.object.bake(type=kind, use_selected_to_active=True, cage_extrusion=.10, max_ray_distance=.4, margin=8, **options)
    img.filepath_raw = str(textures / ("Runtime"+role+".png"))
    img.file_format = 'PNG'
    img.save()
    img.pack()
    if role == 'Normal':
        normal = nodes.new('ShaderNodeNormalMap')
        normal.inputs['Strength'].default_value = .45
        links.new(node.outputs['Color'], normal.inputs['Color'])
        links.new(normal.outputs['Normal'], shader.inputs['Normal'])
    else:
        links.new(node.outputs['Color'], shader.inputs['Base Color' if role=='Color' else 'Roughness'])

bm = bmesh.new()
bm.from_mesh(low.data)
for vertex in bm.verts:
    vertex.co.x -= middle
bmesh.ops.mirror(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces), matrix=Matrix.Identity(4), axis='X', merge_dist=.00005)
# Meshy added raised teeth. Dress the course flat and texture the cut surfaces
# from the baked stone face so adjacent modules meet at a continuous coping.
z0 = min(v.co.z for v in bm.verts)
z1 = max(v.co.z for v in bm.verts)
top = z0 + .78 * (z1-z0)
cut = bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
    plane_co=(0,0,top), plane_no=(0,0,1), dist=.00001, clear_outer=True)
top_edges = [e for e in cut['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and e.is_valid and e.is_boundary]
bmesh.ops.holes_fill(bm, edges=top_edges, sides=0)
uv = bm.loops.layers.uv.active
sample = max((f for f in bm.faces if abs(f.normal.y)>.8),key=lambda f:f.calc_area())
center = sum((l[uv].uv.copy() for l in sample.loops),sample.loops[0][uv].uv.copy()*0)/len(sample.loops)
end = min(v.co.x for v in bm.verts)
for face in bm.faces:
    is_top = all(abs(v.co.z-top)<.00005 for v in face.verts)
    is_end = all(abs(abs(v.co.x)-abs(end))<.00005 for v in face.verts)
    if is_top or is_end:
        for loop in face.loops:
            x,y,z = loop.vert.co
            loop[uv].uv = (center.x + y*.018, center.y + (x-middle if is_top else z-.5)*.018)
bmesh.ops.triangulate(bm, faces=list(bm.faces))
for axis, size in enumerate((1.25,.77,1.085)):
    lower = min(v.co[axis] for v in bm.verts)
    upper = max(v.co[axis] for v in bm.verts)
    for vertex in bm.verts:
        vertex.co[axis] = ((vertex.co[axis]-lower)/(upper-lower) - (.5 if axis < 2 else 0))*size
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
ends = [{(round(v.co.y,4),round(v.co.z,4)) for v in bm.verts if abs(v.co.x-end)<.00001} for end in (-.625,.625)]
assert ends[0] and ends[0] == ends[1]
assert all(e.is_manifold for e in bm.edges)
print("Wall triangles",len(bm.faces),"matched end vertices",len(ends[0]),flush=True)
bm.to_mesh(low.data)
bm.free()
bpy.data.objects.remove(high,do_unlink=True)
bpy.ops.outliner.orphans_purge(do_recursive=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(source / "ModularStoneWall.blend"))
bpy.ops.export_scene.gltf(filepath=str(runtime / "ModularStoneWall.glb"), use_selection=True, export_format='GLB', export_animations=False)
