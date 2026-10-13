using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GildedFate.Chronicle;
using GildedFate.Core;
using GildedFate.Combat;
using GildedFate.ChronicleBook;
using GildedFate.Map;
using GildedFate.Saving;
using GildedFate.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GildedFate.Editor
{
    [InitializeOnLoad]
    public static class ChronicleMilestoneVerification
    {
        const string Request = "Library/ChronicleMilestone.request";
        const string Output = @"C:\Users\river\Documents\Codex\2026-10-08\use-these-files-from-the-libray-4\outputs\Chronicle_Milestone_Verification.json";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] sealed class Index { public ChronicleDialogueTiming[] lines; }
        [Serializable] sealed class Report { public int checks, flows, audioBindings; public List<string> failures = new(); }
        const string Pending = "GildedFate.ChronicleMilestoneVerification.Pending";
        static double readyAt;
        const string SavedProfile = Pending + ".Profile", SavedRun = Pending + ".Run";
        static ChronicleMilestoneVerification()
        {
            // Domain reload happens before scene Start: verification must never write the real profile.
            if (SessionState.GetBool(Pending, false)) ProfileService.Suspended = SaveService.Suspended = true;
            EditorApplication.update += Wait;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                ProfileService.Suspended = SessionState.GetBool(SavedProfile, false);
                SaveService.Suspended = SessionState.GetBool(SavedRun, false);
                readyAt = 0;
                SessionState.SetBool(Pending, false);
            };
        }
        static void Wait()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(Pending, false))
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(Pending, false);
                    ProfileService.Suspended = SessionState.GetBool(SavedProfile, false);
                    SaveService.Suspended = SessionState.GetBool(SavedRun, false);
                    readyAt = 0;
                    return;
                }
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 5; return; }
                if (EditorApplication.timeSinceStartup < readyAt) return;
                SessionState.SetBool(Pending, false);
                try { Verify(); }
                finally { EditorApplication.ExitPlaymode(); }
                return;
            }
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (new[] { "Assets/Scripts/UI/GildedChronicleMilestones.cs", "Assets/Scripts/UI/GildedChroniclePresentation.cs", "Assets/Scripts/UI/GildedMainMenu.cs" }.Any(path => File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(typeof(GildedMainMenu).Assembly.Location))
                || File.GetLastWriteTimeUtc("Assets/Editor/ChronicleMilestoneVerification.cs") > File.GetLastWriteTimeUtc(typeof(ChronicleMilestoneVerification).Assembly.Location)) return;
            GildedModalVerificationRunner.BackupPlayerSaves();
            File.Delete(Request);
            SessionState.SetBool(SavedProfile, ProfileService.Suspended);
            SessionState.SetBool(SavedRun, SaveService.Suspended);
            SessionState.SetBool(Pending, true);
            ProfileService.Suspended = SaveService.Suspended = true;
            EditorApplication.EnterPlaymode();
        }
        [MenuItem("Gilded Fate/Chronicle/Verify Act and Run Ending Playback")]
        static void RequestVerification() => File.WriteAllText(Request, "Verify on an isolated profile; do not alter player saves.");

        [MenuItem("Gilded Fate/Chronicle/Preview Victory Transition (Play Mode)")]
        static void PreviewVictory() => PreviewEnding(true);
        [MenuItem("Gilded Fate/Chronicle/Preview Defeat Transition (Play Mode)")]
        static void PreviewDefeat() => PreviewEnding(false);
        static void PreviewEnding(bool victory)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Chronicle] Start Play Mode to preview a milestone."); return; }
            var menu = UnityEngine.Object.FindAnyObjectByType<GildedMainMenu>();
            if (!menu || menu.ChronicleIsPlaying || (bool)typeof(GildedMainMenu).GetField("chronicleMilestoneActive", Private).GetValue(menu))
            { Debug.LogWarning("[Chronicle] Finish or skip the current scene before previewing an ending."); return; }
            var field = typeof(GildedMainMenu).GetField("profile", Private);
            var original = (PlayerProfile)field.GetValue(menu);
            var preview = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(original));
            preview.EnsureMeta();
            foreach (var memory in ChronicleCatalog.Memories) if (!preview.chronicle.viewedMemories.Contains(memory.id)) preview.chronicle.viewedMemories.Add(memory.id);
            field.SetValue(menu, preview);
            typeof(GildedMainMenu).GetMethod("BeginChronicleMilestone", Private).Invoke(menu,
                new object[] { true, victory, (Action)(() => field.SetValue(menu, original)) });
            Debug.Log("[Chronicle] Run-ending preview: existing recordings, isolated story state, no run results or unlocks granted.");
        }

        static void Verify()
        {
            var report = new Report();
            void Check(bool pass, string message) { report.checks++; if (!pass) report.failures.Add(message); }
            var savedProfile = SessionState.GetBool(SavedProfile, false); var savedRun = SessionState.GetBool(SavedRun, false);
            var savedRandom = UnityEngine.Random.state; var savedMaster = AudioListener.volume;
            var originalScene = SceneManager.GetActiveScene();
            var fixtureScene = SceneManager.CreateScene("Chronicle milestone verification (unsaved)");
            SceneManager.SetActiveScene(fixtureScene);
            ProfileService.Suspended = SaveService.Suspended = true;
            try
            {
                var index = JsonUtility.FromJson<Index>(Resources.Load<TextAsset>("Chronicle/Audio/DialogueIndex").text);
                Check(index.lines.Length == 254, "244 original plus 10 reuse bindings");
                foreach (ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero)))
                foreach (var victory in new[] { true, false })
                {
                    var scene = ChronicleMilestoneScenes.Ending(victory, hero);
                    foreach (var c in scene.commands.Where(c => c.op == ChronicleOp.ShowNarration || c.style == ChronicleTextStyle.DeeperVoice))
                    {
                        var speaker = c.style == ChronicleTextStyle.DeeperVoice ? "BEING" : "NARRATOR";
                        var line = index.lines.SingleOrDefault(l => l.sceneId == scene.id && l.commandId == c.id && l.speaker == speaker && l.text == c.text);
                        Check(line != null, scene.id + ": exact voice binding " + c.id);
                        if (line == null) continue;
                        Check(line.Validate(c.text, out var reason), scene.id + ": measured word validation " + reason);
                        var clip = Resources.Load<AudioClip>(line.audio);
                        Check(clip && Math.Abs(clip.length - line.durationSeconds) <= .025, scene.id + ": original clip duration " + c.id);
                        report.audioBindings++;
                    }
                }
                foreach (HeroId hero in Enum.GetValues(typeof(HeroId)))
                foreach (var kind in new[] { 1, 2, 3, 4 }) // Act I, Act II, victory, defeat
                {
                    var host = new GameObject("Chronicle milestone isolated verification") { hideFlags = HideFlags.HideAndDontSave };
                    var menu = host.AddComponent<GildedMainMenu>(); menu.enabled = false;
                    object Get(string name) => typeof(GildedMainMenu).GetField(name, Private).GetValue(menu);
                    void Set(string name, object value) => typeof(GildedMainMenu).GetField(name, Private).SetValue(menu, value);
                    object Call(string name, params object[] args) => typeof(GildedMainMenu).GetMethod(name, Private).Invoke(menu, args);
                    try
                    {
                        var p = new PlayerProfile(); p.EnsureMeta(); Set("profile", p);
                        var run = (RunModel)Get("run"); run.NewRun(hero, 127);
                        run.act = kind <= 2 ? kind : 3;
                        var originalAct = run.act; var ended = kind >= 3; var won = kind == 3;
                        run.floor = run.ActFloorCount - 1;
                        var enemy = WorldContent.Enemies.First(e => e.boss);
                        Set("currentEnemy", enemy);
                        Set("currentNode", new MapNode { floor = run.floor, lane = 1, kind = NodeKind.Boss });
                        var combat = new CombatState();
                        combat.Begin(hero, Enumerable.Repeat(GameContent.Find("strike"), 12), enemy.hp, 0, run.hp, run.maxHp, null, enemy.id, enemy.baseDamage, 127);
                        if (kind == 4) combat.player.hp = 0;
                        else for (var i = 0; i < combat.EnemyCount; i++) combat.EnemyAt(i).hp = 0;
                        Set("combat", combat);
                        Call("CheckCombat");
                        Check(ended ? run.closed : !run.closed, hero + ": actual combat resolves run closure " + kind);
                        if (!ended)
                        {
                            Check(!(bool)Get("chronicleMilestoneActive") && Get("screen").ToString() == "Reward", hero + ": rewards precede the act cinematic " + kind);
                            Check(run.act == originalAct, hero + ": reward collection stays in the completed act");
                            Call("EnterFateweave");
                        }
                        else
                        {
                            Check(Get("screen").ToString() == "RunResult", hero + ": actual combat returns the proper result screen");
                            Check((bool)Get("runResultVictory") == won, hero + ": correct victory or defeat result");
                            Check(won ? p.wins == 1 : p.losses == 1, hero + ": result recorded exactly once");
                        }
                        var originalUnlocks = p.chronicle.TotalUnlocked;
                        int continued = 0;
                        if (ended) Set("chronicleMilestoneContinue", (Action)(() => continued++));
                        Check((bool)Get("chronicleMilestoneActive"), hero + ": milestone overlay begins " + kind);
                        Check(run.act == originalAct, hero + ": act cannot advance before the book closes");
                        var phases = new HashSet<string>(); var guard = 0;
                        while ((bool)Get("chronicleMilestoneActive") && guard++ < 400)
                        {
                            var phase = Get("chronicleMilestonePhase").ToString(); phases.Add(phase);
                            if (phase == "Playing")
                            {
                                var director = (ChronicleDirector)Get("chronicleDirector"); director.Current.Skip();
                                Set("chronicleEndedAt", Time.unscaledTime - 2f);
                            }
                            Call("AdvanceChronicleMilestone", .1f);
                        }
                        Check(guard < 400, hero + ": playback always hands control back " + kind);
                        foreach (var phase in new[] { "FadeOut", "Emerge", "Playing", "Closing", "FadeIn" })
                            Check(phases.Contains(phase), hero + ": phase " + phase + " " + kind);
                        Check(p.chronicle.TotalUnlocked == originalUnlocks, hero + ": presentation adds no unlocks");
                        Check(!p.chronicle.secretCompleted, hero + ": Chapter X remains player initiated");
                        if (ended)
                        {
                            Check(continued == 1, hero + ": run-end callback fires once");
                            Call("CheckCombat");
                            Check(!(bool)Get("chronicleMilestoneActive") && continued == 1, hero + ": duplicate combat resolution cannot replay the ending");
                            Check(won ? p.wins == 1 : p.losses == 1, hero + ": duplicate combat resolution cannot add results");
                            Check(p.chronicle.viewedMemories.All(id => p.chronicle.IsUnlocked(id)), hero + ": no locked lore marked viewed");
                        }
                        else
                        {
                            Check(run.act == originalAct + 1, hero + ": exactly one next act");
                            Check(Get("screen").ToString() == "Fateweave", hero + ": next-act Fateweave restored");
                            Check(p.chronicle.IsViewed(ChronicleCatalog.MemoryFor(originalAct * 3, (ChronicleHero)(int)hero).id), hero + ": boundary memory committed");
                        }
                        report.flows++;
                    }
                    finally
                    {
                        ((ChronicleBookStageHost)Get("chronicleHost"))?.Dispose();
                        UnityEngine.Object.DestroyImmediate(host);
                    }
                }
            }
            catch (Exception error) { report.failures.Add(error.ToString()); }
            finally
            {
                // Keep verification isolated through OnApplicationQuit when leaving
                // Play Mode. The EnteredEditMode handler restores these flags.
                ProfileService.Suspended = SaveService.Suspended = true;
                UnityEngine.Random.state = savedRandom; AudioListener.volume = savedMaster;
                SceneManager.SetActiveScene(originalScene); SceneManager.UnloadSceneAsync(fixtureScene);
            }
            File.WriteAllText(Output, JsonUtility.ToJson(report, true));
            Debug.Log("[Chronicle milestone verification] " + report.checks + " checks, " + report.flows + " flows, " + report.failures.Count + " failures.");
        }
    }
}
