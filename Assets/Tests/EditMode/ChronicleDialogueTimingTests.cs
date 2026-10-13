using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    [TestFixture]
    public sealed class ChronicleDialogueTimingTests
    {
        // These are synthetic test fixtures, not claimed timestamps from a recording.
        static ChronicleDialogueTiming Timing() => new ChronicleDialogueTiming
        {
            ready=true,text="Not yet.",durationSeconds=3,
            words=new[]{new ChronicleWordTiming{text="Not",charStart=0,charLength=3,startSeconds=.2f,endSeconds=.8f},
                new ChronicleWordTiming{text="yet.",charStart=4,charLength=4,startSeconds=2,endSeconds=2.8f}}
        };
        [Test] public void RecordedWordGapPausesTheInkAndTheAudioClockControlsIt()
        {
            float time=0;
            var block=new ChronicleTextBlock{text="Not yet.",dialogue=new ChronicleDialoguePlayback{timing=Timing(),positionSeconds=()=>time}};
            ChronicleSpokenHandwriting.Step(block,false);Assert.AreEqual(0,block.cursor);
            time=.2f;ChronicleSpokenHandwriting.Step(block,false);Assert.AreEqual(1,block.cursor);
            time=1;ChronicleSpokenHandwriting.Step(block,false);Assert.AreEqual(3,block.cursor);
            time=1.9f;ChronicleSpokenHandwriting.Step(block,false);Assert.AreEqual(3,block.cursor);
            time=2;ChronicleSpokenHandwriting.Step(block,false);Assert.AreEqual(5,block.cursor);
            time=2.9f;ChronicleSpokenHandwriting.Step(block,false);Assert.AreEqual(8,block.cursor);Assert.IsFalse(block.complete);
            time=3;ChronicleSpokenHandwriting.Step(block,false);Assert.IsTrue(block.complete);
        }
        [Test] public void SpokenInkMovesWithinALetterAndStopsDuringSilence()
        {
            float time=.25f;
            var block=new ChronicleTextBlock{text="Not yet.",dialogue=new ChronicleDialoguePlayback{timing=Timing(),positionSeconds=()=>time}};
            ChronicleSpokenHandwriting.Step(block,false);
            Assert.AreEqual(0,block.strokeIndex);Assert.Greater(block.strokeProgress,0f);Assert.Less(block.strokeProgress,1f);
            var first=block.strokeProgress;time=.3f;ChronicleSpokenHandwriting.Step(block,false);
            Assert.Greater(block.strokeProgress,first);
            time=1.5f;ChronicleSpokenHandwriting.Step(block,false);Assert.AreEqual(-1,block.strokeIndex);
            time=3;ChronicleSpokenHandwriting.Step(block,false);Assert.IsTrue(block.complete);Assert.AreEqual(-1,block.strokeIndex);
        }
        [Test] public void UnvoicedInkMovesBeforeTheWholeLetterIsRevealed()
        {
            var block=new ChronicleTextBlock();ChronicleHandwriting.Prepare(block,"Writing",new ChroniclePageMetrics());
            ChronicleHandwriting.Step(block,.001f,ChronicleNarratorStyle.For(ChronicleNarratorState.ConfidentHistorian),ChronicleSettings.Default);
            Assert.AreEqual(0,block.cursor);Assert.AreEqual(0,block.strokeIndex);Assert.Greater(block.strokeProgress,0f);
            ChronicleHandwriting.Complete(block);Assert.AreEqual(-1,block.strokeIndex);Assert.AreEqual(7,block.cursor);
        }
                [Test] public void ChangedWordingAndMissingTimingsAreRejected()
        {
            var line=Timing();Assert.IsTrue(line.Validate("Not yet.",out _));
            Assert.IsFalse(line.Validate("Not now.",out _));
            line.words[1].startSeconds=.1f;Assert.IsFalse(line.Validate("Not yet.",out _));
            line=Timing();line.words=new ChronicleWordTiming[0];Assert.IsFalse(line.Validate("Not yet.",out _));
            line=Timing();line.ready=false;Assert.IsFalse(line.Validate("Not yet.",out _));
        }
        [Test] public void InstantTextStillWaitsForTheActualRecordingToEnd()
        {
            float time=.3f;
            var block=new ChronicleTextBlock{text="Not yet.",dialogue=new ChronicleDialoguePlayback{timing=Timing(),positionSeconds=()=>time}};
            ChronicleSpokenHandwriting.Step(block,true);Assert.AreEqual(8,block.cursor);Assert.IsFalse(block.complete);
            time=3;ChronicleSpokenHandwriting.Step(block,true);Assert.IsTrue(block.complete);
        }
        [Test] public void AnOmittedWordCannotPassValidation()
        {
            var line=Timing();line.words=new[]{line.words[1]};Assert.IsFalse(line.Validate("Not yet.",out _));
        }
        [Test] public void PunctuationWaitsForTheSpokenWordToFinish()
        {
            float time=2.79f;
            var block=new ChronicleTextBlock{text="Not yet.",dialogue=new ChronicleDialoguePlayback{timing=Timing(),positionSeconds=()=>time}};
            ChronicleSpokenHandwriting.Step(block,false);Assert.AreEqual(7,block.cursor);
            time=2.81f;ChronicleSpokenHandwriting.Step(block,false);Assert.AreEqual(8,block.cursor);
        }
        [Test] public void SequencerWaitsForAudioAndSkipDoesNotStartRemainingRecordings()
        {
            int starts=0;bool stopped=false,paused=false;float time=0;
            var stage=new ChronicleRigStage(new ChronicleBookAnimator())
            {
                dialogue=(scene,command,text,speaker,narrator)=>{starts++;return new ChronicleDialoguePlayback{timing=Timing(),positionSeconds=()=>time};},
                stopAudio=()=>stopped=true,pauseDialogue=value=>paused=value
            };
            var scene=new ChronicleSceneBuilder("TEST","test").Say("Not yet.",0).Say("Not yet.",0).Finish();
            var sequence=new ChronicleSequencer(scene,stage,null,null,new ChronicleSettings{instantText=true});
            sequence.Start(ChronicleBookPose.Open);sequence.Update(.01f);
            Assert.AreEqual(1,starts);Assert.IsTrue(sequence.IsWriting);Assert.AreEqual(8,sequence.State.blocks[0].cursor);
            sequence.Update(100);Assert.AreEqual(1,starts);Assert.IsFalse(sequence.Finished);
            sequence.Pause();Assert.IsTrue(paused);sequence.Resume();Assert.IsFalse(paused);
            sequence.Skip();Assert.IsTrue(stopped);Assert.AreEqual(1,starts);Assert.IsTrue(sequence.Finished);
            Assert.AreEqual(2,sequence.State.transcript.Count);Assert.IsTrue(sequence.State.transcript[1].complete);
        }
    }
}
