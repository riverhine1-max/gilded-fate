using System;

namespace GildedFate.Combat
{
    public static class CombatHitTiming
    {
        public const float DefaultHitGap=.18f;
        public static float CardStagger(int receipts,float normal=.05f)=>Math.Min(normal,1.2f/Math.Max(1,receipts));
        public static bool IsRelicWrapper(CombatEvent fact)=>fact.kind==CombatEventKind.Status&&fact.label.StartsWith("TRIGGER:RELIC ",StringComparison.Ordinal);
        public static bool IsHealthFact(CombatEvent fact)=>fact.kind is CombatEventKind.Damage or CombatEventKind.Block or CombatEventKind.Heal;
        // Preserve ordinary hits exactly. Long engines retain their opening beats,
        // then overlap/accelerate presentation only; every receipt stays ordered.
        public static float[] PresentationOffsets(CombatEvent[] facts,float gap=DefaultHitGap)
        {
            var offsets=Offsets(facts,gap);
            if(offsets.Length==0||offsets[offsets.Length-1]<=6f)return offsets;
            var scale=4f/(offsets[offsets.Length-1]-2f);
            for(var i=0;i<offsets.Length;i++)if(offsets[i]>2f)offsets[i]=2f+(offsets[i]-2f)*scale;
            return offsets;
        }
        public static float[] Offsets(CombatEvent[] facts,float gap=DefaultHitGap)
        {
            var offsets=new float[facts.Length];var time=0f;gap=Math.Max(.06f,gap);
            for(var i=0;i<facts.Length;i++)
            {
                offsets[i]=time;var fact=facts[i];
                if(fact.kind is CombatEventKind.ShardTrigger or CombatEventKind.RelicTrigger)time+=.20f;
                else if(fact.kind==CombatEventKind.Status&&!IsRelicWrapper(fact)&&fact.label.StartsWith("TRIGGER:",StringComparison.Ordinal))time+=.20f;
                else if(fact.kind==CombatEventKind.EnemyAction)time+=.20f;
                else if(fact.kind is CombatEventKind.Damage or CombatEventKind.Heal)time+=gap;
                else if(fact.kind==CombatEventKind.Block&&fact.label=="BLOCKED")
                {
                    // Block absorption and HP loss from the same hit are one beat.
                    var shared=fact.hitId>0&&i+1<facts.Length&&facts[i+1].hitId==fact.hitId&&facts[i+1].kind==CombatEventKind.Damage&&facts[i+1].playerSide==fact.playerSide;
                    if(!shared)time+=gap;
                }
            }
            return offsets;
        }
    }
}
