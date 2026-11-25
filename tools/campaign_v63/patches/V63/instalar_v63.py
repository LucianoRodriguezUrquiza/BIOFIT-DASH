#!/usr/bin/env python3
"""DIPL V6.3: local V6.2 map + UI, connected outlines, completion."""
import argparse, copy, hashlib, json, os, sys, tempfile, shutil
from pathlib import Path
from datetime import datetime
BASE='e31ed489afde8fdcd51dc963d947e6cd0cc702bbb83906175a20df7ecd2b990a'
SOURCE='baac8024b41f90e5d08e519f4b51b423e2038853aecb3ce59c6e428d1b49af12'
MAP=Path('Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5/1/Active.json')
CS=Path('Assets/BIOFIT/GeomeTry'); RES=Path('Assets/BIOFIT/Resources/BiofitV63')
HERE=Path(__file__).resolve().parent
MARK='BIOFIT V6.3'
# (first center in GD units, count). Read against portal/grid on supplied PNGs.
BALL_TOP=[(8205,8),(9255,4),(9825,12),(10605,4),(10965,4),(11505,4),(11835,8)]
BALL_BOTTOM=[(8595,4),(8865,8),(9525,4),(9945,4),(10485,12),(11175,8),(11685,4)]
SPIDER_TOP=[(23025,3),(23595,12),(24195,16),(25275,3),(25605,5),(25965,4)]
SPIDER_BOTTOM=[(23295,4),(23715,4),(24015,3),(24255,11),(24765,4),(25065,4),(25425,3)]
MIDDLE=[(9885,8,170),(10545,8,160),(23655,8,170),(24195,14,170),(24765,4,170)]
COLS=[13785,13905,14025,14145,14265,14385,14505]
def encode(d):return json.dumps(d,ensure_ascii=False,separators=(',',':')).encode('utf-8')
def sha(b):return hashlib.sha256(b).hexdigest()
def req(ok,msg):
 if not ok:raise ValueError(msg)
def read(p):return p.read_text(encoding='utf-8-sig')
def one(s,a,b):
 req(s.count(a)==1,'Firma no reconocida: '+a[:100]);return s.replace(a,b,1)
def transform(d):
 req(sha(encode(d))==BASE,'Active.json no corresponde exactamente a V6.2; no se cambia nada.')
 old=d['objects'];out=copy.deepcopy(old);idx=max(o['index'] for o in out)+1
 templates={i:next(o for o in old if o['id']==i) for i in (1,5,6,7,8,39,40)}
 def add(i,x,y,rot=0):
  nonlocal idx
  o=copy.deepcopy(templates[i]);o.update(index=idx,xGD=float(x),yGD=float(y),rotationGD=float(rot),flipX=False,flipY=False,raw={'1':str(i),'2':str(x),'3':str(y),'6':str(rot)});idx+=1;out.append(o);return o
 def move(o,y):o['yGD']=float(y);o['raw']['3']=str(y)
 # Remove visually misleading old decorative protrusions above reduced platforms at 49%.
 out=[o for o in out if not(o['id']==5 and o['xGD'] in (13335,13455) and o['yGD']>=45 and o['yGD']<=105)]
 # Seven ascending pillars; left extension avoids overlap with next plank. Break after 3 jumps.
 # Lower pillars 4..7 by one block, and use a short two-cell resting bridge at the third top.
 for k,x in enumerate(COLS):
  col=[o for o in out if o['xGD']==x and o['kind']=='solid' and o['id'] in (6,7)]
  req(len(col)==k+3,'Columna original inesperada: '+str(x))
  if k>=3:
   low=min(col,key=lambda o:o['yGD']);out.remove(low);col.remove(low)
   for o in col:move(o,o['yGD']-30)
   for o in out:
    if o['xGD']==x and o['id']==16:move(o,o['yGD']-30)
  for o in col:add(o['id'],x-30,o['yGD'],o['rotationGD'])
 # Third/fourth top = 150; full-width rest planks (same top, within .25 GD).
 for x in (14055,14085):add(40,x,143)
 # Final pillar top = 240, connect the short exit platform to existing descent.
 for o in out:
  if o['id']==40 and o['xGD']==14535 and o['yGD']==263:move(o,233)
 add(40,14565,233)
 # Replace only hazards and internal platforms, preserving corridor supports, colors and portals.
 def inside(o):return 7995<o['xGD']<12555 or 22935<o['xGD']<26565
 out=[o for o in out if not(inside(o) and (o['kind']=='hazard' or (o['id']==1 and 135<=o['yGD']<=205)))]
 for rows,upper,spider in [(BALL_TOP,True,False),(BALL_BOTTOM,False,False),(SPIDER_TOP,True,True),(SPIDER_BOTTOM,False,True)]:
  for x,n in rows:
   for j in range(n):
    xx=x+30*j;floor=120 if (not spider and xx<8865) or (spider and xx>=25815) else 90
    i=39 if spider and upper and x==23025 else 8
    h=templates[i]['heightGD'];add(i,xx,240-h/2 if upper else floor+h/2,180 if upper else 0)
 for x,n,y in MIDDLE:
  for j in range(n):add(1,x+j*30,y)
 # New Spider short island has four downward spikes, as in supplied final image.
 for j in range(4):add(8,24765+j*30,140,180)
 out.sort(key=lambda o:(o['xGD'],o['index']))
 req(len({o['index'] for o in out})==len(out),'Indices duplicados')
 req([o for o in old if o['kind']=='coin' or o['kind'].endswith('Portal')]==[o for o in out if o['kind']=='coin' or o['kind'].endswith('Portal')],'Portales/monedas cambiados')
 req(not any(o['id'] in (39,103) and 7995<o['xGD']<12555 for o in out),'Pinchos pequenos Ball')
 req(not any(o['kind']=='solid' and 7995<o['xGD']<12555 and o['yGD']>285 for o in out),'Techo inesperado')
 return dict(d,objects=out)

def patch_ux(s):
 req('BIOFIT_MENU_V6_RUNTIME_FIX' in s,'Falta menu V6.1 validado; no se sobrescribe.')
 s=one(s,'            if (image != null)\n            {\n                if (b.GetComponent<BiofitSettingsButtonFeedback>() == null)','            if (image != null)\n            {\n                b.transition = Selectable.Transition.None;\n                if (b.GetComponent<BiofitSettingsButtonFeedback>() == null)')
 s=one(s,'new Vector2(.44f,.085f),new Vector2(150,86)','new Vector2(.46f,.085f),new Vector2(112,88)')
 s=one(s,'new Vector2(.56f,.085f),new Vector2(150,86)','new Vector2(.54f,.085f),new Vector2(112,88)')
 s=one(s,'        private void RestartLevel()', (HERE/'final_fragment.txt').read_text(encoding='utf-8')+'\n        private void RestartLevel()')
 return s

def patch_feedback(s):
 return one(s,'color.a = Mathf.Lerp(color.a, held ? .16f : .001f, blend);','color.a = .001f; // V6.3: no rectangular overlay on transparent icon hitboxes.')

def patch_practice(s):
 start=s.index('            var raw=go.GetComponent<RawImage>();raw.raycastTarget=true;');end=s.index('            var button=go.AddComponent<BiofitStereoPracticeButton>();',start)
 return s[:start]+(HERE/'practice_fragment.txt').read_text(encoding='utf-8')+s[end:]

def patch_runtime(s):
 s=one(s,'            float maxGD=0f;','            var outlines = slot==1 ? new BiofitV63Outline(d, transform, floorY, unlit, GetSprite("square_05")) : null;\n            float maxGD=0f;')
 s=one(s,'                    if(o.id==9) sr.color=Color.black;','                    if(outlines!=null && outlines.Contains(o)) sr.sprite=outlines.FillSprite;\n                    if(o.id==9) sr.color=Color.black;')
 s=one(s,'            background.items.Sort((a,b)=>a.xGD.CompareTo(b.xGD));','            if(outlines!=null) outlines.Draw();\n            background.items.Sort((a,b)=>a.xGD.CompareTo(b.xGD));')
 s=one(s,'            Physics2D.SyncTransforms();\n            Debug.Log("BIOFIT V5:', '            BiofitV63Finish.Install(wall);\n            Physics2D.SyncTransforms();\n            Debug.Log("BIOFIT V6.3:')
 return s

def atomic(p,b):
 p.parent.mkdir(parents=True,exist_ok=True);fd,tmp=tempfile.mkstemp(dir=p.parent,prefix=p.name+'.',suffix='.tmp')
 try:
  with os.fdopen(fd,'wb') as f:f.write(b)
  os.replace(tmp,p)
 finally:
  if os.path.exists(tmp):os.unlink(tmp)

def plan(root):
 p=root/MAP;d=json.loads(p.read_bytes()); orig=json.loads((p.parent/'Original.json').read_bytes());req(orig['sourceSha256']==SOURCE and len(orig['objects'])==2291,'Original no reconocido');new=transform(d);req(new['endGD']==orig['endGD'],'Meta modificada')
 for f in {f for o in new['objects'] for f in o['frames']}|{'square_05'}:req((p.parents[2]/'Sprites'/(f+'.png')).is_file(),'Falta textura: '+f)
 ui=root/'Assets/BIOFIT/Resources/BiofitStereoV2/UI'
 for f in ['checkpoint','restart','menu']:req((ui/(f+'.png')).is_file(),'Falta arte UI: '+f)
 result={MAP:encode(new)}
 for n,func in [('BiofitGeometryUX.cs',patch_ux),('BiofitSettingsButtonFeedback.cs',patch_feedback),('BiofitStereoPracticeButton.cs',patch_practice),('BiofitStereoLocalRuntime.cs',patch_runtime)]:
  rel=CS/n;result[rel]=func(read(root/rel)).encode()
 for n in ['BiofitV63Outline.cs','BiofitV63Finish.cs']:
  rel=CS/n;req(not(root/rel).exists(),'Script V6.3 ya existente: '+n);result[rel]=(HERE/n).read_bytes()
 for n in ['checkpoint_frame.png','checkpoint_remove.png']:
  rel=RES/n;req(not(root/rel).exists(),'Recurso V6.3 ya existente: '+n);result[rel]=(HERE/n).read_bytes()
 return result,new

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--proyecto',type=Path,required=True);ap.add_argument('--verificar',action='store_true');ap.add_argument('--restaurar',action='store_true');a=ap.parse_args();root=a.proyecto.resolve();req((root/'ProjectSettings/ProjectVersion.txt').is_file(),'Proyecto Unity incorrecto');req(not(root/'Temp/UnityLockfile').exists(),'Cierra Unity antes de aplicar el parche')
 backup=root/'Builds/BiofitV63'
 if a.restaurar:
  manifest=json.loads((backup/'manifest.json').read_bytes())
  for name,record in manifest.items():
   p=root/name;req(p.is_file() and sha(p.read_bytes())==record['after'],'Archivo modificado luego del parche: '+name)
   if record['before'] is not None:req(sha((backup/name).read_bytes())==record['before'],'Respaldo invalido: '+name)
  if not a.verificar:
   for name,record in manifest.items():
    p=root/name
    if record['before'] is None:p.unlink();meta=Path(str(p)+'.meta');meta.unlink(missing_ok=True)
    else:atomic(p,(backup/name).read_bytes())
   shutil.rmtree(backup)
  print('OK RESTAURACION V6.2'+(' - sin escritura' if a.verificar else ''));return
 result,new=plan(root);print('V6.2: 2941 | V6.3:',len(new['objects']),'| monedas: 3');print('SHA256:',sha(encode(new)));print('Archivos previstos:',len(result))
 if a.verificar:print('OK PREVUELO V6.3 - sin escritura');return
 req(not backup.exists(),'Ya existe respaldo V6.3; revisar antes de aplicar')
 manifest={};previous={}
 for rel,b in result.items():
  p=root/rel;previous[rel]=p.read_bytes() if p.exists() else None;manifest[rel.as_posix()]={'before':None if previous[rel] is None else sha(previous[rel]),'after':sha(b)}
 try:
  backup.mkdir(parents=True)
  for rel,b in previous.items():
   if b is not None:atomic(backup/rel,b)
  atomic(backup/'manifest.json',encode(manifest))
  for rel,b in result.items():atomic(root/rel,b)
  for rel,b in result.items():req((root/rel).read_bytes()==b,'Fallo verificacion: '+str(rel))
 except BaseException:
  for rel,b in previous.items():
   if b is None:(root/rel).unlink(missing_ok=True)
   else:atomic(root/rel,b)
  raise
 print('OK V6.3 LOCAL - geometria, interfaz, contornos y meta; BLE, cooldown, firmware y menu V6.1 conservados.')
if __name__=='__main__':
 try:main()
 except Exception as e:print('DETENIDO:',e,file=sys.stderr);sys.exit(1)
