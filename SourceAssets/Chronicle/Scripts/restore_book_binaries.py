"""Restore the exact Chronicle FBX and Blender source from verified GitHub parts.

The download ZIP already has the full files. A Git clone stores the two largest
binaries as transport parts to fit the upload connection's request limit.
"""
import hashlib,json,os,tempfile
from pathlib import Path

def restore(root):
 root=Path(root).resolve()
 manifest=json.loads((root/'SourceAssets/Chronicle/Transport/BookBinaryManifest.json').read_text())
 allowed={'Assets/Resources/Chronicle/BookModel/Chronicle.fbx','SourceAssets/Chronicle/Gilded_Fate_Chronicle.blend'}
 results=[]
 for item in manifest['files']:
  if item['target'] not in allowed:raise ValueError('Unexpected target in book manifest')
  target=root/item['target'];target.parent.mkdir(parents=True,exist_ok=True)
  if target.exists():
   if hashlib.sha256(target.read_bytes()).hexdigest()!=item['sha256']:
    raise ValueError('Existing book file differs. Preserve it before replacing: '+str(target))
   results.append({'target':item['target'],'status':'already_verified'});continue
  temporary=target.with_suffix(target.suffix+'.restoring')
  digest=hashlib.sha256();size=0
  try:
   with temporary.open('wb') as output:
    for part in item['parts']:
     if not part['path'].startswith('SourceAssets/Chronicle/Transport/') or '..' in Path(part['path']).parts:raise ValueError('Unexpected part path')
     block=(root/part['path']).read_bytes()
     if len(block)!=part['size'] or hashlib.sha256(block).hexdigest()!=part['sha256']:raise ValueError('Book part checksum mismatch: '+part['path'])
     output.write(block);digest.update(block);size+=len(block)
   if size!=item['size'] or digest.hexdigest()!=item['sha256']:raise ValueError('Restored book checksum mismatch')
   os.replace(temporary,target)
  finally:
   if temporary.exists():temporary.unlink()
  results.append({'target':item['target'],'status':'restored_verified','size':size,'sha256':digest.hexdigest()})
 return results

if __name__=='__main__':
 import argparse
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[3])
 print(json.dumps(restore(parser.parse_args().repo),indent=2))
