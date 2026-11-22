using System;
using UnityEngine;

namespace Biofit.GeomeTryOriginal
{
    /// <summary>
    /// Recompose the user's unmodified 1672x941 menu reference into separate,
    /// transparent card and decoration layers. Runs once on first entering
    /// Level Selection, never each frame and never in the gameplay physics loop.
    /// No new third-party game assets or in-scene original level objects.
    /// </summary>
    internal sealed class BiofitUiArtwork : IDisposable
    {
        internal const int ReferenceWidth = 1672;
        internal const int ReferenceHeight = 941;
        internal const int CardTop = 339;
        internal const int CardBottom = 656;
        internal static readonly int[] CardLeft = { 138, 414, 689, 963, 1238 };
        internal const int CardWidth = 272;

        internal readonly Texture2D background;
        internal readonly Texture2D logo;
        internal readonly Texture2D[] cards = new Texture2D[5];
        internal readonly Texture2D[] corners = new Texture2D[4];

        private readonly Texture2D[] owned = new Texture2D[11];
        private int ownedCount;

        internal static bool Supports(Texture2D source)
        {
            return source != null && source.isReadable &&
                source.width == ReferenceWidth && source.height == ReferenceHeight;
        }

        internal BiofitUiArtwork(Texture2D original)
        {
            if (!Supports(original))
                throw new ArgumentException(
                    "La imagen HD de BIOFIT debe medir 1672x941 y ser legible.");
            Color32[] pixels = original.GetPixels32();
            // The selectable cards are isolated before removing their baked
            // copies; their original artwork and proportions remain intact.
            for (int i = 0; i < 5; i++)
                cards[i] = Own(Extract(pixels, CardLeft[i], CardTop,
                    CardWidth, CardBottom - CardTop, true, false,
                    "BIOFIT tarjeta " + i));

            // Remove only baked content, preserving the header, background,
            // corner decorations and original arrow graphics. Each repaired
            // strip is interpolated between source rows at the same X.
            Color32[] clean = (Color32[])pixels.Clone();
            EraseStrip(clean, pixels, 132, 1528, 322, 675, 316, 686, 5, 16);
            EraseStrip(clean, pixels, 424, 1245, 695, 771, 690, 775, 10, 7);
            EraseStrip(clean, pixels, 633, 1039, 772, 866, 767, 876, 10, 7);
            background = Own(MakeTexture(clean, ReferenceWidth, ReferenceHeight,
                "BIOFIT selector sin tarjetas ni indicadores duplicados"));

            // Transparent logo from the user's OWN artwork; not a rectangle
            // of baked blue background. Key blue pixels in this crop only.
            logo = Own(Extract(pixels, 332, 21, 1008, 172,
                false, true, "BIOFIT logo sin fondo azul"));
            corners[0] = Own(Extract(pixels, 0, 0, 278, 276,
                false, true, "BIOFIT esquina superior izquierda"));
            corners[1] = Own(Extract(pixels, 1394, 0, 278, 276,
                false, true, "BIOFIT esquina superior derecha"));
            corners[2] = Own(Extract(pixels, 0, 665, 278, 276,
                false, true, "BIOFIT esquina inferior izquierda"));
            corners[3] = Own(Extract(pixels, 1394, 665, 278, 276,
                false, true, "BIOFIT esquina inferior derecha"));
        }

        private Texture2D Own(Texture2D texture)
        {
            owned[ownedCount++] = texture;
            return texture;
        }

        private static int PixelIndex(int x, int topDownY)
        {
            return (ReferenceHeight - 1 - topDownY) * ReferenceWidth + x;
        }

        private static bool IsBlueBackground(Color32 c)
        {
            // A blue gradient can be dark or saturated. Gate by both
            // hue and saturation; never erase black outlines or green cards.
            return c.b >= 65 && c.b > c.r + 35 && c.b > c.g + 17 &&
                c.b > c.r * 1.22f && c.b > c.g * 1.08f;
        }

        private static Texture2D Extract(Color32[] source, int x, int yTop,
            int width, int height, bool floodTransparent, bool keyBlue,
            string title)
        {
            var data = new Color32[width * height];
            for (int py = 0; py < height; py++)
                for (int px = 0; px < width; px++)
                {
                    Color32 c = source[PixelIndex(x + px, yTop + py)];
                    if (keyBlue && IsBlueBackground(c)) c.a = 0;
                    data[(height - 1 - py) * width + px] = c;
                }
            if (floodTransparent)
                RemoveConnectedBlue(data, width, height);
            return MakeTexture(data, width, height, title);
        }

        private static void RemoveConnectedBlue(Color32[] data, int width, int height)
        {
            // Only remove blue CONNECTED to the outer crop perimeter. A blue
            // Wave/Vibe icon surrounded by the black card outline stays intact.
            var visited = new bool[data.Length];
            var queue = new int[data.Length];
            int head = 0, tail = 0;
            for (int x = 0; x < width; x++)
            {
                Add(x, visited, queue, data, ref tail);
                Add((height - 1) * width + x, visited, queue, data, ref tail);
            }
            for (int y = 0; y < height; y++)
            {
                Add(y * width, visited, queue, data, ref tail);
                Add(y * width + width - 1, visited, queue, data, ref tail);
            }
            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width, y = index / width;
                Color32 c = data[index];
                c.a = 0; data[index] = c;
                if (x > 0) Add(index - 1, visited, queue, data, ref tail);
                if (x + 1 < width) Add(index + 1, visited, queue, data, ref tail);
                if (y > 0) Add(index - width, visited, queue, data, ref tail);
                if (y + 1 < height) Add(index + width, visited, queue, data, ref tail);
            }
        }

        private static void Add(int i, bool[] visited, int[] queue,
            Color32[] data, ref int tail)
        {
            if (visited[i] || !IsBlueBackground(data[i])) return;
            visited[i] = true;
            queue[tail++] = i;
        }

        private static void EraseStrip(Color32[] destination, Color32[] original,
            int x0, int x1, int y0, int y1, int fromY, int toY,
            int featherX, int featherY)
        {
            for (int x = x0; x < x1; x++)
            {
                Color32 a = original[PixelIndex(x, fromY)];
                Color32 b = original[PixelIndex(x, toY)];
                float tx = Mathf.Clamp01(Mathf.Min(x - x0, x1 - 1 - x) /
                    (float)Mathf.Max(featherX, 1));
                for (int y = y0; y < y1; y++)
                {
                    float t = Mathf.Clamp01((y - fromY) / (float)(toY - fromY));
                    Color32 inferredBackground = Color32.Lerp(a, b, t);
                    inferredBackground.a = 255;
                    float ty = Mathf.Clamp01(
                        Mathf.Min(y - y0, y1 - 1 - y) /
                        (float)Mathf.Max(featherY, 1));
                    int at = PixelIndex(x, y);
                    destination[at] = Color32.Lerp(original[at],
                        inferredBackground, tx * ty);
                }
            }
        }

        private static Texture2D MakeTexture(Color32[] data, int width, int height,
            string title)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = title;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(data);
            tex.Apply(false, true);
            return tex;
        }

        public void Dispose()
        {
            for (int i = 0; i < ownedCount; i++)
                if (owned[i] != null) UnityEngine.Object.Destroy(owned[i]);
            ownedCount = 0;
        }
    }
}
