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

    public bool Recording { get; private set; }
    public string Stage => stage;
    public int EventRows { get; private set; }
    public int DroppedRows { get; private set; }

    public void Begin(float phoneSeconds)
    {
        rows.Clear();
        stage = "CAMINAR";
        Recording = true;
        EventRows = 0;
        DroppedRows = 0;
        Mark("INICIO", phoneSeconds);
    }

    /// <summary>Marca fin de actividad y habilita la etapa siguiente.</summary>
    public bool FinishStage(string activity, float phoneSeconds)
    {
        // No permitir saltar etapas ni adjudicar la etiqueta incorrecta.
        if (!Recording || stage != activity) return false;
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
