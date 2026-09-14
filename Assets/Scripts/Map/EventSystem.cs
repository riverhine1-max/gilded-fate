using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Map
{
    public readonly struct EventChoiceAvailability
    {
        public readonly bool available;
        public readonly string reason;
        public EventChoiceAvailability(bool available,string reason=""){this.available=available;this.reason=reason;}
    }

    public static class EventSystem
    {
        private static readonly EffectKind[] StackableEffects={EffectKind.Strength,EffectKind.Fortify,EffectKind.Retaliate,EffectKind.Resonance,EffectKind.Burn,EffectKind.Mark,EffectKind.Vulnerable,EffectKind.Weak};

        public static EventDefinition SelectEvent(RunModel run)
        {
            if(run==null)return null;
            run.EnsureEventState();
            var pool=EventContent.All.Where(e=>run.act>=e.minAct&&run.act<=e.maxAct&&(e.repeatable||!run.seenEventIds.Contains(e.id))&&EventViable(run,e)).ToList();
            if(pool.Count==0)pool=EventContent.All.Where(e=>run.act>=e.minAct&&run.act<=e.maxAct&&EventViable(run,e)).ToList();
            if(pool.Count==0)return EventContent.All.First();
            var total=pool.Sum(e=>(int)e.frequency);var roll=PositiveHash(run.seed,run.act,run.floor,7919)%Math.Max(1,total);
            foreach(var candidate in pool){roll-=(int)candidate.frequency;if(roll<0)return candidate;}
            return pool[0];
        }

        public static bool BeginEvent(RunModel run,EventDefinition definition)
        {
            if(run==null||definition==null)return false;
            run.EnsureEventState();ClearPending(run,false);run.activeEventId=definition.id;
            if(!definition.repeatable&&!run.seenEventIds.Contains(definition.id))run.seenEventIds.Add(definition.id);
            run.stage=RunStage.Event;return true;
        }

        public static EventChoiceAvailability Availability(RunModel run,EventChoiceDef choice)
        {
            if(run==null||choice==null)return new(false,"Choice unavailable");
            foreach(var effect in choice.effects)
            {
                if(effect.kind==EventEffectKind.Gold&&EffectiveGoldAmount(run,effect)<0&&run.gold<-EffectiveGoldAmount(run,effect))return new(false,"Requires "+(-EffectiveGoldAmount(run,effect))+" Gold");
                if(effect.kind==EventEffectKind.LoseHp&&effect.amount<0&&run.hp+effect.amount<1)return new(false,"Requires more than "+(-effect.amount)+" HP");
                if(effect.kind==EventEffectKind.Heal&&run.hp>=run.maxHp)return new(false,"Already at full HP");
                if(effect.kind is EventEffectKind.UpgradeSelected or EventEffectKind.RemoveSelected or EventEffectKind.TransformSelected or EventEffectKind.RemoveCurse or EventEffectKind.BindSelected or EventEffectKind.DuplicateWithBinding or EventEffectKind.TemporaryFirstDrawFree)
                {
                    var count=Math.Max(1,effect.count);if(EligibleCards(run,effect).Count()<count)return new(false,CardFailureReason(effect));
                }
                if(effect.kind==EventEffectKind.UpgradeRandom&&!run.cards.Any(c=>CanUpgrade(c)))return new(false,"No eligible card can be upgraded");
                if(effect.kind==EventEffectKind.UpgradeAllBasic&&!run.cards.Any(c=>c.BuildDefinition()?.rarity==Rarity.Basic&&!c.upgraded))return new(false,"No remaining Basic card can be upgraded");
                if(effect.kind==EventEffectKind.RemoveRelic&&!RemovableRelics(run).Any())return new(false,"No removable Common Relic");
                if(effect.kind==EventEffectKind.GrantRelic&&!RelicPool(run,effect).Any())return new(false,"No unowned Relic remains in this pool");
                if(effect.kind is EventEffectKind.RepairShard or EventEffectKind.TradeShard or EventEffectKind.FractureShard)
                {
                    var shards=EligibleShards(run,effect);if(!shards.Any())return new(false,effect.kind==EventEffectKind.FractureShard?"Requires a Stable Fate Shard":"Requires an owned Fate Shard");
                }
            }
            return new(true);
        }

        public static bool BeginChoice(RunModel run,EventChoiceDef choice)
        {
            if(run==null||choice==null||EventContent.FindChoice(run.activeEventId,choice.id)==null||!Availability(run,choice).available)return false;
            ClearPending(run,false);run.pendingEventChoiceId=choice.id;
            var selector=choice.effects.FirstOrDefault(NeedsSelection);
            if(selector==null){if(PrepareFatewheelShardReplacement(run,choice)||PrepareAutomaticShardRewards(run,choice))return true;Commit(run);return true;}
            if(selector.kind==EventEffectKind.RewardCards)
            {
                run.pendingEventOfferIds=BuildCardOffers(run,selector).Select(c=>c.id).ToList();run.pendingEventChoicesNeeded=1;run.eventSelectionKind=EventSelectionKind.RewardCard;
            }
            else if(selector.kind==EventEffectKind.RewardShards)
            {
                run.pendingEventOfferIds=PickShardIds(run,Math.Max(1,selector.amount),"shard-offer");run.pendingEventChoicesNeeded=Math.Max(1,selector.count);run.eventSelectionKind=EventSelectionKind.ShardReward;
            }
            else if(selector.kind==EventEffectKind.RemoveRelic)
            {run.pendingEventChoicesNeeded=1;run.eventSelectionKind=EventSelectionKind.Relic;}
            else if(selector.kind is EventEffectKind.RepairShard or EventEffectKind.TradeShard or EventEffectKind.FractureShard)
            {run.pendingEventChoicesNeeded=1;run.eventSelectionKind=EventSelectionKind.OwnedShard;}
            else
            {run.pendingEventChoicesNeeded=Math.Max(1,selector.count);run.eventSelectionKind=EventSelectionKind.Card;}
            run.stage=RunStage.EventSelection;return true;
        }

        public static EventEffectDef PendingSelector(RunModel run)=>PendingChoice(run)?.effects.FirstOrDefault(NeedsSelection);
        public static IEnumerable<RunCard> PendingEligibleCards(RunModel run)=>EligibleCards(run,PendingSelector(run)).Where(c=>!run.pendingEventSelectionIds.Contains(c.persistentId));
        public static IEnumerable<FateShardState> PendingEligibleShards(RunModel run)=>EligibleShards(run,PendingSelector(run));
        public static IEnumerable<RelicDef> PendingEligibleRelics(RunModel run)=>RemovableRelics(run);

        public static bool ChooseCard(RunModel run,RunCard card)
        {
            if(run?.eventSelectionKind!=EventSelectionKind.Card||card==null||!PendingEligibleCards(run).Contains(card))return false;
            run.pendingEventSelectionIds.Add(card.persistentId);run.pendingEventChoicesNeeded--;
            if(run.pendingEventChoicesNeeded>0)return true;
            var effect=PendingSelector(run);
            if(effect?.kind is EventEffectKind.BindSelected or EventEffectKind.DuplicateWithBinding)
                return PrepareBinding(run,card,effect);
            Commit(run);return true;
        }

        public static bool ChooseOffer(RunModel run,string id)
        {
            if(run==null||string.IsNullOrEmpty(id))return false;
            if(run.eventSelectionKind==EventSelectionKind.RewardCard)
            {
                if(!run.pendingEventOfferIds.Contains(id))return false;run.pendingEventSelectionIds.Add(id);Commit(run);return true;
            }
            if(run.eventSelectionKind==EventSelectionKind.ShardReward)
            {
                if(!run.pendingEventOfferIds.Contains(id)||run.pendingEventSelectionIds.Contains(id))return false;run.pendingEventSelectionIds.Add(id);run.pendingEventChoicesNeeded--;
                if(run.pendingEventChoicesNeeded>0)return true;return PrepareShardReplacementOrCommit(run);
            }
            if(run.eventSelectionKind==EventSelectionKind.OwnedShard)
            {
                if(!PendingEligibleShards(run).Any(s=>s.id==id))return false;run.pendingEventSelectionIds.Add(id);Commit(run);return true;
            }
            if(run.eventSelectionKind==EventSelectionKind.Relic)
            {
                if(!RemovableRelics(run).Any(r=>r.id==id))return false;run.pendingEventSelectionIds.Add(id);Commit(run);return true;
            }
            if(run.eventSelectionKind==EventSelectionKind.Binding)
            {
                if(!run.pendingEventOfferIds.Contains(id))return false;run.pendingEventBindingId=id;
                if(id=="focused")return PrepareFocusedChoice(run);
                Commit(run);return true;
            }
            if(run.eventSelectionKind==EventSelectionKind.FocusedEffect)
            {
                if(!run.pendingEventOfferIds.Contains(id))return false;run.pendingEventBindingId="focused:"+id.ToLowerInvariant();Commit(run);return true;
            }
            return false;
        }

        public static bool ChooseShardReplacement(RunModel run,string newShardId,string replaceShardId)
        {
            if(run?.eventSelectionKind!=EventSelectionKind.ShardReplacement||!run.pendingEventOfferIds.Contains(newShardId))return false;
            if(!string.IsNullOrEmpty(replaceShardId)&&replaceShardId!="discard"&&!run.shards.Any(s=>s.id==replaceShardId))return false;
            var key=newShardId+"=>";run.pendingEventShardDecisions.RemoveAll(d=>d.StartsWith(key,StringComparison.Ordinal));run.pendingEventShardDecisions.Add(key+(string.IsNullOrEmpty(replaceShardId)?"discard":replaceShardId));
            if(run.pendingEventShardDecisions.Count>=run.pendingEventOfferIds.Count)Commit(run);return true;
        }

        public static bool CancelSelection(RunModel run)
        {
            if(run==null||run.stage!=RunStage.EventSelection)return false;ClearPending(run,false);run.stage=RunStage.Event;return true;
        }

        public static bool CompleteResult(RunModel run)
        {
            if(run==null||run.stage!=RunStage.EventResult)return false;ClearPending(run,false);return true;
        }

        public static bool BindingEligible(BindingDef binding,RunCard card)=>binding!=null&&card!=null&&card.specialModificationKind==SpecialModificationKind.None&&BindingEligible(binding,card.BuildDefinition());
        public static bool BindingEligible(BindingDef binding,CardDef card)
        {
            if(binding==null||card==null||card.rarity is Rarity.Curse or Rarity.Status)return false;
            return binding.id switch
            {
                "serrated"=>card.kind==CardKind.Attack,
                "reinforced"=>card.effect==EffectKind.Block,
                "weighted"=>card.kind==CardKind.Attack&&card.cost>=2,
                "quickened"=>card.cost>=1,
                "lingering"=>card.kind==CardKind.Skill,
                "gilded"=>card.kind is CardKind.Attack or CardKind.Skill,
                "focused"=>FocusedTargets(card).Any(),
                "chained"=>card.kind is CardKind.Attack or CardKind.Skill,
                "recurring"=>card.exhaust,
                "fateful"=>card.kind is CardKind.Attack or CardKind.Skill,
                "perfected"=>card.upgraded&&(card.kind==CardKind.Attack||card.effect==EffectKind.Block),
                _=>false
            };
        }

        public static IEnumerable<string> FocusedTargets(CardDef card)
        {
            if(card==null)return Enumerable.Empty<string>();
            var names=new List<string>();if(StackableEffects.Contains(card.effect))names.Add(card.effect.ToString());
            foreach(var keyword in card.keywords??Array.Empty<string>())if(Enum.TryParse<EffectKind>(keyword,true,out var effect)&&StackableEffects.Contains(effect)&&!names.Contains(effect.ToString()))names.Add(effect.ToString());
            return names;
        }

        private static bool PrepareBinding(RunModel run,RunCard card,EventEffectDef effect)
        {
            var compatible=CompatibleBindings(run,card,effect.id).ToList();if(compatible.Count==0)return false;
            if(effect.kind==EventEffectKind.DuplicateWithBinding||effect.id=="act_random"||effect.id.Contains('|'))
            {run.pendingEventBindingId=compatible[PositiveHash(run.seed,run.floor,card.persistentId.GetHashCode(),733)%compatible.Count].id;if(run.pendingEventBindingId=="focused"&&FocusedTargets(card.BuildDefinition()).Count()>1)return PrepareFocusedChoice(run);Commit(run);return true;}
            var reveal=effect.id.StartsWith("compatible:",StringComparison.Ordinal)?int.Parse(effect.id.Split(':')[1]):1;
            if(reveal>1)
            {
                run.pendingEventOfferIds=Pick(compatible.Select(b=>b.id),reveal,PositiveHash(run.seed,run.act,run.floor,991));run.eventSelectionKind=EventSelectionKind.Binding;run.stage=RunStage.EventSelection;return true;
            }
            run.pendingEventBindingId=compatible[0].id;if(run.pendingEventBindingId=="focused"&&FocusedTargets(card.BuildDefinition()).Count()>1)return PrepareFocusedChoice(run);Commit(run);return true;
        }

        private static bool PrepareFocusedChoice(RunModel run)
        {
            var card=run.cards.FirstOrDefault(c=>run.pendingEventSelectionIds.Contains(c.persistentId));if(card==null)return false;
            var targets=FocusedTargets(card.BuildDefinition()).ToList();if(targets.Count==0)return false;if(targets.Count==1){run.pendingEventBindingId="focused:"+targets[0].ToLowerInvariant();Commit(run);return true;}
            run.pendingEventOfferIds=targets;run.eventSelectionKind=EventSelectionKind.FocusedEffect;run.stage=RunStage.EventSelection;return true;
        }

        private static IEnumerable<BindingDef> CompatibleBindings(RunModel run,RunCard card,string rule)
        {
            var unlocked=WorldContent.Bindings.Where(b=>b.minimumAct<=run.act&&BindingEligible(b,card));
            if(rule.StartsWith("compatible:",StringComparison.Ordinal)||rule=="act_random"||string.IsNullOrEmpty(rule))return unlocked;
            var ids=rule.Split('|');return unlocked.Where(b=>ids.Contains(b.id));
        }

        private static bool PrepareAutomaticShardRewards(RunModel run,EventChoiceDef choice)
        {
            var effect=choice.effects.FirstOrDefault(e=>e.kind==EventEffectKind.GrantRandomShards);if(effect==null)return false;
            run.pendingEventOfferIds=PickShardIds(run,Math.Max(1,effect.count),"random-shards");
            if(run.shards.Count+run.pendingEventOfferIds.Count<=RunModel.ShardCapacity)return false;
            run.pendingEventChoicesNeeded=run.pendingEventOfferIds.Count;run.eventSelectionKind=EventSelectionKind.ShardReplacement;run.stage=RunStage.EventSelection;return true;
        }
        private static bool PrepareFatewheelShardReplacement(RunModel run,EventChoiceDef choice)
        {
            if(!choice.effects.Any(e=>e.kind==EventEffectKind.Fatewheel)||FatewheelOutcome(run)!=1||run.shards.Count<RunModel.ShardCapacity)return false;
            run.pendingEventOfferIds=PickShardIds(run,1,"wheel");run.pendingEventChoicesNeeded=1;run.eventSelectionKind=EventSelectionKind.ShardReplacement;run.stage=RunStage.EventSelection;return true;
        }

        private static bool PrepareShardReplacementOrCommit(RunModel run)
        {
            var newShards=run.pendingEventSelectionIds.Count>0?run.pendingEventSelectionIds.ToList():run.pendingEventOfferIds.ToList();
            var overflow=Math.Max(0,run.shards.Count+newShards.Count-RunModel.ShardCapacity);if(overflow<=0){run.pendingEventOfferIds=newShards;Commit(run);return true;}
            run.pendingEventOfferIds=newShards;run.pendingEventSelectionIds.Clear();run.pendingEventChoicesNeeded=newShards.Count;run.eventSelectionKind=EventSelectionKind.ShardReplacement;run.stage=RunStage.EventSelection;return true;
        }

        private static void Commit(RunModel run)
        {
            var choice=PendingChoice(run);if(choice==null)return;
            var selectedCards=run.pendingEventSelectionIds.Select(id=>run.cards.FirstOrDefault(c=>c.persistentId==id)).Where(c=>c!=null).ToList();
            foreach(var effect in choice.effects)
            {
                switch(effect.kind)
                {
                    case EventEffectKind.Nothing:break;
                    case EventEffectKind.Gold:run.gold=Math.Max(0,run.gold+EffectiveGoldAmount(run,effect));break;
                    case EventEffectKind.LoseHp:run.hp=Math.Max(1,run.hp+effect.amount);break;
                    case EventEffectKind.Heal:run.hp=Math.Min(run.maxHp,run.hp+effect.amount);break;
                    case EventEffectKind.MaxHp:run.maxHp+=effect.amount;run.hp+=effect.amount;break;
                    case EventEffectKind.AddCurse:run.AddCard(effect.id);break;
                    case EventEffectKind.RandomCurse:run.AddCard(PickCurse(run,"curse"));break;
                    case EventEffectKind.RemoveCurse:
                    case EventEffectKind.RemoveSelected:foreach(var selectedToRemove in selectedCards.Take(Math.Max(1,effect.count)).ToArray())run.RemoveCard(selectedToRemove);break;
                    case EventEffectKind.UpgradeSelected:foreach(var selectedToUpgrade in selectedCards.Take(Math.Max(1,effect.count)))run.UpgradeCard(selectedToUpgrade);break;
                    case EventEffectKind.TransformSelected:foreach(var selectedToTransform in selectedCards.Take(Math.Max(1,effect.count)))Transform(run,selectedToTransform);break;
                    case EventEffectKind.UpgradeRandom:UpgradeRandom(run,effect.count);break;
                    case EventEffectKind.UpgradeAllBasic:foreach(var basicToUpgrade in run.cards.Where(c=>c.BuildDefinition()?.rarity==Rarity.Basic).ToArray())run.UpgradeCard(basicToUpgrade);break;
                    case EventEffectKind.RewardCards:
                        foreach(var id in run.pendingEventSelectionIds.Where(id=>GameContent.Find(id)!=null))run.AddCard(id,effect.upgraded);break;
                    case EventEffectKind.GrantRandomCard:
                        var grantedCard=PickRandomCard(run,effect,"grant-card");if(grantedCard!=null)run.AddCard(grantedCard.id,effect.upgraded);break;
                    case EventEffectKind.GrantRelic:
                        var relic=PickRelic(run,effect);if(relic!=null)run.AcquireRelic(relic.id);break;
                    case EventEffectKind.RemoveRelic:
                        var relicId=run.pendingEventSelectionIds.FirstOrDefault(id=>run.relics.Contains(id));if(!string.IsNullOrEmpty(relicId))run.relics.Remove(relicId);break;
                    case EventEffectKind.RewardShards:
                    case EventEffectKind.GrantRandomShards:ApplyShardGrants(run);break;
                    case EventEffectKind.BindSelected:
                        if(selectedCards.Count>0)run.ApplySpecialModification(selectedCards[0],SpecialModificationKind.Binding,run.pendingEventBindingId);break;
                    case EventEffectKind.DuplicateWithBinding:
                        if(selectedCards.Count>0){var copy=run.DuplicateCard(selectedCards[0]);if(copy!=null){copy.specialModification="";copy.specialModificationKind=SpecialModificationKind.None;copy.permanentDamageBonus=copy.permanentBlockBonus=0;copy.firstDrawFree=false;run.ApplySpecialModification(copy,SpecialModificationKind.Binding,run.pendingEventBindingId);}}break;
                    case EventEffectKind.TemporaryStartBlock:run.temporaryEventEffects.Add(new TemporaryEventEffect{id="start_block",value=effect.amount,combatsRemaining=effect.duration});break;
                    case EventEffectKind.TemporaryFirstDrawFree:
                        if(selectedCards.Count>0)run.temporaryEventEffects.Add(new TemporaryEventEffect{id="first_draw_free",cardPersistentId=selectedCards[0].persistentId,combatsRemaining=effect.duration});break;
                    case EventEffectKind.RepairShard:ApplyShardOperation(run,effect,"repair");break;
                    case EventEffectKind.TradeShard:ApplyShardOperation(run,effect,"trade");break;
                    case EventEffectKind.FractureShard:ApplyShardOperation(run,effect,"fracture");break;
                    case EventEffectKind.RevealMap:RevealMap(run,effect.count);break;
                    case EventEffectKind.Fatewheel:ApplyFatewheel(run);break;
                }
            }
            run.SyncLegacyDeck();if(string.IsNullOrWhiteSpace(run.pendingEventResult))run.pendingEventResult=(string.IsNullOrWhiteSpace(choice.costText)?"":choice.costText+"  →  ")+choice.rewardText;run.stage=RunStage.EventResult;run.eventSelectionKind=EventSelectionKind.None;
        }

        private static void ApplyShardGrants(RunModel run)
        {
            if(run.pendingEventShardDecisions.Count>0)
            {
                foreach(var decision in run.pendingEventShardDecisions){var parts=decision.Split(new[]{"=>"},StringSplitOptions.None);if(parts.Length!=2||parts[1]=="discard")continue;var old=run.shards.FirstOrDefault(s=>s.id==parts[1]);if(old!=null)run.shards.Remove(old);if(run.shards.Count<RunModel.ShardCapacity)run.AddShard(parts[0]);}
                return;
            }
            var ids=run.pendingEventSelectionIds.Where(id=>WorldContent.FateShards.Any(s=>s.id==id)).ToList();if(ids.Count==0)ids=run.pendingEventOfferIds.Where(id=>WorldContent.FateShards.Any(s=>s.id==id)).ToList();foreach(var id in ids)if(run.shards.Count<RunModel.ShardCapacity)run.AddShard(id);
        }

        private static void ApplyShardOperation(RunModel run,EventEffectDef effect,string operation)
        {
            var id=run.pendingEventSelectionIds.FirstOrDefault();var shard=run.shards.FirstOrDefault(s=>s.id==id);if(shard==null)return;
            if(effect.id=="remove"){run.shards.Remove(shard);return;}
            if(operation=="repair"){shard.uses=Math.Max(0,shard.uses-1);return;}
            if(operation=="fracture"){shard.uses=2;shard.active=false;shard.activeFractured=false;return;}
            var alternatives=WorldContent.FateShards.Where(s=>s.id!=shard.id).ToList();if(alternatives.Count==0)return;var replacement=alternatives[PositiveHash(run.seed,run.floor,id.GetHashCode(),2081)%alternatives.Count];shard.id=replacement.id;shard.uses=0;shard.active=false;shard.activeFractured=false;
        }

        private static void ApplyFatewheel(RunModel run)
        {
            // Configurable weights live here as event-system data, not in UI code.
            var outcome=FatewheelOutcome(run);
            switch(outcome)
            {
                case 0:UpgradeRandom(run,2);run.pendingEventResult="FATEWHEEL · 2 RANDOM CARDS UPGRADED";break;
                case 1:if(run.pendingEventOfferIds.Count>0)ApplyShardGrants(run);else{var shard=PickShardIds(run,1,"wheel").FirstOrDefault();if(shard!=null&&run.shards.Count<RunModel.ShardCapacity)run.AddShard(shard);}run.pendingEventResult="FATEWHEEL · FATE SHARD";break;
                case 2:run.gold+=80;run.pendingEventResult="FATEWHEEL · GAIN 80 GOLD";break;
                case 3:var relic=PickRelic(run,new EventEffectDef{kind=EventEffectKind.GrantRelic,rarity=Rarity.Common});if(relic!=null)run.AcquireRelic(relic.id);run.pendingEventResult="FATEWHEEL · COMMON RELIC";break;
                case 4:run.AddCard(PickCurse(run,"wheel"));run.pendingEventResult="FATEWHEEL · A CURSE ENTERS THE DECK";break;
                default:run.pendingEventResult="FATEWHEEL · THE WHEEL STOPS BETWEEN FATES";break;
            }
        }
        private static int FatewheelOutcome(RunModel run)
        {
            var weights=EventContent.FatewheelWeights;var roll=PositiveHash(run.seed,run.act,run.floor,4049)%weights.Sum();for(var outcome=0;outcome<weights.Length;outcome++){roll-=weights[outcome];if(roll<0)return outcome;}return weights.Length-1;
        }

        private static void Transform(RunModel run,RunCard card)
        {
            var definition=card.BuildDefinition();if(definition==null)return;var own=OwnOrigin(run);var pool=GameContent.Cards.Where(c=>c.origin==own&&c.rarity==definition.rarity&&c.id!=card.cardId).ToList();if(pool.Count==0)return;
            card.cardId=pool[PositiveHash(run.seed,run.floor,card.persistentId.GetHashCode(),1231)%pool.Count].id;card.upgraded=false;card.specialModification="";card.specialModificationKind=SpecialModificationKind.None;card.permanentDamageBonus=card.permanentBlockBonus=0;card.firstDrawFree=false;
        }

        private static void UpgradeRandom(RunModel run,int count)
        {
            var pool=run.cards.Where(CanUpgrade).ToList();for(var i=0;i<count&&pool.Count>0;i++){var index=PositiveHash(run.seed,run.floor,i,1879)%pool.Count;run.UpgradeCard(pool[index]);pool.RemoveAt(index);}
        }

        private static void RevealMap(RunModel run,int count)
        {
            foreach(var node in run.nodes.Where(n=>!n.complete&&n.floor>=run.floor).OrderBy(n=>n.floor).ThenBy(n=>Math.Abs(n.lane-RunModel.LaneCount/2)).Take(Math.Max(1,count)))node.revealed=true;
        }

        private static bool EventViable(RunModel run,EventDefinition definition)=>definition.choices.Any(c=>Availability(run,c).available);
        private static EventChoiceDef PendingChoice(RunModel run)=>EventContent.FindChoice(run?.activeEventId,run?.pendingEventChoiceId);
        private static bool NeedsSelection(EventEffectDef effect)=>effect!=null&&effect.kind is EventEffectKind.UpgradeSelected or EventEffectKind.RemoveSelected or EventEffectKind.TransformSelected or EventEffectKind.RemoveCurse or EventEffectKind.RewardCards or EventEffectKind.RewardShards or EventEffectKind.RemoveRelic or EventEffectKind.BindSelected or EventEffectKind.DuplicateWithBinding or EventEffectKind.TemporaryFirstDrawFree or EventEffectKind.RepairShard or EventEffectKind.TradeShard or EventEffectKind.FractureShard;

        private static IEnumerable<RunCard> EligibleCards(RunModel run,EventEffectDef effect)
        {
            if(run==null||effect==null)return Enumerable.Empty<RunCard>();
            return run.cards.Where(card=>
            {
                var d=card.BuildDefinition();if(d==null)return false;
                if(effect.kind==EventEffectKind.UpgradeSelected&&!CanUpgrade(card))return false;
                if(effect.kind==EventEffectKind.RemoveCurse&&d.origin!=CardOrigin.Curse)return false;
                if(effect.kind is EventEffectKind.BindSelected or EventEffectKind.DuplicateWithBinding)
                {
                    if(card.specialModificationKind!=SpecialModificationKind.None)return false;
                    if(!CompatibleBindings(run,card,effect.id).Any())return false;
                }
                return effect.filter switch
                {
                    EventCardFilter.Any=>d.rarity is not (Rarity.Status),
                    EventCardFilter.Attack=>d.kind==CardKind.Attack,
                    EventCardFilter.Skill=>d.kind==CardKind.Skill,
                    EventCardFilter.DefensiveSkill=>d.effect==EffectKind.Block,
                    EventCardFilter.Power=>d.kind==CardKind.Power,
                    EventCardFilter.CostOnePlus=>d.cost>=1,
                    EventCardFilter.CostTwoPlusAttack=>d.cost>=2&&d.kind==CardKind.Attack,
                    EventCardFilter.CostTwoPlus=>d.cost>=2,
                    EventCardFilter.Exhaust=>d.exhaust,
                    EventCardFilter.Upgraded=>card.upgraded,
                    EventCardFilter.Basic=>d.rarity==Rarity.Basic,
                    EventCardFilter.BasicAttack=>d.rarity==Rarity.Basic&&d.kind==CardKind.Attack,
                    EventCardFilter.Curse=>d.origin==CardOrigin.Curse,
                    EventCardFilter.BindingEligible=>true,
                    _=>true
                };
            });
        }

        private static IEnumerable<FateShardState> EligibleShards(RunModel run,EventEffectDef effect)
        {
            if(run?.shards==null)return Enumerable.Empty<FateShardState>();var shards=run.shards.AsEnumerable();if(effect?.kind==EventEffectKind.FractureShard)shards=shards.Where(s=>s.uses<2&&!s.active);if(effect?.kind==EventEffectKind.RepairShard&&effect.id!="remove")shards=shards.Where(s=>s.uses>0&&!s.active);return shards;
        }
        private static IEnumerable<RelicDef> RemovableRelics(RunModel run)=>GameContent.Relics.Where(r=>r.rarity==Rarity.Common&&r.id is not ("gilded_buckle" or "cracked_prism")&&run.relics.Contains(r.id));
        private static IEnumerable<RelicDef> RelicPool(RunModel run,EventEffectDef effect)
        {
            var pool=GameContent.Relics.Where(r=>!run.relics.Contains(r.id)&&r.rarity is not (Rarity.Boss or Rarity.Special));
            if(effect.id=="common_uncommon")return pool.Where(r=>r.rarity is Rarity.Common or Rarity.Uncommon);
            return pool.Where(r=>r.rarity==effect.rarity);
        }
        private static RelicDef PickRelic(RunModel run,EventEffectDef effect)
        {
            var pool=RelicPool(run,effect).ToList();if(pool.Count==0)return null;
            if(effect.id=="common_uncommon")
            {
                var weighted=pool.SelectMany(r=>Enumerable.Repeat(r,r.rarity==Rarity.Common?3:2)).ToList();return weighted[PositiveHash(run.seed,run.floor,effect.kind.GetHashCode(),313)%weighted.Count];
            }
            return pool[PositiveHash(run.seed,run.act,run.floor,effect.rarity.GetHashCode())%pool.Count];
        }

        private static IEnumerable<CardDef> CardPool(RunModel run,EventEffectDef effect)
        {
            var normal=GameContent.Cards.Where(c=>c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare);var own=OwnOrigin(run);IEnumerable<CardDef> pool=effect.source switch
            {
                EventCardSource.Own=>normal.Where(c=>c.origin==own),
                EventCardSource.Foreign=>normal.Where(c=>PlayableOrigins().Contains(c.origin)&&c.origin!=own),
                EventCardSource.Wanderer=>normal.Where(c=>c.origin==CardOrigin.Wanderer),
                _=>normal.Where(c=>c.origin==own||c.origin==CardOrigin.Wanderer)
            };
            if(effect.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare)pool=pool.Where(c=>c.rarity==effect.rarity);
            if(effect.filter==EventCardFilter.DefensiveSkill)pool=pool.Where(c=>c.kind==CardKind.Skill&&c.effect==EffectKind.Block);
            else if(effect.filter==EventCardFilter.Skill)pool=pool.Where(c=>c.kind is CardKind.Skill or CardKind.Power);
            else if(!(effect.filter==EventCardFilter.Any&&effect.cardKind==CardKind.Skill))pool=pool.Where(c=>c.kind==effect.cardKind);
            return pool;
        }
        private static IEnumerable<CardDef> BuildCardOffers(RunModel run,EventEffectDef effect)
        {
            if(run.activeEventId=="strangers_deck")
            {
                var own=OwnOrigin(run);var foreign=GameContent.Cards.Where(c=>PlayableOrigins().Contains(c.origin)&&c.origin!=own&&c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare).ToList();var wanderers=GameContent.Cards.Where(c=>c.origin==CardOrigin.Wanderer&&c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare).ToList();var special=new List<CardDef>();
                for(var i=0;i<2&&foreign.Count>0;i++){var index=PositiveHash(run.seed,run.floor,i,631)%foreign.Count;special.Add(foreign[index]);foreign.RemoveAt(index);}if(wanderers.Count>0)special.Add(wanderers[PositiveHash(run.seed,run.act,run.floor,997)%wanderers.Count]);return special;
            }
            var pool=CardPool(run,effect).ToList();var result=new List<CardDef>();var seed=PositiveHash(run.seed,run.act,run.floor,4513);
            while(result.Count<Math.Max(1,effect.count)&&pool.Count>0){var index=PositiveHash(seed,result.Count,pool.Count,17)%pool.Count;result.Add(pool[index]);pool.RemoveAt(index);}
            if(effect.source==EventCardSource.AnyPlayable&&result.Count>0&&PositiveHash(run.seed,run.act,run.floor,1559)%100<EventContent.CrossroadsWandererChancePercent)
            {
                var wanderers=GameContent.Cards.Where(c=>c.origin==CardOrigin.Wanderer&&c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare&&!result.Any(r=>r.id==c.id)).ToList();if(wanderers.Count>0)result[result.Count-1]=wanderers[PositiveHash(run.seed,run.floor,result.Count,2017)%wanderers.Count];
            }
            return result;
        }
        private static CardDef PickRandomCard(RunModel run,EventEffectDef effect,string salt)=>BuildCardOffers(run,new EventEffectDef{kind=EventEffectKind.RewardCards,source=effect.source,count=1,rarity=effect.rarity,cardKind=effect.cardKind,filter=effect.filter,upgraded=effect.upgraded}).FirstOrDefault();

        private static List<string> PickShardIds(RunModel run,int count,string salt)
        {
            var pool=WorldContent.FateShards.Select(s=>s.id).Distinct().ToList();var result=new List<string>();var seed=PositiveHash(run.seed,run.act,run.floor,salt.GetHashCode());
            while(result.Count<count&&pool.Count>0){var index=PositiveHash(seed,result.Count,pool.Count,97)%pool.Count;result.Add(pool[index]);pool.RemoveAt(index);}return result;
        }
        private static string PickCurse(RunModel run,string salt)
        {
            var pool=GameContent.Cards.Where(c=>c.origin==CardOrigin.Curse).Where(c=>c.id!="doom").SelectMany(c=>Enumerable.Repeat(c.id,4)).Concat(new[]{"doom"}).ToList();return pool[PositiveHash(run.seed,run.act,run.floor,salt.GetHashCode())%pool.Count];
        }
        private static List<string> Pick(IEnumerable<string> source,int count,int seed)
        {var pool=source.Distinct().ToList();var result=new List<string>();while(result.Count<count&&pool.Count>0){var index=PositiveHash(seed,result.Count,pool.Count,71)%pool.Count;result.Add(pool[index]);pool.RemoveAt(index);}return result;}

        private static int EffectiveGoldAmount(RunModel run,EventEffectDef effect)
        {
            if(run?.activeEventId=="binder"&&effect.kind==EventEffectKind.Gold&&effect.amount<0)return -EventContent.BindingCommissionCosts[Math.Max(0,Math.Min(2,run.act-1))];return effect.amount;
        }
        private static bool CanUpgrade(RunCard card)=>card!=null&&!card.upgraded&&card.BuildDefinition()?.rarity is not (Rarity.Curse or Rarity.Status);
        private static string CardFailureReason(EventEffectDef effect)=>effect.kind switch{EventEffectKind.RemoveCurse=>"No eligible Curse to remove",EventEffectKind.BindSelected or EventEffectKind.DuplicateWithBinding=>"No card can receive this Binding",EventEffectKind.UpgradeSelected=>"No eligible card can be upgraded",_=>"No eligible card available"};
        private static CardOrigin OwnOrigin(RunModel run)=>GameContent.Cards.FirstOrDefault(c=>c.hero==run.hero)?.origin??CardOrigin.Knight;
        private static HashSet<CardOrigin> PlayableOrigins()=>GameContent.Cards.Where(c=>c.hero.HasValue).Select(c=>c.origin).ToHashSet();
        private static int PositiveHash(int a,int b,int c,int d){unchecked{var hash=17;hash=hash*31+a;hash=hash*31+b;hash=hash*31+c;hash=hash*31+d;return hash==int.MinValue?0:Math.Abs(hash);}}

        private static void ClearPending(RunModel run,bool keepResult)
        {
            run.EnsureEventState();run.pendingEventChoiceId="";run.pendingEventBindingId="";if(!keepResult)run.pendingEventResult="";run.pendingEventChoicesNeeded=0;run.eventSelectionKind=EventSelectionKind.None;run.pendingEventOfferIds.Clear();run.pendingEventSelectionIds.Clear();run.pendingEventShardDecisions.Clear();
        }
    }

    public static class EventRunStateExtensions
    {
        public static void EnsureEventState(this RunModel run)
        {
            run.seenEventIds??=new List<string>();run.pendingEventOfferIds??=new List<string>();run.pendingEventSelectionIds??=new List<string>();run.pendingEventShardDecisions??=new List<string>();run.temporaryEventEffects??=new List<TemporaryEventEffect>();run.pendingEventChoiceId??="";run.pendingEventBindingId??="";run.pendingEventResult??="";
        }
    }
}
