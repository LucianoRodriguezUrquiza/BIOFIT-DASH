using System;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
class Program {
 static int checks;static void Check(bool b,string s){if(!b)throw new Exception(s);checks++;}
 static BiofitDashCore Game(){var g=new BiofitDashCore{PracticeMode=true};g.StartNewGame();return g;}
 static void Step(BiofitDashCore g,int frames,int fps=60){for(int i=0;i<frames;i++)g.Step(1f/fps);}
 static bool Pair(BiofitLiftPairDetector d,double t){Check(!d.Observe(1,t,.2f),"isolated onset waits");return d.Observe(2,t+.02,.2f);}
 static void Replay(string file) {
  var d=new BiofitBilateralJumpDetector();var flags=new Dictionary<string,bool>();var rejected=new HashSet<string>();
  int accepted=0,expected=0,cooldown=0,fast=0,eligible=0,both=0;
  var culture=CultureInfo.InvariantCulture;bool mode=false;
  foreach(string line in File.ReadLines(file)) {
   var c=line.Split(';');if(c.Length<15)continue;
   if(c[0]=="DIAG" && c[4].StartsWith("RECHAZO_")){var sq=c[4].Split(' ').First(x=>x.StartsWith("seq="));rejected.Add(c[3]+":"+sq.Substring(4));}
   if(c[0]=="DIAG" && c[4].StartsWith("BILATERAL_DECISION=")){
    var sq=c[4].Split(' ').First(x=>x.StartsWith("seq="));flags[c[3]+":"+sq.Substring(4)]=c[4].Contains("phaseHint=1");mode=c[4].Contains("phaseMode=1");
   }
   if(c[0]!="EV" || c[4]!="UP_IMPULSE")continue;
   if(c[9]=="1")expected++;
   byte foot=byte.Parse(c[3]);double time=double.Parse(c[12],culture);float up=float.Parse(c[8],culture);string key=c[3]+":"+c[6];
   if(rejected.Remove(key))continue;
   bool hint=flags.TryGetValue(key,out bool value)&&value;
   // Inspect pending real production state before processing, for paired hint count only.
   var pending=(IList)typeof(BiofitBilateralJumpDetector).GetField(foot==1?"pendingRight":"pendingLeft",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(d);
   double gap=double.PositiveInfinity;bool other=false;
   foreach(var p in pending){double t=(double)p.GetType().GetField("Time").GetValue(p);if(Math.Abs(t-time)<gap){gap=Math.Abs(t-time);other=(bool)p.GetType().GetField("PhaseEvidence").GetValue(p);}}
   bool ok=d.ProcessImpulse(foot,time,up,hint,mode);if(ok){accepted++;if(d.LastPairUsedFastPhase)fast++;}
   if(d.LastDecision==BiofitBilateralJumpDetector.Decision.PairCooldown)cooldown++;
   if(d.LastDecision==BiofitBilateralJumpDetector.Decision.PairCooldown || ok){if(hint&&other)both++;if(d.LastPairIntervalSeconds>=.6&&d.LastPairIntervalSeconds<.9&&hint&&other)eligible++;}
  }
  Check(accepted==expected,"exact real-event pair replay matches stored pair column");
  Console.WriteLine($"REPLAY {Path.GetFileName(file)} accepted={accepted} cooldown={cooldown} fast={fast} matchedBothHints={both} bothHintsAt600to900ms={eligible}");
 }
 static void CadenceGrid(){
  foreach(int count in new[]{5,10,30})foreach(double period in new[]{.6,.8,.9,1.0,1.2})foreach(int fps in new[]{30,60,120}) {
   var g=Game();var d=new BiofitLiftPairDetector();var b=new BiofitJumpBuffer();int sent=0,paired=0,buffered=0,airborne=0,executed=0,expired=0;
   for(int f=0;f<((count-1)*period+2)*fps;f++) {
    double t=100+f/(double)fps;g.Step(1f/fps);
    var done=b.Tick(g,t);if(done.State==BiofitJumpBuffer.Completion.Executed)executed++;if(done.State==BiofitJumpBuffer.Completion.Expired)expired++;
    if(sent<count && t>=100+sent*period+.07) { // opposite onset +20ms, transport +50ms
     double onset=100+sent*period;sent++;d.Observe(1,onset,.2f);
     if(d.Observe(2,onset+.02,.2f)) {paired++;var result=b.Submit(g,t,0,"LIFT_CYCLE",sent,true,.3,d.LastPairSeconds);
      if(result==BiofitDashCore.JumpResult.Buffered)buffered++;if(result==BiofitDashCore.JumpResult.Airborne)airborne++;
     }
    }
   }
   Check(paired==count,"all synthetic onset pairs pass 600ms guard");
   Check(g.SuccessfulJumps<=count&&!b.Pending,"never invent or retain commands after cadence run");
   if(period>=.8)Check(g.SuccessfulJumps==count,"natural cadence yields every action with real physics");
   if(count==30)Console.WriteLine($"CYCLE period={period:F1} fps={fps} paired={paired} actions={g.SuccessfulJumps} buffered={buffered} executed={executed} airborne={airborne} expired={expired}");
  }
 }
 static void Main(string[] args){
  CadenceGrid();
  var d=new BiofitLiftPairDetector();var g=Game();for(int n=0;n<5;n++){if(n>0)Step(g,48);Check(Pair(d,100+n*.8)&&g.Jump(),"5 separate onset pairs ->5 actions");}
  Check(g.SuccessfulJumps==5,"5 commands no extras");
  Check(!d.Observe(2,103.22,.2f),"duplicate timestamp rejected");
  var r=new BiofitLiftPairDetector();Check(!r.Observe(2,11.10,.2f)&&r.Observe(1,11,.2f),"cross-connection reorder works");
  Check(!Pair(r,11.5)&&r.LastDecision==BiofitLiftPairDetector.Decision.TooSoon,"new channel still rate guarded");
  Check(Pair(r,11.8),"rejected pair cannot extend guard");
  r.Clear();Check(!r.Observe(2,12.6,.2f),"disconnect clears pending");
  Check(!r.Observe(1,13,.2f)&&r.LastDecision==BiofitLiftPairDetector.Decision.OutsideWindow,"missing opposite not fabricated");
  Check(r.Observe(2,13.01,.2f),"fresh pair after loss recovers");
  Check(!r.Observe(1,14,.119f)&&r.LastDecision==BiofitLiftPairDetector.Decision.BelowThreshold,"threshold unchanged");
  foreach(double window in new[]{.18,.25,.3}) {
   g=Game();g.Jump();Step(g,33);var b=new BiofitJumpBuffer();
   var result=b.Submit(g,10.55,0,"LIFT_CYCLE",2,true,window,10.50);
   Console.WriteLine($"buffer window={window} candidateAge=50ms gameElapsed=550ms => {result}");
   Check(result==(window<.3?BiofitDashCore.JumpResult.Airborne:BiofitDashCore.JumpResult.Buffered),"window comparison on real physics");
   if(result==BiofitDashCore.JumpResult.Buffered){
    Check(b.Submit(g,10.56,0,"LIFT_CYCLE",3,true,window,10.56)==BiofitDashCore.JumpResult.AlreadyBuffered,"no stacking or deadline extension");
    int executions=0;for(int f=1;f<=30;f++){g.Step(1f/60);if(b.Tick(g,10.55+f/60.0).State==BiofitJumpBuffer.Completion.Executed)executions++;}
    Check(executions==1&&g.SuccessfulJumps==2&&!b.Pending,"one execution at landing");
   }
  }
  g=Game();g.Jump();Step(g,38);var buffer=new BiofitJumpBuffer();
  Check(buffer.Submit(g,20.64,0,"LIFT_CYCLE",1,true,.25,20.60)==BiofitDashCore.JumpResult.Buffered,"late-descent slot accepted");
  Step(g,20);Check(buffer.Tick(g,20.851).State==BiofitJumpBuffer.Completion.Expired&&g.SuccessfulJumps==1,"expired origin deadline never executed");
  Check(buffer.Submit(g,21,0,"LIFT_CYCLE",2,true,.25,20)==BiofitDashCore.JumpResult.InputExpired,"old input cannot jump even when grounded");
  Check(buffer.Submit(g,21,0,"LIFT_CYCLE",2,true,.25,double.PositiveInfinity)==BiofitDashCore.JumpResult.InputExpired,"invalid event origin cannot receive a new TTL");
  g=Game();g.Jump();Step(g,38);buffer.Submit(g,30.64,0,"LIFT_CYCLE",1,true,.25,30.6);g.TogglePause();Check(buffer.Tick(g,30.65).State==BiofitJumpBuffer.Completion.Cancelled,"pause cancels");
  foreach(string path in args)Replay(path);
  Console.WriteLine($"PASS {checks} lift-pair/buffer checks, not human classifications");
 }
}
