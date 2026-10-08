using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>Unlock rules, save data, duplicate prevention, Chapter X and old-save compatibility.</summary>
    [TestFixture]
    public class ChronicleProgressTests
    {
#if GF_HARNESS
        static readonly System.Text.Json.JsonSerializerOptions Options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        public static string ToJson(ChronicleProgress p) => System.Text.Json.JsonSerializer.Serialize(p, Options);
        public static ChronicleProgress FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<ChronicleProgress>(json, Options);
#else
        public static string ToJson(ChronicleProgress p) => UnityEngine.JsonUtility.ToJson(p);
        public static ChronicleProgress FromJson(string json) => UnityEngine.JsonUtility.FromJson<ChronicleProgress>(json);
#endif
        static ChronicleProgress WinWithEveryone()
        {
            var p = new ChronicleProgress();
            foreach (ChronicleHero h in Enum.GetValues(typeof(ChronicleHero))) p.RegisterRunEnded(h, true, 3, 54, "win-" + h);
            return p;
        }

        // ---------------------------------------------------------------- catalog shape
        [Test] public void CatalogHasNineChaptersOfThreeAndTwentySevenMemories()
        {
            Assert.AreEqual(27, ChronicleCatalog.Memories.Length);
            for (var c = 1; c <= 9; c++)
            {
                var inChapter = ChronicleCatalog.MemoriesInChapter(c).ToList();
                Assert.AreEqual(3, inChapter.Count, "chapter " + c);
                foreach (ChronicleHero h in Enum.GetValues(typeof(ChronicleHero))) Assert.AreEqual(1, inChapter.Count(m => m.hero == h), "chapter " + c + " " + h);
            }
        }

        [Test] public void EachHeroOwnsNineDistinctMemories()
        {
            foreach (ChronicleHero h in Enum.GetValues(typeof(ChronicleHero)))
            {
                var mine = ChronicleCatalog.Memories.Where(m => m.hero == h).ToList();
                Assert.AreEqual(9, mine.Count, h.ToString());
                Assert.AreEqual(9, mine.Select(m => m.title).Distinct().Count(), h + " titles");
            }
        }

        [Test] public void MemoryAndCorrectionIdsAreStable()
        {
            Assert.AreEqual("MEM_07", ChronicleCatalog.MemoryFor(3, ChronicleHero.Vanguard).id);
            Assert.AreEqual("MEM_27", ChronicleCatalog.MemoryFor(9, ChronicleHero.Reaper).id);
            Assert.AreEqual("Three travelers stood together.", ChronicleCatalog.FindCorrection("CORR_01").original);
            Assert.AreEqual("There were four.", ChronicleCatalog.FindCorrection("CORR_01").corrected);
            Assert.AreEqual(10, ChronicleCatalog.Corrections.Length);
        }

        [Test] public void AuthoredContentPassesTheValidator()
        {
            var errors = ChronicleValidator.Validate();
            Assert.AreEqual(0, errors.Count, string.Join("; ", errors));
        }

        // ---------------------------------------------------------------- unlock rules
        [Test] public void FreshChronicleHasNothingUnlocked()
        {
            var p = new ChronicleProgress(); p.Ensure();
            Assert.AreEqual(0, p.TotalUnlocked); Assert.AreEqual(0, p.TotalViewed); Assert.IsFalse(p.secretUnlocked);
            Assert.AreEqual(ChronicleNarratorState.ConfidentHistorian, p.NarratorState);
        }

        [Test] public void FinishingARunUnlocksOnlyThatHerosFirstChapter()
        {
            var p = new ChronicleProgress();
            var r = p.RegisterRunEnded(ChronicleHero.Hexer, false, 1, 2, "r1");
            Assert.AreEqual(1, r.newMemories.Count);
            Assert.IsTrue(p.IsUnlocked("MEM_02"), "Hexer chapter I");
            Assert.IsFalse(p.IsUnlocked("MEM_01")); Assert.IsFalse(p.IsUnlocked("MEM_03"));
        }

        [Test] public void DepthMilestonesUnlockInOrder()
        {
            var p = new ChronicleProgress();
            p.RegisterFloorReached(ChronicleHero.Vanguard, 8); Assert.AreEqual(0, p.TotalUnlocked);
            p.RegisterFloorReached(ChronicleHero.Vanguard, 9); Assert.IsTrue(p.IsUnlocked("MEM_04"), "chapter II");
            p.RegisterFloorReached(ChronicleHero.Vanguard, 24); Assert.IsTrue(p.IsUnlocked("MEM_10"), "chapter IV");
            p.RegisterFloorReached(ChronicleHero.Vanguard, 30); Assert.IsTrue(p.IsUnlocked("MEM_13"), "chapter V");
            p.RegisterFloorReached(ChronicleHero.Vanguard, 42); Assert.IsTrue(p.IsUnlocked("MEM_19"), "chapter VII");
            p.RegisterFloorReached(ChronicleHero.Vanguard, 48); Assert.IsTrue(p.IsUnlocked("MEM_22"), "chapter VIII");
            Assert.IsFalse(p.IsUnlocked("MEM_25"), "a win is still needed for chapter IX");
        }

        [Test] public void ActBossesUnlockTheirChapters()
        {
            var p = new ChronicleProgress();
            p.RegisterBossDefeated(ChronicleHero.Reaper, 1, 18); Assert.IsTrue(p.IsUnlocked("MEM_09"), "Reaper chapter III");
            p.RegisterBossDefeated(ChronicleHero.Reaper, 2, 36); Assert.IsTrue(p.IsUnlocked("MEM_18"), "Reaper chapter VI");
            Assert.IsFalse(p.IsUnlocked("MEM_07"), "Vanguard is unaffected");
        }

        [Test] public void ReachingAnActCreditsTheBossesBeforeIt()
        {
            var p = new ChronicleProgress();
            p.RegisterRunEnded(ChronicleHero.Vanguard, false, 3, 40, "r1");   // died in Act III
            Assert.IsTrue(p.IsUnlocked("MEM_07"), "Act I boss was necessarily beaten");
            Assert.IsTrue(p.IsUnlocked("MEM_16"), "Act II boss was necessarily beaten");
            Assert.IsFalse(p.IsUnlocked("MEM_25"));
        }

        [Test] public void AWinUnlocksChapterIXForThatHero()
        {
            var p = new ChronicleProgress();
            p.RegisterRunEnded(ChronicleHero.Reaper, true, 3, 54, "w");
            Assert.IsTrue(p.IsUnlocked("MEM_27"));
            Assert.IsFalse(p.IsUnlocked("MEM_25")); Assert.IsFalse(p.IsUnlocked("MEM_26"));
        }

        [Test] public void OneVictoryUnlocksAllNineOfThatHerosMemories()
        {
            foreach (ChronicleHero h in Enum.GetValues(typeof(ChronicleHero)))
            {
                var p = new ChronicleProgress(); p.RegisterRunEnded(h, true, 3, 54, "w");
                Assert.AreEqual(9, ChronicleCatalog.Memories.Count(m => m.hero == h && p.IsUnlocked(m.id)), h.ToString());
            }
        }

        [Test] public void LosingEarlyNeverUnlocksLateChapters()
        {
            var p = new ChronicleProgress();
            p.RegisterRunEnded(ChronicleHero.Vanguard, false, 1, 5, "r");
            Assert.AreEqual(1, p.TotalUnlocked);
        }

        [Test] public void TheSameRunIsNeverCountedTwice()
        {
            var p = new ChronicleProgress();
            p.RegisterRunEnded(ChronicleHero.Vanguard, true, 3, 54, "run-1");
            var again = p.RegisterRunEnded(ChronicleHero.Vanguard, true, 3, 54, "run-1");
            Assert.IsFalse(again.Any);
            Assert.AreEqual(1, p.runsEnded[0]); Assert.AreEqual(1, p.wins[0]);
        }

        [Test] public void RepeatedMilestonesDoNotDuplicateUnlocksOrNotices()
        {
            var p = new ChronicleProgress();
            p.RegisterBossDefeated(ChronicleHero.Vanguard, 1, 18);
            p.RegisterBossDefeated(ChronicleHero.Vanguard, 1, 18); p.RegisterFloorReached(ChronicleHero.Vanguard, 18); p.RefreshUnlocks();
            Assert.AreEqual(p.unlockedMemories.Count, p.unlockedMemories.Distinct().Count());
            Assert.AreEqual(p.pendingNotices.Count, p.pendingNotices.Distinct().Count());
        }

        [Test] public void EventsAreRecordedButNeverGateAnything()
        {
            var p = new ChronicleProgress();
            p.RegisterEventEncountered("fractured_altar"); p.RegisterEventEncountered("fractured_altar");
            Assert.AreEqual(1, p.eventsSeen.Count); Assert.AreEqual(0, p.TotalUnlocked);
        }

        // ---------------------------------------------------------------- Chapter X
        [Test] public void ChapterXStaysLockedUntilAllTwentySevenAreCollected()
        {
            var p = new ChronicleProgress();
            p.RegisterRunEnded(ChronicleHero.Vanguard, true, 3, 54, "a"); p.RegisterRunEnded(ChronicleHero.Hexer, true, 3, 54, "b");
            Assert.AreEqual(18, p.TotalUnlocked); Assert.IsFalse(p.secretUnlocked);
            var last = p.RegisterRunEnded(ChronicleHero.Reaper, true, 3, 54, "c");
            Assert.AreEqual(27, p.TotalUnlocked); Assert.IsTrue(p.secretUnlocked); Assert.IsTrue(last.secretUnlocked);
        }

        [Test] public void ChapterXIsAnnouncedExactlyOnce()
        {
            var p = WinWithEveryone();
            Assert.AreEqual(1, p.pendingNotices.Count(n => n == ChronicleCatalog.SecretChapterId));
            p.RefreshUnlocks(); p.RefreshUnlocks();
            Assert.AreEqual(1, p.pendingNotices.Count(n => n == ChronicleCatalog.SecretChapterId));
            Assert.AreEqual(28, p.TakePendingNotices().Count); Assert.AreEqual(0, p.pendingNotices.Count);
        }

        [Test] public void EveryMemoryIsReachableByWinningWithAllThreeHeroes()
        {
            var p = WinWithEveryone();
            foreach (var m in ChronicleCatalog.Memories) Assert.IsTrue(p.IsUnlocked(m.id), m.id);
        }

        // ---------------------------------------------------------------- commits, facts, corrections
        [Test] public void ViewingAMemoryRecordsItsFactsAndCorrectionsOnce()
        {
            var p = WinWithEveryone();
            var first = p.CommitMemoryViewed("MEM_07");
            Assert.IsTrue(first.accepted); Assert.IsTrue(first.firstView);
            Assert.IsTrue(p.HasCorrection("CORR_01")); Assert.IsTrue(p.HasFact("FACT_FOURTH_COMPANION"));
            var second = p.CommitMemoryViewed("MEM_07");
            Assert.IsTrue(second.accepted); Assert.IsFalse(second.firstView);
            Assert.AreEqual(0, second.newCorrections.Count); Assert.AreEqual(0, second.newFacts.Count);
            Assert.AreEqual(1, p.corrections.Count(c => c == "CORR_01")); Assert.AreEqual(1, p.viewedMemories.Count(m => m == "MEM_07"));
        }

        [Test] public void LockedMemoriesCannotBeCommitted()
        {
            var p = new ChronicleProgress();
            var r = p.CommitMemoryViewed("MEM_07");
            Assert.IsFalse(r.accepted); Assert.AreEqual(0, p.TotalViewed); Assert.IsFalse(p.HasCorrection("CORR_01"));
            Assert.IsTrue(p.CommitMemoryViewed("MEM_07", true).accepted, "the developer sandbox may override");
        }

        [Test] public void AnyChapterThreeMemoryCommitsTheFirstCorrectionButOnlyOnce()
        {
            var p = WinWithEveryone();
            p.CommitMemoryViewed("MEM_09"); Assert.IsTrue(p.HasCorrection("CORR_01"));
            p.CommitMemoryViewed("MEM_07"); p.CommitMemoryViewed("MEM_08");
            Assert.AreEqual(1, p.corrections.Count(c => c == "CORR_01"));
        }

        [Test] public void CorrectionsKeepTheirDiscoveryOrderAndSurviveAnyOrder()
        {
            var p = WinWithEveryone();
            p.CommitMemoryViewed("MEM_24"); p.CommitMemoryViewed("MEM_05"); p.CommitMemoryViewed("MEM_17");
            Assert.AreEqual("CORR_08,CORR_02,CORR_06", string.Join(",", p.corrections));
        }

        [Test] public void ChapterXCompletionRecordsTheReveal()
        {
            var p = WinWithEveryone();
            var r = p.CommitSecretCompleted();
            Assert.IsTrue(r.accepted); Assert.IsTrue(p.secretCompleted); Assert.IsTrue(p.HasCorrection("CORR_10")); Assert.IsTrue(p.HasFact("FACT_NARRATOR_IS_OBSERVER"));
            Assert.IsFalse(p.CommitSecretCompleted().firstView, "replays grant nothing new");
            Assert.IsFalse(new ChronicleProgress().CommitSecretCompleted().accepted, "locked chapter refuses");
        }

        [Test] public void NarratorStateFollowsTheCorrectionsUncovered()
        {
            var p = WinWithEveryone();
            Assert.AreEqual(ChronicleNarratorState.ConfidentHistorian, p.NarratorState);
            foreach (var m in new[] { "MEM_05", "MEM_06", "MEM_07" }) p.CommitMemoryViewed(m);
            Assert.AreEqual(ChronicleNarratorState.DoubtingHistorian, p.NarratorState);
            foreach (var m in new[] { "MEM_10", "MEM_14", "MEM_17" }) p.CommitMemoryViewed(m);
            Assert.AreEqual(ChronicleNarratorState.RememberingObserver, p.NarratorState);
        }

        // ---------------------------------------------------------------- persistence
        [Test] public void ProgressSurvivesASaveAndLoadRoundTrip()
        {
            var p = WinWithEveryone();
            p.CommitMemoryViewed("MEM_07"); p.CommitMemoryViewed("MEM_17"); p.CommitSecretCompleted(); p.openingViews = 3; p.openingMode = 1;
            var copy = FromJson(ToJson(p)); copy.Ensure();
            Assert.AreEqual(p.unlockedMemories.Count, copy.unlockedMemories.Count);
            Assert.AreEqual(string.Join(",", p.corrections), string.Join(",", copy.corrections));
            Assert.AreEqual(string.Join(",", p.viewedMemories), string.Join(",", copy.viewedMemories));
            Assert.IsTrue(copy.HasCorrection("CORR_01") && copy.HasCorrection("CORR_06") && copy.HasCorrection("CORR_10"));
            Assert.IsTrue(copy.secretUnlocked && copy.secretCompleted);
            Assert.AreEqual(3, copy.openingViews); Assert.AreEqual(1, copy.openingMode);
            Assert.AreEqual(54, copy.bestFloor[0]);
        }

        [Test] public void AMissingChronicleSectionLoadsAsAFreshChronicle()
        {
            var copy = FromJson("{}"); copy.Ensure();
            Assert.AreEqual(0, copy.TotalUnlocked); Assert.AreEqual(3, copy.runsEnded.Length); Assert.IsFalse(copy.secretUnlocked);
        }

        [Test] public void NullAndShortFieldsAreRepairedOnLoad()
        {
            var p = new ChronicleProgress { unlockedMemories = null, corrections = null, runsEnded = new int[1], bestFloor = null };
            p.Ensure();
            Assert.IsNotNull(p.unlockedMemories); Assert.IsNotNull(p.corrections); Assert.AreEqual(3, p.runsEnded.Length); Assert.AreEqual(3, p.bestFloor.Length);
            Assert.IsFalse(p.RegisterFloorReached(ChronicleHero.Vanguard, 5).Any);
        }

        // ---------------------------------------------------------------- legacy profiles
        [Test] public void ExistingWinsAreMigratedSoVeteransKeepTheirProgress()
        {
            var p = new ChronicleProgress();
            var history = new List<ChronicleLegacyRun> { new ChronicleLegacyRun { hero = 0, act = 2, floor = 25, victory = false } };
            var r = p.SeedFromLegacy(history, new[] { 0, 2, 0 });
            Assert.IsTrue(p.IsUnlocked("MEM_01"), "ended a run"); Assert.IsTrue(p.IsUnlocked("MEM_07"), "act I boss credited");
            Assert.IsTrue(p.IsUnlocked("MEM_10"), "reached floor 24");
            Assert.IsTrue(p.IsUnlocked("MEM_26"), "two Hexer wins");
            Assert.AreEqual(9, ChronicleCatalog.Memories.Count(m => m.hero == ChronicleHero.Hexer && p.IsUnlocked(m.id)));
            Assert.IsFalse(p.IsUnlocked("MEM_25"), "Vanguard has not won");
            Assert.IsTrue(r.Any);
        }

        [Test] public void MigrationRunsOnlyOnce()
        {
            var p = new ChronicleProgress();
            p.SeedFromLegacy(null, new[] { 1, 0, 0 });
            var before = p.unlockedMemories.Count;
            var again = p.SeedFromLegacy(null, new[] { 5, 5, 5 });
            Assert.IsFalse(again.Any); Assert.AreEqual(before, p.unlockedMemories.Count);
        }

        // ---------------------------------------------------------------- opening preference
        [Test] public void OpeningModeChoosesFullShortOrNone()
        {
            var p = new ChronicleProgress();
            Assert.AreEqual(1, p.OpeningToPlay, "default plays the full opening at every launch");
            p.CommitOpeningViewed(); Assert.AreEqual(1, p.OpeningToPlay);
            p.openingMode = 1; Assert.AreEqual(2, p.OpeningToPlay, "short after the first viewing");
            p.openingMode = 2; Assert.AreEqual(0, p.OpeningToPlay);
            p.ResetAll(); Assert.AreEqual(2, p.openingMode, "resetting progress keeps the player's preference");
        }
    }
}
