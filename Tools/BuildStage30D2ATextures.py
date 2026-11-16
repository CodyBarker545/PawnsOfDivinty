"""Bake a unique landscape map from existing CC0 Poly Haven diffuse sources."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter
import json,hashlib,re,shutil
R=Path(__file__).resolve().parents[1]; S=R/'SourceAssets/Environment/LowerTerrace'; A=R/'Assets/Levels/Environment/LowerTerrace'
for p in [S/'Baseline',S/'Textures',A]:p.mkdir(parents=True,exist_ok=True)
scene=R/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn'
if not (S/'Baseline/CliffsideArena.tscn').exists():shutil.copy2(scene,S/'Baseline/CliffsideArena.tscn')
protected={p.relative_to(R).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for folder in ['Scripts','Assets/Levels/Environment/WesternStairSlice','Assets/Levels/Environment/ConnectedCliff'] for p in (R/folder).rglob('*') if p.is_file() and p.suffix in ['.cs','.tscn','.tres','.glb','.png']}
if not (S/'Baseline/ProtectedHashes.json').exists():(S/'Baseline/ProtectedHashes.json').write_text(json.dumps(protected,indent=2))
w,h=2048,512; yy,xx=np.mgrid[0:h,0:w]; x=-2.75+xx/(w-1)*37.5; z=22.8+yy/(h-1)*5.7
provenance=[]; maps=[]
for i,name in enumerate(['brown_mud','stony_dirt_path','mossy_rock']):
 p=R/f'SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/{name}/textures/{name}_diff_4k.jpg'
 if not p.exists():p=next((R/f'SourceAssets/Environment/ThirdParty/PolyHaven/Terrain/{name}').rglob(name+'_diff_4k.jpg'))
 im=np.asarray(Image.open(p).convert('RGB').resize((1024,1024)).filter(ImageFilter.GaussianBlur(1.3)),dtype=float)/255
 # Smoothly warped coordinates suppress regular texture repetition; the final map is unique.
 u=(x/3.8+.14*np.sin(z*1.3+x*.23)+i*.29)%1; v=(z/3.8+.12*np.sin(x*.8-z*.3)+i*.31)%1
 c=im[(v*1023).astype(int),(u*1023).astype(int)]
 lum=c@np.array([.2126,.7152,.0722]); c=lum[...,None]*.65+c*.35
 target=np.array([[.31,.27,.21],[.38,.35,.29],[.23,.27,.20]][i]); c=(.55+lum[...,None]*.9)*target
 maps.append(c)
 provenance.append({'asset':name,'source':p.relative_to(R).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'publisher':'https://polyhaven.com/a/'+name,'license':'CC0-1.0','license_evidence':'https://polyhaven.com/license','verification':'Publisher identity and named maps matched; API unavailable, no publisher digest comparison claimed.'})
path_z=26.15+.30*np.sin(x*.36)+.13*np.sin(x*.93)
dist=np.abs(z-path_z); route=np.clip((1.2-dist)/.85,0,1); route=route*route*(3-2*route)
# Branch wear connects both unchanged staircase landings to the winding main route.
for sx in [3.5,28.5]:route=np.maximum(route,np.exp(-((x-sx-.16*np.sin(z*2))/.8)**2)*np.clip((26.5-z)/1.4,0,1))
noise=(np.sin(x*.91+z*1.43)+np.sin(x*1.89-z*.82)+np.sin(x*.31+z*2.8))/3
moss=np.clip((24.8-z)/1.8+noise*.28,0,.8)*(1-route*.92)
rock=np.clip(noise*.8-.15,0,.45)*(1-route*.8)
c=maps[0]*(1-route[...,None]*.77)+maps[1]*route[...,None]*.77
c=c*(1-(moss+rock*.35)[...,None])+maps[2]*(moss+rock*.35)[...,None]
c*= (.94+.06*np.sin(x*.45+z*.8)+noise*.045)[...,None]
# Focused Godot visual correction: reduce sunlit earth brightness and microcontrast.
c=(c*.82+c.mean(axis=2)[...,None]*.18)*.78
Image.fromarray(np.uint8(np.clip(c,0,1)*255)).save(A/'LowerTerrainColor.png')
lum=c.mean(axis=2); gy,gx=np.gradient(lum); n=np.dstack((-gx*5,gy*5,np.ones_like(gx)));n/=np.linalg.norm(n,axis=2)[...,None]
Image.fromarray(np.uint8((n*.5+.5)*255)).save(A/'LowerTerrainNormal.png')
for p in A.glob('*.png'):shutil.copy2(p,S/'Textures'/p.name)
(S/'Provenance.json').write_text(json.dumps(provenance,indent=2))
manifest=R/'SourceAssets/Environment/Manifests/AssetLicenses.json'; doc=json.loads(manifest.read_text())
doc['stage30d2a_material_clearance']={'date':'2026-11-16','scope':'Only these diffuse maps and derived terrain maps; other pack holds retained.','evidence':provenance};manifest.write_text(json.dumps(doc,indent=2))
# Hide only original decorative lower floor surfaces, preserving gameplay parents and all stairs.
t=(S/'Baseline/CliffsideArena.tscn').read_text(); chunks=re.split(r'(?=\[node )',t); hidden=[]
for i,c in enumerate(chunks):
 m=re.match(r'\[node name="Surface".*parent="Terrain/tile_(\d+)_(\d+)"',c)
 if m:
  tx,ty=map(int,m.groups())
  if ty>=16 and not(ty==16 and tx in [4,5,24,25]):
   if 'visible = false' not in c:chunks[i]=c.replace('\n','\nvisible = false\n',1);hidden.append(f'Terrain/tile_{tx}_{ty}/Surface')
t=''.join(chunks);pos=t.index('[sub_resource');t=t[:pos]+'[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/LowerTerrace/LowerTerrace.tscn" id="LowerTerrace"]\n\n'+t[pos:];t+='\n[node name="LowerTerraceEnvironment" parent="." instance=ExtResource("LowerTerrace")]\n';scene.write_text(t)
(S/'HiddenLegacyNodes.json').write_text(json.dumps(hidden,indent=2));print('Baked two unique 2048x512 maps; hidden',len(hidden),'additional floor visuals.')
