"""Extract original concept views without repainting or changing original sheets."""
import json
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/character_texturing/references'
SPECS={
 'GentleGuardian':('The Gentle Guardian Character Concept Sheet.png',(0.46,0.025,0.66,0.39)),
 'TimeWeaver':('Time Weaver Mage Concept Sheet.png',(0.52,0.015,0.672,0.405)),
 'Nigel':('Nigel the Orc_ Warrior Concept Sheet.png',(0.628,0.665,0.808,0.993)),
 'Colossus':('Gravebound Colossus Concept Sheet.png',(0.413,0.003,0.58,0.411)),
 'SpiritGuardian':('Coastal Spirit Guardian Concept Sheet.png',(0.003,0.015,0.35,0.67)),
}

if __name__=='__main__':
 OUT.mkdir(parents=True,exist_ok=True)
 records=[]
 for name,(filename,box) in SPECS.items():
  path=ROOT/'SourceAssets/characters/Concept_Art'/filename
  with Image.open(path) as image:
   pixels=tuple(round(v*(image.width if i%2==0 else image.height)) for i,v in enumerate(box))
   crop=image.crop(pixels);crop.save(OUT/f'{name}_front.png')
   records.append({'character':name,'original':str(path),'box_pixels':pixels,'size':crop.size})
 (OUT/'manifest.json').write_text(json.dumps(records,indent=2))
 print('Extracted five concept views; originals unchanged')
