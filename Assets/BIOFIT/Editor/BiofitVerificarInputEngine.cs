using System;
using UnityEditor;

/// <summary>
/// Autoverificacion determinista del clasificador sin sensores ni Android.
/// No cambia escenas ni firmware. Se ejecuta desde el menu BIOFIT.
/// </summary>
public static class BiofitVerificarInputEngine
{
    [MenuItem("BIOFIT/Verificar Input Engine (sin APK)")]
    public static void Verificar()
    {
        try
        {
            // A-D-A-D a 0.5 s: ~120 candidatos por minuto.
            var alternado = new BiofitInputEngine();
            Step(alternado, 1, 1.0f);
            Step(alternado, 2, 1.5f);
            Step(alternado, 1, 2.0f);
            Step(alternado, 2, 2.5f);
            Check(alternado.Activity == "CAMINANDO (TENTATIVO)",
                "A-D-A-D debe clasificar caminando (tentativo)");
            Check(Math.Abs(alternado.EstimatedCadence - 120f) < 0.5f,
                "La cadencia de 0,5 s debe dar aprox 120/min");

            // Repetir solo A rompe la cadena que existia antes.
            Step(alternado, 1, 3.0f);
            Step(alternado, 1, 3.5f);
            Check(alternado.EstimatedCadence == 0f,
                "A-A debe borrar la cadencia anterior");
            Check(alternado.Activity == "MOVIMIENTO SIN CLASIFICAR",
                "A-A no debe clasificar caminar/correr");

            // Inactividad real hace desaparecer actividad y cadencia.
            alternado.Update(6.0f);
            Check(alternado.Activity == "QUIETO",
                "Sin actividad reciente debe indicar quieto");

            var unilateral = new BiofitInputEngine();
            Step(unilateral, 2, 1.0f);
            Step(unilateral, 2, 1.5f);
            Step(unilateral, 2, 2.0f);
            Step(unilateral, 2, 2.5f);
            Check(unilateral.EstimatedCadence == 0f &&
                  unilateral.Alternations == 0,
                  "D-D-D-D no debe contar alternancia ni cadencia");

            // Alternancia rapida a 0,35 s (~171/min).
            var rapido = new BiofitInputEngine();
            Step(rapido, 1, 1.0f);
            Step(rapido, 2, 1.35f);
            Step(rapido, 1, 1.70f);
            Step(rapido, 2, 2.05f);
            Check(rapido.Activity == "CORRIENDO (TENTATIVO)",
                "Alternancia rapida debe clasificar corriendo tentativo");

            // Tiempo largo rompe secuencia previa.
            Step(rapido, 1, 4.20f);
            Check(rapido.EstimatedCadence == 0f,
                "Pausa mayor al limite debe resetear cadencia");

            // UP aislados no constituyen pasos.
            var impulsos = new BiofitInputEngine();
            impulsos.ProcessEvent(1, 3, 1f);
            impulsos.ProcessEvent(2, 3, 1.5f);
            impulsos.Update(1.5f);
            Check(impulsos.LeftCandidates == 0 && impulsos.RightCandidates == 0,
                "UP_IMPULSE aislado no debe contar como un MOV_ON");

            EditorUtility.DisplayDialog("BIOFIT Input Engine",
                "9 verificaciones superadas.\n\n" +
                "La logica paso los casos sinteticos en Unity. " +
                "Esto NO valida clasificacion real con las Waveshare.",
                "Entendido");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("BIOFIT: verificacion fallida: " + ex.Message);
            EditorUtility.DisplayDialog("BIOFIT - VERIFICACION FALLIDA",
                ex.Message, "Entendido");
        }
    }

    private static void Step(BiofitInputEngine engine, byte side, float seconds)
    {
        engine.ProcessEvent(side, 1, seconds);
        engine.ProcessEvent(side, 2, seconds);
        engine.Update(seconds);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
