using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    [Serializable]
    public sealed class CombatCheckpoint
    {
        public int version=6;
        public CombatState state;

        public bool TryRestore(out CombatState restored,out string reason)
        {
            restored=null;
            if((version<1||version>6)||state==null){reason="Unsupported or missing combat checkpoint.";return false;}
            state.activeAspects??=new();
            // Unity's inline serializer materializes an absent nested class as empty data.
            // Only that exact empty sentinel is normalized; malformed active plays still fail.
            var pending=state.pendingPlay;
            if(pending!=null&&pending.copiesRemaining==0&&pending.resolvedCopies==0&&!pending.effectStarted&&!pending.gilded&&pending.choice==CardChoiceKind.None&&string.IsNullOrEmpty(pending.card?.id))state.pendingPlay=null;
            if(!state.ValidateCheckpoint(out reason))return false;
            restored=state.CopyForCheckpoint();
            // Nullable hero tags are definition data, not Unity-serializable run state.
            foreach(var card in restored.AllOwnedCards()){var definition=Array.Find(GameContent.Cards,c=>c.id==card.id);if(definition!=null)card.hero=definition.hero;}
            if(version<3)restored.RefreshLegacyRareRules();
            if(version<4)foreach(var card in restored.AllOwnedCards())if(card.id=="soul"){card.value=card.upgraded?5:3;card.text=card.upgraded?"Deal 5 damage. Draw 1. Exhaust.":"Deal 3 damage. Draw 1. Exhaust.";card.upgradeText="Deal 5 damage. Draw 1. Exhaust.";card.upgradeValue=5;}
            if(version<5)restored.RefreshReaperRules();
            if(version<6)restored.RestoreLegacyAspectOwnership();
            foreach(var card in restored.AllOwnedCards())if(card.id=="grand_convergence")
            {var definition=GameContent.Find(card.id);card.text=definition.text;card.upgradeText=definition.upgradeText;}
            return true;
        }
    }

    public sealed partial class CombatState
    {
        public IEnumerable<CardDef> AllOwnedCards()
        {
            foreach(var card in draw)yield return card;
            foreach(var card in hand)yield return card;
            foreach(var card in discard)yield return card;
            foreach(var card in exhaust)yield return card;
            foreach(var card in activeAspects??new())yield return card;
            if(pendingPlay?.card!=null)yield return pendingPlay.card;
        }
        public CombatCheckpoint CaptureCheckpoint()=>new CombatCheckpoint{state=CopyForCheckpoint()};
        internal void RefreshLegacyRareRules()
        {
            foreach(var card in AllOwnedCards())
            {
                if(card.id is not ("unbreakable_spirit" or "executioners_cleave" or "eye_for_an_eye" or "arcane_overload"))continue;
                var updated=GameContent.InspectionVariant(card,card.upgraded);
                var oldCost=card.id=="executioners_cleave"?3:card.id=="unbreakable_spirit"?(card.upgraded?2:3):2;
                // Keep any per-copy cost adjustment, physical identity and Binding.
                card.cost=Math.Max(0,updated.cost+card.cost-oldCost);
                card.kind=updated.kind;card.effect=updated.effect;card.value=updated.value;card.secondary=updated.secondary;
                card.text=updated.text;card.upgradeText=updated.upgradeText;card.keywords=updated.keywords;
                card.upgradeCost=updated.upgradeCost;card.upgradeValue=updated.upgradeValue;card.upgradeSecondary=updated.upgradeSecondary;
                card.exhaust|=updated.exhaust;
            }
            // Eye becoming a Skill can remove the final eligible Attack from an
            // older Grave's Edge prompt. Finish that paid card without replaying it.
            if(AwaitingChoice&&ChoiceCards.Count==0&&ChoiceOptions.Count==0){pendingPlay.choice=CardChoiceKind.None;ContinueCardResolution();}
        }
        internal void RefreshReaperRules()
        {
            foreach(var card in AllOwnedCards())
            {
                if(card.id is not ("army_of_the_dead" or "soul_reaper" or "endless_harvest" or "devour_the_dead" or "death_incarnate" or "final_procession" or "grim_ascension" or "claim_the_fallen" or "soul_conversion" or "soulbound_tome" or "eternal_souls" or "reapers_calling" or "stand_firm"))continue;
                var updated=GameContent.InspectionVariant(card,card.upgraded);
                if(card.id=="army_of_the_dead"&&card.upgraded)card.cost++;
                card.value=updated.value;card.secondary=updated.secondary;card.text=updated.text;card.upgradeText=updated.upgradeText;
                card.upgradeCost=updated.upgradeCost;card.upgradeValue=updated.upgradeValue;card.upgradeSecondary=updated.upgradeSecondary;card.keywords=updated.keywords;card.exhaust|=updated.exhaust;
            }
            if(memory.grimAscensionCadence>0){memory.grimAscensionBonus=memory.grimAscensionCadence==4?2:1;memory.grimAscensionCadence=0;}
        }
        internal CombatState CopyForCheckpoint()
        {
            SaveEnemyContext();var copy=(CombatState)MemberwiseClone();
            copy.shardMemory=(shardMemory??new()).Copy();copy.player=player.Copy();copy.enemy=enemy.Copy();copy.memory=memory.Copy();copy.relics=new List<string>(relics);copy.sigils=new List<SigilKind>(sigils??new());
            copy.draw=CopyPile(draw);copy.hand=CopyPile(hand);copy.discard=CopyPile(discard);copy.exhaust=CopyPile(exhaust);copy.activeAspects=CopyPile(activeAspects??new());
            copy.opponents=new List<CombatOpponent>();foreach(var opponent in opponents??new())copy.opponents.Add(opponent.Copy());copy.LoadEnemyContext(enemyContextIndex);
            copy.intentPreviewSink=null;copy.pendingPlay=pendingPlay?.Copy();copy.events=new List<CombatEvent>();copy.resolvingEffects=null;copy.repeatedTriggerDepth=copy.relicDamageDepth=0;
            return copy;
        }
        private static List<CardDef> CopyPile(List<CardDef> source)
        {
            var copy=new List<CardDef>(source.Count);foreach(var card in source)copy.Add(card.Copy());return copy;
        }
        internal bool ValidateCheckpoint(out string reason)
        {
            reason="Invalid combat checkpoint.";
            if(player==null||enemy==null||memory==null||draw==null||hand==null||discard==null||exhaust==null||relics==null||sigils==null||sigils.Count>SigilCapacity)return false;
            if(!ValidateRemainingMemory()||!ValidateRelicMemory())return false;
            if(activeAspects?.Any(c=>c==null||c.kind!=CardKind.Power)==true)return false;
            if(memory.extraSigilSlots<0||memory.extraSigilSlots>1000||memory.warlordReadyTurn<0||memory.gildedWarlord<0||memory.unmovable<0||memory.brandOfRuin<0||memory.conquestAttacksThisTurn<0||memory.conquestCadence<0)return false;
            foreach(var sigil in sigils)if(!Enum.IsDefined(typeof(SigilKind),sigil))return false;
            if(player.maxHp<=0||enemy.maxHp<=0||player.hp<0||player.hp>player.maxHp||enemy.hp<0||enemy.hp>enemy.maxHp||!ValidFighterEffects(player)||!ValidFighterEffects(enemy))return false;
            if(!Enum.IsDefined(typeof(CombatPhase),phase)||!Enum.IsDefined(typeof(HeroId),hero)||!Enum.IsDefined(typeof(IntentKind),intent))return false;
            if(turn<1||energy<0||resonance<0||memory.randomState==0||nextInstanceId>1000000)return false;
            if(!Array.Exists(WorldContent.Enemies,e=>e.id==enemyId))return false;
            var ids=new HashSet<int>();var count=0;
            foreach(var card in AllOwnedCards())
            {
                if(card==null||card.instanceId<=0||card.instanceId>nextInstanceId||!ids.Add(card.instanceId)||++count>1000)return false;
                if(!Array.Exists(GameContent.Cards,c=>c.id==card.id)||card.cost<0||string.IsNullOrEmpty(card.text))return false;
                if(card.specialModificationKind==SpecialModificationKind.None&&!string.IsNullOrEmpty(card.specialModification)||card.specialModificationKind!=SpecialModificationKind.None&&string.IsNullOrEmpty(card.specialModification))return false;
            }
            if(count==0)return false;
            if(opponents?.Count>0){if(opponents.Count>4||enemyContextIndex<0||enemyContextIndex>=opponents.Count)return false;foreach(var opponent in opponents){if(opponent?.fighter==null||opponent.fighter.maxHp<1||opponent.fighter.hp<0||opponent.fighter.hp>opponent.fighter.maxHp||!ValidFighterEffects(opponent.fighter)||!Array.Exists(WorldContent.Enemies,e=>e.id==opponent.id&&!e.elite&&!e.boss))return false;}}
            if(pendingPlay!=null)
            {
                if(phase!=CombatPhase.Player||pendingPlay.card==null||pendingPlay.copiesRemaining<1||pendingPlay.copiesRemaining>32||pendingPlay.resolvedCopies<0||pendingPlay.additionalAttackCount<0||pendingPlay.attackEffectBonus<0||!pendingPlay.effectStarted)return false;
                if(pendingPlay.choice is CardChoiceKind.ExpansionCard or CardChoiceKind.ExpansionOption){if(!ValidateRemainingChoice())return false;}
                if(pendingPlay.choice==CardChoiceKind.None||!Enum.IsDefined(typeof(CardChoiceKind),pendingPlay.choice)||ChoiceCards.Count==0&&ChoiceOptions.Count==0)return false;
                if(pendingPlay.choice==CardChoiceKind.DiscardFromHand&&pendingPlay.card.id!="clear_mind")return false;
                if(pendingPlay.choice==CardChoiceKind.ExhaustFromHand&&pendingPlay.card.id!="recycle")return false;
                if(pendingPlay.choice==CardChoiceKind.ExhaustCurseFromHand&&pendingPlay.card.id is not ("consume_darkness" or "void_sigil"))return false;
                if(pendingPlay.choice==CardChoiceKind.ExhaustSoulFromHand&&pendingPlay.card.id is not ("soul_offering" or "soul_feast" or "soul_exchange" or "soul_rend"))return false;
                if(pendingPlay.choice==CardChoiceKind.ReturnAttackFromExhaust&&pendingPlay.card.id!="graves_edge")return false;
                if(pendingPlay.choice==CardChoiceKind.ReturnSkillFromExhaust&&pendingPlay.card.id!="call_from_beyond")return false;
                if(pendingPlay.choice==CardChoiceKind.ReturnFromDiscard)return false;
                if(pendingPlay.choice==CardChoiceKind.AdaptMode&&pendingPlay.card.id!="adapt")return false;
                if(pendingPlay.choice==CardChoiceKind.SigilMode&&pendingPlay.card.id is not ("blasphemous_ritual" or "void_sigil" or "first_ritual" or "sigil_of_malice"))return false;
                if(pendingPlay.choice==CardChoiceKind.SigilSlot&&(pendingPlay.card.id!="hexed_reverberation"||!pendingPlay.card.upgraded))return false;
                if(pendingPlay.choice==CardChoiceKind.BuffToDouble&&pendingPlay.card.id!="limit_break")return false;
                if(pendingPlay.choice==CardChoiceKind.BuffToGain&&pendingPlay.card.id!="rally_the_fallen")return false;
            }
            if(IsOver&&phase!=CombatPhase.Finished)return false;
            reason="";return true;
        }
        private static bool ValidFighterEffects(FighterState fighter)
        {
            if(fighter.attemptedAttackDamage<0)return false;
            if(fighter.effects==null)return true; // Saves written before effects existed.
            var ids=new HashSet<string>();
            foreach(var effect in fighter.effects)
                if(effect==null||string.IsNullOrWhiteSpace(effect.id)||!ids.Add(effect.id)||effect.value<=0||!Enum.IsDefined(typeof(CombatEffectDuration),effect.duration)||GameContent.Find(effect.source)==null&&!Array.Exists(GameContent.Relics,r=>r.id==effect.source))return false;
            return true;
        }
    }
}
