using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    // Meta progression: Fate Debt, Daily Runs, Fate Marks and card unlocks, run
    // history, achievements (with optional Steam), plus the screen overlay for
    // achievement toasts and brightness. Profile writes go through ProfileService,
    // which the Playground suspends.
    public sealed partial class GildedMainMenu
    {
        public const string GameVersion="0.9.0";
        // Paste real links here when they exist; empty links hide their buttons.
        private const string SteamPageUrl="",DiscordUrl="",FeedbackUrl="";

        private int pendingFateDebt,pendingSeed;private string pendingDailyDate="";private bool pendingDailyPractice;
        private readonly int[] selectedFateDebt=new int[3];
        private sealed class RunMetaResult{public int marks,score;public bool victory,daily,debtUnlocked;public int newDebtLevel;public List<CardDef> newCards=new();public List<AchievementDef> newAchievements=new();}
        private RunMetaResult lastRunMeta;
        private readonly List<(AchievementDef def,float at)> achievementToasts=new();
        private readonly Dictionary<string,Texture2D> metaArt=new();

        private Texture2D MetaArt(string name)
        {
            if(metaArt.TryGetValue(name,out var t))return t;
            t=Resources.Load<Texture2D>("Art/UI/Meta/"+name);
            // QA builds only: -gfMetaArt <folder> loads art that is not yet imported into the player.
            if(!t&&captureMode){var dir=CommandValue("-gfMetaArt");var file=string.IsNullOrEmpty(dir)?"":System.IO.Path.Combine(dir,name+".png");if(file!=""&&System.IO.File.Exists(file)){t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(System.IO.File.ReadAllBytes(file));t.wrapMode=TextureWrapMode.Clamp;}}
            metaArt[name]=t;return t;
        }
        private void BindMetaUnlocks(){profile.EnsureMeta();MetaUnlocks.Bind(profile.heroMarks,profile.totalMarks);}
        private static int HeroIndex(HeroId hero)=>(int)hero;

        // ---------- Daily Run identity ----------
        private static string TodayDaily=>DateTime.UtcNow.ToString("yyyy-MM-dd");
        private static int DailyHash(string date,int salt){unchecked{var h=(int)2166136261u^salt;foreach(var c in date){h^=c;h*=16777619;}return h&0x7FFFFFFF;}}
        private static int DailySeed(string date)=>DailyHash(date,9173)|1;
        private static HeroId DailyHero(string date)=>(HeroId)(DailyHash(date,31)%3);
        private static int[] DailyModifiers(string date)
        {
            var a=1+DailyHash(date,57)%FateDebt.Count;var b=1+DailyHash(date,113)%(FateDebt.Count-1);if(b>=a)b++;
            return new[]{Math.Min(a,b),Math.Max(a,b)};
        }
        private static int DailyMask(string date){var mask=0;foreach(var m in DailyModifiers(date))mask|=1<<(m-1);return mask;}
        private bool DailyCompleted(string date)=>profile.dailyRecords.Any(d=>d.date==date);

        // ---------- run start ----------
        private void PrepareNormalRunStart(HeroId hero){pendingFateDebt=Mathf.Clamp(selectedFateDebt[HeroIndex(hero)],0,profile.fateDebtUnlocked[HeroIndex(hero)]);pendingSeed=0;pendingDailyDate="";pendingDailyPractice=false;}
        private void BeginDailyRun()=>GuardReplaceRun(BeginDailyRunNow);
        private void BeginDailyRunNow()
        {
            var date=TodayDaily;var practice=DailyCompleted(date);
            pendingFateDebt=0;pendingSeed=DailySeed(date);pendingDailyDate=date;pendingDailyPractice=practice;
            selectingNewRun=true;selectedHero=DailyHero(date);heroSelectionTime=Time.unscaledTime;screen=ScreenMode.CharacterSelect;
            BeginRunStartTransition();
        }
        private int ConsumeRunSeed(){var seed=pendingSeed!=0?pendingSeed:Environment.TickCount;return seed;}
        // AdvanceRunStart, right after NewRun.
        private void ApplyRunStartMeta()
        {
            run.ResetRunMeta();
            if(!string.IsNullOrEmpty(pendingDailyDate))
            {
                run.fateDebtMask=DailyMask(pendingDailyDate);run.dailyDate=pendingDailyPractice?"":pendingDailyDate;
            }
            else{run.fateDebt=pendingFateDebt;run.fateDebtMask=FateDebt.MaskForLevel(pendingFateDebt);}
            run.ApplyFateDebtStart();
            pendingSeed=0;pendingDailyDate="";pendingDailyPractice=false;pendingFateDebt=0;
        }

        // ---------- Fate Debt in combat ----------
        // BeginCombat, right after combat.Begin and before the presentation resets.
        private void ApplyFateDebtToCombat(EnemyDef enemy)
        {
            if(combat==null||enemy==null||run.fateDebtMask==0)return;
            // Each creature uses its own class: Minions count as normal enemies, so an
            // elite's or boss's pack never receives elite or boss bonuses.
            (float hp,int strength) Scaling(EnemyDef e)
            {
                var boss=e.boss;var elite=e.elite;var normal=!boss&&!elite;var finalBoss=boss&&run.act>=3;
                var hp=1f;var strength=0;
                if(boss&&run.Debt(3))hp*=1.15f;
                if(elite&&run.Debt(8))hp*=1.15f;
                if(normal&&run.Debt(7)){hp*=1.10f;strength+=1;}
                if(finalBoss&&run.Debt(10)){hp*=1.20f;strength+=3;}
                if(elite&&run.Debt(1))strength+=Mathf.CeilToInt(e.baseDamage*.2f);
                return (hp,strength);
            }
            for(var i=0;i<combat.EnemyCount;i++)
            {
                var def=WorldContent.Enemies.FirstOrDefault(e=>e.id==combat.EnemyIdAt(i))??enemy;
                var (hp,strength)=Scaling(combat.wildCombat?def:enemy);var f=combat.EnemyAt(i);
                if(hp>1.001f){var full=f.hp>=f.maxHp;f.maxHp=Mathf.RoundToInt(f.maxHp*hp);f.hp=full?f.maxHp:Mathf.Min(f.maxHp,Mathf.RoundToInt(f.hp*hp));}
                f.strength+=strength;
            }
            if(run.Debt(6))combat.gildCostExtra=10;
            // Creatures summoned later in the fight receive normal-enemy Fate Debt scaling.
            var summon=Scaling(new EnemyDef("summon","",1,0,""));
            combat.summonHpPercent=Mathf.RoundToInt(summon.hp*100);combat.summonStrength=summon.strength;
        }
        private int FateDebtGold(int gain)=>run.Debt(2)?Mathf.RoundToInt(gain*.75f):gain;

        // ---------- per-combat tracking (CheckCombat) ----------
        private void TrackCombatMeta(bool won)
        {
            if(combat==null||run.sandbox)return;
            run.highestHitThisRun=Mathf.Max(run.highestHitThisRun,combat.highestDamage);
            run.mostBlockThisRun=Mathf.Max(run.mostBlockThisRun,combat.highestBlock);
            run.gildsThisRun+=combat.gildsThisCombat;profile.totalGilds+=combat.gildsThisCombat;
            if(won&&currentNode!=null)
            {
                if(currentNode.kind==NodeKind.Elite)run.elitesThisRun++;
                if(currentNode.kind==NodeKind.Boss){run.bossesThisRun++;if(currentEnemy!=null&&!profile.bossesDefeatedIds.Contains(currentEnemy.id))profile.bossesDefeatedIds.Add(currentEnemy.id);}
            }
            EvaluateAchievements(false,false,null);
        }

        // ---------- run end ----------
        private int RunScore(bool victory)
        {
            var score=(run.RunFloorNumber-1)*10+run.elitesThisRun*25+run.bossesThisRun*150+run.gold/2;
            if(victory)score+=500+Mathf.Max(0,(3600-Mathf.RoundToInt(run.elapsedSeconds))/10);
            return Mathf.Max(0,score);
        }
        // Called once from CompleteRun (victory) and from the defeat branch of CheckCombat.
        private void FinalizeRunMeta(bool victory)
        {
            lastRunMeta=null;if(run.sandbox)return;
            profile.EnsureMeta();var result=new RunMetaResult{victory=victory,daily=run.IsDaily};
            var hero=HeroIndex(run.hero);
            var before=new HashSet<string>(MetaUnlocks.AllLockable().Where(c=>MetaUnlocks.IsUnlocked(c,profile.heroMarks,profile.totalMarks)).Select(c=>c.id));
            var marks=Mathf.Max(0,run.RunFloorNumber-1)+run.elitesThisRun*5+run.bossesThisRun*20+(victory?40+run.fateDebt*5:0)+(run.IsDaily?10:0);
            profile.heroMarks[hero]+=marks;profile.totalMarks+=marks;result.marks=marks;
            foreach(var c in MetaUnlocks.AllLockable())if(!before.Contains(c.id)&&MetaUnlocks.IsUnlocked(c,profile.heroMarks,profile.totalMarks))result.newCards.Add(c);
            if(victory)
            {
                profile.winsByHero[hero]++;
                if(!run.IsDaily&&string.IsNullOrEmpty(run.dailyDate))
                {
                    profile.fateDebtBestWin[hero]=Mathf.Max(profile.fateDebtBestWin[hero],run.fateDebt);
                    var next=Mathf.Min(FateDebt.Count,run.fateDebt+1);
                    if(next>profile.fateDebtUnlocked[hero]){profile.fateDebtUnlocked[hero]=next;result.debtUnlocked=true;result.newDebtLevel=next;}
                }
            }
            result.score=RunScore(victory);
            if(run.IsDaily&&!DailyCompleted(run.dailyDate))
            {
                profile.dailyRecords.Insert(0,new DailyRecord{date=run.dailyDate,hero=run.hero.ToString(),victory=victory,score=result.score,floor=run.RunFloorNumber});
                if(profile.dailyRecords.Count>120)profile.dailyRecords.RemoveRange(120,profile.dailyRecords.Count-120);
                profile.dailyRunsCompleted++;
            }
            var record=new RunRecord{date=DateTime.Now.ToString("yyyy-MM-dd HH:mm"),hero=run.hero.ToString(),victory=victory,fateDebt=run.fateDebt,dailyDate=run.dailyDate,
                seed=run.seed.ToString(),act=run.act,floor=run.RunFloorNumber,score=result.score,seconds=Mathf.RoundToInt(run.elapsedSeconds),highestHit=run.highestHitThisRun,
                gilds=run.gildsThisRun,marks=marks,killedBy=victory?"":currentEnemy?.name??""};
            foreach(var c in run.cards){var d=c.BuildDefinition();if(d!=null)record.deck.Add(d.name);}
            record.relics.AddRange(run.relics.Select(id=>GameContent.Relics.FirstOrDefault(r=>r.id==id)?.name??id));
            foreach(var s in run.shards){var def=WorldContent.FateShards.FirstOrDefault(f=>f.id==s.id);record.shards.Add(def?.name??s.id);}
            profile.runHistory.Insert(0,record);if(profile.runHistory.Count>100)profile.runHistory.RemoveRange(100,profile.runHistory.Count-100);
            BindMetaUnlocks();
            EvaluateAchievements(true,victory,result);
            lastRunMeta=result;ProfileService.Save(profile);
        }

        // ---------- achievements ----------
        private void GrantAchievement(string id,RunMetaResult result)
        {
            if(run.sandbox||profile.achievements.Contains(id))return;var def=AchievementCatalog.Find(id);if(def==null)return;
            profile.achievements.Add(id);achievementToasts.Add((def,Time.unscaledTime+achievementToasts.Count*1.2f));result?.newAchievements.Add(def);
            GildedSteam.Unlock(id);Sfx(SoundCue.RewardRelic);
        }
        private void EvaluateAchievements(bool runEnd,bool victory,RunMetaResult result)
        {
            if(profile==null||run.sandbox)return;profile.EnsureMeta();
            void Check(bool ok,string id){if(ok)GrantAchievement(id,result);}
            var hit=Mathf.Max(profile.highestDamage,run.highestHitThisRun);
            Check(hit>=50,"ACH_HIT_50");Check(hit>=100,"ACH_HIT_100");Check(hit>=250,"ACH_HIT_250");
            Check(Mathf.Max(profile.mostBlock,run.mostBlockThisRun)>=50,"ACH_BLOCK_50");
            Check(profile.totalGilds>=1,"ACH_GILD_1");Check(profile.totalGilds>=25,"ACH_GILD_25");
            Check(profile.elitesDefeated>=10,"ACH_ELITES_10");Check(profile.enemiesDefeated>=100,"ACH_ENEMIES_100");Check(profile.cardsPlayed>=1000,"ACH_CARDS_1000");
            Check(profile.bossesDefeatedIds.Contains("hollow_king"),"ACH_BOSS_HOLLOW_KING");Check(profile.bossesDefeatedIds.Contains("vault_mother"),"ACH_BOSS_VAULT_MOTHER");Check(profile.bossesDefeatedIds.Contains("last_dealer"),"ACH_BOSS_LAST_DEALER");
            Check(run.relics.Count>=15,"ACH_RELICS_15");Check(run.shards.Count>=3,"ACH_SHARDS_3");
            Check(profile.runsPlayed>=25,"ACH_RUNS_25");
            Check(profile.dailyRunsCompleted>=1,"ACH_DAILY_1");Check(profile.dailyRunsCompleted>=7,"ACH_DAILY_7");
            var unlocked=MetaUnlocks.AllLockable().Count(c=>MetaUnlocks.IsUnlocked(c,profile.heroMarks,profile.totalMarks));
            Check(unlocked>0,"ACH_UNLOCK_FIRST");Check(unlocked>=MetaUnlocks.LockedTotal(),"ACH_UNLOCK_ALL");
            Check(profile.winsByHero.Sum()>0||profile.wins>0,"ACH_FIRST_ASCENT");
            Check(profile.winsByHero[0]>0,"ACH_WIN_VANGUARD");Check(profile.winsByHero[1]>0,"ACH_WIN_HEXER");Check(profile.winsByHero[2]>0,"ACH_WIN_REAPER");
            var bestDebt=profile.fateDebtBestWin.Max();
            Check(bestDebt>=1,"ACH_DEBT_1");Check(bestDebt>=5,"ACH_DEBT_5");Check(bestDebt>=10,"ACH_DEBT_10");
            if(runEnd&&victory)
            {
                Check(run.cards.Count<=15,"ACH_LEAN_DECK");Check(run.cards.Count>=40,"ACH_THICK_DECK");
                Check(run.elapsedSeconds<40*60,"ACH_SPEED");Check(run.fateweaveSelections.Count>=3,"ACH_FATEWOVEN");
            }
            ProfileService.Save(profile);
        }
        private void SyncAchievementsToSteam(){if(profile?.achievements==null||profile.adminUnlocked)return;foreach(var id in profile.achievements)GildedSteam.Unlock(id);}

        // ---------- overlay: brightness + achievement toasts (drawn last) ----------
        private void DrawMetaOverlay()
        {
            if(profile==null)return;
            var scale=UiScale;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            var w=Screen.width/scale;var h=Screen.height/scale;
            var b=Mathf.Clamp(profile.brightness,.7f,1.3f);
            if(b<.995f)Fill(new Rect(0,0,w,h),new Color(0,0,0,(1-b)*1.6f));
            else if(b>1.005f)Fill(new Rect(0,0,w,h),new Color(1,.97f,.9f,(b-1)*.22f));
            if(achievementToasts.Count==0)return;
            var now=Time.unscaledTime;achievementToasts.RemoveAll(t=>now>t.at+4.2f);
            var y=74f;
            foreach(var (def,at) in achievementToasts)
            {
                var age=now-at;if(age<0)continue;
                var slide=profile.reduceMotion?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.35f));var fade=Mathf.Clamp01((4.2f-age)/.5f);
                var r=new Rect(w*.5f-220,y-40+slide*40,440,70);var old=GUI.color;GUI.color=new Color(1,1,1,fade);
                Fill(r,new Color(.02f,.018f,.012f,.96f));Outline(r,new Color(1f,.8f,.4f),2);Fill(new Rect(r.x,r.y,4,r.height),new Color(1f,.78f,.32f));
                DrawAchievementIcon(new Rect(r.x+12,r.y+9,52,52),def,true);
                GUI.Label(new Rect(r.x+76,r.y+8,r.width-90,20),"ACHIEVEMENT UNLOCKED",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.8f,.42f,fade)}});
                GUI.Label(new Rect(r.x+76,r.y+26,r.width-90,24),def.name,new GUIStyle(titleStyle){font=headingFont?headingFont:labelFont,fontSize=19,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.95f,.82f,fade)}});
                GUI.Label(new Rect(r.x+76,r.y+48,r.width-90,18),def.text,new GUIStyle(footerStyle){fontSize=12,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.85f,.82f,.74f,fade)}});
                GUI.color=old;y+=80;
            }
        }
        private void DrawAchievementIcon(Rect r,AchievementDef def,bool unlocked)
        {
            EnsureBossPolishTextures();var old=GUI.color;
            BossPolishTint(r,bossPolishDisc,unlocked?new Color(.12f,.09f,.04f):new Color(.06f,.06f,.07f));
            var inner=new Rect(r.x+r.width*.12f,r.y+r.height*.12f,r.width*.76f,r.height*.76f);
            GUI.color=unlocked?Color.white:new Color(.45f,.45f,.48f,.8f);
            var icon=def.icon;
            if(icon.StartsWith("boss:")){var e=WorldContent.Enemies.FirstOrDefault(x=>x.id==icon.Substring(5));if(e!=null)DrawFloatingEnemy(inner,e);}
            else if(icon.StartsWith("hero:")){DrawFloatingHero(inner,(HeroId)int.Parse(icon.Substring(5)));}
            else
            {
                var art=MetaArt(icon=="debt"?"FateDebtSeal":icon=="daily"?"DailyEmblem":icon=="unlock"?"LockedCard":"AchievementsEmblem");
                if(art)GUI.DrawTexture(inner,art,ScaleMode.ScaleToFit,true);
                else{GUI.color=unlocked?(icon=="gild"?new Color(1f,.8f,.3f):icon=="debt"?new Color(1f,.4f,.3f):icon=="daily"?new Color(.6f,.9f,1f):new Color(.4f,.95f,.7f)):new Color(.4f,.4f,.42f);DrawCardUiShape(new Rect(inner.center.x-inner.width*.3f,inner.center.y-inner.height*.3f,inner.width*.6f,inner.height*.6f),cardVfxPip?cardVfxPip:white,GUI.color);}
            }
            GUI.color=old;
            BossPolishTint(r,bossPolishRing,unlocked?new Color(1f,.8f,.4f):new Color(.35f,.33f,.3f));
            if(!unlocked){EnsureCardVfxTextures();var l=r.width*.38f;DrawCardUiShape(new Rect(r.xMax-l*.9f,r.yMax-l*1.05f,l,l*1.2f),cardVfxLock,new Color(.75f,.75f,.78f));}
        }

        // ---------- Fate Debt selector (character select) ----------
        private void ChangeFateDebt(int delta)
        {
            var hero=HeroIndex(selectedHero);var max=profile.fateDebtUnlocked[hero];if(max<=0)return;
            selectedFateDebt[hero]=Mathf.Clamp(selectedFateDebt[hero]+delta,0,max);Sfx(SoundCue.UiHover);
        }
        private void DrawFateDebtSelector(Rect r)
        {
            var hero=HeroIndex(selectedHero);var max=profile.fateDebtUnlocked[hero];var level=Mathf.Clamp(selectedFateDebt[hero],0,max);selectedFateDebt[hero]=level;
            Fill(r,new Color(.02f,.012f,.01f,.9f));Outline(r,level>0?new Color(.95f,.38f,.26f):new Color(.42f,.34f,.22f),1);
            var seal=MetaArt("FateDebtSeal");var icon=new Rect(r.x+8,r.y+6,r.height-12,r.height-12);
            if(seal){var old=GUI.color;GUI.color=max>0?Color.white:new Color(.5f,.5f,.5f);GUI.DrawTexture(icon,seal,ScaleMode.ScaleToFit,true);GUI.color=old;}
            else{EnsureBossPolishTextures();BossPolishTint(icon,bossPolishDisc,new Color(.16f,.05f,.03f));BossPolishTint(icon,bossPolishRing,level>0?new Color(1f,.45f,.3f):new Color(.6f,.5f,.35f));}
            GUI.Label(icon,level>0?FateDebt.Roman(level):"",new GUIStyle(titleStyle){font=headingFont?headingFont:labelFont,fontSize=15,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.9f,.7f)}});
            var title=max<=0?"FATE DEBT · LOCKED":level==0?"FATE DEBT · NONE":"FATE DEBT · "+FateDebt.Roman(level)+(level==2?"  ·  INCLUDES I":level>2?"  ·  INCLUDES I–"+FateDebt.Roman(level-1):"");
            GUI.Label(new Rect(icon.xMax+10,r.y+4,r.width-icon.width-122,22),title,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=level>0?new Color(1f,.62f,.45f):new Color(.9f,.84f,.7f)}});
            var detail=max<=0?"Win a run with this hero to open Fate Debt I.":level==0?"No penalties. Press ▶ to take on debt.":FateDebt.Names[level-1]+": "+FateDebt.Texts[level-1];
            GUI.Label(new Rect(icon.xMax+10,r.y+24,r.width-icon.width-122,r.height-26),detail,new GUIStyle(footerStyle){fontSize=12,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.84f,.8f,.72f)}});
            if(max<=0)return;
            var left=new Rect(r.xMax-98,r.center.y-18,44,36);var right=new Rect(r.xMax-50,r.center.y-18,44,36);
            DrawButtonFrame(left,left.Contains(PointerPosition),level<=0);DrawButtonFrame(right,right.Contains(PointerPosition),level>=max);
            if(GUI.Button(left,"◀",buttonStyle))ChangeFateDebt(-1);if(GUI.Button(right,"▶",buttonStyle))ChangeFateDebt(1);
            if(ShowPadGlyphs){DrawPadGlyph(new Vector2(left.center.x,left.yMax+9),"LB",true,16);DrawPadGlyph(new Vector2(right.center.x,right.yMax+9),"RB",true,16);}
        }

        // ---------- run result: marks, unlocks, achievements ----------
        private void DrawRunResultMeta(float w,float h)
        {
            var m=lastRunMeta;if(m==null)return;
            var hero=Mathf.Clamp(HeroIndex(run.hero),0,2);var have=profile.heroMarks[hero];var next=-1;foreach(var t in MetaUnlocks.HeroThresholds)if(t>have){next=t;break;}
            var height=14+40+28+22+(m.debtUnlocked?40:0)+(m.newCards.Count>0?122:0)+(m.newAchievements.Count>0?22+34*Mathf.Min(3,m.newAchievements.Count):0)+10;
            var r=new Rect(w-372,h*.2f,340,height);Fill(r,new Color(.012f,.012f,.016f,.92f));Outline(r,new Color(.82f,.62f,.3f),1);
            var label=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=Gold}};
            var body=new GUIStyle(footerStyle){fontSize=13,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.9f,.87f,.8f)}};
            var y=r.y+14;
            var coin=MetaArt("FateMark");var icon=new Rect(r.x+16,y,30,30);if(coin)GUI.DrawTexture(icon,coin,ScaleMode.ScaleToFit,true);else{EnsureBossPolishTextures();BossPolishTint(icon,bossPolishDisc,new Color(.95f,.72f,.3f));}
            GUI.Label(new Rect(icon.xMax+10,y,r.width-70,30),$"FATE MARKS  +{m.marks}",new GUIStyle(label){fontSize=16});y+=40;
            GUI.Label(new Rect(r.x+16,y,r.width-32,20),(m.daily?"DAILY SCORE  ":"SCORE  ")+m.score,label);y+=28;
            GUI.Label(new Rect(r.x+16,y,r.width-32,20),next>0?$"NEXT CARD UNLOCK  {have} / {next} MARKS":"EVERY HERO CARD UNLOCKED",new GUIStyle(body){fontSize=12});y+=22;
            if(m.debtUnlocked){GUI.Label(new Rect(r.x+16,y,r.width-32,40),"FATE DEBT "+FateDebt.Roman(m.newDebtLevel)+" UNLOCKED FOR THIS HERO",new GUIStyle(label){wordWrap=true,normal={textColor=new Color(1f,.55f,.4f)}});y+=40;}
            if(m.newCards.Count>0)
            {
                GUI.Label(new Rect(r.x+16,y,r.width-32,20),"NEW CARDS UNLOCKED · "+m.newCards.Count,label);y+=22;
                GUI.Label(new Rect(r.x+16,y,r.width-32,96),string.Join("\n",m.newCards.Take(5).Select(c=>"• "+c.name))+(m.newCards.Count>5?$"\n…and {m.newCards.Count-5} more":""),body);y+=100;
            }
            if(m.newAchievements.Count>0)
            {
                GUI.Label(new Rect(r.x+16,y,r.width-32,20),"ACHIEVEMENTS",label);y+=22;
                foreach(var a in m.newAchievements.Take(3)){DrawAchievementIcon(new Rect(r.x+16,y,30,30),a,true);GUI.Label(new Rect(r.x+54,y+4,r.width-70,22),a.name,body);y+=34;}
            }
        }

        // ---------- collection: chained locked cards ----------
        private void DrawCollectionLock(Rect r,CardDef card)
        {
            if(card==null||!MetaUnlocks.IsBound||MetaUnlocks.Allowed(card))return;
            var art=MetaArt("LockedCard");
            Fill(r,new Color(0,0,0,.55f));Fill(new Rect(r.x+3,r.y+r.height*.15f,r.width-6,r.height*.85f-3),new Color(.015f,.016f,.02f,.94f));
            if(art){var old=GUI.color;GUI.color=new Color(1,1,1,.92f);GUI.DrawTexture(r,art,ScaleMode.ScaleToFit,true);GUI.color=old;}
            else{EnsureCardVfxTextures();var s=Mathf.Min(r.width,r.height)*.34f;DrawCardUiShape(new Rect(r.center.x-s*.5f,r.center.y-s*.62f,s,s*1.2f),cardVfxLock,new Color(.95f,.8f,.45f));}
            var tier=MetaUnlocks.TierOf(card);var origin=card.origin;var isWanderer=origin==CardOrigin.Wanderer;
            var need=isWanderer?MetaUnlocks.WandererThresholds[tier-1]:MetaUnlocks.HeroThresholds[tier-1];
            var have=isWanderer?profile.totalMarks:profile.heroMarks[origin==CardOrigin.Knight?0:origin==CardOrigin.Arcane?1:2];
            GUI.Label(new Rect(r.x,r.yMax-36,r.width,32),$"LOCKED · TIER {tier}\n{have}/{need} MARKS",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.85f,.55f)}});
        }
    }
}
