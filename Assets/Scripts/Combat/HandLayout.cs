using System;

namespace GildedFate.Combat
{
    // Shared by rendering, pointer picking and the automated interaction checks.
    public readonly struct HandSlot
    {
        public readonly float x,y,angle;
        public readonly int order;
        public HandSlot(float x,float y,float angle,int order){this.x=x;this.y=y;this.angle=angle;this.order=order;}
    }
    public static class HandLayout
    {
        public const float CardWidth=194,CardHeight=264;
        public static HandSlot Slot(int index,int count,float width,float height)
        {
            count=Math.Max(1,count);
            // Rest the fan slightly below the viewport. Hover/drag raises a card far
            // enough to reveal it in full, matching the deliberate tabletop feel.
            var span=Math.Min((count-1)*165f,Math.Min(980f,Math.Max(0,width-430)));
            var t=count==1?0:(index/(float)(count-1)*2-1);
            var fan=Math.Min(10,Math.Max(0,(count-1)*1.65f));
            return new HandSlot(width*.5f+t*span*.5f,height-139+t*t*6,t*fan,index);
        }
        public static bool Contains(float px,float py,float cx,float cy,float angle,float scale=1)
        {
            var a=-angle*Math.PI/180;var dx=px-cx;var dy=py-cy;
            var x=dx*Math.Cos(a)-dy*Math.Sin(a);var y=dx*Math.Sin(a)+dy*Math.Cos(a);
            return Math.Abs(x)<=CardWidth*scale*.5&&Math.Abs(y)<=CardHeight*scale*.5;
        }
    }
}
