using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Captura acotada de eventos BLE de una sesion para analisis posterior.
/// No guarda archivos automaticamente: el usuario decide copiar un CSV.
/// El reloj `phoneSeconds` es comun a ambos tobillos; millis de ESP32 NO.
/// </summary>
public sealed class BiofitEventCapture
{
    private const int MaxRows = 1500;
    private readonly List<string> rows = new List<string>(512);
    private readonly CultureInfo invariant = CultureInfo.InvariantCulture;
    private string stage = "CAMINAR";
    private bool gameMode;

    public bool Recording { get; private set; }
    public string Stage => stage;
    public int EventRows { get; private set; }
    public int DroppedRows { get; private set; }

    public void Begin(float phoneSeconds)
    {
        rows.Clear();
        gameMode = false;
        stage = "CAMINAR";
        Recording = true;
        EventRows = 0;
        DroppedRows = 0;
        Mark("INICIO", phoneSeconds);
    }

    /// <summary>
    /// Sesion libre del juego. Permite marcar varias actividades dentro de
    /// una misma APK y una sola captura, sin alterar la prueba guiada antigua.
    /// </summary>
    public bool BeginGame(float phoneSeconds)
    {
        if (Recording) return false;
        Begin(phoneSeconds);
        gameMode = true;
        stage = "QUIETO";
        Mark("INICIO_JUEGO", phoneSeconds);
        return true;
    }

    public bool MarkGameStage(string nextStage, float phoneSeconds)
    {
        if (!Recording || !gameMode) return false;
        if (nextStage != "QUIETO" && nextStage != "CAMINAR" &&
            nextStage != "CORRER" && nextStage != "ELEVACION" &&
            nextStage != "DASH") return false;
        stage = nextStage;
        Mark("ETAPA_" + nextStage, phoneSeconds);
        return true;
    }

    public void RecordGameAction(string action, float phoneSeconds)
    {
        if (!Recording || !gameMode || rows.Count >= MaxRows) return;
        // Misma cabecera de 12 columnas; este evento proviene de Unity,
        // NO del sensor. Las columnas de la IMU quedan vacias.
        if (action != "SALTO_MANUAL" && action != "PAR_BLE_DETECTADO" &&
            action != "SALTO_BLE_REALIZADO" &&
            action != "SALTO_BLE_NO_REALIZADO" &&
            action != "BLE_ACTIVADO" && action != "BLE_DESACTIVADO") return;
        rows.Add("APP;" + stage + ";" +
            phoneSeconds.ToString("F3", invariant) + ";;" + action + ";;;;;;;");
    }

    public bool FinishGame(float phoneSeconds)
    {
        if (!Recording || !gameMode) return false;
        Mark("FIN_JUEGO", phoneSeconds);
        stage = "FINAL";
        Recording = false;
        gameMode = false;
        return true;
    }

    /// <summary>Marca fin de actividad y habilita la etapa siguiente.</summary>
    public bool FinishStage(string activity, float phoneSeconds)
    {
        // No permitir saltar etapas ni adjudicar la etiqueta incorrecta.
        if (!Recording || gameMode || stage != activity) return false;
        Mark("FIN_" + activity, phoneSeconds);
        switch (activity)
        {
            case "CAMINAR": stage = "CORRER"; break;
            case "CORRER": stage = "SALTOS"; break;
            case "SALTOS":
                stage = "FINAL";
                Recording = false;
                break;
        }
        return true;
    }

    public void Record(float phoneSeconds, byte id, byte eventType,
        ushort sequence, uint sensorMilliseconds, float gyroDps,
        float verticalG, bool bilateralCandidate,
        float unityQueueDelayMs = 0f, double callbackMonotonicSeconds = 0d)
    {
        if (!Recording) return;
        if (rows.Count >= MaxRows)
        {
            DroppedRows++;
            return;
        }

        string name = eventType == 1 ? "MOV_ON" :
                      eventType == 2 ? "MOV_OFF" :
                      eventType == 3 ? "UP_IMPULSE" : "OTRO";

        // Formato CSV compatible con separador de listas habitual de Excel ES.
        rows.Add("EV;" + stage + ";" +
                 phoneSeconds.ToString("F3", invariant) + ";" +
                 id + ";" + name + ";" +
                 sensorMilliseconds + ";" +
                 sequence + ";" +
                 gyroDps.ToString("F1", invariant) + ";" +
                 verticalG.ToString("F3", invariant) + ";" +
                 (bilateralCandidate ? "1" : "0") + ";" +
                 unityQueueDelayMs.ToString("F1", invariant) + ";" +
                 callbackMonotonicSeconds.ToString("F3", invariant));
        EventRows++;
    }

    public string Csv
    {
        get
        {
            var sb = new StringBuilder(rows.Count * 85 + 200);
            sb.AppendLine("tipo;etapa;reloj_unity_procesado_s;tracker_id;evento;reloj_esp32_ms;secuencia;giro_dps;acel_vertical_g;par_bilateral;espera_cola_unity_ms;reloj_callback_monotonico_s");
            foreach (string row in rows)
                sb.AppendLine(row);
            sb.AppendLine("# filas_eventos=" + EventRows +
                          "; descartadas_por_limite=" + DroppedRows);
            sb.AppendLine("# ATENCION: eventos experimentales; NO son pasos ni saltos confirmados");
            return sb.ToString();
        }
    }

    private void Mark(string name, float phoneSeconds)
    {
        if (rows.Count >= MaxRows)
        {
            DroppedRows++;
            return;
        }
        rows.Add("MARCA;" + name + ";" +
                 phoneSeconds.ToString("F3", invariant) + ";;;;;;;;;");
    }
}
