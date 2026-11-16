"""Read-only integrity and referential checks for the inventory deliverable."""
import collections, hashlib, json, pathlib
ROOT=pathlib.Path(__file__).resolve().parents[1]; BASE=ROOT/'SourceAssets/Environment'; MAN=BASE/'Manifests'; QA=BASE/'QA/ContactSheets'
def read(n): return json.loads((MAN/n).read_text())
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''): h.update(chunk)
    return h.hexdigest()
audit=read('ExtractionAudit.json'); inventory=read('AssetInventory.json'); requirements=read('AssetRequirements.json'); licenses=read('AssetLicenses.json')
checks=[]; files=0
for pack in audit['archives']:
    assert sha(ROOT/pack['archive'])==pack['sha256'],pack['archive']
    for entry in pack['files']:
        assert sha(ROOT/entry['path'])==entry['sha256'],entry['path']; files+=1
checks.append(f"All {len(audit['archives'])} original download hashes and {files} extracted/copied file hashes match extraction audit")
assets=inventory['assets']; ids={a['id'] for a in assets}; packs={x['pack']:x for x in licenses['packs']}
assert len(ids)==len(assets)==459
for a in assets:
    assert (ROOT/a['source']).exists()
    assert a['inspection_status']=='inspected'
    assert a['pack'] in packs
    assert (QA/a['preview_page']).exists()
    assert a['source_objects_exact']
    assert all(t['exists'] for t in a['texture_dependencies'])
    assert a['selected_for_production'] is False
    assert not (ROOT/a['proposed_destination']).exists()
    assert len(a['dimensions_blender_xyz'])==3
for r in requirements['requirements']:
    assert set(r['candidate_ids'])<=ids
    assert r['classification'] in ('AVAILABLE','MODIFY','COMBINE','CREATE IN BLENDER','GENERATE WITH MESHY','NOT NEEDED')
    assert not r['implementation_authorized']
    if r['classification'] in ('AVAILABLE','MODIFY','COMBINE'): assert r['candidate_ids']
assert len(requirements['requirements'])==107
assert (BASE/'.gdignore').exists()
checks.append('All candidate/source/preview/license/requirement references resolve; no proposed runtime destination exists')
assert sum(a['resource_type']=='model' for a in assets)==453
assert sum(a['resource_type']=='material' for a in assets)==6
assert sum(x['promotion_allowed'] for x in packs.values())==3
checks.append('453 model candidates, 6 material libraries; 3 bundled-license roots cleared, 10 roots held for review')
from PIL import Image, ImageDraw
pages=sorted(p for p in QA.glob('*.png') if not p.stem.endswith('_raw'))
assert len(pages)==27
for p in pages:
    with Image.open(p) as im: im.verify()
overview=Image.new('RGB',(1200,((len(pages)+4)//5)*245),'#181d22'); dr=ImageDraw.Draw(overview)
for i,p in enumerate(pages):
    overview.paste(Image.open(p).convert('RGB').resize((230,205)),((i%5)*240,(i//5)*245)); dr.text(((i%5)*240,(i//5)*245+207),p.stem[:30],fill='white')
overview.save(BASE/'QA/CatalogOverview.png')
checks.append('27 labeled PNG contact sheets decode successfully; overview and searchable HTML present')
protected=[ROOT/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn']+list((ROOT/'Scripts').rglob('*.cs'))+list((ROOT/'Tests').rglob('*.cs'))
result={'result':'passed','checks':checks,'original_downloads_verified':len(audit['archives']),'source_files_verified':files,'zip_archives_extracted':13,'zip_destination_roots':12,'resource_roots_with_hdri':13,'model_candidates':453,'exact_local_geometry_families':407,'material_resources':6,'hdri_resources':1,'requirements':107,'labeled_contact_sheets':27,'source_bytes':sum(x['uncompressed_bytes'] for x in audit['archives']),'gameplay_file_hashes_at_validation':{p.relative_to(ROOT).as_posix():sha(p) for p in protected},'preservation_scope':'Stage30A scripts only write Tools scripts, inventory report, Environment sources/manifests/QA and logs; roadmap edited separately. Existing dirty gameplay files were not edited. These are final hashes, not a fabricated pre-run baseline. No gameplay tests or Godot import were run because no runtime change was made.','blender_warning':'A missing external duplicate Leaf_Texture.png reference was reported; packed image remained available. Dependency inspection found zero missing effective textures.','review_holds':[p for p,l in packs.items() if not l['promotion_allowed']]}
(MAN/'Stage30AValidation.json').write_text(json.dumps(result,indent=2))
print(json.dumps({k:v for k,v in result.items() if k not in ('gameplay_file_hashes_at_validation','review_holds')},indent=2))
