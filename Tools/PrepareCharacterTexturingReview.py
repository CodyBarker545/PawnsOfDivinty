"""Prepare local backups and UNSUBMITTED Meshy job proposals. No network calls."""
import hashlib
import json
import shutil
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/character_texturing'
BACKUP=OUT/'backups/audit_baseline_001'
REFERENCES={
    'AssassinTextured':'Dark Fantasy Assassin Blueprint Sheet.png',
    'GentleGuardian':'The Gentle Guardian Character Concept Sheet.png',
    'TimeWeaver':'Time Weaver Mage Concept Sheet.png',
    'Nigel':'Nigel the Orc_ Warrior Concept Sheet.png',
    'Colossus':'Gravebound Colossus Concept Sheet.png',
    'SpiritGuardian':'Coastal Spirit Guardian Concept Sheet.png',
}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    baseline=json.loads((OUT/'baseline.json').read_text())
    files=[]
    for name,r in baseline['models'].items():
        files.extend([ROOT/r['path'],ROOT/r['animation_library']])
        files.extend((ROOT/'Assets/Characters/MobaBeta').glob(name+'*.png'))
    files.extend([ROOT/'Assets/Animations/Mixamo/manifest.json',ROOT/'Docs/MOBA_BETA_MODEL_SOURCES.json',
                  ROOT/'Docs/TEXTURED_ASSASSIN_SOURCE.json',ROOT/'Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn'])
    files.extend((ROOT/'SourceAssets/characters/hooded_assassin/working_textured_model_assassin').rglob('*'))
    manifest=[]
    for path in files:
        if not path.is_file():continue
        relative=path.relative_to(ROOT)
        target=BACKUP/relative
        target.parent.mkdir(parents=True,exist_ok=True)
        if target.exists():
            assert sha(target)==sha(path), f'Refusing to overwrite baseline: {target}'
        else:shutil.copy2(path,target)
        assert sha(path)==sha(target)
        manifest.append({'path':relative.as_posix(),'sha256':sha(path),'bytes':path.stat().st_size})
    BACKUP.mkdir(parents=True,exist_ok=True)
    (BACKUP/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    jobs=[]
    for name,reference in REFERENCES.items():
        jobs.append({'character':name,'status':'PROPOSAL_ONLY_NOT_SUBMITTED',
                     'approval_required':True,'estimated_credits':10,
                     'recommendation':'Skip new paid pass; validate existing PBR asset' if name=='AssassinTextured' else 'Process only after Assassin pipeline sign-off; one job at a time',
                     'local_model':f'Assets/Characters/MobaBeta/{name}.glb',
                     'local_reference':f'SourceAssets/characters/Concept_Art/{reference}',
                     'endpoint':'POST https://api.meshy.ai/openapi/v1/retexture',
                     'request_template':{'model_url':'<original-preserving working GLB as data URI>',
                                         'image_style_url':'<clean character reference as data URI>',
                                         'ai_model':'meshy-7','enable_original_uv':True,
                                         'enable_pbr':True,'texture_resolution':'2k','target_formats':['glb']},
                     'acceptance':'Compare returned topology/UVs with input. Apply textures only to original rigged runtime asset; never substitute returned mesh or skin.'})
    (OUT/'meshy_job_proposals.json').write_text(json.dumps(jobs,indent=2)+'\n')
    print(f'{len(manifest)} backup files checksum verified. Six proposal records written; zero API requests.')

if __name__=='__main__':main()
