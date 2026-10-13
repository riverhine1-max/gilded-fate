using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    [TestFixture]
    public sealed class ChronicleFixedWritingTests
    {
        static ChroniclePageMetrics Small(bool auto = true) => new ChroniclePageMetrics {
            fontSize = 34f, lineHeight = 40f, textHeight = 54f, paragraphGap = 14f,
            uniformTextSize = true, autoPageTurns = auto
        };
        static ChronicleSequencer Make(ChronicleScene scene, IChronicleStage stage = null, ChroniclePageMetrics m = null, bool instantText = false)
        {
            var seq = new ChronicleSequencer(scene, stage, null, null,
                new ChronicleSettings { instantText = instantText, textSpeed = 4f }, m ?? Small());
            seq.Start(ChronicleBookPose.Open); return seq;
        }
        static void Run(ChronicleSequencer seq)
        {
            for (var i = 0; !seq.Finished && i < 30000; i++) seq.Update(.1f);
            Assert.IsTrue(seq.Finished, seq.Scene.id);
        }
        sealed class Voices : IChronicleStage, IChronicleDialogueStage
        {
            public readonly List<string> events = new List<string>();
            public float BookActionDuration(ChronicleBookAction a) => 1f;
            public void BookAction(ChronicleBookAction a, ChronicleNarratorState n) { events.Add("turn"); }
            public void SetBookPoseImmediate(ChronicleBookPose p, int s) { }
            public void PlayAudio(string c, float v) { }
            public void StopAllAudio() { }
            public void MagicEffect(string e, float x, float y, float i) { }
            public void SceneStarted(string id) { }
            public void SceneEnded(string id, bool skipped) { }
            public void SetDialoguePaused(bool paused) { }
            public ChronicleDialoguePlayback BeginDialogue(string sceneId, string id, string text, string speaker, ChronicleNarratorState n)
            { events.Add(id); return null; }
        }
        [Test] public void FullPageTurnsWithoutResizingOrLosingCanonicalText()
        {
            var scene = new ChronicleSceneBuilder("test", "test").Open().Say("First sentence.").Say("Second sentence.").Finish();
            var stage = new RecordingChronicleStage(); var seq = Make(scene, stage); Run(seq);
            Assert.AreEqual(0, seq.State.overflowCount);
            Assert.AreEqual(1, stage.Count("book:TurnPageForward"));
            Assert.AreEqual("First sentence.|Second sentence.", string.Join("|", seq.State.transcript.Select(b => b.text)));
            Assert.AreEqual(34f, Small().fontSize);
            Assert.AreEqual(1f, Small().ScaleFor(ChronicleTextStyle.Heading));
            Assert.AreEqual(1f, Small().ScaleFor(ChronicleTextStyle.Ending));
        }
        [Test] public void VoiceAndInkWaitForTheFreshPageIncludingInstantText()
        {
            foreach (var instant in new[] { false, true })
            {
                var stage = new Voices(); var seq = Make(new ChronicleSceneBuilder("test", "test").Open().Say("First.").Say("Second.").Finish(), stage, null, instant);
                for (var i = 0; seq.State.spreadIndex == 0 && i < 100; i++) seq.Update(.1f);
                Assert.AreEqual("b1|turn", string.Join("|", stage.events));
                Assert.AreEqual(0, seq.State.blocks[0].cursor);
                seq.Pause(); seq.Update(10f);
                Assert.AreEqual(2, stage.events.Count);
                seq.Resume(); seq.Update(.5f);
                Assert.AreEqual(0, seq.State.blocks[0].cursor);
                seq.Update(.6f);
                Assert.AreEqual("b1|turn|b2", string.Join("|", stage.events));
                Run(seq);
            }
        }
        [Test] public void SkipDuringAutomaticTurnDoesNotStartThePendingVoice()
        {
            var stage = new Voices(); var seq = Make(new ChronicleSceneBuilder("test", "test").Open().Say("First.").Say("Second.").Finish(), stage);
            for (var i = 0; seq.State.spreadIndex == 0 && i < 100; i++) seq.Update(.1f);
            seq.Skip();
            Assert.AreEqual("b1|turn", string.Join("|", stage.events));
            Assert.IsTrue(seq.Finished); Assert.IsTrue(seq.State.transcript.All(b => b.complete));
        }
        [Test] public void AutomaticTurnsKeepIllustrationActorsAndAuthoredTurnsStillClearThem()
        {
            var scene = new ChronicleSceneBuilder("test", "test").Open().Illustrate("castle", 0f)
                .Hero("hero", ChronicleHero.Vanguard, .5f, .5f).Say("First.").Say("Second.").Finish();
            var seq = Make(scene); Run(seq);
            Assert.AreEqual("castle", seq.State.illustration.backdrop);
            Assert.IsNotNull(seq.State.illustration.Find("hero"));
            Assert.IsNotNull(seq.State.outgoing.illustration.Find("hero"));
            var authored = Make(new ChronicleSceneBuilder("test", "test").Open().Illustrate("castle", 0f)
                .Hero("hero", ChronicleHero.Vanguard, .5f, .5f).Turn().Say("Fresh.").Finish()); Run(authored);
            Assert.IsNull(authored.State.illustration.Find("hero"));
        }
        [Test] public void TextOnlyUsesBothPagesBeforeTurning()
        {
            var seq = Make(new ChronicleSceneBuilder("test", "test").Open(ChronicleSpreadLayout.TextOnly)
                .Say("First.").Say("Second.").Say("Third.").Finish()); Run(seq);
            Assert.AreEqual(ChroniclePageSide.Left, seq.State.transcript[0].side);
            Assert.AreEqual(ChroniclePageSide.Right, seq.State.transcript[1].side);
            Assert.AreEqual(ChroniclePageSide.Left, seq.State.transcript[2].side);
            Assert.AreEqual(1, seq.State.spreadIndex); Assert.AreEqual(0, seq.State.overflowCount);
        }
        [Test] public void CorrectionSequenceMovesTogetherToFreshPaper()
        {
            var metrics = Small(); metrics.textHeight = 175f; metrics.textWidth = 2000f;
            var seq = Make(new ChronicleSceneBuilder("test", "test").Open().Say("Earlier.")
                .Original("CORR_01").Say("Wait.").Correct("CORR_01").Finish(), null, metrics);
            Run(seq);
            var original = seq.State.transcript.First(b => b.id == "corr:CORR_01");
            var rewrite = seq.State.transcript.First(b => b.id == "fix:CORR_01");
            Assert.AreEqual(1, original.spread); Assert.AreEqual(original.spread, rewrite.spread);
            Assert.IsTrue(seq.State.blocks.Contains(original)); Assert.IsTrue(original.struck);
            Assert.AreEqual(0, seq.State.overflowCount);
        }
        [Test] public void LongCorrectionReturnsToOriginalInkAndUsesReservedRewriteSpace()
        {
            var metrics = Small(); metrics.textHeight = 165f; metrics.textWidth = 2000f;
            var scene = new ChronicleSceneBuilder("test", "test").Open().Say("Earlier.")
                .Original("CORR_01").Say("One.").Say("Two.").Say("Three.").Correct("CORR_01").Finish();
            var stage = new RecordingChronicleStage(); var seq = Make(scene, stage, metrics);
            for (var i = 0; stage.Count("book:TurnPageBack") == 0 && i < 1000; i++) seq.Update(.1f);
            var original = seq.State.transcript.First(b => b.id == "corr:CORR_01");
            Assert.IsFalse(original.struck, "Wait for the original page to land before striking it");
            seq.Pause(); seq.Update(10f); Assert.IsFalse(original.struck); seq.Resume(); Run(seq);
            Assert.AreEqual(1, stage.Count("book:TurnPageBack"));
            Assert.IsTrue(original.struck); Assert.IsTrue(seq.State.blocks.Contains(original));
            Assert.AreEqual(original.spread, seq.State.transcript.First(b => b.id == "fix:CORR_01").spread);
            Assert.AreEqual(6, seq.State.transcript.Count); Assert.AreEqual(0, seq.State.overflowCount);
            var skipped = Make(scene, null, metrics); skipped.Skip();
            Assert.AreEqual(seq.State.spreadIndex, skipped.State.spreadIndex); Assert.AreEqual(0, skipped.State.overflowCount);
        }
        [Test] public void AllExistingStoriesCompleteWithFixedWritingAndPreserveSkipResults()
        {
            var p = new ChronicleProgress(); p.UnlockAllForDevelopment();
            foreach (var id in ChronicleScripts.Ids)
            {
                var m = new ChroniclePageMetrics { fontSize = 34f, lineHeight = 44f, uniformTextSize = true, autoPageTurns = true };
                var played = new ChronicleSequencer(ChronicleScripts.Get(id), null, new ChronicleProgressContext(p), null, new ChronicleSettings { textSpeed = 4f }, m);
                played.Start(); Run(played);
                var skipped = new ChronicleSequencer(ChronicleScripts.Get(id), null, new ChronicleProgressContext(p), null, null, m);
                skipped.Start(); skipped.Skip();
                Assert.AreEqual(0, played.State.overflowCount, id);
                Assert.AreEqual(played.State.spreadIndex, skipped.State.spreadIndex, id);
                Assert.AreEqual(string.Join("|", played.State.transcript.Select(b => b.id + b.text + b.struck + b.erased)),
                    string.Join("|", skipped.State.transcript.Select(b => b.id + b.text + b.struck + b.erased)), id);
            }
        }
        [Test] public void AdminOpensAllMemoriesAndSecretWithoutFakingProgress()
        {
            var p = new ChronicleProgress { textSpeed = 1.5f, openingMode = 2, instantText = true };
            p.runsEnded[0] = 2; p.wins[1] = 1; p.discoveredFacts.Add("existing"); p.corrections.Add("existing");
            p.viewedMemories.Add("MEM_01"); p.unlockedMemories.Add("MEM_01");
            var result = p.UnlockAllForDevelopment();
            Assert.AreEqual(26, result.newMemories.Count); Assert.IsTrue(result.secretUnlocked);
            Assert.AreEqual(27, p.TotalUnlocked); Assert.IsTrue(p.secretUnlocked); Assert.IsFalse(p.secretCompleted);
            Assert.AreEqual(1, p.TotalViewed); Assert.AreEqual(1, p.discoveredFacts.Count); Assert.AreEqual(1, p.corrections.Count);
            Assert.AreEqual(2, p.runsEnded[0]); Assert.AreEqual(1, p.wins[1]);
            Assert.AreEqual(1.5f, p.textSpeed); Assert.AreEqual(2, p.openingMode); Assert.IsTrue(p.instantText);
            Assert.IsFalse(p.UnlockAllForDevelopment().Any);
            Assert.AreEqual(p.pendingNotices.Count, p.pendingNotices.Distinct().Count());
            for (var chapter = 1; chapter <= 9; chapter++) Assert.AreEqual(3, p.ChapterUnlockedCount(chapter));
            foreach (var memory in ChronicleCatalog.Memories) Assert.IsNotNull(ChronicleArchive.Reading(memory.id, p));
        }
    }
}
