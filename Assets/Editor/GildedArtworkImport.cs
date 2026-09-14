using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    // Preserve generated cutout alpha and all painted cell detail on import.
    public sealed class GildedArtworkImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/Art/Cards/")&&
               !assetPath.StartsWith("Assets/Resources/Art/Characters/")&&
               !assetPath.StartsWith("Assets/Resources/Art/Enemies/")&&
               !assetPath.StartsWith("Assets/Resources/Art/Powers/")&&
               !assetPath.StartsWith("Assets/Resources/Art/UI/MasterPolish/")&&
               !assetPath.StartsWith("Assets/Resources/Art/Relics/Expansion/"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=!assetPath.Contains("/Cards/");
            importer.mipmapEnabled=false;
            if(assetPath=="Assets/Resources/Art/Powers/MartialOccult.png")importer.isReadable=true;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;
            importer.maxTextureSize=8192;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
        }
    }
}
