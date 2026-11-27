using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniBLE;
class Fake:IBleCharacteristic
{
    public Action<byte[]> Callback;public int Writes,Subscriptions,Unsubscriptions;
    public int Active,Target,Token,State;
    public Task SubscribeAsync(Action<byte[]> cb,CancellationToken cancellationToken=default){Subscriptions++;Callback=cb;return Task.CompletedTask;}
    public Task UnsubscribeAsync(CancellationToken cancellationToken=default){Unsubscriptions++;Callback=null;return Task.CompletedTask;}
    static byte[] Page(int gen,int index,int page){var p=new byte[20];p[0]=0xD3;p[1]=1;p[2]=(byte)page;p[3]=1;p[4]=(byte)gen;p[5]=(byte)(gen>>8);p[6]=(byte)index;p[7]=(byte)(index>>8);p[12]=0x85;p[13]=0xff;return p;}
    void Emit(string s)=>Callback?.Invoke(Encoding.ASCII.GetBytes(s));
    public Task WriteAsync(byte[] b,bool withResponse=true,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();Writes++;string s=Encoding.ASCII.GetString(b);
        if(s=="D,GET"){if(State==1){Active=Target;State=0;}Emit("P,"+Active+","+Target+","+Token+","+State);}
        else if(s.StartsWith("D,P,")){string[] f=s.Split(',');Target=int.Parse(f[2]);Token=int.Parse(f[3]);State=1;Emit("D,Q,65535");Emit("D,Q,"+Token);}
        else if(s=="D,T,1")Emit("T,1,4,7");
        else if(s=="D,T,0"||s=="D,T")Emit("T,2,4,7");
        else if(s.StartsWith("D,B,")){
            var f=s.Split(',');int gen=int.Parse(f[2]),start=int.Parse(f[3]),count=int.Parse(f[4]);uint crc=0xffffffffu;
            for(int i=start;i<start+count;i++)for(int pg=0;pg<3;pg++){
                var page=Page(gen,i,pg);crc=BiofitBmdProtocol.CrcUpdate(crc,page);
                if(i!=start||pg!=1)Callback?.Invoke(page); // one missing page to repair
            }
            var end=new byte[20];end[0]=0xD4;end[1]=1;end[2]=(byte)count;end[3]=1;end[4]=(byte)gen;end[5]=(byte)(gen>>8);end[6]=(byte)start;end[7]=(byte)(start>>8);
            uint v=crc^0xffffffffu;for(int k=0;k<4;k++)end[8+k]=(byte)(v>>(8*k));Callback?.Invoke(end);
        }
        else if(s.StartsWith("D,R,")){var f=s.Split(',');var p=Page(int.Parse(f[2]),int.Parse(f[3]),int.Parse(f[4]));var bad=(byte[])p.Clone();bad[1]=2;Callback?.Invoke(bad);Callback?.Invoke(p);}
        else Emit("ERR,D,CMD");
        return Task.CompletedTask;
    }
}
class Program
{
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    static async Task Main()
    {
        var fake=new Fake();var l=new BiofitBmdLink(fake);
        await l.Batch(async x=>{
            var start=await x.GetProfile(default);Check(start.Applied(0),"initial");
            await x.RequestProfile(15,1,default);
            var end=await x.WaitApplied(15,1,default);Check(end.Applied(15)&&end.Token==1,"verified profile");
            var t=await x.TraceCommand("D,T,1",default);Check(t.State==1,"start");
            t=await x.TraceCommand("D,T,0",default);Check(t.State==2&&t.Count==4,"freeze");
            string row=await x.GetRow(1,7,3,default);Check(row.StartsWith("1;7;3;0;-123;"),"signed field / matching page");
            Check(row.Split(';').Length==21,"CSV schema");
            int before=fake.Writes;var block=await x.GetBlock(1,7,300,4,default);
            Check(block.Length==4&&block[0].StartsWith("1;7;300;")&&block[3].StartsWith("1;7;303;")&&fake.Writes-before==2,"block selective page repair");
        },default);
        Check(fake.Subscriptions==1&&fake.Unsubscriptions==1&&fake.Callback==null,"cleanup");
        Check(!BiofitBmdProtocol.TryProfile(Encoding.ASCII.GetBytes("P,16,0,1,0"),out _),"invalid mask");
        Check(!BiofitBmdProtocol.TryTrace(Encoding.ASCII.GetBytes("T,2,1025,1"),out _),"invalid count");
        Check(BiofitBmdProtocol.TryTrace(Encoding.ASCII.GetBytes("T,2,13452,1"),out _,BiofitBmdProtocol.MaxTrace),"extended trace");
        Check((BiofitBmdProtocol.CrcUpdate(0xffffffffu,Encoding.ASCII.GetBytes("123456789"))^0xffffffffu)==0xcbf43926u,"CRC vector");
        Check(BiofitBmdProtocol.TryNonce(Encoding.ASCII.GetBytes("N,4294967295"),out uint nonce)&&nonce==uint.MaxValue,"nonce uint");
        Check(BiofitBmdProtocol.TryAuto(Encoding.ASCII.GetBytes("A,7,16,0"),out var astate)&&astate.Count==16&&!astate.Armed,"auto state");
        Check(!BiofitBmdProtocol.TryAuto(Encoding.ASCII.GetBytes("A,7,17,1"),out _),"auto cap");
        var meta=new byte[20];meta[0]=0xd5;meta[1]=1;meta[3]=3;meta[5]=0x80;meta[6]=7;meta[16]=1;meta[18]=2;
        Check(BiofitBmdProtocol.TrySnapshot(meta,1,0,out var snap)&&snap.FailureId==7&&snap.Flags==2&&snap.Generation==32768,"snapshot metadata");
        Check(BiofitBmdProtocol.AutoMetadata(1,snap,"SNAPSHOT").Split(';').Length==28,"auto metadata schema");
        var shadow=new BiofitBmdShadow();shadow.Begin(400,.18f);
        shadow.Observe(1,1,.105f,400,.18f);shadow.Observe(2,1.03,.105f,400,.18f);
        string s=shadow.Summary();Check(s.Contains("100mg=1")&&s.Contains("110mg=0")&&s.Contains("120mg=0"),s);
        shadow.ClearPending();shadow.Observe(1,2,.12f,400,.18f);shadow.Observe(2,2.03,.12f,400,.18f);
        Check(shadow.Summary().Contains("120mg=1"),"independent state");
        string dir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"biofit-bmd-test-"+Guid.NewGuid().ToString("N"));
        try {
            string path=System.IO.Path.Combine(dir,"trace.csv");
            var archive=new BiofitSessionFile(path,BiofitBmdProtocol.TraceHeader);
            Check(archive.Add("1;7;3;0;-123;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;4500"),"queue row");
            await archive.Finish();
            Check(archive.Error==""&&BiofitCaptureArchive.VerifyFileStreaming(path),"trace SHA "+archive.Error);
            System.IO.File.AppendAllText(path,"tamper");
            Check(!BiofitCaptureArchive.VerifyFileStreaming(path),"reject tamper");
            string auto=System.IO.Path.Combine(dir,"auto.csv");
            var autoFile=new BiofitSessionFile(auto,BiofitBmdProtocol.AutoHeader);
            autoFile.Add(BiofitBmdProtocol.AutoMetadata(1,snap,"SNAPSHOT"));await autoFile.Finish();
            Check(autoFile.Error==""&&BiofitCaptureArchive.VerifyFileStreaming(auto),"auto archive schema");
            string bad=System.IO.Path.Combine(dir,"bad.csv");
            var invalid=new BiofitSessionFile(bad,BiofitBmdProtocol.TraceHeader);
            invalid.Add("1;7;3");await invalid.Finish();
            Check(invalid.Error.Length>0&&!System.IO.File.Exists(bad),"reject wrong schema");
        } finally {if(System.IO.Directory.Exists(dir))System.IO.Directory.Delete(dir,true);}
        Console.WriteLine("PASS BMD strict protocol, ACK/readback, trace decode, isolated shadow state, archive SHA/schema");
    }
}
