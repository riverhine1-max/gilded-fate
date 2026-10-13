using System;
using GildedFate.Audio;
using GildedFate.Chronicle;
using UnityEngine;

namespace GildedFate.ChronicleBook
{
    /// <summary>Plays verified recordings and exposes their sample clock to the existing handwriting engine.</summary>
    public sealed class ChronicleDialogueAudio : MonoBehaviour
    {
        [Serializable] sealed class Index { public ChronicleDialogueTiming[] lines; }
        AudioSource source;
        ChronicleDialogueTiming[] lines=Array.Empty<ChronicleDialogueTiming>();
        int generation;
        bool paused;
        public static float VoiceVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat("Chronicle.VoiceVolume",1));
            set { PlayerPrefs.SetFloat("Chronicle.VoiceVolume",Mathf.Clamp01(value));PlayerPrefs.Save(); }
        }
        void Awake()
        {
            source=gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;source.spatialBlend=0;source.pitch=1;source.priority=0;
            var data=Resources.Load<TextAsset>("Chronicle/Audio/DialogueIndex");
            if(data)lines=JsonUtility.FromJson<Index>(data.text)?.lines??Array.Empty<ChronicleDialogueTiming>();
        }
        public ChronicleDialoguePlayback Begin(string sceneId,string commandId,string text,string speaker,ChronicleNarratorState narrator)
        {
            foreach(var line in lines)
            {
                if(!line.ready || line.speaker!=speaker || line.text!=text
                    || (!string.IsNullOrEmpty(line.sceneId) && line.sceneId!=sceneId)
                    || (!string.IsNullOrEmpty(line.commandId) && line.commandId!=commandId))continue;
                if(!line.Validate(text,out var reason)) { Debug.LogWarning("[Chronicle] Refusing dialogue "+line.lineId+": "+reason);continue; }
                var clip=Resources.Load<AudioClip>(line.audio);
                if(!clip || Math.Abs(clip.length-line.durationSeconds)>.025f)
                { Debug.LogWarning("[Chronicle] Missing or changed recording for "+line.lineId+"; realignment is required.");continue; }
                Stop();int take=generation;
                source.clip=clip;source.pitch=1;source.loop=false;source.volume=VoiceVolume;
                double start=AudioSettings.dspTime+.02;
                source.PlayScheduled(start);
                AdaptiveMusicDirector.Duck(.72f,clip.length+.3f);
                return new ChronicleDialoguePlayback
                {
                    timing=line,
                    positionSeconds=()=>
                    {
                        if(take!=generation)return line.durationSeconds;
                        if(paused)return source.timeSamples/(float)clip.frequency;
                        if(AudioSettings.dspTime<start)return 0;
                        if(!source.isPlaying && AudioSettings.dspTime-start>=clip.length)return line.durationSeconds;
                        return source.timeSamples/(float)clip.frequency;
                    }
                };
            }
            return null; // no recording: the established readable, silent handwriting remains available
        }
        public void Stop() { generation++;paused=false;if(source)source.Stop(); }
        public void SetPaused(bool value)
        {
            if(paused==value || !source)return;
            paused=value;
            if(paused)source.Pause();else source.UnPause();
        }
        void Update() { if(source)source.volume=VoiceVolume; }
        void OnDisable() => Stop();
    }
}
