using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    // Character-select polish ("the lit pedestal"): the selected hero stands in an
    // accent-coloured light pool with a vertical light shaft and slowly rising
    // themed particles (Vanguard embers/sparks, Hexer rotating violet runes,
    // Reaper swaying teal soul wisps). Switching hero plays a quick accent flash
    // sweep over the portrait and a wipe-reveal of the tagline/lore/deck text.
    // The selected selector button breathes an accent rim and is linked to the
    // big portrait by a thin gold thread. Soft texture generated once in code
    // (HideAndDontSave). Honors reduceMotion (static motes, no sweep/breath),
    // reducedVfx (fewer particles) and reduceFlashing (softer flash/pulses).
    public sealed partial class GildedMainMenu
    {
        private Texture2D hsGlow;
        private Rect hsStageRect;
        private bool HsMotion=>profile==null||!profile.reduceMotion;
        private bool HsLowVfx=>profile!=null&&profile.reducedVfx;
        private bool HsSoftFlash=>profile!=null&&profile.reduceFlashing;
        private float HsSince=>Time.unscaledTime-heroSelectionTime;

        private Texture2D HsGlowTexture()
        {
            if(hsGlow)return hsGlow;
            const int n=64;
            hsGlow=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Hero select glow",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var px=new Color32[n*n];
            for(var y=0;y<n;y++)for(var x=0;x<n;x++)
            {
                var dx=(x+.5f)/n*2f-1f;var dy=(y+.5f)/n*2f-1f;var d=Mathf.Clamp01(1f-Mathf.Sqrt(dx*dx+dy*dy));
                var a=d*d*(3f-2f*d);px[y*n+x]=new Color32(255,255,255,(byte)(a*255f));
            }
            hsGlow.SetPixels32(px);hsGlow.Apply(false,true);return hsGlow;
        }

        private void HsTint(Rect r,Color c)
        {
            if(c.a<=.002f)return;var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,HsGlowTexture(),ScaleMode.StretchToFill,true);GUI.color=old;
        }

        private static float HsHash(int i,int salt){var v=Mathf.Sin(i*127.1f+salt*311.7f)*43758.5453f;return v-Mathf.Floor(v);}

        // Hook: DrawCharacterSelect, right after the big DrawSelectedHeroArtwork call.
        private void DrawHeroSelectPedestal(Rect stage)
        {
            hsStageRect=stage;
            if(Event.current==null||Event.current.type!=EventType.Repaint)return;
            var accent=HeroAccent(selectedHero);var time=Time.unscaledTime;
            var breathe=HsMotion?.5f+.5f*Mathf.Sin(time*1.25f):.5f;
            var cx=stage.center.x;var poolY=stage.yMax-150f;

            // Light shaft from above (wide soft column + narrow core), tapering into the pool.
            var shaftTop=stage.y-40f;var shaftH=poolY-shaftTop+30f;
            HsTint(new Rect(cx-150f,shaftTop,300f,shaftH*1.1f),new Color(accent.r,accent.g,accent.b,.07f+.02f*breathe));
            HsTint(new Rect(cx-70f,shaftTop,140f,shaftH),new Color(Mathf.Lerp(accent.r,1f,.5f),Mathf.Lerp(accent.g,1f,.5f),Mathf.Lerp(accent.b,1f,.5f),.07f+.03f*breathe));
            HsTint(new Rect(cx-24f,shaftTop,48f,shaftH*.95f),new Color(1f,.97f,.9f,.05f+.02f*breathe));

            // Pedestal pool: outer accent ellipse, warm inner core, thin gilt rim line.
            HsTint(new Rect(cx-stage.width*.33f,poolY-44f,stage.width*.66f,88f),new Color(accent.r,accent.g,accent.b,.30f+.08f*breathe));
            HsTint(new Rect(cx-stage.width*.17f,poolY-18f,stage.width*.34f,36f),new Color(Mathf.Lerp(accent.r,1f,.6f),Mathf.Lerp(accent.g,.95f,.6f),Mathf.Lerp(accent.b,.85f,.6f),.26f+.08f*breathe));
            Fill(new Rect(cx-stage.width*.12f,poolY+10f,stage.width*.24f,1f),new Color(1f,.84f,.46f,.22f+.12f*breathe));

            DrawHeroSelectParticles(stage,cx,poolY,accent,time);
            DrawHeroSelectFlash(stage,accent);
        }

        private void DrawHeroSelectParticles(Rect stage,float cx,float poolY,Color accent,float time)
        {
            var count=HsLowVfx?8:20;var rise=Mathf.Min(stage.height*.72f,poolY-stage.y-10f);var spread=stage.width*.46f;
            for(var i=0;i<count;i++)
            {
                var h0=HsHash(i,1);var h1=HsHash(i,2);var h2=HsHash(i,3);
                var speed=selectedHero==HeroId.Hexer?.07f:selectedHero==HeroId.Reaper?.085f:.11f;
                var t=HsMotion?Mathf.Repeat(time*speed*(.7f+h1*.6f)+h0,1f):.15f+h0*.7f;
                var fade=Mathf.Sin(t*Mathf.PI);if(!HsMotion)fade*=.6f;
                var baseX=cx+(h2-.5f)*spread*(1f-.35f*t);var y=poolY-t*rise;
                if(selectedHero==HeroId.Vanguard)
                {
                    var x=baseX+(HsMotion?Mathf.Sin(time*2.3f+i*1.7f)*6f*t:0f);
                    var c=Color.Lerp(new Color(.92f,.18f,.12f),new Color(1f,.80f,.32f),h1);var size=5f+h1*6f;
                    HsTint(new Rect(x-size,y-size,size*2f,size*2f),new Color(c.r,c.g,c.b,.55f*fade));
                    Fill(new Rect(x-1f,y-1f,2f,2f),new Color(1f,.93f,.7f,.85f*fade));
                    if(i%3==0&&HsMotion)DrawLine(new Vector2(x,y),new Vector2(x-1.5f,y+7f+h0*6f),new Color(1f,.72f,.3f,.55f*fade),1f);
                }
                else if(selectedHero==HeroId.Hexer)
                {
                    var x=baseX+(HsMotion?Mathf.Sin(time*.8f+i)*10f:0f);
                    var angle=HsMotion?time*(.6f+h1*.9f)*(i%2==0?1f:-1f)+i:i*.9f;var s=5f+h1*5f;
                    var c=new Color(.74f,.5f,1f,.75f*fade);
                    HsTint(new Rect(x-s*2.2f,y-s*2.2f,s*4.4f,s*4.4f),new Color(.55f,.3f,.95f,.28f*fade));
                    DrawHeroSelectRune(new Vector2(x,y),s,angle,i%4,c);
                }
                else
                {
                    var sway=HsMotion?Mathf.Sin(time*1.4f+i*2.1f+t*6f)*16f*t:0f;var x=baseX+sway;
                    var c=new Color(.3f,.95f,.86f,1f);
                    for(var k=3;k>=0;k--)
                    {
                        var tx=HsMotion?x-Mathf.Sin(time*1.4f+i*2.1f+(t-.03f*k)*6f)*4f*k*t:x;var ty=y+k*6f;var s=(7f-k*1.3f)*(.8f+h1*.5f);
                        HsTint(new Rect(tx-s*.7f,ty-s*1.3f,s*1.4f,s*2.6f),new Color(c.r,c.g,c.b,(.5f-k*.1f)*fade));
                    }
                    Fill(new Rect(x-1f,y-1f,2f,2f),new Color(.85f,1f,.97f,.7f*fade));
                }
            }
        }

        private void DrawHeroSelectRune(Vector2 at,float s,float angle,int kind,Color c)
        {
            var cs=Mathf.Cos(angle);var sn=Mathf.Sin(angle);
            Vector2 P(float x,float y)=>new Vector2(at.x+(x*cs-y*sn)*s,at.y+(x*sn+y*cs)*s);
            if(kind==0){DrawLine(P(0,-1),P(.87f,.5f),c,1);DrawLine(P(.87f,.5f),P(-.87f,.5f),c,1);DrawLine(P(-.87f,.5f),P(0,-1),c,1);DrawLine(P(0,-1),P(0,.5f),c,1);}
            else if(kind==1){DrawLine(P(0,-1),P(1,0),c,1);DrawLine(P(1,0),P(0,1),c,1);DrawLine(P(0,1),P(-1,0),c,1);DrawLine(P(-1,0),P(0,-1),c,1);DrawLine(P(-.5f,0),P(.5f,0),c,1);}
            else if(kind==2){DrawLine(P(0,-1),P(0,1),c,1);DrawLine(P(0,-.4f),P(.7f,-1),c,1);DrawLine(P(0,-.4f),P(-.7f,-1),c,1);DrawLine(P(-.5f,.6f),P(.5f,.6f),c,1);}
            else{DrawLine(P(-1,-1),P(1,1),c,1);DrawLine(P(1,-1),P(-1,1),c,1);DrawLine(P(-.6f,-1),P(.6f,-1),c,1);}
        }

        private void DrawHeroSelectFlash(Rect stage,Color accent)
        {
            if(runStartActive)return;var since=HsSince;const float dur=.45f;if(since<0f||since>=dur)return;
            var p=since/dur;var portrait=new Rect(stage.x,stage.y,stage.width,stage.height-132f);
            if(!HsMotion){if(!HsSoftFlash)Fill(portrait,new Color(accent.r,accent.g,accent.b,.08f*(1f-p)));return;}
            var e=1f-(1f-p)*(1f-p);var bandW=Mathf.Max(140f,stage.width*.22f);var x=Mathf.Lerp(portrait.x-bandW,portrait.xMax,e);
            var strength=(HsSoftFlash?.12f:.34f)*(1f-p*.7f);
            HsTint(new Rect(x,portrait.y-portrait.height*.1f,bandW,portrait.height*1.2f),new Color(Mathf.Lerp(accent.r,1f,.45f),Mathf.Lerp(accent.g,1f,.45f),Mathf.Lerp(accent.b,1f,.45f),strength));
            HsTint(new Rect(x+bandW*.35f,portrait.y,bandW*.3f,portrait.height),new Color(1f,.97f,.9f,strength*.7f));
        }

        // Hook: DrawCharacterSelect, after the starting-deck label (before the confirm button).
        // Wipe-reveals tagline, lore and deck text with a feathered edge (IMGUI labels can't
        // animate letter spacing cheaply, so this masks with the panel/band colour instead).
        private void DrawHeroSelectTextReveal(Rect panel)
        {
            if(runStartActive||Event.current==null||Event.current.type!=EventType.Repaint)return;
            var since=HsSince;
            var panelColor=new Color(.009f,.014f,.024f,1f);var bandColor=new Color(.006f,.009f,.016f,.9f);
            var stage=hsStageRect;
            HsRevealMask(new Rect(stage.x+26,stage.yMax-74,stage.width-52,34),bandColor,since,.02f,.40f);
            HsRevealMask(new Rect(panel.x+29,panel.y+121,panel.width-58,149),panelColor,since,.08f,.55f);
            HsRevealMask(new Rect(panel.x+29,panel.y+283,panel.width-58,96),panelColor,since,.20f,.45f);
        }

        private void HsRevealMask(Rect r,Color c,float since,float delay,float dur)
        {
            var p=(since-delay)/dur;if(p>=1f)return;
            if(!HsMotion){if(p<0f)p=0f;Fill(r,new Color(c.r,c.g,c.b,c.a*(1f-p)));return;}
            if(p<=0f){Fill(r,c);return;}
            var e=Mathf.SmoothStep(0f,1f,p);const float feather=48f;var edge=Mathf.Lerp(r.x-feather,r.xMax,e);
            var solidX=edge+feather;if(solidX<r.xMax)Fill(new Rect(solidX,r.y,r.xMax-solidX,r.height),c);
            for(var k=0;k<6;k++)
            {
                var sx=edge+k*feather/6f;var sw=feather/6f;var x0=Mathf.Max(sx,r.x);var x1=Mathf.Min(sx+sw,r.xMax);if(x1<=x0)continue;
                Fill(new Rect(x0,r.y,x1-x0,r.height),new Color(c.r,c.g,c.b,c.a*(k+1)/7f));
            }
        }

        // Hook: end of DrawHeroSelector. Breathing accent rim + gold thread to the portrait.
        private void DrawHeroSelectorPolish(Rect r,HeroId hero)
        {
            if(selectedHero!=hero||Event.current==null||Event.current.type!=EventType.Repaint)return;
            var accent=HeroAccent(hero);var time=Time.unscaledTime;
            var b=HsMotion?.5f+.5f*Mathf.Sin(time*(HsSoftFlash?1.4f:2.2f)):.6f;var amp=HsSoftFlash?.5f:1f;
            var grow=3f+b*3f*amp;
            Outline(new Rect(r.x-grow-4,r.y-grow-4,r.width+(grow+4)*2,r.height+(grow+4)*2),new Color(accent.r,accent.g,accent.b,(.25f+.35f*b*amp)),2);
            Outline(new Rect(r.x-grow-8,r.y-grow-8,r.width+(grow+8)*2,r.height+(grow+8)*2),new Color(accent.r,accent.g,accent.b,(.08f+.16f*b*amp)),1);

            var stage=hsStageRect;if(stage.width<=0f)return;
            var start=new Vector2(r.center.x,r.y-12f);var end=new Vector2(stage.center.x,stage.yMax-132f);
            var control=new Vector2(start.x,Mathf.Min(start.y,end.y)-26f);
            var gold=new Color(1f,.80f,.38f,.55f);const int segs=18;var prev=start;
            for(var i=1;i<=segs;i++)
            {
                var t=i/(float)segs;var u=1f-t;var pt=u*u*start+2f*u*t*control+t*t*end;
                DrawLine(prev,pt,new Color(gold.r,gold.g,gold.b,gold.a*(.45f+.55f*(1f-Mathf.Abs(t-.5f)*2f)*.6f+.2f)),1f);prev=pt;
            }
            Fill(new Rect(end.x-2f,end.y-2f,4f,4f),new Color(1f,.88f,.52f,.8f));Fill(new Rect(start.x-1.5f,start.y-1.5f,3f,3f),new Color(1f,.88f,.52f,.8f));
            if(HsMotion)
            {
                var t=Mathf.Repeat(time*.45f,1f);var u=1f-t;var bead=u*u*start+2f*u*t*control+t*t*end;
                HsTint(new Rect(bead.x-7f,bead.y-7f,14f,14f),new Color(1f,.85f,.45f,.7f*Mathf.Sin(t*Mathf.PI)));
            }
        }
    }
}
