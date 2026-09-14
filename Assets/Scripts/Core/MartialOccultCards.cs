using System.Collections.Generic;

namespace GildedFate.Core
{
    public static partial class GameContent
    {
        // Appended to the catalogue: existing card IDs, instances and art indices survive.
        public static readonly string[] MartialOccultCardIds={
            "gilded_warlord","unmovable","break_the_line","relentless_conquest",
            "iron_momentum","crushing_advance","stand_your_ground","gilded_fury","breaking_momentum","vengeful_rush",
            "forbidden_convergence","brand_of_ruin","arcane_overload","beyond_the_veil_hexer",
            "sigil_of_malice","unstable_ritual","dark_resonance","arcane_detonation","hexed_reverberation"
        };
        private static void AddMartialOccultCards(List<CardDef> cards)
        {
            void K(string id,string name,Rarity rarity,int cost,CardKind kind,EffectKind effect,int value,string text,string upgraded,int pv=int.MinValue,int secondary=0,int ps=int.MinValue,int pc=-1,int hits=1,string keys="")
                =>cards.Add(D(id,name,HeroId.Vanguard,CardOrigin.Knight,rarity,cost,kind,effect,value,text,upgraded,pv,secondary,ps,pc,hits,keywords:keys));
            void A(string id,string name,Rarity rarity,int cost,CardKind kind,EffectKind effect,int value,string text,string upgraded,int pv=int.MinValue,int secondary=0,int ps=int.MinValue,int pc=-1,bool exhaust=false,string keys="")
                =>cards.Add(D(id,name,HeroId.Hexer,CardOrigin.Arcane,rarity,cost,kind,effect,value,text,upgraded,pv,secondary,ps,pc,exhaust:exhaust,keywords:keys));

            K("gilded_warlord","GILDED WARLORD",Rarity.Rare,3,CardKind.Power,EffectKind.Power,1,
                "The first buff you gain is doubled. After triggering, rests next turn; ready the following turn.",
                "The first buff you gain is doubled. After triggering, rests next turn; ready the following turn.",pc:2,keys:"Buff");
            K("unmovable","UNMOVABLE",Rarity.Rare,2,CardKind.Power,EffectKind.Power,1,
                "Keep your remaining Block between turns.","Keep your remaining Block between turns.",pc:1,keys:"Block");
            K("break_the_line","BREAK THE LINE",Rarity.Rare,3,CardKind.Attack,EffectKind.Damage,24,
                "Deal 24 damage. Heavy: Deal double damage and gain 1 Energy.",
                "Deal 30 damage. Heavy: Deal double damage and gain 1 Energy.",30,keys:"Heavy");
            K("relentless_conquest","RELENTLESS CONQUEST",Rarity.Rare,2,CardKind.Power,EffectKind.Power,4,
                "Every 4th Attack you play each turn plays twice. Duplicates do not advance this counter.",
                "Every 3rd Attack you play each turn plays twice. Duplicates do not advance this counter.",3);
            K("iron_momentum","IRON MOMENTUM",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,7,
                "Deal 7 damage. Gain 3 Block for each Attack played before this card this turn.",
                "Deal 10 damage. Gain 4 Block for each Attack played before this card this turn.",10,3,4,keys:"Block");
            K("crushing_advance","CRUSHING ADVANCE",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,15,
                "Deal 15 damage. Heavy: Gain 1 Fortify.","Deal 19 damage. Heavy: Gain 2 Fortify.",19,1,2,keys:"Heavy,Fortify");
            K("stand_your_ground","STAND YOUR GROUND",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Block,10,
                "Gain 10 Block. If you have Retaliate, gain 1 Fortify.",
                "Gain 13 Block. If you have Retaliate, gain 2 Fortify.",13,1,2,keys:"Block,Retaliate,Fortify");
            K("gilded_fury","GILDED FURY",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Strength,2,
                "Gain 1 Strength for every 2 Attacks played this turn.",
                "Gain 1 Strength for EACH Attack played this turn.",1,keys:"Strength");
            K("breaking_momentum","BREAKING MOMENTUM",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,8,
                "Deal 8 damage. If this is your third Attack this turn, gain 1 Strength and 1 Fortify.",
                "Deal 11 damage. If this is your third Attack this turn, gain 2 Strength and 1 Fortify.",11,1,2,keys:"Strength,Fortify");
            K("vengeful_rush","VENGEFUL RUSH",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,6,
                "Deal 6 damage twice. Revenge: This card costs 0.",
                "Deal 8 damage twice. Revenge: This card costs 0.",8,hits:2,keys:"Revenge");

            A("forbidden_convergence","FORBIDDEN CONVERGENCE",Rarity.Rare,2,CardKind.Skill,EffectKind.Sigil,1,
                "Activate ALL Sigils once, then once more per Curse in your hand. Exhaust.",
                "Activate ALL Sigils once, then once more per Curse in your hand. Exhaust.",pc:1,exhaust:true,keys:"Sigil,Curse,Exhaust");
            A("brand_of_ruin","BRAND OF RUIN",Rarity.Rare,2,CardKind.Power,EffectKind.Power,4,
                "Whenever you consume Marked, apply 4 Burn to that enemy per stack consumed.",
                "Whenever you consume Marked, apply 6 Burn to that enemy per stack consumed.",6,keys:"Marked,Burn");
            A("arcane_overload","ARCANE OVERLOAD",Rarity.Rare,2,CardKind.Attack,EffectKind.Damage,11,
                "Deal 11 damage. Activate ALL Sigils twice. Exhaust.","Deal 15 damage. Activate ALL Sigils twice. Exhaust.",15,exhaust:true,keys:"Sigil,Exhaust");
            // The Reaper's existing beyond_the_veil ID remains untouched.
            A("beyond_the_veil_hexer","BEYOND THE VEIL",Rarity.Rare,3,CardKind.Power,EffectKind.Power,2,
                "Gain 2 additional Sigil slots for the rest of combat.",
                "Gain 3 additional Sigil slots for the rest of combat.",3,keys:"Sigil");
            A("sigil_of_malice","SIGIL OF MALICE",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Sigil,1,
                "Create a Sigil of your choice. If the enemy has a debuff, immediately activate it.",
                "Create a Sigil of your choice. If the enemy has a debuff, immediately activate it.",pc:0,keys:"Sigil,Debuff");
            A("unstable_ritual","UNSTABLE RITUAL",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Sigil,2,
                "Activate your rightmost Sigil twice. Then destroy it.",
                "Activate your rightmost Sigil THREE times. Then destroy it.",3,keys:"Sigil");
            A("dark_resonance","DARK RESONANCE",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Sigil,1,
                "Add a Curse to your discard pile. Activate all Sigils once.",
                "Activate all Sigils once. Gain 1 Resonance per Sigil you control. Add a Curse to your discard pile.",secondary:0,ps:1,keys:"Curse,Sigil,Resonance");
            A("arcane_detonation","ARCANE DETONATION",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,10,
                "Deal 10 damage. Spend up to 3 Resonance; deal 5 additional damage per Resonance spent.",
                "Deal 13 damage. Spend up to 3 Resonance; deal 7 additional damage per Resonance spent.",13,5,7,keys:"Resonance");
            A("hexed_reverberation","HEXED REVERBERATION",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Sigil,2,
                "Consume 1 Marked. Activate your leftmost Sigil twice.",
                "Consume 1 Marked. Choose ANY Sigil and activate it twice.",keys:"Marked,Sigil");
        }
    }
}
