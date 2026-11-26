#!/usr/bin/env python3
"""DIPL V6.4: actualización incremental por Git sobre el proyecto existente."""
import argparse, hashlib, json, re, subprocess, sys
from pathlib import Path
BASE='597631a69eed15a064c96baa14759a06704d22ce'
BR='niveles-mejorados'
REPO='lucianorodriguezurquiza/biofit-game-unity'
MAPS=['e64c3a5a6ef2e7e3234468189bfe38c944b36984b5c6c3818d029f411fff839b','db175d2b95a020ffcca792ea2b6d0b79dd103af3b8ec8b980ca02e2a7f04da5c','6313a59c0eac4bfdc281ac210226aa079f2ed4fcf5ae6b11e6b7838c078d2a33','cc53076263427535a026c7e0d84c6eeb52e717a2627ef9017390afa548bf0d80','87f253f933656a0da853ccd1ddeb139c653e3ec4c5f75e034998e4f9f4b3c756']
G='Assets/BIOFIT/GeomeTry/'
CODE={'BiofitStereoLocalRuntime.cs','BiofitStereoSolidContact.cs','BiofitV63Outline.cs','BiofitGeometryUX.cs','BiofitV2Hud.cs','BiofitThreeModes.cs','BiofitEditedDeathZone.cs','BiofitEditedDeathZone.cs.meta','Editor/BiofitLevelEditor.cs','Editor/BiofitLevelEditor.cs.meta','Editor/BiofitEditorFlm.cs','Editor/BiofitEditorFlm.cs.meta'}
ALLOWED={G+x for x in CODE}|{'README.md','docs/CONTEXTO_ACTUAL.md','docs/CONTEXTO_VIVO.md','docs/LEVEL_EDITOR_V64.md','tools/level_editor/level_delta.py','tools/level_editor/instalar_v64.py','tools/level_editor/VALIDACION_V64.json','tools/level_editor/changes/INDEX.md'}
def req(ok,msg):
    if not ok: raise RuntimeError(msg)
def git(r,*args,check=True):
    p=subprocess.run(['git','-C',str(r),*args],capture_output=True,encoding='utf-8',errors='replace')
    if p.returncode and check: raise RuntimeError('git '+' '.join(args)+'\n'+p.stdout+p.stderr)
    return p.stdout.strip() if check else p
def check_url(r):
    u=git(r,'remote','get-url','origin').lower().removesuffix('.git').rstrip('/')
    req(u.endswith('/'+REPO) or u.endswith(':'+REPO),'Origin inesperado: '+u)
def maps(r):
    for slot,h in enumerate(MAPS,1):
        folder=r/'Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5'/str(slot)
        req((folder/'Original.json').is_file() and (folder/'Level.json').is_file(),'Faltan recursos locales ranura '+str(slot))
        d=json.loads((folder/'Active.json').read_bytes().decode('utf-8-sig'));actual=hashlib.sha256(json.dumps(d,ensure_ascii=False,separators=(',',':')).encode()).hexdigest();req(actual==h,'Mapa activo no es V6.3 ranura '+str(slot)+'; no se sobrescribe')
    print('OK mapas locales V6.3: 12082 registros y 15 monedas; sin escritura')
def preflight(r,target):
    req(re.fullmatch(r'[0-9a-f]{40}',target)!=None,'Commit objetivo inválido')
    req((r/'ProjectSettings/ProjectVersion.txt').is_file() and (r/'.git').exists(),'Ruta del proyecto Unity/Git incorrecta')
    req(not(r/'Temp/UnityLockfile').exists(),'Cerrá Unity antes de ejecutar')
    check_url(r);branch=git(r,'branch','--show-current');head=git(r,'rev-parse','HEAD');req(branch in ('main',BR) and head in (BASE,target),'Rama/HEAD local no coincide con V6.3 o el objetivo; no se fuerza')
    req(not git(r,'status','--porcelain','--untracked-files=no'),'Hay modificaciones versionadas locales. No se modifican. Mostrá git status.')
    maps(r);git(r,'fetch','origin','main',BR)
    req(git(r,'rev-parse','origin/main')==BASE,'main remota cambió; verificar contexto antes de seguir')
    req(git(r,'rev-parse','origin/'+BR)==target,'Rama experimental avanzó o commit incorrecto; no se instala una versión distinta')
    req(git(r,'merge-base','--is-ancestor',BASE,target,check=False).returncode==0,'El objetivo no desciende de V6.3')
    changed=set(git(r,'diff','--name-only',BASE,target).splitlines());req(changed<=ALLOWED,'Archivos fuera del alcance:\n'+'\n'.join(sorted(changed-ALLOWED)))
    req({G+x for x in CODE}<=changed,'Faltan archivos del editor en el commit')
    local=git(r,'show-ref','--verify','refs/heads/'+BR,check=False)
    if local.returncode==0: req(local.stdout.split()[0] in (BASE,target),'Rama experimental local divergente; no se fuerza')
    print('OK PREVUELO V6.4 |',len(changed),'archivos versionados | main V6.3 intacta')
    return local.returncode==0
def install(r,target,exists):
    if git(r,'branch','--show-current')!=BR:
        if exists: git(r,'switch',BR)
        else: git(r,'switch','-c',BR,BASE)
    git(r,'merge','--ff-only',target)
    req(git(r,'rev-parse','HEAD')==target,'HEAD final no coincide')
    maps(r);req(not git(r,'diff',BASE,target,'--','.gitignore'),'Exclusiones modificadas')
    req(git(r,'rev-parse','main')==BASE,'main local no coincide')
    print('OK V6.4 EXPERIMENTAL INSTALADA:',target,'| rama',BR,'| main no modificada')
def main():
    p=argparse.ArgumentParser();p.add_argument('--proyecto',required=True,type=Path);p.add_argument('--commit');p.add_argument('--verificar',action='store_true');p.add_argument('--instalar',action='store_true');p.add_argument('--restaurar',action='store_true');a=p.parse_args()
    req(sum((a.verificar,a.instalar,a.restaurar))<=1,'Una operación por vez')
    v=Path(__file__).with_name('VERSION.json');target=a.commit or (json.loads(v.read_text())['commit'] if v.exists() else None);req(target,'Falta --commit o VERSION.json del paquete')
    r=a.proyecto.resolve();exists=preflight(r,target)
    if a.instalar: install(r,target,exists)
    elif a.restaurar:
        req(git(r,'rev-parse','main')==BASE,'main local cambió');git(r,'switch','main');maps(r);print('OK vuelta a V6.3, rama experimental conservada')
    else: print('OK VERIFICACIÓN | no se modificaron los archivos del proyecto (solo se consultaron/actualizaron refs Git).')
if __name__=='__main__':
    try: main()
    except Exception as ex: print('DETENIDO:',ex,file=sys.stderr);sys.exit(1)
