using System;
using System.IO;
using System.Linq;
using GildedFate.Chronicle;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    /// <summary>
    /// Gilded Fate > Chronicle > Developer Console. Editor only (this file lives under Assets/Editor, so it is never part of a build).
    /// Plays any Chronicle scene, skips, pauses and fast-forwards it, previews corrections, simulates story milestones and shows
    /// the unlock table. Everything runs on a SANDBOX progress saved in Library/, never on the player's real profile.
    /// </summary>
    public sealed class GildedChronicleDevConsole : EditorWindow
    {
        readonly ChronicleDevSession session = new ChronicleDevSession();
        string[] sceneIds, sceneLabels;
        int sceneIndex, hero, floorTarget = 24;
        Vector2 pageScroll, tableScroll;
        double lastTime;
        bool showTable = true, showSimulate = true;
        GUIStyle richStyle, smallStyle;

        static string SandboxPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "GildedChronicleSandbox.json");

        [MenuItem("Gilded Fate/Chronicle/Developer Console")]
        static void Open() { var w = GetWindow<GildedChronicleDevConsole>("Chronicle"); w.minSize = new Vector2(980, 700); w.Show(); }

        [MenuItem("Gilded Fate/Chronicle/Validate Content")]
        static void Validate()
        {
            var errors = ChronicleValidator.Validate();
            if (errors.Count == 0) Debug.Log("[Chronicle] Content valid: 27 memories, opening (full and short) and Chapter X all check out.");
            else Debug.LogError("[Chronicle] " + errors.Count + " content problems:\n" + string.Join("\n", errors));
        }

        void OnEnable()
        {
            var ids = new System.Collections.Generic.List<string> { ChronicleCatalog.OpeningSceneId, ChronicleCatalog.OpeningShortSceneId, ChronicleCatalog.SecretSceneId };
            ids.AddRange(ChronicleCatalog.Memories.Select(m => m.id));
            sceneIds = ids.ToArray();
            sceneLabels = sceneIds.Select(Label).ToArray();
            ChronicleLog.Warn = w => Debug.LogWarning("[Chronicle] " + w);
            LoadSandbox(); lastTime = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
        }
        void OnDisable() { EditorApplication.update -= Tick; SaveSandbox(); }

        static string Label(string id)
        {
            if (id == ChronicleCatalog.OpeningSceneId) return "Opening cinematic (full)";
            if (id == ChronicleCatalog.OpeningShortSceneId) return "Opening cinematic (short)";
            if (id == ChronicleCatalog.SecretSceneId) return "Chapter X - The One Who Remembers";
            var m = ChronicleCatalog.FindMemory(id);
            return id + "  Ch" + m.chapter + "  " + ChronicleCatalog.HeroName(m.hero) + "  " + m.title;
        }

        void LoadSandbox()
        {
            try { if (File.Exists(SandboxPath)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(SandboxPath), session.sandbox); session.sandbox.Ensure(); } }
            catch (Exception e) { Debug.LogWarning("[Chronicle] Could not read the sandbox file: " + e.Message); }
        }
        void SaveSandbox() { try { File.WriteAllText(SandboxPath, JsonUtility.ToJson(session.sandbox, true)); } catch (Exception e) { Debug.LogWarning("[Chronicle] Could not save the sandbox: " + e.Message); } }

        void Tick()
        {
            var now = EditorApplication.timeSinceStartup; var dt = (float)(now - lastTime); lastTime = now;
            if (session.IsPlaying) { var was = session.current; session.Tick(Mathf.Min(dt, .1f)); if (was.Finished) SaveSandbox(); Repaint(); }
        }

        void OnGUI()
        {
            richStyle = richStyle ?? new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true, fontSize = 13 };
            smallStyle = smallStyle ?? new GUIStyle(EditorStyles.miniLabel) { richText = true, wordWrap = true };

            EditorGUILayout.HelpBox("Sandbox only: nothing here touches the real player profile. The sandbox lives in Library/GildedChronicleSandbox.json.", MessageType.Info);
            GUILayout.Label(session.StatusLine(), EditorStyles.boldLabel);

            // ---- transport ----
            GUILayout.BeginHorizontal();
            sceneIndex = EditorGUILayout.Popup(sceneIndex, sceneLabels, GUILayout.Width(420));
            if (GUILayout.Button("Play")) session.Play(sceneIds[sceneIndex]);
            if (GUILayout.Button("Replay")) session.Play(sceneIds[sceneIndex], true);
            if (GUILayout.Button(session.current != null && session.current.Paused ? "Resume" : "Pause")) { if (session.current != null && session.current.Paused) session.Resume(); else session.Pause(); }
            if (GUILayout.Button("Skip")) { session.Skip(); SaveSandbox(); }
            if (GUILayout.Button("Stop")) session.Stop();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("First Correction test (preview, saves nothing)")) { var keep = session.saveToSandbox; session.saveToSandbox = false; session.Stop(); session.Play("MEM_07"); session.saveToSandbox = keep; }
            if (GUILayout.Button("Check all 30 scenes (headless)")) CheckAll();
            if (GUILayout.Button("Validate content")) Validate();
            GUILayout.EndHorizontal();

            // ---- options ----
            GUILayout.BeginHorizontal();
            session.settings.fastForward = EditorGUILayout.Slider("Speed", session.settings.fastForward, .25f, 8f, GUILayout.Width(300));
            session.settings.textSpeed = EditorGUILayout.Slider("Writing", session.settings.textSpeed, .5f, 2f, GUILayout.Width(260));
            var narrator = EditorGUILayout.Popup(session.NarratorOverride + 1, new[] { "Narrator: follow progress", "Narrator: Confident", "Narrator: Doubting", "Narrator: Remembering" }, GUILayout.Width(220)) - 1;
            session.NarratorOverride = narrator;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            session.settings.instantText = EditorGUILayout.ToggleLeft("Instant text", session.settings.instantText, GUILayout.Width(110));
            session.settings.reduceMotion = EditorGUILayout.ToggleLeft("Reduce motion", session.settings.reduceMotion, GUILayout.Width(120));
            session.settings.reduceFlashing = EditorGUILayout.ToggleLeft("Reduce flashing", session.settings.reduceFlashing, GUILayout.Width(130));
            session.saveToSandbox = EditorGUILayout.ToggleLeft("Save results to sandbox", session.saveToSandbox, GUILayout.Width(170));
            session.allowLockedScenes = EditorGUILayout.ToggleLeft("Play locked scenes", session.allowLockedScenes, GUILayout.Width(140));
            var fourth = EditorGUILayout.ToggleLeft("Pretend the fourth is known", session.IsFactForced("FACT_FOURTH_COMPANION"), GUILayout.Width(200));
            session.ForceFact("FACT_FOURTH_COMPANION", fourth);
            GUILayout.EndHorizontal();

            // ---- stage ----
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(440));
            var area = GUILayoutUtility.GetRect(430, 300);
            DrawIllustration(area, session.current?.State);
            GUILayout.Label("Book / audio / effects log", EditorStyles.boldLabel);
            var log = session.stage.log; var shown = string.Join("   ", log.Skip(Mathf.Max(0, log.Count - 10)));
            GUILayout.Label(shown, smallStyle);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("The page", EditorStyles.boldLabel);
            pageScroll = EditorGUILayout.BeginScrollView(pageScroll, GUILayout.Height(330));
            GUILayout.Label(session.current == null ? "(nothing playing)" : ChronicleDevSession.FormatPage(session.current.State), richStyle);
            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // ---- simulate ----
            showSimulate = EditorGUILayout.Foldout(showSimulate, "Simulate story milestones (sandbox)", true);
            if (showSimulate)
            {
                GUILayout.BeginHorizontal();
                hero = EditorGUILayout.Popup(hero, new[] { "Vanguard", "Hexer", "Reaper" }, GUILayout.Width(100));
                var h = (ChronicleHero)hero;
                if (GUILayout.Button("Finish a run")) { session.SimulateRunEnded(h, false, 1, 3); SaveSandbox(); }
                floorTarget = EditorGUILayout.IntField(floorTarget, GUILayout.Width(40));
                if (GUILayout.Button("Reach floor")) { session.SimulateFloor(h, floorTarget); SaveSandbox(); }
                for (var act = 1; act <= 3; act++) if (GUILayout.Button("Beat Act " + act + " boss")) { session.SimulateBoss(h, act); SaveSandbox(); }
                if (GUILayout.Button("Win a run")) { session.SimulateRunEnded(h, true, 3, 54); SaveSandbox(); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Unlock all 27")) { session.UnlockEverythingButChapterX(); session.RefreshUnlocks(); SaveSandbox(); }
                if (GUILayout.Button("Refresh unlocks")) { session.RefreshUnlocks(); SaveSandbox(); }
                if (GUILayout.Button("Reset sandbox")) { if (EditorUtility.DisplayDialog("Reset sandbox", "Clear the Chronicle sandbox? Your real profile is not affected.", "Reset", "Cancel")) { session.ResetSandbox(); SaveSandbox(); } }
                GUILayout.EndHorizontal();
            }

            // ---- unlock table + corrections ----
            showTable = EditorGUILayout.Foldout(showTable, "Unlock conditions, viewed state and corrections", true);
            if (showTable)
            {
                tableScroll = EditorGUILayout.BeginScrollView(tableScroll, GUILayout.Height(190));
                foreach (var line in session.UnlockTable()) GUILayout.Label(line, smallStyle);
                GUILayout.Space(6);
                foreach (var c in ChronicleCatalog.Corrections)
                    GUILayout.Label((session.sandbox.HasCorrection(c.id) ? "<color=#c01818>" + c.id + "  " + ChronicleDevSession.Struck(c.original) + "  ->  " + c.corrected + "</color>" : c.id + "  (not yet discovered)"), smallStyle);
                EditorGUILayout.EndScrollView();
            }
        }

        void CheckAll()
        {
            var bad = 0;
            foreach (var id in sceneIds) { if (!session.CheckScene(id, out var report)) { bad++; Debug.LogWarning("[Chronicle] " + id + ": " + report); } }
            if (bad == 0) Debug.Log("[Chronicle] All " + sceneIds.Length + " scenes run to the end with no warnings and no overflowing text.");
        }

        // A rough picture of the placeholder illustration: gradient backdrop, coloured rectangles for actors, simple cues for effects.
        static Color C(ChronicleColor c, float alpha = 1f) => new Color(c.r, c.g, c.b, c.a * alpha);
        void DrawIllustration(Rect area, ChronicleSceneState state)
        {
            EditorGUI.DrawRect(area, new Color(.08f, .08f, .1f));
            if (state == null || (!state.illustration.visible && state.illustration.alpha <= 0f)) { GUI.Label(area, "  no illustration", EditorStyles.centeredGreyMiniLabel); return; }
            var ill = state.illustration; var a = Mathf.Clamp01(ill.alpha);
            for (var band = 0; band < 8; band++)
                EditorGUI.DrawRect(new Rect(area.x, area.y + area.height * band / 8f, area.width, area.height / 8f + 1), C(ChronicleColor.Lerp(ill.top, ill.bottom, band / 7f), a));
            foreach (var actor in ill.actors)
            {
                var r = new Rect(area.x + (actor.x - actor.w * .5f) * area.width, area.y + (actor.y - actor.h * .5f) * area.height, Mathf.Max(3, actor.w * area.width), Mathf.Max(3, actor.h * area.height));
                var col = C(actor.color, actor.alpha * a);
                if (actor.shape == ChronicleShape.Line) r.height = 3;
                EditorGUI.DrawRect(r, col);
                if (actor.shape == ChronicleShape.Figure) EditorGUI.DrawRect(new Rect(r.center.x - r.width * .3f, r.y - r.width * .6f, r.width * .6f, r.width * .6f), col);
                if (!string.IsNullOrEmpty(actor.label) && actor.alpha > .3f) GUI.Label(new Rect(r.x - 20, r.yMax + 1, r.width + 40, 16), actor.label, EditorStyles.centeredGreyMiniLabel);
                if (actor.speakTimer > 0f) GUI.Label(new Rect(r.x, r.y - r.width - 16, 24, 16), "...", EditorStyles.boldLabel);
            }
            foreach (var anim in ill.animations)
            {
                var p = anim.Progress;
                if (anim.name == "flash") EditorGUI.DrawRect(area, new Color(1, 1, 1, (1f - p) * .6f * anim.intensity));
                else if (anim.name == "expand_circle" || anim.name == "echo_ring" || anim.name == "pulse") { var s = area.width * .5f * p; EditorGUI.DrawRect(new Rect(area.center.x - s * .5f, area.center.y - s * .5f, s, s), new Color(1, 1, 1, (1f - p) * .25f)); }
                else if (anim.name == "threads" || anim.name == "branch") for (var i = 0; i < 6; i++) EditorGUI.DrawRect(new Rect(area.x, area.y + area.height * (i + 1) / 7f, area.width * p, 2), new Color(1f, .8f, .4f, .5f));
                else if (anim.name == "fracture" || anim.name == "shatter_world" || anim.name == "crack_pass") for (var i = 0; i < 5; i++) EditorGUI.DrawRect(new Rect(area.x + area.width * (i + .5f) / 5f, area.y, 2, area.height * p), new Color(1, 1, 1, .45f));
            }
            if (state.pageGlow > 0f) EditorGUI.DrawRect(area, new Color(.2f, .9f, .6f, state.pageGlow * .15f));
            if (state.pageDarkness > 0f) EditorGUI.DrawRect(area, new Color(0, 0, 0, state.pageDarkness * .5f));
        }
    }
}
