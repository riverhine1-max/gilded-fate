using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    // Boss seal-break intro, boss phase-change overlay and carved rune medallions
    // behind enemy intent icons. Soft textures are generated once in code
    // (HideAndDontSave); all line work goes through DrawLine/Fill.
    public sealed partial class GildedMainMenu
    {
        private static Texture2D bossPolishGlow,bossPolishDisc,bossPolishRing,bossPolishThinRing;
        private CombatPhase bossPolishLastPhase;
        private float bossPolishEnemyTurnAt=-10;
        private static readonly Color BossPolishGold=new Color(1f,.78f,.34f);

        // ---------- shared helpers ----------
        private static Texture2D BossPolishTexture(string name,int n,System.Func<float,float> alphaAtRadius)
        {
            var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name=name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[n*n];var mid=(n-1)*.5f;
            for(var y=0;y<n;y++)for(var x=0;x<n;x++){var d=new Vector2(x-mid,y-mid).magnitude/(n*.5f);pixels[y*n+x]=new Color(1,1,1,Mathf.Clamp01(alphaAtRadius(d)));}
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        private static void EnsureBossPolishTextures()
        {
            if(!bossPolishGlow)bossPolishGlow=BossPolishTexture("Boss polish glow",64,d=>Mathf.Exp(-d*d*4f)*Mathf.Clamp01((1-d)*5));
            if(!bossPolishDisc)bossPolishDisc=BossPolishTexture("Boss polish disc",64,d=>(1-d)*32f);
            if(!bossPolishRing)bossPolishRing=BossPolishTexture("Boss polish ring",64,d=>Mathf.Min((1-d)*32f,(d-.86f)*32f));
            if(!bossPolishThinRing)bossPolishThinRing=BossPolishTexture("Boss polish thin ring",64,d=>Mathf.Min((1-d)*32f,(d-.95f)*32f));
        }
        private static void BossPolishTint(Rect rect,Texture2D texture,Color color)
        {
            if(!texture||color.a<=.003f)return;
            var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,true);GUI.color=old;
        }
        private static Rect BossPolishCentered(Vector2 c,float radius)=>new Rect(c.x-radius,c.y-radius,radius*2,radius*2);
        private static float BossPolishHash(int i,int seed){var v=Mathf.Sin(i*127.1f+seed*311.7f)*43758.5453f;return v-Mathf.Floor(v);}
        private static Color BossPolishAlpha(Color c,float a)=>new Color(c.r,c.g,c.b,a);
        private void BossPolishCircle(Vector2 c,float radius,Color color,float width,int segments,float rotation)
        {
            if(color.a<=.003f||radius<=0)return;
            var prev=c+new Vector2(Mathf.Cos(rotation),Mathf.Sin(rotation))*radius;
            for(var i=1;i<=segments;i++){var a=rotation+i*Mathf.PI*2/segments;var p=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;DrawLine(prev,p,color,width);prev=p;}
        }
        // Jagged glowing cracks growing outward from c; growth 0..1 reaches radius.
        private void BossPolishCracks(Vector2 c,float radius,float growth,float rotation,float alpha,int seed,int count)
        {
            const int steps=6;
            for(var i=0;i<count;i++)
            {
                var baseAngle=rotation+i*Mathf.PI*2/count+(BossPolishHash(i,seed)-.5f)*.6f;
                var reach=Mathf.Clamp01(growth)*(.72f+.3f*BossPolishHash(i,seed+7));
                var prev=c;
                for(var s=1;s<=steps;s++)
                {
                    var seg=Mathf.Clamp01((reach-(s-1f)/steps)*steps);if(seg<=0)break;
                    var f=(float)s/steps;var angle=baseAngle+(BossPolishHash(i*13+s,seed+3)-.5f)*.6f;
                    var target=c+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius*f;var p=Vector2.Lerp(prev,target,seg);
                    var width=Mathf.Lerp(3.2f,1f,f);
                    DrawLine(prev,p,new Color(1f,.5f,.16f,.4f*alpha),width+3.5f);
                    DrawLine(prev,p,new Color(1f,.95f,.8f,.95f*alpha),width);
                    if(seg<1)break;prev=target;
                }
            }
        }

        // ---------- 1. boss intro: the final seal breaks ----------
        private void DrawBossIntroCinematic(float w,float h)
        {
            var progress=Mathf.Clamp01(1f-bossIntroTime/2.4f);const float breakAt=.52f;var still=profile.reduceMotion;
            var matrix=GUI.matrix;var sinceBreak=progress-breakAt;
            if(!still&&profile.screenShake&&sinceBreak>0&&sinceBreak<.14f){var k=1-sinceBreak/.14f;k*=k;GUI.matrix=matrix*Matrix4x4.Translate(new Vector3(Mathf.Sin(shimmer*67)*k*5,Mathf.Cos(shimmer*53)*k*3.5f,0));}
            DrawBossIntro(w,h);
            DrawBossSeal(w,h,progress,breakAt,still);
            GUI.matrix=matrix;
        }
        private void DrawBossSeal(float w,float h,float progress,float breakAt,bool still)
        {
            EnsureBossPolishTextures();
            var scale=Mathf.Lerp(1.2f,1f,Mathf.SmoothStep(0,1,progress));
            var c=new Vector2(w*.5f,h*.25f+130*scale);var R=150*scale;var gold=BossPolishGold;
            var rot=still?0:shimmer*.35f;var flashScale=profile.reduceFlashing?.35f:1f;
            if(progress<breakAt)
            {
                var a=Mathf.Clamp01(progress/.1f);var crack=Mathf.Clamp01((progress-.16f)/(breakAt-.16f));var strain=crack*crack;
                BossPolishTint(BossPolishCentered(c,R*1.02f),bossPolishDisc,new Color(.03f,.02f,.012f,.74f*a*(1-strain*.3f)));
                BossPolishTint(BossPolishCentered(c,R*1.6f),bossPolishGlow,new Color(1f,.62f,.2f,(.2f+.28f*strain)*a*flashScale));
                DrawBossSealRunes(c,R,rot,a,scale);
                if(crack>0&&!still)BossPolishCracks(c,R*.98f,crack,rot*.7f,a,11,profile.reducedVfx?5:7);
                else if(crack>0)BossPolishCracks(c,R*.98f,1f,0,a*crack,11,5);
                BossPolishTint(BossPolishCentered(c,R*(.2f+.3f*strain)),bossPolishGlow,new Color(1f,.9f,.62f,.55f*strain*flashScale));
                return;
            }
            var t=Mathf.Clamp01((progress-breakAt)/(1-breakAt));
            if(still)
            {
                var fade=1-Mathf.Clamp01(t/.4f);if(fade<=0)return;
                DrawBossSealRunes(c,R,0,fade,scale);BossPolishCracks(c,R*.98f,1f,0,fade,11,5);return;
            }
            if(!profile.reduceFlashing){var flash=Mathf.Clamp01(1-t/.16f);Fill(new Rect(0,0,w,h),new Color(1f,.86f,.56f,flash*flash*.5f));}
            var sw=Mathf.Clamp01(t/.5f);
            BossPolishTint(BossPolishCentered(c,R*(1.1f+sw*1.2f)),bossPolishGlow,new Color(1f,.68f,.28f,(1-sw)*.55f*flashScale));
            BossPolishCircle(c,R*(1+sw*2.4f),new Color(1f,.76f,.34f,(1-sw)*.85f),(6f*(1-sw)+1f)*scale,40,0);
            if(!profile.reducedVfx)BossPolishCircle(c,R*(.8f+sw*1.7f),new Color(1f,.42f,.16f,(1-sw)*.5f),(3f*(1-sw)+.8f)*scale,32,.1f);
            var count=profile.reducedVfx?12:28;var ease=1-Mathf.Pow(1-t,2.4f);var shardAlpha=Mathf.Pow(1-t,1.3f);
            if(shardAlpha<=.01f)return;
            for(var i=0;i<count;i++)
            {
                var h1=BossPolishHash(i,21);var h2=BossPolishHash(i,37);var h3=BossPolishHash(i,53);
                var angle=rot+i*Mathf.PI*2/count+(h1-.5f)*.3f;var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                var dist=R*(.3f+.68f*h2)+ease*R*(1.3f+1.7f*h3);var spin=angle+Mathf.PI*.5f+t*(h1-.5f)*10f;
                var len=R*(.09f+.13f*h3)*(1-t*.35f);var sd=new Vector2(Mathf.Cos(spin),Mathf.Sin(spin));var p=c+dir*dist+new Vector2(0,t*t*R*.5f);
                DrawLine(p-sd*len*.5f,p+sd*len*.5f,BossPolishAlpha(gold*.8f,.8f*shardAlpha),(3.5f+3f*h2)*scale);
                DrawLine(p-sd*len*.42f,p+sd*len*.42f,new Color(1f,.96f,.82f,.9f*shardAlpha),1.2f*scale);
            }
        }
        private void DrawBossSealRunes(Vector2 c,float R,float rot,float a,float scale)
        {
            var gold=BossPolishGold;var ring=BossPolishAlpha(gold,.92f*a);var segs=profile.reducedVfx?28:40;
            BossPolishCircle(c,R,ring,3.2f*scale,segs,rot);
            BossPolishCircle(c,R*.9f,BossPolishAlpha(gold,.6f*a),1.4f*scale,segs,-rot*1.3f);
            BossPolishCircle(c,R*.62f,BossPolishAlpha(gold,.75f*a),2f*scale,segs-8,rot*.7f);
            BossPolishCircle(c,R*.3f,BossPolishAlpha(gold,.7f*a),1.6f*scale,20,-rot);
            for(var i=0;i<24;i++)
            {
                var angle=-rot*1.3f+i*Mathf.PI*2/24;var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var major=i%3==0;
                DrawLine(c+dir*R*.9f,c+dir*R*(major?.8f:.85f),BossPolishAlpha(gold,(major?.9f:.55f)*a),(major?2.4f:1.3f)*scale);
            }
            for(var i=0;i<6;i++)
            {
                var a0=rot*.7f+i*Mathf.PI/3;var a1=a0+Mathf.PI*2/3;
                DrawLine(c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*R*.62f,c+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*R*.62f,BossPolishAlpha(gold,.42f*a),1.3f*scale);
            }
            for(var i=0;i<8;i++)
            {
                var angle=rot+i*Mathf.PI/4+.2f;var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var tan=new Vector2(-dir.y,dir.x);var p=c+dir*R*.7f;var s=R*.05f;
                DrawLine(p-dir*s,p+dir*s,ring,1.6f*scale);DrawLine(p+dir*s,p+dir*s*.2f+tan*s*(i%2==0?1:-1),ring,1.3f*scale);DrawLine(p-dir*s*.3f-tan*s*.6f,p-dir*s*.3f+tan*s*.6f,ring,1.3f*scale);
            }
        }

        // ---------- 2. boss phase change ----------
        private void DrawBossPhaseCinematic(float w,float h)
        {
            EnsureBossPolishTextures();
            var still=profile.reduceMotion;var total=still?.35f:1.2f;var t=Mathf.Clamp01(1-bossPhaseTime/total);var fade=Mathf.Clamp01(bossPhaseTime*1.4f);
            var portrait=EnemyPortraitRect;var c=portrait.center;var R=Mathf.Max(portrait.width,portrait.height)*.5f;
            var pulse=still?1f:Mathf.Clamp01(t/.7f);var ringR=still?R*1.05f:R*(.6f+pulse*1.5f);var ra=(still?fade:1-pulse)*.85f;
            BossPolishTint(BossPolishCentered(c,R*1.3f),bossPolishGlow,new Color(1f,.28f,.1f,ra*.35f*(profile.reduceFlashing?.5f:1f)));
            BossPolishCircle(c,ringR,new Color(1f,.2f,.1f,ra),5f*(1-pulse)+1.6f,40,0);
            BossPolishCircle(c,ringR*.93f,new Color(1f,.78f,.32f,ra*.8f),2f,40,0);
            BossPolishCracks(c,R*.92f,still?1f:Mathf.Clamp01(t/.22f),.4f+lastBossPhase*1.3f,fade,29+lastBossPhase,profile.reducedVfx?4:6);
            DrawBossPhaseTransition(w,h);
            if(profile.reduceFlashing)return;
            var e=Mathf.Clamp01(1-t/.3f);e*=e;if(e<=.01f)return;
            var edge=Mathf.Min(w,h)*.045f;
            for(var i=0;i<3;i++)
            {
                var k=(3-i)/3f;var x=edge*i/3f;var ww=edge/3f+1;
                Fill(new Rect(x,0,ww,h),new Color(1f,.12f,.08f,.34f*e*k));Fill(new Rect(w-x-ww,0,ww,h),new Color(1f,.12f,.08f,.34f*e*k));
                Fill(new Rect(x+edge*.35f,0,ww,h),new Color(.2f,.85f,1f,.14f*e*k));Fill(new Rect(w-x-ww-edge*.35f,0,ww,h),new Color(.2f,.85f,1f,.14f*e*k));
                Fill(new Rect(0,x,w,ww*.6f),new Color(1f,.7f,.25f,.22f*e*k));Fill(new Rect(0,h-x-ww*.6f,w,ww*.6f),new Color(1f,.7f,.25f,.22f*e*k));
            }
        }

        // ---------- 3. enemy intent rune medallions ----------
        private static Color BossPolishIntentGlow(EnemyActionType type)=>type switch
        {
            EnemyActionType.Attack=>new Color(1f,.16f,.14f),
            EnemyActionType.Block=>new Color(.3f,.62f,1f),
            EnemyActionType.Strength or EnemyActionType.SummonWeapon or EnemyActionType.Heal=>new Color(1f,.76f,.28f),
            EnemyActionType.Weak or EnemyActionType.Vulnerable or EnemyActionType.Curse=>new Color(.68f,.36f,1f),
            _=>new Color(.2f,.9f,.82f)
        };
        private void DrawIntentMedallion(Rect icon,EnemyActionType type,float alpha)
        {
            if(combat==null)return;
            EnsureBossPolishTextures();
            if(combat.phase!=bossPolishLastPhase){if(combat.phase==CombatPhase.Enemy)bossPolishEnemyTurnAt=Time.unscaledTime;bossPolishLastPhase=combat.phase;}
            var glow=BossPolishIntentGlow(type);var c=icon.center;var radius=icon.width*.5f+3;
            var pulse=profile.reduceMotion?.5f:(Mathf.Sin(shimmer*2.2f+c.x*.013f)+1)*.5f;
            var flare=0f;
            if(type==EnemyActionType.Attack&&combat.phase==CombatPhase.Enemy)flare=.55f+.45f*Mathf.Clamp01(1-(Time.unscaledTime-bossPolishEnemyTurnAt)/.6f);
            var old=GUI.color;
            var glowA=(.26f+.14f*pulse+flare*(profile.reduceFlashing?.2f:.55f))*alpha;
            BossPolishTint(BossPolishCentered(c,radius*(1.5f+.08f*pulse+(profile.reduceMotion?0:flare*.25f))),bossPolishGlow,BossPolishAlpha(glow,glowA));
            BossPolishTint(BossPolishCentered(c,radius),bossPolishDisc,new Color(.035f,.03f,.028f,.86f*alpha));
            BossPolishTint(BossPolishCentered(c,radius*.9f),bossPolishDisc,new Color(glow.r*.2f,glow.g*.2f,glow.b*.2f,.65f*alpha));
            BossPolishTint(BossPolishCentered(c,radius),bossPolishRing,new Color(.95f,.74f,.36f,.95f*alpha));
            BossPolishTint(BossPolishCentered(c,radius*.8f),bossPolishThinRing,BossPolishAlpha(Color.Lerp(glow,Color.white,.25f+.3f*flare),(.45f+.2f*pulse+.3f*flare)*alpha));
            if(!profile.reducedVfx)for(var i=0;i<8;i++)
            {
                var angle=i*Mathf.PI/4+Mathf.PI/8;var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                DrawLine(c+dir*radius*.83f,c+dir*radius*.93f,new Color(.42f,.3f,.12f,.9f*alpha),1.6f);
            }
            GUI.color=old;
        }
    }
}
