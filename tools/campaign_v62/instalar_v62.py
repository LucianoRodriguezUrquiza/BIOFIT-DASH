#!/usr/bin/env python3
"""BIOFIT DASH V6.2: corrections to local Stereo Madness data only."""
import argparse, copy, hashlib, json, os, tempfile
from pathlib import Path
REL=Path('Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5/1/Active.json')
BASE='7c1b2f7dbe15cd88627ae27d52d05ad64ebf36dd92c62f8e22577f6b782f502a'
SOURCE='baac8024b41f90e5d08e519f4b51b423e2038853aecb3ce59c6e428d1b49af12'
# Reconstructed from the two edited corridor images. Coordinates in GD units.
# (first center X, ceiling?, sprite ID, number of consecutive spikes)
BALL=[(8595,0,39,4),(8865,0,8,8),(9255,1,39,3),(9525,0,8,4),
      (9825,1,8,12),(9945,0,8,4),(10485,0,8,12),(10605,1,8,4),
      (10965,1,8,4),(11295,0,39,4),(11835,1,8,7)]
SPIDER=[(23025,1,39,3),(23295,0,8,4),(23595,1,8,12),(23715,0,8,4),
        (24255,0,8,12),(24375,1,8,4),(24735,1,8,4),(25065,0,39,4),
        (25305,1,8,3),(25425,0,8,3),(25605,1,8,6),(25965,1,8,4)]
MIDDLE=[(9885,7,170),(10575,7,160),(23655,8,175),(24195,14,175)]

def encode(d):return json.dumps(d,ensure_ascii=False,separators=(',',':')).encode('utf-8')
def sha(d):return hashlib.sha256(encode(d)).hexdigest()
def require(ok,msg):
    if not ok:raise ValueError(msg)
def transform(d):
    require(sha(d)==BASE,'Base distinta de V6; no se modifica nada.')
    old=d['objects']; out=copy.deepcopy(old); idx=max(o['index'] for o in out)+1
    templates={i:next(o for o in old if o['id']==i) for i in [1,5,6,7,8,17,39,40]}
    def add(id,x,y,rot=0):
        nonlocal idx
        o=copy.deepcopy(templates[id]);o.update(index=idx,xGD=float(x),yGD=float(y),rotationGD=float(rot),flipX=False,flipY=False,raw={'1':str(id),'2':str(x),'3':str(y),'6':str(rot)})
        idx+=1;out.append(o);return o
    def move(o,x=None,y=None):
        for k,v,r in [('xGD',x,'2'),('yGD',y,'3')]:
            if v is not None:o[k]=float(v);o['raw'][r]=str(v)
    def delete(pred):out[:]=[o for o in out if not pred(o)]
    def solid(x,y,id=1):return add(id,x,y)
    # Pair 1: keep first four planks; remove intermediate isolated plank;
    # lower the next bank by 45 GD, extend it to four planks.
    delete(lambda o:o['id']==40 and o['xGD'] in (4215,4275,4305,4335))
    delete(lambda o:o['index'] in (113,119))
    for x in (4275,4305,4335,4365):solid(x,158,40)
    add(17,4335,146)
    # Pair 2: three planks at y143, move following plank one grid cell right.
    for x in (6825,6855):solid(x,143,40)
    for o in out:
        if o['index'] in (452,453):move(o,x=6945)
    # Pair 3: widen low column by one cell; add two low stepping blocks.
    # Preserve current collision heights and use existing column graphics.
    solid(13245,15,7);solid(13245,45,6)
    solid(13365,15,6);solid(13395,15,6)
    # Remove ALL V6 spikes and interior thin planks inside the two corridors.
    def inside(o):return 7995<o['xGD']<12555 or 22935<o['xGD']<26565
    delete(lambda o:inside(o) and (o['kind']=='hazard' or o['id']==40))
    # Spider reference has a 150 GD corridor and a +30 GD step at the end.
    # Move only the ceiling, its fill, and final floor; no global physics changes.
    for o in out:
        x,y=o['xGD'],o['yGD']
        if 22935<=x<26565:
            if o['kind'] in ('solid','decoration') and y>=225:move(o,y=y+30)
            if x>=25815 and o['id']==1 and y==75:move(o,y=105)
            if o['kind']=='spiderPortal':o['portalMaxGD']=240
            if o['kind']=='coin' and x==26325:move(o,y=135)
    # Complete the fill below the raised floor with noncolliding decoration.
    for x in range(25815,26565,30):add(5,x,75)
    for rows,is_spider in [(BALL,False),(SPIDER,True)]:
        for x,upper,id,n in rows:
            h=templates[id]['heightGD']
            for j in range(n):
                xx=x+30*j
                floor=120 if (not is_spider and xx<8865) or (is_spider and xx>=25815) else 90
                add(id,xx,240-h/2 if upper else floor+h/2,180 if upper else 0)
    for x,n,y in MIDDLE:
        for j in range(n):solid(x+30*j,y)
    out.sort(key=lambda o:(o['xGD'],o['index']))
    require(len({o['index'] for o in out})==len(out),'Indices duplicados')
    require(len([o for o in out if o['kind']=='coin'])==3,'Monedas invalidas')
    require([(o['kind'],o['xGD']) for o in out if o['kind'].endswith('Portal')]==[(o['kind'],o['xGD']) for o in old if o['kind'].endswith('Portal')],'Portales cambiados')
    return dict(d,objects=out)

def atomic(p,b):
    fd,tmp=tempfile.mkstemp(dir=p.parent,prefix=p.name+'.',suffix='.tmp')
    try:
        with os.fdopen(fd,'wb') as f:f.write(b)
        os.replace(tmp,p)
    finally:
        if os.path.exists(tmp):os.unlink(tmp)

def main():
    a=argparse.ArgumentParser(description=__doc__);a.add_argument('--proyecto',type=Path,required=True);a.add_argument('--verificar',action='store_true');a.add_argument('--restaurar',action='store_true');a=a.parse_args()
    r=a.proyecto.resolve();p=r/REL;b=r/'Builds/BiofitV62/Active_V6.json'
    require((r/'ProjectSettings/ProjectVersion.txt').is_file(),'Ruta Unity incorrecta')
    raw=p.read_bytes();d=json.loads(raw)
    if a.restaurar:
        old=b.read_bytes();require(sha(d)==sha(transform(json.loads(old))),'El mapa no coincide con V6.2')
        if not a.verificar:atomic(p,old)
        print('OK RESTAURACION V6'+(' - sin escritura' if a.verificar else ''));return
    original=json.loads((p.parent/'Original.json').read_bytes());require(original['sourceSha256']==SOURCE and len(original['objects'])==2291,'Fuente original invalida')
    if b.exists() and sha(d)==sha(transform(json.loads(b.read_bytes()))):print('V6.2 ya instalada. Sin cambios.');return
    new=transform(d)
    require(new['endGD']==original['endGD'],'Meta modificada')
    for f in {f for o in new['objects'] for f in o['frames']}:require((p.parents[2]/'Sprites'/(f+'.png')).is_file(),'Falta sprite: '+f)
    print('V6:',len(d['objects']),'V6.2:',len(new['objects']),'monedas: 3');print('SHA256 V6.2:',sha(new))
    if a.verificar:print('OK PREVUELO V6.2 - sin escritura');return
    b.parent.mkdir(parents=True,exist_ok=True)
    if b.exists():require(sha(json.loads(b.read_bytes()))==BASE,'Respaldo V6 inesperado')
    else:atomic(b,raw)
    atomic(p,encode(new));require(sha(json.loads(p.read_bytes()))==sha(new),'Verificacion de escritura fallida')
    print('OK V6.2 LOCAL - solo geometria Stereo Madness; menu, BLE, cooldown y otros niveles intactos.')
if __name__=='__main__':main()
