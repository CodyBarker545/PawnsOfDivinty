"""Load and save corrected existing clips on untouched textured rigged geometry."""
import bpy
import hashlib
import json
import math
from pathlib import Path

ROOT=Path('D:/SoftwareEng/Game')
OUT=ROOT/'artifacts/arm_pose_refinement'

def review_arm_refinement(name):
    scene=bpy.data.scenes.new('POD_CorrectedArms_'+name);bpy.context.window.scene=scene
    prior=set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=str(OUT/f'staged/{name}/{name}.glb'))
    actions=[a for a in bpy.data.actions if a not in prior]
    assert len(actions)==5
    states={a.name.split('.')[0]:a for a in actions}
    rig=next(o for o in scene.objects if o.type=='ARMATURE')
    mesh=next(o for o in scene.objects if o.type=='MESH')
    for track in rig.animation_data.nla_tracks:track.mute=True
    checks=[]
    for state,action in states.items():
        rig.animation_data.action=action
        for fraction in (0,.25,.5,.75,1):
            frame=action.frame_range[0]+fraction*(action.frame_range[1]-action.frame_range[0])
            scene.frame_set(int(frame),subframe=frame-int(frame))
            assert all(math.isfinite(x) for b in rig.pose.bones for row in b.matrix for x in row)
        checks.append({'clip':state,'seconds':(action.frame_range[1]-action.frame_range[0])/scene.render.fps})
    rig.animation_data.action=states['Idle'];scene.frame_set(5)
    for material in mesh.data.materials:
        for node in material.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image and not node.image.packed_file:node.image.pack()
    destination=ROOT/f'SourceAssets/characters/textured_roster/{name}'
    bpy.data.libraries.write(str(destination/f'{name}.blend'),{scene,*actions},fake_user=True,compress=True)
    report={'character':name,'scene':scene.name,'mesh':mesh.name,'rig':rig.name,'bones':len(rig.data.bones),
            'vertices':len(mesh.data.vertices),'clips':checks,'source':str(destination/f'{name}.blend')}
    (OUT/f'staged/{name}/blender_review.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report))
