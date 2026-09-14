using System.Linq;

namespace GildedFate.Core
{
    public static partial class GameContent
    {
        // Appended to the original catalog. Fixed order is also the art-atlas contract.
        private static RelicDef[] BuildMajorRelics()=>new[]
        {
            R("frayed_cord","FRAYED CORD",Rarity.Common,"The first extra card drawn each turn gives your next Attack +3 damage."),
            R("iron_thread","IRON THREAD",Rarity.Common,"The first card Exhausted each turn grants 3 Block."),
            R("cracked_hourglass","CRACKED HOURGLASS",Rarity.Common,"The first 0-cost card played each turn gives your next applicable 2+ Energy card +4 damage or Block."),
            R("omen_nail","OMEN NAIL",Rarity.Common,"The first debuff you apply each turn grants 2 Block."),
            R("war_torn_ribbon","WAR-TORN RIBBON",Rarity.Common,"Play an Attack and Skill in a turn: your next Power that turn costs 1 less. Once per turn."),
            R("hollow_coin","HOLLOW COIN",Rarity.Common,"The first card each turn that did not start in your deck gains +4 damage or Block, if applicable."),
            R("grave_dust","GRAVE DUST",Rarity.Common,"The first time you Exhaust 2 cards in one turn, draw 1."),
            R("bent_warhorn","BENT WARHORN",Rarity.Common,"The first time you gain two different buffs in a turn, gain 3 Block."),
            R("smoldering_wick","SMOLDERING WICK",Rarity.Common,"The first time each turn you damage a debuffed enemy, deal 3 damage to a different living enemy."),
            R("ritual_chalk","RITUAL CHALK",Rarity.Common,"The first time you activate a Sigil each combat, draw 1."),
            R("spirit_fang","SPIRIT FANG",Rarity.Common,"The first Temporary card played each combat gains +5 damage or Block, if applicable."),
            R("dented_gauntlet","DENTED GAUNTLET",Rarity.Common,"After a 2+ Energy Attack, your next Block-granting Skill that turn gains +4 Block."),
            R("ashen_needle","ASHEN NEEDLE",Rarity.Common,"The first time each turn an enemy takes non-Attack damage, apply 1 Weak to it."),
            R("graveyard_key","GRAVEYARD KEY",Rarity.Common,"The first card retrieved from Exhaust each combat costs 1 less that turn."),
            R("fate_die","FATE DIE",Rarity.Common,"At combat start, randomly choose Attack, Skill or Power. The first card of that type played costs 1 less."),
            R("bloodstained_thread","BLOODSTAINED THREAD",Rarity.Common,"The first time you play 3 cards in a turn, your next applicable card gains +4 damage or Block."),
            R("chain_of_opposites","CHAIN OF OPPOSITES",Rarity.Uncommon,"Your first buff each turn gives the next debuff you apply that turn +1 stack."),
            R("funeral_bell","FUNERAL BELL",Rarity.Uncommon,"Every 3 cards Exhausted during combat: draw 1 and give your next Attack +5 damage."),
            R("glass_needle","GLASS NEEDLE",Rarity.Uncommon,"After dealing Attack damage 4 times in a turn, apply 1 Vulnerable to a random enemy. Once per turn."),
            R("gilded_loop","GILDED LOOP",Rarity.Uncommon,"Play cards costing 0, 1 and 2+ in a turn: draw 2. Once per turn."),
            R("blacksteel_clasp","BLACKSTEEL CLASP",Rarity.Uncommon,"Gain two different buffs in a turn: your next Attack gains +6 damage. Once per turn."),
            R("severed_quill","SEVERED QUILL",Rarity.Uncommon,"The first card played each turn that did not start in your deck reduces a random card in hand by 1 Energy that turn."),
            R("mirror_thorn","MIRROR THORN",Rarity.Uncommon,"The first time each turn an enemy gains its third different debuff, deal 8 damage to it and draw 1."),
            R("executioners_chain","EXECUTIONER'S CHAIN",Rarity.Uncommon,"The first enemy damaged each combat becomes Condemned: every fifth hit against it deals 12 bonus damage."),
            R("veilglass","VEILGLASS",Rarity.Uncommon,"Draw 3 extra cards in a turn: gain 1 Foresight. Once per turn. Foresight makes the next card drawn cost 1 less until played."),
            R("black_rose","BLACK ROSE",Rarity.Uncommon,"Your first debuff each turn also applies 1 Wither: reduce that enemy's next buff gain by 1, then remove Wither."),
            R("grave_lantern","GRAVE LANTERN",Rarity.Uncommon,"Every fourth Exhaust grants 1 Afterlife. The next non-Temporary card that would Exhaust goes to discard instead; consume 1 Afterlife."),
            R("bloodglass_shard","BLOODGLASS SHARD",Rarity.Uncommon,"Play 3 Attacks in a turn: gain 1 Ferocity. Once per turn. Your next Attack deals +25% damage, then consume 1 Ferocity."),
            R("golden_scarab","GOLDEN SCARAB",Rarity.Uncommon,"Your first 2+ Energy card each turn grants Preparation: your next applicable 0/1-cost card gains +5 damage or Block."),
            R("mourning_bell","MOURNING BELL",Rarity.Uncommon,"The first time each enemy reaches three different debuffs, apply Death Knell: its next non-Attack damage is repeated once."),
            R("spectral_needle","SPECTRAL NEEDLE",Rarity.Uncommon,"The first card played each turn that did not start in your deck grants Phantom Edge: your next Attack adds a separate 4-damage hit."),
            R("broken_compass","BROKEN COMPASS",Rarity.Uncommon,"At turn start, mark a random card in hand Destined. Playing it this turn makes your next card cost 1 less this turn."),
            R("iron_reliquary","IRON RELIQUARY",Rarity.Uncommon,"Each set of three different buffs gained during combat grants Resolve. When you would lose all Block between turns, retain 50% instead and consume 1 Resolve."),
            R("hollow_hourglass","HOLLOW HOURGLASS",Rarity.Uncommon,"Every sixth card played during combat gains Echoed before resolving: its primary effect resolves one additional time."),
            R("the_golden_cycle","THE GOLDEN CYCLE",Rarity.Rare,"Complete Attack → Skill → Power in exact order to gain 1 Energy, then reset. A wrong type resets the sequence."),
            R("hollow_crown","HOLLOW CROWN",Rarity.Rare,"Exhaust a non-Temporary card: your next card gains +5 damage and +5 Block where applicable. If it does neither, draw 1 after it resolves."),
            R("fates_convergence","FATE'S CONVERGENCE",Rarity.Rare,"First time each turn you trigger three different mechanic categories, draw 2 and gain 1 Energy. Buff, debuff, Exhaust, Sigil, Temporary generation and Retaliate count."),
            R("crown_of_many_paths","CROWN OF MANY PATHS",Rarity.Rare,"First time each turn you play three different origins, your next card costs 0 that turn. Origins: own character, Wanderer, other character, Temporary/generated."),
            R("paradox_engine","PARADOX ENGINE",Rarity.Rare,"First time each turn a card triggers another gameplay effect, your next DIFFERENT triggered effect activates twice. No recursive duplication."),
            R("black_star_of_ruin","BLACK STAR OF RUIN",Rarity.Rare,"Damage an enemy with three different source categories in one turn to apply Rupture. Its next incoming Attack deals +50% damage, then removes Rupture."),
            R("tome_of_the_unwritten","TOME OF THE UNWRITTEN",Rarity.Rare,"At combat start, mark 3 random deck cards Unwritten. They cost 1 less for the entire combat."),
            R("crown_of_echoes","CROWN OF ECHOES",Rarity.Rare,"Play 5 cards in a turn: gain 1 Reverberation, once per turn. Your next card/Power/buff-triggered effect activates once more; consume 1. Does not replay the card."),
            R("broken_law","BROKEN LAW",Rarity.Rare,"First consecutive same-type pair each turn grants Adaptation. Your next applicable DIFFERENT type gains +50% damage or Block. Non-applicable cards do not consume it."),
            R("soul_of_the_chimera","SOUL OF THE CHIMERA",Rarity.Rare,"First time each turn you play two different origins, gain Chimera. Your next card counts as all origins and gains +25% damage and Block."),
            R("eternal_core","ETERNAL CORE",Rarity.Boss,"Gain 1 additional Energy at the start of every turn."),
            R("crown_of_insight","CROWN OF INSIGHT",Rarity.Boss,"Draw 2 additional cards at the start of every turn."),
            R("gilded_chalice","GILDED CHALICE",Rarity.Boss,"At combat start, gain 2 Strength and 2 Fortify."),
            R("fates_lantern","FATE'S LANTERN",Rarity.Boss,"At combat start, draw 3 additional cards and gain 1 Energy."),
            R("perfected_thread","PERFECTED THREAD",Rarity.Boss,"On obtaining, choose an Attack, Skill and Power to Perfect for this run. Each play permanently adds +1 damage to the Attack or +1 Block to the Skill, then it Exhausts. The Power permanently costs 1 less after each play."),
            R("crown_of_plenty","CROWN OF PLENTY",Rarity.Boss,"At the start of each turn, gain 6 Block."),
            R("endless_tome","ENDLESS TOME",Rarity.Boss,"The first time you play 5 cards each turn, draw 2."),
            R("golden_hourglass","GOLDEN HOURGLASS",Rarity.Boss,"At the start of every third turn, gain 2 Energy and draw 2."),
            R("sovereign_seal","SOVEREIGN SEAL",Rarity.Boss,"Your first Attack each turn gains +8 damage. Your first Skill gains +8 Block if it grants Block."),
            R("thread_of_eternity","THREAD OF ETERNITY",Rarity.Boss,"At the start of each turn, upgrade a random unupgraded card in hand for the rest of combat.")
        };
        public static string[] MajorRelicIds=>Relics.Skip(36).Select(r=>r.id).ToArray();
    }
}
