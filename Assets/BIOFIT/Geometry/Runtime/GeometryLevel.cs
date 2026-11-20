using System;
using System.Collections.Generic;

namespace Biofit.Geometry
{
    // Adapted from GeomeTry Level / MapItem (MIT; ThirdParty/GeomeTry/LICENSE).
    // Explicit catalog avoids the duplicate id=2 in the original maps.
    [Serializable] public sealed class GeometryLevel
    {
        public int id;
        public string title;
        public string sourceFile;
        public string sourceSha256;
        public float finishX;
        public GeometryObstacle[] obstacles;
        public GeometrySection[] sections;
        public static readonly string[] ResourceNames = {
            "StereoMadness", "BackOnTrack", "Polargeist", "JumpyNightmare"
        };

        public void Validate(int expectedId)
        {
            if (id != expectedId || string.IsNullOrEmpty(title) ||
                !Finite(finishX) || finishX <= 10 || obstacles == null || sections == null || sections.Length == 0)
                throw new InvalidOperationException("Catalogo de nivel invalido: " + sourceFile);
            var ids = new HashSet<int>();
            foreach (var o in obstacles)
            {
                if (!ids.Add(o.id) || !Finite(o.x) || !Finite(o.y) || !Finite(o.width) || !Finite(o.height) ||
                    o.width <= 0 || o.height <= 0 || o.x >= finishX || o.x < 0 ||
                    (o.kind != "spike" && o.kind != "block" && o.kind != "pad" && o.kind != "orb"))
                    throw new InvalidOperationException("Objeto invalido en " + title);
            }
            float previous = -100;
            foreach (var s in sections)
            {
                if (!Finite(s.jumpX) || s.jumpX - previous < GeometryPhysics.MinimumSpacing - .001f ||
                    s.sourceIndices == null || s.sourceIndices.Length == 0)
                    throw new InvalidOperationException("Cadencia o procedencia invalida en " + title);
                previous = s.jumpX;
            }
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
    [Serializable] public sealed class GeometryObstacle
    {
        public int id;
        public string kind;
        public float x, y, width, height;
    }
    [Serializable] public sealed class GeometrySection
    {
        public int sourceStart, sourceEnd;
        public int[] sourceIndices;
        public string adaptation;
        public float jumpX;
        public bool automaticPad;
    }
    public static class GeometryPhysics
    {
        // GeomeTry's 6.5 / 13.5 / (4*9.81), at timeScale=1 instead of 1.5.
        public const float Speed = 6.5f, JumpVelocity = 13.5f, Gravity = 39.24f;
        public const float CubeSize = .9f, FixedStep = .02f;
        public const float MinimumSpacing = 7.8f; // 1.2 s at Speed; detector still 900 ms.
        public const float SpawnX = -6f;
    }
}
