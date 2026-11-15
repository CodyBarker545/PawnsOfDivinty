"""Read-only runtime baseline. Run before/after texturing; never modifies assets.

Usage: python Tools/AuditCharacterTexturing.py [--compare baseline.json]
Outputs detailed geometry, UV, skin, transform and animation hashes under artifacts.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/character_texturing'
MODELS = ['AssassinTextured', 'GentleGuardian', 'TimeWeaver', 'Nigel', 'Colossus', 'SpiritGuardian']

def digest(data):
    return hashlib.sha256(data).hexdigest()

def stable(data):
    return digest(json.dumps(data, sort_keys=True).encode())

def read_glb(path):
    raw = path.read_bytes()
    magic, version, size = struct.unpack_from('<III', raw)
    assert magic == 0x46546C67 and version == 2 and size == len(raw), path
    chunks = {}
    offset = 12
    while offset < len(raw):
        length, kind = struct.unpack_from('<II', raw, offset)
        chunks[kind] = raw[offset+8:offset+8+length]
        offset += 8 + length
    return json.loads(chunks[0x4E4F534A]), chunks.get(0x004E4942, b''), raw

def inspect(path):
    data, binary, raw = read_glb(path)
    def accessor(index):
        a = data['accessors'][index]
        assert 'sparse' not in a, 'Sparse accessor requires expanded comparison'
        v = data['bufferViews'][a['bufferView']]
        assert v.get('buffer', 0) == 0
        width = {5120:1,5121:1,5122:2,5123:2,5125:4,5126:4}[a['componentType']]
        width *= {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
        start = v.get('byteOffset',0) + a.get('byteOffset',0)
        stride = v.get('byteStride',width)
        payload = b''.join(binary[start+i*stride:start+i*stride+width] for i in range(a['count']))
        return {'count':a['count'], 'type':a['type'], 'componentType':a['componentType'],
                'normalized':a.get('normalized',False), 'sha256':digest(payload)}
    nodes = data.get('nodes',[])
    parents = {child:i for i,n in enumerate(nodes) for child in n.get('children',[])}
    skins = [{'joints':[{'index':j, 'name':nodes[j].get('name'), 'parent':parents.get(j)} for j in s['joints']],
              'inverse_bind_matrices':accessor(s['inverseBindMatrices']) if 'inverseBindMatrices' in s else None}
             for s in data.get('skins',[])]
    primitives = []
    for mi,m in enumerate(data.get('meshes',[])):
        for p in m['primitives']:
            primitives.append({'mesh':mi,'material':p.get('material'), 'mode':p.get('mode',4),
                               'attributes':{k:accessor(v) for k,v in p['attributes'].items()},
                               'indices':accessor(p['indices']) if 'indices' in p else None,
                               'targets':[{k:accessor(v) for k,v in t.items()} for t in p.get('targets',[])]})
    images=[]
    for im in data.get('images',[]):
        v=data['bufferViews'][im['bufferView']] if 'bufferView' in im else None
        payload=binary[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']] if v else b''
        dimensions=None
        if payload.startswith(b'\x89PNG'):
            dimensions=list(struct.unpack_from('>II',payload,16))
        images.append({'name':im.get('name'),'uri':im.get('uri'),'sha256':digest(payload) if v else None,'dimensions':dimensions})
    lib=ROOT/f'Assets/Animations/Mixamo/{path.stem}.tres'
    text=lib.read_text() if lib.exists() else ''
    bindings=sorted(set(re.findall(r'NodePath\("\.:([^\"]+)"\)',text)))
    joint_names={j['name'].replace(':','_') for s in skins for j in s['joints']}
    resource_ids=dict(re.findall(r'\[sub_resource type="Animation" id="([^"]+)"\]\s*resource_name = "([^"]+)"',text))
    clips=re.findall(r'&"([^"]+)": SubResource\("([^"]+)"\)',text)
    assert skins and primitives, path
    assert all('TEXCOORD_0' in p['attributes'] and 'JOINTS_0' in p['attributes'] and 'WEIGHTS_0' in p['attributes'] for p in primitives), path
    assert bindings and set(bindings) <= joint_names, (path, set(bindings)-joint_names)
    assert {c[0] for c in clips} == {'Idle','Walk','Attack','HitReaction','Death'}, (path, clips)
    protected={'primitives':[{k:v for k,v in p.items() if k!='material'} for p in primitives], 'skins':skins,
               'nodes':[{k:v for k,v in n.items() if k not in ('extras','extensions')} for n in nodes], 'scenes':data.get('scenes')}
    return {'path':path.relative_to(ROOT).as_posix(), 'sha256':digest(raw),'bytes':len(raw),
            'mesh_count':len(data['meshes']), 'primitive_vertex_count':sum(p['attributes']['POSITION']['count'] for p in primitives),
            'triangles':sum((p['indices']['count'] if p['indices'] else p['attributes']['POSITION']['count'])//3 for p in primitives),
            'bone_counts':[len(s['joints']) for s in skins], 'skins':skins,'primitives':primitives,
            'protected_sha256':stable(protected),'materials':data.get('materials',[]),'images':images,
            'textures':data.get('textures',[]),'embedded_animations':[a.get('name') for a in data.get('animations',[])],
            'animation_library':str(lib.relative_to(ROOT)), 'animation_library_sha256':digest(lib.read_bytes()),
            'clips':[c[0] for c in clips],'bone_bindings':bindings,'nodes':nodes}

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--compare',type=Path)
    args=parser.parse_args()
    results={name:inspect(ROOT/f'Assets/Characters/MobaBeta/{name}.glb') for name in MODELS}
    scene=(ROOT/'Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn').read_text()
    for name,r in results.items():
        assert r['path'] in scene, name
        if args.compare:
            old=json.loads(args.compare.read_text())['models'][name]
            for key in ('protected_sha256','animation_library_sha256'):
                assert old[key]==r[key], (name,key,'changed')
        print(f"{name}: {r['mesh_count']} meshes; {r['primitive_vertex_count']} exported vertices; {r['triangles']} triangles; bones={r['bone_counts']}; materials={len(r['materials'])}; images={len(r['images'])}; five bound clips")
    OUT.mkdir(parents=True,exist_ok=True)
    output=OUT/('comparison.json' if args.compare else 'baseline.json')
    if not args.compare and output.exists():
        previous=json.loads(output.read_text())['models']
        assert all(previous[n]['sha256']==r['sha256'] and previous[n]['animation_library_sha256']==r['animation_library_sha256'] for n,r in results.items()), 'Baseline already exists with different assets; choose a new audited baseline explicitly.'
    output.write_text(json.dumps({'models':results},indent=2)+'\n')
    print(f'PASS: {output}')

if __name__=='__main__':
    main()
