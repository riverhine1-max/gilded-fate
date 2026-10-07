using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    /// <summary>
    /// Immutable UI receipt produced by a dry run of the authoritative combat resolver.
    /// The HUD never maintains a second damage, Block, or Energy formula.
    /// </summary>
    [Serializable]
    public sealed class CombatCardPreview
    {
        public int cost,authoredCost;
        public int damagePerHit,totalDamage,hits,blockedByTarget,hpDamage,triggeredDamage;
        public int block,cardBlock,triggeredBlock,authoredDamage,authoredBlock;
        public int[] hitDamage=Array.Empty<int>();
        public string DamageExpression=>hitDamage.Length>1&&hitDamage.Distinct().Count()>1
            ?string.Join(" + ",hitDamage):hits>1?$"{damagePerHit} × {hits}":totalDamage.ToString();
        public bool playable,conditionActive,awaitingChoice;
        public string conditionLabel="",breakdown="";
        public bool CostImproved=>cost<authoredCost;
        public bool CostReduced=>cost>authoredCost;
        public bool DamageImproved=>damagePerHit>authoredDamage;
        public bool DamageReduced=>damagePerHit<authoredDamage;
        public bool BlockImproved=>cardBlock>authoredBlock;
        public bool BlockReduced=>cardBlock<authoredBlock;
    }

    public sealed partial class CombatState
    {
        public int KnownIncomingDamage=>IntentDealsDamage?IntentDisplayValue*Math.Max(1,intentHits):0;
        public int IncomingBlockConsumed=>Math.Min(player.block,KnownIncomingDamage);
        public int IncomingHpThreat=>Math.Max(0,KnownIncomingDamage-player.block);

        public CombatCardPreview PreviewCard(CardDef source)
        {
            var preview=new CombatCardPreview
            {
                authoredCost=source==null?0:Math.Max(0,source.cost-source.perfectedCostReduction),
                authoredDamage=source?.id=="eye_for_an_eye"?enemy.attemptedAttackDamage:source!=null&&(source.kind==CardKind.Attack||IsSoul(source))?Math.Max(0,source.value+source.permanentDamageBonus+(source.perfected&&source.kind==CardKind.Attack?source.perfectedGrowth:0)):0,
                authoredBlock=source?.effect==EffectKind.Block?Math.Max(0,source.value+source.permanentBlockBonus+(source.perfected?source.perfectedGrowth:0)):0
            };
            if(source==null)return preview;
            if(source.effect!=EffectKind.Block&&source.kind!=CardKind.Power)
            {
                var match=System.Text.RegularExpressions.Regex.Match(source.text,@"\bGain (\d+) Block\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if(match.Success)preview.authoredBlock=int.Parse(match.Groups[1].Value);
            }
            preview.cost=CostFor(source);preview.playable=CanPlay(source);
            preview.conditionActive=CardConditionActive(source,out var condition);preview.conditionLabel=condition;

            var clean=CopyForCheckpoint();
            var cleanCard=clean.hand.FirstOrDefault(c=>c.instanceId==source.instanceId);
            if(cleanCard==null){preview.breakdown="This card is not currently in hand.";return preview;}
            // Preserve target state and conditional HP thresholds, but give the dry run
            // enough health to observe every hit without ending early.
            var targetRatio=enemy.maxHp>0?enemy.hp/(float)enemy.maxHp:1f;
            clean.enemy.maxHp=1000000;clean.enemy.hp=Math.Max(1,(int)(clean.enemy.maxHp*targetRatio));
            var beforeCleanHp=clean.enemy.hp;
            // Readability must not turn a valid effect into zero just because the
            // real hand cannot currently afford it. Only the isolated forecast
            // receives enough Energy; the real playable flag remains unchanged.
            clean.energy=Math.Max(clean.energy,clean.CostFor(cleanCard));
            clean.Play(cleanCard);
            var cleanFacts=clean.TakeEvents();
            var damageFacts=cleanFacts.Where(e=>!e.playerSide&&e.enemyIndex==EnemyContextIndex&&(e.kind==CombatEventKind.Damage||e.kind==CombatEventKind.Block&&e.label=="BLOCKED")).ToArray();
            if(source.kind==CardKind.Attack||IsSoul(source)){
                preview.triggeredDamage=damageFacts.Where(e=>e.card?.instanceId!=source.instanceId).Sum(e=>e.amount);
                damageFacts=damageFacts.Where(e=>e.card?.instanceId==source.instanceId).ToArray();
            }
            var damageHits=damageFacts.Select((fact,index)=>new{fact,key=fact.hitId>0?fact.hitId:-index-1}).GroupBy(item=>item.key).Select(group=>group.Sum(item=>item.fact.amount)).ToArray();
            preview.totalDamage=damageHits.Sum();
            preview.hitDamage=damageHits;
            preview.hits=Math.Max(1,damageHits.Length>0?damageHits.Length:EffectivePreviewHits(source));
            preview.damagePerHit=damageHits.Length>0?damageHits[0]:0;
            var blockFacts=cleanFacts.Where(e=>e.kind==CombatEventKind.Block&&e.playerSide&&e.label!="BLOCKED").ToArray();
            preview.block=blockFacts.Sum(e=>Math.Max(0,e.amount));
            preview.cardBlock=blockFacts.Where(e=>e.card?.instanceId==source.instanceId).Sum(e=>Math.Max(0,e.amount));
            preview.triggeredBlock=preview.block-preview.cardBlock;
            preview.awaitingChoice=clean.AwaitingChoice;

            var defended=CopyForCheckpoint();
            var defendedCard=defended.hand.FirstOrDefault(c=>c.instanceId==source.instanceId);
            if(defendedCard!=null)
            {
                var defendedRatio=enemy.maxHp>0?enemy.hp/(float)enemy.maxHp:1f;
                defended.enemy.maxHp=1000000;defended.enemy.hp=Math.Max(1,(int)(defended.enemy.maxHp*defendedRatio));
                var beforeHp=defended.enemy.hp;var beforeBlock=defended.enemy.block;
                defended.energy=Math.Max(defended.energy,defended.CostFor(defendedCard));
                defended.Play(defendedCard);
                preview.hpDamage=Math.Max(0,beforeHp-defended.enemy.hp);
                preview.blockedByTarget=Math.Max(0,beforeBlock-defended.enemy.block);
            }

            preview.breakdown=BuildPreviewBreakdown(source,preview);
            return preview;
        }

        private int EffectivePreviewHits(CardDef card)
        {
            var hits=Math.Max(1,card.hits);
            if(card.id=="cremation"&&enemy.burn>=10)hits=2;
            if(card.id=="no_mercy"&&memory.retaliateTriggeredTurn>0)hits=2;
            return hits;
        }

        private bool CardConditionActive(CardDef card,out string label)
        {
            label="";
            if(card==null)return false;
            if(card.keywords?.Contains("Heavy")==true){label="HEAVY";return memory.attacksThisTurn==0;}
            if(card.keywords?.Contains("Revenge")==true){label="REVENGE";return memory.wasAttackedLastTurn;}
            if(card.text.IndexOf("Marked",StringComparison.OrdinalIgnoreCase)>=0){label="TARGET MARKED";return enemy.marked>0;}
            if(card.text.IndexOf("below",StringComparison.OrdinalIgnoreCase)>=0){label="TARGET BELOW 50%";return enemy.hp*2<enemy.maxHp;}
            if(card.text.IndexOf("played a Skill",StringComparison.OrdinalIgnoreCase)>=0){label="SKILL PLAYED";return memory.skillsThisTurn>0;}
            if(card.text.IndexOf("Retaliate triggered",StringComparison.OrdinalIgnoreCase)>=0){label="RETALIATE TRIGGERED";return memory.retaliateTriggeredTurn>0;}
            if(card.text.IndexOf("have Strength",StringComparison.OrdinalIgnoreCase)>=0){label="STRENGTH HELD";return player.strength>0;}
            if(card.text.IndexOf("have Fortify",StringComparison.OrdinalIgnoreCase)>=0){label="FORTIFY HELD";return player.fortify>0;}
            return false;
        }

        // Compact on purpose: one formula line, then only the facts that change the outcome. The tooltip it feeds
        // is read mid-turn, so it must scan in a glance rather than explain every modifier in a sentence.
        private string BuildPreviewBreakdown(CardDef card,CombatCardPreview preview)
        {
            var lines=new List<string>();
            var hasBonuses=relics.Count>0||!string.IsNullOrEmpty(activeShardId)||card.IsModified;
            if(card.id=="eye_for_an_eye")
            {
                lines.Add($"{enemy.attemptedAttackDamage} attempted = {preview.totalDamage} damage");
                if(preview.blockedByTarget>0)lines.Add($"{preview.blockedByTarget} absorbed · {preview.hpDamage} HP");
            }
            else if(card.kind==CardKind.Attack)
            {
                var formula=new List<string>{$"{preview.authoredDamage} base"};
                if(card.perfected)formula.Add("+1 Perfected");
                if(player.strength!=0)formula.Add($"{(player.strength>0?"+":"")}{player.strength} Strength");
                if(hasBonuses)formula.Add("+ bonuses");
                if(player.weak>0)formula.Add("×0.75 Weak");
                if(enemy.vulnerable>0)formula.Add("×1.5 Vulnerable");
                lines.Add(string.Join(" ",formula)+(preview.hits>1?$" = {preview.DamageExpression} ({preview.totalDamage})":$" = {preview.totalDamage} damage"));
                if(preview.blockedByTarget>0)lines.Add($"{preview.blockedByTarget} absorbed · {preview.hpDamage} HP");
                if(!string.IsNullOrEmpty(preview.conditionLabel))lines.Add(preview.conditionLabel+(preview.conditionActive?" · active":" · inactive"));
            }
            else if(preview.block>0||card.effect==EffectKind.Block)
            {
                var formula=new List<string>{$"{preview.authoredBlock} base"};
                if(card.perfected)formula.Add("+1 Perfected");
                if(player.fortify>0)formula.Add($"+{player.fortify} Fortify");
                if(hasBonuses)formula.Add("+ bonuses");
                if(player.frail>0)formula.Add("after Frail");
                lines.Add(string.Join(" ",formula)+$" = {preview.cardBlock} Block");
            }
            if(card.kind==CardKind.Attack&&preview.cardBlock>0)lines.Add($"Also gains {preview.cardBlock} Block");
            if(preview.triggeredBlock>0)lines.Add($"+{preview.triggeredBlock} triggered Block");
            if(preview.triggeredDamage>0)lines.Add($"+{preview.triggeredDamage} triggered damage");
            if(preview.cost!=preview.authoredCost)lines.Add($"Cost {preview.authoredCost} → {preview.cost}");
            if(preview.awaitingChoice)lines.Add("Result depends on your choice.");
            return string.Join("\n",lines);
        }
    }
}
