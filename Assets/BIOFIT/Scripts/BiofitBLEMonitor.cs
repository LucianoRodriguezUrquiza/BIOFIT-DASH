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
    private readonly BiofitInputEngine inputEngine = new BiofitInputEngine();
    public BiofitInputEngine InputEngine => inputEngine;
    private readonly BiofitBilateralJumpDetector jumpDetector = new BiofitBilateralJumpDetector();
    public BiofitBilateralJumpDetector JumpDetector => jumpDetector;

    [SerializeField] private bool showDiagnostics = true;
    public bool ShowDiagnostics
    {
        get => showDiagnostics;
        set => showDiagnostics = value;
    }
    private readonly BiofitSessionLogger sessionLogger = new BiofitSessionLogger();
    private readonly BiofitEventCapture eventCapture = new BiofitEventCapture();

    private string message = "Iniciando...";
    private bool busy;
    private GUIStyle heading, normal, small, buttonStyle;

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
            // Android 12+ requiere BLUETOOTH_CONNECT antes de consultar
            // BluetoothAdapter.isEnabled() (UniBLE.IsAvailableAsync).
            // Si consultamos antes de pedir permiso puede devolver false
            // aunque el Bluetooth este encendido.
            message = "Solicitando permisos de Dispositivos cercanos...";
            IBleAdapter adapter = BleManager.Adapter;
            if (!await adapter.RequestPermissionAsync())
            {
                message = "Permiso Bluetooth denegado. En Android: Ajustes > Aplicaciones > BIOFIT-GAME-UNITY > Permisos > Dispositivos cercanos > Permitir. Luego cerra y abri la app.";
                return;
            }

            message = "Comprobando Bluetooth con permisos autorizados...";
            if (!await adapter.IsAvailableAsync())
            {
                message = "Bluetooth no disponible o apagado. Activalo desde Android y volve a abrir la app.";
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
        inputEngine.Update(Time.realtimeSinceStartup);
        sessionLogger.ObserveCadence(inputEngine.EstimatedCadence);
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

        // Los millis de cada tracker no comparten origen: usamos
        // tiempo local de recepcion del celular para la alternancia.
        float receivedSeconds = Time.realtimeSinceStartup;
        inputEngine.ProcessEvent(state.id, eventType, receivedSeconds);

        ushort sequence = U16(b, 4);
        uint millis = U32(b, 6);
        float gyroDps = I16(b, 10) / 10f;
        float verticalG = I16(b, 12) / 1000f;
        bool paired = eventType == 3 &&
            jumpDetector.ProcessImpulse(state.id, receivedSeconds, verticalG);
        if (paired)
        {
            Debug.Log("BIOFIT: POSIBLE SALTO BILATERAL (NO CONFIRMADO), intervalo " +
                      jumpDetector.LastPairGapMs.ToString("F0") + " ms");
        }
        eventCapture.Record(receivedSeconds, state.id, eventType,
            sequence, millis, gyroDps, verticalG, paired);

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
        if (!showDiagnostics) return;
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
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, wordWrap = true };
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
        y += 111;
        GUI.Label(new Rect(x, y, textWidth, 70),
            "PARES UP: " + jumpDetector.BilateralCandidates +
            " | Ultimo intervalo: " + jumpDetector.LastPairGapMs.ToString("F0") + " ms", normal);
        y += 65;
        GUI.Label(new Rect(x, y, textWidth, 77),
            jumpDetector.LastStatus + " | filtro preliminar, NO salto real", small);
        y += 83;
        GUI.Label(new Rect(x, y, textWidth, 62), "BIOFIT INPUT ENGINE (EXPERIMENTAL)", heading);
        y += 70;
        GUI.Label(new Rect(x, y, textWidth, 60),
            "Actividad: " + inputEngine.Activity, normal);
        y += 63;
        GUI.Label(new Rect(x, y, textWidth, 60),
            "Candidatos MOV_ON: Izq " + inputEngine.LeftCandidates +
            "  |  Der " + inputEngine.RightCandidates, normal);
        y += 63;
        GUI.Label(new Rect(x, y, textWidth, 60),
            "Alternancias: " + inputEngine.Alternations +
            "   |   Cadencia estimada: " + inputEngine.EstimatedCadence.ToString("F0") + " /min", normal);
        y += 70;
        GUI.Label(new Rect(x, y, textWidth, 110),
            "Aviso: los candidatos NO son pasos confirmados. Clasificacion preliminar; falta validar con pruebas.", small);
        y += 111;
        DrawSessionControls(x, y, textWidth);
    }

    private void DrawSessionControls(float x, float y, float width)
    {
        GUI.Label(new Rect(x, y, width, 56), "REGISTRO DE PRUEBA COMBINADA", heading);
        y += 58;
        float buttonWidth = (width - 12f) * 0.5f;
        if (GUI.Button(new Rect(x, y, buttonWidth, 63), "1. INICIAR", buttonStyle))
        {
            sessionLogger.Begin(left.movOn, left.movOff, left.upImpulses,
                right.movOn, right.movOff, right.upImpulses, jumpDetector.BilateralCandidates);
            eventCapture.Begin(Time.realtimeSinceStartup);
            message = "Sesion iniciada: realiza la actividad y registra el final de cada etapa.";
        }
        if (GUI.Button(new Rect(x + buttonWidth + 12f, y, buttonWidth, 63),
            "2. FIN CAMINAR", buttonStyle))
        {
            sessionLogger.MarkStage("CAMINAR", left.movOn, left.movOff,
                left.upImpulses, right.movOn, right.movOff, right.upImpulses,
                jumpDetector.BilateralCandidates);
            eventCapture.FinishStage("CAMINAR", Time.realtimeSinceStartup);
        }
        y += 72;
        if (GUI.Button(new Rect(x, y, buttonWidth, 63), "3. FIN CORRER", buttonStyle))
        {
            sessionLogger.MarkStage("CORRER", left.movOn, left.movOff,
                left.upImpulses, right.movOn, right.movOff, right.upImpulses,
                jumpDetector.BilateralCandidates);
            eventCapture.FinishStage("CORRER", Time.realtimeSinceStartup);
        }
        if (GUI.Button(new Rect(x + buttonWidth + 12f, y, buttonWidth, 63),
            "4. FIN SALTOS", buttonStyle))
        {
            sessionLogger.MarkStage("SALTOS", left.movOn, left.movOff,
                left.upImpulses, right.movOn, right.movOff, right.upImpulses,
                jumpDetector.BilateralCandidates);
            eventCapture.FinishStage("SALTOS", Time.realtimeSinceStartup);
        }
        y += 72;
        if (GUI.Button(new Rect(x, y, buttonWidth, 63),
            "COPIAR RESUMEN", buttonStyle))
        {
            GUIUtility.systemCopyBuffer = sessionLogger.Report;
            message = "Resumen copiado al portapapeles del celular.";
        }
        if (GUI.Button(new Rect(x + buttonWidth + 12f, y, buttonWidth, 63),
            "COPIAR CSV", buttonStyle))
        {
            GUIUtility.systemCopyBuffer = eventCapture.Csv;
            message = "CSV BLE copiado. Pegalo en un archivo de texto o en el chat.";
        }
        y += 76;
        GUI.Label(new Rect(x, y, width, 52),
            "Registro: " + eventCapture.EventRows + " eventos | descartados: " +
            eventCapture.DroppedRows + " | maximo 1500 filas", small);
        y += 58;
        GUI.Label(new Rect(x, y, width, 120),
            sessionLogger.Started ? sessionLogger.LastMark :
            "Registro combinado: INICIAR -> FIN CAMINAR -> FIN CORRER -> FIN SALTOS -> COPIAR.",
            small);
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
