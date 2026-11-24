"""Rollback explícito de V5. Unity cerrado. No cambia Git."""
import argparse,json,shutil
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--proyecto',required=True,type=Path);p.add_argument('--backup',required=True,type=Path);a=p.parse_args();root=a.proyecto.resolve();b=a.backup.resolve()
if b.parent!=root/'Builds' or not b.name.startswith('CampaignV5_backup_'):raise SystemExit('Backup V5 invalido')
m=json.loads((b/'manifest.json').read_text());new=set(m['new']);paths=[]
for n in m['files']:
 q=(root/n).resolve()
 if not q.is_relative_to(root/'Assets'):raise SystemExit('Ruta fuera de Assets')
 if n not in new and not (b/n).is_file():raise SystemExit('Falta respaldo '+n)
 paths.append((n,q))
for n,q in paths:
 if n in new:
  q.unlink(missing_ok=True);Path(str(q)+'.meta').unlink(missing_ok=True)
 else:shutil.copy2(b/n,q)
print('Version anterior restaurada. Abrir Unity y verificar.')
for rel in ['Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5','Assets/BIOFIT/Resources/Songs/Musics/BiofitLocalV5']:
 d=root/rel
 if d.exists():
  for x in sorted(d.rglob('*'),key=lambda p:len(p.parts),reverse=True):
   if x.is_file() and x.suffix=='.meta':x.unlink()
   elif x.is_dir() and not any(x.iterdir()):x.rmdir()
  if not any(d.iterdir()):d.rmdir();Path(str(d)+'.meta').unlink(missing_ok=True)
