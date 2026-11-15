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
            recorder.Record(11.1f, 1, 3, 10, 12345, 28f, 0.33f, false,
                4f, 10f, 9.88d, true, 120d);
            recorder.RecordGameAction("PAR_BLE_DETECTADO", 11.2f);
            recorder.RecordGameConfig(11.25f, 0.12f, 180f);
            Assert(recorder.Csv.Contains("UP=0.12g_PAIR=180ms"),
                "El CSV debe registrar ajustes de deteccion", ref checks);
            Assert(recorder.EventRows == 1 &&
                   recorder.Csv.Contains("PAR_BLE_DETECTADO") &&
                   recorder.Csv.Contains("espera_cola_unity_ms"),
                "CSV de juego combina eventos BLE, callback y acciones Unity",
                ref checks);
            Assert(recorder.Csv.Contains("reloj_evento_estimado_s") &&
                   recorder.Csv.Contains("reloj_alineado") &&
                   recorder.Csv.Contains("demora_relativa_estimacion_ms"),
                "CSV declara la marca temporal alineada y su demora RELATIVA",
                ref checks);
            Assert(recorder.Csv.Contains(";9.880;1;120.0"),
                "CSV conserva los valores numericos de sincronizacion",
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
            Assert(columnsAreAligned && columnCount == 15,
                "Los registros BLE, Unity, APP y MARCA tienen 15 columnas",
                ref checks);
            Assert(!recorder.MarkGameStage("CAMINAR", 16f),
                "No se permiten marcas tras finalizar", ref checks);

            // Regresion: el modo practica mantiene la adquisición
            // y no obliga a reiniciar por chocar durante un registro.
            var practice = new BiofitDashCore();
            practice.PracticeMode = true;
            practice.StartNewGame();
            for (int i = 0; i < 800; i++)
                practice.Step(0.02f);
            Assert(practice.Playing && !practice.GameOver,
                "En modo practica no debe aparecer FIN DEL JUEGO", ref checks);
            Assert(practice.ObstaclesPassed > 0,
                "El modo practica conserva obstaculos y puntos", ref checks);
            practice.StartNewGame();
            Assert(practice.PracticeMode && practice.ObstaclesPassed == 0,
                "Reiniciar no debe borrar la configuracion de practica", ref checks);
            practice.PracticeMode = false;
            for (int i = 0; i < 800 && !practice.GameOver; i++)
                practice.Step(0.02f);
            Assert(practice.GameOver,
                "Al salir de practica vuelven a funcionar colisiones", ref checks);

            // Marcas del control exploratorio por un solo tobillo.
            var solo = new BiofitEventCapture();
            Assert(solo.BeginGame(50f),
                "Puede iniciar una captura de diagnostico unilateral", ref checks);
            solo.RecordGameAction("BLE_SOLO_IZQUIERDO", 51f);
            solo.RecordSingleFootConfig(51.2f, 0.45f);
            solo.RecordGameAction("UP_SOLO_CANDIDATO", 52f);
            solo.RecordGameAction("SALTO_SOLO_REALIZADO", 52.1f);
            solo.RecordGameAction("SALTO_SOLO_NO_REALIZADO", 53f);
            Assert(solo.Csv.Contains("SOLO_UP=0.45g") &&
                   solo.Csv.Contains("BLE_SOLO_IZQUIERDO") &&
                   solo.Csv.Contains("SALTO_SOLO_REALIZADO"),
                "El CSV conserva modo y sensibilidad unilateral", ref checks);
            Assert(solo.FinishGame(54f), "Cierra sesion unilateral", ref checks);
            string[] soloLines = solo.Csv.Split('\n');
            int soloColumns = soloLines[0].TrimEnd('\r').Split(';').Length;
            bool soloValid = soloColumns == 15;
            for (int k = 1; k < soloLines.Length; k++)
            {
                if (soloLines[k].Length == 0 || soloLines[k][0] == '#')
                    continue;
                if (soloLines[k].TrimEnd('\r').Split(';').Length != soloColumns)
                    soloValid = false;
            }
            Assert(soloValid,
                "Filas de diagnostico unilateral tienen 15 columnas", ref checks);

            // Dos ESP32 sin reloj absoluto compartido: la recepcion
            // STATUS permite una alineacion aproximada sin reflashear.
            var clock = new BiofitClockAligner();
            for (int i = 0; i < 12; i++)
            {
                double now = 1000d + i * 0.05d;
                clock.ObserveStatus(1, (uint)(100000 + i * 50), now + 0.02d);
                clock.ObserveStatus(2, (uint)(500000 + i * 50), now + 0.03d);
            }
            Assert(clock.IsReady(1001.0d),
                "Ocho STATUS o mas de ambos trackers preparan estimacion temporal",
                ref checks);
            // Evaluar ambas llamadas por separado: el operador &&
            // puede omitir la segunda y dejar rightAligned sin asignar.
            double leftAligned = 0d;
            double rightAligned = 0d;
            bool mappedLeft = clock.TryMapEvent(1, 102000u, 1002.40d,
                out leftAligned);
            bool mappedRight = clock.TryMapEvent(2, 502070u, 1002.77d,
                out rightAligned);
            Assert(mappedLeft && mappedRight,
                "Las marcas IMU alineadas sobreviven al batching BLE",
                ref checks);
            Assert(Math.Abs(rightAligned - leftAligned - 0.08d) < 0.005d,
                "Diferencia de reloj estimada de 80 ms aunque callbacks difieran",
                ref checks);

            var pairAligned = new BiofitBilateralJumpDetector();
            Assert(!pairAligned.ProcessImpulse(2, rightAligned, 0.55f) &&
                   pairAligned.ProcessImpulse(1, leftAligned, 0.62f),
                "Emparejamiento inverso con reloj de IMU, no de callback",
                ref checks);
            var burst = new BiofitBilateralJumpDetector();
            burst.ProcessImpulse(2, 1.00d, 0.45f);
            burst.ProcessImpulse(2, 1.90d, 0.48f);
            Assert(burst.ProcessImpulse(1, 1.06d, 0.60f),
                "El buffer debe recordar un UP anterior al ultimo recibido",
                ref checks);
            var falseGait = new BiofitBilateralJumpDetector();
            falseGait.ProcessImpulse(2, 1d, 0.45f);
            Assert(!falseGait.ProcessImpulse(1, 1.27d, 0.50f),
                "Pasos alternados separados 270 ms NO son par bilateral",
                ref checks);
            Assert(falseGait.NearMisses == 1 &&
                   System.Math.Abs(falseGait.LastNearMissGapMs - 270f) < 1f,
                "Los casi-pares deben medirse en ms para afinar sin adivinar",
                ref checks);
            Assert(!clock.IsReady(1005d),
                "Si se pierden STATUS, hay que desactivar la alineacion",
                ref checks);
            Assert(!clock.TryMapEvent(1, 104000u, 1005.0d,
                    out double invalidMapped),
                "No usar relojes STATUS caducados en un salto real",
                ref checks);
            clock.ObserveStatus(1, 50u, 1010d);
            Assert(clock.LeftSamples == 1,
                "Al reiniciar una ESP32 deben borrarse anclas viejas",
                ref checks);

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
