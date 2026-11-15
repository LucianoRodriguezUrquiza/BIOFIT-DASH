using System;
using System.Reflection;
using System.Diagnostics;
class Program {
 static int checks;
 static void Check(bool v,string s) { if(!v)throw new Exception(s); checks++; }
 static long Ticks(double t)=>(long)(t*Stopwatch.Frequency);
 static byte[] Packet(byte id,bool status,ushort seq,uint ms) {
  var b=new byte[status?20:16]; b[0]=2;b[1]=id;b[2]=3;b[3]=0x80;
  BitConverter.GetBytes(seq).CopyTo(b,4); BitConverter.GetBytes(ms).CopyTo(b,6);
  if(!status)BitConverter.GetBytes((short)400).CopyTo(b,12);
  return b;
 }
 static object Wrap(byte[] bytes,double t,bool status=false) {
  var type=typeof(BiofitBLEMonitor).GetNestedType("Packet",BindingFlags.NonPublic);
  var p=Activator.CreateInstance(type);
  type.GetField("expectedId").SetValue(p,bytes[1]);type.GetField("bytes").SetValue(p,bytes);
  type.GetField("callbackTicks").SetValue(p,Ticks(t));type.GetField("isStatus").SetValue(p,status);return p;
 }
 static void Read(BiofitBLEMonitor m,byte[] b,double t,bool status=false) {
  typeof(BiofitBLEMonitor).GetMethod(status?"ReadStatus":"ReadEvent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,new[]{Wrap(b,t,status)});
 }
 static void Main() {
  var m=new BiofitBLEMonitor(); int solo=0,pairs=0;
  m.AnkleUpCandidate+=(a,b,c)=>solo++;m.BilateralJumpCandidate+=g=>pairs++;
  Check(m.StartGameCapture(),"capture starts");
  double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
  for(int i=0;i<12;i++) { Read(m,Packet(1,true,(ushort)i,(uint)(100000+100*i)),now-1.1+i*.1,true);Read(m,Packet(2,true,(ushort)i,(uint)(500000+100*i)),now-1.1+i*.1,true); }
  Read(m,Packet(1,false,1,99600),now);Read(m,Packet(2,false,1,499600),now);
  Check(solo==0&&pairs==0&&m.CapturedEventCount==2,"stale batch logged, no gameplay");
  Read(m,Packet(1,false,2,101100),now);Read(m,Packet(2,false,2,501111),now);
  Check(solo==2&&pairs==1,"fresh reverse-clock pair acts");
  Read(m,Packet(1,false,2,101100),now);
  Check(solo==2&&pairs==1&&m.RejectedEvents==3,"duplicate cannot act");
  // A callback held >250ms inside Unity cannot act even with a recent sensor time.
  Read(m,Packet(1,false,3,100100),now-1);
  Check(solo==2&&m.RejectedEvents==4,"Unity stalled packet rejected");
  Check(m.StopGameCapture()&&m.CopyGameCapture(),"export finishes");
  Check(UnityEngine.GUIUtility.systemCopyBuffer.Contains("RECHAZO_ANTIGUO")&&UnityEngine.GUIUtility.systemCopyBuffer.Contains("RECHAZO_SECUENCIA"),"CSV keeps rejection reasons");
  var cold=new BiofitBLEMonitor();cold.AnkleUpCandidate+=(a,b,c)=>solo++;
  Read(cold,Packet(1,false,1,1),now);
  Check(solo==2&&cold.RejectedEvents==1,"no STATUS cannot jump");
  // Actual bounded callback queue + disconnect epoch invalidation in Update().
  var q=new BiofitBLEMonitor(); int qActions=0; q.AnkleUpCandidate+=(a,b,c)=>qActions++;
  var enqueue=typeof(BiofitBLEMonitor).GetMethod("Enqueue",BindingFlags.Instance|BindingFlags.NonPublic);
  var update=typeof(BiofitBLEMonitor).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
  for(int i=0;i<1100;i++) enqueue.Invoke(q,new[]{Wrap(Packet(1,false,(ushort)i,100),now)});
  Check((int)typeof(BiofitBLEMonitor).GetField("pendingCount",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(q)==1024,"queue bounded");
  Check((int)typeof(BiofitBLEMonitor).GetField("callbackDropped",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(q)==76,"queue overflow counted");
  var reset=Wrap(Packet(1,false,1,0),now);reset.GetType().GetField("reset").SetValue(reset,true);
  enqueue.Invoke(q,new[]{reset});
  for(int i=0;i<12;i++)update.Invoke(q,null);
  Check(qActions==0,"unsynchronized queue cannot jump");
  Check((int)typeof(BiofitBLEMonitor).GetField("pendingCount",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(q)==0,"queue drained including disconnect");
  Console.WriteLine("PASS: "+checks+" receiver integration checks using API shims (not a Unity build).");
 }
}
