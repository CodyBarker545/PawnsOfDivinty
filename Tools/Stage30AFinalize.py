"""Assemble updateable Stage 3.0A manifests, labeled sheets and review report."""
import collections, hashlib, html, json, pathlib, re, textwrap
from PIL import Image, ImageDraw, ImageFont
ROOT=pathlib.Path(__file__).resolve().parents[1]; BASE=ROOT/'SourceAssets/Environment'; MAN=BASE/'Manifests'; QA=BASE/'QA/ContactSheets'
def read(n): return json.loads((MAN/n).read_text())
def write(n,d): (MAN/n).write_text(json.dumps(d,indent=2),encoding='utf-8')
audit=read('ExtractionAudit.json'); inspection=read('BlenderInspection.json'); assets=inspection['assets']; byid={a['id']:a for a in assets}
for a in assets:
    if a['name']=='mountainside_4k': a['category']='Rocks and cliff formations'; a['resource_type']='model'
    if a['name'].startswith('Pebble_'): a['category']='Rocks and cliff formations'
    if a['name'].startswith('Petal_'): a['category']='Trees and forest vegetation'
    a['original_relative_path']=a['source'].split(a['pack']+'/')[1]
    a['license_status']='CC0-bundled' if any(k in a['pack'] for k in ('KayKit','Kenney','StylizedNature')) else 'review-required'
    a['intended_role']={'Trees and forest vegetation':'Upper forest and sparse ruin/terrace growth; decorative, traversable trees/bushes','Rocks and cliff formations':'Authored blockers or decorative cliff/perimeter dressing','Castle and fort architecture':'Fort shells, home-castle architecture and stair details; exact authored clearances','Ruins and broken structures':'Central battlefield ruins and lower terrace rings','Props and environmental decoration':'Selective camp supplies, banners and background detail','Ground materials':'Connected terrain surface material'}[a['category']]
    a['runtime_texture_recommendation']='Start 512px-1K for props/foliage; 1K-2K shared terrain; preserve originals. No 4K/EXR runtime copy without demonstrated camera benefit.'
    a['uv_review']='Missing UV layers on some meshes: add UVs for texture/trim-sheet conversion; constant source materials can work without UVs.' if any(not o['uv_layers'] for o in a['mesh_objects']) else 'UV layers present; check distortion and texel density during export ticket.'
    a['geometry_quality_review']='Visual silhouette and source mesh statistics inspected; manifold/collision readiness not certified. Decorative sources must not automatically become gameplay collision.'
    a['source_objects_exact']=a.get('source_object_names',[])
    p=ROOT/a['source']
    if p.suffix=='.gltf': a['source_objects_exact']=[n.get('name',f'node:{i}') for i,n in enumerate(json.loads(p.read_text()).get('nodes',[]))]
    if p.suffix=='.glb':
        import struct
        with p.open('rb') as f:
            f.seek(12); size,typ=struct.unpack('<II',f.read(8)); data=json.loads(f.read(size))
        a['source_objects_exact']=[n.get('name',f'node:{i}') for i,n in enumerate(data.get('nodes',[]))]
    if a['resource_type']=='material':
        a['geometry_note']='Sphere and plane are material-preview geometry; excluded from unique model count.'
    if a['name'] in ('boulder_01_4k','mountainside_4k'):
        a['geometry_note']='Four source LOD objects, not four simultaneous components. Select ONE LOD per export; aggregate triangles/bounds are diagnostic only.'
        a['optimization']='Use LOD3 as initial tactical candidate; compare simpler stylized rocks. Rebuild large planes/bake down, grade saturation, avoid simultaneous LOD export.'
    a['selected_for_production']=False
    a['avoid_reason']=('Interior microprops or siege equipment have no present battlefield need' if any(x in a['name'].lower() for x in ('siege-','bottle','plate','coin','keyring','bed_','table_','chair','shelf','bookcase','beartrap','trapdoor','spikes')) else None)
    if a['avoid_reason']: a['production_priority']='avoid for this stage'
    else: a['production_priority']='candidate; see requirements and shortlist'

# Group exact local-geometry signatures, preserving material/name variants without inflating mesh families.
groups=collections.defaultdict(list)
for a in assets:
    if a['resource_type']=='model': groups[(a['pack'],a['geometry_signature'])].append(a['id'])
for ids in groups.values():
    for aid in ids: byid[aid]['geometry_family']=ids[0]; byid[aid]['same_geometry_variants']=[x for x in ids if x!=aid]

licenses=[]
for pack in sorted(set(x['destination'] for x in audit['archives'])):
    docs=[x['path'] for x in audit['documents'] if x['path'].startswith(pack+'/') and 'license' in x['path'].lower()]
    if 'StylizedNature' in pack: url='https://quaternius.com/packs/stylizednaturemegakit.html'; author='Quaternius'
    elif 'UltimateModularRuins' in pack: url='https://quaternius.com/packs/ultimatemodularruins.html'; author='Quaternius (content match; original download provenance unverified)'
    elif 'KayKit' in pack: url='https://kaylousberg.itch.io/kaykit-dungeon-pack'; author='Kay Lousberg'
    elif 'Kenney' in pack: url='https://kenney.nl/assets/castle-kit'; author='Kenney'
    else: url='https://polyhaven.com/a/'+pack.split('/')[-1]; author='Poly Haven; per-asset creator credit not recovered from archive'
    licenses.append({'pack':pack,'source_url':url,'source_url_status':'Publisher reference; not a recovered download receipt','license':'CC0-1.0' if docs else 'CC0 publisher evidence; local documentation missing','status':'cleared-bundled' if docs else 'review-required','bundled_documents':docs,'promotion_allowed':bool(docs),'attribution_required':False if docs else None,'suggested_credit':author,'evidence_urls':[url]+(['https://polyhaven.com/license'] if 'PolyHaven' in pack else []),'review_note':'Bundled CC0 document preserved; optional creator credit recommended.' if docs else 'Do not promote. Obtain pack-specific provenance/license record; online CC0 statement supports investigation but does not replace missing source evidence.','archive_names':[x['archive'] for x in audit['archives'] if x['destination']==pack]})
write('AssetLicenses.json',{'schema_version':1,'review_date':'2026-11-15','packs':licenses,'note':'No original receipts or browser download history supplied. Local archive hashes identify exactly what was reviewed. All source documents remain unchanged.'})

# Each line: required asset type | production classification | exact source-name search tokens.
spec={
'A. Natural terrain and cliffs': '''Large cliff faces|MODIFY|mountainside_4k
Irregular cliff sections|MODIFY|mountainside_4k
Convex cliff corners|CREATE IN BLENDER|
Concave cliff corners|CREATE IN BLENDER|
Natural cliff-top transitions|CREATE IN BLENDER|
Cliff-to-ground transitions|CREATE IN BLENDER|
Rocky overhang details|COMBINE|Rock_Medium,boulder_01_4k
Large boulders|MODIFY|boulder_01_4k,rocks-large
Medium boulders|MODIFY|Rock_Medium
Small rock clusters|COMBINE|Pebble_,rocks-small
Mossy rocks|COMBINE|mossy_rock_4k,Rock_Medium
Scattered stones|AVAILABLE|Pebble_
Cliff-adjacent vegetation|COMBINE|Bush_Common,Grass_Common_Short
Stair-to-cliff transitions|CREATE IN BLENDER|''',
'B. Ground and paths': '''Forest grass|COMBINE|forrest_ground_01_4k,Grass_Common_Short
Dark soil|MODIFY|brown_mud_4k,floor_dirt_large
Mossy dirt|COMBINE|forrest_ground_01_4k,mossy_rock_4k
Mud|MODIFY|brown_mud_4k
Cobblestone|MODIFY|cobblestone_floor_001_4k
Broken flagstones|MODIFY|Floor_Hole,RockPath_
Castle courtyard paving|COMBINE|floor_tile_large,Floor_Squares
Worn paths|MODIFY|stony_dirt_path_4k
Grass-to-dirt transitions|CREATE IN BLENDER|
Stone-to-earth transitions|CREATE IN BLENDER|
Cliff-edge ground|CREATE IN BLENDER|
Lower terrace ground|COMBINE|stony_dirt_path_4k,brown_mud_4k
Rubble and debris overlays|COMBINE|Bricks,Pebble_,wall_broken''',
'C. Upper forest': '''Multiple conifer variants|MODIFY|Pine_
Multiple deciduous tree variants|MODIFY|CommonTree_
Small trees and saplings|MODIFY|tree-small,CommonTree_
Dead or damaged trees|AVAILABLE|DeadTree_
Tree trunks and roots|MODIFY|tree-trunk,TwistedTree_
Bush variants|MODIFY|Bush_
Dense shrub clusters|COMBINE|Bush_Common,Bush_Large
Grass clumps|AVAILABLE|Grass_Common_,Grass_Wispy_
Fallen logs|MODIFY|tree-log
Tree stumps|MODIFY|tree-trunk
Forest-floor debris|COMBINE|Petal_,Mushroom_,Pebble_
Rock clusters|COMBINE|Rock_Medium,Pebble_''',
'D. Central battlefield': '''Broken stone pillars|MODIFY|Column_Round,pillar
Ruined columns|MODIFY|Column_,column
Collapsed arches|MODIFY|Wall_ArchRound_Broken,Arch_Gothic
Damaged stone walls|MODIFY|Wall_Broken,wall_cracked
Scattered rubble|COMBINE|Brick,Pebble_
Ancient masonry|COMBINE|Wall_Broken,Column_
Broken monuments|MODIFY|Statue_Stag,Statue_Fox
Central objective structure|CREATE IN BLENDER|Floor_Diamond,Column_Round_Short
Weathered stone pathways|COMBINE|RockPath_,cobblestone_floor_001_4k
Sparse vegetation through ruins|COMBINE|Grass_Common_Short,Fern_1''',
'E. Forward forts': '''Stone wall sections|MODIFY|wall-half-modular,wall
Wall corners|MODIFY|wall-corner
Gate structures|MODIFY|gate,wall-doorway
Towers|COMBINE|tower-square-base,tower-square-top
Battlements|MODIFY|tower-square-top,wall
Stone stair details|MODIFY|stairs-stone,stairs_modular
Courtyard floors|COMBINE|floor_tile_large
Capture-floor architecture|CREATE IN BLENDER|Floor_Diamond
Faction banners|MODIFY|banner_blue,banner_red,flag-banner-long
Defensive architectural details|COMBINE|wall-pillar,barrier''',
'F. Aether Spire home castle': '''Elegant stone towers|MODIFY|tower-hexagon,tower-square
Slender spires|CREATE IN BLENDER|tower-hexagon-roof
Arched entrances|MODIFY|tower-square-arch,Arch_Gothic
Pale slate architecture|MODIFY|wall,tower-square
Teal roofs|MODIFY|tower-hexagon-roof,tower-square-roof
Stone railings|MODIFY|Rail_Straight,barrier
Banners|MODIFY|banner_blue,flag
Magical ornamentation|GENERATE WITH MESHY|
Architectural foundations|CREATE IN BLENDER|tower-square-base
Decorative statues|MODIFY|Statue_Stag,Statue_Fox''',
'G. Forgebound Ridge home castle': '''Heavy fortified towers|COMBINE|tower-square,tower-base
Industrial gatehouses|COMBINE|wall-doorway,tower-square-arch
Timber structures|MODIFY|wall-narrow-wood,floor_wood
Dark stone walls|MODIFY|wall
Copper roofs|MODIFY|tower-square-roof
Chimneys|CREATE IN BLENDER|
Iron railings|MODIFY|metal-gate,Window_Bars
Scaffolding|MODIFY|wall_scaffold,wall_open_scaffold
Forge-themed decorations|CREATE IN BLENDER|barrel_large,wall-pillar
Mechanical architectural details|GENERATE WITH MESHY|''',
'H. Lower farming terrace': '''Worn dirt roads|MODIFY|stony_dirt_path_4k
Scattered stone ruins|COMBINE|Wall_Broken,Column_Round_Short
Small camp structures|COMBINE|wall_scaffold,floor_wood,Wall_Half
Crates|AVAILABLE|box_large,crates_stacked,Crate
Barrels|AVAILABLE|barrel_large,Barrel
Supply caches|COMBINE|box_large,barrel_small,chest
Broken carts|MODIFY|Cart
Campfire or fire pit props|CREATE IN BLENDER|Pebble_,tree-log
Neutral encounter markers|COMBINE|Column_Round_Short,floor_tile_small
Rocks and environmental cover|COMBINE|Rock_Medium,rocks-small
Sparse vegetation|AVAILABLE|Grass_Common_Short,Bush_Common''',
'I. Stairs and transitions': '''Main staircase structures|CREATE IN BLENDER|stairs_wide,stairs-stone
Stair landings|CREATE IN BLENDER|floor_tile_large
Stone steps|MODIFY|stairs_modular_center,Stairs
Broken step variations|MODIFY|stairs_modular_center
Stair sidewalls|MODIFY|stairs_wall_left,stairs_wall_right
Terrain-to-stair transitions|CREATE IN BLENDER|
Cliff-to-stair transitions|CREATE IN BLENDER|''',
'J. Distant environment and atmosphere': '''Distant mountains|COMBINE|mountainside_4k
Valley terrain|CREATE IN BLENDER|
Cliff perimeter scenery|COMBINE|mountainside_4k,Rock_Medium
Distant castle or town silhouettes|COMBINE|tower-square,wall
Waterfalls|CREATE IN BLENDER|
Water surfaces|CREATE IN BLENDER|
Atmospheric fog and haze|CREATE IN BLENDER|
Sky and cloud scenery|MODIFY|mossy_forest
Distant vegetation silhouettes|COMBINE|Pine_,CommonTree_'''}
regions={'A':'Row 15 middle-to-lower cliff, southern perimeter; exact blockers retained','B':'Upper forest, central terrace Y0, lower rows 16-19 Y-1.8; existing stair exceptions','C':'Upper forest rows 0-5; row 3 trail and seven authored connectors retained','D':'Central approaches and objective X14-15/Y10-11','E':'West X6-11/Y8-14; east X18-23/Y8-14; parent anchors from native fort scenes','F':'Authored blue_home anchor and decorative west backdrop','G':'Authored red_home anchor and decorative east backdrop','H':'Rows 16-19, camp clearings X8-9 and X20-21/Y17-18','I':'X4-5 and X24-25/Y15-16; join row17 at -1.8','J':'Outside the 30x20 grid; decoration only'}
reqs=[]
for section,lines in spec.items():
    for line in lines.splitlines():
        name,status,tokens=line.split('|'); sources=[]
        for token in filter(None,tokens.split(',')):
            matches=[a for a in assets if token.casefold() in a['name'].casefold()]
            matches.sort(key=lambda a:(a['license_status']!='CC0-bundled',len(a['name'])))
            for a in matches[:3]:
                if a['id'] not in sources: sources.append(a['id'])
        note={'AVAILABLE':'Source silhouette is reusable after scale/material validation; availability does not waive license gate.','MODIFY':'Adapt palette, proportions, pivot/UVs or damage in Blender; preserve source.','COMBINE':'Instance a small reusable family on connected terrain; hide seams and avoid new collision.','CREATE IN BLENDER':'Fit precise connected geometry to authored elevations/clearances; reuse shared surfaces.','GENERATE WITH MESHY':'Optional distinctive ornament only; use simple Blender substitute first. No generation authorized or performed.'}[status]
        if name=='Sky and cloud scenery': note='Mossy Forest is an obstructed forest HDRI, not open-sky scenery. Useful lighting reference only; create a simple Godot procedural sky/cloud layer for the arena.'; status='CREATE IN BLENDER'
        if name in ('Atmospheric fog and haze','Waterfalls','Water surfaces'): note='Future Godot shader/particle/environment work plus simple Blender support mesh where needed; not a generated static model.'
        if name=='Tree trunks and roots': note+=' Nature pack has no dedicated root kit; exposed root adapters require Blender.'
        reqs.append({'id':f'R30-{len(reqs)+1:03}','section':section,'requirement':name,'classification':status,'candidate_ids':sources,'region':regions[section[0]],'production_note':note,'dependencies':['Connected terrain foundation','Authored gameplay datum and clearance audit'] if section[0] not in 'AJ' else ['Authored gameplay datum and clearance audit'],'license_gate':'Only CC0-bundled sources may progress; review-required sources remain quarantined.','implementation_authorized':False})
reqs.append({'id':f'R30-{len(reqs)+1:03}','section':'Scope exclusions','requirement':'Dedicated reward items, siege armies and interior dressing','classification':'NOT NEEDED','candidate_ids':[],'region':'None','production_note':'Absent from the concept production needs; no farming rewards, NPC economy, traps or mechanics invented.','implementation_authorized':False})
write('AssetRequirements.json',{'schema_version':1,'concept':'References/CliffsideArenaConcept.png','classification_semantics':'Design recommendation, not license clearance or runtime approval. CREATE IN BLENDER includes simple support geometry for Godot sky/water/fog effects, explained per row.','requirements':reqs})

font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20); small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16); title=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',28)
def sheet(raw,ids,filename,heading):
    src=Image.open(raw).convert('RGB'); out=Image.new('RGB',(1800,1580),(24,29,34)); dr=ImageDraw.Draw(out); dr.text((25,15),heading,fill='white',font=title)
    dr.text((25,52),'Normalized size; see manifest for actual dimensions. Geometry preview unless marked source materials.',fill=(185,198,204),font=small)
    for j,aid in enumerate(ids):
        a=byid[aid]; cx=250+(j%5)*325; cy=205+(j//5)*313
        crop=src.crop((cx-157,cy-155,cx+157,cy+145)); crop=crop.resize((314,260)); x=20+(j%5)*356; y=90+(j//5)*365
        out.paste(crop,(x+15,y)); dr.text((x+15,y+266),a['name'][:32],fill='white',font=font)
        dr.text((x+15,y+292),a['id']+'  |  '+format(a['triangles'],',')+' tris',fill=(157,195,204),font=small)
        dr.text((x+15,y+315),'LICENSE REVIEW' if a['license_status']=='review-required' else 'CC0 bundled',fill=(227,183,104) if a['license_status']=='review-required' else (154,197,151),font=small)
    out.save(QA/filename)
for p in inspection['pages']:
    filename=pathlib.Path(p['raw']).name.replace('_raw',''); sheet(ROOT/p['raw'],p['assets'],filename,p['category'])
# Ground materials: show actual albedo textures, not the clay library preview objects.
grounds=[a for a in assets if a['resource_type']=='material']
out=Image.new('RGB',(1500,970),(24,29,34)); dr=ImageDraw.Draw(out); dr.text((20,15),'Ground and masonry material sources — original diffuse maps',fill='white',font=title)
for j,a in enumerate(grounds):
    tex=next((ROOT/a['pack']).rglob('*diff*4k.jpg')); im=Image.open(tex).convert('RGB'); im.thumbnail((460,350)); x=20+j%3*495; y=80+j//3*440; out.paste(im,(x,y)); dr.text((x,y+356),a['name'],font=font,fill='white'); dr.text((x,y+384),a['id']+' | LICENSE REVIEW | 4K original',font=small,fill=(227,183,104))
    a['preview_page']='Ground_materials_01.png'
out.save(QA/'Ground_materials_01.png')
if (MAN/'PbrReview.json').exists():
    pbr=read('PbrReview.json'); sheet(QA/'Source_material_review_raw.png',pbr['assets'],'Source_material_review.png','Source shaders — material, transparency and style review')
    for aid in pbr['assets']: byid[aid]['pbr_preview']='Source_material_review.png'

counts=collections.Counter(a['category'] for a in assets if a['resource_type']=='model')
write('AssetInventory.json',{'schema_version':1,'review_date':'2026-11-15','blender_version':inspection['blender_version'],'summary':{'source_candidates':len(assets),'model_candidates':sum(counts.values()),'material_resources':len(grounds),'exact_local_geometry_families':len(groups),'models_by_category':dict(counts)},'deduplication':'Pack+stem collapses export formats. Exact local mesh signatures group material/name variants; signatures do not compare transforms or topology equivalence after reordering. Alternate FBX variants are retained, not counted twice. LODs belong to the same source model. Preview sphere/plane meshes are excluded.','assets':assets,'all_files_manifest':'ExtractionAudit.json','inspection_evidence':'BlenderInspection.json'})

# Shortlist includes exact source objects and destinations; never auto-promote.
targets=[('CommonTree_1','Nature'),('Pine_1','Nature'),('Bush_Common','Nature'),('Grass_Common_Short','Nature'),('Rock_Medium_1','Nature'),('Pebble_Round_1','Nature'),('wall-half-modular','Kenney'),('wall-corner','Kenney'),('tower-square-base','Kenney'),('tower-square-top','Kenney'),('wall_scaffold','KayKit'),('stairs_modular_center','KayKit'),('barrel_large','KayKit'),('box_large','KayKit'),('banner_blue','KayKit'),('Wall_Broken','Ruins'),('Arch_Gothic','Ruins'),('Column_Round_Short','Ruins'),('Cart','Ruins'),('mountainside_4k','PolyHaven')]
selected=[next(a for a in assets if a['name']==n and pack in a['pack']) for n,pack in targets]
for a in selected: a['shortlisted']=True
write('AssetInventory.json',dict(read('AssetInventory.json'),assets=assets))
catalog=read('AssetInventory.json')
all_paths=[f['path'] for p in audit['archives'] for f in p['files']]
catalog['file_resources']={
    'image_sources':[p for p in all_paths if pathlib.Path(p).suffix.lower() in ('.png','.jpg','.jpeg','.exr','.hdr','.tga','.tif','.tiff')],
    'material_definitions':[p for p in all_paths if pathlib.Path(p).suffix.lower()=='.mtl'],
    'example_or_prefab_scenes':[p for p in all_paths if pathlib.Path(p).suffix.lower() in ('.tscn','.scn','.unity','.prefab','.umap')],
    'standalone_animation_files':[p for p in all_paths if pathlib.Path(p).suffix.lower() in ('.anim','.bvh')],
    'source_documents':[p for p in all_paths if pathlib.Path(p).suffix.lower() in ('.txt','.html','.url')],
    'animation_review_scope':'No actions found in canonical Blender/glTF/GLB/OBJ imports; alternate FBX exports preserved but not independently certified for animation differences.'}
write('AssetInventory.json',catalog)

pages=sorted(p.name for p in QA.glob('*.png') if not p.stem.endswith('_raw'))
cards=''.join(f'<h2>{html.escape(p)}</h2><a href="{p}"><img loading="lazy" src="{p}" width="100%"></a>' for p in pages)
rows=''.join('<tr>'+''.join('<td>'+html.escape(str(x))+'</td>' for x in [a['id'],a['name'],a['category'],a['resource_type'],a['triangles'],', '.join(f'{x:.3f}' for x in a['dimensions_blender_xyz']),a['license_status'],a['source'],', '.join(a['source_objects_exact'])])+'</tr>' for a in assets)
(QA/'index.html').write_text('<!doctype html><meta charset="utf-8"><title>Stage 3.0A asset review</title><style>body{background:#181d22;color:#ddd;font:16px Arial;margin:30px}td{padding:8px;border-bottom:1px solid #555;overflow-wrap:anywhere}table{width:100%;font-size:12px}input{padding:12px;width:80%}a{color:#9bd}</style><h1>Stage 3.0A environment catalog</h1><p>Source discovery only. No models approved or promoted. Clay sheets compare geometry; source shader sheet and diffuse maps compare materials. Equal displayed size does not imply equal source scale. Review-required licenses remain quarantined.</p>'+cards+'<h2>Search all sources</h2><input id="q" placeholder="Filter by name, ID, pack, object, category"><table><thead><tr><th>ID<th>Name<th>Category<th>Resource<th>Triangles<th>Blender XYZ size<th>License<th>Source<th>Exact source nodes</tr></thead><tbody>'+rows+'</tbody></table><script>document.getElementById("q").oninput=e=>document.querySelectorAll("tbody tr").forEach(r=>r.hidden=!r.textContent.toLowerCase().includes(e.target.value.toLowerCase()))</script>',encoding='utf-8')

report='''# Stage 3.0A — Environment asset discovery and production inventory

Inventory and visual catalog complete; license review complete with explicit holds. **No runtime promotion, generation, scene construction or gameplay changes.** Review date: 2026-11-15 (user timezone). SourceAssets and QA binaries are local/ignored by the repository's existing policy; back them up with the archives. The scripts and this report are versionable.

## Ground truth and concept

Reviewed `References/CliffsideArenaConcept.png`, the current `Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn`, both native forward-fort scenes, the roadmap, style guide, Stage 2.3 documentation and authored Cliffside tests. The illustration shows connected rocky plateaus, upper forest, a broad central stone battlefield, paired open forts, pale/teal western and industrial/copper eastern home architecture, a lower ruin-lined farming route and decorative valley/waterfalls. Its printed grid is conceptual, not placement data.

The gameplay grid and visible environment are separate systems. Organic visual meshes may span cells. Board: 30×20, 1.25 spacing; tile center X=-2.125+1.25x, Z=4.125+1.25y. Central floor Y0; lower terrace Y-1.8, with existing stair exceptions. Stairs X4–5 / X24–25: row15 Y-0.6, row16 Y-1.2, row17 Y-1.8. Preserve legal movement, collision, LOS and elevation flags. Trees/bushes stay traversable; no evasion is implemented.

Forward forts come from authored data: west X6–11/Y8–14, east X18–23/Y8–14; each has 14 wall blockers and four double gates. Capture cells west X8–9/Y10–11, east X20–21/Y10–11. Six arrivals west X7–9/Y12–13, east X20–22/Y12–13. Central objective X14–15/Y10–11. Lower camp clearings X8–9 and X20–21/Y17–18. Home castles remain on the existing blue_home/red_home scene anchors, with no new footprints or transfer rules.

## Downloads, extraction and provenance

{archive_count} ZIP archives extracted into {zip_destinations} archive destination folders; the two timestamped ZIPs share one ruins-pack root so their texture relationship is retained. One standalone `mossy_forest_4k.exr` copied/hash-verified separately. There are {pack_roots} resource roots in total (four collection packs, eight individual Poly Haven resources, one HDRI). Original downloads and all documents remain unchanged.

All ZIP members passed full CRC checks before extraction. The extractor checks absolute/drive/UNC paths, parent traversal, Windows reserved names, trailing-dot/space aliases, case collisions, symlinks and special entries, destination containment, encryption, expansion size/ratio and free space. No bundled executable, shortcut, installer, add-on or script was executed. Background Blender used factory startup and disabled automatic script execution. Extracted files have per-file SHA-256 values; originals have archive SHA-256 values and byte counts in `ExtractionAudit.json`. Available disk exceeded 620 GB; total unpacked bytes are recorded there. Repeat extraction verifies existing content rather than overwriting differing files.

| Download | Unpacked files | Destination |
|---|---:|---|
{archive_table}

The timestamped Blends archive really contains **91 individual Blender sources**: broken walls, Gothic/round arches, columns, stairs, floor pieces, bushes, trees, props and animal statues. The Textures archive contains `Leaf_Texture.png` and `Bark_Texture.jpg`; packed source references corroborate this pairing. This strongly matches the [Ultimate Modular Ruins pack](https://quaternius.com/packs/ultimatemodularruins.html), whose publisher lists CC0 and 90 models. The 91-versus-90 difference and lack of original receipt/bundled license are recorded rather than silently treating identity as cryptographically verified. Keep this pack review-required until pack provenance is confirmed. No animated character/action was found in these 91 files.

The Nature archive is **Standard: 68 models**, confirmed by its included `License_Standard.txt`; it is not the full 116-model Source edition and contains no ready-made Godot shader project. KayKit has 216 canonical model names; Kenney has 76. Alternate formats and FBX variants remain preserved and do not inflate these counts. Six Poly Haven libraries contain material-preview sphere/plane geometry; these are not terrain assets. Boulder and Mountainside each contain four LODs.

## Licensing review

| Pack/resource | Status | Evidence / action |
|---|---|---|
| KayKit Dungeon 1.1 | CC0 bundled | Original License.txt retained; Kay Lousberg credit optional |
| Kenney Castle Kit 2.0 | CC0 bundled | Original License.txt retained; Kenney credit optional |
| Quaternius Nature Standard | CC0 bundled | Original License_Standard.txt explicitly identifies 68/116 edition |
| Timestamped ruins archives | REVIEW REQUIRED | Publisher CC0 statement and strong content match; no bundled license or recovered receipt; no promotion |
| Eight Poly Haven ZIP resources and HDRI | REVIEW REQUIRED | No bundled license documents. [Publisher policy](https://polyhaven.com/license) declares assets CC0; exact asset-page references recorded. Obtain pack-specific provenance/license record before promotion |

`AssetLicenses.json` records per-root status, source/reference URLs, documents, optional attribution and promotion permission. URL references are publisher evidence, not recovered original download URLs. No unknown license is assumed to permit commercial use. This completes the review with explicit unresolved holds; it does not claim every source is cleared.

## Measured inventory

**{models} canonical model candidates**, **{materials} material resources**, one HDRI. **{families} exact local mesh-signature families** group identical geometry across named/material variants; 453 source names are useful production variants, not a claim of 453 distinct silhouettes. Mesh signatures are conservative and do not normalize vertex order or transforms. Every source imported successfully into Blender {blender}; no unresolved image dependencies remained after unique filename resolution or packed-image fallback. UV layer names, source objects/collections, material slots, texture paths/dimensions, bounds, triangle counts, modifiers and actions are in the manifest. No animation actions were found. Authored source units are not assumed to equal Godot metres; dimensions are Blender world XYZ before normalization, Z-up; convert to Godot Y-up during a future export ticket.

| Model category | Candidates | Useful production direction |
|---|---:|---|
{count_table}

Category counts describe discovery, not approval. The prop pool includes many low-priority dungeon interiors and banner variants. Geometry is reusable/instanceable after export, except LOD alternatives must not be stacked. Counts are based on source meshes before modifiers; displacement/subdivision modifiers and alpha foliage can have a greater render cost.

Poly Haven boulder: LOD0 66,122; LOD1 33,060; LOD2 16,530; LOD3 8,264 triangles. Mountainside: LOD0 153,472; LOD1 76,735; LOD2 38,366; LOD3 19,182. Use LOD3 only as an initial comparison, or simplify further for tactical use. Do not export all four LOD objects at once. Mountainside is a rock/cliff formation, not a complete mountain landscape. Mossy Rock is a texture, not a rock model.

## Most useful exact sources and proposed destinations

These are review shortlists, not final game selections. Every candidate has an exact source and proposed destination in `AssetInventory.json`; no runtime files were written. Native source object lists and glTF node names distinguish source names from Blender's temporary collision-renamed inspection objects.

| Candidate / catalog ID | Exact source objects/nodes | Original source | Proposed destination |
|---|---|---|---|
{shortlist}

Near-immediate geometry reuse: KayKit boxes/barrels, Nature grass/pebbles and dead trees after palette/scale checks. Blender adaptation: Kenney fort walls/corners/towers and roofs; KayKit stairs/scaffolds/banners; Nature living foliage; ruins wall/arch/column sources once license hold clears. None is declared ready for final Godot use without material and clearance review.

## Requirements and production choices

`AssetRequirements.json` contains **{requirements} explicit rows** covering all requested A–J types, classification, source IDs, placement region, production notes, dependency and license gate. The following table is the reviewable human copy.

| Region / requirement | Choice | Source IDs |
|---|---|---|
{requirements_table}

No mandatory Meshy job is justified yet. Only distinctive Aether ornament and Ridge mechanical ornament are optional generation candidates if reused statues/kit pieces and simple Blender work fail art review. Produce these late; no paid calls or generations occurred. Precision terrain connections, cliff corners/top/bottom adapters, roots, stair joins/landings, foundations, objective/capture-floor inlays, chimney shapes and fire-pit rings are Blender work. Waterfalls/water/fog/sky require future Godot shaders/effects and simple support geometry, not expensive static model generation.

Avoid for this battlefield: siege engines, bear traps, dungeon spikes, coins/keys, tiny bottles/plates, beds, tables and interior bookcases. Their files remain preserved. Avoid unmodified bright saturated palettes, glossy photogrammetry, 4K EXR maps on small props, disconnected tile-looking slabs, and all-source-LOD exports. Forest HDRI is obstructed and unsuitable as the arena's distant open sky; use it as a lighting reference only. No crop/farming reward asset or gameplay is inferred from the concept's lower route.

## Visual and material consistency

Clay contact sheets reveal silhouettes and geometry independently of incompatible source shading. Nature has broad clustered leaves and faceted rocks, fitting the miniature style after desaturation; conifers, common/dead/twisted trees give enough starting variation. Leaf cards require alpha/shadow testing and tactical-camera overdraw review. Kenney's clean toy-like towers and walls need less saturated materials, softer edge treatment, variation and foundation adapters. KayKit's rounded blocks and warm timber can supply stairs, scaffolds and props; reserve contrasting material detail for larger forms. Ruins have useful broken openings and Gothic curves but must be graded against the Stage 2.2 matte slate baseline once cleared.

Use charcoal/slate anchors, desaturated forest green, weathered earth and restrained moss; stone roughness near 0.80–0.92, metallic 0, normals initially near the calibrated 0.32 baseline. Teal/pale Aether and copper/iron/timber Ridge accents remain subordinate to units and ownership cues. Do not change every source to one shared stone material: foliage, cloth, wood and metal retain appropriate responses. Existing sources are unchanged; no batch retexturing was performed. The source-material review shows particularly dark leaf-card patches, a red-toned bush and very clean warm Kenney stone: inspect leaf normals/transparency and seasonal texture choice before promotion, then grade the palette. Some ruins use constant materials without UVs; create UVs before applying stone textures or trim sheets. Manifold/collision readiness is not certified by discovery renders.

For Poly Haven preserve all 4K originals, including EXR normal/roughness and displacement. Recommend shared 1K–2K ground maps and 512px–1K small prop maps, then compare at actual min/normal/max gameplay zoom. Bake/convert normal and roughness semantics carefully (Non-Color, GL normal direction); use runtime PNG/compressed textures and pack roughness channels appropriately. Avoid displacement tessellation for small tactical props. Reuse atlases/trim sheets and profile draw calls and alpha coverage before consolidation. No new engine performance budget is asserted.

## Catalog and review limits

[Open searchable visual catalog](../SourceAssets/Environment/QA/ContactSheets/index.html). It includes all source candidates, category sheets with names and IDs, six original ground diffuse-map swatches, and a supplemental Cycles source-material sheet. Normalized geometry previews are for shape comparison, not measured scale or final material acceptance. The source shader sheet uses unmodified materials, in-memory 1K image reduction and 12 Cycles samples. Read actual dimensions/LOD data from JSON. The mountainside was initially grouped with material downloads by filename; the final manifest corrects it to rocks, and the source-shader sheet shows its real geometry. Clay pages retain original batch membership so their cell references stay reproducible.

All sheets were checked in a thumbnail overview plus representative full pages. Source colors need grading; packed foliage images remain usable despite Blender warning about their unavailable external duplicate path. No live Godot import or battlefield camera acceptance was performed, intentionally: final material/clearance/performance approval belongs to a later single-section integration ticket. Tactical suitability here is an assessment of broad shapes against the concept and existing camera evidence, not a claim of game-tested art.

{sheet_links}

## First connected world-building milestone and dependencies

Recommended first section: **the west central-to-lower terrace junction**, centered on existing X4–5 stairs and extending into a short adjacent cliff/ground run. Build one connected terrain foundation across several cells, with the Y0 central floor, authored intermediate stair levels and Y-1.8 lower floor. Preserve the legal stair corridor and camp access. Use a quiet ground surface first; fit cliff top, bottom and stair adapters before decorating. This establishes organic connectivity and exact gameplay alignment in one reviewable slice, without constructing the whole map.

1. Snapshot gameplay data and authored anchors; define mesh envelope and visual picking/clearance checks.
2. Connected terrain foundation and shared dirt/grass/stone transitions.
3. Integrated cliff faces, convex/concave boundaries, lower skirt and exact stair junctions. Reuse the existing 2.3B cliff material/pair where suitable; precision adapters remain local Blender work.
4. Upper forest border and sparse lower vegetation; retain traversability and existing LOS flags.
5. Central ruin kit and objective-floor composition after ruins provenance clears; avoid covering approach lanes.
6. One fort shell at authored wall parents and capture/arrival floor, then mirrored fort review. Home-castle foundations depend on terrain, shared walls/towers, faction shape review and retained transfer clearance.
7. Faction roofs, railings, banners, scaffolds and optional ornaments; decorative valley terrain, castle silhouettes, water and haze follow the foreground silhouette.
8. Profile LODs/texture memory/overdraw and review actual tactical camera with units before any wider rollout.

## Reproduction and preservation

`python Tools/Stage30ADiscover.py` validates/extracts sources and rebuilds canonical source candidates; `D:/Blender/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools/Stage30ABlenderCatalog.py` inspects and renders clay batches; run `Tools/Stage30APbrReview.py` with the same Blender flags for the material review; `python Tools/Stage30AFinalize.py` rebuilds the labeled catalog/manifests/report. Run in that order. Scripts never save extracted sources or export runtime assets. The environment root has `.gdignore` to prevent mass Godot import. No scenes/scripts/tests/characters were edited by Stage 3.0A; the pre-existing dirty worktree was preserved. Preservation checks and complete source-file verification are recorded separately in `Stage30AValidation.json`.

Stop here for review. Connected terrain production and final model selection are the next separately scoped task.
'''
archive_table='\n'.join(f"| `{pathlib.Path(p['archive']).name}` | {len(p['files'])} | `{p['destination']}` |" for p in audit['archives'])
shortlist='\n'.join(f"| {a['name']} / `{a['id']}` | {', '.join(a['source_objects_exact'])} | `{a['source']}` | `{a['proposed_destination']}` |" for a in selected)
report=report.format(archive_count=13,zip_destinations=len(set(x['destination'] for x in audit['archives'] if x['archive'].endswith('.zip'))),pack_roots=len(licenses),archive_table=archive_table,models=sum(counts.values()),materials=len(grounds),families=len(groups),blender=inspection['blender_version'],count_table='\n'.join(f'| {c} | {n} | '+{'Castle and fort architecture':'Reuse modular walls/towers/stairs with fit and style changes','Props and environmental decoration':'Use supplies/banners; avoid unnecessary interior detail','Ruins and broken structures':'Broken walls, columns and arches; license hold applies','Trees and forest vegetation':'Living/dead trees, shrubs, grass and flowers','Rocks and cliff formations':'Stylistic rocks/pebbles and high-detail cliff sources'}[c]+' |' for c,n in sorted(counts.items())),shortlist=shortlist,requirements=len(reqs),requirements_table='\n'.join(f"| {r['section'][0]} / {r['requirement']} | {r['classification']} | {', '.join(r['candidate_ids']) or 'New support geometry / effect'} |" for r in reqs),sheet_links='\n'.join(f'- [{p}](../SourceAssets/Environment/QA/ContactSheets/{p})' for p in pages))
(ROOT/'Docs/Stage30AEnvironmentAssetInventory.md').write_text(report,encoding='utf-8')
print(json.dumps({'models':sum(counts.values()),'materials':len(grounds),'geometry_families':len(groups),'requirements':len(reqs),'sheets':len(pages),'categories':counts},indent=2))
