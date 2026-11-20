"""Resource/GUID/provenance/protected-file contracts; no Unity rendering claim."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

root=Path(__file__).resolve().parents[1]
base='5616fc4571193d4a2d076550a909089344144384'
checks=0
def check(value,message):
    global checks
    checks+=1
    assert value,message

guids={}
for meta in (root/'Assets').rglob('*.meta'):
    match=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(),re.M)
    if not match:continue
    guid=match.group(1)
    check(guid not in guids,'Duplicate GUID '+str(meta))
    guids[guid]=meta
for p in (root/'Assets/BIOFIT/Geometry').rglob('*'):
    if p.suffix!='.meta':check(Path(str(p)+'.meta').exists(),'Missing meta '+str(p))
scene=(root/'Assets/BIOFIT/Geometry/Scenes/BiofitGeometry.unity').read_text()
scripts=re.findall(r'm_Script: \{fileID: 11500000, guid: ([a-f0-9]+), type: 3\}',scene)
check(len(scripts)==2,'Scene must have exactly one monitor and one game')
check({guids[g].name for g in scripts}=={'BiofitBLEMonitor.cs.meta','BiofitGeometryGame.cs.meta'},'Wrong scene components')
for number,name in enumerate(['StereoMadness','BackOnTrack','Polargeist','JumpyNightmare'],1):
    file=root/'Assets/BIOFIT/Geometry/Resources/BiofitGeometry/Levels'/(name+'.json')
    data=json.loads(file.read_text());raw=(root/'ThirdParty/GeomeTry/SourceMaps'/(name+'.json')).read_bytes()
    original_blobs=['0af72f78ccc80ad982fcb7aabc0c39387bb2350c','47e10ff7f5b48a8045e3ddcfdd20e1dce2c0deda','fe4af269c83b2984c643d6f3282799152ea84dc4','7c0b86d610daf6b79654b3edeacb5e412dcc2c80']
    check(hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==original_blobs[number-1],'Original GeomeTry blob byte-for-byte')
    check(data['id']==number,'Explicit unique internal ID')
    check(data['sourceSha256']==hashlib.sha256(raw).hexdigest(),'Source map integrity')
    check('TextScriptImporter:' in Path(str(file)+'.meta').read_text(),'JSON is TextAsset')
    coverage=sorted(i for section in data['sections'] for i in section['sourceIndices'])
    check(coverage==list(range(len(json.loads(raw)['map']))),'All source items mapped exactly once')
check(len(list((root/'Assets/BIOFIT/Geometry/Resources/BiofitGeometry/Levels').glob('*.json')))==4,'Exactly four levels')
# Identical bytes for existing scenes, settings, package manifest, native BLE,
# export and RAW code. The original detector only gains a read-only property.
protected=['Packages','ProjectSettings','Assets/Scenes','Assets/Plugins','Assets/BIOFIT/AndroidPlugins']
protected_files=['Assets/BIOFIT/Scripts/'+n+'.cs' for n in ['BiofitRawCapture','BiofitRawLink','BiofitSessionFile','BiofitCaptureArchive','BiofitAndroidExport','BiofitSingleFootDiagnostic','BiofitDashGame','BiofitDashCore','BiofitJumpBuffer','BiofitClockAligner','BiofitEventGate']]
names=subprocess.check_output(['git','ls-tree','-r','--name-only',base,'--']+protected+protected_files,cwd=root,text=True).splitlines()
for name in names:
    old=subprocess.check_output(['git','show',base+':'+name],cwd=root)
    check((root/name).read_bytes()==old,'Protected file changed: '+name)
detector=(root/'Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs').read_text()
baseline=subprocess.check_output(['git','show',base+':Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs'],cwd=root,text=True)
check(detector.replace('    // Read-only timestamp of the later impulse in the last accepted pair.\n    public double LastPairSeconds => lastPairSeconds;\n','')==baseline,'Legacy algorithm changed')
print('PASS',checks,'asset/provenance/preservation checks. Not Unity import/build.')
