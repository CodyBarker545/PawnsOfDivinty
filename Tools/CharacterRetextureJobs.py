"""Run via Blender MCP. Key stays in Blender memory; bounded paid submissions.

Call start_retexture(name) once, then inspect the local journal. Each worker
polls the existing job and downloads outputs, never resubmits or retries a POST.
"""
import base64
import json
import threading
import time
import urllib.request
import urllib.error
from pathlib import Path

ROOT=Path('D:/SoftwareEng/Game')
STAGE=ROOT/'artifacts/character_texturing'
ORDER=['GentleGuardian','TimeWeaver','Nigel','Colossus','SpiritGuardian']

def _write(path,data):
    temp=path.with_suffix('.tmp')
    temp.write_text(json.dumps(data,indent=2)+'\n')
    temp.replace(path)

def start_retexture(name):
    import bpy
    assert name in ORDER
    folder=STAGE/'jobs'/name
    folder.mkdir(parents=True,exist_ok=True)
    journal=folder/'job.json'
    assert not journal.exists(), 'Job journal exists: resume existing job, never resubmit blindly'
    previous=list((STAGE/'jobs').glob('*/job.json'))
    assert len(previous)<5, '50 credit cap reached'
    assert all(json.loads(p.read_text()).get('status')=='DOWNLOADED' for p in previous), 'Another job is not finished'
    key=bpy.context.preferences.addons['blender_mcp'].preferences.meshy_api_key.strip()
    assert key
    model=ROOT/f'Assets/Characters/MobaBeta/{name}.glb'
    ref=STAGE/f'references/{name}_front.png'
    assert model.stat().st_size<100*1024*1024 and ref.exists()
    record={'character':name,'estimated_credits':10,'status':'SUBMISSION_RESERVED','approved_cap':50}
    _write(journal,record)
    def worker():
        try:
            payload={'model_url':'data:application/octet-stream;base64,'+base64.b64encode(model.read_bytes()).decode(),
                     'image_style_url':'data:image/png;base64,'+base64.b64encode(ref.read_bytes()).decode(),
                     'ai_model':'meshy-7','enable_original_uv':True,'enable_pbr':True,
                     'texture_resolution':'2k','target_formats':['glb']}
            req=urllib.request.Request('https://api.meshy.ai/openapi/v1/retexture',
                 data=json.dumps(payload).encode(),headers={'Authorization':'Bearer '+key,'Content-Type':'application/json'})
            with urllib.request.urlopen(req,timeout=120) as response: result=json.load(response)
            record.update(task_id=result['result'],status='SUBMITTED');_write(journal,record)
            for attempt in range(240):
                req=urllib.request.Request('https://api.meshy.ai/openapi/v1/retexture/'+record['task_id'],headers={'Authorization':'Bearer '+key})
                with urllib.request.urlopen(req,timeout=30) as response:data=json.load(response)
                record.update(status=data['status'],progress=data.get('progress'));_write(journal,record)
                if data['status']=='SUCCEEDED':
                    _write(folder/'response.json',data)
                    downloads={'returned.glb':data['model_urls']['glb']}
                    for i,maps in enumerate(data.get('texture_urls',[])):
                        for kind,url in maps.items():
                            if isinstance(url,str) and url.startswith('https://'):downloads[f'{i}_{kind}.png']=url
                    if data.get('thumbnail_url'):downloads['thumbnail.png']=data['thumbnail_url']
                    for filename,url in downloads.items():
                        with urllib.request.urlopen(url,timeout=120) as response:(folder/filename).write_bytes(response.read())
                    record.update(status='DOWNLOADED',files=list(downloads));_write(journal,record)
                    return
                if data['status'] in ('FAILED','CANCELED'):
                    _write(folder/'response.json',data);return
                time.sleep(10)
            record.update(status='POLL_TIMEOUT');_write(journal,record)
        except urllib.error.HTTPError as error:
            record.update(status='HTTP_ERROR',http_status=error.code,detail=error.read().decode()[:1500]);_write(journal,record)
        except Exception as error:
            record.update(status='ERROR',error_type=type(error).__name__,detail=str(error)[:300]);_write(journal,record)
    thread=threading.Thread(target=worker,daemon=True,name='POD_Retexture_'+name)
    thread.start()
    print('Started one approved 10-credit retexture request:',name,'journal',str(journal))
