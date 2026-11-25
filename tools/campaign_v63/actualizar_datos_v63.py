#!/usr/bin/env python3
"""Rehidratar SOLO Active.json desde datos locales V5/V6/V6.2. No toca C#."""
import argparse, hashlib, importlib.util, json, os, tempfile
from pathlib import Path
HERE=Path(__file__).resolve().parent
FINAL='e64c3a5a6ef2e7e3234468189bfe38c944b36984b5c6c3818d029f411fff839b'
def compact(d):return json.dumps(d,ensure_ascii=False,separators=(',',':')).encode()
def sha(d):return hashlib.sha256(compact(d)).hexdigest()
def module(folder,name):
 spec=importlib.util.spec_from_file_location(name,HERE/'patches'/folder/(name+'.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--proyecto',type=Path,required=True);p.add_argument('--verificar',action='store_true');a=p.parse_args();r=a.proyecto.resolve()
 if not (r/'ProjectSettings/ProjectVersion.txt').is_file() or (r/'Temp/UnityLockfile').exists():raise ValueError('Proyecto incorrecto o Unity abierto')
 v6=module('V6','instalar_v6');v62=module('V62','instalar_v62');v63=module('V63','instalar_v63');f=r/v63.MAP;d=json.loads(f.read_bytes());initial=sha(d)
 original=json.loads((f.parent/'Original.json').read_bytes())
 if original['sourceSha256']!=v63.SOURCE or len(original['objects'])!=2291:raise ValueError('Original.json inesperado')
 for h,fn in [(v6.BASE_SHA,v6.transform),(v62.BASE,v62.transform),(v63.BASE,v63.transform)]:
  if sha(d)==h:d=fn(d)
 if sha(d)!=FINAL or len(d['objects'])!=3015:raise ValueError('Base desconocida o resultado distinto: '+sha(d))
 for frame in {x for o in d['objects'] for x in o['frames']}|{'square_05'}:
  if not (f.parents[2]/'Sprites'/(frame+'.png')).is_file():raise ValueError('Falta sprite '+frame)
 if not a.verificar and initial!=FINAL:
  fd,t=tempfile.mkstemp(dir=f.parent);os.close(fd)
  try:Path(t).write_bytes(compact(d));os.replace(t,f)
  finally:Path(t).unlink(missing_ok=True)
 print('OK datos V6.3: 3015 registros; '+('sin escritura' if a.verificar else 'mapa listo'))
if __name__=='__main__':main()
