using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UniBLE;
using UnityEngine;

/// <summary>
/// BIOFIT GAME: primera prueba de dos trackers BLE en Android.
/// No modifica el firmware ni la calibracion de las Waveshare.
/// El evento UP_IMPULSE es un candidato; no equivale a un salto confirmado.
/// </summary>
public sealed class BiofitBLEMonitor : MonoBehaviour
{
    [Header("MAC de los trackers (verificadas con nRF Connect)")]
    [SerializeField] private string leftAddress = "80:45:6B:35:DE:61";
    [SerializeField] private string rightAddress = "28:84:85:55:6B:B9";

    private const string ServiceUuid = "f9710000-6d68-4b7d-a742-6fae7b5f0001";
    private const string EventUuid = "f9710000-6d68-4b7d-a742-6fae7b5f0002";

    private IBleDevice leftDevice, rightDevice;
    private IBleCharacteristic leftEvents, rightEvents;

    private readonly ConcurrentQueue<Packet> pending = new ConcurrentQueue<Packet>();
    private readonly Tracker left = new Tracker("IZQUIERDO", 1);
    private readonly Tracker right = new Tracker("DERECHO", 2);

    private string message = "Iniciando...";
    private bool busy;
    private GUIStyle heading, normal, small;

    private sealed class Tracker
    {
        public readonly string name;
        public readonly byte id;
        public string connection = "Sin conectar";
        public string lastEvent = "Todavia no hay eventos";
        public bool moving;
        public int movOn, movOff, upImpulses;
        public Tracker(string name, byte id) { this.name = name; this.id = id; }
    }

    private struct Packet
    {
        public byte expectedId;
        public byte[] bytes;
    }

    private async void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        await ConnectBothAsync();
#else
        message = "Vista previa. La conexion BLE se prueba en una APK Android, no en Play de Windows.";
#endif
    }

    private async Task ConnectBothAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            message = "Comprobando Bluetooth...";
            IBleAdapter adapter = BleManager.Adapter;
            if (!await adapter.IsAvailableAsync())
            {
                message = "Bluetooth no disponible o desactivado.";
                return;
            }

            message = "Solicitando permisos Bluetooth...";
            if (!await adapter.RequestPermissionAsync())
            {
                message = "Permiso denegado. Habilita Dispositivos cercanos en Ajustes de Android.";
                return;
            }

            // La conexion se realiza usando las direcciones conocidas.
            // No es necesario escanear ni emparejar desde Ajustes de Android.
            await ConnectOneAsync(adapter, leftAddress, left);
            await ConnectOneAsync(adapter, rightAddress, right);
            message = "Prueba BLE: mové un tobillo y observá los eventos.";
        }
        catch (Exception ex)
        {
            message = "Error al iniciar BLE: " + ex.Message;
            Debug.LogException(ex);
        }
        finally { busy = false; }
    }

    private async Task ConnectOneAsync(IBleAdapter adapter, string address, Tracker tracker)
    {
        try
        {
            tracker.connection = "Conectando " + address + "...";
            IBleDevice device = await adapter.GetDeviceAsync(address);
            if (tracker.id == 1) leftDevice = device;
            else rightDevice = device;

            device.OnConnectionStateChanged += state =>
            {
                tracker.connection = state.ToString();
                if (state == BleConnectionState.Disconnected) tracker.moving = false;
            };

            await device.ConnectAsync();
            tracker.connection = "Buscando el servicio GATT...";
            IBleService service = await device.GetServiceAsync(ServiceUuid);
            IBleCharacteristic characteristic = await service.GetCharacteristicAsync(EventUuid);

            if (tracker.id == 1) leftEvents = characteristic;
            else rightEvents = characteristic;

            byte id = tracker.id;
            await characteristic.SubscribeAsync(bytes =>
            {
                // El callback nativo puede llegar desde otro hilo.
                if (bytes != null)
                {
                    pending.Enqueue(new Packet
                    {
                        expectedId = id,
                        bytes = (byte[])bytes.Clone()
                    });
                }
            });

            tracker.connection = "CONECTADO - NOTIFICACIONES ACTIVAS";
        }
        catch (Exception ex)
        {
            tracker.connection = "ERROR: " + ex.Message;
            Debug.LogWarning("BIOFIT " + tracker.name + ": " + ex);
        }
    }

    private void Update()
    {
        int processed = 0;
        while (processed++ < 100 && pending.TryDequeue(out Packet packet))
        {
            ReadEvent(packet);
        }
    }

    private void ReadEvent(Packet packet)
    {
        byte[] b = packet.bytes;
        if (b == null || b.Length != 16 || b[0] != 2 || b[1] != packet.expectedId)
            return;

        Tracker state = b[1] == 1 ? left : b[1] == 2 ? right : null;
        if (state == null) return;

        byte eventType = b[2];
        switch (eventType)
        {
            case 1: state.moving = true; state.movOn++; break;
            case 2: state.moving = false; state.movOff++; break;
            case 3: state.upImpulses++; break;
            default: return;
        }

        ushort sequence = U16(b, 4);
        uint millis = U32(b, 6);
        float gyroDps = I16(b, 10) / 10f;
        float verticalG = I16(b, 12) / 1000f;

        string eventName = eventType == 1 ? "MOV_ON" :
                           eventType == 2 ? "MOV_OFF" : "UP_IMPULSE";
        state.lastEvent = eventName + "   t=" + millis + " ms   seq=" + sequence +
                          "   giro=" + gyroDps.ToString("F1") + " deg/s" +
                          "   Z=" + verticalG.ToString("+0.000;-0.000;0.000") + " g";

        Debug.Log("BIOFIT " + state.name + " " + state.lastEvent);
    }

    private static ushort U16(byte[] b, int i)
    {
        return (ushort)(b[i] | (b[i + 1] << 8));
    }

    private static short I16(byte[] b, int i)
    {
        return unchecked((short)U16(b, i));
    }

    private static uint U32(byte[] b, int i)
    {
        return (uint)(b[i] | (b[i + 1] << 8) |
                      (b[i + 2] << 16) | (b[i + 3] << 24));
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(0.65f, Screen.width / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        if (heading == null)
        {
            heading = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, wordWrap = true };
            normal = new GUIStyle(GUI.skin.label) { fontSize = 30, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            heading.normal.textColor = Color.white;
            normal.normal.textColor = Color.white;
            small.normal.textColor = new Color(0.82f, 0.88f, 1f);
        }

        GUI.Box(new Rect(12, 12, width - 24, height - 24), "");
        float x = 40;
        float y = 28;
        float textWidth = width - 75;

        GUI.Label(new Rect(x, y, textWidth, 64), "BIOFIT GAME | DIAGNOSTICO BLE", heading);
        y += 76;
        GUI.Label(new Rect(x, y, textWidth, 86), message, small);
        y += 100;

        DrawTracker(left, x, ref y, textWidth);
        y += 22;
        DrawTracker(right, x, ref y, textWidth);
        y += 25;
        GUI.Label(new Rect(x, y, textWidth, 95), "UP_IMPULSE = posible elevacion. Todavia no significa JUMP.", small);
    }

    private void DrawTracker(Tracker tracker, float x, ref float y, float width)
    {
        GUI.Label(new Rect(x, y, width, 57), tracker.name, heading); y += 61;
        GUI.Label(new Rect(x, y, width, 68), tracker.connection, small); y += 74;
        GUI.Label(new Rect(x, y, width, 48),
            "ESTADO: " + (tracker.moving ? "MOVIMIENTO" : "QUIETO"), normal); y += 53;
        GUI.Label(new Rect(x, y, width, 53),
            "MOV_ON: " + tracker.movOn + "    MOV_OFF: " + tracker.movOff +
            "    UP: " + tracker.upImpulses, normal); y += 59;
        GUI.Label(new Rect(x, y, width, 104), tracker.lastEvent, small); y += 104;
    }
}
