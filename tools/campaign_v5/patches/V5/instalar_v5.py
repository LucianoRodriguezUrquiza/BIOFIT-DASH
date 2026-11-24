#!/usr/bin/env python3
"""Parche acumulativo DIPL V3/V4 -> V5. Solo fuentes locales; no red."""
import argparse, collections, copy, hashlib, io, json, math, plistlib, re, subprocess, sys
from pathlib import Path
from PIL import Image
from util_v5 import decode, read, sha, one, apply, patch_modes, donor_pattern, BALL, SPIDER, DONOR_HASHES, V3_HASHES
from catalogo_v5 import CAT, pairs
import base64,gzip
BASE=Path(__file__).resolve().parent
HEAD='73cd148b9f3747b736eb4972c88ddee5a4fbe517'
LEVELS=[(1,'Stereo Madness','StereoMadness',2291,[(7995,12555,'ball',120,90),(22935,26565,'spider',90,90)]),(2,'Back on Track','BackOnTrack',1545,[(12645,16785,'ball',120,90)]),(3,'Polargeist','Polargeist',1531,[(9645,13305,'spider',110,90)]),(6,"Can't Let Go",'CantLetGo',1704,[(14409,17721,'ball',60,60)]),(7,'Jumper','Jumper',2813,[(6741,10103,'ball',60,90),(16815,20167,'spider',90,90)])]
CAT.update({10:('portal_01_front','gravityDown',34.5,77,5,10),11:('portal_02_front','gravityUp',34.5,77,5,10),24:('edit_eeFTBtn','enterEffect',16.25,28,5,2),26:('edit_eeFRBtn','enterEffect',29.25,16.5,5,2),32:('edit_eGhostEBtn','enterEffect',32.25,24.5,5,2),33:('edit_eGhostDBtn','enterEffect',35.25,24.5,5,2),35:('bump_01','pad',25,4,3,12),36:('ring_01','orb',30,30,3,12),67:('gravbump_01','gravityPad',25,6,3,12),68:('square_b_06','solid',30,16,5,2),73:('square_c_05','decoration',30,30,1,-7),110:('d_chain_02','decoration',19.5,34,3,9),140:('bump_03','pinkPad',25,5,3,12),47:('portal_07_front','ballPortal',44.5,85.5,5,10),1331:('portal_17_front','spiderPortal',44.5,84.5,5,10)})
HIDDEN={'enterEffect','backgroundColor','groundColor'}
def rgb(h,key):
 a=h[key].split('_');d=dict(zip(a[::2],a[1::2]));return [int(d[str(i)]) for i in (1,2,3)]
def item(i,p):
 ident=int(p['1']);f,k,w,h,l,z=CAT[ident]; frames=[] if k in HIDDEN else [f]
 if k.endswith('Portal') or k.startswith('gravity') and k!='gravityPad':frames=[f.replace('_front','_back'),f]
 o=dict(index=i,id=ident,kind=k,xGD=float(p['2']),yGD=float(p['3']),rotationGD=float(p.get('6',0)),flipX=p.get('4')=='1',flipY=p.get('5')=='1',widthGD=w,heightGD=h,layer=l,order=z,frames=frames,raw=p)
 if k in ('backgroundColor','groundColor'):o.update(r=int(p['7']),g=int(p['8']),b=int(p['9']),duration=float(p['10']))
 return o
def read_level(p,n):
 b=p.read_bytes().strip();s=gzip.decompress(base64.urlsafe_b64decode(b+b'='*((-len(b))%4))).decode();h,*rows=s.split(';');h=pairs(h);objs=[item(i,pairs(r)) for i,r in enumerate(r for r in rows if r)]
 if len(objs)!=n:raise ValueError('Conteo fuente inesperado: '+str(p))
 return dict(sourceSha256=sha(b),unitsPerBlock=30,header=h,objects=objs,initialBackground=rgb(h,'kS29'),initialGround=rgb(h,'kS30'),endGD=max(o['xGD'] for o in objs)+120)
def adapt(src,spans):
 orig=src['objects'];out=[];index=len(orig);report=[]
 def add(ident,x,y,rot=0,**kw):
  nonlocal index
  a=item(index,{'1':str(ident),'2':str(x),'3':str(y),'6':str(rot)});index+=1;a.update(kw);out.append(a);return a
 # Retain exact source records outside replaced intervals, including color events throughout.
 for o in orig:
  inside=any(a<o['xGD']<b for a,b,*_ in spans)
  if o['kind']=='shipPortal':continue
  if inside and o['kind'] not in HIDDEN:continue
  out.append(copy.deepcopy(o))
 for a,b,mode,entry,exit in spans:
  ident=47 if mode=='ball' else 1331
  portal=next(o for o in orig if o['kind']=='shipPortal' and o['xGD']==a)
  # The portal at the original position, with a tall local trigger limited to this corridor.
  ceiling=max(entry,exit)+120
  add(ident,a,portal['yGD'],portalMinGD=min(entry,exit),portalMaxGD=ceiling)
  length=(b-a)/30
  # Even number of alternating hazards -> final support on floor, for safe Cube return.
  count=6 if length>=145 else 4
  patterns=BALL if mode=='ball' else SPIDER
  offsets=[20+(length-40)*(i/(count-1)) for i in range(count)]
  # Make transition in floor height while player is on upper surface after first lower obstacle.
  switch=a+(offsets[0]+9)*30
  for x in range(math.floor(a)+30,math.ceil(b),30):
   floor=entry if x<switch else exit
   add(1,x,floor-15);add(1,x,ceiling+15)
   # Fill behind surface without extra collision.
   for y in range(15,int(floor-15),30):add(5,x,y)
   add(5,x,ceiling+45)
  actions=[]
  for i,off in enumerate(offsets):
   row=patterns[i%len(patterns)]; sid=row[0];num=min(row[4],4);upper=i%2==1;h=CAT[sid][3]
   start=a+round(off)*30;floor=entry if start<switch else exit
   for j in range(num):add(sid,start+j*30,ceiling-h/2 if upper else floor+h/2,180 if upper else 0)
   actions.append((start-a)/30-6)
  for coin in (o for o in orig if o['kind']=='coin' and a<o['xGD']<b):
   # Preserve X where possible; put coin on whichever surface is safe at that phase.
   phase=sum(coin['xGD']>=a+t*30 for t in actions)%2
   floor=entry if coin['xGD']<switch else exit
   y=ceiling-15 if phase else floor+15
   add(142,coin['xGD'],y)
  for p in out:
   if p['kind']=='cubePortal' and p['xGD']==b:
    p['portalMinGD']=min(entry,exit);p['portalMaxGD']=ceiling
  report.append(dict(mode=mode,start=a,end=b,nominal_action_spacing_s=min((v-u)/9.75 for u,v in zip(actions,actions[1:])),rows=count,entry_floor=entry,exit_floor=exit,ceiling=ceiling))
 out.sort(key=lambda o:(o['xGD'],o['index']))
 # Source index and all coordinates remain exact outside intervals; only return portal trigger metadata added.
 retained={o['index']:o for o in out if o['index']<len(orig)}
 for o in orig:
  if not any(a<o['xGD']<b for a,b,*_ in spans) and o['kind']!='shipPortal':
   q=retained[o['index']];assert all(q[k]==v for k,v in o.items())
 assert sum(o['kind']=='coin' for o in out)==3
 assert not any(o['kind']=='shipPortal' for o in out)
 assert len({o['index'] for o in out})==len(out)
 return dict(src,objects=out),report

def atlases(gd,names):
 found={}; images={}; result={}
 for p in sorted(gd.glob('GJ_GameSheet*-hd.plist')):
  for k,v in plistlib.loads(p.read_bytes())['frames'].items():found.setdefault(k,(p,v))
 nums=lambda s:list(map(int,re.findall(r'-?\d+',s)))
 for name in sorted(names):
  p,v=found[name+'_001.png']; im=images.get(p)
  if im is None:im=images[p]=Image.open(p.with_suffix('.png')).convert('RGBA')
  x,y,w,h=nums(v['textureRect']);r=v['textureRotated'];sw,sh=nums(v['spriteSourceSize']);ox,oy=nums(v['spriteOffset']);crop=im.crop((x,y,x+(h if r else w),y+(w if r else h)))
  if r:crop=crop.transpose(Image.Transpose.ROTATE_90)
  assert crop.size==(w,h);full=Image.new('RGBA',(sw,sh));full.paste(crop,(round((sw-w)/2+ox),round((sh-h)/2-oy)));b=io.BytesIO();full.save(b,format='PNG');result[name]=b.getvalue()
 return result

def js(o):return json.dumps(o,ensure_ascii=False,separators=(',',':')).encode()
def generate(gd,local,music):
 for n in ('9','21'):
  p=gd/'levels'/f'{n}.txt';assert sha(p.read_bytes().strip())==DONOR_HASHES[n]
 donor_pattern(BALL,decode(gd/'levels/9.txt'),9075,13485);donor_pattern(SPIDER,decode(gd/'levels/21.txt'),10695,13551)
 out={};report=[];frames=set()
 for slot,(n,name,song,count,spans) in enumerate(LEVELS,1):
  source=read_level(gd/'levels'/f'{n}.txt',count)
  if source['sourceSha256']!=SOURCE_HASHES[str(n)]:raise ValueError('Fuente distinta de la analizada: '+str(n))
  active,sections=adapt(source,spans);frames.update(f for o in active['objects'] for f in o['frames']);path=local/'CampaignV5'/str(slot)
  out[path/'Original.json']=js(source);out[path/'Active.json']=js(active)
  out[path/'Level.json']=js(dict(id=slot,name=name,difficulty=slot,music='BiofitLocalV5/'+song,map=[dict(type='biofitStereoLocalAnchor',x=math.ceil(source['endGD']/30),y=0)]))
  out[music/(song+'.mp3')]=(gd/(song+'.mp3')).read_bytes()
  report.append(dict(slot=slot,name=name,source=count,active=len(active['objects']),sha256=source['sourceSha256'],sections=sections,retained=sum(o['index']<count for o in active['objects']),ids=len({o['id'] for o in active['objects']})))
 for name,data in atlases(gd,frames).items():out[local/'Sprites'/(name+'.png')]=data
 for name,filename in [('fondo','game_bg_01_001-hd.png'),('suelo','groundSquare_01_001-hd.png')]:out[local/'Sprites'/(name+'.png')]=(gd/filename).read_bytes()
 return out,report

SOURCE_HASHES={'1': 'baac8024b41f90e5d08e519f4b51b423e2038853aecb3ce59c6e428d1b49af12', '2': '40a9b11d0ad1d2de9fb256f89b6f0f241ea4391662e61465805e3da63d3a8f77', '3': 'c85ade754fae33d3acf4b22853382bf42bef1c97be63076a804bf00cabdcc9f9', '6': 'aedd11106fda2b56294b2d802cd203ae6b82b71da40a54824d84ad2187a384a4', '7': 'e3dea95ce616f049353d4048f38feb743a7e4602091dc12b632c31937fb7784f'}

V4_HASHES={'BiofitStereoLocalRuntime.cs': '846bd5635c7778b6cd3ace621e231d64ca6727b3bf615ffe462b934a439e7294', 'BiofitStereoSolidContact.cs': '280119d8ef6a00ff2eec5caa2bd92a9463090a210f674f850907333eb33fcbc0', 'BiofitStereoPractice.cs': 'f58c3ea5036e87907520ccbed6c54b4a8ca3c6bd6ce5a2bdc858e2ee8c78ccf4'}

def patch_motor(s):
 if 'ActiveGroundMask' not in s:s=patch_modes(s) # V3 -> V4 motor only, no V4 data required
 s=one(s,'public enum BiofitMode { Cube, Ball, Spider, Ship }','public enum BiofitMode { Cube, Ball, Spider, Ship, InvertedCube }')
 s=one(s,'if (level.Id == 1)\n            {','if (BiofitStereoLocalRuntime.IsLocal(level)) yield break;\n            if (level.Id == 1)\n            {')
 s=one(s,'if (id == 1) BiofitStereoCoinPickup.ResetRun();\n            motor.SetMode(id == 2 ? BiofitMode.Ball : id == 3 ? BiofitMode.Spider : BiofitMode.Cube);','if (BiofitStereoLocalRuntime.IsLocal(GameManager.Instance?.SelectedLevel)) { BiofitStereoCoinPickup.ResetRun(); motor.SetMode(BiofitMode.Cube); }\n            else { if(id==1) BiofitStereoCoinPickup.ResetRun(); motor.SetMode(id == 2 ? BiofitMode.Ball : id == 3 ? BiofitMode.Spider : BiofitMode.Cube); }')
 s=one(s,'GameManager.Instance.SelectedLevel.Id != 1) return;','!BiofitStereoLocalRuntime.IsLocal(GameManager.Instance.SelectedLevel) && GameManager.Instance.SelectedLevel.Id != 1) return;')
 s=one(s,'public bool StereoInverted => flipped;','public bool StereoInverted => Mode==BiofitMode.InvertedCube || flipped;')
 s=one(s,'if (!BiofitStereoPractice.Available || !initialized || (Mode != BiofitMode.Ball && Mode != BiofitMode.Spider)) return;','if (!BiofitStereoPractice.Available || !initialized || (Mode != BiofitMode.Ball && Mode != BiofitMode.Spider)) return;')
 s=one(s,'if (Mode == BiofitMode.Ship) return; // Ship responds to held input in FixedUpdate.','if (Mode == BiofitMode.Ship) return; // Ship responds to held input in FixedUpdate.\n            if(Mode==BiofitMode.InvertedCube) { if(Input.GetKey(KeyCode.Space) || (Input.GetMouseButton(0) && !BiofitStereoPracticeButton.BlocksGameplayPointer()) || BiofitVrBoxInput.AnyHeld) TryPulse(); return; }')
 s=one(s,'            else\n            {\n                cube.enabled = false;\n                icon.sprite = BiofitThreeModeArt.Make(newMode, false);','''            else if(newMode==BiofitMode.InvertedCube)
            {
                cube.enabled=false; flipped=true; body.gravityScale=-BallSpiderGravity;
                icon.sprite=originalSprite; icon.color=originalTint; icon.flipY=true;
                icon.transform.localRotation=Quaternion.identity;
            }
            else
            {
                cube.enabled = false;
                icon.sprite = BiofitThreeModeArt.Make(newMode, false);''')
 s=one(s,'            if (Mode == BiofitMode.Ball)\n            {\n                icon.transform.Rotate','''            if(Mode==BiofitMode.InvertedCube)
            {
                if(!SurfaceContact(Vector2.up)) icon.transform.Rotate(0,0,280f*Time.deltaTime);
                else icon.transform.localRotation=Quaternion.Euler(0,0,Mathf.Round(icon.transform.localEulerAngles.z/90f)*90f);
            }
            if (Mode == BiofitMode.Ball)
            {
                icon.transform.Rotate''')
 s=one(s,'            Vector2 support = flipped ? Vector2.up : Vector2.down;','''            if(Mode==BiofitMode.InvertedCube)
            {
                foreach(var hit in Physics2D.OverlapCircleAll(body.position,.8f))
                {
                    var interaction=hit.GetComponent<BiofitV5Interaction>();
                    if(interaction!=null && interaction.TryInvertedOrb(cube)) { lastManualAction=Time.unscaledTime; return true; }
                }
                if(!SurfaceContact(Vector2.up)) return false;
                body.linearVelocity=new Vector2(body.linearVelocity.x,-13.5f);
                lastManualAction=Time.unscaledTime; return true;
            }
            Vector2 support = flipped ? Vector2.up : Vector2.down;''')
 # Cube->vehicle transitions keep vertical momentum (V4); inverted->Cube restore normal gravity.
 # Only for custom V5 import, allow the near-ground support ray to include tiles, already installed by V4.
 return s

def patch_ux(s):
 s=one(s,'"NIVEL 1", "NIVEL 2", "NIVEL 3", "NIVEL 4", "NIVEL 5"','"STEREO MADNESS", "BACK ON TRACK", "POLARGEIST", "CAN\'T LET GO", "JUMPER"')
 old='"TRES MODOS", "BOLA", "SPIDER", "TRES MODOS", "TRES MODOS"' if '"TRES MODOS", "BOLA"' in s else '"CUBO / NAVE", "BOLA", "SPIDER", "TRES MODOS", "TRES MODOS"'
 s=one(s,old,'"CUBO / BOLA / SPIDER", "CUBO / BOLA", "CUBO / SPIDER", "CUBO / BOLA", "CUBO / BOLA / SPIDER"')
 s=one(s,'BiofitMode mode = i == 1 ? BiofitMode.Ball :\n                    i == 2 ? BiofitMode.Spider : BiofitMode.Cube;','BiofitMode mode = BiofitMode.Cube;')
 s=one(s,'if (i >= 3)\n                {\n                    // Mixed levels show all three vehicle icons.','if (i >= 0)\n                {\n                    // All five levels begin with Cube and contain one or two other vehicles.')
 s=one(s,'foreach (BiofitMode other in new[] {BiofitMode.Ball, BiofitMode.Spider})','foreach (BiofitMode other in (i==0 || i==4) ? new[] {BiofitMode.Ball,BiofitMode.Spider} : new[] {i==2?BiofitMode.Spider:BiofitMode.Ball})')
 s=one(s,'new Vector2(other == BiofitMode.Ball ? 30f : 95f, 0f)','new Vector2((i!=0 && i!=4) ? 48f : other == BiofitMode.Ball ? 20f : 85f, 0f)')
 s=one(s,'Vector2.zero, 35, Navy);\n                Text("Modo "','Vector2.zero, 23, Navy);\n                Text("Modo "')
 s=one(s,'btn.transform, "≈ 60 s",','btn.transform, new[]{"~ 91 s","~ 86 s","~ 95 s","~ 85 s","~ 92 s"}[i],')
 s=one(s,'GameManager.Instance.SelectedLevel.Id==1)\n            {\n                hud.coins','BiofitStereoLocalRuntime.IsLocal(GameManager.Instance.SelectedLevel))\n            {\n                hud.coins')
 return s

def plan(root,gd):
 geome=root/'Assets/BIOFIT/GeomeTry'; local=root/'Assets/BIOFIT/Resources/BiofitStereoLocal';music=root/'Assets/BIOFIT/Resources/Songs/Musics/BiofitLocalV5'
 if (local/'CampaignV5').exists():raise ValueError('V5 ya instalada. No se sobreescribe.')
 d=json.loads(read(local/'Objects.json'));assert d['sourceSha256']==SOURCE_HASHES['1'] and len(d['objects'])==2291
 if not (geome/'BiofitStereoPracticeButton.cs').is_file():raise ValueError('Se requiere V3 aplicada y validada.')
 v4=(local/'HybridV4.json').exists()
 for name in V3_HASHES:
  p=geome/name
  h=sha(read(p).encode())
  if h!=(V4_HASHES[name] if v4 else V3_HASHES[name]):raise ValueError('Archivo base modificado: '+str(p))
 out,report=generate(gd,local,music)
 # Keep all already installed textures byte-for-byte; only add missing ones.
 for path in list(out):
  if path.parent==local/'Sprites' and path.exists():
   with Image.open(path) as prior, Image.open(io.BytesIO(out[path])) as next_image:
    if prior.size!=next_image.size:raise ValueError('Textura previa de dimensiones inesperadas: '+str(path))
   del out[path]
 for name in [*V3_HASHES,'BiofitV5Interaction.cs']:
  out[geome/name]=(BASE/name).read_bytes()
 modes=geome/'BiofitThreeModes.cs';ux=geome/'BiofitGeometryUX.cs';verify=geome/'Editor/BiofitThreeModesBuild.cs'
 out[modes]=patch_motor(read(modes)).encode();out[ux]=patch_ux(read(ux)).encode()
 out[verify]=one(read(verify),'if (i == 0 && BiofitStereoLocalRuntime.ValidateInstalled()) continue;','if (BiofitStereoLocalRuntime.ValidateInstalled(i)) continue;').encode()
 # Validate the source of the motion pipeline was not changed by this patch.
 forbidden=['BiofitGeomeTryLegacyBridge.cs','BiofitBilateralJumpDetector.cs','BiofitBLEMonitor.cs','Character.cs','Cube.cs']
 assert not any(p.name in forbidden for p in out)
 return out,report,'V4' if v4 else 'V3'

def install(root,out):
 # Existing V4 transaction, backup includes only modified/new paths. Exclude music too.
 exclude=Path(subprocess.check_output(['git','-C',str(root),'rev-parse','--absolute-git-dir'],text=True).strip())/'info/exclude'
 before=exclude.read_bytes() if exclude.exists() else b''
 exclusion=b'\n# BIOFIT V5 local proprietary references\n/Assets/BIOFIT/Resources/BiofitStereoLocal/\n/Assets/BIOFIT/Resources/BiofitStereoLocal.meta\n/Assets/BIOFIT/Resources/Songs/Musics/BiofitLocalV5/\n/Assets/BIOFIT/Resources/Songs/Musics/BiofitLocalV5.meta\n'
 exclude.parent.mkdir(parents=True,exist_ok=True);exclude.write_bytes(before+exclusion)
 try:backup=apply(root,out)
 except BaseException:
  exclude.write_bytes(before);raise
 new=backup.with_name(backup.name.replace('StereoV4_backup_','CampaignV5_backup_'));backup.rename(new)
 return new

def main():
 p=argparse.ArgumentParser();p.add_argument('--proyecto',type=Path,required=True);p.add_argument('--gd',type=Path,required=True);p.add_argument('--verificar',action='store_true');a=p.parse_args();root=a.proyecto.resolve();gd=a.gd.resolve()
 if subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()!=HEAD:raise ValueError('Commit base inesperado. No se modifico nada.')
 out,r,base=plan(root,gd);print(json.dumps(r,indent=2));print('Base: '+base+'; niveles: 5; archivos previstos: '+str(len(out)))
 if a.verificar:print('OK V5 PREVUELO - sin escritura');return
 backup=install(root,out);print('OK V5 LOCAL - 5 niveles, Cube/Ball/Spider; BLE intacto.');print('Backup: '+str(backup));print('No se hizo commit ni push. Abrir Unity y verificar.')
if __name__=='__main__':
 try:main()
 except Exception as e:print('ERROR V5: '+str(e),file=sys.stderr);sys.exit(1)
