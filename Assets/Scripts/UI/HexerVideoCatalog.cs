using System;
using System.IO;
using UnityEngine;

namespace GildedFate.UI
{
    public static class HexerVideoCatalog
    {
        public sealed class Clip
        {
            public readonly int Index;
            public readonly string Name;
            public readonly float Duration;
            public Clip(int index,string name,float duration){Index=index;Name=name;Duration=duration;}
            public string Path=>System.IO.Path.Combine(Application.streamingAssetsPath,"Animations","Hexer",Index.ToString("00")+"_"+Name+".mp4");
            public string Url=>new Uri(Path).AbsoluteUri;
            // All clips share one square camera. Ground/pivot stays fixed, including defeat/revive.
            public Rect DrawRect(Rect actor){var size=actor.height/.9f;return new Rect(actor.center.x-size*.51f,actor.yMax-size*.967f,size,size);}
        }
        public static readonly Clip[] Clips={
            new Clip(1,"CombatIdle",4),
            new Clip(2,"BasicMagicalAttack",4),
            new Clip(3,"AlternateMagicalAttack",3),
            new Clip(4,"PowerfulSigilAttack",3),
            new Clip(5,"FinisherAttack",4),
            new Clip(6,"BasicDefensiveSkill",3),
            new Clip(7,"BarrierImpact",3),
            new Clip(8,"MajorDefensiveSkill",3),
            new Clip(9,"LightHitReaction",3),
            new Clip(10,"HeavyHitStagger",3),
            new Clip(11,"FirstRitual",4),
            new Clip(12,"SigilSelected",4),
            new Clip(13,"NormalSigilTrigger",3),
            new Clip(14,"OverflowTrigger",4),
            new Clip(15,"EchoTrigger",3),
            new Clip(16,"AspectActivation",3),
            new Clip(17,"BuffPositiveEffect",3),
            new Clip(18,"DrawKnowledgeResponse",3),
            new Clip(19,"Dissipate",3),
            new Clip(20,"LowHealthIdle",3),
            new Clip(21,"Defeat",4),
            new Clip(22,"ReviveRecovery",4),
        };
        public static Clip Find(string name)=>Array.Find(Clips,c=>c.Name==name);
        public static bool Available=>File.Exists(Clips[0].Path);
    }
}
