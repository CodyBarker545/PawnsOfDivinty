"""Load staged original-mesh PBR asset and existing clips through Blender MCP."""
import bpy
import hashlib
import json
import math
import struct
from pathlib import Path

ROOT=Path('D:/SoftwareEng/Game')
STAGE=ROOT/'artifacts/character_texturing'

def validate_textured_character(name):
    scene=bpy.data.scenes.new('POD_Textured_'+name)
    bpy.context.window.scene=scene
    prior=set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=str(STAGE/f'validated_reviews/{name}.glb'))
    actions=[a for a in bpy.data.actions if a not in prior]
    assert len(actions)==5
    by_name={next(n for n in ('Idle','Walk','Attack','HitReaction','Death') if a.name==n or a.name.startswith(n+'.')):a for a in actions}
    assert len(by_name)==5
    rig=next(o for o in scene.objects if o.type=='ARMATURE')
    mesh=next(o for o in scene.objects if o.type=='MESH')
    def fingerprint():
        h=hashlib.sha256()
        for v in mesh.data.vertices:
            h.update(struct.pack('<3f',*v.co))
            for g in v.groups:h.update(struct.pack('<If',g.group,g.weight))
        for p in mesh.data.polygons:h.update(struct.pack('<'+'I'*len(p.vertices),*p.vertices))
        for uv in mesh.data.uv_layers:
            for x in uv.data:h.update(struct.pack('<2f',*x.uv))
        for b in rig.data.bones:
            h.update((b.name+'|'+(b.parent.name if b.parent else '')).encode())
            h.update(struct.pack('<16f',*(x for row in b.matrix_local for x in row)))
        return h.hexdigest()
    before=fingerprint();samples=[]
    for track in rig.animation_data.nla_tracks:track.mute=True
    for state,action in by_name.items():
        rig.animation_data.action=action;poses=[]
        for frame in (action.frame_range[0],sum(action.frame_range)/2,action.frame_range[1]):
            scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
            matrix=[x for b in rig.pose.bones for row in b.matrix for x in row]
            assert all(math.isfinite(x) for x in matrix)
            poses.append(hashlib.sha256(struct.pack('<'+'f'*len(matrix),*matrix)).hexdigest())
        assert len(set(poses))>1
        samples.append({'clip':state,'seconds':(action.frame_range[1]-action.frame_range[0])/scene.render.fps,'distinct_poses':len(set(poses))})
    rig.animation_data.action=by_name['Idle'];scene.frame_set(0)
    assert before==fingerprint()
    materials=[]
    for material in mesh.data.materials:
        assert material.use_nodes
        assert any(n.type=='BSDF_PRINCIPLED' for n in material.node_tree.nodes)
        images=[]
        for n in material.node_tree.nodes:
            if n.type=='TEX_IMAGE' and n.image:
                assert n.image.size[0]==2048
                if not n.image.packed_file:n.image.pack()
                images.append({'name':n.image.name,'size':list(n.image.size),'colorspace':n.image.colorspace_settings.name})
        assert len(images)==3
        materials.append({'name':material.name,'images':images})
    dest=ROOT/f'SourceAssets/characters/textured_roster/{name}'
    bpy.data.libraries.write(str(dest/f'{name}.blend'),{scene,*actions},fake_user=True,compress=True)
    report={'character':name,'scene':scene.name,'mesh':mesh.name,'rig':rig.name,'bones':len(rig.data.bones),
            'vertices':len(mesh.data.vertices),'UVs':[u.name for u in mesh.data.uv_layers],
            'fingerprint_before':before,'fingerprint_after':fingerprint(),'clips':samples,'materials':materials}
    (dest/'blender_validation.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report));return report
