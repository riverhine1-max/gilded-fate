using System;
using System.Collections.Generic;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Serialized separately from relic/power bookkeeping. Natural plays, not replays,
    // advance these counters; every recursive trigger reserves its use BEFORE drawing.
    [Serializable] public sealed class ShardCombatMemory
    {
        public int plays,attacks,heavyAttacks,quickDraws,blockGains,bastions,burnApplications;
        public int debuffs,consumeDraws,exhausts,extraDraws,voidDraws,modifiedPlays,cycleMask,cycles;
        public int aftershockCharges,nextAttackBonus,blockProgress,nextTurnDraw,nextTurnEnergy;
        public bool silverDraw,afflictionReplay,ashTriggered;
        public List<int> ashEnemies=new();
        public ShardCombatMemory Copy(){var c=(ShardCombatMemory)MemberwiseClone();c.ashEnemies=new List<int>(ashEnemies??new());return c;}
    }

    public sealed partial class CombatState
    {
        public ShardCombatMemory shardMemory=new();
        [NonSerialized] private Queue<(int count,bool normal,bool soul)> queuedDraws;
        [NonSerialized] private bool drawingFromSoul,soulDrawRequest;
        private void DrawSoulCard(){soulDrawRequest=true;try{Draw(1,false);}finally{soulDrawRequest=false;}}
        private void Draw(int count,bool normalDraw)
        {
            if(count<=0||!normalDraw&&relics.Contains("stolen_hourglass"))return;
            var fromSoul=soulDrawRequest;soulDrawRequest=false;count=ShardExtraDraw(count,normalDraw);
            if(queuedDraws!=null){queuedDraws.Enqueue((count,normalDraw,fromSoul));return;}
            queuedDraws=new();queuedDraws.Enqueue((count,normalDraw,fromSoul));var budget=1000;
            try
            {
                while(queuedDraws.Count>0&&player.hp>0&&budget>0)
                {var request=queuedDraws.Dequeue();var draws=Math.Min(budget,request.count);budget-=draws;drawingFromSoul=request.soul;DrawImmediate(draws,request.normal);drawingFromSoul=false;}
                if(queuedDraws.Count>0)Emit(CombatEventKind.Status,0,true,null,"AUTOMATIC DRAW CHAIN STOPPED");
            }
            finally{queuedDraws=null;drawingFromSoul=false;}
        }
        private void ShardPulse(bool playerSide=true,CardDef card=null,int amount=0)
            =>Emit(CombatEventKind.ShardTrigger,amount,playerSide,card,activeShardId);

        private void ResetShardTurn()
        {
            var previous=shardMemory??new();
            shardMemory=new ShardCombatMemory{nextTurnDraw=previous.nextTurnDraw,nextTurnEnergy=previous.nextTurnEnergy,
                burnApplications=activeShardId=="emberglass"&&activeShardFractured?previous.burnApplications:0};
        }
        private int ShardCost(CardDef card,int cost)
        {
            if(activeShardId=="crooked"&&(shardMemory.plays+1)%(activeShardFractured?3:4)==0)return 0;
            if(activeShardId=="aftershock"&&card.kind==CardKind.Attack&&shardMemory.aftershockCharges>0)return 0;
            return cost;
        }
        private void ShardStartup(bool activation)
        {
            if(activeShardId=="bloodstone"&&activeShardFractured)
            {ShardPulse(amount:3);player.strength+=3;Emit(CombatEventKind.Status,3,true,null,"STRENGTH");}
            if(activeShardId=="hourglass"&&activeShardFractured)
            {ShardPulse(amount:2);energy+=2;Emit(CombatEventKind.Energy,energy,true);}
            if(!activation)return;
            if(activeShardId=="silvermind"&&(turn==1||activeShardFractured))
            {
                // This guaranteed mid-turn draw bypasses Hourglass as before, but
                // is not the automatically dealt opening hand for Death Spiral.
                ShardPulse(amount:2);var before=R.normalDrawn;Draw(2,true);R.additionalGuaranteedDraws+=R.normalDrawn-before;
            }
            if(activeShardId=="quickglass")foreach(var c in hand)ShardDrawCost(c);
        }
        private int ShardOpeningDraw()
        {
            var extra=shardMemory.nextTurnDraw;shardMemory.nextTurnDraw=0;
            energy+=shardMemory.nextTurnEnergy;shardMemory.nextTurnEnergy=0;
            if(activeShardId=="silvermind"&&(activeShardFractured||turn==1)){extra+=2;ShardPulse(amount:2);}
            return extra;
        }
        private int ShardBeginPlay(CardDef card,PendingCardPlay play)
        {
            var m=shardMemory;var replay=0;var attack=card.kind==CardKind.Attack;
            play.shardFirstAttack=attack&&m.attacks==0;
            play.shardHeavyAttack=attack&&card.cost>=2&&m.heavyAttacks==0;
            if(attack)
            {
                play.shardAttackBonus=m.nextAttackBonus;m.nextAttackBonus=0;
                if(activeShardId=="aftershock"&&m.aftershockCharges>0)
                {m.aftershockCharges--;play.shardAttackBonus+=activeShardFractured?12:8;ShardPulse(card:card);}
                m.attacks++;if(card.cost>=2)m.heavyAttacks++;
            }
            m.plays++;
            if(activeShardId=="firstblood"&&activeShardFractured&&play.shardFirstAttack)replay++;
            if(activeShardId=="rhythm"&&attack&&m.attacks%(activeShardFractured?3:4)==0)replay++;
            if(activeShardId=="echo"&&m.plays%(activeShardFractured?3:4)==0)replay++;
            if(activeShardId=="affliction"&&activeShardFractured&&attack&&EnemyDebuffCount()>0&&!m.afflictionReplay)
            {m.afflictionReplay=true;replay++;}
            if(activeShardId=="fatebreaker"&&card.IsModified&&m.modifiedPlays++<(activeShardFractured?2:1))
            {replay++;play.shardDrawAfter=activeShardFractured?1:0;}
            if(activeShardId=="crooked"&&m.plays%(activeShardFractured?3:4)==0)
            {ShardPulse(card:card);if(activeShardFractured)play.shardDrawAfter++;}
            if(replay>0)ShardPulse(card:card,amount:replay);
            return replay;
        }
        private void ShardFinishPlay(CardDef card,PendingCardPlay play)
        {
            if(activeShardId=="aftershock"&&card.kind==CardKind.Attack&&card.cost>=2)
            {ShardPulse(card:card);shardMemory.aftershockCharges=Math.Max(shardMemory.aftershockCharges,activeShardFractured?2:1);}
            if(activeShardId=="cycle")
            {
                shardMemory.cycleMask|=card.kind==CardKind.Attack?1:card.kind==CardKind.Skill?2:card.kind==CardKind.Power?4:0;
                if(shardMemory.cycleMask==7&&shardMemory.cycles<(activeShardFractured?3:1))
                {shardMemory.cycleMask=0;shardMemory.cycles++;ShardPulse(card:card);energy++;Emit(CombatEventKind.Energy,energy,true);Draw(2,false);}
            }
            if(play.shardDrawAfter>0&&!IsOver)Draw(play.shardDrawAfter,false);
        }
        private int ShardAttackDamage(int amount,CardDef card,int hit,int hits)
        {
            if(card.kind!=CardKind.Attack)return amount;
            var play=pendingPlay;var pulse=false;
            if(play?.card==card)amount+=play.shardAttackBonus;
            if(activeShardId=="thousand_cut"&&hits>1){amount+=activeShardFractured?5:3;pulse=true;}
            if(activeShardId=="affliction"&&EnemyDebuffCount()>0){amount+=EnemyDebuffCount()*(activeShardFractured?8:4);pulse=true;}
            if(activeShardId=="firstblood"&&!activeShardFractured&&play?.shardFirstAttack==true){amount+=CeilPercent(amount,50);pulse=true;}
            if(activeShardId=="duelist"&&play?.shardFirstAttack==true){amount+=CeilPercent(amount,activeShardFractured?100:75);pulse=true;}
            if(activeShardId=="giantglass"&&card.cost>=2&&(activeShardFractured||play?.shardHeavyAttack==true))
            {amount+=CeilPercent(amount,activeShardFractured?75:50);pulse=true;}
            if(activeShardId=="execution"&&enemy.hp*100L<enemy.maxHp*(activeShardFractured?50L:30L))
            {amount+=CeilPercent(amount,activeShardFractured?100:50);pulse=true;}
            if(activeShardId=="thousand_cut"&&activeShardFractured&&hits>1&&hit==hits-1)amount*=2;
            if(pulse)ShardPulse(false,card,amount);
            return amount;
        }
        private int ShardBlockAmount(int amount,bool fromCard,CardDef card)
        {
            if(amount<=0)return amount;
            if(activeShardId=="ironheart"&&fromCard&&(activeShardFractured||amount>=10))
            {ShardPulse(card:card,amount:amount);amount+=activeShardFractured?amount:CeilPercent(amount,50);}
            if(activeShardId=="golden_shield"&&shardMemory.blockGains<(activeShardFractured?2:1))
            {ShardPulse(card:card,amount:amount);amount*=2;}
            shardMemory.blockGains++;
            return amount;
        }
        private void ShardBlockGained(int amount,bool fromCard,CardDef card)
        {
            if(activeShardId=="counterweight")
            {
                var step=activeShardFractured?10:15;shardMemory.blockProgress+=amount;
                var stacks=shardMemory.blockProgress/step;shardMemory.blockProgress%=step;
                if(stacks>0){ShardPulse(card:card,amount:stacks);shardMemory.nextAttackBonus+=stacks*(activeShardFractured?15:12);}
            }
            if(activeShardId=="bastion"&&fromCard&&card!=null)
            {
                var play=pendingPlay;
                if(play?.card==card){play.shardBlockTotal+=amount;if(play.shardBastionTriggered)return;amount=play.shardBlockTotal;}
                if(amount>=(activeShardFractured?10:15)&&(!activeShardFractured||shardMemory.bastions<3))
                {if(play!=null)play.shardBastionTriggered=true;shardMemory.bastions++;ShardPulse(card:card);GainFortify(activeShardFractured?2:1);Draw(1,false);}
            }
        }
        private int ShardDebuff(int amount,bool burn)
        {
            if(amount<=0)return amount;
            if(activeShardId=="omen"&&shardMemory.debuffs++<(activeShardFractured?3:1))
            {ShardPulse(false,amount:amount);amount*=2;}
            if(burn&&activeShardId=="emberglass")
            {
                shardMemory.burnApplications++;
                if(activeShardFractured?shardMemory.burnApplications%2==0:shardMemory.burnApplications==1)
                {ShardPulse(false,amount:amount);amount*=2;}
            }
            return amount;
        }
        private void ShardConsumedDebuffs(int count)
        {
            if(activeShardId!="executioners"||count<=0)return;
            ShardPulse(false,amount:count);DamageEnemyRaw(count*(activeShardFractured?12:6),"EXECUTIONER'S SHARD");
            if(activeShardFractured&&shardMemory.consumeDraws<2){shardMemory.consumeDraws++;Draw(1,false);}
        }
        private void ShardExhausted(CardDef card)
        {
            if(activeShardId=="hollow"&&shardMemory.exhausts++<(activeShardFractured?3:1))
            {ShardPulse(card:card);Draw(2,false);}
        }
        private bool ShardInterceptDraw(CardDef card,bool normalDraw)
        {
            if(activeShardId!="voidglass"||card.kind is not (CardKind.Curse or CardKind.Status)||shardMemory.voidDraws>=(activeShardFractured?3:1))return false;
            RecordActualDraw(card,normalDraw);shardMemory.voidDraws++;ShardPulse(card:card);ExhaustCard(card);
            GainBlock(activeShardFractured?10:8);
            if(activeShardFractured){energy++;Emit(CombatEventKind.Energy,energy,true);}
            Draw(1,false);return true;
        }
        private void ShardDrawCost(CardDef card)
        {
            if(activeShardId=="quickglass"&&card.cost>=2&&shardMemory.quickDraws<(activeShardFractured?2:1))
            {shardMemory.quickDraws++;ShardPulse(card:card);memory.freeThisTurnIds.Add(card.instanceId);}
        }
        private void ShardCardDrawn(CardDef card,bool normal)
        {
            ShardDrawCost(card);
            if(!normal&&activeShardId=="greedy")
            {
                shardMemory.extraDraws++;ShardPulse(card:card);shardMemory.nextAttackBonus+=activeShardFractured?8:4;
                if(activeShardFractured&&shardMemory.extraDraws%3==0){energy++;Emit(CombatEventKind.Energy,energy,true);}
            }
        }
        private int ShardExtraDraw(int count,bool normal)
        {
            if(count>0&&!normal&&activeShardId=="silvermind"&&!activeShardFractured&&!shardMemory.silverDraw)
            {shardMemory.silverDraw=true;ShardPulse(amount:1);count++;}
            return count;
        }
        private void ShardEndTurn()
        {
            if(activeShardId=="balanced"&&energy==0)
            {ShardPulse();GainBlock(activeShardFractured?20:12);shardMemory.nextTurnDraw+=activeShardFractured?2:1;if(activeShardFractured)shardMemory.nextTurnEnergy++;}
        }
        private void ShardRetainEnergy()
        {
            memory.retainedEnergy=0;
            if(activeShardId=="hourglass"&&energy>0){ShardPulse();memory.retainedEnergy=activeShardFractured?energy:Math.Min(2,energy);}
        }
        private void ShardBurnTriggered()
        {
            if(activeShardId!="ash"||enemy.hp<=0)return;
            // Each enemy has a stable encounter index; single fights use index zero.
            var key=EnemyContextIndex;
            if(activeShardFractured?shardMemory.ashEnemies.Contains(key):shardMemory.ashTriggered)return;
            shardMemory.ashTriggered=true;shardMemory.ashEnemies.Add(key);
            var damage=activeShardFractured?enemy.burn:CeilPercent(enemy.burn,50);
            if(damage<=0)return;ShardPulse(false,amount:damage);
            var dealt=Math.Min(enemy.hp,damage);enemy.hp-=dealt;
            Emit(CombatEventKind.Damage,dealt,false,null,"ASH SHARD");
        }
    }
}
