"""Stage 3.0C snapshot and reversible visual-only scene integration."""
import pathlib,json,hashlib,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]; SRC=ROOT/'SourceAssets/Environment/ConnectedCliff'; BASE=SRC/'Baseline'; RUN=ROOT/'Assets/Levels/Environment/ConnectedCliff'
BASE.mkdir(parents=True,exist_ok=True); RUN.mkdir(parents=True,exist_ok=True)
scene=ROOT/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn'
if not (BASE/'CliffsideArena.tscn').exists():
    (BASE/'CliffsideArena.tscn').write_bytes(scene.read_bytes())
    paths=[p for d in ['Scripts','Tests','Assets/Levels/Environment/WesternStairSlice','SourceAssets/Environment/WesternStairSlice'] for p in (ROOT/d).rglob('*') if p.is_file() and (p.suffix in ('.cs','.blend','.glb','.tscn','.tres','.png') and not any(x in p.parts for x in ('bin','obj','QA','Baseline')))]
    (BASE/'ProtectedHashes.json').write_text(json.dumps({p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},indent=2))
if '--integrate' not in sys.argv:
    print('Stage 3.0B scene and protected files snapshotted.');sys.exit()
doc=json.loads((SRC/'Composition.json').read_text())
text=(BASE/'CliffsideArena.tscn').read_text();chunks=re.split(r'(?=\[node )',text);hidden=[]
restyled=[]
for i,c in enumerate(chunks):
    match=re.search(r'name="([^"]+)".*parent="([^"]+)"',c.split('\n')[0])
    if not match:continue
    name,parent=match.groups();hide=False
    tile=re.fullmatch(r'Terrain/tile_(\d+)_(\d+)',parent)
    if tile:
        x,y=map(int,tile.groups())
        hide=name=='Surface' and (x<2 or x>=8) and y in (14,15,16) and not(x in (24,25) and y in (15,16))
        hide=hide or (name=='ModularCliff' and x==14 and y==15)
        if x in (24,25) and y in (15,16) and name.startswith('Tread'):
            c=re.sub(r'^material_override = .*$', 'material_override = ExtResource("EasternStepSlate")',c,flags=re.M)
            chunks[i]=c;restyled.append(parent+'/'+name)
    crag=re.fullmatch(r'Cliff_(\d+)_15',name)
    if crag and parent=='Terrain':hide=int(crag[1])<2 or int(crag[1])>=8
    if hide:
        if not re.search(r'^visible = false$',c,re.M):
            chunks[i]=c.replace('\n','\nvisible = false\n',1);hidden.append(parent+'/'+name)
text=''.join(chunks);pos=text.index('[sub_resource')
text=text[:pos]+'[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/ConnectedCliff/ConnectedCliff.tscn" id="ConnectedCliff"]\n[ext_resource type="Material" path="res://Assets/Levels/Environment/ConnectedCliff/EasternStepSlate.tres" id="EasternStepSlate"]\n\n'+text[pos:]
text+='\n[node name="ConnectedCliffEnvironment" parent="." instance=ExtResource("ConnectedCliff")]\n'
scene.write_text(text)
(SRC/'HiddenLegacyNodes.json').write_text(json.dumps(hidden,indent=2))
(SRC/'RestyledEasternTreads.json').write_text(json.dumps(restyled,indent=2))
step_material=(ROOT/'Assets/Levels/Environment/WesternStairSlice/SliceMaterial3.tres').read_text().replace('vertex_color_use_as_albedo = true','vertex_color_use_as_albedo = false').replace('resource_name = "Worn slate steps and masonry"','resource_name = "Eastern original treads - matte slate"\nalbedo_color = Color(0.22, 0.235, 0.225, 1)')
(RUN/'EasternStepSlate.tres').write_text(step_material)
# Resource instances all refer to the same five GLB mesh resources; no per-placement mesh copies.
wrapper='[gd_scene format=3]\n\n'
for a in doc['library']:
    wrapper+=f'[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/ConnectedCliff/{a["name"]}.tscn" id="{a["name"]}"]\n'
wrapper+='[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/ConnectedCliff/CliffTransitions.tscn" id="ground"]\n\n[node name="ConnectedCliffEnvironment" type="Node3D"]\nmetadata/stage = "3.0C"\n\n[node name="TerrainTransitions" parent="." instance=ExtResource("ground")]\n'
for p in doc['placements']:
    wrapper+=f'\n[node name="{p["id"]}" parent="." instance=ExtResource("{p["asset"]}" )]\nposition = Vector3({p["x"]}, {p["h"]}, {p["z"]})\nrotation = Vector3(0, {p["angle"]}, 0)\nscale = Vector3({p["sx"]}, {p["sy"]}, {p["sz"]})\n'
(RUN/'ConnectedCliff.tscn').write_text(wrapper)
for path,digest in json.loads((BASE/'ProtectedHashes.json').read_text()).items():assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==digest,path
print('Integrated',len(doc['placements']),'linked formation instances; hidden',len(hidden),'retained decorative nodes. Protected files unchanged.')
