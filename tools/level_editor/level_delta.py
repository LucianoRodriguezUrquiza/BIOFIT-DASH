#!/usr/bin/env python3
"""BIOFIT V6.4: delta exacto, local y publicación selectiva. Python 3.9+."""
import argparse, hashlib, json, os, re, subprocess, sys, tempfile
from pathlib import Path
PUBLISH_BRANCHES={'main','niveles-mejorados'}
REPO='lucianorodriguezurquiza/biofit-game-unity'
DEC=json.JSONDecoder()
FIELDS=set('index id layer order kind xGD yGD rotationGD widthGD heightGD flipX flipY frames r g b duration portalMinGD portalMaxGD collisionMode deathMask editorScaleX editorScaleY'.split())
def require(ok,msg):
    if not ok: raise ValueError(msg)
def read(p): return Path(p).read_bytes().decode('utf-8-sig')
def sha(s): return hashlib.sha256(s.encode('utf-8')).hexdigest()
def ws(s,i):
    while i<len(s) and s[i].isspace(): i+=1
    return i
def fields(s):
    i=ws(s,0);require(s[i]=='{','Objeto JSON esperado');i=ws(s,i+1);out={}
    while s[i]!='}':
        k,j=DEC.raw_decode(s,i);j=ws(s,j);require(s[j]==':','Separador JSON inválido');a=ws(s,j+1);_,j=DEC.raw_decode(s,a);require(k not in out,'Clave repetida');out[k]=s[a:j];i=ws(s,j)
        if s[i]==',': i=ws(s,i+1)
        else: require(s[i]=='}','Fin JSON inválido')
    return out
def items(s):
    i=ws(s,0);require(s[i]=='[','Array JSON esperado');i=ws(s,i+1);out=[]
    while s[i]!=']':
        _,j=DEC.raw_decode(s,i);out.append(s[i:j]);i=ws(s,j)
        if s[i]==',': i=ws(s,i+1)
        else: require(s[i]==']','Fin array inválido')
    return out
def join(d): return '{'+','.join(json.dumps(k,ensure_ascii=False)+':'+v for k,v in d.items())+'}'
def arr(v): return '['+','.join(v)+']'
def reconstruct(base,delta):
    d=json.loads(delta);raw=fields(delta);require(d['schema']==1 and d['slot'] in range(1,6) and d['editorRevision']==1,'Esquema de delta desconocido')
    require(sha(base)==d['baseSha256'],'La base no coincide con SHA-256 del delta')
    header=fields(base);records={}
    for s in items(header['objects']):
        idx=json.loads(s)['index'];require(idx not in records,'Índice duplicado en base');records[idx]=s
    for idx in d['removed']:
        require(idx in records,'Baja inexistente');del records[idx]
    for s in items(raw['edits']):
        e=fields(s);idx=json.loads(e['index']);f=fields(e['fields']);require(idx in records and set(f)<=FIELDS-{'index'},'Edición inválida / datos no autorizados')
        old=fields(records[idx]);old.update(f);records[idx]=join(old)
    for s in items(raw['added']):
        f=fields(s);idx=json.loads(f['index']);require(idx not in records and set(f)<=FIELDS,'Alta inválida / datos no autorizados');records[idx]=s
    order=d['order'];require(len(order)==len(set(order))==len(records) and set(order)==set(records),'Orden de objetos inválido')
    header['editorRevision']='1';header['endGD']=raw['endGD'];header['objects']=arr(records[i] for i in order)
    result=join(header);require(sha(result)==d['targetSha256'],'Reconstrucción no coincide con SHA-256 objetivo');return result
def atomic(p,s):
    p=Path(p);fd,tmp=tempfile.mkstemp(dir=p.parent,prefix=p.name+'.',suffix='.tmp')
    try:
        with os.fdopen(fd,'wb') as f: f.write(s.encode('utf-8'))
        os.replace(tmp,p)
    finally:
        if os.path.exists(tmp): os.unlink(tmp)
def git(root,*args):
    p=subprocess.run(['git','-C',str(root),*args],capture_output=True,text=True,encoding='utf-8',errors='replace')
    if p.returncode: raise RuntimeError('git '+' '.join(args)+'\n'+p.stdout+p.stderr)
    return p.stdout.strip()
def map_path(root,slot): return root/'Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5'/str(slot)/'Active.json'
def main():
    p=argparse.ArgumentParser();p.add_argument('--proyecto',required=True,type=Path);p.add_argument('--delta',required=True,type=Path);p.add_argument('--aplicar',action='store_true');p.add_argument('--publicar',action='store_true');p.add_argument('--nota',default='Edición experimental desde BIOFIT Level Editor; validación de jugabilidad pendiente.');a=p.parse_args()
    require(not(a.aplicar and a.publicar),'Elegí una operación por vez');root=a.proyecto.resolve();delta=a.delta.resolve();text=read(delta);d=json.loads(text);slot=d['slot'];require(slot in range(1,6),'Ranura inválida')
    active=map_path(root,slot);work=root/'Builds/BiofitLevelEditor'/str(slot)
    base=read(active)
    if sha(base)!=d['baseSha256'] and a.publicar: base=read(work/'Base.json')
    result=reconstruct(base,text);print('OK DELTA: nivel',slot,'|',len(d['edits']),'ediciones,',len(d['added']),'altas,',len(d['removed']),'bajas | SHA objetivo',sha(result))
    if a.aplicar:
        require(not(root/'Temp/UnityLockfile').exists(),'Cerrá Unity para aplicar desde Python');require(sha(read(active))==d['baseSha256'],'Active cambió');work.mkdir(parents=True,exist_ok=True);atomic(work/'PreviousActive.json',base);atomic(active,result);print('OK ACTIVE LOCAL; sin publicar')
    if a.publicar:
        rel=delta.relative_to(root).as_posix();require(rel.startswith('tools/level_editor/changes/') and re.fullmatch(r'BIOFIT_level_[1-5]_\d{8}_\d{6}\.json',delta.name),'Exportá con el nombre sugerido dentro de tools/level_editor/changes')
        BR=git(root,'branch','--show-current');require(BR in PUBLISH_BRANCHES,'Publicación únicamente en main o niveles-mejorados mientras exista');url=git(root,'remote','get-url','origin').lower().removesuffix('.git');require(url.endswith('/'+REPO) or url.endswith(':'+REPO),'Origin no autorizado');require(not git(root,'diff','--cached','--name-only'),'Hay cambios staged; no se mezclan')
        require(bool(git(root,'ls-remote','--heads','origin',BR)),'Rama remota ausente; sincronizá main. No se recrea una rama eliminada');git(root,'fetch','origin',BR);require(git(root,'rev-parse','HEAD')==git(root,'rev-parse','origin/'+BR),'Rama local y remota no coinciden; no se fuerza')
        index=root/'tools/level_editor/changes/INDEX.md';prior=read(index) if index.exists() else '# Cambios experimentales de niveles\n';require(delta.name not in prior,'Delta ya registrado')
        note=' '.join(a.nota.split());line=f'\n- [{delta.name}]({delta.name}) — ranura {slot}; {len(d["edits"])} ediciones, {len(d["added"])} altas, {len(d["removed"])} bajas; base {d["baseSha256"]}; objetivo {d["targetSha256"]}. {note}\n'
        atomic(index,prior+line);idx=index.relative_to(root).as_posix();git(root,'add','--',rel,idx);git(root,'commit','--only','-m',f'Nivel {slot}: delta experimental del editor','--',rel,idx);git(root,'push','origin','HEAD:refs/heads/'+BR);head=git(root,'rev-parse','HEAD');remote=git(root,'ls-remote','--heads','origin',BR).split()[0];require(head==remote,'Publicación no confirmada');print('OK DELTA PUBLICADO',head,'| rama',BR,'| solo delta e índice')
if __name__=='__main__':
    try: main()
    except Exception as e: print('DETENIDO:',e,file=sys.stderr);sys.exit(1)
