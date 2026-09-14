using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Core
{
    public enum EventFrequency { Common=6, Uncommon=3, Rare=1 }
    public enum EventSelectionKind { None, Card, RewardCard, Binding, ShardReward, OwnedShard, ShardReplacement, Relic, FocusedEffect }
    public enum EventCardFilter { Any, Attack, Skill, DefensiveSkill, Power, CostOnePlus, CostTwoPlusAttack, CostTwoPlus, Exhaust, Upgraded, Basic, BasicAttack, Curse, BindingEligible }
    public enum EventCardSource { Own, Foreign, Wanderer, AnyPlayable }
    public enum EventEffectKind
    {
        Nothing, Gold, LoseHp, Heal, MaxHp, AddCurse, RandomCurse, RemoveCurse,
        UpgradeSelected, RemoveSelected, TransformSelected, UpgradeRandom, UpgradeAllBasic,
        RewardCards, GrantRandomCard, GrantRelic, RemoveRelic, RewardShards, GrantRandomShards,
        BindSelected, DuplicateWithBinding, TemporaryStartBlock, TemporaryFirstDrawFree,
        RepairShard, TradeShard, FractureShard, RevealMap, Fatewheel
    }

    [Serializable]
    public sealed class EventEffectDef
    {
        public EventEffectKind kind;
        public int amount,count=1,duration;
        public string id="";
        public Rarity rarity=Rarity.Common;
        public CardKind cardKind=CardKind.Skill;
        public EventCardFilter filter;
        public EventCardSource source;
        public bool upgraded;
    }

    [Serializable]
    public sealed class EventChoiceDef
    {
        public string id,title,costText,rewardText;
        public EventEffectDef[] effects;
        public EventChoiceDef(string id,string title,string cost,string reward,params EventEffectDef[] effects)
        {this.id=id;this.title=title;costText=cost;rewardText=reward;this.effects=effects??Array.Empty<EventEffectDef>();}
    }

    [Serializable]
    public sealed class EventDefinition
    {
        public string id,name,prompt,ambientCue;
        public int minAct,maxAct,artIndex;
        public EventFrequency frequency;
        public bool repeatable;
        public EventChoiceDef[] choices;
        public EventDefinition(string id,string name,string prompt,int minAct,int maxAct,EventFrequency frequency,int artIndex,string cue,params EventChoiceDef[] choices)
        {this.id=id;this.name=name;this.prompt=prompt;this.minAct=minAct;this.maxAct=maxAct;this.frequency=frequency;this.artIndex=artIndex;ambientCue=cue;this.choices=choices??Array.Empty<EventChoiceDef>();}
    }

    public static class EventContent
    {
        public static readonly int[] FatewheelWeights={28,18,17,15,12,10};
        public static readonly int[] BindingCommissionCosts={50,70,90};
        public const int CrossroadsWandererChancePercent=15;
        public static readonly EventDefinition[] All=Build();
        public static EventDefinition Find(string id)=>All.FirstOrDefault(e=>e.id==id);
        public static EventChoiceDef FindChoice(string eventId,string choiceId)=>Find(eventId)?.choices.FirstOrDefault(c=>c.id==choiceId);

        private static EventDefinition[] Build()
        {
            var events=new List<EventDefinition>();
            void Add(string id,string name,string prompt,int min,int max,EventFrequency frequency,string cue,params EventChoiceDef[] choices)=>events.Add(new EventDefinition(id,name,prompt,min,max,frequency,events.Count,cue,choices));
            EventChoiceDef C(string id,string title,string cost,string reward,params EventEffectDef[] fx)=>new(id,title,cost,reward,fx);

            // ACT I — build the run.
            Add("abandoned_forge","THE ABANDONED FORGE","An ancient forge still burns despite having no fuel.",1,1,EventFrequency.Common,"forge",
                C("sharpen","SHARPEN","CHOOSE 1 ATTACK","UPGRADE IT",Select(EventEffectKind.UpgradeSelected,EventCardFilter.Attack)),
                C("reinforce","REINFORCE","CHOOSE 1 SKILL","UPGRADE IT",Select(EventEffectKind.UpgradeSelected,EventCardFilter.Skill)),
                C("ashes","SEARCH THE ASHES","","GAIN 45 GOLD",Gold(45)));
            Add("frayed_thread","THE FRAYED THREAD","A single golden strand hangs unnaturally from the ceiling.",1,1,EventFrequency.Uncommon,"thread",
                C("pull","PULL IT","CHOOSE AN ELIGIBLE ATTACK","APPLY BINDING: SERRATED",Bind("serrated",EventCardFilter.Attack)),
                C("armor","WRAP IT AROUND YOUR ARMOR","CHOOSE AN ELIGIBLE BLOCK CARD","APPLY BINDING: REINFORCED",Bind("reinforced",EventCardFilter.DefensiveSkill)),
                C("leave","LEAVE IT","","GAIN 20 GOLD",Gold(20)));
            Add("heavy_blade","THE HEAVY BLADE","An enormous weapon is embedded in stone.",1,1,EventFrequency.Uncommon,"metal",
                C("lift","LIFT IT","CHOOSE A 2+ ENERGY ATTACK","APPLY BINDING: WEIGHTED",Bind("weighted",EventCardFilter.CostTwoPlusAttack)),
                C("gold","BREAK OFF THE GOLD","","GAIN 55 GOLD",Gold(55)));
            Add("stolen_second","THE STOLEN SECOND","A golden clock ticks backward.",1,1,EventFrequency.Uncommon,"clock",
                C("moment","TAKE THE MOMENT","CHOOSE A 1+ ENERGY CARD","APPLY BINDING: QUICKENED",Bind("quickened",EventCardFilter.CostOnePlus)),
                C("smash","SMASH THE CLOCK","","GAIN 1 RANDOM FATE SHARD",RandomShards(1)));
            Add("traveling_merchant","THE TRAVELING MERCHANT","A nervous merchant offers suspiciously cheap goods.",1,1,EventFrequency.Common,"merchant",
                C("card","BUY A CARD","PAY 35 GOLD","REVEAL 3 CHARACTER CARDS · CHOOSE 1",Gold(-35),RewardCards(EventCardSource.Own,3)),
                C("shard","BUY A SHARD","PAY 45 GOLD","REVEAL 2 FATE SHARDS · CHOOSE 1",Gold(-45),RewardShards(2,1)),
                C("chest","TAKE THE UNATTENDED CHEST","ADD CURSE: GREED","GAIN 75 GOLD",AddCurse("greed"),Gold(75)));
            Add("empty_grave","THE EMPTY GRAVE","Fresh soil surrounds a nameless grave.",1,1,EventFrequency.Common,"grave",
                C("dig","DIG","LOSE 7 HP","GAIN A RANDOM COMMON RELIC",Hp(-7),Relic(Rarity.Common)),
                C("offering","LEAVE AN OFFERING","LOSE 25 GOLD · CHOOSE 1 CARD","REMOVE IT",Gold(-25),Select(EventEffectKind.RemoveSelected,EventCardFilter.Any)),
                C("leave","WALK AWAY","","NOTHING HAPPENS",Nothing()));
            Add("bent_sword","THE BENT SWORD","A broken sword lies beside a fallen adventurer.",1,1,EventFrequency.Common,"metal",
                C("blade","TAKE THE BLADE","","GAIN AN UPGRADED COMMON ATTACK",RandomCard(EventCardSource.Own,Rarity.Common,CardKind.Attack,true)),
                C("supplies","TAKE THE SUPPLIES","","GAIN 35 GOLD · HEAL 6 HP",Gold(35),Heal(6)),
                C("bury","BURY THE ADVENTURER","CHOOSE A BASIC STARTER ATTACK","REMOVE IT",Select(EventEffectKind.RemoveSelected,EventCardFilter.BasicAttack)));
            Add("three_sealed_doors","THREE SEALED DOORS","Three ancient doors display a sword, a shield, and an eye.",1,1,EventFrequency.Common,"door",
                C("sword","SWORD","","GAIN A RANDOM ATTACK",RandomCard(EventCardSource.Own,Rarity.Special,CardKind.Attack)),
                C("shield","SHIELD","","GAIN A RANDOM DEFENSIVE SKILL",RandomCard(EventCardSource.Own,Rarity.Special,CardKind.Skill,false,EventCardFilter.DefensiveSkill)),
                C("eye","EYE","","GAIN A RANDOM POWER",RandomCard(EventCardSource.Own,Rarity.Special,CardKind.Power)));
            Add("golden_beggar","THE GOLDEN BEGGAR","A cloaked stranger extends an empty hand.",1,1,EventFrequency.Common,"whisper",
                C("gold","GIVE 30 GOLD","PAY 30 GOLD","GAIN A RANDOM COMMON RELIC",Gold(-30),Relic(Rarity.Common)),
                C("card","GIVE A CARD","CHOOSE 1 CARD","REMOVE IT",Select(EventEffectKind.RemoveSelected,EventCardFilter.Any)),
                C("refuse","REFUSE","","NOTHING HAPPENS",Nothing()));
            Add("shardfall","SHARDFALL","Several Fate Shards lie inside a fresh crater.",1,1,EventFrequency.Uncommon,"shard",
                C("careful","TAKE ONE CAREFULLY","","REVEAL 3 FATE SHARDS · CHOOSE 1",RewardShards(3,1)),
                C("all","GRAB EVERYTHING","LOSE 10 HP","GAIN 2 RANDOM FATE SHARDS",Hp(-10),RandomShards(2)),
                C("sell","SELL THE LOCATION","","GAIN 55 GOLD",Gold(55)));
            Add("cracked_mirror","THE CRACKED MIRROR","Your reflection is holding a different deck.",1,1,EventFrequency.Uncommon,"mirror",
                C("reach","REACH THROUGH","CHOOSE 1 CARD","TRANSFORM INTO SAME-RARITY CHARACTER CARD",Select(EventEffectKind.TransformSelected,EventCardFilter.Any)),
                C("break","BREAK IT","LOSE 6 HP · CHOOSE 1 CARD","REMOVE IT",Hp(-6),Select(EventEffectKind.RemoveSelected,EventCardFilter.Any)),
                C("look","LOOK LONGER","","REVEAL 3 WANDERER CARDS · CHOOSE 1",RewardCards(EventCardSource.Wanderer,3)));
            Add("hungry_chest","THE HUNGRY CHEST","A chest opens like a mouth.",1,1,EventFrequency.Common,"chest",
                C("gold","FEED IT GOLD","PAY 50 GOLD","GAIN A RANDOM UNCOMMON CARD",Gold(-50),RandomCard(EventCardSource.Own,Rarity.Uncommon,CardKind.Skill,false,EventCardFilter.Any)),
                C("card","FEED IT A CARD","CHOOSE 1 CARD","REMOVE IT · GAIN 35 GOLD",Select(EventEffectKind.RemoveSelected,EventCardFilter.Any),Gold(35)),
                C("force","FORCE IT OPEN","LOSE 8 HP","GAIN AN UNCOMMON CARD · 35 GOLD",Hp(-8),RandomCard(EventCardSource.Own,Rarity.Uncommon,CardKind.Skill,false,EventCardFilter.Any),Gold(35)));
            Add("fallen_banner","THE FALLEN BANNER","An ancient military banner contains faint gilded markings.",1,1,EventFrequency.Common,"cloth",
                C("carry","CARRY IT","NEXT 3 COMBATS","START WITH 5 BLOCK",TempBlock(5,3)),
                C("gold","TEAR AWAY THE GOLD","","GAIN 40 GOLD",Gold(40)),
                C("study","STUDY THE MARKINGS","CHOOSE 1 CARD","UPGRADE IT",Select(EventEffectKind.UpgradeSelected,EventCardFilter.Any)));
            Add("forgotten_shrine","THE FORGOTTEN SHRINE","Three offerings rest beneath an ancient statue.",1,1,EventFrequency.Common,"shrine",
                C("coin","TAKE THE COIN","","GAIN 60 GOLD",Gold(60)),
                C("charm","TAKE THE CHARM","LOSE 5 HP","GAIN A RANDOM COMMON RELIC",Hp(-5),Relic(Rarity.Common)),
                C("kneel","KNEEL","","HEAL 15 HP",Heal(15)));
            Add("wrong_road","THE WRONG ROAD","The road splits around a dark forest.",1,1,EventFrequency.Common,"road",
                C("safe","SAFE PATH","","HEAL 8 HP",Heal(8)),
                C("danger","DANGEROUS PATH","LOSE 6 HP","REVEAL 3 UNCOMMON CARDS · CHOOSE 1",Hp(-6),RewardCards(EventCardSource.Own,3,Rarity.Uncommon)),
                C("gold","FOLLOW THE GOLDEN FOOTPRINTS","","GAIN 1 RANDOM FATE SHARD",RandomShards(1)));

            // ACT II — shape the run.
            Add("binding_chamber","THE BINDING CHAMBER","Golden symbols orbit a stone altar.",2,2,EventFrequency.Uncommon,"thread",
                C("bind","ENTER THE ORBIT","CHOOSE AN ELIGIBLE CARD","REVEAL 2 COMPATIBLE BINDINGS · CHOOSE 1",Bind("compatible:2",EventCardFilter.BindingEligible)));
            Add("living_ink","THE LIVING INK","Golden ink crawls across an open book.",2,2,EventFrequency.Uncommon,"ink",
                C("attack","WRITE ON AN ATTACK","CHOOSE AN ELIGIBLE ATTACK","APPLY SERRATED OR WEIGHTED",Bind("serrated|weighted",EventCardFilter.Attack)),
                C("skill","WRITE ON A SKILL","CHOOSE AN ELIGIBLE SKILL","APPLY REINFORCED OR LINGERING",Bind("reinforced|lingering",EventCardFilter.Skill)),
                C("drink","DRINK THE INK","LOSE 5 HP","GAIN 1 RANDOM FATE SHARD",Hp(-5),RandomShards(1)));
            Add("hanging_chains","THE HANGING CHAINS","Thousands of golden chains hang motionless.",2,2,EventFrequency.Uncommon,"chains",
                C("chain","TAKE A CHAIN","CHOOSE AN ELIGIBLE ATTACK OR SKILL","APPLY BINDING: CHAINED",Bind("chained",EventCardFilter.BindingEligible)),
                C("melt","MELT THEM","","GAIN 80 GOLD",Gold(80)));
            Add("golden_halo","THE GOLDEN HALO","A ring of light floats above an abandoned altar.",2,2,EventFrequency.Uncommon,"thread",
                C("accept","ACCEPT IT","CHOOSE AN ELIGIBLE ATTACK OR SKILL","APPLY BINDING: GILDED",Bind("gilded",EventCardFilter.BindingEligible)),
                C("shatter","SHATTER IT","LOSE 9 HP","GAIN A RANDOM UNCOMMON RELIC",Hp(-9),Relic(Rarity.Uncommon)));
            Add("focused_lens","THE FOCUSED LENS","A strange lens magnifies the effects written across your cards.",2,2,EventFrequency.Uncommon,"lens",
                C("focus","FOCUS A CARD","CHOOSE AN ELIGIBLE BUFF OR DEBUFF CARD","APPLY BINDING: FOCUSED",Bind("focused",EventCardFilter.BindingEligible)),
                C("sell","SELL THE LENS","","GAIN 75 GOLD",Gold(75)));
            Add("echoing_library","THE ECHOING LIBRARY","Every book describes a slightly different version of your journey.",2,2,EventFrequency.Uncommon,"library",
                C("own","READ YOUR STORY","","REVEAL 3 UPGRADED CHARACTER CARDS",RewardCards(EventCardSource.Own,3,Rarity.Special,true)),
                C("other","READ ANOTHER STORY","","REVEAL 3 FOREIGN CHARACTER CARDS",RewardCards(EventCardSource.Foreign,3)),
                C("burn","BURN THE BOOKS","REMOVE EXACTLY 2 CARDS","ADD CURSE: DREAD",Select(EventEffectKind.RemoveSelected,EventCardFilter.Any,2),AddCurse("dread")));
            Add("locked_reliquary","THE LOCKED RELIQUARY","A relic rests behind an ornate gilded lock.",2,2,EventFrequency.Common,"relic",
                C("pay","PAY","PAY 80 GOLD","GAIN A RANDOM UNCOMMON RELIC",Gold(-80),Relic(Rarity.Uncommon)),
                C("break","BREAK THE LOCK","LOSE 12 HP","GAIN A RANDOM UNCOMMON RELIC",Hp(-12),Relic(Rarity.Uncommon)),
                C("leave","LEAVE","","NOTHING HAPPENS",Nothing()));
            Add("collector_event","THE COLLECTOR","A masked collector searches for unusual artifacts.",2,2,EventFrequency.Uncommon,"merchant",
                C("shard","GIVE A FATE SHARD","REMOVE 1 OWNED SHARD","GAIN 75 GOLD",OwnedShard(EventEffectKind.RepairShard,"remove"),Gold(75)),
                C("relic","GIVE A RELIC","LOSE 1 ELIGIBLE COMMON RELIC","GAIN A RANDOM RARE CARD",RemoveRelic(),RandomCard(EventCardSource.Own,Rarity.Rare,CardKind.Skill,false,EventCardFilter.Any)),
                C("leave","GIVE NOTHING","","LEAVE",Nothing()));
            Add("card_eater","THE CARD EATER","A creature made of folded parchment crawls toward your deck.",2,2,EventFrequency.Common,"whisper",
                C("one","FEED ONE","REMOVE 1 CARD","HEAL 12 HP",Select(EventEffectKind.RemoveSelected,EventCardFilter.Any),Heal(12)),
                C("two","FEED TWO","REMOVE EXACTLY 2 CARDS","ADD CURSE: DEAD WEIGHT",Select(EventEffectKind.RemoveSelected,EventCardFilter.Any,2),AddCurse("dead_weight")),
                C("away","DRIVE IT AWAY","","GAIN 45 GOLD",Gold(45)));
            Add("broken_fatewheel","THE BROKEN FATEWHEEL","A damaged golden wheel slowly turns.",2,2,EventFrequency.Uncommon,"clock",
                C("spin","SPIN ONCE","PAY 40 GOLD","A WEIGHTED FATEWHEEL OUTCOME",Gold(-40),Fatewheel()));
            Add("strangers_deck","THE STRANGER'S DECK","A traveler offers cards from unfamiliar fighting styles.",2,2,EventFrequency.Uncommon,"cards",
                C("choose","CHOOSE A TECHNIQUE","","1 FOREIGN CHARACTER CARD · 1 OTHER STYLE · 1 WANDERER CARD",RewardCards(EventCardSource.Foreign,3)));
            Add("duel","THE DUEL","A spectral warrior challenges you.",2,2,EventFrequency.Common,"duel",
                C("honor","FIGHT HONORABLY","LOSE 10 HP","GAIN A RANDOM UNCOMMON RELIC",Hp(-10),Relic(Rarity.Uncommon)),
                C("unfair","FIGHT UNFAIRLY","ADD CURSE: SHACKLED","GAIN A RANDOM RARE ATTACK",AddCurse("shackled"),RandomCard(EventCardSource.Own,Rarity.Rare,CardKind.Attack)),
                C("refuse","REFUSE","","HEAL 5 HP",Heal(5)));
            Add("golden_surgeon","THE GOLDEN SURGEON","A strange figure offers to remove unnecessary pieces.",2,2,EventFrequency.Common,"surgeon",
                C("one","REMOVE WEAKNESS","PAY 30 GOLD · CHOOSE 1 CARD","REMOVE IT",Gold(-30),Select(EventEffectKind.RemoveSelected,EventCardFilter.Any)),
                C("two","REMOVE MORE","LOSE 12 HP · CHOOSE EXACTLY 2 CARDS","REMOVE THEM",Hp(-12),Select(EventEffectKind.RemoveSelected,EventCardFilter.Any,2)),
                C("improve","IMPROVE WHAT REMAINS","LOSE 7 HP","UPGRADE 2 RANDOM ELIGIBLE CARDS",Hp(-7),RandomUpgrades(2)));
            Add("shard_smith","THE SHARD SMITH","A smith works exclusively with pieces of broken fate.",2,2,EventFrequency.Uncommon,"shard",
                C("repair","REPAIR FATE","PAY 55 GOLD · CHOOSE 1 OWNED SHARD","REDUCE ITS ACTIVATION COUNT BY 1",Gold(-55),OwnedShard(EventEffectKind.RepairShard)),
                C("trade","TRADE","CHOOSE 1 OWNED SHARD","REPLACE WITH A DIFFERENT RANDOM SHARD",OwnedShard(EventEffectKind.TradeShard)));
            Add("false_treasure_room","THE FALSE TREASURE ROOM","Gold covers the floor suspiciously neatly.",2,2,EventFrequency.Common,"chest",
                C("little","TAKE A LITTLE","","GAIN 45 GOLD",Gold(45)),
                C("lot","TAKE A LOT","ADD CURSE: GREED","GAIN 110 GOLD",AddCurse("greed"),Gold(110)),
                C("chest","OPEN THE SEALED CHEST","ADD CURSE: HOLLOW","GAIN A RANDOM UNCOMMON RELIC",AddCurse("hollow"),Relic(Rarity.Uncommon)));

            // ACT III — distort the run.
            Add("recurring_knot","THE RECURRING KNOT","A golden strand ties itself into a loop that never ends.",3,3,EventFrequency.Uncommon,"thread",
                C("bind","TIE THE KNOT","CHOOSE AN ELIGIBLE EXHAUST CARD","APPLY BINDING: RECURRING",Bind("recurring",EventCardFilter.Exhaust)));
            Add("third_mark","THE THIRD MARK","Three fate symbols burn into the floor.",3,3,EventFrequency.Uncommon,"thread",
                C("bind","STEP BETWEEN THE MARKS","CHOOSE AN ELIGIBLE ATTACK OR SKILL","APPLY BINDING: FATEFUL",Bind("fateful",EventCardFilter.BindingEligible)));
            Add("perfect_inscription","THE PERFECT INSCRIPTION","A flawless golden symbol floats above the deck.",3,3,EventFrequency.Uncommon,"thread",
                C("bind","ACCEPT PERFECTION","CHOOSE AN UPGRADED ELIGIBLE CARD","APPLY BINDING: PERFECTED",Bind("perfected",EventCardFilter.Upgraded)));
            Add("master_binder","THE MASTER BINDER","A silent figure made from golden thread waits beside a table.",3,3,EventFrequency.Rare,"thread",
                C("bind","REQUEST THE MASTER'S WORK","PAY 75 GOLD · CHOOSE AN ELIGIBLE CARD","REVEAL 3 COMPATIBLE ACT III BINDINGS",Gold(-75),Bind("compatible:3",EventCardFilter.BindingEligible)));
            Add("severed_future","THE SEVERED FUTURE","Two different versions of your deck appear before you.",3,3,EventFrequency.Uncommon,"mirror",
                C("past","ABANDON THE PAST","REMOVE EXACTLY 3 CARDS · LOSE 18 HP","A LEANER FUTURE",Select(EventEffectKind.RemoveSelected,EventCardFilter.Any,3),Hp(-18)),
                C("future","STRENGTHEN THE FUTURE","","UPGRADE 3 RANDOM ELIGIBLE CARDS",RandomUpgrades(3)),
                C("refuse","REFUSE","","GAIN 1 RANDOM FATE SHARD",RandomShards(1)));
            Add("crown_without_king","THE CROWN WITHOUT A KING","A golden crown rests beside its long-dead owner.",3,3,EventFrequency.Uncommon,"relic",
                C("wear","WEAR IT","ADD 1 RANDOM CURSE","GAIN A RANDOM RARE RELIC",RandomCurse(),Relic(Rarity.Rare)),
                C("sell","SELL IT","","GAIN 130 GOLD",Gold(130)),
                C("destroy","DESTROY IT","CHOOSE 1 CURSE","REMOVE IT",RemoveCurse()));
            Add("nameless_card","THE NAMELESS CARD","A completely blank card floats above a golden altar.",3,3,EventFrequency.Rare,"cards",
                C("copy","WRITE A NAME","LOSE 15 HP · CHOOSE AN ELIGIBLE CARD","CREATE A SEPARATE COPY WITH A RANDOM COMPATIBLE BINDING",Hp(-15),DuplicateBound()));
            Add("last_merchant","THE LAST MERCHANT","A merchant insists there will never be another customer.",3,3,EventFrequency.Uncommon,"merchant",
                C("relic","RARE RELIC","PAY 120 GOLD","GAIN A RANDOM RARE RELIC",Gold(-120),Relic(Rarity.Rare)),
                C("card","RARE CARD","PAY 90 GOLD","REVEAL 3 RARE CARDS · CHOOSE 1",Gold(-90),RewardCards(EventCardSource.Own,3,Rarity.Rare)),
                C("shard","FATE SHARD","PAY 60 GOLD","REVEAL 3 FATE SHARDS · CHOOSE 1",Gold(-60),RewardShards(3,1)));
            Add("dead_wanderer","THE DEAD WANDERER","A famous traveler lies beside a shattered weapon.",3,3,EventFrequency.Uncommon,"grave",
                C("technique","TAKE THEIR TECHNIQUE","","REVEAL 3 UPGRADED WANDERER CARDS",RewardCards(EventCardSource.Wanderer,3,Rarity.Special,true)),
                C("fortune","TAKE THEIR FORTUNE","","GAIN 100 GOLD",Gold(100)),
                C("case","TAKE THEIR SHARD CASE","","GAIN 2 RANDOM FATE SHARDS",RandomShards(2)));
            Add("forbidden_vault","THE FORBIDDEN VAULT","Three locks cover an ancient vault.",3,3,EventFrequency.Uncommon,"door",
                C("blood","BLOOD LOCK","LOSE 15 HP","GAIN A RANDOM RARE RELIC",Hp(-15),Relic(Rarity.Rare)),
                C("gold","GOLD LOCK","PAY 100 GOLD","GAIN A RANDOM RARE RELIC",Gold(-100),Relic(Rarity.Rare)),
                C("curse","CURSE LOCK","ADD 1 RANDOM CURSE","GAIN A RANDOM RARE RELIC",RandomCurse(),Relic(Rarity.Rare)));
            Add("broken_hour","THE BROKEN HOUR","Time has stopped inside the room.",3,3,EventFrequency.Uncommon,"clock",
                C("take","STEAL THE HOUR","CHOOSE A 2+ ENERGY CARD · NEXT 3 COMBATS","FIRST DRAW COSTS 0 THAT TURN",TemporaryFree(3)));
            Add("unfinished_relic","THE UNFINISHED RELIC","Half of a relic sits unfinished on a workbench.",3,3,EventFrequency.Uncommon,"relic",
                C("complete","COMPLETE IT","LOSE 1 ELIGIBLE COMMON RELIC","GAIN A RANDOM RARE RELIC",RemoveRelic(),Relic(Rarity.Rare)),
                C("break","BREAK IT APART","","GAIN 2 RANDOM FATE SHARDS",RandomShards(2)),
                C("sell","SELL IT","","GAIN 80 GOLD",Gold(80)));
            Add("three_graves","THE THREE GRAVES","Three graves are labeled POWER, KNOWLEDGE, and FORTUNE.",3,3,EventFrequency.Common,"grave",
                C("power","POWER","","GAIN A RANDOM RARE ATTACK",RandomCard(EventCardSource.Own,Rarity.Rare,CardKind.Attack)),
                C("knowledge","KNOWLEDGE","","GAIN A RANDOM RARE SKILL OR POWER",RandomCard(EventCardSource.Own,Rarity.Rare,CardKind.Skill,false,EventCardFilter.Skill)),
                C("fortune","FORTUNE","","GAIN 100 GOLD",Gold(100)));
            Add("price_of_perfection","THE PRICE OF PERFECTION","A voice offers to improve the weakest pieces of your deck.",3,3,EventFrequency.Uncommon,"whisper",
                C("accept","ACCEPT","ADD CURSE: DOOM","UPGRADE EVERY REMAINING BASIC CARD",AddCurse("doom"),UpgradeBasics()),
                C("refuse","REFUSE","CHOOSE 1 BASIC CARD","REMOVE IT",Select(EventEffectKind.RemoveSelected,EventCardFilter.Basic)));
            Add("fractured_altar","THE FRACTURED ALTAR","An ancient altar resonates with your Fate Shards.",3,3,EventFrequency.Rare,"shard",
                C("fracture","FRACTURE FATE","CHOOSE 1 OWNED STABLE FATE SHARD","MOVE IT TO FINAL FRACTURED STATE · GAIN 75 GOLD",OwnedShard(EventEffectKind.FractureShard),Gold(75)));

            // GENERAL — can surface in every Act.
            Add("binder","THE BINDER","A traveling craftsperson carries golden needles and strands.",1,3,EventFrequency.Uncommon,"thread",
                C("bind","COMMISSION A BINDING","PAY 50 / 70 / 90 GOLD BY ACT · CHOOSE AN ELIGIBLE CARD","APPLY A RANDOM COMPATIBLE ACT-UNLOCKED BINDING",Gold(-50),Bind("act_random",EventCardFilter.BindingEligible)));
            Add("lost_travelers","CAMP OF LOST TRAVELERS","A small group of travelers shares what remains of their supplies.",1,3,EventFrequency.Common,"camp",
                C("stories","TRADE STORIES","","REVEAL 3 WANDERER CARDS · CHOOSE 1",RewardCards(EventCardSource.Wanderer,3)),
                C("supplies","TRADE SUPPLIES","CHOOSE 1 OWNED FATE SHARD","REPLACE IT WITH A DIFFERENT RANDOM SHARD",OwnedShard(EventEffectKind.TradeShard)),
                C("rest","REST","","HEAL 10 HP",Heal(10)));
            Add("gilded_fountain","THE GILDED FOUNTAIN","Gold-colored water flows from a cracked statue.",1,3,EventFrequency.Common,"water",
                C("drink","DRINK","","HEAL 18 HP",Heal(18)),
                C("wash","WASH A CARD","CHOOSE 1 CURSE","REMOVE IT",RemoveCurse()),
                C("collect","COLLECT THE WATER","","GAIN 1 RANDOM FATE SHARD",RandomShards(1)));
            Add("fortune_teller","THE FORTUNE TELLER","A hooded figure offers knowledge of the road ahead.",1,3,EventFrequency.Uncommon,"whisper",
                C("road","READ THE ROAD","PAY 25 GOLD","REVEAL THE NEXT 3 REACHABLE MAP OPPORTUNITIES",Gold(-25),RevealMap(3)),
                C("fortune","ASK FOR FORTUNE","PAY 50 GOLD","GAIN A WEIGHTED COMMON OR UNCOMMON RELIC",Gold(-50),Relic(Rarity.Special,"common_uncommon")));
            Add("crossroads_of_fate","THE CROSSROADS OF FATE","Three golden roads appear where only one should exist.",1,3,EventFrequency.Common,"road",
                C("power","POWER","CHOOSE 1 CARD","UPGRADE IT",Select(EventEffectKind.UpgradeSelected,EventCardFilter.Any)),
                C("fortune","FORTUNE","","GAIN 50 GOLD",Gold(50)),
                C("possibility","POSSIBILITY","","REVEAL 3 CARDS · SMALL WANDERER CHANCE",RewardCards(EventCardSource.AnyPlayable,3)));

            if(events.Count!=50)throw new InvalidOperationException("Event catalog must contain exactly 50 events.");
            return events.ToArray();
        }

        private static EventEffectDef E(EventEffectKind kind)=>new(){kind=kind};
        private static EventEffectDef Nothing()=>E(EventEffectKind.Nothing);
        private static EventEffectDef Gold(int amount)=>new(){kind=EventEffectKind.Gold,amount=amount};
        private static EventEffectDef Hp(int amount)=>new(){kind=EventEffectKind.LoseHp,amount=amount};
        private static EventEffectDef Heal(int amount)=>new(){kind=EventEffectKind.Heal,amount=amount};
        private static EventEffectDef AddCurse(string id)=>new(){kind=EventEffectKind.AddCurse,id=id};
        private static EventEffectDef RandomCurse()=>E(EventEffectKind.RandomCurse);
        private static EventEffectDef RemoveCurse()=>Select(EventEffectKind.RemoveCurse,EventCardFilter.Curse);
        private static EventEffectDef Select(EventEffectKind kind,EventCardFilter filter,int count=1)=>new(){kind=kind,filter=filter,count=count};
        private static EventEffectDef RandomUpgrades(int count)=>new(){kind=EventEffectKind.UpgradeRandom,count=count};
        private static EventEffectDef UpgradeBasics()=>E(EventEffectKind.UpgradeAllBasic);
        private static EventEffectDef RewardCards(EventCardSource source,int count,Rarity rarity=Rarity.Special,bool upgraded=false)=>new(){kind=EventEffectKind.RewardCards,source=source,count=count,rarity=rarity,upgraded=upgraded};
        private static EventEffectDef RandomCard(EventCardSource source,Rarity rarity,CardKind kind,bool upgraded=false,EventCardFilter filter=EventCardFilter.Any)=>new(){kind=EventEffectKind.GrantRandomCard,source=source,rarity=rarity,cardKind=kind,upgraded=upgraded,filter=filter};
        private static EventEffectDef Relic(Rarity rarity,string id="")=>new(){kind=EventEffectKind.GrantRelic,rarity=rarity,id=id};
        private static EventEffectDef RemoveRelic()=>E(EventEffectKind.RemoveRelic);
        private static EventEffectDef RewardShards(int reveal,int choose)=>new(){kind=EventEffectKind.RewardShards,amount=reveal,count=choose};
        private static EventEffectDef RandomShards(int count)=>new(){kind=EventEffectKind.GrantRandomShards,count=count};
        private static EventEffectDef Bind(string id,EventCardFilter filter)=>new(){kind=EventEffectKind.BindSelected,id=id,filter=filter,count=1};
        private static EventEffectDef DuplicateBound()=>new(){kind=EventEffectKind.DuplicateWithBinding,filter=EventCardFilter.BindingEligible};
        private static EventEffectDef TempBlock(int amount,int duration)=>new(){kind=EventEffectKind.TemporaryStartBlock,amount=amount,duration=duration};
        private static EventEffectDef TemporaryFree(int duration)=>new(){kind=EventEffectKind.TemporaryFirstDrawFree,filter=EventCardFilter.CostTwoPlus,duration=duration};
        private static EventEffectDef OwnedShard(EventEffectKind kind,string id="")=>new(){kind=kind,id=id,count=1};
        private static EventEffectDef RevealMap(int count)=>new(){kind=EventEffectKind.RevealMap,count=count};
        private static EventEffectDef Fatewheel()=>E(EventEffectKind.Fatewheel);
    }
}
