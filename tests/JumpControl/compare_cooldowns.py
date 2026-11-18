"""Counterfactuals compile real production sources in a temporary project.
No source file in Assets is modified. Requires base commit in local Git history.
"""
import argparse, pathlib, subprocess, tempfile
parser=argparse.ArgumentParser();parser.add_argument('--dotnet',default='dotnet');args=parser.parse_args()
root=pathlib.Path(__file__).resolve().parents[2]
base='4006bcc50383aad22998682f0c20e3b84e01ec37'
path='Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs'
old=subprocess.check_output(['git','show',base+':'+path],cwd=root,text=True)
current=(root/path).read_text()
program='''using System;
class Program {
 static string Run(double cadence, bool secondary) {
  var d=new BiofitBilateralJumpDetector();string result="";
  for(int n=0;n<5;n++)for(int k=0;k<(secondary?2:1);k++) {
   double t=100+n*cadence+k*.7;d.ProcessImpulse(1,t,.25f);
   if(d.ProcessImpulse(2,t+.02,.25f))result+=(result.Length>0?",":"")+(t-100).ToString("F1",System.Globalization.CultureInfo.InvariantCulture);
  }return result;
 }
 static void Main(){Console.WriteLine(Run(1,true));Console.WriteLine(Run(.8,false));
  var d=new BiofitBilateralJumpDetector();d.ProcessImpulse(1,100,.25f);d.ProcessImpulse(2,100.02,.25f);
  d.ProcessImpulse(1,100.8,.25f);Console.WriteLine(d.ProcessImpulse(2,100.82,.25f));
 }
}'''
expected={
 'v0961': ['0.0,1.7,2.7,3.7,4.7','0.0,1.6,3.2','False'],
 'v097': ['0.0,1.0,2.0,3.0,4.0','0.0,1.6,3.2','False'],
 'counterfactual_750ms': ['0.0,1.0,2.0,3.0,4.0','0.0,0.8,1.6,2.4,3.2','True']}
for name,source in [('v0961',old),('v097',current),('counterfactual_750ms',current.replace('PairCooldownSeconds = 0.90f','PairCooldownSeconds = 0.75f'))]:
 with tempfile.TemporaryDirectory() as tmp:
  p=pathlib.Path(tmp)
  (p/'test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>')
  (p/'Detector.cs').write_text(source);(p/'Program.cs').write_text(program)
  r=subprocess.run([args.dotnet,'run','--project',str(p/'test.csproj')],capture_output=True,text=True,check=True)
  actual=r.stdout.strip().splitlines();assert actual==expected[name],(name,actual)
  print(name,': five rises at 1s + secondary at .7 =>',actual[0],'; clean .8s =>',actual[1],'; second pulse at .8 accepted =>',actual[2])
print('PASS: original and changed production source compiled; 750ms is an OFFLINE alternative only')
