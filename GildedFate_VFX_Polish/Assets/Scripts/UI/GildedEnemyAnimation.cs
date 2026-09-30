using System.Collections.Generic;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    // Procedural 2D "puppet" animation for the illustrated enemy portraits.
    // Presentation only: every pose is a pure function of unscaled time plus a few
    // timestamps taken from receipts the combat screen already presents (enemy turn
    // start, the EnemyAction beat, hit/contact beats, deaths and boss phase changes).
    // Rules, resolution order, AI and saves are untouched.
    //
    // The sprite is transformed around its feet with GUI.matrix only. Health bars,
    // intents, status strips and hit targets keep their stable anchors. Each enemy
    // gets an archetype (beast, crawler, knight, brute, caster, spirit, colossus)
    // so a hound pounces, a knight swings, a brute slams and casters rise and thrust.
    //
    // Reduce Motion freezes every transform (fades only). Reduce Flashing swaps the
    // white hit flash for a dim warm tint. Reduced VFX halves the charge aura.
    public sealed partial class GildedMainMenu
    {
        private enum EnemyMotionKind { Beast, Crawler, Knight, Brute, Caster, Spirit, Colossus }
        private enum EnemyStrikeStyle { Claw, Bite, Chomp, Fang, Blade, Cleave, Slam, Bolt, Shards, Cards, Spectral, Crush, Grab }

        private readonly struct EnemyMotionProfile
        {
            public readonly EnemyMotionKind kind;
            public readonly EnemyStrikeStyle strike;
            public readonly float mass,hover,tempo,lunge;
            public readonly Color accent;
            public EnemyMotionProfile(EnemyMotionKind kind,EnemyStrikeStyle strike,float mass,float hover,float tempo,float lunge,Color accent)
            {this.kind=kind;this.strike=strike;this.mass=mass;this.hover=hover;this.tempo=tempo;this.lunge=lunge;this.accent=accent;}
        }

        // Tuned to each illustration: mass damps hit recoil, lunge is the strike reach
        // in canvas pixels, accent colours the charge aura and the attack VFX.
        private static EnemyMotionProfile EnemyMotionFor(string id)=>id switch
        {
            "vault_rat"=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Bite,.55f,0,1.7f,58,new Color(1f,.56f,.22f)),
            "ash_hound"=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,.75f,0,1.3f,78,new Color(1f,.42f,.12f)),
            "golden_beast"=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.25f,0,.95f,70,new Color(1f,.78f,.30f)),
            "vault_spider"=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Fang,.6f,0,1.6f,52,new Color(.70f,.44f,1f)),
            "coin_mimic"=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Chomp,.85f,0,1.1f,48,new Color(1f,.80f,.34f)),
            "gilded_sentry"=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1f,0,1f,54,new Color(1f,.80f,.38f)),
            "broken_knight"=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1f,0,.9f,50,new Color(.94f,.62f,.32f)),
            "executioner"=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Cleave,1.25f,0,.8f,58,new Color(1f,.24f,.20f)),
            "chained_brute"=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,1.3f,0,.8f,44,new Color(.88f,.82f,.74f)),
            "masked_acolyte"=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.9f,4,1f,20,new Color(.74f,.42f,1f)),
            "rune_mage"=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.9f,6,1.1f,20,new Color(.40f,.64f,1f)),
            "golden_wisp"=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Bolt,.5f,10,1.3f,46,new Color(1f,.84f,.40f)),
            "mirror_witch"=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Shards,1.05f,3,.9f,22,new Color(.45f,.95f,.84f)),
            "collector"=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Grab,1.1f,0,.9f,52,new Color(.96f,.32f,.28f)),
            "hollow_king"=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Spectral,1.5f,0,.7f,30,new Color(.66f,.90f,1f)),
            "vault_mother"=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.7f,0,.6f,34,new Color(.96f,.86f,.46f)),
            "last_dealer"=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Cards,1.3f,4,.8f,24,new Color(.96f,.30f,.36f)),
            _=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1f,0,1f,50,new Color(1f,.62f,.36f))
        };
        private static bool EnemyStrikeIsRanged(EnemyStrikeStyle style)=>style is EnemyStrikeStyle.Bolt or EnemyStrikeStyle.Shards or EnemyStrikeStyle.Cards or EnemyStrikeStyle.Spectral;

        private sealed class EnemyAnim
        {
            public string id;
            public EnemyMotionProfile motion;
            public bool boss,elite,attacking,stealsGold,summons,curses,banishes;
            public float seed;
            public float turnAt=-10,windLength=.3f,actAt=-10,contactAt=-10,lastPredictedContactAt=-10,lastContactAt=-10;
            public float castAt=-10,hitAt=-10,hitPower,statusAt=-10,roarAt=-10;
            public int threat,predictedContacts,contacts,lastPredictedHit,lastShownHit,hitAtlas=7;
            public Color castColor=new Color(.5f,.75f,1f);
            public Vector2 shownCenter,shownFeet;
            public void Clear()
            {
                id=null;turnAt=actAt=contactAt=lastPredictedContactAt=lastContactAt=castAt=hitAt=statusAt=roarAt=-10;
                hitPower=0;threat=predictedContacts=contacts=lastPredictedHit=lastShownHit=0;attacking=stealsGold=summons=curses=banishes=false;hitAtlas=7;
            }
        }
        private struct EnemyPose { public Vector2 offset; public float angle,sx,sy,alpha,flash,aura; public Color auraColor; }

        private readonly EnemyAnim[] enemyAnims={new EnemyAnim(),new EnemyAnim(),new EnemyAnim(),new EnemyAnim()};
        private sealed class EnemySilhouetteSet { public Texture2D solid,glow; public float padX,padY; }
        private readonly Dictionary<Texture2D,EnemySilhouetteSet> enemySilhouettes=new();

        private void ResetEnemyAnimation(){foreach(var a in enemyAnims)a.Clear();}
        private EnemyAnim EnemyAnimFor(int index,EnemyDef def)
        {
            var a=enemyAnims[Mathf.Clamp(index,0,enemyAnims.Length-1)];
            if(def!=null&&a.id!=def.id)
            {
                a.id=def.id;a.motion=EnemyMotionFor(def.id);a.boss=def.boss;a.elite=def.elite;
                a.seed=CardVfxHash(def.id.Length*31+index*7,def.id[0])*6.283f;
            }
            return a;
        }
        private EnemyDef FinalEnemyDef(int index)=>index>=0&&index<finalEnemyDefs.Length?finalEnemyDefs[index]:null;
        private static Color EnemyCastColor(EnemyActionType type)=>type switch
        {
            EnemyActionType.Block=>new Color(.42f,.74f,1f),
            EnemyActionType.Strength=>new Color(1f,.42f,.20f),
            EnemyActionType.Heal=>new Color(.55f,.95f,.55f),
            EnemyActionType.StealGold or EnemyActionType.SummonWeapon=>new Color(1f,.80f,.36f),
            EnemyActionType.Weak=>new Color(.62f,.86f,.42f),
            EnemyActionType.Vulnerable=>new Color(1f,.34f,.30f),
            _=>new Color(.74f,.46f,1f)
        };

        // ---------- timeline hooks ----------
        // AnimateEnemyTurn, when the enemy phase begins: every living enemy telegraphs
        // what its (already visible) intent will do.
        private void BeginEnemyTurnAnimation(float anticipate)
        {
            if(combat==null)return;
            if(finalVfxCombat!=combat)ResetFinalCombatVfx();
            var now=Time.unscaledTime;
            for(var i=0;i<combat.EnemyCount&&i<enemyAnims.Length;i++)
            {
                if(!combat.IsLivingTarget(i))continue;
                var a=EnemyAnimFor(i,FinalEnemyDef(i));
                a.turnAt=now;a.windLength=Mathf.Max(.16f,AnimationSeconds(anticipate));
                a.actAt=a.contactAt=a.lastPredictedContactAt=a.lastContactAt=-10;a.predictedContacts=a.contacts=a.lastPredictedHit=a.lastShownHit=0;
                a.attacking=a.stealsGold=a.summons=a.curses=a.banishes=false;a.threat=0;
                var total=0;var castSet=false;
                foreach(var action in EnemyIntents(i))
                {
                    if(action.prevented)continue;
                    switch(action.type)
                    {
                        case EnemyActionType.Attack:a.attacking=true;total+=action.TotalDamage;break;
                        case EnemyActionType.StealGold:a.stealsGold=true;break;
                        case EnemyActionType.SummonWeapon:a.summons=true;break;
                        case EnemyActionType.Curse:a.curses=true;break;
                        case EnemyActionType.ExhaustDiscard:a.banishes=true;break;
                    }
                    if(action.type!=EnemyActionType.Attack&&!castSet){a.castColor=EnemyCastColor(action.type);castSet=true;}
                }
                a.threat=a.attacking?EnemyIntentAction.ThreatIcon(total):0;
            }
        }

        // ---------- pose ----------
        private float EnemyDeathDuration(EnemyAnim a,bool group)=>profile.reduceMotion?.45f:AnimationSeconds(a.boss?1.55f:a.elite?1f:group?.62f:.75f);
        private static float EnemyFlashPulse(float x,float center)=>Mathf.Max(0,1-Mathf.Abs(x-center)/.05f);

        private static void EnemyWindPose(EnemyMotionKind kind,float w,ref EnemyPose p)
        {
            switch(kind)
            {
                case EnemyMotionKind.Beast:p.sy-=.07f*w;p.sx+=.05f*w;p.offset.x+=12*w;p.angle+=3*w;break;
                case EnemyMotionKind.Crawler:p.sy+=.05f*w;p.offset.x+=8*w;p.angle+=5*w;break;
                case EnemyMotionKind.Knight:p.offset.x+=10*w;p.angle+=4.5f*w;p.sy+=.015f*w;break;
                case EnemyMotionKind.Brute:p.offset.y-=8*w;p.sy+=.05f*w;p.sx-=.02f*w;p.angle+=2.5f*w;break;
                case EnemyMotionKind.Caster:p.offset.y-=12*w;p.sx+=.03f*w;p.sy+=.03f*w;p.angle+=2*w;break;
                case EnemyMotionKind.Spirit:p.offset.x+=16*w;p.sx+=.08f*w;p.sy+=.08f*w;break;
                default:p.offset.x+=6*w;p.sx+=.04f*w;p.sy+=.05f*w;p.angle+=2.5f*w;break;
            }
        }
        // s: approach progress (1 = contact). Negative x is toward the hero.
        private static void EnemyLungePose(EnemyMotionKind kind,float reach,float s,float k,ref EnemyPose p)
        {
            switch(kind)
            {
                case EnemyMotionKind.Beast:p.offset.x-=reach*k;p.offset.y-=Mathf.Sin(s*Mathf.PI)*22*k;p.angle-=7*k;p.sx+=.07f*k;p.sy-=.05f*k;break;
                case EnemyMotionKind.Crawler:p.offset.x-=reach*k;p.angle-=8*k;p.sx+=.06f*k;p.sy-=.06f*k;break;
                case EnemyMotionKind.Knight:p.offset.x-=reach*.95f*k;p.angle-=8*k;p.sx+=.03f*k;break;
                case EnemyMotionKind.Brute:p.offset.x-=reach*.6f*k;p.offset.y-=(1-s)*10*k;p.sx+=.06f*k;p.sy-=.10f*k;p.angle-=4*k;break;
                case EnemyMotionKind.Caster:p.offset.x-=reach*k;p.angle-=3*k;p.sx+=.04f*k;p.sy+=.04f*k;break;
                case EnemyMotionKind.Spirit:p.offset.x-=reach*k;p.sx+=.10f*k;p.sy-=.08f*k;break;
                default:p.offset.x-=reach*k;p.angle-=5*k;p.sy+=.03f*k;break;
            }
        }

        private EnemyPose EnemyPoseAt(EnemyAnim a,float now,float deathAt,bool group)
        {
            var pose=new EnemyPose{sx=1,sy=1,alpha=1,auraColor=a.motion.accent};
            var m=a.motion;var moving=!profile.reduceMotion;var dead=deathAt>=0;
            var reach=m.lunge*(group?.62f:1f);
            if(moving&&!dead)
            {
                var t=now*m.tempo+a.seed;
                switch(m.kind)
                {
                    case EnemyMotionKind.Beast:{var b=Mathf.Sin(t*2.1f);pose.sy+=.014f*b;pose.sx-=.006f*b;pose.angle+=.35f*Mathf.Sin(t*.9f);break;}
                    case EnemyMotionKind.Crawler:pose.offset.y-=1.8f*Mathf.Abs(Mathf.Sin(t*2.6f));pose.sx+=.007f*Mathf.Sin(t*5.2f);pose.angle+=.3f*Mathf.Sin(t*1.3f);break;
                    case EnemyMotionKind.Knight:pose.sy+=.009f*Mathf.Sin(t*1.5f);pose.angle+=.45f*Mathf.Sin(t*.7f);break;
                    case EnemyMotionKind.Brute:{var b=Mathf.Sin(t*1.25f);pose.sy+=.016f*b;pose.sx+=.007f*b;pose.angle+=.3f*Mathf.Sin(t*.6f);break;}
                    case EnemyMotionKind.Caster:pose.offset.y-=m.hover*(.5f+.5f*Mathf.Sin(t*1.3f));pose.angle+=.7f*Mathf.Sin(t*.8f);pose.sy+=.006f*Mathf.Sin(t*1.6f);break;
                    case EnemyMotionKind.Spirit:{pose.offset.y-=m.hover*(.5f+.5f*Mathf.Sin(t*1.7f));var s=.02f*Mathf.Sin(t*2.4f);pose.sx+=s;pose.sy+=s;pose.angle+=1.2f*Mathf.Sin(t*1.1f);break;}
                    default:pose.angle+=.6f*Mathf.Sin(t*.55f);pose.sy+=.01f*Mathf.Sin(t*1.1f);break;
                }
            }
            // Telegraph -> strike -> hold through every hit -> recover.
            var sinceTurn=now-a.turnAt;
            if(!dead&&sinceTurn>=0&&sinceTurn<8)
            {
                var w=Mathf.SmoothStep(0,1,Mathf.Clamp01(sinceTurn/a.windLength));
                var acted=a.actAt>0&&now>=a.actAt;
                // An enemy that never acts (fight ended first) releases its telegraph.
                if(!acted)w*=1-Mathf.Clamp01((sinceTurn-a.windLength-2.5f)/.4f);
                if(a.attacking)
                {
                    var contact=a.contactAt>a.actAt?a.contactAt:a.actAt+.25f;
                    var recoverFrom=Mathf.Max(a.lastPredictedContactAt,a.lastContactAt);
                    var waiting=a.contacts<a.predictedContacts&&now<a.lastPredictedContactAt+.6f;
                    var recover=a.boss?.5f:.36f;
                    float windK=0,lungeK=0,s=1;
                    if(!acted)windK=w;
                    else if(now<contact){s=Mathf.Clamp01((now-a.actAt)/Mathf.Max(.08f,contact-a.actAt));var e=s*s*s;windK=1-e;lungeK=e;}
                    else if(waiting||now<recoverFrom+.05f)lungeK=1;
                    else{var k=Mathf.Clamp01((now-recoverFrom-.05f)/recover);lungeK=1-(1-(1-k)*(1-k)*(1-k));}
                    if(moving)
                    {
                        if(windK>0)EnemyWindPose(m.kind,windK,ref pose);
                        if(lungeK>0)EnemyLungePose(m.kind,reach,s,lungeK,ref pose);
                        // Each further hit of a multi-hit attack jabs forward again.
                        var jab=now-a.lastContactAt;if(a.contacts>1&&jab>=0&&jab<.3f){var j=Mathf.Exp(-jab*14);pose.offset.x-=10*j;pose.angle-=2*j;}
                        if((a.threat>=2||a.boss)&&windK>0){var tremble=(.8f+.7f*a.threat)*(a.boss?1.4f:1f)*windK;pose.offset.x+=Mathf.Sin(now*63+a.seed)*tremble;}
                    }
                    pose.aura=Mathf.Max(pose.aura,(windK+lungeK*.6f)*(.30f+.14f*a.threat)*(a.boss?1.3f:1f));
                }
                else if(a.castColor.a>0)
                {
                    // Non-attacks gather quietly, then the cast beat pulses.
                    var gather=acted?1-Mathf.Clamp01((now-a.actAt)/.3f):w;
                    if(moving)EnemyWindPose(EnemyMotionKind.Caster,gather*.5f,ref pose);
                    pose.aura=Mathf.Max(pose.aura,gather*.28f);pose.auraColor=a.castColor;
                }
            }
            var cast=now-a.castAt;
            if(!dead&&cast>=0&&cast<.5f)
            {
                var b=Mathf.Sin(cast/.5f*Mathf.PI);
                if(moving){pose.offset.y-=9*b;pose.sx+=.04f*b;pose.sy+=.05f*b;}
                if(.7f*b>pose.aura){pose.aura=.7f*b;pose.auraColor=a.castColor;}
            }
            // Hit reaction: an instant knock-back held through the hit-stop, then a
            // damped spring. Heavier bodies recoil less.
            var h=now-a.hitAt;
            if(h>=0&&h<.7f)
            {
                var hp=a.hitPower;var hold=hp>=1.2f?.1f:hp>=.95f?.075f:0;
                var he=Mathf.Max(0,h-hold);var d=Mathf.Exp(-he*10f)*Mathf.Cos(he*19f);
                if(moving)
                {
                    var mass=Mathf.Max(.5f,m.mass);var kick=(6+24*Mathf.Min(1.5f,hp))/mass*(group?.75f:1f)*(dead?.6f:1f);
                    pose.offset.x+=kick*d;pose.angle+=(2+6*hp)/mass*d;pose.sx-=.045f*hp*d;pose.sy+=.025f*hp*d;
                }
                var f=h<hold+.03f?1:Mathf.Clamp01(1-(h-hold-.03f)/.12f);
                pose.flash=Mathf.Max(pose.flash,f*(.5f+.3f*Mathf.Min(1,hp)));
            }
            var st=now-a.statusAt;
            if(moving&&!dead&&st>=0&&st<.3f)pose.offset.x+=4*Mathf.Exp(-st*12)*Mathf.Cos(st*24);
            // Boss phase change: the boss rears up, trembles and flares.
            var roar=now-a.roarAt;
            if(!dead&&roar>=0&&roar<1.1f)
            {
                var k=roar/1.1f;var b=Mathf.Sin(Mathf.Min(1,k*1.6f)*Mathf.PI);
                if(moving){pose.sx+=.06f*b;pose.sy+=.07f*b;pose.offset.x+=Mathf.Sin(now*71)*3*(1-k);pose.angle+=Mathf.Sin(now*23)*1.2f*(1-k);}
                if(1-k>pose.aura){pose.aura=1-k;pose.auraColor=m.accent;}
                if(roar<.14f)pose.flash=Mathf.Max(pose.flash,.8f*(1-roar/.14f));
            }
            if(dead)
            {
                var dd=Mathf.Clamp01((now-deathAt)/EnemyDeathDuration(a,group));float e;
                if(a.boss)
                {
                    if(moving&&dd<.45f){var tremble=1-dd/.45f*.3f;pose.offset.x+=Mathf.Sin(now*67)*4*tremble;pose.offset.y+=Mathf.Cos(now*53)*1.5f*tremble;}
                    pose.flash=Mathf.Max(pose.flash,Mathf.Min(1,EnemyFlashPulse(dd,.03f)+EnemyFlashPulse(dd,.17f)*.8f+EnemyFlashPulse(dd,.31f)*.9f));
                    // Keep the body readable under the flare while it trembles.
                    var flare=.6f*Mathf.Clamp01(1-Mathf.Abs(dd-.3f)/.3f);if(flare>pose.aura){pose.aura=flare;pose.auraColor=m.accent;}
                    e=Mathf.SmoothStep(0,1,Mathf.Clamp01((dd-.42f)/.58f));
                }
                else{e=Mathf.SmoothStep(0,1,dd);pose.flash=Mathf.Max(pose.flash,dd<.12f?1-dd/.12f:0);}
                if(moving)switch(m.kind)
                {
                    case EnemyMotionKind.Beast:case EnemyMotionKind.Crawler:pose.angle+=14*e;pose.sy-=.30f*e;pose.offset.x+=16*e;pose.offset.y+=6*e;break;
                    case EnemyMotionKind.Caster:case EnemyMotionKind.Spirit:
                        pose.offset.y-=28*e;pose.sx-=.12f*e;pose.sy-=.12f*e;if(.7f*(1-e)>pose.aura){pose.aura=.7f*(1-e);pose.auraColor=m.accent;}break;
                    default:pose.angle+=16*e*e;pose.offset.y+=14*e;pose.offset.x+=10*e;pose.sy-=.08f*e;break;
                }
                pose.alpha=moving?1-Mathf.SmoothStep(a.boss?.55f:.3f,1f,dd):1-dd;
            }
            return pose;
        }

        // ---------- drawing ----------
        // Replaces the plain DrawFloatingEnemy call for single and group fights.
        private void DrawAnimatedEnemy(int index,Rect destination,EnemyDef def,Color tint,float deathAt)
        {
            if(def==null)return;
            var texture=index>=0&&index<finalEnemyTextures.Length&&finalEnemyDefs[index]==def?finalEnemyTextures[index]:null;
            if(!texture)texture=LoadAuthoredArt(GildedArtCatalog.EnemyResource(def.id));
            if(!texture)return;
            var a=EnemyAnimFor(index,def);var now=Time.unscaledTime;var group=GroupCombat;
            var pose=EnemyPoseAt(a,now,deathAt,group);
            var fit=Mathf.Min(destination.width/texture.width,destination.height/texture.height);
            var width=texture.width*fit;var height=texture.height*fit;
            var fitted=new Rect(destination.center.x-width*.5f,destination.yMax-height,width,height);
            var pivot=new Vector2(fitted.center.x,fitted.yMax);
            a.shownFeet=pivot+pose.offset;a.shownCenter=a.shownFeet-new Vector2(0,height*.5f*pose.sy);
            var repaint=CardVfxRepaint;
            if(repaint)DrawEnemyContactShadow(fitted,pose,a);
            var matrix=GUI.matrix;var old=GUI.color;
            if(pose.offset.sqrMagnitude>.0001f||Mathf.Abs(pose.angle)>.001f||Mathf.Abs(pose.sx-1)>.0001f||Mathf.Abs(pose.sy-1)>.0001f)
                GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(pivot.x+pose.offset.x,pivot.y+pose.offset.y,0),Quaternion.Euler(0,0,pose.angle),new Vector3(pose.sx,pose.sy,1))*Matrix4x4.Translate(new Vector3(-pivot.x,-pivot.y,0));
            var set=repaint?EnemySilhouette(texture,false):null;
            var aura=pose.aura*(profile.reducedVfx?.5f:1f)*pose.alpha;
            if(set!=null&&set.glow&&aura>.01f)
            {
                var padX=width*set.padX;var padY=height*set.padY;var breathe=profile.reduceMotion?1:1+.015f*Mathf.Sin(now*9);
                var glow=new Rect(fitted.x-padX*breathe,fitted.y-padY*breathe,width+padX*2*breathe,height+padY*2*breathe);
                GUI.color=new Color(pose.auraColor.r,pose.auraColor.g,pose.auraColor.b,Mathf.Clamp01(aura*.85f));
                GUI.DrawTexture(glow,set.glow,ScaleMode.StretchToFill,true);
            }
            GUI.color=new Color(tint.r,tint.g,tint.b,tint.a*pose.alpha);
            GUI.DrawTexture(fitted,texture,ScaleMode.StretchToFill,true);
            if(set!=null&&set.solid&&pose.flash>.01f)
            {
                var alpha=pose.flash*pose.alpha*(profile.reduceFlashing?.2f:.75f);
                GUI.color=profile.reduceFlashing?new Color(1f,.74f,.56f,alpha):new Color(1f,.97f,.9f,alpha);
                GUI.DrawTexture(fitted,set.solid,ScaleMode.StretchToFill,true);
            }
            GUI.color=old;GUI.matrix=matrix;
        }
        // Soft contact shadow that tightens when an enemy leaps or hovers.
        private void DrawEnemyContactShadow(Rect fitted,EnemyPose pose,EnemyAnim a)
        {
            EnsureCardVfxTextures();
            var lift=Mathf.Max(0,-pose.offset.y);var spread=a.motion.kind is EnemyMotionKind.Beast or EnemyMotionKind.Crawler?.86f:.62f;
            var w=fitted.width*spread*(1-Mathf.Min(.35f,lift/90f));var h=Mathf.Clamp(fitted.height*.07f,12,24);
            var alpha=.38f*pose.alpha*(1-Mathf.Min(.5f,lift/70f))*(a.motion.kind==EnemyMotionKind.Spirit?.55f:1f);
            var x=fitted.center.x+pose.offset.x;
            DrawCardUiShape(new Rect(x-w*.5f,fitted.yMax-h*.55f,w,h),cardVfxGlow,new Color(0,0,0,alpha));
        }

        // ---------- cached silhouettes ----------
        // The art is not CPU-readable, so each enemy texture is blitted once into a
        // small RenderTexture and read back: a white silhouette for the hit flash and
        // a blurred, padded one for the charge aura. Built outside OnGUI, cached per
        // texture for the session (HideAndDontSave).
        private EnemySilhouetteSet EnemySilhouette(Texture2D source,bool bake)
        {
            if(!source)return null;
            if(enemySilhouettes.TryGetValue(source,out var set))return set;
            if(!bake)return null;
            set=new EnemySilhouetteSet();enemySilhouettes[source]=set;
            try{BakeEnemySilhouette(source,set);}
            catch(System.Exception e){Debug.LogWarning("[Gilded Fate VFX] Enemy silhouette unavailable for "+source.name+": "+e.Message);}
            return set;
        }
        private static void BakeEnemySilhouette(Texture2D source,EnemySilhouetteSet set)
        {
            const int solidSide=384,glowSide=112,pad=14;
            var aspect=source.width/(float)source.height;
            var w=aspect>=1?solidSide:Mathf.Max(8,Mathf.RoundToInt(solidSide*aspect));
            var h=aspect>=1?Mathf.Max(8,Mathf.RoundToInt(solidSide/aspect)):solidSide;
            var previous=RenderTexture.active;
            var half=RenderTexture.GetTemporary(w*2,h*2,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var target=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Color32[] pixels;
            try
            {
                half.filterMode=FilterMode.Bilinear;target.filterMode=FilterMode.Bilinear;
                Graphics.Blit(source,half);Graphics.Blit(half,target);
                RenderTexture.active=target;
                var read=new Texture2D(w,h,TextureFormat.RGBA32,false,true);
                read.ReadPixels(new Rect(0,0,w,h),0,0,false);read.Apply(false,false);
                pixels=read.GetPixels32();Object.Destroy(read);
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(half);RenderTexture.ReleaseTemporary(target);}
            var solid=new Color32[pixels.Length];
            for(var i=0;i<pixels.Length;i++)solid[i]=new Color32(255,255,255,pixels[i].a);
            set.solid=EnemySilhouetteTexture("Enemy hit flash",w,h,solid);
            var scale=glowSide/(float)Mathf.Max(w,h);var gw=Mathf.Max(4,Mathf.RoundToInt(w*scale));var gh=Mathf.Max(4,Mathf.RoundToInt(h*scale));
            var W=gw+pad*2;var H=gh+pad*2;var alpha=new float[W*H];
            for(var y=0;y<gh;y++)for(var x=0;x<gw;x++)
            {
                int x0=x*w/gw,x1=Mathf.Max(x0+1,(x+1)*w/gw),y0=y*h/gh,y1=Mathf.Max(y0+1,(y+1)*h/gh);float sum=0;var n=0;
                for(var sy=y0;sy<y1;sy++)for(var sx=x0;sx<x1;sx++){sum+=pixels[sy*w+sx].a;n++;}
                alpha[(y+pad)*W+x+pad]=sum/(n*255f);
            }
            EnemyGlowBlur(alpha,W,H,4);EnemyGlowBlur(alpha,W,H,4);EnemyGlowBlur(alpha,W,H,3);
            var max=.0001f;foreach(var v in alpha)if(v>max)max=v;
            var glow=new Color32[alpha.Length];
            for(var i=0;i<alpha.Length;i++){var v=Mathf.Clamp01(alpha[i]/max*1.5f);v=v*v*(3-2*v);glow[i]=new Color32(255,255,255,(byte)(v*255));}
            set.glow=EnemySilhouetteTexture("Enemy charge aura",W,H,glow);set.padX=pad/(float)gw;set.padY=pad/(float)gh;
        }
        private static Texture2D EnemySilhouetteTexture(string name,int w,int h,Color32[] pixels)
        {
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name=name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
        }
        private static void EnemyGlowBlur(float[] a,int w,int h,int r)
        {
            var tmp=new float[a.Length];var n=2*r+1f;
            for(var y=0;y<h;y++)
            {
                var row=y*w;float sum=0;for(var x=-r;x<=r;x++)sum+=a[row+Mathf.Clamp(x,0,w-1)];
                for(var x=0;x<w;x++){tmp[row+x]=sum/n;sum+=a[row+Mathf.Min(w-1,x+r+1)]-a[row+Mathf.Max(0,x-r)];}
            }
            for(var x=0;x<w;x++)
            {
                float sum=0;for(var y=-r;y<=r;y++)sum+=tmp[Mathf.Clamp(y,0,h-1)*w+x];
                for(var y=0;y<h;y++){a[y*w+x]=sum/n;sum+=tmp[Mathf.Min(h-1,y+r+1)*w+x]-tmp[Mathf.Max(0,y-r)*w+x];}
            }
        }
    }
}
