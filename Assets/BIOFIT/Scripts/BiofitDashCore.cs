using System;
using System.Collections.Generic;

/// <summary>
/// Primer nivel ORIGINAL tipo Geometry Dash de BIOFIT GAME.
/// Fisica y colisiones deterministas, desacopladas de BLE y de la interfaz.
/// Las unidades son pixeles virtuales de un lienzo de 1120 x 740.
/// </summary>
public sealed class BiofitDashCore
{
    public const float FloorY = 605f;
    public const float HeroX = 188f;
    public const float HeroSize = 64f;

    public struct Obstacle
    {
        public float X;
        public float Width;
        public float Height;
        public bool Scored;

        public Obstacle(float x, float width, float height)
        {
            X = x;
            Width = width;
            Height = height;
            Scored = false;
        }
    }

    private readonly List<Obstacle> obstacles = new List<Obstacle>(12);
    private float velocityY;
    private float spawnCountdown;
    private int spawned;

    public bool Playing { get; private set; }
    public bool GameOver { get; private set; }
    public bool Paused { get; private set; }
    /// <summary>
    /// Diagnostico sin game-over para no detener una sesion fisica.
    /// Solo desactiva la colision, mantiene obstaculos y puntos.
    /// </summary>
    public bool PracticeMode { get; set; }
    public float JumpAltitude { get; private set; }
    public float Distance { get; private set; }
    public float Speed { get; private set; }
    public float Elapsed { get; private set; }
    public int ObstaclesPassed { get; private set; }
    public int BestObstaclesPassed { get; private set; }
    public int SuccessfulJumps { get; private set; }
    public int ObstacleCount => obstacles.Count;

    public Obstacle GetObstacle(int index) => obstacles[index];

    public void StartNewGame()
    {
        obstacles.Clear();
        Playing = true;
        GameOver = false;
        Paused = false;
        JumpAltitude = 0f;
        velocityY = 0f;
        Elapsed = 0f;
        Distance = 0f;
        Speed = 280f;
        ObstaclesPassed = 0;
        SuccessfulJumps = 0;
        spawned = 0;
        spawnCountdown = 0.95f;
    }

    public void TogglePause()
    {
        if (Playing && !GameOver) Paused = !Paused;
    }

    public bool Jump()
    {
        if (!Playing || GameOver || Paused || JumpAltitude > 0.001f || velocityY > 0f)
            return false;
        velocityY = 575f;
        SuccessfulJumps++;
        return true;
    }

    public void Step(float dt)
    {
        if (!Playing || GameOver || Paused) return;
        dt = Math.Max(0f, Math.Min(dt, 0.05f));
        if (dt <= 0f) return;

        Elapsed += dt;
        Speed = Math.Min(405f, 280f + Elapsed * 1.6f);
        Distance += Speed * dt;

        if (JumpAltitude > 0f || velocityY > 0f)
        {
            JumpAltitude += velocityY * dt;
            velocityY -= 1560f * dt;
            if (JumpAltitude <= 0f)
            {
                JumpAltitude = 0f;
                velocityY = 0f;
            }
        }

        spawnCountdown -= dt;
        if (spawnCountdown <= 0f)
        {
            Spawn();
            // Intervalos siempre mayores al ancho horizontal del salto.
            spawnCountdown = 1.65f + (spawned % 3) * 0.20f;
        }

        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            Obstacle o = obstacles[i];
            o.X -= Speed * dt;

            if (!o.Scored && o.X + o.Width < HeroX)
            {
                o.Scored = true;
                ObstaclesPassed++;
                if (ObstaclesPassed > BestObstaclesPassed)
                    BestObstaclesPassed = ObstaclesPassed;
            }

            if (!PracticeMode && Collides(o))
            {
                GameOver = true;
                Playing = false;
            }

            if (o.X + o.Width < -90f)
                obstacles.RemoveAt(i);
            else
                obstacles[i] = o;

            if (GameOver) return;
        }
    }

    private void Spawn()
    {
        // Obstaculos espaciados; una sola punta por grupo para la primera demo.
        float height = 47f + (spawned % 3) * 7f;
        obstacles.Add(new Obstacle(1080f, 56f, height));
        spawned++;
    }

    private bool Collides(Obstacle o)
    {
        const float inset = 9f;
        float left = HeroX + 8f;
        float right = HeroX + HeroSize - 8f;
        float top = FloorY - HeroSize - JumpAltitude + 8f;
        float bottom = FloorY - JumpAltitude;

        return right > o.X + inset &&
               left < o.X + o.Width - inset &&
               bottom > FloorY - o.Height + 8f &&
               top < FloorY - 2f;
    }
}
