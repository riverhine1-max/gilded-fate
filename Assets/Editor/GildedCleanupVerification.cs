using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using GildedFate.UI;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    [InitializeOnLoad]
    public static class GildedCleanupVerification
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        const string Request="Library/GildedCleanup.request";
        [Serializable] class Report { public int checks,cards,relics,shards,finalBosses;public List<string> failures=new(); }
        static GildedCleanupVerification()=>EditorApplication.update+=Wait;
        static void Wait()
        {
            if(!File.Exists(Request)||EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            var loaded=File.GetLastWriteTimeUtc(typeof(GildedCleanupVerification).Assembly.Location);
            if(new[]{"Assets/Editor/GildedCleanupVerification.cs","Assets/Editor/GildedCleanupTypography.cs"}.Any(p=>File.GetLastWriteTimeUtc(p)>loaded))return;
            File.Delete(Request);
            // Recover empty stages left by a failed Edit-mode audio initialization in an older check.
            foreach(var orphan in Resources.FindObjectsOfTypeAll<GameObject>())
                if(orphan.scene.IsValid()&&orphan.name=="Gilded Fate · Sound Stage"&&!orphan.GetComponent<GildedFate.Audio.GameAudio>())UnityEngine.Object.DestroyImmediate(orphan);
            var random=UnityEngine.Random.state;
            // UI receipts call Sfx. An inactive fixture keeps those checks silent in Edit mode.
            var audioField=typeof(GildedFate.Audio.GameAudio).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
            var originalAudio=audioField.GetValue(null);
            var audioHost=new GameObject("Silent verification audio"){hideFlags=HideFlags.HideAndDontSave};audioHost.SetActive(false);
            audioField.SetValue(null,audioHost.AddComponent<GildedFate.Audio.GameAudio>());
            try{Verify();GildedEnemyArtworkVerification.Verify();GildedGameplayChangesVerification.Verify();GildedFateQualityGate.Validate();GildedCleanupTypography.Begin();}
            finally
            {
                UnityEngine.Random.state=random;
                UnityEngine.Object.DestroyImmediate(audioHost);audioField.SetValue(null,originalAudio);
            }
        }
        static readonly Type Menu=typeof(GildedMainMenu);
        static void Set(object target,string name,object value)=>Menu.GetField(name,Private).SetValue(target,value);
        static object Get(object target,string name)=>Menu.GetField(name,Private).GetValue(target);
        static object Call(object target,string name,params object[] args)=>Menu.GetMethod(name,Private).Invoke(target,args);
        static void Screen(object menu,string name)=>Set(menu,"screen",Enum.Parse(Menu.GetField("screen",Private).FieldType,name));

        [MenuItem("Gilded Fate/Verify Reliability Cleanup")]
        static void RequestVerification()=>File.WriteAllText(Request,"Run reliability, artwork, gameplay, production and typography checks in Edit mode.");

        public static void Verify()
        {
            var report=new Report{cards=GameContent.Cards.Length,relics=GameContent.Relics.Length,shards=WorldContent.FateShards.Length};
            void Check(bool pass,string label){report.checks++;if(!pass)report.failures.Add(label);}
            void Scenario(string label,Action action){try{action();}catch(Exception error){report.failures.Add(label+": "+error.GetBaseException().Message);}}
            var saveSuspended=SaveService.Suspended;var profileSuspended=ProfileService.Suspended;
            var meta=typeof(MetaUnlocks);var staticPrivate=BindingFlags.Static|BindingFlags.NonPublic;
            var bound=MetaUnlocks.IsBound;
            var marks=(int[])meta.GetField("boundHeroMarks",staticPrivate).GetValue(null);
            var total=(int)meta.GetField("boundTotalMarks",staticPrivate).GetValue(null);
            var host=new GameObject("Reliability verification"){hideFlags=HideFlags.HideAndDontSave};host.SetActive(false);
            SaveService.Suspended=ProfileService.Suspended=true;
            var menu=host.AddComponent<GildedMainMenu>();
            try
            {
                Scenario("Completed save marker",()=>
                {
                    // Read-only check of the completion marker seen in the live menu smoke test.
                    // Never create, replace or clear the player's save files for verification.
                    if(!File.Exists(SaveService.PathName))return;
                    var marker=JsonUtility.FromJson<RunModel>(File.ReadAllText(SaveService.PathName));
                    if(marker?.closed==true&&marker.saveFormat==4)
                        Check(!SaveService.HasRun&&!SaveService.DamagedSaveExists&&SaveService.LastError=="","Completed run is not labeled a damaged save");
                });
                foreach(var relic in GameContent.Relics)
                    Check(!Regex.IsMatch(relic.Description,@"\b(?:Powers?|Exhaust(?:s|ed|ing)?)\b",RegexOptions.IgnoreCase),"Relic terminology: "+relic.id);
                foreach(var card in GameContent.Cards.Where(GameContent.HasUpgradePreview))
                {
                    var upgraded=GameContent.Upgrade(card);
                    Check(upgraded.cost==(card.upgradeCost>=0?card.upgradeCost:card.cost),"Upgraded cost badge: "+card.id);
                    Check(!Regex.IsMatch(upgraded.text,@"\(\s*Cost\s*\)|^Costs \d+\.",RegexOptions.IgnoreCase),"No redundant upgrade cost text: "+card.id);
                }
                Scenario("Funeral Bell",()=>
                {
                    var fight=new CombatState();fight.Begin(HeroId.Reaper,Enumerable.Repeat(GameContent.Find("soul"),12),enemyHp:1000,equippedRelics:new[]{"funeral_bell"});
                    for(var i=0;i<3;i++)Check(fight.Play(fight.hand.First(c=>c.id=="soul")),"Funeral Bell Soul play "+i);
                    var facts=fight.TakeEvents();
                    Check(fight.exhaust.Count==3&&fight.RelicProgress("funeral_bell").StartsWith("0/3"),"Funeral Bell cadence preserved");
                    Check(facts.Any(f=>f.label=="funeral_bell"),"Funeral Bell trigger preserved");
                });
                Scenario("Saved card descriptions",()=>
                {
                    var card=GameContent.Upgrade(GameContent.Find("soul_feast"));
                    var fight=new CombatState();fight.Begin(HeroId.Reaper,Enumerable.Repeat(card,8));
                    var owned=fight.hand.First();owned.text="(Cost)";owned.cost=2;owned.permanentBlockBonus=3;
                    var checkpoint=JsonUtility.FromJson<CombatCheckpoint>(JsonUtility.ToJson(fight.CaptureCheckpoint()));
                    Check(checkpoint.TryRestore(out var restored,out _),"Existing upgraded combat checkpoint restores");
                    var refreshed=restored.hand.First(c=>c.instanceId==owned.instanceId);
                    Check(refreshed.text==card.text&&refreshed.cost==2&&refreshed.permanentBlockBonus==3,"Loaded wording refresh preserves modified cost and permanent bonuses");
                });
                foreach(var definition in WorldContent.FateShards)
                {
                    Scenario("Shard "+definition.id,()=>
                    {
                        var run=new RunModel();run.NewRun(HeroId.Vanguard,547);
                        run.shards.Add(new FateShardState{id="bloodstone",slot=1,uses=1});
                        var survivor=run.shards[0];
                        Check(run.AddShard(definition.id)&&run.shards[1].slot==0&&survivor.slot==1,"Shard fills physical gap: "+definition.id);
                        var restored=JsonUtility.FromJson<RunModel>(JsonUtility.ToJson(run));restored.EnsureShardSlots();
                        Check(restored.shards[0].slot==1&&restored.shards[1].slot==0,"Shard slots survive serialization: "+definition.id);
                        run.shards.Clear();var owned=new FateShardState{id=definition.id,slot=1};run.shards.Add(owned);
                        Set(menu,"run",run);Set(menu,"profile",new PlayerProfile());Screen(menu,"Combat");
                        for(var use=1;use<=3;use++)
                        {
                            var fight=new CombatState();fight.Begin(HeroId.Vanguard,new[]{GameContent.Find("defend")},enemyHp:10000,currentHp:10000,currentMaxHp:10000);
                            fight.AttuneShards(definition.id,"",false);fight.shardCharge=fight.ShardChargeTarget;Set(menu,"combat",fight);
                            var animation=(IEnumerator)Call(menu,"AnimateShard",definition,owned,0,false);
                            animation.MoveNext();animation.MoveNext();
                            Check(owned.active&&owned.uses==use&&owned.activeFractured==(use==3)&&owned.slot==1,"Shard activation/durability/socket "+definition.id+" use "+use);
                            Check(!fight.ActivateShard(definition,false),"One activation per combat: "+definition.id+" use "+use);
                            run.FinishActiveShard();
                            Check(use==3?run.shards.Count==0:run.shards.Count==1&&!owned.active,"Shard retirement: "+definition.id+" use "+use);
                        }
                    });
                }
                Scenario("Legacy slots and atomic replacement",()=>
                {
                    var run=new RunModel();run.NewRun(HeroId.Vanguard,133);run.gold=100;
                    run.shards.Add(new FateShardState{id="bloodstone",slot=-1});run.shards.Add(new FateShardState{id="counterweight",slot=0,uses=2});
                    run.EnsureShardSlots();Check(run.shards[0].slot==1&&run.shards[1].slot==0&&run.shards[1].uses==2,"Reserve valid socket before assigning legacy item");
                    var before=JsonUtility.ToJson(run.shards[1]);
                    run.pendingShardDiscoveryId="unknown-shard";run.pendingShardPrice=45;
                    Check(!run.TakeDiscoveredShard(1)&&JsonUtility.ToJson(run.shards[1])==before&&run.gold==100,"Invalid replacement cannot remove an owned Shard or spend Gold");
                    run.DeclineDiscoveredShard();Check(run.OfferShard("bloodstone",45)&&run.TakeDiscoveredShard(1),"Merchant replacement succeeds");
                    Check(run.shards[1].slot==0&&run.shards[0].slot==1&&run.gold==55,"Replacement keeps chosen socket and charges once");
                    Check(!run.TakeDiscoveredShard(1)&&run.gold==55,"Duplicate claim cannot spend twice");
                    run.shards[0].active=true;run.OfferShard("counterweight");
                    Check(!run.TakeDiscoveredShard(0)&&run.shards[0].active,"Active Shard cannot be replaced");
                });
                Scenario("Combat inspection",()=>
                {
                    Screen(menu,"Combat");Set(menu,"inspectedCard",null);
                    Call(menu,"InspectUpgrade",GameContent.Find("strike"));
                    Check(Get(menu,"inspectedCard")==null,"Right-click upgrade entry is blocked in combat");
                    var owned=GameContent.Upgrade(GameContent.Find("strike"));Set(menu,"inspectedCard",owned);Call(menu,"PrepareCardInspection",owned);
                    Check((bool)Get(menu,"inspectionShowUpgrade")&&!(bool)Get(menu,"inspectionComparison"),"Normal inspection retains current owned upgrade without changing it");
                    Screen(menu,"Collection");Call(menu,"InspectUpgrade",GameContent.Find("strike"));
                    Check((bool)Get(menu,"inspectionComparison"),"Upgrade inspection retained outside combat");
                });
                for(var act=1;act<=3;act++)foreach(var kind in new[]{NodeKind.Combat,NodeKind.Elite,NodeKind.Boss})
                {
                    var run=new RunModel();run.NewRun(HeroId.Vanguard,741);run.act=act;run.activeNodeFloor=16;run.activeNodeLane=1;
                    run.RollEncounterRewards(kind);var noRewards=act==3&&kind==NodeKind.Boss;
                    Check(run.encounterRewards.cards.Count==(noRewards?0:3),"Card rewards Act "+act+" "+kind);
                    Check(run.HasValidRewardState(),"Reward state validates Act "+act+" "+kind);
                    if(kind==NodeKind.Boss)Check(run.BossRelicOffers().Length==(noRewards?0:3),"Boss relics Act "+act);
                }
                foreach(var boss in WorldContent.ActiveEnemies.Where(e=>e.boss&&NeutralContent.ActOfTheme(e.theme)==3))
                    Scenario("Final victory "+boss.id,()=>
                    {
                        var run=new RunModel();run.NewRun(HeroId.Vanguard,849);run.act=3;run.elapsedSeconds=123;
                        var node=new MapNode{kind=NodeKind.Boss,floor=run.ActFloorCount-1,lane=1};run.nodes=new List<MapNode>{node};run.floor=node.floor;run.activeNodeFloor=node.floor;run.activeNodeLane=node.lane;run.stage=RunStage.Combat;
                        run.shards.Add(new FateShardState{id="bloodstone",slot=1,uses=3,active=true,activeFractured=true});
                        var fight=new CombatState();fight.Begin(HeroId.Vanguard,new[]{GameContent.Find("strike")},archetype:boss.id,enemyHp:boss.hp,baseDamage:boss.baseDamage);fight.bonusCardRewards=2;
                        for(var i=0;i<fight.EnemyCount;i++)fight.EnemyAt(i).hp=0;
                        var profile=new PlayerProfile();Set(menu,"run",run);Set(menu,"profile",profile);Set(menu,"combat",fight);Set(menu,"currentNode",node);Set(menu,"currentEnemy",boss);Screen(menu,"Combat");
                        Call(menu,"CheckCombat");report.finalBosses++;
                        Check(Get(menu,"screen").ToString()=="RunResult"&&(bool)Get(menu,"runResultVictory"),"Final boss enters victory screen: "+boss.id);
                        Check(run.closed&&run.encounterRewards.cards.Count==0&&run.pendingCombatGold==0&&run.pendingBonusCardRewards==0&&run.shards.Count==0,"Final boss skips rewards and retires spent Shard: "+boss.id);
                        Check(profile.wins==1&&profile.bossesDefeated==1&&profile.runHistory.Count==1&&profile.totalMarks>0&&profile.completedRunIds.Contains(run.runId),"Completion statistics and marks: "+boss.id);
                        Call(menu,"CheckCombat");Check(profile.wins==1&&profile.runHistory.Count==1,"Final completion is idempotent: "+boss.id);
                    });
                Scenario("Resume older final-act rewards",()=>
                {
                    var run=new RunModel();run.NewRun(HeroId.Reaper,657);run.act=3;run.stage=RunStage.CardReward;
                    var node=run.nodes.First(n=>n.kind==NodeKind.Boss);run.floor=node.floor;run.activeNodeFloor=node.floor;run.activeNodeLane=node.lane;
                    run.pendingCombatGold=125;run.combatGoldClaimed=false;run.pendingBonusCardRewards=2;
                    Set(menu,"run",run);Set(menu,"profile",new PlayerProfile());Call(menu,"RestoreRunStage");
                    Check(Get(menu,"screen").ToString()=="RunResult"&&run.pendingCombatGold==0,"Older final-boss reward save resumes as a completed run");
                });
                for(var act=1;act<=3;act++)foreach(var kind in new[]{NodeKind.Combat,NodeKind.Elite,NodeKind.Boss})
                {
                    if(RewardRules.CompletesRun(kind,act))continue;
                    Scenario("Ordinary victory Act "+act+" "+kind,()=>
                    {
                        var run=new RunModel();run.NewRun(HeroId.Vanguard,238);run.act=act;
                        var node=new MapNode{kind=kind,floor=4,lane=1};run.nodes=new List<MapNode>{node};run.floor=node.floor;run.activeNodeFloor=node.floor;run.activeNodeLane=node.lane;
                        var enemy=WorldContent.Enemies.First(e=>e.id=="vault_rat");
                        var fight=new CombatState();fight.Begin(HeroId.Vanguard,new[]{GameContent.Find("strike")});fight.enemy.hp=0;
                        var profile=new PlayerProfile();Set(menu,"run",run);Set(menu,"profile",profile);Set(menu,"currentNode",node);Set(menu,"currentEnemy",enemy);Set(menu,"combat",fight);Screen(menu,"Combat");
                        Call(menu,"CheckCombat");
                        Check(Get(menu,"screen").ToString()=="Reward"&&run.stage==RunStage.CardReward&&!run.closed&&profile.wins==0,"Reward screen preserved Act "+act+" "+kind);
                        Check(run.pendingCombatGold==RewardRules.BaseGold(kind,act)&&run.encounterRewards.cards.Count==3,"Gold and card rewards preserved Act "+act+" "+kind);
                    });
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                SaveService.Suspended=saveSuspended;ProfileService.Suspended=profileSuspended;
                if(bound)MetaUnlocks.Bind(marks,total);else MetaUnlocks.Unbind();
            }
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ReliabilityCleanupVerification.json",JsonUtility.ToJson(report,true));
            Debug.Log("[Reliability Cleanup] "+report.checks+" checks, "+report.failures.Count+" failures.");
            if(report.failures.Count>0)Debug.LogWarning(string.Join("\n",report.failures));
        }
    }
}
