using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    // Events Rework: mystery options ("???").
    // A veiled card, a sealed-fate emblem with a hand-drawn question sigil, an omen bar for the odds,
    // and a seal that cracks open on the outcome screen. Honors Reduce Motion / Flashing / VFX.
    public sealed partial class GildedMainMenu
    {
        private static readonly Color MysteryViolet=new(.62f,.42f,1f);
        private static readonly Color MysteryGold=new(1f,.84f,.5f);
        private static readonly Color OmenRed=new(.95f,.32f,.25f);
        private string eventOutcomeKey="";private float eventOutcomeShownAt;

        private static bool IsMysteryChoice(EventChoiceDef choice)=>choice!=null&&(choice.rewardText??"").Trim()=="???";

        // ---------------- veiled card ----------------
        private void DrawMysteryVeil(Rect r,bool hot,bool open,int seed)
        {
            if(!CardVfxRepaint)return;
            var t=Time.unscaledTime;var motion=!profile.reduceMotion;var a=open?1f:.45f;
            var w=r.width;var h=r.height;
            // Deep violet wash, heavier toward the right where the seal sits.
            for(var band=0;band<10;band++){var k=band/9f;Fill(new Rect(r.x+w*k*.9f,r.y,w*.1f+1,h),new Color(.10f,.04f,.16f,(.10f+k*.22f)*a));}
            // Drifting fog, kept inside the card (no clipping: rotated lines and clip regions don't mix in IMGUI).
            for(var i=0;i<5;i++)
            {
                var size=h*(.7f+CardVfxHash(seed,i+20)*.25f);var margin=size*.38f;
                var speed=motion?(hot?.11f:.05f):0;var u=Mathf.PingPong(CardVfxHash(seed,i)+t*speed*(1+i*.2f),1f);
                var p=new Vector2(Mathf.Lerp(r.x+margin,r.xMax-margin,u),Mathf.Clamp(r.y+h*(.3f+CardVfxHash(seed,i+10)*.4f)+Mathf.Sin(t*.7f+i)*5,r.y+margin,r.yMax-margin));
                ShardSoft(p,size,new Color(MysteryViolet.r,MysteryViolet.g,MysteryViolet.b,(hot?.12f:.08f)*a));
            }
            // Motes rising through the veil.
            if(!profile.reducedVfx)for(var m=0;m<9;m++)
            {
                var mk=Mathf.Repeat((motion?t*(.12f+m*.013f):0)+CardVfxHash(seed,m+40),1f);
                var p=new Vector2(r.x+w*(.35f+CardVfxHash(seed,m+60)*.6f),r.yMax-6-(h-12)*mk);
                var col=m%3==0?MysteryGold:MysteryViolet;ShardSoft(p,6+(1-mk)*4,new Color(col.r,col.g,col.b,.55f*(1-mk)*a));
            }
            // A slow shimmer sweeps across the card every few seconds, cut to the card's edges.
            if(motion&&!profile.reduceFlashing)
            {
                var sweep=Mathf.Repeat(t*.28f+CardVfxHash(seed,90),1.6f)*(w+h*.6f)-h*.6f;
                for(var line=0;line<5;line++)
                {
                    var x0=r.x+sweep+line*5;Vector2 from=new(x0,r.yMax),to=new(x0+h*.6f,r.y);
                    if(ClipToCard(ref from,ref to,r))DrawLine(from,to,new Color(1f,.9f,1f,(.05f-line*.008f)*(hot?1.8f:1f)*a),3);
                }
            }
        }
        // Trims a segment to the card's horizontal span so the shimmer never leaks outside it.
        private static bool ClipToCard(ref Vector2 a,ref Vector2 b,Rect r)
        {
            var p0=a;var p1=b; // local copies: ref parameters can't be captured by a local function
            if(Mathf.Max(p0.x,p1.x)<r.x+2||Mathf.Min(p0.x,p1.x)>r.xMax-2)return false;
            Vector2 At(float x){var k=(x-p0.x)/Mathf.Max(.0001f,p1.x-p0.x);return Vector2.Lerp(p0,p1,Mathf.Clamp01(k));}
            a=p0.x<r.x+2?At(r.x+2):p0;b=p1.x>r.xMax-2?At(r.xMax-2):p1;return Vector2.Distance(a,b)>1;
        }

        // ---------------- sealed-fate emblem ----------------
        private void DrawFateEmblem(Vector2 c,float radius,bool hot,bool open,bool gamble)
        {
            if(!CardVfxRepaint)return;
            var t=Time.unscaledTime;var motion=!profile.reduceMotion;var a=open?1f:.45f;
            var pulse=motion?Mathf.Sin(t*(hot?4.2f:2.4f))*.5f+.5f:.5f;
            ShardSoft(c,radius*4.2f,new Color(MysteryViolet.r,MysteryViolet.g,MysteryViolet.b,(.16f+pulse*.1f+(hot?.08f:0))*a));
            ShardSoft(c,radius*2.1f,new Color(MysteryGold.r,MysteryGold.g,MysteryGold.b,(.10f+pulse*.08f)*a));
            // Turning rune ring.
            var rot=motion?t*(hot?.9f:.35f):0;var ring=radius*1.02f;
            for(var i=0;i<24;i++)
            {
                var ang=rot+i*Mathf.PI/12;var dir=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));var len=i%3==0?radius*.24f:radius*.12f;
                DrawLine(c+dir*ring,c+dir*(ring+len),new Color(MysteryGold.r,MysteryGold.g,MysteryGold.b,(i%3==0?.85f:.45f)*a),i%3==0?2f:1.2f);
            }
            ShardCircle(c,ring-3,ring-3,new Color(MysteryViolet.r,MysteryViolet.g,MysteryViolet.b,.55f*a),1.2f,64,-rot);
            // Double diamond seal.
            var d=radius*.74f;
            var outer=new[]{c+new Vector2(0,-d),c+new Vector2(d,0),c+new Vector2(0,d),c+new Vector2(-d,0)};
            var inner=new[]{c+new Vector2(0,-d*.8f),c+new Vector2(d*.8f,0),c+new Vector2(0,d*.8f),c+new Vector2(-d*.8f,0)};
            for(var k=0;k<4;k++){DrawLine(outer[k],outer[(k+1)%4],new Color(.03f,.01f,.06f,.95f*a),9);}
            for(var k=0;k<4;k++){DrawLine(outer[k],outer[(k+1)%4],new Color(MysteryGold.r,MysteryGold.g,MysteryGold.b,.95f*a),2.2f);DrawLine(inner[k],inner[(k+1)%4],new Color(MysteryViolet.r,MysteryViolet.g,MysteryViolet.b,.7f*a),1.2f);}
            foreach(var tip in outer)Fill(new Rect(tip.x-2,tip.y-2,4,4),new Color(MysteryGold.r,MysteryGold.g,MysteryGold.b,a));
            var bob=motion?Mathf.Sin(t*1.8f)*1.5f:0;
            DrawQuestionSigil(c+new Vector2(0,bob),d*.62f,new Color(MysteryGold.r,MysteryGold.g,MysteryGold.b,a),hot);
            // Three motes orbit the seal.
            if(!profile.reducedVfx)for(var m=0;m<3;m++)
            {
                var ang=-rot*1.6f+m*Mathf.PI*2/3;var p=c+new Vector2(Mathf.Cos(ang)*radius*1.32f,Mathf.Sin(ang)*radius*.7f);
                ShardSoft(p,11,new Color(MysteryGold.r,MysteryGold.g,MysteryGold.b,.75f*a));Fill(new Rect(p.x-1,p.y-1,2,2),new Color(1,1,1,a));
            }
            var label=gamble?"FATE UNKNOWN":"SEE WHAT FOLLOWS";
            GUI.Label(new Rect(c.x-80,c.y+radius*1.32f,160,18),label,new GUIStyle(ReadableStyle(10,true)){alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.86f,.74f,1f,.9f*a)}});
        }
        // A question mark built from strokes: a hooked arc, a short stem and a diamond dot.
        private void DrawQuestionSigil(Vector2 c,float size,Color color,bool bright)
        {
            var arcCenter=c+new Vector2(0,-size*.42f);var arcR=size*.46f;var prev=Vector2.zero;var first=true;
            var glow=new Color(color.r,color.g,color.b,color.a*(bright?.38f:.24f));
            for(var i=0;i<=14;i++)
            {
                var ang=Mathf.Lerp(Mathf.PI*1.05f,Mathf.PI*2.42f,i/14f);
                var p=arcCenter+new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*arcR;
                if(!first){DrawLine(prev,p,glow,size*.32f);DrawLine(prev,p,color,size*.14f);}
                prev=p;first=false;
            }
            var stemEnd=c+new Vector2(0,size*.28f);
            DrawLine(prev,stemEnd,glow,size*.32f);DrawLine(prev,stemEnd,color,size*.14f);
            var dot=c+new Vector2(0,size*.62f);var ds=size*.13f;
            var pts=new[]{dot+new Vector2(0,-ds),dot+new Vector2(ds,0),dot+new Vector2(0,ds),dot+new Vector2(-ds,0)};
            for(var k=0;k<4;k++)DrawLine(pts[k],pts[(k+1)%4],color,size*.12f);
            Fill(new Rect(dot.x-ds*.5f,dot.y-ds*.5f,ds,ds),color);
        }

        // ---------------- omen bar ----------------
        // The odds as a split bar: the chance segment in gold (reward) or red (risk), the rest in shadow.
        private void DrawOmenBar(Rect r,float odds,bool good,string label,bool open)
        {
            var a=open?1f:.5f;var main=good?Gold:OmenRed;var rest=good?OmenRed:Gold;
            Fill(r,new Color(.025f,.02f,.03f,.95f*a));
            var split=r.x+r.width*Mathf.Clamp01(odds);
            Fill(new Rect(r.x,r.y,split-r.x,r.height),new Color(main.r*.32f,main.g*.26f,main.b*.2f,.95f*a));
            Fill(new Rect(r.x,r.y,split-r.x,3),new Color(main.r,main.g,main.b,.9f*a));
            // The remaining odds: faint diagonal hatching in the opposing color.
            for(var x=split+4;x<r.xMax-2;x+=7)DrawLine(new Vector2(x,r.yMax-2),new Vector2(Mathf.Min(r.xMax-2,x+8),r.y+2),new Color(rest.r,rest.g,rest.b,.16f*a),1);
            if(!profile.reduceFlashing&&CardVfxRepaint)ShardSoft(new Rect(r.x-6,r.y-8,split-r.x+12,r.height+16),new Color(main.r,main.g,main.b,.10f*a));
            // Divider gem at the split.
            if(odds>0&&odds<1){DrawLine(new Vector2(split,r.y-3),new Vector2(split,r.yMax+3),new Color(1,1,1,.6f*a),1.4f);EventGem(new Vector2(split,r.y-3),3.5f,new Color(main.r,main.g,main.b,a),true);}
            Outline(r,new Color(main.r,main.g,main.b,.75f*a),1);
            var style=EventChipStyle(15);style.alignment=TextAnchor.MiddleCenter;
            style.normal.textColor=new Color(0,0,0,.7f*a);GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),label,style);
            style.normal.textColor=good?new Color(1f,.93f,.72f,a):new Color(1f,.82f,.76f,a);GUI.Label(r,label,style);
        }

        // ---------------- outcome reveal ----------------
        // Returns the header color; draws the seal cracking open behind the header on the outcome screen.
        private Color DrawOutcomeSeal(Vector2 c,int omen)
        {
            var fortune=omen==1;var tone=fortune?MysteryGold:OmenRed;
            if(!CardVfxRepaint||omen==0)return tone;
            var k=(Time.unscaledTime-eventOutcomeShownAt)/(profile.reduceMotion?.01f:1f);
            if(k<.45f)
            {
                // The seal shakes, then cracks.
                var shake=profile.reduceMotion?Vector2.zero:new Vector2((CardVfxHash(Mathf.FloorToInt(Time.unscaledTime*50),3)-.5f)*k*14,(CardVfxHash(Mathf.FloorToInt(Time.unscaledTime*50),7)-.5f)*k*14);
                DrawFateEmblem(c+shake,30,true,true,true);
                var crackK=Mathf.Clamp01((k-.18f)/.27f);
                for(var cr=0;cr<7;cr++)
                {
                    var ang=cr*Mathf.PI*2/7+.3f;var prev=c+shake;var len=30*crackK;
                    for(var s=1;s<=3;s++){var next=c+shake+new Vector2(Mathf.Cos(ang+(CardVfxHash(cr,s)-.5f)*.7f),Mathf.Sin(ang+(CardVfxHash(cr,s)-.5f)*.7f))*len*s/3f;DrawLine(prev,next,new Color(1,1,1,.9f),2);prev=next;}
                }
            }
            else
            {
                // The seal bursts: a ring, a flash and shards flying out, tinted by the omen.
                var b=Mathf.Clamp01((k-.45f)/.6f);
                if(b<1)
                {
                    EnsureCardVfxTextures();var size=Mathf.Lerp(40,320,ShardEase(b));
                    DrawCardUiShape(new Rect(c.x-size*.5f,c.y-size*.5f,size,size),cardVfxRing,new Color(tone.r,tone.g,tone.b,.8f*(1-b)));
                    if(!profile.reduceFlashing)ShardSoft(c,180*(1-b*.5f),new Color(tone.r,tone.g,tone.b,.4f*(1-b)));
                    var count=profile.reducedVfx?8:22;
                    for(var i=0;i<count;i++)
                    {
                        var ang=i*Mathf.PI*2/count+(CardVfxHash(i,11)-.5f)*.4f;var dist=(60+CardVfxHash(i,13)*120)*ShardEase(b);
                        var p=c+new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*dist+new Vector2(0,60*b*b);
                        var col=i%3==0?Color.white:i%3==1?tone:MysteryViolet;col.a=1-b;ShardCrystal(p,ang+b*6,8+CardVfxHash(i,17)*8,2.6f,col);
                    }
                }
                if(!profile.reduceFlashing)ShardSoft(c,90,new Color(tone.r,tone.g,tone.b,.18f));
            }
            return tone;
        }
        private float EventOutcomeAge=>Time.unscaledTime-eventOutcomeShownAt;
    }
}
