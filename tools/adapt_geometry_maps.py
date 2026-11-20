"""Deterministic, auditable cube adaptations; never overwrite source maps.

Original layouts contain flight and mid-air repeated presses incompatible with
legacy 900 ms. Each source interval is mapped to one bounded cube challenge.
This is a rhythm/geometry adaptation, NOT a faithful commercial level port.
"""
import collections
import hashlib
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ThirdParty/GeomeTry/SourceMaps'
DEST = ROOT / 'Assets/BIOFIT/Geometry/Resources/BiofitGeometry/Levels'
CATALOG = [('StereoMadness', 16), ('BackOnTrack', 12), ('Polargeist', 12), ('JumpyNightmare', 6)]

def adapt(name, number, span):
    raw = (SOURCE / (name + '.json')).read_bytes()
    data = json.loads(raw)
    groups = collections.defaultdict(list)
    for i, item in enumerate(data['map']):
        groups[item['x'] // span].append((i, item))
    obstacles, sections = [], []
    x = 8.0
    previous_bucket = None
    for bucket, entries in sorted(groups.items()):
        if previous_bucket is not None:
            x += max(7.8, min(15.6, (bucket - previous_bucket) * 7.8))
        previous_bucket = bucket
        kinds = [v['type'] for _, v in entries]
        spikes = sum('spike' in k.lower() for k in kinds)
        has_pad = any('Pad' in k for k in kinds)
        has_orb = any('Orb' in k for k in kinds)
        flight = any('Portal' in k for k in kinds)
        # Preserve motif/order; cap dimensions so ONE ground jump clears it.
        kind = 'spike' if spikes or has_orb or flight else 'block'
        width = round(.7 + min(2, spikes // 3) * .35, 3) if kind == 'spike' else 1.2
        height = .65 if kind == 'spike' else .85
        def add(kind, px, py, w, h):
            obstacles.append(dict(id=len(obstacles)+1,kind=kind,x=round(px,3),y=py,width=w,height=h))
        add(kind,x,height/2,width,height)
        if has_pad:
            add('pad',x-1.6,.08,.4,.16)
        if has_orb:
            # Optional bonus, never required to cross the adapted hazard.
            add('orb',x+.6,2.15,.65,.65)
        reasons = ['source interval condensed; ascending platforms -> bounded ground challenge',
                   'spacing >= 1.2 real seconds']
        if flight: reasons.append('ship/cube portal -> cube section; no held input')
        if has_orb: reasons.append('orb chain -> optional single-use orb; no mandatory aerial press')
        if has_pad: reasons.append('automatic pad retained with impulse 13.5; dense pad chains condensed')
        sections.append(dict(sourceStart=bucket*span,sourceEnd=(bucket+1)*span-1,
                             sourceIndices=[i for i,_ in entries],jumpX=round(x-2.25,3),
                             automaticPad=has_pad,adaptation='; '.join(reasons)))
    return dict(id=number,title=data['name']+' · BIOFIT',sourceFile=name+'.json',
                sourceSha256=hashlib.sha256(raw).hexdigest(),finishX=round(x+12,3),
                obstacles=obstacles,sections=sections)

def main():
    check = '--check' in sys.argv
    for number,(name,span) in enumerate(CATALOG,1):
        d=adapt(name,number,span)
        expected=json.dumps(d,ensure_ascii=False,indent=2)+'\n'
        path=DEST/(name+'.json')
        if check:
            assert path.read_text()==expected, 'Regenerate '+str(path)
        else:
            DEST.mkdir(parents=True,exist_ok=True);path.write_text(expected)
        coverage=sorted(i for s in d['sections'] for i in s['sourceIndices'])
        assert coverage==list(range(len(json.loads((SOURCE/(name+'.json')).read_text())['map'])))
        print(name, len(d['sections']),'sections,',len(d['obstacles']),'objects,',round((d['finishX']+6)/6.5,1),'s; all source items mapped')
if __name__=='__main__': main()
