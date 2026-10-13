using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using GildedFate.UI;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    public static class GildedGameplayChangesVerification
    {
        [Serializable] private sealed class Report { public int checks,bosses; public List<string> failures=new(); }
        private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        [MenuItem("Gilded Fate/Verify Gameplay Changes")]
        public static void Verify()
        {
            var report=new Report();
            void Check(bool pass,string label){report.checks++;if(!pass)report.failures.Add(label);}
            foreach(var boss in WorldContent.Enemies.Where(e=>e.boss))
            {
                for(var starting=1;starting<=2;starting++)
                {
                    try
                    {
                        var fight=new CombatState();
                        fight.Begin(HeroId.Vanguard,new[]{GameContent.Find("strike"),GameContent.Find("defend")},boss.hp,2,
                            100000,100000,archetype:boss.id,baseDamage:boss.baseDamage,seed:735);
                        if(starting==2){fight.enemy.hp=fight.enemy.maxHp/2;fight.RefreshEnemyState(true);}
                        string Plan()=>fight.InspectEnemy(0,()=>string.Join("|",fight.PlannedEnemyTurn().Select(a=>a.type+":"+a.amount+":"+a.hits)));
                        var plan=Plan();var phase=fight.bossPhase;var strength=fight.enemy.strength;
                        fight.enemy.hp=Math.Max(1,fight.enemy.maxHp/5);fight.RefreshEnemyState();
                        Check(Plan()==plan,boss.id+" phase "+starting+": announced action retained");
                        Check(fight.bossPhase==phase&&fight.enemy.strength==strength,boss.id+": phase entry waits");
                        var saved=JsonUtility.ToJson(fight.CaptureCheckpoint());
                        var checkpoint=JsonUtility.FromJson<CombatCheckpoint>(saved);
                        Check(checkpoint.TryRestore(out var restored,out _),boss.id+": pending transition checkpoint restores");
                        if(restored!=null)
                        {
                            Check(restored.InspectEnemy(0,()=>string.Join("|",restored.PlannedEnemyTurn().Select(a=>a.type+":"+a.amount+":"+a.hits)))==plan,boss.id+": restored action retained");
                            restored.EndPlayerTurn();restored.ResolveEnemyTurn();
                            Check(restored.bossPhase==3,boss.id+": phase advances after announced action");
                            restored.NextTurn();
                            Check(restored.phase==CombatPhase.Player&&restored.bossPhase==3,boss.id+": next phase plans on next player turn");
                        }
                        report.bosses++;
                    }
                    catch(Exception error){report.failures.Add(boss.id+": "+error.GetBaseException().Message);}
                }
            }
            for(var act=1;act<=3;act++)
            {
                Check(Enumerable.Range(0,100).All(roll=>RewardRules.DropsShard(NodeKind.Boss,roll,act)==(act<3)),"Guaranteed boss shard Act "+act);
                var run=new RunModel();run.NewRun(HeroId.Vanguard,547);run.act=act;run.activeNodeFloor=16;run.activeNodeLane=1;
                run.RollEncounterRewards(NodeKind.Boss);
                Check(!string.IsNullOrEmpty(run.encounterRewards.shardId)==(act<3),"Boss reward contains shard Act "+act);
                var id=run.encounterRewards.shardId;run.RollEncounterRewards(NodeKind.Boss);
                Check(run.encounterRewards.shardId==id,"Boss reward does not reroll Act "+act);
            }
            Check(Enumerable.Range(1,3).All(a=>!ActThemes.ForAct(a).Contains(ActThemes.GildedRuins)),"Ruins absent from active act choices");
            Check(WorldContent.ActiveEnemies.All(e=>e.theme!=ActThemes.GildedRuins),"Ruins absent from active enemy roster");
            Check(ThemeRosters.Formations.All(e=>e.theme!=ActThemes.GildedRuins),"Ruins absent from active encounters");
            Check(GildedRuinsContent.Enemies.All(e=>WorldContent.Enemies.Any(saved=>saved.id==e.id)),"Archived enemies retained for save compatibility");
            var shardfall=EventContent.All.First(e=>e.id=="shardfall");
            Check(EventSystem.EventWeight(shardfall,1)>(int)shardfall.frequency*100
                &&EventSystem.EventWeight(shardfall,2)>EventSystem.EventWeight(shardfall,1)
                &&EventSystem.EventWeight(shardfall,3)>EventSystem.EventWeight(shardfall,2),"Shard event weights increase each act");
            var rewards=new RunModel();rewards.NewRun(HeroId.Vanguard,921);
            var offered=GameContent.Relics.First(r=>!rewards.relics.Contains(r.id)&&r.rarity==Rarity.Common);
            Check(rewards.OfferRelic(offered.id)&&!rewards.relics.Contains(offered.id),"Offered relic does not automatically apply");
            var restoredRewards=JsonUtility.FromJson<RunModel>(JsonUtility.ToJson(rewards));
            Check(restoredRewards.pendingRelicRewards.Contains(offered.id)&&!restoredRewards.relics.Contains(offered.id),"Unread relic persists through save/reload");
            Check(restoredRewards.ClaimOfferedRelic(offered.id)&&restoredRewards.relics.Contains(offered.id),"Relic click applies acquisition");
            Check(!restoredRewards.ClaimOfferedRelic(offered.id),"Relic cannot be claimed twice");
            var host=new GameObject("Gameplay change verification"){hideFlags=HideFlags.HideAndDontSave};host.SetActive(false);
            try
            {
                var menu=host.AddComponent<GildedMainMenu>();var type=typeof(GildedMainMenu);
                var run=(RunModel)type.GetField("run",Private).GetValue(menu);run.NewRun(HeroId.Vanguard,839);
                type.GetField("profile",Private).SetValue(menu,new PlayerProfile());
                type.GetField("screen",Private).SetValue(menu,Enum.Parse(type.GetField("screen",Private).FieldType,"Combat"));
                var combat=new CombatState();combat.Begin(HeroId.Vanguard,new[]{GameContent.Find("strike")},archetype:"vault_rat",encounterEnemyIds:new[]{"vault_rat","vault_spider","gilded_sentry"});
                type.GetField("combat",Private).SetValue(menu,combat);
                var targets=(IList)type.GetField("combatHudTargets",Private).GetValue(menu);
                var targetType=type.GetNestedType("CombatHudTarget",BindingFlags.NonPublic);
                for(var i=0;i<3;i++)
                {
                    var target=Activator.CreateInstance(targetType);targetType.GetField("key").SetValue(target,"enemy:"+i+":body");
                    targetType.GetField("group").SetValue(target,2+i);targetType.GetField("index").SetValue(target,i);
                    targets.Add(target);
                }
                Check((bool)type.GetMethod("OpenCombatEnemyFocus",Private).Invoke(menu,null),"Controller opens enemy overview directly");
                var seen=new HashSet<int>();
                for(var i=0;i<3;i++){seen.Add((int)type.GetField("combatTargetIndex",Private).GetValue(menu));type.GetMethod("MoveCombatEnemyFocus",Private).Invoke(menu,new object[]{1});}
                Check(seen.Count==3,"Controller cycles through every enemy");
                combat.EnemyAt(1).hp=0;
                for(var i=0;i<4;i++){type.GetMethod("MoveCombatEnemyFocus",Private).Invoke(menu,new object[]{1});Check((int)type.GetField("combatTargetIndex",Private).GetValue(menu)!=1,"Controller skips defeated enemy");}
                Check((float)type.GetField("ModificationPreviewSeconds",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue()>=1.5f,"Card change remains readable before departure");
                type.GetMethod("BeginOfferedRelicAcquisition",Private).Invoke(menu,new object[]{offered,(Action)(()=>{})});
                Check((bool)type.GetField("acquisitionAwaitingClaim",Private).GetValue(menu),"Relic preview waits for confirmation");
                Check(type.GetField("acquisitionSequence",Private).GetValue(menu)==null,"Relic preview has no automatic expiry");
            }
            catch(Exception error){report.failures.Add("UI: "+error.GetBaseException().Message);}
            finally{UnityEngine.Object.DestroyImmediate(host);}
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GameplayChangesVerification.json",JsonUtility.ToJson(report,true));
            Debug.Log("[Gameplay changes] "+report.checks+" checks, "+report.bosses+" boss scenarios, "+report.failures.Count+" failures.");
            if(report.failures.Count>0)throw new Exception(string.Join("\n",report.failures));
        }
    }
}
