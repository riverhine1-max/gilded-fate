using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Combat
{
    public enum EnemyActionType { Attack, Block, Strength, Weak, Vulnerable, StealGold, Curse, SummonWeapon, Heal, ExhaustDiscard }
    public enum IntentDestination { None, Hand, Draw, Discard, Deck }
    public readonly struct PlannedEnemyAction
    {
        public readonly EnemyActionType type;
        public readonly int amount,hits;
        public PlannedEnemyAction(EnemyActionType type,int amount,int hits=1){this.type=type;this.amount=amount;this.hits=hits;}
    }
    public sealed class EnemyIntentAction
    {
        public EnemyActionType type;
        public IntentDestination destination;
        public int amount,hits=1;
        public bool prevented;
        public string title,detail;
        public int TotalDamage=>type==EnemyActionType.Attack?amount*hits:0;
        public int Icon=>type switch
        {
            EnemyActionType.Attack=>ThreatIcon(TotalDamage),EnemyActionType.Block=>4,EnemyActionType.Strength=>5,
            EnemyActionType.Heal=>6,EnemyActionType.Weak=>8,EnemyActionType.Vulnerable=>9,EnemyActionType.Curse=>10,
            EnemyActionType.ExhaustDiscard=>12,EnemyActionType.StealGold=>13,EnemyActionType.SummonWeapon=>14,_=>19
        };
        public static int ThreatIcon(int total)=>total<=15?0:total<=34?1:total<=49?2:3;
        public string ValueText=>type==EnemyActionType.Attack&&hits>1?amount+" × "+hits:amount.ToString();
    }
    public sealed partial class CombatState
    {
        [NonSerialized] private List<EnemyIntentAction>[] intentPreviewSink;
        // This is the single plan consumed by both the real turn and the preview.
        // Legacy intent fields remain save-compatible move selectors, not a second executor.
        public PlannedEnemyAction[] PlannedEnemyTurn()
        {
            PlannedEnemyAction A(EnemyActionType type,int amount,int hits=1)=>new(type,amount,hits);
            if(intent==Core.IntentKind.Attack)return new[]{A(EnemyActionType.Attack,intentValue,intentHits)};
            if(intent==Core.IntentKind.Defend)return new[]{A(EnemyActionType.Block,intentValue)};
            if(intent==Core.IntentKind.Buff)return new[]{A(EnemyActionType.Strength,intentValue)};
            if(intent==Core.IntentKind.Debuff)return new[]{A(enemyId=="vault_spider"?EnemyActionType.Weak:EnemyActionType.Vulnerable,intentValue)};
            switch(enemyId)
            {
                case "vault_rat":return new[]{A(EnemyActionType.StealGold,4),A(EnemyActionType.Attack,intentValue)};
                case "collector":return new[]{A(EnemyActionType.StealGold,12),A(EnemyActionType.Attack,intentValue)};
                case "rune_mage":return new[]{A(EnemyActionType.Vulnerable,1),A(EnemyActionType.Attack,intentValue)};
                case "executioner":return enemy.strength<3?new[]{A(EnemyActionType.Attack,intentValue),A(EnemyActionType.Strength,1)}:new[]{A(EnemyActionType.Attack,intentValue)};
                case "mirror_witch":return memory.lastCardWasAttack?new[]{A(EnemyActionType.Attack,Math.Max(intentValue,memory.lastCardValue))}:new[]{A(EnemyActionType.Block,Math.Max(12,memory.lastCardValue)),A(EnemyActionType.Strength,1)};
                case "hollow_king":return new[]{A(EnemyActionType.SummonWeapon,1),A(EnemyActionType.Block,intentValue)};
                case "vault_mother":return new[]{A(EnemyActionType.Block,intentValue),A(EnemyActionType.Heal,3+bossPhase*2)};
                case "last_dealer":
                    var actions=new List<PlannedEnemyAction>{A(EnemyActionType.Curse,1),A(EnemyActionType.Weak,1)};
                    if(bossPhase==3)actions.Add(A(EnemyActionType.ExhaustDiscard,1));
                    actions.Add(A(EnemyActionType.Attack,intentValue));return actions.ToArray();
                default:return new[]{A(EnemyActionType.Attack,intentValue)};
            }
        }
        private int EnemyAttackBeforeBlock(int amount)
        {if(enemy.weak>0)amount=amount*3/4;if(player.vulnerable>0)amount=amount*3/2;return Math.Max(0,amount);}
        private EnemyIntentAction DescribeEnemyAction(PlannedEnemyAction plan)
        {
            var a=new EnemyIntentAction{type=plan.type,amount=plan.amount,hits=plan.hits};
            switch(plan.type)
            {
                case EnemyActionType.Attack:
                    a.amount=EnemyAttackBeforeBlock(plan.amount+enemy.strength);a.title="ATTACK";
                    a.detail=plan.hits>1?$"Deal {a.amount} damage {plan.hits} times ({a.TotalDamage} total before Block).":$"Deal {a.amount} damage before Block.";
                    if(enemy.strength!=0)a.detail+=$"\nEnemy Strength: {enemy.strength:+0;-0} damage per hit.";
                    if(enemy.weak>0)a.detail+="\nEnemy Weak: 25% less attack damage.";
                    if(player.vulnerable>0)a.detail+="\nYour Vulnerable: 50% more attack damage received.";
                    break;
                case EnemyActionType.Block:a.title="BLOCK";a.detail=$"Gain {a.amount} Block.\nBlock absorbs incoming damage.";break;
                case EnemyActionType.Strength:a.amount=Math.Max(0,plan.amount-EffectValue(enemy,"wither"));a.title="STRENGTH";a.detail=$"Gain {a.amount} Strength."+(a.amount<plan.amount?" Wither reduces this gain.":"")+"\nEach point increases attack damage by 1 per hit.";break;
                case EnemyActionType.Weak:a.title="WEAK";a.detail=$"Apply {a.amount} Weak to you.\nYour Attacks deal 25% less damage while Weak lasts.";break;
                case EnemyActionType.Vulnerable:a.title="VULNERABLE";a.detail=$"Apply {a.amount} Vulnerable to you.\nYou receive 50% more attack damage while Vulnerable lasts.";break;
                case EnemyActionType.StealGold:a.title="STEAL GOLD";a.detail=$"Steal {a.amount} Gold."+(enemyId=="collector"?" The Collector holds the stolen Gold.":"");break;
                case EnemyActionType.Curse:a.title="ADD CURSE";a.destination=IntentDestination.Discard;a.detail="Add 1 random Curse to your discard pile. The specific Curse is not revealed in advance.";break;
                case EnemyActionType.SummonWeapon:a.amount=Math.Max(0,Math.Min(3,spectralWeapons+plan.amount)-spectralWeapons);a.title="SUMMON WEAPON";a.detail=$"Add {a.amount} spectral weapon to the armory (maximum 3). Each weapon adds a hit to the King's later volleys; this is not a new enemy.";break;
                case EnemyActionType.Heal:a.amount=Math.Min(plan.amount,Math.Max(0,enemy.maxHp-enemy.hp));a.title="HEAL";a.detail=$"Restore {a.amount} HP, up to maximum health.";break;
                case EnemyActionType.ExhaustDiscard:a.amount=discard.Any(c=>c.origin!=Core.CardOrigin.Curse)?1:0;a.title="BANISH CARD";a.destination=IntentDestination.Discard;a.detail=a.amount>0?"Exhaust the most recently discarded non-Curse card.":"No non-Curse card is available in discard to Exhaust.";break;
            }
            return a;
        }
        private void ExecuteEnemyPlan(PlannedEnemyAction[] actions)
        {
            void Execute()
            {
                foreach(var action in actions)
                {
                    intentPreviewSink?[EnemyContextIndex].Add(DescribeEnemyAction(action));
                    switch(action.type)
                    {
                        case EnemyActionType.Attack:
                            for(var hit=0;hit<action.hits&&!IsOver&&enemy.hp>0;hit++)DamagePlayer(action.amount+enemy.strength);
                            break;
                        case EnemyActionType.Block:enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);break;
                        case EnemyActionType.Strength:var gain=EnemyBuffAfterWither(action.amount);enemy.strength+=gain;Emit(CombatEventKind.Status,gain,false,null,"STRENGTH");break;
                        case EnemyActionType.Weak:ApplyPlayerDebuff(Core.EffectKind.Weak,action.amount);break;
                        case EnemyActionType.Vulnerable:ApplyPlayerDebuff(Core.EffectKind.Vulnerable,action.amount);break;
                        case EnemyActionType.StealGold:goldLost+=action.amount;if(enemyId=="collector")stolenGold+=action.amount;break;
                        case EnemyActionType.Curse:AddRandomCurse(false);break;
                        case EnemyActionType.SummonWeapon:spectralWeapons=Math.Min(3,spectralWeapons+action.amount);break;
                        case EnemyActionType.Heal:var before=enemy.hp;enemy.hp=Math.Min(enemy.maxHp,enemy.hp+action.amount);if(enemy.hp>before)Emit(CombatEventKind.Heal,enemy.hp-before,false);break;
                        case EnemyActionType.ExhaustDiscard:BanishDiscardedCard();break;
                        default:throw new InvalidOperationException("Enemy action has no executor: "+action.type);
                    }
                }
            }
            // Retaliate still responds once to the whole original intent, after all
            // of its hits and side effects. This preserves the current combat rules.
            if(actions.Any(a=>a.type==EnemyActionType.Attack))ResolveEnemyAttack(Execute);else Execute();
            if(intent==Core.IntentKind.Special)RefreshMechanicTelemetry();
        }
        public List<EnemyIntentAction>[] PreviewEnemyIntents()
        {
            var copy=CopyForCheckpoint();
            var result=Enumerable.Range(0,EnemyCount).Select(_=>new List<EnemyIntentAction>()).ToArray();
            copy.intentPreviewSink=result;
            if(copy.phase==CombatPhase.Player)copy.EndPlayerTurn();
            if(copy.phase==CombatPhase.Enemy)copy.ResolveEnemyTurn();
            for(var i=0;i<result.Length;i++)if(result[i].Count==0&&IsLivingTarget(i))
            {
                var index=i;
                result[i]=InspectEnemy(index,()=>PlannedEnemyTurn().Select(DescribeEnemyAction).ToList());
                if(copy.phase==CombatPhase.Finished||copy.EnemyAt(i).hp<=0)
                    foreach(var action in result[i]){action.prevented=true;action.detail+="\nExpected to be prevented by the known end-of-turn effects."; }
            }
            return result;
        }
    }
}
