using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniBLE;
static class ReconnectIntegration
{
    static BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string why){if(!ok)throw new Exception("Reconnect: "+why);}
    sealed class Char:IBleCharacteristic
    {
        public int Subscriptions;public Action<byte[]> Callback;
        public Task SubscribeAsync(Action<byte[]> a,CancellationToken cancellationToken=default){Subscriptions++;Callback=a;return Task.CompletedTask;}
        public Task UnsubscribeAsync(CancellationToken cancellationToken=default){Callback=null;return Task.CompletedTask;}
        public Task WriteAsync(byte[] data,bool withResponse=true,CancellationToken cancellationToken=default){Callback?.Invoke(Encoding.ASCII.GetBytes("OK,"+Encoding.ASCII.GetString(data)));return Task.CompletedTask;}
    }
    sealed class Device:IBleDevice,IBleService
    {
        public BleConnectionState ConnectionState{get;private set;}
        public event Action<BleConnectionState> OnConnectionStateChanged;
        public int Connects,Disconnects;public bool Fail;
        public readonly Dictionary<string,Char> Chars=new Dictionary<string,Char>();
        public Task ConnectAsync(CancellationToken token=default){Connects++;if(Fail)throw new Exception("not found");ConnectionState=BleConnectionState.Connected;OnConnectionStateChanged?.Invoke(ConnectionState);return Task.CompletedTask;}
        public Task DisconnectAsync(CancellationToken token=default){Disconnects++;Drop();return Task.CompletedTask;}
        public void Drop(){ConnectionState=BleConnectionState.Disconnected;OnConnectionStateChanged?.Invoke(ConnectionState);}
        public Task<IBleService> GetServiceAsync(string s,CancellationToken token=default)=>Task.FromResult<IBleService>(this);
        public Task<IBleCharacteristic> GetCharacteristicAsync(string s,CancellationToken token=default){if(!Chars.TryGetValue(s,out var c))Chars[s]=c=new Char();return Task.FromResult<IBleCharacteristic>(c);}
    }
    sealed class Adapter:IBleAdapter
    {
        public bool Available,Permission=true;public int Requests;
        public readonly Device L=new Device(),R=new Device();
        public TaskCompletionSource<bool> Hold;
        public Task<bool> RequestPermissionAsync(CancellationToken token=default){Requests++;return Hold?.Task??Task.FromResult(Permission);}
        public Task<bool> IsAvailableAsync(CancellationToken token=default)=>Task.FromResult(Available);
        public Task<IBleDevice> GetDeviceAsync(string s,CancellationToken token=default)=>Task.FromResult<IBleDevice>(s.StartsWith("80:")?L:R);
    }
    static Task Connect(BiofitBLEMonitor m)=>(Task)typeof(BiofitBLEMonitor).GetMethod("ConnectBothAsync",Flags).Invoke(m,null);
    static void Update(BiofitBLEMonitor m)=>typeof(BiofitBLEMonitor).GetMethod("Update",Flags).Invoke(m,null);
    public static async Task Run()
    {
        var m=new BiofitBLEMonitor();var a=new Adapter();BleManager.Adapter=a;
        await Connect(m);Check(m.ConnectedTrackerCount==0&&m.TrackerConnectionStatus.Contains("BLUETOOTH")&&!m.TrackerConnectionBusy,"off");
        a.Available=true;a.Permission=false;await Connect(m);Check(a.L.Connects==0&&m.TrackerConnectionStatus.Contains("PERMITIR"),"denied");
        a.Permission=true;a.R.Fail=true;
        a.Hold=new TaskCompletionSource<bool>();var first=Connect(m);int requests=a.Requests;await Connect(m);Check(a.Requests==requests&&m.TrackerConnectionBusy,"double tap");
        a.Hold.SetResult(true);await first;a.Hold=null;
        Check(m.ConnectedTrackerCount==1&&a.L.Connects==1&&a.R.Connects==1,"partial");
        a.R.Fail=false;await Connect(m);Check(m.BothTrackersConnected&&a.L.Connects==1&&a.R.Connects==2,"retry only missing");
        await Connect(m);Check(a.L.Connects==1&&a.R.Connects==2,"healthy not reconnected");
        a.R.Drop();Update(m);Check(m.ConnectedTrackerCount==1,"disconnect");
        await Connect(m);Check(m.BothTrackersConnected&&a.R.Connects==3&&a.L.Connects==1,"reconnect");
        string uuid="f9710000-6d68-4b7d-a742-6fae7b5f0002";
        Check(a.L.Chars[uuid].Subscriptions==1&&a.R.Chars[uuid].Subscriptions==2,"one EVENT subscription per successful connection");
        a.R.Drop();await Connect(m);Update(m);Check(m.BothTrackersConnected,"stale reset ignored");
        Console.WriteLine("PASS BLE retry: Bluetooth off/on, permission, double tap, partial, preserved healthy link, disconnect and stale reset");
    }
}
