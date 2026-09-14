using System.Collections.Generic;

namespace GildedFate.Core
{
    public static partial class GameContent
    {
        public static readonly string[] MajorVanguardCardIds={"sweeping_blade","crushing_sweep","vengeful_sweep","shockwave","break_formation","war_cry","whirlwind_guard","concussive_swing","challenge_them_all","rallying_guard","cleaving_momentum","press_the_advantage","steel_through_pain","gilded_sweep","chain_reaction_vanguard","brace_for_impact","shatter_the_ranks","rally_the_fallen","unrelenting_assault","turn_their_strength"};
        private static void AddMajorVanguardCards(List<CardDef> cards)
        {
            void K(string id,string name,int cost,CardKind kind,EffectKind effect,int value,int plus,string text,string upgraded,int secondary=0,int ps=int.MinValue,bool uncommon=false,bool exhaust=false,string keys="")
                =>cards.Add(D(id,name,HeroId.Vanguard,CardOrigin.Knight,uncommon?Rarity.Uncommon:Rarity.Common,cost,kind,effect,value,text,upgraded,plus,secondary,ps,exhaust:exhaust,keywords:keys));
            K("sweeping_blade","SWEEPING BLADE",1,CardKind.Attack,EffectKind.Damage,5,7,"Deal 5 damage to ALL enemies. Deal +2 damage per different buff you have.","Deal 7 damage to ALL enemies. Deal +2 damage per different buff you have.",keys:"Buff");
            K("crushing_sweep","CRUSHING SWEEP",2,CardKind.Attack,EffectKind.Damage,10,13,"Deal 10 damage to ALL enemies. Heavy: Apply 1 Vulnerable to ALL enemies.","Deal 13 damage to ALL enemies. Heavy: Apply 1 Vulnerable to ALL enemies.",keys:"Heavy,Vulnerable");
            K("vengeful_sweep","VENGEFUL SWEEP",1,CardKind.Attack,EffectKind.Damage,5,7,"Deal 5 damage to ALL enemies. If Retaliate triggered this turn, repeat.","Deal 7 damage to ALL enemies. If Retaliate triggered this turn, repeat.",keys:"Retaliate");
            K("shockwave","SHOCKWAVE",2,CardKind.Skill,EffectKind.Weak,1,2,"Apply 1 Weak and 1 Vulnerable to ALL enemies. Exhaust.","Apply 2 Weak and 2 Vulnerable to ALL enemies. Exhaust.",exhaust:true,keys:"Weak,Vulnerable,Exhaust");
            K("break_formation","BREAK FORMATION",1,CardKind.Attack,EffectKind.Damage,6,8,"Deal 6 damage to ALL enemies. Enemies with Block lose 5 additional Block.","Deal 8 damage to ALL enemies. Enemies with Block lose 8 additional Block.",5,8,keys:"Block");
            K("war_cry","WAR CRY",1,CardKind.Skill,EffectKind.Strength,1,2,"Gain 1 Strength. If 2+ enemies are alive, gain 1 Fortify.","Gain 2 Strength. If 2+ enemies are alive, gain 1 Fortify.",keys:"Strength,Fortify");
            K("whirlwind_guard","WHIRLWIND GUARD",1,CardKind.Attack,EffectKind.Damage,4,6,"Deal 4 damage to ALL enemies. Gain 2 Block per enemy hit.","Deal 6 damage to ALL enemies. Gain 3 Block per enemy hit.",2,3,keys:"Block");
            K("concussive_swing","CONCUSSIVE SWING",2,CardKind.Attack,EffectKind.Damage,9,12,"Deal 9 damage to ALL enemies. Apply 1 Weak to the living enemy with the highest HP.","Deal 12 damage to ALL enemies. Apply 2 Weak to the living enemy with the highest HP.",1,2,keys:"Weak");
            K("challenge_them_all","CHALLENGE THEM ALL",1,CardKind.Skill,EffectKind.Retaliate,1,2,"Gain 1 Retaliate per living enemy. Your next Retaliate trigger also grants 4 Block.","Gain 2 Retaliate per living enemy. Your next Retaliate trigger also grants 4 Block.",keys:"Retaliate,Block");
            K("rallying_guard","RALLYING GUARD",1,CardKind.Skill,EffectKind.Block,7,10,"Gain 7 Block. The next buff you gain this turn gains +1 stack.","Gain 10 Block. The next buff you gain this turn gains +1 stack.",keys:"Block,Buff");
            K("cleaving_momentum","CLEAVING MOMENTUM",1,CardKind.Attack,EffectKind.Damage,4,6,"Deal 4 damage to ALL enemies. Counts as one Attack played per enemy hit.","Deal 6 damage to ALL enemies. Counts as one Attack played per enemy hit.");
            K("press_the_advantage","PRESS THE ADVANTAGE",1,CardKind.Attack,EffectKind.Damage,7,10,"Deal 7 damage. If the target has a debuff, repeat against a random DIFFERENT enemy.","Deal 10 damage. If the target has a debuff, repeat against a random DIFFERENT enemy.",keys:"Debuff");
            K("steel_through_pain","STEEL THROUGH PAIN",1,CardKind.Skill,EffectKind.Block,6,9,"Gain 6 Block. The next enemy attack that breaks all your Block before your next turn grants 2 Retaliate.","Gain 9 Block. The next enemy attack that breaks all your Block before your next turn grants 3 Retaliate.",2,3,keys:"Block,Retaliate");
            K("gilded_sweep","GILDED SWEEP",2,CardKind.Attack,EffectKind.Damage,8,11,"Deal 8 damage to ALL enemies. Deal +3 damage per different buff you have.","Deal 11 damage to ALL enemies. Deal +3 damage per different buff you have.",uncommon:true,keys:"Buff");
            K("chain_reaction_vanguard","CHAIN REACTION",1,CardKind.Attack,EffectKind.Damage,8,11,"Deal 8 damage. On Kill: Repeat against a random living enemy with +4 damage.","Deal 11 damage. On Kill: Repeat against a random living enemy with +5 damage.",4,5,uncommon:true);
            K("brace_for_impact","BRACE FOR IMPACT",1,CardKind.Skill,EffectKind.Block,10,13,"Gain 10 Block. Until your next turn, gain 2 Retaliate BEFORE each enemy attack.","Gain 13 Block. Until your next turn, gain 3 Retaliate BEFORE each enemy attack.",2,3,uncommon:true,keys:"Block,Retaliate");
            K("shatter_the_ranks","SHATTER THE RANKS",2,CardKind.Attack,EffectKind.Damage,11,15,"Deal 11 damage to ALL enemies. Enemies with Block take 50% more damage from this card.","Deal 15 damage to ALL enemies. Enemies with Block take 50% more damage from this card.",uncommon:true,keys:"Block");
            K("rally_the_fallen","RALLY THE FALLEN",1,CardKind.Skill,EffectKind.None,2,3,"Choose a buff you currently have. Gain 2 additional stacks.","Choose a buff you currently have. Gain 3 additional stacks.",uncommon:true,keys:"Buff");
            K("unrelenting_assault","UNRELENTING ASSAULT",1,CardKind.Attack,EffectKind.Damage,5,7,"Deal 5 damage to ALL enemies. Your next Attack this turn gains +3 damage per enemy hit.","Deal 7 damage to ALL enemies. Your next Attack this turn gains +4 damage per enemy hit.",3,4,uncommon:true);
            K("turn_their_strength","TURN THEIR STRENGTH",2,CardKind.Skill,EffectKind.Strength,50,100,"Choose an enemy. Gain Strength equal to half its current Strength for this combat. Exhaust.","Choose an enemy. Gain Strength equal to its full current Strength for this combat. Exhaust.",uncommon:true,exhaust:true,keys:"Strength,Exhaust");
        }
    }
}
