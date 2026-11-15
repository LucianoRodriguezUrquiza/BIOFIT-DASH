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
