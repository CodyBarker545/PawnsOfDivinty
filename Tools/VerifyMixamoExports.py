"""Verify animation exports preserve original GLB geometry, skin, materials and data."""
import hashlib
import json
import struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def read(path):
    raw=path.read_bytes();size=struct.unpack_from('<I',raw,12)[0]
    data=json.loads(raw[20:20+size]);offset=20+size
    length=struct.unpack_from('<I',raw,offset)[0]
    return data,raw[offset+8:offset+8+length]

for original in sorted((ROOT/'Assets/Characters/MobaBeta').glob('*.glb')):
    exported=ROOT/'artifacts/animations'/original.name
    before,source_binary=read(original);after,binary=read(exported)
    for key in ('nodes','meshes','skins','materials','textures','images','samplers','scenes'):
        assert before.get(key)==after.get(key),(original.name,key)
    assert binary[:len(source_binary)]==source_binary,original.name
    assert after['accessors'][:len(before['accessors'])]==before['accessors']
    assert after['bufferViews'][:len(before['bufferViews'])]==before['bufferViews']
    assert {a['name'] for a in after['animations']}=={'Idle','Walk','Attack','HitReaction','Death'}
    joints=set(after['skins'][0]['joints'])
    for clip in after['animations']:
        for channel in clip['channels']:
            target=channel['target']
            assert target['node'] in joints
            assert target['path'] in ('rotation','translation')
            if target['path']=='translation':
                assert after['nodes'][target['node']]['name'] in ('mixamorig:Hips','Hips')
                if clip['name']=='Walk':
                    accessor=after['accessors'][clip['samplers'][channel['sampler']]['output']]
                    view=after['bufferViews'][accessor['bufferView']]
                    values=struct.unpack_from('<'+'f'*(accessor['count']*3),binary,view['byteOffset'])
                    assert len(set(values[0::3]))==1 and len(set(values[2::3]))==1
    print(original.stem+': five clips; original mesh/material/skin/node/binary data unchanged; Walk in place')
for clip in json.loads((ROOT/'Assets/Animations/Mixamo/manifest.json').read_text()):
    assert hashlib.sha256((ROOT/clip['file']).read_bytes()).hexdigest()==clip['sha256']
print('MIXAMO EXPORT INTEGRITY PASS')
