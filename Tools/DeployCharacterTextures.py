"""Promote a Blender-validated, original-mesh textured GLB without changing scene refs."""
import argparse
import json
import shutil
from pathlib import Path
from AuditCharacterTexturing import ROOT,inspect

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('name');name=parser.parse_args().name
    stage=ROOT/'artifacts/character_texturing'
    folder=ROOT/f'SourceAssets/characters/textured_roster/{name}'
    validation=json.loads((folder/'blender_validation.json').read_text())
    assert validation['fingerprint_before']==validation['fingerprint_after']
    src=folder/f'{name}.glb';runtime=ROOT/f'Assets/Characters/MobaBeta/{name}.glb'
    before=inspect(runtime);after=inspect(src)
    assert before['protected_sha256']==after['protected_sha256']
    assert before['animation_library_sha256']==after['animation_library_sha256']
    review=ROOT/f'artifacts/animations/{name}.glb'
    backup=stage/f'backups/audit_baseline_001/artifacts/animations/{name}.glb'
    backup.parent.mkdir(parents=True,exist_ok=True)
    if not backup.exists():shutil.copy2(review,backup)
    gallery=stage/'before';gallery.mkdir(exist_ok=True)
    for file in (ROOT/'artifacts').glob('animation_*.png'):
        if not (gallery/file.name).exists():shutil.copy2(file,gallery/file.name)
    shutil.copy2(src,runtime)
    shutil.copy2(stage/f'validated_reviews/{name}.glb',review)
    print(name+': promoted material-only GLB and review; geometry/UV/skin/library fingerprints unchanged')
