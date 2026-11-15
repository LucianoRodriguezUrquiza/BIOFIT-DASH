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
