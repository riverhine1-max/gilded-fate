using System;

namespace GildedFate.Combat
{
    // Pure layout shared by every fighter; each row is centered on its own anchor.
    public static class EffectStripLayout
    {
        public static int Columns(float width,float size,float gap)=>Math.Max(1,(int)Math.Floor((width+gap)/(size+gap)));
        public static int Rows(float height,float size,float gap)=>Math.Max(1,(int)Math.Floor((height+gap)/(size+gap)));
        public static float CellX(float center,int index,int shown,int columns,float size,float gap)
        {
            var row=index/columns;var rowCount=Math.Min(columns,shown-row*columns);
            return center-(rowCount*size+(rowCount-1)*gap)*.5f+(index%columns)*(size+gap);
        }
    }
}
