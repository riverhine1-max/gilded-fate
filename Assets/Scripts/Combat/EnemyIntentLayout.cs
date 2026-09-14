using System;

namespace GildedFate.Combat
{
    public static class EnemyIntentLayout
    {
        public static int Columns(int count,float width,float iconSize)=>Math.Max(1,Math.Min(count,(int)Math.Floor((width+8)/(iconSize+18))));
        public static float CellWidth(float iconSize)=>iconSize+10;
        public static float RowHeight(float iconSize,bool destination)=>iconSize+22+(destination?13:0);
        public static float CellX(float center,int index,int count,int columns,float iconSize)
        {
            var row=index/columns;var inRow=Math.Min(columns,count-row*columns);
            return center-(inRow*(iconSize+10)+(inRow-1)*8)*.5f+index%columns*(iconSize+18);
        }
    }
}

