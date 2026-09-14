using System;
using System.Collections.Generic;
using UnityEngine;

namespace GildedFate.Audio
{
    public sealed class GameAudio : MonoBehaviour
    {
        private sealed class Bank { public AudioClip[] clips; public int previous=-1; public float last=-100; }
        private sealed class Voice { public AudioSource source; public SoundCue cue; public float gain,started; public bool combat,paused; }
        private struct Pending { public SoundCue cue; public float at,pan,intensity; public bool combat; public int generation; }
        private static GameAudio instance;
        private readonly Dictionary<SoundCue,Bank> banks=new();
        private readonly List<Voice> voices=new();
        private readonly List<Pending> pending=new();
        private readonly System.Random variation=new System.Random(90731);
        private float effects=.85f,ui=.8f,mixSafety=1;
        private bool combatPaused;
        private int generation;
        public static int PlayedCount {get;private set;}
        public static int MissingClips {get;private set;}
        public static int LastVariation {get;private set;}=-1;
        public static SoundCue LastCue {get;private set;}
        private static readonly int[] cueCounts=new int[Enum.GetValues(typeof(SoundCue)).Length];
        public static int CueCount(SoundCue cue)=>cueCounts[(int)cue];
        public static int PendingCount=>instance?instance.pending.Count:0;
        public static int VoiceCount=>instance?instance.voices.Count:0;

        public static void Initialize()
        {
            if(instance)return;
            var host=new GameObject("Gilded Fate · Sound Stage");DontDestroyOnLoad(host);instance=host.AddComponent<GameAudio>();
        }
        private void Awake()
        {
            if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;
            for(var i=0;i<24;i++)
            {
                var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;
                voices.Add(new Voice{source=source});
            }
            foreach(SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                var clips=new AudioClip[SoundCatalog.Variations];
                for(var i=0;i<clips.Length;i++)
                {
                    clips[i]=Resources.Load<AudioClip>("Audio/SFX/"+cue+"_"+(i+1));
                    if(!clips[i]){MissingClips++;Debug.LogError("[Gilded Fate Audio] Missing cue "+cue+" take "+(i+1));}
                    else clips[i].LoadAudioData(); // Decode before the first hit, never on its impact frame.
                }
                banks[cue]=new Bank{clips=clips};
            }
        }
        public static void SetVolumes(float master,float music,float effects,float ui)
        {
            Initialize();AudioListener.volume=Mathf.Clamp01(master);instance.effects=Mathf.Clamp01(effects);instance.ui=Mathf.Clamp01(ui);
            AdaptiveMusicDirector.SetVolume(music);
        }
        public static void Play(SoundCue cue,float intensity=1,float pan=0,bool combat=false,float delay=0)
        {
            Initialize();
            if(delay>0||combat&&instance.combatPaused)
            {
                if(instance.pending.Count<128)instance.pending.Add(new Pending{cue=cue,intensity=intensity,pan=pan,combat=combat,at=Time.unscaledTime+Mathf.Max(0,delay),generation=instance.generation});
                return;
            }
            instance.StartVoice(cue,intensity,pan,combat);
        }
        private void StartVoice(SoundCue cue,float intensity,float pan,bool combat)
        {
            if(!banks.TryGetValue(cue,out var bank))return;var spec=SoundCatalog.Get(cue);var now=Time.unscaledTime;
            if(now-bank.last<spec.interval)return;
            if((spec.ui?ui:effects)<=0||AudioListener.volume<=0)return;
            Voice slot=null,oldestSame=null,replace=null;var same=0;
            foreach(var voice in voices)
            {
                if(!voice.source.isPlaying&&!voice.paused){slot??=voice;continue;}
                if(voice.cue==cue){same++;if(oldestSame==null||voice.started<oldestSame.started)oldestSame=voice;}
                if(!voice.paused&&SoundCatalog.Get(voice.cue).priority<=spec.priority&&(replace==null||voice.started<replace.started))replace=voice;
            }
            if(same>=spec.limit)slot=oldestSame;else slot??=replace;if(slot==null)return;
            var take=bank.previous<0?variation.Next(bank.clips.Length):(bank.previous+1+variation.Next(bank.clips.Length-1))%bank.clips.Length;
            var clip=bank.clips[take];if(!clip)return;
            bank.previous=take;bank.last=now;slot.source.Stop();slot.paused=false;slot.cue=cue;slot.combat=combat;slot.started=now;
            slot.gain=spec.gain*Mathf.Clamp(intensity,.35f,1.15f)*(float)(.975+variation.NextDouble()*.05)/Mathf.Sqrt(1+same*.35f);
            slot.source.clip=clip;slot.source.loop=false;slot.source.priority=256-spec.priority*20;
            slot.source.pitch=1+(float)(variation.NextDouble()*2-1)*spec.pitchSpread;slot.source.panStereo=Mathf.Clamp(pan,-.55f,.55f);
            slot.source.volume=0;slot.source.Play();RefreshMix(0);
            PlayedCount++;cueCounts[(int)cue]++;LastCue=cue;LastVariation=take;
            if(spec.priority>=8)AdaptiveMusicDirector.Duck(spec.priority>=9?.30f:.12f,spec.priority>=9?1.2f:.32f);
        }
        public static void SetCombatPaused(bool paused)
        {
            if(!instance||instance.combatPaused==paused)return;instance.combatPaused=paused;
            foreach(var voice in instance.voices)if(voice.combat)
            {
                if(paused&&voice.source.isPlaying){voice.source.Pause();voice.paused=true;}
                else if(!paused&&voice.paused){voice.source.UnPause();voice.paused=false;}
            }
        }
        public static void ClearCombat()
        {
            if(!instance)return;instance.generation++;instance.pending.RemoveAll(p=>p.combat);
            foreach(var voice in instance.voices)if(voice.combat){voice.source.Stop();voice.paused=false;}
            instance.combatPaused=false;
        }
        private void Update()
        {
            var dt=Time.unscaledDeltaTime;var now=Time.unscaledTime;
            for(var i=0;i<pending.Count;)
            {
                var p=pending[i];
                if(p.combat&&p.generation!=generation){pending.RemoveAt(i);continue;}
                if(p.combat&&combatPaused){p.at+=dt;pending[i]=p;i++;continue;}
                if(p.at>now){i++;continue;}
                pending.RemoveAt(i);StartVoice(p.cue,p.intensity,p.pan,p.combat);
            }
            RefreshMix(dt);
        }
        private void RefreshMix(float dt)
        {
            var sum=0f;foreach(var voice in voices)if(voice.source.isPlaying)sum+=voice.gain*(SoundCatalog.Get(voice.cue).ui?ui:effects);
            // Conservative effects budget plus output trim; music has its own .24 ceiling.
            var target=Mathf.Min(1,.72f/Mathf.Max(.001f,sum));mixSafety=Mathf.Min(target,mixSafety+dt*2);
            foreach(var voice in voices)voice.source.volume=voice.gain*(SoundCatalog.Get(voice.cue).ui?ui:effects)*mixSafety*.45f;
        }
        private void OnDestroy(){if(instance==this)instance=null;}
    }
}
