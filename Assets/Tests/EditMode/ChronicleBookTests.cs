using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>The book's behaviour, independent of any mesh: poses, timing, page turns, overlay visibility and sequencer sync.</summary>
    [TestFixture]
    public class ChronicleBookTests
    {
        const float Dt = 1f / 60f;
        static readonly ChronicleNarratorState Conf = ChronicleNarratorState.ConfidentHistorian;

        static void Run(ChronicleBookAnimator a, float seconds) { for (float t = 0; t < seconds; t += Dt) a.Update(Dt); }
        static ChronicleBookAnimator OpenBook()
        {
            var a = new ChronicleBookAnimator(); a.Perform(ChronicleBookAction.Open, Conf); Run(a, a.timings.open + .1f); return a;
        }

        [Test] public void AClosedBookStaysClosedAndShowsNoPageContent()
        {
            var f = new ChronicleBookAnimator().Frame;
            Assert.AreEqual(0f, f.coverAngle); Assert.AreEqual(ChronicleBookPose.Closed, f.pose); Assert.IsFalse(f.overlayVisible); Assert.Greater(f.cameraPitch, 20f);
        }

        [Test] public void OpeningTakesTheConfiguredTimeAndEndsFullyOpenForReading()
        {
            var a = new ChronicleBookAnimator(); a.Perform(ChronicleBookAction.Open, Conf);
            Run(a, a.timings.open * .5f); var mid = a.Frame;
            Assert.Greater(mid.coverAngle, 5f); Assert.Less(mid.coverAngle, 175f); Assert.IsTrue(mid.busy); Assert.IsFalse(mid.overlayVisible, "no text while the cover swings");
            Run(a, a.timings.open * .5f + .05f); var done = a.Frame;
            Assert.AreEqual(180f, done.coverAngle); Assert.IsFalse(done.busy); Assert.AreEqual(1f, done.overlayAlpha); Assert.AreEqual(0f, done.cameraPitch, .001f, "straight down for reading");
        }

        [Test] public void CoverAngleOnlyIncreasesWhileOpeningAndOnlyDecreasesWhileClosing()
        {
            var a = new ChronicleBookAnimator(); a.Perform(ChronicleBookAction.Open, Conf); var last = -1f;
            for (float t = 0; t < 2f; t += Dt) { a.Update(Dt); var angle = a.Frame.coverAngle; Assert.GreaterOrEqual(angle, last - .001f); last = angle; }
            a.Perform(ChronicleBookAction.Close, Conf); last = 181f;
            for (float t = 0; t < 2f; t += Dt) { a.Update(Dt); var angle = a.Frame.coverAngle; Assert.LessOrEqual(angle, last + .001f); last = angle; }
            Assert.AreEqual(0f, a.Frame.coverAngle); Assert.AreEqual(ChronicleBookPose.Closed, a.Frame.pose);
        }

        [Test] public void TheCoversStayCompletelyOpenForTheWholeOfEveryPageTurn()
        {
            var a = OpenBook(); var frames = 0;
            foreach (var action in new[] { ChronicleBookAction.TurnPageForward, ChronicleBookAction.TurnPageForward, ChronicleBookAction.TurnPageBack })
            {
                a.Perform(action, Conf);
                for (float t = 0; t < a.timings.turn + .2f; t += Dt)
                {
                    a.Update(Dt); var f = a.Frame;
                    if (f.flipDirection != 0) { frames++; Assert.AreEqual(180f, f.coverAngle, "covers moved during a flip at progress " + f.flipRaw); Assert.AreEqual(1f, f.openAmount); Assert.AreEqual(0f, f.cameraPitch, .001f); }
                }
            }
            Assert.Greater(frames, 100, "the flips were actually observed");
        }

        [Test] public void AForwardTurnMovesPagesFromRightToLeftAndKeepsTheBlockWhole()
        {
            var a = OpenBook(); var start = a.Frame;
            Assert.AreEqual(0f, start.leftStack); Assert.AreEqual(1f, start.rightStack);
            a.Perform(ChronicleBookAction.TurnPageForward, Conf); var last = -1f;
            for (float t = 0; t < a.timings.turn + .1f; t += Dt)
            {
                a.Update(Dt); var f = a.Frame;
                Assert.AreEqual(1f, f.leftStack + f.rightStack, .0001f); if (f.flipDirection != 0) { Assert.GreaterOrEqual(f.flipProgress, last - .0001f); last = f.flipProgress; }
                if (f.flipDirection != 0) { Assert.AreEqual(1, f.flipDirection); Assert.GreaterOrEqual(f.flipLift, 0f); }
            }
            var end = a.Frame;
            Assert.AreEqual(1, end.spreadIndex); Assert.Greater(end.leftStack, 0f); Assert.AreEqual(0, end.flipDirection); Assert.IsFalse(end.busy);
            a.Perform(ChronicleBookAction.TurnPageBack, Conf); Run(a, a.timings.turn + .1f);
            Assert.AreEqual(0, a.Frame.spreadIndex); Assert.AreEqual(0f, a.Frame.leftStack, .0001f);
        }

        [Test] public void PageContentFadesOutMidTurnAndBackInAtTheEnd()
        {
            var a = OpenBook(); Assert.AreEqual(1f, a.Frame.overlayAlpha);
            a.Perform(ChronicleBookAction.TurnPageForward, Conf); var minimum = 1f; var sawHidden = false;
            for (float t = 0; t < a.timings.turn + .1f; t += Dt) { a.Update(Dt); var f = a.Frame; minimum = Math.Min(minimum, f.overlayAlpha); if (!f.overlayVisible) sawHidden = true; }
            Assert.IsTrue(sawHidden); Assert.AreEqual(0f, minimum, .01f); Assert.AreEqual(1f, a.Frame.overlayAlpha);
        }

        [Test] public void TurningAClosedBookIsRefusedAndStartingASecondTurnFinishesTheFirst()
        {
            var closed = new ChronicleBookAnimator(); closed.Perform(ChronicleBookAction.TurnPageForward, Conf);
            Assert.AreEqual(0, closed.Frame.spreadIndex); Assert.AreEqual(0, closed.Frame.flipDirection);
            var a = OpenBook(); a.Perform(ChronicleBookAction.TurnPageForward, Conf); a.Update(.2f); a.Perform(ChronicleBookAction.TurnPageForward, Conf);
            Assert.AreEqual(2, a.Frame.spreadIndex); Assert.AreEqual(180f, a.Frame.coverAngle);
        }

        [Test] public void OpeningAnOpenBookAndClosingAClosedBookDoNothing()
        {
            var a = OpenBook(); a.Perform(ChronicleBookAction.Open, Conf); Assert.IsFalse(a.Busy); Assert.AreEqual(180f, a.Frame.coverAngle);
            var c = new ChronicleBookAnimator(); c.Perform(ChronicleBookAction.Close, Conf); Assert.IsFalse(c.Busy); Assert.AreEqual(0f, c.Frame.coverAngle);
        }

        [Test] public void ClosingMidFlipSettlesTheSheetFirst()
        {
            var a = OpenBook(); a.Perform(ChronicleBookAction.TurnPageForward, Conf); a.Update(.3f); a.Perform(ChronicleBookAction.Close, Conf);
            Assert.AreEqual(0, a.Frame.flipDirection); Assert.AreEqual(1, a.Frame.spreadIndex);
            Run(a, a.timings.close + .1f); Assert.AreEqual(ChronicleBookPose.Closed, a.Frame.pose);
        }

        [Test] public void SetPoseImmediateSnapsWithoutAnimation()
        {
            var a = new ChronicleBookAnimator(); a.Perform(ChronicleBookAction.Open, Conf); a.Update(.4f);
            a.SetPoseImmediate(ChronicleBookPose.Open, 3);
            var f = a.Frame; Assert.AreEqual(180f, f.coverAngle); Assert.AreEqual(3, f.spreadIndex); Assert.IsFalse(f.busy); Assert.AreEqual(1f, f.overlayAlpha);
            a.SetPoseImmediate(ChronicleBookPose.Closed, 0); Assert.AreEqual(0f, a.Frame.coverAngle); Assert.IsFalse(a.Frame.overlayVisible);
        }

        [Test] public void ReactionsGlowAndTremorThenSettle()
        {
            var a = OpenBook(); a.Perform(ChronicleBookAction.ReactToCorrection, ChronicleNarratorState.DoubtingHistorian); a.Update(Dt);
            var f = a.Frame; Assert.Greater(f.glow, .5f); Assert.IsTrue(Math.Abs(f.tremorX) > 0f || Math.Abs(f.tremorZ) > 0f);
            a.Perform(ChronicleBookAction.MajorMagic, Conf); a.Update(Dt); Assert.Greater(a.Frame.glow, 1.2f, "a major reaction is bigger");
            Run(a, 2.5f); var calm = a.Frame; Assert.AreEqual(0f, calm.glow, .001f); Assert.AreEqual(0f, calm.tremorZ, .0001f);
        }

        [Test] public void ReducedMotionRemovesTremorAndFloat()
        {
            var a = OpenBook(); a.reduceMotion = true; a.Perform(ChronicleBookAction.MajorMagic, ChronicleNarratorState.DoubtingHistorian); a.Update(Dt);
            var f = a.Frame; Assert.AreEqual(0f, f.tremorX); Assert.AreEqual(0f, f.tremorZ); Assert.AreEqual(0f, f.bob); Assert.Greater(f.glow, 1f, "the glow stays; only motion is removed");
        }

        [Test] public void AnIdleOpenBookFloatsGently()
        {
            var a = OpenBook(); var min = 1f; var max = -1f;
            for (float t = 0; t < 6f; t += Dt) { a.Update(Dt); var b = a.Frame.bob; min = Math.Min(min, b); max = Math.Max(max, b); }
            Assert.Less(min, 0f); Assert.Greater(max, 0f); Assert.Less(max, .03f);
        }

        // ---------------------------------------------------------------- the sequencer driving a rig
        sealed class Harness
        {
            public ChronicleBookAnimator book = new ChronicleBookAnimator(); public ChronicleRigStage stage; public ChronicleSequencer seq;
            public List<string> audio = new List<string>(); public bool silenced;
            public Harness(string sceneId, ChronicleBookPose pose)
            {
                stage = new ChronicleRigStage(book) { audio = (c, v) => audio.Add(c), stopAudio = () => silenced = true };
                book.SetPoseImmediate(pose, 0);
                seq = new ChronicleSequencer(ChronicleScripts.Get(sceneId), stage, new ChronicleStaticContext(), null);
                seq.Start(pose);
            }
            public float RunToEnd(Action<ChronicleBookFrame> perFrame = null)
            {
                float t = 0; while (!seq.Finished && t < 400f) { seq.Update(Dt); book.Update(Dt); perFrame?.Invoke(book.Frame); t += Dt; }
                Assert.IsTrue(seq.Finished); return t;
            }
        }

        [Test] public void TheOpeningDrivesTheBookOpenThroughEveryPageTurnAndShutAgain()
        {
            var h = new Harness(ChronicleCatalog.OpeningSceneId, ChronicleBookPose.Closed);
            var coverWasOpenWhileTurning = true; var turns = 0; var wasFlipping = false;
            h.RunToEnd(f => { if (f.flipDirection != 0) { if (!wasFlipping) turns++; if (f.coverAngle != 180f) coverWasOpenWhileTurning = false; } wasFlipping = f.flipDirection != 0; });
            Assert.IsTrue(coverWasOpenWhileTurning); Assert.AreEqual(2, turns, "the opening turns two pages");
            for (var i = 0; i < 120; i++) h.book.Update(Dt);
            Assert.AreEqual(ChronicleBookPose.Closed, h.book.Frame.pose); Assert.AreEqual(0f, h.book.Frame.coverAngle);
        }

        [Test] public void TheBookAndTheStoryStayInStepPageForPage()
        {
            var h = new Harness("MEM_14", ChronicleBookPose.Open);
            h.RunToEnd(f => Assert.AreEqual(180f, f.coverAngle));
            Assert.AreEqual(h.seq.State.spreadIndex, h.book.Frame.spreadIndex, "the book shows the spread the story is on");
            Assert.IsFalse(h.book.Busy, "the sequencer waited for the rig, so nothing is still animating");
        }

        [Test] public void SkippingSnapsTheBookToTheFinalPose()
        {
            var h = new Harness(ChronicleCatalog.OpeningSceneId, ChronicleBookPose.Closed);
            for (var i = 0; i < 400; i++) { h.seq.Update(Dt); h.book.Update(Dt); }
            Assert.AreEqual(ChronicleBookPose.Open, h.book.Frame.pose);
            h.seq.Skip(); var f = h.book.Frame;
            Assert.AreEqual(ChronicleBookPose.Closed, f.pose); Assert.AreEqual(0f, f.coverAngle); Assert.IsFalse(f.busy); Assert.IsTrue(h.silenced);
            var memory = new Harness("MEM_07", ChronicleBookPose.Closed); memory.seq.Update(Dt * 5); memory.seq.Skip();
            Assert.AreEqual(ChronicleBookPose.Open, memory.book.Frame.pose); Assert.AreEqual(180f, memory.book.Frame.coverAngle);
        }

        [Test] public void ACorrectionMakesTheBookReact()
        {
            var h = new Harness("MEM_07", ChronicleBookPose.Open); var maxGlow = 0f;
            h.RunToEnd(f => maxGlow = Math.Max(maxGlow, f.glow));
            Assert.Greater(maxGlow, .5f, "the red strike and the magic make the book glow");
            Assert.IsTrue(h.audio.Contains("red_ink"));
        }

        [Test] public void EveryMemoryPlaysThroughTheRigWithTheCoversNeverMoving()
        {
            foreach (var m in ChronicleCatalog.Memories)
            {
                var h = new Harness(m.id, ChronicleBookPose.Open);
                h.RunToEnd(f => { Assert.AreEqual(180f, f.coverAngle, m.id); });
                Assert.AreEqual(ChronicleBookPose.Open, h.book.Frame.pose, m.id);
            }
        }

        [Test] public void TheOldSpreadIsKeptForTheTurningSheet()
        {
            var h = new Harness("MEM_14", ChronicleBookPose.Open); Assert.IsNull(h.seq.State.outgoing);
            float t = 0; while (!h.seq.Finished && h.seq.State.outgoing == null && t < 200f) { h.seq.Update(Dt); h.book.Update(Dt); t += Dt; }
            var o = h.seq.State.outgoing; Assert.IsNotNull(o, "a turn happened");
            Assert.AreEqual(0, o.spread); Assert.Greater(o.blocks.Count, 0); Assert.IsTrue(o.blocks.All(b => b.spread == 0));
            Assert.Greater(o.illustration.actors.Count, 0, "the old illustration survives the reset");
            Assert.AreEqual(0, h.seq.State.illustration.actors.Count, "while the new spread starts blank");
        }

        [Test] public void TextPreferencesAreSavedAndFeedTheSettings()
        {
            var p = new ChronicleProgress { instantText = true, textSpeed = 1.5f };
            var s = p.ToSettings(true, false);
            Assert.IsTrue(s.instantText); Assert.AreEqual(1.5f, s.textSpeed); Assert.IsTrue(s.reduceMotion); Assert.IsFalse(s.reduceFlashing);
            p.ResetAll(); Assert.IsTrue(p.instantText); Assert.AreEqual(1.5f, p.textSpeed, "resetting story progress keeps accessibility choices");
            var bad = new ChronicleProgress { textSpeed = 99f }; bad.Ensure(); Assert.AreEqual(1f, bad.textSpeed);
            var copy = ChronicleProgressTests.FromJson(ChronicleProgressTests.ToJson(new ChronicleProgress { instantText = true, textSpeed = .75f })); copy.Ensure();
            Assert.IsTrue(copy.instantText); Assert.AreEqual(.75f, copy.textSpeed);
        }
    }
}
