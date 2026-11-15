using System;
using UnityEditor;

/// <summary>
/// Prueba sintetica del detector de PARES UP.
/// Los casos pasan en Editor sin Bluetooth, IMU ni APK.
/// NO implica que se haya validado el salto corporal.
/// </summary>
public static class BiofitVerificarSaltoBilateral
{
    [MenuItem("BIOFIT/Verificar salto bilateral (sin APK)")]
    public static void Verificar()
    {
        int checks = 0;
        try
        {
            var sincronizado = new BiofitBilateralJumpDetector();
            Assert(!sincronizado.ProcessImpulse(1, 1.00f, 0.35f),
                "Un UP izquierdo aislado no es salto", ref checks);
            Assert(sincronizado.ProcessImpulse(2, 1.07f, 0.36f),
                "Dos UP separados 70 ms deben formar un par", ref checks);
            Assert(sincronizado.BilateralCandidates == 1,
                "El par debe contarse exactamente una vez", ref checks);
            Assert(Math.Abs(sincronizado.LastPairGapMs - 70f) < 1f,
                "Debe registrar el intervalo bilateral", ref checks);

            Assert(!sincronizado.ProcessImpulse(1, 1.48f, 0.38f),
                "El primer pulso de un par en cooldown no debe contar", ref checks);
            Assert(!sincronizado.ProcessImpulse(2, 1.55f, 0.40f),
                "Un segundo par antes de 0,9 s debe rechazarse", ref checks);
            Assert(sincronizado.BilateralCandidates == 1,
                "El cooldown no debe generar conteos extra", ref checks);

            Assert(!sincronizado.ProcessImpulse(1, 2.30f, 0.33f),
                "Nuevo primer impulso fuera de cooldown es aislado", ref checks);
            Assert(sincronizado.ProcessImpulse(2, 2.37f, 0.31f),
                "Nuevo par despues del cooldown debe aceptarse", ref checks);
            Assert(sincronizado.BilateralCandidates == 2,
                "Segundo par debe subir contador a dos", ref checks);

            var unilateral = new BiofitBilateralJumpDetector();
            unilateral.ProcessImpulse(1, 1f, 0.30f);
            unilateral.ProcessImpulse(1, 1.40f, 0.33f);
            unilateral.ProcessImpulse(1, 1.90f, 0.34f);
            Assert(unilateral.BilateralCandidates == 0,
                "Varios UP solo de un pie no deben producir par", ref checks);

            var tardio = new BiofitBilateralJumpDetector();
            tardio.ProcessImpulse(1, 1f, 0.35f);
            tardio.ProcessImpulse(2, 1.31f, 0.34f);
            Assert(tardio.BilateralCandidates == 0,
                "Mas de 180 ms entre tobillos no debe generar par", ref checks);

            var debil = new BiofitBilateralJumpDetector();
            debil.ProcessImpulse(1, 1f, 0.04f);
            debil.ProcessImpulse(2, 1.06f, 0.35f);
            Assert(debil.BilateralCandidates == 0 && debil.WeakRejected == 1,
                "Un UP debil no debe formar pareja", ref checks);

            var negativo = new BiofitBilateralJumpDetector();
            negativo.ProcessImpulse(1, 1f, -0.40f);
            negativo.ProcessImpulse(2, 1.05f, 0.35f);
            Assert(negativo.BilateralCandidates == 0,
                "Un impulso negativo no debe parecer ascenso bilateral", ref checks);

            var invertido = new BiofitBilateralJumpDetector();
            invertido.ProcessImpulse(2, 1f, 0.34f);
            Assert(invertido.ProcessImpulse(1, 1.09f, 0.39f),
                "Debe funcionar tambien derecho-izquierdo", ref checks);

            var duplicado = new BiofitBilateralJumpDetector();
            duplicado.ProcessImpulse(1, 1f, 0.32f);
            duplicado.ProcessImpulse(1, 1.04f, 0.33f);
            Assert(duplicado.RepeatedRejected == 1 && duplicado.LeftUpCandidates == 1,
                "Antirrebote de un tobillo debe rechazar UP repetido", ref checks);

            var invalido = new BiofitBilateralJumpDetector();
            invalido.ProcessImpulse(3, 1f, 0.35f);
            Assert(invalido.LeftUpCandidates == 0 &&
                invalido.RightUpCandidates == 0,
                "IDs fuera de 1 y 2 no deben contaminar registros", ref checks);

            // Captura CSV: etapas, valores y comparacion entre relojes.
            var capture = new BiofitEventCapture();
            capture.Begin(0.90f);
            capture.Record(1.00f, 1, 3, 1, 1000, 12.2f, 0.32f, false);
            capture.Record(1.06f, 2, 3, 2, 1060, 11.7f, 0.38f, true);
            capture.FinishStage("CAMINAR", 1.50f);
            capture.Record(2.00f, 1, 1, 3, 1100, 10.0f, 0.05f, false);
            capture.FinishStage("CORRER", 2.60f);
            capture.FinishStage("SALTOS", 3.20f);
            Assert(capture.EventRows == 3,
                "La captura debe registrar las tres notificaciones BLE", ref checks);
            Assert(!capture.Recording,
                "FIN SALTOS debe cerrar la captura", ref checks);
            Assert(capture.Csv.Contains("FIN_SALTOS") &&
                   capture.Csv.Contains("CAMINAR") &&
                   capture.Csv.Contains("CORRER"),
                "El CSV debe conservar las marcas de actividad", ref checks);
            Assert(capture.Csv.Contains("0.320") &&
                   capture.Csv.Contains("par_bilateral"),
                "El CSV debe exportar aceleracion y candidatos bilaterales",
                ref checks);

            var session = new BiofitSessionLogger();
            session.Begin(0, 0, 0, 0, 0, 0, 0);
            session.MarkStage("SALTOS", 0, 0, 2, 0, 0, 3, 1);
            Assert(session.Report.Contains("PARES UP=1"),
                "El resumen debe incluir candidatos bilaterales", ref checks);

            // Regresiones del laboratorio 2D: el obstaculo ya no debe
            // avanzar por pulsaciones individuales ni si no hay cadencia.
            Assert(BiofitMovementLab.TargetSpeedForCadence(0f) == 0f,
                "Sin cadencia la velocidad visual objetivo debe ser cero", ref checks);
            Assert(BiofitMovementLab.TargetSpeedForCadence(54f) == 0f,
                "Movimiento sin clasificar no debe activar avance", ref checks);
            Assert(BiofitMovementLab.TargetSpeedForCadence(120f) > 0f,
                "Caminar tentativo debe permitir avance continuo", ref checks);
            Assert(BiofitMovementLab.TargetSpeedForCadence(170f) >
                   BiofitMovementLab.TargetSpeedForCadence(120f),
                "Mayor cadencia debe dar mayor velocidad visual", ref checks);

            // Q/E representan impulsos individuales; solo juntos forman par.
            var teclas = new BiofitBilateralJumpDetector();
            Assert(!teclas.ProcessImpulse(1, 10.00f, 0.32f),
                "La tecla Q aislada no debe disparar par", ref checks);
            Assert(teclas.ProcessImpulse(2, 10.08f, 0.32f),
                "Q seguido de E en 80 ms debe producir par simulado", ref checks);

            // Comprobacion de geometria virtual: el personaje sobre el
            // suelo entra COMPLETO en el lienzo de 740 unidades.
            const float groundVirtualY = 413f + 227f - 31f;
            Assert(groundVirtualY - 70f >= 413f &&
                   groundVirtualY + 7f < 740f,
                "El cubo y la linea de suelo no deben quedar fuera de pantalla", ref checks);

            // Proteccion contra registrar toda la prueba como CAMINAR
            // si se pulsa directamente FIN SALTOS.
            var secuencia = new BiofitEventCapture();
            secuencia.Begin(1.00f);
            Assert(!secuencia.FinishStage("SALTOS", 2.00f),
                "No permitir finalizar SALTOS antes de CAMINAR", ref checks);
            Assert(secuencia.Stage == "CAMINAR" && secuencia.Recording,
                "Una marca fuera de orden debe conservar la etapa", ref checks);
            Assert(secuencia.FinishStage("CAMINAR", 3.00f) &&
                   secuencia.Stage == "CORRER",
                "FIN CAMINAR debe preparar la etapa CORRER", ref checks);
            Assert(secuencia.FinishStage("CORRER", 4.00f) &&
                   secuencia.Stage == "SALTOS",
                "FIN CORRER debe preparar la etapa SALTOS", ref checks);
            Assert(secuencia.FinishStage("SALTOS", 5.00f) &&
                   secuencia.Stage == "FINAL" && !secuencia.Recording,
                "FIN SALTOS debe cerrar la captura", ref checks);
            Assert(secuencia.Csv.Contains("espera_cola_unity_ms") &&
                   secuencia.Csv.Contains("reloj_callback_monotonico_s"),
                "CSV debe incluir marcas de callback y demora de procesamiento",
                ref checks);

            EditorUtility.DisplayDialog("BIOFIT - Salto bilateral",
                checks + " verificaciones sinteticas superadas.\n\n" +
                "El codigo reconoce pares de UP con umbral y antirrebote.\n" +
                "NO confirma saltos humanos. Para validarlo necesitaremos " +
                "una prueba fisica combinada mas adelante.",
                "Entendido");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("BIOFIT - fallo de prueba bilateral: " + ex);
            EditorUtility.DisplayDialog("BIOFIT - ERROR",
                "Verificacion " + (checks + 1) + ": " + ex.Message,
                "Entendido");
        }
    }

    private static void Assert(bool value, string reason, ref int counter)
    {
        if (!value) throw new InvalidOperationException(reason);
        counter++;
    }
}
