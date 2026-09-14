using System;

namespace GildedFate.Core
{
    [Serializable] public sealed class EnemyDef
    {
        public string id,name,description; public int hp,baseDamage; public bool elite,boss;
        public EnemyDef(string id,string name,int hp,int damage,string description,bool elite=false,bool boss=false){this.id=id;this.name=name;this.hp=hp;this.baseDamage=damage;this.description=description;this.elite=elite;this.boss=boss;}
    }
    [Serializable] public sealed class BindingDef
    {
        public string id,name,text,target;public int minimumAct;
        public BindingDef(string id,string name,int act,string target,string text){this.id=id;this.name=name;minimumAct=act;this.target=target;this.text=text;}
    }
    [Serializable] public sealed class FateweaveDef
    {
        public string id,name,text;public int act;
        public FateweaveDef(string id,string name,int act,string text){this.id=id;this.name=name;this.act=act;this.text=text;}
    }
    [Serializable] public sealed class FateShardDef
    {
        public string id,name,stableText,fracturedText,archetype;public int stableValue,fracturedValue;
        public FateShardDef(string id,string name,string archetype,int stableValue,int fracturedValue,string stable,string fractured){this.id=id;this.name=name;this.archetype=archetype;this.stableValue=stableValue;this.fracturedValue=fracturedValue;stableText=stable;fracturedText=fractured;}
    }
    public static class WorldContent
    {
        public static readonly EnemyDef[] Enemies={
            E("vault_rat","VAULT RAT",32,7,"A quick scavenger with a taste for enchanted metal."),E("gilded_sentry","GILDED SENTRY",48,9,"An armored guardian still obeying a forgotten command."),
            E("masked_acolyte","MASKED ACOLYTE",41,8,"Its whispered rites weaken intruders."),E("ash_hound","ASH HOUND",44,10,"Each breath feeds the furnace in its ribs."),
            E("coin_mimic","COIN MIMIC",52,11,"A glittering reward with far too many teeth."),E("broken_knight","BROKEN KNIGHT",58,12,"An oath animates the empty armor."),
            E("rune_mage","RUNE MAGE",39,9,"Living script coils around its hands."),E("vault_spider","VAULT SPIDER",36,8,"Its silver web drains strength."),
            E("golden_wisp","GOLDEN WISP",34,7,"A support spirit burning with stolen wealth."),E("chained_brute","CHAINED BRUTE",68,16,"Slow, furious, and strong enough to split stone."),
            E("executioner","THE EXECUTIONER",82,12,"A readable three-beat sentence: strike, bloodlust, execution.",true),E("mirror_witch","THE MIRROR WITCH",80,15,"She returns ambition with a cruel reflection.",true),
            E("golden_beast","THE GOLDEN BEAST",86,17,"Its tempo accelerates as blood is spilled.",true),E("collector","THE COLLECTOR",84,14,"Every strike steals gold—victory returns it with interest.",true),
            E("hollow_king","THE HOLLOW KING",180,21,"A fallen ruler commanding spectral weapons.",false,true),E("vault_mother","THE VAULT MOTHER",205,18,"Ancient flesh fused with the architecture of the Vault.",false,true),
            E("last_dealer","THE LAST DEALER",170,20,"A masked master who deals curses from a black hand.",false,true)
        };
        public static readonly BindingDef[] Bindings={
            B("serrated","SERRATED",1,"Attack","Each individual damage hit deals +2."),B("reinforced","REINFORCED",1,"Block card","Gain +4 base Block."),B("weighted","WEIGHTED",1,"Attack costing 2+","Gain +6 base damage."),B("quickened","QUICKENED",1,"Card costing 1+","The first draw each combat costs 1 less that turn."),
            B("lingering","LINGERING",2,"Skill","The first play each combat returns it to hand at the start of next turn."),B("gilded","GILDED",2,"Attack or Skill","The first play each combat grants 5 Block."),B("focused","FOCUSED",2,"Buff or debuff card","Apply +1 stack of one existing valid stackable buff or debuff."),B("chained","CHAINED",2,"Attack or Skill","After a different card type: +4 damage for an Attack or +4 Block for a Skill."),
            B("recurring","RECURRING",3,"Exhaust card","The first time it Exhausts each combat, shuffle a copy into the draw pile."),B("fateful","FATEFUL",3,"Attack or Skill","Every third play of this card during combat causes one additional non-recursive play."),B("perfected","PERFECTED",3,"Upgraded Attack or Block card","Increase primary base damage or Block by 25%, rounded up.")
        };
        public static readonly FateweaveDef[] Fateweaves={
            F("foreign_memory","FOREIGN MEMORY",1,"Reveal 3 random cards from other characters. Choose 1."),F("wanderers_thread","WANDERER'S THREAD",1,"Reveal 3 random Wanderer Cards. Choose 1."),F("gilded_cache","GILDED CACHE",1,"Gain 1 random Common Relic."),F("favorable_hand","FAVORABLE HAND",1,"Reveal 3 random cards from your character. Choose 1."),F("severed_burden","SEVERED BURDEN",1,"Remove exactly 2 cards, then lose 16 HP after the full heal."),F("burdened_fortune","BURDENED FORTUNE",1,"Gain 2 random Common Relics. Add 1 Strike and 1 Defend."),
            F("gilded_edge","GILDED EDGE",2,"Choose 2 Attacks. Permanently +4 damage."),F("gilded_guard","GILDED GUARD",2,"Choose 2 Block Skills. Permanently +5 Block."),F("first_light","FIRST LIGHT",2,"Choose 3 cards costing 1–2. Their first draw each combat costs 0 that turn."),F("deepened_power","DEEPENED POWER",2,"Choose 2 stackable buff/debuff cards. Each applies +1 stack."),F("entangled_fates","ENTANGLED FATES",2,"Reveal 3 cards from the other character. Choose 1 upgraded."),F("heavy_crown","HEAVY CROWN",2,"Gain 1 random Uncommon Relic. Add 2 Dazed Mind for the next 3 combats."),
            F("perfected_edge","PERFECTED EDGE",3,"Choose 1 Attack. Increase base damage permanently by 50%, rounded up."),F("perfected_guard","PERFECTED GUARD",3,"Choose 1 Block Skill. Increase base Block permanently by 50%, rounded up."),F("golden_echo","GOLDEN ECHO",3,"Choose an Attack or Skill costing 2 or less. Its first play each combat plays twice."),F("unbound_thread","UNBOUND THREAD",3,"Choose 2 cards costing 2+. Their first draw each combat costs 0 that turn."),F("stolen_destiny","STOLEN DESTINY",3,"Reveal one Knight Rare, one Arcane Rare, and one Wanderer Rare. Choose 1."),F("fortunes_burden","FORTUNE'S BURDEN",3,"Gain 1 random Rare Relic. Add 1 Strike, 1 Defend, and 1 random Curse.")
        };
        public static readonly FateShardDef[] FateShards={
            S("bloodstone","BLOODSTONE","Strength",1,2,"Whenever you gain Strength, gain 1 additional Strength.","Start combat with 3 Strength. Whenever you gain Strength, gain 2 additional Strength."),
            S("ironheart","IRONHEART","Block",50,100,"Whenever a card gives 10+ Block, gain 50% of that Block again.","Whenever a card gives Block, gain that amount again."),
            S("quickglass","QUICKGLASS","Cost",1,2,"The first card drawn each turn with an original cost of 2+ costs 0 that turn.","The first 2 cards drawn each turn with an original cost of 2+ cost 0 that turn."),
            S("crooked","CROOKED","Cost",4,3,"Every 4th card played each turn costs 0 before being played.","Every 3rd card played each turn costs 0 and draws 1 after resolving."),
            S("hourglass","HOURGLASS","Energy",2,0,"Retain unused Energy between turns, up to 2.","Retain ALL unused Energy. Start combat with +2 Energy."),
            S("silvermind","SILVERMIND","Draw",2,2,"Draw 2 additional cards on turn one. Your first extra draw each turn draws 1 more.","Draw 2 additional cards EVERY turn."),
            S("golden_shield","GOLDEN SHIELD","Block",1,2,"The first Block gain each turn is gained again.","The first 2 Block gains each turn are gained again."),
            S("execution","EXECUTION","Attack",50,100,"Attacks deal 50% more damage to enemies below 30% HP.","Attacks deal DOUBLE damage to enemies below 50% HP."),
            S("firstblood","FIRSTBLOOD","Attack",50,2,"Your first Attack each turn deals 50% more damage.","Your first Attack each turn plays twice."),
            S("balanced","BALANCED","Block",12,20,"End your turn at exactly 0 Energy: gain 12 Block and draw 1 additional card next turn.","End at exactly 0 Energy: gain 20 Block, +1 Energy next turn and +2 draw next turn."),
            S("giantglass","GIANTGLASS","Heavy",50,75,"Your first Attack costing 2+ each turn deals 50% more damage.","ALL Attacks with an original cost of 2+ deal 75% more damage."),
            S("thousand_cut","THOUSAND-CUT","Multi-hit",3,5,"Multi-hit Attacks gain +3 damage PER HIT.","Multi-hit Attacks gain +5 per hit and their final hit deals double damage."),
            S("rhythm","RHYTHM","Momentum",4,3,"Every 4th Attack played each turn plays twice.","Every 3rd Attack played each turn plays twice."),
            S("duelist","DUELIST","Heavy",75,100,"Your first Attack each turn deals 75% more damage.","Your first Attack each turn deals DOUBLE damage. If it is your only Attack that turn, draw 2 at the end of the turn."),
            S("aftershock","AFTERSHOCK","Heavy",8,12,"After an Attack costing 2+, your next Attack this turn costs 0 and deals +8 damage.","After an Attack costing 2+, your next 2 Attacks this turn cost 0 and deal +12 damage."),
            S("bastion","BASTION","Block",15,10,"Whenever a single card gives 15+ Block, gain 1 Fortify and draw 1.","Whenever a single card gives 10+ Block, gain 2 Fortify and draw 1. Up to 3 triggers per turn."),
            S("thorn","THORN","Retaliate",10,20,"Whenever an enemy damages your Block, deal 10 damage back.","Whenever an enemy damages your Block, deal 20 damage back and regain 25% of the Block lost from that hit."),
            S("overflow","OVERFLOW","Block",15,0,"Retain up to 15 Block between turns.","Retain ALL remaining Block between turns."),
            S("counterweight","COUNTERWEIGHT","Block",12,15,"Every 15 Block gained during a turn gives your next Attack +12 damage. Stacks.","Every 10 Block gained gives your next Attack +15 damage. Stacks without a per-turn limit."),
            S("emberglass","EMBERGLASS","Burn",1,2,"The first Burn application each turn is applied TWICE.","Every second Burn application is applied TWICE."),
            S("ash","ASH","Burn",50,100,"The first time Burn deals damage each turn, trigger 50% of that enemy's remaining Burn again.","The first time EACH enemy's Burn deals damage each turn, trigger its FULL remaining Burn again."),
            S("omen","OMEN","Debuff",1,3,"The first stackable debuff applied each turn gains DOUBLE stacks.","The first 3 stackable debuffs applied each turn gain DOUBLE stacks."),
            S("executioners","EXECUTIONER'S","Consume",6,12,"Consuming or removing enemy debuff stacks with your effects deals 6 damage per stack.","Deal 12 damage per stack consumed. Draw 1 after consuming debuffs, up to 2 bonus draws per turn."),
            S("affliction","AFFLICTION","Debuff",4,8,"Attacks deal +4 damage per different debuff on their target.","Attacks deal +8 per different debuff. The first Attack against a debuffed enemy each turn plays twice."),
            S("echo","ECHO","Echo",4,3,"Every 4th card played each turn plays twice. Replays do not advance this counter.","Every 3rd card played each turn plays twice. Replays do not advance this counter."),
            S("hollow","HOLLOW","Exhaust",1,3,"The first card Exhausted each turn draws 2 cards.","The first 3 cards Exhausted each turn each draw 2 cards."),
            S("greedy","GREEDY","Draw",4,8,"Each card drawn outside normal start-of-turn draw gives your next Attack +4 damage this turn. Stacks.","Each extra card drawn gives your next Attack +8 damage. Every third extra draw restores 1 Energy."),
            S("cycle","CYCLE","Sequence",1,3,"Complete an Attack + Skill + Power set in a turn: draw 2 and gain 1 Energy. Once per turn.","Complete up to 3 NEW Attack + Skill + Power sets per turn. Each draws 2 and grants 1 Energy."),
            S("voidglass","VOIDGLASS","Curse",8,10,"The first Curse or Status drawn each turn immediately Exhausts. Draw 1 and gain 8 Block.","The first 3 Curses or Statuses drawn each turn immediately Exhaust. Each draws 1, grants 10 Block and 1 Energy."),
            S("fatebreaker","FATEBREAKER","Modified",1,2,"The first permanently modified card played each turn plays twice. Bindings qualify.","The first 2 modified cards each turn play twice and draw 1 after resolving. Replays cannot trigger this again.")
        };
        public static readonly EventDefinition[] Events=EventContent.All;
        private static EnemyDef E(string id,string n,int hp,int d,string t,bool e=false,bool b=false)=>new(id,n,hp,d,t,e,b);
        private static BindingDef B(string id,string n,int a,string target,string text)=>new(id,n,a,target,text);
        private static FateweaveDef F(string id,string n,int a,string text)=>new(id,n,a,text);
        private static FateShardDef S(string id,string n,string archetype,int stable,int fractured,string stableText,string fracturedText)=>new(id,n,archetype,stable,fractured,stableText,fracturedText);
    }
}
