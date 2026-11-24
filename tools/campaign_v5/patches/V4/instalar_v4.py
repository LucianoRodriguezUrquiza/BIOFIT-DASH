#!/usr/bin/env python3
"""DIPL: V3 -> V4, source levels and sprites read only from user's local installation."""
import argparse, base64, collections, gzip, hashlib, io, json, plistlib, re, sys, zipfile
from pathlib import Path
from datetime import datetime
from PIL import Image
BASE=Path(__file__).resolve().parent
NAMES=('BiofitStereoLocalRuntime.cs','BiofitStereoSolidContact.cs','BiofitStereoPractice.cs')
V3_HASHES={'BiofitStereoLocalRuntime.cs': '6b0b6478bff3a1e88cb4655f1f7d7fb417f5fd721df04eea63252152a7b138a9', 'BiofitStereoSolidContact.cs': '1075aede3b5c8bdd24d036791e3f730b4877796c84026668ad03bdfca3cb1013', 'BiofitStereoPractice.cs': 'e75ff1a5d1c02ee1117dac6740f4566c68a2530c6e007a4e8f004c2e1a75ad31'}
DONOR_HASHES={'9': 'ccaa09c05af5808d5235ac666af1e00b58e4299121fe2ccb5dd8e5bece0e79ff', '21': '0f95f73c72e885353b9709b9e298512491faecfceb4d7f8756aebef05b97795f'}
SOURCE='baac8024b41f90e5d08e519f4b51b423e2038853aecb3ce59c6e428d1b49af12'
# Each donor row is an actual contiguous sequence of spikes in a mode section.
# (id, x first, y, rotation, count, destination offset from portal).
BALL=((39,9495,126,0,6,600),(39,9855,114,0,3,1200),(8,12795,135,0,5,1800),(8,13155,135,0,4,2400),(39,12135,234,180,10,3000),(8,13335,75,0,3,3600))
SPIDER=((8,10875,105,0,7,450),(8,11265,105,0,4,1050),(8,12135,105,0,6,1650),(8,12735,345,180,6,2250),(8,12675,105,0,4,2850))

def read(p):return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
def sha(b):return hashlib.sha256(b).hexdigest()
def one(s,a,b):
    if s.count(a)!=1:raise ValueError('Firma V3 inesperada: '+a[:100])
    return s.replace(a,b,1)
def decode(path):
    raw=path.read_bytes().strip();s=gzip.decompress(base64.urlsafe_b64decode(raw+b'='*((-len(raw))%4))).decode()
    return [dict(zip(t[::2],t[1::2])) for p in s.split(';')[1:] if p for t in [p.split(',')]]
def donor_pattern(rows,donor,start,end):
    items={(int(o['1']),float(o['2']),float(o['3']),float(o.get('6','0'))%360) for o in donor}
    for ident,x,y,rot,count,offset in rows:
        if not all(start<x+i*30<end and (ident,x+i*30,y,rot) in items for i in range(count)):
            raise ValueError('Fila donante no encontrada: '+str((ident,x,y,rot,count)))
    return rows

def make_hybrid(original,cycles,finger):
    assert original['sourceSha256']==SOURCE and len(original['objects'])==2291
    donor_pattern(BALL,cycles,9075,13485);donor_pattern(SPIDER,finger,10695,13551)
    src=original['objects']; templates={}
    for o in src:templates.setdefault(o['id'],o)
    out=[]; index=2291
    def add(ident,x,y,rotation=0,kind=None,frames=None):
        nonlocal index
        template=templates[ident] if ident in templates else templates[13]
        a={**template,'index':index,'id':ident,'xGD':x,'yGD':y,'rotationGD':rotation,'flipX':False,'flipY':False}
        index+=1
        if kind:a['kind']=kind
        if frames:a['frames']=frames
        out.append(a);return a
    for o in src:
        x=o['xGD'];k=o['kind'];
        if k=='shipPortal':continue
        if o['id']==142 and x>22935:continue
        # Preserve approach, original return-to-Cube and all color/effect events.
        if (7995<x<12555 or x>22935) and k not in ('backgroundColor','groundColor','enterEffect'):continue
        out.append(o.copy())
    # Entry floors & return blocks at boundaries preserved, no artificial ship roof.
    add(47,7995,165,kind='ballPortal',frames=['portal_07_back','portal_07_front'])
    add(1331,22995,149,kind='spiderPortal',frames=['portal_17_back','portal_17_front'])['heightGD']=84.5
    def corridor(start,end,floor,ceil,rows):
        # exact 30-GD grid; two continuous rows of actual solid tiles plus decorative backing.
        for x in range(start+30,end,30):
            add(1,x,floor-15);add(1,x,ceil+15)
            for y in range(15,floor-15,30):add(5,x,y)
            add(5,x,ceil+45)
        for i,(ident,source_x,source_y,source_rot,count,offset) in enumerate(rows):
            # Adapt donor row to alternating supporting surface. Do not import its triggers/orbs.
            upper=i%2==1;height=templates[ident]['heightGD'];y=ceil-height/2 if upper else floor+height/2
            for j in range(count):add(ident,start+offset+j*30,y,180 if upper else 0)
    corridor(7995,12555,120,240,BALL)
    corridor(22935,26565,90,270,SPIDER)
    # Third original secret coin is moved vertically to the Spider ceiling route, original X retained.
    add(142,26325,255)
    out.sort(key=lambda o:(o['xGD'],o['index']))
    h={**original,'objects':out}
    validate(original,h)
    return h

def validate(original,h):
    out=h['objects'];k=collections.Counter(o['kind'] for o in out)
    if k['ballPortal']!=1 or k['spiderPortal']!=1 or k['cubePortal']!=1 or k['shipPortal'] or k['coin']!=3:raise ValueError('Secuencia V4 invalida')
    retained={o['index']:o for o in out if o['index']<2291}
    for o in original['objects']:
        x=o['xGD']
        if x<7995 or 12555<=x<22935:
            if o!=retained.get(o['index']):raise ValueError('Tramo Cube modificado: '+str(o['index']))
    assert len({o['index'] for o in out})==len(out)
    # Conservative timing model (6.5 unity/s * 1.5 timeScale), NOT Unity gameplay proof.
    for start,rows,transfer in [(7995,BALL,3.5),(22935,SPIDER,0.0)]:
        prev_end=0
        for _,_,_,_,n,offset in rows:
            action=offset/30-6
            assert action-prev_end>=transfer+1.0,(start,action,prev_end)
            prev_end=offset/30+n-1
        times=[(r[-1]/30-6)/9.75 for r in rows]
        assert min(b-a for a,b in zip(times,times[1:]))>.9
    return k

def portal_pngs(zpath):
    result={};wanted=[f'portal_{n}_{side}' for n in ('07','17') for side in ('back','front')]
    with zipfile.ZipFile(zpath) as z:
        name='GJ_GameSheet02-hd.plist';pl=plistlib.loads(z.read(name));im=Image.open(io.BytesIO(z.read(name.replace('.plist','.png')))).convert('RGBA')
        def nums(s):return list(map(int,re.findall(r'-?\d+',s)))
        for frame in wanted:
            v=pl['frames'][frame+'_001.png'];x,y,w,h=nums(v['textureRect']);r=v['textureRotated'];sw,sh=nums(v['spriteSourceSize']);ox,oy=nums(v['spriteOffset']);
            crop=im.crop((x,y,x+(h if r else w),y+(w if r else h)))
            if r:crop=crop.transpose(Image.Transpose.ROTATE_90)
            assert crop.size==(w,h)
            full=Image.new('RGBA',(sw,sh));full.paste(crop,(round((sw-w)/2+ox),round((sh-h)/2-oy)));b=io.BytesIO();full.save(b,format='PNG');result[frame]=b.getvalue()
    return result

def patch_modes(s):
    if 'BIOFIT V3 portal:' not in s or 'RestorePractice(' not in s:raise ValueError('Motor V3 faltante')
    s=one(s,'        private const int GroundMask = 1 << 3;','''        private const int GroundMask = 1 << 3;
        private int ActiveGroundMask => BiofitStereoPractice.Available ? GroundMask | (1 << 6) : GroundMask;
        public bool StereoInverted => flipped;
        public void RestoreStereoGravity(bool inverted)
        {
            if (!BiofitStereoPractice.Available || !initialized || (Mode != BiofitMode.Ball && Mode != BiofitMode.Spider)) return;
            flipped = inverted;
            body.gravityScale = flipped ? -BallSpiderGravity : BallSpiderGravity;
            if (Mode == BiofitMode.Spider) icon.flipY = flipped;
        }''')
    s=one(s,'direction, SurfaceRay, GroundMask)','direction, SurfaceRay, ActiveGroundMask)')
    s=one(s,'body.position, destinationDirection, 16f, GroundMask)','body.position, destinationDirection, 16f, ActiveGroundMask)')
    s=one(s,'            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);\n            body.gravityScale = newMode', '''            // Keep the incoming jump momentum at the local mode portal; do not edit Cube physics.
            if (!(BiofitStereoPractice.Available && (newMode == BiofitMode.Ball || newMode == BiofitMode.Spider)))
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
            body.gravityScale = newMode''')
    return s

def plan(root,gd,resources):
    geome=root/'Assets/BIOFIT/GeomeTry';local=root/'Assets/BIOFIT/Resources/BiofitStereoLocal'
    if (local/'HybridV4.json').exists():raise ValueError('V4 ya instalada; no se sobreescribe.')
    original=json.loads(read(local/'Objects.json'))
    donors={}
    for n in ('9','21'):
        p=gd/'levels'/(n+'.txt')
        if sha(p.read_bytes().strip())!=DONOR_HASHES[n]:raise ValueError('Nivel donante distinto del archivo analizado: '+str(p))
        donors[n]=decode(p)
    hybrid=make_hybrid(original,donors['9'],donors['21'])
    out={}
    for n in NAMES:
        p=geome/n
        if sha(read(p).encode())!=V3_HASHES[n]:raise ValueError('Archivo V3 inesperado: '+str(p))
        out[p]=(BASE/n).read_bytes()
    modes=geome/'BiofitThreeModes.cs';ux=geome/'BiofitGeometryUX.cs'
    out[modes]=patch_modes(read(modes)).encode()
    out[ux]=one(read(ux),'"CUBO / NAVE", "BOLA"','"TRES MODOS", "BOLA"').encode()
    out[local/'HybridV4.json']=json.dumps(hybrid,separators=(',',':')).encode()
    for name,data in portal_pngs(resources).items():
        p=local/'Sprites'/(name+'.png')
        if p.exists():raise ValueError('Textura V4 ya existe: '+str(p))
        out[p]=data
    report={'original_objects':2291,'active_objects':len(hybrid['objects']),'classes':dict(collections.Counter(o['kind'] for o in hybrid['objects'])),'donors':['Cycles 9','Fingerdash 21'],'cube_unchanged':True,'ship_portals_level_1':0,'geometry_test':'static only'}
    return out,report

def apply(root,out):
    old={p:p.read_bytes() if p.exists() else None for p in out}
    backup=root/'Builds'/('StereoV4_backup_'+datetime.now().strftime('%Y%m%d_%H%M%S_%f'));backup.mkdir(parents=True,exist_ok=False)
    for p,b in old.items():
        if b is not None:q=backup/p.relative_to(root);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
    (backup/'manifest.json').write_text(json.dumps({'files':[str(p.relative_to(root)) for p in out],'new':[str(p.relative_to(root)) for p,b in old.items() if b is None]},indent=2),encoding='utf-8')
    try:
        for p,b in out.items():p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        for p,b in out.items():
            if p.read_bytes()!=b:raise IOError('Verificacion fallida: '+str(p))
    except BaseException:
        for p,b in old.items():
            if b is None:p.unlink(missing_ok=True)
            else:p.write_bytes(b)
        raise
    return backup

def main():
    a=argparse.ArgumentParser();a.add_argument('--proyecto',type=Path,required=True);a.add_argument('--gd',type=Path,required=True,help='Carpeta Resources original');a.add_argument('--recursos',type=Path,required=True,help='ZIP Stereo Madness anterior');a.add_argument('--verificar',action='store_true');args=a.parse_args()
    root=args.proyecto.resolve();out,report=plan(root,args.gd,args.recursos)
    print(json.dumps(report,indent=2))
    if args.verificar:print('OK V3 compatible, sin escritura.');return
    backup=apply(root,out);print('OK V4 LOCAL - Nivel 1 Cube/Ball/Cube/Spider.');print('Backup: '+str(backup));print('No se modifico firmware, BLE, cooldown, mapas 2-5 ni GitHub.')
if __name__=='__main__':
    try:main()
    except Exception as e:print('ERROR V4: '+str(e),file=sys.stderr);sys.exit(1)
