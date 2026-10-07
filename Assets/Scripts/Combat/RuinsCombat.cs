using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Act 1 · Theme 1: Gilded Ruins AI. Plugs into the shared themed-enemy engine (WildCombat.cs) at the end of
    // the chain (Hollowwood → Observatory → here). Identity: SEIZED WEALTH + FORMATION PRIORITY.
    //
    // Gold rules (all combat-local, so nothing here can destroy the run's saved Gold):
    //  - playerGold is the player's current Gold as combat sees it (set when the fight begins).
    //  - SEIZE moves Gold from playerGold to ONE enemy's WildMind.held (never more than the player has).
    //  - When that enemy dies, or the fight is won, held Gold returns to playerGold and held becomes 0,
    //    so a second refund is impossible.
    //  - BONUS GOLD (Coin Mimic) is a separate pool that only becomes extra reward; it never touches playerGold.
    //
    // RNG is used only for: Command targets, Chorister / Auctioneer-ally ties and targets, and the Auctioneer's Lot.
    //
    // WildMind use here:
    //   held     = Gold this enemy has Seized
    //   counter  = Coin Mimic Bonus Gold · Living Treasury Reserve (0–6)
    //   state    = Oathbound Bastion mode ("GROUP" / "SOLO") · Royal Auctioneer announced Lot move
    //   lastSeed = Royal Auctioneer previous Lot move
    //   queue    = Tarnished Appraiser queued appraisal result
    //   flag     = Crown Collector replacement used · Living Treasury Emergency Reserve used
    //   phase    = The Last Procession phase
    public sealed partial class CombatState
    {
        public const int AppraiserGoldThreshold=50,MimicBonusGold=18,MaxReserve=6,CollectorBonusGold=10;
        // The player's Gold as combat sees it. Set by the run when a fight begins; saved with the checkpoint.
        public int playerGold;
        // Cards played in the current / immediately previous player turn (Crownless Duelist reads the previous one).
        public int attackCardsThisTurn,otherCardsThisTurn,lastTurnAttackCards,lastTurnOtherCards;
        public bool hadPreviousTurn;
        // Living Treasury: one Reserve per card, however many hits or copies it has.
        public int reserveCardsPlayed=-1,reserveCardInstance=-1;

        private static readonly string[] AuctionLots={"gr_lot_blades","gr_lot_protection","gr_lot_misfortune","gr_lot_tribute"};

        // ---------- Gold and card memory ----------
        private void RollRuinsMemory()
        {
            RollCathedralMemory();
            hadPreviousTurn=turn>0;lastTurnAttackCards=attackCardsThisTurn;lastTurnOtherCards=otherCardsThisTurn;
            attackCardsThisTurn=otherCardsThisTurn=0;
        }
        private void CountRuinsCard(CardDef card){CountCathedralCard(card);if(card.kind==CardKind.Attack)attackCardsThisTurn++;else otherCardsThisTurn++;}
        public int SeizedGoldAt(int index)=>MindAt(index)?.held??0;
        public int TotalSeizedGold{get{var total=0;for(var i=0;i<EnemyCount;i++)total+=SeizedGoldAt(i);return total;}}
        // Extra Gold added to the encounter reward: what the Coin Mimic did not consume, plus the Crown Collector's bonus.
        public int RuinsRewardBonus
        {
            get
            {
                if(!wildCombat||opponents==null)return 0;var total=0;
                foreach(var o in opponents)
                {
                    if(o.mind==null)continue;
                    if(o.id==GildedRuinsContent.Mimic)total+=Math.Max(0,o.mind.counter);
                    else if(o.id==GildedRuinsContent.Collector)total+=CollectorBonusGold;
                }
                return total;
            }
        }
        private int SeizeGold(int ownerIndex,int amount)
        {
            var m=MindAt(ownerIndex);if(m==null||amount<0)return 0;
            var take=Math.Min(amount,Math.Max(0,playerGold));playerGold-=take;m.held+=take;
            InEnemyContext(ownerIndex,()=>{if(take>0)Emit(CombatEventKind.Status,take,false,null,"SEIZED GOLD");EmitHook("seize_gold");});
            return take;
        }
        // Returns everything one enemy holds. Safe to call twice: the second call finds nothing.
        private void ReturnSeizedGold(int index)
        {
            var m=MindAt(index);if(m==null||m.held<=0)return;
            var amount=m.held;m.held=0;playerGold+=amount;
            InEnemyContext(index,()=>{Emit(CombatEventKind.Status,amount,false,null,"GOLD RETURNED");EmitHook("gold_returned");});
        }
        // Called when the fight is won: any Gold still held comes back exactly once.
        public void ReturnAllSeizedGold(){if(!wildCombat||opponents==null)return;for(var i=0;i<opponents.Count;i++)ReturnSeizedGold(i);}
        private void RuinsOnDeath(int index){ReturnSeizedGold(index);CathedralOnDeath(index);}

        // ---------- Treasury Reserve ----------
        private void RuinsAttackDamage(CardDef card,int dealt)
        {
            if(!wildCombat||dealt<=0||card==null||card.kind!=CardKind.Attack||enemyId!=GildedRuinsContent.Treasury)return;
            // Once per card played, whatever its hit count or repeats.
            if(reserveCardsPlayed==cardsPlayed&&reserveCardInstance==card.instanceId)return;
            reserveCardsPlayed=cardsPlayed;reserveCardInstance=card.instanceId;
            var m=Mind;if(m==null||m.counter>=MaxReserve)return;
            m.counter++;Emit(CombatEventKind.Status,1,false,null,"RESERVE");EmitHook("reserve_gain");
            // Lockdown needs Reserve 2 and Asset Release scales with it: re-read the move so the intent never lies.
            if(PlayerPhaseReplan&&(m.planned=="gr_vault_slam"||m.planned=="gr_lockdown"))ReplanAt(enemyContextIndex>=0?enemyContextIndex:0);
        }
        private bool EmergencyLegal(WildMind m)=>!m.flag&&m.counter>0&&enemy.hp*100<35*enemy.maxHp;

        // ---------- setup ----------
        private static void ResetRuinsState(WildMind m,string id)
        {
            m.held=0;
            if(id==GildedRuinsContent.Mimic)m.counter=MimicBonusGold;
            if(id==GildedRuinsContent.Treasury)m.counter=0;
            ResetCathedralState(m,id);
        }

        // ---------- move choice ----------
        private string ChooseRuinsMove(WildMind m)
        {
            var self=enemyContextIndex;
            switch(enemyId)
            {
                case GildedRuinsContent.Scavenger:return Cycle(m,3) switch{0=>"gr_ragged_slash",1=>"gr_pocket_spoils",_=>"gr_desperate_cut"};
                case GildedRuinsContent.Bastion:
                {
                    var hasAlly=LivingAllies(self,true).Any();
                    // The pattern is latched by how the fight began: with company, or alone.
                    if(string.IsNullOrEmpty(m.state))m.state=hasAlly?"GROUP":"SOLO";
                    switch(Cycle(m,3))
                    {
                        case 0:return m.state=="SOLO"||!hasAlly?"gr_brace":"gr_interpose";
                        case 1:return "gr_shield_crush";
                        default:return m.state=="SOLO"?"gr_shield_crush":"gr_hold_line";
                    }
                }
                case GildedRuinsContent.Chorister:return Cycle(m,3) switch{0=>"gr_march_of_gold",1=>"gr_royal_refrain",_=>"gr_grand_chorus"};
                case GildedRuinsContent.Appraiser:
                    switch(Cycle(m,3))
                    {
                        case 0:return "gr_appraise_wealth";
                        case 1:return !string.IsNullOrEmpty(m.queue)?m.queue:playerGold>=AppraiserGoldThreshold?"gr_overvalued":"gr_worthless";
                        default:return "gr_marked_asset";
                    }
                case GildedRuinsContent.Keeper:
                {
                    var owned=OwnedMinions(self).Count();var canAwaken=CanSummon(self);
                    switch(Cycle(m,4))
                    {
                        case 0:return owned==0&&canAwaken?"gr_awaken_servitor":"gr_relic_slam";
                        case 1:return "gr_relic_slam";
                        case 2:
                            if(owned<2)return canAwaken?"gr_awaken_servitor":"gr_relic_slam";
                            return EligibleCommandTargets(self).Any()?"gr_keeper_command":"gr_relic_slam";
                        default:return OwnedMinions(self).Any(i=>EnemyAt(i).hp<EnemyAt(i).maxHp)?"gr_repair_rite":"gr_relic_slam";
                    }
                }
                case GildedRuinsContent.Servitor:return Cycle(m,2)==0?"gr_servitor_jab":"gr_brace_frame";
                case GildedRuinsContent.Mimic:return Cycle(m,4) switch{0=>"gr_glittering_bait",1=>"gr_snap_shut",2=>"gr_hoard",_=>"gr_devour_value"};
                case GildedRuinsContent.Herald:return Cycle(m,3) switch{0=>"gr_first_toll",1=>"gr_second_toll",_=>"gr_third_toll"};
                case GildedRuinsContent.Duelist:
                {
                    var total=lastTurnAttackCards+lastTurnOtherCards;
                    if(!hadPreviousTurn||total<3||lastTurnAttackCards==lastTurnOtherCards)return "gr_measured_cut";
                    return lastTurnAttackCards>lastTurnOtherCards?"gr_punishing_guard":"gr_relentless_advance";
                }
                case GildedRuinsContent.Collector:
                    switch(Cycle(m,4))
                    {
                        case 0:return "gr_collect_due";
                        case 1:return "gr_royal_levy";
                        case 2:return EligibleCommandTargets(self).Any()?"gr_collection_order":"gr_foreclosure";
                        default:return !m.flag&&OwnedMinions(self).Count()<2&&CanSummon(self)?"gr_repossess":"gr_foreclosure";
                    }
                case GildedRuinsContent.Guard:return Cycle(m,2)==0?"gr_taxblade":"gr_guard_collector";
                case GildedRuinsContent.Auctioneer:
                    // The Lot is announced when Open Bidding is planned and never changes after that.
                    if(string.IsNullOrEmpty(m.state)&&Cycle(m,3)<2)m.state=PickOther(AuctionLots,m.lastSeed);
                    switch(Cycle(m,3))
                    {
                        case 0:return "gr_open_bidding";
                        case 1:return m.state;
                        default:return "gr_hammer_fall";
                    }
                case GildedRuinsContent.Treasury:
                    // Emergency Reserve takes priority, once, the moment it is legal.
                    if(EmergencyLegal(m))return "gr_emergency_reserve";
                    switch(Cycle(m,3))
                    {
                        case 0:return "gr_vault_slam";
                        case 1:return m.counter>=2?"gr_lockdown":"gr_vault_slam";
                        default:return "gr_asset_release";
                    }
                case GildedRuinsContent.Procession:
                    switch(m.phase)
                    {
                        case 1:return Cycle(m,4) switch{0=>"gr_royal_salute",1=>"gr_collect_tribute",2=>"gr_processional_guard",_=>"gr_golden_fanfare"};
                        case 2:return Cycle(m,4) switch{0=>"gr_gilded_ram",1=>"gr_coin_barrage",2=>"gr_forced_march",_=>"gr_seize_streets"};
                        default:return Cycle(m,4) switch{0=>"gr_crown_hammer",1=>"gr_final_tribute",2=>"gr_royal_furnace",_=>"gr_end_procession"};
                    }
            }
            return ChooseCathedralMove(m);
        }

        // ---------- moves ----------
        private PlannedEnemyAction[] RuinsActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,W=EnemyActionType.Weak,V=EnemyActionType.Vulnerable;
            switch(move)
            {
                // Giltblade Scavenger
                case "gr_ragged_slash":return new[]{P(A,7)};
                case "gr_pocket_spoils":return new[]{P(EnemyActionType.SeizeGold,3),P(A,4)};
                case "gr_desperate_cut":return new[]{P(A,m.held>0?13:11)};
                // Oathbound Bastion
                case "gr_shield_crush":return new[]{P(A,7),P(B,6)};
                case "gr_interpose":return new[]{P(EnemyActionType.BlockLowestNonMinion,10)};
                case "gr_brace":return new[]{P(B,13)};
                case "gr_hold_line":return LivingAllies(enemyContextIndex,true).Any()?new[]{P(B,6),P(EnemyActionType.BlockLowestNonMinion,8)}:new[]{P(B,14)};
                // Gilded Chorister
                case "gr_march_of_gold":return new[]{P(EnemyActionType.StrengthRandomAlly,1)};
                case "gr_royal_refrain":return new[]{P(EnemyActionType.BlockAllies,7)};
                case "gr_grand_chorus":return new[]{P(EnemyActionType.BlockAllies,6),P(EnemyActionType.StrengthRandomAlly,1)};
                // Tarnished Appraiser
                case "gr_appraise_wealth":return new[]{P(B,6)};
                case "gr_overvalued":return new[]{P(EnemyActionType.SeizeGold,6),P(B,7)};
                case "gr_worthless":return new[]{P(A,6),P(W,1)};
                case "gr_marked_asset":return new[]{P(V,1),P(A,7)};
                // Reliquary Keeper and Gilded Servitor
                case "gr_awaken_servitor":return new[]{P(EnemyActionType.Summon,1),P(B,5)};
                case "gr_relic_slam":return new[]{P(A,8)};
                case "gr_repair_rite":return new[]{P(EnemyActionType.HealMinion,8),P(EnemyActionType.BlockTarget,6)};
                case "gr_keeper_command":return new[]{P(EnemyActionType.Command,1),P(B,5)};
                case "gr_servitor_jab":return new[]{P(A,5)};
                case "gr_brace_frame":return new[]{P(B,6)};
                // Coin Mimic
                case "gr_glittering_bait":return new[]{P(B,5)};
                case "gr_snap_shut":return new[]{P(A,12)};
                case "gr_hoard":return new[]{P(EnemyActionType.BonusConsume,4),P(B,12)};
                case "gr_devour_value":return new[]{P(EnemyActionType.BonusConsume,4),P(A,15)};
                // Bellbound Herald
                case "gr_first_toll":return LivingAllies(enemyContextIndex,false).Any()?new[]{P(EnemyActionType.BlockAllies,5)}:new[]{P(B,9)};
                case "gr_second_toll":return new[]{P(W,1),P(A,5)};
                case "gr_third_toll":return new[]{P(A,13),P(S,1)};
                // Crownless Duelist
                case "gr_punishing_guard":return new[]{P(B,15),P(A,6)};
                case "gr_relentless_advance":return new[]{P(A,11),P(S,1)};
                case "gr_measured_cut":return new[]{P(A,9),P(B,5)};
                // Crown Collector and Coinbound Guard
                case "gr_collect_due":return new[]{P(EnemyActionType.SeizeGold,8),P(A,10)};
                case "gr_royal_levy":return new[]{P(EnemyActionType.SeizeGold,5),P(EnemyActionType.BlockMinions,8)};
                case "gr_collection_order":return new[]{P(EnemyActionType.Command,1),P(B,8)};
                case "gr_repossess":return new[]{P(EnemyActionType.Summon,1),P(B,10)};
                case "gr_foreclosure":return new[]{P(A,m.held>=10?22:18)};
                case "gr_taxblade":return new[]{P(A,7)};
                case "gr_guard_collector":return new[]{P(EnemyActionType.BlockOwner,8),P(B,4)};
                // Royal Auctioneer
                case "gr_open_bidding":return new[]{P(B,9)};
                case "gr_lot_blades":return new[]{P(A,24),P(S,1)};
                case "gr_lot_protection":return new[]{P(B,25)};
                case "gr_lot_misfortune":return new[]{P(W,2),P(V,1),P(A,5)};
                case "gr_lot_tribute":return new[]{P(EnemyActionType.SeizeGold,10),P(A,10)};
                case "gr_hammer_fall":return new[]{P(A,16)};
                // Living Treasury
                case "gr_vault_slam":return new[]{P(A,14)};
                case "gr_lockdown":return new[]{P(EnemyActionType.ReserveSpend,2),P(B,20)};
                case "gr_asset_release":return new[]{P(A,10+3*Math.Max(0,m.counter)),P(EnemyActionType.ReserveAll,0)};
                case "gr_emergency_reserve":
                {
                    var used=Math.Min(3,Math.Max(0,m.counter));
                    return new[]{P(EnemyActionType.ReserveSpend,used),P(EnemyActionType.Heal,6*used),P(B,10)};
                }
                // The Last Procession
                case "gr_royal_salute":return new[]{P(A,11),P(B,8)};
                case "gr_collect_tribute":return new[]{P(EnemyActionType.SeizeGold,6),P(B,8)};
                case "gr_processional_guard":return new[]{P(B,19)};
                case "gr_golden_fanfare":return new[]{P(S,2)};
                case "gr_gilded_ram":return new[]{P(A,16),P(B,8)};
                case "gr_coin_barrage":return new[]{P(A,4,4)};
                case "gr_forced_march":return new[]{P(A,10),P(S,1)};
                case "gr_seize_streets":return new[]{P(EnemyActionType.SeizeGold,8),P(A,12)};
                case "gr_crown_hammer":return new[]{P(A,18),P(B,10)};
                case "gr_final_tribute":return new[]{P(EnemyActionType.SeizeGold,10),P(A,14)};
                case "gr_royal_furnace":return new[]{P(S,2),P(EnemyActionType.Fortify,1)};
                case "gr_end_procession":return new[]{P(A,5,5)};
            }
            return CathedralActions(move,m);
        }
        private static string RuinsName(string move)=>move switch
        {
            "gr_ragged_slash"=>"RAGGED SLASH","gr_pocket_spoils"=>"POCKET THE SPOILS","gr_desperate_cut"=>"DESPERATE CUT",
            "gr_shield_crush"=>"SHIELD CRUSH","gr_interpose"=>"INTERPOSE","gr_brace"=>"BRACE","gr_hold_line"=>"HOLD THE LINE",
            "gr_march_of_gold"=>"MARCH OF GOLD","gr_royal_refrain"=>"ROYAL REFRAIN","gr_grand_chorus"=>"GRAND CHORUS",
            "gr_appraise_wealth"=>"APPRAISE WEALTH","gr_overvalued"=>"OVERVALUED","gr_worthless"=>"WORTHLESS","gr_marked_asset"=>"MARKED ASSET",
            "gr_awaken_servitor"=>"AWAKEN SERVITOR","gr_relic_slam"=>"RELIC SLAM","gr_repair_rite"=>"REPAIR RITE","gr_keeper_command"=>"COMMAND",
            "gr_servitor_jab"=>"SERVITOR JAB","gr_brace_frame"=>"BRACE FRAME",
            "gr_glittering_bait"=>"GLITTERING BAIT","gr_snap_shut"=>"SNAP SHUT","gr_hoard"=>"HOARD","gr_devour_value"=>"DEVOUR VALUE",
            "gr_first_toll"=>"FIRST TOLL · RALLY","gr_second_toll"=>"SECOND TOLL · DISSONANCE","gr_third_toll"=>"THIRD TOLL · GRAND BELLSTRIKE",
            "gr_punishing_guard"=>"PUNISHING GUARD","gr_relentless_advance"=>"RELENTLESS ADVANCE","gr_measured_cut"=>"MEASURED CUT",
            "gr_collect_due"=>"COLLECT DUE","gr_royal_levy"=>"ROYAL LEVY","gr_collection_order"=>"COLLECTION ORDER","gr_repossess"=>"REPOSSESS","gr_foreclosure"=>"FORECLOSURE",
            "gr_taxblade"=>"TAXBLADE","gr_guard_collector"=>"GUARD THE COLLECTOR",
            "gr_open_bidding"=>"OPEN BIDDING","gr_lot_blades"=>"LOT OF BLADES","gr_lot_protection"=>"LOT OF PROTECTION","gr_lot_misfortune"=>"LOT OF MISFORTUNE","gr_lot_tribute"=>"LOT OF TRIBUTE","gr_hammer_fall"=>"HAMMER FALL",
            "gr_vault_slam"=>"VAULT SLAM","gr_lockdown"=>"LOCKDOWN","gr_asset_release"=>"ASSET RELEASE","gr_emergency_reserve"=>"EMERGENCY RESERVE",
            "gr_royal_salute"=>"ROYAL SALUTE","gr_collect_tribute"=>"COLLECT TRIBUTE","gr_processional_guard"=>"PROCESSIONAL GUARD","gr_golden_fanfare"=>"GOLDEN FANFARE",
            "gr_gilded_ram"=>"GILDED RAM","gr_coin_barrage"=>"COIN BARRAGE","gr_forced_march"=>"FORCED MARCH","gr_seize_streets"=>"SEIZE THE STREETS",
            "gr_crown_hammer"=>"CROWN HAMMER","gr_final_tribute"=>"FINAL TRIBUTE","gr_royal_furnace"=>"ROYAL FURNACE","gr_end_procession"=>"END OF THE PROCESSION",
            _=>"STRIKE"
        };
        private string RuinsMoveLabel(string move,WildMind m)
        {
            switch(move)
            {
                case "gr_desperate_cut":return m.held>0?"DESPERATE CUT · HOLDING GOLD":"DESPERATE CUT";
                case "gr_foreclosure":return m.held>=10?"FORECLOSURE · HOLDING 10+ GOLD":"FORECLOSURE";
                case "gr_punishing_guard":return "PUNISHING GUARD · YOU ATTACKED";
                case "gr_relentless_advance":return "RELENTLESS ADVANCE · YOU PLAYED SKILLS";
                case "gr_open_bidding":return string.IsNullOrEmpty(m.state)?"OPEN BIDDING":"OPEN BIDDING · "+RuinsName(m.state).Replace("LOT OF ","LOT: ")+" NEXT";
                case "gr_asset_release":return m.counter>0?$"ASSET RELEASE · {m.counter} RESERVE":"ASSET RELEASE";
                case "gr_emergency_reserve":return "EMERGENCY RESERVE";
                case "gr_first_toll":return LivingAllies(enemyContextIndex,false).Any()?"FIRST TOLL · RALLY":"FIRST TOLL · RALLY ALONE";
            }
            return move.StartsWith("bc_")?CathedralLabel(move,m):move.StartsWith("fr_")?FractureLabel(move,m):move.StartsWith("gt_")?ThroneLabel(move,m):move.StartsWith("nt_")?NeutralLabel(move,m):RuinsName(move);
        }
        private static string RuinsHook(string move)=>move switch
        {
            "gr_march_of_gold" or "gr_royal_refrain" or "gr_grand_chorus"=>"chorister_support",
            "gr_first_toll"=>"bell_toll_1","gr_second_toll"=>"bell_toll_2","gr_third_toll"=>"bell_toll_3",
            "gr_awaken_servitor"=>"servitor_awaken","gr_keeper_command" or "gr_collection_order"=>"ruins_command",
            "gr_repossess"=>"collector_repossess","gr_foreclosure"=>"collector_foreclosure",
            "gr_asset_release"=>"asset_release","gr_emergency_reserve"=>"emergency_reserve",
            "gr_end_procession"=>"end_of_the_procession",
            _=>CathedralHook(move)
        };

        // ---------- resolution ----------
        // Returns true when the enemy keeps its own pattern position; false lets the engine advance the pattern one step.
        private bool BeginRuinsAction(WildMind m,string move)
        {
            switch(enemyId)
            {
                case GildedRuinsContent.Appraiser:
                    if(move=="gr_appraise_wealth")m.queue=playerGold>=AppraiserGoldThreshold?"gr_overvalued":"gr_worthless";
                    else if(move is "gr_overvalued" or "gr_worthless")m.queue="";
                    return false;
                case GildedRuinsContent.Duelist:
                    EmitHook("duelist_response:"+(move=="gr_punishing_guard"?"ATTACKS":move=="gr_relentless_advance"?"SKILLS":"MIXED"));
                    return false;
                case GildedRuinsContent.Collector:
                    if(move=="gr_repossess")m.flag=true; // the one replacement is spent
                    return false;
                case GildedRuinsContent.Auctioneer:
                    if(move=="gr_open_bidding")EmitHook("auction_lot_announced");
                    else if(move.StartsWith("gr_lot_")){m.lastSeed=m.state;m.state="";EmitHook("auction_lot_resolved");}
                    return false;
                case GildedRuinsContent.Treasury:
                    if(move=="gr_emergency_reserve"){m.flag=true;return true;} // an override: the pattern keeps its place
                    return false;
                default:return BeginCathedralAction(m,move);
            }
        }
        private void ExecuteRuinsAction(PlannedEnemyAction action)
        {
            var self=enemyContextIndex;var m=Mind;
            switch(action.type)
            {
                case EnemyActionType.SeizeGold:SeizeGold(self,action.amount);break;
                case EnemyActionType.BonusConsume:
                {
                    var take=Math.Min(action.amount,Math.Max(0,m.counter));if(take<=0)break;
                    m.counter-=take;Emit(CombatEventKind.Status,-take,false,null,"BONUS GOLD");EmitHook("bonus_gold_consumed");break;
                }
                case EnemyActionType.Fortify:enemy.fortify+=action.amount;Emit(CombatEventKind.Status,action.amount,false,null,"FORTIFY");break;
                case EnemyActionType.ReserveSpend:
                {
                    var take=Math.Min(action.amount,Math.Max(0,m.counter));if(take<=0)break;
                    m.counter-=take;Emit(CombatEventKind.Status,-take,false,null,"RESERVE");break;
                }
                case EnemyActionType.ReserveAll:
                    if(m.counter>0){Emit(CombatEventKind.Status,-m.counter,false,null,"RESERVE");m.counter=0;}break;
                case EnemyActionType.BlockLowestNonMinion:
                {
                    var allies=LivingAllies(self,true).ToArray();if(allies.Length==0)break;
                    var low=allies.Min(i=>EnemyAt(i).hp);var tied=allies.Where(i=>EnemyAt(i).hp==low).ToArray();
                    var target=tied[tied.Length==1?0:NextRandom(tied.Length)];
                    InEnemyContext(target,()=>{enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);});break;
                }
                default:ExecuteCathedralAction(action);break;
            }
        }

        // ---------- deaths and thresholds ----------
        // Moves whose legal targets or values depend on living allies / Minions are re-read while the player can see it.
        private bool RuinsReplanOnDeath(int k)
        {
            var id=opponents[k].id;
            return id is GildedRuinsContent.Keeper or GildedRuinsContent.Collector or GildedRuinsContent.Herald or GildedRuinsContent.Bastion||CathedralReplanOnDeath(k);
        }
        // The Last Procession: 2/3 of maximum health rounded up (140 at the base 210) and 1/3 rounded down (70).
        private bool CheckRuinsThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            if(GildedRuinsContent.Find(o.id)==null)return CheckCathedralThresholds(index);
            if(o.id==GildedRuinsContent.Treasury)
            {
                // Emergency Reserve takes priority the moment it becomes legal, so the player sees the intent change.
                if(PlayerPhaseReplan&&m.planned!="gr_emergency_reserve"&&!m.flag&&m.counter>0&&f.hp*100<35*f.maxHp)ReplanAt(index);
                return true;
            }
            if(o.id!=GildedRuinsContent.Procession)return true;
            var t1=(f.maxHp*2+2)/3;var t2=f.maxHp/3;
            var target=f.hp<=t2?3:f.hp<=t1?2:1;
            while(m.phase<target)
            {
                // The Procession changes form: no heal, Strength, Fortify or Block. Gold it already holds stays held.
                m.phase++;m.step=0;m.hold=false;var phase=m.phase;
                InEnemyContext(index,()=>EmitHook(phase==2?"last_procession_phase_1_to_2":"last_procession_phase_2_to_3"));
                bossPhase=m.phase;memory.resolvedBossPhase=m.phase;
                if(PlayerPhaseReplan)ReplanAt(index);
            }
            return true;
        }

        // ---------- presentation ----------
        private static string ProcessionPhaseName(int phase)=>phase<=1?"THE CEREMONY":phase==2?"THE BROKEN PARADE":"THE CROWN ENGINE";
        private bool RuinsCounter(int index,WildMind m,out string label,out int value,out int max)
        {
            label="";value=max=0;
            switch(EnemyIdAt(index))
            {
                case GildedRuinsContent.Mimic:label="BONUS GOLD "+m.counter;value=m.counter;return true;
                case GildedRuinsContent.Treasury:label="RESERVE";value=m.counter;max=MaxReserve;return true;
                case GildedRuinsContent.Duelist:
                    label=hadPreviousTurn?$"LAST TURN: {lastTurnAttackCards} ATTACK · {lastTurnOtherCards} OTHER":"NO PREVIOUS TURN";return true;
                case GildedRuinsContent.Procession:label=$"PHASE {m.phase} · {ProcessionPhaseName(m.phase)}";return true;
            }
            return CathedralCounter(index,m,out label,out value,out max);
        }
        public bool RuinsForecast(int index,WildMind m,out string title,out List<string> lines,out string tip)
        {
            title="";tip="";lines=new List<string>();
            if(EnemyIdAt(index)==GildedRuinsContent.Auctioneer&&!string.IsNullOrEmpty(m.state))
            {
                title="UPCOMING LOT";lines.Add(RuinsName(m.state)+" · "+RuinsSummary(m.state,m));
                tip="Announced at Open Bidding. It resolves after Open Bidding and cannot be canceled, only prepared for. It never changes.";return true;
            }
            return FractureForecast(index,m,out title,out lines,out tip);
        }
        private string RuinsSummary(string move,WildMind m)
        {
            var parts=new List<string>();
            foreach(var a in RuinsActions(move,m)??Array.Empty<PlannedEnemyAction>())
                switch(a.type)
                {
                    case EnemyActionType.Attack:parts.Add(a.hits>1?$"{a.amount}×{a.hits}":a.amount+" dmg");break;
                    case EnemyActionType.Block:parts.Add(a.amount+" Block");break;
                    case EnemyActionType.Strength:parts.Add("+"+a.amount+" Str");break;
                    case EnemyActionType.Weak:parts.Add(a.amount+" Weak");break;
                    case EnemyActionType.Vulnerable:parts.Add(a.amount+" Vuln");break;
                    case EnemyActionType.SeizeGold:parts.Add("Seize "+a.amount);break;
                }
            return string.Join(" + ",parts);
        }
        private void RuinsStateText(int index,WildMind m,List<string> lines)
        {
            CathedralStateText(index,m,lines);
            switch(EnemyIdAt(index))
            {
                case GildedRuinsContent.Scavenger:lines.Add("Pocket the Spoils Seizes Gold. Desperate Cut deals 13 instead of 11 while it holds Seized Gold.");break;
                case GildedRuinsContent.Bastion:lines.Add(m.state=="SOLO"?"Fighting alone: Brace → Shield Crush → Shield Crush.":"Interpose → Shield Crush → Hold the Line. Shields its lowest-health non-Minion ally.");break;
                case GildedRuinsContent.Chorister:lines.Add("SUPPORT · Never attacks. Sings Strength and Block into its allies. Leaves if only harmless Supports remain.");break;
                case GildedRuinsContent.Appraiser:lines.Add($"APPRAISAL · Reads your current Gold when Appraise Wealth resolves. {AppraiserGoldThreshold}+ Gold: Overvalued (Seizes 6). Otherwise: Worthless (6 damage + Weak).");break;
                case GildedRuinsContent.Keeper:lines.Add($"SUMMONER · {OwnedMinions(index).Count()}/2 Gilded Servitors. Awakens, mends and commands them.");break;
                case GildedRuinsContent.Mimic:lines.Add($"BONUS GOLD · {m.counter}. This is not your Gold. It consumes Bonus Gold with Hoard and Devour Value; what remains when it dies is added to the reward.");break;
                case GildedRuinsContent.Herald:lines.Add("Three Tolls: Rally → Dissonance → Grand Bellstrike. Then the cycle repeats.");break;
                case GildedRuinsContent.Duelist:
                    lines.Add("Answers the cards you played last turn. 3+ cards, mostly Attacks: Punishing Guard. 3+ cards, mostly non-Attacks: Relentless Advance. Otherwise: Measured Cut.");
                    lines.Add(hadPreviousTurn?$"Last turn you played {lastTurnAttackCards} Attack and {lastTurnOtherCards} other cards.":"No previous turn: Measured Cut.");break;
                case GildedRuinsContent.Collector:lines.Add($"COLLECTOR · {OwnedMinions(index).Count()}/2 Coinbound Guards. Can replace one fallen Guard once ({(m.flag?"used":"available")}). Foreclosure deals 22 instead of 18 while it holds 10+ Seized Gold. Bonus reward: +10 Gold.");break;
                case GildedRuinsContent.Auctioneer:lines.Add("LOTS · Open Bidding announces the next Lot (never the same Lot twice in a row), then the Lot resolves, then Hammer Fall. A Lot cannot be canceled by damage.");break;
                case GildedRuinsContent.Treasury:lines.Add($"RESERVE · {m.counter}/{MaxReserve}. Each Attack card that deals unblocked damage to it adds 1 Reserve (once per card). Lockdown spends 2. Asset Release spends all: 10 + 3 per Reserve."+(m.flag?" Emergency Reserve: used.":" Below 35% health it may use Emergency Reserve once (heal 6 per Reserve spent, up to 3)."));break;
                case GildedRuinsContent.Procession:lines.Add($"PHASE {m.phase} · {ProcessionPhaseName(m.phase)} — changes form at 2/3 and 1/3 health. No heal, Strength, Fortify or Block from changing form. Gold it has Seized stays held until it dies.");break;
                case GildedRuinsContent.Servitor:lines.Add("Servitor Jab → Brace Frame.");break;
                case GildedRuinsContent.Guard:lines.Add("Taxblade → Guard the Collector.");break;
            }
        }
        private void RuinsHeldText(WildMind m,List<string> lines)
        {
            if(m.held>0)lines.Add($"SEIZED GOLD · Holds {m.held} of your Gold. Defeating it returns all of it to you.");
        }
        private string DescribeRuinsAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            var m=Mind;
            switch(type)
            {
                case EnemyActionType.SeizeGold:
                {
                    var take=Math.Min(amount,Math.Max(0,playerGold));a.amount=take;a.title="SEIZE GOLD";
                    return take<amount?$"Seize {take} Gold (you only have {Math.Max(0,playerGold)}). It holds your Gold. Defeating it returns all of it."
                        :$"Seize {take} of your Gold. It holds your Gold. Defeating it returns all of it.";
                }
                case EnemyActionType.BonusConsume:
                {
                    var have=m?.counter??0;var take=Math.Min(amount,Math.Max(0,have));a.amount=take;a.title="CONSUME BONUS GOLD";
                    return $"Consumes {take} of its Bonus Gold ({have} left). Bonus Gold is not yours; it is extra reward you lose if it is consumed.";
                }
                case EnemyActionType.Fortify:a.title="FORTIFY";return $"Gains {amount} Fortify. Fortify adds to every Block it gains from its own actions.";
                case EnemyActionType.ReserveSpend:a.title="SPEND RESERVE";return $"Spends {amount} Reserve ({m?.counter??0} held).";
                case EnemyActionType.ReserveAll:a.amount=m?.counter??0;a.title="RELEASE RESERVE";return $"Spends all {a.amount} Reserve. Reserve returns to 0.";
                case EnemyActionType.BlockLowestNonMinion:a.title="INTERPOSE";return $"Its lowest-health non-Minion ally gains {amount} Block. Ties are broken at random.";
            }
            return DescribeCathedralAction(type,amount,a);
        }
    }
}
