#!/usr/bin/env python3
"""Local overlay for the pinned PR31 commit. Never pushes or alters upstream assets."""
import argparse, collections, hashlib, json, math, shutil, subprocess, sys, tempfile, zipfile
from pathlib import Path

HEAD='73cd148b9f3747b736eb4972c88ddee5a4fbe517'
SOURCE='baac8024b41f90e5d08e519f4b51b423e2038853aecb3ce59c6e428d1b49af12'
BASE=Path(__file__).resolve().parent
EXPECTED={'decoration':869,'solid':786,'hazard':610,'backgroundColor':7,'groundColor':7,'enterEffect':5,'shipPortal':3,'coin':3,'cubePortal':1}
UX_OLD='''                : Resources.Load<TextAsset>("maps/BASE/" + MapKeys[i]);'''
UX_NEW=UX_OLD+'\n            map = BiofitStereoLocalRuntime.SelectMap(i, map);'
VERIFY_OLD='''                TextAsset asset = Resources.Load<TextAsset>("BiofitThreeModes/" + Names[i]);'''
VERIFY_NEW='''                if (i == 0 && BiofitStereoLocalRuntime.ValidateInstalled()) continue;\n'''+VERIFY_OLD
LOG_OLD='''            Debug.Log("BIOFIT NIVEL 1 STEREO: video-map 825u, Ship+Cube+3 monedas " +\n                "y Niveles 2-5 legacy de 575u verificados por estructura. " +\n                "Solo validacion estatica: NO equivale a geometria original exacta " +\n                "ni a gameplay verificado en Unity/Android.");'''
LOG_NEW='''            Debug.Log("BIOFIT: Nivel 1 local (si instalado) o mapa historico y niveles 2-5 legacy verificados por estructura. No equivale a gameplay verificado.");'''
def patch(s, old, new):
    if s.count(old)!=1: raise ValueError('Fuente inesperada: no se aplicara el cambio: '+old[:80])
    return s.replace(old,new,1)
def rgb(h,k):
    a=h[k].split('_');d=dict(zip(a[::2],a[1::2]));return [int(d[str(i)]) for i in (1,2,3)]
def prepare_data(source, target):
    d=json.loads((source/'stereo_datos.json').read_text(encoding='utf-8'))
    assert d['sourceSha256']==SOURCE and d['unitsPerBlock']==30
    objs=d['objects'];assert len(objs)==2291 and len({o['id'] for o in objs})==31
    assert collections.Counter(o['kind'] for o in objs)==EXPECTED
    assert {o['index'] for o in objs}==set(range(2291))
    fields='index id kind xGD yGD rotationGD widthGD heightGD flipX flipY layer order frames'.split()
    compact=[]
    for o in objs:
        p=o['raw'];assert int(p['1'])==o['id'] and float(p['2'])==o['xGD'] and float(p['3'])==o['yGD']
        assert float(p.get('6','0'))==o['rotationGD'] and (p.get('4','0')=='1')==o['flipX'] and (p.get('5','0')=='1')==o['flipY']
        a={k:o[k] for k in fields}
        if o['kind'] in ('backgroundColor','groundColor'):
            a.update(r=int(p['7']),g=int(p['8']),b=int(p['9']),duration=float(p['10']))
        compact.append(a)
    assert len({f for o in compact for f in o['frames']})==28
    target.mkdir(parents=True,exist_ok=False)
    (target/'Objects.json').write_text(json.dumps(dict(sourceSha256=SOURCE,unitsPerBlock=30,initialBackground=rgb(d['header'],'kS29'),initialGround=rgb(d['header'],'kS30'),objects=compact),separators=(',',':')),encoding='utf-8')
    # Upstream Level loader uses marker for floor, end and progress. All geometry is independent.
    (target/'Level.json').write_text(json.dumps({'id':1,'name':'Nivel 1','difficulty':1,'music':'StereoMadness','map':[{'type':'biofitStereoLocalAnchor','x':876,'y':0}]}),encoding='utf-8')
    shutil.copytree(source/'sprites_locales',target/'Sprites')
    assert len(list((target/'Sprites').glob('*.png')))==30
    return {'objects':len(compact),'ids':len({o['id'] for o in compact}),'textures':30,'visible':2272,'effects':19,'classes':dict(EXPECTED),'sourceSha256':SOURCE}

def git(root,*args):return subprocess.check_output(['git','-C',str(root),*args],text=True).strip()
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--proyecto',required=True,type=Path);ap.add_argument('--recursos',required=True,type=Path);a=ap.parse_args()
    root=a.proyecto.resolve();z=a.recursos.resolve()
    if git(root,'rev-parse','HEAD')!=HEAD:raise SystemExit('Usa un clon nuevo en el commit exacto del PR31: '+HEAD)
    if git(root,'status','--porcelain','--untracked-files=no'):raise SystemExit('Hay cambios en archivos versionados. Usa un clon nuevo.')
    if not (root/'Assets/GeomeTryOriginal/Assets/Scripts/Game.cs').is_file():raise SystemExit('Ejecuta primero tools/prepare_geometry_original.ps1 con Unity cerrado.')
    if not z.is_file():raise SystemExit('No se encontro el ZIP de recursos: '+str(z))
    with zipfile.ZipFile(z) as f:
        if hashlib.sha256(f.read('1.txt').strip()).hexdigest()!=SOURCE:raise SystemExit('El 1.txt no coincide con el mapa validado.')
    geome=root/'Assets/BIOFIT/GeomeTry';ux=geome/'BiofitGeometryUX.cs';verify=geome/'Editor/BiofitThreeModesBuild.cs';runtime=geome/'BiofitStereoLocalRuntime.cs';post=geome/'Editor/BiofitStereoLocalTextureImport.cs';local=root/'Assets/BIOFIT/Resources/BiofitStereoLocal'
    if any(p.exists() for p in (runtime,post,local)):raise SystemExit('Ya existe una instalacion local. Usa otro clon; no se sobrescribe.')
    # Read all original source before any write; fail closed on changed source.
    ux_bytes=ux.read_bytes();ver_bytes=verify.read_bytes()
    ux_s=patch(ux_bytes.decode('utf-8-sig').replace('\r\n','\n'),UX_OLD,UX_NEW)
    ver_s=patch(patch(ver_bytes.decode('utf-8-sig').replace('\r\n','\n'),VERIFY_OLD,VERIFY_NEW),LOG_OLD,LOG_NEW)
    with tempfile.TemporaryDirectory(prefix='biofit_stereo_') as td:
        td=Path(td);derived=td/'derived';stage=td/'BiofitStereoLocal'
        subprocess.run([sys.executable,str(BASE/'preparar_stereo.py'),str(z),str(derived)],check=True)
        report=prepare_data(derived,stage)
        exclude=Path(git(root,'rev-parse','--absolute-git-dir'))/'info/exclude'
        before=exclude.read_bytes() if exclude.exists() else None
        try:
            exclude.parent.mkdir(parents=True,exist_ok=True)
            with exclude.open('ab') as f:f.write(b'\n# Private local Geometry Dash references; never stage these files\n/Assets/BIOFIT/Resources/BiofitStereoLocal/\n/Assets/BIOFIT/Resources/BiofitStereoLocal.meta\n')
            shutil.copytree(stage,local)
            shutil.copy2(BASE/runtime.name,runtime);shutil.copy2(BASE/post.name,post)
            ux.write_text(ux_s,encoding='utf-8');verify.write_text(ver_s,encoding='utf-8')
        except BaseException:
            ux.write_bytes(ux_bytes);verify.write_bytes(ver_bytes)
            for p in (runtime,post):p.unlink(missing_ok=True)
            if local.exists():shutil.rmtree(local)
            if before is None:exclude.unlink(missing_ok=True)
            else:exclude.write_bytes(before)
            raise
        print(json.dumps(report,indent=2));print('OK: instalacion LOCAL en '+str(root))
        print('Abri Unity y ejecuta BIOFIT > GeomeTry ORIGINAL > 8. Verificar niveles 3 MODOS (60 s).')
        print('No ejecutar git add -f, commit ni push de recursos originales.')
if __name__=='__main__':main()
