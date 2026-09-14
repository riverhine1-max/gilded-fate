using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;

namespace GildedFate.Map
{
    public enum RunStage { Map, Combat, Merchant, Sanctuary, Event, EventSelection, EventResult, Treasure, CardReward, RelicReward, CardUpgrade, CardRemove, BindingSelect, BindingCard, Fateweave, FateweaveCard }
    [Serializable]
    public sealed class RunCard
    {
        public string persistentId="",cardId="",specialModification="";
        public bool upgraded,firstDrawFree,perfected;
        public int perfectedGrowth,perfectedCostReduction;
        public SpecialModificationKind specialModificationKind;
        public int permanentDamageBonus,permanentBlockBonus;

        public CardDef BuildDefinition()
        {
            var definition=GameContent.Find(cardId);if(definition==null)return null;var card=upgraded?GameContent.Upgrade(definition):definition.Copy();
            card.perfected=perfected;card.perfectedGrowth=perfectedGrowth;card.perfectedCostReduction=perfectedCostReduction;card.persistentId=persistentId;card.specialModification=specialModification;card.specialModificationKind=specialModificationKind;
            card.permanentDamageBonus=permanentDamageBonus;card.permanentBlockBonus=permanentBlockBonus;card.firstDrawFree=firstDrawFree;return card;
        }
    }

    [Serializable]
    public sealed class FateShardState
    {
        public string id="";public int uses,slot=-1;public bool active,activeFractured;
        public bool Fractured=>uses>=2;
        public bool CanActivate=>!active&&uses<3;
        public int RemainingUses=>Math.Max(0,3-uses);
    }
    [Serializable] public sealed class MapNode
    {
        public int floor,lane,nextMask;
        // Authored once from the run seed and saved with the route. This subtle
        // horizontal drift keeps each ascent organic without moving hit targets
        // between saves or changing which paths are connected.
        public float xOffset;
        public NodeKind kind;
        public bool complete,available,revealed;
    }
    [Serializable]
    public sealed class TemporaryEventEffect
    {
        public string id="",cardPersistentId="";
        public int value,combatsRemaining;
    }
    [Serializable]
    public sealed partial class RunModel
    {
        public const int FloorCount=18;
        public int ActFloorCount=>nodes!=null&&nodes.Count>0?nodes.Max(n=>n.floor)+1:FloorCount;
        public const int LaneCount=5;
        public int seed;
        public bool immortalThreadUsed;
        public int saveFormat=4;
        public string runId="";
        public bool closed,hasCombatCheckpoint;
        public CombatCheckpoint combatCheckpoint;
        public HeroId hero; public int act=1,floor,gold=75,hp,maxHp,nextCardSerial,pendingCombatGold,pendingBonusCardRewards; public float elapsedSeconds; public bool beggarFavor,merchantRemoved,merchantHealed,combatGoldClaimed=true; public RunStage stage; public int activeNodeFloor=-1,activeNodeLane=-1; public string activeEnemyId="",activeEventId="";
        // cards is authoritative. deck/upgradedCards/consumables remain only as a
        // migration bridge for version-2 saves and are synchronized on save.
        public List<RunCard> cards=new();
        public List<FateShardState> shards=new();
        public List<string> fateweaveSelections=new(),temporaryMultiCombatStatuses=new(),fateweaveOffers=new(),bindingOffers=new(),pendingCardOfferIds=new(),pendingSelectedCardIds=new();
        public List<string> seenEventIds=new(),pendingEventOfferIds=new(),pendingEventSelectionIds=new(),pendingEventShardDecisions=new();
        public List<TemporaryEventEffect> temporaryEventEffects=new();
        public EventSelectionKind eventSelectionKind;
        public string pendingEventChoiceId="",pendingEventBindingId="",pendingEventResult="";
        public int pendingEventChoicesNeeded;
        public string pendingFateweaveId="",pendingBindingId="";public int pendingChoicesNeeded;
        public List<string> deck=new(), upgradedCards=new(), relics=new(), consumables=new(), merchantSold=new(); public List<MapNode> nodes=new();
        public void NewRun(HeroId selected, int seed)
        {
            perfectedSelectionPending=false;perfectedSelectionStep=0;ResetRewardState();ResetMerchantStock();this.seed=seed;hero=selected;runId=Guid.NewGuid().ToString("N");saveFormat=4;closed=false;ClearCombatCheckpoint();immortalThreadUsed=false; maxHp=hp=selected==HeroId.Vanguard?80:selected==HeroId.Hexer?68:72; var rng=new Random(seed); nodes.Clear();
            BuildMap(rng);
            cards.Clear();deck.Clear();upgradedCards.Clear();nextCardSerial=0;var strike=selected==HeroId.Vanguard?"strike":selected==HeroId.Hexer?"hex_strike":"scythe_strike";var defend=selected==HeroId.Vanguard?"defend":selected==HeroId.Hexer?"ward":"deaths_veil";
            for(var i=0;i<4;i++){AddCard(strike);AddCard(defend);}AddCard(selected==HeroId.Vanguard?"battle_cry":selected==HeroId.Hexer?"invocation":"soul_call");AddCard(selected==HeroId.Vanguard?"stand_firm":selected==HeroId.Hexer?"first_ritual":"reaping_blow");
            relics.Clear();relics.Add(selected==HeroId.Vanguard?"gilded_buckle":selected==HeroId.Hexer?"cracked_prism":"deaths_keepsake");shards.Clear();fateweaveSelections.Clear();temporaryMultiCombatStatuses.Clear();temporaryEventEffects.Clear();seenEventIds.Clear();pendingEventOfferIds.Clear();pendingEventSelectionIds.Clear();pendingEventShardDecisions.Clear();eventSelectionKind=EventSelectionKind.None;pendingEventChoiceId=pendingEventBindingId=pendingEventResult="";pendingEventChoicesNeeded=0;fateweaveOffers.Clear();bindingOffers.Clear();pendingCardOfferIds.Clear();pendingSelectedCardIds.Clear();pendingFateweaveId=pendingBindingId="";pendingChoicesNeeded=0;consumables.Clear();merchantSold.Clear();act=1;floor=0;gold=75;pendingCombatGold=pendingBonusCardRewards=0;combatGoldClaimed=true;elapsedSeconds=0;beggarFavor=merchantRemoved=merchantHealed=false;stage=RunStage.Map;activeNodeFloor=activeNodeLane=-1;activeEnemyId=activeEventId="";
        }
        public RunCard AddCard(string cardId,bool upgraded=false)
        {
            if(GameContent.Find(cardId)==null)return null;var card=new RunCard{persistentId=runId+":"+(++nextCardSerial),cardId=cardId,upgraded=upgraded};cards.Add(card);SyncLegacyDeck();return card;
        }
        public RunCard DuplicateCard(RunCard source)
        {
            if(source==null||!cards.Contains(source))return null;var copy=new RunCard{persistentId=runId+":"+(++nextCardSerial),cardId=source.cardId,upgraded=source.upgraded,firstDrawFree=source.firstDrawFree,specialModification=source.specialModification,specialModificationKind=source.specialModificationKind,permanentDamageBonus=source.permanentDamageBonus,permanentBlockBonus=source.permanentBlockBonus,perfected=source.perfected,perfectedGrowth=source.perfectedGrowth,perfectedCostReduction=source.perfectedCostReduction};cards.Add(copy);SyncLegacyDeck();return copy;
        }
        public bool RemoveCard(RunCard card){if(card==null||!cards.Remove(card))return false;SyncLegacyDeck();return true;}
        public bool UpgradeCard(RunCard card){if(card==null||!cards.Contains(card)||card.upgraded||GameContent.Find(card.cardId)?.rarity is Rarity.Curse or Rarity.Status or Rarity.Special)return false;card.upgraded=true;SyncLegacyDeck();return true;}
        public bool ApplySpecialModification(RunCard card,SpecialModificationKind kind,string id,int damageBonus=0,int blockBonus=0,bool firstDrawFree=false)
        {
            if(card==null||!cards.Contains(card)||kind==SpecialModificationKind.None||string.IsNullOrWhiteSpace(id)||card.specialModificationKind!=SpecialModificationKind.None)return false;
            card.specialModificationKind=kind;card.specialModification=id;card.permanentDamageBonus=damageBonus;card.permanentBlockBonus=blockBonus;card.firstDrawFree=firstDrawFree;return true;
        }
        public void EnsureCardInstances()
        {
            cards??=new List<RunCard>();deck??=new List<string>();upgradedCards??=new List<string>();
            if(cards.Count>0){nextCardSerial=Math.Max(nextCardSerial,cards.Count);SyncLegacyDeck();return;}
            var remaining=upgradedCards.GroupBy(id=>id).ToDictionary(g=>g.Key,g=>g.Count());foreach(var id in deck)
            {
                if(GameContent.Find(id)==null)continue;var upgraded=remaining.TryGetValue(id,out var count)&&count>0;if(upgraded)remaining[id]=count-1;AddCard(id,upgraded);
            }
            if(cards.Count==0){var strike=hero==HeroId.Vanguard?"strike":hero==HeroId.Hexer?"hex_strike":"scythe_strike";var defend=hero==HeroId.Vanguard?"defend":hero==HeroId.Hexer?"ward":"deaths_veil";for(var i=0;i<5;i++){AddCard(strike);AddCard(defend);}}
            SyncLegacyDeck();
        }
        public void SyncLegacyDeck(){deck??=new List<string>();upgradedCards??=new List<string>();deck.Clear();upgradedCards.Clear();foreach(var card in cards){deck.Add(card.cardId);if(card.upgraded)upgradedCards.Add(card.cardId);}}
        public int UpgradeCount(string cardId){EnsureCardInstances();return cards.Count(c=>c.cardId==cardId&&c.upgraded);}
        public void ClearCombatCheckpoint(){hasCombatCheckpoint=false;combatCheckpoint=null;}
        public bool AcquireRelic(string id)
        {
            if(GameContent.Relics.All(r=>r.id!=id)||relics.Contains(id))return false;relics.Add(id);if(id=="perfected_thread"){perfectedSelectionPending=true;perfectedSelectionStep=0;NormalizePerfectedSelection();}return true;
        }
        public bool AddShard(string id)
        {
            if(shards.Count>=ShardCapacity||WorldContent.FateShards.All(s=>s.id!=id))return false;EnsureShardSlots();var slot=Enumerable.Range(0,ShardCapacity).First(i=>shards.All(s=>s.slot!=i));shards.Add(new FateShardState{id=id,slot=slot});return true;
        }
        public void FinishActiveShard()
        {
            for(var index=shards.Count-1;index>=0;index--){var shard=shards[index];if(!shard.active)continue;if(shard.uses>=3)shards.RemoveAt(index);else{shard.active=false;shard.activeFractured=false;}}
        }
        public void BeginBindingChoice()
        {
            var available=WorldContent.Bindings.Where(b=>b.minimumAct<=act&&cards.Any(c=>c.specialModificationKind==SpecialModificationKind.None&&BindingEligible(b,c.BuildDefinition()))).ToList();bindingOffers=PickIds(available.Select(b=>b.id),3,seed^act*17041^floor*313);pendingBindingId="";pendingSelectedCardIds.Clear();stage=RunStage.BindingSelect;
        }
        public bool SelectBinding(string id)
        {
            var binding=WorldContent.Bindings.FirstOrDefault(b=>b.id==id);if(binding==null||!bindingOffers.Contains(id))return false;pendingBindingId=id;pendingChoicesNeeded=1;pendingSelectedCardIds.Clear();stage=RunStage.BindingCard;return true;
        }
        public IEnumerable<RunCard> BindingEligibleCards()
        {
            var binding=WorldContent.Bindings.FirstOrDefault(b=>b.id==pendingBindingId);return binding==null?Enumerable.Empty<RunCard>():cards.Where(c=>c.specialModificationKind==SpecialModificationKind.None&&BindingEligible(binding,c.BuildDefinition()));
        }
        public bool ApplyPendingBinding(RunCard card)
        {
            var binding=WorldContent.Bindings.FirstOrDefault(b=>b.id==pendingBindingId);if(binding==null||!BindingEligibleCards().Contains(card))return false;if(!ApplySpecialModification(card,SpecialModificationKind.Binding,binding.id))return false;pendingBindingId="";pendingChoicesNeeded=0;bindingOffers.Clear();return true;
        }
        public void BeginFateweave()
        {
            hp=maxHp;fateweaveOffers=PickIds(WorldContent.Fateweaves.Where(f=>f.act==act&&FateweaveViable(f)).Select(f=>f.id),3,seed^act*7919);pendingFateweaveId="";pendingCardOfferIds.Clear();pendingSelectedCardIds.Clear();pendingChoicesNeeded=0;stage=RunStage.Fateweave;
        }
        public bool SelectFateweave(string id)
        {
            var fate=WorldContent.Fateweaves.FirstOrDefault(f=>f.id==id);if(fate==null||fate.act!=act||!fateweaveOffers.Contains(id))return false;pendingFateweaveId=id;fateweaveSelections.Add(id);pendingSelectedCardIds.Clear();pendingCardOfferIds.Clear();
            switch(id)
            {
                case "foreign_memory":OfferOtherCharacterCards();break;case "wanderers_thread":OfferCards(CardOrigin.Wanderer,false);break;case "favorable_hand":OfferCards(OwnOrigin(),false);break;case "entangled_fates":OfferCards(OtherOrigin(),true);break;case "stolen_destiny":OfferRareDestinies();break;
                case "gilded_cache":GrantRandomRelic(Rarity.Common,1);CompleteFateweaveEffect();break;
                case "burdened_fortune":GrantRandomRelic(Rarity.Common,2);AddCard(hero==HeroId.Vanguard?"strike":hero==HeroId.Hexer?"hex_strike":"scythe_strike");AddCard(hero==HeroId.Vanguard?"defend":hero==HeroId.Hexer?"ward":"deaths_veil");CompleteFateweaveEffect();break;
                case "heavy_crown":GrantRandomRelic(Rarity.Uncommon,1);temporaryMultiCombatStatuses.Add("dazed_mind:2:3");CompleteFateweaveEffect();break;
                case "fortunes_burden":GrantRandomRelic(Rarity.Rare,1);AddCard(hero==HeroId.Vanguard?"strike":hero==HeroId.Hexer?"hex_strike":"scythe_strike");AddCard(hero==HeroId.Vanguard?"defend":hero==HeroId.Hexer?"ward":"deaths_veil");var curses=GameContent.Cards.Where(c=>c.origin==CardOrigin.Curse).ToArray();AddCard(curses[Math.Abs(seed^act*43)%curses.Length].id);CompleteFateweaveEffect();break;
                default:pendingChoicesNeeded=id switch{"severed_burden"=>2,"gilded_edge"=>2,"gilded_guard"=>2,"first_light"=>3,"deepened_power"=>2,"perfected_edge"=>1,"perfected_guard"=>1,"golden_echo"=>1,"unbound_thread"=>2,_=>0};stage=RunStage.FateweaveCard;break;
            }
            return true;
        }
        public IEnumerable<RunCard> FateweaveEligibleCards()
        {
            var candidates=cards.Where(c=>!pendingSelectedCardIds.Contains(c.persistentId));if(pendingFateweaveId=="severed_burden")return candidates;
            candidates=candidates.Where(c=>c.specialModificationKind==SpecialModificationKind.None);return candidates.Where(c=>
            {
                var d=c.BuildDefinition();if(d==null)return false;return pendingFateweaveId switch
                {
                    "gilded_edge"=>d.kind==CardKind.Attack,"gilded_guard"=>d.kind==CardKind.Skill&&d.effect==EffectKind.Block,"first_light"=>d.cost is 1 or 2,
                    "deepened_power"=>d.effect is EffectKind.Strength or EffectKind.Fortify or EffectKind.Burn or EffectKind.Mark or EffectKind.Vulnerable or EffectKind.Weak,
                    "perfected_edge"=>d.kind==CardKind.Attack,"perfected_guard"=>d.kind==CardKind.Skill&&d.effect==EffectKind.Block,"golden_echo"=>d.kind is CardKind.Attack or CardKind.Skill&&d.cost<=2,"unbound_thread"=>d.cost>=2,_=>false
                };
            });
        }
        public bool ChooseFateweaveCard(RunCard card)
        {
            if(card==null||!FateweaveEligibleCards().Contains(card)||pendingChoicesNeeded<=0)return false;pendingSelectedCardIds.Add(card.persistentId);
            switch(pendingFateweaveId)
            {
                case "severed_burden":cards.Remove(card);break;case "gilded_edge":ApplySpecialModification(card,SpecialModificationKind.Fateweave,"gilded_edge",4);break;case "gilded_guard":ApplySpecialModification(card,SpecialModificationKind.Fateweave,"gilded_guard",0,5);break;
                case "first_light":ApplySpecialModification(card,SpecialModificationKind.Fateweave,"first_light",firstDrawFree:true);break;case "deepened_power":ApplySpecialModification(card,SpecialModificationKind.Fateweave,"deepened_power");break;
                case "perfected_edge":var attack=card.BuildDefinition();ApplySpecialModification(card,SpecialModificationKind.Fateweave,"perfected_edge",(int)Math.Ceiling(attack.value*.5f));break;
                case "perfected_guard":var block=card.BuildDefinition();ApplySpecialModification(card,SpecialModificationKind.Fateweave,"perfected_guard",0,(int)Math.Ceiling(block.value*.5f));break;
                case "golden_echo":ApplySpecialModification(card,SpecialModificationKind.Fateweave,"golden_echo");break;case "unbound_thread":ApplySpecialModification(card,SpecialModificationKind.Fateweave,"unbound_thread",firstDrawFree:true);break;
            }
            pendingChoicesNeeded--;if(pendingChoicesNeeded==0){if(pendingFateweaveId=="severed_burden")hp=Math.Max(1,hp-16);CompleteFateweaveEffect();}SyncLegacyDeck();return true;
        }
        public bool ChooseFateweaveReward(string cardId)
        {
            if(pendingChoicesNeeded!=1||!pendingCardOfferIds.Contains(cardId)||GameContent.Find(cardId)==null)return false;AddCard(cardId,pendingFateweaveId=="entangled_fates");pendingChoicesNeeded=0;CompleteFateweaveEffect();return true;
        }
        public void BeginNextAct()
        {
            act++;floor=0;nodes.Clear();BuildMap(new Random(seed^act*104729));stage=RunStage.Map;activeNodeFloor=activeNodeLane=-1;activeEnemyId=activeEventId="";merchantSold.Clear();pendingFateweaveId="";pendingCardOfferIds.Clear();pendingSelectedCardIds.Clear();fateweaveOffers.Clear();
        }
        public void CompleteFateweave()
        {
            pendingFateweaveId="";pendingChoicesNeeded=0;pendingCardOfferIds.Clear();pendingSelectedCardIds.Clear();fateweaveOffers.Clear();stage=RunStage.Map;
        }
        public IEnumerable<CardDef> TemporaryCombatCards()
        {
            foreach(var encoded in temporaryMultiCombatStatuses){var parts=encoded.Split(':');if(parts.Length!=3||!int.TryParse(parts[1],out var count)||!int.TryParse(parts[2],out var combats)||combats<=0)continue;var def=GameContent.Find(parts[0]);for(var i=0;i<count&&def!=null;i++)yield return def;}
        }
        public void ConsumeTemporaryCombatStatuses()
        {
            for(var i=temporaryMultiCombatStatuses.Count-1;i>=0;i--){var parts=temporaryMultiCombatStatuses[i].Split(':');if(parts.Length!=3||!int.TryParse(parts[2],out var combats)){temporaryMultiCombatStatuses.RemoveAt(i);continue;}combats--;if(combats<=0)temporaryMultiCombatStatuses.RemoveAt(i);else temporaryMultiCombatStatuses[i]=parts[0]+":"+parts[1]+":"+combats;}
        }
        public int TemporaryEventValue(string id)=>temporaryEventEffects?.Where(e=>e!=null&&e.id==id&&e.combatsRemaining>0).Sum(e=>e.value)??0;
        public bool HasTemporaryFreeDraw(string persistentId)=>temporaryEventEffects?.Any(e=>e!=null&&e.id=="first_draw_free"&&e.cardPersistentId==persistentId&&e.combatsRemaining>0)==true;
        public void ConsumeTemporaryEventEffects()
        {
            if(temporaryEventEffects==null)return;
            for(var i=temporaryEventEffects.Count-1;i>=0;i--){var effect=temporaryEventEffects[i];if(effect==null||--effect.combatsRemaining<=0)temporaryEventEffects.RemoveAt(i);}
        }

        private void CompleteFateweaveEffect(){pendingCardOfferIds.Clear();pendingSelectedCardIds.Clear();stage=RunStage.Fateweave;}
        private void OfferCards(CardOrigin origin,bool upgraded){var pool=GameContent.Cards.Where(c=>c.origin==origin&&c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare).Select(c=>c.id);pendingCardOfferIds=PickIds(pool,3,seed^act*3571);pendingChoicesNeeded=1;stage=RunStage.FateweaveCard;}
        private void OfferOtherCharacterCards()
        {
            // Pool every other playable character together, not one fixed partner.
            // Keep the existing collectible rarities, seed, and choose-one flow.
            var pool=GameContent.Cards.Where(c=>c.hero.HasValue&&c.hero.Value!=hero&&c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare).Select(c=>c.id);
            pendingCardOfferIds=PickIds(pool,3,seed^act*3571);pendingChoicesNeeded=1;stage=RunStage.FateweaveCard;
        }
        private void OfferRareDestinies(){pendingCardOfferIds.Clear();foreach(var origin in new[]{CardOrigin.Knight,CardOrigin.Arcane,CardOrigin.Reaper,CardOrigin.Wanderer}){var pool=GameContent.Cards.Where(c=>c.origin==origin&&c.rarity==Rarity.Rare).ToArray();pendingCardOfferIds.Add(pool[Math.Abs(seed^act*97+(int)origin*31)%pool.Length].id);}pendingChoicesNeeded=1;stage=RunStage.FateweaveCard;}
        private void GrantRandomRelic(Rarity rarity,int count){var pool=GameContent.Relics.Where(r=>r.rarity==rarity&&!relics.Contains(r.id)).ToList();for(var i=0;i<count&&pool.Count>0;i++){var index=Math.Abs(seed^act*193+i*71)%pool.Count;AcquireRelic(pool[index].id);pool.RemoveAt(index);}}
        private CardOrigin OwnOrigin()=>hero==HeroId.Vanguard?CardOrigin.Knight:hero==HeroId.Hexer?CardOrigin.Arcane:CardOrigin.Reaper;private CardOrigin OtherOrigin()=>hero==HeroId.Vanguard?CardOrigin.Arcane:hero==HeroId.Hexer?CardOrigin.Reaper:CardOrigin.Knight;
        private static List<string> PickIds(IEnumerable<string> source,int count,int seed){var pool=source.Distinct().ToList();var rng=new Random(seed);var result=new List<string>();while(result.Count<count&&pool.Count>0){var index=rng.Next(pool.Count);result.Add(pool[index]);pool.RemoveAt(index);}return result;}
        private static bool BindingEligible(BindingDef binding,CardDef card)
            =>EventSystem.BindingEligible(binding,card);
        private bool FateweaveViable(FateweaveDef fate)
        {
            var open=cards.Where(c=>c.specialModificationKind==SpecialModificationKind.None).Select(c=>c.BuildDefinition()).Where(c=>c!=null).ToArray();return fate.id switch
            {
                "severed_burden"=>cards.Count>=2,"gilded_edge"=>open.Count(c=>c.kind==CardKind.Attack)>=2,"gilded_guard"=>open.Count(c=>c.kind==CardKind.Skill&&c.effect==EffectKind.Block)>=2,"first_light"=>open.Count(c=>c.cost is 1 or 2)>=3,
                "deepened_power"=>open.Count(c=>c.effect is EffectKind.Strength or EffectKind.Fortify or EffectKind.Burn or EffectKind.Mark or EffectKind.Vulnerable or EffectKind.Weak)>=2,"perfected_edge"=>open.Any(c=>c.kind==CardKind.Attack),"perfected_guard"=>open.Any(c=>c.kind==CardKind.Skill&&c.effect==EffectKind.Block),"golden_echo"=>open.Any(c=>(c.kind is CardKind.Attack or CardKind.Skill)&&c.cost<=2),"unbound_thread"=>open.Count(c=>c.cost>=2)>=2,_=>true
            };
        }
        public void AdvanceFrom(MapNode node)
        {
            ClearCombatCheckpoint();
            pendingCombatGold=0;combatGoldClaimed=true;node.complete=true;floor=node.floor+1;stage=RunStage.Map;activeNodeFloor=activeNodeLane=-1;activeEnemyId=activeEventId="";merchantSold.Clear();merchantRemoved=merchantHealed=false;if(floor>=ActFloorCount)return;
            foreach(var candidate in nodes)if(candidate.floor==floor)candidate.available=(node.nextMask&(1<<candidate.lane))!=0;
        }

        public bool HasValidMap()
        {
            if(nodes==null||nodes.Count==0)return false;
            var rows=ActFloorCount;if(rows!=16&&rows!=FloorCount)return false;
            for(var floorIndex=0;floorIndex<rows;floorIndex++)if(!nodes.Exists(n=>n.floor==floorIndex))return false;
            if(nodes.FindAll(n=>n.kind==NodeKind.Boss).Count!=1||!nodes.Exists(n=>n.floor==rows-1&&n.lane==LaneCount/2&&n.kind==NodeKind.Boss))return false;
            var keys=new HashSet<int>();
            foreach(var node in nodes)
            {
                if(node.floor<0||node.floor>=rows||node.lane<0||node.lane>=LaneCount||!keys.Add(node.floor*LaneCount+node.lane))return false;
                if(node.floor==rows-1){if(node.nextMask!=0)return false;continue;}
                if(node.nextMask==0)return false;
                for(var laneIndex=0;laneIndex<LaneCount;laneIndex++)if((node.nextMask&(1<<laneIndex))!=0&&!nodes.Exists(n=>n.floor==node.floor+1&&n.lane==laneIndex))return false;
            }
            for(var floorIndex=1;floorIndex<rows;floorIndex++)foreach(var target in nodes.FindAll(n=>n.floor==floorIndex))
                if(!nodes.Exists(n=>n.floor==floorIndex-1&&(n.nextMask&(1<<target.lane))!=0))return false;
            return true;
        }

        private void BuildMap(Random rng)
        {
            for(var floorIndex=0;floorIndex<FloorCount;floorIndex++)
            {
                if(floorIndex==FloorCount-1)
                {
                    nodes.Add(new MapNode{floor=floorIndex,lane=LaneCount/2,kind=NodeKind.Boss,xOffset=0});
                    continue;
                }
                var count=floorIndex==0?rng.Next(1,4):floorIndex==FloorCount-2?1:rng.Next(3,LaneCount+1);
                var lanes=new List<int>();for(var laneIndex=0;laneIndex<LaneCount;laneIndex++)lanes.Add(laneIndex);
                for(var index=lanes.Count-1;index>0;index--){var swap=rng.Next(index+1);(lanes[index],lanes[swap])=(lanes[swap],lanes[index]);}
                lanes.RemoveRange(count,lanes.Count-count);if(floorIndex==FloorCount-2){lanes.Clear();lanes.Add(LaneCount/2);}lanes.Sort();
                foreach(var laneIndex in lanes)nodes.Add(new MapNode{floor=floorIndex,lane=laneIndex,kind=KindFor(floorIndex,rng),available=floorIndex==0,xOffset=(float)(rng.NextDouble()*.72-.36)});
            }
            for(var floorIndex=0;floorIndex<FloorCount-1;floorIndex++)
            {
                var from=nodes.FindAll(n=>n.floor==floorIndex);var to=nodes.FindAll(n=>n.floor==floorIndex+1);
                foreach(var node in from)
                {
                    to.Sort((a,b)=>Distance(node,a).CompareTo(Distance(node,b)));
                    node.nextMask=1<<to[0].lane;
                    if(to.Count>1&&rng.NextDouble()<.64)node.nextMask|=1<<to[1].lane;
                }
                // Every visible room must belong to at least one real route.
                foreach(var target in to)if(!from.Exists(n=>(n.nextMask&(1<<target.lane))!=0))
                {
                    from.Sort((a,b)=>Distance(a,target).CompareTo(Distance(b,target)));
                    from[0].nextMask|=1<<target.lane;
                }
            }
            // Repair repeated services along actual connections, including merges.
            for(var row=1;row<FloorCount-1;row++)foreach(var node in nodes.Where(n=>n.floor==row))
            {
                if(row==FloorCount/2-1||row==FloorCount-2)continue;
                var incoming=nodes.Where(n=>n.floor==row-1&&(n.nextMask&(1<<node.lane))!=0).Select(n=>n.kind).ToArray();
                if(IsService(node.kind)&&incoming.Contains(node.kind)||row==FloorCount/2-2&&node.kind==NodeKind.Merchant||row==FloorCount-3&&node.kind==NodeKind.Sanctuary)
                    node.kind=rng.Next(2)==0?NodeKind.Event:NodeKind.Combat;
            }
        }
        private static bool IsService(NodeKind kind)=>kind is NodeKind.Merchant or NodeKind.Sanctuary or NodeKind.Treasure;

        private static float Distance(MapNode a,MapNode b)=>Math.Abs((a.lane+a.xOffset*.28f)-(b.lane+b.xOffset*.28f));
        private static NodeKind KindFor(int f,Random rng)
        {
            if(f==0)return NodeKind.Combat;
            if(f==FloorCount/2-1)return NodeKind.Merchant;
            if(f==FloorCount-2)return NodeKind.Sanctuary;
            var pool=f<3?new[]{NodeKind.Combat,NodeKind.Combat,NodeKind.Event}:new[]{NodeKind.Combat,NodeKind.Combat,NodeKind.Combat,NodeKind.Event,NodeKind.Event,NodeKind.Elite,NodeKind.Merchant,NodeKind.Treasure,NodeKind.Sanctuary};
            return pool[rng.Next(pool.Length)];
        }
    }
}
