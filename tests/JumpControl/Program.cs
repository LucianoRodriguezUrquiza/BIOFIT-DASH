using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
class Program
{
 static int checks;
 static void Check(bool ok,string why) { if(!ok)throw new Exception(why);checks++; }
 static bool Pair(BiofitBilateralJumpDetector d,double t,double gap=.02) {
  Check(!d.ProcessImpulse(1,t,.25f),"first ankle alone cannot command");
  return d.ProcessImpulse(2,t+gap,.25f);
 }
 static BiofitDashCore Game() { var g=new BiofitDashCore{PracticeMode=true};g.StartNewGame();return g; }
 static void Advance(BiofitDashCore g,double duration,int fps) {
  int frames=(int)Math.Round(duration*fps);
  for(int i=0;i<frames;i++)g.Step(1f/fps);
 }
 static void Cadence(double seconds,int fps,int expectedPairs,int expectedActions) {
  var d=new BiofitBilateralJumpDetector();var g=Game();int pairs=0,air=0;
  for(int n=0;n<5;n++) {
   if(n>0)Advance(g,seconds,fps);
   if(Pair(d,100+n*seconds)) {pairs++;if(g.TryJump()==BiofitDashCore.JumpResult.Airborne)air++;}
  }
  Check(pairs==expectedPairs,$"{seconds}s/{fps}fps detector {pairs}");
  Check(g.SuccessfulJumps==expectedActions,$"{seconds}s/{fps}fps actions {g.SuccessfulJumps}");
  Check(air==pairs-expectedActions,"airborne reason distinct");
  Check(d.PairCooldownRejected==5-pairs,"cooldown counts rejected complete pairs");
  Console.WriteLine($"cadence={seconds:F1}s fps={fps} pairs={pairs} cooldown={d.PairCooldownRejected} actions={g.SuccessfulJumps} airborne={air}");
 }
 static void Replay(string folder) {
  // Original rows in reception/processing order. Do not equate replay to human labels.
  foreach(var path in Directory.GetFiles(folder,"*.csv").Where(p=>!p.EndsWith("_RAW.csv")).OrderBy(p=>p)) {
   var d=new BiofitBilateralJumpDetector();int paired=0,recorded=0;
   var rejected=new HashSet<string>();
   foreach(var line in File.ReadLines(path)) {
    var c=line.Split(';');if(c.Length<15)continue;
    if(c[0]=="CONFIG"&&c[4].StartsWith("UP=")) {
     var v=c[4].Substring(3).Split(new[]{"g_PAIR="},StringSplitOptions.None);
     d.SetThresholds(float.Parse(v[0],CultureInfo.InvariantCulture),float.Parse(v[1].Replace("ms",""),CultureInfo.InvariantCulture)/1000f);
    }
    if(c[0]=="DIAG"&&c[4].StartsWith("RECHAZO_")) {
     var token=c[4].Split(' ').FirstOrDefault(x=>x.StartsWith("seq="));
     if(token!=null)rejected.Add(c[3]+":"+token.Substring(4));
    }
    if(c[0]!="EV"||c[4]!="UP_IMPULSE")continue;
    if(c[9]=="1")recorded++;
    if(rejected.Remove(c[3]+":"+c[6]))continue;
    if(d.ProcessImpulse(byte.Parse(c[3]),double.Parse(c[12],CultureInfo.InvariantCulture),float.Parse(c[8],CultureInfo.InvariantCulture)))paired++;
   }
   Check(paired==recorded,$"replay {Path.GetFileName(path)} paired={paired} recorded={recorded}");
   Console.WriteLine($"original replay {Path.GetFileName(path)} pairs={paired} cooldown={d.PairCooldownRejected}");
  }
 }
 static BiofitDashCore DescendingNearGround() {
  var g=Game();Check(g.Jump(),"setup: initial cube launch");
  for(int i=0;i<100 && g.EstimatedSecondsUntilLanding>.075;i++)
   g.Step(1f/60f);
  Check(g.JumpAltitude>0 && g.EstimatedSecondsUntilLanding>0 &&
        g.EstimatedSecondsUntilLanding<=.075,"real descending physics reaches 75ms before landing");
  return g;
 }
 static void CheckNew180msWindow() {
   var g=Game();
   Check(g.Jump(),"180ms window: first jump");
   for(int i=0;i<100 && g.EstimatedSecondsUntilLanding>.160d;i++)
    g.Step(1f/60f);
   double remaining=g.EstimatedSecondsUntilLanding;
   Check(g.JumpAltitude>0f && remaining>.120d && remaining<=.160d,
    "180ms window test targets actual 120-160ms remaining; v098 would reject");
   var b=new BiofitJumpBuffer();double t=10+g.Elapsed;
   Check(b.Submit(g,t,0,"BILATERAL",500,true)==BiofitDashCore.JumpResult.Buffered &&
         b.Pending,"new v099 admission in 120-180ms zone");
   BiofitJumpBuffer.Outcome state=default(BiofitJumpBuffer.Outcome);
   for(int frame=0;frame<20 && state.State==BiofitJumpBuffer.Completion.None;frame++){
    g.Step(1f/60f);
    state=b.Tick(g,t+(frame+1)/60d);
   }
   Check(state.State==BiofitJumpBuffer.Completion.Executed &&
         !b.Pending && g.SuccessfulJumps==2 && state.AgeMilliseconds<=180.1,
         "150ms-early second input executes once, not a stored queue");
  }
 static void CheckJumpBuffer() {
  var early=Game();Check(early.Jump(),"early setup");
  Advance(early,.30,60);
  var buffer=new BiofitJumpBuffer();
  Check(buffer.Submit(early,100+early.Elapsed,0,"BILATERAL",1,true)==
        BiofitDashCore.JumpResult.Airborne && !buffer.Pending,
        "too-early bilateral command is not queued");
  Check(buffer.Submit(early,100+early.Elapsed,0,"MANUAL",2,false)==
        BiofitDashCore.JumpResult.Airborne,
        "manual input never buffers, unchanged v097 behavior");
  var game=DescendingNearGround();buffer.Clear();
  double t=200+game.Elapsed;
  Check(buffer.Submit(game,t,0,"BILATERAL",3,true)==
        BiofitDashCore.JumpResult.Buffered && buffer.Pending,
        "one prelanding valid pair reserves at most 180ms");
  Check(buffer.Submit(game,t+.010,0,"BILATERAL",4,true)==
        BiofitDashCore.JumpResult.AlreadyBuffered,
        "second input cannot be stacked or extend expiry");
  Check(buffer.Tick(game,t+.011).State==BiofitJumpBuffer.Completion.None,
        "cannot execute while airborne");
  BiofitJumpBuffer.Outcome result=default(BiofitJumpBuffer.Outcome);
  for(int i=0;i<10 && result.State==BiofitJumpBuffer.Completion.None;i++){
   game.Step(1f/60f);
   result=buffer.Tick(game,t+.011+(i+1)/60d);
  }
  Check(result.State==BiofitJumpBuffer.Completion.Executed &&
        result.CommandId==3 && result.Source=="BILATERAL" &&
        result.AgeMilliseconds<=180.1 && !buffer.Pending &&
        game.SuccessfulJumps==2,
        "single buffered command executes once after landing in 180ms");
  Check(buffer.Tick(game,t+.2).State==BiofitJumpBuffer.Completion.None &&
        game.SuccessfulJumps==2,
        "no second execution after buffer consumption");

  game=DescendingNearGround();buffer.Clear();
  t=300+game.Elapsed;
  Check(buffer.Submit(game,t,0,"BILATERAL",5,true)==BiofitDashCore.JumpResult.Buffered,
        "stall setup");
  // Unity stalls beyond the real-time TTL; old signal may not trigger on landing.
  Advance(game,.100,60);
  result=buffer.Tick(game,t+.181);
  Check(result.State==BiofitJumpBuffer.Completion.Expired &&
        !buffer.Pending && game.SuccessfulJumps==1,
        "stalled callback expires: no delayed automatic jump");

  game=DescendingNearGround();buffer.Clear();t=400+game.Elapsed;
  Check(buffer.Submit(game,t,0,"BILATERAL",6,true)==BiofitDashCore.JumpResult.Buffered,
        "pause setup");
  game.TogglePause();result=buffer.Tick(game,t+.03);
  Check(result.State==BiofitJumpBuffer.Completion.Cancelled &&
        !buffer.Pending && game.SuccessfulJumps==1,
        "pause cancels pending command without execution");

  game=DescendingNearGround();buffer.Clear();t=500+game.Elapsed;
  Check(buffer.Submit(game,t,0,"BILATERAL",7,true)==BiofitDashCore.JumpResult.Buffered,
        "reset setup");
  buffer.Clear();game.StartNewGame();
  Check(!buffer.Pending && buffer.Tick(game,t+.03).State==BiofitJumpBuffer.Completion.None &&
        game.SuccessfulJumps==0,
        "restart cannot inherit old jump");

  // At 60FPS the cube lands around 0.77s, before the 0.90s detector
  // allows a new pair. Buffering alone cannot speed up this cooldown.
  game=Game();var d=new BiofitBilateralJumpDetector();
  Check(Pair(d,600),"initial bilateral pair");
  Check(game.Jump(),"first actual jump");
  Advance(game,.90,60);
  Check(Pair(d,600.95),"next eligible pair after 900ms");
  buffer.Clear();
  Check(buffer.Submit(game,600.95,0,"BILATERAL",8,true)==BiofitDashCore.JumpResult.Accepted &&
        !buffer.Pending,"at 900ms a normal 60FPS cube is already grounded: immediate jump");
 }
 static bool PhasePair(BiofitBilateralJumpDetector d, double t,
                       bool leftReady, bool rightReady, bool fastMode,
                       double gap=.015)
 {
   Check(!d.ProcessImpulse(1,t,.26f,leftReady,fastMode),"first phase ankle waits");
   return d.ProcessImpulse(2,t+gap,.26f,rightReady,fastMode);
 }
 static void CheckPhaseGatedControl() {
   // All v0.9.9 callers with 3 arguments have EXACTLY the old 900ms behavior.
   var old = new BiofitBilateralJumpDetector();
   Check(PhasePair(old,100,true,true,false),"legacy first pair");
   Check(!PhasePair(old,100.62,true,true,false),"legacy ignores flags and retains 900ms");
   Check(old.PairCooldownRejected==1,"old mode suppression reason");
   Check(PhasePair(old,101.03,false,false,false),"legacy pair after cooldown still works");

   var lower=new BiofitBilateralJumpDetector();
   Check(PhasePair(lower,50,true,true,true),"fast lower-bound initial pair");
   Check(!PhasePair(lower,50.55,true,true,true),
         "even two strong phase flags cannot bypass 600ms fast floor");
   Check(lower.LastDecision==BiofitBilateralJumpDetector.Decision.PairCooldown,
         "600ms floor produces explicit cooldown rejection");
   Check(PhasePair(lower,50.65,true,true,true),
         "rejected earlier phase candidate does not postpone genuine next cycle");

   var d=new BiofitBilateralJumpDetector();
   Check(PhasePair(d,10,true,true,true),"experimental initial pair");
   Check(!PhasePair(d,10.35,true,true,true),"even qualified phase cannot bypass 600ms");
   Check(d.LastDecision==BiofitBilateralJumpDetector.Decision.PairCooldown,
        "fast phase still has a 600ms safety floor");
   Check(!PhasePair(d,10.55,true,false,true),
        "single-ankle evidence cannot bypass 900ms");
   Check(d.PairCooldownRejected==2,"both disallowed candidates counted");
   Check(PhasePair(d,10.71,true,true,true),
        "both new-cycle flags permit second distinct pair before 900ms");
   Check(d.LastPairUsedFastPhase && d.FastPhasePairsAccepted==1,
        "fast proof is explicitly counted, not confused with normal pair");
   Check(!PhasePair(d,11.25,false,true,true),
        "positive acceleration from one unqualified ankle is not a new fast pair");
   Check(PhasePair(d,11.67,false,false,true),
        "phase mode falls back to ordinary 900ms on unqualified pairs");
   Check(!d.LastPairUsedFastPhase,"fallback was normal, not phase-fast");

   // The fast accepted event can actually reach the pre-landing buffer.
   var detector=new BiofitBilateralJumpDetector();
   var game=Game();var buffer=new BiofitJumpBuffer();
   Check(PhasePair(detector,200,true,true,true),"fast+buffer first physical pair");
   Check(buffer.Submit(game,300,0,"BILATERAL",1,true)==
     BiofitDashCore.JumpResult.Accepted,"initial first rise acts immediately");
   Advance(game,.62,60);
   Check(game.EstimatedSecondsUntilLanding>0 &&
         game.EstimatedSecondsUntilLanding<=BiofitJumpBuffer.WindowSeconds,
         "game genuinely descending inside 180ms prelanding window");
   Check(PhasePair(detector,200.62,true,true,true),
        "second phase-confirmed cycle is admitted before cube landing");
   Check(buffer.Submit(game,300.62,0,"BILATERAL",2,true)==
         BiofitDashCore.JumpResult.Buffered,
         "second physical rise really enters one-slot buffer");
   Check(!PhasePair(detector,200.90,true,true,true),
         "braking artifact at 280ms blocked from reaching buffer");
   BiofitJumpBuffer.Outcome consumed=default(BiofitJumpBuffer.Outcome);
   for(int frame=0;frame<14 && consumed.State==BiofitJumpBuffer.Completion.None;frame++) {
      game.Step(1f/60);
      consumed=buffer.Tick(game,300.62+(frame+1)/60d);
   }
   Check(consumed.State==BiofitJumpBuffer.Completion.Executed &&
       consumed.CommandId==2 && game.SuccessfulJumps==2 && !buffer.Pending,
       "fast phase command executes EXACTLY ONCE at grounded game transition");
 }
 static void Main(string[] args) {
  CheckJumpBuffer();
  CheckNew180msWindow();
  CheckPhaseGatedControl();
  var d=new BiofitBilateralJumpDetector();Check(Pair(d,10),"A isolated pair");
  Check(!d.ProcessImpulse(1,10,.25f)&&!d.ProcessImpulse(2,10.02,.25f),"duplicate impulses cannot act");
  foreach(int fps in new[]{30,60,120})foreach(double t in new[]{.8,.9,1.0,1.2})Cadence(t,fps,t==.8?3:5,t==.8?3:5);
  Cadence(1,10,5,3); // Step clamps simulated dt to 50ms: slow rendering changes wall-time flight.
  foreach(int fps in new[]{10,15,20,30,60,120}) {
   var g=Game();g.Jump();int n=0;do{g.Step(1f/fps);n++;}while(g.JumpAltitude>0 && n<1000);
   Console.WriteLine($"flight fps={fps} frames={n} wallSeconds={(double)n/fps:F4}");
  }
  d=new BiofitBilateralJumpDetector();Pair(d,100);Check(!Pair(d,100.7),"D short second impulse suppressed");
  Check(d.LastDecision==BiofitBilateralJumpDetector.Decision.PairCooldown,"D explicit cooldown reason");
  Check(Pair(d,101),"suppressed secondary impulse does not extend pair cooldown");
  d=new BiofitBilateralJumpDetector();var repeated=Game();
  for(int n=0;n<5;n++) {
   Check(Pair(d,200+n),"five rises remain eligible after suppressed secondary impulses");
   Check(repeated.Jump(),"five rises become game actions");
   Advance(repeated,.7,60);
   Check(!Pair(d,200+n+.7),"secondary at +700ms suppressed without postponing rearm");
   Advance(repeated,.3,60);
  }
  Check(repeated.SuccessfulJumps==5&&d.PairCooldownRejected==5,"five commands, no secondary command");
  // Preserve a newer pending impulse if the two connections arrive in batches.
  d=new BiofitBilateralJumpDetector();Pair(d,300);
  Check(!d.ProcessImpulse(1,300.7,.25f)&&!d.ProcessImpulse(1,301.1,.25f),"left connection batch waits");
  Check(!d.ProcessImpulse(2,300.72,.25f),"late opposite completes suppressed pair");
  Check(!d.ProcessImpulse(1,301.2,.25f)&&d.LastDecision==BiofitBilateralJumpDetector.Decision.SameAnkleRefractory,"rollback must not erase newer pending left timestamp");
  Check(d.ProcessImpulse(2,301.12,.25f),"newer pending left remains pairable");
  // Known limitation retained, not disguised as a phase detector test pass.
  d=new BiofitBilateralJumpDetector();var ambiguous=Game();
  Check(Pair(d,100)&&ambiguous.Jump(),"first impulse acts");Advance(ambiguous,2,60);
  Check(Pair(d,102)&&ambiguous.Jump(),"KNOWN LIMIT: same-gesture braking at +2s indistinguishable from new gesture");
  Console.WriteLine("KNOWN LIMIT: two impulses +2s produce two actions; human phase unobservable from this event sequence");
  d=new BiofitBilateralJumpDetector();
  Check(!d.ProcessImpulse(2,30.1,.25f)&&d.ProcessImpulse(1,30,.25f),"E cross-ankle reversed delivery pairs on timestamps");
  Check(!d.ProcessImpulse(1,31,.25f),"E missing right gives no action");
  Check(Pair(d,32),"E next complete pair recovers");
  var gate=new BiofitEventGate();Check(gate.ObserveSequence(65535)&&gate.ObserveSequence(0),"sequence wrap");
  Check(!gate.ObserveSequence(0)&&!gate.ObserveSequence(65535),"duplicate and reordered rejected");
  Check(gate.ObserveSequence(2)&&gate.Missing==1,"loss counted, next fresh event survives");
  Check(BiofitEventGate.Rejection(true,true,10.5,10,0,0)=="ANTIGUO","old BLE rejected");
  Check(BiofitEventGate.Rejection(true,true,10.1,10,200,0)=="ANTIGUO","combined callback age and queue");
  Check(BiofitEventGate.Rejection(true,true,10,10,0,251)=="ANTIGUO","firmware queued event rejected");
  Check(BiofitEventGate.Rejection(true,false,10,10,0,0)=="SIN_RELOJ","unmapped rejected");
  d=new BiofitBilateralJumpDetector();Check(!d.ProcessImpulse(1,10,.11f)&&d.LastDecision==BiofitBilateralJumpDetector.Decision.BelowThreshold,"threshold preserved");
  Check(!d.ProcessImpulse(1,double.NaN,.25f)&&d.LastDecision==BiofitBilateralJumpDetector.Decision.Invalid,"invalid reason");
  Check(!Pair(d,20,.181)&&d.LastDecision==BiofitBilateralJumpDetector.Decision.OutsideWindow,"181ms rejected");
  d=new BiofitBilateralJumpDetector();Check(Pair(d,30,.18),"180ms accepted");
  // No reset from starting a capture/threshold change can bypass the protection.
  d.ClearPending();Check(!Pair(d,30.5),"clear pending retains pair cooldown");
  var game=new BiofitDashCore();Check(game.TryJump()==BiofitDashCore.JumpResult.NotPlaying,"not playing reason");
  game.StartNewGame();game.TogglePause();Check(game.TryJump()==BiofitDashCore.JumpResult.Paused,"pause reason");
  game.TogglePause();Check(game.TryJump()==BiofitDashCore.JumpResult.Accepted,"shared command accepted");
  Check(game.TryJump()==BiofitDashCore.JumpResult.Airborne,"same-frame repeat airborne even at zero altitude");
  Advance(game,.8,60);Check(game.SuccessfulJumps==1&&game.JumpAltitude==0,"rejected airborne command never queued for landing");
  var collision=new BiofitDashCore();collision.StartNewGame();for(int i=0;i<300&&!collision.GameOver;i++)collision.Step(1f/60);
  Check(collision.GameOver&&collision.TryJump()==BiofitDashCore.JumpResult.GameOver,"collision reason distinct");
  if(args.Length>0)Replay(args[0]);
  Console.WriteLine($"PASS {checks} checks of production C#; synthetic/replay, no Unity render or physical BLE");
 }
}
