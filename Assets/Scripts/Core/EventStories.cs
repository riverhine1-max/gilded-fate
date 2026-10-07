using System;
using System.Collections.Generic;

namespace GildedFate.Core
{
    // Events Rework, batch A: multi-step story events.
    // Each event's first scene is its normal choice list; later scenes are reached through
    // a choice's To(scene) or Odds(..., scene). Odds rolls are deterministic per run and step.
    public static partial class EventContent
    {
        private static EventChoiceDef Opt(string id,string title,string cost,string reward,params EventEffectDef[] fx)=>new(id,title,cost,reward,fx);
        private static EventSceneDef Scene(string id,int step,string prompt,params EventChoiceDef[] choices)=>new(){id=id,step=step,prompt=prompt,choices=choices??Array.Empty<EventChoiceDef>()};

        private static void AddStoryEvents(List<EventDefinition> events)
        {
            EventDefinition Story(string id,string name,string prompt,int min,int max,EventFrequency frequency,string cue,string artFrom,int steps,EventChoiceDef[] first,params EventSceneDef[] scenes)
            {
                var art=events.FindIndex(e=>e.id==artFrom);
                var definition=new EventDefinition(id,name,prompt,min,max,frequency,art<0?0:art,cue,first){scenes=scenes??Array.Empty<EventSceneDef>(),steps=Math.Max(1,steps)};
                events.Add(definition);return definition;
            }

            // 1. Push your luck: everything found is banked; the deepest stair can take it all.
            Story("deep_vault","THE DEEP VAULT","A stair spirals down into a dark that smells of old coins.",1,3,EventFrequency.Uncommon,"chest","forbidden_vault",5,
                new[]{
                    Opt("descend","DESCEND","","40 GOLD (BANKED)",BankGold(40)).Says("The first landing glitters.").To("vault_1").Then("Coins lie scattered across the first landing. You pocket them and look down."),
                    Opt("leave","WALK AWAY","","NOTHING",Nothing()).Says("Some doors are best left closed.")},
                Scene("vault_1",2,"The stair narrows. Something below shifts its weight.",
                    Opt("descend","DESCEND TO LEVEL 2","","COMMON RELIC (BANKED)",BankRelic(Rarity.Common)).Says("Claw marks line the walls.")
                        .Odds(25,"25% AMBUSH",false,"Something lunges from the dark. You lose 8 HP, but the relic is yours.","vault_2",Hp(-8)).To("vault_2").Then("A relic rests in a niche. You take it and keep going."),
                    Opt("cash","TAKE YOUR LOOT AND LEAVE","","EVERYTHING BANKED",CashOut()).Says("Walk back up with what you found.").Then("You climb back into the light with everything you found.")),
                Scene("vault_2",3,"The air turns cold enough to see. Whispers count along with your steps.",
                    Opt("descend","DESCEND TO LEVEL 3","","UNCOMMON RELIC (BANKED)",BankRelic(Rarity.Uncommon)).Says("The whispers know your name.")
                        .Odds(40,"40% CURSE",false,"The whispers follow you down. A curse enters your deck.","vault_3",RandomCurse()).To("vault_3").Then("You find an uncommon relic and the whispers fall silent."),
                    Opt("cash","TAKE YOUR LOOT AND LEAVE","","EVERYTHING BANKED",CashOut()).Says("Walk back up with what you found.").Then("You climb back into the light with everything you found.")),
                Scene("vault_3",4,"At the bottom of the stair, a guardian sleeps on a bed of gold.",
                    Opt("descend","SNEAK PAST THE GUARDIAN","","RARE RELIC (BANKED)",Nothing()).Says("One wrong step and it wakes.")
                        .Odds(60,"60% IT WAKES",false,"The guardian wakes. You flee with your life and nothing else, losing 12 HP.","",LoseBank(),Hp(-12))
                        .Otherwise(BankRelic(Rarity.Rare)).To("vault_bottom").Then("You slip past the guardian and lift a rare relic from its hoard."),
                    Opt("cash","TAKE YOUR LOOT AND LEAVE","","EVERYTHING BANKED",CashOut()).Says("Walk back up with what you found.").Then("You climb back into the light with everything you found."),
                    Opt("guardian","WAKE THE GUARDIAN AND FIGHT","START AN ELITE FIGHT","EVERYTHING BANKED · ELITE REWARDS",CashOut(),Fight(true)).Only("Vanguard").Says("A Vanguard doesn't sneak.").Then("You bank your loot, plant your feet and shout. The guardian wakes.")),
                Scene("vault_bottom",5,"The hoard is yours. The guardian's breathing slows behind you.",
                    Opt("cash","TAKE EVERYTHING","","EVERYTHING BANKED",CashOut()).Says("Climb, and don't look back.").Then("You climb out with a fortune the Vault will not forget.")));

            // 2. Push your luck with Gold: feed it until it bursts, or walk away with nothing.
            Story("hungry_purse","THE HUNGRY PURSE","A coin purse breathes on the floor. Each time you look away, it is a little fatter.",1,2,EventFrequency.Uncommon,"chest","hungry_chest",5,
                new[]{
                    Opt("feed","FEED IT 20 GOLD","PAY 20 GOLD","IT MAY BURST",Gold(-20),BankGold(60)).Says("It pays back three times what it eats, if it bursts.")
                        .Odds(15,"15% BURST",true,"The purse splits open and coins pour across the floor.","",CashOut()).To("purse_2").Then("It swallows the coins and swells. Not yet."),
                    Opt("leave","LEAVE IT","","NOTHING",Nothing()).Says("It watches you go.")},
                Scene("purse_2",2,"The purse is heavier now. It is still hungry.",
                    Opt("feed","FEED IT 20 GOLD","PAY 20 GOLD","IT MAY BURST",Gold(-20),BankGold(60)).Says("Three times everything you've fed it.")
                        .Odds(25,"25% BURST",true,"The purse splits open and coins pour across the floor.","",CashOut()).To("purse_3").Then("It swallows the coins and swells. Not yet."),
                    Opt("leave","WALK AWAY","LOSE WHAT YOU FED","NOTHING",LoseBank()).Says("What it ate stays eaten.").Then("You leave the purse hungry and your Gold inside it.")),
                Scene("purse_3",3,"The seams strain. Coins clink inside like a heartbeat.",
                    Opt("feed","FEED IT 20 GOLD","PAY 20 GOLD","IT MAY BURST",Gold(-20),BankGold(60)).Says("The seams are close to giving.")
                        .Odds(35,"35% BURST",true,"The purse splits open and coins pour across the floor.","",CashOut()).To("purse_4").Then("It swallows the coins and swells. Not yet."),
                    Opt("leave","WALK AWAY","LOSE WHAT YOU FED","NOTHING",LoseBank()).Says("What it ate stays eaten.").Then("You leave the purse hungry and your Gold inside it.")),
                Scene("purse_4",4,"It is the size of a chest now, and it is looking at you.",
                    Opt("feed","FEED IT 20 GOLD","PAY 20 GOLD","IT MAY BURST",Gold(-20),BankGold(60)).Says("Almost. Almost.")
                        .Odds(45,"45% BURST",true,"The purse splits open and coins pour across the floor.","",CashOut()).To("purse_5").Then("It swallows the coins and swells. One more feeding will decide it."),
                    Opt("leave","WALK AWAY","LOSE WHAT YOU FED","NOTHING",LoseBank()).Says("What it ate stays eaten.").Then("You leave the purse hungry and your Gold inside it.")),
                Scene("purse_5",5,"The final feeding. If it does not burst now, it never will.",
                    Opt("feed","FEED IT 20 GOLD","PAY 20 GOLD","IT MAY BURST",Gold(-20),BankGold(60)).Says("Everything rides on this.")
                        .Odds(55,"55% BURST",true,"The purse splits open and coins pour across the floor.","",CashOut())
                        .Otherwise(LoseBank()).Then("It swallows the last coins, burps, and crawls into a crack in the wall."),
                    Opt("leave","WALK AWAY","LOSE WHAT YOU FED","NOTHING",LoseBank()).Says("What it ate stays eaten.").Then("You leave the purse hungry and your Gold inside it.")));

            // 3. Mystery: three doors and one hint each.
            Story("whispering_doors","THE WHISPERING DOORS","Three doors stand in an empty hall. Each one makes a different sound.",1,3,EventFrequency.Common,"door","three_sealed_doors",2,
                new[]{
                    Opt("warm","THE WARM DOOR","","???",Nothing()).Says("Heat seeps through the keyhole.")
                        .Odds(60,"60% TREASURE",true,"Treasure, still warm from the fire behind it.","",Relic(Rarity.Uncommon))
                        .Otherwise(Hp(-10)).Then("Fire roars out of the doorway. You lose 10 HP."),
                    Opt("silent","THE SILENT DOOR","","???",Nothing()).Says("No sound at all. Not even your footsteps.").To("doors_stranger").Then("Inside, a stranger sits at a table, shuffling a deck."),
                    Opt("scratching","THE SCRATCHING DOOR","","???",Nothing()).Says("Something on the other side wants out.")
                        .Odds(50,"50% COINS",true,"A cache of coins, and whatever was scratching has fled.","",Gold(90))
                        .Otherwise(RandomCurse()).Then("Something slips past you as the door opens. A curse enters your deck."),
                    Opt("leave","WALK ON","","NOTHING",Nothing()).Says("Not every door needs opening.")},
                Scene("doors_stranger",2,"The stranger deals two cards face down and waits.",
                    Opt("trade","TRADE A CARD","CHOOSE 1 CARD","REMOVE IT · GAIN AN UNCOMMON CARD",Select(EventEffectKind.RemoveSelected,EventCardFilter.Any),RandomCard(EventCardSource.Own,Rarity.Uncommon,CardKind.Skill,false,EventCardFilter.Any)).Says("He takes yours without looking at it.").Then("He slides one of his cards across the table."),
                    Opt("hex","HEX THE DECK","ADD A CURSE","RARE RELIC",RandomCurse(),Relic(Rarity.Rare)).Only("Hexer").Says("A Hexer always knows which card bites.").Then("You hex his deck. He loses, pays up and leaves something behind in yours."),
                    Opt("game","PLAY HIS GAME","","???",Nothing()).Says("One card wins. One card bites.")
                        .Odds(50,"50% WIN",true,"You turn the winning card. He bows and hands you a rare relic.","",Relic(Rarity.Rare))
                        .Otherwise(Hp(-8)).Then("You turn the wrong card. It bites. You lose 8 HP."),
                    Opt("leave","LEAVE","","NOTHING",Nothing()).Says("He keeps shuffling as you go.")));

            // 4. Three mirrors in a row, each with its own bargain.
            Story("mirror_gallery","THE MIRROR GALLERY","A long hall of mirrors. Every reflection is a version of you that chose differently.",2,3,EventFrequency.Uncommon,"mirror","cracked_mirror",3,
                new[]{
                    Opt("step","STEP THROUGH","CHOOSE 1 CARD · ADD A CURSE","UPGRADE IT",Select(EventEffectKind.UpgradeSelected,EventCardFilter.Any),RandomCurse()).Says("This reflection is stronger, and it knows the price.").To("mirror_2").Then("You come out the other side sharper, and heavier."),
                    Opt("shatter","SHATTER IT","LOSE 5 HP · CHOOSE 1 CARD","REMOVE IT",Hp(-5),Select(EventEffectKind.RemoveSelected,EventCardFilter.Any)).Says("Break the glass and the card breaks with it.").To("mirror_2").Then("The glass falls, and a card falls with it."),
                    Opt("walk","WALK ON","","NEXT MIRROR",Nothing()).Says("Your reflection watches you leave.").To("mirror_2").Then("You walk on to the next mirror.")},
                Scene("mirror_2",2,"The second mirror shows you richer, and lonelier.",
                    Opt("gift","TAKE ITS GIFT","ADD A CURSE","GAIN 60 GOLD",Gold(60),RandomCurse()).Says("Gold pours from the glass, and something else.").To("mirror_3").Then("Your purse is heavier. So is your deck."),
                    Opt("shatter","SHATTER IT","LOSE 6 HP · CHOOSE 1 CARD","REMOVE IT",Hp(-6),Select(EventEffectKind.RemoveSelected,EventCardFilter.Any)).Says("Break the glass and the card breaks with it.").To("mirror_3").Then("The glass falls, and a card falls with it."),
                    Opt("walk","WALK ON","","NEXT MIRROR",Nothing()).Says("Your reflection does not follow.").To("mirror_3").Then("You walk on to the last mirror.")),
                Scene("mirror_3",3,"The last mirror shows nothing at all.",
                    Opt("enter","STEP INTO THE NOTHING","","???",Nothing()).Says("No reflection means no rules.")
                        .Odds(50,"50% RARE RELIC",true,"The nothing gives something back: a rare relic.","",Relic(Rarity.Rare))
                        .Otherwise(Hp(-12)).Then("The nothing takes something instead. You lose 12 HP."),
                    Opt("cleanse","SHATTER IT","CHOOSE 1 CURSE","REMOVE IT",RemoveCurse()).Says("Break the last mirror and break a curse with it.").Then("The final mirror shatters, and a curse goes with it."),
                    Opt("leave","WALK OUT","","NOTHING",Nothing()).Says("You've seen enough of yourself.")));

            // 5. Three tests, each harder than the last.
            Story("the_trial","THE TRIAL","A robed examiner sets three stones on a table. Each one is a test.",1,3,EventFrequency.Common,"library","echoing_library",3,
                new[]{
                    Opt("edge","TEST OF EDGE","CHOOSE 1 ATTACK","UPGRADE IT",Select(EventEffectKind.UpgradeSelected,EventCardFilter.Attack)).Says("Show me how you strike.").To("trial_2").Then("The examiner nods. One test down."),
                    Opt("ward","TEST OF WARD","CHOOSE 1 SKILL","UPGRADE IT",Select(EventEffectKind.UpgradeSelected,EventCardFilter.Skill)).Says("Show me how you endure.").To("trial_2").Then("The examiner nods. One test down."),
                    Opt("refuse","REFUSE THE TRIAL","","NOTHING",Nothing()).Says("The examiner shrugs and packs the stones away.")},
                Scene("trial_2",2,"The second stone is heavier. \"Now show me what you can give up.\"",
                    Opt("card","OFFER A CARD","CHOOSE 1 CARD","REMOVE IT",Select(EventEffectKind.RemoveSelected,EventCardFilter.Any)).Says("Less to carry, less to lose.").To("trial_3").Then("The examiner writes something down. Two tests down."),
                    Opt("blood","OFFER BLOOD","LOSE 8 HP","PASS THE TEST",Hp(-8)).Says("Pain is also a kind of answer.").To("trial_3").Then("The examiner writes something down. Two tests down."),
                    Opt("yield","YIELD","","20 GOLD",Gold(20)).Says("Leave with a token for trying.").Then("The examiner presses a few coins into your hand.")),
                Scene("trial_3",3,"The last stone is cracked through the middle. \"The final test has no right answer.\"",
                    Opt("accept","ACCEPT THE FINAL TEST","","???",Nothing()).Says("Pass, and the examiner's reward is yours.")
                        .Odds(50,"50% PASS",true,"You pass. The examiner hands you an uncommon relic without a word.","",Relic(Rarity.Uncommon))
                        .Otherwise(Hp(-10)).Then("The stone splits and so does your guard. You lose 10 HP."),
                    Opt("break","BREAK THE STONE","LOSE 6 HP","UNCOMMON RELIC",Hp(-6),Relic(Rarity.Uncommon)).Only("Vanguard").Says("There's always one right answer: hit it.").Then("The stone shatters. The examiner blinks, then hands over the relic."),
                    Opt("yield","YIELD","","30 GOLD",Gold(30)).Says("Two out of three is still a story.").Then("The examiner pays you for the two tests you passed.")));

            // 6. A funeral march, with an option only the Reaper sees.
            Story("hollow_procession","THE HOLLOW PROCESSION","A silent funeral march passes through the hall. The mourners have no faces.",2,3,EventFrequency.Uncommon,"grave","three_graves",2,
                new[]{
                    Opt("carry","CARRY THE COFFIN","LOSE 6 HP","GAIN 5 MAX HP",Hp(-6),MaxHp(5)).Says("It is heavier than any body should be.").To("procession_2").Then("You carry it to the end of the hall and feel stronger for it."),
                    Opt("speak","SPEAK THE NAME","CHOOSE 1 CURSE","REMOVE IT",RemoveCurse()).Says("Name the dead and they release what they hold.").To("procession_2").Then("You say the name. A curse lifts from your deck."),
                    Opt("command","COMMAND THE DEAD","","GAIN A RARE ATTACK",RandomCard(EventCardSource.Own,Rarity.Rare,CardKind.Attack)).Says("They were always going to follow a Reaper.").Only("Reaper").To("procession_2").Then("The mourners turn toward you and kneel."),
                    Opt("aside","STEP ASIDE","","NOTHING",Nothing()).Says("Let the dead pass.")},
                Scene("procession_2",2,"The march stops at an open grave and sets the coffin down.",
                    Opt("open","OPEN THE COFFIN","","???",Nothing()).Says("Whatever is inside was buried for a reason.")
                        .Odds(50,"50% RARE CARD",true,"Inside lies a card no one has played in a century. It's yours.","",RandomCard(EventCardSource.Own,Rarity.Rare,CardKind.Skill,false,EventCardFilter.Any))
                        .Otherwise(RandomCurse(),Hp(-6)).Then("Something climbs out. You lose 6 HP and a curse follows you home."),
                    Opt("bury","BURY IT","","GAIN 40 GOLD",Gold(40)).Says("The mourners leave coins on the grave.").Then("The mourners leave their coins and fade away."),
                    Opt("leave","LEAVE","","NOTHING",Nothing()).Says("The dead can bury the dead.")));

            // 7. Bidding: the longer you stay in, the more it costs, and the surer you are to win.
            Story("gilded_auction","THE GILDED AUCTION","Phantom bidders fill a gilded hall. On the block: a rare relic, still humming.",2,3,EventFrequency.Uncommon,"merchant","last_merchant",3,
                new[]{
                    Opt("bid","BID 60 GOLD","NEED 60 GOLD","RARE RELIC IF YOU WIN",Nothing()).Says("A low opening bid. The phantoms may let it go.")
                        .Odds(45,"45% WIN",true,"No phantom hand rises. Sold, for 60 Gold.","",Gold(-60),Relic(Rarity.Rare)).To("auction_2").Then("A phantom hand rises. Outbid."),
                    Opt("spook","SPOOK THE PHANTOMS","PAY 75 GOLD","RARE RELIC",Gold(-75),Relic(Rarity.Rare)).Only("Reaper").Says("The dead don't bid against a Reaper.").Then("One look from you and the hall empties. Sold, for 75 Gold."),
                    Opt("leave","WALK AWAY","","NOTHING",Nothing()).Says("Let the dead buy their treasures.")},
                Scene("auction_2",2,"The price climbs. Half the phantoms have stopped bidding.",
                    Opt("bid","BID 90 GOLD","NEED 90 GOLD","RARE RELIC IF YOU WIN",Nothing()).Says("Most phantoms can't follow you this high.")
                        .Odds(70,"70% WIN",true,"The hall goes quiet. Sold, for 90 Gold.","",Gold(-90),Relic(Rarity.Rare)).To("auction_3").Then("One phantom stays in. Outbid again."),
                    Opt("leave","DROP OUT","","NOTHING",Nothing()).Says("You pay nothing for losing.")),
                Scene("auction_3",3,"One phantom remains, glaring at you across the hall.",
                    Opt("bid","BID 120 GOLD","PAY 120 GOLD","RARE RELIC",Gold(-120),Relic(Rarity.Rare)).Says("No phantom can follow this bid.").Then("The last phantom bows out. Sold, for 120 Gold."),
                    Opt("leave","DROP OUT","","NOTHING",Nothing()).Says("You pay nothing for losing.")));

            // 8. A shell game with the Dealer, then double or nothing.
            Story("dealers_table","THE DEALER'S TABLE","A grinning dealer shuffles three cups across a velvet table.",1,3,EventFrequency.Common,"merchant","traveling_merchant",3,
                new[]{
                    Opt("bet25","BET 25 GOLD","PAY 25 GOLD","WIN 50",Gold(-25)).Says("A friendly game.").To("cups_25").Then("The cups start moving."),
                    Opt("bet50","BET 50 GOLD","PAY 50 GOLD","WIN 100",Gold(-50)).Says("Now it's interesting.").To("cups_50").Then("The cups start moving faster."),
                    Opt("bet100","BET 100 GOLD","PAY 100 GOLD","WIN 200",Gold(-100)).Says("The dealer stops smiling.").To("cups_100").Then("The cups blur."),
                    Opt("leave","WALK PAST","","NOTHING",Nothing()).Says("The house always wins.")},
                Scene("cups_25",2,"The cups stop. Under one of them is your coin.",Cups(25)),
                Scene("cups_50",2,"The cups stop. Under one of them is your coin.",Cups(50)),
                Scene("cups_100",2,"The cups stop. Under one of them is your coin.",Cups(100)),
                Scene("ride_25",3,"\"Double or nothing?\" The dealer is already shuffling.",Ride(25)),
                Scene("ride_50",3,"\"Double or nothing?\" The dealer is already shuffling.",Ride(50)),
                Scene("ride_100",3,"\"Double or nothing?\" The dealer is already shuffling.",Ride(100)));

            // 9. A shrine that blesses one shard and then names its price.
            Story("waiting_shard","SHRINE OF THE WAITING SHARD","An empty socket waits on an altar, humming the same note as your Fate Shards.",2,3,EventFrequency.Uncommon,"shard","fractured_altar",2,
                new[]{
                    Opt("prime","PRIMED","CHOOSE 1 OWNED SHARD","STARTS EVERY FIGHT WITH 3 CHARGE",OwnedShard(EventEffectKind.RepairShard,"prime")).Says("It will wake faster in every battle.").To("shrine_price").Then("The shard hums louder. It will never start cold again."),
                    Opt("mend","MENDED","CHOOSE 1 OWNED SHARD","REGAIN 1 USE",OwnedShard(EventEffectKind.RepairShard)).Says("One crack seals itself.").To("shrine_price").Then("One crack in the shard seals itself."),
                    Opt("awaken","AWAKENED EARLY","CHOOSE 1 STABLE SHARD","IT BECOMES FRACTURED",OwnedShard(EventEffectKind.FractureShard)).Says("Full power now, one use left.").To("shrine_price").Then("The shard splits along its seams and blazes with Fractured light."),
                    Opt("leave","LEAVE THE ALTAR","","NOTHING",Nothing()).Says("The socket will wait for someone else.")},
                Scene("shrine_price",2,"The altar's hum drops to a growl. Blessings are never free.",
                    Opt("blood","PAY IN BLOOD","LOSE 10 HP","THE DEBT IS PAID",Hp(-10)).Says("Quick and honest.").Then("The altar drinks and falls silent."),
                    Opt("curse","PAY IN FATE","ADD A CURSE","THE DEBT IS PAID",RandomCurse()).Says("A slower price, carried with you.").Then("The altar's growl follows you into your deck.")));

            // ---------------- Batch B: memory across floors, map edits, fights ----------------

            // 10. A loan that comes due three floors later.
            Story("collectors_loan","THE COLLECTOR'S LOAN","A masked collector opens a ledger with your name already written in it.",1,2,EventFrequency.Uncommon,"merchant","collector_event",2,
                new[]{
                    Opt("loan","TAKE THE LOAN","HE RETURNS IN 3 FLOORS","GAIN 100 GOLD",Gold(100),Flag("loan_big"),ReturnIn("collector_returns",3)).Says("Repay 150 when he comes back.").Then("He counts out 100 Gold. \"Three floors,\" he says, and is gone."),
                    Opt("terms","ASK FOR SMALLER TERMS","","???",Nothing()).Says("He has other ledgers.").To("loan_terms").Then("He flips to a thinner page."),
                    Opt("decline","DECLINE","","NOTHING",Nothing()).Says("Never borrow from the Vault.")},
                Scene("loan_terms",2,"\"Sixty now, ninety later. A gentler debt.\"",
                    Opt("small","TAKE THE SMALL LOAN","HE RETURNS IN 3 FLOORS","GAIN 60 GOLD",Gold(60),Flag("loan_small"),ReturnIn("collector_returns",3)).Says("Repay 90 when he comes back.").Then("He counts out 60 Gold and writes something small in the margin."),
                    Opt("decline","DECLINE","","NOTHING",Nothing()).Says("You close the ledger for him.")));
            Story("collector_returns","THE COLLECTOR RETURNS","The Collector steps out of the dark, ledger open. Your debt is due.",1,3,EventFrequency.Rare,"merchant","collector_event",1,
                new[]{
                    Opt("repay","REPAY 150 GOLD","PAY 150 GOLD","THE DEBT IS CLEARED",Gold(-150),Unflag("loan_big")).If("loan_big").Says("He strikes your name from the page.").Then("He takes the Gold and strikes your name from the ledger."),
                    Opt("repay_small","REPAY 90 GOLD","PAY 90 GOLD","THE DEBT IS CLEARED",Gold(-90),Unflag("loan_small")).If("loan_small").Says("He strikes your name from the page.").Then("He takes the Gold and strikes your name from the ledger."),
                    Opt("relic","GIVE HIM A RELIC","LOSE 1 COMMON RELIC","THE DEBT IS CLEARED",RemoveRelic(),Unflag("loan_big"),Unflag("loan_small")).Says("He accepts payment in kind.").Then("He weighs the relic in his palm and nods."),
                    Opt("refuse","REFUSE TO PAY","ADD 2 CURSES","KEEP YOUR GOLD",RandomCurse(),RandomCurse(),Unflag("loan_big"),Unflag("loan_small")).Says("He writes your name twice.").Then("He writes your name twice, and the ink follows you.")});
            events[events.Count-1].returnOnly=true;

            // 11-12. A beggar in Act I who remembers you in Act III.
            Story("ragged_prince","THE RAGGED PRINCE","A young beggar in a torn royal sash asks for anything you can spare.",1,1,EventFrequency.Common,"whisper","golden_beggar",1,
                new[]{
                    Opt("gold","GIVE 40 GOLD","PAY 40 GOLD","HE WILL REMEMBER",Gold(-40),Flag("prince_met"),Flag("prince_gave")).Says("A crown is a long way off.").Then("He bows deeply. \"I will remember this.\""),
                    Opt("card","GIVE HIM A CARD","CHOOSE 1 CARD","REMOVE IT · HE WILL REMEMBER",Select(EventEffectKind.RemoveSelected,EventCardFilter.Any),Flag("prince_met"),Flag("prince_gave")).Says("He studies it like a map.").Then("He tucks the card into his sash. \"I will remember this.\""),
                    Opt("refuse","REFUSE","","HE WILL REMEMBER",Flag("prince_met"),Flag("prince_refused")).Says("He watches you go without a word.").Then("He says nothing. He doesn't need to.")});
            var king=Story("beggar_king","THE BEGGAR KING","A throne of stacked coin. The ragged prince from long ago sits on it, crowned.",3,3,EventFrequency.Common,"whisper","crown_without_king",1,
                new[]{
                    Opt("gift","ACCEPT HIS GRATITUDE","","RARE RELIC · 60 GOLD",Relic(Rarity.Rare),Gold(60)).If("prince_gave").Says("\"You gave when I had nothing.\"").Then("He presses a rare relic and a purse into your hands."),
                    Opt("toll","PAY HIS TOLL","PAY 80 GOLD","PASS IN PEACE",Gold(-80)).If("prince_refused").Says("\"You gave me nothing. Now you pay.\"").Then("You pay. He doesn't look up."),
                    Opt("guard","FIGHT HIS GUARD","START AN ELITE FIGHT","ELITE REWARDS",Fight(true)).If("prince_refused").Says("His champion steps forward.").Then("The king waves a hand, and his champion draws steel."),
                    Opt("bow","BOW AND LEAVE","","NOTHING",Nothing()).Says("Kings have long memories.")});
            king.requiresFlag="prince_met";

            // 13. An escort across three floors.
            Story("ashen_pilgrim","THE ASHEN PILGRIM","A pilgrim covered in ash asks you to walk with her to the next shrine.",1,2,EventFrequency.Uncommon,"road","lost_travelers",1,
                new[]{
                    Opt("escort","ESCORT HER","AVOID ELITES FOR 3 FLOORS","START FIGHTS WITH 6 BLOCK · A RELIC AT THE END",TempBlock(6,3),Flag("pilgrim"),ReturnIn("pilgrim_farewell",3)).Says("She shields you; you shield her.").Then("She falls into step beside you. \"Three floors. Keep me out of the worst of it.\""),
                    Opt("fire","SHARE YOUR FIRE","","HEAL 10 HP",Heal(10)).Says("A rest, and then she walks on alone.").Then("You rest by the fire together before she leaves."),
                    Opt("pass","PASS HER BY","","NOTHING",Nothing()).Says("Everyone walks the Vault alone.")});
            Story("pilgrim_farewell","THE PILGRIM'S FAREWELL","The shrine is in sight. The pilgrim turns to you.",1,3,EventFrequency.Rare,"road","lost_travelers",1,
                new[]{
                    Opt("gift","ACCEPT HER GIFT","","UNCOMMON RELIC",Relic(Rarity.Uncommon),Unflag("pilgrim")).Unless("pilgrim_elite").Says("\"You kept me safe.\"").Then("She gives you the relic she was carrying to the shrine."),
                    Opt("search","SEARCH FOR HER","LOSE 6 HP","COMMON RELIC",Hp(-6),Relic(Rarity.Common),Unflag("pilgrim"),Unflag("pilgrim_elite")).If("pilgrim_elite").Says("She fled when you charged an elite.").Then("You find her hiding, shaken. She gives you a lesser gift."),
                    Opt("let","LET HER GO","","NOTHING",Unflag("pilgrim"),Unflag("pilgrim_elite")).Says("Some roads end early.").Then("You walk the last stretch alone.")});
            events[events.Count-1].returnOnly=true;

            // 14. Change what lies ahead on the map.
            Story("cartographer","THE CARTOGRAPHER","A woman with ink-black fingers redraws the map around you as you watch.",1,3,EventFrequency.Uncommon,"road","wrong_road",2,
                new[]{
                    Opt("treasure","CHART A TREASURE ROOM","PAY 60 GOLD","A ROOM AHEAD BECOMES TREASURE",Gold(-60),Chart(NodeKind.Treasure)).Says("One path ahead turns to gold.").To("chart_more").Then("She draws a chest on the next floor. The room changes to match."),
                    Opt("merchant","CHART A MERCHANT","PAY 40 GOLD","A ROOM AHEAD BECOMES A MERCHANT",Gold(-40),Chart(NodeKind.Merchant)).Says("Someone to sell to, soon.").To("chart_more").Then("She sketches a stall on the next floor. A lantern lights up there."),
                    Opt("elite","CHART AN ELITE","","A ROOM AHEAD BECOMES ELITE · 30 GOLD",Chart(NodeKind.Elite),Gold(30)).Says("She pays you to test her drawing.").To("chart_more").Then("She draws something with teeth on the next floor, and pays you for your nerve."),
                    Opt("leave","LEAVE HER TO HER WORK","","NOTHING",Nothing()).Says("The map is fine as it is.")},
                Scene("chart_more",2,"\"One more mark, if you can pay for it.\"",
                    Opt("rest","CHART A SANCTUARY","PAY 30 GOLD","A ROOM AHEAD BECOMES A SANCTUARY",Gold(-30),Chart(NodeKind.Sanctuary)).Says("A place to rest.").Then("She draws a small flame. A sanctuary waits ahead."),
                    Opt("thanks","THANK HER","","NOTHING",Nothing()).Says("One change is enough.").Then("She rolls up her map and nods.")));

            // 15. Cross to any lane on the next floor.
            Story("bridge_of_threads","THE BRIDGE OF THREADS","A chasm splits the floor. Golden threads hang across it, waiting to be woven.",1,3,EventFrequency.Uncommon,"thread","frayed_thread",2,
                new[]{
                    Opt("weave","WEAVE THE BRIDGE","LOSE 8 HP","NEXT MOVE: ANY PATH",Hp(-8),OpenLanes()).Says("Blood makes the threads hold.").To("bridge_mid").Then("The threads knot themselves into a bridge. You step out onto it."),
                    Opt("leave","TURN BACK","","NOTHING",Nothing()).Says("Your path is your path.")},
                Scene("bridge_mid",2,"Halfway across, the bridge starts to fray.",
                    Opt("hold","HOLD ON","LOSE 6 HP","CROSS SAFELY",Hp(-6)).Says("The threads cut your hands.").Then("You haul yourself to the far side. Every path ahead is open."),
                    Opt("drop","DROP SOME GOLD","PAY 35 GOLD","CROSS SAFELY",Gold(-35)).Says("Lighten the load.").Then("Coins tumble into the dark, and the bridge holds. Every path ahead is open."),
                    Opt("back","CLIMB BACK","","THE BRIDGE IS LOST",Unflag("bridge")).Says("Not today.").Then("You climb back. The bridge unravels behind you.")));

            // 16. A champion's duel: the room becomes an elite fight.
            Story("champions_challenge","THE CHAMPION'S CHALLENGE","A champion of the Vault plants a banner and waits for a worthy opponent.",1,3,EventFrequency.Uncommon,"duel","duel",1,
                new[]{
                    Opt("duel","ACCEPT THE DUEL","START AN ELITE FIGHT","ELITE REWARDS",Fight(true)).Says("Win, and the champion's spoils are yours.").Then("The champion raises a blade in salute. The fight begins."),
                    Opt("spar","SPAR FOR COIN","LOSE 10 HP","GAIN 60 GOLD",Hp(-10),Gold(60)).Says("Just a friendly bout.").Then("A few bruises, a fair purse."),
                    Opt("decline","DECLINE","","NOTHING",Nothing()).Says("The champion spits and turns away.")});

            // 17. Sacrifice one shard to empower the other.
            Story("shard_furnace","THE SHARD FURNACE","A furnace roars with blue fire. Fate Shards melt inside it like candle wax.",2,3,EventFrequency.Uncommon,"shard","shard_smith",2,
                new[]{
                    Opt("feed","FEED A SHARD TO THE FIRE","CHOOSE 1 OWNED SHARD","IT IS DESTROYED",OwnedShard(EventEffectKind.RepairShard,"remove")).NeedsShards(2).Says("Its power will pour into the other.").To("furnace_forge").Then("The shard melts. Its light pours into the one you kept."),
                    Opt("leave","LEAVE THE FURNACE","","NOTHING",Nothing()).Says("Some fires are better left alone.")},
                Scene("furnace_forge",2,"Your remaining shard glows white-hot in the furnace's mouth.",
                    Opt("stoke","STOKE THE FURNACE","","FULLY RESTORED · PRIMED",Nothing()).Says("Restore every use and prime it, if it holds.")
                        .Odds(70,"70% IT HOLDS",true,"It holds. Your shard comes out whole, fully restored and primed.","",EmpowerShards())
                        .Otherwise(FractureAllShards()).Then("It cracks in the heat. Your shard comes out Fractured."),
                    Opt("pull","PULL IT OUT","","NOTHING MORE",Nothing()).Says("Take the loss and walk away.").Then("You pull the shard from the fire, unchanged.")));
        }

        private static EventChoiceDef[] Cups(int stake)
        {
            EventChoiceDef Cup(string id,string title,string line)=>Opt(id,title,"","WIN "+stake*2+" GOLD",Nothing()).Says(line)
                .Odds(34,"1 IN 3",true,"Your coin, right where you guessed. You win "+stake*2+" Gold.","ride_"+stake,Gold(stake*2))
                .Then("Empty. The dealer sweeps your stake away.");
            var sight=Opt("sight","SEE THROUGH THE CUPS","LOSE 4 HP","WIN "+stake*2+" GOLD",Hp(-4),Gold(stake*2)).Only("Hexer").Says("A Hexer's eyes see through wood.").To("ride_"+stake).Then("You point at the right cup without blinking. The dealer pays, scowling.");
            return new[]{Cup("left","THE LEFT CUP","It moved last."),Cup("middle","THE MIDDLE CUP","It never seemed to move."),Cup("right","THE RIGHT CUP","The dealer glanced at it."),sight};
        }
        private static EventChoiceDef[] Ride(int stake)
        {
            var pot=stake*2;
            return new[]{
                Opt("ride","LET IT RIDE","PAY "+pot+" GOLD","WIN "+pot*2+" GOLD",Gold(-pot)).Says("Double or nothing, once.")
                    .Odds(50,"50% WIN",true,"You win again. The dealer pays out "+pot*2+" Gold through gritted teeth.","",Gold(pot*2))
                    .Then("Empty. The dealer smiles for the first time."),
                Opt("cash","CASH OUT","","KEEP YOUR WINNINGS",Nothing()).Says("Quit while you're ahead.").Then("You pocket your winnings and leave the dealer shuffling.")};
        }
    }
}
