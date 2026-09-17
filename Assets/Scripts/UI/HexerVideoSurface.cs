using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace GildedFate.UI
{
    // Presentation only: two bounded decoders, no game-state or combat-rule writes.
    public sealed class HexerVideoSurface : IDisposable
    {
        private sealed class Channel
        {
            public VideoPlayer Player;
            public HexerVideoCatalog.Clip Clip;
            public bool FrameReady,Ended,Failed;
            public float RequestedAt;
            public RenderTexture Keyed;
            public long RenderedFrame=-2;
        }
        private readonly GameObject host;
        private readonly Channel idle,action;
        private readonly Material keyMaterial;
        private bool paused,disposed;
        private string lastError="";
        public string Error=>lastError;
        public string CurrentName=>Active?.Clip?.Name??"Preparing";
        public string ActionName=>action.Clip?.Name;
        public double ActionTime=>action.Player.time;
        public bool ActionPlaying=>action.Clip!=null&&!action.Ended&&!action.Failed;
        private Channel Active=>action.FrameReady&&!action.Ended&&!action.Failed?action:idle.FrameReady&&!idle.Failed?idle:null;
        public bool Ready=>Active!=null&&keyMaterial;
        public RenderTexture PresentedTexture=>Active?.Keyed;

        public HexerVideoSurface()
        {
            host=new GameObject("Hexer video presentation"){hideFlags=HideFlags.HideAndDontSave};
            var shader=Resources.Load<Shader>("HexerPackedAlpha");
            if(shader&&shader.isSupported)keyMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            else lastError="Hexer alpha shader unavailable; keeping illustrated fallback.";
            idle=CreateChannel("CombatIdle");action=CreateChannel("Action");
            Load(idle,"CombatIdle",1,true);
        }
        private Channel CreateChannel(string label)
        {
            var child=new GameObject(label){hideFlags=HideFlags.HideAndDontSave};child.transform.SetParent(host.transform);
            var ch=new Channel{Player=child.AddComponent<VideoPlayer>()};
            var p=ch.Player;p.playOnAwake=false;p.source=VideoSource.Url;p.renderMode=VideoRenderMode.APIOnly;
            p.audioOutputMode=VideoAudioOutputMode.None;p.waitForFirstFrame=true;p.skipOnDrop=true;
            p.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;p.sendFrameReadyEvents=true;
            p.prepareCompleted+=source=>{if(!disposed&&!ch.Failed&&!paused)source.Play();};
            p.frameReady+=(source,frame)=>{if(!disposed&&!ch.Failed)ch.FrameReady=true;};
            p.loopPointReached+=source=>{if(!source.isLooping){if(ch.Clip?.Name=="Defeat")source.Pause();else ch.Ended=true;}};
            p.errorReceived+=(source,message)=>{ch.Failed=true;ch.FrameReady=false;lastError=label+": "+message;Debug.LogWarning("[Hexer Video] "+lastError);};
            return ch;
        }
        private void Load(Channel ch,string name,float speed,bool loop)
        {
            if(disposed)return;
            var clip=HexerVideoCatalog.Find(name);
            ch.Player.Stop();ch.Clip=clip;ch.FrameReady=false;ch.Ended=false;ch.Failed=false;ch.RenderedFrame=-2;
            ch.RequestedAt=Time.realtimeSinceStartup;
            if(clip==null||!File.Exists(clip.Path)){ch.Failed=true;lastError="Missing Hexer clip: "+name;return;}
            ch.Player.url=clip.Url;ch.Player.isLooping=loop;ch.Player.playbackSpeed=Mathf.Clamp(speed,.1f,10f);ch.Player.Prepare();
        }
        public void Play(string name,float seconds=.9f,bool loop=false)
        {
            if(disposed||!keyMaterial)return;
            Load(action,name,(HexerVideoCatalog.Find(name)?.Duration??3f)/Mathf.Max(.4f,seconds),loop);
        }
        public void Reset(){if(disposed)return;action.Player.Stop();action.FrameReady=false;action.Clip=null;action.Ended=true;}
        public void Tick(bool pause,bool lowHealth=false)
        {
            if(disposed)return;
            paused=pause;
            var idleName=lowHealth?"LowHealthIdle":"CombatIdle";
            if(idle.Clip?.Name!=idleName)Load(idle,idleName,1,true);
            foreach(var ch in new[]{idle,action})
            {
                if(ch.Clip==null||ch.Failed||ch.Ended)continue;
                // Time spent inspecting UI is not a decoder timeout.
                if(pause)ch.RequestedAt+=Time.unscaledDeltaTime;
                if(!ch.FrameReady&&!ch.Player.isPrepared&&Time.realtimeSinceStartup-ch.RequestedAt>10&&!pause){ch.Failed=true;lastError="Video preparation timed out: "+ch.Clip.Name;continue;}
                if(pause){if(ch.Player.isPlaying)ch.Player.Pause();}
                else if(ch.Player.isPrepared&&!ch.Player.isPlaying&&!(ch.Clip.Name=="Defeat"&&ch.FrameReady&&ch.Player.frame>=(long)ch.Player.frameCount-2))ch.Player.Play();
                Composite(ch);
            }
        }
        private void Composite(Channel ch)
        {
            if(!keyMaterial||!ch.FrameReady||!ch.Player.texture)return;
            var texture=ch.Player.texture;
            if(ch.Keyed==null||ch.Keyed.width!=texture.width/2||ch.Keyed.height!=texture.height)
            {
                Release(ch);
                ch.Keyed=new RenderTexture(texture.width/2,texture.height,0,RenderTextureFormat.ARGB32){name="Hexer keyed "+ch.Clip.Name,hideFlags=HideFlags.HideAndDontSave};
                ch.Keyed.Create();ch.RenderedFrame=-2;
            }
            if(ch.RenderedFrame!=ch.Player.frame)
            {
                var previous=RenderTexture.active;
                try{Graphics.Blit(texture,ch.Keyed,keyMaterial);ch.RenderedFrame=ch.Player.frame;}
                finally{RenderTexture.active=previous;}
            }
        }
        public bool Draw(Rect actor,bool raw=false)
        {
            if(disposed||!keyMaterial)return false;
            var ch=Active;if(ch==null||!ch.Keyed||!ch.Player.texture)return false;
            if(Event.current.type!=EventType.Repaint)return true;
            GUI.DrawTexture(ch.Clip.DrawRect(actor),raw?ch.Player.texture:ch.Keyed,ScaleMode.StretchToFill,true);
            return true;
        }
        private static void DestroyOwned(UnityEngine.Object obj){if(!obj)return;if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
        private static void Release(Channel ch){if(ch.Keyed){ch.Keyed.Release();DestroyOwned(ch.Keyed);ch.Keyed=null;}}
        public void Dispose()
        {
            if(disposed)return;disposed=true;idle.Player.Stop();action.Player.Stop();Release(idle);Release(action);DestroyOwned(keyMaterial);DestroyOwned(host);
        }
    }
}
