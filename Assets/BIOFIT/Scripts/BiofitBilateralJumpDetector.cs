/// <summary>
/// BIOFIT GAME: detector EXPERIMENTAL de pares de UP_IMPULSE.
/// Devuelve candidatos, NUNCA confirmaciones biomecanicas de salto.
/// Usa un unico reloj local del telefono (segundos desde inicio).
/// Las marcas millis de las ESP32 no se comparan entre placas.
/// </summary>
public sealed class BiofitBilateralJumpDetector
{
    // Parametros preliminares. Se ajustaran con una sola prueba fisica combinada.
    public const float MinimumVerticalG = 0.12f;
    public const float MaxPairGapSeconds = 0.18f;
    public const float SameAnkleRefractorySeconds = 0.32f;
    public const float PairCooldownSeconds = 0.90f;

    private float lastLeftAccepted = -10000f;
    private float lastRightAccepted = -10000f;
    private float pendingLeftSeconds = -10000f;
    private float pendingRightSeconds = -10000f;
    private float lastPairSeconds = -10000f;

    public int LeftUpCandidates { get; private set; }
    public int RightUpCandidates { get; private set; }
    public int BilateralCandidates { get; private set; }
    public int WeakRejected { get; private set; }
    public int RepeatedRejected { get; private set; }
    public float LastPairGapMs { get; private set; }
    public string LastStatus { get; private set; } = "Sin impulsos suficientes";

    /// <returns>true solamente si se detecto un par NUEVO de UP simultaneos.</returns>
    public bool ProcessImpulse(byte trackerId, float receivedSeconds, float verticalG)
    {
        if (trackerId != 1 && trackerId != 2)
            return false;

        if (verticalG < MinimumVerticalG)
        {
            WeakRejected++;
            LastStatus = "Impulso insuficiente (< 0,12 g)";
            return false;
        }

        float lastSame = trackerId == 1 ? lastLeftAccepted : lastRightAccepted;
        if (receivedSeconds - lastSame < SameAnkleRefractorySeconds)
        {
            RepeatedRejected++;
            LastStatus = "Impulso repetido de un mismo tobillo";
            return false;
        }

        // Un evento nuevo de la misma pierna no debe mantener un par viejo
        // indefinidamente. Las parejas se comparan solo dentro de 180 ms.
        if (trackerId == 1)
        {
            lastLeftAccepted = receivedSeconds;
            pendingLeftSeconds = receivedSeconds;
            LeftUpCandidates++;
        }
        else
        {
            lastRightAccepted = receivedSeconds;
            pendingRightSeconds = receivedSeconds;
            RightUpCandidates++;
        }

        float otherTime = trackerId == 1 ? pendingRightSeconds : pendingLeftSeconds;
        float gap = receivedSeconds - otherTime;
        if (gap < 0f || gap > MaxPairGapSeconds)
        {
            LastStatus = "UP " + (trackerId == 1 ? "izquierdo" : "derecho") +
                         " aislado; esperando otro tobillo";
            return false;
        }

        // Despues de un par, descartar ambos pendientes incluso si
        // el cooldown sigue activo. No reutilizar viejos impulsos.
        pendingLeftSeconds = -10000f;
        pendingRightSeconds = -10000f;

        if (receivedSeconds - lastPairSeconds < PairCooldownSeconds)
        {
            LastStatus = "Par descartado por tiempo de seguridad";
            return false;
        }

        lastPairSeconds = receivedSeconds;
        BilateralCandidates++;
        LastPairGapMs = gap * 1000f;
        LastStatus = "POSIBLE SALTO BILATERAL (NO CONFIRMADO)";
        return true;
    }
}
