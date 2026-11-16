"""Validate surgical scene scope, unchanged completed assets, and complete exports."""
from pathlib import Path
import hashlib,json,re,struct
R=Path(__file__).resolve().parents[1];S=R/'SourceAssets/Environment/EasternStair';A=R/'Assets/Levels/Environment/EasternStair'
def blocks(path):
 chunks=re.split(r'(?=\[node )',path.read_text());return {c.split('\n')[0]:c.strip() for c in chunks[1:]}
before=blocks(S/'Baseline/CliffsideArena.tscn');after=blocks(R/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn')
removed=set(before)-set(after);assert not removed,removed
changes=[]
for key,old in before.items():
 new=after[key]
 if old==new:continue
 name=re.search(r'name="([^"]+)"',key)[1];parent=re.search(r'parent="([^"]+)"',key)[1]
 if re.fullmatch(r'Terrain/tile_(24|25)_(15|16)',parent) and re.fullmatch(r'Tread[0-4]',name):assert new.replace('\nvisible = false','',1)==old
 elif parent=='GridOverlay' and name in ['Column_24_15','Column_25_15','Row_24_15']:
  expected=old.replace('-0.58700','-0.10700') if name=='Row_24_15' else old.replace('-0.58700, 22.87500','-0.10700, 22.37500').replace('0.01200, 1.25000','0.01200, 0.25000')
  assert new==expected
 else:raise AssertionError('Out-of-scope scene modification: '+key)
 changes.append(parent+'/'+name)
assert len(changes)==23,len(changes)
added=set(after)-set(before);assert len(added)==9,len(added)
assert all('name="EasternStairGrid_' in k or 'name="EasternStairEnvironment"' in k for k in added)
protected=json.loads((S/'Baseline/ProtectedHashes.json').read_text());bad=[p for p,h in protected.items() if hashlib.sha256((R/p).read_bytes()).hexdigest()!=h];assert not bad,bad
exports=[]
for p in A.glob('*.glb'):
 raw=p.read_bytes();magic,version,length=struct.unpack_from('<III',raw);assert magic==0x46546c67 and version==2 and length==len(raw)
 n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);assert not doc.get('images')
 exports.append({'file':p.relative_to(R).as_posix(),'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest(),'meshes':len(doc['meshes']),'embedded_images':0})
assert len(exports)==2
terrain_file=R/'artifacts/stage30d2b/layout.json';layout_hash=hashlib.sha256(terrain_file.read_bytes()).hexdigest() if terrain_file.exists() else None
if layout_hash:assert layout_hash=='d86240af4ac21bce33222f929b1ef56351ea23811fb63dd49bfec845c7b83f86'
report={'protected_file_count':len(protected),'protected_changed':bad,'modified_existing_nodes':changes,'added_nodes':sorted(added),'removed_nodes':[],'exports':exports,'layout_sha256':layout_hash}
(S/'Validation.json').write_text(json.dumps(report,indent=2));print('SCOPE AUDIT PASSED:',len(protected),'protected files unchanged; 20 tread visibility +3 eastern grid transforms only;',len(added),'new scene nodes; two complete image-free GLBs; layout',layout_hash)
