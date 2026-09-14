using System.Collections.Generic;
using UnityEngine;

namespace GildedFate.Audio
{
    public enum MusicMood { Menu, Map, Combat, Boss, Sanctuary, Merchant, Event, Victory }

    // Licensed, composed stereo recordings. Attribution is in Credits and StreamingAssets.
    public sealed class AdaptiveMusicDirector : MonoBehaviour
    {
        private static AdaptiveMusicDirector instance;
        private readonly Dictionary<MusicMood,AudioClip> clips=new();
        private readonly Dictionary<MusicMood,int> resumeSamples=new();
        private readonly AudioSource[] sources=new AudioSource[2];
        private readonly float[] blend=new float[2];
        private MusicMood current;
        private bool hasMood;
        private int active;
        private float volume=.7f,duckAmount,duckUntil,duck=1,pausedMix=1,pausedTarget=1;
        public static MusicMood CurrentMood=>instance?instance.current:MusicMood.Menu;
        public static int TransitionCount {get;private set;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if(instance)return;var host=new GameObject("Gilded Fate · Music Stage");DontDestroyOnLoad(host);instance=host.AddComponent<AdaptiveMusicDirector>();
        }
        private void Awake()
        {
            if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;
            for(var i=0;i<2;i++){sources[i]=gameObject.AddComponent<AudioSource>();sources[i].playOnAwake=false;sources[i].loop=true;sources[i].spatialBlend=0;sources[i].priority=32;sources[i].volume=0;}
        }
        public static void SetVolume(float level){Boot();instance.volume=Mathf.Clamp01(level);}
        public static void SetMood(MusicMood mood,float level=.7f)
        {
            Boot();instance.volume=Mathf.Clamp01(level);if(instance.hasMood&&instance.current==mood)return;
            if(!instance.clips.TryGetValue(mood,out var clip))
            {clip=Resources.Load<AudioClip>("Audio/Music/"+mood);if(!clip){Debug.LogError("[Gilded Fate Audio] Missing score: "+mood);return;}instance.clips[mood]=clip;}
            if(instance.hasMood){var previous=instance.sources[instance.active];if(previous.clip)instance.resumeSamples[instance.current]=previous.timeSamples;}
            var next=instance.hasMood?1-instance.active:0;instance.active=next;instance.current=mood;instance.hasMood=true;
            var source=instance.sources[next];source.Stop();source.clip=clip;source.timeSamples=instance.resumeSamples.TryGetValue(mood,out var resume)?Mathf.Clamp(resume,0,Mathf.Max(0,clip.samples-1)):0;source.volume=0;instance.blend[next]=0;source.Play();TransitionCount++;
        }
        public static void Duck(float amount,float seconds)
        {if(!instance)return;instance.duckAmount=Mathf.Max(instance.duckAmount,Mathf.Clamp01(amount));instance.duckUntil=Mathf.Max(instance.duckUntil,Time.unscaledTime+seconds);}
        public static void SetPaused(bool paused){if(instance)instance.pausedTarget=paused?.40f:1;}
        private void Update()
        {
            var dt=Time.unscaledDeltaTime;
            pausedMix=Mathf.MoveTowards(pausedMix,pausedTarget,dt*2);
            if(Time.unscaledTime>duckUntil)duckAmount=0;
            duck=Mathf.MoveTowards(duck,1-duckAmount,dt*(duckAmount>0?6:1.4f));
            for(var i=0;i<2;i++)
            {
                blend[i]=Mathf.MoveTowards(blend[i],hasMood&&i==active?1:0,dt/2.3f);
                sources[i].volume=blend[i]*volume*.24f*duck*pausedMix;
                if(i!=active&&blend[i]<=0&&sources[i].isPlaying)sources[i].Stop();
            }
        }
        private void OnDestroy(){if(instance==this)instance=null;}
    }
}
