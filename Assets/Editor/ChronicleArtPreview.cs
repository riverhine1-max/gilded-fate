using System;
using System.IO;
using System.Reflection;
using GildedFate.Chronicle;
using GildedFate.ChronicleBook;
using GildedFate.UI;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    public sealed class ChronicleArtPreview : EditorWindow
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        ChronicleBookStageHost stage;
        GameObject host;
        GildedMainMenu menu;
        ChronicleSceneState state;
        string error;
        [InitializeOnLoadMethod] static void Watch() => EditorApplication.update += () =>
        {
            const string request = "Library/ChronicleArtPreview.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
            File.Delete(request);
            var window = GetWindow<ChronicleArtPreview>(true, "Chronicle art preview", true);
            window.position = new Rect(100, 100, 1440, 810); window.ShowUtility();
        };
        void Initialize()
        {
            foreach(var old in Resources.FindObjectsOfTypeAll<GameObject>())
                if(old.scene.IsValid() && ((old.transform.position == new Vector3(3000,4000,0) && old.name.Contains("Chronicle book stage")) || old.name == "Chronicle art preview UI")) UnityEngine.Object.DestroyImmediate(old);
            stage = ChronicleBookStageHost.Create(2400, 1350);
            stage.Camera.transform.parent.position = new Vector3(3000, 4000, 0);
            stage.SetActive(true); stage.Rig.SetPoseImmediate(ChronicleBookPose.Open, 0); stage.Tick(0, new ChronicleCameraState());
            host = new GameObject("Chronicle art preview UI") { hideFlags = HideFlags.HideAndDontSave };
            menu = host.AddComponent<GildedMainMenu>();
            typeof(GildedMainMenu).GetField("white", Private).SetValue(menu, Texture2D.whiteTexture);
            typeof(GildedMainMenu).GetField("chronicleHost", Private).SetValue(menu, stage);
            var palette = ChronicleBackdrops.Get("kingdom");
            state = new ChronicleSceneState { pose = ChronicleBookPose.Open, layout = ChronicleSpreadLayout.IllustrationLeftTextRight };
            state.illustration.visible = true; state.illustration.alpha = 1; state.illustration.backdrop = "kingdom";
            state.illustration.top = palette.top; state.illustration.bottom = palette.bottom;
            state.illustration.actors.Add(new ChronicleActor { id = "castle", shape = ChronicleShape.Castle, x = .67f, y = .43f, w = .52f, h = .66f, alpha = 1 });
            state.illustration.actors.Add(new ChronicleActor { id = "v", shape = ChronicleShape.Figure, x = .25f, y = .76f, w = .075f, h = .23f, alpha = 1, color = ChronicleColor.Vanguard });
            state.illustration.actors.Add(new ChronicleActor { id = "folk", shape = ChronicleShape.Figure, x = .37f, y = .76f, w = .055f, h = .18f, alpha = 1, color = new ChronicleColor(.32f,.3f,.38f) });
            foreach (var text in new[] { "Before the world was broken, there was a time when tomorrow belonged to no one.", "A kingdom stood beneath an unbroken sky. Its people built monuments to the past, and dreamed of futures they could not yet see.", "Then came a traveler.", "He spoke of worlds beyond our own. Of roads not yet traveled. Of lives that might have been." })
                state.blocks.Add(new ChronicleTextBlock { text = text, side = ChroniclePageSide.Right, complete = true });
        }
        void OnGUI()
        {
            if (!string.IsNullOrEmpty(error)) { EditorGUILayout.HelpBox(error, MessageType.Error); return; }
            try
            {
                if (stage == null || !menu) Initialize();
                var w = position.width; var h = position.height;
                typeof(GildedMainMenu).GetMethod("DrawChronicleAtmosphere", Private).Invoke(menu, new object[] { w, h });
                var dest = new Rect(0, 0, w, h);
                GUI.DrawTexture(dest, stage.Texture, ScaleMode.StretchToFill, true);
                typeof(GildedMainMenu).GetMethod("DrawChroniclePages", Private).Invoke(menu, new object[] { dest, state });
            }
            catch (Exception exception) { error = exception.ToString(); Debug.LogException(exception); }
        }
        void OnDisable() => Cleanup();
        void OnDestroy() => Cleanup();
        void Cleanup()
        {
            // The menu owns the stage once assigned; its normal cleanup also frees the ink and watercolor textures.
            if (host) UnityEngine.Object.DestroyImmediate(host); else stage?.Dispose();
            stage = null; host = null; menu = null;
        }
    }
}
