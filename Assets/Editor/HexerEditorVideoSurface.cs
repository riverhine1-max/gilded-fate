using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using GildedFate.UI;

namespace GildedFate.Editor
{
    // Editor-only decoder: does not depend on Play Mode, create scene objects, or
    // start gameplay. Unity 6000.5's own media tooling uses this native decoder.
    // Reflection is isolated here because Unity exposes it as an internal API.
    public sealed class HexerEditorVideoSurface : IDisposable
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        readonly Type decoderType=Type.GetType("UnityEditorInternal.Media.MediaDecoder, UnityEditor");
        object decoder;
        MethodInfo nextFrame,seek,dispose;
        Texture2D packed;
        RenderTexture keyed;
        Material material;
        HexerVideoCatalog.Clip clip;
        double clock,lastTick;
        float rate=1;
        bool loop,disposed;
        int frame=-1;
        public string Error { get; private set; }="";
        public string CurrentName=>clip?.Name??"Preparing";
        public double ActionTime=>Math.Max(0,frame)/24d;
        public RenderTexture PresentedTexture=>frame>=0?keyed:null;

        public void Play(string name,float seconds,bool repeat)
        {
            if(disposed)return;
            Release();Error="";frame=-1;clock=0;lastTick=EditorApplication.timeSinceStartup;
            clip=HexerVideoCatalog.Find(name);loop=repeat;
            try
            {
                if(clip==null||!File.Exists(clip.Path))throw new IOException("Missing Hexer clip: "+name);
                if(new FileInfo(clip.Path).Length<1024)throw new IOException("Video is a Git LFS pointer, not the downloaded clip. Run git lfs pull.");
                var constructor=decoderType?.GetConstructor(Flags,null,new[]{typeof(string)},null);
                nextFrame=decoderType?.GetMethod("GetNextFrame",Flags);
                seek=decoderType?.GetMethod("SetPosition",Flags);
                dispose=decoderType?.GetMethod("Dispose",Flags);
                if(constructor==null||nextFrame==null||seek==null||dispose==null)
                    throw new NotSupportedException("This Unity version lacks the editor video decoder. This preview is verified with Unity 6000.5.");
                var shader=Resources.Load<Shader>("HexerPackedAlpha");
                if(!shader||!shader.isSupported)throw new NotSupportedException("Hexer alpha shader is unavailable.");
                decoder=constructor.Invoke(new object[]{clip.Path});
                packed=new Texture2D(1024,512,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
                keyed=new RenderTexture(512,512,0,RenderTextureFormat.ARGB32){hideFlags=HideFlags.HideAndDontSave};keyed.Create();
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                rate=clip.Duration/Mathf.Max(.4f,seconds);
                Decode(0);
            }
            catch(Exception e){Fail(e);}
        }
        public void Tick(bool pause)
        {
            var now=EditorApplication.timeSinceStartup;var elapsed=Math.Max(0,now-lastTick);lastTick=now;
            if(disposed||decoder==null||pause||Error!="")return;
            clock+=Math.Min(elapsed,.25)*rate;
            var length=Math.Max(1,(int)Math.Round(clip.Duration*24));
            var desired=loop?(int)(clock*24)%length:Math.Min(length-1,(int)(clock*24));
            if(desired==frame)return;
            try{Decode(desired);}catch(Exception e){Fail(e);}
        }
        void Decode(int desired)
        {
            if(desired!=frame+1)
                if(!(bool)seek.Invoke(decoder,new object[]{new MediaTime(desired,24)}))throw new IOException("Cannot seek Hexer preview: "+clip.Name);
            var args=new object[]{packed,null};
            if(!(bool)nextFrame.Invoke(decoder,args))throw new IOException("Cannot decode Hexer preview: "+clip.Name);
            packed.Apply(false,false);
            var previous=RenderTexture.active;
            try{Graphics.Blit(packed,keyed,material);}finally{RenderTexture.active=previous;}
            frame=desired;
        }
        public bool Draw(Rect actor,bool raw=false)
        {
            if(disposed||frame<0||!keyed)return false;
            if(Event.current.type==EventType.Repaint)GUI.DrawTexture(clip.DrawRect(actor),raw?(Texture)packed:keyed,ScaleMode.StretchToFill,true);
            return true;
        }
        void Fail(Exception e){Error=(e.InnerException??e).Message;Release();frame=-1;}
        void Release()
        {
            if(decoder!=null){try{dispose?.Invoke(decoder,null);}finally{decoder=null;}}
            if(keyed){keyed.Release();UnityEngine.Object.DestroyImmediate(keyed);keyed=null;}
            if(packed){UnityEngine.Object.DestroyImmediate(packed);packed=null;}
            if(material){UnityEngine.Object.DestroyImmediate(material);material=null;}
        }
        public void Dispose(){if(disposed)return;disposed=true;Release();}
    }
}
