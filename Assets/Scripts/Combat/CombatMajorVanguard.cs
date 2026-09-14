using System;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    public sealed partial class CombatState
    {
        private int LivingEnemyCount=>Enumerable.Range(0,EnemyCount).Count(IsLivingTarget);
        private int DifferentBuffCount=>(player.strength>0?1:0)+(player.fortify>0?1:0)+(retaliation>0?1:0);
        private void ApplyAttackCountReceipt(PendingCardPlay play)
        {
            var extra=play.additionalAttackCount;play.attackCountReceipt=1+extra;
            if(extra>0&&play.resolvedCopies==1)
            {
                var before=memory.conquestAttacksThisTurn;memory.conquestAttacksThisTurn+=extra;
                if(memory.conquestCadence>0)
                {
                    var crossed=memory.conquestAttacksThisTurn/memory.conquestCadence-before/memory.conquestCadence;
                    if(crossed>0){play.copiesRemaining+=crossed;ExpansionPowerPulse("relentless_conquest");}
                }
                before=shardMemory.attacks;shardMemory.attacks+=extra;
                if(activeShardId=="rhythm")
                {
                    var cadence=activeShardFractured?3:4;var crossed=shardMemory.attacks/cadence-before/cadence;
                    if(crossed>0){play.copiesRemaining+=crossed;ShardPulse(card:play.card,amount:crossed);}
                }
            }
            memory.attacksThisTurn+=play.attackCountReceipt;play.additionalAttackCount=0;memory.firstAttackPlayed=true;
        }
        private void OnEnemy(int index,Action action)
        {
            if(!IsLivingTarget(index))return;SaveEnemyContext();var previous=EnemyContextIndex;LoadEnemyContext(index);
            try{action();SaveEnemyContext();}finally{LoadEnemyContext(previous);}
        }
        private int RandomOtherEnemy(int excluded)
        {
            var candidates=Enumerable.Range(0,EnemyCount).Where(i=>i!=excluded&&IsLivingTarget(i)).ToArray();
            return candidates.Length==0?-1:candidates[NextRandom(candidates.Length)];
        }
        private bool ResolveMajorVanguardCard(CardDef card)
        {
            if(!GameContent.MajorVanguardCardIds.Contains(card.id))return false;
            var value=card.value;var secondary=card.secondary;var heavy=memory.attacksThisTurn==0;
            if(card.kind==CardKind.Attack)value+=card.permanentDamageBonus;
            if(card.effect==EffectKind.Block)value+=card.permanentBlockBonus;
            ApplySpecialBase(card,ref value,ref secondary,heavy);
            var target=EnemyContextIndex;
            switch(card.id)
            {
                case "sweeping_blade":case "gilded_sweep":
                    var perBuff=card.id=="sweeping_blade"?2:3;var buffBonus=DifferentBuffCount*perBuff;
                    ForEachLivingEnemy(()=>DamageEnemy(value+buffBonus,card));break;
                case "crushing_sweep":
                    ForEachLivingEnemy(()=>DamageEnemy(value,card));if(heavy)ForEachLivingEnemy(()=>ApplyEnemyDebuff(EffectKind.Vulnerable,1));break;
                case "vengeful_sweep":
                    var sweeps=memory.retaliateTriggeredTurn>0?2:1;
                    for(var hit=0;hit<sweeps;hit++){var h=hit;ForEachLivingEnemy(()=>DamageEnemy(value,card,h,sweeps));}break;
                case "shockwave":ForEachLivingEnemy(()=>{ApplyEnemyDebuff(EffectKind.Weak,value);ApplyEnemyDebuff(EffectKind.Vulnerable,value);});break;
                case "break_formation":ForEachLivingEnemy(()=>{DamageEnemy(value,card);if(enemy.block>0){var lost=Math.Min(enemy.block,secondary);enemy.block-=lost;Emit(CombatEventKind.Block,lost,false,card,"BLOCK REMOVED");}});break;
                case "war_cry":GainStrength(value);if(LivingEnemyCount>=2)GainFortify(1);break;
                case "whirlwind_guard":
                    var hitEnemies=0;ForEachLivingEnemy(()=>{DamageEnemy(value,card);hitEnemies++;});
                    if(hitEnemies>0)GainBlock(hitEnemies*secondary,true,card);break;
                case "concussive_swing":
                    ForEachLivingEnemy(()=>DamageEnemy(value,card));var highest=Enumerable.Range(0,EnemyCount).Where(IsLivingTarget).OrderByDescending(i=>EnemyAt(i).hp).DefaultIfEmpty(-1).First();
                    OnEnemy(highest,()=>ApplyEnemyDebuff(EffectKind.Weak,secondary));break;
                case "challenge_them_all":GainRetaliate(value*LivingEnemyCount);GrantEffect(player,card.id,4,card.id,CombatEffectDuration.Combat);break;
                case "rallying_guard":GainBlock(value,true,card);GrantEffect(player,card.id,1,card.id,CombatEffectDuration.TurnEnd);break;
                case "cleaving_momentum":
                    var priorCount=pendingPlay.additionalAttackCount;
                    // Damage modifiers must see this Attack's weighted cadence before
                    // its hits resolve, not only after the final hit has finished.
                    pendingPlay.additionalAttackCount=priorCount+Math.Max(0,LivingEnemyCount-1);
                    var count=0;ForEachLivingEnemy(()=>{DamageEnemy(value,card);count++;});
                    // Extra count is a receipt, never extra CardPlayed/Resolve events.
                    pendingPlay.additionalAttackCount=priorCount+Math.Max(0,count-1);break;
                case "press_the_advantage":
                    var repeat=EnemyDebuffCount()>0;DamageEnemy(value,card);
                    if(repeat)OnEnemy(RandomOtherEnemy(target),()=>DamageEnemy(value,card));break;
                case "steel_through_pain":case "brace_for_impact":
                    GainBlock(value,true,card);GrantEffect(player,card.id,secondary,card.id,CombatEffectDuration.NextTurn);break;
                case "chain_reaction_vanguard":
                    var next=target;var growing=value;
                    // Every continuation requires a real kill, so the chain is bounded
                    // by the living roster, not an arbitrary damage/combo cap.
                    while(IsLivingTarget(next))
                    {
                        var victim=next;OnEnemy(victim,()=>DamageEnemy(growing,card));
                        if(IsLivingTarget(victim))break;growing+=secondary;next=RandomOtherEnemy(victim);
                    }break;
                case "shatter_the_ranks":ForEachLivingEnemy(()=>DamageEnemy(value,card));break;
                case "rally_the_fallen":
                    if(BuffOptions().Count>0){pendingPlay.choice=CardChoiceKind.BuffToGain;pendingPlay.choiceFollowupValue=value;}break;
                case "unrelenting_assault":
                    var struck=0;ForEachLivingEnemy(()=>{DamageEnemy(value,card);struck++;});
                    GrantEffect(player,card.id,struck*secondary,card.id,CombatEffectDuration.TurnEnd);break;
                case "turn_their_strength":GainStrength(Math.Max(0,enemy.strength)*value/100);break;
            }
            return true;
        }
    }
}
