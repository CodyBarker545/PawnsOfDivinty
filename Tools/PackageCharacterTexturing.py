"""Collect six character deliveries and produce matched visual comparison sheets."""
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from AuditCharacterTexturing import ROOT,inspect,MODELS

OUT=ROOT/'artifacts/character_texturing'
DEST=ROOT/'SourceAssets/characters/textured_roster'
LABELS={'AssassinTextured':'Hooded Assassin','GentleGuardian':'Gentle Guardian','TimeWeaver':'Time Weaver',
        'Nigel':'Nigel','Colossus':'Gravebound Colossus','SpiritGuardian':'Beothuk Spirit Guardian'}

def main():
    assassin=DEST/'AssassinTextured';(assassin/'textures').mkdir(parents=True,exist_ok=True)
    shutil.copy2(ROOT/'Assets/Characters/MobaBeta/AssassinTextured.glb',assassin/'AssassinTextured.glb')
    shutil.copy2(ROOT/'SourceAssets/characters/hooded_assassin/texturing_validation/AssassinTextured_Validated.blend',assassin/'AssassinTextured.blend')
    source=next((ROOT/'SourceAssets/characters/hooded_assassin/working_textured_model_assassin').rglob('*.fbx'))
    for suffix,kind in [('', 'base_color'),('_normal','normal'),('_roughness','roughness'),('_metallic','metallic')]:
        shutil.copy2(source.with_name(source.stem+suffix+'.png'),assassin/'textures'/f'{kind}.png')
    manifest=[]
    baseline=json.loads((OUT/'baseline.json').read_text())['models']
    for name in MODELS:
        runtime=ROOT/f'Assets/Characters/MobaBeta/{name}.glb'
        current=inspect(runtime)
        assert current['protected_sha256']==baseline[name]['protected_sha256']
        assert current['animation_library_sha256']==baseline[name]['animation_library_sha256']
        folder=DEST/name
        assert (folder/f'{name}.blend').exists() and (folder/f'{name}.glb').exists()
        shutil.copy2(ROOT/f'Assets/Animations/Mixamo/{name}.tres',folder/'Animations.tres')
        maps={kind:str(folder/'textures'/f'{kind}.png') for kind in ('base_color','normal','roughness','metallic')}
        assert all(Path(p).exists() for p in maps.values())
        record={'character':LABELS[name],'runtime':str(runtime),'blender_source':str(folder/f'{name}.blend'),
                'delivered_model':str(folder/f'{name}.glb'),'maps':maps,'protected_sha256':current['protected_sha256'],
                'runtime_sha256':current['sha256'],'animation_library_sha256':current['animation_library_sha256'],
                'clips':current['clips'],'vertices':current['primitive_vertex_count'],'bones':current['bone_counts'],
                'new_meshy_credits':0 if name=='AssassinTextured' else 10}
        manifest.append(record)
    (DEST/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    jobs=[json.loads(p.read_text()) for p in (OUT/'jobs').glob('*/job.json')]
    assert len(jobs)==5 and all(j['status']=='DOWNLOADED' for j in jobs)
    assert sum(j['estimated_credits'] for j in jobs)==50
    (OUT/'completed_jobs.json').write_text(json.dumps(jobs,indent=2)+'\n')
    backup=OUT/'backups/audit_baseline_001'
    for record in json.loads((backup/'manifest.json').read_text()):
        assert hashlib.sha256((backup/record['path']).read_bytes()).hexdigest()==record['sha256'], record['path']
    for record in json.loads((OUT/'blender_source_audit.json').read_text()):
        assert hashlib.sha256(Path(record['source']).read_bytes()).hexdigest()==record['sha256'], record['source']
    (OUT/'backup_integrity.json').write_text(json.dumps({'original_backup_checksums':'PASS',
        'all_six_legacy_blender_sources_unchanged_since_audit':True,
        'backed_up_animated_reviews':[p.relative_to(backup).as_posix() for p in (backup/'artifacts/animations').glob('*.glb')]},indent=2))
    try:font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',25)
    except OSError:font=ImageFont.load_default()
    # Each panel uses a freshly rendered original and current model with the
    # same camera, light, pose and resolution; only textures/materials differ.
    sheet=Image.new('RGB',(1600,6*490),(23,28,36));draw=ImageDraw.Draw(sheet)
    gallery=Image.new('RGB',(1500,2*490),(23,28,36));gd=ImageDraw.Draw(gallery)
    for i,name in enumerate(MODELS):
        y=i*490;draw.text((20,y+8),LABELS[name],font=font,fill='white')
        draw.text((580,y+8),'Before',font=font,fill='#aab6c7');draw.text((1380,y+8),'After',font=font,fill='#8edbb3')
        for j,version in enumerate(('before','after')):
            image=Image.open(OUT/f'comparison_renders/{name}_{version}.png').convert('RGB')
            image=image.resize((800,450),Image.Resampling.LANCZOS)
            sheet.paste(image,(j*800,y+40))
        image=Image.open(OUT/f'comparison_renders/{name}_after.png').convert('RGB')
        # The character occupies the central region of a landscape render.
        image=image.crop((360,30,920,710)).resize((370,450),Image.Resampling.LANCZOS)
        gx=(i%3)*500;gy=(i//3)*490
        gallery.paste(image,(gx+65,gy+40));gd.text((gx+20,gy+8),LABELS[name],font=font,fill='white')
    sheet.save(OUT/'before_after.png');gallery.save(OUT/'finished_roster.png')
    print('Six character deliveries verified; 50-credit job manifest and matched comparison sheets written.')

if __name__=='__main__':main()
