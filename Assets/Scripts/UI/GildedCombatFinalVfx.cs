using System.Linq;
using System.Collections.Generic;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    // Final combat VFX pass (presentation only). Adds, on top of the existing
    // card/gilding/vitals/boss presentation:
    //   * enemy attacks that match the attacker (claws, bites, blades, cleaves,
    //     slams, bolts, mirror shards, thrown cards, spectral blades, crushes);
    //   * hero-specific hit identity when enemies take damage (Vanguard steel and
    //     sparks, Hexer violet glyphs, Reaper dark soul-scythe);
    //   * damage-tiered scale, shake and hit-stop; gilded and Rare accents;
    //   * short status apply feedback, enemy cast beats, deaths (bosses larger).
    // Everything is drawn in the actor layer, before health bars, intents, status
    // strips and cards, so it can never cover UI. Bursts are value structs in one
    // pre-sized list; textures are generated once (HideAndDontSave).
    public sealed partial class GildedMainMenu
    {
        private enum FinalVfxKind : byte { HeroStrike, EnemyStrike, Deflect, Projectile, Cast, Death, Status, Steal, Summon, Banish, Shockwave }
        private enum FinalHeroStyle : byte { Vanguard, Hexer, Reaper, Burn, Neutral }
        private enum FinalCastStyle : byte { Ward, Bloom }
        private enum FinalProjectile : byte { Hex=200, Curse=201 }
        private enum FinalStatusStyle : byte { Burn, Strength, Ward, Weak, Vulnerable, Marked, Soul, Retaliate, Generic }
        private struct FinalVfxBurst
        {
            public FinalVfxKind kind;
            public byte style;
            public Vector2 a,b;
            public float start,duration,power,angle;
            public Color color;
            public int seed;
            public bool accent;
        }
        private readonly List<FinalVfxBurst> finalVfx=new(128);
        private readonly Dictionary<CombatEvent,int> finalFactAttacker=new();
        private readonly EnemyDef[] finalEnemyDefs=new EnemyDef[6];
        private readonly Texture2D[] finalEnemyTextures=new Texture2D[6];
        private readonly bool[] finalDeathSpawned=new bool[6];
        private CombatState finalVfxCombat;
        private int finalAttacker=-1,finalSeenBossPhase=1,finalGildedCard=-1,finalSerial;
        private float finalVignetteAt=-10,finalVignettePower;
        private Texture2D finalVfxCrescent,finalVfxStreak,finalVfxSmoke;

        private bool FinalParticles=>profile!=null&&!profile.reduceMotion;
        private float FinalScale=>profile!=null&&profile.reducedVfx?.85f:1f;
        private int FinalCount(int n)=>profile!=null&&profile.reducedVfx?Mathf.Max(1,Mathf.RoundToInt(n*.4f)):n;
        private float FinalFlash(float alpha)=>profile!=null&&profile.reduceFlashing?alpha*.3f:alpha;
        private static float FinalEnvelope(float k,float attack=.08f)=>k<attack?k/attack:1-Mathf.SmoothStep(attack,1,k);

        // ---------- lifecycle ----------
        private void ResetFinalCombatVfx()
        {
            finalVfxCombat=combat;finalVfx.Clear();finalFactAttacker.Clear();finalAttacker=-1;finalGildedCard=-1;
            finalSeenBossPhase=lastBossPhase;finalVignetteAt=-10;
            for(var i=0;i<finalEnemyDefs.Length;i++){finalEnemyDefs[i]=null;finalEnemyTextures[i]=null;finalDeathSpawned[i]=false;}
            ResetEnemyAnimation();
            if(combat==null)return;
            for(var i=0;i<combat.EnemyCount&&i<finalEnemyDefs.Length;i++)
            {
                var id=combat.EnemyIdAt(i);EnemyDef def=null;
                foreach(var enemy in WorldContent.Enemies)if(enemy.id==id){def=enemy;break;}
                finalEnemyDefs[i]=def;finalEnemyTextures[i]=def==null?null:LoadAuthoredArt(GildedArtCatalog.EnemyResource(EnemyArtId(def.id,i)));
                finalDeathSpawned[i]=combat.EnemyAt(i).hp<=0; // restored checkpoints never replay old deaths
                EnemyAnimFor(i,def);
            }
        }
        // A summoned creature took this place: refresh its art, animation and death state.
        private void OnEnemySlotChanged(int i)
        {
            if(combat==null||i<0||i>=finalEnemyDefs.Length)return;
            var id=combat.EnemyIdAt(i);var def=WorldContent.Enemies.FirstOrDefault(e=>e.id==id);
            finalEnemyDefs[i]=def;finalEnemyTextures[i]=def==null?null:LoadAuthoredArt(GildedArtCatalog.EnemyResource(EnemyArtId(def.id,i)));
            finalDeathSpawned[i]=combat.EnemyAt(i).hp<=0;enemyAnims[i].Clear();EnemyAnimFor(i,def);
        }
        // End of UpdateCombatPresentation (outside OnGUI).
        private void UpdateFinalCombatVfx(float now)
        {
            if(combat==null)return;
            if(finalVfxCombat!=combat)ResetFinalCombatVfx();
            for(var i=0;i<finalEnemyTextures.Length;i++)if(finalEnemyTextures[i])EnemySilhouette(finalEnemyTextures[i],true);
            if(!GroupCombat)
            {
                if(foeDeath>0&&!finalDeathSpawned[0]){finalDeathSpawned[0]=true;SpawnFinalDeath(0,EnemyPortraitRect,finalEnemyDefs[0]??currentEnemy,now);}
            }
            else for(var i=0;i<opponentVisuals.Count&&i<finalDeathSpawned.Length;i++)
                if(opponentVisuals[i].death>=0&&!finalDeathSpawned[i]){finalDeathSpawned[i]=true;SpawnFinalDeath(i,GroupPortrait(i),finalEnemyDefs[i],opponentVisuals[i].death);}
            if(currentEnemy!=null&&currentEnemy.boss&&lastBossPhase>finalSeenBossPhase)
            {
                finalSeenBossPhase=lastBossPhase;var a=EnemyAnimFor(0,finalEnemyDefs[0]??currentEnemy);a.roarAt=now;
                var body=EnemyPortraitRect;
                QueueFinalVfx(FinalVfxKind.Shockwave,0,new Vector2(body.center.x,body.yMax-8),body.center,now,.9f,1.2f,a.motion.accent,lastBossPhase*97);
            }
            for(var i=finalVfx.Count-1;i>=0;i--)if(now>finalVfx[i].start+finalVfx[i].duration)finalVfx.RemoveAt(i);
        }
        private void QueueFinalVfx(FinalVfxKind kind,byte style,Vector2 a,Vector2 b,float start,float duration,float power,Color color,int seed,float angle=0,bool accent=false)
        {
            if(finalVfx.Count>=120)finalVfx.RemoveAt(0);
            finalVfx.Add(new FinalVfxBurst{kind=kind,style=style,a=a,b=b,start=start,duration=Mathf.Max(.05f,duration),power=power,color=color,seed=seed,angle=angle,accent=accent});
        }
        // AnimateCardPlay, before the play resolves: remember which receipts are gilded.
        private void FinalVfxNoteCardPlay(CardDef card,bool gilded){finalGildedCard=gilded&&card!=null?card.instanceId:-1;}
        private float FinalDeathHold()
        {
            if(profile.reduceMotion||combat==null||combat.player.hp<=0)return profile.reduceMotion?.25f:.85f;
            return currentEnemy?.boss==true?1.7f:currentEnemy?.elite==true?1.1f:.85f;
        }

        // Boss phase banner: says what just changed instead of a generic line.
        private string BossPhaseSubtitle()=>currentEnemy?.id switch
        {
            "hollow_king"=>lastBossPhase>=3?"THE FULL ARMORY RISES · THE KING GROWS STRONGER":"THE ARMORY AWAKENS · MORE SPECTRAL BLADES",
            "vault_mother"=>lastBossPhase>=3?"THE VAULTBLOOM HARDENS · SHE GROWS STRONGER":"THE VAULTBLOOM HARDENS · HEAVY BLOCK",
            "last_dealer"=>lastBossPhase>=3?"THE BLACK HAND · CURSES DEALT · ENERGY TAXED":"THE DECK IS STACKED · CURSES DEALT",
            AshenWildsContent.Alpha=>lastBossPhase>=3?"ITS HIDE TEARS AWAY · THE PACK FLEES · A PURE DIRECT FIGHT":"THE ALPHA IGNITES · +1 STRENGTH",
            CrimsonFoundryContent.Saint=>lastBossPhase>=3?"ITS ARMOR TEARS AWAY · OVERHEATED · HEAT 4":"THE CHAINS SNAP · THE SERVITOR FALLS · HEAT 1",
            HollowwoodContent.Heartroot=>lastBossPhase>=3?"THE PREDATOR HEART · GROWTH RESETS TO 0":"THE HEART SPREADS · GROWTH RESETS TO 1",
            ShatteredObservatoryContent.Curator=>lastBossPhase>=3?"THE CONSTELLATION FORMS · TWIN FATE":"THE ARCHIVE FRACTURES · NEW FUTURES",
            GildedRuinsContent.Procession=>lastBossPhase>=3?"THE CROWN ENGINE · THE PROCESSION SEIZES MORE":"THE PARADE BREAKS · THE RAM ADVANCES",
            DrownedQuarterContent.Magistrate=>lastBossPhase>=3?"IT TEARS FREE OF THE COURT · THE BAILIFF SINKS":"THE COURT FLOODS · THE MAGISTRATE PULLS PARTLY FREE",
            _=>"THE VAULT REWRITES ITS COMMAND"
        };

        // ---------- geometry ----------
        private Rect FinalEnemyRect(int index)=>GroupCombat?GroupPortrait(Mathf.Clamp(index,0,combat.EnemyCount-1)):EnemyPortraitRect;
        private Vector2 FinalBodyPoint(Rect body,int seed)=>body.center+new Vector2((CardVfxHash(seed,3)-.5f)*body.width*.28f,(CardVfxHash(seed,5)-.62f)*body.height*.26f);
        private Vector2 FinalHeroPoint(int seed){var r=HeroPortraitRect;return r.center+new Vector2((CardVfxHash(seed,11)-.5f)*28,(CardVfxHash(seed,13)-.7f)*40);}
        private static Vector2 FinalEmitPoint(Rect body)=>new Vector2(body.x+body.width*.24f,body.y+body.height*.40f);
        private static Vector2 FinalSpectralOrigin(Rect body,int n)=>new Vector2(body.center.x+(n%3-1)*body.width*.32f,body.y+body.height*.12f-(n%2)*18);

        // ---------- scheduling (ConsumeCombatEvents) ----------
        private void BeginFinalVfxBatch(){finalAttacker=-1;}
        private void ScheduleFinalVfxFact(CombatEvent fact,float at)
        {
            if(fact==null||combat==null)return;
            if(finalVfxCombat!=combat)ResetFinalCombatVfx();
            if(fact.kind==CombatEventKind.EnemyAction)
            {
                finalAttacker=Mathf.Clamp(GroupCombat?fact.enemyIndex:0,0,enemyAnims.Length-1);
                var enemy=EnemyAnimFor(finalAttacker,FinalEnemyDef(finalAttacker));
                if(at-enemy.turnAt>6)enemy.turnAt=at-.2f;
                enemy.actAt=at;enemy.contactAt=-10;enemy.lastPredictedContactAt=at+.25f;enemy.predictedContacts=enemy.contacts=enemy.lastPredictedHit=enemy.lastShownHit=0;
                var body=FinalEnemyRect(finalAttacker);var seed=finalAttacker*17+combat.turn*31;
                if(enemy.stealsGold)QueueFinalVfx(FinalVfxKind.Steal,0,FinalHeroPoint(seed),FinalEmitPoint(body),at+.1f,.6f,1,Gold,seed);
                if(enemy.summons){enemy.castAt=at+.2f;enemy.castColor=EnemyCastColor(EnemyActionType.SummonWeapon);QueueFinalVfx(FinalVfxKind.Summon,0,body.center,body.center,at+.18f,.85f,1,enemy.motion.accent,seed);}
                if(enemy.curses&&!profile.reduceMotion)QueueFinalVfx(FinalVfxKind.Projectile,(byte)FinalProjectile.Curse,FinalEmitPoint(body),PilePoint(1),at+.08f,.42f,1,new Color(.74f,.40f,1f),seed+3);
                if(enemy.banishes)QueueFinalVfx(FinalVfxKind.Banish,0,PilePoint(1),PilePoint(1),at+.35f,.6f,1,new Color(.64f,.32f,.92f),seed+5);
                return;
            }
            if(finalAttacker<0||!fact.playerSide)return;
            var a=enemyAnims[finalAttacker];
            if(fact.kind==CombatEventKind.Damage||fact.kind==CombatEventKind.Block&&fact.label=="BLOCKED")
            {
                finalFactAttacker[fact]=finalAttacker;
                if(fact.hitId>0&&fact.hitId==a.lastPredictedHit)return;
                a.lastPredictedHit=fact.hitId;
                if(a.predictedContacts==0)a.contactAt=at;
                a.lastPredictedContactAt=at;a.predictedContacts++;
                if(!profile.reduceMotion&&EnemyStrikeIsRanged(a.motion.strike))
                {
                    var spectral=a.motion.strike==EnemyStrikeStyle.Spectral;var travel=spectral?.3f:.22f;var body=FinalEnemyRect(finalAttacker);
                    var from=spectral?FinalSpectralOrigin(body,a.predictedContacts):FinalEmitPoint(body);
                    QueueFinalVfx(FinalVfxKind.Projectile,(byte)a.motion.strike,from,FinalHeroPoint(fact.hitId*17+finalAttacker*5),at-travel,travel,1,a.motion.accent,fact.hitId*13+finalAttacker);
                }
                return;
            }
            if(fact.kind==CombatEventKind.Status&&fact.amount>0&&(fact.label=="WEAK"||fact.label=="VULNERABLE"||fact.label=="BURN"))
            {
                const float travel=.26f;var color=FinalStatusColor(fact.label);
                a.castAt=at-travel;a.castColor=color;
                if(!profile.reduceMotion)QueueFinalVfx(FinalVfxKind.Projectile,(byte)FinalProjectile.Hex,FinalEmitPoint(FinalEnemyRect(finalAttacker)),FinalHeroPoint(fact.amount*7+finalAttacker),at-travel,travel,1,color,finalSerial++);
            }
        }

        // ---------- presentation beats ----------
        private static float FinalHitPower(int amount)=>amount<10?.2f:amount<25?.45f:amount<50?.7f:amount<100?1f:amount<150?1.25f:1.5f;
        private static float FinalEnemyHitPower(int amount,EnemyAnim a)=>(amount<8?.25f:amount<15?.45f:amount<25?.65f:amount<40?.85f:1.1f)*(a.boss?1.15f:a.elite?1.05f:1f);
        private FinalHeroStyle FinalHeroStyleFor(CombatEvent fact,CardDef card)
        {
            if(fact.label=="BURN")return FinalHeroStyle.Burn;
            if(card==null&&(fact.label=="THORNS"||fact.label=="OVERFLOW"))return FinalHeroStyle.Neutral;
            if(fact.label=="RUIN SIGIL")return FinalHeroStyle.Hexer;
            var hero=card?.hero??run.hero;
            return hero==HeroId.Hexer?FinalHeroStyle.Hexer:hero==HeroId.Reaper?FinalHeroStyle.Reaper:FinalHeroStyle.Vanguard;
        }
        // Painted atlas splash under the procedural layer; Reaper stays procedural.
        private static int FinalHeroAtlas(FinalHeroStyle style)=>style switch{FinalHeroStyle.Vanguard=>0,FinalHeroStyle.Hexer=>3,FinalHeroStyle.Burn=>2,FinalHeroStyle.Reaper=>-1,_=>7};
        private static int FinalStrikeAtlas(EnemyAnim a)=>a.motion.strike switch
        {
            EnemyStrikeStyle.Blade or EnemyStrikeStyle.Cleave or EnemyStrikeStyle.Spectral=>0,
            EnemyStrikeStyle.Bolt=>a.id=="golden_wisp"?4:3,EnemyStrikeStyle.Cards=>6,EnemyStrikeStyle.Shards=>-1,
            EnemyStrikeStyle.Slam or EnemyStrikeStyle.Crush=>-1,_=>7
        };
        private static Color FinalHeroColor(FinalHeroStyle style)=>style switch
        {
            FinalHeroStyle.Vanguard=>new Color(1f,.80f,.40f),FinalHeroStyle.Hexer=>new Color(.76f,.50f,1f),
            FinalHeroStyle.Reaper=>new Color(.30f,.92f,.82f),FinalHeroStyle.Burn=>new Color(1f,.52f,.18f),_=>new Color(1f,.70f,.45f)
        };
        private static Color FinalStatusColor(string label)=>label switch
        {
            "BURN"=>new Color(1f,.52f,.18f),"STRENGTH"=>new Color(1f,.42f,.22f),"FORTIFY"=>new Color(.45f,.78f,1f),
            "WEAK"=>new Color(.64f,.86f,.44f),"VULNERABLE"=>new Color(1f,.32f,.30f),"MARKED"=>new Color(.84f,.52f,1f),
            "SOUL" or "SOUL REPLAY" or "SOUL CONVERSION" or "SOUL DAMAGE"=>new Color(.34f,.94f,.84f),"RETALIATE"=>new Color(1f,.80f,.40f),
            _=>new Color(.86f,.72f,1f)
        };

        // UpdateVitalsPlayback, as each health receipt is shown.
        private void FinalVfxOnVital(CombatEvent fact,CardDef source,float now)
        {
            if(fact==null||combat==null)return;
            if(finalVfxCombat!=combat)ResetFinalCombatVfx();
            var damage=fact.kind==CombatEventKind.Damage;var blocked=fact.kind==CombatEventKind.Block&&fact.label=="BLOCKED";
            if(!fact.playerSide)
            {
                var index=GroupCombat?Mathf.Clamp(fact.enemyIndex,0,enemyAnims.Length-1):0;
                var body=FinalEnemyRect(index);var a=EnemyAnimFor(index,FinalEnemyDef(index));
                if(damage&&fact.amount>0)
                {
                    PlaygroundTrackDamage(fact.amount);
                    var card=fact.card??source;var style=FinalHeroStyleFor(fact,card);
                    var power=FinalHitPower(fact.amount)+(card!=null&&card.rarity==Rarity.Rare?.1f:0)+(fact.enemyHp<=0?.25f:0);
                    a.hitAt=now;a.hitPower=power;a.hitAtlas=FinalHeroAtlas(style);
                    if(!GroupCombat){if(a.hitAtlas>=0)enemyVfxIndex=a.hitAtlas;else enemyVfxTime=0;}
                    var seed=fact.hitId*31+index*7+(finalSerial++)*13;
                    var angle=style==FinalHeroStyle.Vanguard?((fact.hitId&1)==0?-32f:28f)+(CardVfxHash(seed,1)-.5f)*16:style==FinalHeroStyle.Reaper?-12+(CardVfxHash(seed,1)-.5f)*24:0;
                    var gilded=card!=null&&card.instanceId==finalGildedCard;
                    var duration=style==FinalHeroStyle.Reaper?.8f:style==FinalHeroStyle.Hexer?.7f:.6f;
                    QueueFinalVfx(FinalVfxKind.HeroStrike,(byte)style,FinalBodyPoint(body,seed),body.center,now,duration+.1f*Mathf.Min(1.5f,power),power,FinalHeroColor(style),seed,angle,gilded);
                }
                else if(fact.kind==CombatEventKind.Block&&!blocked&&fact.amount>0)
                {a.castAt=now;a.castColor=EnemyCastColor(EnemyActionType.Block);QueueFinalVfx(FinalVfxKind.Cast,(byte)FinalCastStyle.Ward,body.center,body.center,now,.6f,1,a.castColor,finalSerial++);}
                else if(fact.kind==CombatEventKind.Heal&&fact.amount>0)
                {a.castAt=now;a.castColor=EnemyCastColor(EnemyActionType.Heal);QueueFinalVfx(FinalVfxKind.Cast,(byte)FinalCastStyle.Bloom,new Vector2(body.center.x,body.yMax),body.center,now,.8f,1,a.castColor,finalSerial++);}
                return;
            }
            if(!damage&&!blocked)return;
            if(!finalFactAttacker.TryGetValue(fact,out var attacker))return; // self-inflicted costs keep the existing hit only
            finalFactAttacker.Remove(fact);
            var enemy=enemyAnims[attacker];
            if(fact.hitId<=0||fact.hitId!=enemy.lastShownHit){enemy.lastContactAt=now;enemy.contacts++;enemy.lastShownHit=fact.hitId;}
            var hitSeed=fact.hitId*17+attacker*5;var point=FinalHeroPoint(hitSeed);var strength=FinalEnemyHitPower(fact.amount,enemy);
            if(blocked){QueueFinalVfx(FinalVfxKind.Deflect,0,point,FinalEnemyRect(attacker).center,now,.45f,strength,new Color(.52f,.82f,1f),hitSeed);return;}
            playerVfxIndex=FinalStrikeAtlas(enemy);
            QueueFinalVfx(FinalVfxKind.EnemyStrike,(byte)enemy.motion.strike,point,FinalEnemyRect(attacker).center,now,enemy.motion.strike is EnemyStrikeStyle.Cleave or EnemyStrikeStyle.Crush or EnemyStrikeStyle.Slam?.7f:.5f,strength,enemy.motion.accent,hitSeed,0,enemy.boss);
            if(enemy.boss&&(fact.amount>=15||enemy.threat>=2)){finalVignetteAt=now;finalVignettePower=Mathf.Clamp01(fact.amount/40f)*.6f+.4f;}
        }
        // Existing shake saturated at 23 damage; this keeps small hits quiet and lets
        // large, 100+ and boss hits read as clearly heavier.
        private float FinalImpactShake(CombatEvent fact)
        {
            var amount=fact.amount;if(amount<=0)return 0;
            var s=.1f+.9f*(1-Mathf.Exp(-amount/45f));if(amount>=100)s+=.25f;if(amount>=200)s+=.2f;
            if(fact.playerSide&&currentEnemy?.boss==true)s+=.1f;
            return profile.reducedVfx?s*.6f:s;
        }
        // UpdateMasterPolishPlayback, as each status receipt is shown.
        private void FinalVfxOnStatus(CombatEvent fact,float now)
        {
            if(fact==null||combat==null||string.IsNullOrEmpty(fact.label))return;
            if(finalVfxCombat!=combat)ResetFinalCombatVfx();
            var label=fact.label;var trigger=label.StartsWith("TRIGGER:");
            if(fact.amount<=0&&!trigger)return; // consumption is shown by the chip itself
            FinalStatusStyle style;
            switch(label)
            {
                case "BURN":style=FinalStatusStyle.Burn;break;
                case "STRENGTH":style=FinalStatusStyle.Strength;break;
                case "FORTIFY":case "INDOMITABLE · NEXT TURN":case "BRACE":style=FinalStatusStyle.Ward;break;
                case "WEAK":style=FinalStatusStyle.Weak;break;
                case "VULNERABLE":style=FinalStatusStyle.Vulnerable;break;
                case "MARKED":style=FinalStatusStyle.Marked;break;
                case "SOUL":case "SOUL REPLAY":case "SOUL CONVERSION":case "SOUL DAMAGE":style=FinalStatusStyle.Soul;break;
                case "RETALIATE":style=FinalStatusStyle.Retaliate;break;
                default:if(trigger||label.EndsWith(" SIGIL")||label=="POWER"||label=="ASPECT ACTIVE"||label=="OPENING STATUS"||label=="CURSE"||label=="STATUS")return;style=FinalStatusStyle.Generic;break;
            }
            Vector2 at;
            if(fact.playerSide)at=HeroPortraitRect.center+new Vector2(0,-10);
            else
            {
                var index=GroupCombat?Mathf.Clamp(fact.enemyIndex,0,enemyAnims.Length-1):0;var body=FinalEnemyRect(index);at=body.center;
                var a=EnemyAnimFor(index,FinalEnemyDef(index));
                if(style==FinalStatusStyle.Strength){a.castAt=now;a.castColor=EnemyCastColor(EnemyActionType.Strength);}
                else a.statusAt=now;
            }
            QueueFinalVfx(FinalVfxKind.Status,(byte)style,at,at,now,style==FinalStatusStyle.Soul?.7f:.55f,1,FinalStatusColor(label),finalSerial++*7+fact.amount);
        }
        private void SpawnFinalDeath(int index,Rect body,EnemyDef def,float at)
        {
            var a=EnemyAnimFor(index,def);var tier=a.boss?2:a.elite?1:0;var scale=AnimationSeconds(1f);
            // angle carries the animation-speed scale so the boss timeline stays in sync.
            QueueFinalVfx(FinalVfxKind.Death,(byte)tier,body.center,new Vector2(body.center.x,body.yMax),at,(tier==2?1.9f:tier==1?1.1f:.85f)*scale,1,a.motion.accent,index*41+(def?.id.Length??3),scale);
        }

        // ---------- drawing (end of DrawCombatActors: under every HUD element) ----------
        private void EnsureFinalVfxTextures()
        {
            EnsureCardVfxTextures();
            if(finalVfxCrescent&&finalVfxStreak&&finalVfxSmoke)return;
            // A "⌒" crescent: thick in the middle, tapering to sharp tips.
            finalVfxCrescent=CreateCardVfxTexture("Final VFX crescent",128,64,(x,y)=>
            {
                var u=(x+.5f)/128f;var v=1-(y+.5f)/64f;var arc=Mathf.Sin(u*Mathf.PI);
                var centre=.80f-.58f*arc;var thickness=.018f+.15f*Mathf.Pow(arc,1.25f);
                var d=(v-centre)/thickness;if(d<0)d*=1.7f;
                return new Color(1,1,1,Mathf.Clamp01(Mathf.Exp(-d*d*2.2f)*Mathf.Clamp01(arc*6)));
            });
            // Tapered streak with a hot core; runs along +x.
            finalVfxStreak=CreateCardVfxTexture("Final VFX streak",128,32,(x,y)=>
            {
                var u=(x+.5f)/128f;var v=(y+.5f)/32f-.5f;var taper=Mathf.Pow(Mathf.Sin(u*Mathf.PI),.7f);
                var d=Mathf.Abs(v)/Mathf.Max(.001f,.46f*taper);
                return new Color(1,1,1,Mathf.Clamp01(Mathf.Exp(-d*d*3.2f)*taper*1.15f));
            });
            // Soft, noisy puff for smoke and dust.
            finalVfxSmoke=CreateCardVfxTexture("Final VFX smoke",64,64,(x,y)=>
            {
                var d=new Vector2(x-31.5f,y-31.5f).magnitude/32f;
                var n=CardVfxNoise(x*.11f,y*.11f)*.6f+CardVfxNoise(x*.27f+9,y*.27f+3)*.4f;
                return new Color(1,1,1,Mathf.Clamp01(Mathf.Exp(-d*d*3f)*(.35f+n*1.1f)*Mathf.Clamp01((1-d)*3)));
            });
        }
        private void FinalSprite(Texture2D texture,Vector2 c,float width,float height,float degrees,Color color)
        {
            if(!texture||color.a<=.004f||width<=.5f||height<=.5f)return;
            var matrix=GUI.matrix;var old=GUI.color;
            if(Mathf.Abs(degrees)>.01f)GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(c.x,c.y,0),Quaternion.Euler(0,0,degrees),Vector3.one)*Matrix4x4.Translate(new Vector3(-c.x,-c.y,0));
            GUI.color=color;GUI.DrawTexture(new Rect(c.x-width*.5f,c.y-height*.5f,width,height),texture,ScaleMode.StretchToFill,true);
            GUI.color=old;GUI.matrix=matrix;
        }
        private void FinalGlow(Vector2 c,float size,Color color){if(color.a>.004f&&size>.5f)DrawCardUiShape(new Rect(c.x-size*.5f,c.y-size*.5f,size,size),cardVfxGlow,color);}
        private void FinalRing(Vector2 c,float size,Color color,float squash=1){if(color.a>.004f&&size>.5f)DrawCardUiShape(new Rect(c.x-size*.5f,c.y-size*squash*.5f,size,size*squash),cardVfxRing,color);}
        private static Vector2 FinalDir(float degrees){var r=degrees*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(r),Mathf.Sin(r));}
        private static Color FinalA(Color c,float a)=>new Color(c.r,c.g,c.b,Mathf.Clamp01(a));
        // Streak sparks with drag and gravity. Direction 0 is +x, positive is downward.
        private void FinalSparks(Vector2 origin,int count,int seed,float age,float life,float direction,float spread,float speedMin,float speedMax,float gravity,Color color,float length,float width)
        {
            if(!FinalParticles||age<0)return;
            for(var i=0;i<count;i++)
            {
                var r1=CardVfxHash(seed,i);var r2=CardVfxHash(seed,i+57);var t=age-CardVfxHash(seed,i+113)*.04f;if(t<0)continue;
                var k=t/(life*(.6f+.4f*r2));if(k>=1)continue;
                var velocity=FinalDir(direction+(r1-.5f)*spread)*Mathf.Lerp(speedMin,speedMax,r2);var drag=1/(1+t*3.2f);
                var p=origin+velocity*t*drag+new Vector2(0,.5f*gravity*t*t);var now=velocity*drag*drag+new Vector2(0,gravity*t);
                var dir=now.sqrMagnitude>1?now.normalized:Vector2.right;var len=length*(1-k*.5f)*(.6f+.4f*r1);
                DrawLine(p-dir*len,p,FinalA(color,color.a*(1-k)*(1-k)),width*(1-k*.4f));
            }
        }
        // Soft motes (dust, souls, arcane glints) with drift and fade.
        private void FinalMotes(Vector2 origin,int count,int seed,float age,float life,float direction,float spread,float speedMin,float speedMax,float lift,Color color,float size)
        {
            if(age<0)return;
            var moving=FinalParticles;
            for(var i=0;i<count;i++)
            {
                var r1=CardVfxHash(seed,i+7);var r2=CardVfxHash(seed,i+71);var t=age-CardVfxHash(seed,i+131)*.06f;if(t<0)continue;
                var k=t/(life*(.65f+.35f*r2));if(k>=1)continue;
                var dir=FinalDir(direction+(r1-.5f)*spread);var travel=moving?Mathf.Lerp(speedMin,speedMax,r2)*t/(1+t*2.2f):Mathf.Lerp(speedMin,speedMax,r2)*.12f;
                var p=origin+dir*travel+new Vector2(0,moving?lift*t*t:0);var s=size*(.6f+.8f*r1)*(1-k*.4f);
                FinalGlow(p,s*2.4f,FinalA(color,color.a*.55f*(1-k)));FinalGlow(p,s,FinalA(Color.Lerp(color,Color.white,.55f),color.a*(1-k)));
            }
        }

        private void DrawFinalCombatVfx()
        {
            if(!CardVfxRepaint||combat==null)return;
            var now=Time.unscaledTime;
            if(finalVfx.Count==0&&now-finalVignetteAt>.5f)return;
            EnsureFinalVfxTextures();var old=GUI.color;
            DrawFinalVignette(now);
            for(var i=0;i<finalVfx.Count;i++)
            {
                var b=finalVfx[i];var age=now-b.start;if(age<0||age>=b.duration)continue;var k=age/b.duration;
                switch(b.kind)
                {
                    case FinalVfxKind.HeroStrike:DrawFinalHeroStrike(b,k,age);break;
                    case FinalVfxKind.EnemyStrike:DrawFinalEnemyStrike(b,k,age);break;
                    case FinalVfxKind.Deflect:DrawFinalDeflect(b,k,age);break;
                    case FinalVfxKind.Projectile:DrawFinalProjectile(b,k,age);break;
                    case FinalVfxKind.Cast:DrawFinalCast(b,k,age);break;
                    case FinalVfxKind.Death:DrawFinalDeath(b,k,age);break;
                    case FinalVfxKind.Status:DrawFinalStatus(b,k,age);break;
                    case FinalVfxKind.Steal:DrawFinalSteal(b,k,age);break;
                    case FinalVfxKind.Summon:DrawFinalSummon(b,k,age);break;
                    case FinalVfxKind.Banish:DrawFinalBanish(b,k,age);break;
                    case FinalVfxKind.Shockwave:DrawFinalShockwave(b,k,age);break;
                }
            }
            GUI.color=old;
        }

        // ---------- hero identity: enemy takes damage ----------
        private void DrawFinalHeroStrike(FinalVfxBurst b,float k,float age)
        {
            var p=b.a;var power=Mathf.Min(1.5f,b.power);var s=FinalScale*(.85f+.42f*power)*(GroupCombat?.78f:1f); // narrower lanes in group fights
            switch((FinalHeroStyle)b.style)
            {
                case FinalHeroStyle.Vanguard:
                {
                    // Steel: a crisp slash with a gold edge, a white-hot flare and a
                    // fan of sparks thrown away from the hero. Heavy hits cross it.
                    FinalGlow(p,110*s,new Color(1f,.93f,.76f,FinalFlash(.75f)*Mathf.Pow(1-k,3)));
                    void Slash(float angle,float t,float scale)
                    {
                        if(t<0)return;var reveal=profile.reduceMotion?1:Mathf.Clamp01(t/.08f);var fade=1-Mathf.SmoothStep(.18f,1f,k);
                        var length=(150+110*power)*s*scale;var dir=FinalDir(angle);var centre=p-dir*length*.5f+dir*length*reveal*.5f;
                        FinalSprite(finalVfxStreak,centre,length*reveal,30*s*scale,angle,new Color(1f,.72f,.28f,.55f*fade));
                        FinalSprite(finalVfxStreak,centre,length*reveal,10*s*scale,angle,new Color(.90f,.94f,1f,.95f*fade));
                        FinalSprite(finalVfxStreak,centre,length*reveal*.8f,3.5f*s*scale,angle,new Color(1f,1f,1f,FinalFlash(1f)*fade));
                    }
                    Slash(b.angle,age,1);
                    if(power>=.7f)Slash(b.angle+(b.angle<0?64:-64),age-.05f,.85f);
                    FinalSparks(p,FinalCount(8+(int)(power*14)),b.seed,age,.45f,-18,120,220,540,900,new Color(1f,.86f,.50f,1f),14*s,2.2f);
                    FinalSparks(p,FinalCount(3+(int)(power*4)),b.seed+9,age,.3f,-10,60,300,620,700,new Color(1f,1f,.92f,1f),10*s,1.6f);
                    if(power>=.45f){var rk=Mathf.Clamp01(age/.3f);FinalRing(p,Mathf.Lerp(40,(120+70*power)*s,1-(1-rk)*(1-rk)),new Color(1f,.80f,.40f,.7f*(1-rk)));}
                    if(power>=1f)FinalMotes(new Vector2(p.x,p.y+60*s),FinalCount(6),b.seed+21,age,.6f,-90,140,40,110,60,new Color(.72f,.62f,.48f,.5f),9*s);
                    break;
                }
                case FinalHeroStyle.Hexer:
                {
                    // Arcane: lines converge, a violet glyph snaps shut then blooms
                    // outward with rune ticks, a hexagram for heavy hits, and motes.
                    var R=(70+45*power)*s;var fade=1-Mathf.SmoothStep(.15f,1f,k);
                    var contract=profile.reduceMotion?1:age<.1f?Mathf.Lerp(1.35f,.75f,age/.1f):Mathf.Lerp(.75f,1.3f,Mathf.Clamp01((age-.1f)/.5f));
                    FinalGlow(p,R*2.4f,new Color(.45f,.18f,.80f,.42f*fade));
                    FinalGlow(p,R*.9f,new Color(.95f,.82f,1f,FinalFlash(.8f)*Mathf.Pow(1-k,3)));
                    FinalRing(p,R*2*contract,new Color(.78f,.52f,1f,.9f*fade));
                    FinalRing(p,R*1.45f*contract,new Color(1f,.82f,.45f,.55f*fade));
                    var rot=(profile.reduceMotion?0:age*2.2f)+b.seed*.37f;var tick=new Color(.88f,.72f,1f,.85f*fade);
                    for(var i=0;i<8;i++){var d=FinalDir((rot+i*Mathf.PI*.25f)*Mathf.Rad2Deg);DrawLine(p+d*R*contract*.78f,p+d*R*contract*.97f,tick,1.8f);}
                    if(power>=.45f)for(var tri=0;tri<2;tri++)for(var j=0;j<3;j++)
                    {
                        var a0=rot*.5f+tri*Mathf.PI/3+j*Mathf.PI*2/3;var a1=a0+Mathf.PI*2/3;var r=R*contract*.72f;
                        DrawLine(p+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*r,p+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*r,new Color(1f,.84f,.52f,.6f*fade),1.5f);
                    }
                    if(age<.14f&&FinalParticles){var c=age/.14f;for(var i=0;i<6;i++){var d=FinalDir(i*60+b.seed%60);DrawLine(p+d*R*(2.2f-1.8f*c),p+d*R*(1.5f-1.3f*c),new Color(.80f,.56f,1f,.8f*(1-c)),2);}}
                    FinalMotes(p,FinalCount(10+(int)(power*10)),b.seed,age-.08f,.55f,0,360,120,300,-80,new Color(.80f,.56f,1f,.9f),5*s);
                    break;
                }
                case FinalHeroStyle.Reaper:
                {
                    // Souls: a dark scythe crescent sweeps through, smoke blooms and
                    // teal souls are torn loose, drifting back toward the Reaper.
                    var R=(95+55*power)*s;var fade=1-Mathf.SmoothStep(.2f,1f,k);
                    for(var i=0;i<3;i++)
                    {
                        var q=p+new Vector2((CardVfxHash(b.seed,40+i)-.5f)*R*.7f,(CardVfxHash(b.seed,50+i)-.5f)*R*.4f);var size=R*(1f+.9f*k)*(.8f+.4f*CardVfxHash(b.seed,60+i));
                        FinalSprite(finalVfxSmoke,q,size*1.2f,size*1.2f,CardVfxHash(b.seed,70+i)*360,new Color(.02f,.05f,.06f,.6f*fade));
                    }
                    var sweep=profile.reduceMotion?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.16f));var angle=b.angle-40+65*sweep;
                    FinalGlow(p,R*2.2f,new Color(.05f,.32f,.30f,.35f*fade));
                    FinalSprite(finalVfxCrescent,p,R*2.3f,R*1.25f,angle,new Color(.01f,.03f,.04f,.78f*fade));
                    FinalSprite(finalVfxCrescent,p+new Vector2(0,-4),R*2.15f,R*1.05f,angle,new Color(.30f,.95f,.84f,.9f*fade));
                    FinalSprite(finalVfxCrescent,p+new Vector2(0,-7),R*2f,R*.62f,angle,new Color(.85f,1f,.97f,FinalFlash(.75f)*fade));
                    FinalGlow(p,R*1.1f,new Color(.6f,1f,.95f,FinalFlash(.6f)*Mathf.Pow(1-k,3)));
                    {var rk=Mathf.Clamp01(age/.35f);FinalRing(p,Mathf.Lerp(R*.4f,R*(1.6f+.5f*power),1-(1-rk)*(1-rk)),new Color(.35f,.95f,.85f,.75f*(1-rk)));}
                    var souls=FinalCount(3+(int)(power*4));
                    for(var i=0;i<souls;i++)
                    {
                        var t=age-.06f-i*.04f;if(t<0)continue;var kk=t/.7f;if(kk>=1)continue;
                        var start=p+new Vector2((CardVfxHash(b.seed,80+i)-.5f)*R*.8f,(CardVfxHash(b.seed,90+i)-.5f)*R*.6f);
                        for(var j=3;j>=0;j--)
                        {
                            var tt=Mathf.Max(0,kk-j*.05f);var drift=FinalParticles?new Vector2(-70*tt*s-Mathf.Sin(tt*6+i)*10,-120*tt*s):Vector2.zero;
                            var q=start+drift;var a=(1-kk)*Mathf.Sin(Mathf.Min(1,kk*4)*Mathf.PI*.5f)*(1-j*.22f);
                            FinalGlow(q,(22-j*4f)*s*2,new Color(.25f,.90f,.80f,.5f*a));FinalGlow(q,(10-j*1.8f)*s,new Color(.85f,1f,.97f,a));
                        }
                    }
                    break;
                }
                case FinalHeroStyle.Burn:
                {
                    var fade=1-Mathf.SmoothStep(.2f,1f,k);var rise=FinalEnvelope(k,.15f);
                    FinalGlow(p,120*s,new Color(1f,.46f,.14f,.5f*fade));
                    var flames=FinalCount(5);
                    for(var i=0;i<flames;i++)
                    {
                        var x=(i-(flames-1)*.5f)*18*s+(CardVfxHash(b.seed,i)-.5f)*8;var hgt=(46+30*CardVfxHash(b.seed,i+9))*s*(.5f+.7f*rise);
                        FinalSprite(cardVfxFlame,new Vector2(p.x+x,p.y+22*s-hgt*.5f),22*s,hgt,(CardVfxHash(b.seed,i+19)-.5f)*14,new Color(1,1,1,fade));
                    }
                    FinalMotes(p,FinalCount(8),b.seed,age,.6f,-90,70,60,170,-40,new Color(1f,.62f,.22f,.9f),4*s);
                    break;
                }
                default:
                {
                    FinalGlow(p,100*s,new Color(1f,.78f,.5f,FinalFlash(.6f)*Mathf.Pow(1-k,2)));
                    FinalSparks(p,FinalCount(7),b.seed,age,.35f,0,360,120,300,400,new Color(1f,.8f,.55f,1f),9*s,1.8f);
                    break;
                }
            }
            if(b.accent)
            {
                // Gilded receipt: a gold ring and a four-point glint on top of the hero style.
                var ease=1-(1-k)*(1-k);var gold=new Color(1f,.82f,.36f,(1-k)*.85f);
                FinalRing(p,Mathf.Lerp(60,230*s,ease),gold);
                if(!profile.reducedVfx)for(var i=0;i<4;i++){var d=FinalDir(45+i*90);DrawLine(p+d*10,p+d*(40+80*ease)*s,FinalA(gold,gold.a*FinalFlash(1)),3*(1-k)+1);}
                FinalMotes(p,FinalCount(8),b.seed+77,age,.6f,-90,220,80,220,50,new Color(1f,.84f,.4f,.9f),4*s);
            }
        }

        // ---------- enemy attacks landing on the hero ----------
        private void DrawFinalCrescentStroke(Vector2 c,float length,float thickness,float angle,Color accent,float fade,float reveal)
        {
            var w=length*(.35f+.65f*reveal);
            FinalSprite(finalVfxCrescent,c,w*1.06f,thickness*1.5f,angle,new Color(.30f,.02f,.03f,.55f*fade));
            FinalSprite(finalVfxCrescent,c,w,thickness,angle,FinalA(accent,.9f*fade));
            FinalSprite(finalVfxCrescent,c,w*.94f,thickness*.45f,angle,new Color(1f,.96f,.9f,FinalFlash(.9f)*fade));
        }
        private void DrawFinalEnemyStrike(FinalVfxBurst b,float k,float age)
        {
            var p=b.a;var power=b.power;var s=FinalScale*(.85f+.35f*power)*(b.accent?1.2f:1f);
            var fade=1-Mathf.SmoothStep(.18f,1f,k);var reveal=profile.reduceMotion?1:Mathf.Clamp01(age/.08f);var c=b.color;
            FinalGlow(p,90*s,FinalA(Color.Lerp(c,Color.white,.4f),FinalFlash(.55f)*Mathf.Pow(1-k,3)));
            switch((EnemyStrikeStyle)b.style)
            {
                case EnemyStrikeStyle.Claw:
                {
                    const float angle=125;var perp=FinalDir(angle+90);
                    for(var j=-1;j<=1;j++){var r=profile.reduceMotion?1:Mathf.Clamp01((age-(j+1)*.025f)/.08f);if(r<=0)continue;DrawFinalCrescentStroke(p+perp*j*20*s,190*s,34*s,angle,c,fade,r);}
                    FinalSparks(p,FinalCount(b.color.r>.9f&&b.color.g<.5f?12:6),b.seed,age,.5f,200,70,120,320,500,FinalA(c,1),8*s,2f);
                    break;
                }
                case EnemyStrikeStyle.Bite:case EnemyStrikeStyle.Chomp:
                {
                    var big=b.style==(byte)EnemyStrikeStyle.Chomp;var gap=(profile.reduceMotion?8:Mathf.Lerp(46,8,Mathf.Clamp01(age/.09f)))*s;var width=(big?170:120)*s;
                    DrawFinalCrescentStroke(p+new Vector2(0,-gap),width,30*s,180,c,fade,1);
                    DrawFinalCrescentStroke(p+new Vector2(0,gap),width,30*s,0,c,fade,1);
                    for(var i=0;i<4;i++){var x=(i-1.5f)*width*.2f;DrawLine(p+new Vector2(x,-gap-4),p+new Vector2(x,-gap+10*s),new Color(1f,.95f,.9f,.8f*fade),2);DrawLine(p+new Vector2(x+6,gap+4),p+new Vector2(x+6,gap-10*s),new Color(1f,.95f,.9f,.8f*fade),2);}
                    if(big)FinalSparks(p,FinalCount(12),b.seed,age-.05f,.6f,-90,160,160,380,900,new Color(1f,.84f,.36f,1f),5*s,4f);
                    break;
                }
                case EnemyStrikeStyle.Fang:
                {
                    for(var j=-1;j<=1;j+=2)
                    {
                        var angle=90+j*28;var dir=FinalDir(angle);var len=120*s*reveal;
                        FinalSprite(finalVfxStreak,p-dir*len*.5f+new Vector2(j*14*s,0),len,12*s,angle,FinalA(c,.85f*fade));
                        FinalSprite(finalVfxStreak,p-dir*len*.5f+new Vector2(j*14*s,0),len,4*s,angle,new Color(1f,.94f,1f,FinalFlash(.9f)*fade));
                    }
                    FinalSparks(p,FinalCount(8),b.seed,age-.04f,.6f,90,90,40,140,700,new Color(.72f,.46f,1f,.9f),4*s,4f);
                    break;
                }
                case EnemyStrikeStyle.Blade:case EnemyStrikeStyle.Grab:
                {
                    var angle=b.style==(byte)EnemyStrikeStyle.Grab?160:140+(CardVfxHash(b.seed,2)-.5f)*20;
                    if(b.style==(byte)EnemyStrikeStyle.Grab){DrawFinalCrescentStroke(p,200*s,40*s,angle,c,fade,reveal);}
                    else
                    {
                        var dir=FinalDir(angle);var len=(200+60*power)*s;var centre=p-dir*len*.5f+dir*len*reveal*.5f;
                        FinalSprite(finalVfxStreak,centre,len*reveal,30*s,angle,FinalA(c,.55f*fade));
                        FinalSprite(finalVfxStreak,centre,len*reveal,9*s,angle,new Color(.92f,.94f,1f,.95f*fade));
                    }
                    FinalSparks(p,FinalCount(10),b.seed,age,.45f,190,90,200,460,800,new Color(1f,.84f,.52f,1f),12*s,2f);
                    break;
                }
                case EnemyStrikeStyle.Cleave:
                {
                    // Executioner: a huge downward crescent and a ground crack.
                    DrawFinalCrescentStroke(p+new Vector2(-8,0),300*s,58*s,100,c,fade,reveal);
                    var feet=new Vector2(p.x,HeroPortraitRect.yMax-6);var grow=profile.reduceMotion?1:Mathf.Clamp01((age-.05f)/.15f);
                    var prev=feet;for(var i=1;i<=5;i++){var next=feet+new Vector2((i*26-65)*s*grow,(CardVfxHash(b.seed,i)-.5f)*8);DrawLine(prev,next,new Color(1f,.42f,.24f,.8f*fade),3.5f-i*.4f);prev=next;}
                    FinalMotes(feet,FinalCount(8),b.seed,age,.7f,-90,150,60,170,70,new Color(.62f,.52f,.42f,.55f),10*s);
                    break;
                }
                case EnemyStrikeStyle.Slam:case EnemyStrikeStyle.Crush:
                {
                    // Brute and Vault Mother: a flattened shockwave at the hero's feet.
                    var crush=b.style==(byte)EnemyStrikeStyle.Crush;var feet=new Vector2(p.x,HeroPortraitRect.yMax-8);var ease=1-(1-k)*(1-k);
                    FinalRing(feet,Mathf.Lerp(60,(crush?420:320)*s,profile.reduceMotion?.6f:ease),FinalA(c,.8f*(1-k)),.28f);
                    FinalRing(feet,Mathf.Lerp(40,(crush?300:230)*s,profile.reduceMotion?.5f:Mathf.Clamp01(ease*1.3f)),new Color(1f,.95f,.85f,FinalFlash(.6f)*(1-k)),.28f);
                    FinalGlow(p,150*s,FinalA(Color.Lerp(c,Color.white,.5f),FinalFlash(.6f)*Mathf.Pow(1-k,2)));
                    FinalRing(p,Mathf.Lerp(50,(crush?230:180)*s,profile.reduceMotion?.6f:ease),FinalA(c,.7f*(1-k)));
                    // Vault Crush lands from above: three heavy streaks drive down onto the hero.
                    if(crush&&FinalParticles&&age<.16f)for(var i=0;i<3;i++){var t=age/.16f;var y=p.y-110*s*(1-t);FinalSprite(finalVfxStreak,new Vector2(p.x+(i-1)*46*s,y-40*s),110*s,16*s,90,FinalA(c,.75f*(1-t*.6f)));}
                    FinalMotes(feet,FinalCount(crush?14:10),b.seed,age,.8f,-90,170,80,260,120,new Color(.66f,.58f,.46f,.6f),11*s);
                    FinalSparks(feet,FinalCount(10),b.seed+5,age,.55f,-90,120,200,420,1100,new Color(.82f,.78f,.7f,1f),6*s,3f);
                    if(!crush&&!profile.reducedVfx)for(var j=0;j<2;j++){var y=p.y-40+j*50;var prev=new Vector2(p.x+90*s,y);for(var i=1;i<=6;i++){var next=new Vector2(p.x+90*s-i*30*s*reveal,y+((i&1)==0?-10:10)*s);DrawLine(prev,next,new Color(.78f,.76f,.72f,.8f*fade),3);prev=next;}}
                    break;
                }
                case EnemyStrikeStyle.Cards:
                {
                    var ease=1-(1-k)*(1-k);FinalRing(p,Mathf.Lerp(30,170*s,ease),FinalA(c,.7f*(1-k)));
                    for(var i=0;i<FinalCount(5);i++)
                    {
                        var d=FinalDir(i*72+b.seed%72);var q=p+d*(FinalParticles?220*age:30);var spin=(FinalParticles?age*600:0)+i*40;
                        DrawFinalCard(q,36*s,spin,new Color(.96f,.30f,.36f,1-k));
                    }
                    break;
                }
                case EnemyStrikeStyle.Shards:
                {
                    // Mirror Witch: her reflection shatters against the hero.
                    var ease=1-(1-k)*(1-k);FinalGlow(p,170*s,FinalA(c,.5f*fade));FinalRing(p,Mathf.Lerp(30,180*s,ease),FinalA(c,.8f*(1-k)));
                    FinalRing(p,Mathf.Lerp(20,120*s,Mathf.Clamp01(ease*1.3f)),new Color(.92f,1f,.98f,FinalFlash(.7f)*(1-k)));
                    for(var i=0;i<FinalCount(9);i++)
                    {
                        var d=FinalDir(i*40+CardVfxHash(b.seed,i)*25);var q=p+d*(FinalParticles?(60+CardVfxHash(b.seed,i+9)*120)*age*3:20);var len=(10+CardVfxHash(b.seed,i+30)*12)*s;
                        var axis=FinalDir(i*40+(FinalParticles?age*500:0));DrawLine(q-axis*len*.5f,q+axis*len*.5f,FinalA(Color.Lerp(c,Color.white,.4f),1-k),3);
                    }
                    break;
                }
                case EnemyStrikeStyle.Spectral:
                {
                    var ease=1-(1-k)*(1-k);FinalRing(p,Mathf.Lerp(40,190*s,ease),FinalA(c,.8f*(1-k)));
                    FinalSprite(finalVfxStreak,p,170*s,16*s,-58,FinalA(c,.7f*fade));FinalSprite(finalVfxStreak,p,150*s,5*s,-58,new Color(1,1,1,FinalFlash(.9f)*fade));
                    FinalMotes(p,FinalCount(10),b.seed,age,.55f,0,360,90,240,-40,FinalA(c,.9f),5*s);
                    break;
                }
                default:
                {
                    // Bolts: an accent burst with a ring and motes.
                    var ease=1-(1-k)*(1-k);
                    FinalGlow(p,150*s,FinalA(c,.55f*fade));FinalRing(p,Mathf.Lerp(30,170*s,ease),FinalA(c,.8f*(1-k)));
                    FinalMotes(p,FinalCount(12),b.seed,age,.5f,0,360,120,280,-30,FinalA(c,.95f),5*s);
                    break;
                }
            }
        }
        private void DrawFinalCard(Vector2 c,float size,float degrees,Color color)
        {
            var matrix=GUI.matrix;GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(c.x,c.y,0),Quaternion.Euler(0,0,degrees),Vector3.one)*Matrix4x4.Translate(new Vector3(-c.x,-c.y,0));
            var r=new Rect(c.x-size*.36f,c.y-size*.5f,size*.72f,size);
            Fill(r,new Color(.08f,.03f,.06f,color.a*.9f));Outline(r,new Color(1f,.82f,.4f,color.a),2);
            Fill(new Rect(c.x-size*.12f,c.y-size*.12f,size*.24f,size*.24f),color);
            GUI.matrix=matrix;
        }
        private void DrawFinalDeflect(FinalVfxBurst b,float k,float age)
        {
            // Block absorbs the hit: a blue ward flash with sparks glancing back.
            var p=b.a;var s=FinalScale*(.85f+.3f*b.power);var ease=1-(1-k)*(1-k);var fade=1-k;
            FinalGlow(p,130*s,new Color(.52f,.82f,1f,FinalFlash(.5f)*fade*fade));
            FinalRing(p,Mathf.Lerp(80,150*s,ease),new Color(.55f,.85f,1f,.85f*fade));
            var r=Mathf.Lerp(40,70*s,ease);for(var i=0;i<6;i++){var a0=i*60f;DrawLine(p+FinalDir(a0)*r,p+FinalDir(a0+60)*r,new Color(.75f,.92f,1f,.75f*fade),2);}
            FinalSparks(p,FinalCount(9),b.seed,age,.4f,-10,100,200,460,600,new Color(.8f,.94f,1f,1f),9*s,2f);
        }

        // ---------- projectiles ----------
        private void DrawFinalProjectile(FinalVfxBurst b,float k,float age)
        {
            var e=k*k*(3-2*k);var arc=b.style==(byte)EnemyStrikeStyle.Spectral?-40:b.style==(byte)FinalProjectile.Curse?-120:-36;
            Vector2 At(float t){t=Mathf.Clamp01(t);var mid=Vector2.Lerp(b.a,b.b,.5f)+new Vector2(0,arc);return Vector2.Lerp(Vector2.Lerp(b.a,mid,t),Vector2.Lerp(mid,b.b,t),t);}
            var head=At(e);var c=b.color;var s=FinalScale;var fadeIn=Mathf.Clamp01(k*8);
            switch(b.style)
            {
                case (byte)EnemyStrikeStyle.Spectral:
                {
                    var ahead=At(Mathf.Min(1,e+.06f));var angle=Mathf.Atan2(ahead.y-head.y,ahead.x-head.x)*Mathf.Rad2Deg;
                    FinalSprite(finalVfxStreak,head,180*s,34*s,angle,FinalA(c,.6f*fadeIn));FinalSprite(finalVfxStreak,head,150*s,10*s,angle,new Color(1f,1f,1f,.95f*fadeIn));
                    for(var i=1;i<=4;i++){var q=At(e-i*.06f);FinalGlow(q,(40-i*6)*s,FinalA(c,.38f*(1-i*.2f)*fadeIn));}
                    break;
                }
                case (byte)EnemyStrikeStyle.Cards:case (byte)FinalProjectile.Curse:
                {
                    for(var i=1;i<=4;i++){var q=At(e-i*.05f);FinalGlow(q,(48-i*8)*s,FinalA(c,.34f*(1-i*.2f)*fadeIn));}
                    FinalGlow(head,70*s,FinalA(c,.35f*fadeIn));
                    DrawFinalCard(head,46*s,age*720,FinalA(b.style==(byte)FinalProjectile.Curse?new Color(.74f,.40f,1f):c,fadeIn));
                    break;
                }
                case (byte)EnemyStrikeStyle.Shards:
                {
                    for(var j=-1;j<=1;j++)
                    {
                        var q=head+new Vector2(j*10*s,j*22*s*(1-e*.6f));var axis=(b.b-b.a).normalized;
                        FinalGlow(q,34*s,FinalA(c,.45f*fadeIn));
                        DrawLine(q-axis*30*s,q+axis*16*s,FinalA(c,.8f*fadeIn),6);DrawLine(q-axis*26*s,q+axis*14*s,FinalA(Color.Lerp(c,Color.white,.6f),fadeIn),2.5f);
                    }
                    break;
                }
                default:
                {
                    // Bolt, hex: a glowing head with a trail of fading motes.
                    var hex=b.style==(byte)FinalProjectile.Hex;var trail=profile.reducedVfx?3:7;
                    for(var i=trail;i>=1;i--){var q=At(e-i*.045f);var f=(1-i/(trail+1f))*fadeIn;FinalGlow(q,(14+22*f)*s,FinalA(c,.45f*f));}
                    FinalGlow(head,(hex?40:64)*s,FinalA(c,.85f*fadeIn));FinalGlow(head,(hex?14:20)*s,new Color(1f,.96f,1f,FinalFlash(.9f)*fadeIn));
                    if(hex)FinalRing(head,30*s,FinalA(c,.8f*fadeIn));
                    break;
                }
            }
        }

        // ---------- enemy casts, steals, summons ----------
        private void DrawFinalCast(FinalVfxBurst b,float k,float age)
        {
            var p=b.a;var c=b.color;var s=FinalScale;var ease=1-(1-k)*(1-k);var fade=1-Mathf.SmoothStep(.3f,1f,k);
            if(b.style==(byte)FinalCastStyle.Ward)
            {
                FinalRing(p,Mathf.Lerp(120,210*s,profile.reduceMotion?.6f:ease),FinalA(c,.75f*fade));
                var r=Mathf.Lerp(50,95*s,ease);for(var i=0;i<6;i++){var a0=i*60f+30;DrawLine(p+FinalDir(a0)*r,p+FinalDir(a0+60)*r,FinalA(Color.Lerp(c,Color.white,.35f),.6f*fade),2);}
                FinalMotes(p+new Vector2(0,60),FinalCount(10),b.seed,age,.8f,-90,120,40,130,-30,FinalA(c,.8f),5*s);
            }
            else
            {
                FinalGlow(new Vector2(p.x,p.y-60),200*s,FinalA(c,.3f*fade));
                FinalMotes(p,FinalCount(16),b.seed,age,.9f,-90,50,90,260,-20,FinalA(c,.9f),5*s);
                FinalMotes(p,FinalCount(8),b.seed+3,age,.9f,-90,120,40,140,-20,new Color(1f,.86f,.45f,.8f),4*s);
            }
        }
        private void DrawFinalSteal(FinalVfxBurst b,float k,float age)
        {
            // Coins lifted from the hero and pulled into the thief.
            var count=FinalCount(8);
            for(var i=0;i<count;i++)
            {
                var t=Mathf.Clamp01((age-i*.035f)/(b.duration*.7f));if(t<=0||t>=1)continue;var e=t*t*(3-2*t);
                var mid=Vector2.Lerp(b.a,b.b,.5f)+new Vector2((CardVfxHash(b.seed,i)-.5f)*60,-90-CardVfxHash(b.seed,i+5)*50);
                var q=profile.reduceMotion?Vector2.Lerp(b.a,b.b,Mathf.Round(e)):Vector2.Lerp(Vector2.Lerp(b.a,mid,e),Vector2.Lerp(mid,b.b,e),e);
                var fade=Mathf.Sin(t*Mathf.PI);FinalGlow(q,20,new Color(1f,.78f,.3f,.5f*fade));
                var spin=Mathf.Abs(Mathf.Cos((age*14+i)*(profile.reduceMotion?0:1)));Fill(new Rect(q.x-5*spin,q.y-5,10*spin+1,10),new Color(1f,.84f,.36f,fade));
            }
        }
        private void DrawFinalSummon(FinalVfxBurst b,float k,float age)
        {
            // Hollow King: spectral blades materialise around the throne.
            var c=b.color;var count=Mathf.Clamp(combat.spectralWeapons,1,3);
            for(var i=0;i<count;i++)
            {
                var appear=Mathf.Clamp01((age-i*.12f)/.25f);if(appear<=0)continue;var fade=appear*(1-Mathf.SmoothStep(.6f,1f,k));
                var x=b.a.x+(i-(count-1)*.5f)*70;var y=b.a.y-40+(profile.reduceMotion?0:Mathf.Sin(age*3+i)*6);
                FinalSprite(finalVfxStreak,new Vector2(x,y),150,18,90,FinalA(c,.55f*fade));FinalSprite(finalVfxStreak,new Vector2(x,y),130,6,90,new Color(1,1,1,.85f*fade));
                FinalGlow(new Vector2(x,y+70),40,FinalA(c,.5f*fade));
            }
        }
        private void DrawFinalBanish(FinalVfxBurst b,float k,float age)
        {
            var fade=Mathf.Sin(k*Mathf.PI);var s=FinalScale;
            FinalGlow(b.a,110*s,FinalA(b.color,.45f*fade));
            for(var i=0;i<3;i++)FinalSprite(cardVfxFlame,b.a+new Vector2((i-1)*16,-18),20*s,(40+i%2*14)*s*fade,(i-1)*10,new Color(.75f,.55f,1f,fade));
        }
        private void DrawFinalShockwave(FinalVfxBurst b,float k,float age)
        {
            var ease=1-(1-k)*(1-k);var s=FinalScale;var c=b.color;
            FinalRing(b.a,Mathf.Lerp(80,520*s,profile.reduceMotion?.5f:ease),FinalA(c,.8f*(1-k)),.3f);
            FinalRing(b.a,Mathf.Lerp(60,380*s,profile.reduceMotion?.4f:Mathf.Clamp01(ease*1.25f)),new Color(1f,.9f,.7f,FinalFlash(.6f)*(1-k)),.3f);
            FinalMotes(b.b,FinalCount(18),b.seed,age,.9f,-90,300,80,300,-40,FinalA(c,.85f),6*s);
        }

        // ---------- deaths ----------
        private void DrawFinalDeath(FinalVfxBurst b,float k,float age)
        {
            var p=b.a;var c=b.color;var s=FinalScale;var tier=b.style;age/=Mathf.Max(.2f,b.angle);
            if(tier==2)
            {
                // Boss: cracks of light and spinning rays while the body trembles
                // (see EnemyPoseAt), then a large burst as it collapses.
                var build=Mathf.Clamp01(age/.7f);
                if(age<.8f)
                {
                    EnsureBossPolishTextures();
                    BossPolishCracks(p,170*s,build,.3f,(1-Mathf.Clamp01((age-.6f)/.2f))*.9f,b.seed,profile.reducedVfx?4:7);
                    if(!profile.reducedVfx)for(var i=0;i<8;i++)
                    {
                        var d=FinalDir(i*45+(profile.reduceMotion?0:age*40));var len=(120+90*build)*s;
                        FinalSprite(finalVfxStreak,p+d*len*.5f,len,14*s,i*45+(profile.reduceMotion?0:age*40),FinalA(c,.35f*build*(1-Mathf.Clamp01((age-.6f)/.2f))));
                    }
                    FinalGlow(p,260*s*(.6f+.4f*build),FinalA(c,FinalFlash(.45f)*build));
                }
                var burst=age-.72f;
                if(burst>=0)
                {
                    var bk=Mathf.Clamp01(burst/(b.duration/Mathf.Max(.2f,b.angle)-.72f));var ease=1-(1-bk)*(1-bk);
                    FinalGlow(p,Mathf.Lerp(200,520,ease)*s,new Color(1f,.93f,.78f,FinalFlash(.8f)*Mathf.Pow(1-bk,2.5f)));
                    FinalRing(p,Mathf.Lerp(80,560*s,profile.reduceMotion?.6f:ease),FinalA(c,.9f*(1-bk)));
                    FinalRing(p,Mathf.Lerp(60,400*s,profile.reduceMotion?.5f:Mathf.Clamp01(ease*1.3f)),new Color(1f,.84f,.44f,.8f*(1-bk)));
                    FinalRing(b.b,Mathf.Lerp(80,620*s,profile.reduceMotion?.6f:ease),FinalA(c,.6f*(1-bk)),.26f);
                    FinalMotes(p,FinalCount(30),b.seed,burst,1.1f,0,360,160,520,120,FinalA(c,.95f),7*s);
                    FinalMotes(p,FinalCount(18),b.seed+7,burst,1.1f,-90,200,60,260,-40,new Color(1f,.84f,.44f,.9f),5*s);
                    FinalSparks(p,FinalCount(20),b.seed+11,burst,.8f,0,360,260,700,500,new Color(1f,.9f,.7f,1f),16*s,2.5f);
                    for(var i=0;i<4;i++){var q=p+new Vector2((CardVfxHash(b.seed,i)-.5f)*160,(CardVfxHash(b.seed,i+4)-.5f)*120);var size=(120+160*bk)*s;FinalSprite(finalVfxSmoke,q,size,size,i*80,new Color(.05f,.04f,.06f,.45f*(1-bk)));}
                }
                return;
            }
            var ek=1-(1-k)*(1-k);var big=tier==1;
            FinalGlow(p,(big?260:190)*s,FinalA(Color.Lerp(c,Color.white,.3f),FinalFlash(.6f)*Mathf.Pow(1-k,2.5f)));
            FinalRing(p,Mathf.Lerp(60,(big?380:280)*s,profile.reduceMotion?.6f:ek),FinalA(c,.85f*(1-k)));
            if(big)FinalRing(p,Mathf.Lerp(40,270*s,profile.reduceMotion?.5f:Mathf.Clamp01(ek*1.3f)),new Color(1f,.84f,.44f,.7f*(1-k)));
            FinalMotes(p,FinalCount(big?22:14),b.seed,age,.8f,0,360,120,big?380:300,90,FinalA(c,.9f),6*s);
            for(var i=0;i<(big?3:2);i++){var q=p+new Vector2((CardVfxHash(b.seed,i)-.5f)*90,(CardVfxHash(b.seed,i+4)-.5f)*70);var size=(90+120*k)*s;FinalSprite(finalVfxSmoke,q,size,size,i*80,new Color(.05f,.04f,.06f,.42f*(1-k)));}
        }

        // ---------- status apply / trigger feedback ----------
        private void DrawFinalStatus(FinalVfxBurst b,float k,float age)
        {
            var p=b.a;var c=b.color;var s=FinalScale;var fade=1-Mathf.SmoothStep(.35f,1f,k);var ease=1-(1-k)*(1-k);
            switch((FinalStatusStyle)b.style)
            {
                case FinalStatusStyle.Burn:
                    FinalGlow(p,110*s,FinalA(c,.35f*fade));
                    for(var i=0;i<3;i++){var hgt=(40+18*(i%2))*s*FinalEnvelope(k,.2f);FinalSprite(cardVfxFlame,p+new Vector2((i-1)*24*s,34*s-hgt*.5f),20*s,hgt,(i-1)*8,new Color(1,1,1,fade));}
                    FinalMotes(p+new Vector2(0,30),FinalCount(6),b.seed,age,.55f,-90,60,60,150,-40,FinalA(c,.9f),4*s);
                    break;
                case FinalStatusStyle.Strength:
                    for(var i=0;i<3;i++)
                    {
                        var t=Mathf.Clamp01((age-i*.07f)/.4f);if(t<=0||t>=1)continue;var y=p.y+30-(FinalParticles?t*70:20)-i*4;var a=Mathf.Sin(t*Mathf.PI);
                        DrawLine(new Vector2(p.x-20*s,y+12*s),new Vector2(p.x,y),FinalA(c,a),4);DrawLine(new Vector2(p.x,y),new Vector2(p.x+20*s,y+12*s),FinalA(c,a),4);
                    }
                    FinalMotes(p+new Vector2(0,40),FinalCount(8),b.seed,age,.6f,-90,80,70,170,-60,new Color(1f,.6f,.3f,.9f),4*s);
                    break;
                case FinalStatusStyle.Ward:
                    FinalRing(p,Mathf.Lerp(70,150*s,profile.reduceMotion?.6f:ease),FinalA(c,.8f*fade));
                    {var r=Mathf.Lerp(34,60*s,ease);for(var i=0;i<6;i++){var a0=i*60f+30;DrawLine(p+FinalDir(a0)*r,p+FinalDir(a0+60)*r,FinalA(c,.6f*fade),2);}}
                    break;
                case FinalStatusStyle.Weak:
                    FinalRing(p,Mathf.Lerp(150,80*s,profile.reduceMotion?.5f:ease),FinalA(c,.65f*fade));
                    FinalMotes(p+new Vector2(0,-30),FinalCount(9),b.seed,age,.65f,90,80,40,120,80,FinalA(c,.8f),4*s);
                    break;
                case FinalStatusStyle.Vulnerable:
                {
                    FinalGlow(p,100*s,FinalA(c,.3f*fade));var grow=profile.reduceMotion?1:Mathf.Clamp01(age/.15f);
                    for(var i=0;i<5;i++)
                    {
                        var dir=i*72f+CardVfxHash(b.seed,i)*30;var prev=p+FinalDir(dir)*10;
                        for(var j=1;j<=3;j++){var next=p+FinalDir(dir+(CardVfxHash(b.seed,i*7+j)-.5f)*40)*(10+j*18*s*grow);DrawLine(prev,next,FinalA(c,.9f*fade),3-j*.6f);prev=next;}
                    }
                    break;
                }
                case FinalStatusStyle.Marked:
                {
                    var size=Mathf.Lerp(170,90,profile.reduceMotion?1:Mathf.Clamp01(age/.18f))*s;FinalRing(p,size,FinalA(c,.85f*fade));
                    for(var i=0;i<4;i++){var d=FinalDir(i*90);DrawLine(p+d*size*.36f,p+d*size*.56f,FinalA(c,.9f*fade),3);}
                    break;
                }
                case FinalStatusStyle.Soul:
                    for(var i=0;i<FinalCount(5);i++)
                    {
                        var t=Mathf.Clamp01((age-i*.06f)/.5f);if(t<=0||t>=1)continue;var angle=i*72f+(FinalParticles?t*260:0);var r=(1-t)*70*s+8;
                        var q=p+FinalDir(angle)*r+new Vector2(0,FinalParticles?-t*30:0);var a=Mathf.Sin(t*Mathf.PI);
                        FinalGlow(q,24*s,FinalA(c,.5f*a));FinalGlow(q,9*s,new Color(.9f,1f,.98f,a));
                    }
                    break;
                case FinalStatusStyle.Retaliate:
                {
                    var r=Mathf.Lerp(40,70*s,ease);FinalRing(p,r*2.2f,FinalA(c,.7f*fade));
                    for(var i=0;i<8;i++){var d=FinalDir(i*45+22.5f);DrawLine(p+d*r,p+d*(r+18*s),FinalA(c,.9f*fade),3);}
                    break;
                }
                default:
                    FinalRing(p,Mathf.Lerp(60,130*s,profile.reduceMotion?.6f:ease),FinalA(c,.7f*fade));
                    break;
            }
        }
        // Boss heavy hits: a brief dark-red edge pulse around the arena (under UI).
        private void DrawFinalVignette(float now)
        {
            var t=(now-finalVignetteAt)/.45f;if(t<0||t>=1)return;
            var e=(1-t)*(1-t)*finalVignettePower*(profile.reduceFlashing?.4f:1f);var w=CombatWidth;var h=CombatHeight;var edge=Mathf.Min(w,h)*.06f;
            for(var i=0;i<4;i++)
            {
                var kk=(4-i)/4f;var x=edge*i/4f;var ww=edge/4f+1;var color=profile.reduceFlashing?new Color(.25f,.02f,.03f,.30f*e*kk):new Color(.85f,.06f,.05f,.30f*e*kk);
                Fill(new Rect(x,66,ww,h-66),color);Fill(new Rect(w-x-ww,66,ww,h-66),color);
            }
        }
    }
}
