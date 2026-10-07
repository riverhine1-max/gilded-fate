using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Core;
using GildedFate.Combat;
using GildedFate.Map;
using UnityEngine;

namespace GildedFate.UI
{
    // Fateshard Overhaul, batch 2: shard VFX.
    // Every shard moment is the loudest thing on screen: awakening and shatter cinematics,
    // charge comets, a ready beacon, an active hero aura, battlefield tint and card glints.
    // Respects Reduce Motion, Reduce Flashing, Reduced VFX and Screen Shake.
    public sealed partial class GildedMainMenu
    {
        private sealed class ShardCinematic{public FateShardDef shard;public Vector2 origin;public float start,duration;public bool shatter,fractured;public int seed;public Color color;}
        private sealed class ShardComet{public Vector2 from,to;public float start,duration,size;public Color color;public int seed;}
        private readonly List<ShardCinematic> shardCinematics=new();
        private readonly List<ShardComet> shardComets=new();
        private float shardReadyBannerAt=-10;
        private static Texture2D shardVfxSoft,shardVfxVignette;

        // ---------------- identity ----------------
        private static Color ShardArchetypeColor(string archetype)=>archetype switch
        {
            "Strength"=>new Color(1f,.32f,.26f),"Block"=>new Color(.55f,.78f,1f),"Attack"=>new Color(1f,.46f,.3f),
            "Heavy"=>new Color(1f,.55f,.18f),"Cost"=>new Color(.4f,.95f,1f),"Energy"=>new Color(1f,.82f,.32f),
            "Draw"=>new Color(.72f,.84f,1f),"Burn"=>new Color(1f,.48f,.12f),"Debuff"=>new Color(.78f,.45f,1f),
            "Retaliate"=>new Color(.5f,.95f,.55f),"Exhaust"=>new Color(.7f,.62f,.9f),"Echo"=>new Color(.35f,1f,.85f),
            "Curse"=>new Color(.62f,.9f,.3f),"Modified"=>new Color(1f,.86f,.45f),_=>new Color(1f,.84f,.5f)
        };
        private static Color ShardColor(FateShardDef def)=>ShardArchetypeColor(def?.archetype);
        private static FateShardDef ShardDefinition(string id)=>string.IsNullOrEmpty(id)?null:WorldContent.FateShards.FirstOrDefault(s=>s.id==id);
        private Color ActiveShardColor
        {
            get
            {
                var def=ShardDefinition(combat?.activeShardId);var color=ShardColor(def);
                return combat!=null&&combat.activeShardFractured?Color.Lerp(color,new Color(1f,.58f,.24f),.45f):color;
            }
        }
        private Color ChargeColor
        {
            get
            {
                if(combat==null||combat.shardHold)return Gold;
                var id=!string.IsNullOrEmpty(combat.chargeShardA)?combat.chargeShardA:combat.chargeShardB;
                return string.IsNullOrEmpty(id)?Gold:ShardColor(ShardDefinition(id));
            }
        }

        // ---------------- textures and primitives ----------------
        private static Texture2D ShardVfxSoft
        {
            get
            {
                if(shardVfxSoft)return shardVfxSoft;
                const int n=64;var pixels=new Color[n*n];
                for(var y=0;y<n;y++)for(var x=0;x<n;x++)
                {var u=(x+.5f)/n*2-1;var v=(y+.5f)/n*2-1;var a=Mathf.Clamp01(1f-Mathf.Sqrt(u*u+v*v));pixels[y*n+x]=new Color(1,1,1,a*a*(3-2*a));}
                shardVfxSoft=ShardVfxTexture("Shard VFX soft",n,pixels);return shardVfxSoft;
            }
        }
        private static Texture2D ShardVfxVignette
        {
            get
            {
                if(shardVfxVignette)return shardVfxVignette;
                const int n=64;var pixels=new Color[n*n];
                for(var y=0;y<n;y++)for(var x=0;x<n;x++)
                {var u=(x+.5f)/n*2-1;var v=(y+.5f)/n*2-1;var d=Mathf.Clamp01((Mathf.Sqrt(u*u*.8f+v*v)-.45f)/.75f);pixels[y*n+x]=new Color(1,1,1,d*d);}
                shardVfxVignette=ShardVfxTexture("Shard VFX vignette",n,pixels);return shardVfxVignette;
            }
        }
        private static Texture2D ShardVfxTexture(string name,int n,Color[] pixels)
        {
            var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name=name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            t.SetPixels(pixels);t.Apply(false,true);return t;
        }
        private void ShardSoft(Vector2 center,float size,Color color){ShardSoft(new Rect(center.x-size*.5f,center.y-size*.5f,size,size),color);}
        private void ShardSoft(Rect r,Color color)
        {
            if(color.a<=.003f)return;var old=GUI.color;GUI.color=color;GUI.DrawTexture(r,ShardVfxSoft,ScaleMode.StretchToFill,true);GUI.color=old;
        }
        private void ShardVignette(float w,float h,Color color)
        {
            if(color.a<=.003f)return;var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(0,0,w,h),ShardVfxVignette,ScaleMode.StretchToFill,true);GUI.color=old;
        }
        private void ShardCircle(Vector2 c,float rx,float ry,Color color,float width,int segments=48,float rotation=0)
        {
            if(color.a<=.003f)return;
            var prev=c+new Vector2(Mathf.Cos(rotation)*rx,Mathf.Sin(rotation)*ry);
            for(var s=1;s<=segments;s++){var a=rotation+s*Mathf.PI*2/segments;var next=c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);DrawLine(prev,next,color,width);prev=next;}
        }
        private void ShardBolt(Vector2 from,Vector2 to,int seed,Color color,float width,int segments=12,float jag=26)
        {
            var dir=to-from;var normal=new Vector2(-dir.y,dir.x).normalized;var prev=from;
            for(var s=1;s<=segments;s++)
            {
                var k=s/(float)segments;var offset=s==segments?0:(CardVfxHash(seed,s)-.5f)*jag*Mathf.Sin(k*Mathf.PI);
                var next=from+dir*k+normal*offset;
                DrawLine(prev,next,new Color(color.r,color.g,color.b,color.a*.35f),width*3.2f);DrawLine(prev,next,color,width);
                if(!profile.reduceFlashing)DrawLine(prev,next,new Color(1,1,1,color.a*.85f),Mathf.Max(1,width*.4f));
                if(s%4==2&&CardVfxHash(seed,s+50)>.35f)
                {
                    var branch=next+(normal*(CardVfxHash(seed,s+70)>.5f?1:-1)+dir.normalized*.6f)*(20+CardVfxHash(seed,s+90)*40);
                    DrawLine(next,branch,new Color(color.r,color.g,color.b,color.a*.6f),Mathf.Max(1,width*.5f));
                }
                prev=next;
            }
        }
        private void ShardCrystal(Vector2 at,float angle,float length,float width,Color color)
        {
            var axis=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*length*.5f;
            DrawLine(at-axis,at+axis,color,width);
            if(!profile.reduceFlashing)DrawLine(at-axis*.45f,at+axis*.45f,new Color(1,1,1,color.a*.8f),Mathf.Max(1,width*.35f));
        }
        private static float ShardEase(float k)=>1-(1-k)*(1-k)*(1-k);
        private static float ShardWindow(float k,float a,float b)=>Mathf.Clamp01((k-a)/Mathf.Max(.0001f,b-a));

        // ---------------- triggers ----------------
        // Called when a shard activates (or shatters). Plays the full-screen cinematic.
        private void StartShardCinematic(FateShardDef shard,int slot,bool shatter,bool fractured)
        {
            if(shard==null)return;
            var duration=(shatter?1.75f:1.35f)*(profile.reduceMotion?.6f:1f);
            var color=ShardColor(shard);if(fractured||shatter)color=Color.Lerp(color,new Color(1f,.6f,.25f),.35f);
            shardCinematics.Add(new ShardCinematic{shard=shard,origin=ShrineSocket(Mathf.Clamp(slot,0,1)).center,start=Time.unscaledTime,duration=duration,shatter=shatter,fractured=fractured,seed=Random.Range(1,100000),color=color});
            impactShake=Mathf.Max(impactShake,shatter?.6f:.45f);
            Sfx(SoundCue.RewardShard,-.4f,1f,true);Sfx(SoundCue.Power,0,.9f,true,.18f);
            if(shatter){Sfx(SoundCue.BlockBreak,0,1f,true,duration*.48f);Sfx(SoundCue.HitHeavy,0,1f,true,duration*.5f);Sfx(SoundCue.BossPhase,0,.8f,true,duration*.5f);}
            else if(fractured)Sfx(SoundCue.HitHeavy,0,.8f,true,.32f);
        }
        private void QueueShardComet(Vector2 from,Vector2 to,float start,float duration,Color color,float size)
        {
            if(profile.reducedVfx&&shardComets.Count>6)return;
            shardComets.Add(new ShardComet{from=from,to=to,start=start,duration=duration,color=color,size=size,seed=Random.Range(1,100000)});
        }
        // Charge comets fly from the hand to every socket being charged.
        private void OnShardChargeGained(int gained,bool nowFull)
        {
            var color=ChargeColor;
            foreach(var s in run.shards.Where(s=>s.CanActivate&&combat.ShardAttunedTo(s.id)))
            {
                var to=ShrineSocket(s.slot).center;
                for(var i=0;i<Mathf.Clamp(gained,1,3);i++)
                    QueueShardComet(new Vector2(CombatWidth*.5f+Random.Range(-140f,140f),CombatHeight-170+Random.Range(-20f,20f)),to,Time.unscaledTime+i*.07f,.5f,color,gained>=2?16:11);
            }
            if(nowFull){shardReadyBannerAt=Time.unscaledTime+.45f;impactShake=Mathf.Max(impactShake,.18f);}
        }

        // ---------------- top layer: cinematics and comets ----------------
        private void DrawShardCinematics()
        {
            var now=Time.unscaledTime;
            shardCinematics.RemoveAll(c=>now-c.start>c.duration);
            shardComets.RemoveAll(c=>now-c.start>c.duration+.25f);
            if(!CardVfxRepaint||screen!=ScreenMode.Combat||combat==null)return;
            var w=CombatWidth;var h=CombatHeight;
            DrawShardComets(now);
            DrawShardReadyBanner(now);
            foreach(var c in shardCinematics)DrawShardCinematic(c,now,w,h);
        }
        private void DrawShardComets(float now)
        {
            foreach(var c in shardComets)
            {
                var k=(now-c.start)/c.duration;
                if(k<0)continue;
                if(k>1)
                {
                    var land=(now-c.start-c.duration)/.25f;if(land>1)continue;
                    ShardSoft(c.to,c.size*(3+land*4),new Color(c.color.r,c.color.g,c.color.b,.5f*(1-land)));
                    ShardCircle(c.to,c.size*(1+land*4),c.size*(1+land*4),new Color(c.color.r,c.color.g,c.color.b,.8f*(1-land)),2);continue;
                }
                var lift=Mathf.Lerp(120,40,CardVfxHash(c.seed,1));var side=(CardVfxHash(c.seed,2)-.5f)*160;
                var control=(c.from+c.to)*.5f+new Vector2(side,-lift);
                Vector2 At(float t){var u=1-t;return u*u*c.from+2*u*t*control+t*t*c.to;}
                var e=ShardEase(k);var trail=profile.reducedVfx?4:10;
                for(var i=trail;i>=0;i--)
                {
                    var t=Mathf.Clamp01(e-i*.035f);var p=At(t);var fade=1-i/(float)(trail+1);
                    ShardSoft(p,c.size*(1.6f+fade*1.8f),new Color(c.color.r,c.color.g,c.color.b,.28f*fade));
                    if(i==0){ShardSoft(p,c.size*1.2f,new Color(1,1,1,.85f));Fill(new Rect(p.x-1.5f,p.y-1.5f,3,3),Color.white);}
                }
            }
        }
        private void DrawShardReadyBanner(float now)
        {
            var t=now-shardReadyBannerAt;if(t<0||t>1.4f)return;
            var a=Mathf.Clamp01(t*6)*Mathf.Clamp01((1.4f-t)*3);
            var first=ShrineSocket(0);var at=new Vector2(first.center.x+120,first.y-6);
            var scale=1+Mathf.Max(0,.35f-t)*2.2f;var color=ChargeColor;
            ShardSoft(at,220*scale,new Color(color.r,color.g,color.b,.28f*a));
            var style=new GUIStyle(titleStyle){fontSize=Mathf.RoundToInt(26*scale),alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.93f,.72f,a)}};
            ShadowLabel(new Rect(at.x-200,at.y-24,400,48),"SHARD READY",style);
        }
        private void DrawShardCinematic(ShardCinematic c,float now,float w,float h)
        {
            var k=Mathf.Clamp01((now-c.start)/c.duration);var color=c.color;
            var motion=!profile.reduceMotion;var flashOk=!profile.reduceFlashing;var lots=!profile.reducedVfx;
            var center=new Vector2(w*.5f,h*.38f);var hero=HeroPortraitRect.center;
            var env=ShardWindow(k,0,.1f)*(1-ShardWindow(k,.78f,1f));
            var burstK=c.shatter?.5f:.3f;var sinceBurst=k-burstK;

            // 1. The world drops away: dark vignette and shard-tinted wash.
            ShardVignette(w,h,new Color(0,0,0,.72f*env));
            Fill(new Rect(0,0,w,h),new Color(.01f,.008f,.02f,.32f*env));
            ShardVignette(w,h,new Color(color.r,color.g,color.b,.22f*env));

            // 2. Light rays wheel out from the center.
            if(lots)
            {
                var rays=c.shatter?20:14;var rayLen=Mathf.Lerp(80,w*.75f,ShardEase(ShardWindow(k,.05f,.45f)));
                for(var i=0;i<rays;i++)
                {
                    var a=i*Mathf.PI*2/rays+(motion?(now-c.start)*.35f:0)+CardVfxHash(c.seed,i)*.2f;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    var len=rayLen*(.6f+CardVfxHash(c.seed,i+30)*.5f);var alpha=env*(i%2==0?.16f:.09f);
                    DrawLine(center,center+dir*len,new Color(color.r,color.g,color.b,alpha*.5f),22);
                    DrawLine(center,center+dir*len*.9f,new Color(color.r,color.g,color.b,alpha),8);
                    DrawLine(center,center+dir*len*.7f,new Color(1,1,1,alpha*.8f),2);
                }
            }

            // 3. Fate lightning: socket to center, center to hero.
            if(k<.55f)
            {
                var flick=Mathf.FloorToInt((now-c.start)*(motion?26:6));var boltA=(1-ShardWindow(k,.35f,.55f))*ShardWindow(k,0,.05f);
                ShardBolt(c.origin,center,c.seed*13+flick,new Color(color.r,color.g,color.b,.95f*boltA),3.2f,14,34);
                if(k>.12f)ShardBolt(center,hero,c.seed*29+flick,new Color(color.r,color.g,color.b,.8f*boltA),2.4f,10,24);
                if(lots&&k>.18f)ShardBolt(center,new Vector2(w*.7f,h*.34f),c.seed*41+flick,new Color(color.r,color.g,color.b,.55f*boltA),1.8f,10,30);
            }

            // 4. Shockwaves from the socket, then from the center at the burst.
            var wave1=ShardWindow(k,0,.4f);
            if(wave1<1){EnsureCardVfxTextures();var size=Mathf.Lerp(40,420,ShardEase(wave1));DrawCardUiShape(new Rect(c.origin.x-size*.5f,c.origin.y-size*.5f,size,size),cardVfxRing,new Color(color.r,color.g,color.b,.8f*(1-wave1)));}
            if(sinceBurst>=0)
            {
                EnsureCardVfxTextures();
                for(var ring=0;ring<(c.shatter?3:2);ring++)
                {
                    var rk=Mathf.Clamp01((sinceBurst-ring*.06f)/(c.shatter?.45f:.4f));if(rk<=0||rk>=1)continue;
                    var size=Mathf.Lerp(80,w*(c.shatter?1.5f:1.1f),ShardEase(rk));
                    DrawCardUiShape(new Rect(center.x-size*.5f,center.y-size*.5f,size,size),cardVfxRing,new Color(ring==1?1:color.r,ring==1?1:color.g,ring==1?1:color.b,.75f*(1-rk)));
                }
                if(c.shatter)
                {
                    // Ground shock: a line of light tearing across the battlefield.
                    var gk=Mathf.Clamp01(sinceBurst/.5f);var gy=HeroPortraitRect.yMax+8;var half=w*.5f*ShardEase(gk);
                    DrawLine(new Vector2(center.x-half,gy),new Vector2(center.x+half,gy),new Color(color.r,color.g,color.b,.55f*(1-gk)),10);
                    DrawLine(new Vector2(center.x-half,gy),new Vector2(center.x+half,gy),new Color(1,1,1,.8f*(1-gk)),2);
                }
            }

            // 5. The shard itself flies to center, grows, spins and hangs in the air.
            var fly=ShardEase(ShardWindow(k,.04f,.3f));var shardFade=1-ShardWindow(k,c.shatter?.5f:.74f,c.shatter?.52f:1f);
            if(shardFade>0)
            {
                var pos=Vector2.Lerp(c.origin,center,fly);var baseSize=Mathf.Lerp(90,c.shatter?220:200,fly);
                var hover=motion?Mathf.Sin((now-c.start)*5)*6:0;pos.y+=hover;
                var shake=Vector2.zero;
                if(c.shatter&&k>.3f&&k<.5f&&motion){var intensity=ShardWindow(k,.3f,.5f)*9;shake=new Vector2((CardVfxHash(c.seed,Mathf.FloorToInt(now*60))-.5f)*intensity*2,(CardVfxHash(c.seed+7,Mathf.FloorToInt(now*60))-.5f)*intensity*2);}
                pos+=shake;var grow=k>.74f&&!c.shatter?1+ShardWindow(k,.74f,1f)*.6f:1;var size=baseSize*grow;
                ShardSoft(pos,size*3.2f,new Color(color.r,color.g,color.b,.42f*shardFade));
                ShardSoft(pos,size*1.6f,new Color(1,1,1,(c.shatter?ShardWindow(k,.3f,.5f)*.55f:.16f)*shardFade*(flashOk?1:.4f)));
                var matrix=GUI.matrix;var spin=motion?(1-fly)*540+Mathf.Sin((now-c.start)*2.2f)*6:0;
                GUIUtility.RotateAroundPivot(spin,pos);
                var old=GUI.color;GUI.color=new Color(1,1,1,shardFade);
                DrawFateShardArt(new Rect(pos.x-size*.5f,pos.y-size*.5f,size,size),c.shard);
                GUI.color=old;GUI.matrix=matrix;
                // Orbiting motes.
                if(lots)for(var m=0;m<8;m++)
                {
                    var a=m*Mathf.PI/4+(now-c.start)*(motion?2.6f:0);var rr=size*(.62f+.08f*Mathf.Sin(m+now*3));
                    var p=pos+new Vector2(Mathf.Cos(a)*rr,Mathf.Sin(a)*rr*.55f);
                    ShardSoft(p,14,new Color(color.r,color.g,color.b,.8f*shardFade));Fill(new Rect(p.x-1.5f,p.y-1.5f,3,3),new Color(1,1,1,shardFade));
                }
                // Shatter: cracks race across the shard before it breaks.
                if(c.shatter&&k>.3f)
                {
                    var crackK=ShardWindow(k,.3f,.5f);
                    for(var cr=0;cr<9;cr++)
                    {
                        var a=cr*Mathf.PI*2/9+CardVfxHash(c.seed,cr+200);var prev=pos;var len=size*.55f*crackK;
                        for(var s=1;s<=4;s++){var t=s/4f;var next=pos+new Vector2(Mathf.Cos(a+(CardVfxHash(c.seed,cr*10+s)-.5f)*.8f),Mathf.Sin(a+(CardVfxHash(c.seed,cr*10+s)-.5f)*.8f))*len*t;DrawLine(prev,next,new Color(1f,.95f,.8f,.95f*shardFade),2.4f);DrawLine(prev,next,new Color(color.r,color.g,color.b,.5f*shardFade),6);prev=next;}
                    }
                }
            }

            // 6. Burst: flash and a storm of crystals.
            if(sinceBurst>=0)
            {
                var flash=Mathf.Clamp01(1-sinceBurst/(c.shatter?.22f:.16f));
                if(flash>0)Fill(new Rect(0,0,w,h),c.shatter?new Color(1,1,1,(flashOk?.55f:.12f)*flash):new Color(color.r,color.g,color.b,(flashOk?.22f:.07f)*flash));
                if(sinceBurst<.02f&&c.shatter)impactShake=Mathf.Max(impactShake,1.2f);
                var count=c.shatter?(lots?56:16):(lots?30:10);var age=sinceBurst*c.duration;
                for(var i=0;i<count;i++)
                {
                    var a=i/(float)count*Mathf.PI*2+(CardVfxHash(c.seed,i+300)-.5f)*.6f;
                    var speed=(c.shatter?320:200)+CardVfxHash(c.seed,i+340)*(c.shatter?520:260);
                    var p=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*speed*age*(motion?1:.4f)+new Vector2(0,(c.shatter?420:260)*age*age);
                    var fade=Mathf.Clamp01(1.25f-age/(c.duration*(1-burstK)))*(i%5==0?1:.85f);if(fade<=0)continue;
                    var len=(c.shatter?10:7)+CardVfxHash(c.seed,i+380)*(c.shatter?18:10);
                    var tint=i%3==0?new Color(1,1,1):i%3==1?color:Color.Lerp(color,new Color(1f,.7f,.35f),.5f);tint.a=fade;
                    ShardCrystal(p,a+age*(CardVfxHash(c.seed,i+420)*16-8),len,2.5f+CardVfxHash(c.seed,i+460)*2.5f,tint);
                }
                // Embers drift upward after the burst.
                if(lots)for(var e=0;e<(c.shatter?24:12);e++)
                {
                    var ek=Mathf.Repeat(sinceBurst*c.duration*.8f+CardVfxHash(c.seed,e+500),1f);
                    var p=new Vector2(center.x+(CardVfxHash(c.seed,e+540)-.5f)*w*.6f,center.y+140-ek*260);
                    ShardSoft(p,10,new Color(color.r,color.g,color.b,.6f*(1-ek)*env));
                }
            }

            // 7. The name slams onto the screen.
            var slamK=ShardWindow(k,burstK-.02f,burstK+.12f);var textFade=slamK>0?Mathf.Clamp01(slamK*4)*(1-ShardWindow(k,.82f,1f)):0;
            if(textFade>0)
            {
                var overshoot=slamK<1?Mathf.Lerp(2.6f,1f,ShardEase(slamK))+Mathf.Sin(slamK*Mathf.PI)*.12f:1;
                var title=c.shatter?"SHATTERED":c.shard.name;
                var size=Mathf.RoundToInt((c.shatter?88:68)*overshoot);var y=center.y+(c.shatter?40:150);
                var rect=new Rect(center.x-w*.5f,y-size*.7f,w,size*1.4f);
                ShardSoft(new Rect(center.x-380*overshoot,y-70,760*overshoot,140),new Color(color.r,color.g,color.b,.4f*textFade));
                ShardSoft(new Rect(center.x-260,y-30,520,60),new Color(0,0,0,.5f*textFade));
                var style=new GUIStyle(titleStyle){fontSize=size,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.92f,.68f,textFade)}};
                if(c.shatter&&motion&&flashOk)
                {
                    var jitter=(1-slamK)*10+2;
                    var red=new GUIStyle(style){normal={textColor=new Color(1f,.25f,.2f,.55f*textFade)}};var blue=new GUIStyle(style){normal={textColor=new Color(.3f,.7f,1f,.55f*textFade)}};
                    GUI.Label(new Rect(rect.x-jitter,rect.y,rect.width,rect.height),title,red);GUI.Label(new Rect(rect.x+jitter,rect.y,rect.width,rect.height),title,blue);
                }
                ShadowLabel(rect,title,style);
                var sub=c.shatter?c.shard.name+" · FRACTURED POWER UNLEASHED":c.fractured?"FRACTURED · AWAKENED":"FATE SHARD AWAKENED";
                var subStyle=new GUIStyle(titleStyle){fontSize=20,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(color.r,color.g,color.b,textFade)}};
                ShadowLabel(new Rect(center.x-w*.5f,y+size*.62f,w,30),sub,subStyle);
                var lineHalf=Mathf.Lerp(0,300,ShardEase(slamK));
                DrawLine(new Vector2(center.x-lineHalf,y+size*.55f),new Vector2(center.x+lineHalf,y+size*.55f),new Color(1f,.84f,.47f,.85f*textFade),2);
            }
        }

        // ---------------- battlefield layer (behind the actors) ----------------
        private void DrawShardBattlefieldFx(float w,float h)
        {
            if(!CardVfxRepaint||combat==null||string.IsNullOrEmpty(combat.activeShardId))return;
            var def=ShardDefinition(combat.activeShardId);if(def==null)return;
            var color=ActiveShardColor;var now=Time.unscaledTime;var motion=!profile.reduceMotion;var lots=!profile.reducedVfx;
            var fractured=combat.activeShardFractured;
            var pulse=motion?Mathf.Sin(now*2.4f)*.5f+.5f:.5f;
            // Battlefield edges take on the shard's color for the rest of the fight.
            ShardVignette(w,h,new Color(color.r,color.g,color.b,(fractured?.16f:.11f)+pulse*.05f));
            // Hero aura.
            var hero=HeroPortraitRect;var c=hero.center;
            ShardSoft(new Rect(c.x-hero.width*.95f,c.y-hero.height*.8f,hero.width*1.9f,hero.height*1.6f),new Color(color.r,color.g,color.b,.16f+pulse*.08f));
            var feet=new Vector2(c.x,hero.yMax-6);var rot=motion?now*.6f:0;
            ShardCircle(feet,hero.width*.62f,20,new Color(color.r,color.g,color.b,.5f+pulse*.2f),2,56,rot);
            ShardCircle(feet,hero.width*.48f,14,new Color(1f,.9f,.65f,.35f),1.2f,48,-rot*1.4f);
            for(var t=0;t<12;t++){var a=rot*1.4f+t*Mathf.PI/6;var p=feet+new Vector2(Mathf.Cos(a)*hero.width*.62f,Mathf.Sin(a)*20);DrawLine(p,p+Vector2.down*(6+pulse*4),new Color(color.r,color.g,color.b,.7f),2);}
            // Orbiting shard fragments around the hero.
            var orbiters=fractured?7:5;
            for(var o=0;o<orbiters;o++)
            {
                var a=(motion?now*(fractured?1.6f:1.1f):0)+o*Mathf.PI*2/orbiters;
                var p=c+new Vector2(Mathf.Cos(a)*hero.width*.66f,Mathf.Sin(a)*hero.height*.22f-hero.height*.08f);
                ShardSoft(p,26,new Color(color.r,color.g,color.b,.55f));ShardCrystal(p,a*2,12,3,new Color(1f,.95f,.85f,.9f));
            }
            // Rising embers.
            if(lots)for(var e=0;e<(fractured?16:9);e++)
            {
                var ek=Mathf.Repeat(now*(.25f+e*.013f)+e*.137f,1f);
                var p=new Vector2(c.x+(Mathf.Sin(e*12.9f)*.5f)*hero.width*1.2f,hero.yMax-ek*hero.height*1.1f);
                ShardSoft(p,8+ek*6,new Color(color.r,color.g,color.b,.65f*(1-ek)));
            }
        }

        // ---------------- reliquary layer: ready beacon and active socket ----------------
        private void DrawShardSocketFx(Rect r,FateShardState owned,bool active,bool ready)
        {
            if(!CardVfxRepaint||owned==null||!(active||ready))return;
            var def=ShardDefinition(owned.id);var color=active?ActiveShardColor:ShardColor(def);
            var c=r.center;var now=Time.unscaledTime;var motion=!profile.reduceMotion;var lots=!profile.reducedVfx;
            var pulse=motion?Mathf.Sin(now*4)*.5f+.5f:.5f;var radius=r.width*.62f;
            ShardSoft(c,r.width*(2.4f+pulse*.4f),new Color(color.r,color.g,color.b,(ready?.3f:.2f)+pulse*.12f));
            // Rotating rune ring.
            var rot=motion?now*(ready?1.2f:.5f):0;
            for(var t=0;t<16;t++)
            {
                var a=rot+t*Mathf.PI/8;var inner=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(radius+16);var outer=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(radius+(t%4==0?26:21));
                DrawLine(inner,outer,new Color(color.r,color.g,color.b,.75f),t%4==0?2.4f:1.4f);
            }
            ShardCircle(c,radius+16,radius+16,new Color(color.r,color.g,color.b,.35f),1,64,-rot);
            // Ready: motes stream upward like a beacon.
            if(ready&&lots)for(var m=0;m<10;m++)
            {
                var mk=Mathf.Repeat(now*.7f+m*.1f,1f);var p=c+new Vector2(Mathf.Sin(m*2.3f+now)*radius*.7f,-mk*radius*2.2f);
                ShardSoft(p,10+(1-mk)*8,new Color(color.r,color.g,color.b,.8f*(1-mk)));Fill(new Rect(p.x-1,p.y-1,2,2),new Color(1,1,1,1-mk));
            }
        }

        // ---------------- hand layer: card glints ----------------
        // Active shard: boosted cards burn with its color. Charging: matching cards show a +2 sigil.
        private void DrawShardCardGlint(Rect r,HandView view,bool focus)
        {
            if(combat==null||view?.card==null)return;
            var card=view.card;var now=Time.unscaledTime;var motion=!profile.reduceMotion;
            if(!string.IsNullOrEmpty(combat.activeShardId))
            {
                if(!CombatState.MatchesShardArchetype(combat.activeShardId,card))return;
                var color=ActiveShardColor;var pulse=motion?Mathf.Sin(now*3+card.instanceId)*.5f+.5f:.5f;
                EnsureCardVfxTextures();
                DrawCardUiShape(new Rect(r.x-16,r.y-16,r.width+32,r.height+32),cardVfxEdge,new Color(color.r,color.g,color.b,(focus?.85f:.55f)+pulse*.2f));
                // A spark races around the card border.
                var perimeter=2*(r.width+r.height);var d=Mathf.Repeat(now*(motion?260:0)+card.instanceId*37,perimeter);
                Vector2 Edge(float x){if(x<r.width)return new Vector2(r.x+x,r.y);x-=r.width;if(x<r.height)return new Vector2(r.xMax,r.y+x);x-=r.height;if(x<r.width)return new Vector2(r.xMax-x,r.yMax);x-=r.width;return new Vector2(r.x,r.yMax-x);}
                for(var i=0;i<6;i++){var p=Edge(Mathf.Repeat(d-i*9,perimeter));ShardSoft(p,(18-i*2),new Color(color.r,color.g,color.b,.8f-i*.12f));}
                var sp=Edge(d);Fill(new Rect(sp.x-2,sp.y-2,4,4),Color.white);
                DrawShardCardSigil(r,color,"",pulse);
                return;
            }
            if(combat.shardAttunePending||combat.ShardChargeFull)return;
            var match=CombatState.MatchesShardArchetype(combat.chargeShardA,card)?combat.chargeShardA:CombatState.MatchesShardArchetype(combat.chargeShardB,card)?combat.chargeShardB:"";
            if(string.IsNullOrEmpty(match))return;
            DrawShardCardSigil(r,ShardColor(ShardDefinition(match)),"+2",motion?Mathf.Sin(now*2.5f+card.instanceId)*.5f+.5f:.5f);
        }
        private void DrawShardCardSigil(Rect r,Color color,string label,float pulse)
        {
            var at=new Vector2(r.center.x,r.y-4);var s=9+pulse*1.5f;
            ShardSoft(at,38,new Color(color.r,color.g,color.b,.45f+pulse*.2f));
            var pts=new[]{at+new Vector2(0,-s),at+new Vector2(s*.75f,0),at+new Vector2(0,s),at+new Vector2(-s*.75f,0)};
            for(var i=0;i<4;i++){DrawLine(pts[i],pts[(i+1)%4],new Color(.02f,.02f,.04f,.9f),5);DrawLine(pts[i],pts[(i+1)%4],color,2.2f);}
            Fill(new Rect(at.x-2,at.y-2,4,4),Color.white);
            if(!string.IsNullOrEmpty(label))GUI.Label(new Rect(at.x+8,at.y-11,40,22),label,new GUIStyle(ReadableStyle(12,true)){alignment=TextAnchor.MiddleLeft,normal={textColor=color}});
        }
    }
}
