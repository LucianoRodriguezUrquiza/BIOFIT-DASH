#!/usr/bin/env python3
"""DIPL V6: local data-only patch for the V5 Stereo Madness campaign."""
import argparse, copy, hashlib, json, os, tempfile
from pathlib import Path

BASE_SHA = "b92994dcd97d2adf37754b6ceb7b82784d7895ede36b8f39a077133e9c6ef58b"
SOURCE_SHA = "baac8024b41f90e5d08e519f4b51b423e2038853aecb3ce59c6e428d1b49af12"
REL = Path("Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5/1/Active.json")
# Each row: first x, side (0 floor / 1 ceiling), spike ID, count.
BALL = [(8595,0,39,4),(8835,0,39,5),(9255,1,39,3),(9615,0,39,3),
        (10005,1,8,5),(10455,0,8,8),(11025,1,39,3),(11415,0,39,4),(11955,1,8,4)]
SPIDER = [(23265,0,8,5),(23715,1,8,8),(24165,0,8,6),(24615,1,8,4),
          (25065,0,8,4),(25455,1,8,4),(25815,0,8,4),(26175,1,8,4)]
# (start x, count, center y). Mid platforms intentionally outside the
# vertical transfer windows; gaps on each side accommodate a 30 GD avatar.
MIDDLE = [(10005,5,165),(10605,5,165),(23745,7,150),(24165,10,150)]
EXTEND = [(4125,143),(4155,143),(4185,143),(4275,203),(4305,203)]
LOWER = {(13215,105),(13215,75),(13335,135),(13335,105),(13455,165),(13455,135)}

def encode(d):
    return json.dumps(d,ensure_ascii=False,separators=(",",":")).encode("utf-8")

def digest(b):
    return hashlib.sha256(b).hexdigest()

def require(ok,msg):
    if not ok: raise ValueError(msg)

def transform(d):
    require(d.get("sourceSha256")==SOURCE_SHA and d.get("unitsPerBlock")==30,"Fuente V5 inesperada")
    require(digest(encode(d))==BASE_SHA,"Active.json no coincide con Stereo Madness V5. No se sobrescribe.")
    old=d["objects"]
    templates={i:next(o for o in old if o["id"]==i) for i in (8,39,40)}
    def removed(o):
        x,y=o["xGD"],o["yGD"]
        return (o["kind"]=="hazard" and (7995<x<12555 or 22935<x<26565)) or (o["kind"]=="solid" and (x,y) in LOWER)
    out=[copy.deepcopy(o) for o in old if not removed(o)]
    last=max(o["index"] for o in old)
    idx=last+1
    def add(ident,x,y,rot=0):
        nonlocal idx
        obj=copy.deepcopy(templates[ident])
        obj.update(index=idx,xGD=float(x),yGD=float(y),rotationGD=float(rot),
                   flipX=False,flipY=False,raw={"1":str(ident),"2":str(x),"3":str(y),"6":str(rot)})
        idx+=1
        out.append(obj)
    for x,y in EXTEND: add(40,x,y)
    for rows,begin,end,floor,ceiling in [(BALL,7995,12555,90,240),(SPIDER,22935,26565,90,210)]:
        for x,side,ident,n in rows:
            height=templates[ident]["heightGD"]
            # V5 Ball floor steps down at x=8865. Determine actual support per spike.
            for j in range(n):
                xx=x+j*30
                ff=120 if begin==7995 and xx<8865 else floor
                add(ident,xx,ceiling-height/2 if side else ff+height/2,180 if side else 0)
    for x,n,y in MIDDLE:
        for j in range(n): add(40,x+j*30,y)
    out.sort(key=lambda o:(o["xGD"],o["index"]))
    require(len({o["index"] for o in out})==len(out),"Indices duplicados")
    require(len({(o["xGD"],o["yGD"],o["id"]) for o in out if o["index"]>last})==idx-last-1,"Agregados duplicados")
    require([o for o in old if o["kind"]=="coin"]==[o for o in out if o["kind"]=="coin"],"Monedas cambiadas")
    require([o for o in old if o["kind"].endswith("Portal")]==[o for o in out if o["kind"].endswith("Portal")],"Portales cambiados")
    require(all(o in out for o in old if not removed(o)),"Objeto no seleccionado alterado")
    return dict(d,objects=out)

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--proyecto",required=True,type=Path)
    p.add_argument("--verificar",action="store_true")
    p.add_argument("--restaurar",action="store_true")
    a=p.parse_args()
    root=a.proyecto.resolve()
    path=root/REL
    require((root/"ProjectSettings/ProjectVersion.txt").is_file(),"No parece proyecto Unity")
    raw=path.read_bytes()
    backup=root/"Builds/BiofitV6/Active_V5.json"
    if a.restaurar:
        old=backup.read_bytes()
        require(digest(encode(json.loads(old)))==BASE_SHA,"Respaldo V5 invalido")
        require(digest(encode(json.loads(raw)))==digest(encode(transform(json.loads(old)))),"El nivel actual no es la V6 esperada")
        if not a.verificar: atomic(path,old)
        print("OK RESTAURACION V5"+(" (sin escritura)" if a.verificar else ""))
        return
    d=json.loads(raw)
    out=transform(d)
    # Verify all resources exist and the original remains untouched.
    original=json.loads((path.parent/"Original.json").read_bytes())
    require(original["sourceSha256"]==SOURCE_SHA and len(original["objects"])==2291,"Original V5 invalido")
    require(d["endGD"]==out["endGD"]==original["endGD"],"Meta alterada")
    sprites=path.parents[2]/"Sprites"
    for frame in {f for o in out["objects"] for f in o["frames"]}:
        require((sprites/(frame+".png")).is_file(),"Falta sprite: "+frame)
    print("V5:",len(d["objects"]),"V6:",len(out["objects"]),"monedas: 3")
    print("SHA256 V6:",digest(encode(out)))
    if a.verificar:
        print("OK PREVUELO V6 - sin escritura")
        return
    backup.parent.mkdir(parents=True,exist_ok=True)
    if backup.exists(): require(digest(encode(json.loads(backup.read_bytes())))==BASE_SHA,"Respaldo existente no valido")
    else: atomic(backup,raw)
    atomic(path,encode(out))
    print("OK V6 LOCAL - solo Stereo Madness; BLE, cooldown, fisicas y niveles 2-5 intactos.")

def atomic(path,data):
    fd,tmp=tempfile.mkstemp(prefix=path.name+".",suffix=".tmp",dir=path.parent)
    try:
        with os.fdopen(fd,"wb") as f: f.write(data)
        os.replace(tmp,path)
    finally:
        if os.path.exists(tmp): os.unlink(tmp)

if __name__=="__main__":
    main()
