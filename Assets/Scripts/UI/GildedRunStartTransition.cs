using GildedFate.Core;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool runStartActive,runStartBlackPresented,runStartCaptureFrozen;
        private HeroId runStartHero;
        private float runStartElapsed,runStartPortraitReveal;
        private Texture2D runStartGlow,runStartSoulCurtain;
        private Material runStartVeilMaterial;
        private RenderTexture runStartVeilSurface;
        private void PrepareRunStartMaterial()
        {
            if(runStartVeilMaterial)return;
            var shader=Resources.Load<Shader>("RunStartVeil");
            if(shader&&shader.isSupported)runStartVeilMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        }
        private void DisposeRunStartSurface()
        {
            if(runStartVeilSurface){runStartVeilSurface.Release();Destroy(runStartVeilSurface);}
            if(runStartVeilMaterial)Destroy(runStartVeilMaterial);
            if(runStartGlow)Destroy(runStartGlow);if(runStartSoulCurtain)Destroy(runStartSoulCurtain);
        }
        private bool DrawRunStartVeil(float w,float h,float t)
        {
            PrepareRunStartMaterial();if(!runStartVeilMaterial)return false;
            if(Event.current.type!=EventType.Repaint)return true;
            // Render at a bounded resolution; bilinear filtering keeps light smooth on
            // ultrawide displays without allocating a full native-resolution surface.
            var width=Mathf.Min(Screen.width,1600);var height=Mathf.Max(1,Mathf.RoundToInt(width*h/w));
            if(!runStartVeilSurface||runStartVeilSurface.width!=width||runStartVeilSurface.height!=height)
            {
                if(runStartVeilSurface){runStartVeilSurface.Release();Destroy(runStartVeilSurface);}
                runStartVeilSurface=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){name="Character run-start screen veil",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                runStartVeilSurface.Create();
            }
            runStartVeilMaterial.SetFloat("_Phase",t);runStartVeilMaterial.SetFloat("_Hero",(int)runStartHero);
            runStartVeilMaterial.SetFloat("_Aspect",w/h);
            runStartVeilMaterial.SetFloat("_Restraint",profile.reduceFlashing||profile.reducedVfx?1:0);
            var target=RenderTexture.active;Graphics.Blit(Texture2D.whiteTexture,runStartVeilSurface,runStartVeilMaterial);RenderTexture.active=target;
            GUI.DrawTexture(new Rect(0,0,w,h),runStartVeilSurface,ScaleMode.StretchToFill,true);
            if(runStartHero==HeroId.Hexer)
            {
                var convergence=StartEase(.46f,.71f,t);var center=new Vector2(w*.5f,h*.48f);
                for(var i=0;i<3;i++)
                {
                    var enter=StartEase(.02f+i*.085f,.15f+i*.085f,t);
                    var position=Vector2.Lerp(new Vector2(w*(.265f+i*.235f),h*(i==1?.34f:.57f)),center,convergence);
                    var size=h*.185f*Mathf.Lerp(.74f,1,enter)*Mathf.Lerp(1,.15f,convergence)*1.20f;
                    var old=GUI.color;GUI.color=new Color(1,1,1,enter*(1-StartEase(.64f,.79f,t)));
                    DrawRemainingSigilIcon((GildedFate.Combat.SigilKind)i,new Rect(position-Vector2.one*size*.5f,Vector2.one*size));GUI.color=old;
                }
            }
            return true;
        }
        private static float RunStartDuration(HeroId hero)=>hero==HeroId.Vanguard?.78f:hero==HeroId.Hexer?.84f:.82f;
        private float RunStartProgress=>Mathf.Clamp01(runStartElapsed/RunStartDuration(runStartHero));
        private static float StartEase(float from,float to,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,t));

        private void BeginRunStartTransition()
        {
            if(runStartActive)return;
            runStartHero=selectedHero;runStartElapsed=0;runStartBlackPresented=false;
            runStartPortraitReveal=Mathf.Clamp01((Time.unscaledTime-heroSelectionTime)*5f);
            runStartActive=true;transitionAlpha=0;hudNavigationIndex=-1;
        }
        private void AdvanceRunStart(float delta)
        {
            if(!runStartActive)return;
            runStartElapsed+=Mathf.Max(0,delta);
            // Even after a stalled frame, render an opaque black frame before committing.
            if(runStartElapsed<RunStartDuration(runStartHero)||!runStartBlackPresented)return;
            runStartActive=false;
            run.NewRun(runStartHero,System.Environment.TickCount);run.BeginFateweave();
            profile.runsPlayed++;ProfileService.Save(profile);SaveService.Save(run);
            mapFocusFloor=-1;screen=ScreenMode.Fateweave;previousScreen=screen;transitionAlpha=1;
            heldMenuAxis=Vector2Int.zero;menuAxisRepeatAt=Time.unscaledTime+.32f;
        }
        private void RunStartLight(Vector2 center,Vector2 size,Color color)
        {
            if(profile.reducedVfx)return;
            if(!runStartGlow)
            {
                const int n=96;runStartGlow=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Run-start contained light",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[n*n];for(var y=0;y<n;y++)for(var x=0;x<n;x++)
                {var d=new Vector2((x+ .5f)/n*2-1,(y+.5f)/n*2-1).magnitude;pixels[y*n+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),3));}
                runStartGlow.SetPixels(pixels);runStartGlow.Apply(false,true);
            }
            if(profile.reduceFlashing)color.a*=.4f;
            var before=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(center-size*.5f,size),runStartGlow);GUI.color=before;
        }
        private void DrawRunStartTransition(float w,float h)
        {
            var t=RunStartProgress;var oldColor=GUI.color;GUI.color=Color.white;
            if(t>=.88f)
            {
                Fill(new Rect(0,0,w,h),Color.black);
                if(Event.current.type==EventType.Repaint)runStartBlackPresented=true;
            }
            else
            {
                Fill(new Rect(0,0,w,h),new Color(0,0,0,StartEase(0,.30f,t)*.48f));
                if(!DrawRunStartVeil(w,h,t))
                {
                    // Preserve a functional transition on hardware without shader support.
                    if(runStartHero==HeroId.Vanguard)DrawVanguardRunStart(w,h,t);
                    else if(runStartHero==HeroId.Hexer)DrawHexerRunStart(w,h,t);
                    else DrawReaperRunStart(w,h,t);
                }
                Fill(new Rect(0,0,w,h),new Color(0,0,0,StartEase(.68f,.88f,t)));
            }
            GUI.color=oldColor;
        }
        private void DrawVanguardRunStart(float w,float h,float t)
        {
            var a=new Vector2(-w*.12f,h*.92f);var b=new Vector2(w*1.12f,h*.08f);
            var direction=(b-a).normalized;var normal=new Vector2(-direction.y,direction.x);
            var cut=StartEase(.02f,.28f,t);var tip=Vector2.Lerp(a,b,cut);
            var opening=StartEase(.26f,.77f,t)*h*2.8f;var fade=1-StartEase(.60f,.85f,t);
            // One decisive blade stroke followed by a widening, dark diagonal tear.
            DrawLine(a,tip,new Color(.42f,.015f,.04f,.90f),opening+22);
            if(opening>0)DrawLine(a,tip,Color.black,opening);
            foreach(var side in new[]{-1f,1f})
            {
                var offset=normal*(opening*.5f+3)*side;
                DrawLine(a+offset,tip+offset,new Color(.85f,.07f,.12f,fade),5);
                DrawLine(a+offset+normal*side*4,tip+offset+normal*side*4,new Color(1,.72f,.30f,fade*.9f),1.6f);
            }
            if(t<.35f)
            {
                RunStartLight(tip,new Vector2(220,110),new Color(1,.24f,.06f,.65f));
                DrawLine(tip-direction*120,tip,new Color(1,.91f,.64f,profile.reduceFlashing?.35f:.95f),3);
            }
            if(!profile.reduceMotion)for(var i=0;i<7;i++)
            {
                var p=Vector2.Lerp(a,b,.18f+i*.105f)+normal*(opening*.5f+9+i%3*7);
                DrawLine(p,p+direction*(16+i%3*9),new Color(.94f,.51f,.20f,fade*.65f),1);
            }
        }
        private void DrawHexerRunStart(float w,float h,float t)
        {
            var center=new Vector2(w*.5f,h*.48f);var convergence=StartEase(.46f,.70f,t);
            var radius=Mathf.Min(w*.105f,h*.20f);var strength=1-StartEase(.64f,.82f,t);
            for(var i=0;i<3;i++)
            {
                var enter=StartEase(.025f+i*.095f,.15f+i*.095f,t);if(enter<=0)continue;
                var origin=new Vector2(w*(.27f+i*.23f),h*(i==1?.36f:.57f));
                var position=Vector2.Lerp(origin,center,convergence);
                var size=radius*Mathf.Lerp(.84f,1,enter)*Mathf.Lerp(1,.22f,convergence);
                var color=i==0?new Color(1,.53f,.16f):i==1?new Color(.70f,.35f,1):new Color(.81f,.76f,1);
                color.a=enter*strength;
                RunStartLight(position,Vector2.one*size*3,new Color(color.r,color.g,color.b,color.a*.6f));
                // Three inscribed seals, not three identical recolored splashes.
                for(var ring=0;ring<2;ring++)for(var segment=0;segment<48;segment++)
                {
                    if(segment%12==0)continue;var angle=segment*Mathf.PI*2/48;
                    var next=(segment+1)*Mathf.PI*2/48;var r=size*(ring==0?1:.85f);
                    DrawLine(position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r,position+new Vector2(Mathf.Cos(next),Mathf.Sin(next))*r,new Color(color.r,color.g,color.b,color.a*.8f),ring==0?2:1);
                }
                var old=GUI.color;GUI.color=new Color(1,1,1,color.a);
                DrawRemainingSigilIcon((GildedFate.Combat.SigilKind)i,new Rect(position-Vector2.one*size*.67f,Vector2.one*size*1.34f));GUI.color=old;
                for(var mark=0;mark<6;mark++)
                {
                    var angle=(mark/6f+i*.08f)*Mathf.PI*2;var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    DrawLine(position+dir*size*.92f,position+dir*size*1.12f,new Color(.91f,.75f,.43f,color.a),2);
                }
                if(convergence>0)DrawLine(position,center,new Color(.90f,.71f,1,(1-convergence)*.6f),2);
            }
            var flash=Mathf.Sin(Mathf.InverseLerp(.60f,.79f,t)*Mathf.PI);
            RunStartLight(center,new Vector2(w*.9f,h*.85f),new Color(.83f,.71f,1,flash*(profile.reduceFlashing?.18f:.72f)));
        }
        private void DrawReaperRunStart(float w,float h,float t)
        {
            var sweep=StartEase(.04f,.77f,t);var front=Mathf.Lerp(-w*.28f,w*1.28f,sweep);
            var life=1-StartEase(.64f,.87f,t);
            // One filtered distance-field veil avoids scanline seams and hundreds of
            // overlapping glow draws. Only this screen mask moves, never the actor.
            if(!runStartSoulCurtain)
            {
                const int n=512;var pixels=new Color[n*n];
                runStartSoulCurtain=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Soul curtain distance field",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                for(var y=0;y<n;y++)for(var x=0;x<n;x++)
                {
                    var v=1-(y+.5f)/n;var u=(x+.5f)/n;
                    var edge=.5f+Mathf.Sin(v*5.2f+1.3f)*.17f+Mathf.Cos(v*9-1.7f)*.036f;
                    var d=u-edge;var alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.16f,d));
                    var mist=Mathf.Exp(-Mathf.Pow(d/.065f,2))*(.65f+.13f*Mathf.Sin(v*34+u*23));
                    pixels[y*n+x]=new Color(.10f*mist,.80f*mist,.61f*mist,alpha);
                }
                runStartSoulCurtain.SetPixels(pixels);runStartSoulCurtain.Apply(false,true);
            }
            var maskWidth=w*.5f;var maskLeft=front-maskWidth*.5f;
            Fill(new Rect(0,0,Mathf.Max(0,maskLeft+1),h),Color.black);
            var old=GUI.color;var intensity=(profile.reduceFlashing||profile.reducedVfx)?.35f:1;
            GUI.color=new Color(intensity,intensity,intensity,1);GUI.DrawTexture(new Rect(maskLeft,0,maskWidth,h),runStartSoulCurtain);GUI.color=old;
            for(var ribbon=0;ribbon<6;ribbon++)
            {
                var last=Vector2.zero;
                for(var step=0;step<=32;step++)
                {
                    var u=step/32f;var y=h*(.08f+ribbon*.17f)+Mathf.Sin(u*4.8f+ribbon*.9f+t*4)*h*.09f;
                    var p=new Vector2(front-u*w*.30f+Mathf.Sin(y/h*5.2f+1.3f)*w*.085f+Mathf.Cos(y/h*9-1.7f)*w*.018f,y);
                    if(step>0)DrawLine(last,p,new Color(.32f,1,.82f,life*(1-u)*.62f),Mathf.Lerp(3,.6f,u));last=p;
                }
            }
        }
    }
}
