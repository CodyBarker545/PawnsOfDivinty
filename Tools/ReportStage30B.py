"""Preservation evidence and before/after review artifacts for the completed slice."""
import hashlib,json,pathlib,re,shutil
from PIL import Image,ImageDraw,ImageFont
ROOT=pathlib.Path(__file__).resolve().parents[1]; SRC=ROOT/'SourceAssets/Environment/WesternStairSlice'; QA=SRC/'QA'; RUN=ROOT/'Assets/Levels/Environment/WesternStairSlice'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
sources=json.loads((SRC/'SelectedAssets.json').read_text())
code=json.loads((SRC/'Baseline/CodeHashes.json').read_text())
assert all(sha(ROOT/p)==h for p,h in code.items())
assert all(sha(ROOT/a['source'])==a['source_sha256'] for a in sources['assets'])
before=ROOT/'artifacts/stage30b_before_layout.json';after=ROOT/'artifacts/stage30b_after_layout.json'
assert before.read_bytes()==after.read_bytes()
measure_before=json.loads((ROOT/'artifacts/stage30b_before_measurements.json').read_text());measure_after=json.loads((ROOT/'artifacts/stage30b_after_measurements.json').read_text());assert measure_before==measure_after
metrics=json.loads((ROOT/'artifacts/stage30b_runtime_geometry.json').read_text());assert sum(x['triangles'] for x in metrics['meshes'])==sources['total_triangles']
test_log=(ROOT/'artifacts/stage30b_tests.log').read_text();assert re.search(r'Passed:\s+104',test_log) and re.search(r'Failed:\s+0',test_log)
assert 'GODOT MOBA BETA SMOKE PASSED' in (ROOT/'artifacts/stage30b_smoke.log').read_text()
for tag in ('before','after'):
    for view in ('close','tactical'):shutil.copy2(ROOT/f'artifacts/stage30b_{tag}_{view}.png',QA/f'Godot_{tag}_{view}.png')
shutil.copy2(ROOT/'SourceAssets/Environment/ThirdParty/Quaternius/StylizedNatureMegaKit/License_Standard.txt',SRC/'NatureStandard_CC0.txt')
font=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',28)
out=Image.new('RGB',(2560,500),(24,29,34));dr=ImageDraw.Draw(out)
for i,tag in enumerate(('before','after')):
    dr.text((i*1280+20,15),'BEFORE — greybox' if i==0 else 'AFTER — Stage 3.0B western stair slice',font=font,fill='white')
    out.paste(Image.open(QA/f'Godot_{tag}_close.png'),(i*1280,60))
out.save(QA/'BeforeAfter.png')
summary={'stage':'3.0B','status':'constructed and validated; user visual review pending','gameplay_export_sha256':sha(after),'before_after_layout_identical':True,'original_code_files_unchanged':len(code),'new_tests':4,'source_models_verified_unchanged':len(sources['assets']),'visible_meshes':len(metrics['meshes']),'triangles':sources['total_triangles'],'surfaces':sum(x['surfaces'] for x in metrics['meshes']),'materials':4,'runtime_map_size':512,'runtime_shared_maps':2,'glb_bytes':(RUN/'WesternStairSlice.glb').stat().st_size,'hidden_legacy_meshes':len(json.loads((SRC/'HiddenLegacyNodes.json').read_text())),'standing_centers_checked':len(metrics['standing_samples']),'max_standing_height_delta':max(abs(x['visual_height']-x['gameplay_height']) for x in metrics['standing_samples']),'focused_tests_passed':104,'smoke':'passed','automatic_collision':False,'meshy_calls':0,'paid_calls':0,'limitations':['No target-hardware frame-time/VRAM benchmark','Remaining map remains greybox','Only this local western junction constructed','Source vertex tint requires explicit Godot override materials; GLB alone is not the styled runtime artifact'],'qa_images':[p.name for p in QA.glob('*.png')]}
(SRC/'Validation.json').write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))
