using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Combat;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureShardCapture(string mode)
        {
            if(mode is not ("group-two" or "group-three" or "group-four" or "group-input" or "shrine-full" or "no-cost-cards" or "boss-cards"))return false;
            combatTestInput=true;run.NewRun(HeroId.Vanguard,20260907);run.AddShard("bloodstone");run.AddShard("counterweight");run.shards[1].uses=2;
            currentNode=run.nodes.First(n=>n.floor==0&&n.available);run.activeNodeFloor=currentNode.floor;run.activeNodeLane=currentNode.lane;
            if(mode=="no-cost-cards")
            {
                screen=ScreenMode.Collection;bindingCaptureCards=new[]{GameContent.Find("doom"),GameContent.Find("dazed_mind"),GameContent.Find("dead_weight"),GameContent.Find("spirit_scar"),GameContent.Find("soul"),GameContent.Find("strike")};return true;
            }
            if(mode=="shrine-full"){run.OfferShard("silvermind");screen=ScreenMode.Map;return true;}
            if(mode=="boss-cards"){currentNode=run.nodes.First(n=>n.kind==NodeKind.Boss);run.activeNodeFloor=currentNode.floor;run.activeNodeLane=currentNode.lane;run.RollEncounterRewards(NodeKind.Boss);screen=ScreenMode.Reward;rewardRevealTime=0;return true;}
            var enemies=new[]{"vault_rat","gilded_sentry","masked_acolyte","golden_wisp"}.Take(mode=="group-two"?2:mode=="group-three"?3:4).ToArray();
            run.activeEncounterEnemies=enemies.ToList();run.activeEnemyId=enemies[0];currentEnemy=WorldContent.Enemies.First(e=>e.id==enemies[0]);
            combat=new CombatState();combat.Begin(run.hero,BuildCombatDeck(),48,0,80,80,null,enemies[0],7,62781,false,"",false,enemies);
            for(var i=0;i<combat.EnemyCount;i++){combat.EnemyAt(i).marked=i;combat.EnemyAt(i).block=i==1?8:0;}
            screen=ScreenMode.Combat;run.stage=RunStage.Combat;bossIntroTime=0;ResetCombatPresentation();return true;
        }
        private IEnumerator RunGroupInteractionChecks()
        {
            ConfigureShardCapture("group-four");profile.fastMode=true;yield return new WaitForSecondsRealtime(1);
            for(var i=0;i<4;i++)
            {
                var p=GroupDropZone(i).center;CombatCheck(TargetAt(p)==i,"Each group target owns its hit region: "+i);
                for(var j=i+1;j<4;j++)CombatCheck(!GroupDropZone(i).Overlaps(GroupDropZone(j)),"Group hit regions never overlap: "+i+" / "+j);
                CombatCheck(!ShrineSocket(0).Overlaps(GroupDropZone(i))&&!ShrineSocket(1).Overlaps(GroupDropZone(i)),"Shrine does not cover enemy "+i);
            }
            for(var target=0;target<4;target++)
            {
                // Exercise the same internal pointer handler used by the real mouse.
                // No desktop input or visible Unity editor is touched.
                combat.energy=20;var strike=GameContent.Find("strike").Copy();strike.instanceId=++combat.nextInstanceId;combat.hand.Insert(0,strike);
                combat.events.Add(new CombatEvent(CombatEventKind.Draw,1,true,strike));handReadyAt=Time.unscaledTime+ConsumeCombatEvents(combat.TakeEvents());yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.2f);
                var hp=Enumerable.Range(0,4).Select(i=>combat.EnemyAt(i).hp).ToArray();var blocks=Enumerable.Range(0,4).Select(i=>combat.EnemyAt(i).block).ToArray();
                var start=CardPickPoint(0);var destination=GroupDropZone(target).center;
                HandleCombatPointer(start,true,true,false);HandleCombatPointer(destination,false,true,false);HandleCombatPointer(destination,false,false,true);
                yield return WaitForCombatQueue();
                CombatCheck(!combat.hand.Contains(strike),"Release plays attack on group enemy "+target);
                CombatCheck(hp[target]+blocks[target]-combat.EnemyAt(target).hp-combat.EnemyAt(target).block==6,"Target "+target+" receives exact attack");
                for(var other=0;other<4;other++)if(other!=target)CombatCheck(combat.EnemyAt(other).hp==hp[other]&&combat.EnemyAt(other).block==blocks[other],"Other enemy unchanged: "+other);
            }
            combat.EnemyAt(0).hp=0;CombatCheck(TargetAt(GroupDropZone(0).center)==-1,"Defeated enemy cannot be dragged onto");
            var checkpoint=combat.CaptureCheckpoint();CombatCheck(checkpoint.TryRestore(out var restored,out _)&&restored.EnemyCount==4,"Multi-target combat restores from checkpoint");
            CombatCheck(GameContent.Cards.Where(c=>c.kind is CardKind.Curse or CardKind.Status||c.unplayable).All(c=>!c.ShowsEnergyCost),"All unplayable/Curse/Status cards hide energy badges");
            verificationSavesAllowed=true;
            CombatCheck(SaveCombatCheckpoint(),"Group checkpoint writes with a different selected enemy than the root");
            var beforeSave=JsonUtility.ToJson(combat.CaptureCheckpoint());var loaded=SaveService.Load();
            CombatCheck(loaded!=null&&loaded.activeEnemyId=="vault_rat"&&loaded.hasCombatCheckpoint,"Actual Unity save preserves group root ID");
            if(loaded!=null){CopyRun(loaded);RestoreRunStage();CombatCheck(JsonUtility.ToJson(combat.CaptureCheckpoint())==beforeSave,"Actual Unity reload preserves every enemy and the selected target");}
            yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.3f);
            var activated=run.shards[1];QueueShard(WorldContent.FateShards.First(s=>s.id==activated.id),activated,1);yield return WaitForCombatQueue();
            CombatCheck(activated.active&&activated.activeFractured&&activated.uses==3&&activated.slot==1,"Third activation stays in its own shrine socket");
            CombatCheck(!combat.ActivateShard(WorldContent.FateShards.First(s=>s.id=="bloodstone"),false),"Only one shard can activate in a combat");
            var block=GameContent.Find("defend").Copy();block.value=10;block.instanceId=++combat.nextInstanceId;combat.hand.Add(block);combat.energy=10;combat.Play(block);
            var facts=combat.TakeEvents();var trigger=System.Array.FindIndex(facts,f=>f.kind==CombatEventKind.ShardTrigger);
            CombatCheck(trigger>=0&&facts[trigger].label=="counterweight"&&combat.shardMemory.nextAttackBonus>=15,"Fractured Counterweight triggers its exact Block threshold");
            var delay=ConsumeCombatEvents(facts,block);yield return new WaitForSecondsRealtime(delay+.3f);
            CombatCheck(SaveCombatCheckpoint(),"Active fractured shard and counters save together");loaded=SaveService.Load();
            CombatCheck(loaded?.shards[1].uses==3&&loaded.combatCheckpoint.state.shardMemory.nextAttackBonus==combat.shardMemory.nextAttackBonus,"Unity reload cannot reset shard activation or stacked damage");
            RecordShardShatter();run.FinishActiveShard();run.EnsureShardSlots();
            CombatCheck(run.shards.Count==1&&run.shards[0].slot==0&&run.shards[0].uses==0&&shardFlights.Any(f=>f.shatter&&f.from==ShardHealthOrigin),"Final shatter empties only the used socket and leaves the inactive shard untouched");
            run.ClearCombatCheckpoint();run.stage=RunStage.CardReward;screen=ScreenMode.Reward;currentNode=run.nodes.First(n=>n.kind==NodeKind.Boss);run.activeNodeFloor=currentNode.floor;run.activeNodeLane=currentNode.lane;run.RollEncounterRewards(NodeKind.Boss);SaveService.Save(run);loaded=SaveService.Load();
            CombatCheck(loaded!=null&&loaded.encounterRewards.cards.Count==3&&loaded.encounterRewards.cards.All(id=>GameContent.Find(id).rarity==Rarity.Rare),"Actual Unity boss reward reload keeps exactly three Rare choices");
            verificationSavesAllowed=false;
            Debug.Log("[Gilded Fate Capture] Group interaction checks "+combatInteractionChecks+" · failures "+combatInteractionFailures);
        }
    }
}
