"""Stage PBR materials on the original GLB; retain original geometry and skin bytes."""
import argparse
import copy
import json
import struct
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
from PIL import Image
from AuditCharacterTexturing import ROOT, read_glb, inspect

STAGE=ROOT/'artifacts/character_texturing'
DEST=ROOT/'SourceAssets/characters/textured_roster'

def write_glb(path,data,binary):
    data['buffers'][0]['byteLength']=len(binary)
    header=json.dumps(data,separators=(',',':')).encode()
    header+=b' '*((-len(header))%4)
    binary=bytes(binary)+b'\0'*((-len(binary))%4)
    payload=struct.pack('<III',0x46546c67,2,28+len(header)+len(binary))
    payload+=struct.pack('<II',len(header),0x4e4f534a)+header
    payload+=struct.pack('<II',len(binary),0x004e4942)+binary
    path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(payload)

def values(data,binary,index):
    a=data['accessors'][index];v=data['bufferViews'][a['bufferView']]
    dtype={5121:'u1',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]
    width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
    item=np.dtype(dtype).itemsize
    return np.ndarray((a['count'],width),dtype=dtype,buffer=binary,
         offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',width*item),item)).copy()

def correspondence(original,returned):
    a,ab,_=read_glb(original);b,bb,_=read_glb(returned)
    def corners(data,binary):
        all_uv=[];all_pos=[]
        for mesh in data['meshes']:
            for p in mesh['primitives']:
                assert p.get('mode',4)==4
                attrs=p['attributes'];xyz=values(data,binary,attrs['POSITION']);uv=values(data,binary,attrs['TEXCOORD_0'])
                indices=values(data,binary,p['indices']).ravel() if 'indices' in p else np.arange(len(xyz))
                all_uv.append(uv[indices]);all_pos.append(xyz[indices])
        return np.concatenate(all_pos),np.concatenate(all_uv)
    ap,au=corners(a,ab);bp,bu=corners(b,bb)
    # Meshy normalizes the unrigged output. Undo that normalization only in
    # correspondence calculations, never on the preserved target mesh.
    bp=(bp-bp.min(0))/np.ptp(bp,axis=0)*np.ptp(ap,axis=0)+ap.min(0)
    distance,nearest=cKDTree(ap).query(bp)
    reverse=cKDTree(bp).query(ap)[0]
    error=float(max(distance.max(),reverse.max()))
    tolerance=max(2e-6,2e-6*float(np.ptp(ap,axis=0).max()))
    assert error<tolerance, f'Spatial vertex correspondence failed: {error} (scale-relative tolerance {tolerance})'
    _,canonical=np.unique(ap,axis=0,return_inverse=True)
    ac=canonical.reshape(-1,3);bc=canonical[nearest].reshape(-1,3)
    lookup={}
    for i,tri in enumerate(bc):lookup.setdefault(tuple(sorted(tri)),[]).append(i)
    patches=[];max_uv=0
    for i,tri in enumerate(ac):
        candidates=lookup.get(tuple(sorted(tri)))
        assert candidates, f'Original surface triangle {i} missing from generated mesh; validated bake unavailable'
        best=None
        for j in candidates:
            order=[list(bc[j]).index(c) for c in tri]
            source=bu.reshape(-1,3,2)[j][order]
            delta=float(np.max(np.abs(source-au.reshape(-1,3,2)[i])))
            if best is None or delta<best[0]:best=(delta,source)
            if delta<1e-6:break
        max_uv=max(max_uv,best[0])
        if best[0]>1e-6:
            patches.append({'source_uv':best[1].tolist(),'target_uv':au.reshape(-1,3,2)[i].tolist(),'xyz':ap.reshape(-1,3,3)[i].tolist()})
    return {'original_triangles':len(ac),'returned_triangles':len(bc),'position_max_error':error,'position_tolerance':tolerance,
            'uv_alias_triangles_to_bake':len(patches),'method':'Every original surface triangle matched in position; preserve atlas and bake removed duplicate UV aliases'},patches

def repair_maps(name,patches):
    folder=STAGE/f'jobs/{name}';out=DEST/name/'textures';out.mkdir(parents=True,exist_ok=True)
    repaired=0
    def basis(xyz,uv):
        edges=xyz[1:]-xyz[0];delta=uv[1:]-uv[0]
        if abs(np.linalg.det(delta))<1e-12:return np.eye(3)
        tb=np.linalg.solve(delta,edges);normal=np.cross(edges[0],edges[1]);normal/=max(np.linalg.norm(normal),1e-12)
        tangent=tb[0]-normal*np.dot(normal,tb[0]);tangent/=max(np.linalg.norm(tangent),1e-12)
        bitangent=np.cross(normal,tangent)
        if np.dot(bitangent,tb[1])<0:bitangent=-bitangent
        return np.column_stack((tangent,bitangent,normal))
    for kind in ('base_color','normal','roughness','metallic'):
        source=np.array(Image.open(folder/f'0_{kind}.png').convert('RGB'));target=source.copy();h,w=source.shape[:2]
        for patch in patches:
            uv=np.array(patch['target_uv']);suv=np.array(patch['source_uv']);xyz=np.array(patch['xyz'])
            dst=uv*np.array([w-1,-(h-1)])+np.array([0,h-1])
            matrix=np.column_stack((dst[1]-dst[0],dst[2]-dst[0]))
            if abs(np.linalg.det(matrix))<1e-10:continue
            lo=np.maximum(np.floor(dst.min(0)-1).astype(int),0);hi=np.minimum(np.ceil(dst.max(0)+1).astype(int),[w-1,h-1])
            xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1));points=np.column_stack((xx.ravel(),yy.ravel()))
            bary=(points-dst[0])@np.linalg.inv(matrix).T;bary=np.column_stack((1-bary.sum(1),bary))
            inside=np.all(bary>=-.025,axis=1)
            points=points[inside];bary=bary[inside]
            if not len(points):continue
            coords=bary@suv;coords=coords*np.array([w-1,-(h-1)])+np.array([0,h-1])
            coords=np.clip(coords,[0,0],[w-1.001,h-1.001]);low=np.floor(coords).astype(int);fraction=coords-low
            fx=fraction[:,0,None];fy=fraction[:,1,None];x=low[:,0];y=low[:,1]
            sampled=(source[y,x]*(1-fx)*(1-fy)+source[y,x+1]*fx*(1-fy)+source[y+1,x]*(1-fx)*fy+source[y+1,x+1]*fx*fy)
            if kind=='normal':
                transform=basis(xyz,uv).T@basis(xyz,suv)
                normals=(sampled/127.5-1)@transform.T
                normals/=np.maximum(np.linalg.norm(normals,axis=1,keepdims=True),1e-12)
                sampled=(normals+1)*127.5
            target[points[:,1],points[:,0]]=np.clip(sampled,0,255).astype('uint8');repaired+=len(points)
        Image.fromarray(target).save(out/f'{kind}.png')
    rough=Image.open(out/'roughness.png').convert('L');metal=Image.open(out/'metallic.png').convert('L')
    Image.merge('RGB',(Image.new('L',rough.size,255),rough,metal)).save(out/'metallic_roughness.png')
    return repaired

def graft(original,returned,output):
    data,binary,_=read_glb(original);generated,gb,_=read_glb(returned)
    assert len(data['materials'])==len(generated['materials'])==1
    binary=bytearray(binary)
    images=[]
    for kind in ('base_color','metallic_roughness','normal'):
        blob=(output.parent/'textures'/f'{kind}.png').read_bytes()
        while len(binary)%4:binary.append(0)
        new={'name':output.stem+'_'+kind,'mimeType':'image/png','bufferView':len(data['bufferViews'])}
        data['bufferViews'].append({'buffer':0,'byteOffset':len(binary),'byteLength':len(blob)})
        binary.extend(blob);images.append(new)
    data['images']=images;data['textures']=[{'source':i,'sampler':0} for i in range(3)]
    data['samplers']=[{'magFilter':9729,'minFilter':9987,'wrapS':10497,'wrapT':10497}]
    data['materials']=[{'name':output.stem+'_ConceptPBR','doubleSided':True,
        'pbrMetallicRoughness':{'baseColorTexture':{'index':0},'metallicRoughnessTexture':{'index':1},'metallicFactor':1,'roughnessFactor':1},
        'normalTexture':{'index':2}}]
    material=data['materials'][0]
    assert 'baseColorTexture' in material['pbrMetallicRoughness']
    assert 'metallicRoughnessTexture' in material['pbrMetallicRoughness']
    assert 'normalTexture' in material
    for key in ('extensionsUsed','extensionsRequired'):
        if generated.get(key):data[key]=list(dict.fromkeys(data.get(key,[])+generated[key]))
    write_glb(output,data,binary)

def animated_review(name,staged):
    original=STAGE/f'backups/audit_baseline_001/Assets/Characters/MobaBeta/{name}.glb'
    old,oldbin,_=read_glb(original)
    saved_review=STAGE/f'backups/audit_baseline_001/artifacts/animations/{name}.glb'
    review,reviewbin,_=read_glb(saved_review if saved_review.exists() else ROOT/f'artifacts/animations/{name}.glb')
    data,binary,_=read_glb(staged);binary=bytearray(binary)
    old_views=len(old['bufferViews']);old_accessors=len(old['accessors'])
    new_views=len(data['bufferViews']);offset=len(binary)-len(oldbin)
    binary.extend(reviewbin[len(oldbin):])
    for view in review['bufferViews'][old_views:]:
        v=copy.deepcopy(view);v['byteOffset']=v.get('byteOffset',0)+offset
        data['bufferViews'].append(v)
    for accessor in review['accessors'][old_accessors:]:
        a=copy.deepcopy(accessor);a['bufferView']+=new_views-old_views
        data['accessors'].append(a)
    data['animations']=review['animations']
    out=STAGE/f'validated_reviews/{name}.glb';write_glb(out,data,binary)
    return out

def stage(name):
    original=STAGE/f'backups/audit_baseline_001/Assets/Characters/MobaBeta/{name}.glb'
    returned=STAGE/f'jobs/{name}/returned.glb'
    match,patches=correspondence(original,returned)
    match['baked_texture_samples']=repair_maps(name,patches)
    out=DEST/name/f'{name}.glb'
    graft(original,returned,out)
    before=inspect(original);after=inspect(out)
    assert before['protected_sha256']==after['protected_sha256']
    assert before['animation_library_sha256']==after['animation_library_sha256']
    review=animated_review(name,out)
    record={'character':name,'correspondence':match,'protected_before':before['protected_sha256'],
            'protected_after':after['protected_sha256'],'staged':str(out),'animated_review':str(review),'images':after['images']}
    (out.parent/'integration.json').write_text(json.dumps(record,indent=2))
    print(json.dumps(record,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('name');args=parser.parse_args();stage(args.name)
