using System;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    public sealed class ChronicleFinishedBookImporter : AssetPostprocessor
    {
        public const string ModelPath = "Assets/Resources/Chronicle/BookModel/Chronicle.fbx";
        void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        }

        void OnPreprocessAnimation()
        {
            if (assetPath != ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                var start = clip.name.LastIndexOf("CHR_", StringComparison.Ordinal);
                if (start >= 0) clip.name = clip.name.Substring(start);
                clip.loopTime = clip.name == "CHR_Idle_Open";
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Chronicle/Textures/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.sRGBTexture = assetPath.EndsWith("_BaseColor.png", StringComparison.Ordinal);
            importer.textureType = assetPath.EndsWith("_Normal.png", StringComparison.Ordinal)
                ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.anisoLevel = 8;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = (assetPath.Contains("Illuminated") || assetPath.Contains("Weathered")) ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
