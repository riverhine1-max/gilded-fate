using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;
namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private IEnumerator RunRemainingInteractionChecks()
        {
            yield return WaitForCombatQueue();
            MajorDrag(2,EnemyDropZone.center);yield return WaitForCombatQueue();
            CombatCheck(combat.EffectValue(combat.enemy,"reaped")==50,"Reaper's Mark uses attack-style targeting and release");
            PrepareCombatCheck("deaths_echo",5);yield return WaitForCombatQueue();MajorDrag(2,SkillDropZone.center);yield return WaitForCombatQueue();
            CombatCheck(combat.EffectValue(combat.player,"deaths_echo")==6,"Death's Echo Skill releases to play");
            PrepareCombatCheck("soul_dominion",5);yield return WaitForCombatQueue();MajorDrag(2,SkillDropZone.center);yield return WaitForCombatQueue();
            CombatCheck(combat.memory.remaining.soulDominion==1,"Soul Dominion Power releases to play");
            PrepareCombatCheck("soul_infusion",5);yield return WaitForCombatQueue();MajorDrag(2,SkillDropZone.center);yield return new WaitForSecondsRealtime(.6f);
            CombatCheck(combat.ChoiceCards.Count>0&&choicePresented,"Soul Infusion opens exact-card picker");
            var card=combat.ChoiceCards.First();choiceSelected=card;yield return WaitForCombatQueue();CombatCheck(combat.CombatCardModification(card).Contains("Soulbound"),"Soulbound selected card persists");
            PrepareCombatCheck("sigil_mutation",5);combat.sigils.Add(SigilKind.Ember);combat.memory.remaining.accessibleSigils=127;yield return WaitForCombatQueue();
            MajorDrag(2,SkillDropZone.center);yield return new WaitForSecondsRealtime(.6f);CombatCheck(combat.ChoiceOptions.Count==1,"Mutation first chooses owned Sigil");
            choiceOptionSelected=combat.ChoiceOptions[0];yield return new WaitForSecondsRealtime(.6f);CombatCheck(combat.ChoiceOptions.Contains("RUIN SIGIL"),"Mutation second stage offers unlocked Special Sigils");
            choiceOptionSelected="RUIN SIGIL";yield return WaitForCombatQueue();CombatCheck(combat.sigils[0]==SigilKind.Ruin&&combat.memory.sigilActivations==2,"Mutation activates replacement twice without re-paying");
            PrepareCombatCheck("grave_execution",5);combat.enemy.effects.Add(new CombatEffectState{id="gravemark",source="mark_of_the_grave",value=4});yield return WaitForCombatQueue();var hp=combat.enemy.hp;
            MajorDrag(2,EnemyDropZone.center);yield return WaitForCombatQueue();CombatCheck(combat.enemy.hp==hp-20&&combat.EffectValue(combat.enemy,"gravemark")==0,"Grave Execution drag-release consumes marks for four hits");
            Debug.Log($"[Gilded Fate Remaining] {combatInteractionChecks} checks · {combatInteractionFailures} failures");
        }
    }
}
