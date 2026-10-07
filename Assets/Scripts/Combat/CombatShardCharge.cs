using System;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Fate Shard charging (Fateshard Overhaul).
    // A shard can only be activated once the combat's single charge meter is full.
    // Every natural card play adds charge; cards matching an attuned shard's archetype add more.
    // Charge belongs to this combat only and is saved with the combat checkpoint.
    public sealed partial class CombatState
    {
        public const int ShardChargeFill=9;          // attuned shard whose archetype has matching cards
        public const int ShardChargeFillUnmatched=7; // attuned shard whose archetype has no matching card type
        public const int ShardChargeFillHold=12;     // Hold: both shards charge toward one meter
        public const int ShardChargeMatchGain=2,ShardChargeBaseGain=1;

        // Plain fields (not a list) so MemberwiseClone checkpoint copies stay independent.
        public int shardCharge;
        public string chargeShardA="",chargeShardB="";
        public bool shardAttunePending,shardHold,shardShattered;

        public int ShardChargeTarget
        {
            get
            {
                if(shardHold)return ShardChargeFillHold;
                var attuned=!string.IsNullOrEmpty(chargeShardA)?chargeShardA:chargeShardB;
                if(string.IsNullOrEmpty(attuned))return ShardChargeFill;
                var def=Array.Find(WorldContent.FateShards,s=>s.id==attuned);
                return def!=null&&!ArchetypeHasMatches(def.archetype)?ShardChargeFillUnmatched:ShardChargeFill;
            }
        }
        public bool ShardChargeFull=>shardCharge>=ShardChargeTarget;
        public float ShardChargeProgress=>Math.Min(1f,shardCharge/(float)Math.Max(1,ShardChargeTarget));

        // Called by the Attune screen. Hold keeps both shards available and uses the larger meter.
        public void AttuneShards(string first,string second,bool hold)
        {
            chargeShardA=first??"";chargeShardB=hold?second??"":"";
            shardHold=hold&&!string.IsNullOrEmpty(chargeShardA)&&!string.IsNullOrEmpty(chargeShardB);
            shardAttunePending=false;
        }
        // No attunement recorded (older checkpoints, verification) means any owned shard may use the meter.
        public bool ShardAttunedTo(string id)=>string.IsNullOrEmpty(chargeShardA)&&string.IsNullOrEmpty(chargeShardB)||id==chargeShardA||id==chargeShardB;
        public bool CanActivateChargedShard(string id)=>string.IsNullOrEmpty(activeShardId)&&!shardAttunePending&&ShardChargeFull&&ShardAttunedTo(id);

        private void GainShardCharge(CardDef card)
        {
            if(card==null||!string.IsNullOrEmpty(activeShardId)||shardAttunePending)return;
            var target=ShardChargeTarget;if(shardCharge>=target)return;
            var gain=MatchesShardArchetype(chargeShardA,card)||MatchesShardArchetype(chargeShardB,card)?ShardChargeMatchGain:ShardChargeBaseGain;
            shardCharge=Math.Min(target,shardCharge+gain);
        }

        public static bool MatchesShardArchetype(string shardId,CardDef card)
        {
            if(string.IsNullOrEmpty(shardId)||card==null)return false;
            var def=Array.Find(WorldContent.FateShards,s=>s.id==shardId);
            return def!=null&&MatchesArchetype(def.archetype,card);
        }
        public static bool ArchetypeHasMatches(string archetype)=>archetype is "Strength" or "Block" or "Attack" or "Heavy" or "Retaliate" or "Burn" or "Debuff" or "Exhaust" or "Draw" or "Energy" or "Modified";
        public static bool MatchesArchetype(string archetype,CardDef card)
        {
            if(card==null)return false;
            switch(archetype)
            {
                case "Strength":return card.effect==EffectKind.Strength;
                case "Block":return card.effect is EffectKind.Block or EffectKind.Fortify;
                case "Attack":return card.kind==CardKind.Attack;
                case "Heavy":return card.kind==CardKind.Attack&&(card.cost>=2||card.keywords!=null&&Array.IndexOf(card.keywords,"Heavy")>=0);
                case "Retaliate":return card.effect is EffectKind.Retaliate or EffectKind.Block;
                case "Burn":return card.effect==EffectKind.Burn;
                case "Debuff":return card.effect is EffectKind.Mark or EffectKind.Vulnerable or EffectKind.Weak or EffectKind.Burn;
                case "Exhaust":return card.exhaust||card.effect==EffectKind.Exhaust;
                case "Draw":return card.effect==EffectKind.Draw;
                case "Energy":return card.effect==EffectKind.Energy;
                case "Modified":return card.IsModified;
                default:return false; // Cost, Momentum, Echo, Sequence, Curse: every card charges at the base rate
            }
        }

        // Shatter early: take the Fractured power now. The run layer destroys the shard afterwards.
        public bool ShatterShard(FateShardDef shard)
        {
            if(!ActivateShard(shard,true))return false;
            shardShattered=true;return true;
        }
    }
}
