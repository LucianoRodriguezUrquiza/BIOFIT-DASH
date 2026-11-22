using System;
using System.Reflection;
using System.Diagnostics;
using Biofit.GeomeTryOriginal;
using UnityEngine;
class Verify
{
 static BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
 static void Check(bool x,string label){if(!x)throw new Exception("FAIL "+label);checks++;}
 static double Now=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
 static BiofitGeomeTryLegacyBridge bridge;static BiofitBLEMonitor monitor;static Cube latest;
 static Cube Spawn()
 {
  var c=new Cube();c.gameObject.Attach(c);c.gameObject.name="CubePrefab(Clone)";
  foreach(var pair in new (string,object)[]{("rb",new Rigidbody2D()),("spriteRenderer",new SpriteRenderer()),("_particleSystem",new ParticleSystem()),("animator",new Animator()),("deathSfx",new AudioSource())})typeof(Character).GetField(pair.Item1,Flags).SetValue(c,pair.Item2);
  Call(c,"Start");latest=c;return c;
 }
 static void SeedClock()
 {
  var align=(BiofitClockAligner)typeof(BiofitBLEMonitor).GetField("timeAligner",Flags).GetValue(monitor);double now=Now;
  for(byte f=1;f<=2;f++)for(int k=0;k<8;k++)align.ObserveStatus(f,(uint)(1000+k*100),now-.7+k*.1);
  foreach(string side in new[]{"left","right"}){var t=typeof(BiofitBLEMonitor).GetField(side,Flags).GetValue(monitor);t.GetType().GetField("connection").SetValue(t,"CONECTADO - NOTIFICACIONES ACTIVAS");}
 }
 static uint ms=1000;
 static void Motion()
 {
  ms+=10;double now=Now;
  Call(bridge,"OnMotionStatus",(byte)1,(byte)0x8d,ms,now);
  Call(bridge,"OnMotionStatus",(byte)2,(byte)0x8d,ms,now);
 }
 static Rigidbody2D Body(Cube c)=>(Rigidbody2D)typeof(Character).GetField("rb",Flags).GetValue(c);
 static void Frame(Cube c,bool grounded)
 {Physics2D.Grounded=grounded;Call(bridge,"Update");Call(c,"Update");Call(bridge,"LateUpdate");}
 static void Main()
 {
  var root=new GameObject("bridge");monitor=new BiofitBLEMonitor();root.Attach(monitor);bridge=new BiofitGeomeTryLegacyBridge();root.Attach(bridge);Call(bridge,"OnEnable");
  var tilemap=new UnityEngine.Tilemaps.Tilemap();
  UnityEngine.Object.Factory=(a,p,q,t)=>Spawn().gameObject;
  SeedClock();var c=Spawn();Motion();Frame(c,true);
  Check(Body(c).linearVelocity.y==13.5f && bridge.ExecutedJumps==1,"virtual enters ORIGINAL Character.Update -> Cube.Jump");
  Body(c).linearVelocity=new Vector2(6.5f,3);Frame(c,false);
  Check(Body(c).linearVelocity.y==3,"no mid-air velocity change");
  Motion();Frame(c,true);Check(bridge.ExecutedJumps==2,"held input jumps at next grounded callback");
  Frame(c,true);Check(bridge.ExecutedJumps==2,"same grounded episode not counted twice");
  // Physical control still works when virtual entry fails closed.
  GameManager.Instance.PlayMode=false;Call(bridge,"Update");Input.Space=true;Body(c).linearVelocity=Vector2.zero;
  Frame(c,true);Check(Body(c).linearVelocity.y==13.5f,"native keyboard branch unchanged");Input.Space=false;
  GameManager.Instance.PlayMode=true;Call(bridge,"Update");Motion();
  foreach(int cycles in new[]{10,20,50,100})
  {
   for(int n=0;n<cycles;n++)
   {
    SeedClock();Motion();Frame(c,false);Frame(c,true);
    var old=c;int oldId=old.GetInstanceID();old.Die();
    Check(!BiofitGeomeTryLegacyBridge.ReadHeldInput(old),"dead instance cannot read virtual input");
    // Execute the ORIGINAL respawn body. Engine instantiate/destroy are shims.
    typeof(Character).GetMethod("Respawn",Flags).Invoke(old,null);c=latest;
    Check(c.GetInstanceID()!=oldId && bridge.CurrentActorId==c.GetInstanceID(),"new instance registered");
    UnityEngine.Object.FlushDestroyed(); // old OnDestroy AFTER new Start
    Check(bridge.CurrentActorId==c.GetInstanceID(),"late destroy does not erase new instance");
    Frame(c,true);Check(BiofitGeomeTryLegacyBridge.ReadHeldInput(c),"motion survives respawn "+n);
   }
  }
  Check(bridge.DeathCount==180 && bridge.SpawnCount==181,"exact lifecycle counters, no hidden resets");
  // Subscription lifetime is independent of avatars. Enable/disable 100 times.
  for(int n=0;n<100;n++){Call(bridge,"OnDisable");Call(bridge,"OnEnable");}
  foreach(string name in new[]{"MotionStatusReceived","MotionTransitionReceived","MotionEpochReset","BilateralJumpCandidate"})
  {var d=(Delegate)typeof(BiofitBLEMonitor).GetField(name,Flags).GetValue(monitor);Check(d.GetInvocationList().Length==1,"one listener "+name);}
  Motion();Frame(c,true);Check(BiofitGeomeTryLegacyBridge.ReadHeldInput(c),"reenabled bridge rebinds an already alive avatar");
  Call(bridge,"OnDisable");
  foreach(string name in new[]{"MotionStatusReceived","MotionTransitionReceived","MotionEpochReset","BilateralJumpCandidate"})
   Check(typeof(BiofitBLEMonitor).GetField(name,Flags).GetValue(monitor)==null,"unsubscribe "+name);
  Check(!BiofitGeomeTryLegacyBridge.ReadHeldInput(c),"disabled bridge releases static input");
  Console.WriteLine("PASS: "+checks+" actual bridge/Character/Cube source checks using ENGINE SHIMS, not Unity PlayMode");
 }
}
