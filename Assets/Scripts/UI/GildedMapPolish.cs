using GildedFate.Map;
using UnityEngine;

namespace GildedFate.UI
{
    // Map-screen polish ("follow the gilded threads"): glowing route threads with
    // rising motes, travel spark + arrival flare, pulsing/rotating node rims,
    // completed-node glints, drifting fog over distant floors and a boss aura.
    // Soft textures are generated once in code (HideAndDontSave). Honors
    // reduceMotion (no motes/rotation/drift), reducedVfx (fewer particles) and
    // reduceFlashing (softer pulses/flares).
    public sealed partial class GildedMainMenu
    {
        private static Texture2D mapPolishGlow,mapPolishRing,mapPolishFog;
        private static readonly Color MapPolishGold=new Color(1f,.80f,.38f);

        private bool MapPolishMotion=>profile!=null&&!profile.reduceMotion;
        private bool MapPolishLowVfx=>profile!=null&&profile.reducedVfx;
        private bool MapPolishSoftFlash=>profile!=null&&profile.reduceFlashing;

        private static Texture2D MapPolishRadial(string name,int n,System.Func<float,float> alphaAtRadius)
        {
            var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name=name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[n*n];var mid=(n-1)*.5f;
            for(var y=0;y<n;y++)for(var x=0;x<n;x++){var d=new Vector2(x-mid,y-mid).magnitude/(n*.5f);pixels[y*n+x]=new Color(1,1,1,Mathf.Clamp01(alphaAtRadius(d)));}
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        private static Texture2D MapPolishFogTexture()
        {
            // Horizontally tileable soft band: vertical falloff times low-frequency wisps.
            const int w=128,h=32;
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name="Map polish fog",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat};
            var pixels=new Color[w*h];
            for(var y=0;y<h;y++)for(var x=0;x<w;x++)
            {
                var u=x/(float)w*Mathf.PI*2;var v=(y+.5f)/h;
                var band=Mathf.Pow(Mathf.Sin(v*Mathf.PI),1.6f);
                var wisp=.55f+.25f*Mathf.Sin(u*2+v*3.1f)+.2f*Mathf.Sin(u*5+1.7f+v*6f);
                pixels[y*w+x]=new Color(1,1,1,Mathf.Clamp01(band*wisp));
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        private static void EnsureMapPolishTextures()
        {
            if(!mapPolishGlow)mapPolishGlow=MapPolishRadial("Map polish glow",64,d=>Mathf.Exp(-d*d*4f)*Mathf.Clamp01((1-d)*5));
            if(!mapPolishRing)mapPolishRing=MapPolishRadial("Map polish ring",64,d=>Mathf.Min((1-d)*32f,(d-.93f)*32f));
            if(!mapPolishFog)mapPolishFog=MapPolishFogTexture();
        }
        private static void MapPolishTint(Rect rect,Texture2D texture,Color color)
        {
            if(!texture||color.a<=.003f)return;
            var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,true);GUI.color=old;
        }
        private static Rect MapPolishCentered(Vector2 c,float radius)=>new Rect(c.x-radius,c.y-radius,radius*2,radius*2);
        private static Color MapPolishAlpha(Color c,float a)=>new Color(c.r,c.g,c.b,Mathf.Clamp01(a));

        // ---------- 1. route threads ----------
        // Called before DrawFateThread for each edge (canvas space). Returns false when the
        // edge is a far, unreached route: it is drawn here as a faint strand instead.
        private bool MapPolishThread(Vector2 a,Vector2 b,Rect clip,bool completed,bool current,bool hot,MapNode from,MapNode target)
        {
            if(Mathf.Max(a.y,b.y)<clip.yMin||Mathf.Min(a.y,b.y)>clip.yMax)return !(target.floor>run.floor+1&&!completed&&!hot);
            EnsureMapPolishTextures();
            if(current)
            {
                var pulse=MapPolishMotion?.5f+.5f*Mathf.Sin(shimmer*2.1f+from.lane):.6f;if(MapPolishSoftFlash)pulse=.4f+pulse*.25f;
                MapPolishCurve(a,b,clip,MapPolishAlpha(MapPolishGold,.10f+.08f*pulse+(hot?.08f:0)),9);
                MapPolishCurve(a,b,clip,MapPolishAlpha(new Color(1f,.9f,.6f),.16f+.10f*pulse),4);
                if(MapPolishMotion)
                {
                    var motes=MapPolishLowVfx?1:3;
                    for(var i=0;i<motes;i++)
                    {
                        var f=Mathf.Repeat(shimmer*.38f+i/(float)motes+from.lane*.13f+target.lane*.07f,1f);
                        var p=FateThreadPoint(a,b,f);if(!clip.Contains(p))continue;
                        var fade=Mathf.Sin(f*Mathf.PI);
                        MapPolishTint(MapPolishCentered(p,9),mapPolishGlow,MapPolishAlpha(new Color(1f,.86f,.5f),.75f*fade));
                        Fill(new Rect(p.x-1.5f,p.y-1.5f,3,3),MapPolishAlpha(new Color(1f,.98f,.86f),.9f*fade));
                    }
                }
                return true;
            }
            if(completed){MapPolishCurve(a,b,clip,new Color(.85f,.62f,.26f,.12f),6);return true;}
            if(target.floor>run.floor+1&&!hot){MapPolishCurve(a,b,clip,new Color(.55f,.50f,.40f,.16f),1);return false;}
            return true;
        }
        private void MapPolishCurve(Vector2 a,Vector2 b,Rect clip,Color color,float width)
        {
            const int steps=5;var previous=a;
            for(var i=1;i<=steps;i++){var next=FateThreadPoint(a,b,i/(float)steps);DrawMapLink(previous,next,clip,color,width);previous=next;}
        }

        // ---------- 2. travel spark + arrival flare ----------
        // Called from DrawMapTravelTransition after the weaving thread (canvas space).
        private void MapPolishTravel(Vector2 start,Vector2 destination,Rect viewport,float progress,float t)
        {
            EnsureMapPolishTextures();
            var trail=!MapPolishMotion?0:MapPolishLowVfx?3:7;
            for(var i=trail;i>=1;i--)
            {
                var f=progress-i*.035f;if(f<=0)continue;var p=FateThreadPoint(start,destination,f);if(!viewport.Contains(p))continue;
                var k=1f-i/(float)(trail+1);MapPolishTint(MapPolishCentered(p,6+10*k),mapPolishGlow,MapPolishAlpha(MapPolishGold,.55f*k));
            }
            var spark=FateThreadPoint(start,destination,progress);
            if(viewport.Contains(spark)){var flick=MapPolishMotion&&!MapPolishSoftFlash?.85f+.15f*Mathf.Sin(shimmer*40):.9f;MapPolishTint(MapPolishCentered(spark,22),mapPolishGlow,MapPolishAlpha(new Color(1f,.9f,.62f),.9f*flick));MapPolishTint(MapPolishCentered(spark,8),mapPolishGlow,new Color(1,1,.95f,1));}
            var arrive=Mathf.Clamp01((progress-.9f)/.1f);if(arrive<=0||!viewport.Contains(destination))return;
            var strength=MapPolishSoftFlash?.45f:1f;var bloom=MapPolishMotion?Mathf.Lerp(30,78,Mathf.Clamp01((t-.72f)/.28f)):54;
            MapPolishTint(MapPolishCentered(destination,bloom),mapPolishGlow,MapPolishAlpha(new Color(1f,.84f,.45f),.85f*arrive*strength));
            MapPolishTint(MapPolishCentered(destination,bloom*.72f),mapPolishRing,MapPolishAlpha(new Color(1f,.92f,.7f),.9f*arrive*strength*(1.2f-bloom/78f)));
        }

        // ---------- 3. nodes ----------
        // Called inside the viewport group before the node icon.
        private void MapPolishNodeUnder(MapNode node,Rect r,bool active,bool hot,bool current)
        {
            if(!active&&!hot&&!current)return;EnsureMapPolishTextures();var c=r.center;var size=r.width;
            var pulse=MapPolishMotion?.5f+.5f*Mathf.Sin(shimmer*2.4f+node.lane*.9f):.6f;if(MapPolishSoftFlash)pulse=.45f+pulse*.2f;
            var glowAlpha=(hot?.46f:active?.26f:.14f)+(active?.14f*pulse:0);
            MapPolishTint(MapPolishCentered(c,size*(.78f+(active?.06f*pulse:0))),mapPolishGlow,MapPolishAlpha(MapPolishGold,glowAlpha));
            if(!active)return;
            var rim=size*.62f;MapPolishTint(MapPolishCentered(c,rim),mapPolishRing,MapPolishAlpha(MapPolishGold,hot?.85f:.5f+.15f*pulse));
            var spin=MapPolishMotion?shimmer*.35f:0;var ticks=hot?16:12;var tickColor=MapPolishAlpha(new Color(1f,.86f,.52f),hot?.9f:.6f);
            for(var i=0;i<ticks;i++)
            {
                var ang=spin+i*Mathf.PI*2/ticks;var dir=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));var len=i%4==0?5.5f:3f;
                DrawLine(c+dir*(rim-1),c+dir*(rim+len),tickColor,i%4==0?2:1);
            }
        }
        // Called inside the viewport group after the node icon.
        private void MapPolishNodeOver(MapNode node,Rect r,bool current)
        {
            if(!node.complete||current)return;EnsureMapPolishTextures();
            var glint=MapPolishMotion?Mathf.Pow(Mathf.Max(0,Mathf.Sin(shimmer*.8f+node.floor*1.3f+node.lane)),6):.3f;
            var at=new Vector2(r.xMax-r.width*.2f,r.yMax-r.height*.22f);
            MapPolishTint(MapPolishCentered(at,9+5*glint),mapPolishGlow,MapPolishAlpha(MapPolishGold,.28f+.4f*glint));
            var col=MapPolishAlpha(new Color(1f,.86f,.5f),.62f+.35f*glint);
            DrawLine(at+new Vector2(-4,0),at+new Vector2(-1,3),col,2);DrawLine(at+new Vector2(-1,3),at+new Vector2(5,-4),col,2);
        }

        // ---------- 4. fog of war + boss aura ----------
        // Called after the node group (canvas space). Veils floors more than two above the current one.
        private void MapPolishFog(Rect viewport)
        {
            var floors=Mathf.Max(1,run.ActFloorCount);var veilFloor=run.floor+2;if(veilFloor>=floors-1)return;
            EnsureMapPolishTextures();
            var edgeY=viewport.y+82+(floors-1-veilFloor)*118f-59f-mapScroll;var top=viewport.yMin;var bottom=Mathf.Min(edgeY,viewport.yMax);if(bottom<=top+4)return;
            var climb=Mathf.Clamp01(run.floor/(float)floors);var density=Mathf.Lerp(.46f,.22f,climb);
            var bands=MapPolishLowVfx?3:6;var region=bottom-top;var bandH=Mathf.Max(60,region/bands*1.7f);
            for(var i=0;i<bands;i++)
            {
                var k=(i+.5f)/bands;var y=Mathf.Lerp(top,bottom,k)-bandH*.5f;
                var drift=MapPolishMotion?shimmer*(.012f+.006f*(i%3))*(i%2==0?1:-1):0;
                var rect=new Rect(viewport.x,Mathf.Max(top,y),viewport.width,Mathf.Min(bandH,bottom-Mathf.Max(top,y)));if(rect.height<=2)continue;
                var v0=(rect.y-y)/bandH;var vh=rect.height/bandH;
                var old=GUI.color;GUI.color=new Color(.015f,.02f,.035f,density*Mathf.Lerp(.55f,1f,1-k));
                GUI.DrawTextureWithTexCoords(rect,mapPolishFog,new Rect(drift+i*.37f,1-v0-vh,viewport.width/320f,vh),true);GUI.color=old;
            }
            // Soft lower edge so the veil does not end in a hard line.
            var edge=new Rect(viewport.x,bottom-24,viewport.width,48);if(edge.yMax>viewport.yMax)edge.height=Mathf.Max(0,viewport.yMax-edge.y);
            if(edge.height>2){var old=GUI.color;GUI.color=new Color(.015f,.02f,.035f,density*.45f);GUI.DrawTextureWithTexCoords(edge,mapPolishFog,new Rect(0,0,viewport.width/320f,1),true);GUI.color=old;}
        }
        // Called before the boss portrait is drawn.
        private void MapPolishBossAura(Rect crown)
        {
            EnsureMapPolishTextures();var c=crown.center;
            var pulse=MapPolishMotion?.5f+.5f*Mathf.Sin(shimmer*1.6f):.55f;if(MapPolishSoftFlash)pulse=.45f+pulse*.2f;
            MapPolishTint(MapPolishCentered(c,crown.width*(.82f+.08f*pulse)),mapPolishGlow,new Color(.78f,.08f,.06f,.30f+.22f*pulse));
            MapPolishTint(MapPolishCentered(c,crown.width*.6f),mapPolishGlow,new Color(1f,.66f,.22f,.12f+.12f*pulse));
            MapPolishTint(MapPolishCentered(c,crown.width*.58f),mapPolishRing,new Color(.95f,.36f,.14f,.22f+.2f*pulse));
        }
    }
}
