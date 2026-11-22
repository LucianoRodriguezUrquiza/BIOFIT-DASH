"""Expected failures of the pinned v1 production code; host shims, NOT Unity/BLE."""
import argparse
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
v1 = "0d83449a5fa9257a642e123d1b160c553eaaf9d2"
p = argparse.ArgumentParser()
p.add_argument("--dotnet", default="dotnet")
args = p.parse_args()

def original(path):
    return subprocess.check_output(["git", "show", v1 + ":" + path], cwd=root, text=True)

def run(folder, includes, program):
    (folder / "Program.cs").write_text(program)
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    properties = ET.SubElement(project, "PropertyGroup")
    for key, val in {"OutputType": "Exe", "TargetFramework": "net8.0", "EnableDefaultCompileItems": "false"}.items():
        ET.SubElement(properties, key).text = val
    group = ET.SubElement(project, "ItemGroup")
    for path in [folder / "Program.cs"] + includes:
        ET.SubElement(group, "Compile", Include=str(path))
    ET.ElementTree(project).write(folder / "baseline.csproj")
    subprocess.run([args.dotnet, "run", "--project", str(folder / "baseline.csproj")], check=True)

with tempfile.TemporaryDirectory(prefix="biofit-v1-proof-") as temp:
    folder = Path(temp)
    state = folder / "State.cs"
    state.write_text(original("Assets/BIOFIT/GeomeTry/BiofitGeometryHeldMotion.cs"))
    run(folder, [state], '''using System;
using Biofit.GeomeTryOriginal;
class Proof {
 static void Expect(bool condition,string name) { if(!condition)throw new Exception(name); Console.WriteLine("REPRODUCED v1: "+name); }
 static void Main() {
  var m=new BiofitGeometryHeldMotion();
  m.ObserveStatus(1,0x8c,1,0);m.ObserveStatus(2,0x8c,1,0);
  m.ObserveEvent(1,true,10,.01);m.ObserveEvent(1,false,100,.10);m.ObserveEvent(2,true,120,.12);
  Expect(!m.Tick(.12,true,true,true),"recent bilateral activity cannot arm without MOV overlap");
  m=new BiofitGeometryHeldMotion();m.ObserveStatus(1,0x8d,1,0);m.ObserveStatus(2,0x8d,1,0);
  Expect(m.Tick(0,true,true,true),"initial healthy hold works");
  for(uint n=1;n<=6;n++)for(byte foot=1;foot<=2;foot++) {
   m.ObserveEvent(foot,true,n*100,n*.1);m.ObserveStatus(foot,0x8d,n*100,n*.1);
  }
  Expect(!m.Tick(.6,true,true,true),"EVENT before same-sample STATUS causes false stale release");
 }
}''')
    monitor = folder / "Monitor.cs"
    monitor.write_text(original("Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs"))
    project_path = root / "tests/ContinuousReceiver/ContinuousReceiver.csproj"
    includes = [monitor]
    for node in ET.parse(project_path).findall(".//Compile"):
        name = node.attrib["Include"]
        if name == "Program.cs" or "BiofitBLEMonitor" in name:
            continue
        includes.append((project_path.parent / name).resolve())
    run(folder, includes, '''using System;
using System.Reflection;
using System.Threading.Tasks;
using UniBLE;
class Proof {
 static object Call(object o,string method)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 static void Expect(bool condition,string name) { if(!condition)throw new Exception(name); Console.WriteLine("REPRODUCED v1: "+name); }
 static async Task Main() {
  var a=new FakeAdapter();BleManager.Adapter=a;var m=new BiofitBLEMonitor();
  await (Task)Call(m,"ConnectBothAsync");
  Expect(m.BothTrackersConnected,"initial GATT setup works");
  a.Left.RepeatConnected();
  Expect(!m.BothTrackersConnected,"redundant Connected callback removes subscribed readiness");
  for(int i=0;i<100;i++)await (Task)Call(m,"ConnectBothAsync");
  Expect(a.Left.Listeners==101 && a.Right.Listeners==101,"101 native listeners after 101 connects");
  int before=a.Left.Connections;a.Left.Drop();
  for(int frame=0;frame<1000;frame++)Call(m,"Update");
  Expect(a.Left.Connections==before && !a.Left.Connected,"Update never reconnects a dropped link");
 }
}''')
print("PASS: pinned v1 defects reproduced. Not proof of the cause of the user's Android failure.")
