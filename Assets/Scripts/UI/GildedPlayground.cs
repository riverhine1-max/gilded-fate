using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    // PLAYGROUND — admin-only balance sandbox.
    // Compiled only in the Unity Editor and in builds with "Development Build"
    // ticked. A normal release build contains none of this code.
    // Open it from the main menu: Ctrl + Shift + P, or hold LB + RB and press View.
    // Fights here never touch the saved run, the profile, stats, achievements or unlocks.
    public sealed partial class GildedMainMenu
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool playgroundActive;
        private HeroId pgHero=HeroId.Vanguard;
        private readonly string[] pgEnemies={"gilded_sentry","","",""};
        private int pgBossPhase=1,pgAct=1,pgDebt,pgHp=80,pgStrength,pgEnergy=3,pgGold=200,pgHpMultIndex=2,pgCardFilter,pgTurn=-1,pgShard=-1;
        private static readonly float[] PgHpMults={.25f,.5f,1f,2f,4f,10f};
        private bool pgInfiniteEnergy,pgGodMode;
        private readonly Dictionary<string,int> pgDeck=new();private readonly HashSet<string> pgUpgraded=new();private readonly HashSet<string> pgRelics=new();
        private string pgSearch="",pgBackup,pgResult="";private float pgCardScroll,pgRelicScroll;
        private int pgTurnDamage,pgTotalDamage,pgBiggestHit;
        private MapNode pgSavedNode;private EnemyDef pgSavedEnemy;

        private void UpdatePlayground()
        {
            if(screen==ScreenMode.Menu&&!runStartActive&&!bootIntroActive)
            {
                var k=Keyboard.current;var pad=Gamepad.current;
                var keys=k!=null&&(k.leftCtrlKey.isPressed||k.rightCtrlKey.isPressed)&&(k.leftShiftKey.isPressed||k.rightShiftKey.isPressed)&&k.pKey.wasPressedThisFrame;
                var combo=pad!=null&&pad.leftShoulder.isPressed&&pad.rightShoulder.isPressed&&pad.selectButton.wasPressedThisFrame;
                if(keys||combo)OpenPlayground();
            }
            if(!playgroundActive||combat==null||screen!=ScreenMode.Combat)return;
            if(combat.turn!=pgTurn){pgTurn=combat.turn;pgTurnDamage=0;}
            if(pgInfiniteEnergy&&combat.phase==CombatPhase.Player&&combat.energy<9)combat.energy=9;
            if(pgGodMode&&combat.player.hp<combat.player.maxHp)combat.player.hp=combat.player.maxHp;
        }
        private void OpenPlayground()
        {
            if(pgDeck.Count==0)ResetPlaygroundDeck();
            pgResult="";screen=ScreenMode.Playground;Sfx(SoundCue.UiConfirm);
        }
        private void ResetPlaygroundDeck()
        {
            pgDeck.Clear();pgUpgraded.Clear();
            var strike=pgHero==HeroId.Vanguard?"strike":pgHero==HeroId.Hexer?"hex_strike":"scythe_strike";var defend=pgHero==HeroId.Vanguard?"defend":pgHero==HeroId.Hexer?"ward":"deaths_veil";
            pgDeck[strike]=4;pgDeck[defend]=4;pgDeck[pgHero==HeroId.Vanguard?"battle_cry":pgHero==HeroId.Hexer?"invocation":"soul_call"]=1;pgDeck[pgHero==HeroId.Vanguard?"stand_firm":pgHero==HeroId.Hexer?"first_ritual":"reaping_blow"]=1;
            pgHp=pgHero==HeroId.Vanguard?80:pgHero==HeroId.Hexer?68:72;
        }
        private void PlaygroundTrackDamage(int amount){if(!playgroundActive)return;pgTurnDamage+=amount;pgTotalDamage+=amount;pgBiggestHit=Mathf.Max(pgBiggestHit,amount);}
        private bool PlaygroundCheckCombat()
        {
            if(!playgroundActive)return false;
            pgResult=combat!=null&&combat.player.hp>0?$"VICTORY IN {combat.turn} TURNS · {pgTotalDamage} DAMAGE · BIGGEST HIT {pgBiggestHit}":$"DEFEATED ON TURN {combat?.turn} · {pgTotalDamage} DAMAGE DEALT";
            screen=ScreenMode.Playground;return true;
        }
        private void StartPlaygroundFight()
        {
            var ids=pgEnemies.Where(id=>!string.IsNullOrEmpty(id)).ToList();if(ids.Count==0||pgDeck.Values.Sum()==0)return;
            var first=WorldContent.Enemies.First(e=>e.id==ids[0]);
            if(first.boss||first.elite)ids=new List<string>{ids[0]};
            else ids=ids.Where(id=>{var d=WorldContent.Enemies.First(e=>e.id==id);return !d.boss&&!d.elite;}).Take(4).ToList();
            if(!playgroundActive){pgBackup=JsonUtility.ToJson(run);pgSavedNode=currentNode;pgSavedEnemy=currentEnemy;}
            playgroundActive=true;SaveService.Suspended=true;ProfileService.Suspended=true;
            run.NewRun(pgHero,Random.Range(1,int.MaxValue));run.sandbox=true;run.act=pgAct;
            run.cards.Clear();foreach(var pair in pgDeck)for(var i=0;i<pair.Value;i++)run.AddCard(pair.Key,pgUpgraded.Contains(pair.Key));
            run.relics.Clear();foreach(var r in pgRelics)run.AcquireRelic(r);
            run.shards.Clear();if(pgShard>=0&&pgShard<WorldContent.FateShards.Length)run.AddShard(WorldContent.FateShards[pgShard].id);
            run.maxHp=run.hp=Mathf.Max(1,pgHp);run.gold=pgGold;run.fateDebt=pgDebt;run.fateDebtMask=FateDebt.MaskForLevel(pgDebt);run.SyncLegacyDeck();
            var kind=first.boss?NodeKind.Boss:first.elite?NodeKind.Elite:NodeKind.Combat;
            currentNode=new MapNode{floor=first.boss?RunModel.FloorCount-1:6,lane=RunModel.LaneCount/2,kind=kind};run.floor=currentNode.floor;
            run.activeEncounterEnemies=ids.Count>1?new List<string>(ids):new List<string>();
            BeginCombat(first,kind==NodeKind.Boss?2:kind==NodeKind.Elite?1:0);
            var mult=PgHpMults[Mathf.Clamp(pgHpMultIndex,0,PgHpMults.Length-1)];
            for(var i=0;i<combat.EnemyCount;i++){var f=combat.EnemyAt(i);f.maxHp=Mathf.Max(1,Mathf.RoundToInt(f.maxHp*mult));f.hp=f.maxHp;}
            if(first.boss&&pgBossPhase>1){combat.enemy.hp=Mathf.Max(1,Mathf.RoundToInt(combat.enemy.maxHp*(pgBossPhase==2?.6f:.3f)));combat.RefreshEnemyState();}
            combat.player.strength+=pgStrength;combat.energy=pgEnergy;
            combat.events.Clear();foreach(var c in combat.hand)combat.events.Add(new CombatEvent(CombatEventKind.Draw,1,true,c));
            ResetCombatPresentation();lastBossPhase=combat.bossPhase;bossIntroTime=0;
            pgTurn=-1;pgTurnDamage=pgTotalDamage=pgBiggestHit=0;pgResult="";
        }
        private void ExitPlayground()
        {
            if(playgroundActive&&!string.IsNullOrEmpty(pgBackup)){var restored=JsonUtility.FromJson<RunModel>(pgBackup);CopyRun(restored);run.CopyRunMeta(restored);currentNode=pgSavedNode;currentEnemy=pgSavedEnemy;}
            var wasActive=playgroundActive;playgroundActive=false;SaveService.Suspended=false;ProfileService.Suspended=false;if(wasActive)combat=null;pgBackup=null;screen=ScreenMode.Menu;
        }

        // ---------- drawing ----------
        private void DrawPlaygroundCombatHud(float w)
        {
            if(!playgroundActive||screen!=ScreenMode.Combat)return;
            var r=new Rect(64,50,560,40);Fill(r,new Color(.04f,.01f,.02f,.88f));Outline(r,new Color(1f,.4f,.3f),1);
            GUI.Label(new Rect(r.x+12,r.y,370,r.height),$"PLAYGROUND   DMG THIS TURN {pgTurnDamage} · TOTAL {pgTotalDamage} · BIGGEST HIT {pgBiggestHit}",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.8f,.6f)}});
            var restart=new Rect(r.xMax-176,r.y+5,82,30);var exit=new Rect(r.xMax-88,r.y+5,80,30);
            DrawButtonFrame(restart,restart.Contains(PointerPosition),false);DrawButtonFrame(exit,exit.Contains(PointerPosition),false);
            if(GUI.Button(restart,"RESTART",new GUIStyle(buttonStyle){fontSize=11})){StopAllCombatRoutines();StartPlaygroundFight();}
            if(GUI.Button(exit,"SETUP",new GUIStyle(buttonStyle){fontSize=11})){StopAllCombatRoutines();pgResult="";screen=ScreenMode.Playground;}
        }
        private void StopAllCombatRoutines(){if(combatSequence!=null)StopCoroutine(combatSequence);combatSequence=null;combatBusy=false;}
        private bool PgButton(Rect r,string text,bool on=false,int size=12)
        {
            var hot=r.Contains(PointerPosition);Fill(r,on?new Color(.22f,.15f,.06f,.97f):hot?new Color(.09f,.07f,.04f,.97f):new Color(.02f,.024f,.032f,.95f));Outline(r,on?Gold:hot?new Color(.8f,.65f,.35f):new Color(.3f,.3f,.31f),1);
            return GUI.Button(r,text,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=size,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=on?new Color(1f,.92f,.7f):new Color(.86f,.84f,.78f)}});
        }
        private int PgStepper(Rect r,string label,int value,int step,int min,int max)
        {
            GUI.Label(new Rect(r.x,r.y,r.width*.5f,r.height),label,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=11,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.84f,.8f,.7f)}});
            var minus=new Rect(r.x+r.width*.5f,r.y,28,r.height);var plus=new Rect(r.xMax-28,r.y,28,r.height);
            if(PgButton(minus,"−"))value=Mathf.Max(min,value-step);if(PgButton(plus,"+"))value=Mathf.Min(max,value+step);
            GUI.Label(new Rect(minus.xMax,r.y,plus.x-minus.xMax,r.height),value.ToString(),new GUIStyle(footerStyle){fontSize=13,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
            return value;
        }
        private void DrawPlayground(float w,float h)
        {
            Fill(new Rect(0,0,w,h),new Color(.012f,.01f,.016f,1f));
            Heading(w,"PLAYGROUND","ADMIN SANDBOX · NOT IN RELEASE BUILDS · NOTHING HERE IS SAVED");
            {var emblem=MetaArt("PlaygroundEmblem");if(emblem)GUI.DrawTexture(new Rect(w*.5f-262,22,82,82),emblem,ScaleMode.ScaleToFit,true);}
            var label=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=Gold}};
            // ---- left: hero, enemies, numbers ----
            var x=30f;var y=140f;
            GUI.Label(new Rect(x,y,420,20),"HERO",label);y+=22;
            for(var i=0;i<3;i++){var hero=(HeroId)i;if(PgButton(new Rect(x+i*142,y,136,30),hero.ToString().ToUpperInvariant(),pgHero==hero)&&pgHero!=hero){pgHero=hero;ResetPlaygroundDeck();}}
            y+=44;GUI.Label(new Rect(x,y,420,20),"ENEMIES · up to 4 normals, or 1 elite / boss",label);y+=22;
            var options=new List<string>{""};options.AddRange(WorldContent.Enemies.Select(e=>e.id));
            for(var slot=0;slot<4;slot++)
            {
                var id=pgEnemies[slot];var def=WorldContent.Enemies.FirstOrDefault(e=>e.id==id);
                var name=def==null?"— EMPTY —":def.name.ToUpperInvariant()+(def.boss?" · BOSS":def.elite?" · ELITE":"");
                var row=new Rect(x,y+slot*34,420,30);
                if(PgButton(new Rect(row.x,row.y,30,30),"◀")){var k=options.IndexOf(id);k=(k-1+options.Count)%options.Count;if(slot==0&&k==0)k=options.Count-1;pgEnemies[slot]=options[k];}
                if(PgButton(new Rect(row.xMax-30,row.y,30,30),"▶")){var k=options.IndexOf(id);k=(k+1)%options.Count;if(slot==0&&k==0)k=1;pgEnemies[slot]=options[k];}
                GUI.Label(new Rect(row.x+36,row.y,row.width-72,30),(slot+1)+".  "+name,new GUIStyle(footerStyle){fontSize=12,alignment=TextAnchor.MiddleLeft,normal={textColor=def==null?new Color(.5f,.5f,.5f):def.boss?new Color(1f,.5f,.4f):def.elite?new Color(1f,.8f,.45f):new Color(.9f,.88f,.82f)}});
            }
            y+=4*34+10;
            var leadBoss=WorldContent.Enemies.FirstOrDefault(e=>e.id==pgEnemies[0])?.boss==true;
            if(leadBoss){GUI.Label(new Rect(x,y,200,28),"BOSS PHASE",label);for(var p=1;p<=3;p++)if(PgButton(new Rect(x+200+(p-1)*74,y,70,28),"PHASE "+p,pgBossPhase==p))pgBossPhase=p;y+=36;}
            GUI.Label(new Rect(x,y,200,28),"ENEMY HP",label);for(var m=0;m<PgHpMults.Length;m++)if(PgButton(new Rect(x+130+m*49,y,46,28),PgHpMults[m]+"×",pgHpMultIndex==m,11))pgHpMultIndex=m;y+=38;
            pgAct=PgStepper(new Rect(x,y,420,26),"ACT (enemy scaling)",pgAct,1,1,3);y+=32;
            pgDebt=PgStepper(new Rect(x,y,420,26),"FATE DEBT",pgDebt,1,0,FateDebt.Count);y+=32;
            pgHp=PgStepper(new Rect(x,y,420,26),"HERO MAX HP",pgHp,10,10,999);y+=32;
            pgEnergy=PgStepper(new Rect(x,y,420,26),"STARTING ENERGY",pgEnergy,1,0,20);y+=32;
            pgStrength=PgStepper(new Rect(x,y,420,26),"STARTING STRENGTH",pgStrength,1,-5,99);y+=32;
            pgGold=PgStepper(new Rect(x,y,420,26),"GOLD (for Gilding)",pgGold,25,0,9999);y+=34;
            if(PgButton(new Rect(x,y,205,28),"INFINITE ENERGY",pgInfiniteEnergy))pgInfiniteEnergy=!pgInfiniteEnergy;
            if(PgButton(new Rect(x+215,y,205,28),"GOD MODE",pgGodMode))pgGodMode=!pgGodMode;
            // ---- middle: card picker ----
            var mx=480f;var my=140f;var mw=470f;
            GUI.Label(new Rect(mx,my,mw,20),"CARDS · click + / − to set copies, U to upgrade",label);my+=22;
            var filters=new[]{"VANGUARD","HEXER","REAPER","WANDERER","CURSE","ALL"};
            for(var f=0;f<filters.Length;f++)if(PgButton(new Rect(mx+f*79,my,76,26),filters[f],pgCardFilter==f,10)){pgCardFilter=f;pgCardScroll=0;}
            my+=32;pgSearch=GUI.TextField(new Rect(mx,my,mw,26),pgSearch??"",new GUIStyle(GUI.skin.textField){font=bodyFont,fontSize=13});my+=32;
            var origin=new CardOrigin?[]{CardOrigin.Knight,CardOrigin.Arcane,CardOrigin.Reaper,CardOrigin.Wanderer,CardOrigin.Curse,null}[pgCardFilter];
            var cards=GameContent.Cards.Where(c=>(origin==null||c.origin==origin)&&(string.IsNullOrEmpty(pgSearch)||c.name.ToLowerInvariant().Contains(pgSearch.ToLowerInvariant()))).OrderBy(c=>c.rarity).ThenBy(c=>c.name).ToArray();
            var listArea=new Rect(mx,my,mw,h-my-120);const float rowH=30;var content=cards.Length*rowH;UpdateScrollArea(listArea,ref pgCardScroll,content);
            GUI.BeginGroup(listArea);
            for(var i=0;i<cards.Length;i++)
            {
                var c=cards[i];var r=new Rect(0,i*rowH-pgCardScroll,listArea.width-14,rowH-3);if(r.yMax<0||r.y>listArea.height)continue;
                pgDeck.TryGetValue(c.id,out var count);Fill(r,count>0?new Color(.08f,.06f,.03f,.95f):new Color(.02f,.022f,.03f,.9f));
                var rc=c.rarity==Rarity.Rare?new Color(1f,.75f,.4f):c.rarity==Rarity.Uncommon?new Color(.6f,.8f,1f):c.rarity==Rarity.Curse?new Color(.8f,.5f,1f):new Color(.86f,.84f,.8f);
                GUI.Label(new Rect(r.x+8,r.y,r.width-150,r.height),$"{c.name}  <size=9>{c.rarity.ToString().ToUpperInvariant()} · {c.cost}E</size>",new GUIStyle(footerStyle){fontSize=12,richText=true,alignment=TextAnchor.MiddleLeft,normal={textColor=rc}});
                var up=pgUpgraded.Contains(c.id);
                if(PgButton(new Rect(r.xMax-140,r.y+2,30,r.height-4),"U",up,10)){if(up)pgUpgraded.Remove(c.id);else pgUpgraded.Add(c.id);}
                if(PgButton(new Rect(r.xMax-104,r.y+2,28,r.height-4),"−"))pgDeck[c.id]=Mathf.Max(0,count-1);
                GUI.Label(new Rect(r.xMax-74,r.y,36,r.height),count.ToString(),new GUIStyle(footerStyle){fontSize=13,alignment=TextAnchor.MiddleCenter,normal={textColor=count>0?Color.white:new Color(.45f,.45f,.45f)}});
                if(PgButton(new Rect(r.xMax-36,r.y+2,28,r.height-4),"+"))pgDeck[c.id]=Mathf.Min(20,count+1);
            }
            GUI.EndGroup();DrawScrollRail(listArea,pgCardScroll,content,cards.Length+" CARDS");
            foreach(var key in pgDeck.Where(p=>p.Value<=0).Select(p=>p.Key).ToList())pgDeck.Remove(key);
            // ---- right: deck summary, relics, shard ----
            var rx=980f;var ry=140f;var rw=w-rx-30;
            GUI.Label(new Rect(rx,ry,rw,20),$"DECK · {pgDeck.Values.Sum()} CARDS",label);
            if(PgButton(new Rect(rx+rw-160,ry-2,76,22),"STARTER",false,10))ResetPlaygroundDeck();if(PgButton(new Rect(rx+rw-78,ry-2,76,22),"CLEAR",false,10)){pgDeck.Clear();pgUpgraded.Clear();}
            ry+=24;var deckText=string.Join(", ",pgDeck.OrderBy(p=>p.Key).Select(p=>(GameContent.Find(p.Key)?.name??p.Key)+(pgUpgraded.Contains(p.Key)?"+":"")+(p.Value>1?" ×"+p.Value:"")));
            GUI.Label(new Rect(rx,ry,rw,120),deckText,new GUIStyle(footerStyle){fontSize=11,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.88f,.85f,.78f)}});ry+=126;
            GUI.Label(new Rect(rx,ry,rw,20),$"FATE SHARD",label);
            if(PgButton(new Rect(rx+110,ry-2,30,24),"◀"))pgShard=Mathf.Max(-1,pgShard-1);if(PgButton(new Rect(rx+rw-30,ry-2,30,24),"▶"))pgShard=Mathf.Min(WorldContent.FateShards.Length-1,pgShard+1);
            GUI.Label(new Rect(rx+146,ry-2,rw-182,24),pgShard<0?"NONE":WorldContent.FateShards[pgShard].name.ToUpperInvariant(),new GUIStyle(footerStyle){fontSize=11,alignment=TextAnchor.MiddleCenter});ry+=30;
            GUI.Label(new Rect(rx,ry,rw,20),$"RELICS · {pgRelics.Count}",label);if(PgButton(new Rect(rx+rw-78,ry-2,76,22),"CLEAR",false,10))pgRelics.Clear();ry+=24;
            var relicArea=new Rect(rx,ry,rw,h-ry-120);var relics=GameContent.Relics;var relicContent=relics.Length*26f;UpdateScrollArea(relicArea,ref pgRelicScroll,relicContent);
            GUI.BeginGroup(relicArea);
            for(var i=0;i<relics.Length;i++)
            {
                var rel=relics[i];var r=new Rect(0,i*26-pgRelicScroll,relicArea.width-14,23);if(r.yMax<0||r.y>relicArea.height)continue;
                if(PgButton(r,rel.name.ToUpperInvariant(),pgRelics.Contains(rel.id),10)){if(!pgRelics.Remove(rel.id))pgRelics.Add(rel.id);}
            }
            GUI.EndGroup();DrawScrollRail(relicArea,pgRelicScroll,relicContent,relics.Length+" RELICS");
            // ---- bottom ----
            if(!string.IsNullOrEmpty(pgResult))GUI.Label(new Rect(w*.5f-400,h-116,800,24),pgResult,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=14,fontStyle=FontStyle.Bold,normal={textColor=new Color(1f,.85f,.55f)}});
            var start=new Rect(w*.5f-150,h-84,300,52);DrawButtonFrame(start,start.Contains(PointerPosition),false);if(GUI.Button(start,"START FIGHT",buttonStyle))StartPlaygroundFight();
            var back=new Rect(34,h-76,148,46);DrawButtonFrame(back,back.Contains(PointerPosition),false);if(GUI.Button(back,"EXIT",buttonStyle))ExitPlayground();
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.buttonEast.wasPressedThisFrame==true)ExitPlayground();
        }
#else
        private const bool playgroundActive=false;
        private void UpdatePlayground(){}
        private void DrawPlayground(float w,float h){screen=ScreenMode.Menu;}
        private void DrawPlaygroundCombatHud(float w){}
        private void PlaygroundTrackDamage(int amount){}
        private bool PlaygroundCheckCombat()=>false;
#endif
    }
}
