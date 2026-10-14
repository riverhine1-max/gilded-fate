#if UNITY_EDITOR || GILDED_CHRONICLE_QA
using System;
using System.IO;
using System.Text;
using GildedFate.Chronicle;
using GildedFate.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    /// <summary>
    /// CHRONICLE QA performance readout. Press F11 to show or hide a frame-time and garbage readout on any screen; press F12 to drop a numbered
    /// marker at the moment something stutters. It samples every frame whether or not the readout is shown, writes one CSV row per second plus
    /// a summary when the game closes, both in the QA save folder (...\ChronicleQA\perf), so a tester can send them back.
    /// It lives on its own object, not inside the main menu, so it keeps measuring during Chronicle scenes, combat and every other state.
    /// Compiled only in the Editor and the QA build, and only runs while QA isolation is active (a normal player build contains none of it).
    /// The readout draws only on Repaint and reuses one text buffer, so showing it adds almost nothing to the numbers it reports.
    /// </summary>
    public sealed class ChronicleQaPerfMonitor : MonoBehaviour
    {
        readonly ChronicleQaPerfStats stats = new ChronicleQaPerfStats();
        bool show; float textRefreshAt; string text = ""; float markFlashUntil; int lastMark;
        StreamWriter csv; string summaryPath, machine = "";
        GUIStyle style; Texture2D pixel; float flushAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!ChronicleQaMode.Active || FindFirstObjectByType<ChronicleQaPerfMonitor>() != null) return;
            var host = new GameObject("Chronicle QA Perf"); DontDestroyOnLoad(host); host.AddComponent<ChronicleQaPerfMonitor>();
        }

        void Awake()
        {
            machine = "Machine: " + SystemInfo.processorType + " | " + SystemInfo.graphicsDeviceName + " | " + SystemInfo.systemMemorySize + " MB RAM | " + Screen.width + "x" + Screen.height +
                      " | vSync " + QualitySettings.vSyncCount + " | target " + Application.targetFrameRate + " fps | Unity " + Application.unityVersion;
            try
            {
                var dir = Path.Combine(SaveService.DirectoryPath, "perf"); Directory.CreateDirectory(dir);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                csv = new StreamWriter(Path.Combine(dir, "perf-" + stamp + ".csv"), false, new UTF8Encoding(false));
                csv.WriteLine("# " + machine); csv.WriteLine(ChronicleQaPerfStats.CsvHeader); csv.Flush();
                summaryPath = Path.Combine(dir, "perf-" + stamp + "-summary.txt");
            }
            catch (Exception e) { Debug.LogWarning("[Chronicle QA] Performance log unavailable: " + e.Message); csv = null; }
        }

        void Update()
        {
            stats.Record(Time.unscaledDeltaTime, GC.GetTotalMemory(false), GC.CollectionCount(0));
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.f11Key.wasPressedThisFrame) show = !show;
                if (k.f12Key.wasPressedThisFrame) { lastMark = stats.Mark(); markFlashUntil = Time.unscaledTime + 1.5f; }
            }
            if (csv != null)
            {
                var rows = stats.TakeCsvRows();
                if (rows.Count > 0) foreach (var r in rows) csv.WriteLine(r);
                if (Time.unscaledTime >= flushAt) { csv.Flush(); flushAt = Time.unscaledTime + 5f; }
            }
            if (show && Time.unscaledTime >= textRefreshAt) { text = stats.OverlayText(); textRefreshAt = Time.unscaledTime + .25f; }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var flashing = Time.unscaledTime < markFlashUntil;
            if (!show && !flashing) return;
            if (style == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; pixel.SetPixel(0, 0, Color.white); pixel.Apply();
                style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = false, richText = false, padding = new RectOffset(8, 8, 6, 6) };
                style.normal.textColor = new Color(.85f, 1f, .85f);
            }
            GUI.depth = -10000;   // on top of everything the game draws
            var r = new Rect(Screen.width - 460, 8, 452, show ? 78 : 26);
            var old = GUI.color; GUI.color = new Color(0, 0, 0, .72f); GUI.DrawTexture(r, pixel); GUI.color = old;
            GUI.Label(r, show ? text + (flashing ? "\n<MARK " + lastMark + " saved>" : "") : "MARK " + lastMark + " saved", style);
        }

        void OnDestroy() { Finish(); }
        void OnApplicationQuit() { Finish(); }

        bool finished;
        void Finish()
        {
            if (finished) return; finished = true;
            try
            {
                if (csv != null) { foreach (var r in stats.TakeCsvRows()) csv.WriteLine(r); csv.Flush(); csv.Dispose(); csv = null; }
                if (!string.IsNullOrEmpty(summaryPath) && stats.TotalFrames > 60) File.WriteAllText(summaryPath, stats.Summary(machine));
            }
            catch (Exception e) { Debug.LogWarning("[Chronicle QA] Could not write the performance summary: " + e.Message); }
        }
    }
}
#endif
