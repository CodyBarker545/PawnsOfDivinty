"""Check only intended arm rotations changed; meshes/materials and timing stay exact."""
import argparse
import json
import re
from pathlib import Path
import numpy as np
from AuditCharacterTexturing import ROOT,read_glb,inspect,MODELS
from IntegrateCharacterTextures import values

OUT=ROOT/'artifacts/arm_pose_refinement'

def verify(name,deployed=False):
    before=OUT/'before'/name;after=OUT/'staged'/name
    source=ROOT/f'Assets/Animations/Mixamo/{name}.tres' if deployed else after/f'{name}.tres'
    old_text=(before/f'{name}.tres').read_text();new_text=source.read_text()
    allowed=set(json.loads((after/'refinement.json').read_text())['corrections'])
    allowed={n.replace(':','_') for n in allowed}
    # Non-arm tracks and all clip metadata must be byte-identical after masking
    # the explicitly allowed quaternion payloads in both libraries.
    def mask(text):
        blocks=re.split(r'(?=\[sub_resource type="Animation")',text)
        for k,body in enumerate(blocks):
            for index,bone in re.findall(r'tracks/(\d+)/path = NodePath\("\.:([^"]+)"\)',body):
                if bone in allowed:
                    body=re.sub(r'(tracks/'+index+r'/keys = PackedFloat32Array\()[^\n]+(\))',r'\1<approved arm rotation edit>\2',body)
            blocks[k]=body
        return ''.join(blocks)
    assert mask(old_text)==mask(new_text), name+' changed non-arm tracks or metadata'
    old,ob,_=read_glb(before/f'{name}.glb')
    new_path=ROOT/f'artifacts/animations/{name}.glb' if deployed else after/f'{name}.glb'
    new,nb,_=read_glb(new_path)
    assert old==new, name+' changed GLB structure'
    arms=set(json.loads((after/'refinement.json').read_text())['corrections'])
    for a,b in zip(old['animations'],new['animations']):
        assert a['name']==b['name']
        for channel in a['channels']:
            sampler=a['samplers'][channel['sampler']]
            i=sampler['output'];v=values(old,ob,i);w=values(new,nb,i)
            node=old['nodes'][channel['target']['node']]['name']
            assert np.array_equal(values(old,ob,sampler['input']),values(new,nb,sampler['input']))
            if channel['target']['path']=='rotation' and node in arms:
                assert np.max(abs(np.linalg.norm(w,axis=1)-1))<1e-6
                assert np.min(np.sum(w[:-1]*w[1:],axis=1))>=0, (name,a['name'],'quaternion discontinuity')
                if a['name']=='Death':assert abs(np.dot(v[-1],w[-1]))>1-1e-6
            else:assert np.array_equal(v,w),(name,a['name'],node)
    current=inspect(ROOT/f'Assets/Characters/MobaBeta/{name}.glb')
    delivered=json.loads((ROOT/'SourceAssets/characters/textured_roster/manifest.json').read_text())
    original=next(x for x in delivered if Path(x['runtime']).stem==name)
    assert current['sha256']==original['runtime_sha256'], name+' runtime mesh/material asset changed'
    print(name+': only approved arm rotations changed; clip timing, non-arm tracks, final Death pose, rig and textured model preserved')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--deployed',action='store_true');args=parser.parse_args()
    for name in MODELS:verify(name,args.deployed)
