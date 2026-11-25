#!/usr/bin/env python3
"""Publica la copia LOCAL aprobada V6.3, no reinstala ni modifica el juego."""
import argparse, hashlib, importlib.util, json, shutil, subprocess, sys
from pathlib import Path
HERE=Path(__file__).resolve().parent
PAY=HERE/'payload'; BASE='ab1d29b848fd32d393b5aad26a1072c2e5e6183f'
FINAL='e64c3a5a6ef2e7e3234468189bfe38c944b36984b5c6c3818d029f411fff839b'
BR='niveles-mejorados'; REPO='LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY'
G='Assets/BIOFIT/GeomeTry/'; R='Assets/BIOFIT/Resources/'
CODE=['BiofitGeometryUX.cs','BiofitSettingsButtonFeedback.cs','BiofitStereoPracticeButton.cs','BiofitStereoLocalRuntime.cs','BiofitV63Outline.cs','BiofitV63Finish.cs']
LEGACY=['BiofitMenuPressFeedback.cs','Editor/BiofitMenuV6Installer.cs']
SETTINGS=['Packages/packages-lock.json','ProjectSettings/EditorBuildSettings.asset','ProjectSettings/GraphicsSettings.asset','ProjectSettings/ProjectSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ShaderGraphSettings.asset','ProjectSettings/URPProjectSettings.asset']
MAPS=[(3015,2291),(1734,1545),(2111,1531),(1871,1704),(3351,2813)]
MENU=R+'BiofitUiV6/'; CP=R+'BiofitV63/'; D=R+'BiofitStereoLocal/CampaignV5/'

def req(ok,msg):
 if not ok:raise RuntimeError(msg)
def sha(b):return hashlib.sha256(b).hexdigest()
def norm(s):return s.lstrip('\ufeff').replace('\r\n','\n')
def read(p):return norm(p.read_text(encoding='utf-8-sig'))
def compact(d):return json.dumps(d,ensure_ascii=False,separators=(',',':')).encode()
def git(r,*a,check=True):
 p=subprocess.run(['git','-C',str(r),*a],capture_output=True,encoding='utf-8',errors='replace')
 if check and p.returncode:raise RuntimeError('git '+' '.join(a)+'\n'+p.stdout+p.stderr)
 return p.stdout.strip() if check else p
def anc(r,a,b):return git(r,'merge-base','--is-ancestor',a,b,check=False).returncode==0
def load(p,name):
 spec=importlib.util.spec_from_file_location(name,p);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
def jsonread(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def remote(r):
 return {line.split('\t')[1]:line.split('\t')[0] for line in git(r,'ls-remote','--heads','origin').splitlines()}
def filehash(p):return sha(p.read_bytes())
def preflight(r):
 req(PAY.is_dir(),'Ejecutar desde carpeta del ZIP de consolidacion.')
 req((r/'ProjectSettings/ProjectVersion.txt').is_file() and (r/'.git').exists(),'Proyecto Unity/Git incorrecto')
 req(not(r/'Temp/UnityLockfile').exists(),'Cierra Unity antes de consolidar')
 url=git(r,'remote','get-url','origin').rstrip('/').removesuffix('.git');req(url.lower().endswith(('/'+REPO).lower()) or url.lower().endswith((':'+REPO).lower()),'Origin inesperado: '+url)
 req(git(r,'branch','--show-current')==BR,'Rama local distinta de niveles-mejorados; no se cambia automaticamente')
 req(not git(r,'diff','--cached','--name-only'),'Hay cambios staged previamente; revisar')
 req(not git(r,'diff','HEAD','--','.gitignore'),'Reglas de exclusion modificadas; revisar')
 req(anc(r,BASE,'HEAD'),'HEAD no desciende de V5 consolidada')
 # Existing Git refs only; never reset or overwrite user changes.
 refs=remote(r);req('refs/heads/main' in refs and 'refs/heads/'+BR in refs,'Faltan ramas esperadas')
 git(r,'fetch','origin','main',BR)
 rm=refs['refs/heads/main']; rb=refs['refs/heads/'+BR]
 req(anc(r,rm,rb) and anc(r,'HEAD',rb),'Main o rama experimental tienen historia divergente. No se fuerza.')
 req(remote(r)==refs,'Las referencias cambiaron; repetir comprobacion')
 # No code regeneration: compare exact local sources with published V5 + historical transformations.
 pp=PAY/'tools/campaign_v63/patches';v=load(pp/'V63/instalar_v63.py','v63');m=load(pp/'MenuV61/corregir_menu_v6.py','menu61')
 old=lambda n:norm(git(r,'show',BASE+':'+G+n))+'\n'
 expected={'BiofitGeometryUX.cs':v.patch_ux(m.transform(old('BiofitGeometryUX.cs'))),'BiofitSettingsButtonFeedback.cs':v.patch_feedback(old('BiofitSettingsButtonFeedback.cs')),'BiofitStereoPracticeButton.cs':v.patch_practice(old('BiofitStereoPracticeButton.cs')),'BiofitStereoLocalRuntime.cs':v.patch_runtime(old('BiofitStereoLocalRuntime.cs'))}
 for n in CODE:
  p=r/(G+n);req(p.is_file(),'Falta '+n)
  target=expected[n] if n in expected else read(pp/'V63'/n)
  req(read(p).strip()==target.strip(),'Codigo local diferente de V6.3: '+n+'; revisar antes de publicar')
 allowed=[]
 def add(rel,required=True):
  p=r/rel
  if required:req(p.is_file(),'Falta '+rel)
  if p.is_file():allowed.append(rel)
 for n in CODE:
  add(G+n);add(G+n+'.meta')
 for n in LEGACY:
  if (r/(G+n)).is_file():
   req('Biofit.Game.' not in read(r/(G+n)) and 'BiofitMenuV6.' in read(r/(G+n)),'Herramienta historica con namespace incorrecto: '+n)
   add(G+n);add(G+n+'.meta')
 for n,h in jsonread(PAY/'tools/campaign_v63/menu_sha256.json').items():
  req((r/(MENU+n)).is_file() and filehash(r/(MENU+n))==h,'PNG menu diferente: '+n)
  add(MENU+n);add(MENU+n+'.meta')
 for n in ['checkpoint_frame.png','checkpoint_remove.png']:
  req((r/(CP+n)).is_file() and filehash(r/(CP+n))==filehash(pp/'V63'/n),'Grafico checkpoint diferente: '+n)
  add(CP+n);add(CP+n+'.meta')
 for n in [MENU.rstrip('/')+'.meta',CP.rstrip('/')+'.meta',MENU+'LEEME.txt',MENU+'LEEME.txt.meta']:add(n,False)
 for n in SETTINGS:add(n,False)
 manifest={'version':'V6.3','validation':'Aceptacion del usuario; no certificacion integral','files':{},'maps':[]}
 for i,(num,ori) in enumerate(MAPS,1):
  q=r/(D+str(i));a=jsonread(q/'Active.json');o=jsonread(q/'Original.json');req(len(a['objects'])==num and len(o['objects'])==ori,'Mapa diferente: '+str(i));req(sum(x['kind']=='coin' for x in a['objects'])==3,'Monedas incorrectas: '+str(i));h=sha(compact(a))
  if i==1:req(h==FINAL,'Stereo Madness no coincide exactamente con V6.3')
  req((q/'Level.json').is_file(),'Falta Level.json '+str(i))
  manifest['maps'].append({'slot':i,'active':num,'original':ori,'active_sha256_compact':h,'original_sha256_compact':sha(compact(o)),'level_sha256':filehash(q/'Level.json')})
  for f in {f for x in a['objects'] for f in x['frames']}:req((q.parents[1]/'Sprites'/(f+'.png')).is_file(),'Falta sprite '+f)
 tracked=git(r,'diff','--name-only','HEAD').splitlines();unexpected=set(tracked)-set(allowed)
 req(not unexpected,'Cambios versionados fuera de la consolidacion:\n'+'\n'.join(sorted(unexpected)))
 # Protect resource maps and existing rules. Never git add . or -f.
 for n in allowed:req(not git(r,'check-ignore','--',n,check=False).returncode==0,'Ruta ignorada: '+n)
 manifest['files']={n:filehash(r/n) for n in sorted(allowed)}
 print('OK V6.3 LOCAL: 3015 + 9067 = 12082 registros, 15 monedas; fuentes y PNG verificados.')
 print('Main y rama experimental compatibles; .gitignore intacto. No se modifica el juego.')
 return refs,allowed,manifest

def prepare(r):
 paths=[]
 for n in ['CONTEXTO_ACTUAL.md','CONTEXTO_VIVO.md']:
  src=r/'docs'/n;dst=r/'docs/history'/(src.stem+'_V5.md');req(src.is_file(),'Falta contexto anterior');req(not dst.exists(),'Ya existe archivo historico: '+str(dst));dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst);paths.append(dst.relative_to(r).as_posix())
 for p in sorted(PAY.rglob('*')):
  if not p.is_file() or '__pycache__' in p.parts:continue
  rel=p.relative_to(PAY);dst=r/rel
  if dst.exists() and rel.as_posix() not in ['docs/CONTEXTO_ACTUAL.md','docs/CONTEXTO_VIVO.md']:
   req(dst.read_bytes()==p.read_bytes(),'Archivo de documentacion ya existe con contenido distinto: '+str(rel))
  dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dst);paths.append(rel.as_posix())
 rd=r/'README.md';old=read(rd) if rd.exists() else '';rd.write_text('# BIOFIT DASH — V6.3 Campaña Clásica / niveles mejorados\n\nVersión estable de trabajo aprobada por el usuario el 24/11/2026. Leer [contexto actual](docs/CONTEXTO_ACTUAL.md), [relevo](docs/CONTEXTO_VIVO.md) y [referencias visuales](docs/referencias/V63/README.md). Los mapas originales y otros recursos de terceros se conservan localmente; consultar la reconstrucción documentada.\n\n---\n\n'+old,encoding='utf-8');paths.append('README.md')
 return paths

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--proyecto',type=Path,required=True);p.add_argument('--publicar',action='store_true');a=p.parse_args();r=a.proyecto.resolve();refs,allowed,manifest=preflight(r)
 if not a.publicar:print('OK PREVUELO CONSOLIDACION V6.3 - sin escritura de contenido');return
 # Fast-forward remote documentation only, preserving existing local modifications.
 git(r,'merge','--ff-only',refs['refs/heads/'+BR]);req(remote(r)==refs,'Ramas cambiaron durante preparacion')
 paths=prepare(r);q=r/'tools/campaign_v63/MANIFIESTO_LOCAL_V63.json';q.write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf-8');paths.append(q.relative_to(r).as_posix())
 allp=sorted(set(allowed+paths));git(r,'add','--',*allp)
 staged=set(git(r,'diff','--cached','--name-only').splitlines());req(staged and staged<=set(allp),'Seleccion staged inesperada')
 req(not any(x=='.gitignore' or x.startswith(R+'BiofitStereoLocal/') or x.startswith(R+'BiofitStereoV2/') or x.startswith(R+'Songs/') for x in staged),'Recurso local original staged inesperadamente')
 print('Archivos a versionar:',len(staged));git(r,'commit','-m','BIOFIT DASH V6.3: version estable de trabajo, menu y niveles mejorados')
 head=git(r,'rev-parse','HEAD');req(remote(r)==refs,'Ramas remotas cambiaron; commit local preservado, no publicar')
 # Single atomic normal fast-forward push to both refs; no force or branch deletion.
 git(r,'push','--atomic','origin','HEAD:refs/heads/main','HEAD:refs/heads/'+BR)
 rr=remote(r);req(rr.get('refs/heads/main')==head and rr.get('refs/heads/'+BR)==head,'Publicacion no verificada')
 print('OK V6.3 MAIN PUBLICADA:',head);print('OK niveles-mejorados y main apuntan al mismo commit. No se eliminaron ramas.')
 # Update local main without force, then switch to the identical committed tree.
 try:
  git(r,'fetch','origin','refs/heads/main:refs/heads/main')
  git(r,'switch','main')
  git(r,'branch','--set-upstream-to=origin/main','main')
  print('OK rama local: main')
 except Exception as e:print('AVISO: publicacion correcta; no se pudo cambiar rama local:',e)
 print('Menu, mapas locales y reglas de exclusion intactos. No hace falta reinstalar nada.')
if __name__=='__main__':
 try:main()
 except Exception as e:print('DETENIDO:',e,file=sys.stderr);sys.exit(1)
