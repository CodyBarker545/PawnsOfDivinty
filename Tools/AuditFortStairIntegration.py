"""Check this visual correction without redefining historical stage audits."""
from pathlib import Path
import json,hashlib,re,struct
R=Path(__file__).resolve().parents[1];S=R/'SourceAssets/Environment/FortStairIntegration';B=S/'Baseline'
allowed={'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn','Assets/Levels/Environment/EasternStair/EasternStair.tscn'}
hashes=json.loads((B/'ProtectedHashes.json').read_text());changed=[p for p,h in hashes.items() if hashlib.sha256((R/p).read_bytes()).hexdigest()!=h];assert set(changed)==allowed,changed
def nodes(t):return {c.split('\n')[0]:c.strip() for c in re.split(r'(?=\[node )',t)[1:]}
old=nodes((B/'CliffsideArena.tscn').read_text());new=nodes((R/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn').read_text())
assert all(k in new and new[k]==v for k,v in old.items())
added=set(new)-set(old);assert len(added)==1 and 'name="FortStairIntegration"' in next(iter(added))
p=R/'Assets/Levels/Environment/FortStairIntegration/FortStairIntegration.glb';raw=p.read_bytes();assert struct.unpack_from('<I',raw,8)[0]==len(raw);n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);assert not doc.get('images') and len(doc['meshes'])==1
layout=R/'artifacts/fort_stair_integration/layout.json';digest=hashlib.sha256(layout.read_bytes()).hexdigest() if layout.exists() else None
if digest:assert digest=='d86240af4ac21bce33222f929b1ef56351ea23811fb63dd49bfec845c7b83f86'
report={'unchanged_protected_files':len(hashes)-len(changed),'allowed_changed_files':changed,'original_battlefield_nodes_unchanged':len(old),'added_nodes':list(added),'layout_sha256':digest,'glb_bytes':len(raw),'embedded_images':0}
(S/'Validation.json').write_text(json.dumps(report,indent=2));print('FORT STAIR SCOPE PASSED:',report)
