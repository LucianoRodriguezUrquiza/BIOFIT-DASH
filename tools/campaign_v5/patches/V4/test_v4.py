"""Pruebas estáticas y de instalador; no sustituye Play Mode/Android."""
import argparse, json, tempfile, unittest, shutil
from pathlib import Path
from instalar_v4 import *
class Tests(unittest.TestCase):
    def test_patch_motor(self):
        s='BIOFIT V3 portal: RestorePractice(\n        private const int GroundMask = 1 << 3;\n'+'direction, SurfaceRay, GroundMask)\nbody.position, destinationDirection, 16f, GroundMask)\n'+'            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);\n            body.gravityScale = newMode'
        p=patch_modes(s);self.assertIn('ActiveGroundMask',p);self.assertIn('RestoreStereoGravity',p);self.assertIn('GroundMask | (1 << 6)',p)
        with self.assertRaises(ValueError):patch_modes(p)
    def test_install_and_backup(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);p=root/'old.cs';q=root/'new.json';p.write_bytes(b'old');b=apply(root,{p:b'new',q:b'{}'});self.assertEqual((b/'old.cs').read_bytes(),b'old');self.assertEqual(p.read_bytes(),b'new');self.assertEqual(q.read_bytes(),b'{}');self.assertEqual(json.loads((b/'manifest.json').read_text())['new'],['new.json'])
    def test_runtime_guards(self):
        s=read(BASE/'BiofitStereoLocalRuntime.cs');self.assertIn('ValidateHybrid(original)',s);self.assertIn('BiofitMode.Ball',s);self.assertIn('BiofitMode.Spider',s);self.assertNotIn('AddRoof(7995',s)
        c=read(BASE/'BiofitStereoSolidContact.cs');self.assertIn('reverseMode && (top || bottom)',c);self.assertIn('side && overlap>.08f',c)
        p=read(BASE/'BiofitStereoPractice.cs');self.assertIn('inverted=motor.StereoInverted',p);self.assertIn('motor.RestoreStereoGravity(cp.inverted)',p)
if __name__=='__main__':unittest.main()
