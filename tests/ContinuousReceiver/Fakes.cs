using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniBLE;
sealed class FakeCharacteristic : IBleCharacteristic
{
 public Action<byte[]> Callback;
 public List<Action<byte[]>> History = new List<Action<byte[]>>();
 public int Subscribes, Unsubscribes;
 public Task SubscribeAsync(Action<byte[]> a,CancellationToken cancellationToken=default)
 { cancellationToken.ThrowIfCancellationRequested(); Callback=a; History.Add(a); Subscribes++; return Task.CompletedTask; }
 public Task UnsubscribeAsync(CancellationToken cancellationToken=default)
 { Callback=null; Unsubscribes++; return Task.CompletedTask; }
 public Task WriteAsync(byte[] data,bool withResponse=true,CancellationToken cancellationToken=default) => Task.CompletedTask;
}
sealed class FakeDevice : IBleDevice, IBleService
{
 public event Action<BleConnectionState> OnConnectionStateChanged;
 public readonly FakeCharacteristic Events=new FakeCharacteristic(),Status=new FakeCharacteristic();
 public int Listeners => OnConnectionStateChanged?.GetInvocationList().Length ?? 0;
 public int Connections, Disconnections;
 public bool Connected, FailConnect;
 public Task ConnectAsync(CancellationToken token=default)
 { token.ThrowIfCancellationRequested(); Connections++; if(FailConnect)throw new InvalidOperationException("fake unavailable"); Connected=true; OnConnectionStateChanged?.Invoke(BleConnectionState.Connected); return Task.CompletedTask; }
 public Task DisconnectAsync(CancellationToken token=default)
 { Disconnections++; Connected=false; OnConnectionStateChanged?.Invoke(BleConnectionState.Disconnected); return Task.CompletedTask; }
 public void Drop() { Connected=false; OnConnectionStateChanged?.Invoke(BleConnectionState.Disconnected); }
 public void RepeatConnected() => OnConnectionStateChanged?.Invoke(BleConnectionState.Connected);
 public Task<IBleService> GetServiceAsync(string s,CancellationToken token=default)=>Task.FromResult<IBleService>(this);
 public Task<IBleCharacteristic> GetCharacteristicAsync(string s,CancellationToken token=default)
 {
  if(s.EndsWith("0002"))return Task.FromResult<IBleCharacteristic>(Events);
  if(s.EndsWith("0003"))return Task.FromResult<IBleCharacteristic>(Status);
  throw new InvalidOperationException("optional characteristic absent in fake");
 }
}
sealed class FakeAdapter : IBleAdapter
{
 public readonly FakeDevice Left=new FakeDevice(), Right=new FakeDevice();
 public Task<bool> RequestPermissionAsync()=>Task.FromResult(true);
 public Task<bool> IsAvailableAsync(CancellationToken token=default)=>Task.FromResult(true);
 public Task<IBleDevice> GetDeviceAsync(string s,CancellationToken token=default)=>Task.FromResult<IBleDevice>(s.StartsWith("80:")?Left:Right);
}
