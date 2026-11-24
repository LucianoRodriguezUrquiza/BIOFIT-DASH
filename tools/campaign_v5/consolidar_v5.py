#!/usr/bin/env python3
"""Consolidacion DIPL V5. Solo Git y biblioteca estandar Python."""
import argparse, datetime, hashlib, json, shutil, subprocess, sys, zipfile
from pathlib import Path
BASE=Path(__file__).resolve().parent
PATCHES=BASE/("parches" if (BASE/"parches").is_dir() else "patches")
HEAD="73cd148b9f3747b736eb4972c88ddee5a4fbe517"
REPO="LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY"
G="Assets/BIOFIT/GeomeTry/"
R="Assets/BIOFIT/Resources/"
CHAR="Assets/GeomeTryOriginal/Assets/Scripts/GameMechanics/CharacterType/Character.cs"
LOCAL=[R+"BiofitStereoLocal",R+"BiofitStereoV2",R+"Songs/Musics/BiofitLocalV5",R+"BiofitUi",R+"BiofitThreeModes/Artwork"]
CODE=[G+n for n in ["BiofitStereoLocalRuntime.cs","BiofitStereoSolidContact.cs","BiofitStereoPractice.cs","BiofitStereoPracticeButton.cs","BiofitV2Hud.cs","BiofitV2BitmapText.cs","BiofitV5Interaction.cs","BiofitThreeModes.cs","BiofitGeometryUX.cs","Editor/BiofitStereoLocalTextureImport.cs","Editor/BiofitThreeModesBuild.cs"]]
# Configuracion observada en BIOFIT_REVISION_V5.txt (24/11/2026).
SETTINGS=["Packages/packages-lock.json","ProjectSettings/EditorBuildSettings.asset",
          "ProjectSettings/GraphicsSettings.asset","ProjectSettings/ProjectSettings.asset",
          "ProjectSettings/QualitySettings.asset","ProjectSettings/ShaderGraphSettings.asset",
          "ProjectSettings/URPProjectSettings.asset"]
EXTRA_META=[G+n for n in ["BiofitUiArtwork.cs.meta","Editor/BiofitArtworkImporter.cs.meta",
                           "Editor/BiofitGeometryUxPreflight.cs.meta"]]
def versioned_paths(root):
    return CODE+[p+".meta" for p in CODE]+[p for p in SETTINGS+EXTRA_META if (root/p).is_file()]
ARCHIVE_BRANCHES={"docs/catalogo-recursos-biofit-20261123","docs/contexto-vivo-biofit-20261123","feat/configurable-bilateral-cooldown-400-900ms","feat/five-level-progression-1500ms-v1","feat/stereo-madness-ship-vrbox-held-v1","feat/vr-joystick-input-diagnostics"}
def sha(b):return hashlib.sha256(b).hexdigest()
def norm(p):return p.read_text(encoding="utf-8-sig").replace("\r\n","\n")
def git(root,*args,check=True):
    p=subprocess.run(["git","-C",str(root),*args],capture_output=True,text=True,encoding="utf-8",errors="replace")
    if check and p.returncode:raise RuntimeError("git "+" ".join(args)+"\n"+p.stdout+p.stderr)
    return p.stdout.strip() if check else p
def remote(root):
    d={}
    for l in git(root,"ls-remote","--heads","origin").splitlines():
        h,n=l.split("\t");d[n.removeprefix("refs/heads/")]=h
    return d
def local_paths(root):
    files=set()
    for name in LOCAL:
        p=root/name
        if p.is_dir():files.update(q for q in p.rglob("*") if q.is_file())
        meta=root/(name+".meta")
        if meta.is_file():files.add(meta)
    for name in [R+"BiofitUi.meta",R+"Songs.meta",R+"Songs/Musics.meta"]:
        if (root/name).is_file():files.add(root/name)
    files.add(root/CHAR)
    return sorted(files)
def check_v5(root):
    assert (root/".git").exists(),"No es el proyecto Git local."
    url=git(root,"remote","get-url","origin").rstrip("/").removesuffix(".git")
    assert url.lower().endswith(("/"+REPO).lower()) or url.lower().endswith((":"+REPO).lower()),"Repositorio inesperado: "+url
    assert git(root,"rev-parse","HEAD")==HEAD,"HEAD no coincide con la base de V5. No se sobrescribe nada."
    assert not git(root,"diff","--cached","--name-only"),"Hay archivos staged previamente. Revisalos primero."
    for p in CODE+[CHAR]:
        assert (root/p).is_file(),"Falta "+p
    for p in CODE:
        q=root/(p+".meta")
        assert q.is_file(),"Falta "+str(q)+". Abri y cerra Unity para que genere los .meta."
    for n in ["BiofitStereoLocalRuntime.cs","BiofitStereoSolidContact.cs","BiofitStereoPractice.cs","BiofitV5Interaction.cs"]:
        assert norm(root/(G+n))==norm(PATCHES/"V5"/n),"Codigo V5 diferente del parche: "+n
    for n in ["BiofitStereoPracticeButton.cs","BiofitV2Hud.cs"]:
        assert norm(root/(G+n))==norm(PATCHES/"V3"/n),"Codigo V3 diferente: "+n
    assert "BlocksGameplayPointer()" in norm(root/CHAR),"Falta ajuste V3 de Character.cs"
    assert "InvertedCube" in norm(root/(G+"BiofitThreeModes.cs"))
    assert "CAN'T LET GO" in norm(root/(G+"BiofitGeometryUX.cs"))
    report=json.loads((PATCHES/"V5/VALIDACION_V5.json").read_text(encoding="utf-8"))
    for level in report["levels"]:
        d=root/(R+"BiofitStereoLocal/CampaignV5/"+str(level["slot"]))
        a=json.loads((d/"Active.json").read_text(encoding="utf-8-sig"))
        o=json.loads((d/"Original.json").read_text(encoding="utf-8-sig"))
        assert len(a["objects"])==level["active"] and len(o["objects"])==level["source"] and o["sourceSha256"]==level["sha256"],"Mapa distinto: "+level["name"]
        assert sum(x["kind"]=="coin" for x in a["objects"])==3
        assert not any(x["kind"]=="shipPortal" for x in a["objects"])
        assert (d/"Level.json").is_file()
    for n in LOCAL[:3]:assert (root/n).is_dir(),"Faltan recursos locales: "+n
    # No subir modificaciones ajenas al parche.
    tracked=git(root,"diff","--name-only","HEAD").splitlines()
    allowed=set(versioned_paths(root))
    unexpected=[p for p in tracked if p not in allowed]
    assert not unexpected,"Cambios versionados ajenos al parche; revisar antes de publicar:\n"+ "\n".join(unexpected)
    for n in LOCAL[:3]+[CHAR]:
        assert not git(root,"ls-files","--",n,n+".meta"),"Recursos locales ya versionados: "+n
    # No confiar en status de recursos ignorados; solo se seleccionan rutas explicitas.
    print("OK V5 LOCAL: cinco mapas, 11852 registros, 15 monedas; codigo V5 y base reconocidos.")
    print("Configuraciones Unity reconocidas:")
    for name in SETTINGS:
        if (root/name).is_file():print("  "+name)
    print("Reglas de exclusion existentes: sin modificaciones.")
def make_backup(root,dest):
    dest.mkdir(parents=True,exist_ok=False)
    archive=dest/"RECURSOS_LOCALES_V5_NO_SUBIR.zip"
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        manifest={}
        for p in local_paths(root):
            rel=p.relative_to(root).as_posix();b=p.read_bytes();manifest[rel]=sha(b);z.writestr(rel,b)
        z.writestr("MANIFIESTO_SHA256.json",json.dumps(manifest,indent=2))
    with zipfile.ZipFile(archive) as z:assert z.testzip() is None
    # The patch sources and working changes are included in a separate private backup.
    with zipfile.ZipFile(dest/"CODIGO_LOCAL_ANTES_COMMIT.zip","w",zipfile.ZIP_DEFLATED) as z:
        for name in versioned_paths(root):z.write(root/name,name)
        z.writestr("diff.txt",git(root,"diff","HEAD"))
    git(root,"bundle","create",str(dest/"historial_antes.bundle"),"--all")
    git(root,"bundle","verify",str(dest/"historial_antes.bundle"))
    print("OK respaldo local:",dest)
def prepare_docs(root):
    target=root/"tools/campaign_v5";target.mkdir(parents=True,exist_ok=True)
    assert not (target/"patches").exists(),"Ya existe carpeta de consolidacion; revisar."
    shutil.copytree(PATCHES,target/"patches")
    for n in ["restaurar_local_v5.py","LEEME.txt","consolidar_v5.py"]:
        shutil.copy2(BASE/n,target/n)
    # For a later rebuild, archived installer source is located next to the scripts.
    hist=root/"docs/history";hist.mkdir(parents=True,exist_ok=True)
    archived=[]
    for n in ["CONTEXTO_ACTUAL.md","CONTEXTO_VIVO.md"]:
        q=root/"docs"/n
        if q.exists():
            dst=hist/(q.stem+"_PRE_V5.md");shutil.copy2(q,dst)
            archived.append(dst.relative_to(root).as_posix())
    (root/"docs/CONTEXTO_ACTUAL.md").write_text((BASE/"CONTEXTO_ACTUAL.md").read_text(encoding="utf-8"),encoding="utf-8")
    shutil.copy2(BASE/"CONTEXTO_ACTUAL.md",target/"CONTEXTO_ACTUAL.md")
    (root/"docs/CONTEXTO_VIVO.md").write_text("# BIOFIT DASH — relevo V5\n\nLeer primero [CONTEXTO_ACTUAL.md](CONTEXTO_ACTUAL.md), [ASSET_SOURCES.md](ASSET_SOURCES.md) y [validacion V5](../tools/campaign_v5/patches/V5/VALIDACION_V5.json). Contexto historico: [PRE V5](history/CONTEXTO_VIVO_PRE_V5.md). Validacion provisional del usuario: 2026-11-24. DIPL / ICC / RFD obligatorios.\n",encoding="utf-8")
    readme=root/"README.md"
    old=readme.read_text(encoding="utf-8-sig") if readme.exists() else ""
    readme.write_text("# BIOFIT DASH — Campana Clasica V5\n\nVersion principal aprobada provisionalmente por el usuario el 24/11/2026. Leer primero [Contexto actual](docs/CONTEXTO_ACTUAL.md) y [relevo](docs/CONTEXTO_VIVO.md). Los recursos locales se conservan intactos. Esta consolidacion publica codigo y configuracion; las decisiones sobre publicacion de recursos de terceros quedan pendientes. El texto siguiente es el README historico.\n\n---\n\n"+old,encoding="utf-8")
    return ["README.md","docs/CONTEXTO_ACTUAL.md","docs/CONTEXTO_VIVO.md",*archived,"tools/campaign_v5"]
def main():
    p=argparse.ArgumentParser();p.add_argument("--proyecto",type=Path,required=True);p.add_argument("--publicar",action="store_true");a=p.parse_args();root=a.proyecto.resolve()
    check_v5(root)
    git(root,"fetch","origin","--prune")
    refs=remote(root)
    print("Ramas remotas:",", ".join(refs))
    assert "main" in refs
    assert git(root,"merge-base","--is-ancestor",refs["main"],HEAD,check=False).returncode==0,"main avanzo fuera de la base. No se fuerza ni se publica; revisar."
    extra=set(refs)-ARCHIVE_BRANCHES-{"main"}
    assert not extra,"Ramas nuevas no previstas: "+str(extra)
    if not a.publicar:
        print("OK PREVUELO V5.1: sin cambios de contenido; GitHub publico permitido.");return
    print("Publicacion de codigo, configuracion y documentacion. No se modifican recursos ni reglas de exclusion.")
    # Verificar que el main local, si existe, no contenga historia exclusiva.
    local_main=git(root,"rev-parse","--verify","refs/heads/main",check=False)
    if local_main.returncode==0:
        assert git(root,"merge-base","--is-ancestor",local_main.stdout.strip(),HEAD,check=False).returncode==0,"main local contiene commits exclusivos; revisar antes de publicar."
    stamp=datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dest=root.parent/("BIOFIT_RESPALDO_V5_"+stamp)
    make_backup(root,dest)
    docs=prepare_docs(root)
    # Explicit allowlist; no git add . or -f.
    git(root,"add","--",*(versioned_paths(root)+docs))
    staged=git(root,"diff","--cached","--name-only").splitlines()
    assert not any(x==CHAR or any(x==n or x.startswith(n+"/") or x==n+".meta" for n in LOCAL) for x in staged),"Recurso local staged; cancelar."
    allowed_files=set(versioned_paths(root)+[x for x in docs if x!="tools/campaign_v5"])
    patch_root=root/"tools/campaign_v5"
    allowed_files.update(p.relative_to(root).as_posix() for p in patch_root.rglob("*") if p.is_file())
    assert set(staged)<=allowed_files,"Archivo staged inesperado: "+str(set(staged)-allowed_files)
    assert not any(x.startswith(R) for x in staged),"Recurso no contemplado en esta consolidacion. Revisar."
    print("Archivos a versionar:",len(staged))
    git(root,"commit","-m","BIOFIT DASH V5: Campana Clasica validada provisionalmente")
    commit=git(root,"rev-parse","HEAD")
    # Save a second bundle containing V5 plus every previous ref before deletion.
    git(root,"bundle","create",str(dest/"historial_con_V5.bundle"),"--all")
    git(root,"bundle","verify",str(dest/"historial_con_V5.bundle"))
    assert remote(root)==refs,"Ramas cambiaron durante preparacion; detener sin borrar."
    # Archive branch tips in tags. No force; preserve each unique history.
    tagrefs=[]
    for name,h in refs.items():
        if name=="main":continue
        tag="archive/v5-"+stamp+"/"+name
        git(root,"tag",tag,h);tagrefs.append("refs/tags/"+tag)
    release="biofit-dash-v5-"+stamp
    git(root,"tag",release,commit);tagrefs.append("refs/tags/"+release)
    # Atomic publication: no force, abort if server/branch protection rejects.
    git(root,"push","--atomic","origin","HEAD:refs/heads/main",*tagrefs)
    assert remote(root).get("main")==commit,"main no coincide. No borrar ramas."
    for tagref in tagrefs:
        result=git(root,"ls-remote","--tags","origin",tagref)
        expected=git(root,"rev-parse",tagref)
        assert result.split("\t")[0]==expected,"Tag de respaldo no verificado. No borrar ramas."
    # Only after main and archives verified; leases protect against concurrent updates.
    deletes=[(name,h) for name,h in refs.items() if name!="main"]
    if deletes:
        args=["push","--atomic"]+["--force-with-lease=refs/heads/"+n+":"+h for n,h in deletes]+["origin"]+[":refs/heads/"+n for n,h in deletes]
        git(root,*args)
    remaining=remote(root)
    assert remaining=={"main":commit},"Quedan ramas remotas: "+str(remaining)
    print("OK MAIN PUBLICADA:",commit)
    print("OK ramas remotas: solo main. Tags historicos conservados.")
    # No hard reset: branch switch points to already committed identical tree.
    try:
        git(root,"fetch","origin","--prune")
        # main local preexistente se comprobó como ancestro; su historial queda preservado.
        git(root,"switch","-C","main",commit);git(root,"branch","--set-upstream-to=origin/main","main")
    except Exception as e:print("AVISO: rama local no renombrada:",e)
    print("Respaldo local:",dest)
    print("Conserva este respaldo en una unidad separada.")
if __name__=="__main__":
    try:main()
    except Exception as e:print("DETENIDO:",e,file=sys.stderr);sys.exit(1)
