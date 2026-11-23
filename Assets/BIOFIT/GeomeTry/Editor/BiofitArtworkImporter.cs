#if UNITY_EDITOR
using UnityEditor;

namespace Biofit.GeomeTryOriginal.Editor
{
    /// <summary>
    /// The catalog remains outside Git. Only explicitly selected individual PNG
    /// files under Artwork are imported. This does not slice sprite atlases.
    /// No modifications to the pinned original GeomeTry files.
    /// </summary>
    public sealed class BiofitArtworkImporter : AssetPostprocessor
    {
        private const string Prefix =
            "Assets/BIOFIT/Resources/BiofitThreeModes/Artwork/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Prefix, System.StringComparison.Ordinal))
                return;
            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = assetPath.Contains("/Portal") ? 36f :
                assetPath.Contains("/Deco") ? 64f : 96f;
        }
    }
}
#endif
