using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Map
{
    [Serializable] public sealed class EncounterRewards
    {
        public string receipt="",relicId="",shardId="";
        public NodeKind kind;
        public List<string> cards=new();
        public List<string> bossRelics=new();
        public bool bossRelicsRolled,bossRelicClaimed,unboundDeck;
        public bool relicClaimed,shardClaimed,cardClaimed;
        public int bonusIndex,extraChoices;
    }
    public sealed partial class RunModel
    {
        public const int ShardCapacity=2;
        public EncounterRewards encounterRewards=new();
        public string activeEncounterId="",merchantShardReceipt="",merchantShardId="",pendingShardDiscoveryId="",treasureRelicClaimedReceipt="";
        public int pendingShardPrice;
        public List<string> activeEncounterEnemies=new();
        public int normalCombatsCompleted;
        public string RoomReceipt=>runId+":"+act+":"+activeNodeFloor+":"+activeNodeLane;
        public void ResetRewardState()
        {
            encounterRewards=new();activeEncounterId=merchantShardReceipt=merchantShardId=pendingShardDiscoveryId=treasureRelicClaimedReceipt="";
            activeEncounterEnemies=new();pendingShardPrice=normalCombatsCompleted=0;
        }
        private int RoomSeed(int salt)=>seed^act*104729^activeNodeFloor*397^activeNodeLane*7919^salt;
        public void PrepareEncounter(NodeKind kind)
        {
            activeEncounterEnemies=new();activeEncounterId="";
            if(kind!=NodeKind.Combat)return;
            var encounter=EncounterContent.Choose(act,activeNodeFloor+1,normalCombatsCompleted,RoomSeed(27011));
            activeEncounterId=encounter.id;activeEncounterEnemies.AddRange(encounter.enemies);activeEnemyId=encounter.enemies[0];
        }
        public void RollEncounterRewards(NodeKind kind)
        {
            if(encounterRewards?.receipt==RoomReceipt){EnsureBossRelicOffers();return;}
            var random=new Random(RoomSeed(83443));var cards=RewardRules.Cards(hero,kind,random);
            var relic=RewardRules.Relic(kind,random,relics);
            var shard=RewardRules.DropsShard(kind,random.Next(100))?WorldContent.FateShards[random.Next(WorldContent.FateShards.Length)].id:"";
            encounterRewards=new EncounterRewards{receipt=RoomReceipt,kind=kind,cards=cards.Select(c=>c.id).ToList(),relicId=relic?.id??"",shardId=shard};
            encounterRewards.unboundDeck=relics.Contains("unbound_deck");
            if(encounterRewards.unboundDeck)encounterRewards.cards=RewardRules.UnboundCards(kind,new Random(RoomSeed(52457))).Select(c=>c.id).ToList();
            EnsureBossRelicOffers();
        }
        private void EnsureBossRelicOffers()
        {
            var reward=encounterRewards;
            if(reward==null||reward.receipt!=RoomReceipt||reward.kind!=NodeKind.Boss||reward.bossRelicsRolled)return;
            reward.bossRelics=new();var random=new Random(RoomSeed(67391));
            var pool=GameContent.Relics.Where(r=>r.rarity==Rarity.Boss&&!relics.Contains(r.id)).ToList();
            while(reward.bossRelics.Count<3&&pool.Count>0){var index=random.Next(pool.Count);reward.bossRelics.Add(pool[index].id);pool.RemoveAt(index);}
            reward.bossRelicsRolled=true;
        }
        public RelicDef[] BossRelicOffers()
        {
            EnsureBossRelicOffers();
            return encounterRewards?.kind==NodeKind.Boss&&encounterRewards.receipt==RoomReceipt
                ?(encounterRewards.bossRelics??new()).Select(id=>GameContent.Relics.FirstOrDefault(r=>r.id==id)).Where(r=>r!=null).ToArray():Array.Empty<RelicDef>();
        }
        public bool ClaimBossRelic(string id)
        {
            if(encounterRewards==null||encounterRewards.receipt!=RoomReceipt||encounterRewards.kind!=NodeKind.Boss
                ||encounterRewards.bossRelicClaimed||!BossRelicOffers().Any(r=>r.id==id)||!AcquireRelic(id))return false;
            encounterRewards.bossRelicClaimed=true;return true;
        }
        public CardDef[] EncounterCardOffers(NodeKind kind)
        {
            RollEncounterRewards(kind);return encounterRewards.cards.Select(GameContent.Find).Where(c=>c!=null).Select(c=>c.Copy()).ToArray();
        }
        public void AddEncounterCardChoices(int count)
        {
            // Claim Fallen widens this single choose-one offer; it never awards
            // another card pick or rerolls the already determined relic/shard.
            count=Math.Min(count,4);
            if(count<=0||encounterRewards==null||encounterRewards.receipt!=RoomReceipt||encounterRewards.cardClaimed)return;
            var random=new Random(RoomSeed(28193));var pool=GameContent.Cards.Where(c=>c.hero==hero&&c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare).ToArray();
            for(var i=encounterRewards.extraChoices;i<count;i++)
            {
                if(encounterRewards.unboundDeck)
                {
                    var card=RewardRules.UnboundCards(encounterRewards.kind,random,1,encounterRewards.cards)[0];
                    encounterRewards.cards.Add(card.id);encounterRewards.extraChoices++;continue;
                }
                var rarity=RewardRules.CardRarity(encounterRewards.kind,random.Next(100));
                var eligible=pool.Where(c=>c.rarity==rarity&&!encounterRewards.cards.Contains(c.id)).ToArray();
                if(eligible.Length==0)eligible=pool.Where(c=>!encounterRewards.cards.Contains(c.id)).ToArray();
                if(eligible.Length==0)break;encounterRewards.cards.Add(eligible[random.Next(eligible.Length)].id);encounterRewards.extraChoices++;
            }
        }
        public void NextBonusCardReward()
        {
            encounterRewards.bonusIndex++;encounterRewards.extraChoices=0;encounterRewards.cardClaimed=false;
            encounterRewards.unboundDeck=relics.Contains("unbound_deck");
            encounterRewards.cards=(encounterRewards.unboundDeck
                ?RewardRules.UnboundCards(encounterRewards.kind,new Random(RoomSeed(52457)^encounterRewards.bonusIndex*48611))
                :RewardRules.Cards(hero,encounterRewards.kind,new Random(RoomSeed(83443)^encounterRewards.bonusIndex*48611))).Select(c=>c.id).ToList();
        }
        public bool ClaimEncounterCard(string id)
        {
            if(encounterRewards==null||encounterRewards.receipt!=RoomReceipt||encounterRewards.cardClaimed||!encounterRewards.cards.Contains(id))return false;
            AddCard(id);encounterRewards.cardClaimed=true;return true;
        }
        public void PrepareMerchantShard()
        {
            if(merchantShardReceipt==RoomReceipt)return;merchantShardReceipt=RoomReceipt;
            var random=new Random(RoomSeed(94009));merchantShardId=RewardRules.DropsShard(NodeKind.Merchant,random.Next(100))?WorldContent.FateShards[random.Next(WorldContent.FateShards.Length)].id:"";
        }
        public string RollTreasureShard()
        {
            var random=new Random(RoomSeed(45433));return RewardRules.DropsShard(NodeKind.Treasure,random.Next(100))?WorldContent.FateShards[random.Next(WorldContent.FateShards.Length)].id:"";
        }
        public bool OfferShard(string id,int price=0)
        {
            if(!string.IsNullOrEmpty(pendingShardDiscoveryId)||price<0||gold<price||WorldContent.FateShards.All(s=>s.id!=id))return false;
            pendingShardDiscoveryId=id;pendingShardPrice=price;return true;
        }
        public bool TakeDiscoveredShard(int replaceIndex=-1)
        {
            if(string.IsNullOrEmpty(pendingShardDiscoveryId)||gold<pendingShardPrice||shards.Count>ShardCapacity)return false;
            if(shards.Count==ShardCapacity&&(replaceIndex<0||replaceIndex>=shards.Count||shards[replaceIndex].active))return false;
            if(shards.Count==ShardCapacity)shards.RemoveAt(replaceIndex);
            var id=pendingShardDiscoveryId;if(!AddShard(id))return false;
            gold-=pendingShardPrice;if(pendingShardPrice>0)merchantSold.Add("shard:"+id);
            pendingShardDiscoveryId="";pendingShardPrice=0;return true;
        }
        public void DeclineDiscoveredShard(){pendingShardDiscoveryId="";pendingShardPrice=0;}
        public bool ResolveLegacyShardCapacity(int releaseIndex)
        {
            if(shards.Count<=ShardCapacity||releaseIndex<0||releaseIndex>=shards.Count||shards[releaseIndex].active)return false;
            shards.RemoveAt(releaseIndex);return true;
        }
        public void EnsureShardSlots()
        {
            var occupied=new HashSet<int>();
            foreach(var shard in shards)
            {
                if(shard.slot<0||shard.slot>=Math.Max(ShardCapacity,shards.Count)||!occupied.Add(shard.slot))
                {shard.slot=Enumerable.Range(0,Math.Max(ShardCapacity,shards.Count)).First(i=>!occupied.Contains(i));occupied.Add(shard.slot);}
            }
        }
        public void CopyRewardState(RunModel source)
        {
            CopyMerchantState(source);
            encounterRewards=source.encounterRewards??new();activeEncounterId=source.activeEncounterId??"";
            activeEncounterEnemies=source.activeEncounterEnemies??new();normalCombatsCompleted=source.normalCombatsCompleted;
            merchantShardReceipt=source.merchantShardReceipt??"";merchantShardId=source.merchantShardId??"";
            pendingShardDiscoveryId=source.pendingShardDiscoveryId??"";pendingShardPrice=source.pendingShardPrice;
            treasureRelicClaimedReceipt=source.treasureRelicClaimedReceipt??"";
        }
        public bool HasValidRewardState()
        {
            if(pendingShardPrice<0||normalCombatsCompleted<0)return false;
            if(!string.IsNullOrEmpty(pendingShardDiscoveryId)&&WorldContent.FateShards.All(s=>s.id!=pendingShardDiscoveryId))return false;
            if(!string.IsNullOrEmpty(merchantShardId)&&WorldContent.FateShards.All(s=>s.id!=merchantShardId))return false;
            if(activeEncounterEnemies?.Count>0&&(activeEncounterEnemies.Count>4||activeEncounterEnemies.Any(id=>!WorldContent.Enemies.Any(e=>e.id==id&&!e.elite&&!e.boss))))return false;
            if(encounterRewards==null||string.IsNullOrEmpty(encounterRewards.receipt))return true;
            if(encounterRewards.cards==null||encounterRewards.extraChoices<0||encounterRewards.extraChoices>4||encounterRewards.cards.Count!=3+(encounterRewards.unboundDeck?1:0)+encounterRewards.extraChoices||encounterRewards.cards.Distinct().Count()!=encounterRewards.cards.Count||encounterRewards.bonusIndex<0)return false;
            if(encounterRewards.cards.Any(id=>!GameContent.Cards.Any(c=>c.id==id&&(encounterRewards.unboundDeck
                ?c.origin is CardOrigin.Knight or CardOrigin.Arcane or CardOrigin.Reaper or CardOrigin.Wanderer:c.hero==hero)
                &&c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare)))return false;
            if(encounterRewards.kind==NodeKind.Boss&&encounterRewards.cards.Any(id=>GameContent.Find(id).rarity!=Rarity.Rare))return false;
            if(!string.IsNullOrEmpty(encounterRewards.relicId)&&!GameContent.Relics.Any(r=>r.id==encounterRewards.relicId&&r.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare))return false;
            if(!string.IsNullOrEmpty(encounterRewards.shardId)&&WorldContent.FateShards.All(s=>s.id!=encounterRewards.shardId))return false;
            var bossOffers=encounterRewards.bossRelics;
            if(bossOffers!=null&&(bossOffers.Count>3||bossOffers.Distinct().Count()!=bossOffers.Count
                ||bossOffers.Any(id=>!GameContent.Relics.Any(r=>r.id==id&&r.rarity==Rarity.Boss))))return false;
            if(encounterRewards.kind!=NodeKind.Boss&&(bossOffers?.Count>0||encounterRewards.bossRelicsRolled||encounterRewards.bossRelicClaimed))return false;
            return true;
        }
    }
}
