using System;

namespace GildedFate.Combat
{
    // Shared UI/controller geometry. Expansion changes the span, not icon readability.
    public static class SigilLayout
    {
        public const float Gap=8,MinimumSize=44,MaximumSize=54;
        public static float Size(int capacity,float availableWidth)=>
            Math.Max(MinimumSize,Math.Min(MaximumSize,(availableWidth-Gap*(Math.Max(1,capacity)-1))/Math.Max(1,capacity)));
        public static float Center(int index,float visibleSlots,float size,float anchorX)=>
            anchorX+(index-(Math.Max(1,visibleSlots)-1)*.5f)*(size+Gap);
        public static float Span(float slots,float size)=>Math.Max(1,slots)*size+Math.Max(0,slots-1)*Gap;
    }
}
