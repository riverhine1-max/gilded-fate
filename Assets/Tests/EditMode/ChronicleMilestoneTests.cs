using System;
using System.Linq;
using GildedFate.Chronicle;
using NUnit.Framework;

namespace GildedFate.Tests
{
    [TestFixture]
    public sealed class ChronicleMilestoneTests
    {
        [Test] public void BossClearPlaysEarnedMemoriesForOnlyTheCurrentHero()
        {
            foreach (ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero)))
            {
                var p = new ChronicleProgress(); p.RegisterBossDefeated(hero, 1, 18);
                var scenes = ChronicleMilestoneScenes.Memories(p, hero, 1, false);
                Assert.AreEqual(2, scenes.Count);
                Assert.AreEqual(ChronicleCatalog.MemoryFor(2, hero).id, scenes[0]);
                Assert.AreEqual(ChronicleCatalog.MemoryFor(3, hero).id, scenes[1]);
            }
        }
        [Test] public void WatchedActBoundaryStillPlaysWithoutGrantingAnotherUnlock()
        {
            var p = new ChronicleProgress(); p.RegisterBossDefeated(ChronicleHero.Hexer, 2, 36);
            foreach (var memory in p.unlockedMemories.ToArray()) p.CommitMemoryViewed(memory);
            var unlocked = p.TotalUnlocked;
            var scenes = ChronicleMilestoneScenes.Memories(p, ChronicleHero.Hexer, 2, false);
            Assert.AreEqual(1, scenes.Count); Assert.AreEqual("MEM_17", scenes[0]); Assert.AreEqual(unlocked, p.TotalUnlocked);
        }
        [Test] public void FirstDeathRecoversTheCurrentHerosFirstMemory()
        {
            var p = new ChronicleProgress(); p.RegisterRunEnded(ChronicleHero.Reaper, false, 1, 2, "first");
            Assert.AreEqual("MEM_03", ChronicleMilestoneScenes.Memories(p, ChronicleHero.Reaper, 1, true).Single());
        }
        [Test] public void AdministrativeUnlocksDoNotPlayFutureChaptersOrChapterX()
        {
            var p = new ChronicleProgress(); p.UnlockAllForDevelopment(); p.RegisterBossDefeated(ChronicleHero.Vanguard, 1, 18);
            var scenes = ChronicleMilestoneScenes.Memories(p, ChronicleHero.Vanguard, 1, false);
            Assert.AreEqual(2, scenes.Count);
            Assert.IsTrue(scenes.All(id => ChronicleCatalog.FindMemory(id).chapter <= 3));
            Assert.IsFalse(scenes.Contains(ChronicleCatalog.SecretSceneId));
        }
        [Test] public void RunEndChoosesExactlyOneExistingBeingLine()
        {
            var alternatives = new[] { "Not yet.", "Nothing was meant to be lost.", "The world is still broken." };
            foreach (ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero)))
            foreach (var victory in new[] { true, false })
            for (var variant = -4; variant <= 7; variant++)
            {
                var scene = ChronicleMilestoneScenes.Ending(victory, hero, variant);
                var spoken = scene.commands.Where(c => c.op == ChronicleOp.ShowNarration || c.style == ChronicleTextStyle.DeeperVoice).ToArray();
                var expected = victory ? alternatives[0] : alternatives[(variant % 3 + 3) % 3];
                Assert.AreEqual(1, spoken.Count(c => c.style == ChronicleTextStyle.DeeperVoice));
                Assert.IsTrue(spoken.Select(c => c.text).SequenceEqual(new[] { "And there, at last, the tale came to—", expected, "I did not write that.", "Then there is more." }));
                Assert.IsTrue(spoken.Select(c => c.id).SequenceEqual(new[] { "b2", "b3", "b4", "b5" }));
                var seq = new ChronicleSequencer(scene, null, null, null, new ChronicleSettings(), new ChroniclePageMetrics { uniformTextSize = true, autoPageTurns = true });
                seq.Start(); seq.Skip();
                Assert.AreEqual(1, seq.State.transcript.Count(b => b.style == ChronicleTextStyle.DeeperVoice));
                Assert.IsTrue(seq.State.transcript.Single(b => b.id == "run_conclusion").struck);
            }
        }
        [Test] public void EndingWritesAndStrikesTheUnspokenConclusionBeforeTheBeingSpeaks()
        {
            var commands = ChronicleMilestoneScenes.Ending(false, ChronicleHero.Vanguard).commands;
            int write = commands.FindIndex(c => c.id == "run_conclusion" && c.op == ChronicleOp.WriteText);
            int strike = commands.FindIndex(c => c.id == "run_conclusion" && c.op == ChronicleOp.CrossOutText);
            int being = commands.FindIndex(c => c.style == ChronicleTextStyle.DeeperVoice);
            int hush = commands.FindIndex(c => c.op == ChronicleOp.PlayAudio && c.id == "ending_hush");
            Assert.IsTrue(write < hush && hush < strike && strike < being);
            Assert.AreEqual("The End", commands[write].text);
        }
        [Test] public void SharedEndingCannotUnlockOrCommitReapersLore()
        {
            var scene = ChronicleMilestoneScenes.Ending(true, ChronicleHero.Hexer);
            Assert.IsFalse(scene.commands.Any(c => c.op == ChronicleOp.SaveCorrection || c.op == ChronicleOp.DiscoverFact || c.op == ChronicleOp.WriteCorrection));
            Assert.IsFalse(scene.id == "MEM_24");
        }
        [Test] public void SkippingEndingKeepsCompletedWritingAndTheRedStrike()
        {
            var seq = new ChronicleSequencer(ChronicleMilestoneScenes.Ending(false, ChronicleHero.Reaper), null, null, null,
                new ChronicleSettings(), new ChroniclePageMetrics { uniformTextSize = true, autoPageTurns = true });
            seq.Start(); seq.Skip();
            Assert.IsTrue(seq.State.transcript.All(b => b.complete));
            Assert.IsTrue(seq.State.transcript.Single(b => b.id == "run_conclusion").struck);
            Assert.IsTrue(seq.State.transcript.Any(b => b.text == "Not yet."));
        }
        [Test] public void InvalidMilestoneArgumentsDoNotSelectStories()
        {
            Assert.AreEqual(0, ChronicleMilestoneScenes.Memories(new ChronicleProgress(), ChronicleHero.Hexer, 0, false).Count);
            Assert.AreEqual(0, ChronicleMilestoneScenes.Memories(null, ChronicleHero.Hexer, 1, false).Count);
            Assert.AreEqual(0, ChronicleMilestoneScenes.Memories(new ChronicleProgress(), (ChronicleHero)99, 1, false).Count);
        }
        [Test] public void CompletedMemoriesAreNotRepeatedOnEveryDeath()
        {
            var p = new ChronicleProgress(); p.RegisterRunEnded(ChronicleHero.Vanguard, false, 1, 5, "death");
            p.CommitMemoryViewed("MEM_01");
            Assert.AreEqual(0, ChronicleMilestoneScenes.Memories(p, ChronicleHero.Vanguard, 1, true).Count);
            Assert.IsNotNull(ChronicleMilestoneScenes.Ending(false, ChronicleHero.Vanguard));
        }
    }
}
