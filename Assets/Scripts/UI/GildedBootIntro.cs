using GildedFate.Audio;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    // Boot cinematic: plays once per launch on the first main-menu arrival.
    // Mirrors the run-start overlay pattern: a flag advanced in Update with unscaled
    // time, drawn in OnGUI over the menu, blocking menu input while active. The final
    // 0.5 s hands the logo to its exact menu rect while the real menu fades in beneath.
    public sealed partial class GildedMainMenu
    {
        private bool bootIntroActive,bootIntroLaunchChecked,bootIntroForced;
        private float bootIntroElapsed;
        private int bootIntroCueMask;
        private Texture2D bootIntroGlow;

        private const float BootIntroSettleStart=8.0f,BootIntroDuration=8.5f,BootIntroReducedDuration=1.2f,BootIntroSkipGrace=.3f;
        private static readonly Color BootVanguard=new Color(1,.36f,.2f),BootHexer=new Color(.72f,.36f,1),BootReaper=new Color(.22f,.95f,.85f);
        private static readonly float[] BootCueTimes={1.2f,3.4f,4.0f,5.0f,7.1f,7.25f};

        private float BootIntroLength=>profile!=null&&profile.reduceMotion?BootIntroReducedDuration:BootIntroDuration;
        private static float BootEase(float from,float to,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,t));
        private static float BootHash(int i,int salt){var v=Mathf.Sin(i*12.9898f+salt*78.233f)*43758.5453f;return v-Mathf.Floor(v);}

        /// <summary>Start() hook: plays the cinematic once per launch, never under capture or the trailer harness.</summary>
        private void TryBeginLaunchBootIntro()
        {
            if(bootIntroLaunchChecked)return;bootIntroLaunchChecked=true;
            if(captureMode||screen!=ScreenMode.Menu)return;
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-gfTrailer")>=0)return;
            BeginBootIntro(false);
        }

        /// <summary>Begin the boot cinematic. forced=true (trailer harness) disables skipping.</summary>
        private void BeginBootIntro(bool forced)
        {
            bootIntroLaunchChecked=true;bootIntroForced=forced;
            bootIntroActive=true;bootIntroElapsed=0;bootIntroCueMask=0;
            screen=ScreenMode.Menu;previousScreen=screen;transitionAlpha=0;
        }

        private void AdvanceBootIntro(float delta)
        {
            if(!bootIntroActive)return;
            UpdateAudioPresentation();
            // Hold on the black first frame until Unity's splash has finished; clamp load
            // hitches so the opening beats are never skipped by a stalled frame.
            if(!UnityEngine.Rendering.SplashScreen.isFinished)return;
            delta=Mathf.Clamp(delta,0,bootIntroForced?.5f:.1f);bootIntroElapsed+=delta;shimmer+=delta;
            if(!bootIntroForced&&BootIntroSkipPressed())SkipBootIntro();
            if(profile==null||!profile.reduceMotion)
                for(var i=0;i<BootCueTimes.Length;i++)
                {
                    if((bootIntroCueMask&(1<<i))!=0||bootIntroElapsed<BootCueTimes[i])continue;
                    bootIntroCueMask|=1<<i;PlayBootCue(i);
                }
            if(bootIntroElapsed>=BootIntroLength)
            {
                bootIntroActive=false;
                heldMenuAxis=Vector2Int.zero;menuAxisRepeatAt=Time.unscaledTime+.25f;
            }
        }

        private void PlayBootCue(int index)
        {
            switch(index)
            {
                case 0:Sfx(SoundCue.EventWhisper,intensity:.7f);break;
                case 1:Sfx(SoundCue.Resonance,intensity:.9f);break;
                case 2:Sfx(SoundCue.Fateweave,intensity:.75f);break;
                case 3:Sfx(SoundCue.BossPhase,intensity:.7f);break;
                case 4:Sfx(SoundCue.HitHeavy,intensity:.75f);break;
                case 5:Sfx(SoundCue.EventReveal,intensity:.6f);break;
            }
        }

        private bool BootIntroSkipPressed()
        {
            if(bootIntroElapsed<BootIntroSkipGrace)return false;
            var keyboard=Keyboard.current;if(keyboard!=null&&keyboard.anyKey!=null&&keyboard.anyKey.wasPressedThisFrame)return true;
            var mouse=Mouse.current;if(mouse!=null&&((mouse.leftButton!=null&&mouse.leftButton.wasPressedThisFrame)||(mouse.rightButton!=null&&mouse.rightButton.wasPressedThisFrame)))return true;
            var pad=Gamepad.current;
            if(pad!=null)
            {
                var buttons=new[]{pad.buttonSouth,pad.buttonEast,pad.buttonWest,pad.buttonNorth,pad.startButton,pad.selectButton,pad.leftShoulder,pad.rightShoulder};
                foreach(var b in buttons)if(b!=null&&b.wasPressedThisFrame)return true;
            }
            return false;
        }

        private void SkipBootIntro()
        {
            if(!bootIntroActive||bootIntroForced||bootIntroElapsed<BootIntroSkipGrace)return;
            var target=profile!=null&&profile.reduceMotion?BootIntroLength:BootIntroSettleStart;
            if(bootIntroElapsed>=target)return;
            bootIntroElapsed=target;
            // Beats that were skipped stay silent.
            for(var i=0;i<BootCueTimes.Length;i++)if(BootCueTimes[i]<=target)bootIntroCueMask|=1<<i;
        }

        // ---------- geometry ----------
        private Rect BootMenuLogoRect(float w,float h)
        {
            // Must match the logo draw in the main-menu branch of OnGUI (ScaleToFit).
            var contentW=Mathf.Min(720f,w*.66f);var left=(w-contentW)*.5f;var titleY=Mathf.Max(55f,h*.095f);
            var box=new Rect(left-130,titleY-70,contentW+260,190);
            var aspect=logoTexture?logoTexture.width/(float)logoTexture.height:2f;
            if(box.width/box.height>aspect){var fw=box.height*aspect;return new Rect(box.x+(box.width-fw)*.5f,box.y,fw,box.height);}
            var fh=box.width/aspect;return new Rect(box.x,box.y+(box.height-fh)*.5f,box.width,fh);
        }
        private Rect BootHeroLogoRect(float w,float h)
        {
            var aspect=logoTexture?logoTexture.width/(float)logoTexture.height:2f;
            var lw=Mathf.Min(w*.64f,h*.62f*aspect,940f);var lh=lw/aspect;
            return new Rect(w*.5f-lw*.5f,h*.47f-lh*.5f,lw,lh);
        }
        private Rect BootVaultRect(float w,float h,float zoom)
        {
            var tw=vaultBackground?vaultBackground.width:1672f;var th=vaultBackground?vaultBackground.height:941f;
            var s=Mathf.Max(w/tw,h/th)*zoom;var rw=tw*s;var rh=th*s;
            return new Rect(w*.5f-rw*.5f,h*.5f-rh*.5f,rw,rh);
        }
        private static Rect BootSub(Rect r,float x0,float y0,float x1,float y1)=>new Rect(r.x+r.width*x0,r.y+r.height*y0,r.width*(x1-x0),r.height*(y1-y0));
        // Logo parts in top-origin normalized logo coordinates.
        private const float BootCrownX0=.33f,BootCrownX1=.676f,BootSplitY=.46f;
        private static Vector2 BootLogoCenter(Rect logo)=>new Vector2(logo.center.x,logo.y+logo.height*.62f);

        private void DrawLogoPart(Rect logo,float x0,float y0,float x1,float y1,Color tint,Vector2 offset)
        {
            if(!logoTexture||x1<=x0||y1<=y0)return;
            var r=BootSub(logo,x0,y0,x1,y1);r.position+=offset;
            var old=GUI.color;GUI.color=tint;
            GUI.DrawTextureWithTexCoords(r,logoTexture,new Rect(x0,1-y1,x1-x0,y1-y0),true);GUI.color=old;
        }

        private void BootGlow(Vector2 center,Vector2 size,Color color)
        {
            if(color.a<=.003f)return;
            if(!bootIntroGlow)
            {
                const int n=64;bootIntroGlow=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Boot intro glow",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[n*n];for(var y=0;y<n;y++)for(var x=0;x<n;x++)
                {var d=new Vector2((x+.5f)/n*2-1,(y+.5f)/n*2-1).magnitude;pixels[y*n+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2.2f));}
                bootIntroGlow.SetPixels(pixels);bootIntroGlow.Apply(false,true);
            }
            if(profile!=null&&profile.reducedVfx)color.a*=.6f;
            var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(center-size*.5f,size),bootIntroGlow);GUI.color=old;
        }

        // ---------- OnGUI hooks ----------
        /// <summary>Called right after DrawBackdrop. Returns true when the intro owns the whole frame.</summary>
        private bool DrawBootIntro(float w,float h)
        {
            var e=Event.current;
            if(e.isMouse||e.isKey||e.type==EventType.ScrollWheel)
            {
                if(!bootIntroForced&&(e.type==EventType.KeyDown||e.type==EventType.MouseDown))SkipBootIntro();
                e.Use();
            }
            if(profile!=null&&profile.reduceMotion)return false;
            var t=bootIntroElapsed;if(t>=BootIntroSettleStart)return false;
            var baseMatrix=GUI.matrix;var oldColor=GUI.color;GUI.color=Color.white;
            // Screen shake after the crown lands.
            if(t>7.1f&&t<7.6f)
            {
                var k=1-Mathf.InverseLerp(7.1f,7.6f,t);var amp=14*k*k;
                GUI.matrix=baseMatrix*Matrix4x4.Translate(new Vector3(Mathf.Sin(t*83)*amp,Mathf.Cos(t*67)*amp*.7f,0));
            }
            Fill(new Rect(-40,-40,w+80,h+80),Color.black);
            DrawBootVault(w,h,t,1);
            DrawBootMotes(w,h,t);
            DrawBootBeam(w,h,t);
            var logo=BootHeroLogoRect(w,h);
            DrawBootThreads(w,h,t,logo);
            DrawBootLogo(w,h,t,logo);
            // Flash at 4.0 (dimmed under reduced flashing).
            if(t>3.95f&&t<4.5f)
            {
                var f=t<4.02f?Mathf.InverseLerp(3.95f,4.02f,t):1-Mathf.InverseLerp(4.02f,4.5f,t);
                var peak=profile!=null&&profile.reduceFlashing?.16f:.82f;
                Fill(new Rect(-40,-40,w+80,h+80),new Color(1,.93f,.78f,f*peak));
            }
            GUI.matrix=baseMatrix;GUI.color=oldColor;
            return true;
        }

        /// <summary>Vertical offset for the menu buttons while they slide in during the settle.</summary>
        private float BootIntroMenuSlide
        {
            get
            {
                if(!bootIntroActive||profile!=null&&profile.reduceMotion)return 0;
                var s=Mathf.Clamp01((bootIntroElapsed-BootIntroSettleStart)/(BootIntroDuration-BootIntroSettleStart));
                return 30*Mathf.Pow(1-s,3);
            }
        }

        /// <summary>Called at the end of the main-menu branch: fades the menu in and lands the logo.</summary>
        private void DrawBootIntroSettle(float w,float h)
        {
            var old=GUI.color;GUI.color=Color.white;
            var menuLogo=BootMenuLogoRect(w,h);
            var pulse=.82f+Mathf.Sin(shimmer*1.35f)*.12f;var menuAlpha=.9f+pulse*.08f;
            if(profile!=null&&profile.reduceMotion)
            {
                var r=BootEase(0,BootIntroReducedDuration,bootIntroElapsed);
                Fill(new Rect(0,0,w,h),new Color(0,0,0,1-r));
                if(logoTexture){GUI.color=new Color(1,1,1,r*menuAlpha);GUI.DrawTexture(menuLogo,logoTexture,ScaleMode.StretchToFill,true);}
                GUI.color=old;return;
            }
            var s=Mathf.Clamp01((bootIntroElapsed-BootIntroSettleStart)/(BootIntroDuration-BootIntroSettleStart));
            var ease=1-Mathf.Pow(1-s,3);
            // The intro's brighter vault state dissolves into the normal backdrop, uncovering the menu.
            var cover=1-BootEase(0,1,s);
            // The vault art is opaque and covers the canvas, so a plain alpha crossfade avoids a mid-dip.
            if(cover>0){if(!vaultBackground)Fill(new Rect(0,0,w,h),new Color(0,0,0,cover));DrawBootVault(w,h,BootIntroSettleStart,cover);}
            var hero=BootHeroLogoRect(w,h);
            var logo=new Rect(Vector2.Lerp(hero.position,menuLogo.position,ease),Vector2.Lerp(hero.size,menuLogo.size,ease));
            BootGlow(logo.center,logo.size*1.3f,new Color(1,.7f,.3f,.16f*cover));
            if(logoTexture){GUI.color=new Color(1,1,1,Mathf.Lerp(1,menuAlpha,ease));GUI.DrawTexture(logo,logoTexture,ScaleMode.StretchToFill,true);}
            GUI.color=old;
        }

        // ---------- layers ----------
        private void DrawBootVault(float w,float h,float t,float alpha)
        {
            var fade=BootEase(1.2f,3.4f,t)*alpha;if(fade<=0)return;
            var zoom=Mathf.Lerp(1.16f,1f,BootEase(1.2f,BootIntroSettleStart,t));
            var r=BootVaultRect(w,h,zoom);
            if(vaultBackground){var old=GUI.color;GUI.color=new Color(.9f,.88f,.9f,fade);GUI.DrawTexture(r,vaultBackground,ScaleMode.StretchToFill);GUI.color=old;}
            // Darken once the logo takes the stage so it reads against the door.
            var dim=.12f+BootEase(4.2f,5.4f,t)*.26f;
            Fill(new Rect(-40,-40,w+80,h+80),new Color(.004f,.006f,.012f,dim*alpha));
            // Seam glow (door's vertical gold seam, mapped through the zoom).
            var seamX=r.x+r.width*.5f;var y0=r.y+r.height*.19f;var y1=r.y+r.height*.75f;
            var glow=BootEase(1.6f,3.4f,t)*(1-BootEase(4.4f,6.0f,t)*.7f)*alpha;
            if(glow>0)
            {
                var flicker=.9f+Mathf.Sin(t*7.3f)*.06f+Mathf.Sin(t*13.1f)*.04f;
                BootGlow(new Vector2(seamX,(y0+y1)*.5f),new Vector2(46,(y1-y0)*1.15f),new Color(1,.72f,.28f,.55f*glow*flicker));
                BootGlow(new Vector2(seamX,(y0+y1)*.5f),new Vector2(14,(y1-y0)*1.05f),new Color(1,.9f,.62f,.75f*glow*flicker));
                Fill(new Rect(seamX-1,y0,2,y1-y0),new Color(1,.93f,.74f,.8f*glow));
            }
        }

        private void DrawBootMotes(float w,float h,float t)
        {
            var count=profile!=null&&profile.reducedVfx?14:34;
            var fadeIn=BootEase(.1f,1.0f,t);
            for(var i=0;i<count;i++)
            {
                var speed=.012f+BootHash(i,3)*.03f;
                var y=Mathf.Repeat(BootHash(i,1)-t*speed,1.1f)-.05f;
                var x=BootHash(i,2)+Mathf.Sin(t*.4f+i)*.012f;
                var twinkle=.35f+.65f*(Mathf.Sin(t*(1.3f+BootHash(i,4)*2)+i*1.7f)*.5f+.5f);
                var size=1.5f+BootHash(i,5)*2.5f;var p=new Vector2(x*w,y*h);
                var a=fadeIn*twinkle*.75f;
                if(i%4==0)BootGlow(p,Vector2.one*size*7,new Color(1,.66f,.2f,a*.35f));
                Fill(new Rect(p.x-size*.5f,p.y-size*.5f,size,size),new Color(1,.74f,.3f,a));
            }
        }

        private void DrawBootBeam(float w,float h,float t)
        {
            if(t<3.4f||t>5.6f)return;
            var r=BootVaultRect(w,h,Mathf.Lerp(1.16f,1f,BootEase(1.2f,BootIntroSettleStart,t)));
            var seamX=r.x+r.width*.5f;var y0=r.y+r.height*.19f;var y1=r.y+r.height*.75f;
            var open=BootEase(3.4f,4.0f,t);var fade=1-BootEase(4.1f,5.6f,t);
            var width=Mathf.Lerp(8,190,open);var restraint=profile!=null&&profile.reduceFlashing?.5f:1f;
            BootGlow(new Vector2(seamX,(y0+y1)*.5f),new Vector2(width*3.2f,(y1-y0)*1.6f),new Color(1,.72f,.32f,.45f*fade*restraint));
            BootGlow(new Vector2(seamX,(y0+y1)*.5f),new Vector2(width*1.4f,(y1-y0)*1.3f),new Color(1,.88f,.6f,.7f*fade*restraint));
            var core=Mathf.Lerp(2,34,open);
            Fill(new Rect(seamX-core*.5f,y0,core,y1-y0),new Color(1,.97f,.88f,.85f*fade*restraint));
            // God-ray spill onto the floor.
            BootGlow(new Vector2(seamX,y1),new Vector2(width*5,60),new Color(1,.8f,.45f,.35f*open*fade*restraint));
        }

        private static Vector2 BootBezier(Vector2 a,Vector2 c,Vector2 b,float u){var m=1-u;return m*m*a+2*m*u*c+u*u*b;}

        private void DrawBootThreads(float w,float h,float t,Rect logo)
        {
            if(t<4.2f||t>6.2f)return;
            var target=BootLogoCenter(logo);
            var starts=new[]{new Vector2(-30,h*.86f),new Vector2(w+30,h*.12f),new Vector2(w*.78f,h+30)};
            var controls=new[]{new Vector2(w*.22f,h*.30f),new Vector2(w*.70f,h*.78f),new Vector2(w*.30f,h*.70f)};
            var colors=new[]{BootVanguard,BootHexer,BootReaper};
            for(var i=0;i<3;i++)
            {
                var start=4.2f+i*.09f;var raw=Mathf.Clamp01((t-start)/(5.0f-start));
                var head=raw*raw*(1.6f-.6f*raw);// accelerating, lands exactly at 1
                var tail=Mathf.Max(0,head-.42f)+BootEase(5.0f,5.35f,t);
                if(raw<=0||tail>=head)continue;
                const int segments=22;var col=colors[i];var prev=BootBezier(starts[i],controls[i],target,tail);
                for(var s=1;s<=segments;s++)
                {
                    var u=Mathf.Lerp(tail,head,s/(float)segments);var p=BootBezier(starts[i],controls[i],target,u);
                    var k=s/(float)segments;
                    DrawLine(prev,p,new Color(col.r,col.g,col.b,.22f*k),9*k+2);
                    DrawLine(prev,p,new Color(Mathf.Lerp(col.r,1,.45f*k),Mathf.Lerp(col.g,1,.45f*k),Mathf.Lerp(col.b,1,.45f*k),.9f*k),2.4f*k+.6f);
                    prev=p;
                }
                if(t<5.0f)
                {
                    BootGlow(prev,Vector2.one*70,new Color(col.r,col.g,col.b,.55f));
                    BootGlow(prev,Vector2.one*18,new Color(1,1,1,.9f));
                }
            }
            // Collision burst at 5.0.
            if(t>=5.0f)
            {
                var a=Mathf.InverseLerp(5.0f,5.9f,t);var fade=1-a;
                var restraint=profile!=null&&profile.reduceFlashing?.45f:1f;
                BootGlow(target,Vector2.one*Mathf.Lerp(120,520,a),new Color(1,.86f,.6f,.7f*fade*fade*restraint));
                var radius=Mathf.Lerp(10,300,1-Mathf.Pow(1-a,3));var prev=target+new Vector2(radius,0);
                for(var s=1;s<=48;s++)
                {
                    var ang=s*Mathf.PI*2/48;var p=target+new Vector2(Mathf.Cos(ang)*radius,Mathf.Sin(ang)*radius*.55f);
                    var col=colors[s%3];DrawLine(prev,p,new Color(Mathf.Lerp(col.r,1,.5f),Mathf.Lerp(col.g,1,.5f),Mathf.Lerp(col.b,1,.5f),fade*.85f),3*fade+.5f);prev=p;
                }
                var sparks=profile!=null&&profile.reducedVfx?10:24;
                for(var i=0;i<sparks;i++)
                {
                    var ang=BootHash(i,11)*Mathf.PI*2;var dir=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang)*.7f);
                    var dist=(80+BootHash(i,12)*260)*(1-Mathf.Pow(1-a,2));var p=target+dir*dist+Vector2.up*(a*a*60);
                    var col=colors[i%3];DrawLine(p-dir*(14*fade+2),p,new Color(Mathf.Lerp(col.r,1,.6f),Mathf.Lerp(col.g,1,.6f),Mathf.Lerp(col.b,1,.6f),fade),2);
                }
            }
        }

        private void DrawBootLogo(float w,float h,float t,Rect logo)
        {
            if(t<5.0f||!logoTexture)return;
            var reducedVfx=profile!=null&&profile.reducedVfx;
            // Letters ignite outward from the center.
            var ignite=BootEase(5.0f,6.3f,t);var half=Mathf.Lerp(.015f,.5f,ignite);
            var lx0=.5f-half;var lx1=.5f+half;
            DrawLogoPart(logo,lx0,BootSplitY,lx1,1,Color.white,Vector2.zero);
            var bandTop=logo.y+logo.height*BootSplitY;var bandBottom=logo.y+logo.height*.8f;var bandMid=(bandTop+bandBottom)*.5f;
            if(ignite<1)
            {
                var heat=1-BootEase(6.0f,6.4f,t);
                foreach(var side in new[]{-1f,1f})
                {
                    var ex=logo.x+logo.width*(side<0?lx0:lx1);
                    // Molten band just behind the edge, then a white-hot front.
                    var edgeW=logo.width*.035f;
                    var zone=side<0?new Rect(ex,bandTop,edgeW,bandBottom-bandTop):new Rect(ex-edgeW,bandTop,edgeW,bandBottom-bandTop);
                    BootGlow(zone.center,new Vector2(edgeW*2.4f,(bandBottom-bandTop)*1.5f),new Color(1,.45f,.08f,.7f*heat));
                    BootGlow(new Vector2(ex,bandMid),new Vector2(22,(bandBottom-bandTop)*1.25f),new Color(1,.95f,.8f,.95f*heat));
                    Fill(new Rect(ex-1,bandTop+6,2,bandBottom-bandTop-12),new Color(1,.98f,.9f,.9f*heat));
                }
            }
            // Ignition sparks: stateless, born at the edge position of their birth time.
            var count=reducedVfx?14:40;
            for(var i=0;i<count;i++)
            {
                var born=5.0f+i*(1.3f/count);var age=t-born;const float life=.7f;
                if(age<0||age>life)continue;
                var bornHalf=Mathf.Lerp(.015f,.5f,BootEase(5.0f,6.3f,born));var side=i%2==0?-1:1;
                var origin=new Vector2(logo.x+logo.width*(.5f+side*bornHalf),Mathf.Lerp(bandTop,bandBottom,BootHash(i,21)));
                var vel=new Vector2(side*(40+BootHash(i,22)*120),-(60+BootHash(i,23)*140));
                var p=origin+vel*age+new Vector2(0,260*age*age);var k=1-age/life;
                DrawLine(p-vel.normalized*(8*k+2),p,new Color(1,.62f+.3f*k,.25f+.4f*k,k),1.6f);
            }
            // Crown drops from above, accelerating, and lands at 7.1.
            if(t>=6.5f)
            {
                var d=Mathf.Clamp01((t-6.5f)/.6f);var drop=-(logo.y+logo.height*BootSplitY+40)*(1-d*d);
                var landed=t>=7.1f;
                if(t<7.65f)
                {
                    DrawLogoPart(logo,BootCrownX0,0,BootCrownX1,BootSplitY,Color.white,new Vector2(0,drop));
                    // Ribbons unfurl outward from the crown after landing.
                    var unfurl=BootEase(7.1f,7.6f,t);
                    if(unfurl>0)
                    {
                        var tint=new Color(1,1,1,Mathf.Clamp01(unfurl*1.4f));
                        DrawLogoPart(logo,Mathf.Lerp(BootCrownX0,0,unfurl),0,BootCrownX0,BootSplitY,tint,Vector2.zero);
                        DrawLogoPart(logo,BootCrownX1,0,Mathf.Lerp(BootCrownX1,1,unfurl),BootSplitY,tint,Vector2.zero);
                    }
                }
                else
                {
                    // Fully assembled: one draw of the whole logo (no seams between parts).
                    DrawLogoPart(logo,0,0,1,BootSplitY,Color.white,Vector2.zero);
                }
                if(!landed)
                {
                    var crownBase=new Vector2(logo.center.x,logo.y+logo.height*BootSplitY+drop);
                    BootGlow(crownBase,new Vector2(logo.width*.28f,30),new Color(1,.8f,.4f,.3f*d));
                }
                else
                {
                    var a=Mathf.InverseLerp(7.1f,7.9f,t);var fade=(1-a)*(1-a);
                    var restraint=profile!=null&&profile.reduceFlashing?.4f:1f;
                    var baseY=logo.y+logo.height*BootSplitY;
                    // Anamorphic streak through the crown base.
                    BootGlow(new Vector2(logo.center.x,baseY),new Vector2(w*1.3f,46),new Color(1,.78f,.42f,.55f*fade*restraint));
                    BootGlow(new Vector2(logo.center.x,baseY),new Vector2(w*.9f,10),new Color(1,.97f,.9f,.9f*fade*restraint));
                    Fill(new Rect(logo.center.x-w*.35f*(1-a*.5f),baseY-1,w*.7f*(1-a*.5f),2),new Color(1,.98f,.92f,.8f*fade*restraint));
                    var sparks=reducedVfx?10:26;
                    for(var i=0;i<sparks;i++)
                    {
                        var dir=new Vector2(BootHash(i,31)*2-1,-(.2f+BootHash(i,32)*.9f)).normalized;
                        var speed=180+BootHash(i,33)*420;var age=t-7.1f;
                        var p=new Vector2(logo.center.x+(BootHash(i,34)-.5f)*logo.width*.3f,baseY)+dir*speed*age+new Vector2(0,420*age*age);
                        DrawLine(p-dir*(12*fade+2),p,new Color(1,.7f+.25f*fade,.3f+.4f*fade,fade),2);
                    }
                }
            }
            // Diagonal light sweep across the logo, clipped to the logo bounds.
            if(t>=7.2f&&t<=8.0f)
            {
                var a=BootEase(7.2f,8.0f,t);var fade=Mathf.Sin(a*Mathf.PI);
                GUI.BeginGroup(logo);
                var x=Mathf.Lerp(-logo.width*.25f,logo.width*1.25f,a);var slant=logo.height*.45f;
                DrawLine(new Vector2(x-slant,logo.height+10),new Vector2(x+slant,-10),new Color(1,.92f,.72f,.10f*fade),logo.width*.09f);
                DrawLine(new Vector2(x-slant,logo.height+10),new Vector2(x+slant,-10),new Color(1,.97f,.88f,.22f*fade),logo.width*.018f);
                GUI.EndGroup();
            }
        }
    }
}
