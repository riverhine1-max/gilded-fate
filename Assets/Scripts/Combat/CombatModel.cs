using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    public enum SigilKind { Ember, Hex, Echo, Ruin, Wither, Grave, Mirror }
    [Serializable] public sealed class FighterState
    {
        public int hp,maxHp,block,strength,fortify,burn,marked,weak,vulnerable,frail,attemptedAttackDamage;
        public List<CombatEffectState> effects=new();
        public FighterState Copy(){var copy=(FighterState)MemberwiseClone();copy.effects=(effects??new()).Select(e=>e.Copy()).ToList();return copy;}
    }

    [Serializable]
    public sealed class CombatMemory
    {
        public RemainingCardMemory remaining=new();
        public RelicCombatMemory relicExpansion=new();
        public int gildedWarlord,warlordReadyTurn,unmovable,conquestCadence,conquestAttacksThisTurn,brandOfRuin,extraSigilSlots;
        public int skillsThisTurn,attacksThisTurn,powersInPlay,blockGainedThisTurn,blockGainsThisTurn,nextTurnEnergyPenalty,temporaryStrength,resolvedBossPhase=1,lastCardValue,livingArmor;
        public int unblockedDamageAttempted,markedConsumed,retainedEnergy,nextTurnDrawPenalty,nextCardCostPenalty,nextAttackCostReduction,nextSkillCostReduction,nextAttackBonus,nextAttackPenalty;
        public int battleTemper,ironBlood,warMachine,warMachineUses,patientWarrior,retributionPower,holdLinePower,relentless,onslaughtThreshold,indomitable;
        public int sigilMastery,grandConvergence,ashes,deathsGaze,embraceVoid,damnationStrength,battleRhythm,reserveEnergy,tacticalAdvantage,resourceful,overflowPower,chainReaction,againstAllOddsDraw,perfectForm;
        public int sigilActivations,echoArmed,retaliateTriggers,retaliateTriggeredTurn,burnTriggersThisTurn,debuffApplicationsThisTurn,exhaustsThisTurn,extraDrawDamage,cycleMask,cycleCompletesThisTurn,makeNextDrawFree,retainBlock;
        public int nextHeavyCostReduction,temporaryFortify,grimAscensionBonus;
        public int soulsPlayedThisTurn,soulsPlayedCombat,soulsDrawn,soulsSacrificed,cardsDrawnThisTurn,nextSoulDamageBonus,nextSoulReplay,soulDamageBonus,deathIncarnate;
        public int deathsEmbrace,deathMarchCadence,gravePactBonus,gravekeeper,endlessHarvest,grimAscensionCadence,soulboundTome,eternalSouls,reapersCalling;
        public bool firstAttackPlayed,firstSkillPlayed,firstBlockPlayed,lastCardWasAttack,previousWasSkill,previousWasPower,wasAttackedLastTurn,attackedThisEnemyTurn,crownSacrificeDebt;
        public bool firstStrengthRelicUsed,firstResonanceRelicUsed,firstBlockRelicUsed,firstDebuffRelicUsed,statusRelicUsed,curseExhaustRelicUsed,zeroCostRelicUsed,selfDamageRelicUsed;
        public bool spiritStrengthTriggered,spiritFortifyTriggered,onslaughtUsed,crownedBulwarkUsed,sigilMasteryUsed,deathsGazeUsed,indomitableUsed,immortalThreadUsed,gravekeeperUsed,reapersCallingUsed;
        public uint randomState=2463534242u;
        public List<int> specialPlayIds=new(),lingeringIds=new(),fatefulIds=new(),fatefulCounts=new(),freeThisTurnIds=new(),temporaryDamageIds=new(),temporaryDamageValues=new();
        public CombatMemory Copy(){var copy=(CombatMemory)MemberwiseClone();copy.remaining=(remaining??new()).Copy();copy.relicExpansion=(relicExpansion??new()).Copy();copy.specialPlayIds=new List<int>(specialPlayIds??new());copy.lingeringIds=new List<int>(lingeringIds??new());copy.fatefulIds=new List<int>(fatefulIds??new());copy.fatefulCounts=new List<int>(fatefulCounts??new());copy.freeThisTurnIds=new List<int>(freeThisTurnIds??new());copy.temporaryDamageIds=new List<int>(temporaryDamageIds??new());copy.temporaryDamageValues=new List<int>(temporaryDamageValues??new());return copy;}
    }

    [Serializable]
    public sealed partial class CombatState
    {
        public FighterState player=new(),enemy=new();
        public int energy,resonance,maxResonance=5,turn,cardsPlayed,difficulty,bossPhase=1,goldLost,stolenGold,highestDamage,highestBlock;
        public int intentHits=1,spectralWeapons,vaultWards,cursePressure,banishedCards,nextTurnBlock,livingArmor,retaliation,counterDamage,enemyBaseDamage,bonusCardRewards;
        public HeroId hero;public string enemyId,intentLabel="UNKNOWN",mechanicTitle="ENCOUNTER",mechanicText="Watch the intent, then commit your hand.";
        public string activeShardId="";public bool activeShardFractured;
        public IntentKind intent;public int intentValue;public CombatPhase phase;public int nextInstanceId;
        public List<CardDef> draw=new(),hand=new(),discard=new(),exhaust=new(),activeAspects=new();public List<string> relics=new();public List<SigilKind> sigils=new();
        public CombatMemory memory=new();public PendingCardPlay pendingPlay;
        [NonSerialized] public List<CombatEvent> events=new();

        public bool IsOver=>player.hp<=0||!AnyEnemyAlive;
        public bool AwaitingChoice=>pendingPlay!=null&&pendingPlay.choice!=CardChoiceKind.None;
        public CardChoiceKind ChoiceKind=>pendingPlay?.choice??CardChoiceKind.None;
        public IReadOnlyList<CardDef> ChoiceCards=>ChoiceKind switch{CardChoiceKind.ExpansionCard=>RemainingChoiceCards,CardChoiceKind.DiscardFromHand=>hand,CardChoiceKind.ReturnFromDiscard=>discard,CardChoiceKind.ExhaustCurseFromHand=>hand.Where(c=>c.origin==CardOrigin.Curse).ToArray(),CardChoiceKind.ExhaustSoulFromHand=>hand.Where(IsSoul).ToArray(),CardChoiceKind.ReturnAttackFromExhaust=>exhaust.Where(c=>c.kind==CardKind.Attack).ToArray(),CardChoiceKind.ReturnSkillFromExhaust=>exhaust.Where(c=>c.kind==CardKind.Skill).ToArray(),CardChoiceKind.ExhaustFromHand=>hand.Where(c=>c!=pendingPlay?.card).ToArray(),_=>Array.Empty<CardDef>()};
        public IReadOnlyList<string> ChoiceOptions=>ChoiceKind switch{CardChoiceKind.ExpansionOption=>RemainingChoiceOptions,CardChoiceKind.AdaptMode=>new[]{"BLOCK","DAMAGE"},CardChoiceKind.SigilMode=>new[]{"EMBER SIGIL","HEX SIGIL","ECHO SIGIL"},CardChoiceKind.SigilSlot=>sigils.Select((s,i)=>$"{i+1} · {s.ToString().ToUpperInvariant()} SIGIL").ToArray(),CardChoiceKind.BuffToDouble=>BuffOptions(),CardChoiceKind.BuffToGain=>BuffOptions(),_=>Array.Empty<string>()};
        public bool ImmortalThreadUsed=>memory.immortalThreadUsed;
        public bool IsAttack(CardDef card)=>card!=null&&card.kind==CardKind.Attack;
        public bool WillGild(CardDef card)=>false;
        public bool RequiresEnemyTarget(CardDef card)=>RemainingTargets(card)||card!=null&&!AffectsAllEnemies(card)&&!AffectsRandomEnemy(card)&&(card.id is "sigil_of_malice" or "hexed_reverberation" or "turn_their_strength" or "eye_for_an_eye"||IsSoul(card)||IsAttack(card)||card.effect is EffectKind.Burn or EffectKind.Mark or EffectKind.Weak or EffectKind.Vulnerable);

        public bool IntentDealsDamage=>intent==IntentKind.Attack||intent==IntentKind.Special&&enemyId!="hollow_king"&&enemyId!="vault_mother"&&(enemyId!="mirror_witch"||memory.lastCardWasAttack);
        public int IntentDisplayValue{get{if(!IntentDealsDamage)return intentValue;var value=(enemyId=="mirror_witch"&&intent==IntentKind.Special?Math.Max(intentValue,memory.lastCardValue):intentValue)+enemy.strength;if(enemy.weak>0)value=value*3/4;if(player.vulnerable>0)value=value*3/2;return value;}}
        public string IntentSummary=>intentHits>1?$"{intentLabel}  {IntentDisplayValue} × {intentHits}":$"{intentLabel}  {IntentDisplayValue}";
        public string IntentDetail=>intent switch{IntentKind.Attack=>intentHits>1?$"Incoming: {IntentDisplayValue} damage, {intentHits} times.":$"Incoming: {IntentDisplayValue} damage.",IntentKind.Defend=>$"Next action: gain {IntentDisplayValue} Block.",IntentKind.Buff=>$"Next action: gain {IntentDisplayValue} Strength.",IntentKind.Debuff=>enemyId=="vault_spider"?$"Next action: apply {IntentDisplayValue} Weak.":$"Next action: apply {IntentDisplayValue} Vulnerable.",_=>SpecialIntentDetail()};

        public int CostFor(CardDef card)
        {
            if(card==null)return 99;var cost=RelicCost(card,RemainingCost(card,card.cost));
            if(memory.freeThisTurnIds.Contains(card.instanceId))cost=0;
            if(card.firstDrawFree&&card.instanceId>0&&!memory.specialPlayIds.Contains(-card.instanceId))cost=0;
            if(card.specialModification=="quickened"&&card.instanceId>0&&!memory.specialPlayIds.Contains(-card.instanceId))cost--;
            if(card.kind==CardKind.Attack){cost-=memory.nextAttackCostReduction;if(card.keywords?.Contains("Heavy")==true)cost-=memory.nextHeavyCostReduction;if(memory.perfectForm>0&&!memory.firstAttackPlayed)cost--;if(card.id=="grand_finale"&&memory.attacksThisTurn>=3)cost--;if(hand.Any(c=>c.id=="heavy_chains")&&!memory.firstAttackPlayed)cost++;}
            if(card.kind==CardKind.Skill){cost-=memory.nextSkillCostReduction;if(memory.perfectForm>0&&!memory.firstSkillPlayed)cost--;}
            if(card.id=="vengeful_rush"&&(memory.wasAttackedLastTurn||memory.attackedThisEnemyTurn))cost=0;
            if(memory.nextCardCostPenalty>0)cost+=memory.nextCardCostPenalty;
            if(relics.Contains("crown_of_sacrifice")&&cardsPlayed==0)cost=0;
            cost=ShardCost(card,cost);
            if(hand.Any(c=>c.id=="fatebound"))cost=Math.Max(card.cost,cost);
            return Math.Max(0,cost);
        }
        public bool CanPlay(CardDef card)=>pendingPlay==null&&!IsOver&&phase==CombatPhase.Player&&card!=null&&hand.Contains(card)&&!card.unplayable&&energy>=CostFor(card);
        public CombatEvent[] TakeEvents(){events??=new();var result=events.ToArray();events.Clear();return result;}
        private int nextFeedbackHitId;
        [NonSerialized] private int feedbackSigilSlot=-1;
        private void Emit(CombatEventKind kind,int amount=0,bool playerSide=false,CardDef card=null,string label="",int hitId=0)
        {
            events??=new();var fact=new CombatEvent(kind,amount,playerSide,card,label){hitId=hitId,enemyIndex=EnemyContextIndex,sigilSlot=feedbackSigilSlot};
            if(card!=null)
            {
                fact.destination=hand.Contains(card)?CombatCardDestination.Hand:draw.Contains(card)?CombatCardDestination.Draw:discard.Contains(card)?CombatCardDestination.Discard:exhaust.Contains(card)?CombatCardDestination.Exhaust:CombatCardDestination.None;
                fact.generatedCard=((kind is CombatEventKind.Draw or CombatEventKind.Shuffle)&&(label is "SOUL" or "RECURRING"))||(kind==CombatEventKind.Status&&(label is "CURSE" or "STATUS" or "OPENING STATUS"))||(kind==CombatEventKind.Discard&&label=="HAND FULL"&&IsSoul(card));
                fact.handIndex=hand.IndexOf(card);fact.handCount=hand.Count;
            };
            if(kind is CombatEventKind.Status or CombatEventKind.PlayerTurn or CombatEventKind.StateSnapshot or CombatEventKind.CardResolved)
            {
                fact.playerStatuses=player.Copy();fact.playerRetaliation=retaliation;
                if(hero==HeroId.Hexer)fact.sigils=sigils.ToArray();
                fact.spiritDirectionsUsed=(memory.spiritStrengthTriggered?1:0)|(memory.spiritFortifyTriggered?2:0);
                fact.enemyStatuses=enemy.Copy();
            }
            if(kind is CombatEventKind.Damage or CombatEventKind.Block or CombatEventKind.Heal)
            {fact.hasVitals=true;fact.playerHp=player.hp;fact.enemyHp=enemy.hp;fact.playerBlock=player.block;fact.enemyBlock=enemy.block;}
            events.Add(fact);
        }
        private CardDef Instance(CardDef definition){var card=definition.Copy();card.instanceId=++nextInstanceId;return card;}

        public void Begin(HeroId hero,IEnumerable<CardDef> deck,int enemyHp=48,int encounterDifficulty=0,int currentHp=-1,int currentMaxHp=-1,IEnumerable<string> equippedRelics=null,string archetype="vault_rat",int baseDamage=8,int seed=0,bool immortalThreadAlreadyUsed=false,string shardId="",bool fracturedShard=false,string[] encounterEnemyIds=null,int openingTemporaryCards=0)
        {
            activeAspects=new();
            player=new FighterState();enemy=new FighterState();memory=new CombatMemory();shardMemory=new ShardCombatMemory();pendingPlay=null;resolvingEffects=null;sigils.Clear();
            memory.randomState=seed==0?2463534242u:unchecked((uint)seed);memory.immortalThreadUsed=immortalThreadAlreadyUsed;draw.Clear();hand.Clear();discard.Clear();exhaust.Clear();relics.Clear();events=new();nextInstanceId=0;
            energy=resonance=turn=cardsPlayed=goldLost=stolenGold=highestDamage=highestBlock=nextTurnBlock=livingArmor=retaliation=counterDamage=spectralWeapons=vaultWards=cursePressure=banishedCards=bonusCardRewards=0;maxResonance=5;bossPhase=intentHits=1;phase=CombatPhase.Player;
            this.hero=hero;difficulty=encounterDifficulty;enemyId=archetype;enemyBaseDamage=baseDamage;activeShardId=shardId??"";activeShardFractured=fracturedShard;
            player.maxHp=currentMaxHp>0?currentMaxHp:(hero==HeroId.Vanguard?80:hero==HeroId.Hexer?68:72);player.hp=currentHp>=0?Math.Min(currentHp,player.maxHp):player.maxHp;enemy.maxHp=enemy.hp=enemyHp;InitializeOpponentGroup(encounterEnemyIds);
            if(equippedRelics!=null)relics.AddRange(equippedRelics);
            foreach(var card in deck)draw.Add(Instance(card));
            foreach(var added in draw.Skip(Math.Max(0,draw.Count-openingTemporaryCards)))Emit(CombatEventKind.Status,1,true,added,"OPENING STATUS");
            if(relics.Contains("gilded_heart"))
            {
                RelicPresentationPulse("gilded_heart");
                for(var i=0;i<2;i++){var added=Instance(GameContent.Find("dazed_mind"));draw.Add(added);Emit(CombatEventKind.Status,1,true,added,"OPENING STATUS");}
            }
            if(relics.Contains("book_of_hollow_names")){if(draw.Any(c=>c.origin==CardOrigin.Curse))RelicPresentationPulse("book_of_hollow_names");resonance=Math.Min(5,draw.Count(c=>c.origin==CardOrigin.Curse));}
            if(relics.Contains("tarnished_eye")){RelicPresentationPulse("tarnished_eye");ForEachLivingEnemy(()=>enemy.weak=1);}
            
            R.startingIds=draw.Select(c=>c.instanceId).ToList();InitRelicExpansion();Shuffle(draw);if(openingTemporaryCards>0||relics.Contains("gilded_heart"))Emit(CombatEventKind.Shuffle,draw.Count,true,null,"OPENING");NextTurn();ShardStartup(false);
            if(relics.Contains("travelers_candle")){RelicPresentationPulse("travelers_candle");Draw(1,false);}
            if(relics.Contains("fate_spinner")&&draw.Count>0){RelicPresentationPulse("fate_spinner");draw[NextRandom(draw.Count)].firstDrawFree=true;}
        }

        public void NextTurn()
        {
            if(pendingPlay!=null||turn>0&&phase!=CombatPhase.EnemyResolved)return;if(IsOver){phase=CombatPhase.Finished;return;}
            R.turnStartCards=cardsPlayed;
            phase=CombatPhase.Player;turn++;ResetRelicTurn();ResetShardTurn();memory.conquestAttacksThisTurn=0;if(memory.unmovable>0&&player.block>0)ExpansionPowerPulse("unmovable");if(relics.Contains("immortal_plate")&&memory.unmovable==0&&player.block>0&&memory.retainBlock<8)RelicPresentationPulse("immortal_plate");var retained=BlockRetention();player.block=Math.Min(retained,player.block);memory.retainBlock=0;memory.freeThisTurnIds.Clear();ExpireEffects(CombatEffectDuration.NextTurn);memory.blockGainedThisTurn=memory.blockGainsThisTurn=memory.skillsThisTurn=memory.attacksThisTurn=memory.soulsPlayedThisTurn=memory.cardsDrawnThisTurn=0;memory.firstAttackPlayed=memory.firstSkillPlayed=memory.firstBlockPlayed=false;memory.spiritStrengthTriggered=memory.spiritFortifyTriggered=memory.onslaughtUsed=memory.crownedBulwarkUsed=memory.deathsGazeUsed=memory.indomitableUsed=memory.gravekeeperUsed=memory.reapersCallingUsed=false;memory.retaliateTriggeredTurn=memory.burnTriggersThisTurn=memory.debuffApplicationsThisTurn=memory.exhaustsThisTurn=memory.warMachineUses=memory.cycleMask=memory.cycleCompletesThisTurn=0;memory.attackedThisEnemyTurn=false;memory.crownSacrificeDebt=false;
            var opening=nextTurnBlock;nextTurnBlock=0;if(memory.livingArmor>0){if(player.strength>0)PresentationPulse("living_armor");opening+=player.strength;}if(opening>0)GainBlock(opening);
            if(relics.Contains("gilded_heart"))RelicPresentationPulse("gilded_heart");energy=3+(relics.Contains("gilded_heart")?1:0)+memory.retainedEnergy-memory.nextTurnEnergyPenalty;memory.retainedEnergy=memory.nextTurnEnergyPenalty=0;
            var drawCount=5-memory.nextTurnDrawPenalty;memory.nextTurnDrawPenalty=0;if(relics.Contains("stolen_hourglass")){RelicPresentationPulse("stolen_hourglass");drawCount+=2;}if(relics.Contains("threads_of_fate")&&turn%3==0){RelicPresentationPulse("threads_of_fate");drawCount+=2;energy++;}drawCount+=ShardOpeningDraw();
            if(memory.againstAllOddsDraw>=0&&memory.againstAllOddsDraw!=0&&player.hp*2<player.maxHp){PresentationPulse("against_all_odds");energy++;drawCount+=memory.againstAllOddsDraw;}
            StartRemainingTurn();RelicOpeningResources(ref drawCount);ReturnLingeringCards();Emit(CombatEventKind.PlayerTurn,turn,true);Emit(CombatEventKind.Energy,energy,true);Draw(Math.Max(0,drawCount),true);RelicOpeningHand();PlanAllEnemyIntents();
        }

        public bool Play(CardDef card)
        {
            if(!CanPlay(card))return false;var paid=CostFor(card);if(memory.perfectForm>0&&card.cost>0&&(card.kind==CardKind.Attack&&!memory.firstAttackPlayed||card.kind==CardKind.Skill&&!memory.firstSkillPlayed))PresentationPulse("perfect_form");if(relics.Contains("crown_of_sacrifice")&&cardsPlayed==0&&card.cost>0)RelicPresentationPulse("crown_of_sacrifice");if(relics.Contains("crown_of_sacrifice")&&cardsPlayed==0&&card.cost>=2)memory.crownSacrificeDebt=true;energy-=paid;Emit(CombatEventKind.Energy,energy,true);hand.Remove(card);
            if(card.kind==CardKind.Attack){memory.nextAttackCostReduction=0;if(card.keywords?.Contains("Heavy")==true)memory.nextHeavyCostReduction=0;}if(card.kind==CardKind.Skill)memory.nextSkillCostReduction=0;memory.nextCardCostPenalty=0;
            if(card.firstDrawFree||card.specialModification=="quickened")memory.specialPlayIds.Add(-card.instanceId);memory.freeThisTurnIds.Remove(card.instanceId);
            var repeats=1;var echoCadence=sigils.Contains(SigilKind.Echo)?4:0;if(memory.echoArmed>0){repeats++;memory.echoArmed--;}else if(echoCadence>0&&(cardsPlayed+1)%echoCadence==0){PassiveSigilPresentationPulse(SigilKind.Echo);repeats++;GainResonance(1);Emit(CombatEventKind.Status,1,true,null,"ECHO SIGIL");}
            
            if(card.specialModification=="golden_echo"&&!memory.specialPlayIds.Contains(card.instanceId)){repeats++;memory.specialPlayIds.Add(card.instanceId);}
            if(card.specialModification=="fateful"&&IncrementFateful(card.instanceId)%3==0)repeats++;
            if(card.kind==CardKind.Attack){memory.conquestAttacksThisTurn++;if(memory.conquestCadence>0&&memory.conquestAttacksThisTurn%memory.conquestCadence==0){repeats++;ExpansionPowerPulse("relentless_conquest");}}
            pendingPlay=new PendingCardPlay{card=card,copiesRemaining=repeats,gilded=false,relicPaidCost=paid};BeforeRelicPlay(card);BeforeRemainingPlay(card);var assault=EffectValue(player,"unrelenting_assault");if(card.kind==CardKind.Attack&&assault>0)TriggerEffect("unrelenting_assault",()=>{ConsumeEffect(player,"unrelenting_assault");pendingPlay.attackEffectBonus+=assault;});pendingPlay.copiesRemaining+=ShardBeginPlay(card,pendingPlay);ContinueCardResolution();return true;
        }

        private void ContinueCardResolution()
        {
            var play=pendingPlay;if(play==null)return;var card=play.card;
            while(play.copiesRemaining>0)
            {
                if(play.resolvedCopies>0&&(IsOver||RequiresEnemyTarget(card)&&enemy.hp<=0))break;if(!play.effectStarted){play.effectStarted=true;play.shardBlockTotal=0;play.shardBastionTriggered=false;play.relentlessPulseUsed=false;ResolveCardTargets(card);}if(AwaitingChoice&&!IsOver)return;play.choice=CardChoiceKind.None;if(play.relicEcho&&!IsOver){play.relicEcho=false;play.relicPrimaryRepeat=true;play.remaining=new RemainingCardChoice();ConsumeEffect(player,"echoed");play.effectStarted=false;continue;}
                play.relicPrimaryRepeat=false;cardsPlayed++;play.resolvedCopies++;Emit(CombatEventKind.CardResolved,play.resolvedCopies,true,card);
                if(card.kind==CardKind.Attack){ApplyAttackCountReceipt(play);}else if(card.kind==CardKind.Skill){memory.skillsThisTurn++;memory.firstSkillPlayed=true;}else if(card.kind==CardKind.Power)memory.powersInPlay++;
                TriggerAfterCard(card);memory.previousWasSkill=card.kind==CardKind.Skill;memory.previousWasPower=card.kind==CardKind.Power;memory.lastCardWasAttack=card.kind==CardKind.Attack;memory.lastCardValue=Math.Max(1,card.value);play.copiesRemaining--;play.effectStarted=false;
            }
            ShardFinishPlay(card,play);AfterRelicPlay(card,play);MoveResolvedCard(card);pendingPlay=null;if(IsOver)phase=CombatPhase.Finished;
        }

        public bool ChooseCard(CardDef selected)
        {
            if(!AwaitingChoice||selected==null||IsOver||!ChoiceCards.Contains(selected))return false;if(ChoiceKind==CardChoiceKind.ExpansionCard)return ChooseRemainingCard(selected);var choice=pendingPlay.choice;
            if(choice==CardChoiceKind.DiscardFromHand){hand.Remove(selected);discard.Add(selected);Emit(CombatEventKind.Discard,1,true,selected);}
            else if(choice==CardChoiceKind.ReturnFromDiscard){discard.Remove(selected);hand.Add(selected);Emit(CombatEventKind.Draw,1,true,selected,"DISCARD");}
            else if(choice==CardChoiceKind.ReturnAttackFromExhaust||choice==CardChoiceKind.ReturnSkillFromExhaust){exhaust.Remove(selected);hand.Add(selected);if(choice==CardChoiceKind.ReturnAttackFromExhaust)AddTemporaryDamageBonus(selected,pendingPlay.choiceFollowupValue);RelicRetrieved(selected);Emit(CombatEventKind.Draw,1,true,selected,"FROM EXHAUST");}
            else if(choice==CardChoiceKind.ExhaustSoulFromHand){hand.Remove(selected);SacrificeSoul(selected);}
            else{hand.Remove(selected);ExhaustCard(selected);}
            if(choice==CardChoiceKind.ExhaustSoulFromHand)
            {
                if(pendingPlay.card.id=="soul_offering"){GainBlock(pendingPlay.choiceFollowupValue,true,pendingPlay.card);Draw(pendingPlay.choiceFollowupDraw,false);}
                else if(pendingPlay.card.id=="soul_feast"){energy+=pendingPlay.choiceFollowupEnergy;Emit(CombatEventKind.Energy,energy,true);Draw(pendingPlay.choiceFollowupDraw,false);}
                else if(pendingPlay.card.id=="soul_exchange")Draw(pendingPlay.choiceFollowupDraw,false);
                else if(pendingPlay.card.id=="soul_rend")DamageEnemy(pendingPlay.choiceFollowupValue,pendingPlay.card);
                pendingPlay.choiceFollowupEnergy=pendingPlay.choiceFollowupDraw=0;
            }
            pendingPlay.choice=CardChoiceKind.None;if(pendingPlay.choiceFollowupEnergy>0){energy+=pendingPlay.choiceFollowupEnergy;Emit(CombatEventKind.Energy,energy,true);}if(pendingPlay.choiceFollowupDraw>0)Draw(pendingPlay.choiceFollowupDraw,false);if(pendingPlay.choiceFollowupSigil==-2){pendingPlay.choice=CardChoiceKind.SigilMode;pendingPlay.choiceFollowupSigil=-1;return true;}if(pendingPlay.choiceFollowupSigil>=0)CreateSigil((SigilKind)pendingPlay.choiceFollowupSigil);
            ContinueCardResolution();return true;
        }

        public bool ChooseOption(string option)
        {
            if(!AwaitingChoice||string.IsNullOrEmpty(option)||!ChoiceOptions.Contains(option))return false;if(ChoiceKind==CardChoiceKind.ExpansionOption)return ChooseRemainingOption(option);var optionIndex=Array.IndexOf(ChoiceOptions.ToArray(),option);var choice=pendingPlay.choice;var value=pendingPlay.choiceFollowupValue;pendingPlay.choice=CardChoiceKind.None;
            if(choice==CardChoiceKind.AdaptMode){if(option=="BLOCK")GainBlock(value,true,pendingPlay.card);else DamageEnemyRaw(value,"ADAPT");}
            else if(choice==CardChoiceKind.SigilMode){var kind=option.StartsWith("EMBER")?SigilKind.Ember:option.StartsWith("HEX")?SigilKind.Hex:SigilKind.Echo;var created=CreateSigil(kind);if(value>0&&created>=0)ActivateSigil(created);else if(value<0)GainResonance(-value);}
            else if(choice==CardChoiceKind.SigilSlot){for(var i=0;i<value;i++)ActivateSigil(optionIndex);}
            else if(choice==CardChoiceKind.BuffToGain){if(option=="STRENGTH")GainStrength(value);else if(option=="FORTIFY")GainFortify(value);else if(option=="RETALIATE")GainRetaliate(value);}
            else if(choice==CardChoiceKind.BuffToDouble){if(option=="STRENGTH")GainStrength(player.strength);else if(option=="FORTIFY")GainFortify(player.fortify);else if(option=="RETALIATE"){GainRetaliate(retaliation);}}
            ContinueCardResolution();return true;
        }

        private void ResolveCard(CardDef card)
        {
            var value=card.value;var secondary=card.secondary;var heavy=memory.attacksThisTurn==0;var debuffs=EnemyDebuffCount();var curseCount=AllOwnedCards().Count(c=>c.origin==CardOrigin.Curse);
            if(card.kind==CardKind.Attack)value+=card.permanentDamageBonus;if(card.effect==EffectKind.Block)value+=card.permanentBlockBonus;
            ApplySpecialBase(card,ref value,ref secondary,heavy);
            if(ResolveMartialOccultCard(card,value,secondary,heavy))return;
            switch(card.id)
            {
                case "great_cleave":if(heavy)value=secondary;break;case "executioners_cleave":if(heavy)value=secondary+card.permanentDamageBonus;break;case "payback":if(memory.wasAttackedLastTurn)value=secondary;break;
                case "crown_breaker":value+=player.strength+player.fortify;break;case "crushing_weight":value+=player.fortify;break;case "final_judgment":value+=player.strength*2;break;
                case "eye_for_an_eye":DamageEnemyRaw(enemy.attemptedAttackDamage,"EYE FOR AN EYE");return;case "rising_strike":value+=memory.attacksThisTurn*secondary;break;
                case "raging_blow":value+=BuffCount()*secondary;break;case "second_wind":value*=Math.Max(1,memory.powersInPlay);break;
                case "opening_blow":if(cardsPlayed==0)value+=secondary;break;case "follow_through":if(memory.skillsThisTurn>0)value+=secondary;break;
                case "opportunist":if(debuffs>0)value=secondary;break;case "pocket_guard":if(memory.attacksThisTurn>0)value+=secondary;break;case "finishing_cut":if(enemy.hp*2<enemy.maxHp)value+=secondary;break;
                case "exploit_weakness":value+=debuffs*secondary;break;case "emergency_guard":if(IntentDealsDamage&&IntentDisplayValue*intentHits>=15)value+=secondary;break;
                case "perfect_opportunity":value*=debuffs;break;case "brace_yourself":if(player.block==0)value=secondary;break;
                case "resonant_strike":if(resonance>=3)value+=secondary;break;case "void_bolt":value+=curseCount*secondary;break;case "execution_hex":value+=memory.markedConsumed*secondary;break;
                case "forbidden_one":value+=curseCount*secondary;break;case "hexfire":break;case "cremation":break;
                case "reaping_blow":case "spirit_cleave":if(memory.soulsPlayedThisTurn>0)value+=secondary;break;
                case "spirit_scythe":value+=memory.soulsPlayedThisTurn*secondary;break;case "soul_reaper":value+=memory.soulsPlayedCombat*secondary;break;
                case "soul_guard":value*=hand.Count(IsSoul);break;
                case "hollow_cut":if(memory.exhaustsThisTurn>0)value+=secondary;break;
                case "reapers_momentum":value*=Math.Max(0,cardsPlayed-R.turnStartCards);break;
            }
            if(IsSoul(card)){ResolveSoul(card,0);return;}
            if(card.id=="blood_magic")LosePlayerHp(3,true);if(card.id=="blood_rush")LosePlayerHp(secondary,true);if(card.id=="blood_price")LosePlayerHp(secondary,true);if(player.hp<=0)return;
            if(card.id=="breakthrough")enemy.block=0;
            if(card.id=="overhead_strike"&&heavy)ApplyEnemyDebuff(EffectKind.Vulnerable,secondary);
            if(card.id=="soul_pierce"&&enemy.marked>0){ConsumeMarked(1);value+=secondary;}
            if(card.id=="final_curse"){var consumed=ConsumeMarked(enemy.marked);value*=consumed;}
            if(card.id=="black_sun"){var consumed=ConsumeMarked(enemy.marked);value+=consumed*secondary;ApplyBurn(value);return;}
            if(card.id=="expose"){if(ConsumeMarked(1)>0)ApplyEnemyDebuff(EffectKind.Vulnerable,value);return;}
            if(card.id=="soul_drain"){if(ConsumeMarked(1)>0){energy+=value;Emit(CombatEventKind.Energy,energy,true);Draw(secondary,false);}return;}
            if(card.id=="feed_the_flame"){ApplyBurn(CeilPercent(enemy.burn,value));return;}
            if(card.id=="ignite"){TriggerBurn(Math.Max(0,CeilPercent(enemy.burn,value)),true);return;}
            if(card.id=="inferno"){ApplyBurn(enemy.burn);return;}
            if(card.id=="resonant_flame"){var spent=Math.Min(secondary,resonance);resonance-=spent;Emit(CombatEventKind.Resonance,resonance,true);ApplyBurn(value*spent);return;}
            if(card.id=="adapt"){pendingPlay.choice=CardChoiceKind.AdaptMode;pendingPlay.choiceFollowupValue=value;return;}
            if(card.id=="limit_break"){if(BuffOptions().Count>0)pendingPlay.choice=CardChoiceKind.BuffToDouble;return;}
            if(card.id=="improvisation")memory.makeNextDrawFree=1;
            if(card.id=="soul_offering"||card.id=="soul_feast"||card.id=="soul_exchange")
            {
                pendingPlay.choice=CardChoiceKind.ExhaustSoulFromHand;pendingPlay.choiceFollowupValue=value;
                pendingPlay.choiceFollowupEnergy=card.id=="soul_feast"?value:0;pendingPlay.choiceFollowupDraw=card.id=="soul_offering"?secondary:card.id=="soul_feast"?secondary:value;
                if(ChoiceCards.Count==0)pendingPlay.choice=CardChoiceKind.None;return;
            }
            if(card.id=="soulstorm"){PlaySoulsFromHand(value);return;}
            if(card.id=="army_of_the_dead"){FillHandWithSouls(card.upgraded);return;}
            if(card.id=="empty_grave"){Draw(Math.Min(value,exhaust.Count(IsSoul)/2),false);return;}
            if(card.id=="devour_the_dead"){DevourSouls(value);return;}
            if(card.id=="final_procession"){PlaySoulsFromExhaust();return;}
            if(card.id=="soul_conversion"){TransformHandToSouls();return;}
            if(card.id=="consume_darkness"||card.id=="void_sigil"||card.id=="recycle")
            {
                var curse=card.id!="recycle";pendingPlay.choice=curse?CardChoiceKind.ExhaustCurseFromHand:CardChoiceKind.ExhaustFromHand;pendingPlay.choiceFollowupEnergy=card.id=="consume_darkness"?value:0;pendingPlay.choiceFollowupDraw=card.id=="consume_darkness"?secondary:card.id=="recycle"?value:0;pendingPlay.choiceFollowupSigil=card.id=="void_sigil"?-2:-1;if(ChoiceCards.Count==0)pendingPlay.choice=CardChoiceKind.None;return;
            }
            if(card.id=="clear_mind"){Draw(value,false);if(hand.Count>0)pendingPlay.choice=CardChoiceKind.DiscardFromHand;return;}

            switch(card.effect)
            {
                case EffectKind.Damage:
                    var hits=Math.Max(1,card.hits);if(card.id=="cremation"&&enemy.burn>=10)hits=2;if(card.id=="no_mercy"&&memory.retaliateTriggeredTurn>0)hits=2;if(card.id=="soul_piercer_reaper"&&hand.Any(IsSoul))hits++;
                    var beforeEnemyHp=enemy.hp;for(var i=0;i<hits&&!IsOver&&enemy.hp>0;i++){var hitValue=value;if(card.id=="soul_carver"&&i<hand.Count(IsSoul))hitValue+=secondary;DamageEnemy(hitValue,card,i,hits);}if(card.id=="claim_the_fallen"&&beforeEnemyHp>0&&enemy.hp<=0)bonusCardRewards++;break;
                case EffectKind.Block:GainBlock(value,true,card);break;
                case EffectKind.Draw:Draw(value,false);break;
                case EffectKind.Strength:GainStrength(value,true);break;
                case EffectKind.Fortify:GainFortify(value);break;
                case EffectKind.Resonance:GainResonance(value);break;
                case EffectKind.Energy:energy+=value;Emit(CombatEventKind.Energy,energy,true);break;
                case EffectKind.Burn:ApplyBurn(value);break;
                case EffectKind.Mark:ApplyMarked(value);break;
                case EffectKind.Vulnerable:ApplyEnemyDebuff(EffectKind.Vulnerable,value);break;
                case EffectKind.Weak:ApplyEnemyDebuff(EffectKind.Weak,value);break;
                case EffectKind.Sigil:ResolveSigilCard(card,value,secondary);break;
            }
            if(pendingPlay?.relicPrimaryRepeat!=true)ResolveSecondary(card,secondary,heavy);
            if(card.kind==CardKind.Power){InstallPower(card,value,secondary);Emit(CombatEventKind.Status,value,true,card,"POWER");}
        }

        private void ResolveSecondary(CardDef card,int secondary,bool heavy)
        {
            switch(card.id)
            {
                case "battle_cry":case "advance":case "steady_hands":Draw(1,false);break;case "blood_rush":DrawMatching(CardKind.Attack);break;case "stand_firm":GainTemporaryFortify(secondary);break;case "brace":memory.nextHeavyCostReduction++;Emit(CombatEventKind.Status,memory.nextHeavyCostReduction,true,null,"BRACE");break;case "iron_will":case "shield_wall":GainFortify(secondary);break;case "armored_strike":if(player.fortify>0)GainBlock(secondary,true,card);break;
                case "crushing_blow":ApplyEnemyDebuff(EffectKind.Vulnerable,secondary);break;
                case "spiked_guard":case "come_at_me":case "last_bastion":GainRetaliate(secondary);break;
                case "bloodied_armor":if(player.strength>0)GainRetaliate(secondary);break;
                case "forceful_guard":memory.nextAttackBonus+=secondary;break;case "countercharge":if(memory.retaliateTriggeredTurn>0){energy++;Emit(CombatEventKind.Energy,energy,true);}break;
                case "battle_rush":memory.nextAttackCostReduction=1;if(secondary>0)Draw(secondary,false);break;case "preparation":memory.nextSkillCostReduction=1;break;case "preparation_strike":memory.nextAttackCostReduction=1;break;
                case "arcane_ward":case "kindle":GainResonance(secondary);break;case "cinder":ApplyBurn(secondary);break;case "marked_shot":ApplyMarked(secondary);break;
                case "hexed_blade":if(enemy.marked>0)GainResonance(secondary);break;case "burning_hex":ApplyMarked(secondary);break;case "hexfire":if(enemy.marked>0)ApplyBurn(secondary);break;
                case "quick_thinking":if(secondary>0)GainBlock(secondary,true,card);break;case "adrenaline_rush":AddTemporaryCard("dazed_mind",false);if(secondary>0)Draw(secondary,false);break;
                case "dark_bargain":AddRandomCurse(false);break;case "forbidden_knowledge":AddRandomCurse(true);break;case "dark_offering":AddRandomCurse(false);energy+=secondary;Emit(CombatEventKind.Energy,energy,true);break;
                case "blasphemous_ritual":AddRandomCurse(false);break;case "controlled_breathing":if(cardsPlayed==0)memory.retainBlock=Math.Max(memory.retainBlock,card.value+card.permanentBlockBonus+player.fortify);break;
                case "soul_slash":AddSouls(secondary,SoulDestination.Hand);break;
                case "soul_call":AddSouls(card.value,SoulDestination.Hand);break;
                case "deaths_touch":ApplyEnemyDebuff(EffectKind.Weak,secondary);break;
                case "grave_guard":AddSouls(secondary,SoulDestination.Draw);break;
                case "dark_veil_reaper":if(memory.soulsPlayedThisTurn>0)DamageEnemyRaw(secondary,"DARK VEIL");break;
                case "call_beyond":AddSouls(secondary,SoulDestination.Discard);break;
                case "death_knell":AddSouls(1,SoulDestination.Hand);memory.nextSoulDamageBonus+=card.value;break;
                case "grim_focus":memory.nextAttackBonus+=memory.soulsPlayedThisTurn>0?secondary:card.value;break;
                case "scythe_cycle":Draw(secondary,false);break;
                case "dark_insight":var soulDrawnBefore=memory.soulsDrawn;Draw(card.value,false);if(memory.soulsDrawn>soulDrawnBefore)Draw(secondary,false);break;
                case "grave_pact":AddSouls(secondary,SoulDestination.Hand);memory.nextAttackBonus+=card.value*hand.Count(IsSoul);break;
                case "soul_rend":pendingPlay.choice=CardChoiceKind.ExhaustSoulFromHand;pendingPlay.choiceFollowupValue=card.value+card.permanentDamageBonus;if(ChoiceCards.Count==0)pendingPlay.choice=CardChoiceKind.None;break;
                case "hollow_scythe":if(hand.Any(IsSoul))GainBlock(secondary,true,card);break;
                case "deaths_door":if(!hand.Any(IsSoul))AddSouls(secondary,SoulDestination.Hand);break;
                case "beyond_the_veil":DrawEmpoweredSouls(card.value,secondary);break;
                case "graves_edge":pendingPlay.choice=CardChoiceKind.ReturnAttackFromExhaust;pendingPlay.choiceFollowupValue=secondary;if(ChoiceCards.Count==0)pendingPlay.choice=CardChoiceKind.None;break;
                case "call_from_beyond":pendingPlay.choice=CardChoiceKind.ReturnSkillFromExhaust;if(ChoiceCards.Count==0)pendingPlay.choice=CardChoiceKind.None;break;
                case "soul_echo":memory.nextSoulReplay++;break;
            }
        }

        private void InstallPower(CardDef card,int value,int secondary)
        {
            if(InstallMartialOccultPower(card,value))return;
            switch(card.id)
            {
                case "battle_temper":memory.battleTemper+=value;break;case "iron_blood":memory.ironBlood+=value;if(secondary>0)GainFortify(secondary);break;case "living_armor":memory.livingArmor=1;break;case "war_machine":memory.warMachine+=value;break;case "unbreakable_spirit":break;
                case "patient_warrior":memory.patientWarrior+=value;break;case "retribution":memory.retributionPower+=value;break;case "hold_the_line":memory.holdLinePower+=value;break;case "relentless":memory.relentless+=value;break;case "onslaught":memory.onslaughtThreshold=value;break;case "indomitable":memory.indomitable+=value;break;
                case "sigil_mastery":memory.sigilMastery+=value;break;case "grand_convergence":memory.grandConvergence+=value;break;case "ashes":memory.ashes+=value;break;case "deaths_gaze":memory.deathsGaze+=value;break;case "embrace_the_void":memory.embraceVoid+=value;break;case "damnation":memory.damnationStrength+=value;break;
                case "battle_rhythm":memory.battleRhythm+=value;break;case "reserve_energy":memory.reserveEnergy+=value;break;case "tactical_advantage":memory.tacticalAdvantage=Math.Max(memory.tacticalAdvantage,secondary+1);break;case "resourceful":memory.resourceful+=value;break;case "overflow":memory.overflowPower+=value;break;case "chain_reaction":memory.chainReaction+=value;break;case "against_all_odds":memory.againstAllOddsDraw=secondary+1;break;case "perfect_form":memory.perfectForm+=value;break;
                case "deaths_embrace":memory.deathsEmbrace+=value;break;case "death_march":memory.deathMarchCadence=value;break;case "gravekeeper":memory.gravekeeper+=value;break;case "endless_harvest":memory.endlessHarvest+=value;break;case "death_incarnate":memory.deathIncarnate+=value;break;case "grim_ascension":memory.grimAscensionBonus+=value;break;case "soulbound_tome":memory.soulboundTome+=value;break;case "eternal_souls":memory.eternalSouls+=value;break;case "reapers_calling":memory.reapersCalling+=value;break;
            }
        }

        private void ResolveSigilCard(CardDef card,int value,int secondary)
        {
            switch(card.id)
            {
                case "ember_ritual":CreateSigil(SigilKind.Ember);if(secondary>0)GainResonance(secondary);break;case "hex_ritual":CreateSigil(SigilKind.Hex);if(secondary>0)GainResonance(secondary);break;case "echo_ritual":CreateSigil(SigilKind.Echo);if(secondary>0)GainResonance(secondary);break;
                case "invocation":if(sigils.Count>0)ActivateSigil(0);else GainBlock(4,true,card);break;case "first_ritual":pendingPlay.choice=CardChoiceKind.SigilMode;pendingPlay.choiceFollowupValue=-secondary;break;case "shatter_sigil":if(sigils.Count>0){for(var i=0;i<value;i++)ActivateSigil(0);Emit(CombatEventKind.Status,1,true,null,"SIGIL SHATTER");RemoveRemainingSigil(0);}break;
                case "ritual_cycle":ActivateSigil(0);GainResonance(secondary);Draw(1,false);break;case "perfect_ritual":for(var repeat=0;repeat<value;repeat++)for(var i=0;i<sigils.Count;i++)ActivateSigil(i);break;
                case "blasphemous_ritual":pendingPlay.choice=CardChoiceKind.SigilMode;pendingPlay.choiceFollowupValue=secondary;break;case "void_sigil":break;
            }
        }

        private void TriggerAfterCard(CardDef card)
        {
            AfterRemainingCard(card);
            if(memory.battleRhythm>0&&cardsPlayed%3==0)TriggerEffect("battle_rhythm",()=>DamageRandomEnemy(memory.battleRhythm,"BATTLE RHYTHM"));
            if(relics.Contains("silver_feather")&&cardsPlayed%4==0)RelicTrigger("silver_feather",()=>GainBlock(4));
            if(relics.Contains("mirror_fragment")&&card.cost==0&&!memory.zeroCostRelicUsed){memory.zeroCostRelicUsed=true;RelicTrigger("mirror_fragment",()=>RelicDamage(3,"MIRROR FRAGMENT"));}
            if(memory.tacticalAdvantage>0&&card.cost==0&&!memory.specialPlayIds.Contains(-100000-turn)){PresentationPulse("tactical_advantage");Draw(1,false);if(memory.tacticalAdvantage>1)DamageRandomEnemy(2,"TACTICAL ADVANTAGE");memory.specialPlayIds.Add(-100000-turn);}
            if(memory.onslaughtThreshold>0&&card.kind==CardKind.Attack&&memory.attacksThisTurn>=memory.onslaughtThreshold&&!memory.onslaughtUsed){PresentationPulse("onslaught");energy++;memory.onslaughtUsed=true;Emit(CombatEventKind.Energy,energy,true);}
            if(card.specialModification=="gilded"&&!memory.specialPlayIds.Contains(card.instanceId+1000000)){GainBlock(5);memory.specialPlayIds.Add(card.instanceId+1000000);}
            if(card.specialModification=="lingering"&&!memory.specialPlayIds.Contains(card.instanceId+2000000)){memory.lingeringIds.Add(card.instanceId);memory.specialPlayIds.Add(card.instanceId+2000000);}

        }

        private void MoveResolvedCard(CardDef card)
        {
            if(card.kind==CardKind.Power){activeAspects.Add(card);Emit(CombatEventKind.Status,0,true,card,"ASPECT ACTIVE");return;}
            if(card.exhaust||card.perfected&&card.kind is CardKind.Attack or CardKind.Skill)ExhaustCard(card);else{discard.Add(card);Emit(CombatEventKind.Discard,1,true,card);}
        }
        private void ExhaustCard(CardDef card)
        {
            if(RelicPreventExhaust(card))return;exhaust.Add(card);memory.exhaustsThisTurn++;Emit(CombatEventKind.Exhaust,1,true,card);RelicExhausted(card);
            if(memory.resourceful>0)TriggerEffect("resourceful",()=>GainBlock(memory.resourceful));if(relics.Contains("bone_charm"))RelicTrigger("bone_charm",()=>GainBlock(2));
            if(card.origin==CardOrigin.Curse){if(memory.damnationStrength>0){PresentationPulse("damnation");GainStrength(memory.damnationStrength);GainResonance(1);}if(relics.Contains("blackened_tooth")&&!memory.curseExhaustRelicUsed){RelicPresentationPulse("blackened_tooth");energy++;Draw(1,false);memory.curseExhaustRelicUsed=true;}}
            if(card.specialModification=="recurring"&&!memory.specialPlayIds.Contains(card.instanceId+3000000)){var copy=card.Copy();copy.instanceId=++nextInstanceId;draw.Add(copy);memory.specialPlayIds.Add(card.instanceId+3000000);Emit(CombatEventKind.Shuffle,1,true,copy,"RECURRING");}
            ShardExhausted(card);
        }

        public bool PlayFree(CardDef definition){if(pendingPlay!=null||definition==null||IsOver||phase!=CombatPhase.Player)return false;var card=Instance(definition);hand.Add(card);card.cost=0;return Play(card);}
        public void EndPlayerTurn()
        {
            if(pendingPlay!=null||IsOver||phase!=CombatPhase.Player)return;
            if(memory.reserveEnergy>0&&energy>0){PresentationPulse("reserve_energy");GainBlock(memory.reserveEnergy*energy);}if(relics.Contains("balanced_scales")&&energy==0){RelicPresentationPulse("balanced_scales");GainBlock(6);}ShardEndTurn();
            if(memory.overflowPower>0&&player.block>TotalIntendedDamage){PresentationPulse("overflow");DamageEnemyRaw(memory.overflowPower,"OVERFLOW");}
            if(memory.grandConvergence>0&&sigils.Count>=3){PresentationPulse("grand_convergence");for(var i=0;i<sigils.Count;i++)ActivateSigil(i);}for(var i=0;i<sigils.Count;i++)if(sigils[i]==SigilKind.Ember)ActivateSigil(i);
            foreach(var card in hand.ToArray()){if(card.id=="lingering_pain")LosePlayerHp(3,false);else if(card.id is "decay" or "haunting")LosePlayerHp(2,false);if(card.ethereal){hand.Remove(card);ExhaustCard(card);}else{hand.Remove(card);discard.Add(card);Emit(CombatEventKind.Discard,1,true,card);}card.firstDrawFree=false;}
            ExpireEffects(CombatEffectDuration.TurnEnd);memory.temporaryDamageIds.Clear();memory.temporaryDamageValues.Clear();memory.nextSoulDamageBonus=memory.nextSoulReplay=memory.gravePactBonus=0;
            if(memory.temporaryFortify>0){var expired=Math.Min(player.fortify,memory.temporaryFortify);player.fortify-=expired;memory.temporaryFortify=0;Emit(CombatEventKind.Status,-expired,true,null,"FORTIFY");}
            memory.nextAttackBonus=memory.nextAttackPenalty=0;if(memory.temporaryStrength>0){player.strength=Math.Max(0,player.strength-memory.temporaryStrength);memory.temporaryStrength=0;}
            if(memory.crownSacrificeDebt){RelicPresentationPulse("crown_of_sacrifice");LosePlayerHp(3,true);}
            ShardRetainEnergy();
            if(activeShardId=="duelist"&&activeShardFractured&&shardMemory.attacks==1){ShardPulse();Draw(2,false);}
            if(player.burn>0){LosePlayerHp(player.burn,false);player.burn=Math.Max(0,player.burn-GameContent.BurnDecayPerTrigger);}if(player.weak>0)player.weak--;if(player.frail>0)player.frail--;ForEachLivingEnemy(()=>{if(enemy.vulnerable>0)enemy.vulnerable--;Emit(CombatEventKind.StateSnapshot);});
            phase=IsOver?CombatPhase.Finished:CombatPhase.Enemy;if(!IsOver)Emit(CombatEventKind.EnemyTurn,turn);
        }
        public void ResolveEnemyTurn()
        {
            if(pendingPlay!=null||IsOver||phase!=CombatPhase.Enemy)return;
            memory.attackedThisEnemyTurn=false;memory.unblockedDamageAttempted=0;ForEachLivingEnemy(()=>enemy.attemptedAttackDamage=0);
            ForEachLivingEnemy(()=>
            {
                if(player.hp<=0||enemy.hp<=0)return;
                enemy.block=0;Emit(CombatEventKind.EnemyAction,EnemyContextIndex);
                ExecuteEnemyPlan(PlannedEnemyTurn());
                if(enemy.weak>0)enemy.weak--;if(enemy.hp>0&&enemy.burn>0)TriggerBurn(enemy.burn,false);Emit(CombatEventKind.StateSnapshot);
            });
            memory.wasAttackedLastTurn=memory.attackedThisEnemyTurn;phase=CombatPhase.EnemyResolved;
            if(IsOver){phase=CombatPhase.Finished;Emit(CombatEventKind.Death,0,player.hp<=0);}
        }
        public void EndTurn(){if(pendingPlay!=null)return;if(phase==CombatPhase.Player)EndPlayerTurn();if(phase==CombatPhase.Enemy)ResolveEnemyTurn();if(!IsOver&&phase==CombatPhase.EnemyResolved)NextTurn();}
        public void DrawCards(int count)=>Draw(count,false);

        // Attack stacking order is intentionally centralized here. Card-authored base and
        // permanent copy bonuses are prepared by ResolveCard; this method then applies
        // flat Binding/power/relic/Shard additions, percentage Shard multipliers,
        // hand penalties, Strength, Weak, Vulnerable, and finally enemy defenses/Block.
        // One call represents one hit, so Serrated, Strength and multi-hit Shards apply
        // consistently to every hit without recursive card-play triggers.
        private void DamageEnemy(int baseAmount,CardDef card,int hit=0,int hits=1)
        {
            // Relentless modifies the qualifying Attack itself, not a separate
            // unmodified damage proc after it (and therefore previews truthfully).
            if(card.kind==CardKind.Attack&&memory.relentless>0)
            {
                var counted=1+Math.Max(0,pendingPlay?.additionalAttackCount??0);
                var crossings=(memory.attacksThisTurn+counted)/3-memory.attacksThisTurn/3;
                if(crossings>0)
                {
                    if(pendingPlay!=null&&!pendingPlay.relentlessPulseUsed){pendingPlay.relentlessPulseUsed=true;PresentationPulse("relentless");}
                    baseAmount+=memory.relentless*crossings;
                }
            }
            if(enemy.hp<=0)return;var amount=baseAmount+RemainingDamageBonus(card)+TemporaryDamageBonus(card)+(pendingPlay?.attackEffectBonus??0);var heavy=memory.attacksThisTurn==0;if(card.specialModification=="serrated")amount+=2;if(card.specialModification=="weighted"&&card.cost>=2)amount+=6;if(memory.patientWarrior>0&&card.keywords.Contains("Heavy")){PresentationPulse("patient_warrior");amount+=memory.patientWarrior;}if(memory.nextAttackBonus>0){amount+=memory.nextAttackBonus;memory.nextAttackBonus=0;}if(memory.extraDrawDamage>0){amount+=memory.extraDrawDamage;memory.extraDrawDamage=0;}if(memory.nextAttackPenalty>0){amount=Math.Max(0,amount-memory.nextAttackPenalty);memory.nextAttackPenalty=0;}
            if(relics.Contains("worn_whetstone")&&!memory.firstAttackPlayed){RelicPresentationPulse("worn_whetstone");amount+=6;}if(relics.Contains("war_gods_crest")&&card.cost>=2){RelicPresentationPulse("war_gods_crest");amount+=3;GainBlock(3);}if(relics.Contains("duelists_pin")&&heavy){RelicPresentationPulse("duelists_pin");amount+=4;}
            if(hand.Any(c=>c.id=="dread"))amount=amount*90/100;if(hand.Any(c=>c.id=="falter"))amount=Math.Max(0,amount-3);amount+=player.strength;
            amount=RelicAttackAmount(card,amount);amount=ShardAttackDamage(amount,card,hit,hits);if(card.id=="break_the_line"&&heavy)amount*=2;if(card.id=="shatter_the_ranks"&&enemy.block>0)amount=amount*3/2;if(player.weak>0)amount=amount*3/4;if(enemy.vulnerable>0)amount=amount*3/2;
            if(IsSoul(card))amount=amount*(100+EffectValue(enemy,"reaped"))/100;
            var hitId=++nextFeedbackHitId;var absorbed=Math.Min(enemy.block,amount);enemy.block-=absorbed;var dealt=amount-absorbed;enemy.hp=Math.Max(0,enemy.hp-dealt);if(absorbed>0)Emit(CombatEventKind.Block,absorbed,false,card,"BLOCKED",hitId);if(dealt>0)Emit(CombatEventKind.Damage,dealt,false,card,hitId:hitId);highestDamage=Math.Max(highestDamage,dealt);RelicDamageDealt(amount,Temporary(card)?"Temporary":card.kind==CardKind.Attack?"Attack":"Power",card.kind==CardKind.Attack);RefreshEnemyState();RefreshMechanicTelemetry();
            if(sigils.Contains(SigilKind.Hex)&&!memory.specialPlayIds.Contains(-300000-turn)){PassiveSigilPresentationPulse(SigilKind.Hex);ApplyMarked(1);GainResonance(1);memory.specialPlayIds.Add(-300000-turn);}
        }
        private void DamageEnemyRaw(int amount,string label){if(amount<=0||enemy.hp<=0)return;var hitId=++nextFeedbackHitId;var absorbed=Math.Min(enemy.block,amount);enemy.block-=absorbed;var dealt=amount-absorbed;enemy.hp=Math.Max(0,enemy.hp-dealt);if(absorbed>0)Emit(CombatEventKind.Block,absorbed,false,null,"BLOCKED",hitId);if(dealt>0)Emit(CombatEventKind.Damage,dealt,false,null,label,hitId);RelicDamageDealt(amount,relicDamageDepth>0?"Relic":label=="RETALIATE"?"Retaliate":label=="DEATH'S ECHO"?"Death's Echo":"Power",false,label=="CONDEMNED");RefreshEnemyState();}
        private void DamagePlayer(int amount)
        {
            var hitId=++nextFeedbackHitId;memory.attackedThisEnemyTurn=true;amount=EnemyAttackBeforeBlock(amount);memory.unblockedDamageAttempted+=amount;enemy.attemptedAttackDamage+=amount;var beforeBlock=player.block;var absorbed=Math.Min(player.block,amount);player.block-=absorbed;var lost=amount-absorbed;if(absorbed>0)Emit(CombatEventKind.Block,absorbed,true,null,"BLOCKED",hitId);if(lost>0)LosePlayerHp(lost,false,hitId);
            if(beforeBlock>0&&player.block==0&&memory.indomitable>0&&!memory.indomitableUsed){PresentationPulse("indomitable");nextTurnBlock+=memory.indomitable;memory.indomitableUsed=true;Emit(CombatEventKind.Status,memory.indomitable,true,null,"INDOMITABLE · NEXT TURN");}
            if(beforeBlock>0&&player.block==0)OnEnemyBreaksBlock();
            var reflected=0;if(relics.Contains("thorn")&&absorbed>0){RelicPresentationPulse("thorn");reflected+=5;}if(activeShardId=="thorn"&&absorbed>0){ShardPulse(false,amount:absorbed);reflected+=activeShardFractured?20:10;if(activeShardFractured)GainBlock(CeilPercent(absorbed,25));}
            if(reflected>0)DamageEnemyRaw(reflected,"THORNS");
        }
        private void LosePlayerHp(int amount,bool selfInflicted,int hitId=0){var before=player.hp;player.hp=Math.Max(0,player.hp-Math.Max(0,amount));if(player.hp<=0&&memory.immortalThreadUsed==false&&relics.Contains("immortal_thread")){RelicPresentationPulse("immortal_thread");player.hp=1;memory.immortalThreadUsed=true;}if(before>player.hp)Emit(CombatEventKind.Damage,before-player.hp,true,hitId:hitId);if(selfInflicted&&before>player.hp&&relics.Contains("crimson_spur")&&!memory.selfDamageRelicUsed){memory.selfDamageRelicUsed=true;RelicPresentationPulse("crimson_spur");GainStrength(2);}}
        private void Heal(int amount){var before=player.hp;player.hp=Math.Min(player.maxHp,player.hp+amount);if(player.hp>before)Emit(CombatEventKind.Heal,player.hp-before,true);}
        // Block stacking order: resolved card base/permanent copy bonus, Fortify and flat
        // Binding modifiers, in-hand Frailty percentage, then relic and Shard additions.
        // The final amount is recorded once so threshold triggers observe actual Block gained.
        private void GainBlock(int amount,bool fromCard=false,CardDef card=null)
        {
            if(fromCard){amount+=RemainingBlockBonus(card);amount=RelicBlockAmount(card,amount);}
            amount+=player.fortify;
            if(fromCard){if(card!=null&&card.specialModification=="reinforced")amount+=4;if(card!=null&&card.specialModification=="chained"&&memory.lastCardWasAttack)amount+=4;if(hand.Any(c=>c.id=="frailty"))amount=amount*80/100;}
            if(relics.Contains("gilded_buckle")&&!memory.firstBlockRelicUsed){RelicPresentationPulse("gilded_buckle");amount+=5;memory.firstBlockRelicUsed=true;}amount=ShardBlockAmount(amount,fromCard,card);if(amount<=0)return;
            player.block+=amount;memory.blockGainedThisTurn+=amount;memory.blockGainsThisTurn++;memory.firstBlockPlayed=true;highestBlock=Math.Max(highestBlock,player.block);Emit(CombatEventKind.Block,amount,true,fromCard?card:null);
            if(relics.Contains("crowned_bulwark")&&!memory.crownedBulwarkUsed&&memory.blockGainedThisTurn>=15){RelicPresentationPulse("crowned_bulwark");GainFortify(1);memory.crownedBulwarkUsed=true;}
            ShardBlockGained(amount,fromCard,card);
        }
        private void GainStrength(int amount,bool temporary=false)
        {
            if(amount<=0)return;amount=ModifyBuff(amount+RallyBuffBonus());if(activeShardId=="bloodstone"){ShardPulse(amount:activeShardFractured?2:1);amount+=activeShardFractured?2:1;}if(relics.Contains("warriors_knot")&&!memory.firstStrengthRelicUsed){RelicPresentationPulse("warriors_knot");amount++;memory.firstStrengthRelicUsed=true;}amount=DoubleWarlordBuff(amount);player.strength+=amount;if(temporary)memory.temporaryStrength+=amount;Emit(CombatEventKind.Status,amount,true,null,"STRENGTH");RelicBuff("Strength");if(memory.battleTemper>0)TriggerEffect("battle_temper",()=>GainBlock(memory.battleTemper));
            if(memory.spiritStrengthTriggered==false&&activeAspects.Any(c=>c.id=="unbreakable_spirit")){memory.spiritStrengthTriggered=true;TriggerEffect("unbreakable_spirit_strength",()=>GainFortify(amount));}
        }
        private void GainFortify(int amount)=>GainFortifyCore(amount,false);
        private void GainTemporaryFortify(int amount)=>GainFortifyCore(amount,true);
        private void GainFortifyCore(int amount,bool temporary)
        {
            if(amount<=0)return;amount=DoubleWarlordBuff(ModifyBuff(amount+RallyBuffBonus()));player.fortify+=amount;if(temporary)memory.temporaryFortify+=amount;Emit(CombatEventKind.Status,amount,true,null,"FORTIFY");RelicBuff("Fortify");if(memory.ironBlood>0)TriggerEffect("iron_blood",()=>GainStrength(memory.ironBlood));
            if(memory.spiritFortifyTriggered==false&&activeAspects.Any(c=>c.id=="unbreakable_spirit")){memory.spiritFortifyTriggered=true;TriggerEffect("unbreakable_spirit_fortify",()=>GainStrength(amount));}
        }
        private int ModifyBuff(int amount){if(hand.Any(c=>c.id=="fractured_will"))amount=Math.Max(1,amount-1);if(memory.warMachine>memory.warMachineUses){amount++;memory.warMachineUses++;Emit(CombatEventKind.Status,0,true,null,"TRIGGER:WAR MACHINE");}return amount;}
        // maxResonance is retained only for compatibility with old serialized saves.
        // No gameplay ceiling; saturate only at the integer storage boundary.
        private void GainResonance(int amount){if(hand.Any(c=>c.id=="arcane_lock"))return;long gained=Math.Max(0,amount);if(relics.Contains("cracked_prism")&&!memory.firstResonanceRelicUsed){RelicPresentationPulse("cracked_prism");gained+=2;memory.firstResonanceRelicUsed=true;}var before=resonance;resonance=(int)Math.Min(int.MaxValue,(long)resonance+gained);if(before!=resonance){Emit(CombatEventKind.Resonance,resonance,true);if(resonance>before)RelicBuff("Resonance");}}

        private void ApplyBurn(int amount)
        {
            if(amount<=0)return;amount=RelicDebuffAmount(amount);if(relics.Contains("charred_locket")){RelicPresentationPulse("charred_locket");amount++;}amount=ShardDebuff(amount,true);var existed=enemy.burn>0;enemy.burn+=amount;memory.debuffApplicationsThisTurn++;Emit(CombatEventKind.Status,amount,false,null,"BURN");RelicDebuffApplied();if(existed&&memory.chainReaction>0){PresentationPulse("chain_reaction");DamageEnemyRaw(memory.chainReaction,"CHAIN REACTION");}
        }
        private void ApplyMarked(int amount)
        {
            if(amount<=0)return;if(relics.Contains("split_lens")&&!memory.specialPlayIds.Contains(-400000-turn)){RelicPresentationPulse("split_lens");amount++;memory.specialPlayIds.Add(-400000-turn);}ApplyEnemyDebuff(EffectKind.Mark,amount);
        }
        private void ApplyEnemyDebuff(EffectKind kind,int amount)
        {
            if(amount<=0)return;amount=RelicDebuffAmount(amount);var existing=kind==EffectKind.Mark?enemy.marked:kind==EffectKind.Vulnerable?enemy.vulnerable:enemy.weak;if(relics.Contains("mask_of_two_fates")&&!memory.firstDebuffRelicUsed){RelicPresentationPulse("mask_of_two_fates");amount*=2;memory.firstDebuffRelicUsed=true;}amount=ShardDebuff(amount,false);if(kind==EffectKind.Mark)enemy.marked+=amount;else if(kind==EffectKind.Vulnerable)enemy.vulnerable+=amount;else enemy.weak+=amount;memory.debuffApplicationsThisTurn++;Emit(CombatEventKind.Status,amount,false,null,kind==EffectKind.Mark?"MARKED":kind.ToString().ToUpperInvariant());RelicDebuffApplied();if(existing>0&&memory.chainReaction>0){PresentationPulse("chain_reaction");DamageEnemyRaw(memory.chainReaction,"CHAIN REACTION");}if(kind==EffectKind.Vulnerable&&existing>0&&relics.Contains("broken_crown")){RelicPresentationPulse("broken_crown");DamageEnemyRaw(6,"BROKEN CROWN");}
        }
        private int ConsumeMarked(int count)
        {
            var consumed=Math.Min(enemy.marked,Math.Max(0,count));if(consumed==0)return 0;enemy.marked-=consumed;OnMarkedLost();memory.markedConsumed+=consumed;Emit(CombatEventKind.Status,-consumed,false,null,"MARKED");if(memory.brandOfRuin>0){ExpansionPowerPulse("brand_of_ruin");ApplyBurn(memory.brandOfRuin*consumed);}var per=relics.Contains("executioners_seal")?3:0;ShardConsumedDebuffs(consumed);if(per>0){RelicPresentationPulse("executioners_seal");DamageEnemyRaw(per*consumed,"CONSUME");}if(memory.deathsGaze>0&&!memory.deathsGazeUsed){PresentationPulse("deaths_gaze");Draw(1,false);memory.deathsGazeUsed=true;}if(relics.Contains("hand_of_judgment")&&memory.markedConsumed/3>(memory.markedConsumed-consumed)/3){RelicPresentationPulse("hand_of_judgment");energy++;Draw(2,false);Emit(CombatEventKind.Energy,energy,true);}return consumed;
        }
        private void TriggerBurn(int amount,bool outsideNormal)
        {
            if(amount<=0)return;var damage=amount;enemy.hp=Math.Max(0,enemy.hp-damage);Emit(CombatEventKind.Damage,damage,false,null,"BURN");RelicDamageDealt(damage,"Burn",false);memory.burnTriggersThisTurn++;if(memory.ashes>0&&memory.burnTriggersThisTurn==1){PresentationPulse("ashes");GainResonance(memory.ashes);}if(outsideNormal&&relics.Contains("ashen_crown")){RelicPresentationPulse("ashen_crown");ApplyBurn(2);}if(!outsideNormal)enemy.burn=Math.Max(0,enemy.burn-GameContent.BurnDecayPerTrigger);ShardBurnTriggered();RefreshEnemyState();
        }

        private int CreateSigil(SigilKind kind){R.accessibleSigils|=1<<(int)kind;if(sigils.Count>=SigilCapacity)return -1;sigils.Add(kind);Emit(CombatEventKind.Status,1,true,null,kind.ToString().ToUpperInvariant()+" SIGIL");if(memory.sigilMastery>0&&!memory.sigilMasteryUsed){PresentationPulse("sigil_mastery");ActivateSigil(sigils.Count-1);memory.sigilMasteryUsed=true;}if(sigils.Count==3&&relics.Contains("third_eye")){RelicPresentationPulse("third_eye");ActivateSigil(0);}return sigils.Count-1;}
        private void ActivateSigil(int index)
        {
            if(index<0||index>=sigils.Count)return;
            var sigil=sigils[index];var boosted=R.sparkSlots.Remove(index);
            var previousSlot=feedbackSigilSlot;feedbackSigilSlot=index;
            try
            {
                TriggerEffect("sigil_"+sigil.ToString().ToLowerInvariant(),()=>{
                    memory.sigilActivations++;
                    Emit(CombatEventKind.Status,1,true,null,sigil.ToString().ToUpperInvariant()+" ACTIVATE");
                    SigilEffect(sigil,index,boosted?150:100);GainResonance(1);RelicSigilActivated();
                    if(relics.Contains("ritual_bell")&&memory.sigilActivations%3==0)RelicTrigger("ritual_bell",()=>GainResonance(1));
                });
            }
            finally{feedbackSigilSlot=previousSlot;}
        }
        private SigilKind PreferredSigil(){if(!sigils.Contains(SigilKind.Ember))return SigilKind.Ember;if(!sigils.Contains(SigilKind.Hex))return SigilKind.Hex;return SigilKind.Echo;}

        private enum SoulDestination { Hand, Draw, Discard }
        private static bool IsSoul(CardDef card)=>card?.id=="soul";
        private void AddSouls(int count,SoulDestination destination)
        {
            var soul=GameContent.Find("soul");if(soul==null)return;
            for(var i=0;i<Math.Max(0,count);i++)
            {
                var card=Instance(soul);RelicGenerated(card);var actual=destination==SoulDestination.Hand&&hand.Count>=12?SoulDestination.Discard:destination;if(actual==SoulDestination.Hand)hand.Add(card);else if(actual==SoulDestination.Draw)draw.Add(card);else discard.Add(card);
                Emit(actual==SoulDestination.Hand?CombatEventKind.Draw:CombatEventKind.Shuffle,1,true,card,"SOUL");
            }
            if(destination==SoulDestination.Draw&&count>0&&memory.gravekeeper>0&&!memory.gravekeeperUsed){PresentationPulse("gravekeeper");GainBlock(memory.gravekeeper);memory.gravekeeperUsed=true;}
        }
        private void FillHandWithSouls(bool upgraded)
        {
            var definition=GameContent.Find("soul");if(upgraded)definition=GameContent.Upgrade(definition);
            while(hand.Count<12){var soul=Instance(definition);RelicGenerated(soul);hand.Add(soul);Emit(CombatEventKind.Draw,1,true,soul,"SOUL");}
        }
        private void SacrificeSoul(CardDef soul){if(!IsSoul(soul))return;memory.soulsSacrificed++;ExhaustCard(soul);}
        private void ResolveSoul(CardDef card,int sourceBonus)
        {
            // Replay repeats the Soul's effect, not the play event or Replay itself.
            var newPlay=pendingPlay?.soulPlayRegistered!=true;
            var firstCombatSoul=newPlay&&memory.soulsPlayedCombat==0;
            if(newPlay){if(pendingPlay!=null)pendingPlay.soulPlayRegistered=true;memory.soulsPlayedCombat++;memory.soulsPlayedThisTurn++;OnRemainingSoul();}
            var nextBonus=memory.nextSoulDamageBonus;memory.nextSoulDamageBonus=0;
            var replays=Math.Min(32,memory.eternalSouls+(memory.nextSoulReplay>0?1:0));memory.nextSoulReplay=0;
            for(var copy=0;copy<=replays&&!IsOver;copy++)
            {
                if(copy==0&&memory.deathIncarnate>0)PresentationPulse("death_incarnate");
                if(copy==1&&memory.eternalSouls>0)PresentationPulse("eternal_souls");
                if(firstCombatSoul&&copy==0&&relics.Contains("deaths_keepsake"))RelicPresentationPulse("deaths_keepsake");
                var damage=card.value+card.permanentDamageBonus+memory.soulDamageBonus+memory.deathIncarnate+nextBonus+sourceBonus+(firstCombatSoul&&copy==0&&relics.Contains("deaths_keepsake")?3:0);
                DamageEnemy(damage,card);Emit(CombatEventKind.Status,memory.soulsPlayedCombat,true,card,copy>0?"SOUL REPLAY":"SOUL");
                DrawSoulCard();
            }
            if(!newPlay)return;
            if(memory.deathsEmbrace>0&&memory.soulsPlayedThisTurn==1){PresentationPulse("deaths_embrace");GainBlock(memory.deathsEmbrace);}
            if(memory.endlessHarvest>0&&memory.soulsPlayedThisTurn<=2){PresentationPulse("endless_harvest");AddSouls(Math.Max(1,memory.endlessHarvest/2),SoulDestination.Discard);}
            if(memory.deathMarchCadence>0&&memory.soulsPlayedCombat%memory.deathMarchCadence==0){PresentationPulse("death_march");Draw(1,false);}
            if(memory.grimAscensionBonus>0){PresentationPulse("grim_ascension");memory.soulDamageBonus+=memory.grimAscensionBonus;Emit(CombatEventKind.Status,memory.soulDamageBonus,true,null,"SOUL DAMAGE");}
        }
        private void PlaySoulOutsidePipeline(CardDef soul,int bonus)
        {
            if(soul==null||IsOver)return;var parent=pendingPlay;pendingPlay=new PendingCardPlay{card=soul,copiesRemaining=1};
            try{BeforeRelicPlay(soul);ResolveSoul(soul,bonus);if(pendingPlay.relicEcho&&!IsOver){pendingPlay.relicEcho=false;ConsumeEffect(player,"echoed");ResolveSoul(soul,bonus);}cardsPlayed++;memory.skillsThisTurn++;memory.firstSkillPlayed=true;Emit(CombatEventKind.CardResolved,1,true,soul);TriggerAfterCard(soul);AfterRelicPlay(soul,pendingPlay);ExhaustCard(soul);}
            finally{pendingPlay=parent;}
        }
        private void PlaySoulsFromHand(int bonus)
        {
            var souls=hand.Where(IsSoul).ToArray();foreach(var soul in souls){hand.Remove(soul);PlaySoulOutsidePipeline(soul,bonus);if(IsOver)break;}
        }
        private void PlaySoulsFromExhaust()
        {
            var souls=exhaust.Where(IsSoul).ToArray();foreach(var soul in souls){exhaust.Remove(soul);PlaySoulOutsidePipeline(soul,0);if(IsOver)break;}
        }
        private void DevourSouls(int cadence)
        {
            var souls=hand.Where(IsSoul).ToArray();var consumed=0;foreach(var soul in souls){hand.Remove(soul);SacrificeSoul(soul);if(exhaust.Contains(soul))consumed++;}var rewards=cadence<=1?consumed:consumed/cadence;if(rewards>0){energy+=rewards;Emit(CombatEventKind.Energy,energy,true);Draw(rewards,false);}
        }
        private void TransformHandToSouls(){var count=hand.Count;hand.Clear();AddSouls(count,SoulDestination.Hand);Emit(CombatEventKind.Status,count,true,null,"SOUL CONVERSION");}
        private void DrawEmpoweredSouls(int count,int bonus)
        {
            var previous=new HashSet<int>(hand.Select(c=>c.instanceId));Draw(count,false);foreach(var soul in hand.Where(c=>IsSoul(c)&&!previous.Contains(c.instanceId)))AddTemporaryDamageBonus(soul,bonus);
        }
        private void AddTemporaryDamageBonus(CardDef card,int amount)
        {
            if(card==null||amount==0)return;var index=memory.temporaryDamageIds.IndexOf(card.instanceId);if(index<0){memory.temporaryDamageIds.Add(card.instanceId);memory.temporaryDamageValues.Add(amount);}else memory.temporaryDamageValues[index]+=amount;
        }
        private int TemporaryDamageBonus(CardDef card){if(card==null)return 0;var index=memory.temporaryDamageIds.IndexOf(card.instanceId);return index>=0&&index<memory.temporaryDamageValues.Count?memory.temporaryDamageValues[index]:0;}

        private void AddRandomCurse(bool toHand){var pool=GameContent.Cards.Where(c=>c.origin==CardOrigin.Curse).ToArray();if(pool.Length==0)return;var card=Instance(pool[NextRandom(pool.Length)]);if(toHand)hand.Add(card);else discard.Add(card);cursePressure++;Emit(CombatEventKind.Status,1,true,card,"CURSE");}
        private void AddTemporaryCard(string id,bool toHand){var def=GameContent.Find(id);if(def==null)return;var card=Instance(def);RelicGenerated(card);if(toHand)hand.Add(card);else discard.Add(card);Emit(CombatEventKind.Status,1,true,card,"STATUS");}

        private void DrawImmediate(int count,bool normalDraw)
        {
            if(!normalDraw&&relics.Contains("stolen_hourglass")){RelicPresentationPulse("stolen_hourglass");return;}
            while(count-->0&&player.hp>0)
            {
                if(hand.Count>=12)return;
                if(draw.Count==0&&discard.Count>0){var shuffled=discard.Count;draw.AddRange(discard);discard.Clear();Shuffle(draw);Emit(CombatEventKind.Shuffle,shuffled);}if(draw.Count==0)return;
                var card=draw[^1];draw.RemoveAt(draw.Count-1);
                ShardCardDrawn(card,normalDraw);if(ShardInterceptDraw(card,normalDraw))continue;
                if(card.id=="twisted_fate"){RecordActualDraw(card,normalDraw);ExhaustCard(card);var replacement=hand.Count;draw.AddRange(hand);hand.Clear();Shuffle(draw);Draw(replacement,false);continue;}
                if(card.id=="shattered_guard"){RecordActualDraw(card,normalDraw);player.block=Math.Max(0,player.block-5);ExhaustCard(card);continue;}
                if(card.id=="spirit_scar"){RecordActualDraw(card,normalDraw);LosePlayerHp(2,false);ExhaustCard(card);continue;}
                hand.Add(card);if(memory.makeNextDrawFree>0){memory.freeThisTurnIds.Add(card.instanceId);memory.makeNextDrawFree=0;}RecordActualDraw(card,normalDraw);
                if(IsSoul(card)){memory.soulsDrawn++;if(memory.soulboundTome>0){PresentationPulse("soulbound_tome");Draw(memory.soulboundTome,false);}}
                if(card.id=="hollow"&&hand.Count>1){var candidates=hand.Where(c=>c!=card).ToArray();var chosen=candidates[NextRandom(candidates.Length)];hand.Remove(chosen);discard.Add(chosen);Emit(CombatEventKind.Discard,1,true,chosen);}
                if(card.id=="greed")goldLost+=5;if(card.id=="doom")LosePlayerHp(8,false);if(card.id=="misfortune")memory.nextCardCostPenalty++;if(card.id=="rust")memory.nextAttackPenalty+=4;if(card.id=="lost_moment")memory.nextTurnDrawPenalty++;
                if(card.origin==CardOrigin.Status&&relics.Contains("broken_shackles")&&!memory.statusRelicUsed){RelicPresentationPulse("broken_shackles");hand.Remove(card);memory.statusRelicUsed=true;ExhaustCard(card);continue;}
            }
        }
        private void RecordActualDraw(CardDef card,bool normalDraw)
        {
            // Observe the draw even if an on-draw rule immediately removes the card.
            // Generation/return-to-hand never enters this path.
            Emit(CombatEventKind.Draw,1,true,card);memory.cardsDrawnThisTurn++;
            RelicDrawn(card,normalDraw);OnRemainingDraw(card,normalDraw);
            if(phase==CombatPhase.Player&&memory.reapersCalling>0&&!memory.reapersCallingUsed&&memory.cardsDrawnThisTurn>=3)
            {memory.reapersCallingUsed=true;PresentationPulse("reapers_calling");AddSouls(memory.reapersCalling,SoulDestination.Hand);}
            if(card.origin==CardOrigin.Curse&&memory.embraceVoid>0)Draw(memory.embraceVoid,false);
        }
        private void ReturnLingeringCards(){if(memory.lingeringIds.Count==0)return;foreach(var id in memory.lingeringIds.ToArray()){var card=discard.FirstOrDefault(c=>c.instanceId==id);if(card==null)continue;discard.Remove(card);hand.Add(card);Emit(CombatEventKind.Draw,1,true,card,"LINGERING");memory.lingeringIds.Remove(id);}}
        private int BlockRetention(){if(memory.unmovable>0)return int.MaxValue;var retain=memory.retainBlock;if(relics.Contains("immortal_plate"))retain=Math.Max(retain,8);if(activeShardId=="overflow")retain=Math.Max(retain,activeShardFractured?int.MaxValue:15);return RelicRetainedBlock(retain);}
        private IReadOnlyList<string> BuffOptions(){var options=new List<string>();if(player.strength>0)options.Add("STRENGTH");if(player.fortify>0)options.Add("FORTIFY");if(retaliation>0)options.Add("RETALIATE");return options;}
        private void DrawMatching(CardKind kind)
        {
            if(relics.Contains("stolen_hourglass")){RelicPresentationPulse("stolen_hourglass");return;}if(draw.Count==0&&discard.Count>0){draw.AddRange(discard);discard.Clear();Shuffle(draw);Emit(CombatEventKind.Shuffle,draw.Count);}var index=draw.FindLastIndex(c=>c.kind==kind);if(index<0)return;var card=draw[index];draw.RemoveAt(index);draw.Add(card);Draw(1,false);
        }
        private int BuffCount(){var count=0;if(player.strength>0)count++;if(player.fortify>0)count++;if(player.block>0)count++;return count+memory.powersInPlay;}
        private int EnemyDebuffCount()=>DebuffNames(enemy).Count();
        private int IncrementFateful(int id){var index=memory.fatefulIds.IndexOf(id);if(index<0){memory.fatefulIds.Add(id);memory.fatefulCounts.Add(1);return 1;}return ++memory.fatefulCounts[index];}
        private void ApplySpecialBase(CardDef card,ref int value,ref int secondary,bool heavy)
        {
            if(card.specialModification=="perfected"&&card.upgraded)value=(int)Math.Ceiling(value*1.25f);
            if(card.specialModification=="chained"&&((card.kind==CardKind.Attack&&memory.previousWasSkill)||(card.kind==CardKind.Skill&&memory.lastCardWasAttack)))value+=4;
            if(card.specialModification=="deepened_power")value++;
            if(card.specialModification.StartsWith("focused",StringComparison.Ordinal))
            {
                var split=card.specialModification.Split(':');if(split.Length==1||string.Equals(split[1],card.effect.ToString(),StringComparison.OrdinalIgnoreCase))value++;else secondary++;
            }
        }
        private int NextRandom(int max){var x=memory.randomState;x^=x<<13;x^=x>>17;x^=x<<5;memory.randomState=x;return max<=1?0:(int)(x%(uint)max);}
        private static int CeilPercent(int amount,int percent)=>(int)Math.Ceiling(amount*percent/100f);
        private void Shuffle<T>(IList<T> list){for(var i=list.Count-1;i>0;i--){var j=NextRandom(i+1);(list[i],list[j])=(list[j],list[i]);}}

        public bool ActivateShard(FateShardDef shard,bool fractured)
        {
            if(shard==null||pendingPlay!=null||IsOver||phase!=CombatPhase.Player||!string.IsNullOrEmpty(activeShardId))return false;activeShardId=shard.id;activeShardFractured=fractured;shardMemory=new();ShardPulse();ShardStartup(true);return true;
        }

        private string SpecialIntentDetail(){if(enemyId=="vault_rat")return $"Deals {IntentDisplayValue} damage and steals 4 Gold.";if(enemyId=="rune_mage")return $"Deals {IntentDisplayValue} damage and applies Vulnerable.";if(enemyId=="executioner")return $"Deals {IntentDisplayValue} damage, then gains 1 Strength (maximum 3).";if(enemyId=="mirror_witch")return memory.lastCardWasAttack?$"Reflects at least {IntentDisplayValue} damage.":"Copies your Skill as Block and gains Strength.";if(enemyId=="collector")return $"Deals {IntentDisplayValue} damage and seizes Gold.";if(enemyId=="hollow_king")return $"Summons a weapon and gains {IntentDisplayValue} Block.";if(enemyId=="vault_mother")return $"Gains {IntentDisplayValue} Block and restores {3+bossPhase*2} HP.";if(enemyId=="last_dealer")return $"Deals {IntentDisplayValue} damage and adds a Curse.";return $"A special action with power {IntentDisplayValue}.";}

        private void PlanIntent()
        {
            RefreshEnemyState();intentHits=1;
            switch(enemyId)
            {
                case "vault_rat":SetIntent(turn%3==0?IntentKind.Special:IntentKind.Attack,turn%3==0?enemyBaseDamage-2:enemyBaseDamage,turn%3==0?"PILFER":"RUSTED BITE");mechanicTitle="OPPORTUNIST";mechanicText="Pilfer deals damage and steals 4 Gold.";break;
                case "gilded_sentry":SetIntent(turn%2==1?IntentKind.Defend:IntentKind.Attack,turn%2==1?10:enemyBaseDamage,turn%2==1?"FORTIFY":"HALBERD SWEEP");mechanicTitle="CLOCKWORK GUARD";mechanicText="Alternates armor and a committed strike.";break;
                case "masked_acolyte":SetIntent(turn%3==0?IntentKind.Debuff:IntentKind.Attack,turn%3==0?2:enemyBaseDamage,turn%3==0?"WITHERING RITE":"RITUAL KNIFE");mechanicTitle="THREE-BEAT RITE";mechanicText="Every third turn applies 2 Vulnerable.";break;
                case "ash_hound":SetIntent(turn%3==0?IntentKind.Buff:IntentKind.Attack,turn%3==0?2:enemyBaseDamage,turn%3==0?"STOKE FURNACE":"ASHEN MAUL");mechanicTitle="FURNACE HEART";mechanicText="Gains 2 Strength every third turn.";break;
                case "coin_mimic":SetIntent(turn%3==1?IntentKind.Buff:turn%3==2?IntentKind.Attack:IntentKind.Defend,turn%3==1?2:turn%3==2?enemyBaseDamage+4:8,turn%3==1?"FALSE GLITTER":turn%3==2?"SNAP SHUT":"HARDEN SHELL");mechanicTitle="BAITED TREASURE";mechanicText="Glitters, bites hard, then seals its shell.";break;
                case "broken_knight":SetIntent(turn%2==0?IntentKind.Attack:IntentKind.Defend,turn%2==0?enemyBaseDamage+3:9,turn%2==0?"OATHBREAKER":"RAISE SHIELD");mechanicTitle="BROKEN OATH";mechanicText="Alternates a shield with a heavier attack.";break;
                case "rune_mage":SetIntent(turn%3==0?IntentKind.Special:IntentKind.Attack,turn%3==0?enemyBaseDamage-2:enemyBaseDamage,turn%3==0?"FRACTURE RUNE":"GLYPH BOLT");mechanicTitle="LIVING SCRIPT";mechanicText="Fracture Rune damages and applies Vulnerable.";break;
                case "vault_spider":SetIntent(turn%3==1?IntentKind.Debuff:IntentKind.Attack,turn%3==1?2:enemyBaseDamage,turn%3==1?"SILVER WEB":"FANGS");mechanicTitle="BINDING WEB";mechanicText="Its web applies 2 Weak before the fangs land.";break;
                case "golden_wisp":SetIntent(turn%2==1?IntentKind.Buff:IntentKind.Attack,turn%2==1?2:enemyBaseDamage,turn%2==1?"KINDLE":"SOLAR SPARK");mechanicTitle="ESCALATING LIGHT";mechanicText="Kindle permanently increases future sparks.";break;
                case "chained_brute":SetIntent(turn%3==0?IntentKind.Attack:IntentKind.Buff,turn%3==0?enemyBaseDamage+7:1,turn%3==0?"BREAK CHAINS":"HEAVE");mechanicTitle="COUNTDOWN";mechanicText="Two Strength gains telegraph a crushing blow.";break;
                case "executioner":var sentence=(turn-1)%3;if(sentence==0)SetIntent(IntentKind.Attack,12,"BLOODEDGE");else if(sentence==1)SetIntent(IntentKind.Special,7,"BLOODLUST CUT");else SetIntent(IntentKind.Attack,18,"EXECUTION");mechanicTitle="THREE-BEAT SENTENCE";mechanicText="12 damage → 7 damage and +1 Strength → 18 damage. Strength is capped at 3.";break;
                case "mirror_witch":SetIntent(turn%3==0?IntentKind.Special:turn%2==0?IntentKind.Defend:IntentKind.Attack,turn%3==0?Math.Max(enemyBaseDamage,memory.lastCardValue):turn%2==0?14:enemyBaseDamage,turn%3==0?"CRUEL REFLECTION":turn%2==0?"MIRROR WARD":"GLASS LANCE");mechanicTitle="MIRROR MEMORY";mechanicText="She remembers the last card you resolved.";break;
                case "golden_beast":SetIntent(turn%3==0?IntentKind.Buff:IntentKind.Attack,turn%3==0?3:enemyBaseDamage+turn/2,turn%3==0?"GOLDEN FRENZY":"RAVAGE");mechanicTitle="ACCELERANDO";mechanicText="Ravage grows stronger over time.";break;
                case "collector":SetIntent(turn%3==0?IntentKind.Special:IntentKind.Attack,enemyBaseDamage,turn%3==0?"COLLECT DUE":"TITHE BLADE");mechanicTitle="SEIZED ASSETS";mechanicText=$"Gold held: {stolenGold}.";break;
                case "hollow_king":if(turn%(5-bossPhase)==0)SetIntent(IntentKind.Special,8+bossPhase*3,"ROYAL MUSTER");else if(turn%3==0)SetIntent(IntentKind.Defend,12+bossPhase*4,"CROWN GUARD");else{var hits=Math.Max(1,spectralWeapons);SetIntent(IntentKind.Attack,enemyBaseDamage/hits+bossPhase,hits==1?"SPECTRAL BLADE":"PHANTOM ARSENAL",hits);}mechanicTitle=$"PHASE {bossPhase} · SPECTRAL ARMORY";mechanicText=$"{spectralWeapons} spectral weapons strike separately.";break;
                case "vault_mother":vaultWards=0;if(turn%3==0)SetIntent(IntentKind.Special,12+bossPhase*4,"ROOTED REGENERATION");else if(turn%2==0)SetIntent(IntentKind.Defend,16+bossPhase*5,"LIVING BULWARK");else SetIntent(IntentKind.Attack,enemyBaseDamage+bossPhase*2,"VAULT CRUSH");mechanicTitle=$"PHASE {bossPhase} · VAULTBLOOM";mechanicText="Builds visible Block and restores health every third turn. It never reduces damage behind the health bar.";break;
                case "last_dealer":if(turn%3==0)SetIntent(IntentKind.Special,enemyBaseDamage/2+bossPhase*2,bossPhase==3?"BLACK HAND":"DEAL MISFORTUNE");else if(turn%2==0)SetIntent(IntentKind.Debuff,2,"MARKED CARD");else SetIntent(IntentKind.Attack,enemyBaseDamage+bossPhase*2,"HOUSE EDGE");mechanicTitle=$"PHASE {bossPhase} · STACKED DECK";mechanicText=$"{cursePressure} curses dealt · {banishedCards} cards banished.";break;
                default:SetIntent(turn%3==0?IntentKind.Defend:IntentKind.Attack,turn%3==0?7:enemyBaseDamage,turn%3==0?"BRACE":"STRIKE");break;
            }
        }
        private void SetIntent(IntentKind kind,int value,string label,int hits=1){intent=kind;intentValue=Math.Max(0,value);intentLabel=label;intentHits=Math.Max(1,hits);}
        public void RefreshEnemyState(){var next=enemy.hp>enemy.maxHp*2/3?1:enemy.hp>enemy.maxHp/3?2:3;if(enemy.hp<=0)return;bossPhase=next;while(memory.resolvedBossPhase<next){memory.resolvedBossPhase++;ApplyBossPhaseEntry(memory.resolvedBossPhase);}}
        private void ApplyBossPhaseEntry(int next){if(enemyId=="hollow_king"){spectralWeapons=next==2?2:3;enemy.block+=next==2?14:18;if(next==3)enemy.strength+=2;}else if(enemyId=="vault_mother"){vaultWards=0;enemy.block+=next==2?18:24;if(next==3)enemy.strength++;}else if(enemyId=="last_dealer"){for(var i=0;i<(next==2?2:3);i++)AddRandomCurse(false);if(next==3)memory.nextTurnEnergyPenalty=1;}RefreshMechanicTelemetry();}
        private void BanishDiscardedCard(){for(var i=discard.Count-1;i>=0;i--){if(discard[i].origin==CardOrigin.Curse)continue;var card=discard[i];discard.RemoveAt(i);ExhaustCard(card);banishedCards++;return;}}
        private void RefreshMechanicTelemetry(){if(enemyId=="hollow_king")mechanicText=$"{spectralWeapons} spectral weapons strike separately.";else if(enemyId=="vault_mother"){vaultWards=0;mechanicText="Only the visible blue Block bar prevents damage; every third turn restores health.";}else if(enemyId=="last_dealer")mechanicText=$"{cursePressure} curses dealt · {banishedCards} cards banished.";else if(enemyId=="collector")mechanicText=$"Gold held: {stolenGold}.";}
        private void ApplyPlayerDebuff(EffectKind kind,int amount){if(amount<=0)return;if(kind==EffectKind.Weak)player.weak+=amount;else if(kind==EffectKind.Vulnerable)player.vulnerable+=amount;else if(kind==EffectKind.Burn)player.burn+=amount;Emit(CombatEventKind.Status,amount,true,null,kind.ToString().ToUpperInvariant());}
    }
}
