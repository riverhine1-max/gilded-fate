using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Core
{
    public static partial class GameContent
    {
        private static string[] majorHexerIds;
        public static string[] MajorHexerCardIds=>majorHexerIds??=new[]{"spreading_flame","hexwave","ritual_spark","cursed_flame","shared_misfortune","arcane_fracture","sigil_shift","forbidden_exchange","burning_reflection","arcane_contagion","dark_premonition","resonant_wave","unstable_hex","sigil_of_ruin","sigil_of_withering","grave_inscription","mirror_ritual","sigil_mutation","ritual_collapse","forbidden_transfusion"};
        private static string[] majorReaperIds;
        public static string[] MajorReaperCardIds=>majorReaperIds??=new[]{"deaths_echo","reapers_mark","death_spiral","spirit_feast","haunted_blade","soul_infusion","mark_of_the_grave","grave_execution","soul_dominion","the_last_harvest"};
        private static string[] majorWandererIds;
        public static string[] MajorWandererCardIds=>majorWandererIds??=new[]{"gilded_toss","shared_fate","borrowed_strength","threadcutter","golden_opportunity","turn_the_blade","passing_fortune","scattered_threads","second_chance","twist_of_fate","fates_reflection","shared_burden","golden_thread","perfect_sequence","gilded_imprint","hidden_potential"};
        private static void AddRemainingExpansionCards(List<CardDef> cards)
        {
            void A(string id,string name,int cost,CardKind kind,int value,int plus,string text,string up,int secondary=0,int ps=int.MinValue,bool exhaust=false,int upgradeCost=-1)
            {
                var hex=MajorHexerCardIds.Contains(id);var reaper=MajorReaperCardIds.Contains(id);var ids=hex?MajorHexerCardIds:reaper?MajorReaperCardIds:MajorWandererCardIds;var index=System.Array.IndexOf(ids,id);
                var rarity=hex?(index<13?Rarity.Common:Rarity.Uncommon):reaper?(index<7?Rarity.Uncommon:Rarity.Rare):index<10?Rarity.Common:index<14?Rarity.Uncommon:Rarity.Rare;
                var definition=D(id,name,hex?HeroId.Hexer:reaper?HeroId.Reaper:(HeroId?)null,hex?CardOrigin.Arcane:reaper?CardOrigin.Reaper:CardOrigin.Wanderer,rarity,cost,kind,kind==CardKind.Attack?EffectKind.Damage:EffectKind.None,value,text,up,plus,secondary,ps,upgradeCost,exhaust:exhaust,keywords:"Burn,Marked,Weak,Vulnerable,Exhaust,Resonance,Sigil,Strength,Fortify,Block,Soul,Gravemark,Reaped,Soulbound,Death's Echo");
                definition.keywords=definition.keywords.Where(k=>text.Contains(k)||up.Contains(k)).ToArray();cards.Add(definition);
            }
            A("spreading_flame","SPREADING FLAME",1,CardKind.Skill,50,75,"Choose an enemy. Spread 50% of its Burn to every other enemy.","Choose an enemy. Spread 75% of its Burn to every other enemy.");
            A("hexwave","HEXWAVE",1,CardKind.Skill,1,2,"Apply 1 Marked to ALL enemies; 2 if you control a Hex Sigil.","Apply 2 Marked to ALL enemies.");
            A("ritual_spark","RITUAL SPARK",0,CardKind.Skill,50,50,"Activate a Sigil. Its next activation this turn is 50% stronger. Exhaust.","Activate a Sigil. Its next activation this turn is 50% stronger.",exhaust:true);
            A("cursed_flame","CURSED FLAME",1,CardKind.Skill,3,4,"Apply 3 Burn to ALL enemies. If a Curse is in hand, apply 3 more.","Apply 4 Burn to ALL enemies. If a Curse is in hand, apply 4 more.");
            A("shared_misfortune","SHARED MISFORTUNE",1,CardKind.Skill,1,2,"Choose an enemy. Copy 1 stack of each of its debuffs to every other enemy.","Choose an enemy. Copy 2 stacks of each of its debuffs to every other enemy.");
            A("arcane_fracture","ARCANE FRACTURE",1,CardKind.Attack,6,9,"Deal 6 damage to ALL enemies. Gain 1 Resonance per debuffed enemy hit.","Deal 9 damage to ALL enemies. Gain 1 Resonance per debuffed enemy hit.");
            A("sigil_shift","SIGIL SHIFT",0,CardKind.Skill,1,2,"Move your leftmost Sigil to the rightmost slot and activate it.","Move your leftmost Sigil to the rightmost slot and activate it twice.");
            A("forbidden_exchange","FORBIDDEN EXCHANGE",0,CardKind.Skill,1,1,"Exhaust a Curse or Status from hand. Add a Soul to hand. Exhaust.","Exhaust a Curse or Status from hand. Add a Soul+ to hand. Exhaust.",exhaust:true);
            A("burning_reflection","BURNING REFLECTION",1,CardKind.Attack,6,9,"Deal 6 damage. Apply 1 Burn per different debuff on the target.","Deal 9 damage. Apply 2 Burn per different debuff on the target.",1,2);
            A("arcane_contagion","ARCANE CONTAGION",1,CardKind.Skill,3,5,"Choose an enemy's Burn, Marked, Weak or Vulnerable. Move up to 3 stacks to another enemy.","Choose an enemy's Burn, Marked, Weak or Vulnerable. Move up to 5 stacks to another enemy.");
            A("dark_premonition","DARK PREMONITION",1,CardKind.Skill,2,3,"Draw 2. Exhaust any Curses or Statuses drawn this way and replace them.","Draw 3. Exhaust any Curses or Statuses drawn this way and replace them.");
            A("resonant_wave","RESONANT WAVE",1,CardKind.Attack,4,5,"Deal 4 damage to ALL enemies. Spend up to 2 Resonance to repeat once per Resonance spent.","Deal 5 damage to ALL enemies. Spend up to 2 Resonance to repeat once per Resonance spent.");
            A("unstable_hex","UNSTABLE HEX",1,CardKind.Skill,2,3,"Apply 2 Marked. Next time that enemy loses Marked, apply 1 Weak to ALL enemies.","Apply 3 Marked. Next time that enemy loses Marked, apply 2 Weak to ALL enemies.",1,2);
            A("sigil_of_ruin","SIGIL OF RUIN",1,CardKind.Skill,1,1,"Create a Ruin Sigil.","Create and activate a Ruin Sigil.");
            A("sigil_of_withering","SIGIL OF WITHERING",1,CardKind.Skill,1,1,"Create a Wither Sigil.","Create and activate a Wither Sigil.");
            A("grave_inscription","GRAVE INSCRIPTION",1,CardKind.Skill,1,1,"Create a Grave Sigil. Add a Curse to discard.","Create a Grave Sigil.");
            A("mirror_ritual","MIRROR RITUAL",2,CardKind.Skill,1,1,"Create a Mirror Sigil.","Create a Mirror Sigil.",upgradeCost:1);
            A("sigil_mutation","SIGIL MUTATION",1,CardKind.Skill,2,2,"Transform a Sigil into another accessible Sigil. Activate it twice.","Transform a Sigil into another accessible Sigil. Activate it twice.",upgradeCost:0);
            A("ritual_collapse","RITUAL COLLAPSE",1,CardKind.Skill,3,4,"Activate a Sigil twice (3 times if Special), then destroy it.","Activate a Sigil twice (4 times if Special), then destroy it.");
            A("forbidden_transfusion","FORBIDDEN TRANSFUSION",1,CardKind.Skill,2,3,"Exhaust a Curse or Status. Choose: gain 2 Strength, 2 Fortify or 2 Resonance.","Exhaust a Curse or Status. Choose: gain 3 Strength, 3 Fortify or 3 Resonance.");
            A("deaths_echo","DEATH'S ECHO",1,CardKind.Skill,6,9,"Gain 6 Death's Echo this turn.","Gain 9 Death's Echo this turn.");
            A("reapers_mark","REAPER'S MARK",1,CardKind.Skill,50,100,"Apply Reaped 50% to an enemy for this combat.","Apply Reaped 100% to an enemy for this combat.");
            A("death_spiral","DEATH SPIRAL",2,CardKind.Attack,6,6,"Deal 6 damage to ALL enemies. Repeat per 3 additional cards drawn this turn (up to 3 extra hits).","Deal 6 damage to ALL enemies. Repeat per 2 additional cards drawn this turn (up to 3 extra hits).",3,2);
            A("spirit_feast","SPIRIT FEAST",1,CardKind.Skill,4,6,"Exhaust up to 3 cards. Gain 4 Block each. If any was a Soul, gain 1 Energy.","Exhaust up to 3 cards. Gain 6 Block each. If any was a Soul, gain 1 Energy.");
            A("haunted_blade","HAUNTED BLADE",1,CardKind.Attack,8,11,"Deal 8 damage. Deal +3 per extra card drawn this turn beyond normal turn draw.","Deal 11 damage. Deal +4 per extra card drawn this turn beyond normal turn draw.",3,4);
            A("soul_infusion","SOUL INFUSION",1,CardKind.Skill,2,3,"Soulbind a non-Soul card in hand. Each Soul played grants it +2 damage or Block for this combat.","Soulbind a non-Soul card in hand. Each Soul played grants it +3 damage or Block for this combat.");
            A("mark_of_the_grave","MARK OF THE GRAVE",1,CardKind.Skill,1,1,"Choose an enemy. Each card drawn this turn applies 1 Gravemark to it. Exhaust.","Choose an enemy. Each card drawn this turn applies 1 Gravemark to it.",exhaust:true);
            A("grave_execution","GRAVE EXECUTION",2,CardKind.Attack,5,7,"Consume ALL Gravemark on the target. Deal 5 damage for EACH stack consumed.","Consume ALL Gravemark on the target. Deal 7 damage for EACH stack consumed.");
            A("soul_dominion","SOUL DOMINION",2,CardKind.Power,1,2,"Whenever a Soul draws a non-Soul card, add a Soul to discard.","Whenever a Soul draws a non-Soul card, add a Soul to the draw pile.");
            A("the_last_harvest","THE LAST HARVEST",3,CardKind.Skill,4,4,"Exhaust your hand. Deal 4 damage to ALL enemies per card Exhausted. Draw 1 per Soul Exhausted. Exhaust.","Exhaust your hand. Deal 4 damage to ALL enemies per card Exhausted. Draw 1 per Soul Exhausted. Exhaust.",exhaust:true,upgradeCost:2);
            A("gilded_toss","GILDED TOSS",1,CardKind.Attack,7,10,"Deal 7 damage. If this is your first card this turn, draw 1.","Deal 10 damage. If this is your first card this turn, draw 1.");
            A("shared_fate","SHARED FATE",1,CardKind.Skill,1,2,"Copy 1 stack of each debuff from an enemy to a random DIFFERENT enemy.","Copy 2 stacks of each debuff from an enemy to a random DIFFERENT enemy.");
            A("borrowed_strength","BORROWED STRENGTH",1,CardKind.Skill,50,100,"Gain Strength this turn equal to half an enemy's Strength.","Gain Strength this turn equal to an enemy's full Strength.");
            A("threadcutter","THREADCUTTER",0,CardKind.Skill,1,2,"Exhaust another card from hand. Draw 1.","Exhaust another card from hand. Draw 2.");
            A("golden_opportunity","GOLDEN OPPORTUNITY",1,CardKind.Skill,1,2,"Draw 2. Choose one drawn card. It costs 1 less this turn.","Draw 2. Choose one drawn card. It costs 2 less this turn.");
            A("turn_the_blade","TURN THE BLADE",1,CardKind.Attack,6,8,"Deal 6 damage. If the target has a debuff, deal 6 again.","Deal 8 damage. If the target has a debuff, deal 8 again.");
            A("passing_fortune","PASSING FORTUNE",1,CardKind.Skill,5,8,"Your next applicable card this turn gains +5 damage or +5 Block.","Your next applicable card this turn gains +8 damage or +8 Block.");
            A("scattered_threads","SCATTERED THREADS",1,CardKind.Attack,4,6,"Deal 4 damage to ALL enemies. Deal +1 per different debuff type among enemies.","Deal 6 damage to ALL enemies. Deal +2 per different debuff type among enemies.",1,2);
            A("second_chance","SECOND CHANCE",1,CardKind.Skill,1,1,"Put a discarded card on top of your draw pile. It costs 1 less this turn.","Return a discarded card to hand. It costs 1 less this turn.");
            A("twist_of_fate","TWIST OF FATE",0,CardKind.Skill,1,1,"Shuffle another card from hand into your draw pile. Draw 1. It costs 0 this turn. Exhaust.","Shuffle another card from hand into your draw pile. Draw 1. It costs 0 this turn.",exhaust:true);
            A("fates_reflection","FATE'S REFLECTION",1,CardKind.Skill,1,1,"Choose another card in hand. The next time it is played this turn, play it twice. Exhaust.","Choose another card in hand. The next time it is played this turn, play it twice. Exhaust.",exhaust:true,upgradeCost:0);
            A("shared_burden","SHARED BURDEN",1,CardKind.Skill,3,5,"Transfer up to 3 stacks of your debuffs to an enemy.","Transfer up to 5 stacks of your debuffs to an enemy.");
            A("golden_thread","GOLDEN THREAD",1,CardKind.Power,1,2,"The first card each turn that did not start in your deck draws 1.","The first card each turn that did not start in your deck draws 1 and gains +3 damage or Block.");
            A("perfect_sequence","PERFECT SEQUENCE",2,CardKind.Attack,6,8,"Deal 6 damage 3 times. Each hit gains +2 per different card type played this turn.","Deal 8 damage 3 times. Each hit gains +2 per different card type played this turn.");
            A("gilded_imprint","GILDED IMPRINT",1,CardKind.Skill,6,9,"Choose another card. Imprint its printed Energy cost. Playing that cost deals 6 damage to a random enemy. Exhaust.","Choose another card. Imprint its printed Energy cost. Playing that cost deals 9 damage to a random enemy. Exhaust.",exhaust:true);
            A("hidden_potential","HIDDEN POTENTIAL",2,CardKind.Power,1,2,"At turn start, reveal the bottom card. Its type grants Hidden Potential this turn: +5 damage, +5 Block, or reduced costs.","At turn start, reveal the bottom 2 cards. Their different types grant Hidden Potential this turn. Duplicate types do not stack.");
        }
    }
}
