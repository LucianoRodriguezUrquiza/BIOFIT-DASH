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
    void Emit(string s)=>Callback?.Invoke(Encoding.ASCII.GetBytes(s));
    public Task WriteAsync(byte[] b,bool withResponse=true,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();Writes++;string s=Encoding.ASCII.GetString(b);
        if(s=="D,GET"){if(State==1){Active=Target;State=0;}Emit("P,"+Active+","+Target+","+Token+","+State);}
        else if(s.StartsWith("D,P,")){string[] f=s.Split(',');Target=int.Parse(f[2]);Token=int.Parse(f[3]);State=1;Emit("D,Q,65535");Emit("D,Q,"+Token);}
        else if(s=="D,T,1")Emit("T,1,4,7");
        else if(s=="D,T,0"||s=="D,T")Emit("T,2,4,7");
        else if(s.StartsWith("D,R,")){var f=s.Split(',');var p=new byte[20];p[0]=0xD3;p[1]=1;p[2]=byte.Parse(f[4]);p[3]=1;p[4]=byte.Parse(f[2]);p[6]=byte.Parse(f[3]);p[12]=0x85;p[13]=0xff;var bad=(byte[])p.Clone();bad[1]=2;Callback?.Invoke(bad);Callback?.Invoke(p);}
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
        },default);
        Check(fake.Subscriptions==1&&fake.Unsubscriptions==1&&fake.Callback==null,"cleanup");
        Check(!BiofitBmdProtocol.TryProfile(Encoding.ASCII.GetBytes("P,16,0,1,0"),out _),"invalid mask");
        Check(!BiofitBmdProtocol.TryTrace(Encoding.ASCII.GetBytes("T,2,1025,1"),out _),"invalid count");
        var shadow=new BiofitBmdShadow();shadow.Begin(400,.18f);
        shadow.Observe(1,1,.105f,400,.18f);shadow.Observe(2,1.03,.105f,400,.18f);
        string s=shadow.Summary();Check(s.Contains("100mg=1")&&s.Contains("110mg=0")&&s.Contains("120mg=0"),s);
        shadow.ClearPending();shadow.Observe(1,2,.12f,400,.18f);shadow.Observe(2,2.03,.12f,400,.18f);
        Check(shadow.Summary().Contains("120mg=1"),"independent state");
        Console.WriteLine("PASS BMD strict protocol, ACK/readback, trace decode, isolated shadow state");
    }
}
