#!/usr/bin/env python3
"""DIPL: actualizar el proyecto existente V6.4 a V6.5 por fast-forward. No tocar mapas."""
import argparse, hashlib, json, subprocess, sys
from pathlib import Path
BASE="81e49ac4e32b2203ff2c789dde5194f64d25efb6"
BR="v6.5-desarrollo"
REPO="lucianorodriguezurquiza/biofit-game-unity"
def require(ok,msg):
    if not ok:raise RuntimeError(msg)
def git(root,*args,check=True):
    p=subprocess.run(["git","-C",str(root),*args],text=True,capture_output=True,encoding="utf-8",errors="replace")
    if not check:return p
    require(p.returncode==0,"git "+" ".join(args)+": "+p.stdout+p.stderr)
    return p.stdout.strip()
def fingerprint(root):
    base=root/"Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5"
    files=[]
    for i in range(1,6):
        for n in ("Active.json","Original.json","Level.json"):
            f=base/str(i)/n;require(f.is_file(),"Falta mapa local "+str(f));files.append(f)
    work=root/"Builds/BiofitLevelEditor"
    if work.exists():files.extend(f for f in work.rglob("*") if f.is_file() and f.name in ("Base.json","Draft.json","PreviousActive.json","Active.sha256"))
    return {f.relative_to(root).as_posix():hashlib.sha256(f.read_bytes()).hexdigest() for f in files}
def main():
    p=argparse.ArgumentParser();p.add_argument("--proyecto",required=True,type=Path);p.add_argument("--commit");p.add_argument("--verificar",action="store_true");p.add_argument("--instalar",action="store_true");a=p.parse_args()
    require(not(a.verificar and a.instalar),"Una operación por vez")
    root=a.proyecto.resolve();v=Path(__file__).with_name("VERSION.json")
    target=a.commit or (json.loads(v.read_text(encoding="utf-8"))["commit"] if v.exists() else None)
    require(target and len(target)==40 and all(c in "0123456789abcdef" for c in target),"Falta commit fijado de V6.5")
    require((root/".git").exists() and (root/"ProjectSettings/ProjectVersion.txt").is_file(),"Ruta incorrecta")
    require(not(root/"Temp/UnityLockfile").exists(),"Cerrá Unity antes de actualizar")
    url=git(root,"remote","get-url","origin").lower().removesuffix(".git").rstrip("/")
    require(url.endswith("/"+REPO) or url.endswith(":"+REPO),"Origin inesperado")
    current=git(root,"branch","--show-current");head=git(root,"rev-parse","HEAD")
    require(current in ("main",BR) and head in (BASE,target),"Rama o versión local diferente; no se fuerza")
    require(not git(root,"status","--porcelain","--untracked-files=no"),"Hay cambios versionados locales; no se modifican")
    before=fingerprint(root)
    git(root,"fetch","origin","main",BR)
    require(git(root,"rev-parse","origin/main")==BASE,"main remota cambió")
    require(git(root,"rev-parse","origin/"+BR)==target,"La rama V6.5 avanzó; usá el paquete correspondiente")
    require(git(root,"merge-base","--is-ancestor",BASE,target,check=False).returncode==0,"V6.5 no desciende de V6.4")
    paths=git(root,"diff","--name-only",BASE,target).splitlines()
    require(all(x=="README.md" or x.startswith(("docs/","tests/","tools/level_editor/","Assets/BIOFIT/GeomeTry/")) for x in paths),"Archivo fuera del alcance autorizado")
    require(not any(x.startswith("tools/level_editor/changes/BIOFIT_level_") for x in paths),"Deltas de usuario cambiaron en el paquete")
    local=git(root,"show-ref","--verify","refs/heads/"+BR,check=False)
    if local.returncode==0:require(local.stdout.split()[0] in (BASE,target),"Rama local divergente")
    print("OK PREVUELO V6.5:",len(before),"mapas/borradores preservados;",len(paths),"archivos versionados; main intacta")
    if not a.instalar:return
    if current!=BR:
        if local.returncode==0:git(root,"switch",BR)
        else:git(root,"switch","--track","-c",BR,"origin/"+BR)
    git(root,"merge","--ff-only",target)
    require(git(root,"rev-parse","HEAD")==target and git(root,"rev-parse","main")==BASE,"HEAD inesperado")
    require(fingerprint(root)==before,"Mapa o borrador cambió inesperadamente; no aplicar más cambios")
    print("OK V6.5 DESARROLLO INSTALADA",target,"| mapas y borradores intactos")
    print("Siguiente: importar arte local con tools/level_editor/importar_arte_v65.py")
if __name__=="__main__":
    try:main()
    except Exception as e:print("DETENIDO:",e,file=sys.stderr);sys.exit(1)
