using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    // Presentation-only glow for the combat Energy vessel: layered bloom, a slowly
    // swirling inner light, orbiting embers and one gem pip per Energy point.
    // Hooks: DrawEnergyOrbBack (before the vessel art in DrawEnergyMeter) and
    // DrawEnergyOrbFront (last line of DrawEnergyOrbVfx, before the number label).
    // Textures are generated once (HideAndDontSave). Reduce Motion, Reduced VFX
    // and Reduce Flashing are honoured throughout; gameplay state is only read.
    public sealed partial class GildedMainMenu
    {
        private const int EnergyOrbPipCap=10;
        private Texture2D energyOrbBloom,energyOrbCore,energyOrbSwirl;
        private CombatState energyOrbCombat;
        private int energyOrbLast=-1,energyOrbTurn=-1,energyOrbPeak,energyOrbFrame=-1;
        private float energyOrbSurgeAt=-10,energyOrbSurgeStrength;
        private readonly float[] energyOrbLitAt=new float[EnergyOrbPipCap],energyOrbDarkUntil=new float[EnergyOrbPipCap],energyOrbBurstAt=new float[EnergyOrbPipCap];

        private void EnsureEnergyOrbTextures()
        {
            EnsureCardVfxTextures();
            if(energyOrbBloom&&energyOrbCore&&energyOrbSwirl)return;
            energyOrbBloom=CreateCardVfxTexture("Energy orb bloom",128,128,(x,y)=>
            {
                var d=new Vector2(x-63.5f,y-63.5f).magnitude/64f;
                return new Color(1,1,1,Mathf.Clamp01((.5f*Mathf.Exp(-d*d*3.5f)+.5f*Mathf.Exp(-d*3f))*Mathf.Clamp01((1-d)*3f)));
            });
            // Lit rim with a softer centre so the number stays legible.
            energyOrbCore=CreateCardVfxTexture("Energy orb core",64,64,(x,y)=>
            {
                var d=new Vector2(x-31.5f,y-31.5f).magnitude/32f;
                var rim=Mathf.Exp(-Mathf.Pow((d-.6f)/.22f,2))*.8f;var centre=Mathf.Exp(-d*d*3.2f)*.42f;
                return new Color(1,1,1,Mathf.Clamp01((rim+centre)*Mathf.Clamp01((1-d)*5f)));
            });
            // Two soft spiral arms; drawn rotated and mirrored for the swirl.
            energyOrbSwirl=CreateCardVfxTexture("Energy orb swirl",96,96,(x,y)=>
            {
                var v=new Vector2(x-47.5f,y-47.5f);var d=v.magnitude/48f;var a=Mathf.Atan2(v.y,v.x);
                var arm=Mathf.Pow(Mathf.Max(0,Mathf.Cos(2*(a-d*3.1f))),5);
                var window=Mathf.SmoothStep(0,1,Mathf.Clamp01((d-.08f)/.3f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((d-.62f)/.36f)));
                return new Color(1,1,1,Mathf.Clamp01(arm*window*.95f));
            });
        }

        private static Color EnergyOrbAccent(HeroId hero)=>hero==HeroId.Hexer?new Color(.64f,.36f,.94f):hero==HeroId.Reaper?new Color(.22f,.84f,.74f):new Color(.92f,.26f,.16f);
        private int EnergyOrbBaseMax=>3+(run!=null&&run.relics.Contains("gilded_heart")?1:0);
        private int EnergyOrbPipCount=>Mathf.Clamp(Mathf.Max(EnergyOrbBaseMax,Mathf.Max(energyOrbPeak,combat.energy)),1,EnergyOrbPipCap);

        // Once per frame: detect spends (pip bursts), gains and turn-start refills (surge).
        private void TrackEnergyOrb()
        {
            if(energyOrbFrame==Time.frameCount)return;energyOrbFrame=Time.frameCount;
            var now=Time.unscaledTime;var e=combat.energy;
            if(!ReferenceEquals(combat,energyOrbCombat))
            {
                energyOrbCombat=combat;for(var i=0;i<EnergyOrbPipCap;i++){energyOrbLitAt[i]=energyOrbBurstAt[i]=-10;energyOrbDarkUntil[i]=0;}
                energyOrbSurgeAt=-10;energyOrbLast=0;energyOrbTurn=combat.turn;energyOrbPeak=e;
                if(combat.phase==CombatPhase.Player&&e>0)RelightEnergyOrb(0,e,now,true);
                energyOrbLast=e;return;
            }
            var turnStart=combat.turn!=energyOrbTurn&&combat.phase==CombatPhase.Player;
            if(combat.turn!=energyOrbTurn){energyOrbTurn=combat.turn;energyOrbPeak=e;}else energyOrbPeak=Mathf.Max(energyOrbPeak,e);
            if(e<energyOrbLast)for(var i=Mathf.Max(0,e);i<Mathf.Min(energyOrbLast,EnergyOrbPipCap);i++){energyOrbBurstAt[i]=now;energyOrbLitAt[i]=-10;energyOrbDarkUntil[i]=0;}
            if(e>energyOrbLast||turnStart)RelightEnergyOrb(turnStart?0:Mathf.Max(0,energyOrbLast),e,now,turnStart);
            energyOrbLast=e;
        }
        private void RelightEnergyOrb(int from,int to,float now,bool refill)
        {
            var motion=CardVfxMotionAllowed;
            if(motion){energyOrbSurgeAt=now;energyOrbSurgeStrength=refill?1f:.6f;}
            for(var i=from;i<Mathf.Min(to,EnergyOrbPipCap);i++)
            {
                // Pips relight left to right, riding the surge as it climbs.
                var t=motion?now+(refill?.12f:.05f)+(i-from)*.09f:now;
                energyOrbLitAt[i]=t;if(i>=energyOrbLast)energyOrbDarkUntil[i]=t;
            }
        }

        private float EnergyOrbSurge(float now)
        {
            if(!CardVfxMotionAllowed)return 0;var k=(now-energyOrbSurgeAt)/.75f;
            return k<0||k>=1?0:Mathf.Sin(k*Mathf.PI)*energyOrbSurgeStrength;
        }
        // 0 = dim ember, 1 = full; slightly over 1 when overcharged.
        private float EnergyOrbGlow(float now)
        {
            var e=combat.energy;if(e<=0)return 0;
            var g=.45f+.55f*Mathf.Clamp01(e/(float)EnergyOrbBaseMax);if(e>EnergyOrbBaseMax)g=1.1f;
            if(!profile.reduceFlashing)g+=.3f*EnergyOrbSurge(now);
            return Mathf.Min(1.25f,g);
        }
        private float EnergyOrbBreath=>CardVfxMotionAllowed?Mathf.Sin(shimmer*1.8f):0;
        private static Vector2 EnergyOrbCentre(Rect seal)=>seal.center+new Vector2(0,-1.5f);

        // Behind the vessel art: outer bloom, mid halo and the far side of the ember orbits.
        private void DrawEnergyOrbBack(Rect seal,Color _)
        {
            if(!CardVfxRepaint||combat==null||run==null)return;
            EnsureEnergyOrbTextures();TrackEnergyOrb();
            var now=Time.unscaledTime;var c=EnergyOrbCentre(seal);var d=Mathf.Min(seal.width,seal.height);
            var accent=EnergyOrbAccent(run.hero);var g=EnergyOrbGlow(now);var breath=EnergyOrbBreath;
            if(g<=0)
            {
                // Spent: a low ember smoulder, no swirl.
                var flicker=CardVfxMotionAllowed?CardVfxFlicker(shimmer*2.2f,17):.5f;
                var ember=Color.Lerp(new Color(.78f,.26f,.10f),accent,.3f);ember.a=.14f+.08f*flicker;
                var es=d*1.6f;DrawCardUiShape(new Rect(c.x-es*.5f,c.y-es*.5f,es,es),energyOrbBloom,ember);
                return;
            }
            var outer=Color.Lerp(accent,Gold,.42f);outer.a=Mathf.Clamp01((.15f+.50f*g)*(1+.08f*breath));
            var size=d*(2.05f+.07f*breath);DrawCardUiShape(new Rect(c.x-size*.5f,c.y-size*.5f,size,size),energyOrbBloom,outer);
            if(!profile.reducedVfx)
            {
                var mid=Color.Lerp(accent,Gold,.68f);mid.a=Mathf.Clamp01(.10f+.48f*g);
                var ms=d*(1.6f+.05f*breath);DrawCardUiShape(new Rect(c.x-ms*.5f,c.y-ms*.5f,ms,ms),cardVfxGlow,mid);
            }
            DrawEnergyOrbEmbers(seal,accent,g,false);
        }

        // In front of the vessel art, before the number label.
        private void DrawEnergyOrbFront(Rect seal,Color _)
        {
            if(!CardVfxRepaint||combat==null||run==null)return;
            EnsureEnergyOrbTextures();TrackEnergyOrb();
            var now=Time.unscaledTime;var c=EnergyOrbCentre(seal);var d=Mathf.Min(seal.width,seal.height);var hr=d*.23f;
            var accent=EnergyOrbAccent(run.hero);var g=EnergyOrbGlow(now);var breath=EnergyOrbBreath;var motion=CardVfxMotionAllowed;
            var coreColor=Color.Lerp(Color.Lerp(Gold,Color.white,.4f),accent,.14f);
            if(g>0)
            {
                // Swirling inner light: counter-rotating spiral arms inside the hollow.
                var matrix=GUI.matrix;var layers=profile.reducedVfx?1:3;
                for(var i=0;i<layers;i++)
                {
                    var angle=motion?shimmer*(i==0?26f:i==1?-17f:9f)+i*60f:i*60f;var s=(i==0?2.5f:i==1?2.1f:2.9f)*hr*(1+.04f*breath);
                    var tint=i==0?Color.Lerp(accent,Gold,.5f):i==1?coreColor:accent;tint.a=Mathf.Clamp01((i==0?.46f:i==1?.34f:.22f)*g);
                    GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(c.x,c.y,0),Quaternion.Euler(0,0,angle),Vector3.one)*Matrix4x4.Translate(new Vector3(-c.x,-c.y,0));
                    DrawCardUiShape(new Rect(c.x-s*.5f,c.y-s*.5f,s,s),energyOrbSwirl,tint,i==1);
                }
                GUI.matrix=matrix;
                var core=coreColor;core.a=Mathf.Clamp01((.18f+.55f*g)*(1+.06f*breath));
                var cs=hr*3f*(1+.03f*breath);DrawCardUiShape(new Rect(c.x-cs*.5f,c.y-cs*.5f,cs,cs),energyOrbCore,core);
                if(!profile.reducedVfx){var spill=Color.Lerp(accent,Gold,.6f);spill.a=.10f*g;var ss=d*1.12f;DrawCardUiShape(new Rect(c.x-ss*.5f,c.y-ss*.5f,ss,ss),cardVfxGlow,spill);}
                DrawEnergyOrbSurgeWave(c,hr,core.a,now);
            }
            else
            {
                var ember=Color.Lerp(new Color(.85f,.32f,.12f),accent,.25f);ember.a=.16f+(motion?.07f*CardVfxFlicker(shimmer*3.1f,23):0);
                var es=hr*2.4f;DrawCardUiShape(new Rect(c.x-es*.5f,c.y-es*.5f,es,es),energyOrbCore,ember);
            }
            // Dark soft disc keeps the number readable over the glow.
            DrawCardUiShape(new Rect(c.x-33,c.y-22,66,44),cardVfxGlow,new Color(0,0,0,g>0?.58f:.35f));
            DrawEnergyOrbPips(seal,accent,g,now);
            DrawEnergyOrbEmbers(seal,accent,g,true);
        }

        private void DrawEnergyOrbSurgeWave(Vector2 c,float hr,float baseAlpha,float now)
        {
            if(!CardVfxMotionAllowed)return;var k=(now-energyOrbSurgeAt)/.75f;if(k<0||k>=1)return;
            // A bright band climbs through the hollow, its width following the chord.
            var ease=1-(1-k)*(1-k);var y=Mathf.Lerp(hr*1.05f,-hr*1.2f,ease);var w=Mathf.Sqrt(Mathf.Max(0,hr*hr*1.35f-y*y));if(w<2)return;
            var fade=Mathf.Sin(k*Mathf.PI)*energyOrbSurgeStrength;var cap=profile.reduceFlashing?baseAlpha:1f;
            var band=new Color(1f,.93f,.72f,Mathf.Min(cap,.85f*fade));var soft=Color.Lerp(Gold,Color.white,.2f);soft.a=Mathf.Min(cap,.35f*fade);
            DrawCardUiShape(new Rect(c.x-w*1.2f,c.y+y-12,w*2.4f,24),cardVfxGlow,soft);
            DrawCardUiShape(new Rect(c.x-w,c.y+y-4,w*2,8),cardVfxGlow,band);
        }

        private Vector2 EnergyOrbPipPosition(Rect seal,int i,int n)
        {
            var step=n<=1?0:Mathf.Min(24f,140f/(n-1));var a=(-90f+(i-(n-1)*.5f)*step)*Mathf.Deg2Rad;
            return EnergyOrbCentre(seal)+new Vector2(Mathf.Cos(a)*(seal.width*.5f+6),Mathf.Sin(a)*(seal.height*.5f+9));
        }
        private void DrawEnergyOrbPips(Rect seal,Color accent,float g,float now)
        {
            var n=EnergyOrbPipCount;var motion=CardVfxMotionAllowed;var gem=Color.Lerp(Gold,accent,.32f);
            for(var i=0;i<n;i++)
            {
                var p=EnergyOrbPipPosition(seal,i,n);var lit=i<combat.energy&&now>=energyOrbDarkUntil[i];
                var pop=Mathf.Clamp01((now-energyOrbLitAt[i])/.35f);var flash=lit&&pop<1?1-pop:0;
                var scale=1+(motion?.55f*flash*flash:0);var w=10f*scale;var h=13f*scale;
                if(lit)
                {
                    var pulse=motion?.85f+.15f*Mathf.Sin(shimmer*3f+i*1.7f):.9f;
                    var glow=gem;glow.a=Mathf.Clamp01(.42f*pulse+(profile.reduceFlashing?0:.45f*flash));var gs=26*scale;
                    DrawCardUiShape(new Rect(p.x-gs*.5f,p.y-gs*.5f,gs,gs),cardVfxGlow,glow);
                }
                DrawCardUiShape(new Rect(p.x-w*.62f,p.y-h*.62f,w*1.24f,h*1.24f),cardVfxPip,new Color(.46f,.35f,.18f,.92f));
                if(lit)
                {
                    var face=Color.Lerp(gem,Color.white,profile.reduceFlashing?.15f:.15f+.6f*flash);face.a=1;
                    DrawCardUiShape(new Rect(p.x-w*.5f,p.y-h*.5f,w,h),cardVfxPip,face);
                    var glint=motion?Mathf.Pow(Mathf.Max(0,Mathf.Sin(shimmer*2.4f-i*.9f)),12):0;
                    if(glint>.02f){var gs=9f;DrawCardUiShape(new Rect(p.x-gs*.5f-1,p.y-gs*.5f-2,gs,gs),cardVfxGlow,new Color(1,1,1,.8f*glint));}
                }
                else DrawCardUiShape(new Rect(p.x-w*.5f,p.y-h*.5f,w,h),cardVfxPip,new Color(.07f,.06f,.06f,.96f));
                DrawEnergyOrbPipBurst(p,i,gem,now);
            }
        }
        private void DrawEnergyOrbPipBurst(Vector2 p,int i,Color gem,float now)
        {
            var k=(now-energyOrbBurstAt[i])/.55f;if(k<0||k>=1)return;var fade=1-k;
            var cap=profile.reduceFlashing?.42f:.85f;
            if(!CardVfxMotionAllowed){var still=gem;still.a=.4f*fade;DrawCardUiShape(new Rect(p.x-13,p.y-13,26,26),cardVfxGlow,still);return;}
            var ease=1-fade*fade;var ring=Mathf.Lerp(10,40,ease);var rc=Color.Lerp(gem,Color.white,profile.reduceFlashing?0:.3f);rc.a=Mathf.Min(cap,.9f*fade);
            DrawCardUiShape(new Rect(p.x-ring*.5f,p.y-ring*.5f,ring,ring),cardVfxRing,rc);
            if(profile.reducedVfx)return;
            var age=k*.55f;var seed=Mathf.FloorToInt(energyOrbBurstAt[i]*60)+i*31;
            for(var s=0;s<5;s++)
            {
                var a=(-90f+(CardVfxHash(seed,s)-.5f)*200f)*Mathf.Deg2Rad;var speed=45+CardVfxHash(seed,s+10)*55;
                var q=p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*speed*age+new Vector2(0,110*age*age);var sz=2.2f+CardVfxHash(seed,s+20)*1.6f;
                var spark=Color.Lerp(gem,Color.white,.35f);spark.a=Mathf.Min(cap,fade);Fill(new Rect(q.x-sz*.5f,q.y-sz*.5f,sz,sz),spark);
            }
        }

        // Elliptical ember orbits; the far half is drawn behind the vessel art.
        private void DrawEnergyOrbEmbers(Rect seal,Color accent,float g,bool front)
        {
            if(!CardVfxParticles||combat.energy<=0)return;
            var c=EnergyOrbCentre(seal);var count=Mathf.Min(6,3+Mathf.Min(combat.energy,3));
            for(var i=0;i<count;i++)
            {
                var a=seal.width*(.60f+.12f*CardVfxHash(i,41));var b=a*(.30f+.14f*CardVfxHash(i,42));var tilt=(CardVfxHash(i,43)-.5f)*.7f;
                var theta=CardVfxHash(i,44)*Mathf.PI*2+shimmer*(.55f+.45f*CardVfxHash(i,45));
                var near=Mathf.Sin(theta)>0;if(near!=front)continue;
                var local=new Vector2(Mathf.Cos(theta)*a,Mathf.Sin(theta)*b);var ct=Mathf.Cos(tilt);var st=Mathf.Sin(tilt);
                var q=c+new Vector2(local.x*ct-local.y*st,local.x*st+local.y*ct);
                var flicker=CardVfxFlicker(shimmer*4f,60+i);var depth=front?1f:.45f;var size=3.2f+2f*CardVfxHash(i,46);
                var halo=Color.Lerp(accent,Gold,.55f);halo.a=Mathf.Clamp01((.25f+.35f*flicker)*(.55f+.45f*g)*depth);
                var hs=size*3.4f;DrawCardUiShape(new Rect(q.x-hs*.5f,q.y-hs*.5f,hs,hs),cardVfxGlow,halo);
                var hot=Color.Lerp(Gold,Color.white,.55f);hot.a=Mathf.Clamp01((.55f+.4f*flicker)*depth);
                DrawCardUiShape(new Rect(q.x-size*.5f,q.y-size*.5f,size,size),cardVfxGlow,hot);
            }
        }
    }
}
