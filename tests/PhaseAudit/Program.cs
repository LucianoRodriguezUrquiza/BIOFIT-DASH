using System;
class Program {
 static int checks;
 static void Check(bool ok,string why) { if(!ok)throw new Exception(why); checks++; }
 static void Main() {
  var d=new BiofitSingleFootDiagnostic();
  Check(d.LeftThresholdG==.25f && d.RightThresholdG==.25f && !d.ExperimentalMode,"normal defaults");
  Check(BiofitBilateralJumpDetector.MinimumVerticalG==.12f && BiofitBilateralJumpDetector.MaxPairGapSeconds==.18f,"bilateral defaults");
  // Values transcribed from the published report; not a replay of original CSV.
  float[] first={.165f,.260f,.211f,.253f,.172f,.161f,.202f,.252f,.167f,.198f};
  int accepted=0;
  for(int i=0;i<first.Length;i++)
   if(d.Observe(2,first[i],i*2,.25f,.32)==BiofitSingleFootDiagnostic.Decision.Accepted)accepted++;
  Check(accepted==3 && d.Right.BelowThreshold==7,"report 7/10 weak first events");
  foreach(byte foot in new byte[]{1,2}) {
   d.Reset();
   Check(d.Observe(foot,.172f,180.550,.25f,.32)==BiofitSingleFootDiagnostic.Decision.BelowThreshold,"first weak");
   Check(d.Observe(foot,.373f,181.319,.25f,.32)==BiofitSingleFootDiagnostic.Decision.Accepted,"later strong accepted immediately");
   d.RecordGameResult(foot,false);
   var counters=foot==1?d.Left:d.Right;
   Check(counters.GameUnavailable==1 && counters.Jumped==0,"busy distinguished from threshold");
   Check(d.Observe(foot,.4f,181.4,.25f,.32)==BiofitSingleFootDiagnostic.Decision.Refractory,"short repeat rejected");
   Check(d.Observe(foot,.4f,182.019,.25f,.32)==BiofitSingleFootDiagnostic.Decision.Accepted,"700ms repeat not rejected by refractory");
  }
  foreach(float weak in new[]{.111f,.114f,.100f}) {
   var p=new BiofitBilateralJumpDetector();
   Check(!p.ProcessImpulse(1,10,.3f) && !p.ProcessImpulse(2,10.033,weak) && p.WeakRejected==1,"weak aligned pair excluded");
   Check(!p.ProcessImpulse(2,10.870,.240f) && p.ProcessImpulse(1,11.037,.421f),"later pair qualifies");
  }
  var boundary=new BiofitBilateralJumpDetector();
  Check(!boundary.ProcessImpulse(1,10,.12f) && boundary.ProcessImpulse(2,10.180,.12f),"180ms and threshold inclusive");
  boundary=new BiofitBilateralJumpDetector();
  Check(!boundary.ProcessImpulse(1,10,.12f) && !boundary.ProcessImpulse(2,10.181,.12f),"181ms outside");
  // Firmware synthetic braking emits .30721g: both default paths accept it.
  d.Reset();Check(d.Observe(2,.30721f,20,.25f,.32)==BiofitSingleFootDiagnostic.Decision.Accepted,"no phase information in Unity UP gate");
  Console.WriteLine($"PASS {checks} phase/threshold checks (report-derived and synthetic, not human validation)");
 }
}
