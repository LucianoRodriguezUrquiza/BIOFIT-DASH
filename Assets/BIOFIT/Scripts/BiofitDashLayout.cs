using System;

/// <summary>
/// Screen-independent geometry for landscape Android layouts.
/// Uses the full available horizontal width instead of centering a 1120px canvas.
/// Game viewport and diagnostic sidebar never overlap.
/// </summary>
public static class BiofitDashLayout
{
    public const float MinVirtualWidth = 1120f;
    public const float VirtualHeight = 740f;
    public const float WorldTop = 143f;
    public const float WorldHeight = 492f;

    public struct Layout
    {
        public float Scale;
        public float Width;
        public float Height;
        public float GameX;
        public float GameWidth;
        public float PanelX;
        public float PanelWidth;
        public bool PanelVisible;
    }

    public static Layout ForScreen(float screenWidth, float screenHeight,
        bool showPanel)
    {
        if (screenWidth <= 0f || screenHeight <= 0f)
            throw new ArgumentOutOfRangeException("Invalid screen dimensions");
        float scale = Math.Min(screenWidth / MinVirtualWidth,
            screenHeight / VirtualHeight);
        float width = screenWidth / scale;
        // Widen diagnostic sidebar ~20% at 1536x691 so buttons/labels stay readable.
        // Keep >=650 virtual pixels for gameplay even on the 1120px minimum canvas.
        float sidebarWidth = Math.Min(500f, Math.Max(410f, width * 0.30f));
        float sidebarX = width - sidebarWidth - 18f;
        float gameX = 20f;
        float gameWidth = showPanel ? sidebarX - gameX - 16f : width - 40f;
        return new Layout
        {
            Scale = scale,
            Width = width,
            Height = screenHeight / scale,
            GameX = gameX,
            GameWidth = gameWidth,
            PanelX = sidebarX,
            PanelWidth = sidebarWidth,
            PanelVisible = showPanel
        };
    }
}
