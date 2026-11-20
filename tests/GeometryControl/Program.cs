using System;
using System.IO;
using System.Text.Json;
using Biofit.Geometry;

class Program
{
    static int checks;
    static void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
    sealed class Target : IGeometryJumpTarget
    {
        public int Calls;public bool Ground=true;
        public JumpOutcome TryJump() { Calls++;if(!Ground)return JumpOutcome.Airborne;Ground=false;return JumpOutcome.Executed; }
    }
    static void Main()
    {
        var gate=new GeometryJumpCommands();var target=new Target();
        Check(gate.Submit(1,0,10,10,true,target)==JumpOutcome.Executed,"one event acts immediately");
        target.Ground=true;
        Check(gate.Submit(1,0,10,10.1,true,target)==JumpOutcome.Duplicate && target.Calls==1,"held/replayed command cannot auto jump");
        Check(gate.Submit(2,0,10,10.26,true,target)==JumpOutcome.Expired,"old event no new TTL");
        Check(gate.Submit(3,0,10,10.2,true,target)==JumpOutcome.Executed,"preserve receiver 250ms freshness allowance");
        Check(gate.Submit(4,0,10,10.21,true,target)==JumpOutcome.Airborne,"airborne rejects instead of queue");
        target.Ground=true;
        Check(gate.Submit(4,0,10,10.22,true,target)==JumpOutcome.Duplicate,"airborne rejected command never executes on landing");
        gate.Cancel();
        Check(gate.Submit(5,0,10,10.23,true,target)==JumpOutcome.Cancelled,"old epoch rejected after pause/death/retry/disconnect");
        Check(gate.Submit(6,1,10,10.24,false,target)==JumpOutcome.Inactive,"inactive gameplay");
        Check(gate.Submit(7,1,double.NaN,10,true,target)==JumpOutcome.Invalid,"invalid clock");
        Check(gate.Submit(8,1,10.1,10,true,target)==JumpOutcome.Executed,"mapped timestamp tolerance preserved");
        Check(gate.Submit(9,1,10.2,10,true,target)==JumpOutcome.Invalid,"unreasonable future time rejected");
        foreach(double cadence in new[]{.8,.9,1.0,1.2})
        {
            var detector=new BiofitBilateralJumpDetector();gate=new GeometryJumpCommands();target=new Target();int pairs=0;
            for(int i=0;i<5;i++)
            {
                double t=20+i*cadence;target.Ground=true;
                detector.ProcessImpulse(1,t,.25f);
                if(detector.ProcessImpulse(2,t+.02,.25f))
                {
                    pairs++;
                    Check(gate.Submit(i+1,0,detector.LastPairSeconds,t+.03,true,target)==JumpOutcome.Executed,"real legacy detector to command");
                }
            }
            Check(pairs==(cadence<.9?3:5),"legacy cadence unchanged "+cadence);
        }
        var doubleLobe=new BiofitBilateralJumpDetector();
        doubleLobe.ProcessImpulse(1,10,.3f);doubleLobe.ProcessImpulse(2,10.02,.3f);
        doubleLobe.ProcessImpulse(1,10.4,.3f);
        Check(!doubleLobe.ProcessImpulse(2,10.42,.3f),"isolated second lobe at 400ms remains rejected");
        doubleLobe.ProcessImpulse(1,11,.3f);
        Check(doubleLobe.ProcessImpulse(2,11.02,.3f),"documented ambiguity: later lobe at 1s indistinguishable from new gesture");
        var reverse=new BiofitBilateralJumpDetector();reverse.ProcessImpulse(1,20.1,.3f);reverse.ProcessImpulse(2,20,.3f);
        Check(Math.Abs(reverse.LastPairSeconds-20.1)<.000001,"out of order pair uses later sensor timestamp, not last delivery");
        // Conservative semi-implicit Euler/AABB replay. This is a synthetic
        // reachability check, NOT Box2D or Unity Play mode.
        string root=Directory.GetCurrentDirectory();
        var options=new JsonSerializerOptions{IncludeFields=true};
        int routes=0;
        for(int i=0;i<4;i++)
        {
            string path=Path.Combine(root,"Assets/BIOFIT/Geometry/Resources/BiofitGeometry/Levels/"+GeometryLevel.ResourceNames[i]+".json");
            var level=JsonSerializer.Deserialize<GeometryLevel>(File.ReadAllText(path),options);level.Validate(i+1);
            foreach(double dt in new[]{.01,.02})foreach(double offset in new[]{-.06,0,.06})
            { Replay(level,dt,offset);routes++; }
        }
        Console.WriteLine("PASS "+checks+" command/cadence/reachability checks; "+routes+" synthetic complete routes. Not Unity/Android/physical BLE.");
    }
    static void Replay(GeometryLevel level,double dt,double timingOffset)
    {
        double x=GeometryPhysics.SpawnX,y=.45,v=0,time=0,last=-100;int next=0;
        var pads=new System.Collections.Generic.HashSet<int>();
        while(x<level.finishX && time<180)
        {
            if(next<level.sections.Length && x>=level.sections[next].jumpX+timingOffset*GeometryPhysics.Speed)
            {
                if(!level.sections[next].automaticPad)
                {
                    Check(y<=.51 && v<=.1,"grounded at cue: "+level.title+" section "+next);
                    Check(time-last>=.9,"physical demand at least legacy cooldown");
                    v=GeometryPhysics.JumpVelocity;last=time;
                }
                next++;
            }
            double oldBottom=y-.45;
            v-=GeometryPhysics.Gravity*dt;y+=v*dt;x+=GeometryPhysics.Speed*dt;time+=dt;
            if(y<.45){y=.45;v=0;}
            foreach(var o in level.obstacles)
            {
                bool overlap=Math.Abs(x-o.x)<.45+o.width/2;
                if(!overlap)continue;
                double top=o.y+o.height/2,bottom=y-.45;
                if(o.kind=="pad" && bottom<top && !pads.Contains(o.id)){pads.Add(o.id);v=GeometryPhysics.JumpVelocity;}
                if(o.kind=="orb" || o.kind=="pad")continue; // optional orb not required
                if(bottom<top-.001 && y+.45>o.y-o.height/2)
                {
                    if(o.kind=="block" && oldBottom>=top-.03 && v<=0){y=top+.45;v=0;}
                    else throw new Exception("Synthetic collision "+level.title+" id="+o.id+" x="+x+" dt="+dt+" timing="+timingOffset);
                }
            }
        }
        Check(x>=level.finishX,"finish reachable "+level.title);
    }
}
