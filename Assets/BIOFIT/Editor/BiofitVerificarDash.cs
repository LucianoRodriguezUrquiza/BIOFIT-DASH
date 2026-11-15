using System;
using UnityEditor;

/// <summary>
/// Verificacion sintetica del prototipo jugable (sin BLE ni APK).
/// No prueba Android, latencias BLE ni biomecanica.
/// </summary>
public static class BiofitVerificarDash
{
    [MenuItem("BIOFIT/Verificar BIOFIT DASH (sin APK)")]
    public static void Verificar()
    {
        int checks = 0;
        try
        {
            var game = new BiofitDashCore();
            Assert(!game.Jump(), "No saltar antes de iniciar", ref checks);
            game.StartNewGame();
            Assert(game.Playing && !game.GameOver, "Debe empezar jugando", ref checks);
            Assert(game.ObstaclesPassed == 0 && game.Distance == 0,
                "La partida comienza en cero", ref checks);
            Assert(game.Jump(), "Salto inicial permitido", ref checks);
            Assert(!game.Jump(), "No debe haber doble salto en el aire", ref checks);
            game.Step(0.02f);
            Assert(game.JumpAltitude > 0f, "El salto debe elevar al cubo", ref checks);
            for (int i = 0; i < 60; i++)
                game.Step(0.02f);
            Assert(game.JumpAltitude == 0f,
                "El cubo debe aterrizar despues del salto", ref checks);
            Assert(game.Distance > 0f,
                "El mundo debe desplazarse automaticamente", ref checks);

            game.TogglePause();
            float frozen = game.Distance;
            game.Step(0.05f);
            Assert(game.Paused && game.Distance == frozen,
                "La pausa debe detener el juego", ref checks);
            Assert(!game.Jump(), "No saltar estando en pausa", ref checks);
            game.TogglePause();
            Assert(!game.Paused, "Debe poder reanudar", ref checks);

            var crash = new BiofitDashCore();
            crash.StartNewGame();
            for (int i = 0; i < 800 && !crash.GameOver; i++)
                crash.Step(0.02f);
            Assert(crash.GameOver, "Sin saltar debe chocar con el obstaculo", ref checks);
            Assert(!crash.Jump(), "No saltar tras perder", ref checks);
            crash.StartNewGame();
            Assert(!crash.GameOver && crash.Distance == 0f,
                "Reiniciar debe limpiar la partida", ref checks);

            var survive = new BiofitDashCore();
            survive.StartNewGame();
            bool jumped = false;
            for (int i = 0; i < 800 && survive.ObstaclesPassed == 0 &&
                !survive.GameOver; i++)
            {
                if (!jumped && survive.ObstacleCount > 0 &&
                    survive.GetObstacle(0).X < 350f)
                {
                    jumped = survive.Jump();
                }
                survive.Step(0.02f);
            }
            Assert(jumped, "El salto de prueba debe iniciarse", ref checks);
            Assert(!survive.GameOver && survive.ObstaclesPassed > 0,
                "El primer obstaculo debe poder superarse", ref checks);
            Assert(survive.BestObstaclesPassed >= 1,
                "La mejor puntuacion debe registrar el obstaculo superado", ref checks);
            survive.StartNewGame();
            Assert(survive.BestObstaclesPassed >= 1 &&
                survive.ObstaclesPassed == 0,
                "La mejor puntuacion persiste al reiniciar", ref checks);

            // La siguiente APK debe permitir variar parametros sin recompilar.
            var tune = new BiofitBilateralJumpDetector();
            Assert(System.Math.Abs(tune.MinimumVerticalGThreshold - 0.12f) < 0.001f &&
                   System.Math.Abs(tune.MaxPairGapWindowSeconds - 0.18f) < 0.001f,
                "Se conservan los umbrales por defecto", ref checks);
            tune.SetThresholds(0.30f, 0.15f);
            Assert(System.Math.Abs(tune.MinimumVerticalGThreshold - 0.30f) < 0.001f &&
                   System.Math.Abs(tune.MaxPairGapWindowSeconds - 0.15f) < 0.001f,
                "Se pueden modificar los umbrales sin cambiar firmware", ref checks);
            Assert(!tune.ProcessImpulse(1, 5.00f, 0.25f),
                "Una aceleracion inferior al nuevo umbral se rechaza", ref checks);
            Assert(tune.WeakRejected == 1,
                "El nuevo umbral se aplica realmente", ref checks);
            tune.SetThresholds(0.01f, 10f);
            Assert(System.Math.Abs(tune.MinimumVerticalGThreshold - 0.08f) < 0.001f &&
                   System.Math.Abs(tune.MaxPairGapWindowSeconds - 0.40f) < 0.001f,
                "Los ajustes fuera de rango se limitan", ref checks);
            tune.SetThresholds(0.60f, 0.01f);
            Assert(System.Math.Abs(tune.MinimumVerticalGThreshold - 0.50f) < 0.001f &&
                   System.Math.Abs(tune.MaxPairGapWindowSeconds - 0.12f) < 0.001f,
                "Los limites opuestos tambien se aplican", ref checks);

            // Una sola grabacion con etiquetas de reposo, marcha,
            // carrera, elevacion y juego, incluyendo acciones de Unity.
            var recorder = new BiofitEventCapture();
            Assert(recorder.BeginGame(10f) && recorder.Recording &&
                   recorder.Stage == "QUIETO",
                "Iniciar la grabacion experimental desde Dash", ref checks);
            Assert(!recorder.BeginGame(10.1f),
                "No sobrescribir accidentalmente una grabacion activa", ref checks);
            Assert(!recorder.FinishStage("CAMINAR", 10.2f),
                "No confundir la captura libre con la guiada", ref checks);
            Assert(recorder.MarkGameStage("CAMINAR", 11f) &&
                   recorder.Stage == "CAMINAR",
                "La etapa debe actualizarse al marcar CAMINAR", ref checks);
            recorder.Record(11.1f, 1, 3, 10, 12345, 28f, 0.33f, false, 4f, 10f);
            recorder.RecordGameAction("PAR_BLE_DETECTADO", 11.2f);
            Assert(recorder.EventRows == 1 &&
                   recorder.Csv.Contains("PAR_BLE_DETECTADO") &&
                   recorder.Csv.Contains("espera_cola_unity_ms"),
                "CSV de juego combina eventos BLE, callback y acciones Unity",
                ref checks);
            Assert(recorder.MarkGameStage("CORRER", 12f) &&
                   recorder.MarkGameStage("ELEVACION", 13f) &&
                   recorder.MarkGameStage("DASH", 14f),
                "Las etapas fisicas se pueden agrupar en una sesion", ref checks);
            Assert(recorder.FinishGame(15f) && !recorder.Recording,
                "FIN JUEGO debe cerrar la grabacion", ref checks);
            Assert(recorder.Csv.Contains("FIN_JUEGO") &&
                   recorder.Csv.Contains("ETAPA_ELEVACION") &&
                   recorder.Csv.Contains("ETAPA_DASH"),
                "El CSV incluye todas las marcas temporales de la sesion",
                ref checks);
            string[] csvLines = recorder.Csv.Split('\n');
            int columnCount = csvLines[0].TrimEnd('\r').Split(';').Length;
            bool columnsAreAligned = true;
            for (int line = 1; line < csvLines.Length; line++)
            {
                if (csvLines[line].Length == 0 || csvLines[line][0] == '#') continue;
                if (csvLines[line].TrimEnd('\r').Split(';').Length != columnCount)
                    columnsAreAligned = false;
            }
            Assert(columnsAreAligned && columnCount == 12,
                "Los registros BLE, Unity y MARCA tienen 12 columnas", ref checks);
            Assert(!recorder.MarkGameStage("CAMINAR", 16f),
                "No se permiten marcas tras finalizar", ref checks);

            EditorUtility.DisplayDialog("BIOFIT DASH",
                checks + " pruebas sinteticas superadas.\n\n" +
                "Primer juego: avance automatico, salto, colision, " +
                "puntuacion, pausa y reinicio.\n" +
                "La escena se prueba desde BIOFIT > Crear o abrir BIOFIT DASH.",
                "Entendido");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
            EditorUtility.DisplayDialog("BIOFIT DASH - ERROR",
                "Prueba " + (checks + 1) + ": " + ex.Message,
                "Entendido");
        }
    }

    private static void Assert(bool condition, string message, ref int checks)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
}
