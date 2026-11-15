using System;
// Only editor dialog/log shims. Production algorithms are linked unchanged.
namespace UnityEditor {
 [AttributeUsage(AttributeTargets.Method)] public sealed class MenuItem:Attribute { public MenuItem(string s) {} }
 public static class EditorUtility { public static bool DisplayDialog(string t,string m,string b) { Console.WriteLine(t+": "+m); if(t.Contains("ERROR")||t.Contains("FALLIDA")) throw new Exception(m); return true; } }
}
namespace UnityEngine { public static class Debug { public static void LogException(Exception e) { throw e; } public static void LogError(object e) { throw new Exception(e.ToString()); } } }
class Program {
 static int checks;
 static void Check(bool value,string reason) { if(!value) throw new Exception(reason); checks++; }
 static BiofitClockAligner Anchors(uint a=100000,uint b=500000) {
  var c=new BiofitClockAligner();
  for(int i=0;i<12;i++) { c.ObserveStatus(1,unchecked(a+(uint)i*100),1000+i*.1+.02); c.ObserveStatus(2,unchecked(b+(uint)i*100),1000+i*.1+.03); }
  return c;
 }
 static void Main() {
  BiofitVerificarDash.Verificar(); BiofitVerificarInputEngine.Verificar();
  var c=Anchors(); double mapped;
  Check(c.TryMapEvent(1,101100,1001.14,out mapped),"fresh mapped");
  Check(BiofitEventGate.Rejection(true,true,1001.14,mapped,10,3)=="","fresh accepted");
  Check(c.TryMapEvent(1,100100,1001.60,out mapped),"old estimate retained");
  Check(BiofitEventGate.Rejection(true,true,1001.60,mapped,4,2)=="ANTIGUO","1500ms cannot jump");
  Check(BiofitEventGate.Rejection(true,false,2,2,1,0)=="SIN_RELOJ","unknown clock fail closed");
  Check(BiofitEventGate.Rejection(true,true,2,2,251,0)=="ANTIGUO","Unity stalled");
  Check(BiofitEventGate.Rejection(true,true,2,2,2,1500)=="ANTIGUO","firmware queue independently stale");
  var seq=new BiofitEventGate();
  Check(seq.ObserveSequence(65534)&&seq.ObserveSequence(65535)&&seq.ObserveSequence(0),"sequence wrap");
  Check(!seq.ObserveSequence(0)&&!seq.ObserveSequence(65535),"duplicate and reorder rejected");
  Check(seq.ObserveSequence(3)&&seq.Missing==2,"gap counted, not attributed to radio");
  seq.Reset(); Check(seq.ObserveSequence(1),"reconnection sequence reset");
  c.Reset(1); Check(!c.IsReady(1001.2),"disconnect invalidates bilateral");
  Check(!c.TryMapEvent(1,101100,1001.2,out mapped),"disconnected clock unavailable");
  Check(c.TryMapEvent(2,501100,1001.2,out mapped),"other tracker clock survives");
  c=Anchors(); c.ObserveStatus(1,15,1001.2);
  Check(c.LeftSamples==1&&!c.IsReady(1001.2),"reboot clears anchors");
  c=Anchors(uint.MaxValue-500,500000);
  Check(c.IsReady(1001.15)&&c.TryMapEvent(1,599,1001.15,out mapped)&&Math.Abs(mapped-1001.12)<.002,"millis rollover mapped continuously");
  Check(!c.IsReady(1007),"status expiry");
  var dup=new BiofitClockAligner(); for(int i=0;i<10;i++) dup.ObserveStatus(1,10,1+i*.01);
  Check(dup.LeftSamples==1,"duplicate STATUS not anchors");
  // Real compressed intervals: sensor 782ms, callback 12ms.
  c=Anchors(606000,900000); double first,second;
  Check(c.TryMapEvent(1,606954,1002,out first)&&c.TryMapEvent(1,607736,1002.012,out second),"real burst maps");
  c.TryMapEvent(1,607736,1002.012,out second);
  Check(Math.Abs(second-first-.782)<.0001,"retain 782ms generation spacing");
  Check(BiofitEventGate.Rejection(true,true,1002,first,0,0)=="ANTIGUO","first compressed event rejected");
  var pairs=new BiofitBilateralJumpDetector();
  Check(!pairs.ProcessImpulse(2,10.011,.362f)&&pairs.ProcessImpulse(1,10,.263f),"11ms reverse arrival pair");
  Check(!pairs.ProcessImpulse(1,10.05,.3f),"rebound");
  pairs.ClearPending(); Check(!pairs.ProcessImpulse(2,11,.4f),"disconnect cannot reuse opposite candidate");
  var csv=new BiofitEventCapture(); csv.BeginGame(0);
  csv.RecordDiagnostic(1,1,"RECHAZO_ANTIGUO seq=9 fwQueueMs=1500");
  csv.Record(1,1,3,9,100,.2f,.3f,false,2,1002,1000.5,true,1500);
  Check(csv.EventRows==1&&csv.Csv.Contains("RECHAZO_ANTIGUO"),"old event retained with reason");
  foreach(var line in csv.Csv.Split('\n')) if(line.Length>0&&line[0]!='#') Check(line.TrimEnd('\r').Split(';').Length==15,"CSV column count");
  for(int i=0;i<1600;i++) csv.RecordDiagnostic(1,1,"TX");
  Check(csv.DroppedRows>0,"diagnostic overflow accounted");
  Console.WriteLine("PASS: "+checks+" additional latency checks. Synthetic host tests; no Unity/Android/hardware build.");
 }
}
