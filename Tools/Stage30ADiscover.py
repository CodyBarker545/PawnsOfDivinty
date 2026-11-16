"""Stage 3.0A: validate/extract inert archives and enumerate canonical sources."""
import collections, hashlib, json, os, pathlib, re, shutil, stat, zipfile
ROOT = pathlib.Path(__file__).resolve().parents[1]
BASE = ROOT/'SourceAssets/Environment'
MAN = BASE/'Manifests'
MAN.mkdir(parents=True, exist_ok=True)
(BASE/'.gdignore').touch()
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''): h.update(b)
    return h.hexdigest()
def rel(p): return p.relative_to(ROOT).as_posix()
def dest(p):
    n=p.name
    if n.startswith(('Blends-','Textures-')): return 'Quaternius/UltimateModularRuins'
    if n.startswith('Stylized'): return 'Quaternius/StylizedNatureMegaKit'
    if n.startswith('KayKit'): return 'KayKit/DungeonPack'
    if n.startswith('kenney'): return 'Kenney/CastleKit'
    return 'PolyHaven/'+('Lighting' if p.suffix=='.exr' else 'Rocks' if n.startswith('boulder') else 'Terrain')+'/'+n.split('_4k')[0]
packs=[]
for p in sorted((ROOT/'SourceAssets/levels/downloads').iterdir()):
    if not p.is_file(): continue
    out=BASE/'ThirdParty'/dest(p)
    row={'archive':rel(p),'sha256':digest(p),'compressed_bytes':p.stat().st_size,'destination':rel(out),'download_provenance':'User-provided download directory; original download URL/time unavailable','files':[]}
    if p.suffix.lower()=='.zip':
        with zipfile.ZipFile(p) as z:
            entries=z.infolist(); seen=set(); total=sum(i.file_size for i in entries)
            row.update(uncompressed_bytes=total,entry_count=len(entries),disk_free_before=shutil.disk_usage(BASE).free)
            assert total+1024**3 < row['disk_free_before'], 'Insufficient free disk space'
            for i in entries:
                name=i.filename.replace('\\','/'); parts=pathlib.PurePosixPath(name).parts
                assert not name.startswith('/') and parts and not any(x in ('..','.') or ':' in x or x.rstrip(' .')!=x for x in parts),name
                assert not any(re.match(r'^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\.|$)',x,re.I) for x in parts),name
                mode=i.external_attr>>16
                assert not stat.S_ISLNK(mode) and (not stat.S_IFMT(mode) or stat.S_ISREG(mode) or stat.S_ISDIR(mode)),name
                raw_target=out.joinpath(*parts)
                assert not any(a.is_symlink() for a in [out,raw_target,*raw_target.parents]),name
                target=raw_target.resolve()
                assert target.is_relative_to(out.resolve()),name
                key=name.casefold().rstrip('/')
                assert key not in seen, 'Duplicate/case-colliding entry '+name
                seen.add(key)
                assert not i.flag_bits&1,'Encrypted archive'
                assert i.file_size < 4*1024**3 and i.file_size/max(i.compress_size,1)<5000,'Suspicious expansion'
            assert z.testzip() is None,'CRC failed'
            row['integrity']='All member CRCs passed; paths/types/space checked'
            for i in entries:
                if i.is_dir(): continue
                target=out.joinpath(*pathlib.PurePosixPath(i.filename.replace('\\','/')).parts)
                target.parent.mkdir(parents=True,exist_ok=True)
                data=z.read(i)
                if target.exists(): assert target.read_bytes()==data,'Existing source differs: '+str(target)
                else: target.write_bytes(data)
                row['files'].append({'path':rel(target),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
    else:
        out.mkdir(parents=True,exist_ok=True); target=out/p.name
        if not target.exists(): shutil.copy2(p,target)
        assert digest(target)==row['sha256']
        row.update(uncompressed_bytes=p.stat().st_size,integrity='Standalone source copy hash verified',files=[{'path':rel(target),'bytes':p.stat().st_size,'sha256':digest(target)}])
    packs.append(row)
    print(p.name,'->',row['destination'],flush=True)
files=[ROOT/f['path'] for p in packs for f in p['files']]
licenses=[]
for p in files:
    if p.suffix.lower() in ('.txt','.url','.html'):
        licenses.append({'path':rel(p),'text':p.read_text(errors='replace')[:20000]})
(MAN/'ExtractionAudit.json').write_text(json.dumps({'schema_version':1,'archives':packs,'documents':licenses},indent=2))
# Prefer native blend sources, then glTF, GLB, FBX, OBJ. Same pack+stem is one candidate.
groups=collections.defaultdict(list)
for p in files:
    if p.suffix.lower() in ('.blend','.gltf','.glb','.fbx','.obj'):
        pack=next(x['destination'] for x in packs if rel(p).startswith(x['destination']+'/'))
        groups[(pack,p.stem.casefold())].append(p)
def category(n,pack):
    n=n.lower()
    if '/Terrain/' in pack: return 'Ground materials'
    if any(x in n for x in ('tree','bush','grass','clover','fern','flower','plant','log','stump','mushroom','root','leaf')): return 'Trees and forest vegetation'
    if any(x in n for x in ('rock','boulder','cliff')): return 'Rocks and cliff formations'
    if 'Ruins' in pack or any(x in n for x in ('broken','rubble','ruin','debris')): return 'Ruins and broken structures'
    if any(x in n for x in ('wall','tower','gate','roof','stairs','stair','floor','column','pillar','arch','bridge','ground','door','window','battlement','ramp','fence')): return 'Castle and fort architecture'
    return 'Props and environmental decoration'
assets=[]
for (pack,stem),paths in sorted(groups.items()):
    paths.sort(key=lambda p: (['.blend','.gltf','.glb','.fbx','.obj'].index(p.suffix.lower()),len(str(p)),str(p)))
    p=paths[0]; aid='E30-'+hashlib.sha1((pack+'/'+stem).encode()).hexdigest()[:9]
    assets.append({'id':aid,'name':p.stem,'pack':pack,'source':rel(p),'alternate_formats':[rel(q) for q in paths[1:]],'category':category(p.stem,pack),'license_status':'review-required','proposed_destination':'Assets/Levels/Environment/'+aid+'/'+p.stem+'.glb','promotion_status':'proposal only; no runtime export','reuse':'instance after style/scale/UV audit','style_work':'Muted palette, matte response; tactical-camera review and material consolidation before promotion'})
(MAN/'AssetInventory.json').write_text(json.dumps({'schema_version':1,'deduplication':'Pack + case-insensitive source stem; format and material-variant geometry duplicates are additionally identified by Blender signatures. Material library BLENDs are not counted as model candidates.','assets':assets},indent=2))
print('Canonical source candidates',len(assets),'documents',len(licenses))
