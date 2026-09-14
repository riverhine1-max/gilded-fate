using System.Collections;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private void PrepareIntentEncounter(string[] ids,bool thirdPhase=false)
        {
            PrepareCombatCheck("strike",5);
            currentEnemy=WorldContent.Enemies.First(e=>e.id==ids[0]);
            combat.Begin(HeroId.Vanguard,Enumerable.Repeat(GameContent.Find("strike"),14),currentEnemy.hp,0,80,80,null,currentEnemy.id,currentEnemy.baseDamage,892,encounterEnemyIds:ids.Length>1?ids:null);
            combat.hand.Clear();combat.turn=2;combat.phase=CombatPhase.EnemyResolved;
            if(thirdPhase)combat.enemy.hp=Mathf.Max(1,combat.enemy.maxHp/4);
            combat.NextTurn();combat.energy=3;
            var discard=GameContent.Find("defend").Copy();discard.instanceId=++combat.nextInstanceId;combat.discard.Add(discard);
            combat.TakeEvents();RestoreCombatPresentation();combatPointer=new Vector2(CombatWidth*.5f,200);
        }
        private bool ConfigureEnemyIntentCapture(string mode)
        {
            if(!mode.StartsWith("polish-intents"))return false;
            PrepareIntentEncounter(mode.Contains("four")?new[]{"vault_rat","rune_mage","masked_acolyte","vault_spider"}:mode.Contains("boss")?new[]{"last_dealer"}:new[]{"collector"},mode.Contains("boss"));
            return true;
        }
        private IEnumerator RunEnemyIntentChecks()
        {
            foreach(var large in new[]{false,true})foreach(var count in new[]{1,2,3,4})
            {
                profile.largeIntents=large;
                PrepareIntentEncounter(new[]{"vault_rat","rune_mage","masked_acolyte","vault_spider"}.Take(count).ToArray());
                yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.2f);
                for(var owner=0;owner<count;owner++)
                {
                    var actions=EnemyIntents(owner);
                    for(var i=0;i<actions.Count;i++)
                    {
                        var r=IntentActionRect(owner,i);var portrait=count>1?GroupPresentedPortrait(owner):EnemyPortraitRect;
                        CombatCheck(r.y>=77&&r.yMax<=portrait.y-2,"Intent clears HUD and its owner's head: "+count+"/"+owner+"/"+i);
                        if(count>1){var cell=GroupCell(owner);CombatCheck(r.x>=cell.x&&r.xMax<=cell.xMax,"Intent stays within its own enemy lane");}
                        CombatCheck(combatHudTargets.Any(t=>t.key=="enemy:"+owner+":intent:"+i),"Controller can inspect individual action "+owner+"/"+i);
                        HandleCombatPointer(r.center,false,false,false);
                        yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                        CombatCheck(combatEffectTooltipTitle==actions[i].title,"Hover finds the exact action for enemy "+owner+"/"+i);
                    }
                }
            }
            PrepareIntentEncounter(new[]{"last_dealer"},true);yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.2f);
            CombatCheck(EnemyIntents(0).Count==4,"Dealer shows all four actions including curse, Weak and banishment");
            foreach(var large in new[]{false,true})
            {
                profile.largeIntents=large;
                for(var i=0;i<4;i++){var r=IntentActionRect(0,i);CombatCheck(r.y>=77&&r.yMax<=EnemyPortraitRect.y-2,"Boss actions clear the HUD and head at both icon sizes");}
            }
            profile.largeIntents=false;
            var endState=JsonUtility.ToJson(combat.CaptureCheckpoint());
            OpenCombatHudFocus(2);var visited=new System.Collections.Generic.HashSet<string>();
            for(var i=0;i<combatHudTargets.Count;i++){visited.Add(combatHudFocusKey);MoveCombatHudFocus(1,false);}
            CombatCheck(Enumerable.Range(0,4).All(i=>visited.Contains("enemy:0:intent:"+i)),"Controller reaches every boss action");
            CombatCheck(endState==JsonUtility.ToJson(combat.CaptureCheckpoint()),"Intent inspection never resolves the enemy turn");
            combatHudInspectActive=false;
            PrepareIntentEncounter(new[]{"collector"});combat.intentValue=22;InvalidateEnemyIntents();
            yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.2f);
            var original=EnemyIntents(0).Single(a=>a.type==EnemyActionType.Attack);
            CombatCheck(original.Icon==1,"Initial predicted attack uses sword tier");
            combat.enemy.weak=2;InvalidateEnemyIntents();yield return new WaitForSecondsRealtime(.05f);
            var weakened=EnemyIntents(0).Single(a=>a.type==EnemyActionType.Attack);
            CombatCheck(weakened.amount==16&&weakened.Icon==1,"Weak updates exact damage immediately");
            var requestedAt=Time.unscaledTime;combat.intentValue=20;InvalidateEnemyIntents();var transitionDeadline=requestedAt+3;
            do {yield return new WaitForEndOfFrame();}
            while((!intentIconMotions.TryGetValue("0:1",out var observed)||observed.changedAt<requestedAt)&&Time.unscaledTime<transitionDeadline);
            // Verify the transition started for this change. A slow QA frame may
            // observe it after its 0.23-second visual duration has already elapsed.
            CombatCheck(EnemyIntents(0).Last().Icon==0&&intentIconMotions.TryGetValue("0:1",out var transition)&&transition.oldIcon==1&&transition.changedAt>=requestedAt,"Threat change starts a short icon transition");
            var hover=IntentActionRect(0,1);HandleCombatPointer(hover.center,false,false,false);
            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            CombatCheck(combatEffectTooltipTitle=="ATTACK","Action tooltip appears beside the hovered intent");
            CombatCheck(LoadAuthoredArt("Art/UI/MasterPolish/EnemyIntents")!=null,"Cohesive intent atlas is imported");
            PrepareIntentEncounter(new[]{"last_dealer"},true);yield return WaitForCombatQueue();
        }
    }
}
