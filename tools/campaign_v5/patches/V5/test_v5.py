"""Host-side checks. Neither this file nor the installer executes Unity."""
import argparse, ast, tempfile, unittest, subprocess, collections
from pathlib import Path
from instalar_v5 import *

def fixture_for(fn):
    # Strict literal anchors were separately compared to the pinned remote source.
    # This fixture exercises the actual transformation, not a full C# compile.
    t=ast.parse((BASE/'instalar_v5.py').read_text());f=next(n for n in t.body if isinstance(n,ast.FunctionDef) and n.name==fn)
    anchors=[]
    for n in ast.walk(f):
        if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='one' and isinstance(n.args[1],ast.Constant):anchors.append(n.args[1].value)
    return '\n\n'.join(dict.fromkeys(anchors))
class Checks(unittest.TestCase):
 def test_motor_anchors(self):
    s=fixture_for('patch_motor');s+='\nActiveGroundMask';p=patch_motor(s)
    self.assertIn('BiofitMode.InvertedCube',p);self.assertIn('BiofitStereoLocalRuntime.IsLocal(level)',p)
    self.assertIn('TryInvertedOrb',p);self.assertIn('motor.SetMode(BiofitMode.Cube)',p)
 def test_selector_anchors(self):
    s=fixture_for('patch_ux')+'\n"CUBO / NAVE", "BOLA", "SPIDER", "TRES MODOS", "TRES MODOS"'
    p=patch_ux(s);self.assertIn('CAN\'T LET GO',p);self.assertIn('POLARGEIST',p)
 def test_data(self):
    out,report=generate(GD,Path('local'),Path('music'))
    self.assertEqual(sum(r['source'] for r in report),9884);self.assertEqual(sum(len(r['sections']) for r in report),7)
    self.assertEqual(sum(r['active'] for r in report),11852)
    for r in report:
      path=Path('local')/'CampaignV5'/str(r['slot']);orig=json.loads(out[path/'Original.json']);act=json.loads(out[path/'Active.json'])
      self.assertEqual(sum(o['kind']=='coin' for o in act['objects']),3)
      self.assertFalse(any(o['kind']=='shipPortal' for o in act['objects']))
      self.assertEqual(len(orig['objects']),r['source'])
      self.assertTrue(all(s['nominal_action_spacing_s']>.9 for s in r['sections']))
      # Conservative motion estimate for alternating rows, no actual Unity collision simulation.
      for s in r['sections']:
       gap=(s['ceiling']-min(s['entry_floor'],s['exit_floor']))/30
       distance=6.5*math.sqrt(2*max(0,gap-1)/39.24)
       self.assertLess(distance,6)
    self.assertEqual(len([p for p in out if p.suffix=='.mp3']),5)
 def test_runtime_static(self):
    s=read(BASE/'BiofitStereoLocalRuntime.cs');self.assertIn('Campaign(slot)',s);self.assertIn('BiofitV5Interaction',s);self.assertIn('original.endGD',s);self.assertIn('level.Id <= 5',s)
    s=read(BASE/'BiofitStereoPractice.cs');self.assertIn('BiofitStereoLocalRuntime.IsLocal',s);self.assertIn('RestoreStereoGravity',s)
 def test_transaction(self):
    with tempfile.TemporaryDirectory() as d:
     root=Path(d);subprocess.run(['git','init','-q',str(root)],check=True);p=root/'Assets/a.cs';q=root/'Assets/b.json';p.parent.mkdir();p.write_bytes(b'prior')
     b=install(root,{p:b'next',q:b'{}'});self.assertEqual((b/'Assets/a.cs').read_bytes(),b'prior');self.assertEqual(p.read_bytes(),b'next');self.assertTrue(q.is_file());self.assertIn('BiofitLocalV5/',(root/'.git/info/exclude').read_text())
     subprocess.run([sys.executable,str(BASE/'restaurar_v5.py'),'--proyecto',str(root),'--backup',str(b)],check=True)
     self.assertEqual(p.read_bytes(),b'prior');self.assertFalse(q.exists())
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--gd',required=True,type=Path);a=p.parse_args();GD=a.gd;unittest.main(argv=[sys.argv[0]],verbosity=2)
