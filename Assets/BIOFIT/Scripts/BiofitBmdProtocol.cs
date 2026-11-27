using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniBLE;

// V230 CONFIG protocol. No gameplay callback in this class.
public static class BiofitBmdProtocol
{
    public struct Profile
    {
        public int Active, Target, Token, State;
        public bool Applied(int mask) => State==0 && Active==mask && Target==mask;
        public override string ToString() => Active+"/"+Target+" token="+Token+" estado="+State;
    }
    public struct Trace
    {
        public int State, Count, Generation;
        public override string ToString() => "estado="+State+" n="+Count+" gen="+Generation;
    }
    private static bool Fields(byte[] bytes,string prefix,int n,out int[] values)
    {
        values=null;
        if(bytes==null || bytes.Length>20 || bytes.Length<3)return false;
        string[] fields=Encoding.ASCII.GetString(bytes).Split(',');
        if(fields.Length!=n+1 || fields[0]!=prefix)return false;
        var v=new int[n];
        for(int i=0;i<n;i++)
            if(!int.TryParse(fields[i+1],NumberStyles.None,CultureInfo.InvariantCulture,out v[i]) || v[i]>65535)return false;
        values=v;return true;
    }
    public static bool TryProfile(byte[] bytes,out Profile p)
    {
        p=default;
        if(!Fields(bytes,"P",4,out var f)||f[0]>15||f[1]>15||f[3]>2)return false;
        p=new Profile{Active=f[0],Target=f[1],Token=f[2],State=f[3]};return true;
    }
    public const int MaxTrace=13452, MaxBlock=16;
    public static bool TryTrace(byte[] bytes,out Trace t,int maximum=1024)
    {
        t=default;
        if(!Fields(bytes,"T",3,out var f)||f[0]>2||f[1]>maximum||
           (f[0]==0&&(f[1]!=0||f[2]!=0))||(f[0]!=0&&f[2]==0))return false;
        t=new Trace{State=f[0],Count=f[1],Generation=f[2]};return true;
    }
    public static ushort U16(byte[] b,int p)=>(ushort)(b[p]|(b[p+1]<<8));
    public static short I16(byte[] b,int p)=>unchecked((short)U16(b,p));
    public static uint U32(byte[] b,int p)=>(uint)b[p]|((uint)b[p+1]<<8)|((uint)b[p+2]<<16)|((uint)b[p+3]<<24);
    public static bool PageMatches(byte[] b,byte foot,int gen,int index,int page) =>
        b!=null&&b.Length==20&&b[0]==0xD3&&b[1]==foot&&b[2]==page&&b[3]==1&&
        U16(b,4)==gen&&U16(b,6)==index;
    public static uint CrcUpdate(uint crc,byte[] data)
    {
        foreach(byte b in data){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^(0xedb88320u & unchecked(0u-(crc&1u)));}
        return crc;
    }
    public static bool BlockEndMatches(byte[] b,byte foot,int gen,int start,int count)=>
        b!=null&&b.Length==20&&b[0]==0xD4&&b[1]==foot&&b[2]==count&&b[3]==1&&
        U16(b,4)==gen&&U16(b,6)==start;
    public const string TraceHeader="foot;generation;index;sensor_ms;up_mg;velocity_mm_s;activity_mg;gyro_x10;ax_mg;ay_mg;az_mg;gx_x10;gy_x10;gz_x10;flags;block_mask;profile;events;legacy_up_mg;adaptive_up_mg;dt_us";
    public static string TraceRow(byte foot,int gen,int index,byte[] a,byte[] b,byte[] c)
    {
        if(!PageMatches(a,foot,gen,index,0)||!PageMatches(b,foot,gen,index,1)||!PageMatches(c,foot,gen,index,2))
            throw new InvalidOperationException("Páginas de trace incompatibles");
        return FormattableString.Invariant($"{foot};{gen};{index};{U32(a,8)};{I16(a,12)};{I16(a,14)};{I16(a,16)};{I16(a,18)};{I16(b,8)};{I16(b,10)};{I16(b,12)};{I16(b,14)};{I16(b,16)};{I16(b,18)};{c[8]};{c[9]};{c[10]};{c[11]};{I16(c,12)};{I16(c,14)};{U16(c,16)}");
    }
}

// One CONFIG subscription per batch. One request in flight; a delayed unrelated
// notification cannot satisfy a page/token-specific request. Never block a frame.
public sealed class BiofitBmdLink
{
    private readonly IBleCharacteristic config;
    private readonly SemaphoreSlim serial=new SemaphoreSlim(1,1);
    private readonly object gate=new object();
    private TaskCompletionSource<byte[]> reply;
    private Func<byte[],bool> matches;
    public BiofitBmdLink(IBleCharacteristic characteristic){config=characteristic??throw new ArgumentNullException(nameof(characteristic));}
    private void Receive(byte[] bytes)
    {
        if(bytes==null)return;
        lock(gate)
        {
            if(reply==null)return;
            if(matches(bytes))reply.TrySetResult((byte[])bytes.Clone());
            else if(bytes.Length<=20&&Encoding.ASCII.GetString(bytes).StartsWith("ERR,D,",StringComparison.Ordinal))
                reply.TrySetException(new InvalidOperationException(Encoding.ASCII.GetString(bytes)));
        }
    }
    public async Task Batch(Func<BiofitBmdLink,Task> action,CancellationToken token)
    {
        await serial.WaitAsync(token);
        try
        {
            using(var start=CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                start.CancelAfter(TimeSpan.FromSeconds(5));
                await config.SubscribeAsync(Receive,start.Token);
            }
            await action(this);
        }
        finally
        {
            try
            {
                using(var end=new CancellationTokenSource(TimeSpan.FromSeconds(5)))await config.UnsubscribeAsync(end.Token);
            }
            finally {serial.Release();}
        }
    }
    public async Task<byte[]> Send(string command,Func<byte[],bool> accept,CancellationToken token)
    {
        var bytes=Encoding.ASCII.GetBytes(command);
        if(bytes.Length>20)throw new ArgumentOutOfRangeException(nameof(command));
        for(int attempt=0;attempt<2;attempt++)
        {
            token.ThrowIfCancellationRequested();
            var waiter=new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock(gate){if(reply!=null)throw new InvalidOperationException("CONFIG ocupado");reply=waiter;matches=accept;}
            try
            {
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(3));
                    using(timeout.Token.Register(()=>waiter.TrySetCanceled()))
                    {
                        await config.WriteAsync(bytes,true,timeout.Token);
                        return await waiter.Task;
                    }
                }
            }
            catch(OperationCanceledException) when(!token.IsCancellationRequested&&attempt==0) {}
            finally {lock(gate){reply=null;matches=null;}}
        }
        throw new TimeoutException("Sin respuesta CONFIG: "+command);
    }
    public async Task<BiofitBmdProtocol.Profile> GetProfile(CancellationToken token)
    {
        var b=await Send("D,GET",x=>BiofitBmdProtocol.TryProfile(x,out _),token);
        BiofitBmdProtocol.TryProfile(b,out var p);return p;
    }
    public async Task<BiofitBmdProtocol.Trace> TraceCommand(string command,CancellationToken token,bool extended=false)
    {
        int maximum=extended?BiofitBmdProtocol.MaxTrace:1024;
        var b=await Send(command,x=>BiofitBmdProtocol.TryTrace(x,out var t,maximum)&&
            (command=="D,T,1"||command.StartsWith("D,S,",StringComparison.Ordinal)?t.State==1:command=="D,T,0"?t.State==2||t.State==0:true),token);
        BiofitBmdProtocol.TryTrace(b,out var result,maximum);return result;
    }
    // D3 pages retain legacy framing. D4 is the authoritative block CRC.
    // Repair missing pages only, and repeat the block once on CRC mismatch.
    public async Task<string[]> GetBlock(byte foot,int gen,int start,int count,CancellationToken token)
    {
        if(count<1||count>BiofitBmdProtocol.MaxBlock||start<0||start+count>BiofitBmdProtocol.MaxTrace||gen<1||gen>65535)
            throw new ArgumentOutOfRangeException(nameof(count));
        for(int attempt=0;attempt<2;attempt++)
        {
            var pages=new byte[count*3][];
            byte[] end=await Send("D,B,"+gen+","+start+","+count,b=>{
                if(BiofitBmdProtocol.BlockEndMatches(b,foot,gen,start,count))return true;
                if(b.Length!=20||b[0]!=0xD3)return false;
                int index=BiofitBmdProtocol.U16(b,6),page=b[2];
                if(index>=start&&index<start+count&&page<3&&BiofitBmdProtocol.PageMatches(b,foot,gen,index,page))
                    pages[(index-start)*3+page]=(byte[])b.Clone();
                return false;
            },token);
            for(int k=0;k<pages.Length;k++)
                if(pages[k]==null)
                {
                    int i=start+k/3,pg=k%3;
                    pages[k]=await Send("D,R,"+gen+","+i+","+pg,b=>BiofitBmdProtocol.PageMatches(b,foot,gen,i,pg),token);
                }
            uint crc=0xffffffffu;
            foreach(var bytes in pages)crc=BiofitBmdProtocol.CrcUpdate(crc,bytes);
            if((crc^0xffffffffu)!=BiofitBmdProtocol.U32(end,8))continue;
            var rows=new string[count];
            for(int i=0;i<count;i++)rows[i]=BiofitBmdProtocol.TraceRow(foot,gen,start+i,pages[i*3],pages[i*3+1],pages[i*3+2]);
            return rows;
        }
        throw new InvalidOperationException("CRC32 de bloque incorrecto: "+start);
    }
    public async Task RequestProfile(int mask,int id,CancellationToken token)
    {
        string ack="D,Q,"+id;
        await Send("D,P,"+mask+","+id,x=>Encoding.ASCII.GetString(x)==ack,token);
    }
    public async Task<BiofitBmdProtocol.Profile> WaitApplied(int mask,int id,CancellationToken token)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        while(watch.Elapsed.TotalSeconds<13)
        {
            var p=await GetProfile(token);
            if(p.Token==id&&p.Applied(mask))return p;
            if(p.Token==id&&p.State==2)throw new InvalidOperationException("Perfil vencido: mantener quietud");
            await Task.Delay(150,token);
        }
        throw new TimeoutException("Perfil no confirmado");
    }
    public async Task<string> GetRow(byte foot,int gen,int index,CancellationToken token)
    {
        var pages=new byte[3][];
        for(int page=0;page<3;page++)
        {
            int p=page;
            pages[p]=await Send("D,R,"+gen+","+index+","+p,x=>BiofitBmdProtocol.PageMatches(x,foot,gen,index,p),token);
        }
        return BiofitBmdProtocol.TraceRow(foot,gen,index,pages[0],pages[1],pages[2]);
    }
}

// Independent detector state per threshold; never emits a game action.
public sealed class BiofitBmdShadow
{
    public static readonly int[] ThresholdMg={80,90,100,110,120};
    private BiofitBilateralJumpDetector[] detectors;
    private int cooldown;private float window;
    public bool Active=>detectors!=null;
    public void Begin(int cd,float gap)
    {
        cooldown=cd;window=gap;detectors=new BiofitBilateralJumpDetector[ThresholdMg.Length];
        for(int i=0;i<detectors.Length;i++)
        {
            detectors[i]=new BiofitBilateralJumpDetector();
            detectors[i].SetThresholds(ThresholdMg[i]/1000f,gap);
            detectors[i].SetPairCooldownMilliseconds(cd);
        }
    }
    public void Stop(){detectors=null;}
    public void ClearPending(){if(detectors!=null)foreach(var d in detectors)d.ClearPending();}
    public string Observe(byte id,double seconds,float up,int cd,float gap)
    {
        if(detectors==null)return "";
        if(cooldown!=cd||window!=gap)Begin(cd,gap);
        var sb=new StringBuilder("BMD_SHADOW");
        for(int i=0;i<detectors.Length;i++)
        {
            var d=detectors[i];d.ProcessImpulse(id,seconds,up);
            sb.Append(" mg").Append(ThresholdMg[i]).Append('=').Append(d.LastDecision);
        }
        return sb.ToString();
    }
    public string Summary()
    {
        if(detectors==null)return "Sombra: iniciar registro";
        var sb=new StringBuilder("Pares sombra:");
        for(int i=0;i<detectors.Length;i++)sb.Append(' ').Append(ThresholdMg[i]).Append("mg=").Append(detectors[i].BilateralCandidates);
        return sb.ToString();
    }
}
