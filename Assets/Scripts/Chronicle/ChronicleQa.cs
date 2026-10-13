#if UNITY_EDITOR || GILDED_CHRONICLE_QA || GF_HARNESS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GildedFate.Chronicle
{
    // CHRONICLE QA: the logic behind the QA build's testing panel, with no drawing and no engine.
    // This whole file is compiled only in the Unity Editor, in the QA build (scripting define GILDED_CHRONICLE_QA) and in the
    // test harness. A normal player build contains none of it; ci/verify-qa-build.sh checks that on every Windows build.
    //
    // It reuses ChronicleDevSession for milestone simulation, the unlock tables and the headless scene check, and it only ever
    // touches the ChronicleProgress it is handed. In the QA build that object lives in the isolated QA profile, never the player's.

    public enum ChronicleQaGroup { Opening, Memory, Revelation, Finale }

    /// <summary>Preview saves nothing at all. Record commits exactly as the real game does: a scene only counts once it is genuinely
    /// unlocked (memories) or earned (recollections), so viewing can never earn anything.</summary>
    public enum ChronicleQaPlayMode { Preview, Record }

    /// <summary>What kind of finished run to send through the real Chronicle run-end rules.</summary>
    public enum ChronicleQaRun { FateDebtVictory, Defeat, NormalVictory, DailyVictory, PlaygroundVictory, RepeatLastRun }

    public sealed class ChronicleQaEntry
    {
        public string id = "", label = "", state = "";
        public ChronicleQaGroup group;
        public int hero = -1, tier;
        public bool major;                     // one of the 12 major corrections
        public string correctionId = "";       // its ASC_CORR_* id
        public bool available = true;          // the scene is registered in this build
        public string unavailableReason = "";
    }

    public sealed class ChronicleQaRunReport
    {
        public bool earned, duplicate, secretUnlocked, qualified;
        public string revelationId = "", summary = "";
        public List<string> newMemories = new List<string>();
    }

    public sealed class ChronicleQaCheck
    {
        public string name = "", detail = ""; public bool passed;
    }

    public sealed class ChronicleQaSession
    {
        public const int LogLimit = 60;
        public readonly ChronicleProgress progress;
        /// <summary>The existing developer session, bound to the same progress: milestone simulation, unlock tables, headless scene checks.</summary>
        public readonly ChronicleDevSession dev;
        public ChronicleQaPlayMode mode = ChronicleQaPlayMode.Preview;
        public readonly List<string> log = new List<string>();
        ChronicleHero lastHero; int lastFateDebt; string lastRunId = "";

        public ChronicleQaSession(ChronicleProgress progress)
        {
            this.progress = progress ?? new ChronicleProgress(); this.progress.Ensure();
            dev = new ChronicleDevSession(this.progress);
        }

        void Note(string line) { log.Add(line); if (log.Count > LogLimit) log.RemoveRange(0, log.Count - LogLimit); }

        // ---------------------------------------------------------------- the catalogue of everything previewable
        static ChronicleQaEntry Missing(ChronicleQaEntry e, string reason) { e.available = false; e.unavailableReason = reason; return e; }

        public List<ChronicleQaEntry> Entries()
        {
            var list = new List<ChronicleQaEntry>();
            list.Add(Scene(new ChronicleQaEntry { id = ChronicleCatalog.OpeningSceneId, label = "Opening cinematic (full)", group = ChronicleQaGroup.Opening, state = "always available" }));
            list.Add(Scene(new ChronicleQaEntry { id = ChronicleCatalog.OpeningShortSceneId, label = "Opening cinematic (short)", group = ChronicleQaGroup.Opening, state = "always available" }));
            foreach (var m in ChronicleCatalog.Memories)
                list.Add(Scene(new ChronicleQaEntry
                {
                    id = m.id, hero = (int)m.hero, group = ChronicleQaGroup.Memory,
                    label = m.id + "  Ch " + m.chapter + "  " + ChronicleCatalog.HeroName(m.hero) + "  " + m.title + (m.corrections != null && m.corrections.Length > 0 ? "  [" + string.Join(", ", m.corrections) + "]" : ""),
                    state = progress.IsViewed(m.id) ? "viewed" : progress.IsUnlocked(m.id) ? "unlocked" : "locked"
                }));
            foreach (var d in ChronicleAscensionCatalog.All)
                list.Add(Scene(new ChronicleQaEntry
                {
                    id = d.sceneId, hero = (int)d.hero, tier = d.tier, major = d.IsMajor, correctionId = d.correctionId, group = ChronicleQaGroup.Revelation,
                    label = d.id + "  " + ChronicleCatalog.HeroName(d.hero) + "  Fate Debt " + Roman(d.tier) + "  " + (d.IsMajor ? "MAJOR CORRECTION " + d.kindOrdinal + " (" + d.correctionId + ")" : "small revelation " + d.kindOrdinal),
                    state = progress.IsRevelationWatched(d.id) ? "watched" : progress.IsRevelationPresented(d.id) ? "presented" : progress.IsRevelationEarned(d.id) ? "earned, waiting to be shown" : "not earned"
                }));
            list.Add(Scene(new ChronicleQaEntry { id = ChronicleCatalog.SecretSceneId, label = "Chapter X  The One Who Remembers", group = ChronicleQaGroup.Finale, state = progress.secretCompleted ? "completed" : progress.secretUnlocked ? "unlocked" : "locked" }));
            return list;
        }

        static ChronicleQaEntry Scene(ChronicleQaEntry e)
        {
            if (!ChronicleScripts.Has(e.id)) return Missing(e, "this scene is not registered in this build");
            return e;
        }

        public ChronicleQaEntry Find(string sceneId) { foreach (var e in Entries()) if (e.id == sceneId) return e; return null; }
        public ChronicleQaEntry Revelation(ChronicleHero hero, int tier) { var d = ChronicleAscensionCatalog.At(hero, tier); return d == null ? null : Find(d.sceneId); }

        public int MajorCorrectionCount => ChronicleAscensionCatalog.All.Count(d => d.IsMajor);
        public int AvailableCount(ChronicleQaGroup group) => Entries().Count(e => e.group == group && e.available);
        public int AvailableMajorCount => Entries().Count(e => e.major && e.available);

        public static string Roman(int n) => n >= 1 && n <= 10 ? new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[n] : n.ToString();

        // ---------------------------------------------------------------- playing
        /// <summary>Whether playing this scene right now would commit anything to the QA profile.</summary>
        public bool WillRecord(string sceneId)
        {
            if (mode != ChronicleQaPlayMode.Record) return false;
            if (sceneId == ChronicleCatalog.OpeningSceneId || sceneId == ChronicleCatalog.OpeningShortSceneId) return true;
            if (sceneId == ChronicleCatalog.SecretSceneId) return progress.secretUnlocked;
            var rev = ChronicleAscensionCatalog.FindByScene(sceneId);
            if (rev != null) return progress.IsRevelationEarned(rev.id);
            return progress.IsUnlocked(sceneId);
        }

        /// <summary>The preview flag to hand to the book. True saves nothing. Also notes what is about to happen in the log.</summary>
        public bool PlanPlay(string sceneId, out string message)
        {
            var e = Find(sceneId);
            if (e == null) { message = "Unknown scene " + sceneId; return false; }
            if (!e.available) { message = e.id + ": " + e.unavailableReason + "."; Note(message); return false; }
            var preview = mode == ChronicleQaPlayMode.Preview;
            message = (preview ? "PREVIEW " : WillRecord(sceneId) ? "RECORD " : "RECORD (will save nothing: not unlocked yet) ") + sceneId;
            Note(message);
            return true;
        }

        public bool PreviewFlag => mode == ChronicleQaPlayMode.Preview;

        // ---------------------------------------------------------------- genuine progression, in the isolated profile
        static string NewRunId() => "qa-" + Guid.NewGuid().ToString("N");

        /// <summary>Sends one finished run through the same two calls the game's run-end code makes (the Fate Debt recollection hook,
        /// then the original run-end milestones), against the QA profile only. It touches no marks, history, achievements or Steam,
        /// so it fabricates no real win, yet it exercises the real rules: one revelation per victory, exact Fate Debt level only.</summary>
        public ChronicleQaRunReport SimulateRun(ChronicleHero hero, int fateDebt, ChronicleQaRun kind)
        {
            var report = new ChronicleQaRunReport();
            bool victory = true, sandbox = false, daily = false; var fd = fateDebt; var runId = NewRunId();
            switch (kind)
            {
                case ChronicleQaRun.Defeat: victory = false; break;
                case ChronicleQaRun.NormalVictory: fd = 0; break;
                case ChronicleQaRun.DailyVictory: daily = true; break;
                case ChronicleQaRun.PlaygroundVictory: sandbox = true; break;
                case ChronicleQaRun.RepeatLastRun:
                    if (string.IsNullOrEmpty(lastRunId)) { report.summary = "There is no earlier simulated run to repeat."; Note(report.summary); return report; }
                    hero = lastHero; fd = lastFateDebt; runId = lastRunId; break;
            }
            var heroName = ChronicleCatalog.HeroName(hero);
            var label = heroName + " " + (kind == ChronicleQaRun.Defeat ? "defeat" : kind == ChronicleQaRun.NormalVictory ? "Normal victory" : kind == ChronicleQaRun.DailyVictory ? "Daily Run victory" : kind == ChronicleQaRun.PlaygroundVictory ? "Playground victory" : "victory at Fate Debt " + Roman(fd)) + (kind == ChronicleQaRun.RepeatLastRun ? " (same run again)" : "");

            var before = progress.RevelationsEarned(hero);
            ChronicleAscensionResult asc;
            report.qualified = ChronicleAscension.IsQualifyingVictory(victory, sandbox, daily, fd);
            if (report.qualified) asc = progress.RegisterAscensionVictory(hero, fd, runId);
            else asc = new ChronicleAscensionResult { reason = "not a qualifying victory" };
            // The Playground never reaches the run-end milestones; every other finished run does.
            ChronicleUnlockResult end = null;
            if (!sandbox) end = progress.RegisterRunEnded(hero, victory, victory ? 3 : 1, victory ? 54 : 3, runId);
            lastHero = hero; lastFateDebt = fd; lastRunId = runId;

            report.earned = asc.Earned; report.duplicate = asc.duplicate; report.secretUnlocked = asc.secretUnlocked || (end != null && end.secretUnlocked);
            report.revelationId = asc.Earned ? asc.earned.id : "";
            if (end != null) report.newMemories.AddRange(end.newMemories);
            if (asc.unlocks != null) foreach (var m in asc.unlocks.newMemories) if (!report.newMemories.Contains(m)) report.newMemories.Add(m);

            var sb = new StringBuilder(label).Append(": ");
            if (asc.Earned) sb.Append("EARNED ").Append(asc.earned.id).Append(" (").Append(progress.RevelationsEarned(hero)).Append("/10)");
            else sb.Append("nothing earned (").Append(asc.reason).Append(")");
            if (report.secretUnlocked) sb.Append("  + CHAPTER X OPENED");
            if (report.newMemories.Count > 0) sb.Append("  | memories unlocked: ").Append(string.Join(", ", report.newMemories));
            var next = progress.NextRevelation(hero);
            sb.Append("  | next for ").Append(heroName).Append(": ").Append(next == null ? "True History complete" : "Fate Debt " + Roman(next.tier));
            report.summary = sb.ToString();
            if (progress.RevelationsEarned(hero) - before > 1) report.summary += "  !! MORE THAN ONE REVELATION IN A SINGLE VICTORY !!";
            Note(report.summary);
            return report;
        }

        /// <summary>The scenes the real post-victory flow would show now for this hero, in order (authored, approved and registered only).</summary>
        public List<string> ScenesToPresent(ChronicleHero hero) => ChronicleAscension.ScenesToPresent(progress, hero);

        // ---------------------------------------------------------------- simulated story-unlock conditions
        /// <summary>Meets one memory's own unlock rule by sending the matching milestone through the real registration calls.</summary>
        public bool SatisfyMemory(string memoryId)
        {
            var m = ChronicleCatalog.FindMemory(memoryId);
            if (m == null) { Note("No memory " + memoryId); return false; }
            if (progress.IsUnlocked(m.id)) { Note(m.id + " is already unlocked."); return true; }
            switch (m.rule.metric)
            {
                case ChronicleMetric.RunsEnded: dev.SimulateRunEnded(m.hero, false, 1, 3); break;
                case ChronicleMetric.BestFloor: dev.SimulateFloor(m.hero, m.rule.threshold); break;
                case ChronicleMetric.ActBossDefeated: dev.SimulateBoss(m.hero, m.rule.act); break;
                case ChronicleMetric.Wins: dev.SimulateRunEnded(m.hero, true, 3, 54); break;
            }
            Note("Met the rule for " + m.id + " (" + m.rule.hint + ")  -> " + (progress.IsUnlocked(m.id) ? "unlocked" : "STILL LOCKED"));
            return progress.IsUnlocked(m.id);
        }

        public void UnlockAllMemories() { dev.UnlockEverythingButChapterX(); dev.RefreshUnlocks(); Note("All 27 memories unlocked in the QA profile. Chapter X stays locked until all three True Histories are earned."); }

        /// <summary>Earns this hero's recollections for Fate Debt I .. tier-1 through the genuine rule, one victory each, and treats them
        /// as already shown, so the next real victory at `tier` is the one that gets presented.</summary>
        public int PrepareForTier(ChronicleHero hero, int tier)
        {
            var n = 0;
            for (var guard = 0; guard < ChronicleAscensionCatalog.TierCount; guard++)
            {
                var next = progress.NextRevelation(hero);
                if (next == null || next.tier >= tier) break;
                var r = SimulateRun(hero, next.tier, ChronicleQaRun.FateDebtVictory);
                if (!r.earned) break;
                progress.MarkRevelationPresented(r.revelationId); n++;
            }
            Note(ChronicleCatalog.HeroName(hero) + " is now ready for Fate Debt " + Roman(tier) + " (" + n + " earlier recollection" + (n == 1 ? "" : "s") + " earned and treated as shown).");
            return n;
        }

        /// <summary>Earns this hero's chain up to and including `tier`; the final one is left waiting to be presented.</summary>
        public bool EarnThrough(ChronicleHero hero, int tier)
        {
            PrepareForTier(hero, tier);
            if (progress.IsRevelationEarned(ChronicleAscensionCatalog.IdFor(hero, tier))) return true;
            var next = progress.NextRevelation(hero);
            return next != null && next.tier == tier && SimulateRun(hero, tier, ChronicleQaRun.FateDebtVictory).earned;
        }

        /// <summary>The genuine Chapter X route: every hero's True History, by ten rule-driven victories each. Nothing is forced.</summary>
        public bool OpenChapterX()
        {
            foreach (ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero)))
            {
                PrepareForTier(hero, ChronicleAscensionCatalog.TierCount);
                EarnThrough(hero, ChronicleAscensionCatalog.TierCount);
                foreach (var d in progress.PendingRevelations(hero)) progress.MarkRevelationPresented(d.id);
            }
            dev.RefreshUnlocks();
            Note("Chapter X: " + (progress.secretUnlocked ? "OPEN" : "still locked"));
            return progress.secretUnlocked;
        }

        // ---------------------------------------------------------------- reset
        /// <summary>Clears every Chronicle test result: memories, corrections, recollections, journal positions and the log. Reading preferences are kept.</summary>
        public void ResetAll()
        {
            progress.ResetAll(); progress.Ensure(); log.Clear(); lastRunId = "";
            Note("QA Chronicle progress reset.");
        }

        // ---------------------------------------------------------------- save restoration
        /// <summary>A line-by-line description of everything the Chronicle saves. Two equal fingerprints mean a save restored identically.</summary>
        public static string Fingerprint(ChronicleProgress p)
        {
            p.Ensure();
            string J(IEnumerable<string> ids) => string.Join(",", ids);
            var sb = new StringBuilder();
            sb.Append("memories unlocked=").Append(J(p.unlockedMemories)).Append('\n');
            sb.Append("memories viewed=").Append(J(p.viewedMemories)).Append('\n');
            sb.Append("facts=").Append(J(p.discoveredFacts)).Append('\n');
            sb.Append("corrections=").Append(J(p.corrections)).Append('\n');
            sb.Append("asc earned=").Append(J(p.ascension.earned)).Append('\n');
            sb.Append("asc presented=").Append(J(p.ascension.presented)).Append('\n');
            sb.Append("asc watched=").Append(J(p.ascension.watched)).Append('\n');
            sb.Append("asc pending=").Append(J(p.ascension.pending)).Append('\n');
            sb.Append("asc corrections=").Append(J(p.ascension.corrections)).Append('\n');
            sb.Append("fate debt X wins=").Append(string.Join(",", p.ascension.fateDebtXWins)).Append('\n');
            sb.Append("counters=").Append(string.Join(",", p.runsEnded)).Append('|').Append(string.Join(",", p.wins)).Append('|').Append(string.Join(",", p.bestFloor)).Append('|').Append(string.Join(",", p.actBossMask)).Append('\n');
            sb.Append("chapter X=").Append(p.secretUnlocked).Append('/').Append(p.secretCompleted).Append('\n');
            sb.Append("opening views=").Append(p.openingViews).Append(" last chapter=").Append(p.lastChapterOpened).Append('\n');
            sb.Append("story=").Append(p.journal.storySpread).Append('/').Append(p.journal.storySceneId).Append(" reading=").Append(p.journal.readingSpread).Append('/').Append(p.journal.readingId).Append('\n');
            foreach (var m in p.journal.pages) sb.Append("page ").Append(m.id).Append('=').Append(m.page).Append('\n');
            return sb.ToString();
        }

        /// <summary>Compares two fingerprints and says what differs, line by line.</summary>
        public static string Compare(string before, string after)
        {
            if (before == after) return "";
            var a = before.Split('\n'); var b = after.Split('\n'); var sb = new StringBuilder();
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var x = i < a.Length ? a[i] : "(missing)"; var y = i < b.Length ? b[i] : "(missing)";
                if (x != y) sb.Append("saved:    ").Append(x).Append("\nrestored: ").Append(y).Append('\n');
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- the rule battery
        /// <summary>Runs the one-revelation-per-victory rules against a throwaway profile and reports each result. It never touches the QA profile.</summary>
        public static List<ChronicleQaCheck> RunRuleChecks()
        {
            var checks = new List<ChronicleQaCheck>();
            void Check(string name, bool ok, string detail = "") => checks.Add(new ChronicleQaCheck { name = name, passed = ok, detail = detail });
            var q = new ChronicleQaSession(new ChronicleProgress());
            var hero = ChronicleHero.Vanguard; var p = q.progress;

            var r = q.SimulateRun(hero, 1, ChronicleQaRun.FateDebtVictory);
            Check("Fate Debt I victory earns exactly the first recollection", r.earned && r.revelationId == "ASC_VAN_01" && p.RevelationsEarned(hero) == 1, r.summary);
            r = q.SimulateRun(hero, 0, ChronicleQaRun.RepeatLastRun);
            Check("Repeating the same run earns nothing", !r.earned && p.RevelationsEarned(hero) == 1, r.summary);
            r = q.SimulateRun(hero, 1, ChronicleQaRun.FateDebtVictory);
            Check("A second victory at the same level earns nothing", !r.earned && p.RevelationsEarned(hero) == 1, r.summary);
            r = q.SimulateRun(hero, 4, ChronicleQaRun.FateDebtVictory);
            Check("Skipping ahead to Fate Debt IV earns nothing and does not backfill II or III", !r.earned && p.RevelationsEarned(hero) == 1 && !p.IsRevelationEarned("ASC_VAN_02"), r.summary);
            foreach (var kind in new[] { ChronicleQaRun.Defeat, ChronicleQaRun.NormalVictory, ChronicleQaRun.DailyVictory, ChronicleQaRun.PlaygroundVictory })
            {
                r = q.SimulateRun(hero, 2, kind);
                Check(kind + " earns nothing, even at the level that is due", !r.earned && p.RevelationsEarned(hero) == 1, r.summary);
            }
            r = q.SimulateRun(ChronicleHero.Hexer, 2, ChronicleQaRun.FateDebtVictory);
            Check("Another hero's victory never advances this hero's chain", !r.earned && p.RevelationsEarned(hero) == 1 && p.RevelationsEarned(ChronicleHero.Hexer) == 0, r.summary);
            r = q.SimulateRun(hero, 2, ChronicleQaRun.FateDebtVictory);
            Check("Fate Debt II victory earns the second recollection", r.earned && r.revelationId == "ASC_VAN_02" && p.RevelationsEarned(hero) == 2, r.summary);

            var each = true; var detail = "";
            for (var tier = 3; tier <= 10; tier++)
            {
                var before = p.RevelationsEarned(hero); r = q.SimulateRun(hero, tier, ChronicleQaRun.FateDebtVictory);
                if (!r.earned || p.RevelationsEarned(hero) != before + 1) { each = false; detail = "tier " + tier + ": " + r.summary; break; }
            }
            Check("Each remaining victory, Fate Debt III to X in order, earns exactly one", each && p.TrueHistoryComplete(hero), detail);
            r = q.SimulateRun(hero, 10, ChronicleQaRun.FateDebtVictory);
            Check("Once the True History is complete a further Fate Debt X victory earns nothing", !r.earned && p.RevelationsEarned(hero) == 10, r.summary);
            Check("Chapter X stays closed with only one True History complete", !p.secretUnlocked);
            q.EarnThrough(ChronicleHero.Hexer, 10); q.EarnThrough(ChronicleHero.Reaper, 9);
            Check("Chapter X stays closed until the third True History", !p.secretUnlocked);
            q.EarnThrough(ChronicleHero.Reaper, 10);
            Check("Chapter X opens with all three True Histories", p.secretUnlocked && p.AllTrueHistoriesComplete);
            Check("Thirty recollections earned in total", ChronicleAscensionCatalog.All.All(d => p.IsRevelationEarned(d.id)));

            var scratch = new ChronicleQaSession(new ChronicleProgress());
            var pending = scratch.progress.PendingRevelations(ChronicleHero.Reaper).Count;
            scratch.SimulateRun(ChronicleHero.Reaper, 1, ChronicleQaRun.FateDebtVictory);
            Check("A victory queues the earned recollection for presentation", scratch.progress.PendingRevelations(ChronicleHero.Reaper).Count == pending + 1);
            return checks;
        }
    }
}
#endif
