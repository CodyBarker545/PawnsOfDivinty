"""Run in background Blender after CalibrateStoneWallMaps.py."""
from pathlib import Path
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parents[1]
source = root / "SourceAssets/levels/sources/ModularStoneWall/ModularStoneWall.blend"
runtime = root / "Assets/Levels/Props/ModularStoneWall/ModularStoneWall.glb"
bpy.ops.wm.open_mainfile(filepath=str(source))
wall = next(o for o in bpy.context.scene.objects if o.type == "MESH")
material = wall.data.materials[0]
print("Inspected",wall.name,wall.dimensions[:],len(wall.data.polygons),wall.data.uv_layers.active.name,material.name)
material.name = "Weathered charcoal limestone"
for node in material.node_tree.nodes:
    if node.type == "TEX_IMAGE":
        filename = Path(node.image.filepath).name
        assert filename in {"RuntimeColor.png","RuntimeRoughness.png","RuntimeNormal.png"}
        old = node.image
        node.image = bpy.data.images.load(str(root/"SourceAssets/levels/textures/ModularStoneWall"/filename),check_existing=False)
        node.image.colorspace_settings.name = 'sRGB' if filename == 'RuntimeColor.png' else 'Non-Color'
        node.image.pack()
        if not old.users:
            bpy.data.images.remove(old)
    elif node.type == "NORMAL_MAP":
        node.inputs["Strength"].default_value = .32
    elif node.type == "BSDF_PRINCIPLED":
        node.inputs["Metallic"].default_value = 0
        node.inputs["IOR"].default_value = 1.5
        node.inputs["Specular IOR Level"].default_value = .5
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
wall.select_set(True)
bpy.context.view_layer.objects.active = wall
bpy.ops.export_scene.gltf(filepath=str(runtime),use_selection=True,export_format="GLB",export_animations=False)

# Studio review only. Saving the source before this step keeps the asset isolated.
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 16
scene.cycles.use_denoising = True
scene.world = bpy.data.worlds.new("Wall review world")
scene.world.color = (.24,.25,.27)
focus = Vector((0,0,.52))
def aim(obj):
    obj.rotation_euler = (focus-obj.location).to_track_quat('-Z','Y').to_euler()

for pos,power,size in [((-2,-3,4),420,3),((3,-1,2),120,3),((1,2,3),170,2)]:
    bpy.ops.object.light_add(type='AREA',location=pos)
    light=bpy.context.object
    light.data.energy=power
    light.data.shape='DISK'
    light.data.size=size
    aim(light)
bpy.ops.object.camera_add(location=(2.1,-4.5,2.5))
camera=bpy.context.object
aim(camera)
camera.data.type='ORTHO'
camera.data.ortho_scale=2.15
scene.camera=camera
scene.render.resolution_x=900
scene.render.resolution_y=700
scene.render.resolution_percentage=100
scene.render.filepath=str(root/"artifacts/stage22_blender.png")
bpy.ops.render.render(write_still=True)
