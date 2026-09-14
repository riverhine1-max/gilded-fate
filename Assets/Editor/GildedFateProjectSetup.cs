using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    [InitializeOnLoad]
    public static class GildedFateProjectSetup
    {
        static GildedFateProjectSetup() => EditorApplication.delayCall += Configure;

        [MenuItem("Gilded Fate/Apply Production Project Settings")]
        public static void Configure()
        {
            PlayerSettings.productName = "Gilded Fate";
            PlayerSettings.companyName = "Gilded Fate";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            const string scene = "Assets/Scenes/SampleScene.unity";
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != scene)) scenes.Insert(0, new EditorBuildSettingsScene(scene, true));
            else scenes = scenes.Select(s => new EditorBuildSettingsScene(s.path, s.path == scene || s.enabled)).ToList();
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Gilded Fate Setup] Production settings applied · 1920×1080 · borderless fullscreen · Linear color · SampleScene enabled");
        }
    }
}
