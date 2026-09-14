using System;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;

namespace GildedFate.Map
{
    public sealed partial class RunModel
    {
        // Resume the interrupted reward/shop/event screen after all three choices.
        public bool perfectedSelectionPending;
        public int perfectedSelectionStep;
        public CardKind PerfectedChoiceKind=>(CardKind)Math.Min(2,perfectedSelectionStep);
        public RunCard[] PerfectedEligible()=>cards.Where(c=>!c.perfected&&c.BuildDefinition()?.kind==PerfectedChoiceKind).ToArray();
        public bool ChoosePerfected(RunCard card)
        {
            if(!perfectedSelectionPending||card==null||!PerfectedEligible().Contains(card))return false;
            card.perfected=true;perfectedSelectionStep++;NormalizePerfectedSelection();return true;
        }
        public void NormalizePerfectedSelection()
        {
            // No card of a type must never lock an otherwise universal boss reward.
            if(!perfectedSelectionPending)return;
            while(perfectedSelectionStep<3&&PerfectedEligible().Length==0)perfectedSelectionStep++;
            if(perfectedSelectionStep>=3)perfectedSelectionPending=false;
        }
        public void SyncPerfectedGrowth(CombatState combat)
        {
            if(combat==null)return;
            foreach(var played in combat.AllOwnedCards().Where(c=>c.perfected&&!c.temporary&&combat.memory.remaining.startingIds.Contains(c.instanceId)&&!string.IsNullOrEmpty(c.persistentId)))
            {
                var owned=cards.FirstOrDefault(c=>c.persistentId==played.persistentId);if(owned==null||!owned.perfected)continue;
                // Idempotent checkpoint synchronization, not an increment on save/load.
                owned.perfectedGrowth=Math.Max(owned.perfectedGrowth,played.perfectedGrowth);
                owned.perfectedCostReduction=Math.Max(owned.perfectedCostReduction,played.perfectedCostReduction);
            }
        }
    }
}
