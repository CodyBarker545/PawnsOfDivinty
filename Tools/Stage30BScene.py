"""Snapshot and surgically integrate visual-only western stair slice."""
import hashlib,json,pathlib,re,sys,shutil
ROOT=pathlib.Path(__file__).resolve().parents[1]; SRC=ROOT/'SourceAssets/Environment/WesternStairSlice'; BASE=SRC/'Baseline'
BASE.mkdir(parents=True,exist_ok=True); scene=ROOT/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn'
if not (BASE/'CliffsideArena.tscn').exists():
    (BASE/'CliffsideArena.tscn').write_bytes(scene.read_bytes())
    (BASE/'CodeHashes.json').write_text(json.dumps({p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for d in ['Scripts','Tests'] for p in (ROOT/d).rglob('*.cs')},indent=2))
text=scene.read_text(); chunks=re.split(r'(?=\[node )',text)
if '--integrate' not in sys.argv:
    for c in chunks:
        if re.search(r'name="(?:tile_[2-7]_1[3-8]|Cliff_[2-7]_15|Tread[0-4])"',c.split('\n')[0]): print(c[:700])
    sys.exit()
runtime=ROOT/'Assets/Levels/Environment/WesternStairSlice'
for name in ('WeatheredBroadDetail','RestrainedStoneNormal'):
    shutil.copy2(SRC/'Textures'/(name+'.png'),runtime/(name+'.png'))
material_names=['Blended moss earth and grass','Desaturated understory','Weathered charcoal escarpment','Worn slate steps and masonry']
wrapper='[gd_scene format=3]\n\n[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/WesternStairSlice/WesternStairSlice.glb" id="1"]\n'
for i,name in enumerate(material_names):
    filename='SliceMaterial'+str(i)+'.tres'
    (runtime/filename).write_text('[gd_resource type="StandardMaterial3D" load_steps=3 format=3]\n\n[ext_resource type="Texture2D" path="res://Assets/Levels/Environment/WesternStairSlice/WeatheredBroadDetail.png" id="1"]\n[ext_resource type="Texture2D" path="res://Assets/Levels/Environment/WesternStairSlice/RestrainedStoneNormal.png" id="2"]\n\n[resource]\nresource_name = "'+name+'"\nvertex_color_use_as_albedo = true\nvertex_color_is_srgb = false\nalbedo_texture = ExtResource("1")\nroughness = 0.9\nnormal_enabled = true\nnormal_scale = 0.2\nnormal_texture = ExtResource("2")\ncull_mode = 2\n')
    wrapper+='[ext_resource type="Material" path="res://Assets/Levels/Environment/WesternStairSlice/'+filename+'" id="m'+str(i)+'"]\n'
wrapper+='\n[node name="WesternStairEnvironment" type="Node3D"]\nmetadata/stage = "3.0B"\nmetadata/purpose = "Decorative connected western staircase; no gameplay or collision"\n\n[node name="Landscape" parent="." instance=ExtResource("1")]\n'
for i,name in enumerate(material_names):wrapper+='\n[node name="'+name+'" parent="Landscape" index='+str(i)+']\nmaterial_override = ExtResource("m'+str(i)+'")\n'
wrapper+='\n[editable path="Landscape"]\n'
(runtime/'WesternStairSlice.tscn').write_text(wrapper)
# Derive from frozen baseline on rerun, retaining all pre-existing uncommitted edits.
text=(BASE/'CliffsideArena.tscn').read_text(); chunks=re.split(r'(?=\[node )',text); hidden=[]
for i,c in enumerate(chunks):
    header=c.split('\n')[0]; m=re.search(r'name="([^"]+)".*parent="([^"]+)"',header)
    if not m: continue
    name,parent=m.groups(); hide=False
    t=re.fullmatch(r'Terrain/tile_(\d+)_(\d+)',parent)
    if t:
        x,y=map(int,t.groups())
        hide=2<=x<=7 and 13<=y<=18 and (name=='Surface' or name.startswith('Tread'))
    m2=re.fullmatch(r'Cliff_(\d+)_(\d+)',name)
    if parent=='Terrain' and m2:
        x,y=map(int,m2.groups()); hide=2<=x<=7 and y==15
    if hide:
        hidden.append(parent+'/'+name)
        lines=c.splitlines(); lines.insert(1,'visible = false'); chunks[i]='\n'.join(lines)+'\n\n'
text=''.join(chunks)
pos=text.index('[sub_resource')
text=text[:pos]+'[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/WesternStairSlice/WesternStairSlice.tscn" id="WesternStairSlice"]\n\n'+text[pos:]
text+='\n[node name="WesternStairEnvironment" parent="." instance=ExtResource("WesternStairSlice")]\n'
scene.write_text(text)
(SRC/'HiddenLegacyNodes.json').write_text(json.dumps(hidden,indent=2))
for path,digest in json.loads((BASE/'CodeHashes.json').read_text()).items(): assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==digest,path
print('Visual-only integration; retained',len(hidden),'hidden legacy mesh nodes. All C# hashes unchanged.')
