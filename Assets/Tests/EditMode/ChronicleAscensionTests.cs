using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>The Ten Recollections: ordered Fate Debt revelations per hero, the new Chapter X rule, saves and preservation.</summary>
    [TestFixture]
    public class ChronicleAscensionTests
    {
        static readonly ChronicleHero[] Heroes = { ChronicleHero.Vanguard, ChronicleHero.Hexer, ChronicleHero.Reaper };
        static int runCounter;
        static string Run() => "run-" + (++runCounter);
        static ChronicleAscensionResult Win(ChronicleProgress p, ChronicleHero hero, int fateDebt, string runId = null) => p.RegisterAscensionVictory(hero, fateDebt, runId ?? Run());

        static ChronicleProgress NormalWinsForEveryone()
        {
            var p = new ChronicleProgress();
            foreach (var h in Heroes) p.RegisterRunEnded(h, true, 3, 54, "normal-" + h);
            return p;
        }

        // ---------------------------------------------------------------- catalog shape
        [Test] public void ThirtySlotsTwelveMajorEighteenSmallTenPerHero()
        {
            Assert.AreEqual(30, ChronicleAscensionCatalog.All.Length);
            Assert.AreEqual(12, ChronicleAscensionCatalog.All.Count(d => d.IsMajor));
            Assert.AreEqual(18, ChronicleAscensionCatalog.All.Count(d => !d.IsMajor));
            foreach (var h in Heroes)
            {
                var chain = ChronicleAscensionCatalog.For(h);
                Assert.AreEqual(10, chain.Count);
                Assert.AreEqual("1,4,7,10", string.Join(",", chain.Where(d => d.IsMajor).Select(d => d.tier)));
                Assert.AreEqual("2,3,5,6,8,9", string.Join(",", chain.Where(d => !d.IsMajor).Select(d => d.tier)));
                Assert.IsTrue(chain[9].IsTrueHistory); Assert.AreEqual(4, chain[9].kindOrdinal);
            }
            Assert.AreEqual("ASC_VAN_01", ChronicleAscensionCatalog.At(ChronicleHero.Vanguard, 1).id);
            Assert.AreEqual("ASC_REA_10", ChronicleAscensionCatalog.At(ChronicleHero.Reaper, 10).id);
        }

        [Test] public void TheCatalogValidatesAndNeverCollidesWithTheOriginalChronicle()
        {
            var errors = new List<string>(); ChronicleAscensionCatalog.Validate(errors);
            Assert.AreEqual(0, errors.Count, string.Join("; ", errors));
            Assert.AreEqual(0, ChronicleValidator.Validate().Count, string.Join("; ", ChronicleValidator.Validate()));
            foreach (var d in ChronicleAscensionCatalog.All)
            {
                Assert.IsNull(ChronicleCatalog.FindMemory(d.id)); Assert.IsNull(ChronicleCatalog.FindCorrection(d.correctionId));
                Assert.IsNull(ChronicleCatalog.FindCorrection(d.id));
            }
            Assert.AreEqual(27, ChronicleCatalog.Memories.Length, "the 27 original memories are unchanged");
            Assert.AreEqual(10, ChronicleCatalog.Corrections.Length, "the ten shared corrections are unchanged");
        }

        // ---------------------------------------------------------------- 1. Normal victory
        [Test] public void ANormalVictoryKeepsTheOriginalStoryAndEarnsNoRecollection()
        {
            var with = new ChronicleProgress(); var without = new ChronicleProgress();
            with.RegisterRunEnded(ChronicleHero.Vanguard, true, 3, 54, "n"); without.RegisterRunEnded(ChronicleHero.Vanguard, true, 3, 54, "n");
            var r = Win(with, ChronicleHero.Vanguard, 0);
            Assert.IsFalse(r.Earned); Assert.AreEqual(0, with.RevelationsEarned(ChronicleHero.Vanguard));
            Assert.AreEqual(string.Join(",", without.unlockedMemories), string.Join(",", with.unlockedMemories), "the original unlocks are identical");
            Assert.AreEqual(9, ChronicleCatalog.Memories.Count(m => m.hero == ChronicleHero.Vanguard && with.IsUnlocked(m.id)));
            Assert.AreEqual(1, with.NextRequiredFateDebt(ChronicleHero.Vanguard));
        }

        // ---------------------------------------------------------------- 2-6. the exact order
        [Test] public void FateDebtOneEarnsTheFirstMajorCorrection()
        {
            var p = new ChronicleProgress(); var r = Win(p, ChronicleHero.Vanguard, 1);
            Assert.IsTrue(r.Earned); Assert.AreEqual("ASC_VAN_01", r.earned.id);
            Assert.AreEqual(ChronicleRevelationKind.MajorCorrection, r.earned.kind); Assert.AreEqual(1, r.earned.kindOrdinal);
        }

        [Test] public void FateDebtTwoEarnsOnlyTheFirstSmallRevelation()
        {
            var p = new ChronicleProgress(); Win(p, ChronicleHero.Hexer, 1);
            var r = Win(p, ChronicleHero.Hexer, 2);
            Assert.AreEqual("ASC_HEX_02", r.earned.id); Assert.AreEqual(ChronicleRevelationKind.SmallRevelation, r.earned.kind); Assert.AreEqual(1, r.earned.kindOrdinal);
            Assert.AreEqual(2, p.RevelationsEarned(ChronicleHero.Hexer)); Assert.IsFalse(p.IsRevelationEarned("ASC_HEX_03"));
        }

        [Test] public void MajorCorrectionsArriveAtFourSevenAndTen()
        {
            foreach (var h in Heroes)
            {
                var p = new ChronicleProgress(); var kinds = new List<string>();
                for (var tier = 1; tier <= 10; tier++)
                {
                    var r = Win(p, h, tier); Assert.IsTrue(r.Earned, h + " Fate Debt " + tier);
                    Assert.AreEqual(tier, r.earned.tier);
                    kinds.Add((r.earned.IsMajor ? "M" : "s") + r.earned.kindOrdinal);
                }
                Assert.AreEqual("M1,s1,s2,M2,s3,s4,M3,s5,s6,M4", string.Join(",", kinds));
                Assert.IsTrue(p.TrueHistoryComplete(h)); Assert.AreEqual(0, p.NextRequiredFateDebt(h)); Assert.IsNull(p.NextRevelation(h));
                Assert.IsTrue(p.HasWonFateDebtX(h));
            }
        }

        [Test] public void FateDebtTenEarnsTheTrueHistory()
        {
            var p = ChronicleAscensionTestKit.ClearThrough(new ChronicleProgress(), ChronicleHero.Reaper, 9);
            Assert.IsFalse(p.TrueHistoryComplete(ChronicleHero.Reaper));
            var r = Win(p, ChronicleHero.Reaper, 10);
            Assert.AreEqual("ASC_REA_10", r.earned.id); Assert.IsTrue(r.earned.IsTrueHistory); Assert.IsTrue(r.earned.IsMajor);
            Assert.IsTrue(p.TrueHistoryComplete(ChronicleHero.Reaper)); Assert.IsFalse(p.secretUnlocked, "one True History is not Chapter X");
        }

        // ---------------------------------------------------------------- 7-10. restrictions
        [Test] public void OneVictoryNeverEarnsMoreThanOne()
        {
            var p = new ChronicleProgress();
            for (var tier = 1; tier <= 10; tier++)
            {
                var before = p.ascension.earned.Count; Win(p, ChronicleHero.Vanguard, tier);
                Assert.AreEqual(before + 1, p.ascension.earned.Count);
            }
            Assert.IsFalse(Win(p, ChronicleHero.Vanguard, 10).Earned, "nothing after the True History");
            Assert.AreEqual(10, p.ascension.earned.Count);
        }

        [Test] public void RepeatingACompletedLevelGrantsNothing()
        {
            var p = new ChronicleProgress(); Win(p, ChronicleHero.Vanguard, 1); Win(p, ChronicleHero.Vanguard, 2);
            for (var i = 0; i < 5; i++) { Assert.IsFalse(Win(p, ChronicleHero.Vanguard, 1).Earned); Assert.IsFalse(Win(p, ChronicleHero.Vanguard, 2).Earned); }
            Assert.AreEqual(2, p.RevelationsEarned(ChronicleHero.Vanguard)); Assert.AreEqual(2, p.ascension.pending.Count);
            Assert.AreEqual(1, p.ascension.earned.Count(id => id == "ASC_VAN_01"));
        }

        [Test] public void ARepeatedVictoryCallbackForOneRunIsIgnored()
        {
            var p = new ChronicleProgress();
            Assert.IsTrue(Win(p, ChronicleHero.Hexer, 1, "same-run").Earned);
            var again = Win(p, ChronicleHero.Hexer, 1, "same-run");
            Assert.IsTrue(again.duplicate); Assert.IsFalse(again.Earned);
            Assert.IsTrue(Win(p, ChronicleHero.Hexer, 2, "next-run").Earned);
            Assert.IsTrue(Win(p, ChronicleHero.Hexer, 3, "next-run").duplicate, "a run id can only ever be credited once");
            Assert.AreEqual(2, p.RevelationsEarned(ChronicleHero.Hexer));
        }

        [Test] public void AHigherLevelVictoryCannotSkipEarlierRecollections()
        {
            var p = new ChronicleProgress();
            foreach (var tier in new[] { 10, 5, 2, 4 }) Assert.IsFalse(Win(p, ChronicleHero.Vanguard, tier).Earned, "Fate Debt " + tier);
            Assert.AreEqual(0, p.RevelationsEarned(ChronicleHero.Vanguard)); Assert.AreEqual(1, p.NextRequiredFateDebt(ChronicleHero.Vanguard));
            Assert.AreEqual("ASC_VAN_01", Win(p, ChronicleHero.Vanguard, 1).earned.id);
            Assert.IsFalse(Win(p, ChronicleHero.Vanguard, 3).Earned); Assert.AreEqual(2, p.NextRequiredFateDebt(ChronicleHero.Vanguard));
        }

        [Test] public void DefeatsAbandonsAndNonQualifyingRunsNeverAdvance()
        {
            var p = new ChronicleProgress(); Win(p, ChronicleHero.Reaper, 1); Win(p, ChronicleHero.Reaper, 2);
            // A defeat after completing a level goes through the original run-end hook only.
            p.RegisterRunEnded(ChronicleHero.Reaper, false, 3, 50, "lost"); p.RegisterBossDefeated(ChronicleHero.Reaper, 3, 54);
            Assert.AreEqual(2, p.RevelationsEarned(ChronicleHero.Reaper)); Assert.AreEqual(3, p.NextRequiredFateDebt(ChronicleHero.Reaper));
            // The policy the game uses before calling the hook.
            Assert.IsTrue(ChronicleAscension.IsQualifyingVictory(true, false, false, 3));
            Assert.IsFalse(ChronicleAscension.IsQualifyingVictory(false, false, false, 3), "defeat");
            Assert.IsFalse(ChronicleAscension.IsQualifyingVictory(true, true, false, 3), "sandbox / Playground run");
            Assert.IsFalse(ChronicleAscension.IsQualifyingVictory(true, false, true, 3), "Daily Run");
            Assert.IsFalse(ChronicleAscension.IsQualifyingVictory(true, false, false, 0), "Normal");
            Assert.IsFalse(ChronicleAscension.IsQualifyingVictory(true, false, false, 11), "out of range");
            Assert.IsFalse(Win(p, ChronicleHero.Reaper, -1).Earned); Assert.IsFalse(Win(p, ChronicleHero.Reaper, 11).Earned);
            Assert.IsFalse(p.RegisterAscensionVictory((ChronicleHero)7, 3, "bad-hero").Earned);
        }

        // ---------------------------------------------------------------- 11. independent heroes
        [Test] public void EachHeroKeepsTheirOwnHistory()
        {
            var p = new ChronicleProgress();
            ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Vanguard, 7);
            ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Hexer, 3);
            ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Reaper, 1);
            Assert.AreEqual(7, p.RevelationsEarned(ChronicleHero.Vanguard)); Assert.AreEqual(3, p.RevelationsEarned(ChronicleHero.Hexer)); Assert.AreEqual(1, p.RevelationsEarned(ChronicleHero.Reaper));
            Assert.AreEqual(8, p.NextRequiredFateDebt(ChronicleHero.Vanguard)); Assert.AreEqual(4, p.NextRequiredFateDebt(ChronicleHero.Hexer)); Assert.AreEqual(2, p.NextRequiredFateDebt(ChronicleHero.Reaper));
            Assert.IsFalse(Win(p, ChronicleHero.Reaper, 8).Earned, "Vanguard's progress never carries to the Reaper");
            Assert.IsTrue(p.ascension.earned.All(id => ChronicleAscensionCatalog.Find(id) != null));
            Assert.IsTrue(ChronicleAscensionCatalog.For(ChronicleHero.Hexer).Skip(3).All(d => !p.IsRevelationEarned(d.id)));
        }

        [Test] public void TheWinningHeroAloneReceivesCredit()
        {
            var p = new ChronicleProgress(); var r = Win(p, ChronicleHero.Hexer, 1);
            Assert.AreEqual(ChronicleHero.Hexer, r.earned.hero);
            Assert.AreEqual(0, p.RevelationsEarned(ChronicleHero.Vanguard)); Assert.AreEqual(0, p.RevelationsEarned(ChronicleHero.Reaper));
        }

        // ---------------------------------------------------------------- 12. saving and loading
        [Test] public void RecollectionsSurviveASaveAndLoad()
        {
            var p = new ChronicleProgress();
            ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Vanguard, 7); ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Hexer, 3);
            p.CommitRevelationWatched("ASC_VAN_01"); p.MarkRevelationPresented("ASC_HEX_01"); p.CommitAscensionCorrection("ASC_CORR_VAN_1");
            var copy = ChronicleProgressTests.FromJson(ChronicleProgressTests.ToJson(p)); copy.Ensure();
            Assert.AreEqual(ChronicleProgress.CurrentVersion, copy.version);
            Assert.AreEqual(string.Join(",", p.ascension.earned), string.Join(",", copy.ascension.earned));
            Assert.AreEqual(7, copy.RevelationsEarned(ChronicleHero.Vanguard)); Assert.AreEqual(3, copy.RevelationsEarned(ChronicleHero.Hexer)); Assert.AreEqual(0, copy.RevelationsEarned(ChronicleHero.Reaper));
            Assert.IsTrue(copy.IsRevelationWatched("ASC_VAN_01")); Assert.IsTrue(copy.IsRevelationPresented("ASC_HEX_01")); Assert.IsTrue(copy.HasAscensionCorrection("ASC_CORR_VAN_1"));
            Assert.AreEqual(string.Join(",", p.ascension.pending), string.Join(",", copy.ascension.pending));
            Assert.AreEqual(8, copy.NextRequiredFateDebt(ChronicleHero.Vanguard));
            Assert.IsTrue(Win(copy, ChronicleHero.Vanguard, 8).Earned, "progress continues after reloading");
        }

        [Test] public void AProfileWithoutTheNewSectionLoadsWithNoRecollections()
        {
            var copy = ChronicleProgressTests.FromJson("{\"version\":1,\"unlockedMemories\":[\"MEM_01\"]}"); copy.Ensure();
            Assert.IsNotNull(copy.ascension); Assert.AreEqual(0, copy.ascension.earned.Count); Assert.AreEqual(3, copy.ascension.fateDebtXWins.Length);
            Assert.IsTrue(copy.IsUnlocked("MEM_01")); Assert.AreEqual(1, copy.NextRequiredFateDebt(ChronicleHero.Hexer));
        }

        [Test] public void DamagedRecollectionDataIsRepairedWithoutLosingProgress()
        {
            var p = new ChronicleProgress { ascension = null }; p.Ensure();
            Assert.IsNotNull(p.ascension);
            p.ascension.earned = new List<string> { "ASC_VAN_01", "ASC_VAN_01", null, "", "ASC_FUTURE_99" };
            p.ascension.pending = new List<string> { "ASC_VAN_02" }; p.ascension.fateDebtXWins = new int[1];
            p.Ensure();
            Assert.AreEqual(1, p.ascension.earned.Count(id => id == "ASC_VAN_01")); Assert.IsTrue(p.ascension.earned.Contains("ASC_FUTURE_99"), "unknown ids from a newer build are kept");
            Assert.AreEqual(0, p.ascension.pending.Count, "pending never holds an unearned revelation"); Assert.AreEqual(3, p.ascension.fateDebtXWins.Length);
            Assert.AreEqual(1, p.RevelationsEarned(ChronicleHero.Vanguard)); Assert.AreEqual(2, p.NextRequiredFateDebt(ChronicleHero.Vanguard));
        }

        [Test] public void AGapInSavedDataStillEnforcesTheOrder()
        {
            var p = new ChronicleProgress(); p.ascension.earned.Add("ASC_HEX_03"); p.Ensure();
            Assert.AreEqual(1, p.NextRequiredFateDebt(ChronicleHero.Hexer));
            Assert.AreEqual("ASC_HEX_01", Win(p, ChronicleHero.Hexer, 1).earned.id);
            Assert.AreEqual("ASC_HEX_02", Win(p, ChronicleHero.Hexer, 2).earned.id);
            Assert.AreEqual(4, p.NextRequiredFateDebt(ChronicleHero.Hexer));
        }

        // ---------------------------------------------------------------- 13-14. existing saves
        static string AllMemoryIdsJson() => string.Join(",", ChronicleCatalog.Memories.Select(m => "\"" + m.id + "\""));

        [Test] public void AVersionOneVeteranMigratesWithoutLosingAnything()
        {
            var json = "{\"version\":1,\"unlockedMemories\":[" + AllMemoryIdsJson() + "],\"viewedMemories\":[\"MEM_07\",\"MEM_17\"],"
                + "\"discoveredFacts\":[\"FACT_FOURTH_COMPANION\",\"FACT_SEVERING_CAUSE\"],\"corrections\":[\"CORR_01\",\"CORR_06\"],\"pendingNotices\":[],"
                + "\"countedRunIds\":[\"old-run\"],\"eventsSeen\":[\"fractured_altar\"],\"runsEnded\":[6,4,3],\"wins\":[3,1,1],\"bestFloor\":[54,54,54],\"actBossMask\":[7,7,7],"
                + "\"secretUnlocked\":true,\"secretCompleted\":false,\"legacyMigrated\":true,\"openingViews\":4,\"openingMode\":1,\"lastChapterOpened\":3,\"instantText\":true,\"textSpeed\":1.25}";
            var p = ChronicleProgressTests.FromJson(json); p.Ensure();
            Assert.AreEqual(ChronicleProgress.CurrentVersion, p.version);
            Assert.AreEqual(27, p.TotalUnlocked); Assert.AreEqual("MEM_07,MEM_17", string.Join(",", p.viewedMemories));
            Assert.AreEqual("CORR_01,CORR_06", string.Join(",", p.corrections)); Assert.IsTrue(p.HasFact("FACT_SEVERING_CAUSE"));
            Assert.AreEqual(3, p.wins[0]); Assert.AreEqual(54, p.bestFloor[2]); Assert.IsTrue(p.legacyMigrated);
            Assert.AreEqual(4, p.openingViews); Assert.AreEqual(1, p.openingMode); Assert.IsTrue(p.instantText); Assert.AreEqual(1.25, p.textSpeed, .001);
            Assert.AreEqual(0, p.ascension.earned.Count, "no recollection is fabricated from old wins");
            foreach (var h in Heroes) Assert.AreEqual(1, p.NextRequiredFateDebt(h), "veterans begin the recollections at Fate Debt I");
            // 14. Chapter X access that already existed is kept.
            Assert.IsTrue(p.secretUnlocked); Assert.IsTrue(p.ascension.secretGrandfathered);
            p.RefreshUnlocks(); p.RegisterRunEnded(ChronicleHero.Vanguard, false, 1, 3, "after-update");
            Assert.IsTrue(p.secretUnlocked, "re-evaluating the new rule never revokes it");
            Assert.AreEqual(ChronicleArchiveAction.BeginSecret, new ChronicleArchiveController(p, 10).Accept().action);
            Assert.IsTrue(p.CommitSecretCompleted().accepted);
            var reloaded = ChronicleProgressTests.FromJson(ChronicleProgressTests.ToJson(p)); reloaded.Ensure();
            Assert.IsTrue(reloaded.secretUnlocked && reloaded.secretCompleted && reloaded.ascension.secretGrandfathered);
        }

        [Test] public void AVersionOnePlayerWithoutChapterXMustEarnItUnderTheNewRule()
        {
            var json = "{\"version\":1,\"unlockedMemories\":[" + AllMemoryIdsJson() + "],\"wins\":[1,1,1],\"runsEnded\":[1,1,1],\"bestFloor\":[54,54,54],\"actBossMask\":[7,7,7],\"secretUnlocked\":false,\"legacyMigrated\":true}";
            var p = ChronicleProgressTests.FromJson(json); p.Ensure(); p.RefreshUnlocks();
            Assert.IsFalse(p.secretUnlocked); Assert.IsFalse(p.ascension.secretGrandfathered);
            ChronicleAscensionTestKit.CompleteEveryTrueHistory(p);
            Assert.IsTrue(p.secretUnlocked);
        }

        [Test] public void MigrationIsDeterministicAndRepeatable()
        {
            var json = "{\"version\":1,\"secretUnlocked\":true,\"unlockedMemories\":[\"MEM_02\"]}";
            var a = ChronicleProgressTests.FromJson(json); a.Ensure(); a.Ensure();
            var b = ChronicleProgressTests.FromJson(json); b.Ensure();
            Assert.AreEqual(ChronicleProgressTests.ToJson(a), ChronicleProgressTests.ToJson(b));
            var again = ChronicleProgressTests.FromJson(ChronicleProgressTests.ToJson(a)); again.Ensure();
            Assert.AreEqual(ChronicleProgressTests.ToJson(a), ChronicleProgressTests.ToJson(again));
        }

        [Test] public void LegacyRunHistoryAndAdminUnlocksNeverAwardRecollections()
        {
            var p = new ChronicleProgress();
            p.SeedFromLegacy(new[] { new ChronicleLegacyRun { hero = 0, act = 3, floor = 54, victory = true } }, new[] { 9, 9, 9 });
            p.UnlockAllForDevelopment();
            Assert.AreEqual(0, p.ascension.earned.Count); Assert.IsFalse(p.AllTrueHistoriesComplete);
            Assert.IsTrue(p.secretUnlocked, "the existing admin unlock still opens Chapter X for testing");
        }

        // ---------------------------------------------------------------- 15-16. Chapter X
        [Test] public void ChapterXNeedsAllThreeTrueHistories()
        {
            foreach (var missing in Heroes)
            {
                var p = NormalWinsForEveryone();
                foreach (var h in Heroes) ChronicleAscensionTestKit.ClearThrough(p, h, h == missing ? 9 : 10);
                Assert.IsFalse(p.secretUnlocked, "missing " + missing);
                for (var i = 0; i < 3; i++) Win(p, missing, 9);
                Assert.IsFalse(p.secretUnlocked, "repeating Fate Debt IX is not Fate Debt X");
                Assert.IsTrue(Win(p, missing, 10).secretUnlocked); Assert.IsTrue(p.secretUnlocked); Assert.IsFalse(p.secretCompleted);
            }
        }

        [Test] public void ReachingFateDebtTenWithoutWinningIsNotEnough()
        {
            var p = NormalWinsForEveryone();
            foreach (var h in Heroes) ChronicleAscensionTestKit.ClearThrough(p, h, 9);
            foreach (var h in Heroes) { p.RegisterRunEnded(h, false, 3, 53, "lost-x-" + h); p.RegisterBossDefeated(h, 2, 36); }
            Assert.IsFalse(p.secretUnlocked); foreach (var h in Heroes) Assert.IsFalse(p.HasWonFateDebtX(h));
        }

        [Test] public void ChapterXUnlocksOnlyOnce()
        {
            var p = ChronicleAscensionTestKit.CompleteEveryTrueHistory(NormalWinsForEveryone());
            Assert.AreEqual(1, p.pendingNotices.Count(n => n == ChronicleCatalog.SecretChapterId));
            foreach (var h in Heroes) { Assert.IsFalse(Win(p, h, 10).secretUnlocked); p.RegisterRunEnded(h, true, 3, 54, Run()); }
            p.RefreshUnlocks();
            Assert.AreEqual(1, p.pendingNotices.Count(n => n == ChronicleCatalog.SecretChapterId));
            foreach (var h in Heroes) Assert.AreEqual(2, p.ascension.fateDebtXWins[(int)h], "each genuine Fate Debt X victory is still recorded");
        }

        [Test] public void ChapterXKeepsItsNormalCompletionAndReplayBehaviour()
        {
            var p = ChronicleAscensionTestKit.CompleteEveryTrueHistory(NormalWinsForEveryone());
            var first = p.CommitSecretCompleted(); Assert.IsTrue(first.accepted && first.firstView && first.secretCompleted);
            Assert.IsTrue(p.HasCorrection("CORR_10"));
            Assert.IsFalse(p.CommitSecretCompleted().firstView); Assert.AreEqual(1, p.corrections.Count(c => c == "CORR_10"));
        }

        // ---------------------------------------------------------------- 17. automatic act Chronicles
        [Test] public void AutomaticActChroniclesAreUnchangedByRecollections()
        {
            foreach (var h in Heroes)
                for (var act = 1; act <= 3; act++)
                    foreach (var ended in new[] { false, true })
                    {
                        var plain = new ChronicleProgress(); plain.RegisterBossDefeated(h, act, act * 18);
                        var layered = new ChronicleProgress(); layered.RegisterBossDefeated(h, act, act * 18);
                        ChronicleAscensionTestKit.ClearThrough(layered, h, 10);
                        var a = ChronicleMilestoneScenes.Memories(plain, h, act, ended); var b = ChronicleMilestoneScenes.Memories(layered, h, act, ended);
                        Assert.AreEqual(string.Join(",", a), string.Join(",", b), h + " act " + act);
                        Assert.IsTrue(b.All(id => ChronicleAscensionCatalog.Find(id) == null));
                    }
            var ending = ChronicleMilestoneScenes.Ending(true, ChronicleHero.Vanguard);
            Assert.AreEqual(ChronicleMilestoneScenes.VictoryId, ending.id); Assert.AreEqual("Not yet.", ChronicleMilestoneScenes.EndingResponse(true, 0));
        }

        // ---------------------------------------------------------------- 18. shared corrections
        [Test] public void TheTenSharedCorrectionsAreStillDiscoveredAndNeverReplaced()
        {
            var p = ChronicleAscensionTestKit.CompleteEveryTrueHistory(NormalWinsForEveryone());
            foreach (var c in ChronicleCatalog.Corrections.Where(c => c.id != "CORR_10")) p.CommitMemoryViewed(c.sources[0]);
            p.CommitSecretCompleted();
            Assert.AreEqual(10, p.corrections.Count);
            Assert.AreEqual(string.Join(",", Enumerable.Range(1, 10).Select(ChronicleCatalog.CorrectionKey)), string.Join(",", p.corrections.OrderBy(c => c)));
            Assert.IsFalse(p.CommitCorrection("ASC_CORR_VAN_1"), "new corrections never enter the shared list");
            Assert.IsTrue(p.CommitAscensionCorrection("ASC_CORR_VAN_1"));
            Assert.AreEqual(10, p.corrections.Count); Assert.AreEqual(1, p.ascension.corrections.Count);
            Assert.AreEqual("There were four.", ChronicleCatalog.FindCorrection("CORR_01").corrected);
            Assert.AreEqual("The narrator is the Forgotten Observer.", ChronicleCatalog.FindCorrection("CORR_10").corrected);
        }

        [Test] public void ANewCorrectionWaitsForItsRevelation()
        {
            var p = new ChronicleProgress();
            Assert.IsFalse(p.CommitAscensionCorrection("ASC_CORR_HEX_2"), "Hexer's second major is not earned");
            ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Hexer, 4);
            Assert.IsTrue(p.CommitAscensionCorrection("ASC_CORR_HEX_2")); Assert.IsFalse(p.CommitAscensionCorrection("ASC_CORR_HEX_2"), "idempotent");
            Assert.IsFalse(p.CommitAscensionCorrection("CORR_01")); Assert.IsFalse(p.CommitAscensionCorrection("ASC_HEX_02"));
        }

        // ---------------------------------------------------------------- 19. replay never awards
        [Test] public void ReplayingAndReadingNeverAdvanceRecollections()
        {
            var p = NormalWinsForEveryone(); Win(p, ChronicleHero.Vanguard, 1);
            var before = string.Join(",", p.ascension.earned);
            foreach (var id in new[] { "MEM_07", "MEM_25", ChronicleCatalog.OpeningSceneId })
                foreach (var replay in new[] { false, true })
                {
                    var seq = new ChronicleSequencer(ChronicleScripts.Get(id), new NullChronicleStage(), new ChronicleProgressContext(p), new ChronicleProgressSink(p), null, null, replay);
                    seq.Start(id.StartsWith("SCENE_", StringComparison.Ordinal) ? ChronicleBookPose.Closed : ChronicleBookPose.Open); seq.ResolveInstantly();
                }
            ChronicleArchive.Reading("MEM_25", p); new ChronicleArchiveController(p).OpenReading("MEM_07");
            Assert.AreEqual(before, string.Join(",", p.ascension.earned));
        }

        [Test] public void WatchingARevelationRecordsItButNeverEarnsIt()
        {
            var p = new ChronicleProgress(); var sink = new ChronicleProgressSink(p);
            sink.SceneFinished("ASC_VAN_01", false); sink.SaveCorrection("ASC_CORR_VAN_1", "ASC_VAN_01");
            Assert.IsFalse(p.IsRevelationWatched("ASC_VAN_01")); Assert.IsFalse(p.IsRevelationEarned("ASC_VAN_01")); Assert.IsFalse(p.HasAscensionCorrection("ASC_CORR_VAN_1"));
            Assert.IsFalse(p.MarkRevelationPresented("ASC_VAN_01")); Assert.IsFalse(p.CommitRevelationWatched("ASC_VAN_01"));

            Win(p, ChronicleHero.Vanguard, 1);
            Assert.AreEqual(1, p.PendingRevelations(ChronicleHero.Vanguard).Count);
            sink.SaveCorrection("ASC_CORR_VAN_1", "ASC_VAN_01"); sink.SceneFinished("ASC_VAN_01", true);
            Assert.IsTrue(sink.firstView); Assert.IsTrue(p.IsRevelationWatched("ASC_VAN_01")); Assert.IsTrue(p.IsRevelationPresented("ASC_VAN_01"));
            Assert.IsTrue(p.HasAscensionCorrection("ASC_CORR_VAN_1")); Assert.IsTrue(sink.newCorrections.Contains("ASC_CORR_VAN_1"));
            Assert.AreEqual(0, p.PendingRevelations(ChronicleHero.Vanguard).Count);
            Assert.IsFalse(p.CommitRevelationWatched("ASC_VAN_01"), "a rewatch is not a first view");
            Assert.AreEqual(1, p.RevelationsEarned(ChronicleHero.Vanguard)); Assert.AreEqual(1, p.RevelationsWatched(ChronicleHero.Vanguard));
            Assert.IsFalse(p.IsViewed("ASC_VAN_01"), "recollections never enter the 27-memory lists");
        }

        [Test] public void TheDeveloperSandboxNeverTouchesRealProgress()
        {
            var real = new ChronicleProgress(); var s = new ChronicleDevSession();
            for (var tier = 1; tier <= 10; tier++) s.SimulateAscensionVictory(ChronicleHero.Hexer, tier);
            Assert.AreEqual(10, s.sandbox.RevelationsEarned(ChronicleHero.Hexer)); Assert.AreEqual(0, real.ascension.earned.Count);
            Assert.IsTrue(s.Play("ASC_HEX_01"), "authored recollections preview in the sandbox"); Assert.AreEqual(0, real.ascension.watched.Count);
            s.ResetSandbox(); Assert.AreEqual(0, s.sandbox.RevelationsEarned(ChronicleHero.Hexer));
        }

        [Test] public void ResettingProgressClearsRecollectionsButKeepsPreferences()
        {
            var p = ChronicleAscensionTestKit.CompleteEveryTrueHistory(NormalWinsForEveryone()); p.openingMode = 2; p.textSpeed = 1.5f;
            p.ResetAll(); p.Ensure();
            Assert.AreEqual(0, p.ascension.earned.Count); Assert.IsFalse(p.secretUnlocked); Assert.IsFalse(p.ascension.secretGrandfathered);
            Assert.AreEqual(2, p.openingMode); Assert.AreEqual(1.5, p.textSpeed, .001);
            Assert.AreEqual(1, p.NextRequiredFateDebt(ChronicleHero.Vanguard));
        }

        // ---------------------------------------------------------------- 20. nothing unfinished is shown
        // Prompt 4: all thirty are authored and registered. Presentation is still one at a time and waits for base memories.
        [Test] public void EveryRevelationIsAuthoredAndPresentedOneAtATimeAfterItsBaseMemories()
        {
            foreach (var d in ChronicleAscensionCatalog.All)
            {
                Assert.IsTrue(d.contentReady, d.id); Assert.IsTrue(ChronicleAscensionCatalog.IsPresentable(d), d.id);
                Assert.IsTrue(d.chapter >= 1 && d.chapter <= 9, d.id); Assert.IsFalse(string.IsNullOrEmpty(d.memoryId), d.id); Assert.IsFalse(string.IsNullOrEmpty(d.title), d.id);
                Assert.IsNotNull(ChronicleScripts.Get(d.sceneId), d.id);
            }
            var p = ChronicleAscensionTestKit.CompleteEveryTrueHistory(NormalWinsForEveryone());
            foreach (var h in Heroes)
            {
                Assert.AreEqual(10, p.PendingRevelations(h).Count, "earned and queued");
                Assert.AreEqual(0, ChronicleAscension.ScenesToPresent(p, h).Count, "base memories not watched yet: deferred, not lost");
                Assert.AreEqual(0, p.RevelationsWatched(h));
            }
            foreach (var m in ChronicleCatalog.Memories) p.CommitMemoryViewed(m.id);
            foreach (var h in Heroes)
            {
                var next = ChronicleAscension.ScenesToPresent(p, h);
                Assert.AreEqual(1, next.Count, "exactly one revelation is presented at a time"); Assert.AreEqual(ChronicleAscensionCatalog.At(h, 1).sceneId, next[0]);
            }
            Assert.AreEqual(0, p.ascension.presented.Count);
        }

        // ---------------------------------------------------------------- Archive queries (for Prompt 2)
        [Test] public void TheArchiveCanAskEverythingItNeeds()
        {
            var p = NormalWinsForEveryone();
            ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Vanguard, 10); ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Hexer, 3);
            p.CommitRevelationWatched("ASC_VAN_04");
            var v = ChronicleAscension.View(p);
            var van = v.heroes[0]; var hex = v.heroes[1]; var rea = v.heroes[2];
            Assert.AreEqual(10, van.earned); Assert.AreEqual(1, van.watched); Assert.AreEqual(9, van.pending); Assert.IsTrue(van.trueHistoryComplete); Assert.AreEqual(0, van.nextFateDebt); Assert.AreEqual("", van.nextId);
            Assert.AreEqual(3, hex.earned); Assert.AreEqual(4, hex.nextFateDebt); Assert.AreEqual("ASC_HEX_04", hex.nextId); Assert.AreEqual(ChronicleAscensionCatalog.Find("ASC_HEX_04").chapter, hex.nextChapter, "the chapter that receives the next addition");
            Assert.AreEqual(1, hex.entries.Count(e => e.isNext)); Assert.IsTrue(hex.entries[3].isNext); Assert.IsTrue(hex.entries[2].earned && !hex.entries[3].earned);
            Assert.AreEqual(0, rea.earned); Assert.AreEqual(1, rea.nextFateDebt);
            Assert.IsFalse(v.allTrueHistoriesComplete); Assert.IsFalse(v.chapterXAvailable); Assert.AreEqual(13, v.totalEarned);
            Assert.AreEqual(10, van.entries.Count); Assert.IsTrue(van.entries.All(e => e.contentReady));
        }
    }
}
