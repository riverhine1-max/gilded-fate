using System;
using System.Collections.Generic;
using UnityEngine;

namespace GildedFate.ChronicleBook
{
    /// <summary>Owned, cached GPU illustration surfaces. Story state and source artwork stay untouched.</summary>
    public sealed class ChronicleLivingArt : IDisposable
    {
        sealed class Surface { public RenderTexture texture; public float time = -999, reveal = -1; public Color accent; public int mode; }
        readonly Dictionary<Texture2D, Surface> surfaces = new();
        readonly Dictionary<string, Texture2D> assets = new();
        Material material;
        bool shaderChecked;
        Material effects;
        readonly List<RenderTexture> effectSurfaces = new();
        readonly Dictionary<long, Surface> artifacts = new();
        int effectSlot;
        public void BeginIllustration() => effectSlot = 0;
        bool EnsureEffects()
        {
            if(effects)return true;
            var shader=Resources.Load<Shader>("Chronicle/ChronicleIllustrationVfx");
            if(!shader||!shader.isSupported)return false;
            effects=new Material(shader){hideFlags=HideFlags.HideAndDontSave};return true;
        }
        public Texture RenderArtifact(int mode,float time,Color accent)
        {
            if(Event.current==null||Event.current.type!=EventType.Repaint||!EnsureEffects())return null;
            Color32 palette=accent;
            var key=((long)mode<<24)|((long)palette.r<<16)|((long)palette.g<<8)|palette.b;
            if(!artifacts.TryGetValue(key,out var surface))
            {
                if(artifacts.Count>=32)return null;
                surface=new Surface{texture=new RenderTexture(512,512,0,RenderTextureFormat.ARGB32)
                    {name="Chronicle engraved artifact",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave}};
                surface.texture.Create();artifacts.Add(key,surface);
            }
            if(Mathf.Abs(surface.time-time)>.015f)
            {
                effects.SetFloat("_Mode",mode);effects.SetFloat("_Progress",1);effects.SetFloat("_Clock",time);effects.SetFloat("_Intensity",1);
                effects.SetVector("_Focus",new Vector4(.5f,.5f,0,0));effects.SetColor("_Accent",accent);
                var active=RenderTexture.active;
                try{Graphics.Blit(Texture2D.whiteTexture,surface.texture,effects);}finally{RenderTexture.active=active;}
                surface.time=time;
            }
            return surface.texture;
        }
        public Texture RenderEffect(int mode, float progress, float time, float intensity, Vector2 focus, Color accent)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return null;
            if (!EnsureEffects()) return null;
            // Bounded scratch surfaces, reused across scenes and outgoing spreads; never one surface per story command.
            if (effectSlot >= 12) return null;
            if (effectSlot == effectSurfaces.Count)
            {
                var rt = new RenderTexture(768, 1152, 0, RenderTextureFormat.ARGB32) { name = "Chronicle VFX layer", filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                rt.Create(); effectSurfaces.Add(rt);
            }
            var texture = effectSurfaces[effectSlot++];
            effects.SetFloat("_Mode", mode); effects.SetFloat("_Progress", progress); effects.SetFloat("_Clock", time);
            effects.SetFloat("_Intensity", intensity); effects.SetVector("_Focus", new Vector4(focus.x, 1f - focus.y, 0, 0)); effects.SetColor("_Accent", accent);
            var active = RenderTexture.active;
            try { Graphics.Blit(Texture2D.whiteTexture, texture, effects); }
            finally { RenderTexture.active = active; }
            return texture;
        }
        public Texture2D Load(string path)
        {
            if (!assets.TryGetValue(path, out var texture)) assets[path] = texture = Resources.Load<Texture2D>(path);
            return texture;
        }
        public Texture Render(Texture2D source, float time, Color accent, int mode, float reveal = 1f)
        {
            if (!source) return null;
            if (!shaderChecked)
            {
                shaderChecked = true;
                var shader = Resources.Load<Shader>("Chronicle/ChronicleLivingIllustration");
                if (shader && shader.isSupported) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (!material || Event.current == null || Event.current.type != EventType.Repaint) return source;
            if (!surfaces.TryGetValue(source, out var surface))
            {
                surface = new Surface { texture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32)
                    { name = "Chronicle living art · " + source.name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave, useMipMap = false, autoGenerateMips = false } };
                surface.texture.Create(); surfaces.Add(source, surface);
            }
            // Multiple echoes/actors reuse the same processed art rather than allocating a surface per actor.
            if (Mathf.Abs(surface.time - time) > .015f || surface.accent != accent || surface.mode != mode || Mathf.Abs(surface.reveal - reveal) > .001f)
            {
                material.SetFloat("_Clock", time); material.SetFloat("_Mode", mode);
                material.SetColor("_Accent", accent); material.SetFloat("_Reveal", reveal);
                var active = RenderTexture.active;
                try { Graphics.Blit(source, surface.texture, material); }
                finally { RenderTexture.active = active; }
                surface.time = time; surface.accent = accent; surface.mode = mode; surface.reveal = reveal;
            }
            return surface.texture;
        }
        public void Dispose()
        {
            foreach (var surface in surfaces.Values) if (surface.texture) { surface.texture.Release(); Destroy(surface.texture); }
            surfaces.Clear(); assets.Clear(); if (material) Destroy(material); material = null; shaderChecked = false;
            foreach (var texture in effectSurfaces) if (texture) { texture.Release(); Destroy(texture); }
            effectSurfaces.Clear(); if (effects) Destroy(effects); effects = null;
            foreach(var artifact in artifacts.Values)if(artifact.texture){artifact.texture.Release();Destroy(artifact.texture);}artifacts.Clear();
        }
        static void Destroy(UnityEngine.Object value) { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
