"""Offline installation and data checks; not a Unity runtime or compilation test."""
import hashlib, json, shutil, tempfile, unittest
from pathlib import Path
from instalar_v3 import plan,apply,read,patch_character,ADD,REMOVE,BASE,V2

class TestV3(unittest.TestCase):
    def test_ratio(self):
        for w,h in [(146,96),(93,96)]:
            f=min(150/w,86/h)
            self.assertAlmostEqual(w*f/(h*f),w/h)
            self.assertLessEqual(w*f,150.0001)
            self.assertLessEqual(h*f,86.0001)
    def test_patch_character(self):
        source='public class Character { void F(){ if(Input.GetMouseButton(0)) {} Camera c; } }'
        copied=source.replace('Camera','GeometryCamera')
        result=patch_character(copied,source)
        self.assertIn('BlocksGameplayPointer()',result)
        self.assertEqual(result.count('Input.GetMouseButton(0)'),1)
        with self.assertRaises(ValueError):patch_character(copied+'unexpected change',source)
    def test_source_guards(self):
        contact=read(BASE/'BiofitStereoSolidContact.cs')
        self.assertIn('if(top && (!ship || shipEntrySupport)) return;',contact)
        self.assertIn('if(ship) { Kill(',contact)
        practice=read(BASE/'BiofitStereoPractice.cs')
        self.assertIn('if(automatic && (motor.Mode!=BiofitMode.Cube',practice)
        self.assertIn('h.GetComponent<DeathZone>()!=null',practice)
        self.assertIn('CHECKPOINT ELIMINADO',practice)
        button=read(BASE/'BiofitStereoPracticeButton.cs')
        self.assertIn('Mathf.Min(maxSize.x/art.width,maxSize.y/art.height)',button)
        self.assertIn('lastPressFrame==Time.frameCount',button)
        self.assertIn('TouchPhase.Began',button)

if __name__=='__main__':unittest.main()
