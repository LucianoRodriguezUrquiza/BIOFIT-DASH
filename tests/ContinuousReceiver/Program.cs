using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using UniBLE;
class Verify
{
 static int checks;
 static BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Flags).Invoke(obj,args);
 static void Check(bool b,string text){if(!b)throw new Exception("FAIL "+text); checks++;}
 static double Now=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
 static byte[] Packet(byte id,bool status,ushort seq,uint ms,byte kind=1)
 {
  var b=new byte[status?20:16];b[0]=2;b[1]=id;b[2]=status?(byte)0x8d:kind;b[3]=0x80;
  BitConverter.GetBytes(seq).CopyTo(b,4);BitConverter.GetBytes(ms).CopyTo(b,6);
  if(status)BitConverter.GetBytes((ushort)224).CopyTo(b,16);
  return b;
 }
 static void Statuses(FakeAdapter f,uint ms=10000)
 {for(ushort i=0;i<8;i++){f.Left.Status.Callback(Packet(1,true,i,ms+(uint)i*100));f.Right.Status.Callback(Packet(2,true,i,ms+(uint)i*100));}}
 static async Task Main()
 {
  var adapter=new FakeAdapter();BleManager.Adapter=adapter;
  var m=new BiofitBLEMonitor();m.MotionLinkRecoveryEnabled=true;
  int statusEvents=0, resetEvents=0;m.MotionStatusReceived+=(a,b,c,d)=>statusEvents++;m.MotionEpochReset+=()=>resetEvents++;
  await (Task)Call(m,"ConnectBothAsync"); Statuses(adapter);Call(m,"Update");
  Check(m.BothTrackersConnected && m.ClockAlignmentReady,"both notification sessions and clocks ready");
  int before=adapter.Left.Connections;
  for(int n=0;n<100;n++)await (Task)Call(m,"ConnectBothAsync");
  Check(adapter.Left.Listeners==1 && adapter.Right.Listeners==1,"one connection listener after 100 connect calls");
  Check(adapter.Left.Connections==before,"healthy tracker not torn down by redundant connect");
  adapter.Left.RepeatConnected(); Check(m.BothTrackersConnected,"redundant native Connected cannot lose subscribed state");
  int acceptedStatus=statusEvents,epoch=m.ControlEpoch;
  adapter.Left.Status.Callback(Packet(1,true,7,10700));Call(m,"Update");
  Check(statusEvents==acceptedStatus,"duplicate STATUS not published");
  adapter.Left.Status.Callback(Packet(1,true,3,10300));Call(m,"Update");
  Check(m.ControlEpoch==epoch && m.ClockAlignmentReady,"old STATUS cannot falsely reboot clock");
  adapter.Left.Status.Callback(Packet(1,true,8,10700));Call(m,"Update");
  Check(m.GetMotionTelemetry(1,Now).StatusRejection=="imu_timestamp_stalled","frozen sample is diagnosed");
  Check(m.GetMotionTelemetry(1,Now).ImuHz==224,"actual IMU Hz retained");
  uint baseMs=20000;
  for(int cycle=1;cycle<=100;cycle++)
  {
   Action<byte[]> oldStatus=adapter.Left.Status.Callback;
   adapter.Left.Drop();
   typeof(BiofitBLEMonitor).GetField("nextRecoveryAt",Flags).SetValue(m,0d);
   Call(m,"Update"); await m.MotionRecoveryTask;
   Check(adapter.Left.Listeners==1,"one listener after reconnect "+cycle);
   oldStatus(Packet(1,true,9,10701)); // a late callback from the OLD generation
   int beforeOld=statusEvents; Call(m,"Update");
   Check(statusEvents==beforeOld,"old generation cannot refresh input "+cycle);
   for(ushort i=0;i<8;i++)adapter.Left.Status.Callback(Packet(1,true,i,baseMs+(uint)i*100));
   // The other link stays subscribed; advance its sequence and sample.
   adapter.Right.Status.Callback(Packet(2,true,(ushort)(20+cycle),baseMs+700));
   Call(m,"Update");
   Check(m.BothTrackersConnected && m.ClockAlignmentReady,"reconnect rearms from new status "+cycle);
   baseMs+=1000;
  }
  Check(adapter.Left.Status.Unsubscribes>=100 && adapter.Left.Events.Unsubscribes>=100,"obsolete notifications explicitly unsubscribed");
  Check(adapter.Right.Connections==1,"healthy right link survives 100 left recoveries");
  Check(resetEvents>=100,"reset causes are observable");
  Check(m.GetMotionTelemetry(1,Now).Recoveries==100,"recovery count is exact");
  adapter.Left.FailConnect=true; adapter.Left.Drop(); adapter.Right.Drop();
  for(int trial=0;trial<2;trial++)
  {
   typeof(BiofitBLEMonitor).GetField("nextRecoveryAt",Flags).SetValue(m,0d);
   Call(m,"Update"); await m.MotionRecoveryTask;
  }
  Check(adapter.Right.Connections>1 && adapter.Right.Connected,"unavailable left cannot starve right recovery");
  Check(adapter.Right.Listeners==1 && adapter.Left.Listeners==1,"failed connect also has bounded listeners");
  Call(m,"OnDestroy");
  Check(adapter.Left.Listeners==0 && adapter.Right.Listeners==0,"destroy removes native state listeners");
  Console.WriteLine("PASS: "+checks+" real receiver checks with fake GATT (not Android/BLE)");
 }
}
