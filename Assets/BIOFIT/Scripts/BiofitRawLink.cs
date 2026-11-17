using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniBLE;

// Serialized GATT transitions. No Task.Wait/Result or Unity frame polling.
public sealed class BiofitRawLink
{
    private readonly IBleCharacteristic raw,config;
    private readonly SemaphoreSlim serial=new SemaphoreSlim(1,1);
    private bool subscribed;
    public BiofitRawLink(IBleCharacteristic raw, IBleCharacteristic config) { this.raw=raw;this.config=config; }
    private async Task CommandAsync(bool enabled,CancellationToken token)
    {
        string command=enabled?"STREAM,1":"STREAM,0";
        var reply=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using(token.Register(()=>reply.TrySetCanceled()))
        {
            try
            {
                await config.SubscribeAsync(bytes=> {
                    if(bytes!=null && Encoding.ASCII.GetString(bytes).Trim()=="OK,"+command)reply.TrySetResult(true);
                },token);
                await config.WriteAsync(Encoding.ASCII.GetBytes(command),true,token);
                await reply.Task; // GATT write success alone is not firmware acknowledgement.
            }
            finally
            {
                using(var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                    await config.UnsubscribeAsync(cleanup.Token);
            }
        }
    }
    public async Task SetAsync(bool enabled,Action<byte[]> callback)
    {
        await serial.WaitAsync();
        try
        {
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                if(enabled)
                {
                    if(!subscribed) { await raw.SubscribeAsync(callback,timeout.Token);subscribed=true; }
                    await CommandAsync(true,timeout.Token);
                }
                else
                {
                    try { await CommandAsync(false,timeout.Token); }
                    finally
                    {
                        if(subscribed)
                        {
                            using(var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                                await raw.UnsubscribeAsync(cleanup.Token);
                            subscribed=false;
                        }
                    }
                }
            }
        }
        catch
        {
            if(enabled)
            {
                try { using(var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(3)))await CommandAsync(false,cleanup.Token); } catch { }
                try { using(var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(3)))await raw.UnsubscribeAsync(cleanup.Token); } catch { }
                subscribed=false;
            }
            throw;
        }
        finally { serial.Release(); }
    }
}
