#!/usr/bin/env python3
"""Read complete BIOFIT CSVs; verify bytes/row counts; never infer human labels.
python3 tools/analyze_control_sessions.py --main A.csv B.csv --raw A_RAW.csv B_RAW.csv
Optional --raw-tsv-dir /tmp/replay creates 6-axis inputs for firmware run_replay.py.
"""
import argparse, bisect, collections, csv, hashlib, io, json, math, re
from pathlib import Path

def load(path):
    data=path.read_bytes(); marker=b'# END_BIOFIT sha256='
    assert data.count(marker)==1, f'{path.name}: missing/duplicate integrity footer'
    body,footer=data.split(marker)
    assert hashlib.sha256(body).hexdigest()==footer.strip().decode(), f'{path.name}: SHA mismatch'
    text=body.decode('utf-8-sig'); meta=re.search(r'# filas_total=(\d+); finalizada=1',text)
    assert meta, f'{path.name}: session incomplete'
    lines=[line for line in text.splitlines() if not line.startswith('#')]
    rows=list(csv.DictReader(lines,delimiter=';'))
    assert len(rows)==int(meta[1]) and all(None not in r for r in rows), f'{path.name}: row mismatch'
    return rows, {'file':path.name,'rows':len(rows),'body_sha256':footer.strip().decode(),'complete':True}

def stats(xs):
    xs=sorted(xs)
    if not xs:return None
    def q(p):
        t=(len(xs)-1)*p;i=int(t);return xs[i]+(xs[min(i+1,len(xs)-1)]-xs[i])*(t-i)
    return {'n':len(xs),'median':round(q(.5),3),'p95':round(q(.95),3),'max':round(xs[-1],3)}

def values(text,key):
    m=re.search(r'(?:^| )'+key+r'=([^ ]+)',text)
    return m[1] if m else None

def main_summary(rows):
    diag=[r for r in rows if r['tipo']=='DIAG']; ev=[r for r in rows if r['tipo']=='EV'];up=[r for r in ev if r['evento']=='UP_IMPULSE']
    decisions=collections.Counter(r['evento'].split()[0].split('=')[1] for r in diag if r['evento'].startswith('BILATERAL_DECISION='))
    commands=[r['evento'] for r in diag if r['evento'].startswith('JUMP_COMMAND ')]
    cooldown=[r['evento'] for r in diag if r['evento'].startswith('BILATERAL_DECISION=PairCooldown')]
    return {'events':len(ev),'up_by_foot':dict(collections.Counter(r['tracker_id'] for r in up)),
      'paired_EV':sum(r['par_bilateral']=='1' for r in ev),'decisions':dict(decisions),
      'command_results':dict(collections.Counter(values(x,'result') for x in commands)),
      'buffer_executions':sum(r['evento'].startswith('JUMP_BUFFER_Executed') for r in diag),
      'fast_pairs':sum(r['evento'].startswith('PHASE_FAST_ACCEPTED') for r in diag),
      'phase_hint_by_foot':dict(collections.Counter(r['tracker_id'] for r in diag if values(r['evento'],'phaseHint')=='1')),
      'cooldown_600_to_900_ms':sum(.6<=float(values(x,'intervalS'))<.9 for x in cooldown),
      'rejections':sum(r['evento'].startswith('RECHAZO_') for r in diag),
      'up_unity_queue_ms':stats([float(r['espera_cola_unity_ms']) for r in up]),
      'up_relative_age_ms':stats([float(r['demora_relativa_estimacion_ms']) for r in up]),
      'decision_to_simulation_state_ms':stats([float(values(r['evento'],'decisionToFrameMs')) for r in diag if r['evento'].startswith('PRIMER_ESTADO_SALTO ')]),
      'clamped_frame_reports':sum(r['evento'].startswith('FRAME_CLAMP') for r in diag)}

def analyze(main_paths,raw_paths,tsv_dir=None):
    result={'warning':'Commands are not labelled human lifts. Relative age is not absolute BLE latency. RAW is decimated.'}
    mains={};outputs=[]
    for p in main_paths:
        rows,item=load(p);item.update(main_summary(rows));mains[p.name.split('_RAW')[0]]=rows
        marks=[r for r in rows if r['tipo']=='MARCA'];item['marks']=[{'stage':r['etapa'],'t':r['reloj_unity_procesado_s']} for r in marks]
        sync=[float(r['reloj_unity_procesado_s']) for r in marks if r['etapa'].startswith('SYNC')]
        item['blocks']=[main_summary([r for r in rows if r['reloj_unity_procesado_s'] and lo<=float(r['reloj_unity_procesado_s'])<hi]) for lo,hi in zip(sync,sync[1:])]
        outputs.append(item)
    result['main']=outputs;outputs=[]
    for p in raw_paths:
        rows,item=load(p);item['feet']={}
        for foot in ('1','2'):
            r=[x for x in rows if x['tipo']=='RAW' and x['tracker_id']==foot];seq=[int(x['secuencia']) for x in r];ts=[int(x['sensor_ms']) for x in r]
            steps=[(b-a)&65535 for a,b in zip(seq,seq[1:])]
            details={'rows':len(r),'sensor_hz':round((len(r)-1)*1000/((ts[-1]-ts[0])&0xffffffff),4),
                'missing':sum(n-1 for n in steps if 1<n<32768),'dup_or_reorder':sum(n==0 or n>=32768 for n in steps),
                'max_accel_norm_g':round(max(math.sqrt(sum(float(x[k])**2 for k in ('ax_g','ay_g','az_g'))) for x in r),3)}
            main=next((v for k,v in mains.items() if k.split('(')[0].replace('.csv','')==p.name.split('_RAW')[0]),None)
            if main:
                distances=[]
                for e in main:
                    if e['tipo']!='EV' or e['tracker_id']!=foot:continue
                    t=int(e['reloj_esp32_ms']);i=bisect.bisect_left(ts,t)
                    choices=ts[max(0,i-1):min(len(ts),i+1)]
                    if choices:distances.append(min(abs(t-x) for x in choices))
                details['nearest_RAW_to_EV_sensor_ms']=stats(distances)
            item['feet'][foot]=details
            if tsv_dir:
                tsv_dir.mkdir(parents=True,exist_ok=True)
                out=tsv_dir/(p.stem+'-'+foot+'.tsv')
                out.write_text(''.join('\t'.join([x['sensor_ms']]+[x[k] for k in ('ax_g','ay_g','az_g','gx_dps','gy_dps','gz_dps')])+'\n' for x in r))
        outputs.append(item)
    result['raw']=outputs
    return result
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--main',nargs='+',type=Path,required=True);p.add_argument('--raw',nargs='+',type=Path,required=True);p.add_argument('--raw-tsv-dir',type=Path)
    a=p.parse_args();print(json.dumps(analyze(a.main,a.raw,a.raw_tsv_dir),indent=2,ensure_ascii=False))
