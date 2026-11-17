using System;
using System.Globalization;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;

// 20Hz diagnostic snapshots, NEVER fed to the gameplay queue or detector.
public sealed class BiofitRawCapture
{
    public const string Header="tipo;etapa;callback_monotonic_s;tracker_id;secuencia;sensor_ms;ax_g;ay_g;az_g;gx_dps;gy_dps;gz_dps;gap_seq;delta_rx_menos_sensor_ms;raw_worker_queue_ms";
    private readonly object gate=new object();
    private readonly bool[] seen=new bool[3];
    private readonly ushort[] sequences=new ushort[3];
    private readonly uint[] sensor=new uint[3];
    private readonly double[] receive=new double[3];
    private int valid,missing,duplicate,invalid,leftCount,rightCount;
    private long callbackMaxTicks;
    public int LeftCount=>Volatile.Read(ref leftCount);
    public int RightCount=>Volatile.Read(ref rightCount);
    public int Valid => Volatile.Read(ref valid);
    public int Missing => Volatile.Read(ref missing);
    public int Duplicate => Volatile.Read(ref duplicate);
    public int Invalid => Volatile.Read(ref invalid);
    public BiofitSessionFile File { get; }
    public volatile string Segment="OFF";
    private struct Packet { public byte Id; public byte[] Bytes; public double Callback; public string Segment; }
    private readonly BlockingCollection<Packet> packets=new BlockingCollection<Packet>(2048);
    private readonly Task worker;
    private int queueDropped;
    private string lastSegment="";
    public int QueueDropped=>Volatile.Read(ref queueDropped);
    public BiofitRawCapture(BiofitSessionFile file)
    {
        File=file;
        worker=Task.Run(()=> { foreach(var p in packets.GetConsumingEnumerable())
            Parse(p.Id,p.Bytes,p.Callback,p.Segment); });
    }
    public async Task FinishAsync()
    {
        packets.CompleteAdding();await worker;await File.Finish();
    }
    public void Record(byte expected,byte[] b,double callback)
    {
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        if(b==null || b.Length!=20 || expected<1 || expected>2) { Interlocked.Increment(ref invalid);return; }
        try
        {
            if(!packets.TryAdd(new Packet { Id=expected,Bytes=(byte[])b.Clone(),Callback=callback,Segment=Segment }))
                Interlocked.Increment(ref queueDropped);
        }
        catch(InvalidOperationException) { Interlocked.Increment(ref queueDropped); }
        long duration=System.Diagnostics.Stopwatch.GetTimestamp()-started,old;
        do { old=Interlocked.Read(ref callbackMaxTicks); if(duration<=old)break; }
        while(Interlocked.CompareExchange(ref callbackMaxTicks,duration,old)!=old);
    }
    private void Parse(byte expected,byte[] b,double callback,string segment)
    {
        double queueMs=(Now()-callback)*1000;
        lock(gate)
        {
            if(b==null || b.Length!=20 || b[0]!=2 || b[1]!=expected || expected<1 || expected>2) { Interlocked.Increment(ref invalid);return; }
            if(segment!=lastSegment) { Array.Clear(seen,0,seen.Length); lastSegment=segment; }
            ushort seq=U16(b,2);uint ms=U32(b,4);int gap=0;string delta="";
            if(seen[expected])
            {
                int step=unchecked((ushort)(seq-sequences[expected]));
                // Duplicate/reordered packets are counted, not used to move the reference backwards.
                if(step==0 || step>=32768) { duplicate++;return; }
                gap=step-1;missing+=gap;
                uint dt=unchecked(ms-sensor[expected]);
                if(dt<60000)delta=((callback-receive[expected])*1000-dt).ToString("F3",CultureInfo.InvariantCulture);
            }
            seen[expected]=true;sequences[expected]=seq;sensor[expected]=ms;receive[expected]=callback;valid++;if(expected==1)leftCount++;else rightCount++;
            string row="RAW;"+segment+";"+callback.ToString("F6",CultureInfo.InvariantCulture)+";"+expected+";"+seq+";"+ms;
            for(int i=0;i<6;i++) row+=";"+(unchecked((short)U16(b,8+i*2))/(i<3?1000d:10d)).ToString("F3",CultureInfo.InvariantCulture);
            File.Add(row+";"+gap+";"+delta+";"+queueMs.ToString("F3",CultureInfo.InvariantCulture));
        }
    }
    public string Summary => "raw="+Valid+" left="+LeftCount+" right="+RightCount+" callbackMaxMs="+(Interlocked.Read(ref callbackMaxTicks)*1000d/System.Diagnostics.Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture)+" gaps="+Missing+" dup/order="+Duplicate+" invalid="+Invalid+" callbackDrop="+QueueDropped+" diskRejected="+File.Rejected+" pending="+File.Pending+" error="+File.Error;
    private static double Now()=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    private static ushort U16(byte[] b,int i)=>(ushort)(b[i]|b[i+1]<<8);
    private static uint U32(byte[] b,int i)=>(uint)(b[i]|b[i+1]<<8|b[i+2]<<16|b[i+3]<<24);
}
