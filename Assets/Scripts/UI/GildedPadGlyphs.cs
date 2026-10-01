using UnityEngine;

namespace GildedFate.UI
{
    // Small Xbox-style button badges, shown only while a controller is the active
    // input: X beside the top bar, Y on the End Turn seal. Kept deliberately sparse.
    public sealed partial class GildedMainMenu
    {
        private Texture2D padGlyphDisc;
        private bool ShowPadGlyphs=>menuUsesGamepad&&UnityEngine.InputSystem.Gamepad.current!=null&&(profile==null||profile.showPadPrompts);
        // Settings > Controls > Button Prompts: 0 Auto (detect), 1 Xbox, 2 PlayStation.
        private bool PadUsesPlayStation
        {
            get
            {
                var style=profile?.padPromptStyle??0;if(style==1)return false;if(style==2)return true;
                var pad=UnityEngine.InputSystem.Gamepad.current;if(pad==null)return false;
                var id=((pad.layout??"")+" "+(pad.displayName??"")+" "+(pad.name??"")).ToLowerInvariant();
                return id.Contains("dualshock")||id.Contains("dualsense")||id.Contains("playstation")||id.Contains("ps4")||id.Contains("ps5");
            }
        }
        private static string PlayStationName(string xbox)=>xbox switch
        {
            "A"=>"Cross","B"=>"Circle","X"=>"Square","Y"=>"Triangle","LB"=>"L1","RB"=>"R1","LT"=>"L2","RT"=>"R2","View"=>"Create","Menu"=>"Options",_=>xbox
        };
        private static readonly System.Text.RegularExpressions.Regex PadWord=new(@"(?<![A-Za-z])(A|B|X|Y|LB|RB|LT|RT|View)(?![A-Za-z])");
        // Rewrites Xbox button names in a hint line for PlayStation players.
        private string PadHintText(string text)=>PadUsesPlayStation&&!string.IsNullOrEmpty(text)?PadWord.Replace(text,m=>PlayStationName(m.Value)).Replace("R-stick click","R3"):text;
        private readonly System.Collections.Generic.Dictionary<string,Texture2D> padShapes=new();
        private static float SegmentDistance(float px,float py,float ax,float ay,float bx,float by)
        {
            var dx=bx-ax;var dy=by-ay;var h=Mathf.Clamp01(((px-ax)*dx+(py-ay)*dy)/(dx*dx+dy*dy));
            var ex=px-ax-dx*h;var ey=py-ay-dy*h;return Mathf.Sqrt(ex*ex+ey*ey);
        }
        // Signed distance to an apex-up equilateral triangle centred on the origin (half side r).
        private static float TriangleDistance(float x,float y,float r)
        {
            const float k=1.7320508f;x=Mathf.Abs(x)-r;y+=r/k;
            if(x+k*y>0){var nx=(x-k*y)*.5f;var ny=(-k*x-y)*.5f;x=nx;y=ny;}
            x-=Mathf.Clamp(x,-2*r,0);return -Mathf.Sqrt(x*x+y*y)*Mathf.Sign(y);
        }
        // Procedural PlayStation face-button symbols (outline shapes, no font glyphs needed).
        private Texture2D PadShape(string button)
        {
            if(padShapes.TryGetValue(button,out var t)&&t)return t;
            const int n=64;t=new Texture2D(n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name="Pad shape "+button};
            var px=new Color32[n*n];
            for(var y=0;y<n;y++)for(var x=0;x<n;x++)
            {
                var u=(x+.5f)/n*2-1;var v=(y+.5f)/n*2-1;float d;
                switch(button)
                {
                    case "B":d=Mathf.Abs(Mathf.Sqrt(u*u+v*v)-.60f);break;
                    case "X":d=Mathf.Abs(Mathf.Max(Mathf.Abs(u),Mathf.Abs(v))-.54f);break;
                    case "A":d=Mathf.Min(SegmentDistance(u,v,-.52f,-.52f,.52f,.52f),SegmentDistance(u,v,-.52f,.52f,.52f,-.52f));break;
                    default:d=Mathf.Abs(TriangleDistance(u,v+.16f,.62f));break;
                }
                var alpha=Mathf.Clamp01(1-(d-.075f)/.045f);
                px[y*n+x]=new Color32(255,255,255,(byte)(alpha*255));
            }
            t.SetPixels32(px);t.Apply(false,false);padShapes[button]=t;return t;
        }
        private bool TopBarUsesX=>ShowsPersistentRunHud&&screen!=ScreenMode.Collection;
        private static Color PadGlyphColor(string button)=>button switch
        {
            "A"=>new Color(.42f,.78f,.30f),"B"=>new Color(.90f,.30f,.26f),
            "X"=>new Color(.40f,.68f,1f),"Y"=>new Color(.98f,.78f,.18f),_=>new Color(.7f,.7f,.72f)
        };
        private void DrawPadGlyph(Vector2 center,string button,bool enabled,float size=22)
        {
            if(!CardVfxRepaint)return;
            if(!padGlyphDisc)padGlyphDisc=BossPolishTexture("Pad glyph disc",64,d=>(1-d)*28f);
            var c=PadGlyphColor(button);var alpha=enabled?1f:.45f;var r=new Rect(center.x-size*.5f,center.y-size*.5f,size,size);
            var old=GUI.color;
            GUI.color=new Color(0,0,0,.6f*alpha);GUI.DrawTexture(new Rect(r.x-2,r.y-1,r.width+4,r.height+4),padGlyphDisc);
            GUI.color=new Color(.08f,.08f,.09f,alpha);GUI.DrawTexture(r,padGlyphDisc);
            GUI.color=new Color(c.r,c.g,c.b,.5f*alpha);GUI.DrawTexture(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),padGlyphDisc);
            GUI.color=old;
            if(PadUsesPlayStation&&button is "A" or "B" or "X" or "Y")
            {
                var ps=button switch{"A"=>new Color(.50f,.70f,1f),"B"=>new Color(1f,.42f,.45f),"X"=>new Color(.95f,.56f,.86f),_=>new Color(.30f,.88f,.74f)};
                GUI.color=new Color(ps.r,ps.g,ps.b,alpha);GUI.DrawTexture(new Rect(r.x+r.width*.2f,r.y+r.height*.2f,r.width*.6f,r.height*.6f),PadShape(button));GUI.color=old;return;
            }
            GUI.Label(r,PadUsesPlayStation?PlayStationName(button):button,new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=Mathf.RoundToInt(size*.62f),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(c.r,c.g,c.b,alpha)}});
        }
    }
}
