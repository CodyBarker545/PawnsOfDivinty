"""Correct arm bind-direction mismatch in existing sampled clips only.

No mesh, skin, bone/rest or gameplay edits. Originals are backed up before any
write. Repeated runs always derive from that backup, avoiding accumulated edits.
"""
import argparse
import copy
import hashlib
import json
import re
import shutil
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation as R
from AuditCharacterTexturing import ROOT,read_glb,MODELS
from IntegrateCharacterTextures import values,write_glb

OUT=ROOT/'artifacts/arm_pose_refinement'

def main(name):
    OUT.mkdir(parents=True,exist_ok=True)
    backup=OUT/'before'/name;backup.mkdir(parents=True,exist_ok=True)
    library=ROOT/f'Assets/Animations/Mixamo/{name}.tres'
    review=ROOT/f'artifacts/animations/{name}.glb'
    for path in (library,review):
        if not (backup/path.name).exists():shutil.copy2(path,backup/path.name)
    delivery=ROOT/f'SourceAssets/characters/textured_roster/{name}'
    for filename in (f'{name}.blend','Animations.tres'):
        path=delivery/filename
        if path.exists() and not (backup/filename).exists():shutil.copy2(path,backup/filename)
    original_library=(backup/library.name).read_text()
    data,binary,_=read_glb(backup/review.name);binary=bytearray(binary)
    source=json.loads((OUT/'source_rest.json').read_text())
    nodes=data['nodes'];joints=data['skins'][0]['joints'];joint_set=set(joints)
    parents={c:i for i,n in enumerate(nodes) for c in n.get('children',[])}
    rest_rotation={i:R.from_quat(nodes[i].get('rotation',[0,0,0,1])) for i in joints}
    rest_global={};rest_position={}
    def rest(i):
        if i not in rest_global:
            p=parents.get(i);q=nodes[i].get('translation',[0,0,0])
            if p in joint_set:
                rest(p);rest_global[i]=rest_global[p]*rest_rotation[i]
                rest_position[i]=rest_position[p]+rest_global[p].apply(q)
            else:rest_global[i]=rest_rotation[i];rest_position[i]=np.array(q)
        return rest_global[i]
    for i in joints:rest(i)
    names={n['name']:i for i,n in enumerate(nodes) if 'name' in n}
    corrections={};details={}
    for side in ('Left','Right'):
        for bone,child in (('Arm','ForeArm'),('ForeArm','Hand'),('Hand','HandMiddle4')):
            prefix='' if name=='AssassinTextured' else 'mixamorig:'
            node_name=prefix+side+bone;child_name=prefix+side+child
            if node_name not in names or child_name not in names:continue
            i=names[node_name];ci=names[child_name]
            target=rest_position[ci]-rest_position[i];target/=np.linalg.norm(target)
            sa='mixamorig:'+side+bone;sb='mixamorig:'+side+child
            desired=np.array(source[sb]['position'])-source[sa]['position'];desired/=np.linalg.norm(desired)
            cross=np.cross(target,desired);dot=np.clip(np.dot(target,desired),-1,1)
            angle=float(np.arctan2(np.linalg.norm(cross),dot))
            alignment=R.identity() if np.linalg.norm(cross)<1e-10 else R.from_rotvec(cross/np.linalg.norm(cross)*angle)
            corrections[i]=alignment
            details[node_name]={'bind_direction_correction_degrees':float(np.degrees(angle))}
    assert len(corrections)>=4
    changes={};metrics={}
    for clip in data['animations']:
        channels={c['target']['node']:c for c in clip['channels'] if c['target']['path']=='rotation'}
        first=next(iter(channels.values()));sampler=clip['samplers'][first['sampler']]
        times=values(data,binary,sampler['input']).ravel();count=len(times)
        local={i:R.from_quat(values(data,binary,clip['samplers'][c['sampler']]['output'])) for i,c in channels.items()}
        old_global={};new_global={}
        def old(i):
            if i not in old_global:
                p=parents.get(i);q=local.get(i,rest_rotation[i]);old_global[i]=old(p)*q if p in joint_set else q
            return old_global[i]
        def new(i):
            if i not in new_global:
                if i in corrections:
                    weight=np.ones(count)
                    # Keep the original grounded final Death pose. Blend the arm
                    # correction out smoothly as the body settles onto the floor.
                    if clip['name']=='Death':
                        t=np.clip((times-.45)/(max(float(times[-1])*.75,.46)-.45),0,1)
                        weight=1-t*t*(3-2*t)
                    align=R.from_rotvec(weight[:,None]*corrections[i].as_rotvec())
                    new_global[i]=old(i)*rest_global[i].inv()*align*rest_global[i]
                else:
                    p=parents.get(i);q=local.get(i,rest_rotation[i]);new_global[i]=new(p)*q if p in joint_set else q
            return new_global[i]
        changed={}
        for i in corrections:
            if i not in channels:continue
            p=parents.get(i);q=(new(p).inv()*new(i) if p in joint_set else new(i)).as_quat()
            for k in range(1,len(q)):
                if np.dot(q[k-1],q[k])<0:q[k]*=-1
            q=q.astype('<f4');changed[nodes[i]['name'].replace(':','_')]=q
            channel=channels[i];a=data['accessors'][clip['samplers'][channel['sampler']]['output']]
            view=data['bufferViews'][a['bufferView']];offset=view.get('byteOffset',0)+a.get('byteOffset',0)
            assert view.get('byteStride',16)==16 and a['count']==count
            binary[offset:offset+q.nbytes]=q.tobytes()
        changes[clip['name']]=changed
        metrics[clip['name']]={'samples':count,'duration':float(times[-1]),'arm_rotation_tracks_changed':len(changed)}
    def edit_block(match):
        state=match.group(1);body=match.group(0)
        if state not in changes:return body
        for bone,quaternions in changes[state].items():
            found=re.search(r'tracks/(\d+)/path = NodePath\("\.:'+re.escape(bone)+r'"\)',body)
            assert found,(state,bone)
            index=found.group(1)
            pattern=r'(tracks/'+index+r'/keys = PackedFloat32Array\()([^\n]+)(\))'
            def replace(keys):
                numbers=[float(x) for x in keys.group(2).split(',')]
                assert len(numbers)==len(quaternions)*6
                array=np.array(numbers).reshape(-1,6);array[:,2:]=quaternions
                return keys.group(1)+', '.join(format(float(x),'.8g') for x in array.ravel())+keys.group(3)
            body,n=re.subn(pattern,replace,body);assert n==1
        return body
    text=re.sub(r'\[sub_resource type="Animation" id="([^"]+)"\][\s\S]*?(?=\n\[|\Z)',edit_block,original_library)
    assert text!=original_library
    staged=OUT/'staged'/name;staged.mkdir(parents=True,exist_ok=True)
    (staged/library.name).write_text(text)
    write_glb(staged/review.name,data,binary)
    report={'character':name,'corrections':details,'clips':metrics,'scope':'Arm, ForeArm and available Hand rotation tracks only; shoulder/spine/legs/root/clip timing unchanged',
            'original_library_sha256':hashlib.sha256((backup/library.name).read_bytes()).hexdigest(),
            'new_library_sha256':hashlib.sha256((staged/library.name).read_bytes()).hexdigest()}
    (staged/'refinement.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('name',choices=MODELS);args=parser.parse_args();main(args.name)
