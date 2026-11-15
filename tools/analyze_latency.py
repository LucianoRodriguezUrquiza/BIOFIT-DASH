"""python tools/analyze_latency.py registro.txt -- accepts summary + semicolon CSV."""
import csv, io, math, sys
from collections import Counter
from pathlib import Path

def percentile(values, p):
    a=sorted(values)
    if not a:return None
    i=(len(a)-1)*p; lo=int(i); hi=min(lo+1,len(a)-1)
    return a[lo]+(a[hi]-a[lo])*(i-lo)

def analyze(text):
    start=text.index('tipo;etapa;')
    rows=list(csv.DictReader(io.StringIO(text[start:]),delimiter=';'))
    ev=[r for r in rows if r['tipo']=='EV']
    result={}
    for key in ['espera_cola_unity_ms','demora_relativa_estimacion_ms']:
        values=[float(r[key]) for r in ev if r.get(key) and math.isfinite(float(r[key]))]
        result[key]={'n':len(values),'p50':percentile(values,.5),'p95':percentile(values,.95),'max':max(values) if values else None}
    result['rejections']=dict(Counter(r['evento'].split()[0] for r in rows if r['tipo']=='DIAG' and r.get('evento','').startswith('RECHAZO_')))
    result['events']=len(ev)
    result['note']='Demora relativa a STATUS, NO latencia BLE absoluta ni movimiento-a-pantalla. TX contiene maximos acumulados desde arranque.'
    return result

if __name__=='__main__':
    import json
    print(json.dumps(analyze(Path(sys.argv[1]).read_text(encoding='utf-8-sig')),indent=2,ensure_ascii=False))
