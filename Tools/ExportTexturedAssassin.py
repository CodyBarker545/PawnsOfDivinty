"""Export the downloaded textured Assassin without modifying its FBX or texture files."""
from pathlib import Path
import hashlib
import json
import bpy

ROOT=Path(__file__).resolve().parents[1]
source=next((ROOT/'SourceAssets/characters/hooded_assassin/working_textured_model_assassin').rglob('*.fbx'))
digest=hashlib.sha256(source.read_bytes()).hexdigest()
scene=bpy.data.scenes.new('POD_Export_TexturedAssassin')
bpy.context.window.scene=scene
bpy.ops.import_scene.fbx(filepath=str(source))
rig=next(o for o in scene.objects if o.type=='ARMATURE')
meshes=[o for o in scene.objects if o.type=='MESH']
assert len(meshes)==1 and len(rig.data.bones)==24
rig.animation_data_clear()
for bone in rig.pose.bones:bone.matrix_basis.identity()
# FBX is in centimetres with a rotated armature object. Normalize that object transform
# in this working copy so the GLB skeleton uses metre-sized rests like the battlefield.
for o in scene.objects:o.select_set(o==rig or o in meshes)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
material=meshes[0].data.materials[0]
for node in material.node_tree.nodes:
    if node.type!='TEX_IMAGE' or not node.image:continue
    label=node.image.name.lower()
    suffix=next((s for s in ('normal','roughness','metallic') if s in label),None)
    texture=source.with_name(source.stem+('_'+suffix if suffix else '')+'.png')
    node.image=bpy.data.images.load(str(texture),check_existing=True)
    if suffix:node.image.colorspace_settings.name='Non-Color'
images=[n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
assert len(images)==4 and all(i.size[0]>0 and len(i.pixels)>0 for i in images)
output=ROOT/'Assets/Characters/MobaBeta/AssassinTextured.glb'
bpy.ops.export_scene.gltf(filepath=str(output),export_format='GLB',use_selection=True,
                          use_active_scene=True,export_animations=False,export_cameras=False,export_lights=False)
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
report={'Source':str(source.relative_to(ROOT)).replace('\\','/'),'SourceSHA256':digest,
        'Runtime':str(output.relative_to(ROOT)).replace('\\','/'),
        'RuntimeSHA256':hashlib.sha256(output.read_bytes()).hexdigest(),
        'Bones':len(rig.data.bones),'Vertices':len(meshes[0].data.vertices),
        'Textures':[i.name for i in images]}
(ROOT/'Docs/TEXTURED_ASSASSIN_SOURCE.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
