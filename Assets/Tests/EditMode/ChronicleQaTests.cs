using System;
using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>The QA build's Chronicle testing logic: preview vs genuine progression, rule-driven setup, instant reset, save fingerprints.</summary>
    [TestFixture]
    public class ChronicleQaTests
    {
        static readonly ChronicleHero[] Heroes = { ChronicleHero.Vanguard, ChronicleHero.Hexer, ChronicleHero.Reaper };
        static ChronicleQaSession Fresh() => new ChronicleQaSession(new ChronicleProgress());

        static void PlayThrough(ChronicleProgress p, string sceneId, bool preview)
        {
            var scene = ChronicleScripts.Get(sceneId); Assert.IsNotNull(scene, sceneId);
            var sink = preview ? null : new ChronicleProgressSink(p, false);
            var seq = new ChronicleSequencer(scene, new NullChronicleStage(), new ChronicleProgressContext(p), sink);
            seq.Start(sceneId.StartsWith("SCENE_", StringComparison.Ordinal) ? ChronicleBookPose.Closed : ChronicleBookPose.Open); seq.ResolveInstantly();
            Assert.IsTrue(seq.Finished, sceneId);
        }

        // ---------------------------------------------------------------- the catalogue
        [Test] public void EverythingPreviewableIsListed()
        {
            var q = Fresh(); var all = q.Entries();
            Assert.AreEqual(27, all.Count(e => e.group == ChronicleQaGroup.Memory));
            Assert.AreEqual(30, all.Count(e => e.group == ChronicleQaGroup.Revelation));
            Assert.AreEqual(12, all.Count(e => e.major)); Assert.AreEqual(12, q.MajorCorrectionCount);
            Assert.AreEqual(2, all.Count(e => e.group == ChronicleQaGroup.Opening));
            Assert.AreEqual(1, all.Count(e => e.group == ChronicleQaGroup.Finale && e.id == ChronicleCatalog.SecretSceneId));
            Assert.AreEqual(all.Count, all.Select(e => e.id).Distinct().Count(), "scene ids are unique");
        }

        [Test] public void MajorCorrectionsAreFateDebtOneFourSevenTenForEveryHero()
        {
            var q = Fresh();
            foreach (var h in Heroes)
            {
                var majors = q.Entries().Where(e => e.major && e.hero == (int)h).Select(e => e.tier).ToArray();
                Assert.AreEqual("1,4,7,10", string.Join(",", majors));
                foreach (var tier in new[] { 1, 4, 7, 10 }) Assert.IsTrue(q.Revelation(h, tier).correctionId.StartsWith("ASC_CORR_", StringComparison.Ordinal));
            }
        }

        [Test] public void AvailabilityFollowsWhatIsActuallyRegistered()
        {
            var q = Fresh();
            foreach (var e in q.Entries())
            {
                Assert.AreEqual(ChronicleScripts.Has(e.id), e.available, e.id);
                if (!e.available) Assert.IsTrue(e.unavailableReason.Length > 0, e.id + " explains why it is missing");
            }
            foreach (var m in ChronicleCatalog.Memories) Assert.IsTrue(q.Find(m.id).available, m.id + " is original content and must always be previewable");
            Assert.IsTrue(q.Find(ChronicleCatalog.SecretSceneId).available);
            Assert.IsTrue(q.Find(ChronicleCatalog.OpeningSceneId).available);
        }

        [Test] public void AnUnavailableSceneIsRefusedWithAReasonRatherThanPlayed()
        {
            var q = Fresh();
            Assert.IsFalse(q.PlanPlay("NO_SUCH_SCENE", out var message)); Assert.IsTrue(message.Length > 0);
            var missing = q.Entries().FirstOrDefault(e => !e.available);
            if (missing != null) { Assert.IsFalse(q.PlanPlay(missing.id, out message)); Assert.IsTrue(message.Contains(missing.id)); }
            Assert.IsTrue(q.PlanPlay("MEM_07", out message));
        }

        // ---------------------------------------------------------------- preview is separate from progression
        [Test] public void PreviewNeverRecordsAndRecordOnlyCountsWhatIsGenuinelyEarned()
        {
            var q = Fresh();
            q.mode = ChronicleQaPlayMode.Preview; Assert.IsTrue(q.PreviewFlag);
            foreach (var e in q.Entries()) Assert.IsFalse(q.WillRecord(e.id), "preview records nothing: " + e.id);

            q.mode = ChronicleQaPlayMode.Record; Assert.IsFalse(q.PreviewFlag);
            Assert.IsFalse(q.WillRecord("MEM_07"), "a locked memory is not recorded");
            Assert.IsFalse(q.WillRecord(ChronicleCatalog.SecretSceneId));
            Assert.IsFalse(q.WillRecord("ASC_VAN_01"), "an unearned recollection is not recorded");
            Assert.IsTrue(q.WillRecord(ChronicleCatalog.OpeningSceneId));
            q.SatisfyMemory("MEM_07"); Assert.IsTrue(q.WillRecord("MEM_07"));
            q.SimulateRun(ChronicleHero.Vanguard, 1, ChronicleQaRun.FateDebtVictory); Assert.IsTrue(q.WillRecord("ASC_VAN_01"));
        }

        [Test] public void PreviewPlaybackLeavesTheProfileByteForByteUntouched()
        {
            var p = new ChronicleProgress(); var q = new ChronicleQaSession(p); q.UnlockAllMemories();
            var before = ChronicleQaSession.Fingerprint(p);
            foreach (var m in ChronicleCatalog.Memories) PlayThrough(p, m.id, true);
            PlayThrough(p, ChronicleCatalog.SecretSceneId, true); PlayThrough(p, ChronicleCatalog.OpeningSceneId, true);
            Assert.AreEqual(before, ChronicleQaSession.Fingerprint(p));
        }

        [Test] public void RecordedPlaybackCommitsLikeTheRealGameAndRefusesLockedScenes()
        {
            var p = new ChronicleProgress(); var q = new ChronicleQaSession(p);
            PlayThrough(p, "MEM_07", false);
            Assert.IsFalse(p.HasCorrection("CORR_01")); Assert.IsFalse(p.IsViewed("MEM_07"), "locked: nothing commits");
            q.SatisfyMemory("MEM_07"); PlayThrough(p, "MEM_07", false);
            Assert.IsTrue(p.HasCorrection("CORR_01")); Assert.IsTrue(p.IsViewed("MEM_07"));
        }

        // ---------------------------------------------------------------- genuine progression
        [Test] public void TheRuleBatteryPasses()
        {
            var checks = ChronicleQaSession.RunRuleChecks();
            Assert.GreaterOrEqual(checks.Count, 15);
            foreach (var c in checks) Assert.IsTrue(c.passed, c.name + " :: " + c.detail);
        }

        [Test] public void OneRevelationPerVictoryAcrossAFullChainForEveryHero()
        {
            foreach (var h in Heroes)
            {
                var q = Fresh();
                for (var tier = 1; tier <= 10; tier++)
                {
                    var before = q.progress.RevelationsEarned(h);
                    var r = q.SimulateRun(h, tier, ChronicleQaRun.FateDebtVictory);
                    Assert.IsTrue(r.earned, h + " " + tier); Assert.AreEqual(ChronicleAscensionCatalog.IdFor(h, tier), r.revelationId);
                    Assert.AreEqual(before + 1, q.progress.RevelationsEarned(h));
                }
                Assert.IsTrue(q.progress.TrueHistoryComplete(h));
            }
        }

        [Test] public void DisqualifiedRunsAndRepeatsEarnNothingButStillCountForTheOriginalStory()
        {
            var q = Fresh(); var h = ChronicleHero.Hexer;
            foreach (var kind in new[] { ChronicleQaRun.Defeat, ChronicleQaRun.NormalVictory, ChronicleQaRun.DailyVictory, ChronicleQaRun.PlaygroundVictory })
                Assert.IsFalse(q.SimulateRun(h, 1, kind).earned, kind.ToString());
            Assert.AreEqual(0, q.progress.RevelationsEarned(h));
            Assert.AreEqual(3, q.progress.runsEnded[(int)h], "defeat, Normal and Daily runs end a run; the Playground never does");
            var first = q.SimulateRun(h, 1, ChronicleQaRun.FateDebtVictory); Assert.IsTrue(first.earned);
            var again = q.SimulateRun(h, 1, ChronicleQaRun.RepeatLastRun);
            Assert.IsFalse(again.earned); Assert.IsTrue(again.duplicate);
            Assert.AreEqual(1, q.progress.RevelationsEarned(h));
        }

        [Test] public void RepeatingWithNoEarlierRunSaysSoAndChangesNothing()
        {
            var q = Fresh(); var before = ChronicleQaSession.Fingerprint(q.progress);
            var r = q.SimulateRun(ChronicleHero.Reaper, 1, ChronicleQaRun.RepeatLastRun);
            Assert.IsFalse(r.earned); Assert.IsTrue(r.summary.Length > 0); Assert.AreEqual(before, ChronicleQaSession.Fingerprint(q.progress));
        }

        [Test] public void AVictoryQueuesExactlyTheEarnedRecollectionForPresentation()
        {
            var q = Fresh(); var h = ChronicleHero.Vanguard;
            q.SimulateRun(h, 1, ChronicleQaRun.FateDebtVictory);
            Assert.AreEqual("ASC_VAN_01", string.Join(",", q.progress.PendingRevelations(h).Select(d => d.id)));
            // What the post-victory flow would show is limited to authored, registered scenes.
            foreach (var id in q.ScenesToPresent(h)) Assert.AreEqual("ASC_VAN_01", ChronicleAscensionCatalog.FindByScene(id).id);
        }

        // ---------------------------------------------------------------- simulated unlock conditions
        [Test] public void EveryMemoryCanBeUnlockedByMeetingItsOwnRule()
        {
            foreach (var m in ChronicleCatalog.Memories)
            {
                var q = Fresh();
                Assert.IsTrue(q.SatisfyMemory(m.id), m.id); Assert.IsTrue(q.progress.IsUnlocked(m.id), m.id);
            }
        }

        [Test] public void PrepareForTierEarnsOnlyTheEarlierOnesAndLeavesTheTargetDue()
        {
            var q = Fresh(); var h = ChronicleHero.Hexer;
            Assert.AreEqual(4, q.PrepareForTier(h, 5));
            Assert.AreEqual(4, q.progress.RevelationsEarned(h)); Assert.AreEqual(5, q.progress.NextRequiredFateDebt(h));
            Assert.AreEqual(0, q.progress.PendingRevelations(h).Count, "earlier ones are treated as already shown");
            Assert.AreEqual(0, q.PrepareForTier(h, 5), "repeating it changes nothing");
            Assert.IsTrue(q.EarnThrough(h, 5));
            Assert.AreEqual("ASC_HEX_05", string.Join(",", q.progress.PendingRevelations(h).Select(d => d.id)), "the target waits to be presented");
            Assert.IsTrue(q.EarnThrough(h, 3), "an earlier tier is already earned, so this is a no-op success");
            Assert.AreEqual(5, q.progress.RevelationsEarned(h));
        }

        [Test] public void ChapterXOpensOnlyThroughTheGenuineTrueHistoryRoute()
        {
            var q = Fresh();
            q.UnlockAllMemories(); Assert.IsFalse(q.progress.secretUnlocked);
            Assert.IsTrue(q.OpenChapterX()); Assert.IsTrue(q.progress.secretUnlocked); Assert.IsFalse(q.progress.secretCompleted, "opening is not completing");
            Assert.IsTrue(q.progress.AllTrueHistoriesComplete);
            foreach (var d in ChronicleAscensionCatalog.All) Assert.IsTrue(q.progress.IsRevelationEarned(d.id), d.id);
            Assert.AreEqual(0, ChronicleCatalog.Memories.Count(m => !q.progress.IsUnlocked(m.id)));
        }

        // ---------------------------------------------------------------- isolation and reset
        [Test] public void NothingTheSessionDoesLeaksIntoAnotherProfile()
        {
            var real = new ChronicleProgress(); var untouched = ChronicleQaSession.Fingerprint(real);
            var q = Fresh(); q.OpenChapterX(); q.UnlockAllMemories(); q.SimulateRun(ChronicleHero.Vanguard, 1, ChronicleQaRun.FateDebtVictory);
            Assert.AreEqual(untouched, ChronicleQaSession.Fingerprint(real));
        }

        [Test] public void ResetClearsEverythingAtOnceAndKeepsReadingPreferences()
        {
            var p = new ChronicleProgress { instantText = true, textSpeed = 1.5f }; var q = new ChronicleQaSession(p);
            q.OpenChapterX(); PlayThrough(p, "MEM_07", false); p.journal.readingId = "MEM_07"; p.journal.readingSpread = 3;
            q.mode = ChronicleQaPlayMode.Record;
            q.ResetAll();
            Assert.AreEqual(ChronicleQaSession.Fingerprint(new ChronicleProgress()), ChronicleQaSession.Fingerprint(p));
            Assert.IsTrue(p.instantText); Assert.AreEqual(1.5f, p.textSpeed, .001f);
            Assert.AreEqual(0, p.TotalUnlocked); Assert.IsFalse(p.secretUnlocked); Assert.AreEqual(0, p.ascension.earned.Count);
            Assert.IsFalse(q.SimulateRun(ChronicleHero.Reaper, 1, ChronicleQaRun.RepeatLastRun).earned, "the run memory is cleared too");
            Assert.IsTrue(q.SimulateRun(ChronicleHero.Vanguard, 1, ChronicleQaRun.FateDebtVictory).earned, "the profile is usable again straight away");
        }

        // ---------------------------------------------------------------- save restoration
        [Test] public void FingerprintsDetectAnyChangeToSavedState()
        {
            var p = new ChronicleProgress(); var q = new ChronicleQaSession(p);
            var empty = ChronicleQaSession.Fingerprint(p);
            q.SimulateRun(ChronicleHero.Vanguard, 1, ChronicleQaRun.FateDebtVictory);
            var earned = ChronicleQaSession.Fingerprint(p);
            Assert.AreNotEqual(empty, earned); Assert.AreEqual("", ChronicleQaSession.Compare(earned, earned));
            Assert.IsTrue(ChronicleQaSession.Compare(empty, earned).Contains("asc earned"));
            p.journal.readingId = "MEM_01"; p.journal.readingSpread = 2;
            Assert.IsTrue(ChronicleQaSession.Compare(earned, ChronicleQaSession.Fingerprint(p)).Contains("reading"));
        }

        [Test] public void TheLogIsBoundedAndSessionsCanBeBoundToAnyProgress()
        {
            var q = Fresh();
            for (var i = 0; i < 200; i++) q.SimulateRun(ChronicleHero.Vanguard, 0, ChronicleQaRun.NormalVictory);
            Assert.LessOrEqual(q.log.Count, ChronicleQaSession.LogLimit);
            var p = new ChronicleProgress(); var bound = new ChronicleDevSession(p);
            Assert.IsTrue(ReferenceEquals(p, bound.sandbox));
            bound.SimulateBoss(ChronicleHero.Vanguard, 1); Assert.IsTrue(p.IsUnlocked("MEM_07"), "the existing developer session works on the progress it is given");
        }
    }
}
