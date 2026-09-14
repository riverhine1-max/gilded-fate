using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureReaperFixCapture(string mode)
        {
            if(!mode.StartsWith("reaper-fix-"))return false;
            combatTestInput=true;
            if(mode=="reaper-fix-cards")
            {
                run.NewRun(HeroId.Reaper,20260909);run.cards.Clear();run.deck.Clear();
                foreach(var card in GameContent.Cards.Where(c=>c.hero==HeroId.Reaper&&c.rarity==Rarity.Rare))run.AddCard(card.id);
                viewingRunDeck=true;collectionRelics=false;collectionSort=collectionFilter=0;collectionScroll=0;screen=ScreenMode.Collection;return true;
            }
            ConfigureShardCapture("group-four");
            for(var i=0;i<combat.EnemyCount;i++)
            {
                var f=combat.EnemyAt(i);f.hp=f.maxHp=200;f.block=0;f.strength=i+1;f.burn=i+2;f.marked=i+3;
                f.effects.Add(new CombatEffectState{id="rupture",value=i+1,source="barbed_seal"});
            }
            combat.retaliation=9;combat.TakeEvents();RestoreCombatPresentation();
            if(mode=="reaper-fix-reward"){run.RollEncounterRewards(NodeKind.Combat);run.AddEncounterCardChoices(4);run.encounterRewards.relicClaimed=run.encounterRewards.shardClaimed=true;run.combatGoldClaimed=true;run.stage=RunStage.CardReward;screen=ScreenMode.Reward;rewardRevealTime=0;}
            if(mode=="reaper-fix-map")OpenRouteInspection();
            return true;
        }
        private IEnumerator RunReaperFixChecks()
        {
            yield return WaitForCombatQueue();
            for(var count=1;count<=4;count++)
            {
                if(count==1)PrepareCombatCheck("soul",5);
                else ConfigureShardCapture(count==2?"group-two":count==3?"group-three":"group-four");
                yield return WaitForCombatQueue();
                for(var i=0;i<count;i++){combat.EnemyAt(i).strength=i+1;combat.EnemyAt(i).burn=i+3;}
                ResetPlayerStatusPlayback();
                for(var i=count-1;i>=0;i--)
                {
                    var owner=i;WithEnemyPresentation(owner,()=>{
                        var chips=EnemyEffectChips(owner);
                        CombatCheck(chips.First(c=>c.title=="STRENGTH").value==owner+1&&chips.First(c=>c.title=="BURN").value==owner+3,"Owner-specific status data "+count+"/"+owner);
                        if(count>1){
                            var area=GroupEffectArea(owner);var portrait=GroupPresentedPortrait(owner);var cell=GroupCell(owner);
                            CombatCheck(Mathf.Abs(area.center.x-portrait.center.x)<.01f&&area.y>portrait.yMax&&area.x>=cell.x&&area.xMax<=cell.xMax,"Independent enemy effect area "+count+"/"+owner);
                        }
                    });
                }
                combat.retaliation=11;combat.TakeEvents();ResetPlayerStatusPlayback();
                CombatCheck(PlayerEffectChips().Any(c=>c.title=="RETALIATE"&&c.value==11),"Retaliate remains on idle HUD "+count);
            }
            PrepareCombatCheck("brace",5);yield return WaitForCombatQueue();
            var heavy=GameContent.Find("executioners_cleave").Copy();heavy.instanceId=combat.hand[3].instanceId;combat.hand[3]=heavy;
            RestoreCombatPresentation();yield return WaitForCombatQueue();
            MajorDrag(2,new Vector2(CombatWidth*.5f,CombatHeight*.38f));yield return WaitForCombatQueue();
            CombatCheck(combat.memory.nextHeavyCostReduction==1&&combat.CostFor(heavy)==1,"Released Brace produces real Heavy discount");
            var before=combat.energy;var index=combat.hand.IndexOf(heavy);MajorDrag(index,EnemyDropZone.center);yield return WaitForCombatQueue();
            CombatCheck(!combat.hand.Contains(heavy)&&combat.energy==before-1,"Existing attack drag uses discounted payment");
            PrepareCombatCheck("grim_ascension",5);yield return WaitForCombatQueue();
            MajorDrag(2,new Vector2(CombatWidth*.5f,CombatHeight*.38f));yield return WaitForCombatQueue();
            CombatCheck(combat.memory.grimAscensionBonus==1,"Released Reaper Power resolves");
            PrepareCombatCheck("soul",5);yield return WaitForCombatQueue();
            var state=JsonUtility.ToJson(combat.CaptureCheckpoint());var runState=JsonUtility.ToJson(run);var origin=screen;
            OpenRouteInspection();CombatCheck(routeInspectionOpen&&screen==origin,"Map opens as an overlay, not a new room");
            yield return new WaitForSecondsRealtime(.25f);
            CombatCheck(JsonUtility.ToJson(combat.CaptureCheckpoint())==state&&JsonUtility.ToJson(run)==runState,"Rendered map leaves combat, piles, route, rewards and elapsed time untouched");
            mapScroll+=120;CloseRouteInspection();CombatCheck(!routeInspectionOpen&&screen==origin,"Return restores original screen");
            var hp=combat.enemy.hp;MajorDrag(2,EnemyDropZone.center);yield return WaitForCombatQueue();
            CombatCheck(combat.enemy.hp<hp,"Soul targeting still works after returning from Map");
            ConfigureReaperFixCapture("reaper-fix-reward");
            CombatCheck(SaveService.Save(run),"Seven-choice reward saves through actual Unity validation");
            var saved=SaveService.Load();
            CombatCheck(saved!=null&&saved.encounterRewards.cards.Count==7&&saved.encounterRewards.extraChoices==4,"Unity reload retains Claim Fallen extra choices");
            ConfigureReaperFixCapture("reaper-fix-group");yield return WaitForCombatQueue();
            combatPointer=new Vector2(CombatWidth*.5f,120);
        }
    }
}
