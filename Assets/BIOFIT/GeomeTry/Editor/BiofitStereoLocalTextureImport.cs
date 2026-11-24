#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace Biofit.GeomeTryOriginal.Editor
{
    public sealed class BiofitStereoLocalTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/BIOFIT/Resources/BiofitStereoLocal/Sprites/",System.StringComparison.Ordinal) &&
                !assetPath.StartsWith("Assets/BIOFIT/Resources/BiofitStereoV2/",System.StringComparison.Ordinal)) return;
            var t=(TextureImporter)assetImporter;
            t.textureType=TextureImporterType.Default;
            t.alphaIsTransparency=true;
            t.mipmapEnabled=false;
            t.npotScale=TextureImporterNPOTScale.None;
            t.textureCompression=TextureImporterCompression.Uncompressed;
            t.wrapMode=TextureWrapMode.Clamp;
            t.filterMode=FilterMode.Bilinear;
            t.maxTextureSize=2048;
        }
    }
}
#endif
