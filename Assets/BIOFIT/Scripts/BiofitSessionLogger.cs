using System.Collections.Generic;
using System.Text;

/// <summary>
/// Registro compacto de una prueba: evita reiniciar la APK entre etapas.
/// Guarda diferencias de contadores; no supone que UP_IMPULSE equivalga a pasos.
/// </summary>
public sealed class BiofitSessionLogger
{
    private struct Mark
    {
        public int leftOn, leftOff, leftUp, rightOn, rightOff, rightUp, pairs;
    }

    private readonly List<string> entries = new List<string>();
    private Mark last;
    private bool started;
    private float maxCadence;
    private readonly StringBuilder report = new StringBuilder(1024);

    public bool Started => started;
    public int MarkCount => entries.Count;
    public string LastMark => entries.Count > 0 ? entries[entries.Count - 1] : "Sin registros.";

    public string Report
    {
        get
        {
            report.Length = 0;
            report.AppendLine("BIOFIT GAME | SESION EXPERIMENTAL");
            report.AppendLine("Las cifras son eventos, no pasos confirmados.");
            foreach (string item in entries) report.AppendLine(item);
            return report.ToString();
        }
    }

    public void ObserveCadence(float estimatedCadence)
    {
        if (started && estimatedCadence > maxCadence)
            maxCadence = estimatedCadence;
    }

    public void Begin(int lOn, int lOff, int lUp, int rOn, int rOff, int rUp, int bilateralPairs)
    {
        entries.Clear();
        last = Capture(lOn, lOff, lUp, rOn, rOff, rUp, bilateralPairs);
        started = true;
        maxCadence = 0f;
        entries.Add("INICIO - punto de referencia de contadores");
    }

    public void MarkStage(string name, int lOn, int lOff, int lUp,
        int rOn, int rOff, int rUp, int bilateralPairs)
    {
        if (!started) return;
        Mark current = Capture(lOn, lOff, lUp, rOn, rOff, rUp, bilateralPairs);
        string line = name +
            " | IZQ ON=" + Diff(current.leftOn, last.leftOn) +
            " OFF=" + Diff(current.leftOff, last.leftOff) +
            " UP=" + Diff(current.leftUp, last.leftUp) +
            " | DER ON=" + Diff(current.rightOn, last.rightOn) +
            " OFF=" + Diff(current.rightOff, last.rightOff) +
            " UP=" + Diff(current.rightUp, last.rightUp) +
            " | PARES UP=" + Diff(current.pairs, last.pairs) +
            " (NO confirmados)" +
            " | pico cadencia estimada=" + maxCadence.ToString("F0") + "/min";
        entries.Add(line);
        last = current;
        maxCadence = 0f;
    }

    private static int Diff(int now, int before) => System.Math.Max(0, now - before);

    private static Mark Capture(int lOn, int lOff, int lUp,
        int rOn, int rOff, int rUp, int bilateralPairs)
    {
        return new Mark
        {
            leftOn=lOn, leftOff=lOff, leftUp=lUp,
            rightOn=rOn, rightOff=rOff, rightUp=rUp, pairs=bilateralPairs
        };
    }
}
