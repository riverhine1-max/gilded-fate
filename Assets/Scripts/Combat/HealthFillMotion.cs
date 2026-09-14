using System;

namespace GildedFate.Combat
{
    // Visual-only interpolation. Damage is immediate; healing settles smoothly.
    public static class HealthFillMotion
    {
        public static float Step(float current,int target,float deltaSeconds,bool reducedMotion)
        {
            if(reducedMotion||target<=current||target-current<.06f)return target;
            return current+(target-current)*(1-(float)Math.Exp(-12*Math.Max(0,deltaSeconds)));
        }
    }
}
