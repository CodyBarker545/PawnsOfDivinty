"""Promote checked arm-only edits and synchronize existing deliverables."""
import hashlib
import json
import shutil
from pathlib import Path
from AuditCharacterTexturing import ROOT,MODELS
from VerifyCharacterArmRefinement import verify

OUT=ROOT/'artifacts/arm_pose_refinement'

if __name__=='__main__':
    for name in MODELS:verify(name)
    manifest_path=ROOT/'SourceAssets/characters/textured_roster/manifest.json'
    manifest=json.loads(manifest_path.read_text())
    for name in MODELS:
        staged=OUT/'staged'/name;delivery=ROOT/f'SourceAssets/characters/textured_roster/{name}'
        shutil.copy2(staged/f'{name}.tres',ROOT/f'Assets/Animations/Mixamo/{name}.tres')
        shutil.copy2(staged/f'{name}.tres',delivery/'Animations.tres')
        shutil.copy2(staged/f'{name}.glb',ROOT/f'artifacts/animations/{name}.glb')
        target=ROOT/f'artifacts/character_texturing/validated_reviews/{name}.glb'
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(staged/f'{name}.glb',target)
        shutil.copy2(staged/'refinement.json',delivery/'arm_refinement.json')
        for item in manifest:
            if Path(item['runtime']).stem==name:
                item['animation_library_sha256']=hashlib.sha256((staged/f'{name}.tres').read_bytes()).hexdigest()
    manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
    print('Deployed corrected arm tracks for six characters; runtime model files unchanged')
