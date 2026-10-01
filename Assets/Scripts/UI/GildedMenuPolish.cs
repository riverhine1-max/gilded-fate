using UnityEngine;

namespace GildedFate.UI
{
    // Main-menu polish ("living vault"): two drifting fog layers, flickering brazier
    // light, a breathing glow along the vault door seam, a breathing logo halo with a
    // periodic diagonal light sweep, and gilded-plaque menu buttons (gold thread
    // underline drawn from the center, travelling glint, subtle lift). Soft textures
    // are generated once (HideAndDontSave). Everything is skipped while the boot intro
    // is active. Honors reduceMotion (no drift/sweep/lift, static glows), reducedVfx
    // (single fog layer, fewer extras) and reduceFlashing (no flicker, softer pulses).
    public sealed partial class GildedMainMenu
    {
        private static Texture2D menuLiveGlow,menuLiveFog,menuLiveSweep;
        private static readonly Color MenuLiveGold=new Color(1f,.78f,.36f);
        private float[] menuLiveHover=new float[0];

        private bool MenuLiveMotion=>profile==null||!profile.reduceMotion;
        private bool MenuLiveLowVfx=>profile!=null&&profile.reducedVfx;
        private bool MenuLiveSoftFlash=>profile!=null&&profile.reduceFlashing;

        private static void MenuLiveEnsureTextures()
        {
            if(!menuLiveGlow)
            {
                const int n=64;menuLiveGlow=new Texture2D(n,n,TextureFormat.RGBA32,false){name="MenuLiveGlow",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var px=new Color[n*n];var mid=(n-1)*.5f;
                for(var y=0;y<n;y++)for(var x=0;x<n;x++){var d=Mathf.Clamp01(new Vector2(x-mid,y-mid).magnitude/(n*.5f));var a=1f-d;px[y*n+x]=new Color(1,1,1,a*a*(3f-2f*a));}
                menuLiveGlow.SetPixels(px);menuLiveGlow.Apply(false,true);
            }
            if(!menuLiveFog)
            {
                // Horizontally tileable soft noise (wrapped sines) with a baked vertical falloff, alpha only.
                const int n=128;menuLiveFog=new Texture2D(n,n,TextureFormat.RGBA32,false){name="MenuLiveFog",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp};
                var px=new Color[n*n];
                for(var y=0;y<n;y++)for(var x=0;x<n;x++)
                {
                    var u=x/(float)n*Mathf.PI*2f;var v=y/(float)n*Mathf.PI*2f;
                    var f=Mathf.Sin(u+Mathf.Sin(v)*1.3f)*.5f+Mathf.Sin(u*2f+v+1.7f)*.28f+Mathf.Sin(u*3f-v*2f+.4f)*.16f+Mathf.Sin(v*2f+2.1f)*.22f;
                    var a=Mathf.Clamp01(f*.5f+.42f);a=a*a*(3f-2f*a)*Mathf.Sin(Mathf.PI*y/(n-1f));px[y*n+x]=new Color(1,1,1,a);
                }
                menuLiveFog.SetPixels(px);menuLiveFog.Apply(false,true);
            }
            if(!menuLiveSweep)
            {
                // Soft vertical band (alpha peaks in the middle column), rotated when drawn.
                const int n=64;menuLiveSweep=new Texture2D(n,4,TextureFormat.RGBA32,false){name="MenuLiveSweep",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var px=new Color[n*4];
                for(var y=0;y<4;y++)for(var x=0;x<n;x++){var d=Mathf.Abs(x-(n-1)*.5f)/(n*.5f);var a=Mathf.Clamp01(1f-d);px[y*n+x]=new Color(1,1,1,a*a);}
                menuLiveSweep.SetPixels(px);menuLiveSweep.Apply(false,true);
            }
        }

        private static void MenuLiveDraw(Rect r,Texture2D tex,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,tex,ScaleMode.StretchToFill,true);GUI.color=old;}

        // Maps normalized coordinates of the 1672x941 vault art through DrawBackdrop's ScaleAndCrop.
        private Vector2 MenuLiveVaultPoint(float w,float h,float nx,float ny,out float scale)
        {
            float texW=vaultBackground?vaultBackground.width:1672f,texH=vaultBackground?vaultBackground.height:941f;
            scale=Mathf.Max(w/texW,h/texH);var dw=texW*scale;var dh=texH*scale;
            return new Vector2((w-dw)*.5f+nx*dw,(h-dh)*.5f+ny*dh);
        }

        // Hook: menu branch, before the logo. Fog, braziers, door seam.
        private void MenuLiveBackdrop(float w,float h)
        {
            if(bootIntroActive||Event.current.type!=EventType.Repaint)return;
            MenuLiveEnsureTextures();
            var t=Time.unscaledTime;var motion=MenuLiveMotion;var soft=MenuLiveSoftFlash;

            // Fog: two layers drifting at different speeds/directions, weighted toward the floor.
            var layers=MenuLiveLowVfx?1:2;
            for(var l=0;l<layers;l++)
            {
                var speed=l==0?.006f:-.0042f;var tile=l==0?1.35f:.9f;
                var off=motion?t*speed:l*.37f;var bob=motion?Mathf.Sin(t*.11f+l*2f)*.02f:0f;
                var band=new Rect(-8,h*((l==0?.52f:.38f)+bob),w+16,h*(l==0?.52f:.4f));
                var old=GUI.color;GUI.color=new Color(.62f,.66f,.78f,l==0?.16f:.1f);
                GUI.DrawTextureWithTexCoords(band,menuLiveFog,new Rect(off+l*.5f,0f,tile*w/Mathf.Max(1f,h),1f),true);
                GUI.color=old;
            }

            // Braziers at ~20% / 80%, y 57% of the art.
            for(var i=0;i<2;i++)
            {
                var p=MenuLiveVaultPoint(w,h,i==0?.2f:.8f,.57f,out var s);
                var flicker=soft?.9f:.8f+(Mathf.PerlinNoise(t*(motion?3.1f:.4f),i*7.3f)-.5f)*.5f+Mathf.Sin(t*11f+i*2.3f)*(motion?.05f:0f);
                if(!motion&&!soft)flicker=.85f+Mathf.Sin(t*.8f+i)*.05f;
                var size=Mathf.Max(90f,300f*s)*(.94f+flicker*.08f);
                MenuLiveDraw(new Rect(p.x-size*.5f,p.y-size*.55f,size,size),menuLiveGlow,new Color(1f,.55f,.18f,.2f*flicker));
                var core=size*.38f;
                MenuLiveDraw(new Rect(p.x-core*.5f,p.y-core*.62f,core,core),menuLiveGlow,new Color(1f,.78f,.4f,.28f*flicker));
            }

            // Door seam breathing: x 50%, y 19%-75%.
            var top=MenuLiveVaultPoint(w,h,.5f,.19f,out var ss);var bottom=MenuLiveVaultPoint(w,h,.5f,.75f,out _);
            var breathe=motion?.5f+.5f*Mathf.Sin(t*(soft?.7f:1.05f)):.6f;
            var seamH=bottom.y-top.y;var glowW=Mathf.Max(26f,70f*ss)*(.85f+breathe*.3f);
            MenuLiveDraw(new Rect(top.x-glowW*.5f,top.y-glowW*.3f,glowW,seamH+glowW*.6f),menuLiveGlow,new Color(1f,.7f,.3f,.08f+breathe*.1f));
            Fill(new Rect(top.x-1f,top.y,2f,seamH),new Color(1f,.82f,.46f,.14f+breathe*.2f));
            Fill(new Rect(top.x-.5f,top.y+seamH*.08f,1f,seamH*.84f),new Color(1f,.93f,.72f,.1f+breathe*.18f));
        }

        private Rect MenuLiveLogoRect(Rect box)
        {
            if(!logoTexture)return box;var aspect=logoTexture.width/(float)Mathf.Max(1,logoTexture.height);
            if(box.width/box.height>aspect){var fw=box.height*aspect;return new Rect(box.center.x-fw*.5f,box.y,fw,box.height);}
            var fh=box.width/aspect;return new Rect(box.x,box.center.y-fh*.5f,box.width,fh);
        }

        // Hook: before the logo DrawTexture. Breathing halo behind the fitted logo.
        private void MenuLiveLogoBack(Rect box)
        {
            if(bootIntroActive||!logoTexture||Event.current.type!=EventType.Repaint)return;
            MenuLiveEnsureTextures();var r=MenuLiveLogoRect(box);
            var breathe=MenuLiveMotion?.5f+.5f*Mathf.Sin(Time.unscaledTime*(MenuLiveSoftFlash?.6f:.9f)):.5f;
            var gw=r.width*(1.02f+breathe*.06f);var gh=r.height*(1.1f+breathe*.1f);
            MenuLiveDraw(new Rect(r.center.x-gw*.5f,r.center.y-gh*.5f,gw,gh),menuLiveGlow,new Color(1f,.66f,.26f,.12f+breathe*(MenuLiveSoftFlash?.05f:.1f)));
        }

        // Hook: after the logo DrawTexture. Diagonal light sweep every ~7 s, clipped to the logo rect.
        private void MenuLiveLogoSweep(Rect box)
        {
            if(bootIntroActive||!logoTexture||!MenuLiveMotion||Event.current.type!=EventType.Repaint)return;
            const float period=7f,dur=1.25f;var phase=Mathf.Repeat(Time.unscaledTime,period);if(phase>dur)return;
            MenuLiveEnsureTextures();var r=MenuLiveLogoRect(box);var k=Mathf.SmoothStep(0f,1f,phase/dur);
            var fade=Mathf.Sin(k*Mathf.PI)*(MenuLiveSoftFlash?.1f:.2f);
            GUI.BeginClip(r);
            var bandW=r.width*.12f;var bandH=r.height*2.2f;var x=Mathf.Lerp(-bandW*2f,r.width+bandW*2f,k);
            var oldM=GUI.matrix;GUIUtility.RotateAroundPivot(22f,new Vector2(x,r.height*.5f));
            MenuLiveDraw(new Rect(x-bandW*.5f,r.height*.5f-bandH*.5f,bandW,bandH),menuLiveSweep,new Color(1f,.94f,.76f,fade));
            MenuLiveDraw(new Rect(x-bandW*.12f,r.height*.5f-bandH*.5f,bandW*.24f,bandH),menuLiveSweep,new Color(1f,1f,.92f,fade*.8f));
            GUI.matrix=oldM;GUI.EndClip();
        }

        private float MenuLiveHoverAmount(int index,bool over)
        {
            if(menuLiveHover.Length<=index){var grown=new float[index+1];System.Array.Copy(menuLiveHover,grown,menuLiveHover.Length);menuLiveHover=grown;}
            if(Event.current.type==EventType.Repaint)
            {
                var dt=Mathf.Clamp(Time.unscaledDeltaTime,0f,.1f);
                var target=over?1f:0f;menuLiveHover[index]=MenuLiveMotion?Mathf.MoveTowards(menuLiveHover[index],target,dt*(over?4.2f:5.5f)):target;
            }
            return menuLiveHover[index];
        }

        // Hook: after `var over=...` in the menu-button loop. Returns the lifted rect.
        private Rect MenuLiveLift(Rect rect,bool over,int index)
        {
            if(bootIntroActive)return rect;var a=MenuLiveHoverAmount(index,over);
            if(!MenuLiveMotion)return rect;rect.y-=Mathf.SmoothStep(0f,1f,a)*2.5f;return rect;
        }

        // Hook: after DrawButtonFrame in the menu-button loop. Gold thread underline + glint.
        private void MenuLivePlaque(Rect rect,bool over,int index)
        {
            if(bootIntroActive||Event.current.type!=EventType.Repaint)return;
            var a=index<menuLiveHover.Length?menuLiveHover[index]:(over?1f:0f);if(a<=.001f)return;
            MenuLiveEnsureTextures();var e=Mathf.SmoothStep(0f,1f,a);
            var cx=rect.center.x;var half=(rect.width*.5f-22f)*e;var y=rect.yMax-7f;
            Fill(new Rect(cx-half,y,half*2f,1.5f),new Color(1f,.8f,.38f,.85f*e));
            Fill(new Rect(cx-half*.8f,y+1.5f,half*1.6f,1f),new Color(.55f,.36f,.12f,.6f*e));
            if(!MenuLiveLowVfx)MenuLiveDraw(new Rect(cx-half-6f,y-5f,half*2f+12f,11f),menuLiveGlow,new Color(1f,.7f,.3f,.16f*e));
            // Knots at the thread ends.
            Fill(new Rect(cx-half-2f,y-1f,3f,3.5f),new Color(1f,.88f,.55f,.9f*e));Fill(new Rect(cx+half-1f,y-1f,3f,3.5f),new Color(1f,.88f,.55f,.9f*e));
            if(!MenuLiveMotion||half<8f)return;
            // Travelling glint: bounces end to end along the drawn thread.
            var ping=Mathf.PingPong(Time.unscaledTime*.55f+index*.21f,1f);var gx=Mathf.Lerp(cx-half,cx+half,Mathf.SmoothStep(0f,1f,ping));
            var gs=MenuLiveSoftFlash?10f:14f;var ga=(MenuLiveSoftFlash?.45f:.8f)*e;
            MenuLiveDraw(new Rect(gx-gs*.5f,y+.75f-gs*.5f,gs,gs),menuLiveGlow,new Color(1f,.95f,.78f,ga));
            Fill(new Rect(gx-3f,y,6f,1.5f),new Color(1f,1f,.9f,ga));
        }
    }
}
