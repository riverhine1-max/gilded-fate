using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GildedFate.Chronicle;
using GildedFate.ChronicleBook;
using GildedFate.Saving;
using GildedFate.UI;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    [InitializeOnLoad]
    public static class ChronicleFixedWritingVerification
    {
        const string Request = "Library/ChronicleFixedWriting.request";
        const string Output = @"C:\Users\river\Documents\Codex\2026-10-08\use-these-files-from-the-libray-4\outputs";
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        [Serializable] sealed class Report {
            public int checks, scenes, passages, readings, unlockedMemories;
            public float writingSize, pageHeight, tallestPassage;
            public bool chapterXUnlocked;
            public List<string> failures = new();
        }
        static ChronicleFixedWritingVerification() => EditorApplication.update += Wait;
        static void Wait()
        {
            if (!File.Exists(Request) || EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.GetLastWriteTimeUtc("Assets/Scripts/Chronicle/ChronicleSequencer.cs") > File.GetLastWriteTimeUtc(typeof(ChronicleSequencer).Assembly.Location)
                || File.GetLastWriteTimeUtc("Assets/Editor/ChronicleFixedWritingVerification.cs") > File.GetLastWriteTimeUtc(typeof(ChronicleFixedWritingVerification).Assembly.Location)) return;
            File.Delete(Request); Verify();
        }
        [MenuItem("Gilded Fate/Chronicle/Verify Fixed Writing and Admin Unlocks")]
        static void RequestVerification() => File.WriteAllText(Request, "Verify without changing the player's profile.");

        public static void Verify()
        {
            var report = new Report(); ChronicleBookStageHost stage = null;
            var host = new GameObject("Chronicle fixed writing verification") { hideFlags = HideFlags.HideAndDontSave }; host.SetActive(false);
            var menu = host.AddComponent<GildedMainMenu>();
            object Call(string name, params object[] args) => typeof(GildedMainMenu).GetMethod(name, Private).Invoke(menu, args);
            void Set(string name, object value) => typeof(GildedMainMenu).GetField(name, Private).SetValue(menu, value);
            void Check(bool pass, string message) { report.checks++; if (!pass) report.failures.Add(message); }
            GUIStyle Style(int size) => (GUIStyle)Call("ChronicleStyle", size, false);
            var profileSuspended = ProfileService.Suspended;
            var random = UnityEngine.Random.state;
            ProfileService.Suspended = true;
            try
            {
                stage = ChronicleBookStageHost.Create(1600, 900);
                Check(stage != null && stage.UsingFinishedModel, "Finished book model loads");
                stage.Camera.transform.parent.position = new Vector3(2100, 4000, 0);
                stage.SetActive(true); stage.Rig.SetPoseImmediate(ChronicleBookPose.Open, 0); stage.Tick(0f, null);
                Set("chronicleHost", stage);
                var metrics = (ChroniclePageMetrics)Call("ChronicleWritingMetrics");
                report.writingSize = metrics.fontSize; report.pageHeight = metrics.textHeight;
                Check(metrics.fontSize == 34f && metrics.uniformTextSize && metrics.autoPageTurns, "Fixed hand and pagination are enabled");
                Check(Style(34).font == Resources.Load<Font>("Chronicle/Fonts/ChronicleHandwriting"), "The installed handwriting font is used");
                var profile = new PlayerProfile(); profile.EnsureMeta(); profile.chronicle.openingMode = 2; profile.chronicle.textSpeed = 1.5f;
                Set("profile", profile); Call("PgUnlockChronicles");
                var restored = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile)); restored.EnsureMeta();
                report.unlockedMemories = restored.chronicle.TotalUnlocked; report.chapterXUnlocked = restored.chronicle.secretUnlocked;
                Check(report.unlockedMemories == 27 && report.chapterXUnlocked, "Admin opens all 27 memories and Chapter X after serialization");
                Check(restored.chronicle.TotalViewed == 0 && !restored.chronicle.secretCompleted && restored.chronicle.runsEnded.Sum() == 0 && restored.chronicle.wins.Sum() == 0, "Admin leaves viewed stories and gameplay records intact");
                Check(restored.chronicle.openingMode == 2 && restored.chronicle.textSpeed == 1.5f, "Admin preserves Chronicle preferences");
                var areas = new List<Rect>();
                foreach (var zoom in new[] { .85f, 1f, 1.15f })
                {
                    stage.Tick(0f, new ChronicleCameraState { zoom = zoom });
                    foreach (ChroniclePageSide side in Enum.GetValues(typeof(ChroniclePageSide)))
                    {
                        Check(stage.TryGetPageViewportRect(side, out var view), "Visible paper at zoom " + zoom);
                        areas.Add(new Rect(0, 0, view.width * 1440f, view.height * 810f));
                    }
                }
                void CheckPage(IList<ChronicleTextBlock> blocks, string label)
                {
                    foreach (var area in areas)
                    foreach (ChroniclePageSide side in Enum.GetValues(typeof(ChroniclePageSide)))
                    {
                        var unit = area.width / 470f; var size = Mathf.Max(9, Mathf.RoundToInt(34f * unit)); var used = 0f;
                        foreach (var block in blocks.Where(b => b.side == side && !(b.erased && b.eraseProgress >= 1f)))
                        {
                            used += Style(size).CalcHeight(new GUIContent(block.text), area.width) + 14f * unit;
                            if (!string.IsNullOrEmpty(block.speaker)) used += size * 1.05f;
                        }
                        Check(used <= area.height + .01f, label + ": fixed-size ink fits paper, used " + used + " / " + area.height);
                    }
                }
                foreach (var known in new[] { false, true })
                foreach (var replay in new[] { false, true })
                foreach (ChronicleNarratorState narrator in Enum.GetValues(typeof(ChronicleNarratorState)))
                foreach (var id in ChronicleScripts.Ids)
                {
                    var context = new ChronicleStaticContext { narrator = narrator };
                    if (known) { foreach (var f in ChronicleCatalog.Facts) context.facts.Add(f.id); foreach (var c in ChronicleCatalog.Corrections) context.corrections.Add(c.id); }
                    var seq = new ChronicleSequencer(ChronicleScripts.Get(id), null, context, null, new ChronicleSettings { textSpeed = 4f }, metrics, replay);
                    seq.Start(); var seen = new HashSet<ChronicleTextBlock>(); var struck = new HashSet<ChronicleTextBlock>();
                    for (var i = 0; !seq.Finished && i < 20000; i++)
                    {
                        seq.Update(.1f); var added = false;
                        foreach (var b in seq.State.transcript)
                        {
                            if (seen.Add(b)) { added = true; report.passages++; report.tallestPassage = Mathf.Max(report.tallestPassage, ChronicleLayout.HeightOf(b.text, metrics, b.style)); }
                            if (b.struck && struck.Add(b)) Check(seq.State.blocks.Contains(b), id + ": correction strike stays on the visible page: " + b.id);
                        }
                        if (added) CheckPage(seq.State.blocks, id);
                    }
                    Check(seq.Finished, id + ": playback completes"); Check(seq.State.overflowCount == 0, id + ": no overflowing passages"); report.scenes++;
                }
                foreach (var memory in ChronicleCatalog.Memories)
                {
                    var reading = ChronicleArchive.Reading(memory.id, restored.chronicle, metrics);
                    Check(reading != null, memory.id + ": admin-unlocked story is readable");
                    foreach (var spread in reading.spreads) CheckPage(spread.left.Concat(spread.right).ToList(), memory.id + " Archive");
                    report.readings++;
                }
            }
            catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
            finally
            {
                Set("chronicleHost", null); stage?.Dispose(); UnityEngine.Object.DestroyImmediate(host);
                ProfileService.Suspended = profileSuspended; UnityEngine.Random.state = random;
            }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "Chronicle_Fixed_Writing_Verification.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[Chronicle] Fixed writing: " + report.checks + " checks, " + report.failures.Count + " failures.");
        }
    }
}
