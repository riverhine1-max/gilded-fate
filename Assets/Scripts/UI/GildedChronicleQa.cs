#if UNITY_EDITOR || GILDED_CHRONICLE_QA
using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Chronicle;
using GildedFate.Core;
using GildedFate.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    // CHRONICLE QA: the developer-only testing panel of the QA build (GildedFate-Windows-QA.zip).
    // Open it from the main menu: the CHRONICLE QA button, or F10.
    //
    // Compiled only in the Unity Editor and in the QA build (scripting define GILDED_CHRONICLE_QA). A normal player build contains none
    // of this code, and ci/verify-qa-build.sh fails the Windows release build if any of it is found there.
    //
    // Isolation: in the QA build every save goes to a separate folder (see ChronicleQaMode) and Steam is off, so nothing here can touch the
    // player's real profile, run save, achievements or Steam. The panel works on that QA profile, using the existing Chronicle machinery
    // (ChronicleDevSession, the real sequencer and sink, the real book). The rules and bookkeeping live in ChronicleQaSession, which has tests.
    //
    // Two kinds of testing are kept apart:
    //   PREVIEW  plays any scene and saves nothing at all.
    //   RECORD   plays it the way the real game does: it only counts when the scene is genuinely unlocked or earned.
    //   Progression tests send finished runs through the real recollection rules (one revelation per victory, exact Fate Debt level only)
    //   without fabricating any real win, marks, history, achievements or Steam progress.
    public sealed partial class GildedMainMenu
    {
        private ChronicleQaSession qa;
        private int qaTab, qaHero, qaTier = 1, qaMemoryHero = -1;
        private float qaListScroll, qaLogScroll;
        private string qaStatus = "";
        private string qaSaveReport = "";
        private readonly Queue<string> qaPresentQueue = new();
        private bool qaPresenting;
        private List<ChronicleQaCheck> qaChecks;
        private Vector2 qaOrigin;   // canvas-space origin of the clip group being drawn, so hover tests match what is drawn

        private static readonly string[] QaTabs = { "ORIGINAL MEMORIES (27)", "RECOLLECTIONS (30)", "MAJOR CORRECTIONS (12)", "OPENING · CHAPTER X" };

        private ChronicleQaSession QaSession()
        {
            if (qa == null || !ReferenceEquals(qa.progress, profile.chronicle)) { var mode = qa?.mode ?? ChronicleQaPlayMode.Preview; qa = new ChronicleQaSession(profile.chronicle) { mode = mode }; }
            return qa;
        }

        // ---------------- entering, leaving, input ----------------
        private void UpdateChronicleQa()
        {
            if (screen != ScreenMode.Menu || runStartActive || bootIntroActive || !ChronicleQaMode.Active) return;
            var k = Keyboard.current;
            if (k != null && k.f10Key.wasPressedThisFrame) OpenChronicleQa();
        }

        private void OpenChronicleQa()
        {
            if (!ChronicleQaMode.Active || profile == null) return;
            qa = null; QaSession(); qaStatus = ""; qaSaveReport = ""; qaChecks = null;
            screen = ScreenMode.ChronicleQa; Sfx(SoundCue.UiConfirm);
        }

        private void CloseChronicleQa() { ProfileService.Save(profile); screen = ScreenMode.Menu; Sfx(SoundCue.UiBack); }

        // The small main-menu entry, drawn only while QA isolation is active.
        private void DrawChronicleQaMenuButton(float w, float h)
        {
            if (!ChronicleQaMode.Active) return;
            var r = new Rect(w - 340, h - 120, 160, 34); DrawButtonFrame(r, r.Contains(PointerPosition), false);
            if (GUI.Button(r, "CHRONICLE QA", new GUIStyle(buttonStyle) { fontSize = 11 })) OpenChronicleQa();
            GUI.Label(new Rect(w - 520, h - 156, 500, 20), "QA BUILD · isolated profile · F10", new GUIStyle(footerStyle) { fontSize = 11, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1f, .6f, .5f) } });
        }

        // ---------------- helpers ----------------
        private bool QaButton(Rect r, string text, bool on = false, int size = 11, bool enabled = true, Color? tint = null)
        {
            var hot = enabled && r.Contains(PointerPosition - qaOrigin);
            var fill = !enabled ? new Color(.02f, .02f, .025f, .8f) : on ? new Color(.22f, .15f, .06f, .97f) : hot ? new Color(.09f, .07f, .04f, .97f) : new Color(.02f, .024f, .032f, .95f);
            Fill(r, fill); Outline(r, !enabled ? new Color(.18f, .18f, .19f) : on ? Gold : tint ?? (hot ? new Color(.8f, .65f, .35f) : new Color(.3f, .3f, .31f)), 1);
            var style = new GUIStyle(footerStyle) { font = labelFont ? labelFont : bodyFont, fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = !enabled ? new Color(.4f, .4f, .4f) : on ? new Color(1f, .92f, .7f) : new Color(.86f, .84f, .78f) } };
            if (!enabled) { GUI.Label(r, text, style); return false; }
            return GUI.Button(r, text, style);
        }

        private void QaText(Rect r, string text, int size = 11, Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false) =>
            GUI.Label(r, text, new GUIStyle(footerStyle) { font = labelFont ? labelFont : bodyFont, fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, richText = true, wordWrap = true, alignment = anchor, normal = { textColor = color ?? new Color(.88f, .85f, .78f) } });

        private void QaSay(string text) { qaStatus = text; Debug.Log("[Chronicle QA] " + text); }

        // ---------------- playing ----------------
        private void QaPlay(string sceneId)
        {
            var q = QaSession();
            if (chronicleActive) { QaSay("A scene is already playing. Skip it first."); return; }
            if (!q.PlanPlay(sceneId, out var message)) { QaSay(message); return; }
            var preview = q.PreviewFlag;
            if (!PlayChronicleScene(sceneId, preview, () => { qaStatus = "Finished " + sceneId + (preview ? " (preview, nothing saved)" : " (recorded where it counts)"); qaSaveReport = ""; }))
                QaSay("The book could not be shown for " + sceneId + ".");
            else QaSay(message);
        }

        // The recollections the real post-victory flow would show for this hero, one after another, recorded like real viewings.
        private void QaPresentPending(ChronicleHero hero)
        {
            if (chronicleActive || qaPresenting) return;
            qaPresentQueue.Clear(); foreach (var id in QaSession().ScenesToPresent(hero)) qaPresentQueue.Enqueue(id);
            if (qaPresentQueue.Count == 0)
            {
                var waiting = profile.chronicle.PendingRevelations(hero).Count;
                QaSay(waiting > 0 ? waiting + " earned, but its authored scene is not in this build, so nothing can be shown (this is the real game's behaviour)." : "Nothing is waiting to be shown for " + ChronicleCatalog.HeroName(hero) + ".");
                return;
            }
            qaPresenting = true; QaPresentNext();
        }

        private void QaPresentNext()
        {
            if (qaPresentQueue.Count == 0) { qaPresenting = false; QaSay("All pending recollections shown."); return; }
            var id = qaPresentQueue.Dequeue();
            if (!PlayChronicleScene(id, false, QaPresentNext)) { qaPresenting = false; qaPresentQueue.Clear(); QaSay("Could not show " + id + "."); }
            else QaSay("Presenting " + id + " (" + qaPresentQueue.Count + " more waiting)");
        }

        // The whole real post-victory sequence: any new memories, the shared ending, then the earned recollections, on the open book.
        private void QaPlayRealVictoryFlow(ChronicleHero hero)
        {
            if (chronicleActive || chronicleMilestoneActive) return;
            var savedHero = run.hero; var savedAct = run.act; var savedSandbox = run.sandbox;
            run.hero = (HeroId)(int)hero; run.act = 3; run.sandbox = false;
            BeginChronicleMilestone(true, true, () => { run.hero = savedHero; run.act = savedAct; run.sandbox = savedSandbox; ProfileService.Save(profile); QaSay("Real post-victory flow finished."); });
            if (!chronicleMilestoneActive) { run.hero = savedHero; run.act = savedAct; run.sandbox = savedSandbox; QaSay("The real post-victory flow did not start."); }
            else QaSay("Playing the real post-victory flow for " + ChronicleCatalog.HeroName(hero) + ".");
        }

        // ---------------- reset and save restoration ----------------
        private void QaResetEverything()
        {
            if (chronicleActive) return;
            // The whole QA profile goes back to a fresh install's progression; only display and audio settings are kept.
            var keep = profile;
            var fresh = new PlayerProfile();
            fresh.master = keep.master; fresh.music = keep.music; fresh.effects = keep.effects; fresh.ui = keep.ui; fresh.cardAnimationSpeed = keep.cardAnimationSpeed;
            fresh.fpsLimit = keep.fpsLimit; fresh.antiAliasing = keep.antiAliasing; fresh.textureQuality = keep.textureQuality; fresh.fullscreen = keep.fullscreen; fresh.vSync = keep.vSync;
            fresh.windowMode = keep.windowMode; fresh.resolutionWidth = keep.resolutionWidth; fresh.resolutionHeight = keep.resolutionHeight; fresh.displayMigrated = keep.displayMigrated;
            fresh.reduceFlashing = keep.reduceFlashing; fresh.reduceMotion = keep.reduceMotion; fresh.brightness = keep.brightness;
            fresh.chronicle.instantText = keep.chronicle.instantText; fresh.chronicle.textSpeed = keep.chronicle.textSpeed; fresh.chronicle.openingMode = keep.chronicle.openingMode;
            fresh.EnsureMeta(); profile = fresh;
            archive = null; archiveReadings.Clear(); chronicleToasts.Clear(); archiveFlipActive = archiveSyncBook = archiveClosing = false;
            archiveOutReadingId = ""; archiveOutSpread = archiveOutReadingSpread = 0;
            for (var i = 0; i < 3; i++) selectedFateDebt[i] = 0;
            BindMetaUnlocks(); ProfileService.Save(profile);
            qa = null; QaSession(); qaChecks = null; qaSaveReport = ""; qaPresentQueue.Clear(); qaPresenting = false;
            QaSay("ALL QA PROGRESS RESET. Memories, recollections, corrections, journal pages, Fate Debt and records are fresh. The real profile was never touched.");
            Sfx(SoundCue.UiBack);
        }

        // What the game does after a restart: save, read the file back, and compare every Chronicle field.
        private void QaSaveAndReload()
        {
            if (chronicleActive) return;
            var before = ChronicleQaSession.Fingerprint(profile.chronicle);
            if (!ProfileService.Save(profile)) { qaSaveReport = "SAVE FAILED, see the log."; return; }
            var loaded = ProfileService.Load(); GildedFate.Saving.ChronicleHooks.Prepare(loaded);
            var diff = ChronicleQaSession.Compare(before, ChronicleQaSession.Fingerprint(loaded.chronicle));
            if (diff.Length == 0)
            {
                profile = loaded; qa = null; QaSession();
                qaSaveReport = "SAVE RESTORED IDENTICALLY: every unlock, viewed scene, correction, recollection, true-history flag and journal page matches (" + SaveService.DirectoryPath + ").";
            }
            else qaSaveReport = "SAVE MISMATCH (the live profile was kept):\n" + diff;
            QaSay(qaSaveReport.Split('\n')[0]);
        }

        private void QaUnlockFateDebtForRealRuns()
        {
            profile.EnsureMeta();
            for (var i = 0; i < 3; i++) profile.fateDebtUnlocked[i] = FateDebt.Count;
            BindMetaUnlocks(); ProfileService.Save(profile);
            QaSay("Fate Debt I-X is open for every hero in the QA profile. Start a real run from Character Select to test genuine wins.");
        }

        // ---------------- drawing ----------------
        private void DrawChronicleQa(float w, float h)
        {
            var q = QaSession();
            Fill(new Rect(0, 0, w, h), new Color(.012f, .01f, .016f, 1f));
            Heading(w, "CHRONICLE QA", "QA BUILD · ISOLATED PROFILE · NOTHING HERE TOUCHES REAL SAVES, ACHIEVEMENTS OR STEAM");
            var gold = new Color(1f, .84f, .47f);
            var hero = (ChronicleHero)qaHero;

            // ---- mode bar ----
            var y = 128f;
            QaText(new Rect(30, y, 90, 30), "PLAY MODE", 12, gold, TextAnchor.MiddleLeft, true);
            if (QaButton(new Rect(126, y, 250, 30), "PREVIEW · saves nothing", q.mode == ChronicleQaPlayMode.Preview, 12)) q.mode = ChronicleQaPlayMode.Preview;
            if (QaButton(new Rect(384, y, 330, 30), "RECORD · counts only when genuinely earned", q.mode == ChronicleQaPlayMode.Record, 12)) q.mode = ChronicleQaPlayMode.Record;
            QaText(new Rect(730, y, w - 760, 30), chronicleActive ? "<b>PLAYING</b>  any key skips, P pauses, Space/Enter turns a waiting page" : qaStatus, 12, chronicleActive ? gold : new Color(.6f, 1f, .7f), TextAnchor.MiddleLeft);

            // ---- left column: scenes ----
            var lx = 30f; var lw = 590f; y = 170f;
            for (var t = 0; t < QaTabs.Length; t++) if (QaButton(new Rect(lx + t * 148, y, 144, 28), QaTabs[t], qaTab == t, 10)) { qaTab = t; qaListScroll = 0; }
            y += 36;
            var all = q.Entries();
            var area = new Rect(lx, y, lw, h - y - 96);
            if (qaTab == 0) DrawQaMemories(all, area);
            else if (qaTab == 1) DrawQaRecollections(q, all, area);
            else if (qaTab == 2) DrawQaCorrections(all, area);
            else DrawQaOther(all, area);

            // ---- middle column: genuine progression ----
            var mx = 660f; var mw = 380f; y = 170f;
            QaText(new Rect(mx, y, mw, 22), "GENUINE PROGRESSION (QA PROFILE)", 12, gold, TextAnchor.MiddleLeft, true); y += 24;
            QaText(new Rect(mx, y, mw, 34), "Sends finished runs through the real recollection rules. No real win, marks, history, achievements or Steam.", 10, new Color(.7f, .68f, .62f)); y += 38;
            for (var i = 0; i < 3; i++) if (QaButton(new Rect(mx + i * 128, y, 124, 28), ChronicleCatalog.HeroName((ChronicleHero)i).ToUpperInvariant(), qaHero == i, 11)) qaHero = i;
            y += 36;
            qaTier = Mathf.Clamp(qaTier, 1, 10);
            QaText(new Rect(mx, y, 120, 26), "FATE DEBT", 11, null, TextAnchor.MiddleLeft, true);
            for (var t = 1; t <= 10; t++) if (QaButton(new Rect(mx + 84 + (t - 1) * 30, y, 28, 26), FateDebt.Roman(t), qaTier == t, 9)) qaTier = t;
            y += 34;
            DrawQaChainStatus(q, mx, ref y, mw);
            var half = (mw - 8) / 2;
            if (QaButton(new Rect(mx, y, mw, 34), "WIN A RUN AT FATE DEBT " + FateDebt.Roman(qaTier), false, 12, !chronicleActive)) QaSay(q.SimulateRun(hero, qaTier, ChronicleQaRun.FateDebtVictory).summary);
            y += 40;
            if (QaButton(new Rect(mx, y, half, 28), "DEFEAT", false, 10, !chronicleActive)) QaSay(q.SimulateRun(hero, qaTier, ChronicleQaRun.Defeat).summary);
            if (QaButton(new Rect(mx + half + 8, y, half, 28), "NORMAL WIN (no Fate Debt)", false, 10, !chronicleActive)) QaSay(q.SimulateRun(hero, qaTier, ChronicleQaRun.NormalVictory).summary);
            y += 34;
            if (QaButton(new Rect(mx, y, half, 28), "DAILY RUN WIN", false, 10, !chronicleActive)) QaSay(q.SimulateRun(hero, qaTier, ChronicleQaRun.DailyVictory).summary);
            if (QaButton(new Rect(mx + half + 8, y, half, 28), "PLAYGROUND WIN", false, 10, !chronicleActive)) QaSay(q.SimulateRun(hero, qaTier, ChronicleQaRun.PlaygroundVictory).summary);
            y += 34;
            if (QaButton(new Rect(mx, y, mw, 28), "REPEAT THE LAST RUN (same run id, must earn nothing)", false, 10, !chronicleActive)) QaSay(q.SimulateRun(hero, qaTier, ChronicleQaRun.RepeatLastRun).summary);
            y += 38;
            if (QaButton(new Rect(mx, y, mw, 28), "PREPARE: EARN EVERYTHING BEFORE FATE DEBT " + FateDebt.Roman(qaTier), false, 10, !chronicleActive)) q.PrepareForTier(hero, qaTier);
            y += 34;
            var pending = profile.chronicle.PendingRevelations(hero).Count;
            if (QaButton(new Rect(mx, y, half, 30), "SHOW PENDING (" + pending + ")", false, 10, !chronicleActive && pending > 0)) QaPresentPending(hero);
            if (QaButton(new Rect(mx + half + 8, y, half, 30), "REAL POST-VICTORY FLOW", false, 10, !chronicleActive)) QaPlayRealVictoryFlow(hero);
            y += 38;
            if (QaButton(new Rect(mx, y, mw, 28), "RUN THE RULE CHECKS (throwaway profile)", false, 10, !chronicleActive)) { qaChecks = ChronicleQaSession.RunRuleChecks(); QaSay(qaChecks.All(c => c.passed) ? "All " + qaChecks.Count + " rule checks passed." : qaChecks.Count(c => !c.passed) + " RULE CHECKS FAILED."); }
            y += 34;
            if (qaChecks != null)
            {
                var line = new Rect(mx, y, mw, h - y - 96); var sb = new System.Text.StringBuilder();
                foreach (var c in qaChecks) sb.Append(c.passed ? "<color=#8fe3a2>PASS</color> " : "<color=#ff7a6a>FAIL</color> ").Append(c.name).Append(c.passed ? "" : "  (" + c.detail + ")").Append('\n');
                QaText(line, sb.ToString(), 10);
            }

            // ---- right column: setup, state, reset ----
            var rx = 1070f; var rw = w - rx - 30; y = 170f;
            QaText(new Rect(rx, y, rw, 22), "SIMULATE UNLOCK CONDITIONS", 12, gold, TextAnchor.MiddleLeft, true); y += 26;
            if (QaButton(new Rect(rx, y, rw, 28), "UNLOCK ALL 27 MEMORIES", false, 10, !chronicleActive)) { q.UnlockAllMemories(); QaSay("All 27 memories unlocked (not viewed)."); }
            y += 33;
            if (QaButton(new Rect(rx, y, rw, 28), "OPEN CHAPTER X (30 GENUINE VICTORIES)", false, 10, !chronicleActive)) QaSay(q.OpenChapterX() ? "Chapter X is open through all three True Histories." : "Chapter X did not open.");
            y += 33;
            if (QaButton(new Rect(rx, y, rw, 28), "OPEN FATE DEBT I-X FOR REAL RUNS", false, 10, !chronicleActive)) QaUnlockFateDebtForRealRuns();
            y += 40;
            QaText(new Rect(rx, y, rw, 22), "NAVIGATION · SAVES", 12, gold, TextAnchor.MiddleLeft, true); y += 26;
            if (QaButton(new Rect(rx, y, rw, 30), "OPEN THE ARCHIVE (journal, chapter jumps)", false, 10, !chronicleActive)) { ProfileService.Save(profile); OpenChronicleArchive(); }
            y += 35;
            if (QaButton(new Rect(rx, y, rw, 30), "SAVE, RELOAD FROM DISK AND COMPARE", false, 10, !chronicleActive)) QaSaveAndReload();
            y += 36;
            if (!string.IsNullOrEmpty(qaSaveReport)) { QaText(new Rect(rx, y, rw, 92), qaSaveReport, 10, qaSaveReport.StartsWith("SAVE RESTORED") ? new Color(.6f, 1f, .7f) : new Color(1f, .5f, .4f)); }
            y += 100;
            var p = profile.chronicle;
            QaText(new Rect(rx, y, rw, 118),
                "<b>STATE</b>\nMemories unlocked " + p.TotalUnlocked + "/27 · viewed " + p.TotalViewed + "\nRecollections earned " + p.ascension.earned.Count + "/30 · watched " + p.ascension.watched.Count +
                "\nCorrections " + p.corrections.Count + "/10 shared · " + p.ascension.corrections.Count + "/12 new\nChapter X " + (p.secretCompleted ? "completed" : p.secretUnlocked ? "open" : "locked") + " · narrator " + p.NarratorState, 10);

            // ---- bottom bar ----
            var reset = new Rect(w - 372, h - 76, 338, 46);
            if (QaButton(reset, "RESET ALL QA PROGRESS", false, 13, !chronicleActive, new Color(1f, .42f, .32f))) QaResetEverything();
            var back = new Rect(34, h - 76, 148, 46); DrawButtonFrame(back, back.Contains(PointerPosition), false);
            if (!chronicleActive && GUI.Button(back, "BACK", buttonStyle)) CloseChronicleQa();
            QaText(new Rect(200, h - 80, w - 600, 54), string.Join("\n", q.log.Skip(Mathf.Max(0, q.log.Count - 3))), 10, new Color(.7f, .68f, .62f), TextAnchor.LowerLeft);
        }

        private void DrawQaChainStatus(ChronicleQaSession q, float x, ref float y, float width)
        {
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < 3; i++)
            {
                var hero = (ChronicleHero)i; var next = profile.chronicle.NextRevelation(hero);
                sb.Append(i == qaHero ? "<b>" : "").Append(ChronicleCatalog.HeroName(hero).ToUpperInvariant()).Append(" ").Append(profile.chronicle.RevelationsEarned(hero)).Append("/10")
                  .Append(" · next ").Append(next == null ? "TRUE HISTORY COMPLETE" : "Fate Debt " + FateDebt.Roman(next.tier)).Append(i == qaHero ? "</b>" : "").Append('\n');
            }
            QaText(new Rect(x, y, width, 56), sb.ToString(), 11); y += 62;
        }

        // ---- tab 0: the 27 original memories ----
        private void DrawQaMemories(List<ChronicleQaEntry> all, Rect area)
        {
            var heroFilter = qaMemoryHero;
            var x = area.x; var by = area.y;
            QaText(new Rect(x, by, 50, 24), "HERO", 10, null, TextAnchor.MiddleLeft, true);
            if (QaButton(new Rect(x + 52, by, 52, 24), "ALL", heroFilter < 0, 10)) heroFilter = qaMemoryHero = -1;
            for (var i = 0; i < 3; i++) if (QaButton(new Rect(x + 108 + i * 90, by, 86, 24), ChronicleCatalog.HeroName((ChronicleHero)i).ToUpperInvariant(), heroFilter == i, 10)) heroFilter = qaMemoryHero = i;
            area.y += 30; area.height -= 30;
            var rows = all.Where(e => e.group == ChronicleQaGroup.Memory && (heroFilter < 0 || e.hero == heroFilter)).ToList();
            DrawQaList(area, rows, e =>
            {
                var mem = ChronicleCatalog.FindMemory(e.id);
                var unlocked = profile.chronicle.IsUnlocked(e.id);
                QaRowButtons(e, area.width, mem != null && !unlocked ? "MEET RULE" : null, () => QaSession().SatisfyMemory(e.id));
            });
        }

        // ---- tab 1: hero x Fate Debt grid + the selected revelation ----
        private void DrawQaRecollections(ChronicleQaSession q, List<ChronicleQaEntry> all, Rect area)
        {
            var x = area.x; var y = area.y;
            QaText(new Rect(x, y, 80, 24), "HERO", 10, null, TextAnchor.MiddleLeft, true);
            for (var i = 0; i < 3; i++) if (QaButton(new Rect(x + 52 + i * 128, y, 124, 24), ChronicleCatalog.HeroName((ChronicleHero)i).ToUpperInvariant(), qaHero == i, 10)) qaHero = i;
            y += 32;
            QaText(new Rect(x, y, 80, 26), "FATE DEBT", 10, null, TextAnchor.MiddleLeft, true);
            for (var t = 1; t <= 10; t++)
            {
                var d = ChronicleAscensionCatalog.At((ChronicleHero)qaHero, t); var entry = all.First(e => e.id == d.sceneId);
                var earned = profile.chronicle.IsRevelationEarned(d.id);
                var rect = new Rect(x + 84 + (t - 1) * 49, y, 46, 26);
                if (QaButton(rect, FateDebt.Roman(t) + (d.IsMajor ? "*" : ""), qaTier == t, 10, true, earned ? new Color(.45f, .85f, .55f) : entry != null && !entry.available ? new Color(.5f, .25f, .2f) : (Color?)null)) qaTier = t;
            }
            y += 34;
            QaText(new Rect(x, y, area.width, 30), "* = major correction (Fate Debt I, IV, VII, X).  Green = earned in the QA profile.  Red outline = scene not installed in this build.", 9, new Color(.7f, .68f, .62f));
            y += 34;
            var def = ChronicleAscensionCatalog.At((ChronicleHero)qaHero, qaTier);
            var sel = all.First(e => e.id == def.sceneId);
            var card = new Rect(x, y, area.width, 200); Fill(card, new Color(.04f, .03f, .02f, .95f)); Outline(card, new Color(.34f, .24f, .12f), 1);
            var text = "<b>" + def.id + "</b>   " + ChronicleCatalog.HeroName(def.hero) + " · Fate Debt " + FateDebt.Roman(def.tier) + "\n" +
                       (def.IsMajor ? "MAJOR CORRECTION " + def.kindOrdinal + "  ·  correction id " + def.correctionId : "Smaller revelation " + def.kindOrdinal) +
                       (def.IsTrueHistory ? "  ·  completes the hero's True History" : "") + "\n" +
                       "State: " + sel.state + "\n" +
                       (profile.chronicle.HasAscensionCorrection(def.correctionId) && def.IsMajor ? "Correction recorded in the journal.\n" : "") +
                       (sel.available ? "Scene installed" : "<color=#ff9a8a>" + sel.unavailableReason + ".</color> Install the Ascension content to preview it; every other tool here already works.");
            QaText(new Rect(card.x + 12, card.y + 10, card.width - 24, 110), text, 12);
            var bw = (card.width - 36) / 3f; var by = card.y + 126;
            if (QaButton(new Rect(card.x + 12, by, bw, 32), (q.mode == ChronicleQaPlayMode.Preview ? "PREVIEW" : "PLAY") + " / REPLAY", false, 11, !chronicleActive && sel.available)) QaPlay(sel.id);
            if (QaButton(new Rect(card.x + 18 + bw, by, bw, 32), "EARN IT (RULES)", false, 10, !chronicleActive)) { QaSay(q.EarnThrough((ChronicleHero)qaHero, qaTier) ? "Earned " + def.id + " through the genuine rule." : "Could not earn " + def.id + "."); }
            if (QaButton(new Rect(card.x + 24 + bw * 2, by, bw, 32), "WIN A RUN AT THIS LEVEL", false, 10, !chronicleActive)) QaSay(q.SimulateRun((ChronicleHero)qaHero, qaTier, ChronicleQaRun.FateDebtVictory).summary);
            QaText(new Rect(card.x + 12, by + 38, card.width - 24, 34), "PREVIEW plays it without saving. In RECORD mode it only counts once the recollection is earned.", 9, new Color(.7f, .68f, .62f));
        }

        // ---- tab 2: the 12 new major corrections ----
        private void DrawQaCorrections(List<ChronicleQaEntry> all, Rect area)
        {
            var rows = all.Where(e => e.major).ToList();
            DrawQaList(area, rows, e =>
            {
                var d = ChronicleAscensionCatalog.FindByScene(e.id);
                QaRowButtons(e, area.width, !profile.chronicle.IsRevelationEarned(d.id) ? "EARN IT" : null, () => QaSession().EarnThrough(d.hero, d.tier));
            });
        }

        // ---- tab 3: opening and Chapter X ----
        private void DrawQaOther(List<ChronicleQaEntry> all, Rect area)
        {
            var rows = all.Where(e => e.group == ChronicleQaGroup.Opening || e.group == ChronicleQaGroup.Finale).ToList();
            DrawQaList(area, rows, e =>
                QaRowButtons(e, area.width, e.group == ChronicleQaGroup.Finale && !profile.chronicle.secretUnlocked ? "OPEN (GENUINE)" : null, () => QaSession().OpenChapterX()));
        }

        private void DrawQaList(Rect area, List<ChronicleQaEntry> rows, Action<ChronicleQaEntry> rowButtons)
        {
            const float rowH = 34f; var content = rows.Count * rowH;
            UpdateScrollArea(area, ref qaListScroll, content);
            GUI.BeginGroup(area);
            for (var i = 0; i < rows.Count; i++)
            {
                var r = new Rect(0, i * rowH - qaListScroll, area.width - 14, rowH - 3); if (r.yMax < 0 || r.y > area.height) continue;
                var e = rows[i];
                Fill(r, e.available ? new Color(.03f, .028f, .026f, .95f) : new Color(.025f, .012f, .012f, .95f));
                var stateColor = e.state.StartsWith("viewed") || e.state.StartsWith("watched") ? "#8fe3a2" : e.state.StartsWith("unlocked") || e.state.StartsWith("earned") || e.state.StartsWith("presented") ? "#ffd780" : "#9a948a";
                QaText(new Rect(r.x + 8, r.y, r.width - 262, r.height), e.label + "   <color=" + stateColor + ">" + e.state + "</color>" + (e.available ? "" : "   <color=#ff9a8a>not in this build</color>"), 10, null, TextAnchor.MiddleLeft);
                // Row buttons are drawn by the tab; translate into the row's local space.
                GUI.BeginGroup(new Rect(0, r.y, area.width, r.height));
                qaOrigin = new Vector2(area.x, area.y + r.y);
                rowButtons(e);
                qaOrigin = Vector2.zero;
                GUI.EndGroup();
            }
            GUI.EndGroup();
            DrawScrollRail(area, ref qaListScroll, content, rows.Count + " SCENES");
        }

        // The two buttons at the right end of a row: PLAY / REPLAY, and an optional setup action that simulates what the scene needs.
        private void QaRowButtons(ChronicleQaEntry e, float areaWidth, string setupLabel, Action setup)
        {
            const float rowH = 34f; var right = areaWidth - 14;
            var play = new Rect(right - 82, 3, 78, rowH - 9);
            var verb = QaSession().mode == ChronicleQaPlayMode.Preview ? "PREVIEW" : "PLAY";
            if (QaButton(play, verb, false, 10, !chronicleActive && e.available)) QaPlay(e.id);
            if (setupLabel != null && QaButton(new Rect(right - 82 - 4 - 104, 3, 104, rowH - 9), setupLabel, false, 9, !chronicleActive)) { setup(); }
        }
    }
}
#endif
