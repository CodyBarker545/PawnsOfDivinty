"""Blender background pipeline: bake FBX motion into per-rig Godot libraries.

Reads source FBXs and runtime GLBs; never rebuilds meshes or saves source art.
Emits animated GLB review copies and animation-only Godot libraries from the same bake.
Run: blender --background --factory-startup --python Tools/BakeMixamoAnimations.py
"""
import hashlib
import json
import struct
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Animations/Mixamo'
OUT.mkdir(parents=True, exist_ok=True)
CLIPS = {'Breathing Idle': 'Idle', 'Walking': 'Walk',
         'Standing Melee Attack Downward': 'Attack',
         'Standing React Small From Headshot': 'HitReaction',
         'Standing Death Backward 01': 'Death'}
C = Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
# The downloaded textured Assassin uses a reduced, unprefixed rig. Spine names run
# in the reverse numerical order; map by inspected hierarchy rather than spelling.
TEXTURED_BONES={name:'mixamorig:'+name for name in (
    'Hips','Head','LeftShoulder','LeftArm','LeftForeArm','LeftHand',
    'RightShoulder','RightArm','RightForeArm','RightHand','LeftUpLeg','LeftLeg',
    'LeftFoot','LeftToeBase','RightUpLeg','RightLeg','RightFoot','RightToeBase')}
TEXTURED_BONES.update({'Spine02':'mixamorig:Spine','Spine01':'mixamorig:Spine1',
                       'Spine':'mixamorig:Spine2','neck':'mixamorig:Neck'})
def rotation(m): return m.to_quaternion().normalized()
def number(x): return format(float(x), '.8g')
def read_accessor(data,binary,index):
    a=data['accessors'][index];v=data['bufferViews'][a['bufferView']]
    dtype={5121:'u1',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]
    width={'SCALAR':1,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
    offset=v.get('byteOffset',0)+a.get('byteOffset',0)
    return np.ndarray((a['count'],width),dtype=dtype,buffer=binary,offset=offset,
                      strides=(v.get('byteStride',np.dtype(dtype).itemsize*width),np.dtype(dtype).itemsize)).copy()

targets = {}
exports = {}
geometry = {}
only = sys.argv[sys.argv.index('--character')+1] if '--character' in sys.argv else None
for path in sorted((ROOT/'Assets/Characters/MobaBeta').glob('*.glb')):
    if only and path.stem != only: continue
    raw = path.read_bytes()
    data = json.loads(raw[20:20+struct.unpack_from('<I',raw,12)[0]])
    binary_offset=20+struct.unpack_from('<I',raw,12)[0]
    binary_length=struct.unpack_from('<I',raw,binary_offset)[0]
    exports[path.stem]=(data,bytearray(raw[binary_offset+8:binary_offset+8+binary_length]))
    binary=exports[path.stem][1]
    attributes=data['meshes'][0]['primitives'][0]['attributes']
    xyz=read_accessor(data,binary,attributes['POSITION'])
    positions4=np.column_stack((xyz,np.ones(len(xyz),dtype=np.float32)))
    indices=read_accessor(data,binary,attributes['JOINTS_0']).astype(np.int32)
    weights=read_accessor(data,binary,attributes['WEIGHTS_0'])
    inverse=read_accessor(data,binary,data['skins'][0]['inverseBindMatrices']).reshape((-1,4,4)).transpose((0,2,1))
    geometry[path.stem]=(positions4,indices,weights,inverse,float(xyz[:,1].min()))
    data['animations']=[]
    nodes = data['nodes']; joints = data['skins'][0]['joints']
    parents = {child:i for i,n in enumerate(nodes) for child in n.get('children',[])}
    local = {}
    world = {}
    for i in joints:
        n = nodes[i]
        x,y,z,w = n.get('rotation',[0,0,0,1])
        local[i] = Matrix.LocRotScale(Vector(n.get('translation',[0,0,0])),Quaternion((w,x,y,z)),Vector(n.get('scale',[1,1,1])))
    def global_rest(i):
        if i not in world:
            p = parents.get(i)
            world[i] = (global_rest(p) if p in joints else Matrix.Identity(4)) @ local[i]
        return world[i]
    for i in joints: global_rest(i)
    targets[path.stem] = (nodes,joints,parents,local,world,[])

manifest = []
for filename,state in CLIPS.items():
    source = ROOT/'SourceAssets/characters/_base/animations'/f'{filename}.fbx'
    # Isolated scenes also allow this script to run through MCP without touching an
    # artist's open scene, actions or unsaved source file.
    bpy.context.window.scene=bpy.data.scenes.new('POD_Bake_'+filename)
    bpy.ops.import_scene.fbx(filepath=str(source))
    rig = next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    action = rig.animation_data.action
    start,end = map(int,action.frame_range)
    fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
    rest = {b.name:C @ rig.matrix_world @ b.matrix_local @ C.inverted() for b in rig.data.bones}
    poses = []
    for frame in range(start,end+1):
        bpy.context.scene.frame_set(frame)
        poses.append({b.name:C @ rig.matrix_world @ b.matrix @ C.inverted() for b in rig.pose.bones})
    manifest.append({'file':str(source.relative_to(ROOT)).replace('\\','/'),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
                     'state':state,'action':action.name,'bones':len(rest),'frames':[start,end],'fps':fps,'seconds':(end-start)/fps})
    for name,(nodes,joints,parents,local,world,clips) in targets.items():
        aliases=TEXTURED_BONES if name=='AssassinTextured' else {}
        mapped = {i:aliases.get(nodes[i]['name'],nodes[i]['name']) for i in joints
                  if aliases.get(nodes[i]['name'],nodes[i]['name']) in rest
                  and not nodes[i]['name'].endswith(('_End','Middle4'))}
        if len(mapped)<22: raise RuntimeError(f'{name}: missing humanoid bones')
        samples = {i:[] for i in mapped}
        hips = next(i for i in mapped if mapped[i]=='mixamorig:Hips')
        height_ratio = world[hips].translation.y/rest['mixamorig:Hips'].translation.y
        # The target meshes were fitted in differing A/T bind poses. Applying
        # the source T-pose delta directly double-lowered some target arms into
        # the torso. Align limb directions before applying that existing motion;
        # keep fitted clavicles, all rests, geometry and weights unchanged.
        arm_alignment={}
        target_names={nodes[i]['name']:i for i in joints}
        prefix='' if name=='AssassinTextured' else 'mixamorig:'
        for side in ('Left','Right'):
            for part,child in (('Arm','ForeArm'),('ForeArm','Hand'),('Hand','HandMiddle4')):
                bone_name=prefix+side+part;child_name=prefix+side+child
                if bone_name not in target_names or child_name not in target_names:continue
                i=target_names[bone_name];ci=target_names[child_name]
                target_direction=(world[ci].translation-world[i].translation).normalized()
                source_direction=(rest['mixamorig:'+side+child].translation-rest['mixamorig:'+side+part].translation).normalized()
                arm_alignment[i]=target_direction.rotation_difference(source_direction)
        positions=[]
        for frame_index,pose in enumerate(poses):
            desired = {i:rotation(pose[b]) @ rotation(rest[b]).inverted() @ rotation(world[i]) for i,b in mapped.items()}
            for i,alignment in arm_alignment.items():
                weight=1.0
                if state=='Death':
                    duration=(end-start)/fps
                    t=max(0,min(1,(frame_index/fps-.45)/(max(duration*.75,.46)-.45)))
                    weight=1-t*t*(3-2*t)
                aligned=Quaternion().slerp(alignment,weight)
                b=mapped[i]
                desired[i]=rotation(pose[b]) @ rotation(rest[b]).inverted() @ aligned @ rotation(world[i])
            for i in mapped:
                parent = parents.get(i)
                parent_q = desired.get(parent,rotation(world[parent]) if parent in world else Quaternion())
                q = parent_q.inverted() @ desired[i]
                if samples[i] and samples[i][-1].dot(q)<0:q.negate()
                samples[i].append(q)
            p=local[hips].translation.copy()
            # Walk stays in place. Stationary actions retain intentional local body shifts;
            # these bone tracks never animate the model/fixture's tactical transform.
            if state!='Walk':
                shift=pose['mixamorig:Hips'].translation-poses[0]['mixamorig:Hips'].translation
                p.x+=shift.x*height_ratio;p.z+=shift.z*height_ratio
            p.y+=(pose['mixamorig:Hips'].translation.y-rest['mixamorig:Hips'].translation.y)*height_ratio
            positions.append(p)
        if state=='Death':
            # Exact linear skinning floor check using unchanged source vertex/weight data.
            # Correct only the hips translation, never geometry or tactical placement.
            xyz,indices,weights,inverse,floor=geometry[name]
            max_lift=0
            for k in range(len(poses)):
                global_pose={}
                def posed(i):
                    if i not in global_pose:
                        q=samples[i][k] if i in samples else rotation(local[i])
                        p=positions[k] if i==hips else local[i].translation
                        m=Matrix.LocRotScale(p,q,local[i].to_scale())
                        parent=parents.get(i)
                        global_pose[i]=(posed(parent) if parent in joints else Matrix.Identity(4)) @ m
                    return global_pose[i]
                skin=np.asarray([np.asarray(posed(i)) for i in joints]) @ inverse
                rows=skin[:,1,:][indices]
                lowest=float(np.einsum('nki,ni,nk->n',rows,xyz,weights,optimize=True).min())
                lift=max(0,floor-lowest)
                # Small clearance covers sub-frame quaternion interpolation between baked keys.
                positions[k].y+=lift+.01
                max_lift=max(max_lift,lift)
            print(f'{name}: Death ground clearance corrected by up to {max_lift:.4f} model units',flush=True)
        lines=[f'[sub_resource type="Animation" id="{state}"]',f'resource_name = "{state}"',f'length = {number((end-start)/fps)}',f'loop_mode = {1 if state in ("Idle","Walk") else 0}']
        track=0
        for i in mapped:
            bone=nodes[i]['name'].replace(':','_')
            keys=', '.join(number(v) for k,q in enumerate(samples[i]) for v in (k/fps,1,q.x,q.y,q.z,q.w))
            lines += [f'tracks/{track}/type = "rotation_3d"',f'tracks/{track}/path = NodePath(".:{bone}")',
                      f'tracks/{track}/keys = PackedFloat32Array({keys})']
            track+=1
        keys=', '.join(number(v) for k,p in enumerate(positions) for v in (k/fps,1,p.x,p.y,p.z))
        hip_name=nodes[hips]['name'].replace(':','_')
        lines += [f'tracks/{track}/type = "position_3d"',f'tracks/{track}/path = NodePath(".:{hip_name}")',
                  f'tracks/{track}/keys = PackedFloat32Array({keys})']
        clips.append('\n'.join(lines))
        # Animated GLB review export retains the original mesh/material/skin data byte for
        # byte. Runtime uses extracted libraries on the existing model, avoiding a second
        # imported copy of the character's geometry and textures.
        gltf,binary=exports[name]
        def accessor(values,kind):
            width={'SCALAR':1,'VEC3':3,'VEC4':4}[kind]
            while len(binary)%4:binary.append(0)
            offset=len(binary)
            binary.extend(struct.pack('<'+'f'*len(values),*values))
            views=gltf.setdefault('bufferViews',[])
            views.append({'buffer':0,'byteOffset':offset,'byteLength':len(values)*4})
            item={'bufferView':len(views)-1,'componentType':5126,'count':len(values)//width,'type':kind}
            if kind=='SCALAR':item.update(min=[min(values)],max=[max(values)])
            accessors=gltf.setdefault('accessors',[]);accessors.append(item)
            return len(accessors)-1
        time_accessor=accessor([k/fps for k in range(len(poses))],'SCALAR')
        animation={'name':state,'samplers':[],'channels':[]}
        def channel(node,path,values,kind):
            output=accessor(values,kind)
            animation['samplers'].append({'input':time_accessor,'output':output,'interpolation':'LINEAR'})
            animation['channels'].append({'sampler':len(animation['samplers'])-1,'target':{'node':node,'path':path}})
        for i in mapped:channel(i,'rotation',[v for q in samples[i] for v in (q.x,q.y,q.z,q.w)],'VEC4')
        channel(hips,'translation',[v for p in positions for v in p],'VEC3')
        gltf['animations'].append(animation)

for name,(*_,clips) in targets.items():
    text='[gd_resource type="AnimationLibrary" load_steps=6 format=3]\n\n'+'\n\n'.join(clips)
    text+='\n\n[resource]\n_data = {\n'+',\n'.join(f'&"{s}": SubResource("{s}")' for s in CLIPS.values())+'\n}\n'
    (OUT/f'{name}.tres').write_text(text,encoding='utf-8')
    gltf,binary=exports[name]
    gltf['buffers'][0]['byteLength']=len(binary)
    header=json.dumps(gltf,separators=(',',':')).encode('utf-8')
    header+=b' '*((-len(header))%4)
    binary+=b'\x00'*((-len(binary))%4)
    exported=struct.pack('<III',0x46546c67,2,12+8+len(header)+8+len(binary))
    exported+=struct.pack('<II',len(header),0x4e4f534a)+header
    exported+=struct.pack('<II',len(binary),0x004e4942)+binary
    review=ROOT/'artifacts/animations';review.mkdir(parents=True,exist_ok=True)
    (review/f'{name}.glb').write_bytes(exported)
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(f'BAKED five clips for {len(targets)} existing rigs; original assets unchanged',flush=True)
