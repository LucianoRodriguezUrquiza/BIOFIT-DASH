import argparse, base64, collections, gzip, hashlib, io, json, plistlib, re, sys, zipfile
from pathlib import Path
from datetime import datetime

V3_HASHES={'BiofitStereoLocalRuntime.cs': '6b0b6478bff3a1e88cb4655f1f7d7fb417f5fd721df04eea63252152a7b138a9', 'BiofitStereoSolidContact.cs': '1075aede3b5c8bdd24d036791e3f730b4877796c84026668ad03bdfca3cb1013', 'BiofitStereoPractice.cs': 'e75ff1a5d1c02ee1117dac6740f4566c68a2530c6e007a4e8f004c2e1a75ad31'}


DONOR_HASHES={'9': 'ccaa09c05af5808d5235ac666af1e00b58e4299121fe2ccb5dd8e5bece0e79ff', '21': '0f95f73c72e885353b9709b9e298512491faecfceb4d7f8756aebef05b97795f'}


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

