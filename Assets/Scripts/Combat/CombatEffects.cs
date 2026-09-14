using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Combat
{
    public enum CombatEffectDuration { Combat, TurnEnd, NextTurn }

    // A fighter owns its effects. No shared enemy-index dictionary or UI-owned rules.
    [Serializable] public sealed class CombatEffectState
    {
        public string id,source;
        public int value;
        public CombatEffectDuration duration;
        public CombatEffectState Copy()=>(CombatEffectState)MemberwiseClone();
    }

    public sealed partial class CombatState
    {
        [NonSerialized] private HashSet<string> resolvingEffects;
        // Presentation receipt ONLY. Do not route through TriggerEffect: doing so
        // would let presentation changes activate gameplay replay modifiers.
        // Receipt-only path for legacy relic rules. Do not wrap these effects in
        // RelicTrigger/TriggerEffect: that would make old rules participate in
        // gameplay replay modifiers and silently rebalance them.
        private void RelicPresentationPulse(string id)=>Emit(CombatEventKind.RelicTrigger,1,true,null,id);
        private void PassiveSigilPresentationPulse(SigilKind kind)
        {
            var slot=sigils.IndexOf(kind);if(slot<0)return;
            var previous=feedbackSigilSlot;feedbackSigilSlot=slot;
            try{Emit(CombatEventKind.Status,0,true,null,"TRIGGER:SIGIL "+kind.ToString().ToUpperInvariant());}
            finally{feedbackSigilSlot=previous;}
        }
        private void PresentationPulse(string cardId)
        {
            var card=Core.GameContent.Find(cardId);
            Emit(CombatEventKind.Status,0,true,card,"TRIGGER:"+(card?.name??cardId.Replace('_',' ').ToUpperInvariant()));
        }
        public int EffectValue(FighterState owner,string id)=>owner.effects?.FirstOrDefault(e=>e.id==id)?.value??0;
        private void GrantEffect(FighterState owner,string id,int value,string source,CombatEffectDuration duration)
        {
            if(value<=0)return;owner.effects??=new();var state=owner.effects.FirstOrDefault(e=>e.id==id);
            if(state==null){state=new CombatEffectState{id=id,source=source,duration=duration};owner.effects.Add(state);}
            state.value+=value;Emit(CombatEventKind.Status,value,owner==player,null,id.Replace('_',' ').ToUpperInvariant());
            if(owner==player&&id is "reverberation" or "adaptation" or "chimera" or "foresight" or "afterlife" or "ferocity" or "preparation" or "phantom_edge" or "resolve")RelicBuff(id);
            else if(owner==enemy&&id is "reaped" or "gravemark" or "wither" or "condemned" or "rupture" or "death_knell")RelicDebuffApplied();
        }
        private int ConsumeEffect(FighterState owner,string id)
        {
            var state=owner.effects?.FirstOrDefault(e=>e.id==id);if(state==null)return 0;
            owner.effects.Remove(state);Emit(CombatEventKind.Status,-state.value,owner==player,null,id.Replace('_',' ').ToUpperInvariant());return state.value;
        }
        private void ExpireEffects(CombatEffectDuration duration)
        {
            foreach(var effect in (player.effects??new()).Where(e=>e.duration==duration).ToArray())ConsumeEffect(player,effect.id);
            ForEachLivingEnemy(()=>{foreach(var effect in (enemy.effects??new()).Where(e=>e.duration==duration).ToArray())ConsumeEffect(enemy,effect.id);});
        }
        private void TriggerEffect(string id,Action resolve,bool playerSide=true)
        {
            resolvingEffects??=new();var key=(playerSide?"P:":"E:"+EnemyContextIndex+":")+id;
            // Guard only the active causal chain: later independent triggers are allowed.
            if(!resolvingEffects.Add(key))return;
            try{var repeats=TriggerRepeats(id,playerSide);Emit(CombatEventKind.Status,0,playerSide,null,"TRIGGER:"+(id.StartsWith("unbreakable_spirit_")?"unbreakable_spirit":id).Replace('_',' ').ToUpperInvariant());resolve();if(repeats>0){repeatedTriggerDepth++;try{for(var n=0;n<repeats;n++)resolve();}finally{repeatedTriggerDepth--;}}}
            finally{resolvingEffects.Remove(key);}
        }
        private int RallyBuffBonus()
        {
            var bonus=EffectValue(player,"rallying_guard");var total=0;if(bonus>0)TriggerEffect("rallying_guard",()=>{ConsumeEffect(player,"rallying_guard");total+=bonus;});return total;
        }
        private void ResolveAttackRetaliate(int captured)
        {
            if(captured<=0||player.hp<=0)return;
            DamageEnemyRaw(captured,"RETALIATE");RelicMechanic("Retaliate");memory.retaliateTriggers++;memory.retaliateTriggeredTurn++;
            if(memory.retributionPower>0)TriggerEffect("retribution",()=>GainStrength(memory.retributionPower,true));
            if(memory.holdLinePower>0)TriggerEffect("hold_the_line",()=>GainBlock(memory.holdLinePower));
            var block=EffectValue(player,"challenge_them_all");if(block>0)TriggerEffect("challenge_them_all",()=>{ConsumeEffect(player,"challenge_them_all");GainBlock(block);});
        }
        private void ResolveEnemyAttack(Action attack)
        {
            if(EffectValue(player,"brace_for_impact")>0)TriggerEffect("brace_for_impact",()=>GainRetaliate(EffectValue(player,"brace_for_impact")));
            // Capture once per intent, before its hits. Retaliate earned by a broken
            // shield during this attack belongs to the NEXT attack, not this one.
            var captured=retaliation;
            if(captured<=0){attack();return;}
            var attacker=EnemyContextIndex;retaliation=0;Emit(CombatEventKind.Status,-captured,true,null,"RETALIATE");
            // Resolve the intent exactly once; reactions cannot replay enemy attacks.
            attack();
            InspectEnemy(attacker,()=>{TriggerEffect("retaliate",()=>ResolveAttackRetaliate(captured));return 0;});
        }
        private void OnEnemyBreaksBlock()
        {
            var amount=EffectValue(player,"steel_through_pain");if(amount>0)TriggerEffect("steel_through_pain",()=>{ConsumeEffect(player,"steel_through_pain");GainRetaliate(amount);});
        }
    }
}
