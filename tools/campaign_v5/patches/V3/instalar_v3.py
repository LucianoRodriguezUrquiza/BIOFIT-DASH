#!/usr/bin/env python3
"""Upgrade the installed local V2. No network, no changes to original level data."""
import argparse, hashlib, json, re, subprocess, sys
from datetime import datetime
from pathlib import Path
BASE=Path(__file__).resolve().parent
HEAD='73cd148b9f3747b736eb4972c88ddee5a4fbe517'
V2={
'BiofitStereoLocalRuntime.cs':'e1121a62f7649a542162190b8b91bde062027f6f31ed8743bebbe3cac72e8ed7',
'BiofitStereoSolidContact.cs':'dd317efed807762ce0fcea05cdbdbc606551c384a85b5c1834d35f22eb4d60ab',
'BiofitStereoPractice.cs':'addd7ef1e9524be262a704044d2b53d8aec5c203d6fa49834547df66282b2f5b',
'BiofitV2Hud.cs':'119e7545cd46c4dcda57f412f20da6611e56ce9b4c88add1ccc87c48e5493634',
}
SHA='baac8024b41f90e5d08e519f4b51b423e2038853aecb3ce59c6e428d1b49af12'
def read(p):return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
def digest(s):return hashlib.sha256(s.encode('utf-8')).hexdigest()
def one(s,a,b):
    if s.count(a)!=1:raise ValueError('Firma V2 inesperada: '+a[:140])
    return s.replace(a,b,1)
ADD='ImageButton("Colocar checkpoint",canvasRoot,BiofitV2BitmapText.Art("add_checkpoint"),"+",new Vector2(.44f,.085f),new Vector2(150,86),Vector2.zero,BiofitStereoPractice.Place);'
REMOVE='ImageButton("Eliminar checkpoint",canvasRoot,BiofitV2BitmapText.Art("remove_checkpoint"),"-",new Vector2(.56f,.085f),new Vector2(150,86),Vector2.zero,BiofitStereoPractice.Remove);'
GATE='(Input.GetMouseButton(0) && !Biofit.GeomeTryOriginal.BiofitStereoPracticeButton.BlocksGameplayPointer())'
def patch_ux(s):
    s=one(s,ADD,'BiofitStereoPracticeButton.Create(canvasRoot,new Vector2(.44f,.085f),new Vector2(150,86),BiofitV2BitmapText.Art("add_checkpoint"),true);')
    s=one(s,REMOVE,'BiofitStereoPracticeButton.Create(canvasRoot,new Vector2(.56f,.085f),new Vector2(150,86),BiofitV2BitmapText.Art("remove_checkpoint"),false);')
    return s

def patch_modes(s):
    s=one(s,'Input.GetMouseButton(0) ||',GATE+' ||')
    s=one(s,'            motor.SetMode(Target);','''            motor.SetMode(Target);
            if (BiofitStereoPractice.Available)
                Debug.Log("BIOFIT V3 portal: " + Target + " playerX=" + cube.transform.position.x.ToString("F3") + " playerY=" + cube.transform.position.y.ToString("F3"));''')
    return s

def patch_character(s,original):
    # Verify the copied GeomeTry script still matches the pinned source with
    # the existing Unity Camera compatibility rename. Never edit submodule.
    compat=re.sub(r'(?<![A-Za-z0-9_])Camera(?![A-Za-z0-9_])','GeometryCamera',original)
    if s!=compat:raise ValueError('Character.cs copiado no coincide con la fuente y compatibilidad esperadas; no se modifica.')
    return one(s,'Input.GetMouseButton(0)',GATE)

def plan(root):
    geome=root/'Assets/BIOFIT/GeomeTry'
    local=root/'Assets/BIOFIT/Resources/BiofitStereoLocal'
    ui=root/'Assets/BIOFIT/Resources/BiofitStereoV2/UI'
    d=json.loads(read(local/'Objects.json'))
    if d['sourceSha256']!=SHA or len(d['objects'])!=2291 or len({o['id'] for o in d['objects']})!=31:raise ValueError('Datos V1/V2 inesperados.')
    for name in ['add_checkpoint','remove_checkpoint']:
        if not (ui/(name+'.png')).is_file():raise ValueError('Falta UI V2: '+name)
    if not (local/'Level.json').is_file():raise ValueError('Falta Level.json local.')
    out={}
    for name,h in V2.items():
        path=geome/name
        if digest(read(path))!=h:raise ValueError('Archivo V2 modificado o V3 ya instalada: '+str(path))
        out[path]=read(BASE/name)
    new=geome/'BiofitStereoPracticeButton.cs'
    if new.exists():raise ValueError('Ya existe '+str(new))
    out[new]=read(BASE/new.name)
    ux=geome/'BiofitGeometryUX.cs';modes=geome/'BiofitThreeModes.cs'
    uxText=read(ux);modeText=read(modes)
    if 'BiofitV2BitmapText.Attach' not in uxText or 'BiofitStereoPractice.Enabled' not in uxText or 'RestorePractice(' not in modeText:raise ValueError('La V2 no esta aplicada.')
    out[ux]=patch_ux(uxText);out[modes]=patch_modes(modeText)
    relative=Path('Assets/Scripts/GameMechanics/CharacterType/Character.cs')
    char=root/'Assets/GeomeTryOriginal'/relative
    original=root/'ThirdParty/GeomeTryOriginal'/relative
    out[char]=patch_character(read(char),read(original))
    return out

def apply(root,out):
    old={p:p.read_bytes() if p.exists() else None for p in out}
    backup=root/'Builds'/('StereoV3_backup_'+datetime.now().strftime('%Y%m%d_%H%M%S_%f'))
    backup.mkdir(parents=True,exist_ok=False)
    for p,b in old.items():
        if b is not None:
            q=backup/p.relative_to(root);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
    (backup/'manifest.json').write_text(json.dumps({'files':[str(p.relative_to(root)) for p in out],'new':[str(p.relative_to(root)) for p,b in old.items() if b is None]},indent=2),encoding='utf-8')
    try:
        for p,s in out.items():p.write_text(s,encoding='utf-8')
        for p,s in out.items():
            if read(p)!=s:raise IOError('Verificacion de escritura: '+str(p))
    except BaseException:
        for p,b in old.items():
            if b is None:p.unlink(missing_ok=True)
            else:p.write_bytes(b)
        raise
    return backup

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--proyecto',type=Path,required=True);ap.add_argument('--verificar',action='store_true',help='Solo valida V2 y muestra cambios, no escribe.');a=ap.parse_args();root=a.proyecto.resolve()
    head=subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()
    if head!=HEAD:raise ValueError('Commit base inesperado: '+head)
    out=plan(root)
    if a.verificar:print('OK V2 compatible; '+str(len(out))+' archivos previstos. No se escribio nada.');return
    backup=apply(root,out)
    print('OK V3 LOCAL - portal Ship, checkpoints manuales y botones sin deformacion.')
    print('Archivos actualizados: '+str(len(out)))
    print('Datos del nivel, fuentes, BLE y mapas 2-5 intactos.')
    print('Backup: '+str(backup))
if __name__=='__main__':
    try:main()
    except Exception as e:print('ERROR V3: '+str(e),file=sys.stderr);sys.exit(1)
