using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Core
{
    public enum HeroId { Vanguard, Hexer, Reaper }
    public enum NodeKind { Combat, Elite, Event, Treasure, Merchant, Sanctuary, Boss }
    public enum IntentKind { Attack, Defend, Buff, Debuff, Special, Unknown }
    public enum Rarity { Basic, Common, Uncommon, Rare, Boss, Special, Curse, Status }
    public enum EffectKind { Damage, Block, Draw, Strength, Fortify, Retaliate, Resonance, Burn, Mark, Heal, Vulnerable, Weak, Power, Energy, Sigil, Exhaust, None }
    public enum CardKind { Attack, Skill, Power, Curse, Status }
    // Reaper is appended so existing serialized origin values keep their meaning.
    public enum CardOrigin { Knight, Arcane, Wanderer, Curse, Status, Reaper }
    public enum SpecialModificationKind { None, Binding, Fateweave }

    [Serializable]
    public sealed class CardDef
    {
        public string id,name,text,upgradeText;
        [NonSerialized] public HeroId? hero;
        public CardOrigin origin;
        public Rarity rarity;
        public int cost,value,secondary,hits=1;
        public int upgradeCost=-1,upgradeValue=int.MinValue,upgradeSecondary=int.MinValue;
        public EffectKind effect;
        public CardKind kind;
        public bool upgraded,exhaust,ethereal,unplayable;
        public int instanceId;
        public string[] keywords=Array.Empty<string>();
        public string persistentId="",specialModification="";
        public SpecialModificationKind specialModificationKind;
        public int permanentDamageBonus,permanentBlockBonus;
        public bool perfected,temporary;
        public int perfectedCostReduction,perfectedGrowth;
        public bool firstDrawFree;

        public CardDef(){}
        public CardDef(string id,string name,HeroId? hero,CardOrigin origin,Rarity rarity,int cost,CardKind kind,EffectKind effect,int value,string text,string upgradeText)
        {this.id=id;this.name=name;this.hero=hero;this.origin=origin;this.rarity=rarity;this.cost=cost;this.kind=kind;this.effect=effect;this.value=value;this.text=text;this.upgradeText=upgradeText;}
        public CardDef(string id,string name,HeroId? hero,Rarity rarity,int cost,EffectKind effect,int value,string text,int secondary=0)
            :this(id,name,hero,hero==HeroId.Vanguard?CardOrigin.Knight:hero==HeroId.Hexer?CardOrigin.Arcane:hero==HeroId.Reaper?CardOrigin.Reaper:rarity==Rarity.Curse?CardOrigin.Curse:CardOrigin.Wanderer,rarity,cost,rarity==Rarity.Curse?CardKind.Curse:effect==EffectKind.Damage?CardKind.Attack:effect==EffectKind.Power?CardKind.Power:CardKind.Skill,effect,value,text,text)
        {this.secondary=secondary;}
        public bool IsModified=>specialModificationKind!=SpecialModificationKind.None&&!string.IsNullOrEmpty(specialModification);
        public bool ShowsEnergyCost=>!unplayable&&kind is not (CardKind.Curse or CardKind.Status)&&rarity is not (Rarity.Curse or Rarity.Status);
        public CardDef Copy()=>new(id,name,hero,origin,rarity,cost,kind,effect,value,text,upgradeText)
        {
            secondary=secondary,hits=hits,upgradeCost=upgradeCost,upgradeValue=upgradeValue,upgradeSecondary=upgradeSecondary,
            upgraded=upgraded,exhaust=exhaust,ethereal=ethereal,unplayable=unplayable,instanceId=instanceId,
            keywords=keywords?.ToArray()??Array.Empty<string>(),persistentId=persistentId,specialModification=specialModification,
            specialModificationKind=specialModificationKind,permanentDamageBonus=permanentDamageBonus,permanentBlockBonus=permanentBlockBonus,firstDrawFree=firstDrawFree,perfected=perfected,temporary=temporary,perfectedCostReduction=perfectedCostReduction,perfectedGrowth=perfectedGrowth
        };
    }

    [Serializable]
    public sealed class RelicDef
    {
        public string id,name,text;public Rarity rarity;
        public RelicDef(string id,string name,Rarity rarity,string text){this.id=id;this.name=name;this.rarity=rarity;this.text=text;}
    }

    public static partial class GameContent
    {
        public const int BurnDecayPerTrigger=1;
        public static readonly CardDef[] Cards=BuildCards();
        public static readonly RelicDef[] Relics=BuildRelics();

        public static CardDef[] CardRewards(HeroId hero,int seed)=>CardRewards(hero,seed,3,false,false);
        public static CardDef[] CardRewards(HeroId hero,int seed,int count,bool includeWanderer,bool includeOtherHero)
        {
            var own=hero==HeroId.Vanguard?CardOrigin.Knight:hero==HeroId.Hexer?CardOrigin.Arcane:CardOrigin.Reaper;
            var pool=Cards.Where(c=>c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare)
                .Where(c=>c.origin==own||includeWanderer&&c.origin==CardOrigin.Wanderer||includeOtherHero&&(c.origin==CardOrigin.Knight||c.origin==CardOrigin.Arcane||c.origin==CardOrigin.Reaper)).ToList();
            var random=new Random(seed);var weighted=new List<CardDef>();
            foreach(var card in pool){var weight=card.rarity==Rarity.Common?6:card.rarity==Rarity.Uncommon?3:1;for(var i=0;i<weight;i++)weighted.Add(card);}
            var result=new List<CardDef>();while(result.Count<count&&weighted.Count>0){var pick=weighted[random.Next(weighted.Count)];result.Add(pick);weighted.RemoveAll(c=>c.id==pick.id);}
            return result.ToArray();
        }

        public static CardDef Upgrade(CardDef card)
        {
            if(card==null)return null;if(card.id=="soul"){var soul=card.Copy();soul.upgraded=true;soul.name="SOUL+";soul.value=5;soul.text="Deal 5 damage. Draw 1. Exhaust.";return soul;}if(card.rarity is Rarity.Curse or Rarity.Status or Rarity.Special)return card.Copy();
            var result=card.Copy();result.upgraded=true;result.name=card.name.EndsWith("+",StringComparison.Ordinal)?card.name:card.name+"+";
            if(card.upgradeCost>=0)result.cost=card.upgradeCost;if(card.upgradeValue!=int.MinValue)result.value=card.upgradeValue;if(card.upgradeSecondary!=int.MinValue)result.secondary=card.upgradeSecondary;
            if(!string.IsNullOrWhiteSpace(card.upgradeText))result.text=card.upgradeText;
            if(card.id is "ritual_spark" or "mark_of_the_grave" or "twist_of_fate"){result.exhaust=false;result.keywords=result.keywords.Where(k=>k!="Exhaust").ToArray();}
            if(card.id=="battle_rush"){result.exhaust=false;result.keywords=result.keywords.Where(k=>k!="Exhaust").ToArray();}
            return result;
        }

        public static bool HasUpgradePreview(CardDef card)=>card!=null&&Find(card.id)!=null&&card.rarity is not (Rarity.Curse or Rarity.Status or Rarity.Special);
        public static CardDef InspectionVariant(CardDef source,bool upgraded)
        {
            if(source==null)return null;
            var authored=Find(source.id);if(authored==null||!HasUpgradePreview(source))return source.Copy();
            var result=upgraded?Upgrade(authored):authored.Copy();
            // Comparing versions must never upgrade/downgrade the owned instance.
            result.instanceId=source.instanceId;result.persistentId=source.persistentId;
            result.specialModification=source.specialModification;result.specialModificationKind=source.specialModificationKind;
            result.perfected=source.perfected;result.perfectedCostReduction=source.perfectedCostReduction;result.perfectedGrowth=source.perfectedGrowth;result.temporary=source.temporary;result.permanentDamageBonus=source.permanentDamageBonus;result.permanentBlockBonus=source.permanentBlockBonus;result.firstDrawFree=source.firstDrawFree;
            return result;
        }

        public static CardDef Find(string id)=>Array.Find(Cards,c=>c.id==id);
        public static string Describe(CardDef card)=>card?.text??"";

        private static CardDef D(string id,string name,HeroId? hero,CardOrigin origin,Rarity rarity,int cost,CardKind kind,EffectKind effect,int value,string text,string plus,int plusValue=int.MinValue,int secondary=0,int plusSecondary=int.MinValue,int plusCost=-1,int hits=1,bool exhaust=false,bool ethereal=false,bool unplayable=false,string keywords="")
        {
            var card=new CardDef(id,name,hero,origin,rarity,cost,kind,effect,value,text,plus){upgradeValue=plusValue,secondary=secondary,upgradeSecondary=plusSecondary,upgradeCost=plusCost,hits=hits,exhaust=exhaust,ethereal=ethereal,unplayable=unplayable};
            card.keywords=string.IsNullOrWhiteSpace(keywords)?Array.Empty<string>():keywords.Split(',').Select(k=>k.Trim()).Where(k=>k.Length>0).ToArray();return card;
        }

        private static CardDef[] BuildCards()
        {
            var c=new List<CardDef>();
            void K(string id,string n,Rarity r,int cost,CardKind kind,EffectKind effect,int value,string text,string plus,int pv=int.MinValue,int secondary=0,int ps=int.MinValue,int pc=-1,int hits=1,bool exhaust=false,string keys="")=>c.Add(D(id,n,HeroId.Vanguard,CardOrigin.Knight,r,cost,kind,effect,value,text,plus,pv,secondary,ps,pc,hits,exhaust,false,false,keys));
            void A(string id,string n,Rarity r,int cost,CardKind kind,EffectKind effect,int value,string text,string plus,int pv=int.MinValue,int secondary=0,int ps=int.MinValue,int pc=-1,int hits=1,bool exhaust=false,string keys="")=>c.Add(D(id,n,HeroId.Hexer,CardOrigin.Arcane,r,cost,kind,effect,value,text,plus,pv,secondary,ps,pc,hits,exhaust,false,false,keys));
            void RP(string id,string n,Rarity r,int cost,CardKind kind,EffectKind effect,int value,string text,string plus,int pv=int.MinValue,int secondary=0,int ps=int.MinValue,int pc=-1,int hits=1,bool exhaust=false,string keys="")=>c.Add(D(id,n,HeroId.Reaper,CardOrigin.Reaper,r,cost,kind,effect,value,text,plus,pv,secondary,ps,pc,hits,exhaust,false,false,keys));
            void W(string id,string n,Rarity r,int cost,CardKind kind,EffectKind effect,int value,string text,string plus,int pv=int.MinValue,int secondary=0,int ps=int.MinValue,int pc=-1,int hits=1,bool exhaust=false,string keys="")=>c.Add(D(id,n,null,CardOrigin.Wanderer,r,cost,kind,effect,value,text,plus,pv,secondary,ps,pc,hits,exhaust,false,false,keys));

            K("strike","STRIKE",Rarity.Basic,1,CardKind.Attack,EffectKind.Damage,6,"Deal 6 damage.","Deal 9 damage.",9);
            K("defend","DEFEND",Rarity.Basic,1,CardKind.Skill,EffectKind.Block,5,"Gain 5 Block.","Gain 8 Block.",8,keys:"Block");
            K("battle_temper","BATTLE TEMPER",Rarity.Common,1,CardKind.Power,EffectKind.Power,2,"Whenever you gain Strength, gain 2 Block.","Whenever you gain Strength, gain 3 Block.",3,keys:"Strength,Block");
            K("battle_cry","BATTLE CRY",Rarity.Basic,1,CardKind.Skill,EffectKind.Strength,2,"Gain 2 Strength this turn. Draw 1.","Gain 3 Strength this turn. Draw 1.",3,secondary:1,ps:1,keys:"Strength");
            K("stand_firm","STAND FIRM",Rarity.Basic,1,CardKind.Skill,EffectKind.Block,6,"Gain 6 Block. Gain 1 Fortify this turn.","Gain 8 Block. Gain 1 Fortify this turn.",8,secondary:1,ps:1,keys:"Block,Fortify");
            K("iron_will","IRON WILL",Rarity.Common,1,CardKind.Skill,EffectKind.Block,7,"Gain 7 Block. Gain 1 Fortify.","Gain 9 Block. Gain 1 Fortify.",9,secondary:1,ps:1,keys:"Block,Fortify");
            K("armored_strike","ARMORED STRIKE",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage. If you have Fortify, gain 4 Block.","Deal 9 damage. If you have Fortify, gain 5 Block.",9,secondary:4,ps:5,keys:"Fortify,Block");
            K("iron_blood","IRON BLOOD",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,1,"Whenever you gain Fortify, gain 1 Strength.","Gain 1 Fortify. Whenever you gain Fortify, gain 1 Strength.",1,secondary:0,ps:1,keys:"Fortify,Strength");
            K("living_armor","LIVING ARMOR",Rarity.Uncommon,2,CardKind.Power,EffectKind.Power,1,"At the start of your turn, gain Block equal to your Strength.","At the start of your turn, gain Block equal to your Strength.",pc:1,keys:"Strength,Block");
            K("war_machine","WAR MACHINE",Rarity.Uncommon,2,CardKind.Power,EffectKind.Power,1,"The first buff you gain each turn gains +1 stack.","The first two buffs you gain each turn gain +1 stack.",2,keys:"Buff");
            K("crown_breaker","CROWN BREAKER",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,10,"Deal 10 + Strength + Fortify damage.","Deal 14 + Strength + Fortify damage.",14,keys:"Strength,Fortify");
            K("unbreakable_spirit","UNBREAKABLE SPIRIT",Rarity.Rare,3,CardKind.Power,EffectKind.Power,1,"Once per turn, gaining Strength grants equal Fortify.\nOnce per turn, gaining Fortify grants equal Strength.","Once per turn, gaining Strength grants equal Fortify.\nOnce per turn, gaining Fortify grants equal Strength.",pc:2,keys:"Strength,Fortify");
            K("great_cleave","GREAT CLEAVE",Rarity.Common,2,CardKind.Attack,EffectKind.Damage,16,"Deal 16 damage. Heavy: deal 22 instead.","Deal 19 damage. Heavy: deal 26 instead.",19,secondary:22,ps:26,keys:"Heavy");
            K("brace","BRACE",Rarity.Common,1,CardKind.Skill,EffectKind.Block,7,"Gain 7 Block. Your next Heavy Attack costs 1 less.","Gain 10 Block. Your next Heavy Attack costs 1 less.",10,keys:"Block,Heavy");
            K("crushing_blow","CRUSHING BLOW",Rarity.Common,2,CardKind.Attack,EffectKind.Damage,14,"Deal 14 damage. Apply 1 Vulnerable.","Deal 18 damage. Apply 1 Vulnerable.",18,secondary:1,ps:1,keys:"Vulnerable");
            K("overhead_strike","OVERHEAD STRIKE",Rarity.Uncommon,3,CardKind.Attack,EffectKind.Damage,25,"Deal 25 damage. Heavy: apply 2 Vulnerable.","Deal 30 damage. Heavy: apply 3 Vulnerable.",30,secondary:2,ps:3,keys:"Heavy,Vulnerable");
            K("crushing_weight","CRUSHING WEIGHT",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,14,"Deal 14 + Fortify damage.","Deal 18 + Fortify damage.",18,keys:"Fortify");
            K("patient_warrior","PATIENT WARRIOR",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,5,"Heavy Attacks deal +5 damage.","Heavy Attacks deal +8 damage.",8,keys:"Heavy");
            K("executioners_cleave","EXECUTIONER'S CLEAVE",Rarity.Rare,2,CardKind.Attack,EffectKind.Damage,28,"Deal 28 damage. Heavy: Deal 40 damage instead.","Deal 28 damage. Heavy: Deal 40 damage instead.",28,secondary:40,ps:40,pc:1,keys:"Heavy");
            K("final_judgment","FINAL JUDGMENT",Rarity.Rare,3,CardKind.Attack,EffectKind.Damage,24,"Deal 24 + twice your Strength damage.","Deal 30 + twice your Strength damage.",30,keys:"Strength");
            K("spiked_guard","SPIKED GUARD",Rarity.Common,1,CardKind.Skill,EffectKind.Block,6,"Gain 6 Block and 2 Retaliate.","Gain 8 Block and 3 Retaliate.",8,secondary:2,ps:3,keys:"Block,Retaliate");
            K("payback","PAYBACK",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage. Revenge: deal 14 instead.","Deal 9 damage. Revenge: deal 18 instead.",9,secondary:14,ps:18,keys:"Revenge");
            K("come_at_me","COME AT ME",Rarity.Common,1,CardKind.Skill,EffectKind.Block,9,"Gain 9 Block and 3 Retaliate.","Gain 12 Block and 3 Retaliate.",12,secondary:3,ps:3,keys:"Block,Retaliate");
            K("no_mercy","NO MERCY",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage. If Retaliate triggered this turn, deal 8 again.","Deal 11 damage. If Retaliate triggered this turn, deal 11 again.",11,secondary:8,ps:11,keys:"Retaliate");
            K("shield_wall","SHIELD WALL",Rarity.Uncommon,2,CardKind.Skill,EffectKind.Block,15,"Gain 15 Block and 1 Fortify.","Gain 19 Block and 1 Fortify.",19,secondary:1,ps:1,keys:"Block,Fortify");
            K("retribution","RETRIBUTION",Rarity.Uncommon,2,CardKind.Power,EffectKind.Power,1,"Whenever Retaliate triggers, gain 1 Strength this turn.","Whenever Retaliate triggers, gain 2 Strength this turn.",2,keys:"Retaliate,Strength");
            K("hold_the_line","HOLD THE LINE",Rarity.Uncommon,2,CardKind.Power,EffectKind.Power,2,"Whenever Retaliate triggers, gain 2 Block.","Whenever Retaliate triggers, gain 3 Block.",3,keys:"Retaliate,Block");
            K("eye_for_an_eye","EYE FOR AN EYE",Rarity.Rare,2,CardKind.Skill,EffectKind.Damage,0,"Deal damage to an enemy equal to the Attack damage it attempted against you before Block this turn. Exhaust.","Deal damage to an enemy equal to the Attack damage it attempted against you before Block this turn. Exhaust.",0,pc:1,exhaust:true,keys:"Block,Exhaust");
            K("last_bastion","LAST BASTION",Rarity.Rare,3,CardKind.Skill,EffectKind.Block,25,"Gain 25 Block and 5 Retaliate. Exhaust.","Gain 30 Block and 7 Retaliate. Exhaust.",30,secondary:5,ps:7,exhaust:true,keys:"Block,Retaliate,Exhaust");
            K("quick_slash","QUICK SLASH",Rarity.Common,0,CardKind.Attack,EffectKind.Damage,3,"Deal 3 damage.","Deal 5 damage.",5);
            K("advance","ADVANCE",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,6,"Deal 6 damage. Draw 1.","Deal 9 damage. Draw 1.",9,secondary:1,ps:1);
            K("battle_rush","BATTLE RUSH",Rarity.Common,0,CardKind.Skill,EffectKind.None,1,"Your next Attack this turn costs 1 less. Exhaust.","Your next Attack this turn costs 1 less. Draw 1.",1,secondary:0,ps:1,exhaust:true,keys:"Exhaust");
            K("rising_strike","RISING STRIKE",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,5,"Deal 5 damage, plus 3 per Attack played this turn.","Deal 7 damage, plus 4 per Attack played this turn.",7,secondary:3,ps:4);
            K("relentless","RELENTLESS",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,6,"Every third Attack each turn deals +6 damage.","Every third Attack each turn deals +9 damage.",9);
            K("blood_rush","BLOOD RUSH",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Energy,1,"Lose 2 HP. Gain 1 Energy. Draw 1 Attack.","Lose 1 HP. Gain 1 Energy. Draw 1 Attack.",1,secondary:2,ps:1);
            K("flurry","FLURRY",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,3,"Deal 3 damage three times.","Deal 4 damage three times.",4,hits:3);
            K("onslaught","ONSLAUGHT",Rarity.Rare,2,CardKind.Power,EffectKind.Power,4,"After playing 4 Attacks in one turn, gain 1 Energy once per turn.","After playing 3 Attacks in one turn, gain 1 Energy once per turn.",3);
            K("grand_finale","GRAND FINALE",Rarity.Rare,2,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage three times. Costs 1 less if you already played 3 Attacks this turn.","Deal 10 damage three times. Costs 1 less if you already played 3 Attacks this turn.",10,secondary:3,ps:3,hits:3);
            K("bloodied_armor","BLOODIED ARMOR",Rarity.Common,1,CardKind.Skill,EffectKind.Block,8,"Gain 8 Block. If you have Strength, gain 2 Retaliate.","Gain 10 Block. If you have Strength, gain 3 Retaliate.",10,secondary:2,ps:3,keys:"Strength,Block,Retaliate");
            K("forceful_guard","FORCEFUL GUARD",Rarity.Common,1,CardKind.Skill,EffectKind.Block,6,"Gain 6 Block. Your next Attack deals +4 damage.","Gain 8 Block. Your next Attack deals +6 damage.",8,secondary:4,ps:6,keys:"Block");
            K("raging_blow","RAGING BLOW",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage, plus 2 per buff currently held.","Deal 10 damage, plus 3 per buff currently held.",10,secondary:2,ps:3);
            K("countercharge","COUNTERCHARGE",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage. If Retaliate triggered this turn, gain 1 Energy.","Deal 10 damage. If Retaliate triggered this turn, gain 1 Energy.",10,secondary:1,ps:1,keys:"Retaliate");
            K("second_wind","SECOND WIND",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Block,5,"Gain 5 Block per Power in play.","Gain 7 Block per Power in play.",7,keys:"Block");
            K("blood_price","BLOOD PRICE",Rarity.Uncommon,0,CardKind.Skill,EffectKind.Strength,2,"Lose 3 HP. Gain 2 Strength this turn.","Lose 2 HP. Gain 3 Strength this turn.",3,secondary:3,ps:2,keys:"Strength");
            K("indomitable","INDOMITABLE",Rarity.Rare,2,CardKind.Power,EffectKind.Power,5,"The first time you lose all Block each enemy turn, gain 5 Block at the start of your next turn.","The first time you lose all Block each enemy turn, gain 8 Block at the start of your next turn.",8,keys:"Block");

            A("hex_strike","HEX STRIKE",Rarity.Basic,1,CardKind.Attack,EffectKind.Damage,6,"Deal 6 damage.","Deal 9 damage.",9);
            A("ward","WARD",Rarity.Basic,1,CardKind.Skill,EffectKind.Block,5,"Gain 5 Block.","Gain 8 Block.",8,keys:"Block");
            A("ember_ritual","EMBER RITUAL",Rarity.Common,1,CardKind.Skill,EffectKind.Sigil,0,"Create an Ember Sigil.","Create an Ember Sigil. Gain 1 Resonance.",0,secondary:0,ps:1,keys:"Sigil,Resonance,Burn");
            A("hex_ritual","HEX RITUAL",Rarity.Common,1,CardKind.Skill,EffectKind.Sigil,1,"Create a Hex Sigil.","Create a Hex Sigil. Gain 1 Resonance.",1,secondary:0,ps:1,keys:"Sigil,Resonance,Marked");
            A("echo_ritual","ECHO RITUAL",Rarity.Common,1,CardKind.Skill,EffectKind.Sigil,2,"Create an Echo Sigil.","Create an Echo Sigil. Gain 1 Resonance.",2,secondary:0,ps:1,keys:"Sigil,Resonance");
            A("invocation","INVOCATION",Rarity.Basic,1,CardKind.Skill,EffectKind.Sigil,3,"Activate your leftmost Sigil. If no Sigils exist, gain 4 Block instead.","Activate your leftmost Sigil. If no Sigils exist, gain 4 Block instead.",pc:0,keys:"Sigil,Resonance,Block");
            A("first_ritual","FIRST RITUAL",Rarity.Basic,1,CardKind.Skill,EffectKind.Sigil,0,"Choose Ember, Hex, or Echo Sigil and create it.","Choose Ember, Hex, or Echo Sigil and create it. Gain 1 Resonance.",0,secondary:0,ps:1,keys:"Sigil,Resonance");
            A("arcane_ward","ARCANE WARD",Rarity.Common,1,CardKind.Skill,EffectKind.Block,7,"Gain 7 Block and 1 Resonance.","Gain 10 Block and 1 Resonance.",10,secondary:1,ps:1,keys:"Block,Resonance");
            A("shatter_sigil","SHATTER SIGIL",Rarity.Uncommon,0,CardKind.Skill,EffectKind.Sigil,2,"Destroy a Sigil; activate it twice first.","Destroy a Sigil; activate it three times first.",3,keys:"Sigil");
            A("ritual_cycle","RITUAL CYCLE",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Sigil,1,"Activate a Sigil, gain 1 Resonance, and draw 1.","Activate a Sigil, gain 2 Resonance, and draw 1.",1,secondary:1,ps:2,keys:"Sigil,Resonance");
            A("sigil_mastery","SIGIL MASTERY",Rarity.Uncommon,2,CardKind.Power,EffectKind.Power,1,"The first Sigil created each combat activates immediately.","The first Sigil created each combat activates immediately.",pc:1,keys:"Sigil");
            A("perfect_ritual","PERFECT RITUAL",Rarity.Rare,2,CardKind.Skill,EffectKind.Sigil,2,"Activate all Sigils twice.","Activate all Sigils twice.",pc:1,keys:"Sigil,Resonance");
            A("grand_convergence","GRAND CONVERGENCE",Rarity.Rare,3,CardKind.Power,EffectKind.Power,1,"At the end of your turn, if three or more Sigil slots are filled, activate each filled Sigil.","At the end of your turn, if three or more Sigil slots are filled, activate each filled Sigil.",pc:2,keys:"Sigil");
            A("cinder","CINDER",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,5,"Deal 5 damage. Apply 4 Burn.","Deal 7 damage. Apply 5 Burn.",7,secondary:4,ps:5,keys:"Burn");
            A("scorch","SCORCH",Rarity.Common,1,CardKind.Skill,EffectKind.Burn,6,"Apply 6 Burn.","Apply 9 Burn.",9,keys:"Burn");
            A("kindle","KINDLE",Rarity.Common,0,CardKind.Skill,EffectKind.Burn,2,"Apply 2 Burn. Gain 1 Resonance.","Apply 3 Burn. Gain 2 Resonance.",3,secondary:1,ps:2,keys:"Burn,Resonance");
            A("wildfire","WILDFIRE",Rarity.Uncommon,2,CardKind.Skill,EffectKind.Burn,5,"Apply 5 Burn to ALL enemies.","Apply 8 Burn to ALL enemies.",8,keys:"Burn");
            A("feed_the_flame","FEED THE FLAME",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Burn,50,"Increase enemy Burn by 50%.","Increase enemy Burn by 75%.",75,keys:"Burn");
            A("ignite","IGNITE",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Burn,50,"Trigger 50% of the enemy's Burn immediately.","Trigger 75% of the enemy's Burn immediately.",75,keys:"Burn");
            A("ashes","ASHES",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,1,"The first time Burn deals damage each turn, gain 1 Resonance.","The first time Burn deals damage each turn, gain 2 Resonance.",2,keys:"Burn,Resonance");
            A("cremation","CREMATION",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,12,"Deal 12 damage. If the enemy has 10+ Burn, deal 12 again.","Deal 15 damage. If the enemy has 10+ Burn, deal 15 again.",15,secondary:10,ps:10,keys:"Burn");
            A("inferno","INFERNO",Rarity.Rare,3,CardKind.Skill,EffectKind.Burn,100,"Double enemy Burn. Exhaust.","Double enemy Burn. Exhaust.",pc:2,exhaust:true,keys:"Burn,Exhaust");
            A("hex","HEX",Rarity.Common,0,CardKind.Skill,EffectKind.Mark,1,"Apply 1 Marked.","Apply 2 Marked.",2,keys:"Marked");
            A("marked_shot","MARKED SHOT",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage. Apply 1 Marked.","Deal 9 damage. Apply 1 Marked.",9,secondary:1,ps:1,keys:"Marked");
            A("soul_pierce","SOUL PIERCE",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage. Consume 1 Marked for +8 damage.","Deal 10 damage. Consume 1 Marked for +10 damage.",10,secondary:8,ps:10,keys:"Marked");
            A("expose","EXPOSE",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Vulnerable,2,"Consume 1 Marked. Apply 2 Vulnerable.","Consume 1 Marked. Apply 3 Vulnerable.",3,keys:"Marked,Vulnerable");
            A("soul_drain","SOUL DRAIN",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Energy,1,"Consume 1 Marked. Gain 1 Energy and draw 1.","Consume 1 Marked. Gain 1 Energy and draw 2.",1,secondary:1,ps:2,keys:"Marked");
            A("hexed_blade","HEXED BLADE",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,14,"Deal 14 damage. If the target is Marked, gain 2 Resonance.","Deal 18 damage. If the target is Marked, gain 3 Resonance.",18,secondary:2,ps:3,keys:"Marked,Resonance");
            A("deaths_gaze","DEATH'S GAZE",Rarity.Uncommon,2,CardKind.Power,EffectKind.Power,1,"The first time you consume Marked each turn, draw 1.","The first time you consume Marked each turn, draw 1.",pc:1,keys:"Marked");
            A("execution_hex","EXECUTION HEX",Rarity.Rare,2,CardKind.Attack,EffectKind.Damage,12,"Deal 12 damage, plus 3 per Marked consumed this combat.","Deal 15 damage, plus 4 per Marked consumed this combat.",15,secondary:3,ps:4,keys:"Marked");
            A("final_curse","FINAL CURSE",Rarity.Rare,3,CardKind.Attack,EffectKind.Damage,10,"Consume all Marked. Deal 10 damage per stack.","Consume all Marked. Deal 13 damage per stack.",13,keys:"Marked");
            A("dark_bargain","DARK BARGAIN",Rarity.Common,0,CardKind.Skill,EffectKind.Draw,2,"Draw 2. Add a random Curse to your discard pile.","Draw 3. Add a random Curse to your discard pile.",3,keys:"Curse");
            A("blood_magic","BLOOD MAGIC",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,18,"Lose 3 HP. Deal 18 damage.","Lose 3 HP. Deal 23 damage.",23,secondary:3,ps:3);
            A("forbidden_knowledge","FORBIDDEN KNOWLEDGE",Rarity.Uncommon,0,CardKind.Skill,EffectKind.Draw,3,"Draw 3. Add a random Curse to your hand.","Draw 4. Add a random Curse to your hand.",4,keys:"Curse");
            A("void_bolt","VOID BOLT",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage, plus 3 per Curse in your deck.","Deal 10 damage, plus 4 per Curse in your deck.",10,secondary:3,ps:4,keys:"Curse");
            A("consume_darkness","CONSUME DARKNESS",Rarity.Uncommon,0,CardKind.Skill,EffectKind.Exhaust,1,"Exhaust a Curse from your hand. Gain 1 Energy and draw 1.","Exhaust a Curse from your hand. Gain 2 Energy and draw 1.",2,secondary:1,ps:1,keys:"Curse,Exhaust");
            A("embrace_the_void","EMBRACE THE VOID",Rarity.Uncommon,2,CardKind.Power,EffectKind.Power,1,"Whenever you draw a Curse, draw 1 additional card.","Whenever you draw a Curse, draw 1 additional card.",pc:1,keys:"Curse");
            A("dark_offering","DARK OFFERING",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Resonance,2,"Add a random Curse to your discard pile. Gain 2 Resonance and 1 Energy.","Add a random Curse to your discard pile. Gain 3 Resonance and 1 Energy.",3,secondary:1,ps:1,keys:"Curse,Resonance");
            A("damnation","DAMNATION",Rarity.Rare,2,CardKind.Power,EffectKind.Power,1,"Whenever you Exhaust a Curse, gain 1 Strength and 1 Resonance.","Whenever you Exhaust a Curse, gain 2 Strength and 1 Resonance.",2,secondary:1,ps:1,keys:"Curse,Exhaust,Strength,Resonance");
            A("forbidden_one","FORBIDDEN ONE",Rarity.Rare,3,CardKind.Attack,EffectKind.Damage,10,"Deal 10 damage three times. Each hit gains +2 per Curse in your deck.","Deal 12 damage three times. Each hit gains +3 per Curse in your deck.",12,secondary:2,ps:3,hits:3,keys:"Curse");
            A("burning_hex","BURNING HEX",Rarity.Common,1,CardKind.Skill,EffectKind.Burn,3,"Apply 1 Marked and 3 Burn.","Apply 2 Marked and 4 Burn.",4,secondary:1,ps:2,keys:"Burn,Marked");
            A("resonant_strike","RESONANT STRIKE",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage. If you have 3+ Resonance, deal +4.","Deal 10 damage. If you have 3+ Resonance, deal +5.",10,secondary:4,ps:5,keys:"Resonance");
            A("blasphemous_ritual","BLASPHEMOUS RITUAL",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Sigil,4,"Create a Sigil of your choice. Add a random Curse to your discard pile.","Create a Sigil of your choice and activate it. Add a random Curse to your discard pile.",4,secondary:0,ps:1,keys:"Sigil,Curse");
            A("hexfire","HEXFIRE",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,6,"Deal 6 damage. If the target is Marked, apply 6 Burn.","Deal 9 damage. If the target is Marked, apply 8 Burn.",9,secondary:6,ps:8,keys:"Marked,Burn");
            A("resonant_flame","RESONANT FLAME",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Burn,4,"Spend up to 3 Resonance. Apply 4 Burn per Resonance spent.","Spend up to 3 Resonance. Apply 6 Burn per Resonance spent.",6,secondary:3,ps:3,keys:"Resonance,Burn");
            A("void_sigil","VOID SIGIL",Rarity.Rare,1,CardKind.Skill,EffectKind.Sigil,4,"Exhaust a Curse. Create a Sigil of your choice.","Exhaust a Curse. Create a Sigil of your choice.",pc:0,keys:"Curse,Exhaust,Sigil");
            A("black_sun","BLACK SUN",Rarity.Rare,3,CardKind.Skill,EffectKind.Burn,12,"Apply 12 Burn to ALL enemies. Consume all Marked for +4 Burn each.","Apply 16 Burn to ALL enemies. Consume all Marked for +6 Burn each.",16,secondary:4,ps:6,keys:"Burn,Marked");

            // THE REAPER · temporary Souls, scythe attacks, draw and Exhaust recursion.
            RP("soul","SOUL",Rarity.Special,0,CardKind.Skill,EffectKind.Damage,3,"Deal 3 damage. Draw 1. Exhaust.","Deal 5 damage. Draw 1. Exhaust.",5,exhaust:true,keys:"Soul,Temporary,Exhaust,Draw");
            RP("scythe_strike","SCYTHE STRIKE",Rarity.Basic,1,CardKind.Attack,EffectKind.Damage,6,"Deal 6 damage.","Deal 9 damage.",9);
            RP("deaths_veil","DEATH'S VEIL",Rarity.Basic,1,CardKind.Skill,EffectKind.Block,5,"Gain 5 Block.","Gain 8 Block.",8,keys:"Block");
            RP("soul_call","SOUL CALL",Rarity.Basic,1,CardKind.Skill,EffectKind.None,2,"Add 2 Souls to your hand.","Add 3 Souls to your hand.",3,keys:"Soul");
            RP("reaping_blow","REAPING BLOW",Rarity.Basic,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage. If you played a Soul this turn, deal 4 additional damage.","Deal 9 damage. If you played a Soul this turn, deal 6 additional damage.",9,secondary:4,ps:6,keys:"Soul");

            RP("grave_cut","GRAVE CUT",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage.","Deal 11 damage.",11);
            RP("soul_slash","SOUL SLASH",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,6,"Deal 6 damage. Add 1 Soul to your hand.","Deal 9 damage. Add 1 Soul to your hand.",9,secondary:1,ps:1,keys:"Soul");
            RP("spirit_cleave","SPIRIT CLEAVE",Rarity.Common,2,CardKind.Attack,EffectKind.Damage,14,"Deal 14 damage. If you played a Soul this turn, deal 5 additional damage.","Deal 18 damage. Soul bonus becomes 7.",18,secondary:5,ps:7,keys:"Soul");
            RP("deaths_touch","DEATH'S TOUCH",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage. Apply 1 Weak.","Deal 10 damage. Apply 1 Weak.",10,secondary:1,ps:1,keys:"Weak");
            RP("reaping_sweep","REAPING SWEEP",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,5,"Deal 5 damage twice.","Deal 7 damage twice.",7,hits:2);
            RP("grave_guard","GRAVE GUARD",Rarity.Common,1,CardKind.Skill,EffectKind.Block,8,"Gain 8 Block. Add 1 Soul to your draw pile.","Gain 11 Block. Add 1 Soul to your draw pile.",11,secondary:1,ps:1,keys:"Block,Soul");
            RP("soul_guard","SOUL GUARD",Rarity.Common,1,CardKind.Skill,EffectKind.Block,6,"Gain 6 Block for each Soul currently in your hand.","Gain 8 Block per Soul.",8,keys:"Block,Soul");
            RP("dark_veil_reaper","DARK VEIL",Rarity.Common,1,CardKind.Skill,EffectKind.Block,7,"Gain 7 Block. If you played a Soul this turn, deal 4 damage.","Gain 10 Block and conditional damage becomes 6.",10,secondary:4,ps:6,keys:"Block,Soul");
            RP("call_beyond","CALL BEYOND",Rarity.Common,1,CardKind.Skill,EffectKind.Draw,1,"Add 2 Souls to your discard pile. Draw 1 card.","Add 3 Souls to your discard pile. Draw 1 card.",1,secondary:2,ps:3,keys:"Soul,Draw");
            RP("soul_offering","SOUL OFFERING",Rarity.Common,0,CardKind.Skill,EffectKind.Exhaust,5,"Exhaust a Soul from your hand. Gain 5 Block and draw 1 card.","Gain 8 Block and draw 1 card.",8,secondary:1,ps:1,keys:"Soul,Exhaust,Block,Draw");
            RP("death_knell","DEATH KNELL",Rarity.Common,1,CardKind.Skill,EffectKind.None,4,"Add 1 Soul to your hand. Your next Soul this turn deals +4 damage.","Next Soul deals +7 damage.",7,secondary:1,ps:1,keys:"Soul");
            RP("grim_focus","GRIM FOCUS",Rarity.Common,0,CardKind.Skill,EffectKind.None,4,"Your next Attack this turn deals +4 damage; +7 after a Soul.","Your next Attack deals +6; +10 after a Soul.",6,secondary:7,ps:10,keys:"Soul");
            RP("grave_search","GRAVE SEARCH",Rarity.Common,1,CardKind.Skill,EffectKind.Draw,2,"Draw 2 cards.","Draw 3 cards.",3,keys:"Draw");
            RP("scythe_cycle","SCYTHE CYCLE",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,6,"Deal 6 damage. Draw 1 card.","Deal 9 damage. Draw 1 card.",9,secondary:1,ps:1,keys:"Draw");
            RP("grim_flurry","GRIM FLURRY",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,3,"Deal 3 damage 3 times.","Deal 4 damage 3 times.",4,hits:3);
            RP("soul_piercer_reaper","SOUL PIERCER",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,4,"Deal 4 damage twice. If a Soul is in your hand, deal 4 damage one additional time.","Deal 5 damage per hit.",5,hits:2,keys:"Soul");
            RP("dark_insight","DARK INSIGHT",Rarity.Common,0,CardKind.Skill,EffectKind.None,1,"Draw 1 card. If it is a Soul, draw 1 additional card.","Draw 2 cards initially.",2,secondary:1,ps:1,keys:"Soul,Draw");
            RP("hollow_cut","HOLLOW CUT",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage. If a card was Exhausted this turn, deal 4 additional damage.","Deal 9 damage and +6 additional damage.",9,secondary:4,ps:6,keys:"Exhaust");

            RP("soul_feast","SOUL FEAST",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Exhaust,1,"Exhaust a Soul from your hand. Gain 1 Energy and draw 2 cards.","Costs 0.",1,secondary:2,ps:2,pc:0,keys:"Soul,Exhaust,Draw");
            RP("spirit_scythe","SPIRIT SCYTHE",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,13,"Deal 13 damage. Deal +4 for each Soul played this turn.","Deal 17 base damage.",17,secondary:4,ps:4,keys:"Soul");
            RP("deaths_embrace","DEATH'S EMBRACE",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,4,"The first Soul you play each turn grants 4 Block.","Grants 7 Block.",7,keys:"Soul,Block");
            RP("grave_pact","GRAVE PACT",Rarity.Uncommon,1,CardKind.Skill,EffectKind.None,3,"Add 2 Souls to your hand. Your next Attack deals +3 per Soul currently in hand.","Attack bonus becomes +5 per Soul.",5,secondary:2,ps:2,keys:"Soul");
            RP("soul_rend","SOUL REND",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage. You may Exhaust a Soul to deal 8 damage again.","Deal 11 damage each time.",11,keys:"Soul,Exhaust");
            RP("death_march","DEATH MARCH",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,3,"Every 3 Souls played during combat, draw 1 card.","Every 2 Souls.",2,keys:"Soul,Draw");
            RP("hollow_scythe","HOLLOW SCYTHE",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,16,"Deal 16 damage. If a Soul is in your hand, gain 7 Block.","Deal 20 damage and gain 9 Block.",20,secondary:7,ps:9,keys:"Soul,Block");
            RP("soul_exchange","SOUL EXCHANGE",Rarity.Uncommon,0,CardKind.Skill,EffectKind.Exhaust,2,"Exhaust a Soul from your hand. Draw 2 cards.","Draw 3 cards.",3,keys:"Soul,Exhaust,Draw");
            RP("gravekeeper","GRAVEKEEPER",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,4,"First Soul added to your draw pile each turn grants 4 Block.","Gain 7 Block.",7,keys:"Soul,Block");
            RP("deaths_door","DEATH'S DOOR",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Block,10,"Gain 10 Block. If no Souls are in hand, add 1 Soul to hand.","Gain 14 Block.",14,secondary:1,ps:1,keys:"Block,Soul");
            RP("soulstorm","SOULSTORM",Rarity.Uncommon,2,CardKind.Skill,EffectKind.None,2,"Play every Soul currently in your hand. Those Souls deal +2 damage.","Those Souls deal +4 damage.",4,keys:"Soul");
            RP("beyond_the_veil","BEYOND THE VEIL",Rarity.Uncommon,1,CardKind.Skill,EffectKind.None,2,"Draw 2 cards. Souls drawn this way deal +3 damage this turn.","Draw 3 cards.",3,secondary:3,ps:3,keys:"Soul,Draw");
            RP("empty_grave","EMPTY GRAVE",Rarity.Uncommon,0,CardKind.Skill,EffectKind.Draw,3,"Draw 1 per 2 Souls in Exhaust, up to 3. Exhaust.","Maximum becomes 4 cards.",4,exhaust:true,keys:"Soul,Exhaust,Draw");
            RP("soul_carver","SOUL CARVER",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,4,"Deal 4 damage 4 times. Each Soul in hand empowers one hit by +3.","Deal 5 damage 4 times.",5,secondary:3,ps:3,hits:4,keys:"Soul");
            RP("graves_edge","GRAVE'S EDGE",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage. Return an Attack from Exhaust; it deals +4 damage this turn.","Deal 11 damage; returned Attack gets +6.",11,secondary:4,ps:6,keys:"Exhaust");
            RP("call_from_beyond","CALL FROM BEYOND",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Block,6,"Return a Skill from Exhaust to hand. Gain 6 Block. Souls may be selected.","Gain 9 Block.",9,keys:"Soul,Exhaust,Block");
            RP("soul_echo","SOUL ECHO",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Draw,1,"The next Soul this turn plays twice. Draw 1 card.","Costs 0.",1,pc:0,keys:"Soul,Replay,Draw");
            RP("reapers_momentum","REAPER'S MOMENTUM",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,3,"Deal 3 damage per card played before this card this turn, twice.","4 damage per previous card, twice.",4,hits:2);

            RP("army_of_the_dead","ARMY OF THE DEAD",Rarity.Rare,2,CardKind.Skill,EffectKind.None,1,"Fill your hand with Souls. Exhaust.","Fill your hand with Soul+. Exhaust.",2,exhaust:true,keys:"Soul,Exhaust");
            RP("soul_reaper","SOUL REAPER",Rarity.Rare,2,CardKind.Attack,EffectKind.Damage,12,"Deal 12 damage. +5 for each Soul played this combat.","Deal 16 damage. +6 for each Soul played this combat.",16,secondary:5,ps:6,keys:"Soul");
            RP("endless_harvest","ENDLESS HARVEST",Rarity.Rare,2,CardKind.Power,EffectKind.Power,2,"The first 2 Souls played each turn each add 1 Soul to your discard pile.","Costs 1. The first 2 Souls played each turn each add 1 Soul to your discard pile.",2,pc:1,keys:"Soul");
            RP("devour_the_dead","DEVOUR THE DEAD",Rarity.Rare,1,CardKind.Skill,EffectKind.Exhaust,2,"Exhaust all Souls in hand. For every 2 Exhausted, gain 1 Energy and draw 1.","Exhaust all Souls in hand. For each Exhausted, gain 1 Energy and draw 1.",1,keys:"Soul,Exhaust,Draw");
            RP("death_incarnate","DEATH INCARNATE",Rarity.Rare,3,CardKind.Power,EffectKind.Power,4,"Souls deal +4 damage for the rest of combat.","Costs 2. Souls deal +4 damage for the rest of combat.",4,pc:2,keys:"Soul");
            RP("final_procession","FINAL PROCESSION",Rarity.Rare,2,CardKind.Skill,EffectKind.None,1,"Play every Soul currently in your Exhaust pile. Exhaust.","Costs 1. Play every Soul currently in your Exhaust pile. Exhaust.",1,pc:1,exhaust:true,keys:"Soul,Exhaust");
            RP("grim_ascension","GRIM ASCENSION",Rarity.Rare,2,CardKind.Power,EffectKind.Power,1,"Whenever you play a Soul, increase Soul damage by 1 for the rest of combat.","Whenever you play a Soul, increase Soul damage by 2 for the rest of combat.",2,keys:"Soul");
            RP("claim_the_fallen","CLAIM THE FALLEN",Rarity.Rare,2,CardKind.Attack,EffectKind.Damage,24,"Deal 24 damage. On Kill: gain 1 additional card reward choice after combat. Exhaust.","Deal 30 damage. On Kill: gain 1 additional card reward choice after combat. Exhaust.",30,exhaust:true,keys:"On Kill,Exhaust");
            RP("soul_conversion","SOUL CONVERSION",Rarity.Rare,1,CardKind.Skill,EffectKind.None,1,"Transform every other card in hand into Souls for this combat. Exhaust.","Costs 0. Transform every other card in hand into Souls for this combat. Exhaust.",1,pc:0,exhaust:true,keys:"Soul,Transform,Exhaust");
            RP("soulbound_tome","SOULBOUND TOME",Rarity.Rare,2,CardKind.Power,EffectKind.Power,1,"Whenever you draw a Soul, draw 1 additional card.","Costs 1. Whenever you draw a Soul, draw 1 additional card.",1,pc:1,keys:"Soul,Draw");
            RP("eternal_souls","ETERNAL SOULS",Rarity.Rare,2,CardKind.Power,EffectKind.Power,1,"Every Soul gains Replay 1: resolve its effect once more before it Exhausts.","Costs 1. Every Soul gains Replay 1: resolve its effect once more before it Exhausts.",1,pc:1,keys:"Soul,Replay");
            RP("reapers_calling","REAPER'S CALLING",Rarity.Rare,2,CardKind.Power,EffectKind.Power,2,"The first time you draw 3 cards during your turn, add 2 Souls to hand. Once per turn.","The first time you draw 3 cards during your turn, add 3 Souls to hand. Once per turn.",3,keys:"Soul,Draw");

            W("quick_thinking","QUICK THINKING",Rarity.Common,0,CardKind.Skill,EffectKind.Draw,1,"Draw 1.","Draw 1. Gain 2 Block.",1,secondary:0,ps:2);
            W("cheap_shot","CHEAP SHOT",Rarity.Common,0,CardKind.Attack,EffectKind.Damage,4,"Deal 4 damage.","Deal 6 damage.",6);
            W("brace_yourself","BRACE YOURSELF",Rarity.Common,1,CardKind.Skill,EffectKind.Block,7,"Gain 7 Block; gain 9 instead if you have no Block.","Gain 9 Block; gain 12 instead if you have no Block.",9,secondary:9,ps:12,keys:"Block");
            W("opening_blow","OPENING BLOW",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage; +4 if this is your first card this turn.","Deal 10 damage; +5 if this is your first card this turn.",10,secondary:4,ps:5);
            W("preparation","PREPARATION",Rarity.Common,0,CardKind.Skill,EffectKind.None,1,"Your next Skill costs 1 less. Exhaust.","Your next Skill costs 1 less.",1,exhaust:true,keys:"Exhaust");
            W("follow_through","FOLLOW THROUGH",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage; +4 if you played a Skill this turn.","Deal 9 damage; +5 if you played a Skill this turn.",9,secondary:4,ps:5);
            W("clear_mind","CLEAR MIND",Rarity.Common,1,CardKind.Skill,EffectKind.Draw,2,"Draw 2, then discard 1.","Draw 3, then discard 1.",3);
            W("steady_hands","STEADY HANDS",Rarity.Common,1,CardKind.Skill,EffectKind.Block,6,"Gain 6 Block. Draw 1.","Gain 9 Block. Draw 1.",9,secondary:1,ps:1,keys:"Block");
            W("opportunist","OPPORTUNIST",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,6,"Deal 9 damage to a debuffed enemy; otherwise deal 6.","Deal 12 damage to a debuffed enemy; otherwise deal 8.",8,secondary:9,ps:12);
            W("adrenaline_rush","ADRENALINE RUSH",Rarity.Common,0,CardKind.Skill,EffectKind.Energy,1,"Gain 1 Energy. Add Dazed Mind to your discard pile.","Gain 1 Energy. Draw 1. Add Dazed Mind to your discard pile.",1,secondary:0,ps:1,keys:"Status");
            W("pocket_guard","POCKET GUARD",Rarity.Common,1,CardKind.Skill,EffectKind.Block,5,"Gain 5 Block; gain +5 if you played an Attack this turn.","Gain 7 Block; gain +5 if you played an Attack this turn.",7,secondary:5,ps:5,keys:"Block");
            W("finishing_cut","FINISHING CUT",Rarity.Common,1,CardKind.Attack,EffectKind.Damage,7,"Deal 7 damage; +5 if the enemy is below 50% HP.","Deal 9 damage; +7 if the enemy is below 50% HP.",9,secondary:5,ps:7);
            W("adapt","ADAPT",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Block,8,"Choose: gain 8 Block OR deal 8 damage.","Choose: gain 11 Block OR deal 11 damage.",11,secondary:8,ps:11,keys:"Block");
            W("exploit_weakness","EXPLOIT WEAKNESS",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage, plus 3 per debuff on the enemy.","Deal 8 damage, plus 4 per debuff on the enemy.",8,secondary:3,ps:4);
            W("battle_rhythm","BATTLE RHYTHM",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,4,"Every third card each turn deals 4 damage to a random enemy.","Every third card each turn deals 6 damage to a random enemy.",6);
            W("reserve_energy","RESERVE ENERGY",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,4,"At end of turn, gain 4 Block per unused Energy.","At end of turn, gain 6 Block per unused Energy.",6,keys:"Block");
            W("recycle","RECYCLE",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Exhaust,2,"Exhaust a card. Draw 2.","Exhaust a card. Draw 2.",pc:0,keys:"Exhaust");
            W("preparation_strike","PREPARATION STRIKE",Rarity.Uncommon,1,CardKind.Attack,EffectKind.Damage,8,"Deal 8 damage. Your next Attack costs 1 less.","Deal 11 damage. Your next Attack costs 1 less.",11);
            W("emergency_guard","EMERGENCY GUARD",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Block,5,"Gain 5 Block; if the enemy intends 15+ damage, gain +10.","Gain 7 Block; if the enemy intends 15+ damage, gain +12.",7,secondary:10,ps:12,keys:"Block");
            W("tactical_advantage","TACTICAL ADVANTAGE",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,1,"The first 0-cost card each turn draws 1.","The first 0-cost card each turn draws 1 and deals 2 damage to a random enemy.",1,secondary:0,ps:2);
            W("breakthrough","BREAKTHROUGH",Rarity.Uncommon,2,CardKind.Attack,EffectKind.Damage,14,"Remove enemy Block, then deal 14 damage.","Remove enemy Block, then deal 18 damage.",18);
            W("controlled_breathing","CONTROLLED BREATHING",Rarity.Uncommon,1,CardKind.Skill,EffectKind.Block,10,"Gain 10 Block. If this is your only card played this turn, retain that Block next turn.","Gain 14 Block. If this is your only card played this turn, retain that Block next turn.",14,keys:"Block");
            W("resourceful","RESOURCEFUL",Rarity.Uncommon,1,CardKind.Power,EffectKind.Power,3,"Whenever you Exhaust a card, gain 3 Block.","Whenever you Exhaust a card, gain 5 Block.",5,keys:"Exhaust,Block");
            W("perfect_opportunity","PERFECT OPPORTUNITY",Rarity.Rare,2,CardKind.Attack,EffectKind.Damage,6,"Deal 6 damage per debuff on the enemy.","Deal 8 damage per debuff on the enemy.",8);
            W("improvisation","IMPROVISATION",Rarity.Rare,1,CardKind.Skill,EffectKind.Draw,3,"Draw 3. The first drawn card costs 0 this turn. Exhaust.","Draw 4. The first drawn card costs 0 this turn. Exhaust.",4,exhaust:true,keys:"Exhaust");
            W("overflow","OVERFLOW",Rarity.Rare,2,CardKind.Power,EffectKind.Power,5,"Excess Block above enemy intended damage deals 5 damage.","Excess Block above enemy intended damage deals 8 damage.",8,keys:"Block");
            W("chain_reaction","CHAIN REACTION",Rarity.Rare,2,CardKind.Power,EffectKind.Power,3,"Applying a debuff already present deals 3 damage.","Applying a debuff already present deals 5 damage.",5);
            W("limit_break","LIMIT BREAK",Rarity.Rare,1,CardKind.Skill,EffectKind.Power,2,"Double one valid stackable buff. Exhaust.","Double one valid stackable buff. Exhaust.",pc:0,exhaust:true,keys:"Exhaust,Buff");
            W("against_all_odds","AGAINST ALL ODDS",Rarity.Rare,2,CardKind.Power,EffectKind.Power,1,"At the start of a turn below 50% HP, gain 1 Energy.","At the start of a turn below 50% HP, gain 1 Energy and draw 1.",1,secondary:0,ps:1);
            W("perfect_form","PERFECT FORM",Rarity.Rare,3,CardKind.Power,EffectKind.Power,1,"The first Attack and first Skill each turn cost 1 less.","The first Attack and first Skill each turn cost 1 less.",pc:2);

            c.Add(D("dead_weight","DEAD WEIGHT",null,CardOrigin.Curse,Rarity.Curse,0,CardKind.Curse,EffectKind.None,0,"Unplayable.","",unplayable:true,keywords:"Curse"));
            c.Add(D("dread","DREAD",null,CardOrigin.Curse,Rarity.Curse,0,CardKind.Curse,EffectKind.Weak,10,"While in hand, Attacks deal 10% less damage.","",unplayable:true,keywords:"Curse"));
            c.Add(D("frailty","FRAILTY",null,CardOrigin.Curse,Rarity.Curse,0,CardKind.Curse,EffectKind.None,20,"While in hand, cards grant 20% less Block.","",unplayable:true,keywords:"Curse"));
            c.Add(D("lingering_pain","LINGERING PAIN",null,CardOrigin.Curse,Rarity.Curse,0,CardKind.Curse,EffectKind.None,3,"Unplayable. Ethereal. At end of turn, lose 3 HP if still in hand.","",ethereal:true,unplayable:true,keywords:"Curse,Ethereal"));
            c.Add(D("decay","DECAY",null,CardOrigin.Curse,Rarity.Curse,0,CardKind.Curse,EffectKind.None,2,"At end of turn while in hand, lose 2 HP.","",unplayable:true,keywords:"Curse"));
            c.Add(D("shackled","SHACKLED",null,CardOrigin.Curse,Rarity.Curse,1,CardKind.Curse,EffectKind.None,0,"Costs 1. Exhaust.","",exhaust:true,keywords:"Curse,Exhaust"));
            c.Add(D("hollow","HOLLOW",null,CardOrigin.Curse,Rarity.Curse,0,CardKind.Curse,EffectKind.None,1,"When drawn, discard 1 random card.","",unplayable:true,keywords:"Curse"));
            c.Add(D("greed","GREED",null,CardOrigin.Curse,Rarity.Curse,0,CardKind.Curse,EffectKind.None,5,"If drawn during combat, lose 5 Gold at combat end.","",unplayable:true,keywords:"Curse"));
            c.Add(D("doom","DOOM",null,CardOrigin.Curse,Rarity.Curse,0,CardKind.Curse,EffectKind.None,8,"Unplayable. Ethereal. When drawn, lose 8 HP ignoring Block.","",ethereal:true,unplayable:true,keywords:"Curse,Ethereal"));
            c.Add(D("dazed_mind","DAZED MIND",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,0,"Unplayable. Ethereal.","",ethereal:true,unplayable:true,keywords:"Status,Ethereal"));
            c.Add(D("shattered_guard","SHATTERED GUARD",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,5,"On draw, lose 5 Block. Exhaust.","",exhaust:true,unplayable:true,keywords:"Status,Exhaust"));
            c.Add(D("falter","FALTER",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,3,"Ethereal. While held, Attacks deal 3 less damage.","",ethereal:true,unplayable:true,keywords:"Status,Ethereal"));
            c.Add(D("heavy_chains","HEAVY CHAINS",null,CardOrigin.Status,Rarity.Status,1,CardKind.Status,EffectKind.None,1,"Costs 1. Exhaust. While held, your first Attack each turn costs 1 more.","",exhaust:true,keywords:"Status,Exhaust"));
            c.Add(D("misfortune","MISFORTUNE",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,1,"Ethereal. On draw, your next card this turn costs 1 more.","",ethereal:true,unplayable:true,keywords:"Status,Ethereal"));
            c.Add(D("haunting","HAUNTING",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,2,"At end of turn while held, lose 2 HP.","",unplayable:true,keywords:"Status"));
            c.Add(D("fractured_will","FRACTURED WILL",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,1,"Ethereal. While held, buffs gained are reduced by 1 stack, minimum 1.","",ethereal:true,unplayable:true,keywords:"Status,Ethereal"));
            c.Add(D("arcane_lock","ARCANE LOCK",null,CardOrigin.Status,Rarity.Status,1,CardKind.Status,EffectKind.None,0,"Costs 1. Exhaust. While held, you cannot gain Resonance.","",exhaust:true,keywords:"Status,Exhaust,Resonance"));
            c.Add(D("rust","RUST",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,4,"On draw, your next Attack deals 4 less damage. Exhaust.","",exhaust:true,unplayable:true,keywords:"Status,Exhaust"));
            c.Add(D("fatebound","FATEBOUND",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,0,"Ethereal. While held, Energy costs cannot be reduced.","",ethereal:true,unplayable:true,keywords:"Status,Ethereal"));
            c.Add(D("lost_moment","LOST MOMENT",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,1,"Ethereal. On draw, draw one fewer card next turn.","",ethereal:true,unplayable:true,keywords:"Status,Ethereal"));
            c.Add(D("spirit_scar","SPIRIT SCAR",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,2,"On draw, lose 2 HP ignoring Block. Exhaust.","",exhaust:true,unplayable:true,keywords:"Status,Exhaust"));
            c.Add(D("twisted_fate","TWISTED FATE",null,CardOrigin.Status,Rarity.Status,0,CardKind.Status,EffectKind.None,0,"On draw, immediately Exhaust this, shuffle the rest of your hand into the draw pile, then draw that many replacement cards.","",exhaust:true,unplayable:true,keywords:"Status,Exhaust"));
            AddMartialOccultCards(c);AddMajorVanguardCards(c);AddRemainingExpansionCards(c);
            return c.ToArray();
        }

        private static RelicDef[] BuildRelics()=>new[]
        {
            R("charred_locket","CHARRED LOCKET",Rarity.Common,"Whenever you apply Burn, apply +1 Burn."),R("split_lens","SPLIT LENS",Rarity.Common,"The first time each turn you apply Marked, apply +1."),R("worn_whetstone","WORN WHETSTONE",Rarity.Common,"The first Attack each combat deals +6 damage."),R("gilded_buckle","GILDED BUCKLE",Rarity.Common,"The first Block gain each combat gains +5."),
            R("warriors_knot","WARRIOR'S KNOT",Rarity.Common,"The first Strength gain each combat gains +1 Strength."),R("cracked_prism","CRACKED PRISM",Rarity.Common,"The first Resonance gain each combat gives +2."),R("travelers_candle","TRAVELER'S CANDLE",Rarity.Common,"Draw +1 at combat start."),R("lucky_coin","LUCKY COIN",Rarity.Common,"Gain 15% more Gold from combat rewards."),
            R("bone_charm","BONE CHARM",Rarity.Common,"Whenever you Exhaust, gain 2 Block."),R("duelists_pin","DUELIST'S PIN",Rarity.Common,"Your first Attack in a turn receives +4 damage if it is the only Attack played that turn."),R("silver_feather","SILVER FEATHER",Rarity.Common,"The fourth card played each turn grants 4 Block."),R("tarnished_eye","TARNISHED EYE",Rarity.Common,"At combat start, apply 1 Weak to ALL enemies."),
            R("everlasting_ember","EVERLASTING EMBER",Rarity.Uncommon,"When an enemy dies with Burn, transfer half its remaining Burn to a random living enemy."),R("executioners_seal","EXECUTIONER'S SEAL",Rarity.Uncommon,"Consuming Marked deals 3 damage."),R("crowned_bulwark","CROWNED BULWARK",Rarity.Uncommon,"The first time you gain 15+ Block in a turn, gain 1 Fortify."),R("broken_crown","BROKEN CROWN",Rarity.Uncommon,"Applying Vulnerable to an already Vulnerable enemy deals 6 damage."),
            R("ritual_bell","RITUAL BELL",Rarity.Uncommon,"Every third Sigil activation grants +1 Resonance."),R("crimson_spur","CRIMSON SPUR",Rarity.Uncommon,"The first time each combat you lose HP from a self-inflicted effect, gain 2 Strength for this combat."),R("broken_shackles","BROKEN SHACKLES",Rarity.Uncommon,"The first Status drawn each combat immediately Exhausts."),R("mirror_fragment","MIRROR FRAGMENT",Rarity.Uncommon,"The first 0-cost card each turn deals 3 random damage."),
            R("balanced_scales","BALANCED SCALES",Rarity.Uncommon,"End a turn at exactly 0 Energy to gain 6 Block."),R("blackened_tooth","BLACKENED TOOTH",Rarity.Uncommon,"The first Curse Exhaust each combat grants 1 Energy and draws 1."),R("fate_spinner","FATE SPINNER",Rarity.Uncommon,"At combat start, one random card in the draw pile costs 0 until played."),
            R("mask_of_two_fates","MASK OF TWO FATES",Rarity.Rare,"The first debuff applied each combat has its stacks doubled."),R("ashen_crown","ASHEN CROWN",Rarity.Rare,"Whenever Burn triggers outside its normal end-turn trigger, apply 2 Burn afterward."),R("war_gods_crest","WAR GOD'S CREST",Rarity.Rare,"Playing an Attack costing 2+ grants 3 Block and +3 damage to that Attack."),R("immortal_plate","IMMORTAL PLATE",Rarity.Rare,"Retain up to 8 Block at end of turn."),
            R("third_eye","THIRD EYE",Rarity.Rare,"Whenever all 3 Sigil slots become filled, activate the leftmost Sigil."),R("book_of_hollow_names","BOOK OF HOLLOW NAMES",Rarity.Rare,"Start combat with 1 Resonance per Curse in the deck, maximum 5."),R("hand_of_judgment","HAND OF JUDGMENT",Rarity.Rare,"Every third Marked stack consumed during combat grants 1 Energy and draws 2; then reset the counter."),R("threads_of_fate","THREADS OF FATE",Rarity.Rare,"At the start of every third turn, draw +2 and gain 1 Energy."),
            R("gilded_heart","GILDED HEART",Rarity.Boss,"Gain +1 Energy each turn. Start combat with 2 Dazed Mind shuffled into the draw pile."),R("stolen_hourglass","STOLEN HOURGLASS",Rarity.Boss,"Draw +2 each turn. Card effects cannot draw additional cards."),R("crown_of_sacrifice","CROWN OF SACRIFICE",Rarity.Boss,"The first card each turn costs 0. If its original cost was 2+, lose 3 HP at end of turn."),R("unbound_deck","THE UNBOUND DECK",Rarity.Boss,"Card rewards can contain Wanderer and other-character cards and display +1 choice; normal character cards appear less often."),
            R("deaths_keepsake","DEATH'S KEEPSAKE",Rarity.Special,"The first Soul you play each combat deals 3 additional damage.")
        }.Concat(BuildMajorRelics()).ToArray();
        private static RelicDef R(string id,string name,Rarity rarity,string text)=>new(id,name,rarity,text);
    }
}
