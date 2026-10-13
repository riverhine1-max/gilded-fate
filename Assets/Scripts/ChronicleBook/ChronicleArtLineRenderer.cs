using System;
using System.Collections.Generic;
using UnityEngine;

namespace GildedFate.ChronicleBook
{
    // Axis-aligned alpha textures keep manuscript ink inside IMGUI's page clip.
    // Rotating GUI.matrix inside a BeginGroup can move both the ink and its clip.
    public sealed class ChronicleArtLineRenderer : IDisposable
    {
        readonly Dictionary<string, Texture2D> strokes = new();
        public void Draw(Vector2 a, Vector2 b, Color color, float width)
        {
            if (Event.current.type != EventType.Repaint) { if(Event.current.type == EventType.Layout && strokes.Count > 512) Dispose(); return; }
            var delta = b - a;
            var padding = Mathf.Max(1f, width);
            var r = new Rect(Mathf.Min(a.x,b.x)-padding, Mathf.Min(a.y,b.y)-padding, Mathf.Abs(delta.x)+padding*2, Mathf.Abs(delta.y)+padding*2);
            var w = Mathf.Clamp(Mathf.CeilToInt(r.width), 4, 256); var h = Mathf.Clamp(Mathf.CeilToInt(r.height), 4, 256);
            var start = new Vector2((a.x-r.x)/r.width*w, (a.y-r.y)/r.height*h);
            var end = new Vector2((b.x-r.x)/r.width*w, (b.y-r.y)/r.height*h);
            var key = w+":"+h+":"+(delta.x*delta.y>=0)+":"+Mathf.RoundToInt(width*10);
            if (!strokes.TryGetValue(key, out var texture))
            {
                texture = new Texture2D(w,h,TextureFormat.RGBA32,false){name="Chronicle engraved ink",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels = new Color32[w*h]; var segment=end-start; var scaleX=r.width/w; var scaleY=r.height/h;
                var realSegment=new Vector2(segment.x*scaleX,segment.y*scaleY);
                for(var y=0;y<h;y++)for(var x=0;x<w;x++)
                {
                    var offset=new Vector2((x+.5f-start.x)*scaleX,(y+.5f-start.y)*scaleY);
                    var t=Mathf.Clamp01(Vector2.Dot(offset,realSegment)/Mathf.Max(.0001f,realSegment.sqrMagnitude));
                    var distance=(offset-realSegment*t).magnitude;
                    pixels[(h-1-y)*w+x]=new Color32(255,255,255,(byte)(Mathf.Clamp01(width*.5f+.6f-distance)*255));
                }
                texture.SetPixels32(pixels); texture.Apply(false,true);strokes[key]=texture;
            }
            var saved=GUI.color;GUI.color=color;GUI.DrawTexture(r,texture,ScaleMode.StretchToFill,true);GUI.color=saved;
        }
        public void Dispose()
        {
            foreach(var texture in strokes.Values)if(texture){if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}
            strokes.Clear();
        }
    }
}
