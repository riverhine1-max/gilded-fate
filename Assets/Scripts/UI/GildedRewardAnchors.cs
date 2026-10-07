using System.Linq;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private static Rect RunDeckControlRect(float width)=>new Rect(width-126,7,46,44);
        // Relics live inside the top bar, between the act badge and the map button, so a long relic row can never run
        // under a page heading. Anything past the last slot collapses into a "+N" chip.
        private const float RelicRowStart=600,RelicRowRightGap=205;
        private static Rect RunRelicSlotRect(int index)=>new Rect(RelicRowStart+index*45,10,38,38);
        private int VisibleRunRelics(float width)
        {
            var capacity=Mathf.Clamp(Mathf.FloorToInt((width-RelicRowStart-RelicRowRightGap+7)/45),1,18);
            return Mathf.Min(run.relics.Count,run.relics.Count>capacity?capacity-1:capacity);
        }
        private Vector2 AcquisitionTarget(float width)
        {
            if(acquisitionKind==AcquisitionKind.Relic)
            {
                var index=acquisitionRelic==null?-1:run.relics.IndexOf(acquisitionRelic.id);
                return RunRelicSlotRect(Mathf.Clamp(index<0?run.relics.Count:index,0,VisibleRunRelics(width))).center;
            }
            if(acquisitionKind==AcquisitionKind.Shard)
            {
                run.EnsureShardSlots();
                var slot=acquisitionShard==null?-1:run.shards.LastOrDefault(state=>state.id==acquisitionShard.id)?.slot??-1;
                return ShrineSocket(Mathf.Max(0,slot)).center;
            }
            return RunDeckControlRect(width).center;
        }
    }
}
