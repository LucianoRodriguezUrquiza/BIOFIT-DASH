"""Read original local ZIP extract; validate bytes, footers and sequence gaps.
Usage: python3 tests/JumpControl/audit_csv.py /path/to/extracted-zip
No CSV is copied into the repository. Output is aggregate evidence, not accuracy.
"""
import csv, hashlib, json, re, sys
from pathlib import Path
root = Path(sys.argv[1]); manifest = json.loads((root/'MANIFEST_SHA256.json').read_text())
assert manifest['csv_count'] == 14 and len(manifest['data']) == 14
summary=[]
for entry in manifest['data']:
    p=root/'CSV'/entry['file']; b=p.read_bytes()
    assert len(b)==entry['size_bytes'] and hashlib.sha256(b).hexdigest()==entry['sha256_file'], p
    body, end=b.rsplit(b'# END_BIOFIT sha256=',1)
    assert hashlib.sha256(body).hexdigest()==end.decode().strip(), p
    lines=body.decode('utf-8-sig').splitlines()
    rows=list(csv.DictReader((x for x in lines if not x.startswith('#')),delimiter=';'))
    assert int(re.search(r'# filas_total=(\d+); finalizada=1',body.decode())[1])==len(rows),p
    ev=[r for r in rows if r['tipo']=='EV']; raw=[r for r in rows if r['tipo']=='RAW']
    assert int(re.search(r'# filas_eventos=(\d+)',body.decode())[1])==len(ev),p
    assert 'descartadas_por_limite=0' in body.decode(),p
    out={'id':p.stem,'rows':len(rows),'events':len(ev)}
    if raw:
        last={}; gaps=0; disorder=0
        for r in raw:
            k=r['tracker_id']; s=int(r['secuencia'])
            if k in last:
                delta=(s-last[k])%65536
                if delta==0 or delta>=32768: disorder+=1
                else: gaps+=delta-1
            last[k]=s
        out.update(raw=len(raw),gaps=gaps,duplicate_or_reordered=disorder)
    else:
        accepted=[r for r in rows if r['tipo']=='APP' and r['evento'] in ('SALTO_BLE_REALIZADO','SALTO_SOLO_REALIZADO')]
        times=[float(r['reloj_unity_procesado_s']) for r in accepted]
        out.update(jumps=len(times),jump_times=times,intervals=[round(y-x,3) for x,y in zip(times,times[1:])],
                   up=sum(r['evento']=='UP_IMPULSE' for r in ev),
                   stale=sum('RECHAZO_ANTIGUO' in r['evento'] for r in rows))
    summary.append(out)
print(json.dumps(summary,indent=2))
print('PASS: 14 original files: manifest, SHA footer, counts, finalized, zero storage drops')
