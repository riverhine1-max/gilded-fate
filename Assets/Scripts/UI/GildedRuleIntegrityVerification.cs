using System.Collections;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureRuleIntegrityCapture(string mode)
        {
            if(!mode.StartsWith("integrity-"))return false;
            combatTestInput=true;PrepareIntegrityGallery(mode);
            return true;
        }
        private void PrepareIntegrityGallery(string mode)
        {
            PrepareCombatCheck(mode=="integrity-vanguard"?"armored_strike":"soul_guard",5);
            combat.hand.Clear();combat.energy=99;
            var aspects=mode=="integrity-vanguard"?new[]{"relentless","unbreakable_spirit"}:new[]{"death_incarnate","soulbound_tome"};
            foreach(var id in aspects){var c=GameContent.Find(id).Copy();c.instanceId=++combat.nextInstanceId;combat.hand.Add(c);combat.Play(c);}
            var ids=mode=="integrity-vanguard"?new[]{"strike","defend","armored_strike","overhead_strike","battle_rush"}:new[]{"soul","soul","soul_guard","soul_carver","reapers_momentum","soul","soul"};
            foreach(var id in ids){var c=GameContent.Find(id).Copy();c.instanceId=++combat.nextInstanceId;combat.hand.Add(c);}
            combat.player.fortify=3;combat.player.strength=2;combat.enemy.vulnerable=1;combat.energy=3;
            if(mode=="integrity-pile")
            {var curse=GameContent.Find("doom").Copy();curse.instanceId=++combat.nextInstanceId;combat.exhaust.Add(curse);}
            combat.TakeEvents();RestoreCombatPresentation();combatPointer=new Vector2(CombatWidth*.5f,210);
            if(mode=="integrity-pile")pileOpen=2;
        }
        private IEnumerator RunRuleIntegrityChecks()
        {
            combatTestInput=true;profile.fastMode=false;profile.cardAnimationSpeed=1;
            foreach(var id in new[]{"death_incarnate","battle_temper","soulbound_tome"})
            {
                PrepareCombatCheck(id,5);yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.25f);
                combat.relics.Add("bone_charm");var card=combat.hand[2];var energy=combat.energy;var block=combat.player.block;
                var point=CardPickPoint(2);HandleCombatPointer(point,true,true,false);
                HandleCombatPointer(SkillDropZone.center,false,true,false);HandleCombatPointer(SkillDropZone.center,false,false,true);
                yield return WaitForCombatQueue();
                CombatCheck(combat.activeAspects.Any(c=>c.instanceId==card.instanceId)&&!combat.exhaust.Any(c=>c.instanceId==card.instanceId),"Release installs Aspect separately: "+id);
                CombatCheck(combat.energy<energy&&combat.player.block==block&&combat.memory.exhaustsThisTurn==0,"One payment, no Dissipate listeners: "+id);
                CombatCheck(PlayerEffectChips().Any(c=>c.title==PowerIconCatalog.Title(card.name)),"Installed Aspect has correct visible HUD identity: "+id);
                CombatCheck(CardTypeLabel(card)=="ASPECT","Card type is Aspect: "+id);
                var json=JsonUtility.ToJson(combat.CaptureCheckpoint());var saved=JsonUtility.FromJson<CombatCheckpoint>(json);
                CombatCheck(saved.TryRestore(out var loaded,out _)&&loaded.activeAspects.Count==1&&loaded.exhaust.Count==0,"Unity save/load preserves active Aspect ownership: "+id);
            }
            foreach(var id in new[]{"armored_strike","hollow_scythe","defend"})
            {
                PrepareCombatCheck(id,5);yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.25f);
                if(id=="hollow_scythe")
                {var soul=GameContent.Find("soul").Copy();soul.instanceId=combat.hand[0].instanceId;combat.hand[0]=soul;}
                combat.player.fortify=3;combat.player.strength=2;combat.enemy.vulnerable=1;cardPreviewCache.Clear();
                var card=combat.hand[2];var preview=CardPreview(card);var text=StripRichTags.Replace(LiveCardRules(card,preview),"");
                CombatCheck(preview.cardBlock>0&&text.Contains(preview.cardBlock+" Block"),"Displayed numerical Block includes Fortify: "+id);
                var block=combat.player.block;var hp=combat.enemy.hp;var point=CardPickPoint(2);var destination=combat.RequiresEnemyTarget(card)?EnemyDropZone.center:SkillDropZone.center;
                HandleCombatPointer(point,true,true,false);HandleCombatPointer(destination,false,true,false);HandleCombatPointer(destination,false,false,true);
                yield return WaitForCombatQueue();
                CombatCheck(combat.player.block-block==preview.block&&hp-combat.enemy.hp==preview.hpDamage,"Displayed preview matches real drag-release resolution: "+id);
            }
            PrepareIntegrityGallery("integrity-aspect");yield return WaitForCombatQueue();
            var guard=combat.hand.First(c=>c.id=="soul_guard");var guardText=StripRichTags.Replace(LiveCardRules(guard,CardPreview(guard)),"");
            CombatCheck(guardText.Contains("27 Block")&&guardText.Contains("4 Souls")&&!guardText.Contains("per Soul"),"Soul Guard shows total for four Souls, not a misleading per-Soul multiplier");
            var carver=combat.hand.First(c=>c.id=="soul_carver");combat.hand.Remove(combat.hand.Last(c=>c.id=="soul"));cardPreviewCache.Clear();
            var sequence=CardPreview(carver);CombatCheck(sequence.hitDamage.Distinct().Count()>1&&StripRichTags.Replace(LiveCardRules(carver,sequence),"").Contains(sequence.DamageExpression),"Unequal multi-hit values remain visible in the card text");
            CombatCheck(!FormatCardRules("Power. Exhaust. Exhausted cards.").Contains("Exhaust")&&FormatCardRules("Power. Exhaust.").Contains("Aspect"),"Rules display Aspect / Dissipate terminology");
            CombatCheck(RuleKeywords.First(k=>k.term=="Fortify").detail.Contains("triggered effects"),"Fortify tooltip matches its canonical rule");
            foreach(var id in new[]{"second_wind","whirlwind_guard","iron_momentum"})
            {
                PrepareCombatCheck(id,5);combat.memory.powersInPlay=3;combat.memory.attacksThisTurn=2;combat.player.fortify=3;cardPreviewCache.Clear();
                var card=combat.hand[2];var preview=CardPreview(card);var text=StripRichTags.Replace(LiveCardRules(card,preview),"");
                CombatCheck(text.Contains(preview.cardBlock+" Block total.")&&!text.Contains(preview.cardBlock+" Block per")&&!text.Contains(preview.cardBlock+" Block for each"),"Resolved Block total is not presented as a second multiplier: "+id);
            }
            PrepareCombatCheck("blood_rush",5);yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.25f);
            combat.relics.Add("crimson_spur");var selfHp=combat.player.hp;
            HandleCombatPointer(CardPickPoint(2),true,true,false);HandleCombatPointer(SkillDropZone.center,false,true,false);HandleCombatPointer(SkillDropZone.center,false,false,true);
            yield return WaitForCombatQueue();
            CombatCheck(combat.player.hp<selfHp&&combat.player.strength==2&&combat.memory.temporaryStrength==0,"Crimson Spur gives exactly 2 persistent Strength from real drag-release self-damage");
            combat.EndTurn();ConsumeCombatEvents(combat.TakeEvents());
            CombatCheck(combat.player.strength==2,"Crimson Spur Strength survives turn cleanup in the Unity player");
            PrepareIntegrityGallery("integrity-pile");yield return WaitForCombatQueue();
            CombatCheck(pileOpen==2&&combat.exhaust.Count==1&&combat.activeAspects.Count==2,"Dissipate inspection opens with the real card only, excluding two active Aspects");
            PrepareIntegrityGallery("integrity-aspect");yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.25f);
            HandleCombatPointer(CardPickPoint(2),false,false,false);yield return new WaitForSecondsRealtime(.45f);
        }
    }
}
