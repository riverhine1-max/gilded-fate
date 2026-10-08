using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>The developer console's logic: sandboxed, previewable, and unable to touch real progression.</summary>
    [TestFixture]
    public class ChronicleDevSessionTests
    {
        static void RunOut(ChronicleDevSession s) { for (var i = 0; i < 20000 && s.IsPlaying; i++) s.Tick(1f / 30f); Assert.IsFalse(s.IsPlaying, "scene should have ended"); }

        [Test] public void PlayingARealSceneNeverTouchesProgressOutsideTheSandbox()
        {
            var real = new ChronicleProgress(); var s = new ChronicleDevSession();
            Assert.IsTrue(s.Play("MEM_07")); RunOut(s);
            Assert.IsTrue(s.sandbox.HasCorrection("CORR_01"));
            Assert.AreEqual(0, real.corrections.Count); Assert.AreEqual(0, real.TotalViewed);
        }

        [Test] public void PreviewModeRecordsNothingEvenInTheSandbox()
        {
            var s = new ChronicleDevSession { saveToSandbox = false };
            s.Play("MEM_07"); RunOut(s);
            Assert.AreEqual(0, s.sandbox.corrections.Count); Assert.AreEqual(0, s.sandbox.TotalViewed);
        }

        [Test] public void SkippingCommitsToTheSandboxLikePlayingThrough()
        {
            var s = new ChronicleDevSession(); s.Play("MEM_17"); s.Tick(1f); s.Skip();
            Assert.IsTrue(s.sandbox.HasCorrection("CORR_06")); Assert.IsTrue(s.sandbox.IsViewed("MEM_17"));
        }

        [Test] public void OnlyOneSceneAtATimeAndLockedScenesFollowTheToggle()
        {
            var s = new ChronicleDevSession(); Assert.IsTrue(s.Play("MEM_01")); Assert.IsFalse(s.Play("MEM_02"));
            s.Stop();
            s.allowLockedScenes = false; s.Play("MEM_07"); RunOut(s);
            Assert.IsFalse(s.sandbox.HasCorrection("CORR_01"), "a locked scene plays but saves nothing when locked scenes are not allowed");
        }

        [Test] public void SimulatedMilestonesUnlockAndResetClearsThemWithoutBreakingPlayback()
        {
            var s = new ChronicleDevSession();
            s.SimulateRunEnded(ChronicleHero.Hexer, true, 3, 54);
            Assert.AreEqual(9, ChronicleCatalog.Memories.Count(m => m.hero == ChronicleHero.Hexer && s.sandbox.IsUnlocked(m.id)));
            s.ResetSandbox(); Assert.AreEqual(0, s.sandbox.TotalUnlocked);
            s.SimulateBoss(ChronicleHero.Vanguard, 1); Assert.IsTrue(s.sandbox.IsUnlocked("MEM_07"));
            s.allowLockedScenes = false; s.Play("MEM_07"); RunOut(s);
            Assert.IsTrue(s.sandbox.HasCorrection("CORR_01"), "playback after a reset commits to the new sandbox");
        }

        [Test] public void UnlockingEverythingOpensChapterX()
        {
            var s = new ChronicleDevSession(); s.UnlockEverythingButChapterX(); s.RefreshUnlocks();
            Assert.AreEqual(27, s.sandbox.TotalUnlocked); Assert.IsTrue(s.sandbox.secretUnlocked);
            Assert.AreEqual(28, s.UnlockTable().Count);
        }

        [Test] public void TheNarratorOverrideAndForcedFactsChangeWhatPlays()
        {
            var s = new ChronicleDevSession { NarratorOverride = 1 };
            s.Play("MEM_08"); Assert.AreEqual(ChronicleNarratorState.DoubtingHistorian, s.current.State.narrator); RunOut(s);
            Assert.IsTrue(s.current.State.transcript.Any(b => b.text.Contains("Its owner is unknown")));
            var t = new ChronicleDevSession { saveToSandbox = false }; t.ForceFact("FACT_FOURTH_COMPANION", true); t.Play("MEM_08"); RunOut(t);
            Assert.IsTrue(t.current.State.transcript.Any(b => b.text.Contains("belongs to the fourth companion")));
        }

        [Test] public void EveryScenePassesTheHeadlessCheck()
        {
            var s = new ChronicleDevSession();
            foreach (var id in ChronicleScripts.Ids) { Assert.IsTrue(s.CheckScene(id, out var report), id + ": " + report); }
        }

        [Test] public void ThePageTextShowsStrikesCorrectionsAndHidesErasedPassages()
        {
            var s = new ChronicleDevSession(); s.Play("MEM_07"); RunOut(s);
            var page = ChronicleDevSession.FormatPage(s.current.State);
            Assert.IsTrue(page.Contains(ChronicleDevSession.Struck("Three travelers stood together.")), "the original is shown struck through");
            Assert.IsTrue(page.Contains("<color=#c01818>There were four.</color>"), "the correction is in red ink");
            var end = new ChronicleDevSession(); end.Play(ChronicleCatalog.SecretSceneId); RunOut(end);
            Assert.IsFalse(ChronicleDevSession.FormatPage(end.current.State).Contains("THE EN"), "the erased ending is gone");
            Assert.IsTrue(ChronicleDevSession.FormatPage(end.current.State).Contains("I AM THE FORGOTTEN OBSERVER."));
        }

        [Test] public void TheStatusLineDescribesIdleAndPlayingStates()
        {
            var s = new ChronicleDevSession(); StringAssert();
            void StringAssert() { Assert.IsTrue(s.StatusLine().StartsWith("Idle")); }
            s.Play("MEM_01"); Assert.IsTrue(s.StatusLine().StartsWith("Playing"));
            s.Pause(); Assert.IsTrue(s.StatusLine().StartsWith("Paused"));
        }
    }
}
