using System.Collections;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureMajorCapture(string mode)
        {
            if(!mode.StartsWith("major-")&&!mode.StartsWith("rare-"))return false;
            if(mode is "major-hexer" or "major-reaper" or "major-wanderer"){screen=ScreenMode.Collection;bindingCaptureCards=(mode=="major-hexer"?GameContent.MajorHexerCardIds:mode=="major-reaper"?GameContent.MajorReaperCardIds:GameContent.MajorWandererCardIds).Select(GameContent.Find).ToArray();return true;}
            if(mode=="major-new-choice"){PrepareCombatCheck("sigil_mutation",5);combat.memory.extraSigilSlots=1;combat.sigils.AddRange(new[]{SigilKind.Ruin,SigilKind.Wither,SigilKind.Grave,SigilKind.Mirror});combat.memory.remaining.accessibleSigils=127;combat.PlayFree(GameContent.Find("sigil_mutation"));combat.TakeEvents();RestoreCombatPresentation();return true;}
            if(mode=="major-new-input"){PrepareCombatCheck("reapers_mark",5);return true;}
            if(mode is "major-vanguard" or "major-vanguard-last" or "rare-cards")
            {
                screen=ScreenMode.Collection;
                bindingCaptureCards=mode=="rare-cards"?new[]{"unbreakable_spirit","executioners_cleave","eye_for_an_eye","arcane_overload"}.SelectMany(id=>new[]{GameContent.Find(id),GameContent.Upgrade(GameContent.Find(id))}).ToArray():
                    GameContent.MajorVanguardCardIds.Skip(mode=="major-vanguard-last"?12:0).Select(GameContent.Find).ToArray();
                return true;
            }
            combatTestInput=true;
            if(mode is "major-effects" or "major-input")PrepareMajorGroup();
            else
            {
                PrepareCombatCheck(mode=="major-rally"?"rally_the_fallen":"executioners_cleave",5);
                combat.PlayFree(GameContent.Find("unbreakable_spirit"));
                if(mode=="rare-heavy-spent")combat.PlayFree(GameContent.Find("strike"));
                if(mode=="major-rally"){combat.player.strength=2;combat.player.fortify=3;combat.retaliation=4;combat.PlayFree(GameContent.Find("rally_the_fallen"));}
                combat.TakeEvents();RestoreCombatPresentation();
            }
            return true;
        }
        private void PrepareMajorGroup()
        {
            ConfigureShardCapture("group-four");
            for(var i=0;i<4;i++)
            {
                var f=combat.EnemyAt(i);f.hp=f.maxHp=300;f.block=0;
                f.strength=i+1;f.fortify=i+2;f.burn=i+3;f.marked=i+4;f.weak=1;f.vulnerable=2;f.frail=1;
            }
            combat.hand.Clear();foreach(var id in new[]{"sweeping_blade","challenge_them_all","rallying_guard","shatter_the_ranks","unrelenting_assault"})
            {var card=GameContent.Find(id).Copy();card.instanceId=++combat.nextInstanceId;combat.hand.Add(card);}
            combat.player.effects.Add(new CombatEffectState{id="brace_for_impact",source="brace_for_impact",value=2,duration=CombatEffectDuration.NextTurn});
            combat.player.effects.Add(new CombatEffectState{id="rallying_guard",source="rallying_guard",value=1,duration=CombatEffectDuration.TurnEnd});
            combat.energy=20;combat.TakeEvents();RestoreCombatPresentation();
        }
        private void MajorDrag(int cardIndex,Vector2 destination)
        {
            var start=CardPickPoint(cardIndex);HandleCombatPointer(start,true,true,false);
            HandleCombatPointer(destination,false,true,false);HandleCombatPointer(destination,false,false,true);
        }
        private IEnumerator RunMajorInteractionChecks()
        {
            yield return WaitForCombatQueue();
            for(var i=0;i<4;i++)
            {
                var owner=i;CombatEffectChip[] chips=null;WithEnemyPresentation(owner,()=>chips=EnemyEffectChips().ToArray());
                CombatCheck(chips.First(c=>c.title=="STRENGTH").value==i+1&&chips.First(c=>c.title=="BURN").value==i+3,"Effect values belong to enemy "+i);
                var portrait=GroupPresentedPortrait(i);CombatCheck(Mathf.Abs(portrait.center.x-GroupCell(i).center.x)<8,"Effects follow their own moving portrait "+i);
            }
            PrepareCombatCheck("eye_for_an_eye",5);yield return WaitForCombatQueue();
            combat.enemy.attemptedAttackDamage=22;combat.player.strength=30;combat.TakeEvents();ResetPlayerStatusPlayback();
            var hp=combat.enemy.hp;MajorDrag(2,EnemyDropZone.center);yield return WaitForCombatQueue();
            CombatCheck(combat.enemy.hp==hp-22,"Eye drag-release deals attempted damage without Strength inflation");
            CombatCheck(combat.exhaust.Any(c=>c.id=="eye_for_an_eye")&&combat.memory.attacksThisTurn==0,"Eye Exhausts as a Skill without spending Heavy");
            PrepareCombatCheck("executioners_cleave",5);yield return WaitForCombatQueue();
            combat.PlayFree(GameContent.Find("defend"));combat.PlayFree(GameContent.Find("living_armor"));combat.TakeEvents();
            CombatCheck(combat.PreviewCard(combat.hand[2]).conditionActive,"Heavy survives prior Skill and Power");
            hp=combat.enemy.hp;MajorDrag(2,EnemyDropZone.center);yield return WaitForCombatQueue();
            CombatCheck(combat.enemy.hp==hp-40,"First Attack drag-release deals Heavy 40");
            var spent=combat.PreviewCard(combat.hand[0]);CombatCheck(!spent.conditionActive&&spent.totalDamage==28,"Next Cleave shows inactive Heavy and 28 damage");
            PrepareCombatCheck("war_cry",5);yield return WaitForCombatQueue();
            combat.PlayFree(GameContent.Find("unbreakable_spirit"));combat.TakeEvents();ResetPlayerStatusPlayback();
            combat.PlayFree(GameContent.Upgrade(GameContent.Find("war_cry")));var facts=combat.TakeEvents();
            var duration=ConsumeCombatEvents(facts);var start=Time.unscaledTime;
            CombatCheck(ScheduledPowerPulse("P:UNBREAKABLE SPIRIT")>0,"First Spirit pulse starts immediately");
            CombatCheck(PresentedPlayerStatuses().strength==2&&PresentedPlayerStatuses().fortify==0,"First pulse precedes mirrored Fortify");
            yield return new WaitForSecondsRealtime(.22f);
            CombatCheck(ScheduledPowerPulse("P:UNBREAKABLE SPIRIT")>0,"Second Spirit pulse is independent, not overwritten");
            CombatCheck(PresentedPlayerStatuses().strength==2&&PresentedPlayerStatuses().fortify==2,"Fortify arrives before reverse-direction Strength");
            yield return new WaitForSecondsRealtime(.24f);
            CombatCheck(PresentedPlayerStatuses().strength==4&&PresentedPlayerStatuses().fortify==2,"Reverse Strength arrives after its pulse");
            CombatCheck(PlayerEffectChips().Single(c=>c.title=="UNBREAKABLE SPIRIT").counter=="0/2","Spirit HUD shows both directions used");
            yield return new WaitForSecondsRealtime(Mathf.Max(0,start+duration-Time.unscaledTime));
            PrepareCombatCheck("arcane_overload",5);yield return WaitForCombatQueue();combat.sigils.Add(SigilKind.Hex);
            hp=combat.enemy.hp;MajorDrag(2,EnemyDropZone.center);yield return WaitForCombatQueue();
            CombatCheck(combat.enemy.hp==hp-11&&combat.memory.sigilActivations==2&&combat.exhaust.Any(c=>c.id=="arcane_overload"),"Overload drag-release attacks, activates twice and Exhausts");
            PrepareCombatCheck("rally_the_fallen",5);yield return WaitForCombatQueue();combat.player.strength=3;combat.TakeEvents();ResetPlayerStatusPlayback();
            MajorDrag(2,SkillDropZone.center);yield return new WaitForSecondsRealtime(1.2f);
            CombatCheck(combat.ChoiceKind==CardChoiceKind.BuffToGain&&combat.ChoiceOptions.SequenceEqual(new[]{"STRENGTH"}),"Rally Skill drag-release offers only owned buffs");
            choiceOptionSelected="STRENGTH";yield return WaitForCombatQueue();
            CombatCheck(combat.player.strength==5&&!combat.AwaitingChoice,"Rally choice applies two stacks and resumes combat");
            PrepareMajorGroup();yield return WaitForCombatQueue();
            Debug.Log($"[Gilded Fate Major] {combatInteractionChecks} interaction checks · {combatInteractionFailures} failures");
        }
    }
}
