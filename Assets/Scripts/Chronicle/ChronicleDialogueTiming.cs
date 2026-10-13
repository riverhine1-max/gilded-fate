using System;

namespace GildedFate.Chronicle
{
    [Serializable]
    public sealed class ChronicleWordTiming
    {
        public string text;
        public int charStart, charLength;
        public float startSeconds, endSeconds;
    }

    [Serializable]
    public sealed class ChronicleDialogueTiming
    {
        public string lineId, sceneId, commandId, speaker, text, audio, emotion, correctionId;
        public bool ready;
        public float durationSeconds;
        public ChronicleWordTiming[] words;

        public bool Validate(string canonicalText, out string reason)
        {
            reason="";
            if (!ready || text != canonicalText) { reason="The recording is unavailable or its exact wording differs."; return false; }
            if (durationSeconds <= 0 || float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || words == null || words.Length == 0)
            { reason="Measured duration and word timestamps are required."; return false; }
            int end=0;float time=0;
            foreach(var word in words)
            {
                if(word == null || word.charLength <= 0 || word.charStart < end || word.charStart+word.charLength > text.Length
                    || float.IsNaN(word.startSeconds) || float.IsNaN(word.endSeconds) || float.IsInfinity(word.startSeconds) || float.IsInfinity(word.endSeconds)
                    || word.startSeconds < time-.001f || word.endSeconds <= word.startSeconds || word.endSeconds > durationSeconds+.001f)
                { reason="Invalid word range or measured timestamp.";return false; }
                for(int i=end;i<word.charStart;i++)if(!char.IsWhiteSpace(text[i])){reason="A spoken word is missing.";return false;}
                if(text.Substring(word.charStart,word.charLength)!=word.text){reason="A word differs from the canonical page text.";return false;}
                end=word.charStart+word.charLength;time=word.endSeconds;
            }
            for(int i=end;i<text.Length;i++)if(!char.IsWhiteSpace(text[i])){reason="The recording omits the end of the line.";return false;}
            return true;
        }
    }

    /// <summary>Optional extension to Claude's existing stage. The audio source owns the clock.</summary>
    public interface IChronicleDialogueStage
    {
        ChronicleDialoguePlayback BeginDialogue(string sceneId,string commandId,string text,string speaker,ChronicleNarratorState narrator);
        void SetDialoguePaused(bool paused);
    }

    public sealed class ChronicleDialoguePlayback
    {
        public ChronicleDialogueTiming timing;
        public Func<float> positionSeconds;
    }

    public static class ChronicleSpokenHandwriting
    {
        public static void Step(ChronicleTextBlock block,bool instantText)
        {
            var playback=block.dialogue;
            var time=Math.Max(0,playback.positionSeconds());
            int cursor=0;block.strokeIndex=-1;block.strokeProgress=0f;
            foreach(var word in playback.timing.words)
            {
                if(time < word.startSeconds)break; // include the full dramatic silence between words
                if(time >= word.endSeconds)cursor=word.charStart+word.charLength;
                else
                {
                    var progress=(time-word.startSeconds)/(word.endSeconds-word.startSeconds);
                    int letters=word.charLength;
                    while(letters>0 && char.IsPunctuation(word.text[letters-1]))letters--;
                    var inkPosition=progress*letters;
                    block.strokeIndex=word.charStart+Math.Min(Math.Max(0,letters-1),(int)Math.Floor(inkPosition));
                    block.strokeProgress=inkPosition-(float)Math.Floor(inkPosition);
                    cursor=word.charStart+Math.Min(letters,1+(int)Math.Floor(inkPosition));
                    break;
                }
            }
            block.cursor=instantText?block.text.Length:Math.Min(block.text.Length,cursor);
            if(instantText){block.strokeIndex=-1;block.strokeProgress=0f;}
            block.complete=time>=playback.timing.durationSeconds;
            if(block.complete)ChronicleHandwriting.Complete(block);
        }
    }
}
