using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>Scene playback, skipping, corrections, replay, handwriting, the director and the book-pose rules.</summary>
    [TestFixture]
    public class ChronicleSceneTests
    {
        const float Dt = 1f / 30f;
        readonly List<string> warnings = new List<string>();

        [SetUp] public void SetUp() { warnings.Clear(); ChronicleLog.Warn = w => warnings.Add(w); }

        // ---------------------------------------------------------------- helpers
        static ChronicleProgress Everything()
        {
            var p = new ChronicleProgress();
            foreach (ChronicleHero h in Enum.GetValues(typeof(ChronicleHero))) p.RegisterRunEnded(h, true, 3, 54, "win-" + h);
            return p;
        }

        sealed class Rig
        {
            public ChronicleProgress progress; public RecordingChronicleStage stage = new RecordingChronicleStage(); public ChronicleProgressSink sink; public ChronicleSequencer seq;
        }

        Rig Make(string sceneId, ChronicleProgress progress, ChronicleSettings settings = null, bool replay = false, ChronicleBookPose pose = ChronicleBookPose.Open, bool allowLocked = false)
        {
            var rig = new Rig { progress = progress };
            rig.sink = new ChronicleProgressSink(progress, allowLocked);
            rig.seq = new ChronicleSequencer(ChronicleScripts.Get(sceneId), rig.stage, new ChronicleProgressContext(progress), rig.sink, settings, null, replay);
            rig.seq.Start(pose);
            return rig;
        }

        static float RunToEnd(ChronicleSequencer s, float max = 400f)
        {
            float t = 0; while (!s.Finished && t < max) { s.Update(Dt); t += Dt; }
            Assert.IsTrue(s.Finished, s.Scene.id + " did not finish within " + max + "s");
            return t;
        }

        static string Signature(ChronicleSceneState st)
        {
            var parts = new List<string>();
            foreach (var b in st.transcript) parts.Add(b.id + "|" + b.text + "|" + b.struck + "|" + b.erased + "|" + b.complete);
            parts.Add("pose=" + st.pose + "@" + st.spreadIndex + " blocks=" + st.blocks.Count);
            parts.Add("illus=" + st.illustration.visible + ":" + st.illustration.actors.Count + ":" + st.illustration.animations.Count);
            foreach (var a in st.illustration.actors) parts.Add(a.id + "@" + Math.Round(a.x, 3) + "," + Math.Round(a.y, 3) + "a" + Math.Round(a.alpha, 3));
            return string.Join("\n", parts);
        }

        static ChronicleScene Tiny(Action<ChronicleSceneBuilder> build)
        {
            var b = new ChronicleSceneBuilder("TEST", "test"); build(b); return b.Finish();
        }

        // ---------------------------------------------------------------- the First Correction
        [Test] public void TheFirstCorrectionPlaysEndToEndAndIsSaved()
        {
            var p = Everything(); var r = Make("MEM_07", p);
            Assert.IsFalse(p.HasCorrection("CORR_01"));
            var seconds = RunToEnd(r.seq);
            var original = r.seq.State.transcript.First(b => b.id == "corr:CORR_01");
            Assert.AreEqual("Three travelers stood together.", original.text); Assert.IsTrue(original.struck); Assert.AreEqual(1f, original.strikeProgress);
            var fix = r.seq.State.transcript.First(b => b.id == "fix:CORR_01");
            Assert.AreEqual("There were four.", fix.text); Assert.IsTrue(fix.complete); Assert.AreEqual(ChronicleTextStyle.Correction, fix.style);
            Assert.IsTrue(p.HasCorrection("CORR_01")); Assert.IsTrue(p.HasFact("FACT_FOURTH_COMPANION")); Assert.IsTrue(p.IsViewed("MEM_07"));
            Assert.IsTrue(r.stage.ended); Assert.IsFalse(r.stage.endedSkipped);
            Assert.Greater(seconds, 20f); Assert.AreEqual(0, warnings.Count, string.Join("; ", warnings));
        }

        [Test] public void TheCorrectionIsOnlySavedAfterItsBeat()
        {
            var p = Everything(); var r = Make("MEM_07", p);
            var sawStrikeUnsaved = false; float t = 0;
            while (!r.seq.Finished && t < 300) { r.seq.Update(Dt); t += Dt; if (!sawStrikeUnsaved && r.seq.State.transcript.Any(b => b.struck)) { sawStrikeUnsaved = !p.HasCorrection("CORR_01"); } }
            Assert.IsTrue(sawStrikeUnsaved, "the red strike begins before the correction is recorded");
            Assert.IsTrue(p.HasCorrection("CORR_01"));
        }

        [Test] public void SkippingDuringTheStrikeStillAppliesTheWholeCorrection()
        {
            var p = Everything(); var r = Make("MEM_07", p);
            float t = 0;
            while (!r.seq.Finished && t < 300 && !r.seq.State.transcript.Any(b => b.struck && b.strikeProgress > 0f && b.strikeProgress < 1f)) { r.seq.Update(Dt); t += Dt; }
            Assert.IsFalse(r.seq.Finished, "caught the strike mid-animation");
            r.seq.Skip();
            var original = r.seq.State.transcript.First(b => b.id == "corr:CORR_01");
            Assert.AreEqual(1f, original.strikeProgress); Assert.IsTrue(original.struck);
            Assert.AreEqual("There were four.", r.seq.State.transcript.First(b => b.id == "fix:CORR_01").text);
            Assert.IsTrue(p.HasCorrection("CORR_01")); Assert.IsTrue(p.IsViewed("MEM_07")); Assert.IsTrue(r.stage.endedSkipped);
        }

        [Test] public void ALockedMemoryPlaysButSavesNothing()
        {
            var p = new ChronicleProgress(); var r = Make("MEM_07", p);
            RunToEnd(r.seq);
            Assert.IsFalse(p.HasCorrection("CORR_01")); Assert.AreEqual(0, p.TotalViewed); Assert.AreEqual(0, p.discoveredFacts.Count);
        }

        [Test] public void ThePreviewSandboxMayPlayAnythingWithoutTouchingRealProgress()
        {
            var real = new ChronicleProgress(); var sandbox = new ChronicleProgress();
            var r = Make("MEM_07", sandbox, allowLocked: true); RunToEnd(r.seq);
            Assert.IsTrue(sandbox.HasCorrection("CORR_01")); Assert.IsFalse(real.HasCorrection("CORR_01")); Assert.AreEqual(0, real.TotalViewed);
        }

        [Test] public void ReplayingNeverDuplicatesOrRegrantsProgress()
        {
            var p = Everything(); var r1 = Make("MEM_07", p); RunToEnd(r1.seq);
            var corrections = p.corrections.Count; var facts = p.discoveredFacts.Count; var viewed = p.viewedMemories.Count;
            var r2 = Make("MEM_07", p, replay: true); RunToEnd(r2.seq);
            Assert.AreEqual(corrections, p.corrections.Count); Assert.AreEqual(facts, p.discoveredFacts.Count); Assert.AreEqual(viewed, p.viewedMemories.Count);
            Assert.IsFalse(r2.sink.firstView, "a replay is not a first view");
            Assert.IsTrue(r2.seq.State.transcript.First(b => b.id == "corr:CORR_01").struck, "the dramatic beat still plays again");
        }

        [Test] public void ReplayAfterASkipBehavesTheSame()
        {
            var p = Everything(); var r1 = Make("MEM_17", p); r1.seq.Update(Dt * 30); r1.seq.Skip();
            Assert.IsTrue(p.HasCorrection("CORR_06")); Assert.IsTrue(p.IsViewed("MEM_17"));
            var r2 = Make("MEM_17", p, replay: true); r2.seq.Skip();
            Assert.AreEqual(1, p.corrections.Count(c => c == "CORR_06"));
        }

        // ---------------------------------------------------------------- out-of-order discovery
        [Test] public void ReachingChapterThreeThroughTheHexerStillCorrectsTheOldRecord()
        {
            var p = Everything(); var r = Make("MEM_08", p); RunToEnd(r.seq);
            Assert.IsTrue(p.HasCorrection("CORR_01"));
            Assert.IsTrue(r.seq.State.transcript.Any(b => b.id == "corr:CORR_01" && b.struck), "the narrator reconciles the record in this scene");
        }

        [Test] public void ALateChapterThreeMemoryDoesNotRepeatAnAlreadyRecordedCorrection()
        {
            var p = Everything(); RunToEnd(Make("MEM_07", p).seq);
            var r = Make("MEM_08", p); RunToEnd(r.seq);
            Assert.IsFalse(r.seq.State.transcript.Any(b => b.id == "corr:CORR_01"), "no second correction block");
            Assert.AreEqual(1, p.corrections.Count(c => c == "CORR_01"));
        }

        [Test] public void NarrationAdaptsToWhatThePlayerHasDiscovered()
        {
            var unknown = new ChronicleStaticContext(); var known = new ChronicleStaticContext(); known.facts.Add("FACT_FOURTH_COMPANION");
            var a = ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_08"), unknown); var b = ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_08"), known);
            Assert.IsTrue(a.transcript.Any(x => x.text.Contains("Its owner is unknown")));
            Assert.IsFalse(a.transcript.Any(x => x.text.Contains("belongs to the fourth companion")));
            Assert.IsTrue(b.transcript.Any(x => x.text.Contains("belongs to the fourth companion")));
        }

        [Test] public void AMemoryShowingTheFourthBeforeHeIsKnownSaysSoHonestly()
        {
            var unknown = new ChronicleStaticContext(); var known = new ChronicleStaticContext(); known.facts.Add("FACT_FOURTH_COMPANION");
            Assert.IsTrue(ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_13"), unknown).transcript.Any(x => x.text.StartsWith("A fourth voice speaks")));
            Assert.IsFalse(ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_13"), known).transcript.Any(x => x.text.StartsWith("A fourth voice speaks")));
        }

        [Test] public void TheFourthIsAShadowUntilDiscoveredThenAFigure()
        {
            var unknown = new ChronicleStaticContext(); var known = new ChronicleStaticContext(); known.facts.Add("FACT_FOURTH_COMPANION");
            Assert.AreEqual(ChronicleShape.Shadow, ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_11"), unknown).illustration.Find("f4").shape);
            Assert.AreEqual(ChronicleShape.Figure, ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_11"), known).illustration.Find("f4").shape);
        }

        [Test] public void TheArchiveShowsACorrectionOnlyOnceItIsDiscovered()
        {
            var none = new ChronicleStaticContext(); var has = new ChronicleStaticContext(); has.corrections.Add("CORR_02");
            var before = ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_05"), none);
            var after = ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_05"), has);
            Assert.IsFalse(before.transcript.First(b => b.id == "corr:CORR_02").struck); Assert.IsFalse(before.transcript.Any(b => b.id == "fix:CORR_02"));
            Assert.IsTrue(after.transcript.First(b => b.id == "corr:CORR_02").struck); Assert.AreEqual("The traveler was changing them.", after.transcript.First(b => b.id == "fix:CORR_02").text);
            Assert.AreEqual("The traveler watched the futures unfold.", before.transcript.First(b => b.id == "corr:CORR_02").text, "the original is always preserved");
        }

        // ---------------------------------------------------------------- every scene
        [Test] public void AllThirtyScenesPlayToTheEndWithoutWarnings()
        {
            foreach (var id in ChronicleScripts.Ids.OrderBy(x => x))
            {
                var r = Make(id, Everything(), pose: id.StartsWith("SCENE_") ? ChronicleBookPose.Closed : ChronicleBookPose.Open, allowLocked: true);
                RunToEnd(r.seq);
                Assert.IsTrue(r.stage.ended, id);
            }
            Assert.AreEqual(0, warnings.Count, string.Join("; ", warnings));
        }

        [Test] public void SkippingAnySceneReachesTheSameFinalStateAsPlayingItThrough()
        {
            foreach (var id in ChronicleScripts.Ids.OrderBy(x => x))
            {
                var pose = id.StartsWith("SCENE_") ? ChronicleBookPose.Closed : ChronicleBookPose.Open;
                var played = Make(id, Everything(), pose: pose, allowLocked: true); RunToEnd(played.seq);
                var skipped = Make(id, Everything(), pose: pose, allowLocked: true); for (var i = 0; i < 50; i++) skipped.seq.Update(Dt); skipped.seq.Skip();
                Assert.AreEqual(Signature(played.seq.State), Signature(skipped.seq.State), id + " final state differs after skipping");
                Assert.AreEqual(string.Join(",", played.progress.corrections.OrderBy(x => x)), string.Join(",", skipped.progress.corrections.OrderBy(x => x)), id + " corrections");
                Assert.AreEqual(played.progress.discoveredFacts.Count, skipped.progress.discoveredFacts.Count, id + " facts");
                Assert.AreEqual(played.progress.viewedMemories.Count, skipped.progress.viewedMemories.Count, id + " viewed");
            }
        }

        [Test] public void SkippingFromTheVeryStartStillFinishesCleanly()
        {
            foreach (var id in ChronicleScripts.Ids)
            {
                var r = Make(id, Everything(), pose: id.StartsWith("SCENE_") ? ChronicleBookPose.Closed : ChronicleBookPose.Open, allowLocked: true);
                r.seq.Skip();
                Assert.IsTrue(r.seq.Finished, id); Assert.IsTrue(r.seq.Skipped, id); Assert.IsTrue(r.stage.audioStopped, id + " audio stopped");
                r.seq.Skip(); r.seq.Update(Dt);   // a second skip or update after the end is harmless
            }
        }

        [Test] public void EveryMemoryHasEnoughToReadAndWatch()
        {
            foreach (var m in ChronicleCatalog.Memories)
            {
                var scene = ChronicleScripts.Get(m.id);
                Assert.IsNotNull(scene, m.id); Assert.GreaterOrEqual(scene.CountOf(ChronicleOp.ShowNarration), 3, m.id + " narration");
                Assert.Greater(scene.CountOf(ChronicleOp.SpawnActor), 0, m.id + " has something on the page");
                Assert.Greater(scene.CountOf(ChronicleOp.MoveIllustrationCharacter) + scene.CountOf(ChronicleOp.FadeIllustrationCharacter) + scene.CountOf(ChronicleOp.AnimateIllustration), 0, m.id + " moves");
            }
        }

        // ---------------------------------------------------------------- the opening and the finale
        [Test] public void TheOpeningTakesAboutNinetySecondsAndDoesNotSpoil()
        {
            var r = Make(ChronicleCatalog.OpeningSceneId, new ChronicleProgress(), pose: ChronicleBookPose.Closed);
            var seconds = RunToEnd(r.seq);
            Assert.Greater(seconds, 60f, "opening length " + seconds); Assert.Less(seconds, 100f, "opening length " + seconds);
            foreach (var b in r.seq.State.transcript) { Assert.IsFalse(b.text.ToLowerInvariant().Contains("fourth")); Assert.IsFalse(b.text.ToLowerInvariant().Contains("observer")); }
            Assert.AreEqual(ChronicleBookPose.Closed, r.seq.State.pose);
        }

        [Test] public void TheShortOpeningIsShort()
        {
            var seconds = RunToEnd(Make(ChronicleCatalog.OpeningShortSceneId, new ChronicleProgress(), pose: ChronicleBookPose.Closed).seq);
            Assert.Less(seconds, 25f, "short opening length " + seconds); Assert.Greater(seconds, 6f);
        }

        [Test] public void SkippingTheOpeningClosesTheBookStopsAudioAndCountsOneViewing()
        {
            var p = new ChronicleProgress(); var r = Make(ChronicleCatalog.OpeningSceneId, p, pose: ChronicleBookPose.Closed);
            for (var i = 0; i < 90; i++) r.seq.Update(Dt);
            Assert.AreEqual(ChronicleBookPose.Open, r.seq.State.pose, "the book is open mid-opening");
            r.seq.Skip();
            Assert.AreEqual(ChronicleBookPose.Closed, r.stage.pose); Assert.IsTrue(r.stage.audioStopped); Assert.IsTrue(r.stage.endedSkipped);
            Assert.AreEqual(1, p.openingViews);
        }

        [Test] public void ChapterXPlaysRecordsTheRevealAndStaysReplayable()
        {
            var p = Everything(); var r = Make(ChronicleCatalog.SecretSceneId, p, pose: ChronicleBookPose.Closed); RunToEnd(r.seq);
            Assert.IsTrue(p.secretCompleted); Assert.IsTrue(p.HasCorrection("CORR_10")); Assert.IsTrue(p.HasFact("FACT_NARRATOR_IS_OBSERVER"));
            Assert.IsTrue(r.seq.State.transcript.Any(b => b.text == "I AM THE FORGOTTEN OBSERVER."));
            Assert.IsTrue(r.seq.State.transcript.First(b => b.id == "corr:CORR_10").struck);
            var again = Make(ChronicleCatalog.SecretSceneId, p, replay: true, pose: ChronicleBookPose.Closed); RunToEnd(again.seq);
            Assert.AreEqual(1, p.corrections.Count(c => c == "CORR_10")); Assert.IsFalse(again.sink.secretCompleted);
        }

        [Test] public void ChapterXCannotCompleteWhileLocked()
        {
            var p = new ChronicleProgress(); RunToEnd(Make(ChronicleCatalog.SecretSceneId, p, pose: ChronicleBookPose.Closed).seq);
            Assert.IsFalse(p.secretCompleted); Assert.IsFalse(p.HasCorrection("CORR_10"));
        }

        [Test] public void ThePrematureEndingIsStruckInTheArchiveOnlyOnceItsCorrectionIsKnown()
        {
            var none = new ChronicleStaticContext(); var has = new ChronicleStaticContext(); has.corrections.Add("CORR_08");
            Assert.IsFalse(ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_24"), none).transcript.First(b => b.id == "end_word").struck);
            Assert.IsTrue(ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_24"), has).transcript.First(b => b.id == "end_word").struck);
        }

        [Test] public void TheTheEndFadesAwayInsteadOfBecomingPermanent()
        {
            var state = ChronicleSceneResolver.Resolve(ChronicleScripts.Get(ChronicleCatalog.SecretSceneId), new ChronicleStaticContext(), null, ChronicleBookPose.Closed);
            var theEnd = state.transcript.First(b => b.id == "the_end");
            Assert.IsTrue(theEnd.erased); Assert.AreEqual(1f, theEnd.eraseProgress);
        }

        // ---------------------------------------------------------------- the book
        [Test] public void BookCoversStayOpenWhilePagesTurn()
        {
            foreach (var id in ChronicleScripts.Ids.OrderBy(x => x))
            {
                var pose = id.StartsWith("SCENE_") ? ChronicleBookPose.Closed : ChronicleBookPose.Open;
                var r = Make(id, Everything(), pose: pose, allowLocked: true); RunToEnd(r.seq);
                var book = r.stage.log.Where(l => l.StartsWith("book:")).Select(l => l.Substring(5)).ToList();
                var open = book.IndexOf("Open"); var close = book.LastIndexOf("Close");
                var turns = 0;
                for (var i = 0; i < book.Count; i++)
                {
                    if (book[i] == "TurnPageForward") { turns++; Assert.IsTrue(open < i || pose == ChronicleBookPose.Open, id + ": a page turned before the book opened"); if (close >= 0) Assert.IsTrue(i < close, id + ": a page turned after the book closed"); }
                    if (i > open && open >= 0 && book[i] == "Open") Assert.Fail(id + ": the book re-opened mid-scene");
                    if (book[i] == "Close" && i != close) Assert.Fail(id + ": the book closed mid-scene");
                }
                if (pose == ChronicleBookPose.Open) Assert.AreEqual(-1, close, id + ": a memory must leave the book open");
                else Assert.IsTrue(open >= 0 && close > open, id + ": opening and finale open then close the book once");
            }
        }

        [Test] public void TurningAClosedBookIsRefusedRatherThanFaked()
        {
            var scene = Tiny(b => b.Turn().Say("hello"));
            var stage = new RecordingChronicleStage(); var seq = new ChronicleSequencer(scene, stage, new ChronicleStaticContext(), null);
            seq.Start(ChronicleBookPose.Closed); RunToEnd(seq);
            Assert.AreEqual(0, seq.State.spreadIndex); Assert.AreEqual(0, stage.Count("book:TurnPageForward")); Assert.AreEqual(1, warnings.Count(w => w.Contains("TurnPage ignored")));
        }

        [Test] public void OpeningAnAlreadyOpenBookOnAFreshPageDoesNotTurnIt()
        {
            var scene = Tiny(b => b.Open().Say("fresh"));
            var stage = new RecordingChronicleStage(); var seq = new ChronicleSequencer(scene, stage, new ChronicleStaticContext(), null);
            seq.Start(ChronicleBookPose.Open); RunToEnd(seq);
            Assert.AreEqual(0, stage.Count("book:TurnPageForward")); Assert.AreEqual(0, stage.Count("book:Open"));
        }

        [Test] public void OpeningAnOpenBookWithContentTurnsToAFreshSpread()
        {
            var scene = Tiny(b => b.Open().Say("one").Open().Say("two"));
            var stage = new RecordingChronicleStage(); var seq = new ChronicleSequencer(scene, stage, new ChronicleStaticContext(), null);
            seq.Start(ChronicleBookPose.Open); RunToEnd(seq);
            Assert.AreEqual(1, seq.State.spreadIndex); Assert.AreEqual(1, seq.State.blocks.Count);
        }

        [Test] public void TheThreeNarratorStatesDriveTheSameBookModel()
        {
            var logs = new List<string>();
            foreach (ChronicleNarratorState s in Enum.GetValues(typeof(ChronicleNarratorState)))
            {
                var ctx = new ChronicleStaticContext { narrator = s }; var stage = new RecordingChronicleStage();
                var seq = new ChronicleSequencer(ChronicleScripts.Get("MEM_01"), stage, ctx, null); seq.Start(ChronicleBookPose.Open); RunToEnd(seq);
                Assert.AreEqual(s, seq.State.narrator);
                logs.Add(string.Join(",", stage.log.Where(l => l.StartsWith("book:"))));
            }
            Assert.AreEqual(logs[0], logs[1]); Assert.AreEqual(logs[1], logs[2]);
        }

        [Test] public void NarratorStatesChangeHowTheNarratorWrites()
        {
            var times = new List<float>();
            foreach (ChronicleNarratorState s in Enum.GetValues(typeof(ChronicleNarratorState)))
            {
                var seq = new ChronicleSequencer(ChronicleScripts.Get("MEM_01"), new NullChronicleStage(), new ChronicleStaticContext { narrator = s }, null);
                seq.Start(ChronicleBookPose.Open); times.Add(RunToEnd(seq));
            }
            Assert.Greater(times[1], times[0], "the doubting historian writes more slowly and hesitantly");
            Assert.Less(times[2], times[1], "the remembering observer writes with more urgency");
            Assert.Greater(ChronicleNarratorStyle.For(ChronicleNarratorState.DoubtingHistorian).tremor, 0f);
        }

        // ---------------------------------------------------------------- pause, speed, accessibility
        [Test] public void PausingHaltsTheSceneAndResumingContinuesIt()
        {
            var r = Make("MEM_01", Everything());
            for (var i = 0; i < 60; i++) r.seq.Update(Dt);
            var elapsed = r.seq.Elapsed; var written = r.seq.State.transcript.Sum(b => b.VisibleLength);
            r.seq.Pause(); for (var i = 0; i < 300; i++) r.seq.Update(Dt);
            Assert.AreEqual(elapsed, r.seq.Elapsed); Assert.AreEqual(written, r.seq.State.transcript.Sum(b => b.VisibleLength));
            r.seq.Resume(); for (var i = 0; i < 90; i++) r.seq.Update(Dt);
            Assert.Greater(r.seq.State.transcript.Sum(b => b.VisibleLength), written);
        }

        [Test] public void FastForwardScalesTimeForDeveloperTesting()
        {
            var normal = RunToEnd(Make("MEM_01", Everything()).seq);
            var fast = RunToEnd(Make("MEM_01", Everything(), new ChronicleSettings { fastForward = 4f }).seq);
            Assert.Less(fast, normal * .4f);
        }

        [Test] public void InstantTextWritesEveryPassageAtOnce()
        {
            var r = Make("MEM_01", Everything(), new ChronicleSettings { instantText = true });
            r.seq.Update(Dt); r.seq.Update(Dt);
            Assert.IsTrue(r.seq.State.transcript.All(b => b.complete), "everything created so far is already written");
            var normal = RunToEnd(Make("MEM_01", Everything()).seq); var instant = RunToEnd(Make("MEM_01", Everything(), new ChronicleSettings { instantText = true }).seq);
            Assert.Less(instant, normal * .6f);
        }

        [Test] public void ReducedFlashingAndMotionSoftenTheEffects()
        {
            var scene = Tiny(b => b.Open().Illustrate("void").Actor("a", ChronicleShape.Rect, ChronicleColor.Ink, .5f, .5f).Anim("flash", "", 3f, 1f).Anim("shake", "", 3f, 1f).Wait(.1f));
            var seq = new ChronicleSequencer(scene, new NullChronicleStage(), new ChronicleStaticContext(), null, new ChronicleSettings { reduceFlashing = true, reduceMotion = true });
            seq.Start(ChronicleBookPose.Open); for (var i = 0; i < 10; i++) seq.Update(Dt);
            var anims = seq.State.illustration.animations;
            Assert.LessOrEqual(anims.First(a => a.name == "flash").intensity, .31f); Assert.AreEqual(0f, anims.First(a => a.name == "shake").intensity);
            Assert.AreEqual(0f, seq.State.tremor);
        }

        [Test] public void MissingArtAndAudioNeverBreakAScene()
        {
            var scene = Tiny(b => b.Open().Illustrate("not_a_real_backdrop").Audio("missing_cue").Say("still readable").Move("ghost", .5f, .5f, 1f).Fade("ghost", 0f, 1f).Strike("nothing"));
            var seq = new ChronicleSequencer(scene, new NullChronicleStage(), new ChronicleStaticContext(), null);
            seq.Start(ChronicleBookPose.Closed); RunToEnd(seq);
            Assert.IsTrue(seq.Finished); Assert.IsTrue(seq.State.transcript.Any(b => b.text == "still readable"));
            Assert.Greater(warnings.Count, 0, "problems are reported, not thrown");
        }

        // ---------------------------------------------------------------- director
        [Test] public void TheDirectorRefusesOverlappingPlayback()
        {
            var d = new ChronicleDirector(); var ctx = new ChronicleStaticContext();
            Assert.IsTrue(d.TryPlay(ChronicleScripts.Get("MEM_01"), new NullChronicleStage(), ctx, null, null, ChronicleBookPose.Open, false));
            Assert.IsFalse(d.TryPlay(ChronicleScripts.Get("MEM_02"), new NullChronicleStage(), ctx, null, null, ChronicleBookPose.Open, false));
            Assert.AreEqual("MEM_01", d.Current.Scene.id);
        }

        [Test] public void ASkipPressRightAfterStartIsIgnoredSoItCannotCancelTheSceneItLaunched()
        {
            var d = new ChronicleDirector();
            d.TryPlay(ChronicleScripts.Get("MEM_01"), new NullChronicleStage(), new ChronicleStaticContext(), null, null, ChronicleBookPose.Open, false);
            d.Update(.1f); Assert.IsFalse(d.RequestSkip()); Assert.IsTrue(d.IsPlaying);
            d.Update(.4f); Assert.IsTrue(d.RequestSkip()); Assert.IsFalse(d.IsPlaying);
            Assert.IsFalse(d.RequestSkip(), "nothing left to skip");
        }

        [Test] public void TheDirectorAllowsANewSceneOnceTheLastEnds()
        {
            var d = new ChronicleDirector(); var ended = 0; d.SceneEnded += s => ended++;
            d.TryPlay(ChronicleScripts.Get("MEM_01"), new NullChronicleStage(), new ChronicleStaticContext(), null, null, ChronicleBookPose.Open, false);
            d.Update(1f); d.RequestSkip();
            Assert.AreEqual(1, ended); Assert.IsTrue(d.TryPlay(ChronicleScripts.Get("MEM_02"), new NullChronicleStage(), new ChronicleStaticContext(), null, null, ChronicleBookPose.Open, false));
        }

        // ---------------------------------------------------------------- handwriting
        [Test] public void HandwritingRevealsProgressively()
        {
            var b = new ChronicleTextBlock { style = ChronicleTextStyle.Narration }; ChronicleHandwriting.Prepare(b, "The quick brown fox jumps over the lazy dog.", new ChroniclePageMetrics());
            var ns = ChronicleNarratorStyle.For(ChronicleNarratorState.ConfidentHistorian); var last = 0;
            for (var i = 0; i < 400 && !b.complete; i++) { ChronicleHandwriting.Step(b, Dt, ns, ChronicleSettings.Default); Assert.GreaterOrEqual(b.cursor, last); last = b.cursor; }
            Assert.IsTrue(b.complete); Assert.AreEqual(b.text.Length, b.cursor); Assert.AreEqual(1f, b.Progress);
        }

        [Test] public void AnInlinePauseMarkerHoldsThePenAndIsNeverDisplayed()
        {
            var plain = new ChronicleTextBlock(); var paused = new ChronicleTextBlock(); var m = new ChroniclePageMetrics();
            ChronicleHandwriting.Prepare(plain, "Wait here", m); ChronicleHandwriting.Prepare(paused, "Wait{p=2.0} here", m);
            Assert.AreEqual("Wait here", paused.text); Assert.AreEqual(1, paused.pauses.Count); Assert.AreEqual(4, paused.pauses[0].index);
            var ns = ChronicleNarratorStyle.For(ChronicleNarratorState.ConfidentHistorian); float tPlain = 0, tPaused = 0;
            while (!plain.complete) { ChronicleHandwriting.Step(plain, Dt, ns, ChronicleSettings.Default); tPlain += Dt; }
            while (!paused.complete) { ChronicleHandwriting.Step(paused, Dt, ns, ChronicleSettings.Default); tPaused += Dt; }
            Assert.Greater(tPaused - tPlain, 1.8f);
        }

        [Test] public void TextSpeedSettingScalesHandwriting()
        {
            float Time(float speed) { var b = new ChronicleTextBlock(); ChronicleHandwriting.Prepare(b, new string('a', 120), new ChroniclePageMetrics()); var s = new ChronicleSettings { textSpeed = speed }; float t = 0; while (!b.complete) { ChronicleHandwriting.Step(b, Dt, ChronicleNarratorStyle.For(ChronicleNarratorState.ConfidentHistorian), s); t += Dt; } return t; }
            Assert.Less(Time(2f), Time(1f) * .7f); Assert.Greater(Time(.5f), Time(1f) * 1.5f);
        }

        [Test] public void LongPassagesWrapWithinThePageWidth()
        {
            var m = new ChroniclePageMetrics();
            var text = string.Join(" ", Enumerable.Repeat("remembering", 20));
            var starts = ChronicleLayout.Wrap(text, m.textWidth, m.fontSize, m.measure);
            Assert.Greater(starts.Length, 4);
            for (var i = 0; i < starts.Length; i++)
            {
                var end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
                Assert.LessOrEqual(m.measure(text.Substring(starts[i], end - starts[i]).TrimEnd(), m.fontSize), m.textWidth + .01f, "line " + i);
            }
        }

        [Test] public void AnOverlongPassageIsFlaggedAsOverflowingItsPage()
        {
            var scene = Tiny(b => { b.Open(); for (var i = 0; i < 8; i++) b.Say(string.Join(" ", Enumerable.Repeat("overflowing", 30))); });
            var state = ChronicleSceneResolver.Resolve(scene, new ChronicleStaticContext(), null, ChronicleBookPose.Closed);
            Assert.Greater(state.overflowCount, 0); Assert.IsTrue(state.transcript.Any(b => b.overflow));
        }

        [Test] public void TextOnlySpreadsFlowFromTheLeftPageToTheRight()
        {
            var scene = Tiny(b => { b.Open(ChronicleSpreadLayout.TextOnly); for (var i = 0; i < 9; i++) b.Say(string.Join(" ", Enumerable.Repeat("flowing", 14))); });
            var state = ChronicleSceneResolver.Resolve(scene, new ChronicleStaticContext(), null, ChronicleBookPose.Closed);
            Assert.IsTrue(state.transcript.Any(b => b.side == ChroniclePageSide.Left)); Assert.IsTrue(state.transcript.Any(b => b.side == ChroniclePageSide.Right));
            Assert.AreEqual(0, state.overflowCount);
        }

        [Test] public void ConditionsGateIndividualCommands()
        {
            var scene = Tiny(b => b.Open().If("fact:FACT_FOURTH_COMPANION").Say("known").EndIf().If("!fact:FACT_FOURTH_COMPANION").Say("unknown").EndIf().If("first").Say("first time").EndIf().If("replay").Say("again").EndIf());
            var ctx = new ChronicleStaticContext();
            var a = new ChronicleSequencer(scene, new NullChronicleStage(), ctx, null, null, null, false); a.Start(ChronicleBookPose.Closed); RunToEnd(a);
            Assert.AreEqual("unknown,first time", string.Join(",", a.State.transcript.Select(b => b.text)));
            ctx.facts.Add("FACT_FOURTH_COMPANION");
            var r = new ChronicleSequencer(scene, new NullChronicleStage(), ctx, null, null, null, true); r.Start(ChronicleBookPose.Closed); RunToEnd(r);
            Assert.AreEqual("known,again", string.Join(",", r.State.transcript.Select(b => b.text)));
        }
    }
}
