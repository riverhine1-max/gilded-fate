using System;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Core;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    // Records (Achievements · Run History · Statistics · Unlocks), the Daily Run
    // screen, and small main-menu touches (Continue summary, quit confirm, links,
    // What's New). Mouse, keyboard and controller all work on every screen.
    public sealed partial class GildedMainMenu
    {
        private int recordsTab,historySelected;private float recordsScroll;
        private bool quitConfirmOpen,whatsNewOpen;
        private string continueSummary="";private float continueSummaryAt=-10;
        private static readonly string[] WhatsNew=
        {
            "TITLE SCREEN · PLAY and ARCHIVE gather every mode and record into two big hubs.",
            "FATE DEBT · ten stacking challenge levels per hero. Win to unlock the next.",
            "DAILY RUN · one seed for everyone each day, a rotating hero and two modifiers.",
            "UNLOCKS · earn Fate Marks every run to open new cards for each hero.",
            "RECORDS · achievements, run history, statistics and unlock progress.",
            "SETTINGS · window mode, resolution, brightness, game speed and a Controls page.",
            "COMBAT · new enemy animations, attack effects and a clearer End Turn seal.",
        };

        // ---------- navigation for the new screens (called from HandleLegacyMenuNavigation) ----------
        private bool HandleMetaScreenNavigation(MenuNavigation input)
        {
            if(HandleReplaceRunNavigation(input))return true;
            if(screen==ScreenMode.Menu&&(quitConfirmOpen||whatsNewOpen))
            {
                if(!input.Any)return true;controllerNavigation=true;
                if(quitConfirmOpen){if(input.accept)Application.Quit();else if(input.back)quitConfirmOpen=false;}
                else if(input.accept||input.back)CloseWhatsNew();
                return true;
            }
            if(screen==ScreenMode.Records)
            {
                if(!input.Any)return true;controllerNavigation=true;
                if(input.back){screen=ScreenMode.Menu;Sfx(SoundCue.UiBack);return true;}
                if(input.page!=0||input.x!=0&&recordsTab!=1){recordsTab=(recordsTab+(input.page!=0?input.page:input.x)+4)%4;recordsScroll=0;historySelected=0;Sfx(SoundCue.UiHover);return true;}
                if(recordsTab==1&&profile.runHistory.Count>0){if(input.y!=0){historySelected=Mathf.Clamp(historySelected+input.y,0,profile.runHistory.Count-1);recordsScroll=Mathf.Max(0,historySelected*46-300);}}
                else if(input.y!=0)recordsScroll=Mathf.Max(0,recordsScroll+input.y*60);
                return true;
            }
            if(screen==ScreenMode.Daily)
            {
                if(!input.Any)return true;controllerNavigation=true;
                if(input.back){screen=ScreenMode.Menu;Sfx(SoundCue.UiBack);}else if(input.accept)BeginDailyRun();
                return true;
            }
            return false;
        }

        // ---------- main menu touches ----------
        private void OpenRecords(){recordsTab=0;recordsScroll=0;historySelected=0;screen=ScreenMode.Records;}
        private void CloseWhatsNew(){whatsNewOpen=false;profile.lastSeenVersion=GameVersion;ProfileService.Save(profile);}
        private string ContinueSummary()
        {
            if(Time.unscaledTime-continueSummaryAt<2f)return continueSummary;continueSummaryAt=Time.unscaledTime;continueSummary="";
            if(!SaveService.HasRun)return continueSummary;
            var saved=SaveService.Load();if(saved==null||saved.closed)return continueSummary;
            var floor=(Mathf.Clamp(saved.act,1,3)-1)*Map.RunModel.FloorCount+saved.floor+1;
            continueHero=saved.hero;continueSummary=$"{saved.hero.ToString().ToUpperInvariant()} · ACT {FateDebt.Roman(Mathf.Clamp(saved.act,1,3))} · FLOOR {floor}"+(saved.fateDebt>0?" · DEBT "+FateDebt.Roman(saved.fateDebt):"")+(saved.IsDaily?" · DAILY":"");
            return continueSummary;
        }
        // Drawn at the end of the main menu.
        private void DrawMenuExtras(float w,float h,Rect continueButton)
        {
            if(profile.lastSeenVersion!=GameVersion&&!bootIntroActive&&!whatsNewOpen&&string.IsNullOrEmpty(profile.lastSeenVersion)==false)whatsNewOpen=true;
            if(string.IsNullOrEmpty(profile.lastSeenVersion)){profile.lastSeenVersion=GameVersion;ProfileService.Save(profile);}
            var summary=ContinueSummary();
            if(!string.IsNullOrEmpty(summary))GUI.Label(new Rect(continueButton.xMax+14,continueButton.y,320,continueButton.height),summary,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=11,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.86f,.76f,.55f)}});
            // Links (hidden until a URL is set in GildedMetaProgress.cs).
            var x=26f;
            void Link(string label,string url){if(string.IsNullOrEmpty(url))return;var r=new Rect(x,h-74,170,38);DrawButtonFrame(r,r.Contains(PointerPosition),false);if(GUI.Button(r,label,new GUIStyle(buttonStyle){fontSize=12}))Application.OpenURL(url);x+=180;}
            Link("WISHLIST ON STEAM",SteamPageUrl);Link("DISCORD",DiscordUrl);Link("SEND FEEDBACK",FeedbackUrl);
            var news=new Rect(w-170,h-74,144,38);DrawButtonFrame(news,news.Contains(PointerPosition),false);if(GUI.Button(news,"WHAT'S NEW",new GUIStyle(buttonStyle){fontSize=12}))whatsNewOpen=true;
            if(!whatsNewOpen&&!quitConfirmOpen)DrawPlaygroundMenuButton(w,h); // Editor / Development Build only
            if(whatsNewOpen)DrawWhatsNew(w,h);
            if(quitConfirmOpen)DrawQuitConfirm(w,h);
        }
        private void DrawModalPanel(float w,float h,Rect panel)
        {
            Fill(new Rect(0,0,w,h),new Color(0,0,0,.62f));Fill(panel,new Color(.012f,.014f,.02f,.98f));Outline(panel,new Color(.86f,.66f,.3f),2);Fill(new Rect(panel.x,panel.y,panel.width,3),new Color(1f,.8f,.4f));
        }
        private void DrawQuitConfirm(float w,float h)
        {
            var panel=new Rect(w*.5f-230,h*.5f-110,460,220);DrawModalPanel(w,h,panel);
            GUI.Label(new Rect(panel.x,panel.y+26,panel.width,40),"LEAVE THE VAULT?",new GUIStyle(titleStyle){fontSize=28});
            GUI.Label(new Rect(panel.x+30,panel.y+72,panel.width-60,40),SaveService.HasRun?"Your current run is saved and can be continued later.":"See you in the Vault.",new GUIStyle(footerStyle){fontSize=14,wordWrap=true});
            var yes=new Rect(panel.x+40,panel.yMax-70,180,44);var no=new Rect(panel.xMax-220,panel.yMax-70,180,44);
            DrawButtonFrame(yes,yes.Contains(PointerPosition),false);DrawButtonFrame(no,no.Contains(PointerPosition),false);
            if(GUI.Button(yes,"QUIT",buttonStyle))Application.Quit();if(GUI.Button(no,"STAY",buttonStyle))quitConfirmOpen=false;
            if(ShowPadGlyphs){DrawPadGlyph(new Vector2(yes.x+18,yes.center.y),"A",true);DrawPadGlyph(new Vector2(no.x+18,no.center.y),"B",true);}
        }
        private void DrawWhatsNew(float w,float h)
        {
            // Emblem column on the left, one row per change on the right; the panel grows with the list.
            const float rowH=44f;var listH=WhatsNew.Length*rowH;
            var panel=new Rect(w*.5f-410,h*.5f-(listH+170)*.5f,820,listH+170);DrawModalPanel(w,h,panel);
            GUI.Label(new Rect(panel.x,panel.y+22,panel.width,40),"WHAT'S NEW · "+GameVersion,new GUIStyle(titleStyle){fontSize=27});
            var emblem=MetaArt("WhatsNewEmblem");var textX=panel.x+40;
            if(emblem){var s=Mathf.Min(190,listH);GUI.DrawTexture(new Rect(panel.x+30,panel.y+76+(listH-s)*.5f,s,s),emblem,ScaleMode.ScaleToFit,true);textX=panel.x+30+s+24;}
            var body=new GUIStyle(footerStyle){fontSize=14,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.9f,.87f,.8f)}};
            var textW=panel.xMax-36-textX-16;
            for(var i=0;i<WhatsNew.Length;i++){var y=panel.y+80+i*rowH;Fill(new Rect(textX,y+8,6,6),Gold);GUI.Label(new Rect(textX+16,y,textW,rowH-2),WhatsNew[i],body);}
            var close=new Rect(panel.center.x-100,panel.yMax-64,200,44);DrawButtonFrame(close,close.Contains(PointerPosition),false);if(GUI.Button(close,"CONTINUE",buttonStyle))CloseWhatsNew();
        }

        // ---------- Records ----------
        private void DrawRecords(float w,float h)
        {
            DrawFullBackdrop(collectionBackground,w,h,.82f);
            Heading(w,"RECORDS","EVERY FATE THE VAULT HAS WITNESSED");
            {var emblem=MetaArt(recordsTab==0?"AchievementsEmblem":recordsTab==3?"LockedCard":"RunHistoryEmblem");if(emblem){GUI.DrawTexture(new Rect(w*.5f-236,24,78,78),emblem,ScaleMode.ScaleToFit,true);GUI.DrawTexture(new Rect(w*.5f+158,24,78,78),emblem,ScaleMode.ScaleToFit,true);}}
            var tabs=new[]{"ACHIEVEMENTS","RUN HISTORY","STATISTICS","UNLOCKS"};
            DrawTabs(w*.5f-tabs.Length*90,146,180,tabs,recordsTab,i=>{recordsTab=i;recordsScroll=0;historySelected=0;});
            var view=new Rect(w*.5f-560,198,1120,h-300);
            if(recordsTab==0)DrawAchievementsTab(view);else if(recordsTab==1)DrawRunHistoryTab(view);else if(recordsTab==2)DrawStatsTab(view);else DrawUnlocksTab(view);
            BackButton(w,h);
            DrawMenuNavigationHint(w,h,"Q / E  Tab    ↑ ↓  Browse    Esc  Return","LB / RB  Tab    D-pad / Stick  Browse    B  Return");
        }
        private void DrawAchievementsTab(Rect view)
        {
            var all=AchievementCatalog.All;var have=all.Count(a=>profile.achievements.Contains(a.id));
            GUI.Label(new Rect(view.x,view.y-4,view.width,22),$"{have} / {all.Length} UNLOCKED"+(GildedSteam.Available?"  ·  SYNCED WITH STEAM":""),new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,normal={textColor=Gold}});
            var area=new Rect(view.x,view.y+24,view.width,view.height-24);const int cols=3;const float cellH=78;var rows=(all.Length+cols-1)/cols;var content=rows*cellH;
            UpdateScrollArea(area,ref recordsScroll,content);recordsScroll=Mathf.Clamp(recordsScroll,0,Mathf.Max(0,content-area.height));
            GUI.BeginGroup(area);
            for(var i=0;i<all.Length;i++)
            {
                var a=all[i];var got=profile.achievements.Contains(a.id);var r=new Rect(i%cols*(area.width/cols),i/cols*cellH-recordsScroll,area.width/cols-12,cellH-10);if(r.yMax<0||r.y>area.height)continue;
                Fill(r,got?new Color(.07f,.05f,.025f,.95f):new Color(.02f,.022f,.028f,.92f));Outline(r,got?new Color(.86f,.66f,.32f):new Color(.25f,.25f,.27f),1);
                DrawAchievementIcon(new Rect(r.x+8,r.y+7,r.height-14,r.height-14),a,got);
                GUI.Label(new Rect(r.x+r.height+2,r.y+8,r.width-r.height-10,24),a.name,new GUIStyle(titleStyle){font=headingFont?headingFont:labelFont,fontSize=16,alignment=TextAnchor.MiddleLeft,normal={textColor=got?new Color(1f,.92f,.75f):new Color(.62f,.6f,.56f)}});
                GUI.Label(new Rect(r.x+r.height+2,r.y+32,r.width-r.height-10,32),a.text,new GUIStyle(footerStyle){fontSize=11,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=got?new Color(.85f,.82f,.74f):new Color(.55f,.54f,.52f)}});
            }
            GUI.EndGroup();DrawScrollRail(area,recordsScroll,content,"SCROLL");
        }
        private void DrawRunHistoryTab(Rect view)
        {
            var list=profile.runHistory;
            if(list.Count==0){GUI.Label(new Rect(view.x,view.y+80,view.width,40),"No runs recorded yet. Every run you finish or lose is kept here.",new GUIStyle(footerStyle){fontSize=15});return;}
            historySelected=Mathf.Clamp(historySelected,0,list.Count-1);
            var area=new Rect(view.x,view.y,640,view.height);const float rowH=46;var content=list.Count*rowH;UpdateScrollArea(area,ref recordsScroll,content);
            GUI.BeginGroup(area);
            for(var i=0;i<list.Count;i++)
            {
                var rec=list[i];var r=new Rect(0,i*rowH-recordsScroll,area.width-14,rowH-6);if(r.yMax<0||r.y>area.height)continue;var sel=i==historySelected;
                Fill(r,sel?new Color(.1f,.07f,.03f,.97f):new Color(.02f,.022f,.03f,.92f));Outline(r,sel?Gold:new Color(.26f,.26f,.27f),sel?2:1);
                var color=rec.victory?new Color(.6f,.95f,.65f):new Color(1f,.55f,.45f);
                var style=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.9f,.87f,.8f)}};
                GUI.Label(new Rect(r.x+12,r.y,120,r.height),rec.date,style);
                GUI.Label(new Rect(r.x+138,r.y,100,r.height),rec.hero.ToUpperInvariant(),style);
                GUI.Label(new Rect(r.x+238,r.y,90,r.height),rec.victory?"VICTORY":"DEFEAT",new GUIStyle(style){normal={textColor=color}});
                GUI.Label(new Rect(r.x+328,r.y,120,r.height),$"FLOOR {rec.floor}"+(rec.fateDebt>0?" · FD "+FateDebt.Roman(rec.fateDebt):""),style);
                GUI.Label(new Rect(r.x+452,r.y,160,r.height),(string.IsNullOrEmpty(rec.dailyDate)?"":"DAILY · ")+"SCORE "+rec.score,new GUIStyle(style){alignment=TextAnchor.MiddleRight});
                if(GUI.Button(r,"",GUIStyle.none)){historySelected=i;Sfx(SoundCue.UiHover);}
            }
            GUI.EndGroup();DrawScrollRail(area,recordsScroll,content,list.Count+" RUNS");
            var d=list[historySelected];var detail=new Rect(area.xMax+24,view.y,view.xMax-area.xMax-24,view.height);
            Fill(detail,new Color(.012f,.014f,.02f,.95f));Outline(detail,new Color(.6f,.48f,.28f),1);
            var head=new GUIStyle(titleStyle){font=headingFont?headingFont:labelFont,fontSize=20,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.92f,.75f)}};
            var body=new GUIStyle(footerStyle){fontSize=12,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.88f,.85f,.78f)}};
            GUI.Label(new Rect(detail.x+18,detail.y+12,detail.width-36,30),$"{d.hero.ToUpperInvariant()} · {(d.victory?"VICTORY":"DEFEAT")}",head);
            var time=TimeSpan.FromSeconds(d.seconds).ToString(@"h\:mm\:ss");
            var summary=$"{d.date}   ·   Floor {d.floor} of 54   ·   {time}\nFate Debt {(d.fateDebt>0?FateDebt.Roman(d.fateDebt):"none")}   ·   Score {d.score}   ·   +{d.marks} Fate Marks\nBiggest hit {d.highestHit}   ·   Gilds {d.gilds}"+(d.victory?"":$"\nFell to: {(string.IsNullOrEmpty(d.killedBy)?"unknown":d.killedBy)}")+(string.IsNullOrEmpty(d.seed)?"":$"\nSeed {d.seed}")+(string.IsNullOrEmpty(d.dailyDate)?"":$"   ·   Daily {d.dailyDate}");
            GUI.Label(new Rect(detail.x+18,detail.y+46,detail.width-36,96),summary,body);
            var deck=string.Join(", ",d.deck.GroupBy(n=>n).OrderBy(g=>g.Key).Select(g=>g.Count()>1?g.Key+" ×"+g.Count():g.Key));
            GUI.Label(new Rect(detail.x+18,detail.y+148,detail.width-36,20),"FINAL DECK · "+d.deck.Count,new GUIStyle(body){font=labelFont?labelFont:bodyFont,fontStyle=FontStyle.Bold,normal={textColor=Gold}});
            GUI.Label(new Rect(detail.x+18,detail.y+170,detail.width-36,detail.height-290),deck,body);
            GUI.Label(new Rect(detail.x+18,detail.yMax-114,detail.width-36,20),"RELICS · "+d.relics.Count+(d.shards.Count>0?"   ·   SHARDS · "+d.shards.Count:""),new GUIStyle(body){font=labelFont?labelFont:bodyFont,fontStyle=FontStyle.Bold,normal={textColor=Gold}});
            GUI.Label(new Rect(detail.x+18,detail.yMax-92,detail.width-36,86),string.Join(", ",d.relics.Concat(d.shards)),body);
        }
        private void DrawStatsTab(Rect view)
        {
            var fastest=profile.fastestVictory<=0?"—":TimeSpan.FromSeconds(profile.fastestVictory).ToString(@"mm\:ss");
            string[] left={$"RUNS PLAYED|{profile.runsPlayed}",$"WINS|{profile.wins}",$"LOSSES|{profile.losses}",$"FASTEST VICTORY|{fastest}",$"DAILY RUNS|{profile.dailyRunsCompleted}",$"TOTAL FATE MARKS|{profile.totalMarks}",$"GILDS|{profile.totalGilds}"};
            string[] right={$"HIGHEST DAMAGE|{profile.highestDamage}",$"MOST BLOCK|{profile.mostBlock}",$"CARDS PLAYED|{profile.cardsPlayed}",$"ENEMIES DEFEATED|{profile.enemiesDefeated}",$"ELITES DEFEATED|{profile.elitesDefeated}",$"BOSSES DEFEATED|{profile.bossesDefeated}",$"RELICS COLLECTED|{profile.relicsCollected}"};
            void Column(string[] lines,float x)
            {
                for(var i=0;i<lines.Length;i++)
                {
                    var parts=lines[i].Split('|');var r=new Rect(x,view.y+i*44,520,38);Fill(r,new Color(.02f,.022f,.03f,.9f));Outline(r,new Color(.3f,.28f,.24f),1);
                    GUI.Label(new Rect(r.x+16,r.y,300,r.height),parts[0],new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.86f,.82f,.72f)}});
                    GUI.Label(new Rect(r.x+16,r.y,r.width-32,r.height),parts[1],new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=17,alignment=TextAnchor.MiddleRight,normal={textColor=new Color(1f,.9f,.65f)}});
                }
            }
            Column(left,view.x+20);Column(right,view.x+580);
            var y=view.y+7*44+16;var names=new[]{"VANGUARD","HEXER","REAPER"};
            for(var hero=0;hero<3;hero++)
            {
                var r=new Rect(view.x+20+hero*373,y,360,64);Fill(r,new Color(.03f,.024f,.016f,.92f));Outline(r,HeroAccent((HeroId)hero),1);
                GUI.Label(new Rect(r.x+14,r.y+6,r.width-28,22),names[hero],new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=HeroAccent((HeroId)hero)}});
                var best=profile.fateDebtBestWin[hero];
                GUI.Label(new Rect(r.x+14,r.y+30,r.width-28,26),$"WINS {profile.winsByHero[hero]}   ·   BEST DEBT {(best<0?"—":best==0?"NONE":FateDebt.Roman(best))}   ·   UNLOCKED {FateDebt.Roman(Mathf.Max(0,profile.fateDebtUnlocked[hero]))}",new GUIStyle(footerStyle){fontSize=12,alignment=TextAnchor.MiddleLeft});
            }
        }
        private void DrawUnlocksTab(Rect view)
        {
            var names=new[]{"VANGUARD","HEXER","REAPER","WANDERER (ALL HEROES)"};var origins=new[]{CardOrigin.Knight,CardOrigin.Arcane,CardOrigin.Reaper,CardOrigin.Wanderer};
            GUI.Label(new Rect(view.x,view.y-4,view.width,22),"Earn Fate Marks on every run: floors climbed, elites, bosses and victories. Higher Fate Debt pays more.",new GUIStyle(footerStyle){fontSize=12,normal={textColor=new Color(.85f,.8f,.7f)}});
            for(var i=0;i<4;i++)
            {
                var r=new Rect(view.x+20,view.y+30+i*104,view.width-40,92);Fill(r,new Color(.02f,.022f,.03f,.92f));Outline(r,i<3?HeroAccent((HeroId)i):Gold,1);
                var marks=i<3?profile.heroMarks[i]:profile.totalMarks;var thresholds=i<3?MetaUnlocks.HeroThresholds:MetaUnlocks.WandererThresholds;
                var pool=MetaUnlocks.AllLockable().Where(c=>c.origin==origins[i]).ToArray();var open=pool.Count(c=>MetaUnlocks.IsUnlocked(c,profile.heroMarks,profile.totalMarks));
                GUI.Label(new Rect(r.x+16,r.y+8,400,24),names[i],new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=14,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=i<3?HeroAccent((HeroId)i):Gold}});
                GUI.Label(new Rect(r.x+16,r.y+8,r.width-32,24),$"{marks} MARKS   ·   {open} / {pool.Length} LOCKED CARDS OPENED",new GUIStyle(footerStyle){fontSize=12,alignment=TextAnchor.MiddleRight});
                var bar=new Rect(r.x+16,r.y+48,r.width-32,14);var max=thresholds[thresholds.Length-1];
                Fill(bar,new Color(.1f,.09f,.08f));Fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(marks/(float)max),bar.height),new Color(.86f,.64f,.28f));
                for(var t=0;t<thresholds.Length;t++)
                {
                    var x=bar.x+bar.width*thresholds[t]/(float)max;Fill(new Rect(x-1,bar.y-4,2,bar.height+8),marks>=thresholds[t]?new Color(1f,.92f,.6f):new Color(.4f,.38f,.34f));
                    GUI.Label(new Rect(x-40,bar.yMax+2,80,18),"TIER "+(t+1),new GUIStyle(footerStyle){fontSize=9,normal={textColor=marks>=thresholds[t]?Gold:new Color(.55f,.53f,.5f)}});
                }
            }
        }

        // ---------- Daily Run ----------
        private void DrawDaily(float w,float h)
        {
            DrawFullBackdrop(rewardBackground,w,h,.7f);
            var date=TodayDaily;var hero=DailyHero(date);var mods=DailyModifiers(date);var done=DailyCompleted(date);
            Heading(w,"DAILY RUN",DateTime.UtcNow.ToString("dddd · d MMMM yyyy").ToUpperInvariant()+" · SAME FATE FOR EVERYONE");
            var stage=new Rect(w*.5f-560,170,520,h-290);Fill(stage,new Color(.008f,.012f,.02f,.7f));Outline(stage,HeroAccent(hero),2);
            DrawFloatingHero(new Rect(stage.x+10,stage.y+10,stage.width-20,stage.height-90),hero);
            GUI.Label(new Rect(stage.x,stage.yMax-74,stage.width,40),"TODAY'S HERO · "+hero.ToString().ToUpperInvariant(),new GUIStyle(titleStyle){fontSize=24,normal={textColor=new Color(1f,.92f,.75f)}});
            var panel=new Rect(w*.5f+20,170,540,h-290);Fill(panel,new Color(.01f,.014f,.022f,.95f));Outline(panel,Gold,1);
            var emblem=MetaArt("DailyEmblem");if(emblem)GUI.DrawTexture(new Rect(panel.xMax-96,panel.y+14,80,80),emblem,ScaleMode.ScaleToFit,true);
            var label=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=Gold}};
            var body=new GUIStyle(footerStyle){fontSize=13,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.9f,.87f,.8f)}};
            GUI.Label(new Rect(panel.x+24,panel.y+20,300,24),"TODAY'S MODIFIERS",label);
            for(var i=0;i<mods.Length;i++)
            {
                var m=mods[i];GUI.Label(new Rect(panel.x+24,panel.y+52+i*62,panel.width-140,22),FateDebt.Names[m-1],new GUIStyle(label){normal={textColor=new Color(1f,.6f,.45f)}});
                GUI.Label(new Rect(panel.x+24,panel.y+74+i*62,panel.width-48,36),FateDebt.Texts[m-1],body);
            }
            GUI.Label(new Rect(panel.x+24,panel.y+186,panel.width-48,40),done?"You have played today's scored attempt. You can keep practicing; practice runs are not scored.":"One scored attempt per day. Score: floors, elites, bosses, gold, and a speed bonus for victory.",body);
            GUI.Label(new Rect(panel.x+24,panel.y+238,300,22),"RECENT DAILIES",label);
            var recent=profile.dailyRecords.Take(6).ToArray();
            if(recent.Length==0)GUI.Label(new Rect(panel.x+24,panel.y+262,panel.width-48,24),"No daily runs yet.",body);
            for(var i=0;i<recent.Length;i++){var d=recent[i];GUI.Label(new Rect(panel.x+24,panel.y+262+i*24,panel.width-48,22),$"{d.date}   {d.hero.ToUpperInvariant()}   {(d.victory?"VICTORY":"FLOOR "+d.floor)}",body);GUI.Label(new Rect(panel.x+24,panel.y+262+i*24,panel.width-48,22),"SCORE "+d.score,new GUIStyle(body){alignment=TextAnchor.UpperRight});}
            var best=profile.dailyRecords.Count>0?profile.dailyRecords.Max(d=>d.score):0;
            if(best>0)GUI.Label(new Rect(panel.x+24,panel.yMax-112,panel.width-48,22),"BEST DAILY SCORE · "+best,label);
            var begin=new Rect(panel.x+24,panel.yMax-70,panel.width-48,48);DrawButtonFrame(begin,begin.Contains(PointerPosition)||controllerNavigation,false);
            if(GUI.Button(begin,done?"PRACTICE TODAY'S RUN · UNSCORED":"BEGIN TODAY'S RUN",buttonStyle))BeginDailyRun();
            if(ShowPadGlyphs)DrawPadGlyph(new Vector2(begin.x+22,begin.center.y),"A",true);
            BackButton(w,h);
        }
    }
}
